using Newtonsoft.Json;

namespace LogicApps.Liquid.Cli.Models;

/// <summary>
/// The test suite file (for example <c>liquid-tests.json</c>). Paths are relative to the file itself.
/// </summary>
internal sealed record TestSuiteDefinition
{
    /// <summary>Culture of the Logic Apps host. Defaults to en-US. The <c>--culture</c> option overrides it.</summary>
    [JsonProperty("culture")]
    public string? Culture { get; init; }

    /// <summary>Time zone of the Logic Apps host. Defaults to UTC. The <c>--time-zone</c> option overrides it.</summary>
    [JsonProperty("timeZone")]
    public string? TimeZone { get; init; }

    /// <summary>Treat warnings as errors for all tests.</summary>
    [JsonProperty("warningsAsErrors")]
    public bool WarningsAsErrors { get; init; }

    /// <summary>The test cases.</summary>
    [JsonProperty("tests")]
    public IReadOnlyList<TestCaseDefinition> Tests { get; init; } = [];
}
