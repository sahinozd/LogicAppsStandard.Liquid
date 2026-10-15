namespace LogicApps.Liquid.Cli.Enums;

/// <summary>
/// Enum representing how much the pretty format shows per finding: one line, the location with values and fix, or also the explanation.
/// </summary>
internal enum OutputVerbosity
{
    Minimal,
    Normal,
    Detailed
}
