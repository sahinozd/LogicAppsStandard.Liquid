namespace LogicApps.Liquid.ConformanceTests;

/// <summary>
/// The outcome of a transform by the real Logic Apps engine.
/// </summary>
public sealed record RuntimeOutcome
{
    /// <summary>
    /// Creates a runtime outcome.
    /// </summary>
    /// <param name="output">The action output serialized as JSON, or null when the action failed.</param>
    /// <param name="error">The error message when the action failed.</param>
    public RuntimeOutcome(string? output, string? error)
    {
        Output = output;
        Error = error;
    }

    /// <summary>The action output serialized as JSON, or null when the action failed.</summary>
    public string? Output { get; }

    /// <summary>The error message when the action failed.</summary>
    public string? Error { get; }
}
