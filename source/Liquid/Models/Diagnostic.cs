using System.Globalization;
using System.Text;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;

namespace LogicApps.Liquid.Models;

/// <summary>
/// A validation finding. Line and column are 1-based and refer to <see cref="Source"/>.
/// A finding consists of a short <see cref="Title"/>, and optionally the actual <see cref="Values"/>,
/// an <see cref="Explanation"/> and a <see cref="Fix"/>. <see cref="Message"/> combines them into one text.
/// </summary>
public sealed record Diagnostic
{
    /// <summary>
    /// Creates a diagnostic.
    /// </summary>
    /// <param name="code">Stable identifier, see <see cref="DiagnosticCodes"/>.</param>
    /// <param name="severity">The severity.</param>
    /// <param name="title">Short description of the problem.</param>
    /// <param name="source">The file the finding refers to.</param>
    /// <param name="line">1-based line number, if known.</param>
    /// <param name="column">1-based column number, if known.</param>
    public Diagnostic(string code, DiagnosticSeverity severity, string title, DiagnosticSource source = DiagnosticSource.Map, int? line = null, int? column = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Severity = severity;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Source = source;
        Line = line;
        Column = column;
    }

    /// <summary>Stable identifier, see <see cref="DiagnosticCodes"/>.</summary>
    public string Code { get; }

    /// <summary>The severity.</summary>
    public DiagnosticSeverity Severity { get; init; }

    /// <summary>Short description of the problem.</summary>
    public string Title { get; }

    /// <summary>The actual values that caused the finding, for example <c>'2026' | Plus: 1 → 20261 (Shopify Liquid: 2027)</c>.</summary>
    public string? Values { get; init; }

    /// <summary>Why this happens in Logic Apps.</summary>
    public string? Explanation { get; init; }

    /// <summary>What to write instead.</summary>
    public string? Fix { get; init; }

    /// <summary>The file the finding refers to.</summary>
    public DiagnosticSource Source { get; init; }

    /// <summary>1-based line number, if known.</summary>
    public int? Line { get; init; }

    /// <summary>1-based column number, if known.</summary>
    public int? Column { get; init; }

    /// <summary>1-based line in the rendered output where the effect is visible, for a finding located in the map.</summary>
    public int? OutputLine { get; init; }

    /// <summary>True for the codes LQ100 and up: DotLiquid behaves differently from Shopify Liquid for the observed values.</summary>
    public bool IsShopifyDeviation => Code.StartsWith("LQ1", StringComparison.Ordinal) && Code.Length == 5;

    /// <summary>All parts in one text: title, values, explanation and fix.</summary>
    public string Message
    {
        get
        {
            var builder = new StringBuilder(Title.TrimEnd('.')).Append('.');
            if (!string.IsNullOrEmpty(Values))
            {
                builder.Append(' ').Append(Values.TrimEnd('.')).Append('.');
            }

            if (!string.IsNullOrEmpty(Explanation))
            {
                builder.Append(' ').Append(Explanation);
            }

            if (!string.IsNullOrEmpty(Fix))
            {
                builder.Append(" Fix: ").Append(Fix);
            }

            return builder.ToString();
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var location = Line is null
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"({Line},{Column ?? 1})");
        return $"{Source}{location}: {Severity.ToString().ToUpperInvariant()} {Code}: {Message}";
    }
}
