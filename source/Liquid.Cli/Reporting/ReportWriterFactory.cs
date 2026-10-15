using LogicApps.Liquid.Cli.Enums;
using LogicApps.Liquid.Cli.Models;

namespace LogicApps.Liquid.Cli.Reporting;

/// <summary>
/// Creates the report writer for the chosen output format.
/// </summary>
internal static class ReportWriterFactory
{
    /// <summary>
    /// Creates a report writer.
    /// </summary>
    /// <param name="settings">The output settings.</param>
    /// <param name="writer">Where to write; the console when null.</param>
    /// <returns>The writer.</returns>
    public static IReportWriter Create(OutputSettings settings, TextWriter? writer = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var output = writer ?? Console.Out;
        return settings.Format switch
        {
            OutputFormat.Json => new JsonReportWriter(output),
            OutputFormat.MsBuild => new MsBuildReportWriter(output, new CiAnnotationWriter(output)),
            _ => new PrettyReportWriter(output, new AnsiPalette(settings.Color), settings.Verbosity, new CiAnnotationWriter(output)),
        };
    }
}
