using System.Globalization;
using LogicApps.Liquid.Cli.Models;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Cli.Reporting;

/// <summary>
/// Writes one line per finding in the MSBuild format <c>file(line,col): error CODE: message</c>, which Visual Studio
/// and VS Code (<c>$msCompile</c> problem matcher) turn into clickable errors and warnings.
/// </summary>
internal sealed class MsBuildReportWriter : IReportWriter
{
    private readonly TextWriter _writer;
    private readonly CiAnnotationWriter _annotations;

    /// <summary>
    /// Creates the writer.
    /// </summary>
    /// <param name="writer">Where to write.</param>
    /// <param name="annotations">Writes the pipeline annotations.</param>
    public MsBuildReportWriter(TextWriter writer, CiAnnotationWriter annotations)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _annotations = annotations ?? throw new ArgumentNullException(nameof(annotations));
    }

    public void WriteDiagnostics(IReadOnlyList<Diagnostic> diagnostics, DiagnosticContext context, bool warningsAsErrors)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Kind is { } kind)
        {
            _writer.WriteLine($"Kind: {kind}{(context.KindInferred ? " (inferred)" : string.Empty)}");
        }

        foreach (var diagnostic in diagnostics)
        {
            var severity = ReportFormatting.SeverityOf(diagnostic, warningsAsErrors);
            var path = ReportFormatting.PathFor(diagnostic.Source, context);
            var location = diagnostic.Line is null
                ? path
                : string.Create(CultureInfo.InvariantCulture, $"{path}({diagnostic.Line},{diagnostic.Column ?? 1})");
            var label = severity switch
            {
                DiagnosticSeverity.Error => "error",
                DiagnosticSeverity.Warning => "warning",
                _ => "info",
            };

            _writer.WriteLine($"{location}: {label} {diagnostic.Code}: {diagnostic.Message}");

            if (diagnostic is { Source: DiagnosticSource.Output, Line: { } line } && ReportFormatting.LineOf(context.RenderedText, line) is { } content)
            {
                _writer.WriteLine($"    {content}");
                _writer.WriteLine($"    {new string(' ', Math.Max(0, Math.Min((diagnostic.Column ?? 1) - 1, content.Length)))}^");
            }

            _annotations.Write(diagnostic, severity, path);
        }
    }

    public void WriteTestResult(TestCaseResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        _writer.WriteLine($"{(result.Passed ? "  PASS" : "  FAIL")}  {result.Name}");
        foreach (var failure in result.Failures)
        {
            _writer.WriteLine($"        {failure.Replace(Environment.NewLine, Environment.NewLine + "        ", StringComparison.Ordinal)}");
        }
    }

    public void WriteMessage(string message)
    {
        _writer.WriteLine(message);
    }

    public void WriteSummary(RunSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var parts = new List<string>();
        if (summary.TestsPassed is { } passed)
        {
            parts.Add($"{passed} passed");
            parts.Add($"{summary.TestsFailed ?? 0} failed");
        }
        else
        {
            if (summary.MapCount is { } maps)
            {
                parts.Add($"{maps} map(s)");
            }

            parts.Add($"{summary.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)} error(s)");
            parts.Add($"{summary.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning)} warning(s)");
        }

        var outcome = summary.Succeeded ? "succeeded" : "failed";
        var note = summary.Note is null ? string.Empty : " " + summary.Note;
        _writer.WriteLine($"{summary.Operation} {outcome}: {string.Join(", ", parts)}.{note}");
    }
}
