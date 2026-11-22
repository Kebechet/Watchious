import Foundation
import WatchConnectivity

// WatchConnectivity for Watchious.Messaging (watchOS/WatchConnectivityPhoneMessaging.cs). Dictionaries cross to C#
// as JSON objects of strings; Plugin.Maui.WearableMessaging on the iPhone reads every value as text too.

public typealias WatchiousMessageCallback = @convention(c) (Int64, UnsafePointer<CChar>) -> Void
public typealias WatchiousReplyCallback = @convention(c) (Int64, UnsafePointer<CChar>?, UnsafePointer<CChar>?) -> Void
public typealias WatchiousContextCallback = @convention(c) (UnsafePointer<CChar>) -> Void

private final class WatchiousMessagingSession: NSObject, WCSessionDelegate, @unchecked Sendable {
    static let shared = WatchiousMessagingSession()

    var onMessage: WatchiousMessageCallback?
    var onReply: WatchiousReplyCallback?
    var onContext: WatchiousContextCallback?

    private let lock = NSLock()
    private var replyHandlers: [Int64: ([String: Any]) -> Void] = [:]
    private var lastReplyToken: Int64 = 0

    var isReachable: Bool {
        WCSession.isSupported() && WCSession.default.activationState == .activated && WCSession.default.isReachable
    }

    func activate() {
        guard WCSession.isSupported() else {
            return
        }
        WCSession.default.delegate = self
        WCSession.default.activate()
    }

    func reply(token: Int64, message: [String: Any]) {
        lock.lock()
        let handler = replyHandlers.removeValue(forKey: token)
        lock.unlock()
        handler?(message)
    }

    func session(_ session: WCSession, activationDidCompleteWith activationState: WCSessionActivationState, error: Error?) {
        if !session.receivedApplicationContext.isEmpty {
            deliverContext(session.receivedApplicationContext)
        }
    }

    func session(_ session: WCSession, didReceiveMessage message: [String: Any]) {
        deliver(message, token: 0)
    }

    // The phone waits for an answer: C# gets a token to answer with.
    func session(_ session: WCSession, didReceiveMessage message: [String: Any], replyHandler: @escaping ([String: Any]) -> Void) {
        lock.lock()
        lastReplyToken += 1
        let token = lastReplyToken
        replyHandlers[token] = replyHandler
        lock.unlock()
        deliver(message, token: token)
    }

    func session(_ session: WCSession, didReceiveApplicationContext applicationContext: [String: Any]) {
        deliverContext(applicationContext)
    }

    private func deliver(_ message: [String: Any], token: Int64) {
        guard let onMessage else {
            return
        }
        watchiousJSON(message).withCString { onMessage(token, $0) }
    }

    private func deliverContext(_ context: [String: Any]) {
        guard let onContext else {
            return
        }
        watchiousJSON(context).withCString { onContext($0) }
    }
}

private func watchiousJSON(_ dictionary: [String: Any]) -> String {
    let strings = dictionary.mapValues { "\($0)" }
    guard let data = try? JSONSerialization.data(withJSONObject: strings) else {
        return "{}"
    }
    return String(decoding: data, as: UTF8.self)
}

private func watchiousDictionary(_ json: UnsafePointer<CChar>) -> [String: Any] {
    let data = Data(String(cString: json).utf8)
    return (try? JSONSerialization.jsonObject(with: data)) as? [String: Any] ?? [:]
}

@_cdecl("watchious_messaging_start")
public func watchiousMessagingStart(
    _ onMessage: WatchiousMessageCallback,
    _ onReply: WatchiousReplyCallback,
    _ onContext: WatchiousContextCallback
) {
    let session = WatchiousMessagingSession.shared
    session.onMessage = onMessage
    session.onReply = onReply
    session.onContext = onContext
    session.activate()
}

@_cdecl("watchious_messaging_is_reachable")
public func watchiousMessagingIsReachable() -> Int32 {
    WatchiousMessagingSession.shared.isReachable ? 1 : 0
}

// requestId 0 sends without waiting for an answer.
@_cdecl("watchious_messaging_send")
public func watchiousMessagingSend(_ json: UnsafePointer<CChar>, _ requestId: Int64) -> Int32 {
    let session = WatchiousMessagingSession.shared
    guard session.isReachable else {
        return 1
    }
    let message = watchiousDictionary(json)
    if requestId == 0 {
        WCSession.default.sendMessage(message, replyHandler: nil, errorHandler: nil)
        return 0
    }
    let onReply = session.onReply
    WCSession.default.sendMessage(
        message,
        replyHandler: { reply in
            watchiousJSON(reply).withCString { onReply?(requestId, $0, nil) }
        },
        errorHandler: { error in
            error.localizedDescription.withCString { onReply?(requestId, nil, $0) }
        })
    return 0
}

@_cdecl("watchious_messaging_reply")
public func watchiousMessagingReply(_ token: Int64, _ json: UnsafePointer<CChar>) {
    WatchiousMessagingSession.shared.reply(token: token, message: watchiousDictionary(json))
}

@_cdecl("watchious_messaging_update_context")
public func watchiousMessagingUpdateContext(_ json: UnsafePointer<CChar>) -> Int32 {
    guard WCSession.isSupported(), WCSession.default.activationState == .activated else {
        return 1
    }
    do {
        try WCSession.default.updateApplicationContext(watchiousDictionary(json))
        return 0
    } catch {
        return 1
    }
}
