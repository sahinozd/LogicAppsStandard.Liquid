using LogicApps.Liquid.Cli.Enums;

namespace LogicApps.Liquid.Cli.Models;

/// <summary>
/// How findings are written, as given on the command line.
/// </summary>
internal sealed record OutputSettings
{
    /// <summary>The output format.</summary>
    public OutputFormat Format { get; init; } = OutputFormat.Pretty;

    /// <summary>How much the pretty format shows per finding.</summary>
    public OutputVerbosity Verbosity { get; init; } = OutputVerbosity.Normal;

    /// <summary>Write ANSI colors.</summary>
    public bool Color { get; init; }
}
