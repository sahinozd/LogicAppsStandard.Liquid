using System.Globalization;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Cli.Reporting;

/// <summary>
/// Writes findings as pipeline annotations: <c>##vso[task.logissue]</c> in Azure DevOps (<c>TF_BUILD</c> is set) and
/// <c>::error</c> / <c>::warning</c> in GitHub Actions (<c>GITHUB_ACTIONS</c> is set). The pipelines hide these lines
/// from the log and show the findings on the run summary and, in GitHub, in the changed files of a pull request.
/// </summary>
internal sealed class CiAnnotationWriter
{
    private readonly TextWriter _writer;
    private readonly bool _azureDevOps;
    private readonly bool _gitHubActions;

    /// <summary>
    /// Creates an annotation writer.
    /// </summary>
    /// <param name="writer">Where to write the annotations.</param>
    /// <param name="azureDevOps">Write Azure DevOps logging commands. Detected from <c>TF_BUILD</c> when null.</param>
    /// <param name="gitHubActions">Write GitHub Actions annotations. Detected from <c>GITHUB_ACTIONS</c> when null.</param>
    public CiAnnotationWriter(TextWriter writer, bool? azureDevOps = null, bool? gitHubActions = null)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _azureDevOps = azureDevOps ?? IsAzureDevOps;
        _gitHubActions = gitHubActions ?? IsGitHubActions;
    }

    /// <summary>True when running in an Azure DevOps pipeline.</summary>
    public static bool IsAzureDevOps => string.Equals(Environment.GetEnvironmentVariable("TF_BUILD"), "True", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when running in a GitHub Actions workflow.</summary>
    public static bool IsGitHubActions => string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Writes the annotations for one finding.
    /// </summary>
    /// <param name="diagnostic">The finding.</param>
    /// <param name="severity">The severity as reported.</param>
    /// <param name="path">The file the finding refers to.</param>
    public void Write(Diagnostic diagnostic, DiagnosticSeverity severity, string path)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        if (severity == DiagnosticSeverity.Info)
        {
            return;
        }

        if (_azureDevOps)
        {
            _writer.WriteLine(AzureDevOpsIssue(diagnostic, severity, path));
        }

        if (_gitHubActions)
        {
            _writer.WriteLine(GitHubAnnotation(diagnostic, severity, path));
        }
    }

    // GitHub Actions workflow command: ::error file=...,line=...,col=...,title=...::message
    private static string GitHubAnnotation(Diagnostic diagnostic, DiagnosticSeverity severity, string path)
    {
        var command = severity == DiagnosticSeverity.Error ? "error" : "warning";
        var properties = $"file={EscapeGitHubProperty(path.Replace('\\', '/'))},title={EscapeGitHubProperty($"{diagnostic.Code}: {diagnostic.Title}")}";
        if (diagnostic.Line is { } line)
        {
            properties += string.Create(CultureInfo.InvariantCulture, $",line={line},col={diagnostic.Column ?? 1}");
        }

        return $"::{command} {properties}::{EscapeGitHubData(diagnostic.Message)}";
    }

    private static string EscapeGitHubData(string value)
    {
        return value.Replace("%", "%25", StringComparison.Ordinal).Replace("\r", "%0D", StringComparison.Ordinal).Replace("\n", "%0A", StringComparison.Ordinal);
    }

    private static string EscapeGitHubProperty(string value)
    {
        return EscapeGitHubData(value).Replace(":", "%3A", StringComparison.Ordinal).Replace(",", "%2C", StringComparison.Ordinal);
    }

    private static string AzureDevOpsIssue(Diagnostic diagnostic, DiagnosticSeverity severity, string path)
    {
        var type = severity == DiagnosticSeverity.Error ? "error" : "warning";
        var properties = $"type={type};sourcepath={path};code={diagnostic.Code}";
        if (diagnostic.Line is { } line)
        {
            properties += string.Create(CultureInfo.InvariantCulture, $";linenumber={line};columnnumber={diagnostic.Column ?? 1}");
        }

        var message = diagnostic.Message.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        return $"##vso[task.logissue {properties}]{diagnostic.Code}: {message}";
    }
}
