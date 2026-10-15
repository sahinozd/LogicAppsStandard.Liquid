# Logic Apps Standard - Liquid validator and runner

`la-liquid` is a command line tool that validates Liquid maps and runs them **exactly like the Azure Logic Apps Standard Liquid action**, without deploying anything. It runs locally and in Azure DevOps pipelines.

```text
> la-liquid validate --map samples/maps/common-mistakes.liquid --input samples/inputs/common-mistakes.json
samples/maps/common-mistakes.liquid  JsonToJson (inferred)

  ✖ error   LQ010  Filter 'upcase' is skipped by Logic Apps
     at samples/maps/common-mistakes.liquid:6:32
     6 │     "name": "{{ content.name | upcase }}",
       │                                ^^^^^^
     fix    Use 'Upcase'.

  ⚠ warning LQ021  Comparison with nil misses JSON null values
     at samples/maps/common-mistakes.liquid:8:40
     8 │     "hasEmail": {% if content.email == nil %}false{% else %}true{% endif %},
       │                                        ^^^
     fix    Also compare with "", for example 'x != nil and x != ""'.

✖ Validation failed  1 map · 1 error · 1 warning
  filters               1 error  LQ010
  values & paths        1 warning  LQ021
  More about a code: la-liquid explain LQ010
```

In a terminal, errors are red, warnings yellow and differences from Shopify Liquid magenta.

---

## Table of contents

1. [Why](#why)
2. [What Logic Apps really runs](#what-logic-apps-really-runs)
3. [Installing](#installing)
4. [Commands](#commands)
5. [Diagnostics](#diagnostics)
6. [Where DotLiquid differs from Shopify Liquid](#where-dotliquid-differs-from-shopify-liquid)
7. [Patterns found in real-world maps](#patterns-found-in-real-world-maps)
8. [Related tools](#related-tools)
9. [Test suites](#test-suites)
10. [Azure DevOps](#azure-devops)
11. [VS Code and Visual Studio](#vs-code-and-visual-studio)
12. [How conformance is verified](#how-conformance-is-verified)
13. [Known limitations](#known-limitations)

---

## Why

The Liquid that Logic Apps runs is not the Liquid in the Shopify documentation, and most mistakes do **not** fail the workflow run. They silently produce a wrong output:

| You write | Shopify Liquid | Logic Apps |
|---|---|---|
| `{{ name \| upcase }}` | `SAHIN` | `sahin`: the filter is unknown and silently skipped |
| `{{ items \| where: "id", 1 }}` | filtered array | the unfiltered array: there is no `where` filter |
| `{% if value == nil %}` for JSON `null` | true | **false**: `null` becomes an empty string |
| `{{ 1.5 \| Times: 2 }}` on a Dutch machine | `3` | `30`: the decimal literal is parsed with the host culture |
| `{% include 'x' %}` | includes a file | writes `Liquid error: ...` into the output and succeeds |

`la-liquid` uses the same engine and the same settings as the runtime, and reports these problems with file, line and column.

## What Logic Apps really runs

Established by decompiling the Logic Apps Standard workflow runtime (extension bundle `Microsoft.Azure.Functions.ExtensionBundle.Workflows` 1.170.91) and confirmed with the [conformance tests](#how-conformance-is-verified):

- **Engine:** [DotLiquid](https://github.com/dotliquid/dotliquid) **2.0.361**. The runtime's `DotLiquid.dll` is byte-identical to the `netstandard1.3` build of the NuGet package, which this tool pins. (The runtime also ships Fluid, but only the OpenAI connector uses it.)
- **Naming:** `CSharpNamingConvention`. Filters are PascalCase (`Upcase`, `Date`, `Times`, `DividedBy`, `StripHtml`). Tags stay lowercase (`if`, `for`, `assign`).
- **Custom tag:** `{% JSONArrayFor item in collection [where Name] %}...{% endJSONArrayFor %}` writes a comma between iterations.
- **Input:** wrapped as `content`.
  - **JSON:** parsed without date parsing (dates stay strings). Integers become `long`, decimals become `decimal`, `null` becomes `""`.
  - **XML:** lookups use the local name only (namespaces are ignored), are case-insensitive, and return the **first** matching element.
- **Rendering:** `Template.Render(Hash)`. Render errors do not fail the action: they are written into the output as `Liquid error: ...`. Missing values render as empty.
- **Output:** for `JsonToJson` and `XmlToJson` the rendered text is parsed as JSON (Newtonsoft.Json 13.0.3), and invalid JSON fails the action.
- **Culture:** decimal literals are parsed, and numbers and dates formatted, with the current culture of the host.
- **Time zone:** `Date` converts dates with a time zone (`Z`, `+02:00`) to the host's local time and uses the host clock for `now`. In Azure that is UTC, unless the app setting `WEBSITE_TIME_ZONE` is set. The tool emulates UTC by default; a plain run on a laptop in Central European Time would otherwise shift times by one or two hours.

### Logic Apps Consumption

Microsoft documents that both Consumption (maps in an integration account) and Standard use DotLiquid 2.0.361 with the same C# filter names, `JSONArrayFor` and `content` wrapper. The tool works for Consumption maps in the same way.

The difference is in the evidence: the conformance tests run against the **Standard** runtime, because only that runtime can be installed locally. For Consumption, equality is documented by Microsoft, but not proven by the tests.

Consumption has no `WEBSITE_TIME_ZONE` app setting, so the default `--time-zone UTC` applies. The culture is assumed to be en-US, as for Standard.

## Installing

| Way | Command or link | Needs |
|---|---|---|
| .NET tool from NuGet | `dotnet tool install --global LogicApps.Liquid.Cli` | .NET 10 |
| Executable (Windows, Linux, macOS) | [GitHub Releases](https://github.com/sahinozd/LogicAppsStandard.Liquid/releases) | nothing |
| .NET library | `dotnet add package LogicApps.Liquid` | .NET 10 |
| GitHub Action | `uses: sahinozd/LogicAppsStandard.Liquid@v1.0.0` | – |
| Azure DevOps step template | [pipelines/templates/steps/validate-liquid-maps.yml](pipelines/templates/steps/validate-liquid-maps.yml) | – |

**The [usage guide](USAGE_GUIDE.md) explains every way of installing, every command and option, test suites, Azure DevOps, GitHub Actions, VS Code and Visual Studio, and the .NET library.**

Releases are created by the [CI/CD workflow](.github/workflows/ci-cd.yml) when a version tag (`v1.2.3`) is pushed. Each release includes:
- the NuGet packages, published to NuGet.org and GitHub Packages
- self-contained executables for Windows, Linux and macOS
- checksums

## Commands

### `validate`

Checks maps without writing anything.

```powershell
la-liquid validate --map <file-or-folder> [--input <file>] [--kind <kind>] [engine options] [output options] [--warnings-as-errors]
```

- With a **folder**, every `*.liquid` file in it is checked (recursively). That's a good first step in a pipeline.
- With an **input**, the map is also rendered and the output is checked: a dry run of `transform`.

### `transform`

Validates, renders and writes the output. The output file is **only written when there are no errors**.

```powershell
la-liquid transform --map <file> --input <file> --output <file> [--kind <kind>] [--raw] [engine options] [output options] [--warnings-as-errors]
```

- JSON output is written indented, the way the Logic Apps run history shows it. Use `--raw` to get the exact rendered text.
- **Kind:** when `--kind` is omitted it is inferred. `.xml` input gives `XmlTo*`; a `.json` output gives `*ToJson` and any other extension gives `*ToText`. Set it explicitly when it differs from your Liquid action.

### `test`

Runs a [test suite](#test-suites) with expected outputs and writes a JUnit report.

```powershell
la-liquid test --suite liquid-tests.json [--junit results.xml] [--update-expected] [engine options] [output options] [--warnings-as-errors]
```

The findings of a passing test are expected, so they are only shown for failing tests (or with `--verbosity detailed`).

### `explain`

Explains a code: what happens in Logic Apps, an example and the fix. Without a code, it lists all codes per category.

```powershell
la-liquid explain LQ102
la-liquid explain
```

### Engine options

All commands accept these options:

| Option | Default | Meaning |
|---|---|---|
| `--culture` | `en-US` | Culture of the Logic Apps host. DotLiquid uses it to parse decimal literals and text, and to format numbers and dates. |
| `--time-zone` | `UTC` | Time zone of the Logic Apps host. Use the value of `WEBSITE_TIME_ZONE` when your Logic App sets it (Windows or IANA id). `local` uses this machine's time zone. |
| `--no-lint` | off | Skip the static checks. |
| `--no-deviations` | off | Skip the detection of filters that behave differently from Shopify Liquid (LQ1xx). |

For `test`, `--culture` and `--time-zone` override the `culture` and `timeZone` of the suite file.

### Output options

`validate`, `transform` and `test` accept these options:

| Option | Default | Meaning |
|---|---|---|
| `--format` | `pretty` | `pretty`: readable, grouped per file, with the source line and a summary per category. `msbuild`: one line per finding (`file(line,col): error CODE: message`), for the Visual Studio error list and the VS Code `$msCompile` problem matcher. `json`: one JSON document, for scripts. |
| `--verbosity` | `normal` | For `pretty`: `minimal` (one line per finding), `normal` (location, source line, values and fix) or `detailed` (also why). |
| `--no-color` | off | No colors. Colors are also off when `NO_COLOR` is set, or when the output is redirected outside a pipeline. |

### Exit codes

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | Validation errors or failed tests |
| 2 | Invalid arguments, or a file could not be read or written |

## Diagnostics

Each finding has a short title, the actual values, a fix and, with `--verbosity detailed`, an explanation. `la-liquid explain <code>` shows the full description with an example.

When two checks see the same problem, it is reported once, at the place in the map to fix:
- a `Liquid error` in the output caused by a filter call (LQ102, LQ101, LQ022) is reported at that call, as an error, with the output line
- a missing value caused by a special character in the path (LQ023) is reported with LQ025
- the parse error of DotLiquid (LQ001) is reported at the location the static checks found

**Pipelines:** the readable format is also the default in Azure DevOps and GitHub Actions; both show the colors. Every finding is also logged as an annotation (`##vso[task.logissue]` in Azure DevOps, `::error` / `::warning` in GitHub), so it appears on the run summary and, in GitHub, next to the changed lines of a pull request.

| Code | Severity | What |
|---|---|---|
| LQ001 | error | DotLiquid cannot parse the map; Logic Apps fails the action |
| LQ002 | error | Unknown tag, for example `{% If %}` (tags are case-sensitive) |
| LQ003 | error | Block never closed, or an end tag that doesn't match |
| LQ004 | error | `{{` or `{%` never closed |
| LQ010 | error | Filter in Ruby/Shopify style (`upcase`, `strip_html`), silently skipped by Logic Apps |
| LQ011 | error | Unknown filter, silently skipped; for example `| myProperty: value` does **not** filter an array |
| LQ020 | warning | Variable used but never assigned (input fields are under `content`) |
| LQ021 | warning | Comparison with `nil` (JSON `null` is `""` in Logic Apps) |
| LQ022 | error | `include` / `extends`: no template file system in Logic Apps |
| LQ023 | warning | A value was not found while rendering (for example a misspelled field); renders empty |
| LQ030 / LQ031 | error | Input is not valid JSON / XML |
| LQ040 | error | Render error; Logic Apps writes `Liquid error: ...` into the output |
| LQ041 | error | Output is not valid JSON for a `*ToJson` kind; Logic Apps fails the action |
| LQ042 | warning | The output is empty |
| LQ024 | warning | `{% if content.x %}` without a comparison: JSON `null` becomes `""`, which is truthy |
| LQ025 | warning | `.@code`, `.$content`, `.#text` or `.ns0:x` in a dot path: DotLiquid drops the character and reads another property |
| LQ026 | warning | `if`/`unless`/`case` directly inside `JSONArrayFor`: skipped items become `undefined` |
| LQ027 | warning | `Plus: 0` / `Times: 1` used as a text-to-number conversion: on text it joins or repeats |
| LQ028 | warning | Empty search text in `Replace`/`Remove`, for example `''''` meant as an escaped apostrophe: does nothing |
| LQ043 | warning | The JSON output contains empty array items, which become `undefined` |
| LQ100–LQ128 | warning | A filter behaves differently from Shopify Liquid, see [Where DotLiquid differs from Shopify Liquid](#where-dotliquid-differs-from-shopify-liquid) |

## Where DotLiquid differs from Shopify Liquid

DotLiquid's filters don't always behave like the original Ruby implementation, especially with numbers and decimals. Most of these differences don't fail the workflow; they silently change the output.

`validate --input` and `transform` detect them **with the actual values**:
- **How:** the map is rendered a second time. In that second render, each relevant filter is wrapped in an observer that records its real input and then calls the original DotLiquid filter.
- **What you get:** each warning has the line and column of the filter call, the actual result and the Shopify Liquid result.
- **Your output is not affected:** the output always comes from the normal render, and the tests verify that both renders give identical text.

A few checks also run without input (LQ024–LQ028, LQ112, LQ118, LQ120).

Every result below was confirmed on the real Logic Apps engine (conformance tests) and compared with the [Shopify Liquid source](https://github.com/Shopify/liquid/blob/main/lib/liquid/standardfilters.rb).

| Code | Example | Logic Apps | Shopify Liquid |
|---|---|---|---|
| LQ100 | `"2026" \| Plus: 10` (also after `Date: "yyyy"`) | `202610`: text is joined | `2036` |
| LQ101 | `"2026" \| Times: 3` | `202620262026`: text is repeated (negative fails) | `6078` |
| LQ102 | `"2026" \| Minus: 1` (also `DividedBy`, `Modulo`) | `Liquid error: Parameter count mismatch.` | `2025` |
| LQ103 | `"125,50" \| Times: 1.0` | `12550` on en-US: text is parsed with the host culture | `125.0` (`to_i` stops at the comma) |
| LQ105 | `"2026" \| Times: 1.0 \| Append: "-12-31"` | `2026.0-12-31`: trailing zeros kept in text filters (`{{ }}` shows `2026`) | `2026.0-12-31` |
| LQ106 | `2.5 \| Round` | `2`: banker's rounding | `3` |
| LQ107 | `"abc" \| Round` | empty | `0` |
| LQ108 | `-7 \| DividedBy: 2` | `-3`: truncates | `-4`: floors |
| LQ109 | `-7 \| Modulo: 3` | `-1` | `2` |
| LQ110 | `1.5 \| DividedBy: 0` | `∞` | error |
| LQ111 | `false \| Default: "x"` / `"  " \| Default: "x"` | `False` / `x` | `x` / `"  "` |
| LQ112 | `"1.234.5" \| Replace: ".", ","` (also `ReplaceFirst`, `RemoveFirst`) | `,,,,,,,`: search text is a regular expression | `1,234,5` |
| LQ113 | `"a,,b" \| Split: ","` / `"abc" \| Split: ""` | 2 items / 1 item | 3 items / 3 items |
| LQ114 | `nil \| Append: "x"` | empty | `x` |
| LQ115 | `"hELLO wORLD" \| Capitalize` | `HELLO WORLD` | `Hello world` |
| LQ116 | `"café" \| Escape` | `caf&#233;` | `café` |
| LQ117 | `items \| Sort: "n"` on JSON or XML input / `["b","A","a"] \| Sort` | **not sorted** / `a,A,b` (culture order) | sorted / `A,a,b` |
| LQ118 | `"2026-10-15T23:30:00Z" \| Date: "HH:mm"` | converted to the host time zone | keeps the time zone |
| LQ119 | `"03/04/2026" \| Date: "yyyy-MM-dd"` | `2026-03-04` on en-US, `2026-04-03` on nl-NL | `2026-04-03` |
| LQ120 | `Date: "%Y-%m-%d"` | `Y-0-15`: .NET format strings | `2026-10-15` |
| LQ121 | `"not a date" \| Date: "yyyy"` | returned unchanged | returned unchanged |
| LQ122 | `"now" \| Date: "yyyy"` | host clock and time zone | server clock |
| LQ123 | `"abc" \| Abs` / `"abc" \| AtLeast: 3` | `0` / `abc` | `0` / `3` |
| LQ124 | `12345 \| Size` | `0` | `8` |
| LQ125 | `["x","x","y"] \| Uniq` | `x,y`: reported because Uniq is often used as an array wrapper (see below) | `x,y` |
| LQ126 | `{% for x in singleObject %}` | renders nothing | iterates the hash |
| LQ127 | `{{ content.address }}` / `{{ content.items }}` (objects) | nothing / `Microsoft.Azure.Function.Common.JObjectDrop…` | the hash / array as text |
| LQ128 | `content.Root.Packages \| Sort: "Weight"` (also `Compact`, `Uniq`) on XML elements | the elements are flattened into all their child values (`12.5, 2, 3.75, 1`) | sorted elements |

The working conversion from text to a whole number is `| Times: 1.0 | Round`, for example `{{ "2026" | Times: 1.0 | Round | Plus: 10 }}` gives `2036`.

Two rules keep the warnings useful:
- **Backslash escapes:** a `Replace` search text with a backslash escape (`'\\'`, `'\.'`) is treated as an intentional regular expression and isn't flagged. `Replace: '\\', '\\'` correctly escapes backslashes for JSON *because* DotLiquid uses regular expressions.
- **Trailing zeros (LQ105):** reported only where such a value reaches a text filter, not when it's written to the output directly.

Use `--no-lint` to see only what the Logic Apps engine itself reports (LQ001, LQ023 and up).

## Patterns found in real-world maps

To test the checks on real code, I ran the tool on a large set of production Liquid maps from several Logic Apps integration platforms, both Standard and Consumption. I read their comments for workarounds and verified each pattern on the real Logic Apps engine.

The patterns below are generalised: names and values are examples, not taken from the maps. They come up again and again in practice; each one is now flagged, together with the working alternative.

| Pattern | What actually happens in Logic Apps | Code | Use instead |
|---|---|---|---|
| `\| Times: 1.0` (with a decimal, not `1`) to make text numeric | Works, but the result is a decimal (`2026.0`) once it reaches a text filter | LQ105 | `\| Times: 1.0 \| Round` for whole numbers |
| `\| Plus: 0` or `\| Times: 1` to make text numeric | On text: `"123" \| Plus: 0` gives `1230`; `Times: 1` leaves it text | LQ027, LQ100, LQ101 | `\| Times: 1.0` (`\| Round`) |
| `\| Round: n` to make text numeric, with `n` counted from the decimals | Works, but text is parsed with the host culture, and halves round to even | LQ103, LQ106 | `\| Times: 1.0` |
| `\| Times: -1` and then `Plus`, as a replacement for `Minus` | `Minus` on text fails; `Plus` on text joins (`"2026" \| Plus: -45` gives `2026-45`) | LQ100, LQ102 | Convert first: `\| Times: 1.0 \| Round \| Minus: 45` |
| `x \| Concat: x \| Uniq` to turn "one object or an array" into an array | `Concat` doesn't exist and is skipped; `Uniq` does the wrapping, and **removes duplicate values** | LQ011, LQ125 | `\| Compact`: wraps without removing duplicates |
| `{% for x in content.element %}` where the XML had one element | XML converted to JSON gives an object for a single element, not an array: the loop renders nothing | LQ126 | `{% assign items = content.element \| Compact %}` |
| `{% if x.first %}` to tell an array from an object | Works: `.first` of an object is empty | – | Keep, or simply use `\| Compact` |
| `JSONArrayFor` with an `if` inside; replaced by a `for` with manual commas and code that removes the last comma | `JSONArrayFor` also writes a comma for skipped items; the JSON gets `undefined` items | LQ026, LQ043 | Select the items first, then loop over the selection |
| `content.item.@code`, `.$content`, `.#text` (names from XML converted to JSON) | The special character is dropped, so another property is read, without any error | LQ025 | `content.item["@code"]` |
| `\| Replace: '''' ,'&apos;'` to escape an apostrophe for XML | `''''` is two empty strings: nothing is replaced | LQ028 | `\| Replace: "'", '&apos;'` |
| `\| Replace: '\\', '\\' \| Replace: '"', '\"'` to escape text for JSON | Correct and needed: DotLiquid has no JSON escape filter. Because `Replace` uses a regular expression, `'\\'` matches one backslash | – (not flagged) | Keep it for every text value written into JSON |
| `\| Date: "yyyy-MM-ddTHH:mm:ssZ"` | Prints a literal `Z` after a time that is converted to the host time zone; only correct while the Logic App runs in UTC | LQ118 | `K` for the real offset, or keep the app in UTC |
| `\| Append: '     ' \| Truncate: 5, ''` to pad text to a fixed length | Works the same as in Shopify Liquid (there is no pad filter) | – | Keep |
| `\| type: 'A'` (a property name used as a filter) to select array items | Unknown filters are skipped: nothing is selected, and `[0]` is just the first item | LQ011 | A `for` loop with an `if` |
| `{{ content \| Json }}` to include the whole message, for example in a notification | `Json` doesn't exist and is skipped; an object prints **nothing**, so the message never ends up in the output | LQ011, LQ127 | Print the fields, or pass the JSON as text in the input of the Liquid action |
| `\| Abs \| Times: 100` to convert text to a number | Works, but drops the minus sign (`"-1.5"` gives `150`) and gives `0` for text that isn't a number | LQ027, LQ123 | `\| Times: 1.0 \| Times: 100` |
| `{% if x != null %}` to skip empty input | JSON `null` becomes `""`, which isn't nil: the condition is true for null values | LQ021 | `{% if x != null and x != "" %}` |

## Related tools

There are tools that **render** DotLiquid 2.0.361 templates:
- [LiquidTester](https://liquidtester.se/Home/About): browser-based
- [DotLiquid Debugger](https://dev.to/imdj/debug-dotliquid-templates-locally-with-the-vs-code-dotliquid-debugger-2eb3): VS Code preview and step debugger
- Older CLI renderers such as [LiquidTransformation](https://skastberg.com/2019/01/20/test-your-liquid-transformations-without-deployment/)

They are useful for seeing the output while you write a map.

`la-liquid` adds what none of them does:
- **Same as Logic Apps:** it reproduces the Logic Apps engine setup, including culture and time zone, and proves equality with the real runtime in conformance tests.
- **Checks and warnings:** it finds Logic Apps pitfalls (silently skipped filters, `null` handling, special characters in paths), and the deviations from Shopify Liquid using the actual values.
- **Pipelines:** it gives exit codes, file and line findings, test suites and JUnit reports.

Some deviations are known in the DotLiquid project itself, for example [Replace uses regular expressions](https://github.com/dotliquid/dotliquid/issues/385) and the [missing json filter](https://github.com/dotliquid/dotliquid/issues/161).

## Test suites

A test suite pins a map's behaviour to known outputs, which is useful as a regression test in a pipeline. Paths are relative to the suite file.

```json
{
  "culture": "en-US",
  "timeZone": "UTC",
  "warningsAsErrors": false,
  "tests": [
    {
      "name": "Orders to shipment request",
      "map": "../Artifacts/Maps/orders.to.shipment.liquid",
      "input": "inputs/orders.json",
      "kind": "JsonToJson",
      "expectedOutput": "expected/shipment.json"
    },
    {
      "name": "Known Logic Apps behaviour is accepted",
      "map": "../Artifacts/Maps/legacy.liquid",
      "input": "inputs/legacy.json",
      "expectedDiagnostics": [ "LQ021" ]
    }
  ]
}
```

- **Output comparison:** JSON outputs are compared structurally. Text outputs are compared line by line, ignoring line endings.
- **`expectedDiagnostics`:** these codes must be reported, and they don't fail the test.
- **`--update-expected`:** writes the actual outputs into the expected files. Review the changes before you commit them.

See [samples/liquid-tests.json](samples/liquid-tests.json) for a working suite.

## Azure DevOps

[pipelines/templates/steps/validate-liquid-maps.yml](pipelines/templates/steps/validate-liquid-maps.yml) installs the tool, validates all maps of a Logic App and runs a test suite. Findings appear on the run summary, and test results on the Tests tab.

```yaml
steps:
  - template: templates/steps/validate-liquid-maps.yml
    parameters:
      mapsFolder: '$(Build.SourcesDirectory)/src/LogicApp/Artifacts/Maps'
      testSuite: '$(Build.SourcesDirectory)/tests/Liquid/liquid-tests.json'
      warningsAsErrors: false
      toolFeed: 'https://pkgs.dev.azure.com/<org>/_packaging/<feed>/nuget/v3/index.json' # optional
```

[pipelines/azure-pipelines.yml](pipelines/azure-pipelines.yml) builds this repository. It runs the unit and conformance tests against a workflow bundle downloaded from the public Azure Functions CDN, then packs the tool.

## VS Code and Visual Studio

Open the folder in VS Code or `LogicAppsStandard.Liquid.slnx` in Visual Studio.

The VS Code tasks (**Terminal → Run Task**):
- **la-liquid: validate current map:** validates the open `.liquid` file; findings show up in the Problems panel (the tasks use `--format msbuild` for the problem matcher).
- **la-liquid: transform current map:** asks for an input and output file.
- **la-liquid: test samples**, plus **build** and **test**.

There are also two launch configurations to debug the CLI.

## How conformance is verified

`tests/Liquid.ConformanceTests` loads the **real** Logic Apps Liquid engine (`LiquidTransformer` from `Microsoft.Azure.Workflows.Integration.Edge.dll`) from the workflow extension bundle, in an isolated `AssemblyLoadContext`. It then runs every case through both engines, for both the `en-US` and `nl-NL` cultures. A case passes only when the outputs are identical, or both engines fail.

- **Where the bundle comes from:** locally it is the bundle installed by the Azure Logic Apps (Standard) VS Code extension, found under `%USERPROFILE%\.azure-functions-core-tools\Functions\ExtensionBundles`. Set `LOGICAPPS_WORKFLOW_BUNDLE_BIN` to use another `bin` folder.
- **No bundle:** the tests are reported as ignored.
- **When a new bundle ships:** run the conformance tests against it before trusting the tool with it.

## Known limitations

- **Deviation detection needs input:** the LQ1xx checks run while rendering, so they only see the values in your test input. A filter that is never reached, or never gets a problem value, isn't reported.
- **One transform at a time per process:** DotLiquid's configuration and the emulated time zone (set through .NET's time zone cache) are process-wide. The library therefore runs transforms in one process one at a time. Use several processes for parallel work.
- **XML is read as UTF-8:** Logic Apps loads XML from the bytes of the message, and the tool does the same with the UTF-8 text of the file. A file in UTF-16 is not supported, and an XML declaration with `encoding="utf-16"` on UTF-8 content is reported as invalid, just as Logic Apps rejects it.
- **Culture of the Azure host:** the default `en-US` is an assumption. To verify it for your environment, deploy a workflow with a Liquid action rendering `{{ 1.5 | Times: 2 }}`. `3` means en-US style parsing.
- **Static checks are heuristic:** they work on the template text. The variable checks (LQ020) can miss usages in unusual expressions. The engine checks (LQ001, LQ023, LQ040, LQ041) come from DotLiquid itself.
- **No output schema validation:** the Liquid action's optional output JSON schema is not supported yet.
- **The `$content` envelope:** XML input is read from a plain XML file. Base64 content envelopes are not supported as input files.

## License

[MIT](LICENSE)
