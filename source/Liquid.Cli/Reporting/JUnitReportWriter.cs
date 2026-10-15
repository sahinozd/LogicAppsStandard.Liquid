using System.Globalization;
using System.Xml.Linq;
using LogicApps.Liquid.Cli.Models;

namespace LogicApps.Liquid.Cli.Reporting;

/// <summary>
/// Writes test results as JUnit XML, which the Azure DevOps <c>PublishTestResults@2</c> task understands.
/// </summary>
internal static class JUnitReportWriter
{
    /// <summary>
    /// Writes the report.
    /// </summary>
    /// <param name="path">The report file.</param>
    /// <param name="suiteName">The name of the test suite.</param>
    /// <param name="results">The test results.</param>
    public static void Write(string path, string suiteName, IReadOnlyCollection<TestCaseResult> results)
    {
        var totalSeconds = results.Sum(r => r.Duration.TotalSeconds);
        var suite = new XElement("testsuite",
            new XAttribute("name", suiteName),
            new XAttribute("tests", results.Count),
            new XAttribute("failures", results.Count(r => !r.Passed)),
            new XAttribute("errors", 0),
            new XAttribute("time", Seconds(totalSeconds)),
            new XAttribute("timestamp", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)),
            results.Select(ToTestCase));

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement("testsuites",
                new XAttribute("tests", results.Count),
                new XAttribute("failures", results.Count(r => !r.Passed)),
                new XAttribute("time", Seconds(totalSeconds)),
                suite));

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        document.Save(path);
    }

    private static XElement ToTestCase(TestCaseResult result)
    {
        var testCase = new XElement("testcase",
            new XAttribute("name", result.Name),
            new XAttribute("classname", Path.GetFileName(result.MapPath)),
            new XAttribute("time", Seconds(result.Duration.TotalSeconds)));

        if (!result.Passed)
        {
            testCase.Add(new XElement("failure",
                new XAttribute("message", result.Failures[0]),
                string.Join(Environment.NewLine, result.Failures)));
        }

        return testCase;
    }

    private static string Seconds(double seconds)
    {
        return seconds.ToString("0.000", CultureInfo.InvariantCulture);
    }
}
