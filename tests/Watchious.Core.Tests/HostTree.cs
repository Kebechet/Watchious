using Watchious.Protocol;

namespace Watchious.Core.Tests;

// Applies patches the way the native hosts do, and fails on anything a host couldn't apply.
public sealed class HostTree
{
    private readonly Dictionary<uint, UiNode> _nodes = [];

    public HostTree()
    {
        _nodes.Add(PatchProtocol.RootNodeId, Root);
    }

    public UiNode Root { get; } = new(PatchProtocol.RootNodeId, "#root");

    public int NodeCount => _nodes.Count;

    public void Apply(IEnumerable<Patch> patches)
    {
        foreach (var patch in patches)
        {
            Apply(patch);
        }
    }

    private void Apply(Patch patch)
    {
        switch (patch.Operation)
        {
            case PatchOperation.CreateElement:
                _nodes.Add(patch.NodeId, new UiNode(patch.NodeId, patch.Name!));
                break;
            case PatchOperation.CreateText:
                _nodes.Add(patch.NodeId, new UiNode(patch.NodeId, UiNode.TextType) { Text = patch.Value });
                break;
            case PatchOperation.SetAttribute:
                Node(patch.NodeId).Attributes[patch.Name!] = patch.Value;
                break;
            case PatchOperation.RemoveAttribute:
                Require(Node(patch.NodeId).Attributes.Remove(patch.Name!), patch);
                break;
            case PatchOperation.SetEvent:
                Node(patch.NodeId).Events[patch.Name!] = patch.EventHandlerId;
                break;
            case PatchOperation.RemoveEvent:
                Require(Node(patch.NodeId).Events.Remove(patch.Name!), patch);
                break;
            case PatchOperation.SetText:
                var textNode = Node(patch.NodeId);
                Require(textNode.Type == UiNode.TextType, patch);
                textNode.Text = patch.Value;
                break;
            case PatchOperation.InsertChild:
                var parent = Node(patch.NodeId);
                var child = Node(patch.ChildId);
                Require(child.Parent is null && patch.Index >= 0 && patch.Index <= parent.Children.Count, patch);
                parent.Children.Insert(patch.Index, child);
                child.Parent = parent;
                break;
            case PatchOperation.RemoveChild:
                var removed = ChildAt(patch.NodeId, patch.Index, patch);
                removed.Parent!.Children.RemoveAt(patch.Index);
                Forget(removed);
                break;
            case PatchOperation.MoveChild:
                var moved = ChildAt(patch.NodeId, patch.Index, patch);
                var siblings = moved.Parent!.Children;
                siblings.RemoveAt(patch.Index);
                Require(patch.ToIndex >= 0 && patch.ToIndex <= siblings.Count, patch);
                siblings.Insert(patch.ToIndex, moved);
                break;
            default:
                throw new InvalidOperationException($"Unknown operation in {patch}.");
        }
    }

    private void Forget(UiNode node)
    {
        _nodes.Remove(node.Id);
        foreach (var child in node.Children)
        {
            Forget(child);
        }
    }

    private UiNode Node(uint id)
    {
        return _nodes.TryGetValue(id, out var node) ? node : throw new InvalidOperationException($"No node {id}.");
    }

    private UiNode ChildAt(uint parentId, int index, Patch patch)
    {
        var parent = Node(parentId);
        Require(index >= 0 && index < parent.Children.Count, patch);
        return parent.Children[index];
    }

    private static void Require(bool condition, Patch patch)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"A host can't apply {patch}.");
        }
    }
}
