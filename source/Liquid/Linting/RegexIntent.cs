using System.Text.RegularExpressions;

namespace LogicApps.Liquid.Linting;

/// <summary>
/// Decides whether the search text of <c>Replace</c>, <c>ReplaceFirst</c> or <c>RemoveFirst</c> is accidentally a regular
/// expression. DotLiquid always treats it as one. A search text with a backslash escape (<c>\.</c>, <c>\\</c>) is written as
/// a regular expression on purpose, for example <c>Replace: '\\', '\\'</c> to escape backslashes for JSON, so it is not flagged.
/// </summary>
public static class RegexIntent
{
    private static readonly Regex _specialCharacters = new(@"[.$^{\[(|)*+?]");

    /// <summary>
    /// Determines whether the search text contains special regular expression characters that were probably meant literally.
    /// </summary>
    /// <param name="search">The search text.</param>
    /// <returns>True when the text has unescaped special characters and no escapes.</returns>
    public static bool IsAccidentalRegex(string? search)
    {
        if (string.IsNullOrEmpty(search) || search.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        return _specialCharacters.IsMatch(search);
    }
}
