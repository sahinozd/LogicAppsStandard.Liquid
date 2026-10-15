using LogicApps.Liquid.Cli.Models;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogicApps.Liquid.Cli.Reporting;

/// <summary>
/// Collects everything and writes one JSON document at the end, for tools and scripts.
/// Pipeline annotations are not written, so the output stays valid JSON.
/// </summary>
internal sealed class JsonReportWriter : IReportWriter
{
    private readonly TextWriter _writer;
    private readonly JArray _files = [];
    private readonly JArray _tests = [];
    private readonly JArray _messages = [];

    /// <summary>
    /// Creates the writer.
    /// </summary>
    /// <param name="writer">Where to write.</param>
    public JsonReportWriter(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public void WriteDiagnostics(IReadOnlyList<Diagnostic> diagnostics, DiagnosticContext context, bool warningsAsErrors)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(context);

        var items = new JArray();
        foreach (var diagnostic in diagnostics)
        {
            items.Add(new JObject
            {
                ["code"] = diagnostic.Code,
                ["severity"] = ReportFormatting.SeverityOf(diagnostic, warningsAsErrors) switch
                {
                    DiagnosticSeverity.Error => "error",
                    DiagnosticSeverity.Warning => "warning",
                    _ => "info",
                },
                ["category"] = DiagnosticCatalog.CategoryOf(diagnostic.Code).ToString(),
                ["shopifyDeviation"] = diagnostic.IsShopifyDeviation,
                ["title"] = diagnostic.Title,
                ["values"] = diagnostic.Values,
                ["explanation"] = diagnostic.Explanation,
                ["fix"] = diagnostic.Fix,
                ["message"] = diagnostic.Message,
                ["file"] = ReportFormatting.PathFor(diagnostic.Source, context),
                ["source"] = diagnostic.Source switch
                {
                    DiagnosticSource.Input => "input",
                    DiagnosticSource.Output => "output",
                    _ => "map",
                },
                ["line"] = diagnostic.Line,
                ["column"] = diagnostic.Column,
                ["outputLine"] = diagnostic.OutputLine,
            });
        }

        _files.Add(new JObject
        {
            ["map"] = context.MapPath,
            ["input"] = context.InputPath,
            ["output"] = context.OutputPath,
            ["kind"] = context.Kind?.ToString(),
            ["diagnostics"] = items,
        });
    }

    public void WriteTestResult(TestCaseResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        _tests.Add(new JObject
        {
            ["name"] = result.Name,
            ["map"] = result.MapPath,
            ["passed"] = result.Passed,
            ["failures"] = new JArray(result.Failures),
            ["durationMs"] = Math.Round(result.Duration.TotalMilliseconds),
        });
    }

    public void WriteMessage(string message)
    {
        _messages.Add(message);
    }

    public void WriteSummary(RunSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var document = new JObject
        {
            ["operation"] = summary.Operation,
            ["succeeded"] = summary.Succeeded,
            ["maps"] = summary.MapCount,
            ["errors"] = summary.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error),
            ["warnings"] = summary.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning),
            ["testsPassed"] = summary.TestsPassed,
            ["testsFailed"] = summary.TestsFailed,
            ["note"] = summary.Note,
            ["files"] = _files,
            ["tests"] = _tests,
            ["messages"] = _messages,
        };
        _writer.WriteLine(document.ToString(Formatting.Indented));
    }
}
