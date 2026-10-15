using System.Globalization;

namespace LogicApps.Liquid.Runtime;

/// <summary>
/// Temporarily sets the current culture. DotLiquid parses decimal literals (<c>Times: 1.0</c>) and formats
/// numbers and dates with the current culture, so local runs must use the culture of the Azure host.
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUiCulture;

    /// <summary>
    /// Sets the current culture and UI culture until the scope is disposed.
    /// </summary>
    /// <param name="culture">The culture to use.</param>
    public CultureScope(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        _previousCulture = CultureInfo.CurrentCulture;
        _previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// Restores the previous culture.
    /// </summary>
    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
    }
}
