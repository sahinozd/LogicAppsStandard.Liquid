using System.CommandLine;
using LogicApps.Liquid.Cli.Models;
using LogicApps.Liquid.Constants;

namespace LogicApps.Liquid.Cli.Commands;

/// <summary>
/// The options that control how the Logic Apps engine is emulated: culture, time zone and which checks run.
/// </summary>
internal sealed class EngineOptionSet
{
    /// <summary>
    /// Creates the options.
    /// </summary>
    /// <param name="cultureDefault">The default culture, or null when the culture comes from elsewhere (a test suite).</param>
    public EngineOptionSet(string? cultureDefault = RuntimeConstants.DefaultCultureName)
    {
        Culture = new Option<string?>("--culture")
        {
            Description = "The culture of the Logic Apps host. DotLiquid uses it to parse decimal literals and text, and to format numbers and dates.",
        };
        if (cultureDefault is not null)
        {
            Culture.DefaultValueFactory = _ => cultureDefault;
        }

        TimeZone = new Option<string?>("--time-zone")
        {
            Description = "The time zone of the Logic Apps host: UTC in Azure, unless the app setting WEBSITE_TIME_ZONE is set. Accepts Windows and IANA ids. " +
                "'local' uses the time zone of this machine.",
        };

        NoLint = new Option<bool>("--no-lint")
        {
            Description = "Skip the static checks and only report what the Logic Apps engine itself reports.",
        };

        NoDeviations = new Option<bool>("--no-deviations")
        {
            Description = "Skip the detection of filters that behave differently from Shopify Liquid (LQ1xx warnings).",
        };
    }

    /// <summary>The <c>--culture</c> option.</summary>
    public Option<string?> Culture { get; }

    /// <summary>The <c>--time-zone</c> option.</summary>
    public Option<string?> TimeZone { get; }

    /// <summary>The <c>--no-lint</c> option.</summary>
    public Option<bool> NoLint { get; }

    /// <summary>The <c>--no-deviations</c> option.</summary>
    public Option<bool> NoDeviations { get; }

    /// <summary>
    /// Adds the options to a command.
    /// </summary>
    /// <param name="command">The command.</param>
    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Options.Add(Culture);
        command.Options.Add(TimeZone);
        command.Options.Add(NoLint);
        command.Options.Add(NoDeviations);
    }

    /// <summary>
    /// Reads the settings from the parsed command line.
    /// </summary>
    /// <param name="parseResult">The parsed command line.</param>
    /// <param name="cultureFallback">Culture when the option is not given.</param>
    /// <param name="timeZoneFallback">Time zone when the option is not given.</param>
    /// <returns>The settings.</returns>
    public EngineSettings Read(ParseResult parseResult, string? cultureFallback = null, string? timeZoneFallback = null)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        return new EngineSettings
        {
            CultureName = parseResult.GetValue(Culture) ?? cultureFallback ?? RuntimeConstants.DefaultCultureName,
            TimeZoneId = parseResult.GetValue(TimeZone) ?? timeZoneFallback ?? RuntimeConstants.DefaultTimeZoneId,
            NoLint = parseResult.GetValue(NoLint),
            NoDeviations = parseResult.GetValue(NoDeviations),
        };
    }
}
