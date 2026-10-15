# la-liquid usage guide

This guide explains how to install `la-liquid` and use every part of it:
- the three commands
- the engine options
- test suites
- pipelines in Azure DevOps and GitHub Actions
- VS Code and Visual Studio
- the .NET library

For background on *why* Logic Apps Liquid behaves the way it does, see the [README](README.md).

---

## Table of contents

1. [Installing](#1-installing)
2. [Quick start](#2-quick-start)
3. [Choosing the transform kind](#3-choosing-the-transform-kind)
4. [validate: check maps](#4-validate-check-maps)
5. [transform: run a map](#5-transform-run-a-map)
6. [test: test suites with expected outputs](#6-test-test-suites-with-expected-outputs)
7. [Engine options: culture, time zone and checks](#7-engine-options-culture-time-zone-and-checks)
8. [Reading the findings](#8-reading-the-findings)
9. [Azure DevOps](#9-azure-devops)
10. [GitHub Actions](#10-github-actions)
11. [VS Code and Visual Studio](#11-vs-code-and-visual-studio)
12. [Using the .NET library](#12-using-the-net-library)
13. [Recommended way of working](#13-recommended-way-of-working)
14. [Troubleshooting](#14-troubleshooting)

---

## 1. Installing

There are four ways to get `la-liquid`. Use whichever suits you.

### As a .NET tool from NuGet (recommended)

This needs the .NET 10 SDK or runtime.

```powershell
dotnet tool install --global LogicApps.Liquid.Cli
la-liquid --version
```

- **Update:** `dotnet tool update --global LogicApps.Liquid.Cli`
- **Specific version:** add `--version 1.2.3`.
- **Per repository:** run `dotnet new tool-manifest` once in the repository, then `dotnet tool install LogicApps.Liquid.Cli`. Everyone who clones the repository runs `dotnet tool restore` and gets the same version.

### As an executable from GitHub Releases (no .NET needed)

1. Open the [Releases page](https://github.com/sahinozd/LogicAppsStandard.Liquid/releases).
2. Download the archive for your platform: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` or `osx-arm64`.
3. Unpack it and run `la-liquid` (`la-liquid.exe` on Windows). Optionally add the folder to your `PATH`.

`SHA256SUMS.txt` in the release has the checksum of every file.

### From GitHub Packages

Every release is also published to GitHub Packages. GitHub Packages needs authentication, even for public packages: use a personal access token with the `read:packages` scope.

```powershell
dotnet nuget add source https://nuget.pkg.github.com/sahinozd/index.json --name sahinozd --username <your-github-user> --password <token>
dotnet tool install --global LogicApps.Liquid.Cli --add-source sahinozd
```

### From source

```powershell
git clone https://github.com/sahinozd/LogicAppsStandard.Liquid.git
cd LogicAppsStandard.Liquid
dotnet run --project source/Liquid.Cli -- validate --map samples/maps
```

---

## 2. Quick start

```powershell
# 1. Check all maps in a folder (no input needed)
la-liquid validate --map ./Artifacts/Maps

# 2. Run one map with a sample message and write the output
la-liquid transform --map ./Artifacts/Maps/orders.liquid --input ./samples/order.json --output ./out/order.json

# 3. Run a test suite and write a report for your pipeline
la-liquid test --suite ./tests/liquid-tests.json --junit ./TestResults/liquid.xml
```

The exit code is `0` when everything passed, `1` when there are errors or failed tests, and `2` when a file or an option is wrong.

---

## 3. Choosing the transform kind

The Liquid action in Logic Apps has four kinds. Choose the same kind as your action, because the kind decides how the input is read and the output is checked.

| Kind | Input | Output |
|---|---|---|
| `JsonToJson` | JSON, available as `content` | Parsed as JSON; invalid JSON fails the action |
| `JsonToText` | JSON | Text, as is (CSV, XML, a mail body, ...) |
| `XmlToJson` | XML, available as `content` | Parsed as JSON |
| `XmlToText` | XML | Text, as is |

When you don't pass `--kind`, it is inferred from the file names:
- **Input:** an input file ending in `.xml` gives `XmlTo*`; any other gives `JsonTo*`.
- **Output:** an output file ending in `.json` (or no output file) gives `*ToJson`; any other extension gives `*ToText`.

The chosen kind is printed (`Kind: JsonToJson (inferred)`). Set `--kind` explicitly when the file names don't match your action, for example JSON output saved as `.txt`.

---

## 4. `validate`: check maps

`validate` checks maps and never writes files.

```powershell
la-liquid validate --map <file-or-folder> [--input <file>] [--kind <kind>] [engine options] [--warnings-as-errors]
```

### Without input: static checks

```powershell
la-liquid validate --map ./Artifacts/Maps
```

Every `*.liquid` file in the folder and its subfolders is checked:
- **Syntax and structure:**
  - unknown or wrongly cased tags (`{% If %}`)
  - unclosed blocks
  - tags spread over several lines
- **Filters:**
  - wrongly cased filters (`upcase` instead of `Upcase`)
  - filters that don't exist in Logic Apps (`where`, `json`, `concat`, ...)
- **Paths and null values:**
  - property paths that read a different property (`.@code`)
  - `nil` and `null` pitfalls
- **Risky idioms:** `Plus: 0`, `''''`, `Concat` + `Uniq` and others.

This is the check to run on every commit.

### With input: dry run

```powershell
la-liquid validate --map ./Artifacts/Maps/orders.liquid --input ./samples/order.json
```

The map is also rendered with the input, exactly as Logic Apps renders it. On top of the static checks, you get:
- render errors, and missing input values
- output that isn't valid JSON (for `*ToJson`)
- **deviations from Shopify Liquid, based on the actual values**, for example `"2026" | Plus: 10` giving `202610`

Nothing is written.

---

## 5. `transform`: run a map

```powershell
la-liquid transform --map <file> --input <file> --output <file> [--kind <kind>] [--raw] [engine options] [--warnings-as-errors]
```

```text
> la-liquid transform --map samples/maps/orders.to.shipment.request.liquid --input samples/inputs/orders.json --output out/shipment.json
Kind: JsonToJson (inferred)
Transform succeeded: 0 warning(s). Output written to 'out/shipment.json'.
```

- **When it writes:** the output file is **only written when there are no errors**, or no warnings either with `--warnings-as-errors`. A file you find in the output folder therefore always comes from a clean run.
- **JSON output** is written indented, the way the Logic Apps run history shows it. `--raw` writes the exact text DotLiquid produced, which is useful to see whitespace.
- **Text output** is always written as is.
- **Folders:** the output folder is created when needed.

---

## 6. `test`: test suites with expected outputs

A test suite pins down what each map must produce for a given input. This is a regression test that you run in your pipeline.

### The suite file

Create `liquid-tests.json`. Paths are relative to the suite file.

```json
{
  "culture": "en-US",
  "timeZone": "UTC",
  "warningsAsErrors": false,
  "tests": [
    {
      "name": "Order to shipment request",
      "map": "../src/LogicApp/Artifacts/Maps/orders.to.shipment.liquid",
      "input": "inputs/order.json",
      "kind": "JsonToJson",
      "expectedOutput": "expected/shipment.json"
    },
    {
      "name": "Legacy map with known Logic Apps behaviour",
      "map": "../src/LogicApp/Artifacts/Maps/legacy.liquid",
      "input": "inputs/legacy.json",
      "expectedDiagnostics": [ "LQ021" ]
    }
  ]
}
```

| Field | Required | Meaning |
|---|---|---|
| `culture`, `timeZone` | no | Engine options for all tests (see [section 7](#7-engine-options-culture-time-zone-and-checks)). The command line overrides them. |
| `warningsAsErrors` | no | Warnings fail the tests too. |
| `name` | no | Shown in the report. Defaults to the map name. |
| `map`, `input` | yes | The map and the input message. |
| `kind` | no | Inferred from the file names when omitted. |
| `expectedOutput` | no | The output the map must produce. Without it, the test only checks that the map runs without errors. |
| `expectedDiagnostics` | no | Codes that must be reported. Errors with these codes don't fail the test. Use this to accept known behaviour on purpose. |

**How outputs are compared:**
- **JSON outputs** are compared as JSON: formatting and property order don't matter.
- **Text outputs** are compared line by line, ignoring the difference between Windows and Unix line endings.

### Creating and updating expected outputs

```powershell
# First time, or after an intended change: write the actual outputs as expected outputs
la-liquid test --suite tests/liquid-tests.json --update-expected

# Review the changes (for example with git diff), commit them, and from then on:
la-liquid test --suite tests/liquid-tests.json --junit TestResults/liquid.xml
```

```text
  PASS  Order to shipment request
  FAIL  Legacy map with known Logic Apps behaviour
        Output differs at line 3.
          expected:   "amount": 12.5,
          actual:     "amount": 125,
Tests failed: 1 passed, 1 failed.
```

`--junit` writes a JUnit XML report that Azure DevOps (`PublishTestResults@2`) and GitHub test reporters show as test results.

### Where to get good test inputs

- **Real inputs:** take them from a Logic Apps run: open the run history, select the Liquid action and copy its input.
- **Edge cases:** add inputs that trigger them, such as `null` values, a single item instead of an array, decimals with a comma, and dates with a time zone. The deviation checks only see the values in your inputs.

---

## 7. Engine options: culture, time zone and checks

All three commands accept these options:

| Option | Default | When to change it |
|---|---|---|
| `--culture` | `en-US` | DotLiquid reads decimal literals and text, and formats numbers and dates, with this culture. Keep `en-US` to match Azure. Use another culture only to see what a map does on such a host. |
| `--time-zone` | `UTC` | Dates with a time zone are converted to this zone, and `now` uses it. Logic Apps in Azure runs in UTC, **unless** the app setting `WEBSITE_TIME_ZONE` is set: then use that value, for example `--time-zone "W. Europe Standard Time"`. Windows and IANA names both work. `local` uses your machine's time zone. |
| `--no-lint` | off | Skip the static checks: you only see what the Logic Apps engine itself reports. |
| `--no-deviations` | off | Skip the deviation checks (LQ100–LQ127). These render the map a second time to observe the actual filter values; the output is not affected. |
| `--warnings-as-errors` | off | Warnings fail the command. A good setting for new maps in a pipeline. |

Why this matters: on a Dutch laptop, DotLiquid computes `{{ 1.5 | Times: 2 }}` as `30` and shifts dates by one or two hours. The defaults make your local result equal to Azure.

---

## 8. Reading the findings

Every finding has the same format, which Visual Studio, VS Code, Azure DevOps and GitHub link to the source:

```text
Artifacts/Maps/customer.liquid(6,14): error LQ010: Filter 'upcase' does not exist in Logic Apps, use 'Upcase'. ...
<file>(<line>,<column>): <severity> <code>: <message>
```

- **`error`:** Logic Apps fails, or silently produces a wrong result. The command fails.
- **`warning`:** probably a mistake, or a deviation from Shopify Liquid that changes the output for the values seen. The command passes, unless you use `--warnings-as-errors`.
- **Location:** findings about the rendered output point to the output (`... (rendered output)(3,10)`) and show the offending line.

The messages explain what happens and what to write instead. All codes are listed in the [README](README.md#diagnostics). The most common ones:

| Code | Meaning | Usual fix |
|---|---|---|
| LQ010 / LQ011 | Filter is written in Shopify style or doesn't exist, and is silently skipped | Use the C# name (`Upcase`, `StripHtml`), or a `for` loop with an `if` |
| LQ021 / LQ024 | `nil` check or truthiness check on input: JSON `null` is `""` in Logic Apps | Also compare with `""` |
| LQ041 | Output isn't valid JSON | Escape text values: `\| Replace: '\\', '\\' \| Replace: '"', '\"'` |
| LQ100–LQ103 | Arithmetic on text | Convert first: `\| Times: 1.0` (`\| Round` for whole numbers) |
| LQ126 | Loop over a single object (XML with one element) | `{% assign items = x \| Compact %}` |

There is no suppression comment. To accept known behaviour on purpose, list the code in `expectedDiagnostics` of a test.

---

## 9. Azure DevOps

### Option A: the step template, straight from GitHub

This needs a GitHub service connection in your Azure DevOps project (*Project settings → Service connections → GitHub*).

```yaml
resources:
  repositories:
    - repository: laliquid
      type: github
      endpoint: my-github-connection          # your GitHub service connection
      name: sahinozd/LogicAppsStandard.Liquid
      ref: refs/tags/v1.0.0                   # pin a release

steps:
  - template: pipelines/templates/steps/validate-liquid-maps.yml@laliquid
    parameters:
      mapsFolder: '$(Build.SourcesDirectory)/src/LogicApp/Artifacts/Maps'
      testSuite: '$(Build.SourcesDirectory)/tests/liquid-tests.json'   # optional
      warningsAsErrors: false
      timeZone: UTC            # or the value of WEBSITE_TIME_ZONE
      toolVersion: ''          # empty = latest
```

The template:
1. installs .NET and `la-liquid`
2. validates all maps
3. runs the suite
4. publishes the test results

Findings appear on the run summary with file and line; test results appear on the *Tests* tab.

### Option B: copy the template

Copy [pipelines/templates/steps/validate-liquid-maps.yml](pipelines/templates/steps/validate-liquid-maps.yml) into your repository, and use `- template: templates/steps/validate-liquid-maps.yml`.

### Option C: inline steps

```yaml
steps:
  - task: UseDotNet@2
    inputs:
      packageType: sdk
      version: 10.0.x

  - script: |
      dotnet tool install LogicApps.Liquid.Cli --tool-path "$(Agent.TempDirectory)/la-liquid"
      echo "##vso[task.prependpath]$(Agent.TempDirectory)/la-liquid"
    displayName: Install la-liquid

  - script: la-liquid validate --map "$(Build.SourcesDirectory)/src/LogicApp/Artifacts/Maps"
    displayName: Validate Liquid maps

  - script: la-liquid test --suite "$(Build.SourcesDirectory)/tests/liquid-tests.json" --junit "$(Agent.TempDirectory)/liquid.xml"
    displayName: Run Liquid tests

  - task: PublishTestResults@2
    condition: succeededOrFailed()
    inputs:
      testResultsFormat: JUnit
      testResultsFiles: $(Agent.TempDirectory)/liquid.xml
```

In Azure DevOps (`TF_BUILD` is set), every finding is also logged as a warning or error on the run summary.

### Without access to nuget.org

Download the `.nupkg` files from the GitHub release, push them to your Azure Artifacts feed, and pass `toolFeed: 'https://pkgs.dev.azure.com/<org>/_packaging/<feed>/nuget/v3/index.json'` to the template. Or put the release executable in your repository or agent image, and call it directly.

---

## 10. GitHub Actions

### Option A: the action

```yaml
jobs:
  liquid:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6

      - name: Validate Liquid maps
        id: liquid
        uses: sahinozd/LogicAppsStandard.Liquid@v1.0.0
        with:
          maps: src/LogicApp/Artifacts/Maps
          test-suite: tests/liquid-tests.json      # optional
          warnings-as-errors: false
          time-zone: UTC                           # or the value of WEBSITE_TIME_ZONE

      - name: Publish Liquid test results
        if: always() && steps.liquid.outputs.junit-report != ''
        uses: dorny/test-reporter@v3
        with:
          name: Liquid maps
          path: ${{ steps.liquid.outputs.junit-report }}
          reporter: java-junit
```

| Input | Default | Meaning |
|---|---|---|
| `maps` | required | A map or a folder with maps |
| `test-suite` | – | Optional test suite file |
| `version` | latest | Version of `LogicApps.Liquid.Cli` |
| `culture`, `time-zone` | `en-US`, `UTC` | Engine options |
| `warnings-as-errors` | `false` | Warnings fail the step |
| `nuget-source` | nuget.org | Another NuGet source, for example GitHub Packages |
| `dotnet-version` | `10.x` | .NET SDK used to run the tool |

The output `junit-report` holds the path of the JUnit report.

### Option B: inline steps

```yaml
      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: 10.x
      - run: |
          dotnet tool install LogicApps.Liquid.Cli --tool-path "$RUNNER_TEMP/la-liquid"
          echo "$RUNNER_TEMP/la-liquid" >> "$GITHUB_PATH"
      - run: la-liquid validate --map src/LogicApp/Artifacts/Maps
      - run: la-liquid test --suite tests/liquid-tests.json --junit TestResults/liquid.xml
```

In GitHub Actions (`GITHUB_ACTIONS` is set), every finding is also written as an annotation. It shows on the changed line in pull requests and on the run summary.

---

## 11. VS Code and Visual Studio

### VS Code

Open the repository folder. *Terminal → Run Task* offers:

| Task | What it does |
|---|---|
| `la-liquid: validate current map` | Validates the open `.liquid` file. Findings appear in the **Problems** panel and link to the line. |
| `la-liquid: transform current map` | Asks for an input and an output file, and runs the open map. |
| `la-liquid: test samples` | Runs the sample suite. |
| `build`, `test` | Build and test the solution. |

To use the tasks in your own integration repository, copy `.vscode/tasks.json` and replace `dotnet run --project ...` with `la-liquid` (the installed tool). The `$msCompile` problem matcher understands the findings.

Two launch configurations (`F5`) let you debug the tool itself.

### Visual Studio

Open `LogicAppsStandard.Liquid.slnx`. To validate maps from Visual Studio in your own solution, add an external tool (*Tools → External Tools*):
- **Command:** `la-liquid`
- **Arguments:** `validate --map $(ItemPath)`
- **Use Output window:** on

Double-clicking a finding in the Output window opens the line.

---

## 12. Using the .NET library

The engine is also available as the NuGet package `LogicApps.Liquid`. You can use it in your own tools or unit tests, for example next to [LogicAppsStandard.Testing](https://github.com/sahinozd/LogicAppsStandard.Testing).

```powershell
dotnet add package LogicApps.Liquid
```

```csharp
using System.Globalization;
using LogicApps.Liquid;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

var transformer = new LiquidTransformer(new LiquidTransformOptions
{
    Culture = CultureInfo.GetCultureInfo("en-US"),   // default
    TimeZone = TimeZoneInfo.Utc,                      // default; null = this machine's time zone
    Lint = true,                                      // static checks
    DetectDeviations = true,                          // deviations from Shopify Liquid
});

// Only check a map
IReadOnlyList<Diagnostic> findings = transformer.Validate(File.ReadAllText("orders.liquid"));

// Run a map
LiquidTransformResult result = transformer.Transform(
    File.ReadAllText("orders.liquid"),
    File.ReadAllText("order.json"),
    LiquidTransformKind.JsonToJson);

foreach (var finding in result.Diagnostics)
{
    Console.WriteLine($"{finding.Severity} {finding.Code} at {finding.Line},{finding.Column}: {finding.Message}");
}

if (result.Succeeded)
{
    Console.WriteLine(result.Output);          // JToken: parsed JSON, or a string for *ToText
}
```

| Member | Meaning |
|---|---|
| `Transform(map, input, kind)` | Validates and runs the map. |
| `Validate(map)` | Checks the map without input. |
| `result.Succeeded` | No errors. |
| `result.HasFailures(warningsAsErrors)` | Errors, or warnings when you pass `true`. |
| `result.RenderedText` | The exact text DotLiquid rendered. |
| `result.Output` | The action output as Logic Apps returns it. |
| `result.Diagnostics` | All findings. Use `DiagnosticCodes` constants to check for specific codes. |

### Example: a unit test for a map (NUnit)

```csharp
[Test]
public void Order_map_Should_produce_shipment_request()
{
    // Arrange
    var transformer = new LiquidTransformer();

    // Act
    var result = transformer.Transform(File.ReadAllText("Maps/orders.liquid"), File.ReadAllText("Inputs/order.json"), LiquidTransformKind.JsonToJson);

    // Assert
    Assert.That(result.HasFailures(warningsAsErrors: true), Is.False, string.Join(Environment.NewLine, result.Diagnostics));
    Assert.That((string?)result.Output!["packages"]![0]!["sku"], Is.EqualTo("ABC-100"));
}
```

DotLiquid's configuration and the emulated time zone are process-wide, so the library runs transforms one at a time. This is safe with parallel test runners; they just wait for each other.

---

## 13. Recommended way of working

1. **While writing a map:** run the VS Code task *validate current map* on save, and *transform* with a sample input.
2. **Before committing:** run `la-liquid validate --map <maps folder>`.
3. **In the build pipeline:**
   - validate all maps on every PR
   - run the test suite with real inputs, and fail the build on errors
   - for new projects, use `--warnings-as-errors`
4. **When a map changes on purpose:** run `test --update-expected`, review the diff of the expected outputs, and commit the map and outputs together.
5. **When a new Logic Apps runtime appears:** the repository's conformance tests show whether the engine changed. Pin a tool version in pipelines (`toolVersion`, `version`), and update on purpose.

---

## 14. Troubleshooting

| Symptom | Cause and solution |
|---|---|
| The output differs from Azure in decimals or dates | Check `--culture` and `--time-zone`. Does your Logic App set `WEBSITE_TIME_ZONE`? Then use the same value. |
| `Liquid error: ...` appears in the output | Logic Apps doesn't fail on render errors; it writes them into the output. The finding LQ040 says which error. |
| A value is empty in the output | Look for LQ023 (value not found in the input), LQ025 (special character in the path), LQ127 (an object printed instead of a value) and LQ010/LQ011 (filter skipped). |
| A loop renders nothing | LQ126: the value is a single object, typically XML converted to JSON with one element. Wrap it with `\| Compact`. |
| `JsonToJson` output is `null` | The map rendered nothing (LQ042). Logic Apps doesn't fail on this; the action output is `null`. |
| `error: unknown time zone` | Use a Windows name (`W. Europe Standard Time`) or an IANA name (`Europe/Amsterdam`). |
| An XML file is reported as invalid | The tool reads XML as UTF-8, like Logic Apps reads the message bytes. A file saved as UTF-16, or with a declaration like `encoding="utf-16"` on UTF-8 content, is rejected, just as in Logic Apps. |
| Consumption logic app | Works the same (same DotLiquid version according to Microsoft). The conformance tests are verified against the Standard runtime. |
| `la-liquid` not found after `dotnet tool install --global` | Open a new terminal, or add `%USERPROFILE%\.dotnet\tools` (Windows) or `~/.dotnet/tools` (Linux, macOS) to `PATH`. |
