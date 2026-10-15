using System.Globalization;
using LogicApps.Liquid.Constants;

namespace LogicApps.Liquid.Models;

/// <summary>
/// Options for <see cref="LiquidTransformer"/>.
/// </summary>
public sealed class LiquidTransformOptions
{
    /// <summary>
    /// The culture to emulate. DotLiquid uses the current culture to parse decimal literals and to format numbers
    /// and dates: on a Dutch machine <c>{{ 1.5 | Times: 2 }}</c> renders <c>30</c>, on en-US it renders <c>3</c>.
    /// Defaults to <see cref="RuntimeConstants.DefaultCultureName"/>.
    /// </summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo(RuntimeConstants.DefaultCultureName);

    /// <summary>
    /// The time zone to emulate. DotLiquid's <c>Date</c> filter converts dates with a time zone to local time and uses the
    /// local clock for <c>now</c>. Defaults to UTC, the time zone of Logic Apps in Azure unless <c>WEBSITE_TIME_ZONE</c> is set.
    /// Null uses the time zone of the machine.
    /// </summary>
    public TimeZoneInfo? TimeZone { get; init; } = TimeZoneInfo.Utc;

    /// <summary>
    /// Run the static checks (linter). Defaults to true.
    /// </summary>
    public bool Lint { get; init; } = true;

    /// <summary>
    /// Detect filters that behave differently from Shopify Liquid, using the actual values while rendering.
    /// This renders the map a second time with observing filters; the output always comes from the normal render. Defaults to true.
    /// </summary>
    public bool DetectDeviations { get; init; } = true;
}
