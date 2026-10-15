using System.CommandLine;
using System.Globalization;
using LogicApps.Liquid.Cli.Models;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Cli.Commands;

/// <summary>
/// Options and helpers shared by the commands.
/// </summary>
internal static class CommonOptions
{
    /// <summary>
    /// Creates the <c>--kind</c> option.
    /// </summary>
    /// <returns>The option.</returns>
    public static Option<LiquidTransformKind?> Kind()
    {
        return new Option<LiquidTransformKind?>("--kind", "-k")
        {
            Description = "The transform kind of the Liquid action: JsonToJson, JsonToText, XmlToJson or XmlToText. " +
                "When omitted, it is inferred from the input and output file extensions.",
        };
    }


    /// <summary>
    /// Creates the <c>--warnings-as-errors</c> option.
    /// </summary>
    /// <returns>The option.</returns>
    public static Option<bool> WarningsAsErrors()
    {
        return new Option<bool>("--warnings-as-errors")
        {
            Description = "Treat warnings as errors.",
        };
    }


    /// <summary>
    /// Creates the transformer, or returns null after printing an error when the culture or time zone is unknown.
    /// </summary>
    /// <param name="settings">How the Logic Apps engine is emulated and which checks run.</param>
    /// <returns>The transformer, or null.</returns>
    public static ILiquidTransformer? CreateTransformer(EngineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(settings.CultureName);
        }
        catch (CultureNotFoundException)
        {
            Console.Error.WriteLine($"error: unknown culture '{settings.CultureName}'.");
            return null;
        }

        TimeZoneInfo? timeZone;
        try
        {
            timeZone = string.Equals(settings.TimeZoneId, "local", StringComparison.OrdinalIgnoreCase)
                ? null
                : TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            Console.Error.WriteLine($"error: unknown time zone '{settings.TimeZoneId}'.");
            return null;
        }

        return new LiquidTransformer(new LiquidTransformOptions
        {
            Culture = culture,
            TimeZone = timeZone,
            Lint = !settings.NoLint,
            DetectDeviations = !settings.NoDeviations,
        });
    }

    /// <summary>
    /// Reads a file, or returns null after printing an error.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="description">What the file is, used in the error message.</param>
    /// <returns>The file content, or null.</returns>
    public static string? ReadFile(string path, string description)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: cannot read {description} '{path}': {ex.Message}");
            return null;
        }
    }
}
