using LogicApps.Liquid.Enums;

namespace LogicApps.Liquid.Cli.Helpers;

/// <summary>
/// Works out the transform kind when <c>--kind</c> is not given: XML input gives <c>XmlTo*</c>,
/// a <c>.json</c> output file (or no output file) gives <c>*ToJson</c>, any other output extension gives <c>*ToText</c>.
/// </summary>
internal static class KindResolver
{
    /// <summary>
    /// Resolves the transform kind.
    /// </summary>
    /// <param name="explicitKind">The kind given on the command line, if any.</param>
    /// <param name="inputPath">The input file.</param>
    /// <param name="outputPath">The output file, if any.</param>
    /// <returns>The explicit kind, or the kind inferred from the file extensions.</returns>
    public static LiquidTransformKind Resolve(LiquidTransformKind? explicitKind, string inputPath, string? outputPath)
    {
        if (explicitKind is { } kind)
        {
            return kind;
        }

        var xmlInput = HasExtension(inputPath, ".xml");
        var jsonOutput = outputPath is null || HasExtension(outputPath, ".json");
        return (xmlInput, jsonOutput) switch
        {
            (true, true) => LiquidTransformKind.XmlToJson,
            (true, false) => LiquidTransformKind.XmlToText,
            (false, true) => LiquidTransformKind.JsonToJson,
            (false, false) => LiquidTransformKind.JsonToText,
        };
    }

    private static bool HasExtension(string path, string extension)
    {
        return string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);
    }
}
