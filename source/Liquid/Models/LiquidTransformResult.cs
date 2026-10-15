using LogicApps.Liquid.Enums;
using Newtonsoft.Json.Linq;

namespace LogicApps.Liquid.Models;

/// <summary>
/// The result of validating and running a Liquid map.
/// </summary>
public sealed class LiquidTransformResult
{
    /// <summary>
    /// Creates a transform result.
    /// </summary>
    /// <param name="kind">The transform kind that was used.</param>
    /// <param name="diagnostics">All findings.</param>
    /// <param name="renderedText">The rendered text, or null when rendering did not happen.</param>
    /// <param name="output">The action output, or null when it could not be produced.</param>
    public LiquidTransformResult(LiquidTransformKind kind, IReadOnlyList<Diagnostic> diagnostics, string? renderedText, JToken? output)
    {
        Kind = kind;
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        RenderedText = renderedText;
        Output = output;
    }

    /// <summary>The transform kind that was used.</summary>
    public LiquidTransformKind Kind { get; }

    /// <summary>All findings.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// The text DotLiquid rendered, exactly as Logic Apps renders it (including any "Liquid error: ..." text).
    /// Null when the map could not be parsed or the input was invalid.
    /// </summary>
    public string? RenderedText { get; }

    /// <summary>
    /// The action output as Logic Apps returns it: the parsed JSON for <c>*ToJson</c> kinds, a JSON string for <c>*ToText</c> kinds.
    /// Null when the output could not be produced.
    /// </summary>
    public JToken? Output { get; }

    /// <summary>True when there are no errors.</summary>
    public bool Succeeded
    {
        get
        {
            return !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        }
    }

    /// <summary>
    /// Determines whether the result has errors, or warnings when <paramref name="warningsAsErrors"/> is set.
    /// </summary>
    /// <param name="warningsAsErrors">Treat warnings as errors.</param>
    /// <returns>True when the result should be treated as failed.</returns>
    public bool HasFailures(bool warningsAsErrors)
    {
        return Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error || (warningsAsErrors && d.Severity == DiagnosticSeverity.Warning));
    }
}
