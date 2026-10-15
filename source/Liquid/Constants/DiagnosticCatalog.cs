using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Constants;

/// <summary>
/// The documentation of every diagnostic code: what it means, an example and how to fix it.
/// </summary>
public static class DiagnosticCatalog
{
    private const DiagnosticSeverity Error = DiagnosticSeverity.Error;
    private const DiagnosticSeverity Warning = DiagnosticSeverity.Warning;

    private static readonly DiagnosticDescriptor[] _descriptors =
    [
        // Syntax and tags
        Describe(DiagnosticCodes.TemplateSyntaxError, DiagnosticCategory.Syntax, Error, "The map cannot be parsed",
            "DotLiquid cannot parse the map, so Logic Apps fails the action with 'An error occurred while parsing liquid template'. " +
            "DotLiquid stops at the first syntax error, so fix them one by one.",
            "{% for line in content.lines %} ... (no endfor)",
            "Fix the reported syntax error; the static checks (LQ002-LQ004) usually point at the exact location."),
        Describe(DiagnosticCodes.UnknownTag, DiagnosticCategory.Syntax, Error, "Unknown tag",
            "Logic Apps only knows the tags of DotLiquid 2.0.361 plus JSONArrayFor, and tags are case-sensitive. " +
            "An unknown tag makes parsing fail. Shopify-only tags such as 'render', 'liquid' and 'echo' do not exist.",
            "{% If content.total > 0 %}",
            "Use the lowercase tag name ('if'), or rewrite Shopify-only tags with assign, capture, if and for."),
        Describe(DiagnosticCodes.UnbalancedBlock, DiagnosticCategory.Syntax, Error, "Block never closed, or end tag without block",
            "Every block tag (if, unless, case, for, JSONArrayFor, capture, comment, raw) needs its own end tag, in the right order. Otherwise parsing fails.",
            "{% if a %}{% for x in list %}{% endif %}{% endfor %}",
            "Close the innermost block first: '{% endfor %}{% endif %}'."),
        Describe(DiagnosticCodes.UnterminatedTag, DiagnosticCategory.Syntax, Error, "Tag or output never closed, or spans lines",
            "'{{' needs '}}' and '{%' needs '%}' on the same line. DotLiquid does not allow line breaks inside a tag or output (Shopify Liquid does).",
            "{{ content.name\n   | Upcase }}",
            "Put the whole tag or output on one line."),

        // Filters
        Describe(DiagnosticCodes.FilterCasing, DiagnosticCategory.Filters, Error, "Filter in Shopify style is skipped",
            "Logic Apps uses C# names for filters (CSharpNamingConvention). A filter in Ruby/Shopify style is unknown, and an unknown filter is silently skipped: the value is output unchanged.",
            "{{ content.name | upcase }}  → name is not upper case",
            "{{ content.name | Upcase }}"),
        Describe(DiagnosticCodes.UnknownFilter, DiagnosticCategory.Filters, Error, "Unknown filter is skipped",
            "A filter that does not exist is silently skipped. Shopify-only filters (where, json, concat, ...) do not exist in DotLiquid. " +
            "A property name used as filter ('| type: \"A\"') does not select anything.",
            "{{ content | Json }}  → prints nothing",
            "Print the fields one by one; select items with a for loop and an if."),
        Describe(DiagnosticCodes.NumberConversionIdiom, DiagnosticCategory.Filters, Warning, "Conversion of text to a number does not work",
            "'| Plus: 0' and '| Times: 1' only work when the value is already a number. On text, Plus joins ('123' gives '1230') and Times repeats. '| Abs' drops the minus sign.",
            "{{ content.amount | Plus: 0 }}  → '1230' when amount is the text '123'",
            "'| Times: 1.0' for a decimal, '| Times: 1.0 | Round' for a whole number."),
        Describe(DiagnosticCodes.EmptySearchText, DiagnosticCategory.Filters, Warning, "Replace or Remove with an empty search text does nothing",
            "Liquid has no escape characters: '''' is two empty strings, not an apostrophe. An empty search text does nothing in DotLiquid.",
            "{{ content.name | Replace: '''', '&apos;' }}",
            "{{ content.name | Replace: \"'\", '&apos;' }}"),

        // Values and paths
        Describe(DiagnosticCodes.UndefinedVariable, DiagnosticCategory.ValuesAndPaths, Warning, "Variable is never assigned",
            "A variable that is never assigned renders as empty, without an error. In Logic Apps the input is available under 'content'.",
            "{{ order.number }}",
            "{{ content.order.number }}"),
        Describe(DiagnosticCodes.NilComparison, DiagnosticCategory.ValuesAndPaths, Warning, "Comparison with nil misses JSON null values",
            "In Logic Apps a JSON null value becomes an empty string, so it is not nil. Only a missing property is nil.",
            "{% if content.middleName != nil %}  → true for null",
            "{% if content.middleName != nil and content.middleName != \"\" %}"),
        Describe(DiagnosticCodes.IncludeNotSupported, DiagnosticCategory.ValuesAndPaths, Error, "include and extends are not supported",
            "Logic Apps does not configure a template file system, so an include renders as a 'Liquid error' text instead of the content.",
            "{% include 'header' %}",
            "Put the content in the map itself."),
        Describe(DiagnosticCodes.MissingValue, DiagnosticCategory.ValuesAndPaths, Warning, "Value not found in the input",
            "A path that does not exist in the input renders as empty, without an error in Logic Apps. Names are case-sensitive.",
            "{{ content.order.Number }}  → empty when the input has 'number'",
            "Check the spelling against the input. Use '| Default' for optional values."),
        Describe(DiagnosticCodes.NullIsTruthy, DiagnosticCategory.ValuesAndPaths, Warning, "Check without comparison is true for JSON null",
            "In Logic Apps a JSON null value becomes an empty string, which is truthy in Liquid.",
            "{% if content.discount %}  → true for null",
            "{% if content.discount != \"\" %}"),
        Describe(DiagnosticCodes.SpecialCharacterInPath, DiagnosticCategory.ValuesAndPaths, Warning, "Special character in a dot path",
            "XML converted to JSON gives names such as '@code', '#text' and 'ns0:Item'. In a dot path DotLiquid drops the special character, so another property (or nothing) is read, without any error.",
            "{{ content.item.@code }}  → reads 'code'",
            "{{ content.item[\"@code\"] }}"),
        Describe(DiagnosticCodes.ConditionInJsonArrayFor, DiagnosticCategory.ValuesAndPaths, Warning, "Condition inside JSONArrayFor",
            "JSONArrayFor writes a comma after every item, also when an if inside it skips the item. Skipped items become empty array items, which the JSON output turns into 'undefined'.",
            "{% JSONArrayFor x in list %}{% if x.ok %}{...}{% endif %}{% endJSONArrayFor %}",
            "Select the items first (a for loop with an if that builds the list), or use a for loop with your own commas."),

        // Input
        Describe(DiagnosticCodes.InvalidJsonInput, DiagnosticCategory.Input, Error, "The input is not valid JSON",
            "Logic Apps fails the action when the input of a JsonTo* kind is not valid JSON.",
            null,
            "Fix the input, or check the transform kind."),
        Describe(DiagnosticCodes.InvalidXmlInput, DiagnosticCategory.Input, Error, "The input is not valid XML",
            "Logic Apps fails the action when the input of an XmlTo* kind is not valid XML. The XML is loaded from the UTF-8 bytes of the message, so a declaration with encoding=\"utf-16\" is also an error.",
            null,
            "Fix the input, or check the transform kind."),

        // Output
        Describe(DiagnosticCodes.RenderError, DiagnosticCategory.Output, Error, "Render error written into the output",
            "Logic Apps does not fail the action on a render error: DotLiquid writes 'Liquid error: ...' into the output. " +
            "When the cause is a filter call in the map, the finding is reported there instead (for example LQ102).",
            "{{ 10 | DividedBy: 0 }}  → 'Liquid error: Exception has been thrown by the target of an invocation.'",
            "Fix the cause shown in the message."),
        Describe(DiagnosticCodes.InvalidJsonOutput, DiagnosticCategory.Output, Error, "The output is not valid JSON",
            "For a *ToJson kind Logic Apps parses the rendered text as JSON and fails the action when it is not valid. " +
            "A common cause is a text value with a double quote or backslash: DotLiquid has no JSON escape filter.",
            "\"name\": \"{{ content.name }}\"  → invalid when the name contains a double quote",
            "Escape text values with '| Replace: '\\\\', '\\\\' | Replace: '\"', '\\\"''."),
        Describe(DiagnosticCodes.EmptyOutput, DiagnosticCategory.Output, Warning, "The output is empty",
            "The map rendered nothing. For a *ToJson kind Logic Apps does not fail: the action output is null.",
            null,
            "Check the input and the conditions in the map."),
        Describe(DiagnosticCodes.UndefinedInOutput, DiagnosticCategory.Output, Warning, "The output contains an empty array item",
            "An empty array item ('[1,,2]') is read as 'undefined', which is not valid JSON for the next system. Typically a stray comma from an if inside JSONArrayFor.",
            "[{% for x in list %}{% if x.ok %}{{ x.id }}{% endif %},{% endfor %}]",
            "Remove the stray comma, or select the items before the loop."),

        // Differences from Shopify Liquid
        Describe(DiagnosticCodes.PlusJoinsText, DiagnosticCategory.ShopifyDeviation, Warning, "Plus on text joins instead of adding",
            "DotLiquid adds text and a number as text. Filters such as Date, Append and Split return text.",
            "{{ \"2026\" | Plus: 10 }}  → 202610 (Shopify Liquid: 2036)",
            "{{ \"2026\" | Times: 1.0 | Round | Plus: 10 }}"),
        Describe(DiagnosticCodes.TimesRepeatsText, DiagnosticCategory.ShopifyDeviation, Warning, "Times on text repeats the text",
            "DotLiquid repeats text when the operand is a whole number, and fails for a negative number.",
            "{{ \"2026\" | Times: 3 }}  → 202620262026 (Shopify Liquid: 6078)",
            "Use a decimal operand: '| Times: 3.0'."),
        Describe(DiagnosticCodes.MathOnText, DiagnosticCategory.ShopifyDeviation, Warning, "Minus, DividedBy or Modulo on text fails",
            "DotLiquid does not convert text to a number for whole-number math and writes 'Liquid error: Parameter count mismatch.' into the output.",
            "{{ \"2026\" | Minus: 1 }}  → Liquid error (Shopify Liquid: 2025)",
            "{{ \"2026\" | Times: 1.0 | Round | Minus: 1 }}"),
        Describe(DiagnosticCodes.CultureNumberParsing, DiagnosticCategory.ShopifyDeviation, Warning, "Text is converted to a number with the host culture",
            "Decimal math converts text with the culture of the Logic Apps host (en-US in Azure). Shopify Liquid always reads '.' as the decimal separator.",
            "{{ \"125,50\" | Times: 1.0 }}  → 12550 on en-US",
            "Make sure the source uses '.' and no thousands separator."),
        Describe(DiagnosticCodes.DecimalTrailingZeros, DiagnosticCategory.ShopifyDeviation, Warning, "A decimal keeps its trailing zeros in a text filter",
            "{{ }} output drops trailing zeros (2026.0 prints as 2026), but text filters such as Append receive the decimal with them.",
            "{{ \"2026\" | Times: 1.0 | Append: \"-12-31\" }}  → 2026.0-12-31",
            "Use '| Round' (or '| Round: n') before passing it on."),
        Describe(DiagnosticCodes.BankersRounding, DiagnosticCategory.ShopifyDeviation, Warning, "Round rounds halves to the even number",
            "DotLiquid uses banker's rounding; Shopify Liquid rounds half away from zero.",
            "{{ 2.5 | Round }}  → 2 (Shopify Liquid: 3)",
            "Add a tiny amount before rounding when halves must round up: '| Plus: 0.0000001 | Round: 2'."),
        Describe(DiagnosticCodes.RoundNotANumber, DiagnosticCategory.ShopifyDeviation, Warning, "Round on text that is not a number returns nothing",
            "Round returns nothing when the value is not a number; Shopify Liquid returns 0.",
            "{{ \"abc\" | Round }}  → empty",
            "Check the input, or use '| Default' before Round."),
        Describe(DiagnosticCodes.NegativeIntegerDivision, DiagnosticCategory.ShopifyDeviation, Warning, "Negative integer division rounds toward zero",
            "DotLiquid truncates, Shopify Liquid floors.",
            "{{ -7 | DividedBy: 2 }}  → -3 (Shopify Liquid: -4)",
            null),
        Describe(DiagnosticCodes.NegativeModulo, DiagnosticCategory.ShopifyDeviation, Warning, "Modulo of a negative number takes the sign of the input",
            "DotLiquid uses the C# remainder, Shopify Liquid the mathematical modulo.",
            "{{ -7 | Modulo: 3 }}  → -1 (Shopify Liquid: 2)",
            null),
        Describe(DiagnosticCodes.DivisionByZero, DiagnosticCategory.ShopifyDeviation, Warning, "Decimal division by zero renders a value",
            "A decimal division by zero gives infinity or NaN; Shopify Liquid raises an error.",
            "{{ 1.5 | DividedBy: 0 }}  → ∞",
            "Check for zero first."),
        Describe(DiagnosticCodes.DefaultDiffers, DiagnosticCategory.ShopifyDeviation, Warning, "Default treats whitespace and false differently",
            "Default treats a whitespace value as empty, and does not treat false as empty.",
            "{{ false | Default: \"x\" }}  → False (Shopify Liquid: x)",
            "Use an if instead."),
        Describe(DiagnosticCodes.ReplaceIsRegex, DiagnosticCategory.ShopifyDeviation, Warning, "Replace uses the search text as a regular expression",
            "Replace, ReplaceFirst and RemoveFirst use the search text as a regular expression, so special characters such as '.' match more than themselves. Shopify Liquid replaces literal text.",
            "{{ \"1.234.5\" | Replace: \".\", \",\" }}  → ,,,,,,, (Shopify Liquid: 1,234,5)",
            "Escape special characters: '\\.' for a dot."),
        Describe(DiagnosticCodes.SplitDiffers, DiagnosticCategory.ShopifyDeviation, Warning, "Split gives a different number of items",
            "Split drops empty items, and an empty separator does not split the text into characters.",
            "{{ \"a,,b\" | Split: \",\" | Size }}  → 2 (Shopify Liquid: 3)",
            null),
        Describe(DiagnosticCodes.AppendToNil, DiagnosticCategory.ShopifyDeviation, Warning, "Append or Prepend on a missing value returns nothing",
            "On a missing value Append and Prepend return nothing; Shopify Liquid returns the appended text.",
            "{{ nothing | Append: \"x\" }}  → empty (Shopify Liquid: x)",
            "Use '| Default: \"\"' first."),
        Describe(DiagnosticCodes.CapitalizeDiffers, DiagnosticCategory.ShopifyDeviation, Warning, "Capitalize capitalizes every word",
            "DotLiquid gives every word a capital and keeps the rest; Shopify Liquid capitalizes only the first character and lowercases the rest.",
            "{{ \"hELLO wORLD\" | Capitalize }}  → HELLO WORLD (Shopify Liquid: Hello world)",
            null),
        Describe(DiagnosticCodes.EscapeDiffers, DiagnosticCategory.ShopifyDeviation, Warning, "Escape also encodes non-ASCII characters",
            "Escape and H encode non-ASCII characters as well.",
            "{{ \"café\" | Escape }}  → caf&#233;",
            null),
        Describe(DiagnosticCodes.SortDiffers, DiagnosticCategory.ShopifyDeviation, Warning, "Sort does not sort JSON input, or sorts by culture",
            "Sort by property leaves JSON items in their original order; Sort on text orders by the host culture instead of by character code.",
            "{{ content.items | Sort: \"n\" }}  → original order",
            "Build the order yourself, for example with a for loop per value."),
        Describe(DiagnosticCodes.DateTimeZoneConversion, DiagnosticCategory.ShopifyDeviation, Warning, "Date converts to the time zone of the host",
            "A date with a time zone is converted to the time zone of the Logic Apps host: UTC in Azure, unless WEBSITE_TIME_ZONE is set. A literal 'Z' in the format is then only correct in UTC.",
            "{{ \"2026-10-15T23:30:00+02:00\" | Date: \"HH:mm\" }}  → 21:30 in UTC",
            "Use 'K' in the format to write the actual offset, or convert the time in the workflow."),
        Describe(DiagnosticCodes.DateCultureParsing, DiagnosticCategory.ShopifyDeviation, Warning, "Date parses the value with the host culture",
            "A date that is not ISO is parsed with the host culture, so day and month can swap.",
            "{{ \"03/04/2026\" | Date: \"yyyy-MM-dd\" }}  → 2026-03-04 on en-US, 2026-04-03 on nl-NL",
            "Use ISO dates (yyyy-MM-dd) in the input."),
        Describe(DiagnosticCodes.DateStrftimeFormat, DiagnosticCategory.ShopifyDeviation, Warning, "Date uses a Ruby strftime format",
            "DotLiquid uses .NET date formats, so '%Y' and similar are not replaced.",
            "{{ \"2026-10-15\" | Date: \"%Y-%m-%d\" }}",
            "{{ \"2026-10-15\" | Date: \"yyyy-MM-dd\" }}"),
        Describe(DiagnosticCodes.DateNotParsed, DiagnosticCategory.ShopifyDeviation, Warning, "Date cannot parse the value",
            "A value the host culture cannot parse as a date is returned unchanged.",
            "{{ \"not a date\" | Date: \"yyyy\" }}  → not a date",
            "Use ISO dates (yyyy-MM-dd) in the input."),
        Describe(DiagnosticCodes.DateNow, DiagnosticCategory.ShopifyDeviation, Warning, "Date uses the clock of the host",
            "'now' and 'today' use the clock and time zone of the Logic Apps host: UTC in Azure, unless WEBSITE_TIME_ZONE is set.",
            "{{ \"now\" | Date: \"yyyy-MM-dd\" }}",
            "Pass the time from the workflow (utcNow()) in the input when the time zone matters."),
        Describe(DiagnosticCodes.NumberFilterOnText, DiagnosticCategory.ShopifyDeviation, Warning, "Number filter on a value that is not a number",
            "Abs returns 0, AtLeast and AtMost return the value unchanged; Shopify Liquid converts the value to a number first.",
            "{{ \"abc\" | AtLeast: 3 }}  → abc (Shopify Liquid: 3)",
            "Check the input, or use '| Default' first."),
        Describe(DiagnosticCodes.SizeOfNumber, DiagnosticCategory.ShopifyDeviation, Warning, "Size of a number returns 0",
            "Size returns 0 for a number; Shopify Liquid returns its byte size.",
            "{{ 12345 | Size }}  → 0",
            "Use Size on text or lists only."),
        Describe(DiagnosticCodes.UniqRemovedDuplicates, DiagnosticCategory.ShopifyDeviation, Warning, "Uniq removed duplicate values",
            "Reported because Uniq is often used to turn 'one object or an array' into an array, where removing duplicates is not intended.",
            "{{ x | Concat: x | Uniq }}",
            "Use '| Compact' to wrap a single object without removing duplicates."),
        Describe(DiagnosticCodes.LoopOverSingleObject, DiagnosticCategory.ShopifyDeviation, Warning, "Loop over a single object renders nothing",
            "When XML is converted to JSON (json(xml(...))), a repeating element that occurs once becomes an object instead of an array. A loop over an object renders nothing.",
            "{% for line in content.order.line %}  → nothing when there is one line",
            "{% assign lines = content.order.line | Compact %}{% for line in lines %}"),
        Describe(DiagnosticCodes.ObjectInOutput, DiagnosticCategory.ShopifyDeviation, Warning, "An object is printed",
            "DotLiquid cannot serialize objects: an object prints nothing, and a list of objects prints internal type names.",
            "{{ content.address }}  → nothing",
            "Print the fields one by one, or pass the JSON as text in the input of the Liquid action."),
        Describe(DiagnosticCodes.XmlElementsFlattened, DiagnosticCategory.ShopifyDeviation, Warning, "Sort, Compact or Uniq flattens XML elements",
            "XML elements are lists of their child elements, so these filters turn a list of elements into a list of all their child values.",
            "{{ content.Root.Packages | Sort: \"Weight\" }}  → 12.5, 2, 3.75, 1",
            "Loop over the elements directly; to order them, loop once per value."),
    ];

    private static readonly Dictionary<string, DiagnosticDescriptor> _byCode = _descriptors.ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>All documented codes, in code order.</summary>
    public static IReadOnlyList<DiagnosticDescriptor> All { get; } = [.. _descriptors.OrderBy(d => d.Code, StringComparer.Ordinal)];

    /// <summary>
    /// Finds the documentation of a code.
    /// </summary>
    /// <param name="code">The code, for example <c>LQ102</c> (case-insensitive).</param>
    /// <returns>The documentation, or null for an unknown code.</returns>
    public static DiagnosticDescriptor? Find(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _byCode.GetValueOrDefault(code.Trim());
    }

    /// <summary>
    /// The category of a code; codes LQ100 and up are differences from Shopify Liquid.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The category.</returns>
    public static DiagnosticCategory CategoryOf(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return Find(code)?.Category ?? DiagnosticCategory.ShopifyDeviation;
    }

    private static DiagnosticDescriptor Describe(string code, DiagnosticCategory category, DiagnosticSeverity severity, string title, string description, string? example, string? fix)
    {
        return new DiagnosticDescriptor
        {
            Code = code,
            Category = category,
            Severity = severity,
            Title = title,
            Description = description,
            Example = example,
            Fix = fix,
        };
    }
}
