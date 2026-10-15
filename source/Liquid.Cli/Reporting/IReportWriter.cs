using LogicApps.Liquid.Cli.Models;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Cli.Reporting;

/// <summary>
/// Writes what a command found, in one of the output formats.
/// </summary>
internal interface IReportWriter
{
    /// <summary>
    /// Writes the findings for one map.
    /// </summary>
    /// <param name="diagnostics">The findings.</param>
    /// <param name="context">The files the findings refer to.</param>
    /// <param name="warningsAsErrors">Report warnings as errors.</param>
    void WriteDiagnostics(IReadOnlyList<Diagnostic> diagnostics, DiagnosticContext context, bool warningsAsErrors);

    /// <summary>
    /// Writes the outcome of one test case.
    /// </summary>
    /// <param name="result">The outcome.</param>
    void WriteTestResult(TestCaseResult result);

    /// <summary>
    /// Writes an informational message.
    /// </summary>
    /// <param name="message">The message.</param>
    void WriteMessage(string message);

    /// <summary>
    /// Writes the outcome of the command. This is the last call.
    /// </summary>
    /// <param name="summary">The outcome.</param>
    void WriteSummary(RunSummary summary);
}
