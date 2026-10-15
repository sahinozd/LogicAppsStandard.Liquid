using System.Runtime.CompilerServices;
using DotLiquid;
using DotLiquid.NamingConventions;
using LogicApps.Liquid.Constants;

namespace LogicApps.Liquid.Runtime;

/// <summary>
/// Configures DotLiquid the same way the Logic Apps Standard runtime does
/// (Microsoft.Azure.Workflows.Integration.LiquidMessageProcessors.LiquidTransformer):
/// <list type="bullet">
/// <item>C# naming convention, so filters are PascalCase (<c>Upcase</c>, <c>Date</c>, <c>Times</c>).</item>
/// <item>The custom <c>JSONArrayFor</c> block tag.</item>
/// </list>
/// DotLiquid keeps this configuration in static state, so it is applied once per process.
/// </summary>
public static class LogicAppsLiquidRuntime
{
    private static readonly Lock SyncRoot = new();
    private static bool _configured;

    /// <summary>
    /// Applies the Logic Apps DotLiquid configuration. Safe to call multiple times.
    /// </summary>
    public static void EnsureConfigured()
    {
        lock (SyncRoot)
        {
            if (_configured)
            {
                return;
            }

            // The standard tags (if, for, assign, ...) are registered by the static constructor of DotLiquid.Liquid,
            // which normally runs on the first Template.Parse. Run it now, so the tag registry is complete.
            RuntimeHelpers.RunClassConstructor(typeof(DotLiquid.Liquid).TypeHandle);

            Template.NamingConvention = new CSharpNamingConvention();
            Template.RegisterTag<JsonArrayFor>(RuntimeConstants.JsonArrayForTagName);
            _configured = true;
        }
    }
}
