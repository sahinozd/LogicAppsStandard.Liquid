# Diagnostics reference

Every finding has a code. `la-liquid explain <code>` shows the same information in the terminal, with an example and the fix.

- **Errors** fail the command: Logic Apps fails, or silently produces a wrong result.
- **Warnings** don't, unless you use `--warnings-as-errors`.

## Codes

### Syntax and tags

| Code | Severity | What |
|---|---|---|
| LQ001 | error | DotLiquid cannot parse the map; Logic Apps fails the action |
| LQ002 | error | Unknown tag, for example `{% If %}` (tags are case-sensitive) or Shopify's `render` |
| LQ003 | error | Block never closed, or an end tag that doesn't match |
| LQ004 | error | `{{` or `{%` never closed, or spread over several lines |
| LQ005 | error | A `}` inside `{{ }}`, also inside quotes (`{{ x \| Default: '{}' }}`): ends the output, so the map fails to parse |

### Filters

| Code | Severity | What |
|---|---|---|
| LQ010 | error | Filter in Shopify style (`upcase`, `strip_html`): unknown, so skipped |
| LQ011 | error | Filter that doesn't exist (`where`, `json`, `concat`, a property name): skipped |
| LQ027 | warning | `Plus: 0`, `Times: 1` or `Abs` used to turn text into a number |
| LQ028 | warning | Empty search text in `Replace`/`Remove`, for example `''''` meant as an apostrophe |

### Values and paths

| Code | Severity | What |
|---|---|---|
| LQ020 | warning | Variable never assigned (input fields are under `content`) |
| LQ021 | warning | Comparison with `nil`: JSON `null` is `""` in Logic Apps |
| LQ022 | error | `include` / `extends`: Logic Apps has no template file system |
| LQ023 | warning | Value not found in the input (for example a misspelled field); renders empty |
| LQ024 | warning | `{% if content.x %}` without comparison: true for JSON `null` |
| LQ025 | warning | `.@code`, `.$content`, `.#text` or `.ns0:x` in a dot path: another property is read |
| LQ026 | warning | `if`/`unless`/`case` inside `JSONArrayFor`: skipped items become `undefined` |
| LQ029 | error | `forloop.Last`, `forloop.Index`: forloop properties are lowercase, so these are always empty |

### Conditions and loops

| Code | Severity | What |
|---|---|---|
| LQ050 | error | `{% else if x %}`: the text after `else` is ignored, so it is a plain `else` |
| LQ051 | error | Parentheses in a condition: Liquid has none, and the condition is evaluated differently |
| LQ052 | error | A filter in a condition (`{% if x \| Size > 5 %}`) is ignored: only checks whether x is set |
| LQ053 | error | `forloop` inside `JSONArrayFor`: that tag has no forloop |
| LQ054 | error | A comparison or `and`/`or` in `assign` is not evaluated: the variable gets the first value |
| LQ055 | warning | Two texts with numbers are compared as text: `'100' < '20'`. Values from XML are always text. Found while rendering |

### Input and output

| Code | Severity | What |
|---|---|---|
| LQ030 / LQ031 | error | Input is not valid JSON / XML |
| LQ040 | error | Render error: Logic Apps writes `Liquid error: ...` into the output |
| LQ041 | error | Output is not valid JSON for a `*ToJson` kind; Logic Apps fails the action |
| LQ042 | warning | The output is empty |
| LQ043 | warning | The JSON output has an empty array item or value (`"x": ,` from `{{ x }}` without quotes), which becomes `undefined` |

### Differences from Shopify Liquid (LQ100–LQ133)

DotLiquid's filters don't always behave like Shopify's Ruby implementation, especially with numbers. `validate --input`, `transform` and `test` find these differences **with the actual values**:
- **How:** the map is rendered a second time, with each relevant filter wrapped in an observer. The observer records the real input and calls the original filter.
- **Your output is not affected:** it comes from the normal render.

A few of these checks also run without input (LQ112, LQ118, LQ120).

Every result was confirmed on the real Logic Apps engine and compared with the [Shopify Liquid source](https://github.com/Shopify/liquid/blob/main/lib/liquid/standardfilters.rb).

| Code | Example | Logic Apps | Shopify Liquid |
|---|---|---|---|
| LQ100 | `"2026" \| Plus: 10` (also after `Date: "yyyy"`) | `202610`: text is joined | `2036` |
| LQ101 | `"2026" \| Times: 3` | `202620262026`: text is repeated (negative fails) | `6078` |
| LQ102 | `"2026" \| Minus: 1` (also `DividedBy`, `Modulo`) | `Liquid error: Parameter count mismatch.` | `2025` |
| LQ103 | `"125,50" \| Times: 1.0` | `12550` on en-US: text is read with the host culture | `125.0` |
| LQ105 | `"2026" \| Times: 1.0 \| Append: "-12-31"` | `2026.0-12-31`: trailing zeros kept in text filters | `2026.0-12-31` |
| LQ106 | `2.5 \| Round` | `2`: banker's rounding | `3` |
| LQ107 | `"abc" \| Round` | empty | `0` |
| LQ108 | `-7 \| DividedBy: 2` | `-3` | `-4` |
| LQ109 | `-7 \| Modulo: 3` | `-1` | `2` |
| LQ110 | `1.5 \| DividedBy: 0` | `∞` | error |
| LQ111 | `false \| Default: "x"` / `"  " \| Default: "x"` | `False` / `x` | `x` / `"  "` |
| LQ112 | `"1.234.5" \| Replace: ".", ","` (also `ReplaceFirst`, `RemoveFirst`) | `,,,,,,,`: the search text is a regular expression | `1,234,5` |
| LQ113 | `"a,,b" \| Split: ","` / `"abc" \| Split: ""` | 2 items / 1 item | 3 items / 3 items |
| LQ114 | `nil \| Append: "x"` | empty | `x` |
| LQ115 | `"hELLO wORLD" \| Capitalize` | `HELLO WORLD` | `Hello world` |
| LQ116 | `"café" \| Escape` | `caf&#233;` | `café` |
| LQ117 | `items \| Sort: "n"` on JSON or XML / `["b","A","a"] \| Sort` | **not sorted** / `a,A,b` | sorted / `A,a,b` |
| LQ118 | `"2026-10-15T23:30:00Z" \| Date: "HH:mm"` | converted to the host time zone | keeps the time zone |
| LQ119 | `"03/04/2026" \| Date: "yyyy-MM-dd"` | `2026-03-04` on en-US, `2026-04-03` in a day-month culture | `2026-04-03` |
| LQ120 | `Date: "%Y-%m-%d"` | not replaced: .NET formats | `2026-10-15` |
| LQ121 | `"not a date" \| Date: "yyyy"` | returned unchanged | returned unchanged |
| LQ122 | `"now" \| Date: "yyyy"` | host clock and time zone | server clock |
| LQ123 | `"abc" \| Abs` / `"abc" \| AtLeast: 3` | `0` / `abc` | `0` / `3` |
| LQ124 | `12345 \| Size` | `0` | `8` |
| LQ125 | `["x","x","y"] \| Uniq` | `x,y`: reported because `Uniq` is often used to wrap a single object | `x,y` |
| LQ126 | `{% for x in singleObject %}` | renders nothing | iterates the hash |
| LQ127 | `{{ content.address }}` (an object) | nothing | the object as text |
| LQ128 | `content.Root.Packages \| Sort: "Weight"` (also `Compact`, `Uniq`) on XML elements | flattened into all child values | sorted elements |
| LQ129 | `3000000000 \| Plus: 1` / `25000000 \| Times: 100` | `-1294967295` / `Liquid error`: 32-bit whole numbers | `3000000001` / `2500000000` |
| LQ130 | `{% if content.tags contains 'urgent' %}` with `["urgent"]` | false: `contains` never finds text in a list | true |
| LQ131 | `{% if content.amount > 1000 %}` with `"999.50"` | true: compared as text | error |
| LQ132 | `{% if content.missing == blank %}` / `{% if {} == empty %}` | false / false | true / true |
| LQ133 | `{{ content.active \| Default: false }}` with `true` | `True`, invalid in JSON output | `true` |

LQ121 also covers numbers: `{{ 1700000000 | Date: "yyyy" }}` gives `1700000000` (Shopify Liquid reads a Unix timestamp). LQ124 also covers objects: `{{ content.address | Size }}` gives `0`.

The working conversion from text to a whole number is `| Times: 1.0 | Round`: `{{ "2026" | Times: 1.0 | Round | Plus: 10 }}` gives `2036`.

Two rules avoid false alarms:
- **Intentional regular expressions:** a `Replace` search text with a backslash escape (`'\\'`, `'\.'`) is not flagged.
- **Trailing zeros (LQ105):** only reported where the value reaches a text filter; `{{ }}` output drops them.

## One problem, one finding

When two checks see the same problem, it is reported once, at the place in the map to fix:

| Instead of | You get |
|---|---|
| LQ040 `Liquid error: Parameter count mismatch.` in the output, plus LQ102 at `Minus` | One LQ102 **error** at the `Minus` call, with the output line |
| LQ040 `(Parameter 'count')` in the output, plus LQ101 at `Times` | One LQ101 error at the `Times` call |
| LQ040 `does not allow includes`, plus LQ022 at `include` | One LQ022 error |
| LQ040 `target of an invocation` in the output, plus LQ129 at `Times` | One LQ129 error at the `Times` call |
| LQ023 for `content.item.code`, plus LQ025 for `.@code` | One LQ025, noting the value was not found |
| LQ001 without location, plus LQ003 with location | One LQ003, with the message of Logic Apps |

Keep this in mind for `expectedDiagnostics` in a test suite: list the code that is reported, for example LQ102 rather than LQ040.

## Patterns found in real-world maps

I ran the tool on a large set of production maps from several Logic Apps integration platforms (Standard and Consumption). I read their comments for workarounds and verified each pattern on the real engine. The examples are generalised.

| Pattern | What actually happens in Logic Apps | Code | Use instead |
|---|---|---|---|
| `\| Plus: 0` or `\| Times: 1` to make text numeric | On text, `"123" \| Plus: 0` gives `1230`, and `Times: 1` leaves it text | LQ027, LQ100, LQ101 | `\| Times: 1.0` (`\| Round` for whole numbers) |
| `\| Times: 1.0` to make text numeric | Works, but the decimal `2026.0` keeps its zero in text filters | LQ105 | `\| Times: 1.0 \| Round` |
| `\| Times: -1` and `Plus` instead of `Minus` | `Minus` on text fails, `Plus` on text joins | LQ100, LQ102 | `\| Times: 1.0 \| Round \| Minus: 45` |
| `\| Abs \| Times: 100` to convert text | Drops the minus sign, and gives `0` for text that isn't a number | LQ027, LQ123 | `\| Times: 1.0 \| Times: 100` |
| `x \| Concat: x \| Uniq` to turn "one object or an array" into an array | `Concat` doesn't exist; `Uniq` does the wrapping and **removes duplicates** | LQ011, LQ125 | `\| Compact` |
| `{% for x in content.element %}` when the XML had one element | A single element becomes an object, and the loop renders nothing | LQ126 | `{% assign items = content.element \| Compact %}` |
| `JSONArrayFor` with an `if` inside | A comma is also written for skipped items, giving `undefined` items | LQ026, LQ043 | Select the items first, then loop |
| `content.item.@code`, `.$content`, `.#text` | The special character is dropped, so another property is read | LQ025 | `content.item["@code"]` |
| `\| Replace: '''', '&apos;'` to escape an apostrophe | `''''` is two empty strings: nothing is replaced | LQ028 | `\| Replace: "'", '&apos;'` |
| `\| Date: "yyyy-MM-ddTHH:mm:ssZ"` | A literal `Z` after a time in the host time zone: only right in UTC | LQ118 | `K` for the real offset |
| `\| type: 'A'` to select array items | Unknown filters are skipped: nothing is selected | LQ011 | A `for` loop with an `if` |
| `{{ content \| Json }}` to include the whole message | `Json` doesn't exist; an object prints **nothing** | LQ011, LQ127 | Print the fields, or pass the JSON as text in the action input |
| `{% if x != null %}` to skip empty input | JSON `null` is `""`, not nil: the condition is true for null | LQ021 | `{% if x != null and x != "" %}` |
| `\| Replace: '\\', '\\' \| Replace: '"', '\"'` to escape JSON | Correct and needed: DotLiquid has no JSON escape filter | – | Keep it |
| `\| Append: '     ' \| Truncate: 5, ''` to pad text | Works as in Shopify Liquid | – | Keep it |

Some differences are known in the DotLiquid project itself, for example [Replace uses regular expressions](https://github.com/dotliquid/dotliquid/issues/385) and the [missing json filter](https://github.com/dotliquid/dotliquid/issues/161).
