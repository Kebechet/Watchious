import Foundation

enum PatchError: Error {
    case unsupportedVersion(Int32)
    case truncated
    case unknownOperation(UInt8)
    case unknownNode(UInt32)
    case duplicateNode(UInt32)
    case invalidIndex(Int32)
}

// Applies patch buffers of Watchious's protocol v2. The format is described in Watchious.Core's
// Protocol/PatchOperation.cs.
@MainActor
final class UiTree {
    static let protocolVersion: Int32 = 2

    let root = UiNode(id: 0, type: "#root")
    private var nodes: [UInt32: UiNode]

    init() {
        nodes = [root.id: root]
    }

    func apply(_ patches: Data) throws {
        var buffer = PatchBuffer(bytes: [UInt8](patches))
        let version = try buffer.readInt32()
        guard version == Self.protocolVersion else {
            throw PatchError.unsupportedVersion(version)
        }

        while buffer.hasRemaining {
            let operation = try buffer.readUInt8()
            let id = try buffer.readUInt32()
            switch operation {
            case 1:
                try add(UiNode(id: id, type: buffer.readString() ?? ""))
            case 2:
                let node = UiNode(id: id, type: UiNode.textType)
                node.text = try buffer.readString()
                try add(node)
            case 3:
                let name = try buffer.readString() ?? ""
                let value = try buffer.readString()
                try find(id).attributes[name] = value
            case 4:
                let name = try buffer.readString() ?? ""
                try find(id).attributes.removeValue(forKey: name)
            case 5:
                let name = try buffer.readString() ?? ""
                let eventHandlerId = try buffer.readUInt64()
                try find(id).events[name] = eventHandlerId
            case 6:
                let name = try buffer.readString() ?? ""
                try find(id).events.removeValue(forKey: name)
            case 7:
                let text = try buffer.readString()
                try find(id).text = text
            case 8:
                let index = try buffer.readInt32()
                let child = try find(buffer.readUInt32())
                let parent = try find(id)
                guard index >= 0, Int(index) <= parent.children.count else {
                    throw PatchError.invalidIndex(index)
                }
                parent.children.insert(child, at: Int(index))
            case 9:
                let index = try buffer.readInt32()
                let parent = try find(id)
                guard index >= 0, Int(index) < parent.children.count else {
                    throw PatchError.invalidIndex(index)
                }
                forget(parent.children.remove(at: Int(index)))
            case 10:
                let fromIndex = try buffer.readInt32()
                let toIndex = try buffer.readInt32()
                let parent = try find(id)
                guard fromIndex >= 0, Int(fromIndex) < parent.children.count,
                      toIndex >= 0, Int(toIndex) < parent.children.count else {
                    throw PatchError.invalidIndex(fromIndex < 0 ? fromIndex : toIndex)
                }
                parent.children.insert(parent.children.remove(at: Int(fromIndex)), at: Int(toIndex))
            default:
                throw PatchError.unknownOperation(operation)
            }
        }
    }

    private func add(_ node: UiNode) throws {
        guard nodes.updateValue(node, forKey: node.id) == nil else {
            throw PatchError.duplicateNode(node.id)
        }
    }

    private func find(_ id: UInt32) throws -> UiNode {
        guard let node = nodes[id] else {
            throw PatchError.unknownNode(id)
        }
        return node
    }

    private func forget(_ node: UiNode) {
        nodes.removeValue(forKey: node.id)
        for child in node.children {
            forget(child)
        }
    }
}

private struct PatchBuffer {
    let bytes: [UInt8]
    var position = 0

    var hasRemaining: Bool {
        position < bytes.count
    }

    mutating func readUInt8() throws -> UInt8 {
        try require(1)
        defer { position += 1 }
        return bytes[position]
    }

    mutating func readUInt32() throws -> UInt32 {
        try require(4)
        var value: UInt32 = 0
        for byte in 0..<4 {
            value |= UInt32(bytes[position + byte]) << (8 * byte)
        }
        position += 4
        return value
    }

    mutating func readInt32() throws -> Int32 {
        Int32(bitPattern: try readUInt32())
    }

    mutating func readUInt64() throws -> UInt64 {
        let low = UInt64(try readUInt32())
        let high = UInt64(try readUInt32())
        return low | (high << 32)
    }

    mutating func readString() throws -> String? {
        let length = Int(try readInt32())
        if length < 0 {
            return nil
        }
        try require(length)
        defer { position += length }
        return String(decoding: bytes[position..<position + length], as: UTF8.self)
    }

    private func require(_ count: Int) throws {
        guard position + count <= bytes.count else {
            throw PatchError.truncated
        }
    }
}
