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
    /// <param name="message">What happened, with the actual values.</param>
    /// <param name="line">1-based line of the filter in the map.</param>
    /// <param name="column">1-based column of the filter in the map.</param>
    public FilterObservation(string code, string message, int line, int column)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Line = line;
        Column = column;
    }

    /// <summary>The diagnostic code.</summary>
    public string Code { get; }

    /// <summary>What happened, with the actual values.</summary>
    public string Message { get; }

    /// <summary>1-based line of the filter in the map.</summary>
    public int Line { get; }

    /// <summary>1-based column of the filter in the map.</summary>
    public int Column { get; }
}
