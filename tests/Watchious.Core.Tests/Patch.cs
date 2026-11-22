using Watchious.Protocol;

namespace Watchious.Core.Tests;

public sealed record Patch(
    PatchOperation Operation,
    uint NodeId,
    uint ChildId,
    int Index,
    int ToIndex,
    string? Name,
    string? Value,
    ulong EventHandlerId)
{
    public static List<Patch> ReadAll(ReadOnlySpan<byte> buffer)
    {
        var patches = new List<Patch>();
        var reader = new PatchReader(buffer);
        while (reader.Read())
        {
            patches.Add(new Patch(
                reader.Operation,
                reader.NodeId,
                reader.ChildId,
                reader.Index,
                reader.ToIndex,
                reader.Name,
                reader.Value,
                reader.EventHandlerId));
        }

        return patches;
    }

    public override string ToString()
    {
        return Operation switch
        {
            PatchOperation.CreateElement => $"CreateElement {NodeId} {Name}",
            PatchOperation.CreateText => $"CreateText {NodeId} \"{Value}\"",
            PatchOperation.SetAttribute => $"SetAttribute {NodeId} {Name}={Value}",
            PatchOperation.RemoveAttribute => $"RemoveAttribute {NodeId} {Name}",
            PatchOperation.SetEvent => $"SetEvent {NodeId} {Name}",
            PatchOperation.RemoveEvent => $"RemoveEvent {NodeId} {Name}",
            PatchOperation.SetText => $"SetText {NodeId} \"{Value}\"",
            PatchOperation.InsertChild => $"InsertChild {NodeId}[{Index}] {ChildId}",
            PatchOperation.RemoveChild => $"RemoveChild {NodeId}[{Index}]",
            PatchOperation.MoveChild => $"MoveChild {NodeId}[{Index}] to [{ToIndex}]",
            _ => $"{Operation}",
        };
    }
}
