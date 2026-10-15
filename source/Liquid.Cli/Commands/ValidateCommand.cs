using System.CommandLine;
using LogicApps.Liquid.Cli.Constants;
using LogicApps.Liquid.Cli.Helpers;
using LogicApps.Liquid.Cli.Models;
using LogicApps.Liquid.Cli.Reporting;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Cli.Commands;

/// <summary>
/// <c>la-liquid validate --map &lt;file|folder&gt; [--input file]</c>: checks maps without writing anything.
/// With a folder, every <c>*.liquid</c> file in it (recursively) is checked. With an input, the map is also
/// rendered and the output is checked, as a dry run of <c>transform</c>.
/// </summary>
internal static class ValidateCommand
{
    public static Command Create()
    {
        var map = new Option<string>("--map", "-m")
        {
            Description = "A Liquid map, or a folder with Liquid maps (searched recursively for *.liquid).",
            Required = true,
        };
        var input = new Option<string?>("--input", "-i")
        {
            Description = "Optional input message. When given, the map is also rendered and the output is validated. Only valid with a single map.",
        };
        var kind = CommonOptions.Kind();
        var warningsAsErrors = CommonOptions.WarningsAsErrors();
        var engine = new EngineOptionSet();

        var command = new Command("validate", "Validate Liquid maps against the Logic Apps Liquid engine.")
        {
            map, input, kind, warningsAsErrors,
        };
        engine.AddTo(command);

        command.SetAction(parseResult => Run(
            parseResult.GetValue(map)!,
            parseResult.GetValue(input),
            parseResult.GetValue(kind),
            parseResult.GetValue(warningsAsErrors),
            engine.Read(parseResult)));

        return command;
    }

    private static int Run(string mapPath, string? inputPath, LiquidTransformKind? kind, bool warningsAsErrors, EngineSettings settings)
    {
        var transformer = CommonOptions.CreateTransformer(settings);
        if (transformer is null)
        {
            return ExitCodes.UsageError;
        }

        var reporter = new DiagnosticReporter(Console.Out);

        if (Directory.Exists(mapPath))
        {
            if (inputPath is not null)
            {
                Console.Error.WriteLine("error: --input can only be used with a single map, not with a folder.");
                return ExitCodes.UsageError;
            }

            return ValidateFolder(transformer, reporter, mapPath, warningsAsErrors);
        }

        var map = CommonOptions.ReadFile(mapPath, "map");
        if (map is null)
        {
            return ExitCodes.UsageError;
        }

        if (inputPath is null)
        {
            var diagnostics = transformer.Validate(map);
            reporter.Report(diagnostics, new DiagnosticContext(mapPath), warningsAsErrors);
            return Summarize(diagnostics, 1, warningsAsErrors);
        }

        var input = CommonOptions.ReadFile(inputPath, "input");
        if (input is null)
        {
            return ExitCodes.UsageError;
        }

        var resolvedKind = KindResolver.Resolve(kind, inputPath, null);
        Console.WriteLine($"Kind: {resolvedKind}{(kind is null ? " (inferred)" : string.Empty)}");
        var result = transformer.Transform(map, input, resolvedKind);
        reporter.Report(result.Diagnostics, new DiagnosticContext(mapPath, inputPath, null, result.RenderedText), warningsAsErrors);
        return Summarize(result.Diagnostics, 1, warningsAsErrors);
    }

    private static int ValidateFolder(ILiquidTransformer transformer, DiagnosticReporter reporter, string folder, bool warningsAsErrors)
    {
        var maps = Directory.GetFiles(folder, "*.liquid", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (maps.Count == 0)
        {
            Console.Error.WriteLine($"error: no *.liquid files found in '{folder}'.");
            return ExitCodes.UsageError;
        }

        var all = new List<Diagnostic>();
        foreach (var mapPath in maps)
        {
            var map = CommonOptions.ReadFile(mapPath, "map");
            if (map is null)
            {
                return ExitCodes.UsageError;
            }

            var diagnostics = transformer.Validate(map);
            reporter.Report(diagnostics, new DiagnosticContext(mapPath), warningsAsErrors);
            all.AddRange(diagnostics);
        }

        return Summarize(all, maps.Count, warningsAsErrors);
    }

    private static int Summarize(IReadOnlyCollection<Diagnostic> diagnostics, int mapCount, bool warningsAsErrors)
    {
        var errors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        var warnings = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        var failed = errors > 0 || (warningsAsErrors && warnings > 0);
        Console.WriteLine($"{(failed ? "Validation failed" : "Validation succeeded")}: {mapCount} map(s), {errors} error(s), {warnings} warning(s).");
        return failed ? ExitCodes.Failed : ExitCodes.Success;
    }
}
