namespace LogicApps.Liquid.Enums;

/// <summary>
/// Enum representing the kind of a piece of a Liquid template: literal text, an output (<c>{{ }}</c>) or a tag (<c>{% %}</c>).
/// </summary>
public enum LiquidTokenKind
{
    Text,
    Output,
    Tag
}
