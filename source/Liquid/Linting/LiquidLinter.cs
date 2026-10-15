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
    private static readonly Regex _stringPattern = new(@"""[^""]*""|'[^']*'");
    private static readonly Regex _filterPattern = new(@"\|\s*(\w+)");
    // An identifier that is not part of a property path (no leading '.'), not a named argument (no trailing ':').
    private static readonly Regex _identifierPattern = new(@"(?<![\w.\]\[@$#-])([A-Za-z_][\w-]*)(?![\w-]*\s*:)");
    private static readonly Regex _assignPattern = new(@"^(?<name>[\w-]+)\s*=\s*(?<value>.*)$", RegexOptions.Singleline);
    private static readonly Regex _firstWordPattern = new(@"^(\w+)");
    private static readonly Regex _loopPattern = new(@"^(?<item>\w+)\s+in\s+(?<collection>\S+)");
    private static readonly Regex _nilComparisonPattern = new(@"(==|!=|<>)\s*(nil|null)\b|\b(nil|null)\s*(==|!=|<>)");
    private static readonly Regex _logicalOperatorPattern = new(@"\s+(?:and|or)\s+");
    // A bare path into the input, such as content.customer.email or content["a"].b, without any operator.
    private static readonly Regex _inputPathPattern = new(@"^content(\.[\w-]+|\[[^\]]+\])+$");
    private static readonly Regex _literalArgumentPattern = new(@"\|\s*(?<filter>Replace|ReplaceFirst|Remove|RemoveFirst|Date)\s*:\s*(?:""(?<double>[^""]*)""|'(?<single>[^']*)')");
    private static readonly Regex _absConversionPattern = new(@"\|\s*Abs\s*\|\s*(?:Times|Plus|Minus|DividedBy|Modulo)\b");
    private static readonly Regex _conversionIdiomPattern =new(@"\|\s*(?<idiom>(?:Plus|Minus)\s*:\s*0|(?:Times|DividedBy)\s*:\s*1)(?![\w.])");
    private static readonly Regex _uniqAfterConcatPattern = new(@"\|\s*Concat\b[^|]*\|\s*Uniq\b", RegexOptions.IgnoreCase);
    private static readonly Regex _specialPathSegmentPattern = new(@"(?<=[\w\]])\.(?<segment>[@$#][\w:-]*|[\w-]+:[\w:-]+)");
    // An unquoted Z in a .NET date format is written literally.
    private static readonly Regex _literalUtcMarkerPattern = new(@"(?<!')Z(?!')");

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
                if (!insideComment)
                {
                    CheckFilters(token, token.Markup, position, diagnostics);
                    CheckPaths(token, token.Markup, position, diagnostics);
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

            if (LiquidLanguage.IntermediateTags.Contains(name))
            {
                if (openBlocks.Count == 0)
                {
                    diagnostics.Add(At(position, token, DiagnosticCodes.UnbalancedBlock, DiagnosticSeverity.Error, $"'{name}' is used outside of a block",
                        "Logic Apps fails to parse the map.", $"Put '{name}' inside an if, unless or case block."));
                }
                else if (!insideComment)
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
                        "Tags are case-sensitive in Logic Apps, so the map fails to parse.", $"Use '{suggestion}'.", name),
                };
                diagnostics.Add(unknownTag);
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
        var withoutFilterNames = _filterPattern.Replace(withoutStrings, "|");
        foreach (Match match in _identifierPattern.Matches(withoutFilterNames))
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
                var assignment = _assignPattern.Match(arguments);
                if (assignment.Success)
                {
                    assigned.Add(assignment.Groups["name"].Value);
                    CheckFilters(token, assignment.Groups["value"].Value, position, diagnostics);
                    usages.AddRange(VariableRoots(assignment.Groups["value"].Value).Select(n => (n, token)));
                }

                break;

            case "capture":
                var target = _firstWordPattern.Match(arguments);
                if (target.Success)
                {
                    assigned.Add(target.Groups[1].Value);
                }

                break;

            case "for" or "tablerow" or RuntimeConstants.JsonArrayForTagName:
                var loop = _loopPattern.Match(arguments);
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

            default:
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

        foreach (Match match in _filterPattern.Matches(RemoveStrings(expression)))
        {
            var filter = match.Groups[1].Value;
            if (LiquidLanguage.Filters.Contains(filter))
            {
                continue;
            }

            if (filter.Equals("Concat", StringComparison.OrdinalIgnoreCase) && _uniqAfterConcatPattern.IsMatch(RemoveStrings(expression)))
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
        foreach (Match match in _conversionIdiomPattern.Matches(RemoveStrings(expression)))
        {
            var idiom = match.Groups["idiom"].Value;
            diagnostics.Add(At(position, token, DiagnosticCodes.NumberConversionIdiom, DiagnosticSeverity.Warning, $"'| {idiom}' does not convert text to a number",
                "On text, Plus joins ('123' | Plus: 0 gives '1230'), Times with a whole number repeats, and Minus or DividedBy fail. It only works when the value is already a number.",
                "Use '| Times: 1.0' for a decimal, or '| Times: 1.0 | Round' for a whole number.", idiom.Split(':')[0].Trim()));
        }

        if (_absConversionPattern.IsMatch(RemoveStrings(expression)))
        {
            diagnostics.Add(At(position, token, DiagnosticCodes.NumberConversionIdiom, DiagnosticSeverity.Warning, "'| Abs' is used to convert text to a number",
                "It works, but it also drops the minus sign ('-1.5' becomes 1.5), returns 0 for text that is not a number, and reads the text with the host culture.",
                "Use '| Times: 1.0' when the sign matters.", "Abs"));
        }

        foreach (Match match in _literalArgumentPattern.Matches(expression))
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
            else if (filter == "Date" && _literalUtcMarkerPattern.IsMatch(argument))
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
        foreach (Match match in _specialPathSegmentPattern.Matches(RemoveStrings(expression)))
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

    private static void CheckCondition(LiquidToken token, TextPosition position, List<Diagnostic> diagnostics)
    {
        var condition = RemoveStrings(token.TagArguments);
        if (_nilComparisonPattern.IsMatch(condition))
        {
            diagnostics.Add(At(position, token, DiagnosticCodes.NilComparison, DiagnosticSeverity.Warning, "Comparison with nil misses JSON null values",
                "In Logic Apps a JSON null value becomes an empty string, so it is not nil. Only a missing property is nil.",
                "Also compare with \"\", for example 'x != nil and x != \"\"'.", token.TagArguments.Contains("null", StringComparison.Ordinal) ? "null" : "nil"));
        }

        if (token.TagName is "if" or "unless" or "elsif" or "elseif")
        {
            var truthinessCheck = _logicalOperatorPattern.Split(condition).Select(part => part.Trim()).FirstOrDefault(part => _inputPathPattern.IsMatch(part));
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
        return _stringPattern.Replace(expression, "\"\"");
    }
}
