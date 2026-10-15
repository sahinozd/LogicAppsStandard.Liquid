namespace LogicApps.Liquid.Enums;

/// <summary>
/// Enum representing the file a diagnostic refers to: the Liquid map, the input message or the rendered output.
/// </summary>
public enum DiagnosticSource
{
    Map,
    Input,
    Output
}
