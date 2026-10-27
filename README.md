# Logic Apps Standard - Liquid validator and runner

`la-liquid` validates Liquid maps and runs them **exactly like the Azure Logic Apps Liquid action**, without deploying anything. Use it on your machine, in Azure DevOps and in GitHub Actions.

```text
> la-liquid validate --map samples/maps/common-mistakes.liquid --input samples/inputs/common-mistakes.json
samples/maps/common-mistakes.liquid  JsonToJson (inferred)

  ✗ error   LQ010  Filter 'upcase' is skipped by Logic Apps
     at samples/maps/common-mistakes.liquid:6:32
     6 │     "name": "{{ content.name | upcase }}",
       │                                ^^^^^^
     fix    Use 'Upcase'.

  ▲ warning LQ021  Comparison with nil misses JSON null values
     at samples/maps/common-mistakes.liquid:8:40
     8 │     "hasEmail": {% if content.email == nil %}false{% else %}true{% endif %},
       │                                        ^^^
     fix    Also compare with "", for example 'x != nil and x != ""'.

✗ Validation failed  1 map · 1 error · 1 warning
  filters               1 error  LQ010
  values & paths        1 warning  LQ021
  More about a code: la-liquid explain LQ010
```

## Why

I built this tool because Liquid in Logic Apps is hard to debug during development.

Testing a change to a map used to go like this for me: commit the map, let the Azure DevOps pipeline deploy the Logic App to the development environment, run the workflow manually or with a transformation test from the [testing framework](https://github.com/sahinozd/LogicAppsStandard.Testing), and open the run history to see what the Liquid action did. A full deployment easily takes 10 to 30 minutes. Per attempt, per environment: once it works in development, the same again for test.

And most of the time, the run history didn't even show a failure. The workflow was green, but a property was empty, or a value was simply wrong.

Existing tools can render a DotLiquid template, but they don't run it with the setup of Logic Apps, and they don't tell you *what* is wrong. I wanted answers before deploying anything:

- Does this map behave in Logic Apps the way I think it does?
- And if not, where exactly, and what should I write instead?

That is what `la-liquid` does: on your machine in seconds, and in your Azure DevOps or GitHub pipeline before anything is deployed.

### What goes wrong

The Liquid that Logic Apps runs is not the Liquid in the Shopify documentation, and most mistakes do **not** fail the workflow. They silently produce a wrong output:

| You write | Shopify Liquid | Logic Apps |
|---|---|---|
| `{{ name \| upcase }}` | `SAHIN` | `sahin`: the filter is unknown and skipped |
| `{{ items \| where: "id", 1 }}` | filtered array | the whole array: there is no `where` filter |
| `{% if value == nil %}` for JSON `null` | true | **false**: `null` becomes an empty string |
| `{{ "2026" \| Plus: 1 }}` | `2027` | `20261`: text is joined |
| `{{ 1.5 \| Times: 2 }}` on a host with a comma as decimal separator | `3` | `30`: the decimal is read with the host culture |

`la-liquid` runs the same engine with the same settings as Logic Apps, and reports these problems with file, line, the actual values and a fix.

## Install

| Way | Command | Needs |
|---|---|---|
| .NET tool | `dotnet tool install --global LogicApps.Liquid.Cli` | .NET 10 |
| Executable | [GitHub Releases](https://github.com/sahinozd/LogicAppsStandard.Liquid/releases) (Windows, Linux, macOS) | nothing |
| GitHub Action | `uses: sahinozd/LogicAppsStandard.Liquid@v1.0.2` | – |
| Azure DevOps | [step template](pipelines/templates/steps/validate-liquid-maps.yml) | – |
| .NET library | `dotnet add package LogicApps.Liquid` | .NET 10 |

## Quick start

```powershell
# Check every map in a folder
la-liquid validate --map ./Artifacts/Maps

# Run a map with a sample message; the output is only written when there are no errors
la-liquid transform --map ./Artifacts/Maps/order.liquid --input ./samples/order.json --output ./out/order.json

# Run a test suite with expected outputs, with a JUnit report for the pipeline
la-liquid test --suite ./tests/liquid-tests.json --junit ./TestResults/liquid.xml

# What does a code mean?
la-liquid explain LQ102
```

**[Usage guide](USAGE_GUIDE.md):** every command and option with examples, test suites, pipelines, editors and the library.

## What it checks

| Check | Needs input | Finds |
|---|---|---|
| Static checks | no | Syntax errors with their location, wrongly cased or unknown tags and filters, `nil`/`null` pitfalls, paths that read another property (`.@code`), risky idioms such as `Plus: 0` |
| Engine | yes | Parse and render errors exactly as Logic Apps reports them, missing values, invalid JSON output |
| Differences from Shopify Liquid | yes | Filters that give a different result than in Shopify Liquid **for the actual values**, for example `"2026" \| Plus: 1` giving `20261` |

The [diagnostics reference](docs/diagnostics.md) lists every code, the differences from Shopify Liquid, and the patterns found in real-world maps. `la-liquid explain` shows the same information in the terminal.

## How it matches Logic Apps

The Logic Apps Standard runtime (workflow extension bundle 1.170.91) runs Liquid like this, and `la-liquid` does the same:
- **Engine:** [DotLiquid](https://github.com/dotliquid/dotliquid) 2.0.361, the same binary.
- **Names:** filters in C# style (`Upcase`, `DividedBy`), tags lowercase (`if`, `for`), plus the `JSONArrayFor` tag.
- **Input:** available as `content`. JSON `null` becomes `""` and dates stay text; XML lookups ignore namespaces and case.
- **Errors:** render errors don't fail the action, but are written into the output as `Liquid error: ...`. For `*ToJson`, invalid JSON output fails the action.
- **Host:** culture `en-US` and time zone UTC, unless the app setting `WEBSITE_TIME_ZONE` is set. The tool emulates both, so a run on your machine gives the same result as Azure, whatever its regional settings.

**Proven, not assumed:** the conformance tests (`tests/Liquid.ConformanceTests`) load the real Logic Apps Liquid engine from the extension bundle. They run every case through both engines and require identical results. The input handling and `JSONArrayFor` are written from a [specification of the observable behaviour](docs/runtime-behaviour.md), not from Microsoft's code.

**Logic Apps Consumption** uses DotLiquid 2.0.361 with the same settings according to Microsoft, so the tool works the same way. The conformance tests can only prove this for Standard, because only that runtime can be installed locally.

## Related tools

[LiquidTester](https://liquidtester.se/Home/About), the [DotLiquid Debugger](https://dev.to/imdj/debug-dotliquid-templates-locally-with-the-vs-code-dotliquid-debugger-2eb3) for VS Code and older CLI renderers show the output of a DotLiquid map while you write it. `la-liquid` adds the Logic Apps setup (culture, time zone, `content`, `JSONArrayFor`) proven by conformance tests, the checks with fixes, and pipeline support: exit codes, annotations, test suites and JUnit reports.

## Known limitations

- **Differences from Shopify Liquid need input:** they are found while rendering, so only for the values in your input.
- **Static checks are heuristic:** they read the map text, and can miss unusual expressions.
- **Input files:** save XML input as UTF-8. When you copy an input from the run history that looks like `{"$content-type": ..., "$content": "PD94..."}`, decode the base64 first: the tool expects the plain XML.
- **One transform at a time per process:** DotLiquid's configuration and the emulated time zone are process-wide.

## Releases

A release is created when a version tag (`v1.2.3`) is pushed: NuGet packages on NuGet.org and GitHub Packages, executables for six platforms, and checksums. What changed is in the [changelog](CHANGELOG.md).

## License

[MIT](LICENSE)
