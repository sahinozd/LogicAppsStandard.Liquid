using System.Diagnostics;
using System.Globalization;
using System.Text;
using LogicApps.Liquid.Cli.Commands;
using LogicApps.Liquid.Cli.Helpers;
using LogicApps.Liquid.Cli.Models;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Extensions;
using LogicApps.Liquid.Models;
using LogicApps.Liquid.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogicApps.Liquid.Cli.Testing;

/// <summary>
/// Runs the cases of a <see cref="TestSuiteDefinition"/>.
/// </summary>
internal sealed class TestRunner
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly ILiquidTransformer _transformer;
    private readonly string _baseDirectory;
    private readonly bool _warningsAsErrors;
    private readonly bool _updateExpected;

    /// <summary>
    /// Creates a test runner.
    /// </summary>
    /// <param name="transformer">The transformer that runs the maps.</param>
    /// <param name="baseDirectory">The folder the paths in the test cases are relative to.</param>
    /// <param name="warningsAsErrors">Treat warnings as failures.</param>
    /// <param name="updateExpected">Write actual outputs to the expected output files instead of comparing.</param>
    public TestRunner(ILiquidTransformer transformer, string baseDirectory, bool warningsAsErrors, bool updateExpected)
    {
        _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
        _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        _warningsAsErrors = warningsAsErrors;
        _updateExpected = updateExpected;
    }

    /// <summary>
    /// Runs one test case.
    /// </summary>
    /// <param name="test">The test case.</param>
    /// <returns>The outcome, and the findings of the transform with the files they refer to.</returns>
    public (TestCaseResult Result, IReadOnlyList<Diagnostic> Diagnostics, DiagnosticContext? Context) Run(TestCaseDefinition test)
    {
        ArgumentNullException.ThrowIfNull(test);

        var stopwatch = Stopwatch.StartNew();
        var mapPath = Path.GetFullPath(Path.Combine(_baseDirectory, test.Map));
        var inputPath = Path.GetFullPath(Path.Combine(_baseDirectory, test.Input));
        var expectedPath = test.ExpectedOutput is null ? null : Path.GetFullPath(Path.Combine(_baseDirectory, test.ExpectedOutput));
        var name = test.Name ?? Path.GetFileNameWithoutExtension(test.Map);
        var failures = new List<string>();

        string map;
        string input;
        try
        {
            map = File.ReadAllText(mapPath);
            input = File.ReadAllText(inputPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failures.Add($"Cannot read test files: {ex.Message}");
            return (new TestCaseResult(name, mapPath, failures, stopwatch.Elapsed), [], null);
        }

        var kind = KindResolver.Resolve(test.Kind, inputPath, expectedPath);
        var result = _transformer.Transform(map, input, kind);
        var context = new DiagnosticContext(mapPath, inputPath, null, result.RenderedText) { MapText = map, InputText = input, Kind = kind };

        CheckDiagnostics(test, result, failures);

        if (expectedPath is not null && result.RenderedText is not null)
        {
            CompareOutput(result, expectedPath, failures);
        }

        return (new TestCaseResult(name, mapPath, failures, stopwatch.Elapsed), result.Diagnostics, context);
    }

    private void CheckDiagnostics(TestCaseDefinition test, LiquidTransformResult result, List<string> failures)
    {
        var expected = test.ExpectedDiagnostics.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var code in expected.Where(code => !result.Diagnostics.Any(d => d.Code.Equals(code, StringComparison.OrdinalIgnoreCase))))
        {
            failures.Add($"Expected diagnostic {code} was not reported.");
        }

        foreach (var diagnostic in result.Diagnostics.Where(d => !expected.Contains(d.Code)))
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error || (_warningsAsErrors && diagnostic.Severity == DiagnosticSeverity.Warning))
            {
                failures.Add($"{diagnostic.Code}: {diagnostic.Title}{(diagnostic.Line is null ? string.Empty : $" (line {diagnostic.Line})")}");
            }
        }
    }

    private void CompareOutput(LiquidTransformResult result, string expectedPath, List<string> failures)
    {
        var actual = TransformCommand.FormatOutput(result, raw: false);
        if (_updateExpected)
        {
            File.WriteAllText(expectedPath, actual, Utf8NoBom);
            return;
        }

        if (!File.Exists(expectedPath))
        {
            failures.Add($"Expected output '{expectedPath}' does not exist. Run with --update-expected to create it.");
            return;
        }

        var expected = File.ReadAllText(expectedPath);
        var difference = result.Kind.HasJsonOutput() && result.Output is not null
            ? CompareJson(expected, result.Output)
            : CompareText(expected, actual);
        if (difference is not null)
        {
            failures.Add(difference);
        }
    }

    private static string? CompareJson(string expectedText, JToken actual)
    {
        JToken expected;
        try
        {
            expected = RuntimeJson.Parse(expectedText);
        }
        catch (JsonReaderException ex)
        {
            return $"The expected output is not valid JSON: {ex.Message}";
        }

        if (JToken.DeepEquals(expected, actual))
        {
            return null;
        }

        return CompareText(expected.ToString(Formatting.Indented), actual.ToString(Formatting.Indented))
            ?? "The output differs from the expected output.";
    }

    private static string? CompareText(string expected, string actual)
    {
        var expectedLines = SplitLines(expected);
        var actualLines = SplitLines(actual);
        var count = Math.Max(expectedLines.Length, actualLines.Length);
        for (var i = 0; i < count; i++)
        {
            var expectedLine = i < expectedLines.Length ? expectedLines[i] : "<end of output>";
            var actualLine = i < actualLines.Length ? actualLines[i] : "<end of output>";
            if (!string.Equals(expectedLine, actualLine, StringComparison.Ordinal))
            {
                return string.Create(CultureInfo.InvariantCulture, $"Output differs at line {i + 1}.{Environment.NewLine}  expected: {expectedLine}{Environment.NewLine}  actual:   {actualLine}");
            }
        }

        return null;
    }

    private static string[] SplitLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
    }
}
