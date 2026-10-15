using System.Globalization;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;
using LogicApps.Liquid.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LogicApps.Liquid.ConformanceTests;

/// <summary>
/// Runs every case through the real Logic Apps engine and through the tool, and requires identical results:
/// the same output, or a failure in both. Ignored when no workflow extension bundle is installed.
/// </summary>
[TestFixture]
[Category("Conformance")]
public sealed class RuntimeConformanceTests
{
    private static readonly Lazy<RuntimeLiquidEngine?> _runtime = new(RuntimeLiquidEngine.TryLoad);
    private static readonly string[] _cultures = ["en-US", "nl-NL"];

    private static IEnumerable<TestCaseData> Cases()
    {
        return from culture in _cultures
               from conformanceCase in ConformanceCases.All()
               select new TestCaseData(conformanceCase, culture).SetName($"{conformanceCase.Name} [{culture}]");
    }

    [TestCaseSource(nameof(Cases))]
    public void Transform_Should_Match_Logic_Apps_Runtime_When_Running_Same_Case(ConformanceCase conformanceCase, string cultureName)
    {
        ArgumentNullException.ThrowIfNull(conformanceCase);

        // Arrange
        var runtime = _runtime.Value;
        if (runtime is null)
        {
            Assert.Ignore($"No Logic Apps workflow extension bundle found. Install the Azure Logic Apps (Standard) VS Code extension, or set {RuntimeLiquidEngine.BundlePathVariable}.");
        }

        var culture = CultureInfo.GetCultureInfo(cultureName);
        var transformer = new LiquidTransformer(new LiquidTransformOptions { Culture = culture });

        // Act
        RuntimeOutcome expected;
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            // The tool emulates the time zone of Logic Apps in Azure (UTC); the runtime runs in this process, so it gets the same.
            using var timeZone = new TimeZoneScope(TimeZoneInfo.Utc);
            expected = runtime.Transform(conformanceCase.Map, conformanceCase.Input, conformanceCase.Kind);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }

        var actual = transformer.Transform(conformanceCase.Map, conformanceCase.Input, conformanceCase.Kind);

        TestContext.Out.WriteLine($"Runtime ({runtime.BinPath}): {expected.Output ?? "FAILED: " + expected.Error}");
        TestContext.Out.WriteLine($"Tool: {(actual.Output is null ? "FAILED" : actual.Output.ToString(Formatting.None))}");
        foreach (var diagnostic in actual.Diagnostics)
        {
            TestContext.Out.WriteLine($"  {diagnostic}");
        }

        // Assert
        if (expected.Error is not null)
        {
            Assert.That(actual.Output, Is.Null, $"Logic Apps fails with '{expected.Error}', but the tool produced output.");
            Assert.That(actual.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), Is.True, "The tool must report an error when Logic Apps fails.");
            return;
        }

        Assert.That(actual.Output, Is.Not.Null, "Logic Apps succeeds, but the tool could not produce output.");
        Assert.That(actual.Output!.ToString(Formatting.None), Is.EqualTo(expected.Output));
    }
}
