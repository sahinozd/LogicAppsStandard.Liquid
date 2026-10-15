using System.Text.RegularExpressions;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Linting;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Analysis;

/// <summary>
/// Merges findings that describe the same problem from two sides, so each problem is reported once:
/// a render error in the output (LQ040) and the filter call in the map that caused it, and a missing value (LQ023)
/// and the dot path with a special character (LQ025) that caused it.
/// </summary>
internal static class DiagnosticMerger
{
    private static readonly Regex _pathSegmentPattern = new(@"^'\.(?<segment>[^']+)'");

    private static readonly HashSet<string> _syntaxCodes =
        [DiagnosticCodes.TemplateSyntaxError, DiagnosticCodes.UnknownTag, DiagnosticCodes.UnbalancedBlock, DiagnosticCodes.UnterminatedTag];

    // Render error text written by DotLiquid → the map finding that explains it.
    private static readonly (string ErrorText, string Code)[] _renderErrorCauses =
    [
        ("Parameter count mismatch", DiagnosticCodes.MathOnText),
        ("(Parameter 'count')", DiagnosticCodes.TimesRepeatsText),
        ("does not allow includes", DiagnosticCodes.IncludeNotSupported),
    ];

    /// <summary>
    /// Merges related findings.
    /// </summary>
    /// <param name="diagnostics">All findings of one transform.</param>
    /// <param name="map">The Liquid map, to locate missing values.</param>
    /// <returns>The findings, with related findings merged into the one located in the map.</returns>
    public static List<Diagnostic> Merge(List<Diagnostic> diagnostics, string map)
    {
        var result = new List<Diagnostic>(diagnostics);
        MergeParseError(result);
        MergeRenderErrors(result);
        MergeMissingValues(result);
        LocateMissingValues(result, map);
        return result;
    }

    // DotLiquid does not tell where a missing value is used; the first use of the path in the map is a good guess.
    private static void LocateMissingValues(List<Diagnostic> diagnostics, string map)
    {
        var position = new TextPosition(map);
        for (var i = 0; i < diagnostics.Count; i++)
        {
            var variable = diagnostics[i].Code == DiagnosticCodes.MissingValue && diagnostics[i].Line is null ? MissingVariable(diagnostics[i]) : null;
            var index = variable is null ? -1 : map.IndexOf(variable, StringComparison.Ordinal);
            if (index >= 0)
            {
                var (line, column) = position.At(index);
                diagnostics[i] = diagnostics[i] with { Line = line, Column = column };
            }
        }
    }

    // DotLiquid reports a parse error without location; the static checks usually found the same problem with its location.
    private static void MergeParseError(List<Diagnostic> diagnostics)
    {
        var parseError = diagnostics.FirstOrDefault(d => d.Code == DiagnosticCodes.TemplateSyntaxError && d.Line is null);
        var located = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error && d.Source == DiagnosticSource.Map && d.Line is not null && _syntaxCodes.Contains(d.Code))
            .OrderBy(d => d.Line)
            .ThenBy(d => d.Column)
            .FirstOrDefault();
        if (parseError is null || located is null)
        {
            return;
        }

        diagnostics[diagnostics.IndexOf(located)] = located with { Values = $"Logic Apps reports: '{parseError.Values}'" };
        diagnostics.Remove(parseError);
    }

    // The render error shows where the output is wrong, the map finding where to fix it: keep the map location,
    // make it an error, and remember the output line.
    private static void MergeRenderErrors(List<Diagnostic> diagnostics)
    {
        foreach (var (errorText, code) in _renderErrorCauses)
        {
            var renderErrors = diagnostics
                .Where(d => d.Code == DiagnosticCodes.RenderError && d.Source == DiagnosticSource.Output && d.Values?.Contains(errorText, StringComparison.Ordinal) == true)
                .ToList();
            var causes = diagnostics.Where(d => d.Code == code && d.Source == DiagnosticSource.Map).OrderBy(d => d.Line).ThenBy(d => d.Column).ToList();
            if (renderErrors.Count == 0 || causes.Count == 0)
            {
                continue;
            }

            // In order of appearance; a cause in a loop can produce more errors than there are causes, those go to the last cause.
            var errorsPerCause = renderErrors
                .Select((error, index) => (Error: error, Cause: causes[Math.Min(index, causes.Count - 1)]))
                .GroupBy(pair => pair.Cause, pair => pair.Error);
            foreach (var group in errorsPerCause)
            {
                var errors = group.ToList();
                var times = errors.Count == 1 ? string.Empty : $" ({errors.Count} times)";
                var merged = group.Key with
                {
                    Severity = DiagnosticSeverity.Error,
                    OutputLine = errors[0].Line,
                    Explanation = $"{group.Key.Explanation} Logic Apps writes 'Liquid error: {errors[0].Values}' into the output{times}.".TrimStart(),
                };
                diagnostics[diagnostics.IndexOf(group.Key)] = merged;
                diagnostics.RemoveAll(errors.Contains);
            }
        }
    }

    // '.@code' reads 'code' instead, which is then reported as missing: report it once, at the path in the map.
    private static void MergeMissingValues(List<Diagnostic> diagnostics)
    {
        var paths = diagnostics.Where(d => d.Code == DiagnosticCodes.SpecialCharacterInPath).ToList();
        foreach (var path in paths)
        {
            var match = _pathSegmentPattern.Match(path.Title);
            if (!match.Success)
            {
                continue;
            }

            var segment = match.Groups["segment"].Value;
            var readsInstead = segment.TrimStart('@', '$', '#').Split(':')[0];
            var missing = diagnostics.FirstOrDefault(d => d.Code == DiagnosticCodes.MissingValue
                && MissingVariable(d)?.Split('.', '[').Select(s => s.TrimStart('@', '$', '#').Split(':')[0]).Contains(readsInstead) == true);
            if (missing is null)
            {
                continue;
            }

            diagnostics[diagnostics.IndexOf(path)] = path with { Values = $"'{MissingVariable(missing)}' was not found and renders as empty" };
            diagnostics.Remove(missing);
        }
    }

    private static string? MissingVariable(Diagnostic diagnostic)
    {
        var title = diagnostic.Title;
        var end = title.IndexOf("' was not found", StringComparison.Ordinal);
        return title.StartsWith('\'') && end > 0 ? title[1..end] : null;
    }
}
