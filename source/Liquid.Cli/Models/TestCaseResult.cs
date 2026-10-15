namespace LogicApps.Liquid.Cli.Models;

/// <summary>
/// The outcome of one test case.
/// </summary>
internal sealed record TestCaseResult
{
    /// <summary>
    /// Creates a test case result.
    /// </summary>
    /// <param name="name">The test name.</param>
    /// <param name="mapPath">The Liquid map that was tested.</param>
    /// <param name="failures">The failure messages; empty when the test passed.</param>
    /// <param name="duration">How long the test took.</param>
    public TestCaseResult(string name, string mapPath, IReadOnlyList<string> failures, TimeSpan duration)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        MapPath = mapPath ?? throw new ArgumentNullException(nameof(mapPath));
        Failures = failures ?? throw new ArgumentNullException(nameof(failures));
        Duration = duration;
    }

    /// <summary>The test name.</summary>
    public string Name { get; }

    /// <summary>The Liquid map that was tested.</summary>
    public string MapPath { get; }

    /// <summary>The failure messages; empty when the test passed.</summary>
    public IReadOnlyList<string> Failures { get; }

    /// <summary>How long the test took.</summary>
    public TimeSpan Duration { get; }

    /// <summary>True when there are no failures.</summary>
    public bool Passed
    {
        get
        {
            return Failures.Count == 0;
        }
    }
}
