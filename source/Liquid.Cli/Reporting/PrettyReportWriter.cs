using System.Globalization;
using System.Text.RegularExpressions;
using LogicApps.Liquid.Cli.Enums;
using LogicApps.Liquid.Cli.Models;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Cli.Reporting;

/// <summary>
/// Writes findings for people: grouped per file and sorted by line, with a source excerpt that points at the problem,
/// the actual values and the fix, and a summary per category. Colors are optional.
/// </summary>
internal sealed class PrettyReportWriter : IReportWriter
{
    private const int MaxExcerptWidth = 110;
    private const int ExcerptContext = 40;
    private const int TabWidth = 4;

    // The word at the column: a name, a path such as 'content.order.number' or '.@code', or else a single character.
    private static readonly Regex _wordPattern = new(@"^\.?[@$#]?\w+(?:[.:][@$#]?\w+)*");

    private static readonly Dictionary<DiagnosticCategory, string> _categoryNames = new()
    {
        [DiagnosticCategory.Syntax] = "syntax & tags",
        [DiagnosticCategory.Filters] = "filters",
        [DiagnosticCategory.ValuesAndPaths] = "values & paths",
        [DiagnosticCategory.Conditions] = "conditions & loops",
        [DiagnosticCategory.Input] = "input",
        [DiagnosticCategory.Output] = "output",
        [DiagnosticCategory.ShopifyDeviation] = "differs from Shopify",
    };

    private readonly TextWriter _writer;
    private readonly AnsiPalette _palette;
    private readonly OutputVerbosity _verbosity;
    private readonly CiAnnotationWriter _annotations;
    private bool _wroteFile;

    /// <summary>
    /// Creates the writer.
    /// </summary>
    /// <param name="writer">Where to write.</param>
    /// <param name="palette">The colors.</param>
    /// <param name="verbosity">How much to show per finding.</param>
    /// <param name="annotations">Writes the pipeline annotations.</param>
    public PrettyReportWriter(TextWriter writer, AnsiPalette palette, OutputVerbosity verbosity, CiAnnotationWriter annotations)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _palette = palette ?? throw new ArgumentNullException(nameof(palette));
        _verbosity = verbosity;
        _annotations = annotations ?? throw new ArgumentNullException(nameof(annotations));
    }

    public void WriteDiagnostics(IReadOnlyList<Diagnostic> diagnostics, DiagnosticContext context, bool warningsAsErrors)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(context);

        if (_wroteFile && _verbosity != OutputVerbosity.Minimal)
        {
            _writer.WriteLine();
        }

        _wroteFile = true;
        WriteFileHeader(diagnostics, context);

        foreach (var diagnostic in diagnostics.OrderBy(d => d.Source).ThenBy(d => d.Line ?? 0).ThenBy(d => d.Column ?? 0))
        {
            var severity = ReportFormatting.SeverityOf(diagnostic, warningsAsErrors);
            var path = ReportFormatting.PathFor(diagnostic.Source, context);
            if (_verbosity == OutputVerbosity.Minimal)
            {
                WriteOneLine(diagnostic, severity, path);
            }
            else
            {
                WriteFinding(diagnostic, severity, path, context);
            }

            _annotations.Write(diagnostic, severity, path);
        }
    }

    public void WriteTestResult(TestCaseResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var label = result.Passed ? _palette.Green("✔ PASS") : _palette.Red("✖ FAIL");
        var duration = _palette.Dim(string.Create(CultureInfo.InvariantCulture, $"({result.Duration.TotalMilliseconds:0} ms)"));
        _writer.WriteLine($"{label}  {result.Name} {duration}");
        foreach (var failure in result.Failures)
        {
            _writer.WriteLine($"         {failure.Replace(Environment.NewLine, Environment.NewLine + "         ", StringComparison.Ordinal)}");
        }

        if (!result.Passed)
        {
            _writer.WriteLine();
        }
    }

    public void WriteMessage(string message)
    {
        _writer.WriteLine(_palette.Dim(message));
    }

    public void WriteSummary(RunSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var errors = summary.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        var warnings = summary.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
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
                parts.Add(ReportFormatting.Plural(maps, "map"));
            }

            parts.Add(ReportFormatting.Plural(errors, "error"));
            parts.Add(ReportFormatting.Plural(warnings, "warning"));
        }

        _writer.WriteLine();
        var outcome = summary.Succeeded
            ? _palette.Green($"✔ {summary.Operation} succeeded")
            : _palette.Red($"✖ {summary.Operation} failed");
        _writer.WriteLine($"{outcome}  {string.Join(_palette.Dim(" · "), parts)}");

        if (_verbosity != OutputVerbosity.Minimal)
        {
            WriteCategories(summary.Diagnostics);
        }

        if (summary.Note is not null)
        {
            _writer.WriteLine($"  {summary.Note}");
        }

        var example = summary.Diagnostics.OrderByDescending(d => d.Severity).Select(d => d.Code).FirstOrDefault();
        if (example is not null && _verbosity != OutputVerbosity.Minimal)
        {
            _writer.WriteLine(_palette.Dim($"  More about a code: la-liquid explain {example}"));
        }
    }

    private void WriteFileHeader(IReadOnlyList<Diagnostic> diagnostics, DiagnosticContext context)
    {
        var kind = context.Kind is { } value ? _palette.Dim($"  {value}{(context.KindInferred ? " (inferred)" : string.Empty)}") : string.Empty;
        var status = diagnostics.Count == 0 ? "  " + _palette.Green("✔ no findings") : string.Empty;
        _writer.WriteLine($"{_palette.Bold(context.MapPath)}{kind}{status}");
    }

    private void WriteOneLine(Diagnostic diagnostic, DiagnosticSeverity severity, string path)
    {
        var values = diagnostic.Values is null ? string.Empty : _palette.Dim($" ({diagnostic.Values})");
        _writer.WriteLine($"  {Label(diagnostic, severity)} {_palette.Dim(diagnostic.Code)}  {Location(path, diagnostic)}  {diagnostic.Title}{values}");
    }

    private void WriteFinding(Diagnostic diagnostic, DiagnosticSeverity severity, string path, DiagnosticContext context)
    {
        _writer.WriteLine();
        _writer.WriteLine($"  {Label(diagnostic, severity)} {_palette.Dim(diagnostic.Code)}  {_palette.Bold(diagnostic.Title)}");
        _writer.WriteLine($"     {_palette.Dim("at")} {Location(path, diagnostic)}");

        if (diagnostic.Line is { } line)
        {
            WriteExcerpt(ReportFormatting.TextFor(diagnostic.Source, context), line, diagnostic.Column ?? 1, Paint(diagnostic, severity));
        }

        WriteField("values", diagnostic.Values);
        if (diagnostic.OutputLine is { } outputLine)
        {
            var rendered = ReportFormatting.LineOf(context.RenderedText, outputLine)?.Trim();
            WriteField("output", string.Create(CultureInfo.InvariantCulture, $"line {outputLine}{(rendered is null ? string.Empty : ": " + rendered)}"));
        }

        if (_verbosity == OutputVerbosity.Detailed)
        {
            WriteField("why", diagnostic.Explanation);
        }

        if (diagnostic.Fix is not null)
        {
            _writer.WriteLine($"     {_palette.Green("fix")}    {diagnostic.Fix}");
        }
    }

    private void WriteField(string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _writer.WriteLine($"     {_palette.Dim(name.PadRight(6))} {value}");
        }
    }

    // Shows the line with a marker under the problem. Tabs are expanded, and a long line is cut around the column.
    private void WriteExcerpt(string? text, int line, int column, Func<string, string> paintMarker)
    {
        var content = ReportFormatting.LineOf(text, line);
        if (content is null)
        {
            return;
        }

        var index = Math.Clamp(column - 1, 0, content.Length);
        var word = index < content.Length ? _wordPattern.Match(content[index..]) : Match.Empty;
        var markerLength = word.Success ? word.Length : 1;

        var before = content[..index].Replace("\t", new string(' ', TabWidth), StringComparison.Ordinal);
        var rest = content[index..].Replace("\t", new string(' ', TabWidth), StringComparison.Ordinal);
        if (before.Length > ExcerptContext && before.Length + rest.Length > MaxExcerptWidth)
        {
            before = "…" + before[^ExcerptContext..];
        }

        var shown = before + rest;
        if (shown.Length > MaxExcerptWidth)
        {
            shown = shown[..MaxExcerptWidth] + "…";
        }

        var number = line.ToString(CultureInfo.InvariantCulture);
        var gutter = new string(' ', number.Length);
        _writer.WriteLine($"     {_palette.Dim($"{number} │")} {shown}");
        _writer.WriteLine($"     {_palette.Dim($"{gutter} │")} {new string(' ', before.Length)}{paintMarker(new string('^', markerLength))}");
    }

    private void WriteCategories(IReadOnlyList<Diagnostic> diagnostics)
    {
        foreach (var group in diagnostics.GroupBy(d => DiagnosticCatalog.CategoryOf(d.Code)).OrderBy(g => g.Key))
        {
            var errors = group.Count(d => d.Severity == DiagnosticSeverity.Error);
            var warnings = group.Count(d => d.Severity != DiagnosticSeverity.Error);
            var counts = new List<string>();
            if (errors > 0)
            {
                counts.Add(_palette.Red(ReportFormatting.Plural(errors, "error")));
            }

            if (warnings > 0)
            {
                counts.Add(group.Key == DiagnosticCategory.ShopifyDeviation
                    ? _palette.Magenta(ReportFormatting.Plural(warnings, "warning"))
                    : _palette.Yellow(ReportFormatting.Plural(warnings, "warning")));
            }

            var codes = string.Join(", ", group.Select(d => d.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            _writer.WriteLine($"  {_categoryNames[group.Key],-22}{string.Join(", ", counts)}  {_palette.Dim(codes)}");
        }
    }

    private string Label(Diagnostic diagnostic, DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Error => _palette.Red("✖ error  "),
            DiagnosticSeverity.Warning when diagnostic.IsShopifyDeviation => _palette.Magenta("≠ differs"),
            DiagnosticSeverity.Warning => _palette.Yellow("⚠ warning"),
            _ => _palette.Cyan("ℹ info   "),
        };
    }

    private Func<string, string> Paint(Diagnostic diagnostic, DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Error => _palette.Red,
            DiagnosticSeverity.Warning when diagnostic.IsShopifyDeviation => _palette.Magenta,
            DiagnosticSeverity.Warning => _palette.Yellow,
            _ => _palette.Cyan,
        };
    }

    // path:line:col, which terminals such as VS Code make clickable.
    private string Location(string path, Diagnostic diagnostic)
    {
        var position = diagnostic.Line is { } line
            ? string.Create(CultureInfo.InvariantCulture, $":{line}:{diagnostic.Column ?? 1}")
            : string.Empty;
        return _palette.Cyan(path + position);
    }
}
