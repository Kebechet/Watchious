import Foundation

// Live mode's host on watchOS: the SwiftUI host built without .NET, from the sources Kebechet.Watchious.Sdk ships
// (dotnet build -t:WatchiousLive, which runs its tools/build-live-watch-app.sh). The app's C# runs on a computer
// (dotnet run -f net10.0 -- --live), and these stand in for the four functions .NET exports to the host (Watchious.h):
// the computer sends the app's patches, and the taps and confirmations go back, over plain HTTP (Watchious.Preview's
// WatchiousLive.cs). The computer is the launch argument --watchious-live=http://<computer>:5293.
final class WatchiousLiveConnection: @unchecked Sendable {
    static let shared = WatchiousLiveConnection()

    private static let patchBatch: UInt8 = 1
    private static let json: UInt8 = 2
    private static let headerBytes = 1 + 8 + 4

    private let baseURL: URL
    private let urlSession: URLSession
    // Events and confirmations reach the computer in their order.
    private let sendQueue = DispatchQueue(label: "watchious.live.send")
    private let lock = NSLock()
    private var sessionId: String?
    private var applyPatches: watchious_patches_callback?
    private var reportError: watchious_text_callback?
    private var hasShownPatches = false
    // The last message of the session this host got, which the next request confirms. Read on the polling thread only.
    private var lastSequence: Int64 = 0

    init() {
        let prefix = "--watchious-live="
        let argument = ProcessInfo.processInfo.arguments.first { $0.hasPrefix(prefix) }
        let text = argument.map { String($0.dropFirst(prefix.count)) } ?? "http://localhost:5293"
        baseURL = URL(string: text) ?? URL(string: "http://localhost:5293")!
        let configuration = URLSessionConfiguration.ephemeral
        // Longer than the 20 s the computer holds a request for messages open.
        configuration.timeoutIntervalForRequest = 40
        urlSession = URLSession(configuration: configuration)
    }

    func start(_ apply: watchious_patches_callback?, _ report: watchious_text_callback?) {
        applyPatches = apply
        reportError = report
        Thread.detachNewThread { self.poll() }
    }

    func send(_ message: [String: Any]) {
        guard let id = lock.withLock({ sessionId }),
              let body = try? JSONSerialization.data(withJSONObject: message) else {
            return
        }
        sendQueue.async {
            do {
                _ = try self.request("POST", "/live/events?session=\(id)", body: body)
            } catch {
                NSLog("[Watchious] An event didn't reach the computer: %@", error.localizedDescription)
            }
        }
    }

    private func poll() {
        while true {
            do {
                let id = try currentSession()
                guard let answer = try request("GET", "/live/messages?session=\(id)&ack=\(lastSequence)", body: nil) else {
                    // The computer no longer knows the session: its app was restarted, so the screen starts over.
                    lock.withLock { sessionId = nil }
                    if hasShownPatches {
                        hasShownPatches = false
                        DispatchQueue.main.async {
                            WatchiousStore.shared.reset()
                        }
                    }
                    continue
                }
                readMessages(answer)
            } catch {
                NSLog("[Watchious] The computer at %@ can't be reached (dotnet run -f net10.0 -- --live): %@",
                      baseURL.absoluteString, error.localizedDescription)
                Thread.sleep(forTimeInterval: 2)
            }
        }
    }

    private func currentSession() throws -> String {
        if let id = lock.withLock({ sessionId }) {
            return id
        }
        guard let answer = try request("POST", "/live/start?platform=watchos", body: nil) else {
            throw URLError(.badServerResponse)
        }
        let id = String(decoding: answer, as: UTF8.self)
        lastSequence = 0
        lock.withLock { sessionId = id }
        return id
    }

    // Each message is a kind byte, its little-endian sequence number and length, and its bytes. The computer sends again
    // what this host hasn't confirmed, as an answer can be lost on the way, so a message seen before is skipped.
    private func readMessages(_ answer: Data) {
        let bytes = [UInt8](answer)
        var offset = 0
        while offset + Self.headerBytes <= bytes.count {
            let kind = bytes[offset]
            let sequence = Self.littleEndian(bytes, at: offset + 1, as: Int64.self)
            let length = Int(Self.littleEndian(bytes, at: offset + 9, as: Int32.self))
            let start = offset + Self.headerBytes
            guard length >= 0, start + length <= bytes.count else {
                NSLog("[Watchious] A message from the computer is cut short; the computer sends it again.")
                return
            }
            let message = Array(bytes[start..<start + length])
            offset = start + length
            guard sequence > lastSequence else {
                continue
            }
            lastSequence = sequence
            switch kind {
            case Self.patchBatch:
                hasShownPatches = true
                message.withUnsafeBufferPointer { buffer in
                    applyPatches?(buffer.baseAddress, Int32(buffer.count))
                }
            case Self.json:
                let text = Array(Self.errorText(message).utf8)
                text.withUnsafeBufferPointer { buffer in
                    reportError?(buffer.baseAddress, Int32(buffer.count))
                }
            default:
                break
            }
        }
    }

    private static func littleEndian<T: FixedWidthInteger>(_ bytes: [UInt8], at offset: Int, as type: T.Type) -> T {
        var value: T = 0
        for index in 0..<MemoryLayout<T>.size {
            value |= T(truncatingIfNeeded: bytes[offset + index]) << (8 * index)
        }
        return value
    }

    // The app's error, which comes as {"error": "..."}, as the text .NET would report.
    private static func errorText(_ message: [UInt8]) -> String {
        let object = try? JSONSerialization.jsonObject(with: Data(message)) as? [String: Any]
        return object?["error"] as? String ?? String(decoding: message, as: UTF8.self)
    }

    // The response's bytes, or nil when the computer doesn't know the session (404).
    private func request(_ method: String, _ path: String, body: Data?) throws -> Data? {
        guard let url = URL(string: baseURL.absoluteString.trimmingCharacters(in: CharacterSet(charactersIn: "/")) + path) else {
            throw URLError(.badURL)
        }
        var request = URLRequest(url: url)
        request.httpMethod = method
        if let body {
            request.httpBody = body
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        }
        let done = DispatchSemaphore(value: 0)
        var result: Result<(Data, Int), Error> = .failure(URLError(.unknown))
        urlSession.dataTask(with: request) { data, response, error in
            if let error {
                result = .failure(error)
            } else {
                result = .success((data ?? Data(), (response as? HTTPURLResponse)?.statusCode ?? 0))
            }
            done.signal()
        }.resume()
        done.wait()
        let (data, status) = try result.get()
        if status == 404 {
            return nil
        }
        guard (200..<300).contains(status) else {
            throw URLError(.badServerResponse)
        }
        return data
    }
}

// The four functions .NET exports to the host in an app (Watchious.h), here talking to the computer.

@_cdecl("watchious_start_runtime")
public func watchiousLiveStartRuntime() {
}

@_cdecl("watchious_start")
public func watchiousLiveStart(_ applyPatches: watchious_patches_callback?, _ reportError: watchious_text_callback?) -> Int32 {
    WatchiousLiveConnection.shared.start(applyPatches, reportError)
    return 0
}

@_cdecl("watchious_dispatch")
public func watchiousLiveDispatch(_ eventHandlerId: UInt64, _ eventName: UnsafePointer<CChar>?, _ value: UnsafePointer<CChar>?) -> Int32 {
    let name = eventName.map { String(cString: $0) } ?? ""
    let valueText: Any = value.map { String(cString: $0) } ?? NSNull()
    WatchiousLiveConnection.shared.send(["id": eventHandlerId, "name": name, "value": valueText])
    return 0
}

@_cdecl("watchious_patches_applied")
public func watchiousLivePatchesApplied() {
    WatchiousLiveConnection.shared.send(["applied": true])
}
