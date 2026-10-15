using System.CommandLine;
using LogicApps.Liquid.Cli.Commands;
using LogicApps.Liquid.Constants;

var root = new RootCommand(
    $"Validate and run Liquid maps exactly like the Azure Logic Apps Standard Liquid action " +
    $"(DotLiquid {RuntimeConstants.DotLiquidVersion}, verified against workflow extension bundle {RuntimeConstants.VerifiedExtensionBundleVersion}).")
{
    ValidateCommand.Create(),
    TransformCommand.Create(),
    TestCommand.Create(),
};

return root.Parse(args).Invoke();
