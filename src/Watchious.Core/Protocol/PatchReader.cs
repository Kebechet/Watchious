using System.Buffers.Binary;
using System.Text;

namespace Watchious.Protocol;

// Reads a patch buffer (see PatchOperation) for hosts written in C#, such as tests and tools.
public ref struct PatchReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _position;

    public PatchReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        Version = ReadInt32();
        if (Version != PatchProtocol.Version)
        {
            throw new InvalidDataException(
                $"Watchious patch protocol version {Version} isn't supported, this reader supports version {PatchProtocol.Version}.");
        }
    }

    public int Version { get; }

    public PatchOperation Operation { get; private set; }

    public uint NodeId { get; private set; }

    public uint ChildId { get; private set; }

    public int Index { get; private set; }

    public int ToIndex { get; private set; }

    public string? Name { get; private set; }

    public string? Value { get; private set; }

    public ulong EventHandlerId { get; private set; }

    public bool Read()
    {
        if (_position == _buffer.Length)
        {
            return false;
        }

        Operation = (PatchOperation)_buffer[_position++];
        NodeId = ReadUInt32();
        ChildId = 0;
        Index = 0;
        ToIndex = 0;
        Name = null;
        Value = null;
        EventHandlerId = 0;
        switch (Operation)
        {
            case PatchOperation.CreateElement:
            case PatchOperation.RemoveAttribute:
            case PatchOperation.RemoveEvent:
                Name = ReadString();
                break;
            case PatchOperation.CreateText:
            case PatchOperation.SetText:
                Value = ReadString();
                break;
            case PatchOperation.SetAttribute:
                Name = ReadString();
                Value = ReadString();
                break;
            case PatchOperation.SetEvent:
                Name = ReadString();
                EventHandlerId = ReadUInt64();
                break;
            case PatchOperation.InsertChild:
                Index = ReadInt32();
                ChildId = ReadUInt32();
                break;
            case PatchOperation.RemoveChild:
                Index = ReadInt32();
                break;
            case PatchOperation.MoveChild:
                Index = ReadInt32();
                ToIndex = ReadInt32();
                break;
            default:
                throw new InvalidDataException($"Unknown Watchious patch operation {(byte)Operation}.");
        }

        return true;
    }

    private int ReadInt32()
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(_buffer[_position..]);
        _position += sizeof(int);
        return value;
    }

    private uint ReadUInt32()
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer[_position..]);
        _position += sizeof(uint);
        return value;
    }

    private ulong ReadUInt64()
    {
        var value = BinaryPrimitives.ReadUInt64LittleEndian(_buffer[_position..]);
        _position += sizeof(ulong);
        return value;
    }

    private string? ReadString()
    {
        var length = ReadInt32();
        if (length < 0)
        {
            return null;
        }

        var value = Encoding.UTF8.GetString(_buffer.Slice(_position, length));
        _position += length;
        return value;
    }
}
