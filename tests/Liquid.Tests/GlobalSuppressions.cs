using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test methods use underscores to clearly separate scenario, condition and expected result (Arrange/Act/Assert). This improves test discoverability and readability for the team.", Scope = "namespaceanddescendants", Target = "~N:LogicApps.Liquid.Tests")]
[assembly: SuppressMessage("Naming", "CA1515:Consider making public types internal", Justification = "NUnit requires test fixture classes to be public so the test runner can instantiate and run them; making them internal would prevent discovery without additional test runner configuration.", Scope = "namespaceanddescendants", Target = "~N:LogicApps.Liquid.Tests")]
