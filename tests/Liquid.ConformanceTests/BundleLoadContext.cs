using System.Reflection;
using System.Runtime.Loader;

namespace LogicApps.Liquid.ConformanceTests;

/// <summary>
/// Loads assemblies from a workflow extension bundle first, so the Logic Apps runtime gets its own DotLiquid and
/// Newtonsoft.Json, fully separated from the tool under test.
/// </summary>
public sealed class BundleLoadContext : AssemblyLoadContext
{
    private readonly string _binPath;

    /// <summary>
    /// Creates a load context for a bundle.
    /// </summary>
    /// <param name="binPath">The <c>bin</c> folder of the workflow extension bundle.</param>
    public BundleLoadContext(string binPath)
        : base("LogicAppsWorkflowBundle")
    {
        _binPath = binPath ?? throw new ArgumentNullException(nameof(binPath));
    }

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);

        var path = Path.Combine(_binPath, assemblyName.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }
}
