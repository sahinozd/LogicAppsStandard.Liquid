# Changelog

All notable changes to `la-liquid` and the `LogicApps.Liquid` library. The release pipeline publishes the section of a version as its release notes, so every version tag needs a section here.

## [1.1.0] - Unreleased

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
