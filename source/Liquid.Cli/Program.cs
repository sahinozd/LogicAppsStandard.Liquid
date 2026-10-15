using System.CommandLine;
using System.Text;
using LogicApps.Liquid.Cli.Commands;
using LogicApps.Liquid.Constants;

// The pretty output uses symbols such as ✗ and →; without this the Windows console shows them as '?'.
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var root = new RootCommand(
    $"Validate and run Liquid maps exactly like the Azure Logic Apps Standard Liquid action " +
    $"(DotLiquid {RuntimeConstants.DotLiquidVersion}, verified against workflow extension bundle {RuntimeConstants.VerifiedExtensionBundleVersion}).")
{
    ValidateCommand.Create(),
    TransformCommand.Create(),
    TestCommand.Create(),
    ExplainCommand.Create(),
};

return root.Parse(args).Invoke();
