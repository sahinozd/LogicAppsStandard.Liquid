using System.Globalization;
using LogicApps.Liquid.Cli.Models;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Cli.Reporting;

/// <summary>
/// Writes diagnostics in the MSBuild format <c>file(line,col): error CODE: message</c>, which Visual Studio,
/// VS Code (<c>$msCompile</c> problem matcher) and Azure DevOps recognise. When running in Azure DevOps
/// (<c>TF_BUILD</c> is set), each finding is also logged with <c>##vso[task.logissue]</c> so it shows up on the run summary.
/// </summary>
internal sealed class DiagnosticReporter
{
    private readonly TextWriter _writer;
    private readonly bool _azureDevOps;

    /// <summary>
    /// Creates a reporter.
    /// </summary>
    /// <param name="writer">Where to write the diagnostics.</param>
    /// <param name="azureDevOps">Also write Azure DevOps logging commands. Detected from <c>TF_BUILD</c> when null.</param>
    public DiagnosticReporter(TextWriter writer, bool? azureDevOps = null)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _azureDevOps = azureDevOps ?? string.Equals(Environment.GetEnvironmentVariable("TF_BUILD"), "True", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Writes the diagnostics.
    /// </summary>
    /// <param name="diagnostics">The findings.</param>
    /// <param name="context">The files the findings refer to.</param>
    /// <param name="warningsAsErrors">Report warnings as errors.</param>
    public void Report(IEnumerable<Diagnostic> diagnostics, DiagnosticContext context, bool warningsAsErrors)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var diagnostic in diagnostics)
        {
            var severity = warningsAsErrors && diagnostic.Severity == DiagnosticSeverity.Warning ? DiagnosticSeverity.Error : diagnostic.Severity;
            var path = PathFor(diagnostic.Source, context);
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

            if (diagnostic.Source == DiagnosticSource.Output && diagnostic.Line is { } line && context.RenderedText is not null)
            {
                WriteExcerpt(context.RenderedText, line, diagnostic.Column ?? 1);
            }

            if (_azureDevOps && severity != DiagnosticSeverity.Info)
            {
                _writer.WriteLine(AzureDevOpsIssue(diagnostic, severity, path));
            }
        }
    }

    private void WriteExcerpt(string text, int line, int column)
    {
        var lines = text.Split('\n');
        if (line < 1 || line > lines.Length)
        {
            return;
        }

        var content = lines[line - 1].TrimEnd('\r');
        _writer.WriteLine($"    {content}");
        _writer.WriteLine($"    {new string(' ', Math.Max(0, Math.Min(column - 1, content.Length)))}^");
    }

    private static string PathFor(DiagnosticSource source, DiagnosticContext context)
    {
        return source switch
        {
            DiagnosticSource.Input => context.InputPath ?? context.MapPath,
            DiagnosticSource.Output => context.OutputPath ?? $"{context.MapPath} (rendered output)",
            _ => context.MapPath,
        };
    }

    private static string AzureDevOpsIssue(Diagnostic diagnostic, DiagnosticSeverity severity, string path)
    {
        var type = severity == DiagnosticSeverity.Error ? "error" : "warning";
        var properties = $"type={type};sourcepath={path};code={diagnostic.Code}";
        if (diagnostic.Line is { } line)
        {
            properties += string.Create(CultureInfo.InvariantCulture, $";linenumber={line};columnnumber={diagnostic.Column ?? 1}");
        }

        var message = diagnostic.Message.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        return $"##vso[task.logissue {properties}]{diagnostic.Code}: {message}";
    }
}
