namespace LogicApps.Liquid.Cli.Models;

/// <summary>
/// The files a set of diagnostics refers to, used to print clickable locations.
/// </summary>
internal sealed record DiagnosticContext
{
    /// <summary>
    /// Creates a diagnostic context.
    /// </summary>
    /// <param name="mapPath">The Liquid map.</param>
    /// <param name="inputPath">The input message, if any.</param>
    /// <param name="outputPath">The output file, if any.</param>
    /// <param name="renderedText">The rendered output, used to show the offending line for output errors.</param>
    public DiagnosticContext(string mapPath, string? inputPath = null, string? outputPath = null, string? renderedText = null)
    {
        MapPath = mapPath ?? throw new ArgumentNullException(nameof(mapPath));
        InputPath = inputPath;
        OutputPath = outputPath;
        RenderedText = renderedText;
    }

    /// <summary>The Liquid map.</summary>
    public string MapPath { get; }

    /// <summary>The input message, if any.</summary>
    public string? InputPath { get; }

    /// <summary>The output file, if any.</summary>
    public string? OutputPath { get; }

    /// <summary>The rendered output, used to show the offending line for output errors.</summary>
    public string? RenderedText { get; }
}
