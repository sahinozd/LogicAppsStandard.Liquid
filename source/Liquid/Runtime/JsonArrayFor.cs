using System.Text.RegularExpressions;
using DotLiquid;
using DotLiquid.Exceptions;

namespace LogicApps.Liquid.Runtime;

/// <summary>
/// The <c>{% JSONArrayFor item in collection [where Name] %}...{% endJSONArrayFor %}</c> block tag of Logic Apps,
/// as specified in docs/runtime-behaviour.md (The JSONArrayFor tag): a loop that writes a comma between the items.
/// </summary>
public class JsonArrayFor : Block
{
    private const string Separator = ",";

    private static readonly Regex InternalMarkup = new($@"(?<item>\w+)\s+in\s+(?<collection>(?:{DotLiquid.Liquid.QuotedFragment})+)(?:\s+where\s+(?<name>\w+))?");

    private string _item = string.Empty;
    private string _collection = string.Empty;
    private string? _onlyNamed;

    /// <inheritdoc />
    public override void Initialize(string tagName, string markup, List<string> tokens)
    {
        var match = InternalMarkup.Match(markup);
        if (!match.Success)
        {
            throw new SyntaxException("Incorrect format for JSONArrayFor");
        }

        _item = match.Groups["item"].Value;
        _collection = match.Groups["collection"].Value;
        _onlyNamed = match.Groups["name"].Success ? match.Groups["name"].Value : null;

        base.Initialize(tagName, markup, tokens);
    }

    /// <inheritdoc />
    public override void Render(Context context, TextWriter result)
    {
        ArgumentNullException.ThrowIfNull(context);

        var items = SelectItems(context[_collection]);
        if (items is null)
        {
            return;
        }

        var bodyWithSeparator = AppendSeparator(NodeList);
        context.Stack(() =>
        {
            for (var index = 0; index < items.Count; index++)
            {
                context[_item] = items[index];
                var isLast = index == items.Count - 1;
                try
                {
                    RenderAll(isLast ? NodeList : bodyWithSeparator, context, result);
                }
                catch (BreakInterrupt)
                {
                    break;
                }
                catch (ContinueInterrupt)
                {
                    // Continue with the next item.
                }
            }
        });
    }

    // XML: the child elements, optionally only those with a given name. Lists: the items, optionally only those
    // whose text equals the name. Anything else gives no items.
    private List<object>? SelectItems(object? collection)
    {
        if (collection is IEnumerable<XmlInputDrop> elements)
        {
            return elements.Where(element => _onlyNamed is null || element.Name == _onlyNamed).Cast<object>().ToList();
        }

        if (collection is IEnumerable<object> values)
        {
            return values.Where(value => _onlyNamed is null || value.ToString() == _onlyNamed).ToList();
        }

        return null;
    }

    // The separator goes before the last line break of trailing literal text, or after the last output or tag.
    private static List<object> AppendSeparator(List<object> body)
    {
        var result = new List<object>(body);
        if (result.Count == 0)
        {
            return result;
        }

        if (result[^1] is not string trailingText)
        {
            result.Add(Separator);
            return result;
        }

        var lastLineBreak = trailingText.LastIndexOf('\n');
        result[^1] = lastLineBreak < 0 ? trailingText + Separator : trailingText.Insert(lastLineBreak, Separator);
        return result;
    }
}
