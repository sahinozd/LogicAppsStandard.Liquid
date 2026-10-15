using System.CommandLine;
using LogicApps.Liquid.Cli.Enums;
using LogicApps.Liquid.Cli.Helpers;
using LogicApps.Liquid.Cli.Models;

namespace LogicApps.Liquid.Cli.Commands;

/// <summary>
/// The options that control how findings are written: format, verbosity and colors.
/// </summary>
internal sealed class OutputOptionSet
{
    /// <summary>
    /// Creates the options.
    /// </summary>
    public OutputOptionSet()
    {
        Format = new Option<OutputFormat>("--format")
        {
            Description = "How findings are written: pretty (readable, the default, also in pipelines), msbuild (one line per finding, " +
                "for the Visual Studio error list and the VS Code $msCompile problem matcher) or json.",
            DefaultValueFactory = _ => OutputFormat.Pretty,
        };

        Verbosity = new Option<OutputVerbosity>("--verbosity")
        {
            Description = "How much the pretty format shows per finding: minimal (one line), normal (location, values and fix) or detailed (also why).",
            DefaultValueFactory = _ => OutputVerbosity.Normal,
        };

        NoColor = new Option<bool>("--no-color")
        {
            Description = "Write no colors. Colors are also off when the NO_COLOR environment variable is set, or when the output is redirected outside a pipeline.",
        };
    }

    /// <summary>The <c>--format</c> option.</summary>
    public Option<OutputFormat> Format { get; }

    /// <summary>The <c>--verbosity</c> option.</summary>
    public Option<OutputVerbosity> Verbosity { get; }

    /// <summary>The <c>--no-color</c> option.</summary>
    public Option<bool> NoColor { get; }

    /// <summary>
    /// Adds the options to a command.
    /// </summary>
    /// <param name="command">The command.</param>
    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Options.Add(Format);
        command.Options.Add(Verbosity);
        command.Options.Add(NoColor);
    }

    /// <summary>
    /// Reads the settings from the parsed command line.
    /// </summary>
    /// <param name="parseResult">The parsed command line.</param>
    /// <returns>The settings.</returns>
    public OutputSettings Read(ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        return new OutputSettings
        {
            Format = parseResult.GetValue(Format),
            Verbosity = parseResult.GetValue(Verbosity),
            Color = ConsoleColors.Enabled(parseResult.GetValue(NoColor)),
        };
    }
}
