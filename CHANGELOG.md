# Changelog

All notable changes to `la-liquid` and the `LogicApps.Liquid` library. The release pipeline publishes the section of a version as its release notes, so every version tag needs a section here.

## [1.4.0] - Unreleased

### Added
- LQ006: an extra `{` at the start of an output (`{{{ x }}`). Logic Apps doesn't fail, but writes `Liquid error: Value cannot be null. (Parameter 'input')` instead of the value. Without a space after the extra brace it happens to work, which is reported as a warning. The cryptic render error is merged into this finding, at the place in the map to fix.

## [1.3.0] - 2026-10-27

### Added
- The tool and the library also target .NET 8. The Logic Apps extension for VS Code installs a .NET 8 SDK and puts it first in the `PATH` of the VS Code terminal; installing the tool there failed with "Settings file 'DotnetToolSettings.xml' was not found in the package". It now installs with .NET 8 or later.
- The unit and conformance tests run on both .NET 8 and .NET 10.

## [1.2.1] - 2026-10-15

Maintenance release: no changes in what the tool reports.

### Changed
- The conformance tests run in `en-US` only, the culture of Logic Apps in Azure.
- Documentation and examples use Central European Standard Time (`Europe/Berlin`) for the time zone, and describe culture differences as "a comma as decimal separator" or "a day-month culture".

### Fixed
- All ReSharper warnings: the filter methods that DotLiquid calls through reflection are marked as such, static readonly fields use PascalCase, and a broken reference in a documentation comment is fixed.
- The one-time configuration of DotLiquid takes one lock instead of two.

## [1.2.0] - 2026-10-15

New checks, found by comparing the Logic Apps engine with Shopify Liquid, Microsoft's documentation, DotLiquid's issue list and questions on Microsoft Q&A. Every finding is confirmed on the real Logic Apps engine by the conformance tests.

### Added
- LQ005: a `}` inside `{{ }}`, also inside quotes (`{{ x | Default: '{}' }}`), ends the output, so the map fails to parse.
- LQ029: `forloop` properties with the wrong casing (`forloop.Last`, also in Microsoft's documentation sample) are always empty.
- Conditions and loops (new category):
  - LQ050: `{% else if x %}` is a plain `else`, the condition is ignored
  - LQ051: parentheses in a condition
  - LQ052: a filter in a condition is ignored
  - LQ053: `forloop` inside `JSONArrayFor` is always empty
  - LQ054: a comparison or `and`/`or` in `assign` is not evaluated
  - LQ055: two texts with numbers are compared as text (`'100' < '20'`), typical for XML input
- Differences from Shopify Liquid, found with the actual values:
  - LQ129: whole numbers above 2,147,483,647 overflow in math filters (IDs, amounts in cents, timestamps in milliseconds)
  - LQ130: `contains` on a list of text is always false
  - LQ131: text compared with a number gives a wrong result (`'999.50' > 1000` is true)
  - LQ132: `blank` doesn't match a missing value or whitespace, `empty` doesn't match an empty object
  - LQ133: `true`/`false` becomes `True`/`False` in a text filter, which breaks JSON output
- LQ121 also reports a number passed to `Date` (Shopify Liquid reads it as a Unix timestamp), and LQ124 also reports `Size` of an object.
- Hints for engine errors: a text filter on a list (`Default`, `Slice`), `Sort` with mixed types, a list index out of range, a list filter on a single value, and invalid JSON from `True`/`False` or a `Liquid error`.
- Missing values (LQ023) explain `.0` (use `[0]`) and `.Size` (use `.size`), and say "has no value" for variables that don't come from the input.

### Fixed
- A second finding with the same code on the same line was dropped (two `Minus` calls on one line).
- LQ113 reported a difference for `Split: ' '`, which splits on runs of whitespace in Shopify Liquid too.
- Render errors named the tool's own type for an input value; they now name the runtime's type, as Logic Apps does.
- LQ043 called an empty value (`"x": ,`) an empty array item.
- The symbols for error, warning and success (✖ ⚠ ✔) are emoji, which Windows Terminal draws two columns wide, so they covered the space after them. They are now ✗ ▲ ✓.

## [1.1.0] - 2026-10-15

### Added
- Readable output, now the default (`--format pretty`): findings grouped per map and sorted by line, the source line with a marker under the problem, the actual values and the fix, and a summary per category. Colors in the terminal and in Azure DevOps and GitHub Actions logs.
- `--format pretty|msbuild|json`, `--verbosity minimal|normal|detailed` and `--no-color` (also `NO_COLOR`).
- `la-liquid explain [code]`: what a code means, an example and the fix. Without a code it lists all codes.
- Library: `DiagnosticCatalog` with the documentation of every code, and `Diagnostic.Title`, `Values`, `Explanation`, `Fix` and `OutputLine`.

### Changed
- Findings that describe the same problem are reported once, at the place in the map to fix: a `Liquid error` caused by `Minus`/`Times` on text or by `include` is reported at that call, as an error, with the output line; a missing value caused by `.@x` is reported with LQ025; the DotLiquid parse error is reported at the location the static checks found.
- A filter in a loop is reported once per location, with up to three different values.
- Missing values (LQ023) are located at their first use in the map, and static findings point at the problem itself (the filter or path) instead of the start of the tag.
- `test` shows the findings of a test only when it fails (or with `--verbosity detailed`).
- **Scripts that read the output:** the old one-line format is still available with `--format msbuild`; `--format json` is better for scripts.

### Fixed
- `{% include %}` and `{% extends %}` were also reported as unclosed blocks (LQ003).

## [1.0.2] - 2026-10-15

### Added
- LQ128: `Sort`, `Compact` and `Uniq` on XML elements flatten them into all their child values.

### Fixed
- False LQ020 (undefined variable) for path segments with special characters, such as `.@code`.

## [1.0.1] - 2026-10-15

### Changed
- Packages are published to NuGet.org with Trusted Publishing.

## [1.0.0] - 2026-10-15

First release: `validate`, `transform` and `test`, with the static checks, the detection of deviations from Shopify Liquid, conformance tests against the Logic Apps runtime, the Azure DevOps step template and the GitHub Action.
