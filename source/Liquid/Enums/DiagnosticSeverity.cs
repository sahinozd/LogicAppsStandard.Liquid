namespace LogicApps.Liquid.Enums;

/// <summary>
/// Enum representing the severity of a diagnostic.
/// Info is informational, Warning is probably a mistake while Logic Apps would still run the map,
/// Error means Logic Apps fails or silently produces a wrong result.
/// </summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}
