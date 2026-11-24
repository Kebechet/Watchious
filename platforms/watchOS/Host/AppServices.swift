import Foundation
import WatchKit

// The app's own name, version and folders (IAppInfo, IFileSystem), and keeping it on screen (IDeviceDisplay).

// 0 name, 1 bundle identifier, 2 version, 3 build; 10 Library (app data), 11 Library/Caches. A UTF-8 string from
// strdup, which C# frees.
@_cdecl("watchious_app_string")
public func watchiousAppString(_ field: Int32) -> UnsafeMutablePointer<CChar>? {
    let info = Bundle.main.infoDictionary ?? [:]
    let value: String
    switch field {
    case 0:
        value = (info["CFBundleDisplayName"] as? String) ?? (info["CFBundleName"] as? String) ?? ""
    case 1:
        value = Bundle.main.bundleIdentifier ?? ""
    case 2:
        value = (info["CFBundleShortVersionString"] as? String) ?? ""
    case 3:
        value = (info["CFBundleVersion"] as? String) ?? ""
    case 10:
        value = FileManager.default.urls(for: .libraryDirectory, in: .userDomainMask).first?.path ?? ""
    case 11:
        value = FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask).first?.path ?? ""
    default:
        value = ""
    }
    return strdup(value)
}

// watchOS turns the screen off (or dims it) when the wrist goes down, and then suspends the app. An extended runtime
// session keeps the app running and on screen meanwhile. Its kind comes from the app's Info.plist (WKBackgroundModes),
// and App Store review checks that the app is that kind of app.
final class WatchiousDisplay: NSObject, WKExtendedRuntimeSessionDelegate, @unchecked Sendable {
    static let shared = WatchiousDisplay()

    private static let sessionModes: Set<String> = ["self-care", "mindfulness", "physical-therapy"]

    private var session: WKExtendedRuntimeSession?

    var hasSessionMode: Bool {
        let modes = Bundle.main.object(forInfoDictionaryKey: "WKBackgroundModes") as? [String] ?? []
        return modes.contains { Self.sessionModes.contains($0) }
    }

    // Main thread only.
    func keepOn(_ isOn: Bool) {
        if isOn {
            startIfNeeded()
        } else {
            session?.invalidate()
            session = nil
        }
    }

    private func startIfNeeded() {
        if let session, session.state == .running || session.state == .scheduled {
            return
        }
        let session = WKExtendedRuntimeSession()
        session.delegate = self
        self.session = session
        session.start()
    }

    func extendedRuntimeSessionDidStart(_ extendedRuntimeSession: WKExtendedRuntimeSession) {
    }

    // A session lasts as long as its kind allows (10 minutes for self-care, an hour for the others).
    func extendedRuntimeSessionWillExpire(_ extendedRuntimeSession: WKExtendedRuntimeSession) {
        NSLog("[Watchious] KeepScreenOn: the extended runtime session is about to expire")
    }

    func extendedRuntimeSession(
        _ extendedRuntimeSession: WKExtendedRuntimeSession,
        didInvalidateWith reason: WKExtendedRuntimeSessionInvalidationReason,
        error: Error?
    ) {
        if let error {
            NSLog("[Watchious] KeepScreenOn: the extended runtime session ended: %@", error.localizedDescription)
        }
        DispatchQueue.main.async {
            if self.session === extendedRuntimeSession {
                self.session = nil
            }
        }
    }
}

// 0 when done, 2 when the app's Info.plist declares no extended runtime session (see WatchiousDisplay).
@_cdecl("watchious_display_keep_on")
public func watchiousDisplayKeepOn(_ isOn: Int32) -> Int32 {
    let display = WatchiousDisplay.shared
    if isOn != 0 && !display.hasSessionMode {
        return 2
    }
    onMainThreadSync {
        display.keepOn(isOn != 0)
    }
    return 0
}
