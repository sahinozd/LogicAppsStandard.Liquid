using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using DotLiquid;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Analysis;

/// <summary>
/// Helpers for the observing loop tags. The location of a loop is passed through its markup as a
/// <c>__site:line_column</c> marker, because DotLiquid creates tags without constructor arguments.
/// </summary>
internal static class ObservedLoopMarkup
{
    private static readonly Regex _markerPattern = new(@"\s__site:(?<line>\d+)_(?<column>\d+)");
    private static readonly Regex _collectionPattern = new(@"^\s*\w+\s+in\s+(?<collection>[^\s]+)");

    /// <summary>The name of the observing <c>for</c> tag.</summary>
    public const string ForTagName = "for__observed";

    /// <summary>The name of the observing <c>JSONArrayFor</c> tag.</summary>
    public const string JsonArrayForTagName = RuntimeConstants.JsonArrayForTagName + "__observed";

    private static readonly Lock _syncRoot = new();
    private static bool _registered;

    /// <summary>
    /// Registers the observing loop tags. Their names cannot occur in a real map, so the normal render is not affected.
    /// </summary>
    public static void EnsureRegistered()
    {
        lock (_syncRoot)
        {
            if (_registered)
            {
                return;
            }

            Template.RegisterTag<ObservingFor>(ForTagName);
            Template.RegisterTag<ObservingJsonArrayFor>(JsonArrayForTagName);
            _registered = true;
        }
    }

    /// <summary>
    /// Creates the marker that is appended to the markup of an observed loop.
    /// </summary>
    /// <param name="line">1-based line of the loop.</param>
    /// <param name="column">1-based column of the loop.</param>
    /// <returns>The marker, with a leading space.</returns>
    public static string Marker(int line, int column)
    {
        return string.Create(CultureInfo.InvariantCulture, $" __site:{line}_{column}");
    }

    /// <summary>
    /// Removes the marker from the markup.
    /// </summary>
    /// <param name="markup">The markup with marker.</param>
    /// <returns>The original markup, the location and the collection expression.</returns>
    public static (string Markup, int Line, int Column, string Collection) Parse(string markup)
    {
        var marker = _markerPattern.Match(markup);
        var line = marker.Success ? int.Parse(marker.Groups["line"].Value, CultureInfo.InvariantCulture) : 0;
        var column = marker.Success ? int.Parse(marker.Groups["column"].Value, CultureInfo.InvariantCulture) : 0;
        var clean = marker.Success ? markup.Remove(marker.Index, marker.Length) : markup;
        var collection = _collectionPattern.Match(clean);
        return (clean, line, column, collection.Success ? collection.Groups["collection"].Value : string.Empty);
    }

    /// <summary>
    /// Reports a loop over a single object, which renders nothing.
    /// </summary>
    /// <param name="context">The render context.</param>
    /// <param name="tagName">The tag name as written in the map.</param>
    /// <param name="collection">The collection expression.</param>
    /// <param name="line">1-based line of the loop.</param>
    /// <param name="column">1-based column of the loop.</param>
    public static void InspectCollection(Context context, string tagName, string collection, int line, int column)
    {
        if (collection.Length == 0 || context[collection] is not IIndexable value || value is IEnumerable)
        {
            return;
        }

        FilterObserver.Report(new FilterObservation(DiagnosticCodes.LoopOverSingleObject,
            $"{tagName} over '{collection}' renders nothing: it is a single object, not an array. When XML is converted to JSON (json(xml(...))), a repeating element " +
            "that occurs once becomes an object. Wrap it first: '{% assign items = " + collection + " | Compact %}' gives a list for both cases (Uniq also works, but removes duplicate values).",
            line, column));
    }
}
