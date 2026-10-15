using System.Globalization;
using DotLiquid;
using LogicApps.Liquid.Constants;
using Newtonsoft.Json.Linq;

namespace LogicApps.Liquid.Runtime;

/// <summary>
/// Offers JSON input to a template, as specified in docs/runtime-behaviour.md (JSON input).
/// The conversion is lazy: every lookup converts the value again, so each lookup gives new objects, like the runtime.
/// </summary>
internal sealed class JsonInputDrop : IIndexable, ILiquidizable
{
    private readonly JToken _token;

    /// <summary>
    /// Wraps a JSON token. For an object the drop itself is the template value; for anything else the converted value is.
    /// </summary>
    /// <param name="token">The JSON token.</param>
    internal JsonInputDrop(JToken token)
    {
        _token = token ?? throw new ArgumentNullException(nameof(token));
    }

    public object? this[object key]
    {
        get
        {
            return ToTemplateValue(_token[key]);
        }
    }

    /// <summary>True for a JSON object without properties. Used by the analysis only.</summary>
    internal bool IsEmptyObject
    {
        get
        {
            return _token is JObject { Count: 0 };
        }
    }

    public bool ContainsKey(object key)
    {
        // Uses the JSON indexer, so an invalid key fails in the same way as in the runtime.
        return _token is JObject && _token[key] is not null;
    }

    public object? ToLiquid()
    {
        return _token is JObject ? this : ToTemplateValue(_token);
    }

    /// <summary>
    /// An object inside a rendered list prints the type name of the runtime's wrapper, so the output is identical to Logic Apps.
    /// </summary>
    /// <returns>The type name of the runtime wrapper.</returns>
    public override string ToString()
    {
        return RuntimeConstants.JsonDropTypeName;
    }

    private static object? ToTemplateValue(JToken? token)
    {
        switch (token)
        {
            case null:
                return null;
            case JObject:
                return new JsonInputDrop(token);
            case JArray array:
                var items = new List<object?>(array.Count);
                foreach (var item in array)
                {
                    items.Add(ToTemplateValue(item));
                }

                return items;
            case JValue value:
                return ToScalar(value);
            default:
                return token.ToString();
        }
    }

    private static object ToScalar(JValue value)
    {
        if (value.Type == JTokenType.Integer)
        {
            return value.ToObject<long>();
        }

        if (value.Type == JTokenType.Float)
        {
            return value.ToObject<decimal>();
        }

        if (value.Type == JTokenType.Boolean)
        {
            return value.ToObject<bool>();
        }

        if (value.Type == JTokenType.Date)
        {
            return value.ToObject<DateTime>();
        }

        // Strings, and null as empty text.
        return value.ToString(CultureInfo.CurrentCulture);
    }
}
