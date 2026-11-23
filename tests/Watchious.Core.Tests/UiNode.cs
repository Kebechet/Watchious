namespace Watchious.Core.Tests;

public sealed class UiNode(uint id, string type)
{
    public const string TextType = "#text";

    public uint Id { get; } = id;

    public string Type { get; } = type;

    public Dictionary<string, string?> Attributes { get; } = [];

    public Dictionary<string, ulong> Events { get; } = [];

    public List<UiNode> Children { get; } = [];

    public string? Text { get; set; }

    public UiNode? Parent { get; set; }

    public bool IsBlankText => Type == TextType && string.IsNullOrWhiteSpace(Text);

    // The hosts skip blank text in layouts, as a browser collapses whitespace between blocks.
    public IEnumerable<UiNode> VisibleChildren => Children.Where(child => !child.IsBlankText);

    // What the hosts show: a Label joins its text, other controls show their Text attribute.
    public string? Content => Type switch
    {
        TextType => Text,
        "Label" => string.Concat(Children.Where(child => child.Type == TextType).Select(child => child.Text)),
        _ => Attributes.GetValueOrDefault("Text"),
    };

    public string? Attribute(string name)
    {
        return Attributes[name];
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
}
