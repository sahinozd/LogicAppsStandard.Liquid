using LogicApps.Liquid.Cli.Constants;
using LogicApps.Liquid.Cli.Helpers;
using NUnit.Framework;

namespace LogicApps.Liquid.Tests.Cli;

[TestFixture]
public sealed class TestCommandTests
{
    [Test]
    public void Test_Should_Return_Success_When_Sample_Suite_Passes()
    {
        // Arrange
        var suite = Path.Combine(RepositoryRoot(), "samples", "liquid-tests.json");

        // Act
        var exitCode = RunCli("test", "--suite", suite);

        // Assert
        Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
    }

    private static int RunCli(params string[] args)
    {
        var entryPoint = typeof(KindResolver).Assembly.EntryPoint!;
        return (int)entryPoint.Invoke(null, [args])!;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LogicAppsStandard.Liquid.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
