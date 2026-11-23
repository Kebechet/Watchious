using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Watchious.Protocol;

internal sealed class PatchWriter
{
    private const int HeaderLength = sizeof(int);

    private readonly ArrayBufferWriter<byte> _buffer = new(1024);

    public bool HasOperations => _buffer.WrittenCount > HeaderLength;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.WrittenSpan;

    public void Begin()
    {
        _buffer.ResetWrittenCount();
        WriteInt32(PatchProtocol.Version);
    }

    public void CreateElement(uint id, string type)
    {
        WriteOperation(PatchOperation.CreateElement);
        WriteUInt32(id);
        WriteString(type);
    }

    public void CreateText(uint id, string text)
    {
        WriteOperation(PatchOperation.CreateText);
        WriteUInt32(id);
        WriteString(text);
    }

    public void SetAttribute(uint id, string name, string? value)
    {
        WriteOperation(PatchOperation.SetAttribute);
        WriteUInt32(id);
        WriteString(name);
        WriteString(value);
    }

    public void RemoveAttribute(uint id, string name)
    {
        WriteOperation(PatchOperation.RemoveAttribute);
        WriteUInt32(id);
        WriteString(name);
    }

    public void SetEvent(uint id, string eventName, ulong eventHandlerId)
    {
        WriteOperation(PatchOperation.SetEvent);
        WriteUInt32(id);
        WriteString(eventName);
        WriteUInt64(eventHandlerId);
    }

    public void RemoveEvent(uint id, string eventName)
    {
        WriteOperation(PatchOperation.RemoveEvent);
        WriteUInt32(id);
        WriteString(eventName);
    }

    public void SetText(uint id, string text)
    {
        WriteOperation(PatchOperation.SetText);
        WriteUInt32(id);
        WriteString(text);
    }

    public void InsertChild(uint parentId, int index, uint childId)
    {
        WriteOperation(PatchOperation.InsertChild);
        WriteUInt32(parentId);
        WriteInt32(index);
        WriteUInt32(childId);
    }

    public void RemoveChild(uint parentId, int index)
    {
        WriteOperation(PatchOperation.RemoveChild);
        WriteUInt32(parentId);
        WriteInt32(index);
    }

    public void MoveChild(uint parentId, int fromIndex, int toIndex)
    {
        WriteOperation(PatchOperation.MoveChild);
        WriteUInt32(parentId);
        WriteInt32(fromIndex);
        WriteInt32(toIndex);
    }

    private void WriteOperation(PatchOperation operation)
    {
        _buffer.GetSpan(1)[0] = (byte)operation;
        _buffer.Advance(1);
    }

    private void WriteInt32(int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.GetSpan(sizeof(int)), value);
        _buffer.Advance(sizeof(int));
    }

    private void WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.GetSpan(sizeof(uint)), value);
        _buffer.Advance(sizeof(uint));
    }

    private void WriteUInt64(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer.GetSpan(sizeof(ulong)), value);
        _buffer.Advance(sizeof(ulong));
    }

    private void WriteString(string? value)
    {
        if (value is null)
        {
            WriteInt32(-1);
            return;
        }

        var length = Encoding.UTF8.GetByteCount(value);
        WriteInt32(length);
        Encoding.UTF8.GetBytes(value, _buffer.GetSpan(length));
        _buffer.Advance(length);
    }
}
