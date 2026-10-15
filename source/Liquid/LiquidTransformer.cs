using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DotLiquid;
using DotLiquid.Exceptions;
using LogicApps.Liquid.Analysis;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Extensions;
using LogicApps.Liquid.Linting;
using LogicApps.Liquid.Models;
using LogicApps.Liquid.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogicApps.Liquid;

/// <summary>
/// Validates and runs a Liquid map the way the Logic Apps Standard Liquid action does.
/// </summary>
/// <remarks>
/// The pipeline follows the observable behaviour of the Liquid action (see docs/runtime-behaviour.md):
/// <list type="number">
/// <item>Parse the map with DotLiquid 2.0.361 using the C# naming convention.</item>
/// <item>Wrap the input as <c>content</c> (JSON or XML).</item>
/// <item>Render with <c>Template.Render(Hash)</c>: render errors do not fail, they end up as text in the output.</item>
/// <item>For <c>*ToJson</c> kinds, parse the rendered text as JSON.</item>
/// </list>
/// On top of that the static checks report problems Logic Apps silently ignores, and render errors are reported as errors.
/// </remarks>
public sealed class LiquidTransformer : ILiquidTransformer
{
    private static readonly Regex _missingVariablePattern = new("Variable '(.+)' could not be found");

    // DotLiquid configuration, the emulated time zone and the deviation observer are process-wide, so transforms run one at a time.
    private static readonly Lock _processWide = new();

    private readonly LiquidTransformOptions _options;
    private readonly ILiquidLinter _linter;
    private readonly IDeviationDetector _deviationDetector;

    /// <summary>
    /// Creates a transformer with default options (en-US culture, linting on).
    /// </summary>
    public LiquidTransformer()
        : this(new LiquidTransformOptions(), new LiquidLinter(), new DeviationDetector())
    {
    }

    /// <summary>
    /// Creates a transformer with the given options and the default linter and deviation detector.
    /// </summary>
    /// <param name="options">The transform options.</param>
    public LiquidTransformer(LiquidTransformOptions options)
        : this(options, new LiquidLinter(), new DeviationDetector())
    {
    }

    /// <summary>
    /// Creates a transformer with the given options, linter and deviation detector.
    /// </summary>
    /// <param name="options">The transform options.</param>
    /// <param name="linter">The linter that runs the static checks.</param>
    /// <param name="deviationDetector">Detects filters that behave differently from Shopify Liquid while rendering.</param>
    public LiquidTransformer(LiquidTransformOptions options, ILiquidLinter linter, IDeviationDetector deviationDetector)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _linter = linter ?? throw new ArgumentNullException(nameof(linter));
        _deviationDetector = deviationDetector ?? throw new ArgumentNullException(nameof(deviationDetector));
        LogicAppsLiquidRuntime.EnsureConfigured();
    }

    /// <summary>
    /// Validates a map without input: static checks plus a parse with the Logic Apps engine.
    /// </summary>
    /// <param name="map">The Liquid map.</param>
    /// <returns>The findings.</returns>
    public IReadOnlyList<Diagnostic> Validate(string map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var diagnostics = new List<Diagnostic>();
        lock (_processWide)
        using (new CultureScope(_options.Culture))
        {
            _ = Parse(map, diagnostics);
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates the map, renders it with the input and validates the output.
    /// </summary>
    /// <param name="map">The Liquid map.</param>
    /// <param name="input">The input message: JSON for <c>JsonTo*</c> kinds, XML for <c>XmlTo*</c> kinds.</param>
    /// <param name="kind">The transform kind, as configured on the Liquid action.</param>
    /// <returns>The findings, the rendered text and the action output.</returns>
    public LiquidTransformResult Transform(string map, string input, LiquidTransformKind kind)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(input);

        lock (_processWide)
        {
            return TransformLocked(map, input, kind);
        }
    }

    private LiquidTransformResult TransformLocked(string map, string input, LiquidTransformKind kind)
    {
        var diagnostics = new List<Diagnostic>();
        using var cultureScope = new CultureScope(_options.Culture);
        using var timeZoneScope = _options.TimeZone is null ? null : new TimeZoneScope(_options.TimeZone);

        var template = Parse(map, diagnostics);
        var content = ReadInput(input, kind, diagnostics);
        if (template is null || content is null)
        {
            return new LiquidTransformResult(kind, diagnostics, null, null);
        }

        var rendered = Render(template, content, diagnostics);
        if (rendered is null)
        {
            return new LiquidTransformResult(kind, diagnostics, null, null);
        }

        if (_options.DetectDeviations)
        {
            AddDeviations(_deviationDetector.Detect(map, content), diagnostics);
        }

        var output = ReadOutput(rendered, kind, diagnostics);
        return new LiquidTransformResult(kind, diagnostics, rendered, output);
    }

    // A deviation that the static checks already reported on the same line is not reported twice.
    private static void AddDeviations(IReadOnlyList<Diagnostic> deviations, List<Diagnostic> diagnostics)
    {
        foreach (var deviation in deviations)
        {
            if (!diagnostics.Any(d => d.Code == deviation.Code && d.Line == deviation.Line))
            {
                diagnostics.Add(deviation);
            }
        }
    }

    private Template? Parse(string map, List<Diagnostic> diagnostics)
    {
        if (_options.Lint)
        {
            diagnostics.AddRange(_linter.Lint(map));
        }

        try
        {
            return Template.Parse(map);
        }
        catch (SyntaxException ex)
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.TemplateSyntaxError, DiagnosticSeverity.Error,
                $"Logic Apps fails with 'An error occurred while parsing liquid template. {ex.Message}'"));
            return null;
        }
    }

    private static object? ReadInput(string input, LiquidTransformKind kind, List<Diagnostic> diagnostics)
    {
        if (kind.HasXmlInput())
        {
            try
            {
                // Like the runtime: the XML is loaded from the (UTF-8) bytes of the message, so an XML declaration
                // with a different encoding (encoding="utf-16") is an error, as it is in Logic Apps.
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
                return new XmlInputDrop(XElement.Load(stream));
            }
            catch (XmlException ex)
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.InvalidXmlInput, DiagnosticSeverity.Error,
                    $"The input is not valid XML: {ex.Message}", DiagnosticSource.Input, ex.LineNumber, ex.LinePosition));
                return null;
            }
        }

        try
        {
            return new JsonInputDrop(RuntimeJson.Parse(input));
        }
        catch (JsonReaderException ex)
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.InvalidJsonInput, DiagnosticSeverity.Error,
                $"The input is not valid JSON: {ex.Message}", DiagnosticSource.Input, NullIfZero(ex.LineNumber), NullIfZero(ex.LinePosition)));
            return null;
        }
    }

    private static string? Render(Template template, object content, List<Diagnostic> diagnostics)
    {
        string rendered;
        try
        {
            // Exactly like the runtime: Render(Hash) without RenderParameters, so errors are written into the output.
            rendered = template.Render(Hash.FromAnonymousObject(new { content }), CultureInfo.CurrentCulture);
        }
        catch (RenderException ex)
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.RenderError, DiagnosticSeverity.Error,
                $"Logic Apps fails with 'An error occurred while executing the liquid transformation. {ex.Message}'"));
            return null;
        }

        // Each error is written into the output as "Liquid error: ..." (or "Liquid syntax error: ..."), in order.
        var position = new TextPosition(rendered);
        var searchFrom = 0;
        foreach (var error in template.Errors)
        {
            // A missing value is recorded as an error by DotLiquid, but renders as empty without any text in the output.
            if (error is VariableNotFoundException)
            {
                AddMissingValue(error.Message, diagnostics);
                continue;
            }

            var index = IndexOfErrorText(rendered, searchFrom);
            int? line = null;
            int? column = null;
            if (index >= 0)
            {
                (line, column) = position.At(index);
                searchFrom = index + 1;
            }

            diagnostics.Add(new Diagnostic(DiagnosticCodes.RenderError, DiagnosticSeverity.Error,
                $"Render error: {error.Message} Logic Apps does not fail on this: it writes 'Liquid error: ...' into the output.",
                DiagnosticSource.Output, line, column));
        }

        return rendered;
    }

    private static void AddMissingValue(string message, List<Diagnostic> diagnostics)
    {
        var match = _missingVariablePattern.Match(message);
        var variable = match.Success ? match.Groups[1].Value : message;
        var root = variable.Split('.', '[')[0];

        // An unassigned variable is already reported by the linter (LQ020), with its location.
        if (root != "content" && diagnostics.Any(d => d.Code == DiagnosticCodes.UndefinedVariable && d.Message.Contains($"'{root}'", StringComparison.Ordinal)))
        {
            return;
        }

        if (diagnostics.Any(d => d.Code == DiagnosticCodes.MissingValue && d.Message.Contains($"'{variable}'", StringComparison.Ordinal)))
        {
            return;
        }

        diagnostics.Add(new Diagnostic(DiagnosticCodes.MissingValue, DiagnosticSeverity.Warning,
            $"'{variable}' was not found while rendering, so it renders as empty. Check the spelling against the input, or whether the input is complete."));
    }

    private static int IndexOfErrorText(string rendered, int start)
    {
        var error = rendered.IndexOf(RuntimeConstants.LiquidErrorMarker, start, StringComparison.Ordinal);
        var syntaxError = rendered.IndexOf(RuntimeConstants.LiquidSyntaxErrorMarker, start, StringComparison.Ordinal);
        if (error < 0)
        {
            return syntaxError;
        }

        return syntaxError < 0 ? error : Math.Min(error, syntaxError);
    }

    private static JToken? ReadOutput(string rendered, LiquidTransformKind kind, List<Diagnostic> diagnostics)
    {
        if (!kind.HasJsonOutput())
        {
            if (string.IsNullOrWhiteSpace(rendered))
            {
                diagnostics.Add(new Diagnostic(DiagnosticCodes.EmptyOutput, DiagnosticSeverity.Warning,
                    "The map rendered an empty output.", DiagnosticSource.Output));
            }

            return new JValue(rendered);
        }

        // The runtime parses empty JSON output as null: the action succeeds with an empty (null) output.
        if (string.IsNullOrWhiteSpace(rendered))
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.EmptyOutput, DiagnosticSeverity.Warning,
                "The map rendered an empty output. Logic Apps does not fail: the action output is null.", DiagnosticSource.Output));
            return JValue.CreateNull();
        }

        JToken output;
        try
        {
            output = RuntimeJson.Parse(rendered);
        }
        catch (JsonReaderException ex)
        {
            diagnostics.Add(new Diagnostic(DiagnosticCodes.InvalidJsonOutput, DiagnosticSeverity.Error,
                $"The output is not valid JSON, so Logic Apps fails the action ('The transformed value is not a valid JSON'). {ex.Message} " +
                "A common cause is a text value with a double quote or backslash: DotLiquid has no JSON escape filter, so escape it with '| Replace: '\\\\', '\\\\' | Replace: '\"', '\\\"''.",
                DiagnosticSource.Output, NullIfZero(ex.LineNumber), NullIfZero(ex.LinePosition)));
            return null;
        }

        // Newtonsoft reads an empty array item ('[,2]') as undefined instead of failing; the action output then contains 'undefined'.
        var undefined = (output as JContainer)?.Descendants().FirstOrDefault(t => t.Type == JTokenType.Undefined);
        if (undefined is not null)
        {
            var lineInfo = (IJsonLineInfo)undefined;
            diagnostics.Add(new Diagnostic(DiagnosticCodes.UndefinedInOutput, DiagnosticSeverity.Warning,
                $"The output contains an empty array item at '{undefined.Path}', which becomes 'undefined' (not valid JSON for the next system). " +
                "Typically a stray comma, for example from an if inside JSONArrayFor that skips an item.",
                DiagnosticSource.Output, lineInfo.HasLineInfo() ? lineInfo.LineNumber : null, lineInfo.HasLineInfo() ? lineInfo.LinePosition : null));
        }

        return output;
    }

    private static int? NullIfZero(int value)
    {
        return value == 0 ? null : value;
    }
}
