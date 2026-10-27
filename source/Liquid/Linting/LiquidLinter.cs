using System.Text.RegularExpressions;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Linting;

/// <summary>
/// Static checks on a Liquid map, with line and column information. These checks find problems that
/// DotLiquid either reports without a location (unknown tags, unclosed blocks) or does not report at all
/// (wrongly cased or unknown filters are silently skipped by Logic Apps).
/// </summary>
public sealed class LiquidLinter : ILiquidLinter
{
    private static readonly Regex StringPattern = new(@"""[^""]*""|'[^']*'");
    private static readonly Regex FilterPattern = new(@"\|\s*(\w+)");
    // An identifier that is not part of a property path (no leading '.'), not a named argument (no trailing ':').
    private static readonly Regex IdentifierPattern = new(@"(?<![\w.\]\[@$#-])([A-Za-z_][\w-]*)(?![\w-]*\s*:)");
    private static readonly Regex AssignPattern = new(@"^(?<name>[\w-]+)\s*=\s*(?<value>.*)$", RegexOptions.Singleline);
    private static readonly Regex FirstWordPattern = new(@"^(\w+)");
    private static readonly Regex LoopPattern = new(@"^(?<item>\w+)\s+in\s+(?<collection>\S+)");
    private static readonly Regex NilComparisonPattern = new(@"(==|!=|<>)\s*(nil|null)\b|\b(nil|null)\s*(==|!=|<>)");
    private static readonly Regex LogicalOperatorPattern = new(@"\s+(?:and|or)\s+");
    // A bare path into the input, such as content.customer.email or content["a"].b, without any operator.
    private static readonly Regex InputPathPattern = new(@"^content(\.[\w-]+|\[[^\]]+\])+$");
    private static readonly Regex LiteralArgumentPattern = new(@"\|\s*(?<filter>Replace|ReplaceFirst|Remove|RemoveFirst|Date)\s*:\s*(?:""(?<double>[^""]*)""|'(?<single>[^']*)')");
    private static readonly Regex AbsConversionPattern = new(@"\|\s*Abs\s*\|\s*(?:Times|Plus|Minus|DividedBy|Modulo)\b");
    private static readonly Regex ConversionIdiomPattern =new(@"\|\s*(?<idiom>(?:Plus|Minus)\s*:\s*0|(?:Times|DividedBy)\s*:\s*1)(?![\w.])");
    private static readonly Regex UniqAfterConcatPattern = new(@"\|\s*Concat\b[^|]*\|\s*Uniq\b", RegexOptions.IgnoreCase);
    private static readonly Regex SpecialPathSegmentPattern = new(@"(?<=[\w\]])\.(?<segment>[@$#][\w:-]*|[\w-]+:[\w:-]+)");
    // An unquoted Z in a .NET date format is written literally.
    private static readonly Regex LiteralUtcMarkerPattern = new("(?<!')Z(?!')");
    private static readonly Regex ForLoopPropertyPattern = new(@"\bforloop\.(?<property>\w+)");
    // A comparison or logical operator in the value of an assign, before the first filter.
    private static readonly Regex AssignOperatorPattern = new(@"==|!=|<>|<=|>=|\s[<>]\s|\s(?:contains|and|or)\s");

    private static readonly HashSet<string> ForLoopProperties = new(StringComparer.Ordinal) { "name", "length", "index", "index0", "rindex", "rindex0", "first", "last" };
    private static readonly HashSet<string> LoopTags = new(StringComparer.Ordinal) { "for", "tablerow", RuntimeConstants.JsonArrayForTagName };

    private const string CommentHint = " DotLiquid also reads the tags inside a comment block.";

    /// <summary>
    /// Runs all static checks on a template.
    /// </summary>
    /// <param name="map">The Liquid map.</param>
    /// <returns>The findings, ordered by line and column.</returns>
    public IReadOnlyList<Diagnostic> Lint(string map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var diagnostics = new List<Diagnostic>();
        var position = new TextPosition(map);
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var usages = new List<(string Name, LiquidToken Token)>();
        var openBlocks = new Stack<OpenBlock>();

        foreach (var token in LiquidTokenizer.Tokenize(map))
        {
            if (token.Kind == LiquidTokenKind.Text)
            {
                CheckUnterminated(token, position, diagnostics);
                continue;
            }

            // The content of raw and literal is not parsed.
            var insideRaw = openBlocks.Count > 0 && openBlocks.Peek().Name is "raw" or "literal";
            if (insideRaw && token.TagName != "end" + openBlocks.Peek().Name)
            {
                continue;
            }

            var insideComment = openBlocks.Any(b => b.Name == "comment");

            if (token.Kind == LiquidTokenKind.Output)
            {
                CheckBraceInOutput(token, position, diagnostics);
                if (!insideComment)
                {
                    CheckFilters(token, token.Markup, position, diagnostics);
                    CheckPaths(token, token.Markup, position, diagnostics);
                    CheckForLoop(token, token.Markup, openBlocks, position, diagnostics);
                    usages.AddRange(VariableRoots(token.Markup).Select(n => (n, token)));
                }

                continue;
            }

            var name = token.TagName;
            if (name.Length == 0)
            {
                if (!token.Markup.StartsWith('#'))
                {
                    diagnostics.Add(At(position, token, DiagnosticCodes.TemplateSyntaxError, DiagnosticSeverity.Error, $"Tag '{token.Raw}' has no name",
                        "Logic Apps fails to parse the map.", "Write the tag name directly after '{%', for example '{% if ... %}'."));
                }

                continue;
            }

            if (name.StartsWith("end", StringComparison.Ordinal) && !LiquidLanguage.IsTag(name))
            {
                CloseBlock(token, name[3..], openBlocks, position, diagnostics);
                continue;
            }

            if (!insideComment)
            {
                CheckForLoop(token, token.TagArguments, openBlocks, position, diagnostics);
            }

            if (LiquidLanguage.IntermediateTags.Contains(name))
            {
                if (openBlocks.Count == 0)
                {
                    diagnostics.Add(At(position, token, DiagnosticCodes.UnbalancedBlock, DiagnosticSeverity.Error, $"'{name}' is used outside of a block",
                        "Logic Apps fails to parse the map.", $"Put '{name}' inside an if, unless or case block."));
                }
                else if (insideComment)
                {
                    // Not evaluated.
                }
                else if (name == "else" && token.TagArguments.Length > 0)
                {
                    diagnostics.Add(At(position, token, DiagnosticCodes.ElseWithCondition, DiagnosticSeverity.Error, $"'else {token.TagArguments}': the condition is ignored",
                        "DotLiquid ignores everything after 'else', so this is a plain else: it runs whenever the conditions before it are false.",
                        "Use 'elsif', for example '{% elsif ... %}'.", "else"));
                }
                else
                {
                    CheckCondition(token, position, diagnostics);
                    CheckPaths(token, token.TagArguments, position, diagnostics);
                    usages.AddRange(VariableRoots(token.TagArguments).Select(n => (n, token)));
                }

                continue;
            }

            if (!LiquidLanguage.IsTag(name))
            {
                var suggestion = LiquidLanguage.SuggestTag(name);
                var unknownTag = (suggestion, LiquidLanguage.ShopifyOnlyTags.Contains(name)) switch
                {
                    (_, true) => At(position, token, DiagnosticCodes.UnknownTag, DiagnosticSeverity.Error, $"Tag '{name}' does not exist in Logic Apps",
                        $"'{name}' exists in Shopify Liquid, but not in DotLiquid {RuntimeConstants.DotLiquidVersion} that Logic Apps uses, so Logic Apps fails to parse the map.",
                        "Rewrite it with the tags DotLiquid has (assign, capture, if, for, case, ...).", name),
                    (null, _) => At(position, token, DiagnosticCodes.UnknownTag, DiagnosticSeverity.Error, $"Unknown tag '{name}'",
                        "Logic Apps fails to parse the map.", null, name),
                    _ => At(position, token, DiagnosticCodes.UnknownTag, DiagnosticSeverity.Error, $"Unknown tag '{name}'",
                        name.Equals(suggestion, StringComparison.OrdinalIgnoreCase) ? "Tags are case-sensitive in Logic Apps, so the map fails to parse." : "Logic Apps fails to parse the map.",
                        $"Use '{suggestion}'.", name),
                };
                diagnostics.Add(insideComment ? unknownTag with { Explanation = unknownTag.Explanation + CommentHint } : unknownTag);
                continue;
            }

            // extends is a block without an end tag: it takes the rest of the template.
            if (LiquidLanguage.IsBlockTag(name))
            {
                if (!insideComment && name is "if" or "unless" or "case" && openBlocks.Count > 0 && openBlocks.Peek().Name == RuntimeConstants.JsonArrayForTagName)
                {
                    diagnostics.Add(At(position, token, DiagnosticCodes.ConditionInJsonArrayFor, DiagnosticSeverity.Warning, $"'{name}' inside JSONArrayFor",
                        $"JSONArrayFor writes a comma after every item, also when the {name} skips it. Skipped items become empty array items, which the JSON output turns into 'undefined'.",
                        "Select the items first (a for loop with an if that builds the list), or use a for loop with your own commas.", name));
                }

                openBlocks.Push(new OpenBlock(name, token));
            }

            if (!insideComment)
            {
                AnalyzeTag(token, name, assigned, usages, position, diagnostics);
                CheckPaths(token, token.TagArguments, position, diagnostics);
            }
        }

        foreach (var open in openBlocks)
        {
            diagnostics.Add(At(position, open.Token, DiagnosticCodes.UnbalancedBlock, DiagnosticSeverity.Error, $"'{open.Name}' is never closed",
                "Logic Apps fails to parse the map.", $"Add '{{% end{open.Name} %}}'."));
        }

        foreach (var (variable, token) in usages)
        {
            if (!assigned.Contains(variable) && !LiquidLanguage.BuiltInVariables.Contains(variable))
            {
                diagnostics.Add(At(position, token, DiagnosticCodes.UndefinedVariable, DiagnosticSeverity.Warning, $"Variable '{variable}' is never assigned",
                    "It renders as empty. Input fields are under 'content'.", $"Use 'content.{variable}', or assign '{variable}' first.", variable));
            }
        }

        return [.. diagnostics.OrderBy(d => d.Line).ThenBy(d => d.Column)];
    }

    /// <summary>
    /// Returns the root names of the variables used in an expression, for example <c>content</c> for <c>content.items[0].name</c>.
    /// Filter names, named filter arguments, keywords, strings and numbers are skipped.
    /// </summary>
    /// <param name="expression">A Liquid expression.</param>
    /// <returns>The variable root names.</returns>
    internal static IEnumerable<string> VariableRoots(string expression)
    {
        var withoutStrings = RemoveStrings(expression);
        var withoutFilterNames = FilterPattern.Replace(withoutStrings, "|");
        foreach (Match match in IdentifierPattern.Matches(withoutFilterNames))
        {
            var name = match.Groups[1].Value;
            if (!LiquidLanguage.Keywords.Contains(name))
            {
                yield return name;
            }
        }
    }

    private static void AnalyzeTag(LiquidToken token, string name, HashSet<string> assigned, List<(string Name, LiquidToken Token)> usages, TextPosition position, List<Diagnostic> diagnostics)
    {
        var arguments = token.TagArguments;
        switch (name)
        {
            case "assign":
                var assignment = AssignPattern.Match(arguments);
                if (assignment.Success)
                {
                    assigned.Add(assignment.Groups["name"].Value);
                    var operation = AssignOperatorPattern.Match(RemoveStrings(assignment.Groups["value"].Value).Split('|')[0]);
                    if (operation.Success)
                    {
                        var symbol = operation.Value.Trim();
                        diagnostics.Add(At(position, token, DiagnosticCodes.ComparisonInAssign, DiagnosticSeverity.Error, $"'{symbol}' in assign is not evaluated",
                            $"assign does not evaluate comparisons or and/or: '{assignment.Groups["name"].Value}' gets the first value of the expression.",
                            "Use an if that assigns true or false, for example '{% assign isBig = false %}{% if x > 10 %}{% assign isBig = true %}{% endif %}'.", symbol));
                    }

                    CheckFilters(token, assignment.Groups["value"].Value, position, diagnostics);
                    usages.AddRange(VariableRoots(assignment.Groups["value"].Value).Select(n => (n, token)));
                }

                break;

            case "capture":
                var target = FirstWordPattern.Match(arguments);
                if (target.Success)
                {
                    assigned.Add(target.Groups[1].Value);
                }

                break;

            case "for" or "tablerow" or RuntimeConstants.JsonArrayForTagName:
                var loop = LoopPattern.Match(arguments);
                if (loop.Success)
                {
                    assigned.Add(loop.Groups["item"].Value);
                    usages.AddRange(VariableRoots(loop.Groups["collection"].Value).Select(n => (n, token)));
                }

                break;

            case "if" or "unless" or "case":
                CheckCondition(token, position, diagnostics);
                usages.AddRange(VariableRoots(arguments).Select(n => (n, token)));
                break;

            case "include" or "extends":
                diagnostics.Add(At(position, token, DiagnosticCodes.IncludeNotSupported, DiagnosticSeverity.Error, $"'{name}' is not supported in Logic Apps",
                    "Logic Apps does not configure a template file system, so the tag renders as a 'Liquid error' text instead of the content.", "Put the content in the map itself.", name));
                break;
        }
    }

    private static void CloseBlock(LiquidToken token, string blockName, Stack<OpenBlock> openBlocks, TextPosition position, List<Diagnostic> diagnostics)
    {
        if (openBlocks.Count == 0)
        {
            diagnostics.Add(At(position, token, DiagnosticCodes.UnbalancedBlock, DiagnosticSeverity.Error, $"'{token.TagName}' has no matching '{blockName}'",
                "Logic Apps fails to parse the map.", $"Remove it, or add the opening '{{% {blockName} %}}'."));
            return;
        }

        var open = openBlocks.Peek();
        if (open.Name == blockName)
        {
            openBlocks.Pop();
            return;
        }

        var hint = open.Name.Equals(blockName, StringComparison.OrdinalIgnoreCase)
            ? $" End tags are case-sensitive: use 'end{open.Name}'."
            : string.Empty;
        if (openBlocks.Any(b => b.Name == "comment"))
        {
            hint += CommentHint;
        }
        diagnostics.Add(At(position, token, DiagnosticCodes.UnbalancedBlock, DiagnosticSeverity.Error, $"'{token.TagName}' does not close '{open.Name}'",
            $"'{open.Name}' was opened at '{open.Token.Raw.Split('\n')[0]}'. Logic Apps fails to parse the map.{hint}", $"Close '{open.Name}' first with '{{% end{open.Name} %}}'."));

        // Recover when the end tag closes an outer block, so one mistake does not cascade.
        if (openBlocks.Any(b => b.Name == blockName))
        {
            while (openBlocks.Pop().Name != blockName)
            {
            }
        }
        else if (open.Name.Equals(blockName, StringComparison.OrdinalIgnoreCase))
        {
            openBlocks.Pop();
        }
    }

    private static void CheckFilters(LiquidToken token, string expression, TextPosition position, List<Diagnostic> diagnostics)
    {
        CheckFilterArguments(token, expression, position, diagnostics);

        foreach (Match match in FilterPattern.Matches(RemoveStrings(expression)))
        {
            var filter = match.Groups[1].Value;
            if (LiquidLanguage.Filters.Contains(filter))
            {
                continue;
            }

            if (filter.Equals("Concat", StringComparison.OrdinalIgnoreCase) && UniqAfterConcatPattern.IsMatch(RemoveStrings(expression)))
            {
                diagnostics.Add(At(position, token, DiagnosticCodes.UnknownFilter, DiagnosticSeverity.Warning, $"Filter '{filter}' does not exist in Logic Apps",
                    $"It is skipped, so 'x | {filter}: x | Uniq' only works because Uniq wraps a single object in a list. Uniq also removes duplicate values.",
                    $"Remove {filter} and use '| Compact' (for JSON input).", filter));
                continue;
            }

            var suggestion = LiquidLanguage.SuggestFilter(filter);
            diagnostics.Add(suggestion is null
                ? UnknownFilter(position, token, filter)
                : At(position, token, DiagnosticCodes.FilterCasing, DiagnosticSeverity.Error, $"Filter '{filter}' is skipped by Logic Apps",
                    "Logic Apps uses C# names for filters. It silently skips an unknown filter, so the value is output unchanged.", $"Use '{suggestion}'.", filter));
        }
    }

    // Literal filter arguments whose meaning differs from Shopify Liquid.
    private static void CheckFilterArguments(LiquidToken token, string expression, TextPosition position, List<Diagnostic> diagnostics)
    {
        foreach (Match match in ConversionIdiomPattern.Matches(RemoveStrings(expression)))
        {
            var idiom = match.Groups["idiom"].Value;
            diagnostics.Add(At(position, token, DiagnosticCodes.NumberConversionIdiom, DiagnosticSeverity.Warning, $"'| {idiom}' does not convert text to a number",
                "On text, Plus joins ('123' | Plus: 0 gives '1230'), Times with a whole number repeats, and Minus or DividedBy fail. It only works when the value is already a number.",
                "Use '| Times: 1.0' for a decimal, or '| Times: 1.0 | Round' for a whole number.", idiom.Split(':')[0].Trim()));
        }

        if (AbsConversionPattern.IsMatch(RemoveStrings(expression)))
        {
            diagnostics.Add(At(position, token, DiagnosticCodes.NumberConversionIdiom, DiagnosticSeverity.Warning, "'| Abs' is used to convert text to a number",
                "It works, but it also drops the minus sign ('-1.5' becomes 1.5), returns 0 for text that is not a number, and reads the text with the host culture.",
                "Use '| Times: 1.0' when the sign matters.", "Abs"));
        }

        foreach (Match match in LiteralArgumentPattern.Matches(expression))
        {
            var filter = match.Groups["filter"].Value;
            var argument = match.Groups["double"].Success ? match.Groups["double"].Value : match.Groups["single"].Value;
            if (filter is "Replace" or "ReplaceFirst" or "Remove" or "RemoveFirst" && argument.Length == 0)
            {
                // '''' is matched as '' followed by another ''.
                var doubledQuote = expression.AsSpan(match.Index + match.Length).StartsWith("''", StringComparison.Ordinal);
                diagnostics.Add(doubledQuote
                    ? At(position, token, DiagnosticCodes.EmptySearchText, DiagnosticSeverity.Warning, $"{filter}: '''' is not an escaped apostrophe",
                        $"Liquid has no escape characters, so '''' is two empty strings and {filter} does nothing.", "Write \"'\" (an apostrophe in double quotes).", "''''")
                    : At(position, token, DiagnosticCodes.EmptySearchText, DiagnosticSeverity.Warning, $"{filter} with an empty search text does nothing",
                        "Shopify Liquid would insert the replacement between every character; DotLiquid does nothing.", "Remove the filter, or give a search text.", filter));
            }
            else if (filter is "Replace" or "ReplaceFirst" or "RemoveFirst" && RegexIntent.IsAccidentalRegex(argument))
            {
                diagnostics.Add(At(position, token, DiagnosticCodes.ReplaceIsRegex, DiagnosticSeverity.Warning, $"{filter}: '{argument}' is a regular expression",
                    "DotLiquid uses the search text as a regular expression, so special characters such as '.' match more than themselves. Shopify Liquid replaces literal text.",
                    "Escape special characters, for example '\\.' for a dot.", filter));
            }
            else if (filter == "Date" && argument.Contains('%', StringComparison.Ordinal))
            {
                diagnostics.Add(At(position, token, DiagnosticCodes.DateStrftimeFormat, DiagnosticSeverity.Warning, $"Date: '{argument}' is a Ruby strftime format",
                    "DotLiquid uses .NET date formats, so '%Y' and similar are not replaced.", "Use .NET formats, for example 'yyyy-MM-dd' instead of '%Y-%m-%d'.", "Date"));
            }
            else if (filter == "Date" && LiteralUtcMarkerPattern.IsMatch(argument))
            {
                diagnostics.Add(At(position, token, DiagnosticCodes.DateTimeZoneConversion, DiagnosticSeverity.Warning, $"Date: '{argument}' writes a literal 'Z'",
                    "DotLiquid converts a date with a time zone to the time zone of the Logic Apps host. The 'Z' (UTC) is only correct while the Logic App runs in UTC (no WEBSITE_TIME_ZONE app setting).",
                    "Use 'K' to write the actual offset.", "Date"));
            }
        }
    }

    // XML converted to JSON gives names such as '@code', '#text' and 'ns0:Bal'. In a dot path DotLiquid drops the special character.
    private static void CheckPaths(LiquidToken token, string expression, TextPosition position, List<Diagnostic> diagnostics)
    {
        foreach (Match match in SpecialPathSegmentPattern.Matches(RemoveStrings(expression)))
        {
            var segment = match.Groups["segment"].Value;
            var readsInstead = segment.Contains(':', StringComparison.Ordinal)
                ? "it reads nothing"
                : $"it reads the property '{segment.TrimStart('@', '$', '#')}' instead";
            diagnostics.Add(At(position, token, DiagnosticCodes.SpecialCharacterInPath, DiagnosticSeverity.Warning, $"'.{segment}' does not read '{segment}'",
                $"In a dot path DotLiquid drops the special character, so {readsInstead}, without any error. Such names come from XML converted to JSON.",
                $"Use brackets: '[\"{segment}\"]'.", "." + segment));
        }
    }

    private static Diagnostic UnknownFilter(TextPosition position, LiquidToken token, string filter)
    {
        const string effect = "Logic Apps does not fail: it skips the filter and passes the value through unchanged.";
        if (filter.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return At(position, token, DiagnosticCodes.UnknownFilter, DiagnosticSeverity.Error, $"Filter '{filter}' does not exist in Logic Apps",
                $"Shopify's json filter is not in DotLiquid {RuntimeConstants.DotLiquidVersion} and is skipped. An object then prints as nothing, and a list of objects as internal type names.",
                "Print the fields one by one, or pass the JSON as text in the input of the Liquid action.", filter);
        }

        if (LiquidLanguage.ShopifyOnlyFilters.Contains(filter))
        {
            return At(position, token, DiagnosticCodes.UnknownFilter, DiagnosticSeverity.Error, $"Filter '{filter}' does not exist in Logic Apps",
                $"'{filter}' exists in Shopify Liquid, but not in DotLiquid {RuntimeConstants.DotLiquidVersion}. {effect}",
                "Rewrite it with a for loop and the filters DotLiquid has.", filter);
        }

        return At(position, token, DiagnosticCodes.UnknownFilter, DiagnosticSeverity.Error, $"Unknown filter '{filter}'",
            $"{effect} If it is meant to select array items by a property, it does not filter anything: DotLiquid has no 'Where' filter.",
            "Use a for loop with an if to select items.", filter);
    }

    // DotLiquid ends an output at the first '}', also inside quotes ('{{ x | Default: "{}" }}').
    // And in '{{{ x }}' the third '{' becomes part of the expression.
    private static void CheckBraceInOutput(LiquidToken token, TextPosition position, List<Diagnostic> diagnostics)
    {
        if (token.Raw.StartsWith("{{{", StringComparison.Ordinal))
        {
            // With a space after the extra brace Logic Apps writes 'Liquid error'; without one it happens to work.
            var breaks = token.Raw.Length > 3 && char.IsWhiteSpace(token.Raw[3]);
            var (braceLine, braceColumn) = position.At(token.Offset + 2);
            diagnostics.Add(new Diagnostic(DiagnosticCodes.ExtraOpeningBrace, breaks ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning, "Extra '{' at the start of {{ }}", DiagnosticSource.Map, braceLine, braceColumn)
            {
                Explanation = breaks
                    ? "DotLiquid reads the third '{' as part of the expression, so Logic Apps writes 'Liquid error: Value cannot be null. (Parameter 'input')' instead of the value."
                    : "It only works because there is no space after the extra '{'; one added space turns the output into a 'Liquid error'.",
                Fix = "Use two braces: '{{ ... }}'. If the first '{' is meant as text, for example to open a JSON object, put a space after it: '{ {{ ... }}'.",
            });
        }

        var brace = token.Raw.IndexOf('}', 2);
        if (brace >= 0 && brace < token.Raw.Length - 2)
        {
            var (line, column) = position.At(token.Offset + brace);
            diagnostics.Add(new Diagnostic(DiagnosticCodes.BraceInOutput, DiagnosticSeverity.Error, "'}' inside {{ }} ends the output", DiagnosticSource.Map, line, column)
            {
                Explanation = "DotLiquid ends an output at the first '}', also inside quotes, so Logic Apps fails to parse the map.",
                Fix = "Assign the text first, for example '{% assign braces = \"{}\" %}{{ x | Default: braces }}'. A '}' inside {% %} is fine.",
            });
        }
    }

    // forloop properties are lowercase, and JSONArrayFor has no forloop.
    private static void CheckForLoop(LiquidToken token, string expression, Stack<OpenBlock> openBlocks, TextPosition position, List<Diagnostic> diagnostics)
    {
        var matches = ForLoopPropertyPattern.Matches(RemoveStrings(expression));
        if (matches.Count == 0)
        {
            return;
        }

        var loop = openBlocks.FirstOrDefault(b => LoopTags.Contains(b.Name));
        if (loop?.Name == RuntimeConstants.JsonArrayForTagName)
        {
            diagnostics.Add(At(position, token, DiagnosticCodes.ForLoopInJsonArrayFor, DiagnosticSeverity.Error, "JSONArrayFor has no forloop",
                $"'{matches[0].Value}' is always empty inside JSONArrayFor; only for and tablerow set forloop.",
                "Use a for loop when you need forloop, and write the commas yourself: '{% unless forloop.last %},{% endunless %}'.", "forloop"));
            return;
        }

        foreach (Match match in matches)
        {
            var property = match.Groups["property"].Value;
            if (ForLoopProperties.Contains(property))
            {
                continue;
            }

            var lower = ForLoopProperties.FirstOrDefault(p => p.Equals(property, StringComparison.OrdinalIgnoreCase));
            diagnostics.Add(lower is not null
                ? At(position, token, DiagnosticCodes.ForLoopProperty, DiagnosticSeverity.Error, $"'forloop.{property}' is always empty",
                    $"forloop properties are lowercase in DotLiquid; '{property}' does not exist, so a check such as 'forloop.{property} == true' is never true.",
                    $"Use 'forloop.{lower}'.", match.Value)
                : At(position, token, DiagnosticCodes.ForLoopProperty, DiagnosticSeverity.Error, $"forloop has no property '{property}'",
                    "It is always empty.", "Use index, index0, rindex, rindex0, first, last or length.", match.Value));
        }
    }

    private static void CheckCondition(LiquidToken token, TextPosition position, List<Diagnostic> diagnostics)
    {
        var condition = RemoveStrings(token.TagArguments);
        if (token.TagName is "if" or "unless" or "elsif" or "elseif")
        {
            if (condition.Contains('(', StringComparison.Ordinal) || condition.Contains(')', StringComparison.Ordinal))
            {
                diagnostics.Add(At(position, token, DiagnosticCodes.ParenthesesInCondition, DiagnosticSeverity.Error, "Parentheses in a condition",
                    "Liquid has no parentheses in conditions. DotLiquid does not fail, but evaluates the condition differently than intended.",
                    "Nest if blocks, or assign the parts to variables first. and/or are evaluated from right to left: 'a and b or c' means 'a and (b or c)'.",
                    condition.Contains('(', StringComparison.Ordinal) ? "(" : ")"));
            }

            if (condition.Contains('|', StringComparison.Ordinal))
            {
                diagnostics.Add(At(position, token, DiagnosticCodes.FilterInCondition, DiagnosticSeverity.Error, "Filter in a condition is ignored",
                    "DotLiquid ignores filters in a condition, so it only checks whether the value is set: '{% if x | Size > 5 %}' is true for any non-empty x.",
                    "Assign first: '{% assign length = x | Size %}{% if length > 5 %}'.", "|"));
            }
        }

        if (NilComparisonPattern.IsMatch(condition))
        {
            diagnostics.Add(At(position, token, DiagnosticCodes.NilComparison, DiagnosticSeverity.Warning, "Comparison with nil misses JSON null values",
                "In Logic Apps a JSON null value becomes an empty string, so it is not nil. Only a missing property is nil.",
                "Also compare with \"\", for example 'x != nil and x != \"\"'.", token.TagArguments.Contains("null", StringComparison.Ordinal) ? "null" : "nil"));
        }

        if (token.TagName is "if" or "unless" or "elsif" or "elseif")
        {
            var truthinessCheck = LogicalOperatorPattern.Split(condition).Select(part => part.Trim()).FirstOrDefault(part => InputPathPattern.IsMatch(part));
            if (truthinessCheck is not null)
            {
                diagnostics.Add(At(position, token, DiagnosticCodes.NullIsTruthy, DiagnosticSeverity.Warning, $"'{truthinessCheck}' is true for JSON null",
                    "In Logic Apps a JSON null value becomes an empty string, which is truthy, so a check without comparison is true for null.",
                    $"Compare explicitly, for example '{truthinessCheck} != \"\"'.", truthinessCheck));
            }
        }
    }

    private static void CheckUnterminated(LiquidToken token, TextPosition position, List<Diagnostic> diagnostics)
    {
        foreach (var opener in new[] { "{{", "{%" })
        {
            var index = token.Raw.IndexOf(opener, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            var (line, column) = position.At(token.Offset + index);
            var closer = opener == "{{" ? "}}" : "%}";
            var spansLines = token.Raw.IndexOf(closer, index, StringComparison.Ordinal) > index;
            diagnostics.Add(spansLines
                ? new Diagnostic(DiagnosticCodes.UnterminatedTag, DiagnosticSeverity.Error, $"'{opener} ... {closer}' spans more than one line", DiagnosticSource.Map, line, column)
                {
                    Explanation = "DotLiquid does not allow line breaks inside a tag or output (Shopify Liquid does), so Logic Apps fails to parse the map.",
                    Fix = "Put it on one line.",
                }
                : new Diagnostic(DiagnosticCodes.UnterminatedTag, DiagnosticSeverity.Error, $"'{opener}' is never closed", DiagnosticSource.Map, line, column)
                {
                    Explanation = "Logic Apps fails to parse the map.",
                    Fix = $"Add '{closer}'.",
                });
        }
    }

    // A finding at the token, or at the first occurrence of 'find' in the token when given, so the excerpt points at the problem.
    private static Diagnostic At(TextPosition position, LiquidToken token, string code, DiagnosticSeverity severity, string title, string? explanation, string? fix, string? find = null)
    {
        var offset = token.Offset;
        if (!string.IsNullOrEmpty(find))
        {
            var index = token.Raw.IndexOf(find, StringComparison.Ordinal);
            if (index >= 0)
            {
                offset += index;
            }
        }

        var (line, column) = position.At(offset);
        return new Diagnostic(code, severity, title, DiagnosticSource.Map, line, column) { Explanation = explanation, Fix = fix };
    }

    private static string RemoveStrings(string expression)
    {
        return StringPattern.Replace(expression, "\"\"");
    }
}
