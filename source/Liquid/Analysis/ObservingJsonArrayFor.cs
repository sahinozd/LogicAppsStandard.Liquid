using DotLiquid;
using LogicApps.Liquid.Constants;
using LogicApps.Liquid.Runtime;

namespace LogicApps.Liquid.Analysis;

/// <summary>
/// The <c>JSONArrayFor</c> tag of the observing render: reports a loop over a single object, then renders exactly like <c>JSONArrayFor</c>.
/// </summary>
internal sealed class ObservingJsonArrayFor : JsonArrayFor
{
    private string _collection = string.Empty;
    private int _line;
    private int _column;

    /// <inheritdoc />
    public override void Initialize(string tagName, string markup, List<string> tokens)
    {
        (var clean, _line, _column, _collection) = ObservedLoopMarkup.Parse(markup);
        base.Initialize(tagName, clean, tokens);
    }

    /// <inheritdoc />
    public override void Render(Context context, TextWriter result)
    {
        ObservedLoopMarkup.InspectCollection(context, RuntimeConstants.JsonArrayForTagName, _collection, _line, _column);
        base.Render(context, result);
    }
}
