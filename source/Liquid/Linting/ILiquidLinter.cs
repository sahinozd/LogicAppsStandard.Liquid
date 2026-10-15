using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Linting;

/// <summary>
/// Static checks on a Liquid map, with line and column information.
/// </summary>
public interface ILiquidLinter
{
    /// <summary>
    /// Runs all static checks on a template.
    /// </summary>
    /// <param name="map">The Liquid map.</param>
    /// <returns>The findings, ordered by line and column.</returns>
    IReadOnlyList<Diagnostic> Lint(string map);
}
