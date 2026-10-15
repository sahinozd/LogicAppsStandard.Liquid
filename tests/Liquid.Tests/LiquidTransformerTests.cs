using System.Globalization;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LogicApps.Liquid.Tests;

/// <summary>
/// Pins down how Logic Apps renders Liquid. The expected values were observed on the real Logic Apps engine
/// (workflow extension bundle 1.170.91); the conformance tests compare against that engine directly.
/// </summary>
[TestFixture]
public sealed class LiquidTransformerTests
{
    private static string Render(string map, string input, LiquidTransformKind kind = LiquidTransformKind.JsonToText, string culture = "en-US")
    {
        var transformer = new LiquidTransformer(new LiquidTransformOptions { Culture = CultureInfo.GetCultureInfo(culture), Lint = false });
        var result = transformer.Transform(map, input, kind);
        Assert.That(result.RenderedText, Is.Not.Null, string.Join(Environment.NewLine, result.Diagnostics));
        return result.RenderedText!;
    }

    [Test]
    public void Constructor_Should_Throw_ArgumentNullException_When_Options_Is_Null()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new LiquidTransformer(null!);
        });

        Assert.That(exception.ParamName, Is.EqualTo("options"));
    }

    [Test]
    public void Transform_Should_Apply_Filter_When_Filter_Uses_CSharp_Naming()
    {
        // Act
        var output = Render("{{ content.name | Upcase }}", """{"name":"sahin"}""");

        // Assert
        Assert.That(output, Is.EqualTo("SAHIN"));
    }

    [Test]
    public void Transform_Should_Skip_Filter_When_Filter_Uses_Ruby_Naming()
    {
        // Act
        var output = Render("{{ content.name | upcase }}", """{"name":"sahin"}""");

        // Assert
        Assert.That(output, Is.EqualTo("sahin"));
    }

    [Test]
    public void Transform_Should_Pass_Value_Through_When_Filter_Is_Unknown()
    {
        // Act
        var output = Render("{% assign x = content.items | id: 'b' %}{{ x[0].id }}", """{"items":[{"id":"a"},{"id":"b"}]}""");

        // Assert
        Assert.That(output, Is.EqualTo("a"));
    }

    [Test]
    public void Transform_Should_Render_Empty_String_Not_Nil_When_Json_Value_Is_Null()
    {
        // Act
        var output = Render("[{{ content.v }}]{% if content.v == nil %}nil{% else %}not nil{% endif %}", """{"v":null}""");

        // Assert
        Assert.That(output, Is.EqualTo("[]not nil"));
    }

    [Test]
    public void Transform_Should_Treat_Property_As_Nil_When_Property_Is_Missing()
    {
        // Act
        var output = Render("{% if content.missing == nil %}nil{% endif %}", "{}");

        // Assert
        Assert.That(output, Is.EqualTo("nil"));
    }

    [Test]
    public void Transform_Should_Keep_Date_As_String_When_Input_Contains_Date()
    {
        // Act
        var output = Render("{{ content.d }}", """{"d":"2026-10-15T10:00:00Z"}""");

        // Assert
        Assert.That(output, Is.EqualTo("2026-10-15T10:00:00Z"));
    }

    [TestCase("en-US", "3")]
    [TestCase("nl-NL", "30")]
    public void Transform_Should_Parse_Decimal_Literal_With_Culture_When_Culture_Is_Set(string culture, string expected)
    {
        // Act
        var output = Render("{{ 1.5 | Times: 2 }}", "{}", culture: culture);

        // Assert
        Assert.That(output, Is.EqualTo(expected));
    }

    [Test]
    public void Transform_Should_Convert_String_To_Number_When_Multiplied_With_One_Point_Zero()
    {
        // Act
        var output = Render("{{ content.s | Times: 1.0 | Plus: 1 }}", """{"s":"4"}""");

        // Assert
        Assert.That(output, Is.EqualTo("5"));
    }

    [Test]
    public void Transform_Should_Write_Commas_Between_Items_When_Using_JsonArrayFor()
    {
        // Act
        var output = Render("[{% JSONArrayFor x in content.a %}{{ x }}{% endJSONArrayFor %}]", """{"a":[1,2,3]}""");

        // Assert
        Assert.That(output, Is.EqualTo("[1,2,3]"));
    }

    [Test]
    public void Transform_Should_Ignore_Namespace_And_Case_And_Return_First_Match_When_Input_Is_Xml()
    {
        // Arrange
        const string xml = """<n:Root xmlns:n="urn:x"><n:Item>a</n:Item><n:Item>b</n:Item></n:Root>""";

        // Act
        var output = Render("{{ content.root.item }}", xml, LiquidTransformKind.XmlToText);

        // Assert
        Assert.That(output, Is.EqualTo("a"));
    }

    [Test]
    public void Transform_Should_Select_Elements_By_Name_When_JsonArrayFor_Has_Where()
    {
        // Arrange
        const string xml = "<Root><Name>x</Name><Item>a</Item><Item>b</Item></Root>";

        // Act
        var output = Render("{% JSONArrayFor i in content.Root where Item %}\"{{ i }}\"{% endJSONArrayFor %}", xml, LiquidTransformKind.XmlToText);

        // Assert
        Assert.That(output, Is.EqualTo("\"a\",\"b\""));
    }

    [Test]
    public void Transform_Should_Parse_Output_When_Kind_Is_JsonToJson()
    {
        // Act
        var result = new LiquidTransformer().Transform("""{"n": {{ content.n }}}""", """{"n":1.10}""", LiquidTransformKind.JsonToJson);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Output!["n"]!.ToString(Formatting.None), Is.EqualTo("1.1"));
    }

    [Test]
    public void Transform_Should_Report_Location_When_Output_Is_Not_Valid_Json()
    {
        // Act
        var result = new LiquidTransformer().Transform("{\n  \"a\": {{ content.s }}\n}", """{"s":"text"}""", LiquidTransformKind.JsonToJson);

        // Assert
        var diagnostic = result.Diagnostics.Single(d => d.Code == DiagnosticCodes.InvalidJsonOutput);
        Assert.That(diagnostic.Source, Is.EqualTo(DiagnosticSource.Output));
        Assert.That(diagnostic.Line, Is.EqualTo(2));
    }

    [Test]
    public void Transform_Should_Write_Error_Into_Output_And_Report_It_When_Rendering_Fails()
    {
        // Act
        var result = new LiquidTransformer().Transform("{{ 'abc' | Truncate: 'short' }}", "{}", LiquidTransformKind.JsonToText);

        // Assert
        Assert.That(result.RenderedText, Does.Contain("Liquid error"));
        var diagnostic = result.Diagnostics.Single(d => d.Code == DiagnosticCodes.RenderError);
        Assert.That((diagnostic.Source, diagnostic.Line), Is.EqualTo((DiagnosticSource.Output, 1)));
        Assert.That(diagnostic.Fix, Is.Not.Null, "a hint for the unhelpful engine message");
    }

    [Test]
    public void Transform_Should_Report_Render_Error_At_Filter_Call_When_Cause_Is_Known()
    {
        // Act
        var result = new LiquidTransformer().Transform("Line 1\n{{ content.year | Minus: 1 }}", """{"year":"2026"}""", LiquidTransformKind.JsonToText);

        // Assert
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Not.Contain(DiagnosticCodes.RenderError));
        var diagnostic = result.Diagnostics.Single(d => d.Code == DiagnosticCodes.MathOnText);
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That((diagnostic.Source, diagnostic.Line), Is.EqualTo((DiagnosticSource.Map, 2)));
            Assert.That(diagnostic.OutputLine, Is.EqualTo(2));
            Assert.That(diagnostic.Explanation, Does.Contain("Parameter count mismatch"));
        });
    }

    [Test]
    public void Transform_Should_Report_Include_Once_When_Map_Uses_Include()
    {
        // Act
        var result = new LiquidTransformer().Transform("{% include 'x' %}", "{}", LiquidTransformKind.JsonToText);

        // Assert
        Assert.That(result.RenderedText, Does.Contain("Liquid error"));
        Assert.That(result.Diagnostics.Select(d => d.Code), Is.EqualTo(new[] { DiagnosticCodes.IncludeNotSupported }));
        Assert.That(result.Diagnostics[0].OutputLine, Is.EqualTo(1));
    }

    [Test]
    public void Transform_Should_Report_Missing_Value_At_Path_When_Path_Has_Special_Character()
    {
        // Act
        var result = new LiquidTransformer().Transform("{{ content.item.@code }}", """{"item":{"@code":"A"}}""", LiquidTransformKind.JsonToText);

        // Assert
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Not.Contain(DiagnosticCodes.MissingValue));
        Assert.That(result.Diagnostics.Single(d => d.Code == DiagnosticCodes.SpecialCharacterInPath).Values, Does.Contain("was not found"));
    }

    [Test]
    public void Validate_Should_Report_Parse_Error_At_Location_When_Static_Checks_Find_It()
    {
        // Act
        var diagnostics = new LiquidTransformer().Validate("Order\n{% for line in content.lines %}x");

        // Assert
        var diagnostic = diagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Code, Is.EqualTo(DiagnosticCodes.UnbalancedBlock));
            Assert.That(diagnostic.Line, Is.EqualTo(2));
            Assert.That(diagnostic.Values, Does.Contain("Logic Apps reports"));
        });
    }

    [Test]
    public void Transform_Should_Report_Every_Deviation_When_Several_Are_On_One_Line()
    {
        // Act
        var result = new LiquidTransformer().Transform("{{ content.y | Minus: 1 }}|{{ content.y | Modulo: 2 }}", """{"y":"2026"}""", LiquidTransformKind.JsonToText);

        // Assert
        Assert.That(result.Diagnostics.Count(d => d.Code == DiagnosticCodes.MathOnText), Is.EqualTo(2));
    }

    [TestCase("[{{ content.o | Default: 'x' }}]", """{"o":{"a":1}}""", LiquidTransformKind.JsonToText, "Microsoft.Azure.Function.Common.JObjectDrop")]
    [TestCase("[{{ content.Root | Upcase }}]", "<Root><A>1</A></Root>", LiquidTransformKind.XmlToText, "Microsoft.Azure.Function.Common.XElementDrop")]
    public void Transform_Should_Write_Runtime_Type_Names_When_Error_Names_An_Input_Value(string map, string input, LiquidTransformKind kind, string typeName)
    {
        // Act
        var result = new LiquidTransformer(new LiquidTransformOptions { Lint = false }).Transform(map, input, kind);

        // Assert
        Assert.That(result.RenderedText, Does.Contain(typeName));
        Assert.That(result.Diagnostics.Single(d => d.Code == DiagnosticCodes.RenderError).Values, Does.Contain(typeName));
    }

    [Test]
    public void Transform_Should_Report_Empty_Value_When_Unquoted_Output_Is_Null()
    {
        // Act
        var result = new LiquidTransformer().Transform("{\"x\": {{ content.z }}, \"y\": 1}", """{"z":null}""", LiquidTransformKind.JsonToJson);

        // Assert
        var diagnostic = result.Diagnostics.Single(d => d.Code == DiagnosticCodes.UndefinedInOutput);
        Assert.That(diagnostic.Title, Is.EqualTo("The output contains an empty value"));
    }

    [Test]
    public void Transform_Should_Explain_Invalid_Json_When_Boolean_Went_Through_Text_Filter()
    {
        // Act
        var result = new LiquidTransformer().Transform("{\"a\": {{ content.flag | Default: false }}}", """{"flag":true}""", LiquidTransformKind.JsonToJson);

        // Assert
        Assert.That(result.Diagnostics.Single(d => d.Code == DiagnosticCodes.InvalidJsonOutput).Explanation, Does.Contain("'True'"));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(DiagnosticCodes.BooleanAsText));
    }

    [Test]
    public void Transform_Should_Report_Overflow_As_Error_At_Times_When_It_Writes_Liquid_Error()
    {
        // Act
        var result = new LiquidTransformer().Transform("{{ content.big | Plus: 1 }}\n{{ content.cents | Times: 100 }}", """{"big":3000000000,"cents":25000000}""", LiquidTransformKind.JsonToText);

        // Assert
        var overflows = result.Diagnostics.Where(d => d.Code == DiagnosticCodes.WholeNumberOverflow).OrderBy(d => d.Line).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(overflows, Has.Count.EqualTo(2));
            Assert.That(overflows[0].Severity, Is.EqualTo(DiagnosticSeverity.Warning), "Plus wraps around without an error");
            Assert.That(overflows[1].Severity, Is.EqualTo(DiagnosticSeverity.Error), "Times writes 'Liquid error'");
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Not.Contain(DiagnosticCodes.RenderError));
        });
    }

    [TestCase("{% assign s = content.list | Sort %}{{ s }}", "'s' has no value", "assigned")]
    [TestCase("{{ content.list.0 }}", "'content.list.0' was not found in the input", "[0]")]
    [TestCase("{{ content.list.Size }}", "'content.list.Size' was not found in the input", "'.size'")]
    public void Transform_Should_Explain_Missing_Value_When_Path_Has_A_Known_Mistake(string map, string title, string fix)
    {
        // Act
        var result = new LiquidTransformer(new LiquidTransformOptions { Lint = false }).Transform(map, """{"list":[3,1.5]}""", LiquidTransformKind.JsonToText);

        // Assert
        var diagnostic = result.Diagnostics.Single(d => d.Code == DiagnosticCodes.MissingValue);
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Title, Is.EqualTo(title));
            Assert.That(diagnostic.Fix, Does.Contain(fix));
            Assert.That(diagnostic.Line, Is.EqualTo(1), "located in the map");
        });
    }

    [Test]
    public void Validate_Should_Report_Parse_Error_When_Static_Checks_Are_Off()
    {
        // Act
        var diagnostics = new LiquidTransformer(new LiquidTransformOptions { Lint = false }).Validate("{% for line in content.lines %}x");

        // Assert
        Assert.That(diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.TemplateSyntaxError));
    }

    [Test]
    public void Transform_Should_Report_Warning_When_Value_Is_Missing_In_Input()
    {
        // Act
        var result = new LiquidTransformer().Transform("Name: {{ content.nmae }}", """{"name":"x"}""", LiquidTransformKind.JsonToText);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        var diagnostic = result.Diagnostics.Single();
        Assert.That(diagnostic.Code, Is.EqualTo(DiagnosticCodes.MissingValue));
        Assert.That((diagnostic.Line, diagnostic.Column), Is.EqualTo((1, 10)), "located at the first use in the map");
    }

    [Test]
    public void Transform_Should_Report_Error_When_Input_Is_Invalid()
    {
        // Act
        var json = new LiquidTransformer().Transform("x", "{ broken", LiquidTransformKind.JsonToText);
        var xml = new LiquidTransformer().Transform("x", "<a>", LiquidTransformKind.XmlToText);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(json.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.InvalidJsonInput));
            Assert.That(xml.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.InvalidXmlInput));
        });
    }
}
