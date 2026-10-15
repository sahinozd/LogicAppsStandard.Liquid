using LogicApps.Liquid.Enums;

namespace LogicApps.Liquid.Extensions;

/// <summary>
/// Extension methods for <see cref="LiquidTransformKind"/>.
/// </summary>
public static class LiquidTransformKindExtensions
{
    /// <summary>
    /// Determines whether the input of the transform is XML.
    /// </summary>
    /// <param name="kind">The transform kind.</param>
    /// <returns>True for <c>XmlToJson</c> and <c>XmlToText</c>; otherwise false.</returns>
    public static bool HasXmlInput(this LiquidTransformKind kind)
    {
        return kind is LiquidTransformKind.XmlToJson or LiquidTransformKind.XmlToText;
    }

    /// <summary>
    /// Determines whether the output of the transform is parsed as JSON.
    /// </summary>
    /// <param name="kind">The transform kind.</param>
    /// <returns>True for <c>JsonToJson</c> and <c>XmlToJson</c>; otherwise false.</returns>
    public static bool HasJsonOutput(this LiquidTransformKind kind)
    {
        return kind is LiquidTransformKind.JsonToJson or LiquidTransformKind.XmlToJson;
    }
}
