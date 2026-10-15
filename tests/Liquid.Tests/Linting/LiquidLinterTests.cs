using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Linting;
using LogicApps.Liquid.Models;
using NUnit.Framework;

namespace LogicApps.Liquid.Tests.Linting;

[TestFixture]
public sealed class LiquidLinterTests
{
    private LiquidLinter _linter;

    [SetUp]
    public void SetUp()
    {
        _linter = new LiquidLinter();
    }

    private Diagnostic LintAndFind(string map, string code)
    {
        var diagnostics = _linter.Lint(map);
        Assert.That(diagnostics.Select(d => d.Code), Does.Contain(code), string.Join(Environment.NewLine, diagnostics));
        return diagnostics.First(d => d.Code == code);
    }

    [Test]
    public void Lint_Should_Return_No_Findings_When_Map_Is_Valid()
    {
        // Arrange
        const string map = """
            {%- assign total = 0 -%}
            {%- for line in content.lines -%}
              {%- if line.amount > 0 -%}{%- assign total = total | Plus: line.amount -%}{%- elsif line.x -%}{%- elseif line.y -%}{%- else -%}{%- endif -%}
            {%- endfor -%}
            {%- capture text -%}{{ total | Round: 2 }}{%- endcapture -%}
            {%- comment -%} anything {{ here | is_ignored }} {%- endcomment -%}
            {%- JSONArrayFor x in content.items -%}{{ x }}{%- endJSONArrayFor -%}
            {{ text | Append: "x" | Date: "yyyy" }}
            """;

        // Act
        var diagnostics = _linter.Lint(map);

        // Assert
        Assert.That(diagnostics, Is.Empty);
    }

    [TestCase("{{ content.a | upcase }}", "Upcase")]
    [TestCase("{{ content.a | strip_html }}", "StripHtml")]
    [TestCase("{% assign x = content.a | divided_by: 2 %}", "DividedBy")]
    public void Lint_Should_Suggest_CSharp_Name_When_Filter_Uses_Ruby_Naming(string map, string suggestion)
    {
        // Act
        var diagnostic = LintAndFind(map, DiagnosticCodes.FilterCasing);

        // Assert
        Assert.That(diagnostic.Message, Does.Contain($"'{suggestion}'"));
    }

    [Test]
    public void Lint_Should_Report_Error_When_Filter_Is_Unknown()
    {
        // Act
        var diagnostic = LintAndFind("{% assign selected = content.items | type: content.type %}", DiagnosticCodes.UnknownFilter);

        // Assert
        Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
    }

    [Test]
    public void Lint_Should_Explain_Shopify_Origin_When_Filter_Only_Exists_In_Shopify()
    {
        // Act
        var diagnostic = LintAndFind("{{ content.a | where: 'x', 1 }}", DiagnosticCodes.UnknownFilter);

        // Assert
        Assert.That(diagnostic.Message, Does.Contain("Shopify"));
    }

    [Test]
    public void Lint_Should_Ignore_Filter_Names_When_They_Are_Inside_Strings()
    {
        // Act
        var diagnostics = _linter.Lint("{{ \"a | upcase\" | Append: '| b' }}");

        // Assert
        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public void Lint_Should_Suggest_Lowercase_Tag_When_Tag_Is_Wrongly_Cased()
    {
        // Act
        var diagnostic = LintAndFind("{% If content.a %}x{% endif %}", DiagnosticCodes.UnknownTag);

        // Assert
        Assert.That(diagnostic.Fix, Does.Contain("'if'"));
        Assert.That((diagnostic.Line, diagnostic.Column), Is.EqualTo((1, 4)), "points at the tag name");
    }

    [Test]
    public void Lint_Should_Report_Opening_Location_When_Block_Is_Never_Closed()
    {
        // Act
        var diagnostic = LintAndFind("line1\n  {% for x in content.a %}\n{{ x }}", DiagnosticCodes.UnbalancedBlock);

        // Assert
        Assert.That((diagnostic.Line, diagnostic.Column), Is.EqualTo((2, 3)));
    }

    [Test]
    public void Lint_Should_Report_Error_When_End_Tag_Does_Not_Match()
    {
        // Act
        var diagnostics = _linter.Lint("{% if content.a %}{% endfor %}");

        // Assert
        Assert.That(diagnostics.Select(d => d.Message), Has.Some.Contains("does not close 'if'"));
    }

    [Test]
    public void Lint_Should_Report_Error_When_Output_Is_Never_Closed()
    {
        // Act & Assert
        LintAndFind("{{ content.a ", DiagnosticCodes.UnterminatedTag);
    }

    [Test]
    public void Lint_Should_Report_Warning_When_Comparing_With_Nil()
    {
        // Act
        var diagnostic = LintAndFind("{% if content.a == nil %}{% endif %}", DiagnosticCodes.NilComparison);

        // Assert
        Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
    }

    [Test]
    public void Lint_Should_Suggest_Content_Prefix_When_Variable_Is_Never_Assigned()
    {
        // Act
        var diagnostic = LintAndFind("{{ name }}", DiagnosticCodes.UndefinedVariable);

        // Assert
        Assert.That(diagnostic.Message, Does.Contain("content.name"));
    }

    [Test]
    public void Lint_Should_Not_Report_Undefined_Variables_When_Using_Loop_Variables_And_Named_Arguments()
    {
        // Act
        var diagnostics = _linter.Lint("{% for x in content.a limit: 2 %}{{ x.b | Truncate: 5 }}{{ forloop.index }}{% endfor %}");

        // Assert
        Assert.That(diagnostics, Is.Empty);
    }

    [TestCase("{% if content.email %}x{% endif %}", "content.email")]
    [TestCase("{% if content.a == 1 and content.customer.email %}x{% endif %}", "content.customer.email")]
    [TestCase("{% unless content[\"x\"] %}x{% endunless %}", "content[\"\"]")]
    public void Lint_Should_Report_Warning_When_Input_Value_Is_Checked_Without_Comparison(string map, string path)
    {
        // Act
        var diagnostic = LintAndFind(map, DiagnosticCodes.NullIsTruthy);

        // Assert
        Assert.That(diagnostic.Message, Does.Contain(path));
    }

    [Test]
    public void Lint_Should_Not_Report_Null_Truthiness_When_Condition_Compares()
    {
        // Act
        var diagnostics = _linter.Lint("{% if content.email != \"\" %}x{% endif %}");

        // Assert
        Assert.That(diagnostics, Is.Empty);
    }

    [TestCase("{{ content.a | Replace: '.', ',' }}")]
    [TestCase("{{ content.a | ReplaceFirst: \"(\", \"\" }}")]
    [TestCase("{% assign x = content.a | RemoveFirst: '$' %}")]
    public void Lint_Should_Report_Warning_When_Replace_Search_Text_Has_Regex_Characters(string map)
    {
        // Act & Assert
        LintAndFind(map, DiagnosticCodes.ReplaceIsRegex);
    }

    [TestCase("{{ content.a | Replace: 'abc', 'x' }}")]
    [TestCase("{{ content.a | Replace: '\\\\', '\\\\' | Replace: '\"', '\\\"' }}")]
    [TestCase("{{ content.a | Replace: '\\.', ',' }}")]
    public void Lint_Should_Not_Report_Regex_When_Search_Text_Is_Plain_Or_Escaped_On_Purpose(string map)
    {
        // Act
        var diagnostics = _linter.Lint(map);

        // Assert
        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public void Lint_Should_Report_Warning_When_Date_Uses_Strftime_Format()
    {
        // Act & Assert
        LintAndFind("{{ content.d | Date: '%Y-%m-%d' }}", DiagnosticCodes.DateStrftimeFormat);
    }

    [TestCase("{{ content.attr.@code }}", "it reads the property 'code' instead")]
    [TestCase("{{ content.env.$content }}", "it reads the property 'content' instead")]
    [TestCase("{% assign t = content.x.#text %}", "it reads the property 'text' instead")]
    [TestCase("{% if content.x.ns0:Bal == 1 %}{% endif %}", "it reads nothing")]
    public void Lint_Should_Report_Warning_When_Dot_Path_Has_Special_Character(string map, string effect)
    {
        // Act
        var diagnostic = LintAndFind(map, DiagnosticCodes.SpecialCharacterInPath);

        // Assert
        Assert.That(diagnostic.Message, Does.Contain(effect));
    }

    [Test]
    public void Lint_Should_Not_Report_Undefined_Variable_When_Path_Segment_Has_Special_Character()
    {
        // Act
        var diagnostics = _linter.Lint("{{ content.totals.@currency }}");

        // Assert
        Assert.That(diagnostics.Select(d => d.Code), Is.EqualTo([DiagnosticCodes.SpecialCharacterInPath]));
    }

    [Test]
    public void Lint_Should_Not_Report_Special_Character_When_Brackets_Are_Used()
    {
        // Act
        var diagnostics = _linter.Lint("{{ content.attr[\"@code\"] }}{{ content.x['ns0:Bal'] }}{{ content.x.[\"@type\"] }}{{ 1.5 | Times: 2 }}");

        // Assert
        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public void Lint_Should_Report_Warning_When_If_Is_Directly_Inside_JsonArrayFor()
    {
        // Act & Assert
        LintAndFind("[{% JSONArrayFor n in content.a %}{% if n > 1 %}{{ n }}{% endif %}{% endJSONArrayFor %}]", DiagnosticCodes.ConditionInJsonArrayFor);
    }

    [TestCase("{% assign v = content.a | Plus: 0 %}")]
    [TestCase("{{ content.a | Times: 1 }}")]
    [TestCase("{{ content.a | Minus: 0 }}")]
    public void Lint_Should_Report_Warning_When_Using_Conversion_Idiom_That_Does_Not_Convert_Text(string map)
    {
        // Act & Assert
        LintAndFind(map, DiagnosticCodes.NumberConversionIdiom);
    }

    [Test]
    public void Lint_Should_Report_Warning_When_Abs_Is_Used_As_Conversion_Before_Calculating()
    {
        // Act
        var diagnostic = LintAndFind("{% assign minutes = content.hours | Abs | Times: 60 %}", DiagnosticCodes.NumberConversionIdiom);

        // Assert
        Assert.That(diagnostic.Message, Does.Contain("minus sign"));
    }

    [Test]
    public void Lint_Should_Explain_Json_When_Json_Filter_Is_Used()
    {
        // Act
        var diagnostic = LintAndFind("{{ content | Json }}", DiagnosticCodes.UnknownFilter);

        // Assert
        Assert.That(diagnostic.Message, Does.Contain("prints as nothing"));
    }

    [TestCase("{{ content.a | Times: 1.0 }}")]
    [TestCase("{{ content.a | Times: 10 }}")]
    [TestCase("{{ content.a | Plus: 0.5 }}")]
    public void Lint_Should_Not_Report_Conversion_Idiom_When_Operand_Differs(string map)
    {
        // Act
        var diagnostics = _linter.Lint(map);

        // Assert
        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public void Lint_Should_Explain_Doubled_Quote_When_Replace_Searches_For_Four_Quotes()
    {
        // Act
        var diagnostic = LintAndFind("{{ content.a | Replace: '''' ,'&apos;' }}", DiagnosticCodes.EmptySearchText);

        // Assert
        Assert.That(diagnostic.Message, Does.Contain("not an escaped apostrophe"));
    }

    [Test]
    public void Lint_Should_Explain_Idiom_When_Concat_Is_Followed_By_Uniq()
    {
        // Act
        var diagnostic = LintAndFind("{% assign items = content.a | Concat: content.a | Uniq %}", DiagnosticCodes.UnknownFilter);

        // Assert
        Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        Assert.That(diagnostic.Message, Does.Contain("Compact"));
    }

    [Test]
    public void Lint_Should_Report_Warning_When_Date_Format_Has_Literal_Z()
    {
        // Act
        var diagnostic = LintAndFind("{{ content.d | Date: 'yyyy-MM-ddTHH:mm:ssZ' }}", DiagnosticCodes.DateTimeZoneConversion);

        // Assert
        Assert.That(diagnostic.Message, Does.Contain("WEBSITE_TIME_ZONE"));
    }

    [TestCase("{% increment counter %}")]
    [TestCase("{% render 'snippet' %}")]
    [TestCase("{% echo content.a %}")]
    public void Lint_Should_Report_Shopify_Only_Tag_When_Tag_Does_Not_Exist_In_DotLiquid(string map)
    {
        // Act
        var diagnostic = LintAndFind(map, DiagnosticCodes.UnknownTag);

        // Assert
        Assert.That(diagnostic.Message, Does.Contain("Shopify"));
    }

    [Test]
    public void Lint_Should_Report_Error_When_Tag_Spans_Lines()
    {
        // Act
        var diagnostic = LintAndFind("{% assign x =\n content.a %}", DiagnosticCodes.UnterminatedTag);

        // Assert
        Assert.That(diagnostic.Message, Does.Contain("more than one line"));
    }

    [TestCase("{% if content.a startsWith 'x' or content.b hasKey 'y' %}{% endif %}")]
    [TestCase("{% literal %}{{ not code }}{% endliteral %}")]
    public void Lint_Should_Not_Report_When_Using_Valid_DotLiquid_Syntax(string map)
    {
        // Act
        var diagnostics = _linter.Lint(map);

        // Assert
        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public void Lint_Should_Report_Error_When_Map_Uses_Include()
    {
        // Act & Assert
        LintAndFind("{% include 'header' %}", DiagnosticCodes.IncludeNotSupported);
    }

    [TestCase("{{ content.x | Default: '{}' }}", 27)]
    [TestCase("{{ content.x | Append: '}' }}", 25)]
    public void Lint_Should_Report_Error_When_Output_Contains_A_Single_Brace(string map, int column)
    {
        // Act
        var diagnostic = LintAndFind(map, DiagnosticCodes.BraceInOutput);

        // Assert
        Assert.That(diagnostic.Column, Is.EqualTo(column), "points at the '}'");
    }

    [Test]
    public void Lint_Should_Not_Report_Brace_When_It_Is_Inside_A_Tag()
    {
        // Act
        var diagnostics = _linter.Lint("{% assign braces = '{}' %}{{ content.x | Default: braces }}");

        // Assert
        Assert.That(diagnostics, Is.Empty);
    }

    [TestCase("{% for x in content.a %}{% if forloop.Last == true %}x{% endif %}{% endfor %}", "forloop.last")]
    [TestCase("{% for x in content.a %}{{ forloop.Index }}{% endfor %}", "forloop.index")]
    [TestCase("{% for x in content.a %}{{ forloop.position }}{% endfor %}", "index0")]
    public void Lint_Should_Report_Error_When_ForLoop_Property_Does_Not_Exist(string map, string fix)
    {
        // Act
        var diagnostic = LintAndFind(map, DiagnosticCodes.ForLoopProperty);

        // Assert
        Assert.That(diagnostic.Fix, Does.Contain(fix));
    }

    [Test]
    public void Lint_Should_Not_Report_ForLoop_When_Properties_Are_Lowercase()
    {
        // Act
        var diagnostics = _linter.Lint("{% for x in content.a %}{{ forloop.index }}{% unless forloop.last %},{% endunless %}{% endfor %}");

        // Assert
        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public void Lint_Should_Report_Error_When_ForLoop_Is_Used_Inside_JsonArrayFor()
    {
        // Act & Assert
        LintAndFind("[{% JSONArrayFor x in content.a %}{{ forloop.index }}{% endJSONArrayFor %}]", DiagnosticCodes.ForLoopInJsonArrayFor);
    }

    [Test]
    public void Lint_Should_Not_Report_ForLoop_When_For_Is_Nested_In_JsonArrayFor()
    {
        // Act
        var diagnostics = _linter.Lint("[{% JSONArrayFor x in content.a %}{% for y in x.b %}{{ forloop.index }}{% endfor %}{% endJSONArrayFor %}]");

        // Assert
        Assert.That(diagnostics.Select(d => d.Code), Does.Not.Contain(DiagnosticCodes.ForLoopInJsonArrayFor));
    }

    [TestCase("{% if content.n == 1 %}a{% else if content.n == 2 %}b{% endif %}")]
    [TestCase("{% if content.n == 1 %}a{% else content.n == 2 %}b{% endif %}")]
    public void Lint_Should_Report_Error_When_Else_Has_A_Condition(string map)
    {
        // Act
        var diagnostics = _linter.Lint(map);

        // Assert
        Assert.That(diagnostics.Select(d => d.Code), Does.Contain(DiagnosticCodes.ElseWithCondition));
        Assert.That(diagnostics.Select(d => d.Code), Does.Not.Contain(DiagnosticCodes.UndefinedVariable), "'if' after else is not a variable");
    }

    [Test]
    public void Lint_Should_Report_Error_When_Condition_Has_Parentheses()
    {
        // Act & Assert
        LintAndFind("{% if (content.a or content.b) and content.c %}x{% endif %}", DiagnosticCodes.ParenthesesInCondition);
    }

    [Test]
    public void Lint_Should_Not_Report_Parentheses_When_They_Are_In_A_String()
    {
        // Act
        var diagnostics = _linter.Lint("{% if content.a == '(x)' %}x{% endif %}");

        // Assert
        Assert.That(diagnostics.Select(d => d.Code), Does.Not.Contain(DiagnosticCodes.ParenthesesInCondition));
    }

    [TestCase("{% if content.t | Size > 5 %}x{% endif %}")]
    [TestCase("{% unless content.t | Downcase == 'a' %}x{% endunless %}")]
    public void Lint_Should_Report_Error_When_Condition_Has_A_Filter(string map)
    {
        // Act & Assert
        LintAndFind(map, DiagnosticCodes.FilterInCondition);
    }

    [TestCase("{% assign big = content.n > 10 %}", ">")]
    [TestCase("{% assign same = content.a == content.b %}", "==")]
    [TestCase("{% assign both = content.a and content.b %}", "and")]
    public void Lint_Should_Report_Error_When_Assign_Has_A_Comparison(string map, string symbol)
    {
        // Act
        var diagnostic = LintAndFind(map, DiagnosticCodes.ComparisonInAssign);

        // Assert
        Assert.That(diagnostic.Title, Does.Contain($"'{symbol}'"));
    }

    [TestCase("{% assign label = 'a > b' %}")]
    [TestCase("{% assign total = content.n | Plus: 1 %}")]
    [TestCase("{% assign items = content.list | Sort: 'date' %}")]
    public void Lint_Should_Not_Report_Comparison_When_Assign_Has_None(string map)
    {
        // Act
        var diagnostics = _linter.Lint(map);

        // Assert
        Assert.That(diagnostics.Select(d => d.Code), Does.Not.Contain(DiagnosticCodes.ComparisonInAssign));
    }

    [Test]
    public void Lint_Should_Suggest_Elsif_When_Tag_Is_Elif()
    {
        // Act
        var diagnostic = LintAndFind("{% if content.a %}a{% elif content.b %}b{% endif %}", DiagnosticCodes.UnknownTag);

        // Assert
        Assert.That(diagnostic.Fix, Does.Contain("'elsif'"));
    }

    [Test]
    public void Lint_Should_Explain_That_Comment_Tags_Are_Parsed_When_Comment_Contains_A_Tag()
    {
        // Act
        var diagnostic = LintAndFind("{% comment %} {% If x %} {% endcomment %}", DiagnosticCodes.UnknownTag);

        // Assert
        Assert.That(diagnostic.Explanation, Does.Contain("inside a comment"));
    }

    [Test]
    public void Lint_Should_Not_Report_Unclosed_Block_When_Map_Uses_Include()
    {
        // Act
        var diagnostics = _linter.Lint("{% include 'header' %}");

        // Assert
        Assert.That(diagnostics.Select(d => d.Code), Does.Not.Contain(DiagnosticCodes.UnbalancedBlock));
    }
}
