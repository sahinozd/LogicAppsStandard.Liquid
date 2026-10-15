using System.CommandLine;
using System.Text;
using LogicApps.Liquid.Cli.Constants;
using LogicApps.Liquid.Cli.Helpers;
using LogicApps.Liquid.Cli.Models;
using LogicApps.Liquid.Cli.Reporting;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Extensions;
using LogicApps.Liquid.Models;
using Newtonsoft.Json;

namespace LogicApps.Liquid.Cli.Commands;

/// <summary>
/// <c>la-liquid transform --map m.liquid --input in.json --output out.json</c>: validates, renders and writes the output.
/// The output file is only written when validation succeeds.
/// </summary>
internal static class TransformCommand
{
    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static Command Create()
    {
        var map = new Option<string>("--map", "-m") { Description = "The Liquid map.", Required = true };
        var input = new Option<string>("--input", "-i") { Description = "The input message (JSON or XML).", Required = true };
        var output = new Option<string>("--output", "-o") { Description = "The output file. Only written when there are no errors.", Required = true };
        var raw = new Option<bool>("--raw")
        {
            Description = "Write the rendered text exactly as DotLiquid produced it. By default JSON output is written indented, as Logic Apps shows it.",
        };
        var kind = CommonOptions.Kind();
        var warningsAsErrors = CommonOptions.WarningsAsErrors();
        var engine = new EngineOptionSet();

        var command = new Command("transform", "Validate a Liquid map, run it with an input message and write the output.")
        {
            map, input, output, raw, kind, warningsAsErrors,
        };
        engine.AddTo(command);

        command.SetAction(parseResult => Run(
            parseResult.GetValue(map)!,
            parseResult.GetValue(input)!,
            parseResult.GetValue(output)!,
            parseResult.GetValue(raw),
            parseResult.GetValue(kind),
            parseResult.GetValue(warningsAsErrors),
            engine.Read(parseResult)));

        return command;
    }

    private static int Run(string mapPath, string inputPath, string outputPath, bool raw, LiquidTransformKind? kind, bool warningsAsErrors, EngineSettings settings)
    {
        var transformer = CommonOptions.CreateTransformer(settings);
        var map = CommonOptions.ReadFile(mapPath, "map");
        var input = CommonOptions.ReadFile(inputPath, "input");
        if (transformer is null || map is null || input is null)
        {
            return ExitCodes.UsageError;
        }

        var resolvedKind = KindResolver.Resolve(kind, inputPath, outputPath);
        Console.WriteLine($"Kind: {resolvedKind}{(kind is null ? " (inferred)" : string.Empty)}");

        var result = transformer.Transform(map, input, resolvedKind);
        new DiagnosticReporter(Console.Out).Report(result.Diagnostics, new DiagnosticContext(mapPath, inputPath, outputPath, result.RenderedText), warningsAsErrors);

        var errors = result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        var warnings = result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        if (result.HasFailures(warningsAsErrors) || result.RenderedText is null)
        {
            Console.WriteLine($"Transform failed: {errors} error(s), {warnings} warning(s). No output written.");
            return ExitCodes.Failed;
        }

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, FormatOutput(result, raw), _utf8NoBom);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: cannot write output '{outputPath}': {ex.Message}");
            return ExitCodes.UsageError;
        }

        Console.WriteLine($"Transform succeeded: {warnings} warning(s). Output written to '{outputPath}'.");
        return ExitCodes.Success;
    }

    internal static string FormatOutput(LiquidTransformResult result, bool raw)
    {
        if (raw || !result.Kind.HasJsonOutput() || result.Output is null)
        {
            return result.RenderedText ?? string.Empty;
        }

        return result.Output.ToString(Formatting.Indented);
    }
}
