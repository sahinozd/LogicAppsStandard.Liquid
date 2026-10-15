using LogicApps.Liquid.Enums;
using Newtonsoft.Json;

namespace LogicApps.Liquid.Cli.Models;

/// <summary>
/// One test case of a test suite: a map, an input and optionally the expected output and expected diagnostics.
/// </summary>
internal sealed record TestCaseDefinition
{
    /// <summary>Name shown in the report. Defaults to the map file name.</summary>
    [JsonProperty("name")]
    public string? Name { get; init; }

    /// <summary>The Liquid map.</summary>
    [JsonProperty("map", Required = Required.Always)]
    public string Map { get; init; } = string.Empty;

    /// <summary>The input message.</summary>
    [JsonProperty("input", Required = Required.Always)]
    public string Input { get; init; } = string.Empty;

    /// <summary>The transform kind. Inferred from the file extensions when omitted.</summary>
    [JsonProperty("kind")]
    public LiquidTransformKind? Kind { get; init; }

    /// <summary>
    /// The expected output. JSON outputs are compared structurally, text outputs line by line (line endings are ignored).
    /// When omitted, the test only checks that the map runs without errors.
    /// </summary>
    [JsonProperty("expectedOutput")]
    public string? ExpectedOutput { get; init; }

    /// <summary>
    /// Diagnostic codes the test expects, for example <c>["LQ021"]</c>. These must be reported, and errors with
    /// these codes do not fail the test. Use this to pin down known Logic Apps behaviour.
    /// </summary>
    [JsonProperty("expectedDiagnostics")]
    public IReadOnlyList<string> ExpectedDiagnostics { get; init; } = [];
}
