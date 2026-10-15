namespace LogicApps.Liquid.Cli.Enums;

/// <summary>
/// Enum representing how findings are written: readable (pretty), one line per finding for editors (msbuild), or a JSON document for tools.
/// </summary>
internal enum OutputFormat
{
    Pretty,
    MsBuild,
    Json
}
