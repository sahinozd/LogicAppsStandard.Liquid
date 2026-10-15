using LogicApps.Liquid.Enums;

namespace LogicApps.Liquid.Models;

/// <summary>
/// The documentation of one diagnostic code, as shown by <c>la-liquid explain</c>.
/// </summary>
public sealed record DiagnosticDescriptor
{
    /// <summary>Stable identifier, see <see cref="Constants.DiagnosticCodes"/>.</summary>
    public required string Code { get; init; }

    /// <summary>The group the code belongs to.</summary>
    public required DiagnosticCategory Category { get; init; }

    /// <summary>The usual severity. Some findings become an error when they break the output.</summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>Short description of the problem.</summary>
    public required string Title { get; init; }

    /// <summary>What happens in Logic Apps, and why.</summary>
    public required string Description { get; init; }

    /// <summary>A map fragment that triggers the finding, with its effect.</summary>
    public string? Example { get; init; }

    /// <summary>What to write instead.</summary>
    public string? Fix { get; init; }
}
