using LogicApps.Liquid.Enums;

namespace LogicApps.Liquid.ConformanceTests;

/// <summary>
/// The cases compared against the real engine: one per known quirk, plus every sample map in the repository.
/// </summary>
public static class ConformanceCases
{
    private const string Person = """{"name":"sahin","n":3,"s":"4","f":1.10,"big":12345678901234567890,"b":true,"nul":null,"d":"2026-10-15T10:00:00Z","arr":[1,2,3],"items":[{"id":"a","v":2},{"id":"b","v":1}],"words":["banana","Apple","apple"],"ml":"a\nb"}""";
    private const string Patterns = """{"attr":{"@code":"D","code":"WRONG"},"env":{"$content":"B64","content":"WRONG"},"x":{"#text":"T","text":"WRONG","ns0:Bal":"NS"},"one":{"id":"A"},"dups":["x","x","y"],"arr":[1,2,3],"num":"123","t":"it's","q":"say \"hi\" \\ end"}""";
    private const string Orders ="""<n:Orders xmlns:n="urn:x"><n:Customer>Contoso</n:Customer><n:Order><n:Id>A</n:Id></n:Order><n:Order><n:Id>B</n:Id></n:Order></n:Orders>""";

    /// <summary>
    /// Gets all conformance cases.
    /// </summary>
    /// <returns>The probes and the samples.</returns>
    public static IEnumerable<ConformanceCase> All()
    {
        return Probes().Concat(Samples());
    }

    private static IEnumerable<ConformanceCase> Probes()
    {
        yield return Text("C# filter names", "{{ content.name | Upcase }}|{{ content.name | Capitalize }}|{{ content.name | Size }}");
        yield return Text("Ruby filter names are skipped", "{{ content.name | upcase }}|{{ content.name | strip_html }}");
        yield return Text("Unknown filter passes value through", "{% assign x = content.items | id: 'b' %}{{ x[0].id }}");
        yield return Text("JSON null is empty, not nil", "[{{ content.nul }}]{% if content.nul == nil %}nil{% else %}not nil{% endif %}{% if content.nul == '' %}empty{% endif %}");
        yield return Text("Missing property is nil", "[{{ content.missing }}]{% if content.missing == nil %}nil{% endif %}");
        yield return Text("Undefined variable renders empty", "[{{ nope }}][{{ content.a.b.c }}]");
        yield return Text("Dates are strings", "{{ content.d }}|{{ content.d | Date: 'yyyyMM' }}|{{ 'now' | Date: 'yyyy' | Size }}");
        yield return Text("Numbers", "{{ content.n | Times: 2 }}|{{ content.s | Times: 1.0 }}|{{ content.f }}|{{ content.f | Plus: 1 }}|{{ 1.5 | Times: 2 }}|{{ 10 | DividedBy: 4 }}|{{ 10.0 | DividedBy: 4 }}|{{ content.f | Round: 0 }}");
        yield return Text("Big integer", "{{ content.big }}");
        yield return Text("Booleans", "{{ content.b }}|{% if content.b %}yes{% endif %}");
        yield return Text("Arrays", "{{ content.arr }}|{{ content.arr | Join: ',' }}|{{ content.arr | First }}|{{ content.arr.size }}|{{ content.items | Map: 'id' | Join: '-' }}");
        yield return Text("JSONArrayFor", "[{% JSONArrayFor x in content.arr %}{{ x }}{% endJSONArrayFor %}]");
        yield return Text("JSONArrayFor with line breaks", "[\n{%- JSONArrayFor i in content.items %}\n  {\"id\": \"{{ i.id }}\"}\n{%- endJSONArrayFor %}\n]");
        yield return Text("elseif and elsif", "{% if content.n == 1 %}a{% elseif content.n == 3 %}b{% else %}c{% endif %}{% if false %}{% elsif true %}d{% endif %}");
        yield return Text("Render error is written into output", "before {% include 'x' %} after");
        yield return Text("Wrong filter arguments", "{{ content.name | Truncate: 'abc' }}");
        yield return Text("Unknown tag fails to parse", "{% If content.b %}x{% endif %}");
        yield return Text("Unclosed block fails to parse", "{% for x in content.arr %}{{ x }}");
        yield return Text("Comment body is parsed", "{% comment %}{% foo %}{% endcomment %}");
        yield return Text("Whitespace control", "a  {{- content.name -}}  b\n{%- assign x = 1 -%}\n c");
        yield return Text("Deviation: text arithmetic", "{{ content.s | Plus: 10 }}|{{ content.d | Date: 'yyyy' | Plus: 10 }}|{{ content.s | Times: 3 }}|{{ content.s | Times: 1.0 | Append: '' }}|{{ content.s | Times: 1.0 | Round | Plus: 10 }}");
        yield return Text("Deviation: text minus fails", "{{ content.s | Minus: 1 }}");
        yield return Text("Deviation: rounding and integer math", "{{ 2.5 | Round }}|{{ 3.5 | Round }}|{{ -7 | DividedBy: 2 }}|{{ -7 | Modulo: 3 }}|{{ 1.5 | DividedBy: 0 }}|{{ 4 | Times: 1.00 | Append: '' }}");
        yield return Text("Deviation: text filters", "{{ '1.234.5' | Replace: '.', ',' }}|{{ 'a.b.c' | RemoveFirst: '.' }}|{{ 'a,,b' | Split: ',' | Size }}|{{ 'abc' | Split: '' | Size }}|[{{ content.missing | Append: 'x' }}]|{{ 'hELLO wORLD' | Capitalize }}|{{ 'café & <' | Escape }}");
        yield return Text("Deviation: default", "{{ content.b | Default: 'x' }}|[{{ '  ' | Default: 'x' }}]|{{ false | Default: 'x' }}");
        yield return Text("Deviation: sort", "{% assign s = content.items | Sort: 'v' %}{% for i in s %}{{ i.id }}{% endfor %}|{{ content.words | Sort | Join: ',' }}");
        yield return Text("Deviation: dates", "{{ '2026-10-15T23:30:00Z' | Date: 'yyyy-MM-dd HH:mm' }}|{{ '2026-10-15T23:30:00+02:00' | Date: 'HH:mm' }}|{{ '03/04/2026' | Date: 'yyyy-MM-dd' }}|{{ '2026-10-15' | Date: '%Y-%m-%d' }}|{{ 'x' | Date: 'yyyy' }}");
        yield return Text("Deviation: number filters", "{{ 'abc' | Abs }}|{{ '-5' | Abs }}|{{ 'abc' | AtLeast: 3 }}|{{ 12345 | Size }}|{{ 1.25 | AtLeast: 1 }}");
        // Full language coverage: every filter, tag, operator and literal of DotLiquid 2.0.361.
        yield return Text("Coverage: string filters", "{{ '  a b  ' | Strip }}|{{ '  a' | Lstrip }}|{{ 'a  ' | Rstrip }}|{{ 'ABC' | Downcase }}|{{ 'abc' | Slice: 1, 2 }}|{{ 'abc' | Slice: -1 }}|{{ 'b' | Prepend: 'a' }}|{{ 'a-b-a' | Remove: 'a' }}|{{ 'a.b.a' | ReplaceFirst: 'a', 'x' }}|{{ '<p>x</p>' | StripHtml }}|{{ content.ml | StripNewlines }}|{{ content.ml | NewlineToBr }}|{{ 'one two three four' | TruncateWords: 2 }}|{{ 'a b&c' | UrlEncode }}|{{ 'a+b%26c' | UrlDecode }}|{{ '<b>' | H }}|{{ 'abcdef' | Truncate: 4 }}");
        yield return Text("Coverage: number and array filters", "{{ 5 | AtMost: 3 }}|{{ 5 | AtLeast: 7 }}|{{ content.arr | Last }}|{{ content.arr | First }}|{{ 1234.5 | Currency }}|{{ content.arr | Size }}|{{ content.name | Size }}|{{ content.items | Map: 'id' | Join: '+' }}|{{ content.arr | Sort | Join: '' }}");
        yield return Text("Coverage: tags", "{% capture c %}x{{ content.n }}{% endcapture %}{{ c }}|{% case content.n %}{% when 1 %}one{% when 3 %}three{% else %}other{% endcase %}|{% for i in content.arr %}{% cycle 'a', 'b' %}{% endfor %}|{% for i in content.arr %}{% if i == 2 %}{% continue %}{% endif %}{% if i == 3 %}{% break %}{% endif %}{{ i }}{% endfor %}|{% unless content.b %}no{% else %}yes{% endunless %}|{% raw %}{{ not rendered }}{% endraw %}|{% for i in content.arr %}{% ifchanged %}{{ i | DividedBy: 2 }}{% endifchanged %}{% endfor %}|{% tablerow i in content.arr cols: 2 %}{{ i }}{% endtablerow %}");
        yield return Text("Coverage: literal tag", "{% literal %}{{ x }}{% endliteral %}");
        yield return Text("Coverage: tags spanning lines fail to parse", "{% assign x =\n content.n %}{{ x }}");
        yield return Text("Coverage: whitespace control removes spaces on the same line only", "a\n  {%- assign x = 1 -%}  \n  b\n{{- content.n -}}\nc");
        yield return Text("Coverage: block and extends need a file system", "{% extends 'base' %}{% block body %}x{% endblock %}");
        yield return Text("Coverage: operators", "{% if content.n != 2 %}a{% endif %}{% if content.n <> 2 %}b{% endif %}{% if content.n < 4 %}c{% endif %}{% if content.n <= 3 %}d{% endif %}{% if content.n >= 3 %}e{% endif %}{% if content.name contains 'ah' %}f{% endif %}{% if content.arr contains 2 %}g{% endif %}{% if content.name startsWith 'sa' %}h{% endif %}{% if content.name endsWith 'in' %}i{% endif %}{% if content.items[0] hasKey 'id' %}j{% endif %}{% if content.items[0] hasValue 'a' %}k{% endif %}{% if content.name startswith 'sa' %}l{% endif %}");
        yield return Text("Coverage: literals and loop options", "{% if content.nul == blank %}a{% endif %}{% if content.missing == empty %}b{% endif %}{% if content.arr != empty %}c{% endif %}|{% for i in content.arr reversed %}{{ i }}{% endfor %}|{% for i in content.arr limit: 2 offset: 1 %}{{ i }}{% endfor %}|{% for i in (1..3) %}{{ i }}{% endfor %}|{% for i in content.arr %}{{ forloop.index }}{{ forloop.index0 }}{{ forloop.rindex }}{{ forloop.rindex0 }}{{ forloop.first }}{{ forloop.last }}{{ forloop.length }};{% endfor %}|{{ content.arr.size }}{{ content.arr.first }}{{ content.arr.last }}{{ content.name.size }}");
        // Scenario matrix: input and output shapes for the four transform kinds.
        yield return new ConformanceCase("Scenario JsonToText: root array input", "{% for x in content %}{{ x.id }};{% endfor %}{{ content.size }}", """[{"id":1},{"id":2}]""", LiquidTransformKind.JsonToText);
        yield return new ConformanceCase("Scenario JsonToText: root string input", "[{{ content }}]", "\"abc\"", LiquidTransformKind.JsonToText);
        yield return new ConformanceCase("Scenario JsonToText: root number input", "[{{ content | Plus: 1 }}]", "41", LiquidTransformKind.JsonToText);
        yield return new ConformanceCase("Scenario JsonToText: empty object input", "[{{ content.a }}]", "{}", LiquidTransformKind.JsonToText);
        yield return new ConformanceCase("Scenario JsonToText: unicode, escapes and numbers", "{{ content.u }}|{{ content.e }}|{{ content.exp }}|{{ content.neg }}|{{ content.small }}|{{ content.t }}|{{ content.deep.a.b.c.d.e.f }}", """{"u":"café 😀","e":"line1\nline2\ttab","exp":1e3,"neg":-0.0,"small":0.1,"t":true,"deep":{"a":{"b":{"c":{"d":{"e":{"f":"deep"}}}}}}}""", LiquidTransformKind.JsonToText);
        yield return new ConformanceCase("Scenario JsonToJson: root array output", "[{% JSONArrayFor x in content.arr %}{{ x }}{% endJSONArrayFor %}]", Person, LiquidTransformKind.JsonToJson);
        yield return new ConformanceCase("Scenario JsonToJson: plain value output", "\"{{ content.name }}\"", Person, LiquidTransformKind.JsonToJson);
        yield return new ConformanceCase("Scenario JsonToJson: number output", "{{ content.n }}", Person, LiquidTransformKind.JsonToJson);
        yield return new ConformanceCase("Scenario JsonToJson: empty output", "{% if false %}x{% endif %}", Person, LiquidTransformKind.JsonToJson);
        yield return new ConformanceCase("Scenario JsonToJson: trailing comma", """{"a": 1, "b": [1, 2,],}""", Person, LiquidTransformKind.JsonToJson);
        yield return new ConformanceCase("Scenario JsonToJson: comments in output", "{ /* note */ \"a\": 1 // end\n}", Person, LiquidTransformKind.JsonToJson);
        yield return new ConformanceCase("Scenario JsonToJson: duplicate keys", """{"a": 1, "a": 2}""", Person, LiquidTransformKind.JsonToJson);
        yield return new ConformanceCase("Scenario JsonToJson: single quotes and unquoted names", "{a: 'x'}", Person, LiquidTransformKind.JsonToJson);
        yield return new ConformanceCase("Scenario JsonToText: CSV with line endings", "id;name\r\n{% for i in content.items %}{{ i.id }};{{ i.v }}\r\n{% endfor %}", Person, LiquidTransformKind.JsonToText);
        yield return new ConformanceCase("Scenario XmlToText: attributes are not accessible", "[{{ content.a.id }}][{{ content.Root.a }}][{{ content.Root.b.id }}]", "<Root><a id=\"1\">x</a><b id=\"2\"><c>y</c></b></Root>", LiquidTransformKind.XmlToText);
        yield return new ConformanceCase("Scenario XmlToText: CDATA, mixed content and empty element", "[{{ content.Root.c }}][{{ content.Root.p }}][{{ content.Root.e }}][{{ content.Root.p.b }}]", "<Root><c><![CDATA[<x> & y]]></c><p>Hello <b>bold</b> world</p><e/></Root>", LiquidTransformKind.XmlToText);
        yield return new ConformanceCase("Scenario XmlToText: default namespace", "{{ content.Root.Name }}", "<Root xmlns=\"urn:default\"><Name>N</Name></Root>", LiquidTransformKind.XmlToText);
        yield return new ConformanceCase("Scenario XmlToText: XML declaration", "{{ content.Root.Name }}", "<?xml version=\"1.0\" encoding=\"utf-8\"?><Root><Name>N</Name></Root>", LiquidTransformKind.XmlToText);
        yield return new ConformanceCase("Scenario XmlToText: declaration with utf-16", "{{ content.Root.Name }}", "<?xml version=\"1.0\" encoding=\"utf-16\"?><Root><Name>N</Name></Root>", LiquidTransformKind.XmlToText);
        yield return new ConformanceCase("Scenario XmlToJson: single versus repeated element", "{\"one\": [{% JSONArrayFor x in content.Root.One %}\"{{ x }}\"{% endJSONArrayFor %}], \"many\": [{% JSONArrayFor x in content.Root.Many where Item %}\"{{ x }}\"{% endJSONArrayFor %}]}", "<Root><One><Item>a</Item></One><Many><Item>a</Item><Item>b</Item></Many></Root>", LiquidTransformKind.XmlToJson);
        yield return new ConformanceCase("Scenario XmlToText: invalid XML input", "x", "<Root><a></Root>", LiquidTransformKind.XmlToText);
        yield return new ConformanceCase("Scenario JsonToText: invalid JSON input", "x", "{\"a\": }", LiquidTransformKind.JsonToText);
        yield return new ConformanceCase("Pattern: objects in output and Abs as conversion", "json=[{{ content | Json }}] obj=[{{ content.obj }}] arr=[{{ content.arr }}] objs=[{{ content.objs }}] abs=[{{ content.neg | Abs | Times: 60 }}] abs-text=[{{ content.txt | Abs | Times: 60 }}] mm=[{{ '2026-08-01' | Date: 'MM' | Abs }}]", """{"obj":{"a":1},"arr":[1,2,3],"objs":[{"a":1},{"a":2}],"neg":"-1.5","txt":"n/a"}""", LiquidTransformKind.JsonToText);
        yield return new ConformanceCase("Pattern: XML element in output", "el=[{{ content.Root.Item }}] leaf=[{{ content.Root.Name }}]", "<Root><Name>N</Name><Item><A>1</A></Item></Root>", LiquidTransformKind.XmlToText);
        yield return new ConformanceCase("Pattern: special characters in dot paths", "{{ content.attr.@code }}|{{ content.attr[\"@code\"] }}|{{ content.env.$content }}|{{ content.env[\"$content\"] }}|{{ content.x.#text }}|{{ content.x[\"#text\"] }}|[{{ content.x.ns0:Bal }}]|{{ content.x[\"ns0:Bal\"] }}", Patterns, LiquidTransformKind.JsonToText);
        yield return new ConformanceCase("Pattern: single object versus array", "{% for x in content.one %}<{{ x.id }}>{% endfor %}|[{% JSONArrayFor x in content.one %}{{ x.id }}{% endJSONArrayFor %}]|{% assign w = content.one | Concat: content.one | Uniq %}{% for x in w %}<{{ x.id }}>{% endfor %}|{% assign c = content.one | Compact %}{% for x in c %}<{{ x.id }}>{% endfor %}|{{ content.dups | Uniq | Join: ',' }}|{{ content.dups | Compact | Join: ',' }}|[{{ content.one.first }}]", Patterns, LiquidTransformKind.JsonToText);
        yield return new ConformanceCase("Pattern: if inside JSONArrayFor", "{\"items\":[{% JSONArrayFor n in content.arr %}{% if n > 1 %}{{ n }}{% endif %}{% endJSONArrayFor %}]}", Patterns, LiquidTransformKind.JsonToJson);
        yield return new ConformanceCase("Pattern: conversion idioms and quotes", "{{ content.num | Plus: 0 }}|{{ content.num | Times: 1 }}|{{ content.num | Times: 1.0 | Round }}|{{ content.t | Replace: '''' ,'&apos;' }}|{{ content.t | Replace: \"'\", '&apos;' }}|{{ content.q | Replace: '\\\\', '\\\\' | Replace: '\"', '\\\"' }}", Patterns, LiquidTransformKind.JsonToText);
        yield return new ConformanceCase("JSON output is parsed", """{"f": {{ content.f }}, "n": {{ content.n }}, "s": "{{ content.d }}"}""", Person, LiquidTransformKind.JsonToJson);
        yield return new ConformanceCase("Invalid JSON output fails", """{"a": {{ content.name }}}""", Person, LiquidTransformKind.JsonToJson);
        yield return new ConformanceCase("XML lookup: namespaces and case ignored, first match", "{{ content.orders.customer }}|{{ content.Orders.Order.Id }}", Orders, LiquidTransformKind.XmlToText);
        yield return new ConformanceCase("XML JSONArrayFor where", "[{% JSONArrayFor o in content.Orders where Order %}\"{{ o.Id }}\"{% endJSONArrayFor %}]", Orders, LiquidTransformKind.XmlToJson);
        yield return new ConformanceCase("XML for loop", "{% for o in content.Orders %}[{{ o.Id }}]{% endfor %}", Orders, LiquidTransformKind.XmlToText);
    }

    private static IEnumerable<ConformanceCase> Samples()
    {
        var samples = Path.Combine(RepositoryRoot(), "samples");
        yield return Sample(samples, "orders.to.shipment.request.liquid", "orders.json", LiquidTransformKind.JsonToJson);
        yield return Sample(samples, "webshop.to.warehouse.xml.liquid", "webshop.json", LiquidTransformKind.JsonToText);
        yield return Sample(samples, "orders.xml.to.json.liquid", "orders.xml", LiquidTransformKind.XmlToJson);
        yield return Sample(samples, "common-mistakes.liquid", "common-mistakes.json", LiquidTransformKind.JsonToJson);
    }

    private static ConformanceCase Text(string name, string map)
    {
        return new ConformanceCase(name, map, Person, LiquidTransformKind.JsonToText);
    }

    private static ConformanceCase Sample(string samples, string map, string input, LiquidTransformKind kind)
    {
        return new ConformanceCase($"Sample {map}", File.ReadAllText(Path.Combine(samples, "maps", map)), File.ReadAllText(Path.Combine(samples, "inputs", input)), kind);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LogicAppsStandard.Liquid.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
