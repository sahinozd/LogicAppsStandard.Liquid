namespace LogicApps.Liquid.Constants;

/// <summary>
/// The diagnostic codes reported by the validator. Codes are stable, so they can be used in test expectations.
/// </summary>
public static class DiagnosticCodes
{
    /// <summary>DotLiquid could not parse the map. Logic Apps fails the action with "An error occurred while parsing liquid template".</summary>
    public const string TemplateSyntaxError = "LQ001";

    /// <summary>A tag that Logic Apps does not know, for example a wrongly cased <c>{% If %}</c>.</summary>
    public const string UnknownTag = "LQ002";

    /// <summary>A block tag that is never closed, or an end tag without a matching block.</summary>
    public const string UnbalancedBlock = "LQ003";

    /// <summary>A Liquid tag or output that is never closed (<c>{{</c> without <c>}}</c>).</summary>
    public const string UnterminatedTag = "LQ004";

    /// <summary>A filter written in snake_case or lowercase (<c>upcase</c>) instead of C# naming (<c>Upcase</c>). Logic Apps silently skips it.</summary>
    public const string FilterCasing = "LQ010";

    /// <summary>A filter that does not exist. Logic Apps silently skips it.</summary>
    public const string UnknownFilter = "LQ011";

    /// <summary>A variable that is used but never assigned. It renders as empty.</summary>
    public const string UndefinedVariable = "LQ020";

    /// <summary>A comparison with <c>nil</c>. JSON <c>null</c> values become an empty string in Logic Apps, so they are not <c>nil</c>.</summary>
    public const string NilComparison = "LQ021";

    /// <summary><c>include</c> or <c>extends</c>: Logic Apps does not configure a template file system.</summary>
    public const string IncludeNotSupported = "LQ022";

    /// <summary>A value the map uses was not found while rendering (for example a misspelled input field). It renders as empty.</summary>
    public const string MissingValue = "LQ023";

    /// <summary>The input file is not valid JSON.</summary>
    public const string InvalidJsonInput = "LQ030";

    /// <summary>The input file is not valid XML.</summary>
    public const string InvalidXmlInput = "LQ031";

    /// <summary>An error during rendering. Logic Apps does not fail: it writes "Liquid error: ..." into the output.</summary>
    public const string RenderError = "LQ040";

    /// <summary>The rendered output is not valid JSON while the transform kind expects JSON. Logic Apps fails the action.</summary>
    public const string InvalidJsonOutput = "LQ041";

    /// <summary>The rendered output is empty.</summary>
    public const string EmptyOutput = "LQ042";

    /// <summary>A condition on an input value without comparison: JSON <c>null</c> becomes an empty string in Logic Apps, which is truthy.</summary>
    public const string NullIsTruthy = "LQ024";

    /// <summary>A dot path with <c>@</c>, <c>$</c>, <c>#</c> or <c>:</c> (<c>content.x.@code</c>): DotLiquid drops the character and reads another property.</summary>
    public const string SpecialCharacterInPath = "LQ025";

    /// <summary>An <c>if</c>, <c>unless</c> or <c>case</c> directly inside <c>JSONArrayFor</c>: skipped items still get a comma and become <c>undefined</c>.</summary>
    public const string ConditionInJsonArrayFor = "LQ026";

    /// <summary><c>Plus: 0</c>, <c>Minus: 0</c>, <c>Times: 1</c> or <c>DividedBy: 1</c> used to convert text to a number: on text this joins or repeats instead.</summary>
    public const string NumberConversionIdiom = "LQ027";

    /// <summary>An empty search text in <c>Replace</c> or <c>Remove</c>, often a doubled quote (<c>''''</c>) meant as an escaped quote: it does nothing.</summary>
    public const string EmptySearchText = "LQ028";

    /// <summary>The JSON output contains <c>undefined</c> array items, typically from stray commas.</summary>
    public const string UndefinedInOutput = "LQ043";

    // LQ1xx: DotLiquid behaves differently from Shopify Liquid. Most are detected while rendering, with the actual values.

    /// <summary><c>Plus</c> on text joins the text instead of adding numbers (<c>"2026" | Plus: 10</c> gives <c>202610</c>).</summary>
    public const string PlusJoinsText = "LQ100";

    /// <summary><c>Times</c> on text with a whole number repeats the text (<c>"4" | Times: 3</c> gives <c>444</c>).</summary>
    public const string TimesRepeatsText = "LQ101";

    /// <summary><c>Minus</c>, <c>DividedBy</c>, <c>Modulo</c> or <c>Times</c> on text without a decimal operand: DotLiquid does not convert text to a number and fails.</summary>
    public const string MathOnText = "LQ102";

    /// <summary>Text is converted to a number with the host culture, so separators can be misread (<c>"125,50"</c> becomes <c>12550</c> on en-US).</summary>
    public const string CultureNumberParsing = "LQ103";

    /// <summary>A decimal result with trailing zeros (<c>2026.0</c>) renders as <c>2026</c> in output, but keeps the zeros when passed to a text filter such as <c>Append</c>.</summary>
    public const string DecimalTrailingZeros = "LQ105";

    /// <summary><c>Round</c> uses banker's rounding (<c>2.5</c> becomes <c>2</c>); Shopify Liquid rounds half away from zero (<c>3</c>).</summary>
    public const string BankersRounding = "LQ106";

    /// <summary><c>Round</c> on a value that is not a number returns nothing.</summary>
    public const string RoundNotANumber = "LQ107";

    /// <summary>Integer <c>DividedBy</c> with a negative result truncates (<c>-7 / 2 = -3</c>); Shopify Liquid floors (<c>-4</c>).</summary>
    public const string NegativeIntegerDivision = "LQ108";

    /// <summary><c>Modulo</c> with a negative number keeps the sign of the input (<c>-7 % 3 = -1</c>); Shopify Liquid uses the sign of the divisor (<c>2</c>).</summary>
    public const string NegativeModulo = "LQ109";

    /// <summary>Decimal division by zero renders <c>∞</c>; Shopify Liquid raises an error.</summary>
    public const string DivisionByZero = "LQ110";

    /// <summary><c>Default</c> treats whitespace as empty and does not treat <c>false</c> as empty, unlike Shopify Liquid.</summary>
    public const string DefaultDiffers = "LQ111";

    /// <summary><c>Replace</c>, <c>ReplaceFirst</c> and <c>RemoveFirst</c> treat the search text as a regular expression; Shopify Liquid replaces literal text.</summary>
    public const string ReplaceIsRegex = "LQ112";

    /// <summary><c>Split</c> drops empty items and does not split on an empty separator, unlike Shopify Liquid.</summary>
    public const string SplitDiffers = "LQ113";

    /// <summary><c>Append</c> or <c>Prepend</c> on a missing value returns nothing; Shopify Liquid returns the appended text.</summary>
    public const string AppendToNil = "LQ114";

    /// <summary><c>Capitalize</c> capitalizes every word and keeps the rest; Shopify Liquid capitalizes the first letter and lowercases the rest.</summary>
    public const string CapitalizeDiffers = "LQ115";

    /// <summary><c>Escape</c> also encodes non-ASCII characters (<c>é</c> becomes <c>&amp;#233;</c>), unlike Shopify Liquid.</summary>
    public const string EscapeDiffers = "LQ116";

    /// <summary><c>Sort</c> by property does not sort JSON or XML input at all, and text is sorted culture-aware instead of by character code.</summary>
    public const string SortDiffers = "LQ117";

    /// <summary><c>Date</c> converts a date with a time zone (<c>Z</c>, <c>+02:00</c>) to the time zone of the Logic Apps host.</summary>
    public const string DateTimeZoneConversion = "LQ118";

    /// <summary><c>Date</c> parses a non-ISO date with the host culture (<c>03/04/2026</c> is March 4 on en-US, April 3 on nl-NL).</summary>
    public const string DateCultureParsing = "LQ119";

    /// <summary><c>Date</c> uses .NET format strings (<c>yyyy-MM-dd</c>), not Ruby strftime (<c>%Y-%m-%d</c>).</summary>
    public const string DateStrftimeFormat = "LQ120";

    /// <summary><c>Date</c> could not parse the value and returns it unchanged.</summary>
    public const string DateNotParsed = "LQ121";

    /// <summary><c>Date</c> on <c>now</c> or <c>today</c> uses the clock and time zone of the Logic Apps host.</summary>
    public const string DateNow = "LQ122";

    /// <summary><c>Abs</c>, <c>AtLeast</c> or <c>AtMost</c> on a value that is not a number: <c>Abs</c> returns 0, the others return the value unchanged.</summary>
    public const string NumberFilterOnText = "LQ123";

    /// <summary><c>Size</c> of a number returns 0.</summary>
    public const string SizeOfNumber = "LQ124";

    /// <summary><c>Uniq</c> removed duplicate values. Often used to wrap a single object in an array, where <c>Compact</c> is safer.</summary>
    public const string UniqRemovedDuplicates = "LQ125";

    /// <summary>A <c>for</c> or <c>JSONArrayFor</c> loop over a single object renders nothing; XML converted to JSON gives an object for one element.</summary>
    public const string LoopOverSingleObject = "LQ126";

    /// <summary><c>{{ }}</c> outputs an object (prints nothing) or an array of objects (prints internal type names) instead of a value.</summary>
    public const string ObjectInOutput = "LQ127";

    /// <summary><c>Sort</c>, <c>Compact</c> or <c>Uniq</c> on XML elements flattens them into all of their child values: the elements are lost.</summary>
    public const string XmlElementsFlattened = "LQ128";
}
