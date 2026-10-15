# Assessment: la-liquid

Status on 15 October 2026. This assessment covers:
- what the tool does
- whether it covers all of DotLiquid as Logic Apps uses it
- whether it flags misuse and deviations from Shopify Liquid
- whether all transformation scenarios work
- what is missing
- a critical review of the work itself

All claims below are backed by tests:
- **Unit tests:** 146.
- **Conformance tests:** 148. These run every case through the real Logic Apps Liquid engine from workflow extension bundle 1.170.91, in both en-US and nl-NL, and require identical results.

---

## 1. What the tool does

`la-liquid` runs a Liquid map the way the Logic Apps Standard Liquid action does, and reports everything that would make the result different from what the author intended.

**The engine.** It uses the same DotLiquid 2.0.361 binary as the runtime, set up the same way:
- C# filter names
- the `JSONArrayFor` tag
- the input wrapped as `content`
- JSON read without date parsing
- XML loaded from bytes, with namespace-free and case-insensitive lookups
- `Render(Hash)`, so render errors end up in the output
- JSON output parsed with Newtonsoft 13.0.3

It also emulates the host **culture** (default en-US) and **time zone** (default UTC). Both change DotLiquid's results.

**Three layers of checks.**

| Layer | When | Finds |
|---|---|---|
| Static checks (linter) | Always, also without input | Syntax errors with location, wrongly cased or unknown tags and filters, unclosed blocks, multi-line tags, nil and null pitfalls, special characters in paths, risky idioms (`Plus: 0`, `''''`, `Concat`), `if` inside `JSONArrayFor` |
| Engine | With input | Parse and render errors exactly as Logic Apps reports them, missing values, invalid or `undefined` JSON output |
| Deviation detection | With input | A second, observing render records the real input of every relevant filter and loop call, and reports where DotLiquid behaves differently from Shopify Liquid (LQ100–LQ126), with line, column, actual result and Shopify result. The output always comes from the normal render. |

**Commands:**
- `validate`: a map or a folder, optionally with input.
- `transform`: writes the output only when there are no errors.
- `test`: a suite with expected outputs, plus a JUnit report.

**Output:** findings in the MSBuild format, which Visual Studio, VS Code and Azure DevOps link to the source; Azure DevOps logging commands; and exit codes 0, 1 or 2.

**Integration:** an Azure DevOps step template, a build pipeline, VS Code tasks and launch configurations, and a `.slnx` solution.

## 2. Coverage of DotLiquid as used by Logic Apps

Inventory, read from the DotLiquid assembly:

| Area | Items | Proven against the runtime |
|---|---|---|
| Tags | `assign`, `block`, `break`, `capture`, `case`, `comment`, `continue`, `cycle`, `extends`, `for`, `if`, `ifchanged`, `include`, `literal`, `raw`, `tablerow`, `unless`, plus Logic Apps' `JSONArrayFor` | All |
| Filters | All 43 (`Abs` … `UrlEncode`, including `H` and `Currency`) | All |
| Operators | `==`, `!=`, `<>`, `<`, `>`, `<=`, `>=`, `contains`, `startsWith`, `endsWith`, `hasKey`, `hasValue` | All |
| Literals and loops | `nil`, `null`, `blank`, `empty`, ranges, `reversed`, `limit`, `offset`, all `forloop` properties, `.size` / `.first` / `.last` | All |
| Not in DotLiquid 2.0.361 | `increment`, `decrement`, `echo`, `render`, `liquid` tags; `where`, `concat`, `reverse`, `ceil`, `floor` and other filters | The linter reports them as "exists in Shopify Liquid, not in DotLiquid" |

The coverage run also found and fixed linter false alarms: word operators reported as variables, `extends` reported as unclosed, and `literal` content being checked. It also exposed that DotLiquid does not allow line breaks inside a tag; this is now an error with a location.

## 3. Detection of misuse and of differences from Shopify Liquid

The deviations were collected from:
- the DotLiquid source
- the Shopify Liquid source
- the article on numbers and decimals
- a large set of production maps from several integration platforms (Standard and Consumption), including their comments about workarounds

Each one is confirmed on the real engine.
- **Numbers and decimals:**
  - text arithmetic joins, repeats or fails (LQ100–LQ102, LQ027)
  - text is parsed with the host culture (LQ103)
  - trailing zeros appear once a decimal reaches text filters (LQ105)
  - banker's rounding (LQ106)
  - integer division and modulo of negative numbers (LQ108–LQ109)
  - division by zero renders `∞` (LQ110)
- **Text:**
  - `Replace` uses regular expressions (LQ112; intentional `\\` escapes are not flagged)
  - `Split` drops empty items (LQ113)
  - `Default` (LQ111), `Append` on nil (LQ114), `Capitalize` (LQ115), `Escape` (LQ116)
  - empty search texts and `''''` (LQ028)
- **Arrays:**
  - `Sort` by property doesn't sort JSON or XML input (LQ117)
  - `Uniq` removes duplicates (LQ125)
  - a loop over a single object renders nothing (LQ126)
  - `if` inside `JSONArrayFor` (LQ026, LQ043)
  - fields used as filters (LQ011)
- **Dates:** time zone conversion and a literal `Z` (LQ118), culture-dependent parsing (LQ119), strftime formats (LQ120), unparseable values (LQ121), `now` (LQ122).
- **Paths and null:** `.@x`, `.$x`, `.#x` and `.ns:x` read another property (LQ025); JSON `null` is `""`, which is truthy and not nil (LQ021, LQ024).

**Result on the production maps.** The checks found real issues in every collection. The examples below are generalised:
- property names used as a filter, so nothing is selected
- `Concat` + `Uniq` used to wrap a single object, which works by accident and removes duplicates
- attribute paths that read the wrong property
- conversion idioms that do not convert text (`Plus: 0`, `Times: 1`, and `Abs` that drops the sign)
- apostrophe "escapes" that do nothing
- dates printed with a literal `Z`
- `{{ content | Json }}` used to include the whole message, which prints nothing
- `!= null` checks that are true for JSON null values

There were no false alarms among the new checks. Intentional regex escaping is recognised and not flagged.

**Known gap.** The runtime checks see only the values in the test input. A deviation that needs a value the input doesn't contain is not reported. The static checks partly cover this.

## 4 + 5. Transformation scenarios and the four transform kinds

| Kind | Verified scenarios | Verdict |
|---|---|---|
| JsonToJson | Object, array and plain value as the root of the output; empty output (null, action succeeds); trailing commas, comments, single quotes and duplicate keys accepted; invalid output fails; `undefined` items flagged | Works, identical to the runtime |
| JsonToText | Object, array, string, number and empty object as the root of the input; unicode, escapes, exponent notation, deep nesting; CSV with CRLF; SOAP/XML text; render errors in the output | Works, identical to the runtime |
| XmlToJson | Namespaces ignored, case-insensitive lookups, first match, `JSONArrayFor ... where`, single versus repeated elements | Works, identical to the runtime |
| XmlToText | Attributes not accessible, CDATA, mixed content (text lost), empty element, default namespace, XML declaration; a contradicting `encoding="utf-16"` declaration fails, as in the runtime | Works, identical to the runtime |

The scenario run found two real differences, both fixed:
- **Empty JSON output:** the runtime returns `null` and succeeds; the tool used to report an error.
- **XML loading:** the runtime loads XML from bytes; the tool used to parse it as text and accepted a wrong encoding declaration.

**Not supported:** UTF-16 XML input files, base64 `$content` envelopes as input files, and the output JSON schema (`TransformedContentSchema`) of the Liquid action.

## 6. Missing functionality and opportunities

Ranked by value for the effort:

1. **Read `workflow.json`:** find every Liquid action, with its map name and kind, so `validate` can check a whole Logic App project without configuration. A wrong kind is a common mistake.
2. **Test inputs from real runs:** use the LogicAppsStandard.Testing framework to fetch the actual input of a Liquid action from the run history and store it as a test case. The deviation checks then run on production-like values.
3. **Output JSON schema** (`TransformedContentSchema`): validate the output against the schema configured on the action. Needs a licence-friendly schema library; Newtonsoft.Json.Schema is AGPL.
4. **Automatic fixes:**
   - `upcase` → `Upcase`
   - `.@x` → `["@x"]`
   - `Concat: x | Uniq` → `Compact`
   - `''''` → `"'"`
   - `Plus: 0` → `Times: 1.0`
5. **SARIF output:** for GitHub and Azure DevOps code scanning.
6. **Bundle drift check:** a scheduled pipeline that runs the conformance tests against the newest workflow bundle on the CDN and reports when Microsoft changes the engine.
7. **VS Code extension:** live diagnostics while typing, using the library. The existing DotLiquid debugger extension shows the output; this would add the checks.
8. **UTF-16 and envelope input,** and the culture probe as a sample workflow.

## 7. Critical review

| # | Finding | Status |
|---|---|---|
| 1 | Empty JSON output reported as an error; Logic Apps succeeds with null | Fixed, proven by a conformance test |
| 2 | XML parsed as text; Logic Apps loads bytes, so encoding declarations behave differently | Fixed, proven by a conformance test |
| 3 | Tokenizer accepted tags spanning lines; DotLiquid rejects them | Fixed: error LQ004 with location |
| 4 | Linter false alarms: word operators, `extends`, `literal` | Fixed, with tests |
| 5 | Local runs used the laptop's time zone (+1 or +2 hours) | Fixed: UTC emulation, also applied to the runtime in conformance tests |
| 6 | Parallel use of the library could mix culture, time zone and observations | Fixed: process-wide lock |
| 7 | The Shopify reference for `"125,50"` was wrong (`to_i` gives 125) | Fixed |
| 8 | **The default culture en-US for Azure is an assumption** | Open: verify once with the probe workflow from the README |
| 9 | **The Azure DevOps pipelines have never run**, and the step template installs `LogicApps.Liquid.Cli` from a feed where it isn't published yet | Open: run once and publish the package |
| 10 | `JTokenDrop`, `XElementDrop` and `JsonArrayFor` reproduce the behaviour of Microsoft's runtime classes and were written after reading decompiled code. They are small, and behaviour cannot be copyrighted, but the structure is close to the original | Open: decide before publishing. A clean-room rewrite from the documented behaviour (now pinned by the conformance tests) removes the risk |
| 11 | The linter's variable and path checks are regex based; unusual expressions can be missed | Accepted, documented |
| 12 | The observing render rewrites tag names inside `raw` and `comment` too | Harmless: the observing output is not used, and observations come from real filter calls only |
| 13 | Diagnostics for the rendered output use a path such as `map.liquid (rendered output)`, which is not a clickable file in VS Code | Minor, open |
| 14 | No versioning or release process (version 1.0.0) | Open |
| 15 | An array of objects in output printed the tool's own type name; the runtime prints `Microsoft.Azure.Function.Common.JObjectDrop` | Fixed, proven by a conformance test |
| 16 | CS8795 in Visual Studio: the regex source generator did not run in the IDE | Fixed: plain static `Regex` fields, no source generator needed |
| 17 | Consumption runs maps in the integration account; the conformance tests can only use the Standard runtime | Documented: same DotLiquid 2.0.361 according to Microsoft, not proven by the tests |
