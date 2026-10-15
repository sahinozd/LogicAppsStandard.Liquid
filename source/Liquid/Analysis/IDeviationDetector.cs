using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Analysis;

/// <summary>
/// Detects filters that behave differently from Shopify Liquid, using the actual values while rendering.
/// </summary>
public interface IDeviationDetector
{
    /// <summary>
    /// Renders the map with observing filters and reports every deviation as a warning.
    /// </summary>
    /// <param name="map">The Liquid map. It must be parseable.</param>
    /// <param name="content">The input, wrapped the same way as for the normal render.</param>
    /// <returns>The warnings, with the location of the filter in the map where possible.</returns>
    IReadOnlyList<Diagnostic> Detect(string map, object content);
}
