namespace LogicApps.Liquid.Models;

/// <summary>
/// A block tag (<c>if</c>, <c>for</c>, <c>comment</c>, ...) that has been opened but not yet closed while linting.
/// </summary>
public sealed record OpenBlock
{
    /// <summary>
    /// Creates an open block.
    /// </summary>
    /// <param name="name">The tag name.</param>
    /// <param name="token">The token that opened the block.</param>
    public OpenBlock(string name, LiquidToken token)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Token = token ?? throw new ArgumentNullException(nameof(token));
    }

    /// <summary>The tag name.</summary>
    public string Name { get; }

    /// <summary>The token that opened the block.</summary>
    public LiquidToken Token { get; }
}
