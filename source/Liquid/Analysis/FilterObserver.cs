using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Analysis;

/// <summary>
/// Collects the observations of <see cref="ObservingFilters"/> during one observing render on the current thread.
/// DotLiquid renders synchronously on the calling thread, so the collector is thread-static.
/// </summary>
internal static class FilterObserver
{
    [ThreadStatic]
    private static List<FilterObservation>? _observations;

    [ThreadStatic]
    private static Dictionary<string, (string Rendered, string FilterName)>? _decimalsWithTrailingZeros;

    [ThreadStatic]
    private static bool _jsonInput;

    /// <summary>True when the input of the current render is JSON (XML input has no numbers or booleans, only text).</summary>
    public static bool JsonInput => _jsonInput;

    /// <summary>
    /// Starts collecting observations on the current thread.
    /// </summary>
    /// <param name="jsonInput">True when the input is JSON.</param>
    public static void Start(bool jsonInput)
    {
        _jsonInput = jsonInput;
        _observations = [];
        _decimalsWithTrailingZeros = new Dictionary<string, (string Rendered, string FilterName)>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Stops collecting and returns the observations.
    /// </summary>
    /// <returns>The observations since <see cref="Start"/>.</returns>
    public static IReadOnlyList<FilterObservation> Stop()
    {
        var observations = _observations ?? [];
        _observations = null;
        _decimalsWithTrailingZeros = null;
        return observations;
    }

    /// <summary>
    /// Records an observation when collecting is active.
    /// </summary>
    /// <param name="observation">The observation.</param>
    public static void Report(FilterObservation observation)
    {
        _observations?.Add(observation);
    }

    /// <summary>
    /// Remembers a decimal result whose text form has trailing zeros, for example <c>2026.0</c>.
    /// </summary>
    /// <param name="text">The text form, as a text filter receives it.</param>
    /// <param name="rendered">The form as it renders in output.</param>
    /// <param name="filterName">The filter that produced it.</param>
    public static void RememberTrailingZeros(string text, string rendered, string filterName)
    {
        _decimalsWithTrailingZeros?.TryAdd(text, (rendered, filterName));
    }

    /// <summary>
    /// Finds a remembered decimal result by its text form.
    /// </summary>
    /// <param name="text">Text received by a text filter.</param>
    /// <param name="origin">How it renders and which filter produced it.</param>
    /// <returns>True when the text is a remembered decimal result.</returns>
    public static bool TryGetTrailingZeros(string? text, out (string Rendered, string FilterName) origin)
    {
        origin = default;
        return text is not null && _decimalsWithTrailingZeros is not null && _decimalsWithTrailingZeros.TryGetValue(text, out origin);
    }
}
