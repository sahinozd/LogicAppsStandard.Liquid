using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Liquid tag names are lowercase by definition; the lowercase form is used to suggest the correct tag name for a wrongly cased tag.", Scope = "member", Target = "~M:LogicApps.Liquid.Linting.LiquidLanguage.SuggestTag(System.String)~System.String")]
[assembly: SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Reproduces Ruby's String#capitalize, which lowercases everything after the first character, to show the Shopify Liquid result.", Scope = "member", Target = "~M:LogicApps.Liquid.Analysis.ShopifyReference.Capitalize(System.String)~System.String")]
