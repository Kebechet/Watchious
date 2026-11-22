namespace Watchious.Protocol;

// Protocol v2: after each render batch the renderer sends the native host one buffer of patches, little-endian:
//
//   int32 version (PatchProtocol.Version), then operations until the end of the buffer, each a byte
//   PatchOperation followed by its fields:
//
//   CreateElement    u32 id, string type
//   CreateText       u32 id, string text
//   SetAttribute     u32 id, string name, string? value
//   RemoveAttribute  u32 id, string name
//   SetEvent         u32 id, string event, u64 eventHandlerId   (replaces the event's previous handler)
//   RemoveEvent      u32 id, string event
//   SetText          u32 id, string text
//   InsertChild      u32 parentId, i32 index, u32 childId
//   RemoveChild      u32 parentId, i32 index                    (the host forgets the child and its subtree)
//   MoveChild        u32 parentId, i32 fromIndex, i32 toIndex   (toIndex counts after the removal)
//
//   A string is an int32 UTF-8 byte length (-1 for null) and the bytes.
//
// Node ids are never reused. Node 0 is the root, which the host owns from the start. Only elements and text
// reach the host: Blazor components are transparent, so a component's nodes sit directly in the element that
// contains the component. A created node is detached until an InsertChild places it. Text arrives as it is in
// the Razor markup, including the whitespace between controls: a host joins the text inside a Text control and
// skips blank text in layouts, as a browser collapses whitespace between blocks.
public enum PatchOperation : byte
{
    CreateElement = 1,
    CreateText = 2,
    SetAttribute = 3,
    RemoveAttribute = 4,
    SetEvent = 5,
    RemoveEvent = 6,
    SetText = 7,
    InsertChild = 8,
    RemoveChild = 9,
    MoveChild = 10,
}

public static class PatchProtocol
{
    public const int Version = 2;

    public const uint RootNodeId = 0;
}
