using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DotLiquid;
using DotLiquid.Exceptions;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Linting;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Analysis;

/// <summary>
/// Renders a map a second time with <see cref="ObservingFilters"/> and turns the observations into warnings.
/// The observing render is only used for analysis; the output of the transform comes from the normal render.
/// </summary>
/// <remarks>
/// To know which filter call in the map caused an observation, every observed filter call gets its own name in a copy
/// of the map (<c>| Round</c> becomes <c>| Round__s4</c>), and each name is bound to an <see cref="ObservingFilters"/>
/// instance that knows the line and column of that call. Filters that are not observed keep their name and behaviour.
/// </remarks>
public sealed class DeviationDetector : IDeviationDetector
{
    private static readonly Regex _stringPattern = new(@"""[^""]*""|'[^']*'");
    private static readonly Regex _filterNamePattern = new(@"\|\s*(?<name>\w+)");

    private const int MaxValuesPerLocation = 3;

    private static readonly Dictionary<string, MethodInfo> _observedFilters = typeof(ObservingFilters)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .ToDictionary(m => m.Name, StringComparer.Ordinal);

    /// <summary>
    /// Renders the map with observing filters and reports every deviation as a warning.
    /// </summary>
    /// <param name="map">The Liquid map. It must be parseable.</param>
    /// <param name="content">The input, wrapped the same way as for the normal render.</param>
    /// <returns>The warnings, with the location of the filter call in the map.</returns>
    public IReadOnlyList<Diagnostic> Detect(string map, object content)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(content);

        var (_, observations) = RenderObserved(map, content);
        return ToDiagnostics(observations);
    }

    /// <summary>
    /// Renders the map with observing filters. The rendered text must be identical to the normal render; tests verify this.
    /// </summary>
    /// <param name="map">The Liquid map. It must be parseable.</param>
    /// <param name="content">The input, wrapped the same way as for the normal render.</param>
    /// <returns>The rendered text (null when rendering failed) and the observations.</returns>
    internal static (string? Rendered, IReadOnlyList<FilterObservation> Observations) RenderObserved(string map, object content)
    {
        var (observedMap, sites) = NameFilterCalls(map);
        Template template;
        try
        {
            template = Template.Parse(observedMap);
        }
        catch (SyntaxException)
        {
            // The map itself parses (checked by the caller); if the observing copy does not, skip the deviation detection.
            return (null, []);
        }

        // Same context as Template.Render(Hash) creates, plus one observing filter per call site.
        var defaults = new RenderParameters(CultureInfo.CurrentCulture);
        var context = new Context(
            [Hash.FromAnonymousObject(new { content }), template.Assigns],
            template.InstanceAssigns,
            template.Registers,
            defaults.ErrorsOutputMode,
            defaults.MaxIterations,
            defaults.Timeout,
            CultureInfo.CurrentCulture);
        foreach (var site in sites)
        {
            context.Strainer.AddMethodInfo(site.Name, new ObservingFilters(site.Line, site.Column), _observedFilters[site.FilterName]);
        }

        string? rendered = null;
        IReadOnlyList<FilterObservation> observations;
        FilterObserver.Start();
        try
        {
            rendered = template.Render(RenderParameters.FromContext(context, CultureInfo.CurrentCulture));
        }
        catch (RenderException)
        {
            // Already reported by the normal render.
        }
        finally
        {
            observations = FilterObserver.Stop();
        }

        return (rendered, observations);
    }

    // Builds the copy of the map for the observing render: every observed filter call gets its own name, and loops
    // use the observing loop tags with their location in a marker. Nothing else changes, so the output stays identical.
    private static (string Map, List<(string Name, string FilterName, int Line, int Column)> Sites) NameFilterCalls(string map)
    {
        ObservedLoopMarkup.EnsureRegistered();

        var position = new TextPosition(map);
        var sites = new List<(string Name, string FilterName, int Line, int Column)>();
        var edits = new List<(int Offset, int RemoveLength, string Insert)>();
        foreach (var token in LiquidTokenizer.Tokenize(map).Where(t => t.Kind != LiquidTokenKind.Text))
        {
            AddLoopEdits(token, position, edits);

            // Strings are masked, so a '|' inside a string literal is not seen as a filter.
            var masked = _stringPattern.Replace(token.Raw, m => new string(' ', m.Length));
            foreach (Match match in _filterNamePattern.Matches(masked))
            {
                var name = match.Groups["name"];
                if (_observedFilters.ContainsKey(name.Value))
                {
                    var offset = token.Offset + name.Index;
                    var (line, column) = position.At(offset);
                    var siteName = $"{name.Value}__s{sites.Count}";
                    sites.Add((siteName, name.Value, line, column));
                    edits.Add((offset, name.Length, siteName));
                }
            }

            // Every output gets a pass-through observer as last filter, to see the printed value.
            if (token.Kind == LiquidTokenKind.Output && !string.IsNullOrWhiteSpace(token.Markup))
            {
                var closing = token.Raw.EndsWith("-}}", StringComparison.Ordinal) ? token.Raw.Length - 3 : token.Raw.Length - 2;
                var (line, column) = position.At(token.Offset);
                var siteName = $"{nameof(ObservingFilters.ObserveOutput)}__s{sites.Count}";
                sites.Add((siteName, nameof(ObservingFilters.ObserveOutput), line, column));
                edits.Add((token.Offset + closing, 0, $" | {siteName} "));
            }
        }

        var builder = new StringBuilder(map);
        foreach (var (offset, removeLength, insert) in edits.OrderByDescending(e => e.Offset))
        {
            builder.Remove(offset, removeLength).Insert(offset, insert);
        }

        return (builder.ToString(), sites);
    }

    private static void AddLoopEdits(LiquidToken token, TextPosition position, List<(int Offset, int RemoveLength, string Insert)> edits)
    {
        var observedName = token.TagName switch
        {
            "for" or "endfor" => token.TagName.Replace("for", ObservedLoopMarkup.ForTagName, StringComparison.Ordinal),
            RuntimeConstants.JsonArrayForTagName => ObservedLoopMarkup.JsonArrayForTagName,
            "end" + RuntimeConstants.JsonArrayForTagName => "end" + ObservedLoopMarkup.JsonArrayForTagName,
            _ => null,
        };
        if (observedName is null)
        {
            return;
        }

        var nameOffset = token.Raw.IndexOf(token.TagName, 2, StringComparison.Ordinal);
        edits.Add((token.Offset + nameOffset, token.TagName.Length, observedName));

        if (!token.TagName.StartsWith("end", StringComparison.Ordinal))
        {
            var closing = token.Raw.EndsWith("-%}", StringComparison.Ordinal) ? token.Raw.Length - 3 : token.Raw.Length - 2;
            var (line, column) = position.At(token.Offset);
            edits.Add((token.Offset + closing, 0, ObservedLoopMarkup.Marker(line, column)));
        }
    }

    private static List<Diagnostic> ToDiagnostics(IReadOnlyList<FilterObservation> observations)
    {
        var diagnostics = new List<Diagnostic>();
        foreach (var group in observations.GroupBy(o => (o.Code, o.Title, o.Line, o.Column)))
        {
            // A filter in a loop is called many times: one finding per location, with a few distinct values.
            var first = group.First();
            var values = group.Select(o => o.Values).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
            var shown = string.Join("; ", values.Take(MaxValuesPerLocation));
            if (values.Count > MaxValuesPerLocation)
            {
                shown += $"; {values.Count - MaxValuesPerLocation} more value(s)";
            }

            diagnostics.Add(new Diagnostic(group.Key.Code, DiagnosticSeverity.Warning, group.Key.Title, DiagnosticSource.Map, group.Key.Line, group.Key.Column)
            {
                Values = values.Count == 0 ? null : shown,
                Explanation = first.Explanation,
                Fix = first.Fix,
            });
        }

        return diagnostics;
    }
}
