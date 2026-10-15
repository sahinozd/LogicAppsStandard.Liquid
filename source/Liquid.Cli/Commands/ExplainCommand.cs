using System.CommandLine;
using LogicApps.Liquid.Cli.Constants;
using LogicApps.Liquid.Cli.Helpers;
using LogicApps.Liquid.Cli.Reporting;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Enums;
using LogicApps.Liquid.Models;

namespace LogicApps.Liquid.Cli.Commands;

/// <summary>
/// <c>la-liquid explain [LQxxx]</c>: explains a diagnostic code with an example and the fix, or lists all codes.
/// </summary>
internal static class ExplainCommand
{
    public static Command Create()
    {
        var code = new Argument<string?>("code")
        {
            Description = "The code to explain, for example LQ102. Without a code, all codes are listed.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var noColor = new Option<bool>("--no-color") { Description = "Write no colors." };

        var command = new Command("explain", "Explain a diagnostic code: what it means, an example and how to fix it.")
        {
            code, noColor,
        };
        command.SetAction(parseResult => Run(parseResult.GetValue(code), new AnsiPalette(ConsoleColors.Enabled(parseResult.GetValue(noColor)))));
        return command;
    }

    private static int Run(string? code, AnsiPalette palette)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            WriteList(palette);
            return ExitCodes.Success;
        }

        var descriptor = DiagnosticCatalog.Find(code);
        if (descriptor is null)
        {
            Console.Error.WriteLine($"error: unknown code '{code}'. Run 'la-liquid explain' for the list of codes.");
            return ExitCodes.UsageError;
        }

        WriteDescriptor(descriptor, palette);
        return ExitCodes.Success;
    }

    private static void WriteList(AnsiPalette palette)
    {
        foreach (var group in DiagnosticCatalog.All.GroupBy(d => d.Category).OrderBy(g => g.Key))
        {
            Console.WriteLine(palette.Bold(CategoryTitle(group.Key)));
            foreach (var descriptor in group)
            {
                Console.WriteLine($"  {Severity(descriptor, palette)} {descriptor.Code}  {descriptor.Title}");
            }

            Console.WriteLine();
        }

        Console.WriteLine(palette.Dim("Run 'la-liquid explain <code>' for an example and the fix."));
    }

    private static void WriteDescriptor(DiagnosticDescriptor descriptor, AnsiPalette palette)
    {
        Console.WriteLine($"{Severity(descriptor, palette)} {palette.Bold($"{descriptor.Code}  {descriptor.Title}")}");
        Console.WriteLine(palette.Dim(CategoryTitle(descriptor.Category)));
        Console.WriteLine();
        Console.WriteLine(descriptor.Description);
        if (descriptor.Example is not null)
        {
            Console.WriteLine();
            Console.WriteLine(palette.Dim("Example"));
            foreach (var line in descriptor.Example.Split('\n'))
            {
                Console.WriteLine($"  {line}");
            }
        }

        if (descriptor.Fix is not null)
        {
            Console.WriteLine();
            Console.WriteLine(palette.Green("Fix"));
            Console.WriteLine($"  {descriptor.Fix}");
        }
    }

    private static string Severity(DiagnosticDescriptor descriptor, AnsiPalette palette)
    {
        return descriptor.Severity switch
        {
            DiagnosticSeverity.Error => palette.Red("✖ error  "),
            _ when descriptor.Category == DiagnosticCategory.ShopifyDeviation => palette.Magenta("≠ differs"),
            _ => palette.Yellow("⚠ warning"),
        };
    }

    private static string CategoryTitle(DiagnosticCategory category)
    {
        return category switch
        {
            DiagnosticCategory.Syntax => "Syntax and tags (the map does not parse)",
            DiagnosticCategory.Filters => "Filters (skipped or misused filters)",
            DiagnosticCategory.ValuesAndPaths => "Values and paths (empty or wrong values, without an error)",
            DiagnosticCategory.Conditions => "Conditions and loops (silently wrong decisions)",
            DiagnosticCategory.Input => "Input",
            DiagnosticCategory.Output => "Output",
            _ => "Differences from Shopify Liquid (found while rendering with the input)",
        };
    }
}
