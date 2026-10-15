namespace LogicApps.Liquid.Linting;

/// <summary>
/// Converts character offsets in a text into 1-based line and column numbers.
/// </summary>
public sealed class TextPosition
{
    private readonly List<int> _lineStarts = [0];

    /// <summary>
    /// Indexes the line starts of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    public TextPosition(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                _lineStarts.Add(i + 1);
            }
        }
    }

    /// <summary>
    /// Gets the line and column of an offset.
    /// </summary>
    /// <param name="offset">Zero-based character offset.</param>
    /// <returns>The 1-based line and column.</returns>
    public (int Line, int Column) At(int offset)
    {
        var index = _lineStarts.BinarySearch(offset);
        var line = index >= 0 ? index : ~index - 1;
        return (line + 1, offset - _lineStarts[line] + 1);
    }
}
