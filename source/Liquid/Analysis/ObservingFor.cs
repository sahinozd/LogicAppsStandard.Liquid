using DotLiquid;
using DotLiquid.Tags;

namespace LogicApps.Liquid.Analysis;

/// <summary>
/// The <c>for</c> tag of the observing render: reports a loop over a single object, then renders exactly like <c>for</c>.
/// </summary>
internal sealed class ObservingFor : For
{
    private string _collection = string.Empty;
    private int _line;
    private int _column;

    /// <inheritdoc />
    public override void Initialize(string tagName, string markup, List<string> tokens)
    {
        string clean;
        (clean, _line, _column, _collection) = ObservedLoopMarkup.Parse(markup);
        base.Initialize(tagName, clean, tokens);
    }

    /// <inheritdoc />
    public override void Render(Context context, TextWriter result)
    {
        ObservedLoopMarkup.InspectCollection(context, "for", _collection, _line, _column);
        base.Render(context, result);
    }
}
