using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using DotLiquid;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Models;
using LogicApps.Liquid.Runtime;

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
        if (TryObserveOverflow(nameof(Plus), input, operand, () => StandardFilters.Plus(input, operand), (a, b) => a + b, out var overflowed))
        {
            return overflowed;
        }

        if (input is string text)
        {
            var sum = IsNumber(operand)
                ? (decimal.Parse(ShopifyReference.ToNumberText(text), CultureInfo.InvariantCulture) + Convert.ToDecimal(operand, CultureInfo.CurrentCulture)).ToString(CultureInfo.InvariantCulture)
                : "a sum";
            Report(DiagnosticCodes.PlusJoinsText, "Plus on text joins instead of adding",
                $"{Show(input)} | Plus: {Show(operand)} → '{string.Concat(input, operand)}' (Shopify Liquid: {sum})",
                "DotLiquid adds text and a number as text. Filters such as Date, Append and Split return text.",
                "Convert to a number first, for example '| Times: 1.0 | Round | Plus: ...'.");
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
        if (TryObserveOverflow(nameof(Minus), input, operand, () => StandardFilters.Minus(input, operand), (a, b) => a - b, out var overflowed))
        {
            return overflowed;
        }

        InspectMath(nameof(Minus), input, operand);
        return RememberTrailingZeros(nameof(Minus), StandardFilters.Minus(input, operand));
    }

    public object? Times(object? input, object? operand)
    {
        if (TryObserveOverflow(nameof(Times), input, operand, () => StandardFilters.Times(input, operand), (a, b) => a * b, out var overflowed))
        {
            return overflowed;
        }

        if (input is string text && operand is int or long)
        {
            var count = Convert.ToInt64(operand, CultureInfo.InvariantCulture);
            var repeated = count switch
            {
                < 0 => "'Liquid error'",
                <= 5 => $"'{string.Concat(Enumerable.Repeat(text, (int)count))}'",
                _ => $"the text {count} times",
            };
            Report(DiagnosticCodes.TimesRepeatsText, "Times on text repeats the text instead of multiplying",
                $"{Show(input)} | Times: {Show(operand)} → {repeated} (Shopify Liquid multiplies)",
                "DotLiquid repeats text when the operand is a whole number, and fails for a negative number.",
                "Use a decimal operand, for example '| Times: 1.0', to convert text to a number.");
        }
        else
        {
            InspectMath(nameof(Times), input, operand);
        }

        return RememberTrailingZeros(nameof(Times), StandardFilters.Times(input, operand));
    }

    public object? DividedBy(object? input, object? operand)
    {
        // Integer division truncates (LQ108 reports where that differs from Shopify); only the overflow is checked here.
        if (TryObserveOverflow(nameof(DividedBy), input, operand, () => StandardFilters.DividedBy(input, operand), (a, b) => decimal.Truncate(a / b), out var overflowed))
        {
            return overflowed;
        }

        InspectMath(nameof(DividedBy), input, operand);
        var result = StandardFilters.DividedBy(input, operand);

        if (result is double { } quotient && (double.IsInfinity(quotient) || double.IsNaN(quotient)))
        {
            Report(DiagnosticCodes.DivisionByZero, "Division by zero renders a value instead of an error",
                $"{Show(input)} | DividedBy: {Show(operand)} → '{Show(result)}' (Shopify Liquid: error)",
                "A decimal division by zero gives infinity or NaN in DotLiquid.",
                "Check for zero first.");
        }
        else if (IsWholeNumber(input) && IsWholeNumber(operand) && result is not null)
        {
            var dividend = Convert.ToInt64(input, CultureInfo.InvariantCulture);
            var divisor = Convert.ToInt64(operand, CultureInfo.InvariantCulture);
            var expected = ShopifyReference.FloorDivide(dividend, divisor);
            if (expected != Convert.ToInt64(result, CultureInfo.InvariantCulture))
            {
                Report(DiagnosticCodes.NegativeIntegerDivision, "Negative integer division rounds toward zero",
                    $"{Show(input)} | DividedBy: {Show(operand)} → {Show(result)} (Shopify Liquid: {expected})",
                    "DotLiquid rounds toward zero, Shopify Liquid rounds down.",
                    null);
            }
        }

        return RememberTrailingZeros(nameof(DividedBy), result);
    }

    public object? Modulo(object? input, object? operand)
    {
        if (TryObserveOverflow(nameof(Modulo), input, operand, () => StandardFilters.Modulo(input, operand), (a, b) => a % b, out var overflowed))
        {
            return overflowed;
        }

        InspectMath(nameof(Modulo), input, operand);
        var result = StandardFilters.Modulo(input, operand);

        if (IsWholeNumber(input) && IsWholeNumber(operand) && result is not null)
        {
            var dividend = Convert.ToInt64(input, CultureInfo.InvariantCulture);
            var divisor = Convert.ToInt64(operand, CultureInfo.InvariantCulture);
            var expected = ShopifyReference.Modulo(dividend, divisor);
            if (expected != Convert.ToInt64(result, CultureInfo.InvariantCulture))
            {
                Report(DiagnosticCodes.NegativeModulo, "Modulo of a negative number takes the sign of the input",
                    $"{Show(input)} | Modulo: {Show(operand)} → {Show(result)} (Shopify Liquid: {expected})",
                    "DotLiquid uses the C# remainder, Shopify Liquid the mathematical modulo.",
                    null);
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
            Report(DiagnosticCodes.RoundNotANumber, "Round on a value that is not a number returns nothing",
                $"{Show(input)} | Round → nothing",
                null,
                "Check the input, or use '| Default' before Round.");
            return result;
        }

        var decimals = places is null ? 0 : Convert.ToInt32(places, CultureInfo.CurrentCulture);
        var expected = ShopifyReference.Round(Convert.ToDecimal(input, CultureInfo.CurrentCulture), decimals);
        if (expected != (decimal)result)
        {
            Report(DiagnosticCodes.BankersRounding, "Round rounds halves to the even number",
                $"{Show(input)} | Round{(places is null ? string.Empty : ": " + Show(places))} → {Show(result)} (Shopify Liquid: {expected.ToString(CultureInfo.CurrentCulture)})",
                "DotLiquid uses banker's rounding; Shopify Liquid rounds half away from zero.",
                "Add a tiny amount before rounding when halves must round up, for example '| Plus: 0.0000001 | Round: 2'.");
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
        ReportBooleanAsText(nameof(Default), input);
        if (!string.IsNullOrEmpty(input) && string.IsNullOrWhiteSpace(input))
        {
            Report(DiagnosticCodes.DefaultDiffers, "Default treats whitespace as empty",
                $"{Show(input)} | Default: {Show(defaultValue)} → {Show(defaultValue)} (Shopify Liquid: {Show(input)})",
                null,
                null);
        }
        else if (input is "False")
        {
            Report(DiagnosticCodes.DefaultDiffers, "Default does not treat false as empty",
                $"false | Default: {Show(defaultValue)} → 'False' (Shopify Liquid: {Show(defaultValue)})",
                null,
                "Use an if instead.");
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
                Report(DiagnosticCodes.SplitDiffers, "Split gives a different number of items",
                    $"{Show(input)} | Split: {Show(pattern)} → {result.Length} item(s) (Shopify Liquid: {expected})",
                    $"In DotLiquid {reason}.",
                    null);
            }
        }

        return result;
    }

    public string? Append(string? input, string? @string)
    {
        ReportBooleanAsText(nameof(Append), input);
        ReportNilInput(nameof(Append), input, @string);
        ReportTrailingZerosReachText(nameof(Append), input);
        ReportTrailingZerosReachText(nameof(Append), @string);
        return StandardFilters.Append(input, @string);
    }

    public string? Prepend(string? input, string? @string)
    {
        ReportBooleanAsText(nameof(Prepend), input);
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
                Report(DiagnosticCodes.CapitalizeDiffers, "Capitalize capitalizes every word",
                    $"{Show(input)} | Capitalize → '{result}' (Shopify Liquid: '{expected}')",
                    "DotLiquid gives every word a capital and keeps the rest; Shopify Liquid only capitalizes the first character and lowercases the rest.",
                    null);
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

        if (ReportXmlFlattening(nameof(Sort), input))
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
                    Report(DiagnosticCodes.SortDiffers, "Sort by property does not sort JSON input",
                        $"Sort: {Show(property)} → original order (Shopify Liquid sorts by '{property}')",
                        "DotLiquid cannot read the property of JSON objects while sorting, so the items keep their original order.",
                        "Build the order yourself, for example with a for loop per value.");
                }
            }
        }
        else if (sorted.Count > 1 && sorted.All(item => item is string))
        {
            var ordinal = sorted.Cast<string>().OrderBy(item => item, StringComparer.Ordinal).ToList();
            if (!ordinal.SequenceEqual(sorted.Cast<string>()))
            {
                Report(DiagnosticCodes.SortDiffers, "Sort orders text by the host culture",
                    $"'{string.Join(", ", sorted.Take(5))}' (Shopify Liquid: '{string.Join(", ", ordinal.Take(5))}')",
                    "Shopify Liquid sorts by character code, so upper case comes first.",
                    null);
            }
        }

        return result;
    }

    public string? Date(object? input, string? format)
    {
        if (IsNumber(input) && !string.IsNullOrWhiteSpace(format))
        {
            Report(DiagnosticCodes.DateNotParsed, "Date does not read a number as a timestamp",
                $"{Show(input)} | Date: {Show(format)} → '{StandardFilters.Date(input, format)}' (Shopify Liquid reads it as a Unix timestamp)",
                "DotLiquid reads a number as text: a whole number is returned unchanged, and a decimal such as 1.5 can even become a date in the current year.",
                "Convert the timestamp in the workflow, for example with addSeconds('1970-01-01', ...), and pass the date as text.");
        }

        if (format is not null && format.Contains('%', StringComparison.Ordinal))
        {
            Report(DiagnosticCodes.DateStrftimeFormat, "Date uses a Ruby strftime format",
                $"Date: {Show(format)}",
                "DotLiquid uses .NET date formats, so '%Y' and similar are not replaced.",
                "Use .NET formats, for example 'yyyy-MM-dd' instead of '%Y-%m-%d'.");
        }

        if (input is string text && !string.IsNullOrWhiteSpace(format))
        {
            if (string.Equals(text, "now", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "today", StringComparison.OrdinalIgnoreCase))
            {
                Report(DiagnosticCodes.DateNow, "Date uses the clock and time zone of the Logic Apps host",
                    $"{Show(input)} | Date (time zone here: '{TimeZoneInfo.Local.Id}')",
                    "In Azure this is UTC, unless the app setting WEBSITE_TIME_ZONE is set.",
                    "Pass the time from the workflow (utcNow()) in the input when the time zone matters.");
            }
            else if (!DateTime.TryParse(text, out _))
            {
                Report(DiagnosticCodes.DateNotParsed, "Date cannot parse the value",
                    $"{Show(input)} | Date: {Show(format)} → {Show(input)} (unchanged)",
                    $"The host culture ({CultureInfo.CurrentCulture.Name}) cannot parse it as a date, so the value is returned unchanged.",
                    "Use ISO dates (yyyy-MM-dd) in the input.");
            }
            else
            {
                if (_timeZonePattern.IsMatch(text))
                {
                    Report(DiagnosticCodes.DateTimeZoneConversion, "Date converts the time to the time zone of the host",
                        $"{Show(input)} | Date: {Show(format)} → '{StandardFilters.Date(input, format)}' (host time zone '{TimeZoneInfo.Local.Id}')",
                        "In Azure the host runs in UTC, unless the app setting WEBSITE_TIME_ZONE is set. Shopify Liquid keeps the time zone of the value.",
                        "Use 'K' in the format to write the actual offset, or convert the time in the workflow.");
                }

                if (!_isoDatePattern.IsMatch(text))
                {
                    Report(DiagnosticCodes.DateCultureParsing, "Date parses the value with the host culture",
                        $"{Show(input)} → {DateTime.Parse(text, CultureInfo.CurrentCulture):yyyy-MM-dd} (culture {CultureInfo.CurrentCulture.Name})",
                        "Day and month can swap between cultures.",
                        "Use ISO dates (yyyy-MM-dd).");
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
            Report(DiagnosticCodes.ObjectInOutput, "An object is printed, which prints nothing",
                null,
                "The value is an object (or an XML element with child elements). DotLiquid cannot serialize objects: there is no json filter ('| Json' is skipped).",
                "Print the fields one by one, or pass the JSON as text in the input of the Liquid action.");
        }
        else if (input is IEnumerable items and not string && items.Cast<object?>().Any(item => item is ILiquidizable))
        {
            Report(DiagnosticCodes.ObjectInOutput, "A list of objects is printed as internal type names",
                $"each object prints as '{RuntimeConstants.JsonDropTypeName}'",
                "DotLiquid cannot serialize objects.",
                "Loop over the list and print the fields instead.");
        }

        return input;
    }

    public IEnumerable? Compact(object? input)
    {
        ReportXmlFlattening(nameof(Compact), input);
        return StandardFilters.Compact(input);
    }

    public IEnumerable? Uniq(object? input)
    {
        var result = StandardFilters.Uniq(input);
        if (ReportXmlFlattening(nameof(Uniq), input))
        {
            return result;
        }

        if (input is IEnumerable items && input is not string && result is not null)
        {
            var original = items.Cast<object?>().ToList();
            var unique = result.Cast<object?>().Count();
            if (original.TrueForAll(item => item is not IEnumerable || item is string) && unique < original.Count)
            {
                Report(DiagnosticCodes.UniqRemovedDuplicates, "Uniq removed duplicate values",
                    $"{original.Count} → {unique} item(s)",
                    "This is intended when Uniq is used to remove duplicates, but not when it is used to turn a single object into an array.",
                    "To turn a single object into an array, use '| Compact': it wraps a single object in the same way, but keeps duplicates.");
            }
        }

        return result;
    }

    public int Size(object? input)
    {
        if (IsNumber(input))
        {
            Report(DiagnosticCodes.SizeOfNumber, "Size of a number returns 0",
                $"{Show(input)} | Size → 0 (Shopify Liquid: the byte size of the number)",
                null,
                "Use Size on text or lists only.");
        }
        else if (input is IIndexable and not IEnumerable)
        {
            Report(DiagnosticCodes.SizeOfNumber, "Size of an object returns 0",
                "object | Size → 0 (Shopify Liquid: the number of properties)",
                "DotLiquid only counts text and lists; '.size' on an object is empty as well.",
                "Use Size on text or lists only.");
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
                Report(DiagnosticCodes.MathOnText, $"{filterName} on text fails",
                    $"{Show(input)} | {filterName}: {Show(operand)} → 'Liquid error' (Shopify Liquid converts {Show(text)} to a number)",
                    "DotLiquid does not convert text to a number for whole-number math.",
                    "Convert it first, for example '| Times: 1.0 | Round'.");
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
            Report(DiagnosticCodes.NumberFilterOnText, $"{filterName} on a value that is not a number",
                $"{Show(value)} → {filterName} {effect}",
                "Shopify Liquid converts it to a number (0 when not numeric).",
                "Check the input, or use '| Default' first.");
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
        Report(DiagnosticCodes.CultureNumberParsing, "Text is converted to a number with the host culture",
            $"{Show(text)} → {actual} in {CultureInfo.CurrentCulture.Name} (Shopify Liquid: {ShopifyReference.ToNumberText(text)})",
            $"{filterName} converts text with the culture of the Logic Apps host; Shopify Liquid always reads '.' as the decimal separator.",
            "Make sure the source uses '.' and no thousands separator.");
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
        Report(DiagnosticCodes.DecimalTrailingZeros, "A decimal keeps its trailing zeros in a text filter",
            $"{filterName} receives '{text}' (from {origin}; {{{{ }}}} output renders '{rendered}')",
            "Text filters receive the decimal with its trailing zeros, while {{ }} output drops them.",
            "Use '| Round' (or '| Round: n') before passing it on.");
    }

    // Math filters calculate whole numbers as 32-bit integers. Compare with the exact result, and report a wrong result or a failure.
    private bool TryObserveOverflow(string filterName, object? input, object? operand, Func<object?> calculate, Func<decimal, decimal, decimal> exact, out object? result)
    {
        result = null;
        if (!IsWholeNumber(input) || !IsWholeNumber(operand))
        {
            return false;
        }

        var left = Convert.ToDecimal(input, CultureInfo.InvariantCulture);
        var right = Convert.ToDecimal(operand, CultureInfo.InvariantCulture);
        decimal expected;
        try
        {
            expected = exact(left, right);
        }
        catch (DivideByZeroException)
        {
            return false;
        }

        if (Math.Abs(left) <= int.MaxValue && Math.Abs(right) <= int.MaxValue && Math.Abs(expected) <= int.MaxValue)
        {
            return false;
        }

        var call = $"{Show(input)} | {filterName}: {Show(operand)}";
        const string Explanation = "DotLiquid calculates whole numbers as 32-bit integers, so a value or result above 2,147,483,647 wraps around or fails. " +
            "IDs, amounts in cents and timestamps in milliseconds easily get that large.";
        var fix = $"Use a decimal operand, for example '| {filterName}: {right.ToString(CultureInfo.InvariantCulture)}.0'. Output shows no decimals for a whole result.";
        try
        {
            result = calculate();
        }
        catch (Exception ex) when (ex is OverflowException or TargetInvocationException or InvalidOperationException)
        {
            Report(DiagnosticCodes.WholeNumberOverflow, "Large whole numbers overflow in math filters",
                $"{call} → 'Liquid error' (Shopify Liquid: {expected.ToString(CultureInfo.InvariantCulture)})", Explanation, fix);
            throw;
        }

        if (result is null || Convert.ToDecimal(result, CultureInfo.InvariantCulture) == expected)
        {
            return false;
        }

        Report(DiagnosticCodes.WholeNumberOverflow, "Large whole numbers overflow in math filters",
            $"{call} → {Show(result)} (Shopify Liquid: {expected.ToString(CultureInfo.InvariantCulture)})", Explanation, fix);
        return true;
    }

    // A text filter receives a true/false value as 'True'/'False'. Only for JSON input: XML has no booleans, so 'True' there is real text.
    private void ReportBooleanAsText(string filterName, string? input)
    {
        if (FilterObserver.JsonInput && input is "True" or "False")
        {
            var json = input == "True" ? "true" : "false";
            Report(DiagnosticCodes.BooleanAsText, "A true/false value becomes 'True'/'False' in a text filter",
                $"{json} | {filterName} → '{input}…' (Shopify Liquid: '{json}…')",
                "Printed directly, a JSON boolean is 'true' or 'false', but text filters get the .NET text 'True' or 'False'. In JSON output that is invalid.",
                "Print the value directly, or add '| Downcase'.");
        }
    }

    private void Report(string code, string title, string? values, string? explanation, string? fix)
    {
        FilterObserver.Report(new FilterObservation(code, title, _line, _column) { Values = values, Explanation = explanation, Fix = fix });
    }

    // Sort, Compact and Uniq flatten nested lists. XML elements are lists of their child elements, so a list of elements
    // becomes a list of all their child values: the elements themselves are lost.
    private bool ReportXmlFlattening(string filterName, object? input)
    {
        var containsElements = input switch
        {
            XmlInputDrop drop => drop.Cast<object?>().Any(child => child is XmlInputDrop),
            IEnumerable items and not string => items.Cast<object?>().Any(item => item is XmlInputDrop),
            _ => false,
        };
        if (!containsElements)
        {
            return false;
        }

        Report(DiagnosticCodes.XmlElementsFlattened, $"{filterName} on XML elements flattens them",
            null,
            "The result is a list of all child values, so the elements themselves are lost ('item.Name' no longer works). Shopify Liquid keeps the elements.",
            "Loop over the elements directly; to order them, loop once per value.");
        return true;
    }

    private void ReportNilInput(string filterName, string? input, string? value)
    {
        if (input is null)
        {
            Report(DiagnosticCodes.AppendToNil, $"{filterName} on a missing value returns nothing",
                $"nil | {filterName}: {Show(value)} → nothing (Shopify Liquid: {Show(value)})",
                null,
                "Use '| Default: \"\"' first.");
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
            Report(DiagnosticCodes.ReplaceIsRegex, $"{filterName} fails on an invalid regular expression",
                $"{filterName}: {Show(search)} → error",
                "DotLiquid uses the search text as a regular expression. Shopify Liquid replaces literal text.",
                "Escape special characters, for example '\\.' for a dot.");
            throw;
        }

        var expected = literal();
        if (!string.Equals(expected, result, StringComparison.Ordinal))
        {
            Report(DiagnosticCodes.ReplaceIsRegex, $"{filterName} uses the search text as a regular expression",
                $"{Show(input)} | {filterName}: {Show(search)}{(replacement is null ? string.Empty : ", " + Show(replacement))} → '{result}' (Shopify Liquid: '{expected}')",
                "Shopify Liquid replaces literal text. In the replacement, '$0', '$$', '$_' and similar are substitutions too.",
                "Escape special characters in the search text, for example '\\.' for a dot. In the replacement, write '$$' for a '$' that is followed by 0-9, $, _, & or a quote.");
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
                Report(DiagnosticCodes.EscapeDiffers, $"{filterName} also encodes non-ASCII characters",
                    $"{Show(input)} | {filterName} → '{result}' (Shopify Liquid: '{expected}')",
                    null,
                    null);
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

    internal static string Show(object? value)
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
