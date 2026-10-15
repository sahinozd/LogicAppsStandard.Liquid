namespace LogicApps.Liquid.Cli.Reporting;

/// <summary>
/// Wraps text in ANSI color codes, or returns it unchanged when colors are off.
/// </summary>
internal sealed class AnsiPalette
{
    private readonly bool _enabled;

    /// <summary>
    /// Creates a palette.
    /// </summary>
    /// <param name="enabled">Write color codes.</param>
    public AnsiPalette(bool enabled)
    {
        _enabled = enabled;
    }

    public string Red(string text)
    {
        return Paint("1;31", text);
    }

    public string Yellow(string text)
    {
        return Paint("1;33", text);
    }

    public string Magenta(string text)
    {
        return Paint("1;35", text);
    }

    public string Green(string text)
    {
        return Paint("32", text);
    }

    public string Cyan(string text)
    {
        return Paint("36", text);
    }

    public string Bold(string text)
    {
        return Paint("1", text);
    }

    public string Dim(string text)
    {
        return Paint("2", text);
    }

    private string Paint(string code, string text)
    {
        return _enabled && text.Length > 0 ? $"\u001b[{code}m{text}\u001b[0m" : text;
    }
}
