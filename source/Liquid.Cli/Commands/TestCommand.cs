using System.CommandLine;
using LogicApps.Liquid.Cli.Constants;
using LogicApps.Liquid.Cli.Models;
using LogicApps.Liquid.Cli.Reporting;
using LogicApps.Liquid.Cli.Testing;
using LogicApps.Liquid.Constants;
using Newtonsoft.Json;

namespace LogicApps.Liquid.Cli.Commands;

/// <summary>
/// <c>la-liquid test --suite liquid-tests.json [--junit results.xml]</c>: runs a suite of map/input/expected-output cases.
/// Designed for pipelines: the JUnit report can be published with <c>PublishTestResults@2</c>.
/// </summary>
internal static class TestCommand
{
    public static Command Create()
    {
        var suite = new Option<string>("--suite", "-s")
        {
            Description = "The test suite file (JSON). Paths in it are relative to the file.",
            Required = true,
        };
        var junit = new Option<string?>("--junit")
        {
            Description = "Write a JUnit XML report to this path (for the Azure DevOps PublishTestResults task).",
        };
        var updateExpected = new Option<bool>("--update-expected")
        {
            Description = "Write the actual outputs to the expected output files instead of comparing. Review the changes before committing them.",
        };
        var warningsAsErrors = CommonOptions.WarningsAsErrors();
        var engine = new EngineOptionSet(cultureDefault: null);

        var command = new Command("test", "Run a suite of Liquid map tests with expected outputs. --culture and --time-zone override the suite file.")
        {
            suite, junit, updateExpected, warningsAsErrors,
        };
        engine.AddTo(command);

        command.SetAction(parseResult => Run(
            parseResult.GetValue(suite)!,
            parseResult.GetValue(junit),
            parseResult.GetValue(updateExpected),
            parseResult.GetValue(warningsAsErrors),
            suiteDefinition => engine.Read(parseResult, suiteDefinition.Culture, suiteDefinition.TimeZone)));

        return command;
    }

    private static int Run(string suitePath, string? junitPath, bool updateExpected, bool warningsAsErrorsOption, Func<TestSuiteDefinition, EngineSettings> readSettings)
    {
        var text = CommonOptions.ReadFile(suitePath, "test suite");
        if (text is null)
        {
            return ExitCodes.UsageError;
        }

        TestSuiteDefinition? definition;
        try
        {
            definition = JsonConvert.DeserializeObject<TestSuiteDefinition>(text);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"error: invalid test suite '{suitePath}': {ex.Message}");
            return ExitCodes.UsageError;
        }

        if (definition is null || definition.Tests.Count == 0)
        {
            Console.Error.WriteLine($"error: the test suite '{suitePath}' contains no tests.");
            return ExitCodes.UsageError;
        }

        // The command line overrides the suite file, the suite file overrides the defaults.
        var transformer = CommonOptions.CreateTransformer(readSettings(definition));
        if (transformer is null)
        {
            return ExitCodes.UsageError;
        }

        var warningsAsErrors = warningsAsErrorsOption || definition.WarningsAsErrors;
        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(suitePath)) ?? Directory.GetCurrentDirectory();
        var runner = new TestRunner(transformer, new DiagnosticReporter(Console.Out), baseDirectory, warningsAsErrors, updateExpected);

        var results = new List<TestCaseResult>();
        foreach (var test in definition.Tests)
        {
            var result = runner.Run(test);
            results.Add(result);
            Console.WriteLine($"{(result.Passed ? "  PASS" : "  FAIL")}  {result.Name}");
            foreach (var failure in result.Failures)
            {
                Console.WriteLine($"        {failure.Replace(Environment.NewLine, Environment.NewLine + "        ", StringComparison.Ordinal)}");
            }
        }

        if (junitPath is not null)
        {
            JUnitReportWriter.Write(junitPath, Path.GetFileNameWithoutExtension(suitePath), results);
            Console.WriteLine($"JUnit report written to '{junitPath}'.");
        }

        var failed = results.Count(r => !r.Passed);
        Console.WriteLine($"{(failed == 0 ? "Tests passed" : "Tests failed")}: {results.Count - failed} passed, {failed} failed.");
        if (updateExpected)
        {
            Console.WriteLine("Expected outputs were updated. Review the changes before committing them.");
        }

        return failed == 0 ? ExitCodes.Success : ExitCodes.Failed;
    }
}
