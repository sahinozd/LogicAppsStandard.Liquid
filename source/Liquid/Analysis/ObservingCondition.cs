using System.Collections;
using System.Globalization;
using DotLiquid;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Analysis;

/// <summary>
/// Replaces one comparison in a condition during the observing render. It evaluates the comparison with DotLiquid's own
/// operator, so the result is unchanged, and records an observation when the actual values hit a pitfall.
/// </summary>
internal sealed class ObservingCondition
{
    private static readonly HashSet<string> OrderingOperators = new(StringComparer.Ordinal) { "<", ">", "<=", ">=" };
    private static readonly HashSet<string> EqualityOperators = new(StringComparer.Ordinal) { "==", "!=", "<>" };

    private readonly ConditionOperatorDelegate _original;
    private readonly string _operator;
    private readonly string _left;
    private readonly string _right;
    private readonly int _line;
    private readonly int _column;

    /// <summary>
    /// Creates the observer for one comparison.
    /// </summary>
    /// <param name="original">DotLiquid's operator.</param>
    /// <param name="operator">The operator as written, for example <c>&gt;</c>.</param>
    /// <param name="left">The left operand as written.</param>
    /// <param name="right">The right operand as written.</param>
    /// <param name="line">1-based line of the operator.</param>
    /// <param name="column">1-based column of the operator.</param>
    public ObservingCondition(ConditionOperatorDelegate original, string @operator, string left, string right, int line, int column)
    {
        _original = original ?? throw new ArgumentNullException(nameof(original));
        _operator = @operator;
        _left = left;
        _right = right;
        _line = line;
        _column = column;
    }

    /// <summary>
    /// Evaluates the comparison with DotLiquid's operator and inspects the values.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of DotLiquid's operator.</returns>
    public bool Evaluate(object left, object right)
    {
        var result = _original(left, right);
        if (_operator == "contains")
        {
            InspectContains(left, right, result);
        }
        else if (EqualityOperators.Contains(_operator) && (IsKeyword(_right, "blank") || IsKeyword(_right, "empty")))
        {
            InspectBlankOrEmpty(left, _right, result);
        }
        else if (EqualityOperators.Contains(_operator) && (IsKeyword(_left, "blank") || IsKeyword(_left, "empty")))
        {
            InspectBlankOrEmpty(right, _left, result);
        }
        else if (OrderingOperators.Contains(_operator) || EqualityOperators.Contains(_operator))
        {
            InspectComparison(left, right, result);
        }

        return result;
    }

    // DotLiquid's contains only finds numbers in a list, never text.
    private void InspectContains(object? left, object? right, bool result)
    {
        if (result || right is not string text || left is string or IDictionary or not IEnumerable)
        {
            return;
        }

        var items = ((IEnumerable)left).Cast<object?>().ToList();
        if (!items.Any(item => string.Equals(item?.ToString(), text, StringComparison.Ordinal)))
        {
            return;
        }

        Report(DiagnosticCodes.ContainsOnTextList, "contains on a list of text is always false",
            $"[{string.Join(", ", items.Take(5).Select(item => ObservingFilters.Show(item)))}{(items.Count > 5 ? ", …" : string.Empty)}] contains {ObservingFilters.Show(text)} → false (Shopify Liquid: true)",
            "In DotLiquid, contains finds text inside a text and numbers in a list, but never text in a list.",
            "Loop over the list: '{% assign found = false %}{% for x in list %}{% if x == \"A\" %}{% assign found = true %}{% endif %}{% endfor %}'.");
    }

    // Text with a number compares as text ('9' > 10), or converts ('15' == 15): it depends on the order of the operands.
    private void InspectComparison(object? left, object? right, bool result)
    {
        var textAndNumber = (left is string && IsNumber(right)) || (IsNumber(left) && right is string);
        var bothNumericText = left is string leftText && right is string rightText && TryNumber(leftText, out _) && TryNumber(rightText, out _);
        if (!textAndNumber && !(bothNumericText && OrderingOperators.Contains(_operator)))
        {
            return;
        }

        var numbers = TryNumber(left, out var leftNumber) & TryNumber(right, out var rightNumber);
        var intended = numbers && Compare(leftNumber, rightNumber);
        if (numbers && intended == result)
        {
            return;
        }

        var values = $"{ObservingFilters.Show(left)} {_operator} {ObservingFilters.Show(right)} → {(result ? "true" : "false")}";
        if (textAndNumber)
        {
            Report(DiagnosticCodes.TextComparedWithNumber, "Text is compared with a number",
                numbers ? $"{values} (as numbers: {(intended ? "true" : "false")})" : values,
                "DotLiquid compares text with a number as text, or converts one side, depending on the order of the operands. Shopify Liquid fails on '<' and '>', and '==' is false. " +
                "Values from XML input are always text.",
                "Convert the text first: '{% assign amount = content.amount | Times: 1.0 %}{% if amount > 1000 %}'.");
        }
        else
        {
            Report(DiagnosticCodes.NumbersComparedAsText, "Numbers in text are compared as text",
                $"{values} (as numbers: {(intended ? "true" : "false")})",
                "Both values are text, so they are compared character by character: '100' < '20'. Values from XML input are always text.",
                "Convert first: '{% assign a = x | Times: 1.0 %}{% assign b = y | Times: 1.0 %}{% if a > b %}'.");
        }
    }

    // Shopify: blank is nil, false, empty or whitespace text, and empty lists and objects; empty is empty text, lists and objects.
    private void InspectBlankOrEmpty(object? value, string keyword, bool result)
    {
        var isBlank = IsKeyword(keyword, "blank");
        var shopify = isBlank ? ShopifyReference.IsBlank(value) : ShopifyReference.IsEmpty(value);
        var expected = _operator == "==" ? shopify : !shopify;
        if (expected == result)
        {
            return;
        }

        var shown = value is null ? "a missing value" : value is IIndexable and not IEnumerable ? "an empty object" : ObservingFilters.Show(value);
        Report(DiagnosticCodes.BlankOrEmptyDiffers, $"'{keyword}' gives another result than in Shopify Liquid",
            $"{shown} {_operator} {keyword} → {(result ? "true" : "false")} (Shopify Liquid: {(expected ? "true" : "false")})",
            "In DotLiquid, blank does not match a missing value or whitespace, and empty does not match an empty object.",
            "Check explicitly, for example '{% if x == nil or x == \"\" %}', or '{% assign t = x | Strip %}{% if t == \"\" %}' for whitespace.");
    }

    private bool Compare(decimal left, decimal right)
    {
        return _operator switch
        {
            "<" => left < right,
            ">" => left > right,
            "<=" => left <= right,
            ">=" => left >= right,
            "==" => left == right,
            _ => left != right,
        };
    }

    private void Report(string code, string title, string values, string explanation, string fix)
    {
        FilterObserver.Report(new FilterObservation(code, title, _line, _column) { Values = values, Explanation = explanation, Fix = fix });
    }

    private static bool IsKeyword(string operand, string keyword)
    {
        return string.Equals(operand.Trim(), keyword, StringComparison.Ordinal);
    }

    private static bool IsNumber(object? value)
    {
        return value is int or long or short or byte or decimal or double or float;
    }

    private static bool TryNumber(object? value, out decimal number)
    {
        switch (value)
        {
            case string text:
                return TryNumber(text, out number);
            case null:
                number = 0;
                return false;
            default:
                if (IsNumber(value))
                {
                    number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    return true;
                }

                number = 0;
                return false;
        }
    }

    private static bool TryNumber(string text, out decimal number)
    {
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }
}
