using LogicApps.Liquid.Constants;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogicApps.Liquid.Runtime;

/// <summary>
/// Parses JSON with the same settings the Logic Apps runtime uses
/// (Microsoft.WindowsAzure.ResourceStack JsonExtensions.ObjectSerializationSettings):
/// dates are <b>not</b> parsed (they stay strings), floats are parsed as <see cref="double"/>, and the maximum depth is 512.
/// </summary>
public static class RuntimeJson
{
    private static readonly JsonSerializer _serializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        MaxDepth = RuntimeConstants.JsonMaxDepth,
        TypeNameHandling = TypeNameHandling.None,
        DateParseHandling = DateParseHandling.None,
        DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
    });

    /// <summary>
    /// Parses a JSON document into a <see cref="JToken"/>.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The parsed token.</returns>
    /// <exception cref="JsonReaderException">The text is not valid JSON.</exception>
    public static JToken Parse(string json)
    {
        using var reader = new StringReader(json);
        using var jsonReader = new JsonTextReader(reader);
        return _serializer.Deserialize<JToken>(jsonReader)
            ?? throw new JsonReaderException("The JSON document is empty.");
    }
}
