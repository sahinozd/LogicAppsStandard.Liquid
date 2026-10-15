namespace LogicApps.Liquid.Constants;

/// <summary>
/// Versions and names used by the Logic Apps Standard runtime for the Liquid action.
/// </summary>
public static class RuntimeConstants
{
    /// <summary>The DotLiquid version used by the Logic Apps Standard runtime.</summary>
    public const string DotLiquidVersion = "2.0.361";

    /// <summary>The workflow extension bundle version this behaviour was verified against.</summary>
    public const string VerifiedExtensionBundleVersion = "1.170.91";

    /// <summary>The name of the custom tag Logic Apps registers.</summary>
    public const string JsonArrayForTagName = "JSONArrayFor";

    /// <summary>The culture assumed for the Logic Apps host in Azure.</summary>
    public const string DefaultCultureName = "en-US";

    /// <summary>The type name of the runtime's JSON wrapper, which is what an object prints inside a rendered array.</summary>
    public const string JsonDropTypeName = "Microsoft.Azure.Function.Common.JObjectDrop";

    /// <summary>The type name of the runtime's XML wrapper, as it appears in render error messages.</summary>
    public const string XmlDropTypeName = "Microsoft.Azure.Function.Common.XElementDrop";

    /// <summary>The time zone of the Logic Apps host in Azure, unless the app setting <c>WEBSITE_TIME_ZONE</c> is set.</summary>
    public const string DefaultTimeZoneId = "UTC";

    /// <summary>The maximum JSON depth the runtime accepts.</summary>
    public const int JsonMaxDepth = 512;

    /// <summary>The text DotLiquid writes into the output for a render error.</summary>
    public const string LiquidErrorMarker = "Liquid error:";

    /// <summary>The text DotLiquid writes into the output for a syntax error during rendering.</summary>
    public const string LiquidSyntaxErrorMarker = "Liquid syntax error:";
}
