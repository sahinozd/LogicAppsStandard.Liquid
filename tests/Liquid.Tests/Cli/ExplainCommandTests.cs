using System.Reflection;
using LogicApps.Liquid.Cli.Constants;
using LogicApps.Liquid.Cli.Helpers;
using LogicApps.Liquid.Constants;
using NUnit.Framework;

namespace LogicApps.Liquid.Tests.Cli;

[TestFixture]
public sealed class ExplainCommandTests
{
    private static int RunCli(params string[] args)
    {
        var entryPoint = typeof(KindResolver).Assembly.EntryPoint!;
        return (int)entryPoint.Invoke(null, [args])!;
    }

    [Test]
    public void Catalog_Should_Describe_Every_Diagnostic_Code()
    {
        // Arrange
        var codes = typeof(DiagnosticCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetValue(null)!);

        // Act & Assert
        Assert.That(codes.Where(code => DiagnosticCatalog.Find(code) is null), Is.Empty);
    }

    [TestCase("LQ102")]
    [TestCase("lq010")]
    public void Explain_Should_Return_Success_When_Code_Is_Known(string code)
    {
        // Act & Assert
        Assert.That(RunCli("explain", code, "--no-color"), Is.EqualTo(ExitCodes.Success));
    }

    [Test]
    public void Explain_Should_Return_Success_When_Listing_All_Codes()
    {
        // Act & Assert
        Assert.That(RunCli("explain", "--no-color"), Is.EqualTo(ExitCodes.Success));
    }

    [Test]
    public void Explain_Should_Return_Usage_Error_When_Code_Is_Unknown()
    {
        // Act & Assert
        Assert.That(RunCli("explain", "LQ999"), Is.EqualTo(ExitCodes.UsageError));
    }
}
