# la-liquid usage guide

How to install and use `la-liquid`: every command and option with an example, test suites, pipelines, editors and the .NET library. For what the tool is and why, see the [README](README.md). For the meaning of each code, see the [diagnostics reference](docs/diagnostics.md).

## Contents

1. [Install](#1-install)
2. [Commands at a glance](#2-commands-at-a-glance)
3. [validate](#3-validate)
4. [transform](#4-transform)
5. [test](#5-test)
6. [explain](#6-explain)
7. [Engine options](#7-engine-options)
8. [Output options](#8-output-options)
9. [Reading the findings](#9-reading-the-findings)
10. [Azure DevOps](#10-azure-devops)
11. [GitHub Actions](#11-github-actions)
12. [VS Code and Visual Studio](#12-vs-code-and-visual-studio)
13. [The .NET library](#13-the-net-library)
14. [Recommended way of working](#14-recommended-way-of-working)
15. [Troubleshooting](#15-troubleshooting)

---

## 1. Install

**.NET tool** (needs .NET 10):

```powershell
dotnet tool install --global LogicApps.Liquid.Cli                  # install
dotnet tool update --global LogicApps.Liquid.Cli                   # update
dotnet tool install --global LogicApps.Liquid.Cli --version 1.0.2  # a specific version
la-liquid --version
```

To pin the version per repository: run `dotnet new tool-manifest` and `dotnet tool install LogicApps.Liquid.Cli` once. Everyone who clones the repository then runs `dotnet tool restore`.

**Executable** (no .NET needed): download the archive for your platform from [GitHub Releases](https://github.com/sahinozd/LogicAppsStandard.Liquid/releases), unpack it and run `la-liquid`. `SHA256SUMS.txt` has the checksums.

**GitHub Packages:** needs a personal access token with `read:packages`, even for public packages.

```powershell
dotnet nuget add source https://nuget.pkg.github.com/sahinozd/index.json --name sahinozd --username <github-user> --password <token>
dotnet tool install --global LogicApps.Liquid.Cli --add-source sahinozd
```

**From source:**

```powershell
git clone https://github.com/sahinozd/LogicAppsStandard.Liquid.git
dotnet run --project LogicAppsStandard.Liquid/source/Liquid.Cli -- validate --map LogicAppsStandard.Liquid/samples/maps
```

---

## 2. Commands at a glance

| Command | What it does | Writes files |
|---|---|---|
| `validate` | Checks a map or a folder of maps; with `--input` also renders the map | no |
| `transform` | Checks and renders a map, and writes the output | the output, only without errors |
| `test` | Runs a test suite with expected outputs | a JUnit report, if asked |
| `explain` | Explains a diagnostic code | no |

`la-liquid <command> --help` lists the options of a command.

**Exit codes:** `0` everything passed, `1` errors or failed tests, `2` wrong option or unreadable file.

---

## 3. validate

```text
la-liquid validate --map <file|folder> [--input <file>] [--kind <kind>] [--warnings-as-errors] [engine options] [output options]
```

| Option | Example | Meaning |
|---|---|---|
| `-m`, `--map` | `--map ./Artifacts/Maps` | A map, or a folder: every `*.liquid` in it and its subfolders. Required. |
| `-i`, `--input` | `--input ./samples/order.json` | Also render the map with this message and check the result. Only with a single map. |
| `-k`, `--kind` | `--kind JsonToText` | The transform kind of your Liquid action. Inferred when omitted, see [Transform kinds](#transform-kinds). |
| `--warnings-as-errors` | `--warnings-as-errors` | Warnings also fail the command (exit code 1). |

**Without input**, only the static checks run: syntax, tags, filter names, paths and risky idioms. This is fast and needs no test data, so run it on every commit:

```powershell
la-liquid validate --map ./Artifacts/Maps
la-liquid validate --map ./Artifacts/Maps/order.liquid
```

**With input**, the map is rendered exactly as Logic Apps renders it. On top of the static checks you get render errors, missing values, invalid JSON output and the differences from Shopify Liquid for the actual values. Nothing is written.

```powershell
la-liquid validate --map ./Artifacts/Maps/order.liquid --input ./samples/order.json
la-liquid validate --map ./Artifacts/Maps/order-to-csv.liquid --input ./samples/order.json --kind JsonToText
la-liquid validate --map ./Artifacts/Maps --warnings-as-errors
```

### Transform kinds

Use the same kind as your Liquid action: it decides how the input is read and the output is checked.

| Kind | Input | Output |
|---|---|---|
| `JsonToJson` | JSON, as `content` | Parsed as JSON; invalid JSON fails the action |
| `JsonToText` | JSON | Text as is (CSV, XML, a mail body) |
| `XmlToJson` | XML, as `content` | Parsed as JSON |
| `XmlToText` | XML | Text as is |

Without `--kind`, the kind is inferred from the file names and printed after the map name (`JsonToJson (inferred)`):
- **Input:** `.xml` gives `XmlTo*`, anything else `JsonTo*`.
- **Output:** `.json`, or no output file (`validate`), gives `*ToJson`; any other extension gives `*ToText`.

So for a `*ToText` map, `validate --input` needs `--kind`.

---

## 4. transform

```text
la-liquid transform --map <file> --input <file> --output <file> [--kind <kind>] [--raw] [--warnings-as-errors] [engine options] [output options]
```

| Option | Example | Meaning |
|---|---|---|
| `-m`, `--map` | `--map ./Artifacts/Maps/order.liquid` | The map. Required. |
| `-i`, `--input` | `--input ./samples/order.json` | The input message. Required. |
| `-o`, `--output` | `--output ./out/order.json` | Where to write the output. The folder is created when needed. Required. |
| `-k`, `--kind` | `--kind XmlToText` | The transform kind; inferred from the input and output file names when omitted. |
| `--raw` | `--raw` | Write the exact text DotLiquid rendered. By default JSON output is written indented, as the Logic Apps run history shows it. |
| `--warnings-as-errors` | `--warnings-as-errors` | Warnings also block the output. |

The output is **only written when there are no errors**, so a file in the output folder always comes from a clean run.

```powershell
la-liquid transform --map ./Artifacts/Maps/order.liquid --input ./samples/order.json --output ./out/order.json
la-liquid transform --map ./Artifacts/Maps/shipment-to-csv.liquid --input ./samples/shipment.xml --output ./out/shipment.csv
la-liquid transform --map ./Artifacts/Maps/order.liquid --input ./samples/order.json --output ./out/order.txt --kind JsonToJson --raw
```

---

## 5. test

```text
la-liquid test --suite <file> [--junit <file>] [--update-expected] [--warnings-as-errors] [engine options] [output options]
```

| Option | Example | Meaning |
|---|---|---|
| `-s`, `--suite` | `--suite ./tests/liquid-tests.json` | The suite file. Required. |
| `--junit` | `--junit ./TestResults/liquid.xml` | Also write a JUnit report, for Azure DevOps and GitHub test reports. |
| `--update-expected` | `--update-expected` | Write the actual outputs into the expected output files instead of comparing. |
| `--warnings-as-errors` | `--warnings-as-errors` | Warnings also fail a test. Same as `warningsAsErrors` in the suite file. |

```powershell
la-liquid test --suite ./tests/liquid-tests.json
la-liquid test --suite ./tests/liquid-tests.json --junit ./TestResults/liquid.xml
la-liquid test --suite ./tests/liquid-tests.json --update-expected
```

### The suite file

Paths are relative to the suite file.

```json
{
  "culture": "en-US",
  "timeZone": "UTC",
  "warningsAsErrors": false,
  "tests": [
    {
      "name": "Order to shipment request",
      "map": "../src/LogicApp/Artifacts/Maps/order-to-shipment.liquid",
      "input": "inputs/order.json",
      "kind": "JsonToJson",
      "expectedOutput": "expected/shipment.json"
    },
    {
      "name": "Legacy map: known null checks and a Date in UTC",
      "map": "../src/LogicApp/Artifacts/Maps/legacy.liquid",
      "input": "inputs/legacy.json",
      "expectedDiagnostics": [ "LQ021", "LQ118" ]
    }
  ]
}
```

| Field | Required | Meaning |
|---|---|---|
| `culture`, `timeZone` | no | [Engine options](#7-engine-options) for all tests. `--culture` and `--time-zone` on the command line win. |
| `warningsAsErrors` | no | Warnings fail the tests. |
| `name` | no | Shown in the results. Defaults to the map's file name. |
| `map`, `input` | yes | The map and the input message. |
| `kind` | no | Inferred from the file names when omitted. |
| `expectedOutput` | no | The output the map must produce. Without it, the test only checks for errors. |
| `expectedDiagnostics` | no | Codes the map is known and allowed to report, see below. |

A test **fails** when:
- the output differs from `expectedOutput`. JSON is compared as JSON (formatting and property order don't matter); text is compared line by line (line endings don't matter).
- there is an error, or a warning with `warningsAsErrors`, whose code is not in `expectedDiagnostics`.
- a code in `expectedDiagnostics` is **not** reported.

### expectedDiagnostics

Use it to accept a known problem on purpose, for example in a legacy map you can't change yet, without switching the checks off:
- **All other checks still run:** an error with any other code still fails the test, and so does a warning with `warningsAsErrors`.
- **More than one code:** it is a list, `[ "LQ021", "LQ118" ]`.
- **Every listed code must be reported:** if you fix the map and the finding disappears, the test fails, which reminds you to remove the code from the list.
- **A code covers every finding with that code in the map,** not one specific line. A new LQ021 elsewhere in the same map is accepted too.
- **List the code that is reported:** related findings are merged, so a `Minus` on text is LQ102, not LQ040. See [one problem, one finding](docs/diagnostics.md#one-problem-one-finding).

There is no suppression comment inside maps; `expectedDiagnostics` is the only way to accept a finding.

### Creating and updating expected outputs

```powershell
# First time, or after an intended change: write the actual outputs as expected outputs
la-liquid test --suite ./tests/liquid-tests.json --update-expected
# Review the changes with git diff, commit them, and from then on compare:
la-liquid test --suite ./tests/liquid-tests.json
```

```text
✓ PASS  Order to shipment request (81 ms)
✗ FAIL  Legacy map: known null checks and a Date in UTC (9 ms)
         Output differs at line 3.
           expected:   "amount": 12.5,
           actual:     "amount": 125,

✗ Tests failed  1 passed · 1 failed
```

The findings of a passing test are expected, so they are only shown for failing tests. Use `--verbosity detailed` to see all of them.

**Good test inputs:** copy real inputs from the Logic Apps run history (select the Liquid action and copy its input). Add edge cases: `null` values, a single item instead of an array, decimals with a comma, and dates with a time zone. The differences from Shopify Liquid are only found for values that are in your inputs.

---

## 6. explain

```text
la-liquid explain [<code>] [--no-color]
```

```powershell
la-liquid explain LQ102   # what LQ102 means, an example and the fix
la-liquid explain         # all codes, per category
```

---

## 7. Engine options

`validate`, `transform` and `test` emulate the Logic Apps host. The defaults match Azure, so you rarely need these.

| Option | Default | Example | When to use |
|---|---|---|---|
| `--culture` | `en-US` | `--culture nl-NL` | To see what a map does on a host with another culture. DotLiquid reads decimals and formats numbers and dates with it. |
| `--time-zone` | `UTC` | `--time-zone "W. Europe Standard Time"` | When your Logic App sets the app setting `WEBSITE_TIME_ZONE`: use the same value. Windows and IANA names (`Europe/Amsterdam`) both work; `local` uses this machine's zone. |
| `--no-lint` | off | `--no-lint` | Skip the static checks, for example to see only what the engine reports. |
| `--no-deviations` | off | `--no-deviations` | Skip the differences from Shopify Liquid (LQ100–LQ133). |

Why the defaults matter: on a Dutch laptop without them, `{{ 1.5 | Times: 2 }}` gives `30`, and dates shift by one or two hours.

```powershell
la-liquid validate --map ./Maps/order.liquid --input ./samples/order.json --time-zone "W. Europe Standard Time"
la-liquid transform --map ./Maps/order.liquid --input ./samples/order.json --output ./out/order.json --culture nl-NL
la-liquid validate --map ./Maps/order.liquid --input ./samples/order.json --no-lint --no-deviations   # only DotLiquid's own errors
```

---

## 8. Output options

| Option | Default | Example | Meaning |
|---|---|---|---|
| `--format` | `pretty` | `--format msbuild` | `pretty`: readable, for people. `msbuild`: one line per finding, for editors. `json`: one document, for scripts. |
| `--verbosity` | `normal` | `--verbosity minimal` | For `pretty`: `minimal` gives one line per finding, `normal` adds the source line, values and fix, `detailed` also adds why. |
| `--no-color` | off | `--no-color` | No colors. Also off when the environment variable `NO_COLOR` is set, or when the output goes to a file. |

```powershell
la-liquid validate --map ./Maps --verbosity minimal                                   # a quick overview of many maps
la-liquid validate --map ./Maps/order.liquid --input ./samples/order.json --verbosity detailed
la-liquid validate --map ./Maps --format msbuild                                      # file(line,col): error CODE: message
la-liquid validate --map ./Maps --format json > findings.json                         # for a script
la-liquid validate --map ./Maps --no-color > findings.txt
```

| Format | For |
|---|---|
| `pretty` | The terminal, and the Azure DevOps and GitHub Actions logs, which show the colors. |
| `msbuild` | The Visual Studio error list and the VS Code `$msCompile` problem matcher: `<file>(<line>,<column>): <severity> <code>: <message>`. |
| `json` | Scripts. Every finding with code, severity, category, title, values, explanation, fix, file, line and column. |

In Azure DevOps and GitHub Actions, `pretty` and `msbuild` also log every finding as an annotation, so it shows on the run summary and, in GitHub, next to the changed line of a pull request. `json` doesn't, so its output stays valid JSON.

---

## 9. Reading the findings

```text
invoice.liquid  JsonToText

  ≠ differs LQ100  Plus on text joins instead of adding
     at invoice.liquid:8:36
     8 │ {%- assign dueYear = invoiceYear | Plus: 1 -%}
       │                                    ^^^^
     values '2026' | Plus: 1 → '20261' (Shopify Liquid: 2027)
     fix    Convert to a number first, for example '| Times: 1.0 | Round | Plus: ...'.

  ✗ error   LQ102  Minus on text fails
     at invoice.liquid:9:44
     9 │ Days left: {{ content.order.deliveryDays | Minus: 1 }}
       │                                            ^^^^^
     values '5' | Minus: 1 → 'Liquid error' (Shopify Liquid converts '5' to a number)
     output line 4: Days left: Liquid error: Parameter count mismatch.
     fix    Convert it first, for example '| Times: 1.0 | Round'.

✗ Validation failed  1 map · 1 error · 1 warning
  differs from Shopify  1 error, 1 warning  LQ100, LQ102
  More about a code: la-liquid explain LQ102
```

| Part | Meaning |
|---|---|
| `✗ error` | Logic Apps fails, or silently produces a wrong result. The command fails. |
| `▲ warning` | Probably a mistake. The command passes, unless `--warnings-as-errors`. |
| `≠ differs` | A filter gives another result than in Shopify Liquid for these values. An error when it writes `Liquid error` into the output. |
| `at` | `file:line:column`. Ctrl+click opens it in VS Code and Windows Terminal. |
| `values` | The actual values. A filter in a loop is reported once, with up to three values. |
| `output` | The line of the rendered output where the problem shows. |
| `fix` | What to write instead. |
| summary | Totals, and the findings per category with their codes. |

The codes are explained in the [diagnostics reference](docs/diagnostics.md) and by `la-liquid explain`.

---

## 10. Azure DevOps

### The step template, straight from GitHub

Needs a GitHub service connection (*Project settings → Service connections → GitHub*).

```yaml
resources:
  repositories:
    - repository: laliquid
      type: github
      endpoint: my-github-connection          # your GitHub service connection
      name: sahinozd/LogicAppsStandard.Liquid
      ref: refs/tags/v1.0.2                   # pin a release

steps:
  - template: pipelines/templates/steps/validate-liquid-maps.yml@laliquid
    parameters:
      mapsFolder: '$(Build.SourcesDirectory)/src/LogicApp/Artifacts/Maps'
      testSuite: '$(Build.SourcesDirectory)/tests/liquid-tests.json'
```

| Parameter | Default | Meaning |
|---|---|---|
| `mapsFolder` | required | The folder with maps to validate |
| `testSuite` | – | A test suite to run; its results are published to the *Tests* tab |
| `warningsAsErrors` | `false` | Warnings fail the build |
| `culture`, `timeZone` | `en-US`, `UTC` | [Engine options](#7-engine-options) |
| `toolVersion` | latest | Version of `LogicApps.Liquid.Cli` |
| `toolFeed` | nuget.org | Another feed, for example an Azure Artifacts feed |
| `dotnetVersion` | `10.0.x` | .NET SDK used to run the tool |

You can also copy [the template](pipelines/templates/steps/validate-liquid-maps.yml) into your repository and use `- template: templates/steps/validate-liquid-maps.yml`.

### Inline steps

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

**Without access to nuget.org:** push the `.nupkg` files from the GitHub release to your Azure Artifacts feed and pass it as `toolFeed`. Or put the release executable in your repository or agent image.

---

## 11. GitHub Actions

### The action

```yaml
jobs:
  liquid:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6

      - name: Validate Liquid maps
        id: liquid
        uses: sahinozd/LogicAppsStandard.Liquid@v1.0.2
        with:
          maps: src/LogicApp/Artifacts/Maps
          test-suite: tests/liquid-tests.json

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
| `test-suite` | – | A test suite to run |
| `warnings-as-errors` | `false` | Warnings fail the step |
| `culture`, `time-zone` | `en-US`, `UTC` | [Engine options](#7-engine-options) |
| `version` | latest | Version of `LogicApps.Liquid.Cli` |
| `nuget-source` | nuget.org | Another NuGet source, for example GitHub Packages |
| `dotnet-version` | `10.x` | .NET SDK used to run the tool |

The output `junit-report` is the path of the JUnit report.

### Inline steps

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

---

## 12. VS Code and Visual Studio

**VS Code:** this repository has tasks (*Terminal → Run Task*):

| Task | What it does |
|---|---|
| `la-liquid: validate current map` | Validates the open map; findings appear in the **Problems** panel |
| `la-liquid: transform current map` | Asks for an input and output file, and runs the open map |
| `la-liquid: test samples` | Runs the sample suite |

To use them in your own repository, copy [.vscode/tasks.json](.vscode/tasks.json) and replace `dotnet run --project ...` with `la-liquid`. Keep `--format msbuild`: the Problems panel needs it.

**Visual Studio:** add an external tool (*Tools → External Tools*) with command `la-liquid`, arguments `validate --map $(ItemPath) --format msbuild`, and *Use Output window* on. Double-click a finding in the Output window to open the line.

---

## 13. The .NET library

```powershell
dotnet add package LogicApps.Liquid
```

```csharp
using System.Globalization;
using LogicApps.Liquid;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

var transformer = new LiquidTransformer(new LiquidTransformOptions
{
    Culture = CultureInfo.GetCultureInfo("en-US"),   // default
    TimeZone = TimeZoneInfo.Utc,                      // default; null = this machine's time zone
    Lint = true,                                      // static checks
    DetectDeviations = true,                          // differences from Shopify Liquid
});

// Check a map without input
var findings = transformer.Validate(File.ReadAllText("order.liquid"));

// Run a map
var result = transformer.Transform(File.ReadAllText("order.liquid"), File.ReadAllText("order.json"), LiquidTransformKind.JsonToJson);
foreach (var finding in result.Diagnostics)
{
    Console.WriteLine($"{finding.Severity} {finding.Code} line {finding.Line}: {finding.Title}. Fix: {finding.Fix}");
}

Console.WriteLine(result.Output);   // JToken: parsed JSON for *ToJson, a string for *ToText

// Documentation of a code
Console.WriteLine(DiagnosticCatalog.Find("LQ102")?.Description);
```

| Member | Meaning |
|---|---|
| `Transform(map, input, kind)` / `Validate(map)` | Run and check a map, or only check it |
| `result.Succeeded` / `result.HasFailures(warningsAsErrors)` | No errors / errors (or warnings) |
| `result.RenderedText` / `result.Output` | The exact rendered text / the action output as Logic Apps returns it |
| `result.Diagnostics` | The findings: `Code`, `Severity`, `Title`, `Values`, `Explanation`, `Fix`, `Line`, `Column`, `OutputLine`, and `Message` with everything in one text |
| `DiagnosticCodes`, `DiagnosticCatalog` | The codes as constants, and their documentation |

**A unit test for a map (NUnit):**

```csharp
[Test]
public void Order_map_Should_produce_shipment_request()
{
    // Act
    var result = new LiquidTransformer().Transform(File.ReadAllText("Maps/order.liquid"), File.ReadAllText("Inputs/order.json"), LiquidTransformKind.JsonToJson);

    // Assert
    Assert.That(result.HasFailures(warningsAsErrors: true), Is.False, string.Join(Environment.NewLine, result.Diagnostics));
    Assert.That((string?)result.Output!["packages"]![0]!["sku"], Is.EqualTo("ABC-100"));
}
```

Transforms run one at a time per process, because DotLiquid's configuration is process-wide. Parallel test runners are safe; they wait for each other.

---

## 14. Recommended way of working

1. **While writing a map:** run *validate current map* in VS Code, and *transform* with a sample input.
2. **On every pull request:** `validate` all maps, and run the test suite with real inputs. Use `--warnings-as-errors` for new projects.
3. **When a map changes on purpose:** run `test --update-expected`, review the diff, and commit the map and the expected outputs together.
4. **Pin the tool version** in pipelines (`toolVersion`, `version`), and update it on purpose.

---

## 15. Troubleshooting

| Symptom | Cause and solution |
|---|---|
| The output differs from Azure in decimals or dates | Check `--culture` and `--time-zone`. Does your Logic App set `WEBSITE_TIME_ZONE`? Use the same value. |
| `Liquid error: ...` in the output | Logic Apps doesn't fail on render errors; it writes them into the output. The finding at the filter call (for example LQ102), or LQ040 with the output line, says which error. |
| A value is empty in the output | Look for LQ023 (not in the input), LQ025 (special character in the path), LQ127 (an object printed) and LQ010/LQ011 (filter skipped). |
| A loop renders nothing | LQ126: the value is a single object, typically XML with one element converted to JSON. Wrap it with `\| Compact`. |
| An if takes the wrong branch | Look for LQ131/LQ055 (numbers in text compared as text, typical for XML input), LQ130 (`contains` on a list of text), LQ132 (`blank`/`empty`), LQ050 (`else if`), LQ051/LQ052 (parentheses or a filter in the condition) and LQ029 (`forloop.Last`). |
| A large number is negative or wrong | LQ129: math filters calculate whole numbers as 32-bit integers. Use a decimal operand, for example `\| Times: 100.0`. |
| `JsonToJson` output is `null` | The map rendered nothing (LQ042). Logic Apps doesn't fail on this. |
| An XML file is reported as invalid | Logic Apps reads the message as UTF-8 bytes, and so does the tool: a UTF-16 file, or `encoding="utf-16"` on UTF-8 content, is rejected. |
| `?` instead of `✗`, or codes like `[31m` | The console doesn't understand UTF-8 or colors. Use `--no-color`, or `--format msbuild`. |
| `error: unknown time zone` | Use a Windows name (`W. Europe Standard Time`) or an IANA name (`Europe/Amsterdam`). |
| `la-liquid` not found after installing | Open a new terminal, or add `%USERPROFILE%\.dotnet\tools` (Windows) or `~/.dotnet/tools` to `PATH`. |
| Logic Apps Consumption | Works the same: the same DotLiquid version, according to Microsoft. |
