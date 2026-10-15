using LogicApps.Liquid.Cli.Helpers;
using LogicApps.Liquid.Enums;
using NUnit.Framework;

namespace LogicApps.Liquid.Tests.Cli;

[TestFixture]
public sealed class KindResolverTests
{
    [TestCase("in.json", null, LiquidTransformKind.JsonToJson)]
    [TestCase("in.json", "out.json", LiquidTransformKind.JsonToJson)]
    [TestCase("in.json", "out.xml", LiquidTransformKind.JsonToText)]
    [TestCase("in.XML", "out.json", LiquidTransformKind.XmlToJson)]
    [TestCase("in.xml", "out.csv", LiquidTransformKind.XmlToText)]
    public void Resolve_Should_Infer_Kind_From_Extensions_When_Kind_Is_Not_Given(string input, string? output, LiquidTransformKind expected)
    {
        // Act
        var kind = KindResolver.Resolve(null, input, output);

        // Assert
        Assert.That(kind, Is.EqualTo(expected));
    }

    [Test]
    public void Resolve_Should_Return_Explicit_Kind_When_Kind_Is_Given()
    {
        // Act
        var kind = KindResolver.Resolve(LiquidTransformKind.JsonToText, "in.json", "out.json");

        // Assert
        Assert.That(kind, Is.EqualTo(LiquidTransformKind.JsonToText));
    }
}
