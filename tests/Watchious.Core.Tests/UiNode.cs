using System.Text.Json;

namespace Watchious.Core.Tests;

public sealed record UiNode(
    string Type,
    IReadOnlyDictionary<string, JsonElement> Attributes,
    IReadOnlyDictionary<string, ulong> Events,
    IReadOnlyList<UiNode> Children,
    string? Text)
{
    public static UiNode ParseRoot(string json)
    {
        using var document = JsonDocument.Parse(json);
        var children = document.RootElement.GetProperty("children").EnumerateArray().Select(Parse).ToList();
        return new UiNode("#root", new Dictionary<string, JsonElement>(), new Dictionary<string, ulong>(), children, null);
    }

    public string Attribute(string name)
    {
        return Attributes[name].ToString();
    }

    public UiNode Single(string type)
    {
        return Descendants().Single(node => node.Type == type);
    }

    public IEnumerable<UiNode> Descendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var descendant in child.Descendants())
            {
                yield return descendant;
            }
        }
    }

    private static UiNode Parse(JsonElement element)
    {
        var type = element.GetProperty("type").GetString()!;
        if (type == "#text")
        {
            return new UiNode(type, new Dictionary<string, JsonElement>(), new Dictionary<string, ulong>(), [], element.GetProperty("text").GetString());
        }

        var attributes = element.GetProperty("attributes").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone());
        var events = element.GetProperty("events").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetUInt64());
        var children = element.GetProperty("children").EnumerateArray().Select(Parse).ToList();
        return new UiNode(type, attributes, events, children, null);
    }
}
