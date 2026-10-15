using System.Reflection;
using System.Text;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Extensions;

namespace LogicApps.Liquid.ConformanceTests;

/// <summary>
/// Runs a map through the real Logic Apps Standard Liquid engine
/// (<c>Microsoft.Azure.Workflows.Integration.LiquidMessageProcessors.LiquidTransformer</c>), loaded from a locally
/// installed workflow extension bundle in its own <see cref="BundleLoadContext"/>.
/// </summary>
/// <remarks>
/// The bundle is installed by the Azure Logic Apps (Standard) extension for VS Code, or by Azure Functions Core Tools.
/// Set <c>LOGICAPPS_WORKFLOW_BUNDLE_BIN</c> to use a specific bundle <c>bin</c> folder.
/// </remarks>
public sealed class RuntimeLiquidEngine
{
    /// <summary>The environment variable that points to a bundle <c>bin</c> folder.</summary>
    public const string BundlePathVariable = "LOGICAPPS_WORKFLOW_BUNDLE_BIN";

    private readonly object _transformer;
    private readonly MethodInfo _transformMethod;
    private readonly Type _inputType;
    private readonly Type _kindType;
    private readonly Type _jTokenType;
    private readonly MethodInfo _fromJson;
    private readonly MethodInfo _parseToken;

    private RuntimeLiquidEngine(string binPath)
    {
        BinPath = binPath;
        var context = new BundleLoadContext(binPath);
        var edge = context.LoadFromAssemblyPath(Path.Combine(binPath, "Microsoft.Azure.Workflows.Integration.Edge.dll"));
        var newtonsoft = context.LoadFromAssemblyName(new AssemblyName("Newtonsoft.Json"));
        var resourceStack = context.LoadFromAssemblyName(new AssemblyName("Microsoft.WindowsAzure.ResourceStack"));

        var transformerType = edge.GetType("Microsoft.Azure.Workflows.Integration.LiquidMessageProcessors.LiquidTransformer", throwOnError: true)!;
        _transformer = transformerType.GetField("Instance")!.GetValue(null)!;
        _transformMethod = transformerType.GetMethod("Transform")!;
        _inputType = edge.GetType("Microsoft.Azure.Workflows.Integration.Entities.LiquidTransformInput", throwOnError: true)!;
        _kindType = _inputType.GetProperty("Kind")!.PropertyType;
        _jTokenType = newtonsoft.GetType("Newtonsoft.Json.Linq.JToken", throwOnError: true)!;
        _parseToken = _jTokenType.GetMethod("Parse", [typeof(string)])!;

        // The runtime reads action inputs with ResourceStack's JSON settings (dates are not parsed).
        _fromJson = resourceStack.GetType("Microsoft.WindowsAzure.ResourceStack.Common.Json.JsonExtensions", throwOnError: true)!
            .GetMethods()
            .Single(m => m is { Name: "FromJson", IsGenericMethodDefinition: true } && m.GetParameters().Length == 1)
            .MakeGenericMethod(_jTokenType);
    }

    /// <summary>The bundle <c>bin</c> folder the engine was loaded from.</summary>
    public string BinPath { get; }

    /// <summary>
    /// Loads the engine from the newest installed bundle.
    /// </summary>
    /// <returns>The engine, or null when no bundle is installed.</returns>
    public static RuntimeLiquidEngine? TryLoad()
    {
        var binPath = FindBundleBin();
        return binPath is null ? null : new RuntimeLiquidEngine(binPath);
    }

    /// <summary>
    /// Runs a map through the real engine.
    /// </summary>
    /// <param name="map">The Liquid map.</param>
    /// <param name="input">The input message.</param>
    /// <param name="kind">The transform kind.</param>
    /// <returns>The output, or the error when the action fails.</returns>
    public RuntimeOutcome Transform(string map, string input, LiquidTransformKind kind)
    {
        // The runtime caches parsed maps by file name, so every call gets a unique file.
        var mapPath = Path.Combine(Path.GetTempPath(), $"la-liquid-conformance-{Guid.NewGuid():N}.liquid");
        File.WriteAllText(mapPath, map);
        try
        {
            var request = Activator.CreateInstance(_inputType)!;
            _inputType.GetProperty("MapFilePath")!.SetValue(request, mapPath);
            _inputType.GetProperty("Kind")!.SetValue(request, Enum.Parse(_kindType, kind.ToString()));
            _inputType.GetProperty("Content")!.SetValue(request, CreateContent(input, kind));

            var task = (Task)_transformMethod.Invoke(_transformer, [request, CancellationToken.None])!;
            task.GetAwaiter().GetResult();
            var result = task.GetType().GetProperty("Result")!.GetValue(task);
            return new RuntimeOutcome(result is null ? "null" : Serialize(result), null);
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException { InnerException: { } innerException } ? innerException : ex;
            return new RuntimeOutcome(null, inner.Message);
        }
        finally
        {
            File.Delete(mapPath);
        }
    }

    private object CreateContent(string input, LiquidTransformKind kind)
    {
        if (!kind.HasXmlInput())
        {
            return _fromJson.Invoke(null, [input])!;
        }

        // XML content reaches the Liquid action as a content envelope.
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        return _parseToken.Invoke(null, [$$"""{"$content-type":"application/xml","$content":"{{base64}}"}"""])!;
    }

    private string Serialize(object token)
    {
        var assembly = _jTokenType.Assembly;
        var formatting = assembly.GetType("Newtonsoft.Json.Formatting", throwOnError: true)!;
        var converter = assembly.GetType("Newtonsoft.Json.JsonConverter", throwOnError: true)!;
        var toString = _jTokenType.GetMethod("ToString", [formatting, converter.MakeArrayType()])!;
        return (string)toString.Invoke(token, [Enum.Parse(formatting, "None"), Array.CreateInstance(converter, 0)])!;
    }

    private static string? FindBundleBin()
    {
        var configured = Environment.GetEnvironmentVariable(BundlePathVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Directory.Exists(configured) ? configured : null;
        }

        var bundles = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".azure-functions-core-tools", "Functions", "ExtensionBundles", "Microsoft.Azure.Functions.ExtensionBundle.Workflows");
        if (!Directory.Exists(bundles))
        {
            return null;
        }

        return Directory.GetDirectories(bundles)
            .Select(d => (Path: Path.Combine(d, "bin"), Version: Version.TryParse(Path.GetFileName(d), out var version) ? version : null))
            .Where(b => b.Version is not null && File.Exists(Path.Combine(b.Path, "Microsoft.Azure.Workflows.Integration.Edge.dll")))
            .OrderByDescending(b => b.Version)
            .Select(b => b.Path)
            .FirstOrDefault();
    }
}
