using System.Text.RegularExpressions;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Linting;

/// <summary>
/// Splits a template into text, output and tag tokens the same way DotLiquid does, but keeps the offsets
/// so findings can be reported with a line and column.
/// </summary>
public static class LiquidTokenizer
{
    // Same as DotLiquid's Liquid.TemplateParser: the shortest match from an opening to a closing delimiter, on one line.
    // DotLiquid does not match across line breaks, so a tag that spans lines is a parse error in Logic Apps.
    private static readonly Regex _tokenPattern = new(@"\{%.*?%\}|\{\{.*?\}\}", RegexOptions.CultureInvariant);

    /// <summary>
    /// Splits a template into tokens.
    /// </summary>
    /// <param name="template">The Liquid template.</param>
    /// <returns>The tokens in order of appearance.</returns>
    public static IReadOnlyList<LiquidToken> Tokenize(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var tokens = new List<LiquidToken>();
        var position = 0;
        foreach (Match match in _tokenPattern.Matches(template))
        {
            if (match.Index > position)
            {
                tokens.Add(Text(template, position, match.Index - position));
            }

            var kind = match.Value.StartsWith("{%", StringComparison.Ordinal) ? LiquidTokenKind.Tag : LiquidTokenKind.Output;
            tokens.Add(new LiquidToken(kind, match.Value, ExtractMarkup(match.Value), match.Index));
            position = match.Index + match.Length;
        }

        if (position < template.Length)
        {
            tokens.Add(Text(template, position, template.Length - position));
        }

        return tokens;
    }

    private static LiquidToken Text(string template, int offset, int length)
    {
        var text = template.Substring(offset, length);
        return new LiquidToken(LiquidTokenKind.Text, text, text, offset);
    }

    private static string ExtractMarkup(string raw)
    {
        var inner = raw[2..^2];
        if (inner.StartsWith('-'))
        {
            inner = inner[1..];
        }

        if (inner.EndsWith('-'))
        {
            inner = inner[..^1];
        }

        return inner.Trim();
    }
}
