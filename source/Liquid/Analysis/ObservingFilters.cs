using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using DotLiquid;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Analysis;

/// <summary>
/// Replacements for the DotLiquid filters that behave differently from Shopify Liquid. Each method has exactly the same
/// signature as its <see cref="StandardFilters"/> counterpart, so DotLiquid converts the arguments in the same way. It
/// records an observation when the actual values hit a deviation, and then calls the original filter, so the result is
/// unchanged. These filters are only used for the observing render; the output always comes from the normal render.
/// </summary>
internal sealed class ObservingFilters
{
    private static readonly Regex _timeZonePattern = new(@"(Z|[+-]\d{2}:?\d{2})$", RegexOptions.IgnoreCase);
    private static readonly Regex _isoDatePattern = new(@"^\d{4}-\d{2}-\d{2}");

    // How DotLiquid renders a decimal in output (Variable.Render): without trailing zeros.
    private const string RenderedDecimalFormat = "0.#############################";

    private readonly int _line;
    private readonly int _column;

    /// <summary>
    /// Creates the observing filters for one filter call in the map.
    /// </summary>
    /// <param name="line">1-based line of the filter call.</param>
    /// <param name="column">1-based column of the filter call.</param>
    public ObservingFilters(int line, int column)
    {
        _line = line;
        _column = column;
    }
    public object? Plus(object? input, object? operand)
    {
        if (input is string text)
        {
            var sum = IsNumber(operand)
                ? (decimal.Parse(ShopifyReference.ToNumberText(text), CultureInfo.InvariantCulture) + Convert.ToDecimal(operand, CultureInfo.CurrentCulture)).ToString(CultureInfo.InvariantCulture)
                : "a sum";
            Report(DiagnosticCodes.PlusJoinsText,
                $"{Show(input)} | Plus: {Show(operand)} joins text instead of adding: the result is '{string.Concat(input, operand)}', Shopify Liquid gives {sum}. " +
                "Filters such as Date, Append and Split return text. Convert to a number first, for example '| Times: 1.0 | Round | Plus: ...'.");
            ReportTrailingZerosReachText(nameof(Plus), text);
        }
        else
        {
            InspectMath(nameof(Plus), input, operand);
        }

        return RememberTrailingZeros(nameof(Plus), StandardFilters.Plus(input, operand));
    }

    public object? Minus(object? input, object? operand)
    {
        InspectMath(nameof(Minus), input, operand);
        return RememberTrailingZeros(nameof(Minus), StandardFilters.Minus(input, operand));
    }

    public object? Times(object? input, object? operand)
    {
        if (input is string text && operand is int or long)
        {
            var count = Convert.ToInt64(operand, CultureInfo.InvariantCulture);
            var repeated = count is >= 0 and <= 5 ? $"'{string.Concat(Enumerable.Repeat(text, (int)count))}'" : $"the text {count} times";
            Report(DiagnosticCodes.TimesRepeatsText,
                $"{Show(input)} | Times: {Show(operand)} repeats the text instead of multiplying (result: {repeated}), " +
                "and fails for a negative number. Shopify Liquid multiplies. Use a decimal operand, for example '| Times: 1.0', to convert text to a number.");
        }
        else
        {
            InspectMath(nameof(Times), input, operand);
        }

        return RememberTrailingZeros(nameof(Times), StandardFilters.Times(input, operand));
    }

    public object? DividedBy(object? input, object? operand)
    {
        InspectMath(nameof(DividedBy), input, operand);
        var result = StandardFilters.DividedBy(input, operand);

        if (result is double { } quotient && (double.IsInfinity(quotient) || double.IsNaN(quotient)))
        {
            Report(DiagnosticCodes.DivisionByZero,
                $"{Show(input)} | DividedBy: {Show(operand)} divides by zero and renders '{Show(result)}'. Shopify Liquid raises an error. Check for zero first.");
        }
        else if (IsWholeNumber(input) && IsWholeNumber(operand) && result is not null)
        {
            var dividend = Convert.ToInt64(input, CultureInfo.InvariantCulture);
            var divisor = Convert.ToInt64(operand, CultureInfo.InvariantCulture);
            var expected = ShopifyReference.FloorDivide(dividend, divisor);
            if (expected != Convert.ToInt64(result, CultureInfo.InvariantCulture))
            {
                Report(DiagnosticCodes.NegativeIntegerDivision,
                    $"{Show(input)} | DividedBy: {Show(operand)} gives {Show(result)}: integer division rounds toward zero. Shopify Liquid rounds down and gives {expected}.");
            }
        }

        return RememberTrailingZeros(nameof(DividedBy), result);
    }

    public object? Modulo(object? input, object? operand)
    {
        InspectMath(nameof(Modulo), input, operand);
        var result = StandardFilters.Modulo(input, operand);

        if (IsWholeNumber(input) && IsWholeNumber(operand) && result is not null)
        {
            var dividend = Convert.ToInt64(input, CultureInfo.InvariantCulture);
            var divisor = Convert.ToInt64(operand, CultureInfo.InvariantCulture);
            var expected = ShopifyReference.Modulo(dividend, divisor);
            if (expected != Convert.ToInt64(result, CultureInfo.InvariantCulture))
            {
                Report(DiagnosticCodes.NegativeModulo,
                    $"{Show(input)} | Modulo: {Show(operand)} gives {Show(result)}: the result takes the sign of the input. Shopify Liquid gives {expected}.");
            }
        }

        return RememberTrailingZeros(nameof(Modulo), result);
    }

    public object? Round(object? input, object? places = null)
    {
        if (input is string text)
        {
            ReportCultureParsing(nameof(Round), text);
        }

        var result = StandardFilters.Round(input, places);
        if (result is null)
        {
            Report(DiagnosticCodes.RoundNotANumber,
                $"{Show(input)} | Round returns nothing because the value is not a number.");
            return result;
        }

        var decimals = places is null ? 0 : Convert.ToInt32(places, CultureInfo.CurrentCulture);
        var expected = ShopifyReference.Round(Convert.ToDecimal(input, CultureInfo.CurrentCulture), decimals);
        if (expected != (decimal)result)
        {
            Report(DiagnosticCodes.BankersRounding,
                $"{Show(input)} | Round{(places is null ? string.Empty : ": " + Show(places))} gives {Show(result)}: halves are rounded to the even number (banker's rounding). Shopify Liquid rounds half away from zero and gives {expected.ToString(CultureInfo.CurrentCulture)}.");
        }

        return result;
    }

    public double Abs(object? input)
    {
        InspectNumberFilter(nameof(Abs), input, "returns 0");
        return StandardFilters.Abs(input);
    }

    public object? AtLeast(object? input, object? atLeast)
    {
        InspectNumberFilter(nameof(AtLeast), input, "returns the value unchanged");
        InspectNumberFilter(nameof(AtLeast), atLeast, "returns the value unchanged");
        return StandardFilters.AtLeast(input, atLeast);
    }

    public object? AtMost(object? input, object? atMost)
    {
        InspectNumberFilter(nameof(AtMost), input, "returns the value unchanged");
        InspectNumberFilter(nameof(AtMost), atMost, "returns the value unchanged");
        return StandardFilters.AtMost(input, atMost);
    }

    public string? Default(string? input, string? defaultValue)
    {
        if (!string.IsNullOrEmpty(input) && string.IsNullOrWhiteSpace(input))
        {
            Report(DiagnosticCodes.DefaultDiffers,
                $"Default treats the whitespace value {Show(input)} as empty and returns {Show(defaultValue)}. Shopify Liquid keeps the whitespace.");
        }
        else if (input is "False")
        {
            Report(DiagnosticCodes.DefaultDiffers,
                $"Default does not treat false as empty: it returns 'False'. Shopify Liquid returns the default {Show(defaultValue)}. Use an if instead.");
        }

        return StandardFilters.Default(input, defaultValue);
    }

    public string? Replace(string? input, string? @string, string? replacement = "")
    {
        return ObserveReplace(nameof(Replace), input, @string, replacement, () => StandardFilters.Replace(input, @string, replacement),
            () => ShopifyReference.ReplaceAll(input!, @string!, replacement ?? string.Empty));
    }

    public string? ReplaceFirst(string? input, string? @string, string? replacement = "")
    {
        return ObserveReplace(nameof(ReplaceFirst), input, @string, replacement, () => StandardFilters.ReplaceFirst(input, @string, replacement),
            () => ShopifyReference.ReplaceFirst(input!, @string!, replacement ?? string.Empty));
    }

    public string? RemoveFirst(string? input, string? @string)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return StandardFilters.RemoveFirst(input, @string);
        }

        return ObserveReplace(nameof(RemoveFirst), input, @string, string.Empty, () => StandardFilters.RemoveFirst(input, @string),
            () => ShopifyReference.ReplaceFirst(input!, @string!, string.Empty));
    }

    public string[] Split(string? input, string? pattern)
    {
        var result = StandardFilters.Split(input, pattern);
        if (!string.IsNullOrWhiteSpace(input) && pattern is not null)
        {
            var expected = ShopifyReference.SplitCount(input, pattern);
            if (expected != result.Length)
            {
                var reason = pattern.Length == 0
                    ? "an empty separator does not split the text into characters"
                    : "empty items are dropped";
                Report(DiagnosticCodes.SplitDiffers,
                    $"{Show(input)} | Split: {Show(pattern)} gives {result.Length} item(s), Shopify Liquid gives {expected}: {reason}.");
            }
        }

        return result;
    }

    public string? Append(string? input, string? @string)
    {
        ReportNilInput(nameof(Append), input, @string);
        ReportTrailingZerosReachText(nameof(Append), input);
        ReportTrailingZerosReachText(nameof(Append), @string);
        return StandardFilters.Append(input, @string);
    }

    public string? Prepend(string? input, string? @string)
    {
        ReportNilInput(nameof(Prepend), input, @string);
        ReportTrailingZerosReachText(nameof(Prepend), input);
        ReportTrailingZerosReachText(nameof(Prepend), @string);
        return StandardFilters.Prepend(input, @string);
    }

    public string? Join(IEnumerable? input, string? glue = " ")
    {
        if (input is not null)
        {
            foreach (var item in input)
            {
                if (item is decimal number && number.ToString(CultureInfo.CurrentCulture) is var asText
                    && asText != number.ToString(RenderedDecimalFormat, CultureInfo.CurrentCulture))
                {
                    ReportTrailingZeros(nameof(Join), asText, number.ToString(RenderedDecimalFormat, CultureInfo.CurrentCulture), "the input");
                    break;
                }

                ReportTrailingZerosReachText(nameof(Join), item as string);
            }
        }

        return StandardFilters.Join(input, glue);
    }

    public string? Capitalize(string? input)
    {
        var result = StandardFilters.Capitalize(input);
        if (!string.IsNullOrWhiteSpace(input) && result is not null)
        {
            var expected = ShopifyReference.Capitalize(input);
            if (!string.Equals(expected, result, StringComparison.Ordinal))
            {
                Report(DiagnosticCodes.CapitalizeDiffers,
                    $"{Show(input)} | Capitalize gives '{result}': every word gets a capital and the rest is kept. Shopify Liquid gives '{expected}'.");
            }
        }

        return result;
    }

    public string? Escape(string? input)
    {
        return ObserveEscape(nameof(Escape), input, StandardFilters.Escape(input));
    }

    public string? H(string? input)
    {
        return ObserveEscape(nameof(H), input, StandardFilters.H(input));
    }

    public IEnumerable? Sort(object? input, string? property = null)
    {
        var result = StandardFilters.Sort(input, property);
        if (input is not IEnumerable items || input is string || result is null)
        {
            return result;
        }

        var original = items.Cast<object?>().ToList();
        var sorted = result.Cast<object?>().ToList();
        if (!string.IsNullOrEmpty(property))
        {
            var indexables = original.OfType<IIndexable>().ToList();
            if (original.Count > 1 && indexables.Count == original.Count && original.Any(item => item is not IDictionary))
            {
                var expected = indexables.OrderBy(item => item.ContainsKey(property) ? item[property] : null, Comparer<object?>.Default).Cast<object?>().ToList();
                if (!expected.SequenceEqual(sorted))
                {
                    Report(DiagnosticCodes.SortDiffers,
                        $"Sort: {Show(property)} does not sort JSON or XML input: the items keep their original order. Shopify Liquid sorts them by '{property}'. Sort by building the order yourself, for example with a for loop per value.");
                }
            }
        }
        else if (sorted.Count > 1 && sorted.All(item => item is string))
        {
            var ordinal = sorted.Cast<string>().OrderBy(item => item, StringComparer.Ordinal).ToList();
            if (!ordinal.SequenceEqual(sorted.Cast<string>()))
            {
                Report(DiagnosticCodes.SortDiffers,
                    $"Sort orders text by the host culture ('{string.Join(", ", sorted.Take(5))}'). Shopify Liquid sorts by character code, so upper case comes first ('{string.Join(", ", ordinal.Take(5))}').");
            }
        }

        return result;
    }

    public string? Date(object? input, string? format)
    {
        if (format is not null && format.Contains('%', StringComparison.Ordinal))
        {
            Report(DiagnosticCodes.DateStrftimeFormat,
                $"Date: {Show(format)} uses a Ruby strftime format. DotLiquid uses .NET formats, for example 'yyyy-MM-dd' instead of '%Y-%m-%d'.");
        }

        if (input is string text && !string.IsNullOrWhiteSpace(format))
        {
            if (string.Equals(text, "now", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "today", StringComparison.OrdinalIgnoreCase))
            {
                Report(DiagnosticCodes.DateNow,
                    $"{Show(input)} | Date uses the clock of the Logic Apps host in time zone '{TimeZoneInfo.Local.Id}'. In Azure this is UTC, unless the app setting WEBSITE_TIME_ZONE is set.");
            }
            else if (!DateTime.TryParse(text, out _))
            {
                Report(DiagnosticCodes.DateNotParsed,
                    $"{Show(input)} | Date: {Show(format)} is not a date the host culture can parse, so the value is returned unchanged.");
            }
            else
            {
                if (_timeZonePattern.IsMatch(text))
                {
                    Report(DiagnosticCodes.DateTimeZoneConversion,
                        $"{Show(input)} | Date: {Show(format)} gives '{StandardFilters.Date(input, format)}': the time is converted to the time zone of the Logic Apps host ('{TimeZoneInfo.Local.Id}'). " +
                        "In Azure this is UTC, unless the app setting WEBSITE_TIME_ZONE is set. Shopify Liquid keeps the time zone of the value.");
                }

                if (!_isoDatePattern.IsMatch(text))
                {
                    Report(DiagnosticCodes.DateCultureParsing,
                        $"{Show(input)} is parsed with the host culture ({CultureInfo.CurrentCulture.Name}) and becomes {DateTime.Parse(text, CultureInfo.CurrentCulture):yyyy-MM-dd}. Day and month can swap between cultures; use ISO dates (yyyy-MM-dd).");
                }
            }
        }

        return StandardFilters.Date(input, format);
    }

    /// <summary>
    /// Not a DotLiquid filter: added at the end of every <c>{{ }}</c> in the observing render to see the value that is printed.
    /// It returns the value unchanged, so the output stays the same.
    /// </summary>
    /// <param name="input">The value that is printed.</param>
    /// <returns>The same value.</returns>
    public object? ObserveOutput(object? input)
    {
        if (input is ILiquidizable)
        {
            Report(DiagnosticCodes.ObjectInOutput,
                "This output is an object (or an XML element with child elements), which prints nothing. DotLiquid cannot serialize objects: " +
                "there is no json filter ('| Json' is skipped). Print the fields one by one, or pass the JSON as text in the input of the Liquid action.");
        }
        else if (input is IEnumerable items and not string && items.Cast<object?>().Any(item => item is ILiquidizable))
        {
            Report(DiagnosticCodes.ObjectInOutput,
                $"This output is a list of objects: each object prints as the internal type name '{RuntimeConstants.JsonDropTypeName}'. " +
                "Loop over the list and print the fields instead.");
        }

        return input;
    }

    public IEnumerable? Uniq(object? input)
    {
        var result = StandardFilters.Uniq(input);
        if (input is IEnumerable items && input is not string && result is not null)
        {
            var original = items.Cast<object?>().ToList();
            var unique = result.Cast<object?>().Count();
            if (original.TrueForAll(item => item is not IEnumerable || item is string) && unique < original.Count)
            {
                Report(DiagnosticCodes.UniqRemovedDuplicates,
                    $"Uniq removed {original.Count - unique} duplicate value(s). If Uniq is used to turn a single object into an array, use '| Compact' instead: it wraps a single object in the same way, but keeps duplicates.");
            }
        }

        return result;
    }

    public int Size(object? input)
    {
        if (IsNumber(input))
        {
            Report(DiagnosticCodes.SizeOfNumber,
                $"{Show(input)} | Size returns 0 for a number. Shopify Liquid returns the byte size of the number.");
        }

        return StandardFilters.Size(input);
    }

    private void InspectMath(string filterName, object? input, object? operand)
    {
        var decimalOperation = IsDecimal(input) || IsDecimal(operand);
        foreach (var value in new[] { input, operand })
        {
            if (value is not string text)
            {
                continue;
            }

            if (decimalOperation)
            {
                ReportCultureParsing(filterName, text);
            }
            else
            {
                Report(DiagnosticCodes.MathOnText,
                    $"{Show(input)} | {filterName}: {Show(operand)} fails ('Liquid error'): DotLiquid does not convert the text {Show(text)} to a number. " +
                    "Shopify Liquid does. Convert it first, for example '| Times: 1.0 | Round'.");
            }
        }
    }

    private void InspectNumberFilter(string filterName, object? value, string effect)
    {
        if (value is null)
        {
            return;
        }

        var text = value.ToString() ?? string.Empty;
        if (!double.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out _))
        {
            Report(DiagnosticCodes.NumberFilterOnText,
                $"{Show(value)} is not a number, so {filterName} {effect}. Shopify Liquid converts it to a number (0 when not numeric).");
        }
        else if (value is string)
        {
            ReportCultureParsing(filterName, text);
        }
    }

    private void ReportCultureParsing(string filterName, string text)
    {
        var format = CultureInfo.CurrentCulture.NumberFormat;
        var hasGroupSeparator = text.Contains(format.NumberGroupSeparator, StringComparison.Ordinal);
        var dotIsNotDecimal = format.NumberDecimalSeparator != "." && text.Contains('.', StringComparison.Ordinal);
        if (!hasGroupSeparator && !dotIsNotDecimal)
        {
            return;
        }

        var actual = decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var number)
            ? number.ToString(CultureInfo.InvariantCulture)
            : "an error";
        Report(DiagnosticCodes.CultureNumberParsing,
            $"{Show(text)} is converted to a number with the host culture ({CultureInfo.CurrentCulture.Name}) and becomes {actual}. " +
            $"Shopify Liquid reads '.' as the decimal separator and gives {ShopifyReference.ToNumberText(text)}. Make sure the source uses '.' and no thousands separator.");
    }

    // A decimal such as 2026.0 renders as 2026, so trailing zeros only matter when the value reaches a text filter.
    private static object? RememberTrailingZeros(string filterName, object? result)
    {
        if (result is decimal number)
        {
            var asText = number.ToString(CultureInfo.CurrentCulture);
            var rendered = number.ToString(RenderedDecimalFormat, CultureInfo.CurrentCulture);
            if (!string.Equals(asText, rendered, StringComparison.Ordinal))
            {
                FilterObserver.RememberTrailingZeros(asText, rendered, filterName);
            }
        }

        return result;
    }

    private void ReportTrailingZerosReachText(string filterName, string? text)
    {
        if (FilterObserver.TryGetTrailingZeros(text, out var origin))
        {
            ReportTrailingZeros(filterName, text!, origin.Rendered, origin.FilterName);
        }
    }

    private void ReportTrailingZeros(string filterName, string text, string rendered, string origin)
    {
        Report(DiagnosticCodes.DecimalTrailingZeros,
            $"{filterName} receives '{text}' (a decimal from {origin}): text filters keep the trailing zeros, while {{{{ }}}} output renders the same value as '{rendered}'. " +
            "Use '| Round' (or '| Round: n') before passing it on.");
    }

    private void Report(string code, string message)
    {
        FilterObserver.Report(new FilterObservation(code, message, _line, _column));
    }

    private void ReportNilInput(string filterName, string? input, string? value)
    {
        if (input is null)
        {
            Report(DiagnosticCodes.AppendToNil,
                $"{filterName}: {Show(value)} on a missing value returns nothing. Shopify Liquid returns {Show(value)}. Use '| Default: \"\"' first.");
        }
    }

    private string? ObserveReplace(string filterName, string? input, string? search, string? replacement, Func<string?> actual, Func<string> literal)
    {
        // A search text with a backslash escape is a regular expression on purpose (see RegexIntent).
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(search) || search.Contains('\\', StringComparison.Ordinal))
        {
            return actual();
        }

        string? result;
        try
        {
            result = actual();
        }
        catch (ArgumentException)
        {
            Report(DiagnosticCodes.ReplaceIsRegex,
                $"{filterName}: {Show(search)} fails: the search text is used as a regular expression and is not a valid one. Shopify Liquid replaces literal text. Escape it, for example '\\.' for a dot.");
            throw;
        }

        var expected = literal();
        if (!string.Equals(expected, result, StringComparison.Ordinal))
        {
            Report(DiagnosticCodes.ReplaceIsRegex,
                $"{Show(input)} | {filterName}: {Show(search)}{(replacement is null ? string.Empty : ", " + Show(replacement))} gives '{result}': the search text is a regular expression. " +
                $"Shopify Liquid replaces literal text and gives '{expected}'. Escape special characters, for example '\\.' for a dot.");
        }

        return result;
    }

    private string? ObserveEscape(string filterName, string? input, string? result)
    {
        if (!string.IsNullOrEmpty(input) && result is not null)
        {
            var expected = ShopifyReference.EscapeHtml(input);
            if (!string.Equals(expected, result, StringComparison.Ordinal))
            {
                Report(DiagnosticCodes.EscapeDiffers,
                    $"{Show(input)} | {filterName} gives '{result}': non-ASCII characters are encoded as well. Shopify Liquid gives '{expected}'.");
            }
        }

        return result;
    }

    private static bool IsDecimal(object? value)
    {
        return value is double or float or decimal;
    }

    private static bool IsWholeNumber(object? value)
    {
        return value is int or long or short or byte;
    }

    private static bool IsNumber(object? value)
    {
        return IsDecimal(value) || IsWholeNumber(value);
    }

    private static string Show(object? value)
    {
        return value switch
        {
            null => "nil",
            string text => $"'{text}'",
            IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
            IEnumerable => "[array]",
            _ => value.ToString() ?? string.Empty,
        };
    }
}
