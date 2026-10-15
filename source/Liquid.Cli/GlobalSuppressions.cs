using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "The command line output is English only and is parsed by Visual Studio, VS Code and Azure DevOps; localizing it would break the MSBuild-style diagnostic format.", Scope = "namespaceanddescendants", Target = "~N:LogicApps.Liquid.Cli")]
