namespace LogicApps.Liquid.Cli.Models;

/// <summary>
/// How the Logic Apps engine is emulated and which checks run, as given on the command line.
/// </summary>
internal sealed record EngineSettings
{
    /// <summary>The culture of the Logic Apps host.</summary>
    public required string CultureName { get; init; }

    /// <summary>The time zone of the Logic Apps host (Windows or IANA id).</summary>
    public required string TimeZoneId { get; init; }

    /// <summary>Skip the static checks.</summary>
    public bool NoLint { get; init; }

    /// <summary>Skip the detection of filters that behave differently from Shopify Liquid.</summary>
    public bool NoDeviations { get; init; }
}
