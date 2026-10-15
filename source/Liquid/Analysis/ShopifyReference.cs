using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LogicApps.Liquid.Analysis;

/// <summary>
/// The results Shopify Liquid (the Ruby reference implementation, lib/liquid/standardfilters.rb) produces for the
/// filters where DotLiquid behaves differently. Used to show the expected value next to the actual value.
/// </summary>
internal static class ShopifyReference
{
    private static readonly Regex _rubyDecimalPattern = new(@"^-?\d+\.\d+$");
    private static readonly Regex _rubyIntegerPrefixPattern = new(@"^[+-]?\d+");

    /// <summary>Ruby rounds half away from zero.</summary>
    public static decimal Round(decimal value, int decimals)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>Ruby integer division floors.</summary>
    public static long FloorDivide(long dividend, long divisor)
    {
        var quotient = dividend / divisor;
        return (dividend % divisor != 0) && ((dividend < 0) != (divisor < 0)) ? quotient - 1 : quotient;
    }

    /// <summary>Ruby modulo takes the sign of the divisor.</summary>
    public static long Modulo(long dividend, long divisor)
    {
        return ((dividend % divisor) + divisor) % divisor;
    }

    /// <summary>Ruby capitalize: first character upper case, the rest lower case.</summary>
    public static string Capitalize(string input)
    {
        if (input.Length == 0)
        {
            return input;
        }

        return char.ToUpperInvariant(input[0]) + input[1..].ToLowerInvariant();
    }

    /// <summary>Ruby CGI.escapeHTML: only &amp;, &lt;, &gt;, &quot; and &#39; are encoded.</summary>
    public static string EscapeHtml(string input)
    {
        var builder = new StringBuilder(input.Length);
        foreach (var character in input)
        {
            builder.Append(character switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => character.ToString(),
            });
        }

        return builder.ToString();
    }

    /// <summary>Ruby String#split: empty items are kept, except trailing ones; an empty separator splits into characters.</summary>
    public static int SplitCount(string input, string separator)
    {
        if (separator.Length == 0)
        {
            return input.Length;
        }

        var parts = input.Split(separator, StringSplitOptions.None).ToList();
        while (parts.Count > 0 && parts[^1].Length == 0)
        {
            parts.RemoveAt(parts.Count - 1);
        }

        return parts.Count;
    }

    /// <summary>Ruby gsub with a string pattern replaces literal text.</summary>
    public static string ReplaceAll(string input, string search, string replacement)
    {
        return input.Replace(search, replacement, StringComparison.Ordinal);
    }

    /// <summary>Ruby sub with a string pattern replaces the first literal occurrence.</summary>
    public static string ReplaceFirst(string input, string search, string replacement)
    {
        var index = input.IndexOf(search, StringComparison.Ordinal);
        return index < 0 ? input : string.Concat(input.AsSpan(0, index), replacement, input.AsSpan(index + search.Length));
    }

    /// <summary>
    /// Ruby Utils.to_number: text matching <c>-?\d+\.\d+</c> becomes a decimal ('.' as separator, independent of culture);
    /// any other text becomes <c>String#to_i</c>: the leading whole number, or 0.
    /// </summary>
    public static string ToNumberText(string input)
    {
        var text = input.Trim();
        if (_rubyDecimalPattern.IsMatch(text))
        {
            return decimal.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        }

        var leadingInteger = _rubyIntegerPrefixPattern.Match(text);
        return leadingInteger.Success ? long.Parse(leadingInteger.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : "0";
    }
}
