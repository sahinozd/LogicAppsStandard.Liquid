# Required runtime behaviour

This is the specification of how the Logic Apps Liquid action exposes its input to DotLiquid, and of the `JSONArrayFor` tag. It describes **observable behaviour only**: what a map sees and what it renders.

The classes `JsonInputDrop`, `XmlInputDrop` and `JsonArrayFor` are written from this specification. The conformance tests (`tests/Liquid.ConformanceTests`) verify every point against the real Logic Apps runtime.

## JSON input (`content` for JsonToJson and JsonToText)

The input is parsed as JSON without date parsing, and every JSON value is offered to the template as follows:

| JSON value | Value in the template |
|---|---|
| Whole number | 64-bit integer |
| Number with a fraction or exponent | `decimal` |
| `true` / `false` | boolean |
| String | text, unchanged (dates stay text) |
| `null` | empty text `""`, not `nil` |
| Array | a list of the converted items |
| Object | an object whose properties can be looked up by name |

Looking up properties of an object:
- **Existing property:** the converted value. A property with value `null` exists, so it gives `""`.
- **Missing property:** the property does not exist (`nil`). DotLiquid records it as "variable not found" and renders nothing.
- **Inside a list:** an object prints as `Microsoft.Azure.Function.Common.JObjectDrop`, the name of the runtime's wrapper type. On its own (`{{ object }}`) an object prints nothing.

## XML input (`content` for XmlToJson and XmlToText)

The input is loaded as XML from its UTF-8 bytes. Every element is offered to the template as follows:

- **Element with child elements:** an object.
  - Looking up a name compares it with the local name (namespace prefixes ignored), case-insensitively, against first the element itself and then its child elements.
  - The element itself gives the element again (so `content.Root` works on `<Root>`).
  - A child element gives the converted value of the **first** child with that name.
  - No match gives "does not exist".
- **Element without child elements:** its text. Looking up names on it gives nothing.
- **Attributes:** not available.
- **Looping over an element:** gives the converted values of its child elements, in document order.
- **Printing an element:** an element with child elements prints nothing. Inside a list, it prints its local name.

## The `JSONArrayFor` tag

Syntax: `{% JSONArrayFor item in collection %} ... {% endJSONArrayFor %}`, optionally followed by `where Name`. Any other syntax is a parse error with the message `Incorrect format for JSONArrayFor`.

- **Collection:**
  - An XML element: its child elements; with `where Name`, only the child elements with local name `Name`.
  - A list: its items; with `where Name`, only the items whose text equals `Name`.
  - Anything else (a single object, text, a number, a missing value): the tag renders nothing.
- **Items:** the body is rendered once per item, with `item` set in a new scope; `break` and `continue` work as in `for`.
- **Commas:** after every item except the last, a comma is added.
  - If the body ends with literal text, the comma goes before the last line break in that text, or at the end of that text when it has no line break.
  - If the body ends with an output or a tag, the comma is written after it.
