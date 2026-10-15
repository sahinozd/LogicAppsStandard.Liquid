namespace LogicApps.Liquid.Models;

/// <summary>
/// A filter call observed while rendering whose result differs from what Shopify Liquid would produce.
/// </summary>
public sealed record FilterObservation
{
    /// <summary>
    /// Creates an observation.
    /// </summary>
    /// <param name="code">The diagnostic code.</param>
    /// <param name="title">Short description of the problem, without the actual values, so repeated calls can be grouped.</param>
    /// <param name="line">1-based line of the filter in the map.</param>
    /// <param name="column">1-based column of the filter in the map.</param>
    public FilterObservation(string code, string title, int line, int column)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Line = line;
        Column = column;
    }

    /// <summary>The diagnostic code.</summary>
    public string Code { get; }

    /// <summary>Short description of the problem, without the actual values.</summary>
    public string Title { get; }

    /// <summary>The actual values, for example <c>'2026' | Plus: 1 → '20261' (Shopify Liquid: 2027)</c>.</summary>
    public string? Values { get; init; }

    /// <summary>Why this happens in Logic Apps.</summary>
    public string? Explanation { get; init; }

    /// <summary>What to write instead.</summary>
    public string? Fix { get; init; }

    /// <summary>1-based line of the filter in the map.</summary>
    public int Line { get; }

    /// <summary>1-based column of the filter in the map.</summary>
    public int Column { get; }
}
