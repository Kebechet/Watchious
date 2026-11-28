using Microsoft.AspNetCore.Components.RenderTree;
using Watchious.Protocol;

namespace Watchious;

// Turns Blazor's render batches into patches (see PatchOperation) by mirroring what the native host holds.
// Blazor's edits address a node by its position among the frames of one component, but the host has only
// elements and text, and a component's nodes sit directly in the element around the component. This is the job
// Blazor's browser renderer does with its "logical elements": here the mirror keeps the components, and every
// edit is translated to the host position it lands on.
internal sealed class NativeTree
{
    private const string EventAttributePrefix = "on";

    private readonly ElementNode _root = new(PatchProtocol.RootNodeId, "");
    private readonly Dictionary<int, ComponentNode> _components = [];
    private readonly List<(int From, int To)> _permutation = [];
    // Every handler id the host may still send, by its element: a replaced one too, until Blazor disposes it.
    private readonly Dictionary<ulong, ElementNode> _eventElements = [];
    private uint _lastNodeId = PatchProtocol.RootNodeId;

    public void AddRootComponent(int componentId)
    {
        var component = new ComponentNode();
        Attach(_root, _root.Children.Count, component, writer: null);
        _components.Add(componentId, component);
    }

    // Whether the element of the event sits in an element of the given type that a later sibling of that type covers,
    // as a page under the top one of a navigation stack.
    public bool IsCovered(ulong eventHandlerId, string elementType)
    {
        Container? node = _eventElements.GetValueOrDefault(eventHandlerId);
        for (; node is not null; node = node.Parent)
        {
            if (node is ElementNode element && element.Type == elementType && HasLaterHostSibling(element))
            {
                return true;
            }
        }

        return false;
    }

    // Handler ids Blazor has disposed. Blazor keeps a replaced one working until the host shows the batch that replaced
    // it (WatchiousRenderer.UpdateDisplayAsync), and a stale tap from a covered page can carry such an id: a list row
    // whose OnClick is a new lambda after every render gets a new id, while the host still shows the old one.
    public void ForgetEventHandlers(IEnumerable<ulong> eventHandlerIds)
    {
        foreach (var eventHandlerId in eventHandlerIds)
        {
            _eventElements.Remove(eventHandlerId);
        }
    }

    public void Apply(in RenderBatch batch, PatchWriter writer)
    {
        var referenceFrames = batch.ReferenceFrames.Array;
        for (var index = 0; index < batch.UpdatedComponents.Count; index++)
        {
            ApplyDiff(in batch.UpdatedComponents.Array[index], referenceFrames, writer);
        }

        for (var index = 0; index < batch.DisposedComponentIDs.Count; index++)
        {
            _components.Remove(batch.DisposedComponentIDs.Array[index]);
        }
    }

    private void ApplyDiff(in RenderTreeDiff diff, RenderTreeFrame[] referenceFrames, PatchWriter writer)
    {
        if (!_components.TryGetValue(diff.ComponentId, out var component))
        {
            throw new InvalidOperationException($"Component {diff.ComponentId} rendered before its parent placed it.");
        }

        Container parent = component;
        var edits = diff.Edits;
        for (var index = 0; index < edits.Count; index++)
        {
            ref var edit = ref edits.Array[edits.Offset + index];
            switch (edit.Type)
            {
                case RenderTreeEditType.PrependFrame:
                    var node = CreateNode(referenceFrames, edit.ReferenceFrameIndex, writer);
                    Attach(parent, edit.SiblingIndex, node, writer);
                    break;
                case RenderTreeEditType.RemoveFrame:
                    Detach(parent, edit.SiblingIndex, writer);
                    break;
                case RenderTreeEditType.SetAttribute:
                    SetAttribute(ElementAt(parent, edit.SiblingIndex), referenceFrames[edit.ReferenceFrameIndex], writer);
                    break;
                case RenderTreeEditType.RemoveAttribute:
                    RemoveAttribute(ElementAt(parent, edit.SiblingIndex), edit.RemovedAttributeName!, writer);
                    break;
                case RenderTreeEditType.UpdateText:
                    writer.SetText(TextAt(parent, edit.SiblingIndex).Id, referenceFrames[edit.ReferenceFrameIndex].TextContent);
                    break;
                case RenderTreeEditType.UpdateMarkup:
                    writer.SetText(TextAt(parent, edit.SiblingIndex).Id, referenceFrames[edit.ReferenceFrameIndex].MarkupContent);
                    break;
                case RenderTreeEditType.StepIn:
                    parent = ElementAt(parent, edit.SiblingIndex);
                    break;
                case RenderTreeEditType.StepOut:
                    parent = parent.Parent ?? throw new InvalidOperationException("A render diff stepped out of the root.");
                    break;
                case RenderTreeEditType.PermutationListEntry:
                    _permutation.Add((edit.SiblingIndex, edit.MoveToSiblingIndex));
                    break;
                case RenderTreeEditType.PermutationListEnd:
                    Permute(parent, writer);
                    _permutation.Clear();
                    break;
                default:
                    throw new NotSupportedException($"Render tree edit {edit.Type} isn't supported.");
            }
        }
    }

    private Node CreateNode(RenderTreeFrame[] frames, int frameIndex, PatchWriter writer)
    {
        ref var frame = ref frames[frameIndex];
        switch (frame.FrameType)
        {
            case RenderTreeFrameType.Element:
                return CreateElement(frames, frameIndex, writer);
            case RenderTreeFrameType.Text:
                return CreateText(frame.TextContent, writer);
            case RenderTreeFrameType.Markup:
                return CreateText(frame.MarkupContent, writer);
            case RenderTreeFrameType.Component:
                // Its content arrives in the component's own diff, later in the same batch.
                var component = new ComponentNode();
                _components.Add(frame.ComponentId, component);
                return component;
            default:
                throw new InvalidOperationException($"A {frame.FrameType} frame can't be placed in the tree.");
        }
    }

    private ElementNode CreateElement(RenderTreeFrame[] frames, int elementIndex, PatchWriter writer)
    {
        ref var frame = ref frames[elementIndex];
        var element = new ElementNode(++_lastNodeId, frame.ElementName);
        writer.CreateElement(element.Id, frame.ElementName);

        var end = elementIndex + frame.ElementSubtreeLength;
        var index = elementIndex + 1;
        for (; index < end && frames[index].FrameType == RenderTreeFrameType.Attribute; index++)
        {
            SetAttribute(element, frames[index], writer);
        }

        AddChildren(element, frames, index, end, writer);
        return element;
    }

    private TextNode CreateText(string text, PatchWriter writer)
    {
        var node = new TextNode(++_lastNodeId);
        writer.CreateText(node.Id, text);
        return node;
    }

    // Children are attached while the element is still detached, so it reaches the host's tree complete.
    private void AddChildren(ElementNode element, RenderTreeFrame[] frames, int start, int end, PatchWriter writer)
    {
        var index = start;
        while (index < end)
        {
            ref var frame = ref frames[index];
            switch (frame.FrameType)
            {
                case RenderTreeFrameType.Region:
                    AddChildren(element, frames, index + 1, index + frame.RegionSubtreeLength, writer);
                    index += frame.RegionSubtreeLength;
                    break;
                case RenderTreeFrameType.Element:
                    Attach(element, element.Children.Count, CreateNode(frames, index, writer), writer);
                    index += frame.ElementSubtreeLength;
                    break;
                case RenderTreeFrameType.Component:
                    Attach(element, element.Children.Count, CreateNode(frames, index, writer), writer);
                    index += frame.ComponentSubtreeLength;
                    break;
                case RenderTreeFrameType.Text:
                case RenderTreeFrameType.Markup:
                    Attach(element, element.Children.Count, CreateNode(frames, index, writer), writer);
                    index++;
                    break;
                default:
                    index++;
                    break;
            }
        }
    }

    private static void Attach(Container parent, int childIndex, Node node, PatchWriter? writer)
    {
        parent.Children.Insert(childIndex, node);
        node.Parent = parent;
        if (writer is null)
        {
            return;
        }

        var (host, hostIndex) = HostPosition(parent, childIndex);
        foreach (var hostNodeId in HostNodeIds(node))
        {
            writer.InsertChild(host.Id, hostIndex++, hostNodeId);
        }
    }

    private static void Detach(Container parent, int childIndex, PatchWriter writer)
    {
        var node = parent.Children[childIndex];
        var (host, hostIndex) = HostPosition(parent, childIndex);
        for (var count = HostNodeCount(node); count > 0; count--)
        {
            writer.RemoveChild(host.Id, hostIndex);
        }

        parent.Children.RemoveAt(childIndex);
        node.Parent = null;
    }

    // A permutation moves each child from its old position to its new one in one step (new[to] = old[from]).
    private void Permute(Container parent, PatchWriter writer)
    {
        var children = parent.Children;
        var original = children.ToArray();
        foreach (var (from, to) in _permutation)
        {
            children[to] = original[from];
        }

        var (host, start) = HostPosition(parent, 0);
        var current = new List<uint>();
        foreach (var child in original)
        {
            current.AddRange(HostNodeIds(child));
        }

        var target = new List<uint>(current.Count);
        foreach (var child in children)
        {
            target.AddRange(HostNodeIds(child));
        }

        for (var index = 0; index < target.Count; index++)
        {
            if (current[index] == target[index])
            {
                continue;
            }

            var fromIndex = current.IndexOf(target[index], index + 1);
            writer.MoveChild(host.Id, start + fromIndex, start + index);
            current.RemoveAt(fromIndex);
            current.Insert(index, target[index]);
        }
    }

    private void SetAttribute(ElementNode element, in RenderTreeFrame attribute, PatchWriter writer)
    {
        var name = attribute.AttributeName;
        if (attribute.AttributeEventHandlerId != 0)
        {
            (element.EventAttributes ??= []).Add(name);
            _eventElements[attribute.AttributeEventHandlerId] = element;
            writer.SetEvent(element.Id, ToEventName(name), attribute.AttributeEventHandlerId);
            return;
        }

        if (element.EventAttributes is not null && element.EventAttributes.Remove(name))
        {
            writer.RemoveEvent(element.Id, ToEventName(name));
        }

        writer.SetAttribute(element.Id, name, ToProtocolValue(attribute.AttributeValue));
    }

    private static void RemoveAttribute(ElementNode element, string name, PatchWriter writer)
    {
        if (element.EventAttributes is not null && element.EventAttributes.Remove(name))
        {
            writer.RemoveEvent(element.Id, ToEventName(name));
            return;
        }

        writer.RemoveAttribute(element.Id, name);
    }

    private static string ToEventName(string attributeName)
    {
        return attributeName.StartsWith(EventAttributePrefix, StringComparison.Ordinal)
            ? attributeName[EventAttributePrefix.Length..]
            : attributeName;
    }

    // Blazor turns element attribute values into strings, except a true boolean. Controls format numbers and
    // booleans culture-invariant themselves before handing them over.
    private static string? ToProtocolValue(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            bool boolean => boolean ? "true" : "false",
            _ => value.ToString(),
        };
    }

    // Where a container's child lands in the host: in the element itself, or for a component, in the nearest
    // element around it, after the host nodes of everything before the component.
    private static (ElementNode Host, int Index) HostPosition(Container container, int childIndex)
    {
        var index = 0;
        for (var sibling = 0; sibling < childIndex; sibling++)
        {
            index += HostNodeCount(container.Children[sibling]);
        }

        while (container is ComponentNode)
        {
            var parent = container.Parent ?? throw new InvalidOperationException("A component isn't in the tree.");
            var position = parent.Children.IndexOf(container);
            for (var sibling = 0; sibling < position; sibling++)
            {
                index += HostNodeCount(parent.Children[sibling]);
            }

            container = parent;
        }

        return ((ElementNode)container, index);
    }

    private static int HostNodeCount(Node node)
    {
        switch (node)
        {
            case ComponentNode component:
                var count = 0;
                foreach (var child in component.Children)
                {
                    count += HostNodeCount(child);
                }

                return count;
            default:
                return 1;
        }
    }

    private static IEnumerable<uint> HostNodeIds(Node node)
    {
        switch (node)
        {
            case ElementNode element:
                yield return element.Id;
                break;
            case TextNode text:
                yield return text.Id;
                break;
            case ComponentNode component:
                foreach (var child in component.Children)
                {
                    foreach (var id in HostNodeIds(child))
                    {
                        yield return id;
                    }
                }

                break;
        }
    }

    private static ElementNode ElementAt(Container parent, int childIndex)
    {
        return parent.Children[childIndex] as ElementNode
            ?? throw new InvalidOperationException($"Child {childIndex} of a render diff isn't an element.");
    }

    // Among the nodes the host has in the same element, as the hosts see them: a component's nodes sit directly in the
    // element around it, so a page wrapped in a component still has the other pages as siblings.
    private static bool HasLaterHostSibling(ElementNode element)
    {
        var host = element.Parent;
        while (host is ComponentNode)
        {
            host = host.Parent;
        }

        if (host is null)
        {
            return false;
        }

        var isAfterElement = false;
        foreach (var node in HostChildren(host))
        {
            if (ReferenceEquals(node, element))
            {
                isAfterElement = true;
            }
            else if (isAfterElement && node is ElementNode sibling && sibling.Type == element.Type)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Node> HostChildren(Container container)
    {
        foreach (var child in container.Children)
        {
            if (child is ComponentNode component)
            {
                foreach (var node in HostChildren(component))
                {
                    yield return node;
                }
            }
            else
            {
                yield return child;
            }
        }
    }

    private static TextNode TextAt(Container parent, int childIndex)
    {
        return parent.Children[childIndex] as TextNode
            ?? throw new InvalidOperationException($"Child {childIndex} of a render diff isn't text.");
    }

    private abstract class Node
    {
        public Container? Parent { get; set; }
    }

    private abstract class Container : Node
    {
        public List<Node> Children { get; } = [];
    }

    private sealed class ElementNode(uint id, string type) : Container
    {
        public uint Id { get; } = id;

        public string Type { get; } = type;

        public HashSet<string>? EventAttributes { get; set; }
    }

    private sealed class ComponentNode : Container;

    private sealed class TextNode(uint id) : Node
    {
        public uint Id { get; } = id;
    }
}
