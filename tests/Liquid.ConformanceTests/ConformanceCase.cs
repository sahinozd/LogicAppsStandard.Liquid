using LogicApps.Liquid.Enums;

namespace LogicApps.Liquid.ConformanceTests;

/// <summary>
/// A map, an input and a kind that are run through both the real Logic Apps engine and the tool.
/// </summary>
public sealed record ConformanceCase
{
    /// <summary>
    /// Creates a conformance case.
    /// </summary>
    /// <param name="name">Name shown in the test results.</param>
    /// <param name="map">The Liquid map.</param>
    /// <param name="input">The input message.</param>
    /// <param name="kind">The transform kind.</param>
    public ConformanceCase(string name, string map, string input, LiquidTransformKind kind)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Map = map ?? throw new ArgumentNullException(nameof(map));
        Input = input ?? throw new ArgumentNullException(nameof(input));
        Kind = kind;
    }

    /// <summary>Name shown in the test results.</summary>
    public string Name { get; }

    /// <summary>The Liquid map.</summary>
    public string Map { get; }

    /// <summary>The input message.</summary>
    public string Input { get; }

    /// <summary>The transform kind.</summary>
    public LiquidTransformKind Kind { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }
}
