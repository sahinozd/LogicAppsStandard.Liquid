using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Cli.Models;

/// <summary>
/// The outcome of a command, written as the last lines of the output.
/// </summary>
internal sealed record RunSummary
{
    /// <summary>What ran: Validation, Transform or Tests.</summary>
    public required string Operation { get; init; }

    /// <summary>True when the command succeeded.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>All findings, used for the counts per category.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    /// <summary>The number of maps that were checked, if relevant.</summary>
    public int? MapCount { get; init; }

    /// <summary>The number of passed tests, for a test run.</summary>
    public int? TestsPassed { get; init; }

    /// <summary>The number of failed tests, for a test run.</summary>
    public int? TestsFailed { get; init; }

    /// <summary>An extra remark, for example where the output was written.</summary>
    public string? Note { get; init; }
}
