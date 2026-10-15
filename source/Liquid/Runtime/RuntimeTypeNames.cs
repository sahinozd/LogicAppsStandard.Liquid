using LogicApps.Liquid.Constants;

namespace LogicApps.Liquid.Runtime;

/// <summary>
/// Render errors can name the type of an input value ("Object of type '...' cannot be converted"). The runtime's wrappers
/// have other names than the tool's, so the names are replaced to keep the output identical to Logic Apps.
/// </summary>
internal static class RuntimeTypeNames
{
    private static readonly string _jsonDrop = typeof(JsonInputDrop).FullName!;
    private static readonly string _xmlDrop = typeof(XmlInputDrop).FullName!;

    /// <summary>
    /// Replaces the tool's type names with the runtime's.
    /// </summary>
    /// <param name="text">Rendered text or an error message.</param>
    /// <returns>The text with the runtime's type names.</returns>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Replace(_jsonDrop, RuntimeConstants.JsonDropTypeName, StringComparison.Ordinal)
            .Replace(_xmlDrop, RuntimeConstants.XmlDropTypeName, StringComparison.Ordinal);
    }
}
