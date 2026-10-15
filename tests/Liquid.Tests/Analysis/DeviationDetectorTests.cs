using System.Globalization;
using LogicApps.Liquid.Analysis;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;
using LogicApps.Liquid.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LogicApps.Liquid.Tests.Analysis;

/// <summary>
/// Every DotLiquid deviation from Shopify Liquid is reported as a warning, with the location of the filter call.
/// The values in the comments are the outputs of the real Logic Apps engine.
/// </summary>
[TestFixture]
public sealed class DeviationDetectorTests
{
    private const string Input = """{"year":"2026","amount":"125,50","flag":false,"blank":"  ","items":[{"n":3},{"n":1},{"n":2}],"words":["banana","Apple","apple"],"stamp":"2026-10-15T23:30:00Z","local":"03/04/2026"}""";

    private static IEnumerable<TestCaseData> Deviations()
    {
        yield return Case("{{ content.year | Plus: 10 }}", DiagnosticCodes.PlusJoinsText, "202610");
        yield return Case("{{ content.year | Date: 'yyyy' | Plus: 10 }}", DiagnosticCodes.PlusJoinsText, "202610");
        yield return Case("{{ content.year | Times: 3 }}", DiagnosticCodes.TimesRepeatsText, "202620262026");
        yield return Case("{{ content.year | Minus: 1 }}", DiagnosticCodes.MathOnText, "Parameter count mismatch");
        yield return Case("{{ content.amount | Times: 1.0 }}", DiagnosticCodes.CultureNumberParsing, "12550");
        yield return Case("{{ content.year | Times: 1.0 | Append: '-12-31' }}", DiagnosticCodes.DecimalTrailingZeros, "2026.0-12-31");
        yield return Case("{{ 2.5 | Round }}", DiagnosticCodes.BankersRounding, "2");
        yield return Case("{{ 'abc' | Round }}", DiagnosticCodes.RoundNotANumber, string.Empty);
        yield return Case("{{ -7 | DividedBy: 2 }}", DiagnosticCodes.NegativeIntegerDivision, "-3");
        yield return Case("{{ -7 | Modulo: 3 }}", DiagnosticCodes.NegativeModulo, "-1");
        yield return Case("{{ 1.5 | DividedBy: 0 }}", DiagnosticCodes.DivisionByZero, "∞");
        yield return Case("{{ content.flag | Default: 'x' }}", DiagnosticCodes.DefaultDiffers, "False");
        yield return Case("[{{ content.blank | Default: 'x' }}]", DiagnosticCodes.DefaultDiffers, "[x]");
        yield return Case("{{ '1.234.5' | Replace: '.', ',' }}", DiagnosticCodes.ReplaceIsRegex, ",,,,,,,");
        yield return Case("{{ 'a.b' | RemoveFirst: '.' }}", DiagnosticCodes.ReplaceIsRegex, ".b");
        yield return Case("{{ 'a,,b' | Split: ',' | Size }}", DiagnosticCodes.SplitDiffers, "2");
        yield return Case("[{{ content.missing | Append: 'x' }}]", DiagnosticCodes.AppendToNil, "[]");
        yield return Case("{{ 'hELLO wORLD' | Capitalize }}", DiagnosticCodes.CapitalizeDiffers, "HELLO WORLD");
        yield return Case("{{ 'café' | Escape }}", DiagnosticCodes.EscapeDiffers, "caf&#233;");
        yield return Case("{% assign s = content.items | Sort: 'n' %}{% for i in s %}{{ i.n }}{% endfor %}", DiagnosticCodes.SortDiffers, "312");
        yield return Case("{{ content.words | Sort | Join: ',' }}", DiagnosticCodes.SortDiffers, "apple,Apple,banana");
        yield return Case("{{ content.stamp | Date: 'yyyy-MM-dd HH:mm' }}", DiagnosticCodes.DateTimeZoneConversion, "2026-10-15 23:30");
        yield return Case("{{ content.local | Date: 'yyyy-MM-dd' }}", DiagnosticCodes.DateCultureParsing, "2026-03-04");
        yield return Case("{{ content.year | Date: '%Y' }}", DiagnosticCodes.DateStrftimeFormat, null);
        yield return Case("{{ 'not a date' | Date: 'yyyy' }}", DiagnosticCodes.DateNotParsed, "not a date");
        yield return Case("{{ 'now' | Date: 'yyyy' }}", DiagnosticCodes.DateNow, null);
        yield return Case("{{ 'abc' | Abs }}", DiagnosticCodes.NumberFilterOnText, "0");
        yield return Case("{{ 12345 | Size }}", DiagnosticCodes.SizeOfNumber, "0");
    }

    private static TestCaseData Case(string map, string code, string? output)
    {
        return new TestCaseData(map, code, output).SetName($"Detect_Should_Report_{code}_When_Map_Is {map}");
    }

    private static LiquidTransformResult Transform(string map, string culture = "en-US")
    {
        var transformer = new LiquidTransformer(new LiquidTransformOptions { Culture = CultureInfo.GetCultureInfo(culture), Lint = false });
        return transformer.Transform(map, Input, LiquidTransformKind.JsonToText);
    }

    [TestCaseSource(nameof(Deviations))]
    public void Detect_Should_Report_Deviation_When_Values_Hit_It(string map, string code, string? output)
    {
        // Act
        var result = Transform(map);

        // Assert
        var deviation = result.Diagnostics.FirstOrDefault(d => d.Code == code);
        Assert.That(deviation, Is.Not.Null, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.That(deviation!.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        Assert.That(deviation.Line, Is.EqualTo(1));
        if (output is not null)
        {
            Assert.That(result.RenderedText, Does.Contain(output));
        }
    }

    [TestCaseSource(nameof(Deviations))]
    public void RenderObserved_Should_Produce_Same_Output_As_Normal_Render_When_Filters_Are_Observed(string map, string code, string? output)
    {
        ArgumentNullException.ThrowIfNull(map);

        // Arrange
        var normal = Transform(map).RenderedText;

        // Act
        string? observed;
        using (new CultureScope(CultureInfo.GetCultureInfo("en-US")))
        using (new TimeZoneScope(TimeZoneInfo.Utc))
        {
            (observed, _) = DeviationDetector.RenderObserved(map, new JsonInputDrop(RuntimeJson.Parse(Input)));
        }

        // Assert
        if (map.Contains("'now'", StringComparison.Ordinal))
        {
            Assert.Pass("The clock moves between the two renders.");
        }

        Assert.That(observed, Is.EqualTo(normal));
    }

    [TestCase("{% for x in content.single %}<{{ x.id }}>{% endfor %}", "for")]
    [TestCase("[{% JSONArrayFor x in content.single %}{{ x.id }}{% endJSONArrayFor %}]", "JSONArrayFor")]
    public void Detect_Should_Report_Loop_Over_Single_Object_When_Collection_Is_An_Object(string map, string tag)
    {
        // Arrange
        var transformer = new LiquidTransformer(new LiquidTransformOptions { Lint = false });

        // Act
        var result = transformer.Transform(map, """{"single":{"id":"A"}}""", LiquidTransformKind.JsonToText);

        // Assert
        var deviation = result.Diagnostics.Single(d => d.Code == DiagnosticCodes.LoopOverSingleObject);
        Assert.That(deviation.Message, Does.StartWith(tag));
        Assert.That(deviation.Message, Does.Contain("Compact"));
    }

    [Test]
    public void Detect_Should_Not_Report_Loop_When_Single_Object_Is_Wrapped_With_Compact()
    {
        // Arrange
        var transformer = new LiquidTransformer(new LiquidTransformOptions { Lint = false });

        // Act
        var result = transformer.Transform("{% assign items = content.single | Compact %}{% for x in items %}<{{ x.id }}>{% endfor %}", """{"single":{"id":"A"}}""", LiquidTransformKind.JsonToText);

        // Assert
        Assert.That(result.RenderedText, Is.EqualTo("<A>"));
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [TestCase("[{{ content | Json }}]", "prints nothing")]
    [TestCase("[{{ content.obj }}]", "prints nothing")]
    [TestCase("[{{ content.objs }}]", "internal type name")]
    public void Detect_Should_Report_Object_In_Output_When_Output_Is_Not_A_Value(string map, string effect)
    {
        // Arrange
        var transformer = new LiquidTransformer(new LiquidTransformOptions { Lint = false });

        // Act
        var result = transformer.Transform(map, """{"obj":{"a":1},"objs":[{"a":1},{"a":2}]}""", LiquidTransformKind.JsonToText);

        // Assert
        var deviation = result.Diagnostics.Single(d => d.Code == DiagnosticCodes.ObjectInOutput);
        Assert.That(deviation.Message, Does.Contain(effect));
    }

    [Test]
    public void Detect_Should_Not_Report_Object_In_Output_When_Output_Is_A_Value_Or_Plain_Array()
    {
        // Arrange
        var transformer = new LiquidTransformer(new LiquidTransformOptions { Lint = false });

        // Act
        var result = transformer.Transform("{{ content.obj.a }}|{{ content.arr }}|{{- content.obj.a -}}", """{"obj":{"a":1},"arr":[1,2]}""", LiquidTransformKind.JsonToText);

        // Assert
        Assert.That(result.RenderedText, Is.EqualTo("1|12|1"));
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void Detect_Should_Report_Removed_Duplicates_When_Uniq_Drops_Values()
    {
        // Arrange
        var transformer = new LiquidTransformer(new LiquidTransformOptions { Lint = false });

        // Act
        var result = transformer.Transform("{{ content.a | Uniq | Join: ',' }}", """{"a":["x","x","y"]}""", LiquidTransformKind.JsonToText);

        // Assert
        Assert.That(result.RenderedText, Is.EqualTo("x,y"));
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.UniqRemovedDuplicates));
    }

    [Test]
    public void Transform_Should_Report_Undefined_Items_When_JsonArrayFor_Skips_Items()
    {
        // Arrange
        var transformer = new LiquidTransformer(new LiquidTransformOptions { Lint = false });

        // Act
        var result = transformer.Transform("{\"items\":[{% JSONArrayFor n in content.a %}{% if n > 1 %}{{ n }}{% endif %}{% endJSONArrayFor %}]}", """{"a":[1,2,3]}""", LiquidTransformKind.JsonToJson);

        // Assert
        var diagnostic = result.Diagnostics.Single(d => d.Code == DiagnosticCodes.UndefinedInOutput);
        Assert.That(diagnostic.Message, Does.Contain("items[0]"));
    }

    [Test]
    public void Detect_Should_Report_Location_Of_Each_Call_When_Filter_Is_Used_Twice()
    {
        // Act
        var result = Transform("{{ 3.5 | Round }}\n{{ 2.5 | Round }}");

        // Assert
        var deviation = result.Diagnostics.Single(d => d.Code == DiagnosticCodes.BankersRounding);
        Assert.That((deviation.Line, deviation.Column), Is.EqualTo((2, 10)));
    }

    [Test]
    public void Detect_Should_Not_Report_Trailing_Zeros_When_Decimal_Is_Rendered_Directly()
    {
        // Act
        var result = Transform("{{ content.year | Times: 1.0 }}");

        // Assert
        Assert.That(result.RenderedText, Is.EqualTo("2026"));
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void Detect_Should_Not_Report_When_Article_Workaround_Is_Used()
    {
        // Act
        var result = Transform("{{ content.year | Times: 1.0 | Round | Plus: 10 | Append: '-12-31' }}");

        // Assert
        Assert.That(result.RenderedText, Is.EqualTo("2036-12-31"));
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void Detect_Should_Report_Culture_Parsing_When_Host_Uses_Comma_As_Decimal_Separator()
    {
        // Act
        var result = Transform("{{ '1.5' | Times: 2.0 }}", "nl-NL");

        // Assert
        Assert.That(result.RenderedText, Is.EqualTo("300"));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(DiagnosticCodes.CultureNumberParsing));
    }

    [Test]
    public void Transform_Should_Not_Report_Deviations_When_Detection_Is_Off()
    {
        // Arrange
        var transformer = new LiquidTransformer(new LiquidTransformOptions { Lint = false, DetectDeviations = false });

        // Act
        var result = transformer.Transform("{{ 2.5 | Round }}", "{}", LiquidTransformKind.JsonToText);

        // Assert
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [TestCase("UTC", "2026-10-15 23:30")]
    [TestCase("W. Europe Standard Time", "2026-10-16 01:30")]
    public void Transform_Should_Convert_Dates_To_Emulated_Time_Zone_When_Time_Zone_Is_Set(string timeZoneId, string expected)
    {
        // Arrange
        var transformer = new LiquidTransformer(new LiquidTransformOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId), DetectDeviations = false });

        // Act
        var result = transformer.Transform("{{ content.stamp | Date: 'yyyy-MM-dd HH:mm' }}", Input, LiquidTransformKind.JsonToText);

        // Assert
        Assert.That(result.RenderedText, Is.EqualTo(expected));
    }
}
