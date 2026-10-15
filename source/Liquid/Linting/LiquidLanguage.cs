using System.Collections;
using System.Reflection;
using DotLiquid;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Runtime;

namespace LogicApps.Liquid.Linting;

/// <summary>
/// What the Logic Apps flavour of DotLiquid knows: registered tags and the filter names under the C# naming convention.
/// Both are read from the DotLiquid assembly itself, so they always match the pinned engine version.
/// </summary>
public static class LiquidLanguage
{
    private static readonly Lazy<IReadOnlySet<string>> InternalFilters = new(LoadFilterNames);
    private static readonly Lazy<Dictionary<string, Type>> InternalTags = new(LoadTags);

    /// <summary>
    /// Tags that only exist inside a block and are handled by that block. DotLiquid's <c>if</c> accepts both <c>elsif</c> and <c>elseif</c>.
    /// </summary>
    public static IReadOnlySet<string> IntermediateTags { get; } = new HashSet<string>(StringComparer.Ordinal) { "else", "elsif", "elseif", "when" };

    /// <summary>
    /// Filters that exist in Shopify Liquid but not in DotLiquid 2.0.361.
    /// </summary>
    public static IReadOnlySet<string> ShopifyOnlyFilters { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "where", "concat", "reverse", "ceil", "floor", "escape_once", "sort_natural", "url_escape", "url_param_escape", "json",
    };

    /// <summary>
    /// Tags that exist in Shopify Liquid but not in DotLiquid 2.0.361.
    /// </summary>
    public static IReadOnlySet<string> ShopifyOnlyTags { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "render", "echo", "liquid", "increment", "decrement", "paginate", "form", "section", "style", "javascript", "schema",
    };

    /// <summary>
    /// Names that are always available in a Logic Apps map.
    /// </summary>
    public static IReadOnlySet<string> BuiltInVariables { get; } = new HashSet<string>(StringComparer.Ordinal) { "content", "forloop", "tablerowloop" };

    /// <summary>
    /// Keywords and literals that can appear in expressions.
    /// </summary>
    /// <remarks>DotLiquid matches the word operators case-insensitively (<c>startsWith</c> and <c>startswith</c>).</remarks>
    public static IReadOnlySet<string> Keywords { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "contains", "startsWith", "endsWith", "hasKey", "hasValue", "in", "reversed", "limit", "offset", "cols", "with", "where", "for", "as",
        "true", "false", "nil", "null", "empty", "blank",
    };

    /// <summary>
    /// All filter names, exactly as Logic Apps resolves them.
    /// </summary>
    public static IReadOnlySet<string> Filters => InternalFilters.Value;

    /// <summary>
    /// Determines whether a tag is registered.
    /// </summary>
    /// <param name="name">The tag name, case-sensitive.</param>
    /// <returns>True when Logic Apps knows the tag.</returns>
    public static bool IsTag(string name)
    {
        return InternalTags.Value.ContainsKey(name);
    }

    /// <summary>
    /// Determines whether a tag is a block tag that needs an end tag.
    /// </summary>
    /// <param name="name">The tag name, case-sensitive.</param>
    /// <returns>True for block tags such as <c>if</c>, <c>for</c> and <c>JSONArrayFor</c>.</returns>
    public static bool IsBlockTag(string name)
    {
        // Include and Extends derive from Block, but override parsing and have no end tag.
        return InternalTags.Value.TryGetValue(name, out var type) && type.IsSubclassOf(typeof(Block))
            && !typeof(DotLiquid.Tags.Include).IsAssignableFrom(type) && !typeof(DotLiquid.Tags.Extends).IsAssignableFrom(type);
    }

    /// <summary>
    /// Finds the correctly cased tag for a wrongly cased one, for example <c>If</c> → <c>if</c>.
    /// </summary>
    /// <param name="name">The wrongly cased tag name.</param>
    /// <returns>The correct tag name, or null when there is none.</returns>
    public static string? SuggestTag(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var lower = name.ToLowerInvariant();
        if (IsTag(lower) || IntermediateTags.Contains(lower))
        {
            return lower;
        }

        // Python and other languages: elif, else_if.
        if (lower is "elif" or "else_if" or "elseif")
        {
            return "elsif";
        }

        return name.Equals(RuntimeConstants.JsonArrayForTagName, StringComparison.OrdinalIgnoreCase)
            ? RuntimeConstants.JsonArrayForTagName
            : null;
    }

    /// <summary>
    /// Finds the C# named filter for a Ruby/Shopify style name, for example <c>upcase</c> → <c>Upcase</c>
    /// and <c>strip_html</c> → <c>StripHtml</c>.
    /// </summary>
    /// <param name="name">The filter name as written in the map.</param>
    /// <returns>The correct filter name, or null when there is none.</returns>
    public static string? SuggestFilter(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var normalized = name.Replace("_", string.Empty, StringComparison.Ordinal);
        return Filters.FirstOrDefault(f => f.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    // Template.GetTagType throws for unknown names in DotLiquid 2.0.361, so the registry is read once instead.
    private static Dictionary<string, Type> LoadTags()
    {
        LogicAppsLiquidRuntime.EnsureConfigured();
        var registry = typeof(Template).GetProperty("Tags", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as IDictionary
            ?? throw new InvalidOperationException("The DotLiquid tag registry could not be read. Was the DotLiquid version changed?");

        var tags = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in registry)
        {
            tags[(string)entry.Key] = (Type)entry.Value!.GetType().GetProperty("Item2")!.GetValue(entry.Value)!;
        }

        return tags;
    }

    private static HashSet<string> LoadFilterNames()
    {
        LogicAppsLiquidRuntime.EnsureConfigured();
        return typeof(StandardFilters)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(m => Template.NamingConvention.GetMemberName(m.Name))
            .ToHashSet(StringComparer.Ordinal);
    }
}
