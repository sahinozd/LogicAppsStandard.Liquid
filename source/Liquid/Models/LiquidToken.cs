using System.Text.RegularExpressions;
using LogicApps.Liquid.Enums;

namespace LogicApps.Liquid.Models;

/// <summary>
/// A piece of a Liquid template: literal text, an output (<c>{{ }}</c>) or a tag (<c>{% %}</c>).
/// </summary>
public sealed record LiquidToken
{
    private static readonly Regex TagNamePattern = new(@"^(\w+)", RegexOptions.CultureInvariant);

    /// <summary>
    /// Creates a token.
    /// </summary>
    /// <param name="kind">The kind of token.</param>
    /// <param name="raw">The raw text, including delimiters.</param>
    /// <param name="markup">For output and tags: the content between the delimiters, trimmed and without whitespace control dashes.</param>
    /// <param name="offset">Zero-based offset in the template.</param>
    public LiquidToken(LiquidTokenKind kind, string raw, string markup, int offset)
    {
        Kind = kind;
        Raw = raw ?? throw new ArgumentNullException(nameof(raw));
        Markup = markup ?? throw new ArgumentNullException(nameof(markup));
        Offset = offset;
    }

    /// <summary>The kind of token.</summary>
    public LiquidTokenKind Kind { get; }

    /// <summary>The raw text, including delimiters.</summary>
    public string Raw { get; }

    /// <summary>For output and tags: the content between the delimiters, trimmed and without whitespace control dashes.</summary>
    public string Markup { get; }

    /// <summary>Zero-based offset in the template.</summary>
    public int Offset { get; }

    /// <summary>For tags: the tag name (first word of the markup), otherwise empty.</summary>
    public string TagName
    {
        get
        {
            if (Kind != LiquidTokenKind.Tag)
            {
                return string.Empty;
            }

            var match = TagNamePattern.Match(Markup);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }

    /// <summary>For tags: the markup after the tag name, otherwise empty.</summary>
    public string TagArguments
    {
        get
        {
            var tagName = TagName;
            return tagName.Length > 0 ? Markup[tagName.Length..].Trim() : string.Empty;
        }
    }
}
