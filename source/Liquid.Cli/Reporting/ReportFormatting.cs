using System.Globalization;
using LogicApps.Liquid.Cli.Models;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Cli.Reporting;

/// <summary>
/// Helpers shared by the report writers.
/// </summary>
internal static class ReportFormatting
{
    /// <summary>
    /// The file a finding refers to.
    /// </summary>
    /// <param name="source">The source of the finding.</param>
    /// <param name="context">The files.</param>
    /// <returns>The path; for the rendered output without an output file, the map path with a remark.</returns>
    public static string PathFor(DiagnosticSource source, DiagnosticContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return source switch
        {
            DiagnosticSource.Input => context.InputPath ?? context.MapPath,
            DiagnosticSource.Output => context.OutputPath ?? $"{context.MapPath} (rendered output)",
            _ => context.MapPath,
        };
    }

    /// <summary>
    /// The text of the file a finding refers to, for excerpts.
    /// </summary>
    /// <param name="source">The source of the finding.</param>
    /// <param name="context">The files.</param>
    /// <returns>The text, or null when unknown.</returns>
    public static string? TextFor(DiagnosticSource source, DiagnosticContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return source switch
        {
            DiagnosticSource.Input => context.InputText,
            DiagnosticSource.Output => context.RenderedText,
            _ => context.MapText,
        };
    }

    /// <summary>
    /// The severity as reported: a warning becomes an error with --warnings-as-errors.
    /// </summary>
    /// <param name="diagnostic">The finding.</param>
    /// <param name="warningsAsErrors">Report warnings as errors.</param>
    /// <returns>The severity.</returns>
    public static DiagnosticSeverity SeverityOf(Diagnostic diagnostic, bool warningsAsErrors)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        return warningsAsErrors && diagnostic.Severity == DiagnosticSeverity.Warning ? DiagnosticSeverity.Error : diagnostic.Severity;
    }

    /// <summary>
    /// One line of a text, without the line break.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="line">1-based line number.</param>
    /// <returns>The line, or null when out of range.</returns>
    public static string? LineOf(string? text, int line)
    {
        if (text is null || line < 1)
        {
            return null;
        }

        var lines = text.Split('\n');
        return line > lines.Length ? null : lines[line - 1].TrimEnd('\r');
    }

    /// <summary>
    /// A count with a singular or plural noun, for example <c>1 error</c> or <c>3 errors</c>.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <param name="noun">The singular noun.</param>
    /// <returns>The text.</returns>
    public static string Plural(int count, string noun)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");
    }
}
