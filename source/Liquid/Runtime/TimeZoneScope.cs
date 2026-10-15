using System.Reflection;

namespace LogicApps.Liquid.Runtime;

/// <summary>
/// Temporarily sets the local time zone of the process. DotLiquid's <c>Date</c> filter converts dates with a time zone
/// (<c>Z</c>, <c>+02:00</c>) to local time and uses the local clock for <c>now</c>, so local runs must use the time zone
/// of the Logic Apps host: UTC, unless the app setting <c>WEBSITE_TIME_ZONE</c> is set.
/// </summary>
/// <remarks>
/// .NET has no public API to change the local time zone, so this sets the time zone cache of <see cref="TimeZoneInfo"/>.
/// The time zone is process-wide: do not render maps with different time zones in parallel.
/// </remarks>
public sealed class TimeZoneScope : IDisposable
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static readonly FieldInfo? CachedDataField = typeof(TimeZoneInfo).GetField("s_cachedData", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly Type? OffsetAndRuleType = typeof(TimeZoneInfo).Assembly.GetType("System.TimeZoneInfo+OffsetAndRule");

    /// <summary>
    /// Sets the local time zone until the scope is disposed.
    /// </summary>
    /// <param name="timeZone">The time zone to use as local time zone.</param>
    /// <exception cref="InvalidOperationException">The .NET runtime does not allow the time zone to be changed.</exception>
    public TimeZoneScope(TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        TimeZoneInfo.ClearCachedData();
        var cachedData = CachedDataField?.GetValue(null);
        var localTimeZoneField = cachedData?.GetType().GetField("_localTimeZone", InstanceMembers);
        var oneYearField = cachedData?.GetType().GetField("_oneYearLocalFromUtc", InstanceMembers);
        var offsetAndRuleConstructor = OffsetAndRuleType?.GetConstructors(InstanceMembers).FirstOrDefault(c => c.GetParameters().Length == 3);
        if (cachedData is null || localTimeZoneField is null || oneYearField is null || offsetAndRuleConstructor is null)
        {
            throw new InvalidOperationException("The local time zone cannot be changed on this .NET runtime.");
        }

        // DateTime.Parse uses the local time zone; DateTime.Now uses a separate cache for the current year.
        var year = DateTime.UtcNow.Year;
        var rule = timeZone.GetAdjustmentRules().FirstOrDefault(r => r.DateStart.Year <= year && r.DateEnd.Year >= year);
        localTimeZoneField.SetValue(cachedData, timeZone);
        oneYearField.SetValue(cachedData, offsetAndRuleConstructor.Invoke([year, timeZone.BaseUtcOffset, rule]));
    }

    /// <summary>
    /// Restores the time zone of the machine.
    /// </summary>
    public void Dispose()
    {
        TimeZoneInfo.ClearCachedData();
    }
}
