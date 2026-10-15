using LogicApps.Liquid.Cli.Enums;
using LogicApps.Liquid.Cli.Models;
using LogicApps.Liquid.Cli.Reporting;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LogicApps.Liquid.Tests.Cli;

[TestFixture]
public sealed class ReportWriterTests
{
    private const string Map = "Name: {{ content.name | upcase }}\n";

    private static readonly Diagnostic Finding = new(DiagnosticCodes.FilterCasing, DiagnosticSeverity.Error, "Filter 'upcase' is skipped by Logic Apps", DiagnosticSource.Map, 1, 25)
    {
        Explanation = "Logic Apps uses C# names for filters.",
        Fix = "Use 'Upcase'.",
    };

    private static readonly DiagnosticContext Context = new("map.liquid") { MapText = Map };

    private static string Write(IReportWriter writer, StringWriter output, params Diagnostic[] diagnostics)
    {
        writer.WriteDiagnostics(diagnostics, Context, warningsAsErrors: false);
        writer.WriteSummary(new RunSummary { Operation = "Validation", Succeeded = false, Diagnostics = diagnostics, MapCount = 1 });
        return output.ToString();
    }

    private static PrettyReportWriter Pretty(StringWriter output, OutputVerbosity verbosity = OutputVerbosity.Normal, bool color = false)
    {
        return new PrettyReportWriter(output, new AnsiPalette(color), verbosity, new CiAnnotationWriter(output, azureDevOps: false, gitHubActions: false));
    }

    [Test]
    public void Pretty_Should_Show_Excerpt_With_Marker_And_Fix()
    {
        // Arrange
        using var output = new StringWriter();

        // Act
        var text = Write(Pretty(output), output, Finding);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("✗ error"));
            Assert.That(text, Does.Contain("map.liquid:1:25"));
            Assert.That(text, Does.Contain("1 │ Name: {{ content.name | upcase }}"));
            Assert.That(text, Does.Contain("  │                         ^^^^^^"), "the marker is under 'upcase'");
            Assert.That(text, Does.Contain("fix    Use 'Upcase'."));
            Assert.That(text, Does.Not.Contain("C# names"), "the explanation is only shown with --verbosity detailed");
            Assert.That(text, Does.Contain("✗ Validation failed"));
            Assert.That(text, Does.Contain("filters"));
            Assert.That(text, Does.Not.Contain("\u001b["));
        });
    }

    [Test]
    public void Pretty_Should_Show_Explanation_When_Verbosity_Is_Detailed()
    {
        // Arrange
        using var output = new StringWriter();

        // Act
        var text = Write(Pretty(output, OutputVerbosity.Detailed), output, Finding);

        // Assert
        Assert.That(text, Does.Contain("why    Logic Apps uses C# names for filters."));
    }

    [Test]
    public void Pretty_Should_Write_One_Line_Per_Finding_When_Verbosity_Is_Minimal()
    {
        // Arrange
        using var output = new StringWriter();

        // Act
        var text = Write(Pretty(output, OutputVerbosity.Minimal), output, Finding);

        // Assert
        Assert.That(text, Does.Contain("LQ010  map.liquid:1:25  Filter 'upcase' is skipped by Logic Apps"));
        Assert.That(text, Does.Not.Contain("^"));
    }

    [Test]
    public void Pretty_Should_Label_Shopify_Deviation_As_Differs()
    {
        // Arrange
        using var output = new StringWriter();
        var deviation = new Diagnostic(DiagnosticCodes.BankersRounding, DiagnosticSeverity.Warning, "Round rounds halves to the even number", DiagnosticSource.Map, 1, 1);

        // Act
        var text = Write(Pretty(output), output, deviation);

        // Assert
        Assert.That(text, Does.Contain("≠ differs LQ106"));
    }

    [Test]
    public void Pretty_Should_Write_Ansi_Colors_When_Enabled()
    {
        // Arrange
        using var output = new StringWriter();

        // Act
        var text = Write(Pretty(output, color: true), output, Finding);

        // Assert
        Assert.That(text, Does.Contain("\u001b[1;31m✗ error"));
    }

    [Test]
    public void MsBuild_Should_Write_Clickable_Line_With_Whole_Message()
    {
        // Arrange
        using var output = new StringWriter();
        var writer = new MsBuildReportWriter(output, new CiAnnotationWriter(output, azureDevOps: false, gitHubActions: false));

        // Act
        var text = Write(writer, output, Finding);

        // Assert
        Assert.That(text, Does.StartWith("map.liquid(1,25): error LQ010: Filter 'upcase' is skipped by Logic Apps. Logic Apps uses C# names for filters. Fix: Use 'Upcase'."));
        Assert.That(text, Does.Contain("Validation failed: 1 map(s), 1 error(s), 0 warning(s)."));
    }

    [Test]
    public void Json_Should_Write_One_Document_With_Structured_Findings()
    {
        // Arrange
        using var output = new StringWriter();

        // Act
        var document = JObject.Parse(Write(new JsonReportWriter(output), output, Finding));

        // Assert
        var finding = document["files"]![0]!["diagnostics"]![0]!;
        Assert.Multiple(() =>
        {
            Assert.That((bool)document["succeeded"]!, Is.False);
            Assert.That((string?)finding["code"], Is.EqualTo(DiagnosticCodes.FilterCasing));
            Assert.That((string?)finding["severity"], Is.EqualTo("error"));
            Assert.That((string?)finding["category"], Is.EqualTo(nameof(DiagnosticCategory.Filters)));
            Assert.That((string?)finding["fix"], Is.EqualTo("Use 'Upcase'."));
            Assert.That((int?)finding["line"], Is.EqualTo(1));
        });
    }

    [Test]
    public void CiAnnotations_Should_Write_Azure_DevOps_And_GitHub_Commands()
    {
        // Arrange
        using var output = new StringWriter();
        var annotations = new CiAnnotationWriter(output, azureDevOps: true, gitHubActions: true);

        // Act
        annotations.Write(Finding, DiagnosticSeverity.Error, "Maps\\map.liquid");

        // Assert
        var text = output.ToString();
        Assert.That(text, Does.Contain("##vso[task.logissue type=error;sourcepath=Maps\\map.liquid;code=LQ010;linenumber=1;columnnumber=25]LQ010: "));
        Assert.That(text, Does.Contain("::error file=Maps/map.liquid,title=LQ010%3A Filter 'upcase' is skipped by Logic Apps,line=1,col=25::"));
    }

    [Test]
    public void Factory_Should_Create_Writer_For_Format()
    {
        // Arrange
        using var output = new StringWriter();

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(ReportWriterFactory.Create(new OutputSettings(), output), Is.TypeOf<PrettyReportWriter>());
            Assert.That(ReportWriterFactory.Create(new OutputSettings { Format = OutputFormat.MsBuild }, output), Is.TypeOf<MsBuildReportWriter>());
            Assert.That(ReportWriterFactory.Create(new OutputSettings { Format = OutputFormat.Json }, output), Is.TypeOf<JsonReportWriter>());
        });
    }
}
