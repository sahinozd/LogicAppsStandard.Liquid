using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid;

/// <summary>
/// Validates and runs Liquid maps the way the Logic Apps Standard Liquid action does.
/// </summary>
public interface ILiquidTransformer
{
    /// <summary>
    /// Validates a map without input: static checks plus a parse with the Logic Apps engine.
    /// </summary>
    /// <param name="map">The Liquid map.</param>
    /// <returns>The findings.</returns>
    IReadOnlyList<Diagnostic> Validate(string map);

    /// <summary>
    /// Validates the map, renders it with the input and validates the output.
    /// </summary>
    /// <param name="map">The Liquid map.</param>
    /// <param name="input">The input message: JSON for <c>JsonTo*</c> kinds, XML for <c>XmlTo*</c> kinds.</param>
    /// <param name="kind">The transform kind, as configured on the Liquid action.</param>
    /// <returns>The findings, the rendered text and the action output.</returns>
    LiquidTransformResult Transform(string map, string input, LiquidTransformKind kind);
}
