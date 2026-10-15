using System.Globalization;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;

namespace LogicApps.Liquid.Models;

/// <summary>
/// A validation finding. Line and column are 1-based and refer to <see cref="Source"/>.
/// </summary>
public sealed record Diagnostic
{
    /// <summary>
    /// Creates a diagnostic.
    /// </summary>
    /// <param name="code">Stable identifier, see <see cref="DiagnosticCodes"/>.</param>
    /// <param name="severity">The severity.</param>
    /// <param name="message">Human readable message.</param>
    /// <param name="source">The file the finding refers to.</param>
    /// <param name="line">1-based line number, if known.</param>
    /// <param name="column">1-based column number, if known.</param>
    public Diagnostic(string code, DiagnosticSeverity severity, string message, DiagnosticSource source = DiagnosticSource.Map, int? line = null, int? column = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Severity = severity;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Source = source;
        Line = line;
        Column = column;
    }

    /// <summary>Stable identifier, see <see cref="DiagnosticCodes"/>.</summary>
    public string Code { get; }

    /// <summary>The severity.</summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>Human readable message.</summary>
    public string Message { get; }

    /// <summary>The file the finding refers to.</summary>
    public DiagnosticSource Source { get; }

    /// <summary>1-based line number, if known.</summary>
    public int? Line { get; }

    /// <summary>1-based column number, if known.</summary>
    public int? Column { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var location = Line is null
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"({Line},{Column ?? 1})");
        return $"{Source}{location}: {Severity.ToString().ToUpperInvariant()} {Code}: {Message}";
    }
}
