using System.Collections;
using System.Xml.Linq;
using DotLiquid;

namespace LogicApps.Liquid.Runtime;

/// <summary>
/// Offers XML input to a template, as specified in docs/runtime-behaviour.md (XML input): lookups by local name,
/// case-insensitive, first match only; elements without child elements become their text; attributes are not available.
/// </summary>
internal sealed class XmlInputDrop : IIndexable, ILiquidizable, IEnumerable<XmlInputDrop>
{
    private readonly XElement _element;

    /// <summary>
    /// Wraps an XML element.
    /// </summary>
    /// <param name="element">The element.</param>
    internal XmlInputDrop(XElement element)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
    }

    /// <summary>The local name of the element, used by <c>JSONArrayFor ... where Name</c>.</summary>
    public string Name => _element.Name.LocalName;

    public object? this[object key]
    {
        get
        {
            var name = (string)key;
            if (!_element.HasElements)
            {
                return null;
            }

            if (IsNamed(_element, name))
            {
                return this;
            }

            var match = FindChild(name);
            return match is null ? null : ToTemplateValue(match);
        }
    }

    public bool ContainsKey(object key)
    {
        var name = (string)key;
        return _element.HasElements && (IsNamed(_element, name) || FindChild(name) is not null);
    }

    public object ToLiquid()
    {
        return ToTemplateValue(_element);
    }

    // Looping over an element in the template gives the template values of its child elements.
    public IEnumerator GetEnumerator()
    {
        foreach (var child in _element.Elements())
        {
            yield return ToTemplateValue(child);
        }
    }

    // JSONArrayFor needs the child elements themselves, to filter them on their name.
    IEnumerator<XmlInputDrop> IEnumerable<XmlInputDrop>.GetEnumerator()
    {
        foreach (var child in _element.Elements())
        {
            yield return new XmlInputDrop(child);
        }
    }

    public override string ToString()
    {
        return Name;
    }

    private static object ToTemplateValue(XElement element)
    {
        return element.HasElements ? new XmlInputDrop(element) : element.Value;
    }

    private static bool IsNamed(XElement element, string name)
    {
        return string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);
    }

    private XElement? FindChild(string name)
    {
        foreach (var child in _element.Elements())
        {
            if (IsNamed(child, name))
            {
                return child;
            }
        }

        return null;
    }
}
