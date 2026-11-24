import Foundation
import Network
import WatchKit

// C functions behind Watchious.Hosts.watchOS's watch APIs (Essentials/WatchOSNative.cs). C# calls them through
// P/Invoke into the app itself, so WatchKit, which has no .NET binding, needs no binding.

// WatchKit's device object belongs to the main thread. C# may call from it (event handlers run there), so a
// synchronous hop is taken only from another thread.
private func onMainThread<T>(_ body: () -> T) -> T {
    Thread.isMainThread ? body() : DispatchQueue.main.sync(execute: body)
}

// The values of Watchious.Essentials.HapticFeedback.
@_cdecl("watchious_haptics_play")
public func watchiousHapticsPlay(_ feedback: Int32) {
    let type: WKHapticType
    switch feedback {
    case 1:
        type = .success
    case 2:
        type = .failure
    case 3:
        type = .notification
    case 4:
        type = .start
    case 5:
        type = .stop
    case 6:
        type = .retry
    case 7:
        type = .directionUp
    case 8:
        type = .directionDown
    default:
        type = .click
    }
    DispatchQueue.main.async {
        WKInterfaceDevice.current().play(type)
    }
}

// The caller frees the string.
@_cdecl("watchious_device_string")
public func watchiousDeviceString(_ field: Int32) -> UnsafeMutablePointer<CChar>? {
    let value = field == 0 ? modelIdentifier() : onMainThread { WKInterfaceDevice.current().systemVersion }
    return strdup(value)
}

// "Watch7,1". The simulator reports the Mac's CPU instead, and names the simulated watch in its environment.
private func modelIdentifier() -> String {
    if let simulated = ProcessInfo.processInfo.environment["SIMULATOR_MODEL_IDENTIFIER"] {
        return simulated
    }
    var system = utsname()
    uname(&system)
    return withUnsafeBytes(of: &system.machine) { bytes in
        String(decoding: bytes.prefix { $0 != 0 }, as: UTF8.self)
    }
}

@_cdecl("watchious_battery_level")
public func watchiousBatteryLevel() -> Float {
    onMainThread {
        let device = WKInterfaceDevice.current()
        device.isBatteryMonitoringEnabled = true
        return device.batteryLevel
    }
}

@_cdecl("watchious_battery_state")
public func watchiousBatteryState() -> Int32 {
    onMainThread {
        let device = WKInterfaceDevice.current()
        device.isBatteryMonitoringEnabled = true
        switch device.batteryState {
        case .charging:
            return 1
        case .unplugged:
            return 2
        case .full:
            return 3
        default:
            return 0
        }
    }
}

// NWPathMonitor reports asynchronously, so it starts on first use and the first call waits briefly for its first
// report instead of answering "unknown".
private final class NetworkStatus: @unchecked Sendable {
    static let shared = NetworkStatus()

    private let monitor = NWPathMonitor()
    private let firstUpdate = DispatchSemaphore(value: 0)
    private let lock = NSLock()
    private var status: Int32 = 0
    private var hasUpdate = false

    private init() {
        monitor.pathUpdateHandler = { [weak self] path in
            self?.update(path.status == .satisfied ? 2 : 1)
        }
        monitor.start(queue: DispatchQueue(label: "Watchious.NetworkStatus"))
    }

    var current: Int32 {
        lock.lock()
        let isWaiting = !hasUpdate
        lock.unlock()
        if isWaiting {
            _ = firstUpdate.wait(timeout: .now() + .milliseconds(300))
        }
        lock.lock()
        defer { lock.unlock() }
        return status
    }

    private func update(_ newStatus: Int32) {
        lock.lock()
        status = newStatus
        let isFirst = !hasUpdate
        hasUpdate = true
        lock.unlock()
        if isFirst {
            firstUpdate.signal()
        }
    }
}

@_cdecl("watchious_network_status")
public func watchiousNetworkStatus() -> Int32 {
    NetworkStatus.shared.current
}

// The Watchious inspector to mirror the app to: --watchious-inspector=http://host:5292/inspector/app. The caller frees
// the string.
@_cdecl("watchious_inspector_url")
public func watchiousInspectorUrl() -> UnsafeMutablePointer<CChar>? {
    let prefix = "--watchious-inspector="
    guard let argument = ProcessInfo.processInfo.arguments.first(where: { $0.hasPrefix(prefix) }) else {
        return nil
    }
    return strdup(String(argument.dropFirst(prefix.count)))
}

// Watchious's own suite, so clearing it leaves the app's other defaults alone.
private let preferencesSuite = "watchious"
private let preferences = UserDefaults(suiteName: preferencesSuite) ?? .standard

// The caller frees the string.
@_cdecl("watchious_preferences_get")
public func watchiousPreferencesGet(_ key: UnsafePointer<CChar>) -> UnsafeMutablePointer<CChar>? {
    guard let value = preferences.string(forKey: String(cString: key)) else {
        return nil
    }
    return strdup(value)
}

@_cdecl("watchious_preferences_set")
public func watchiousPreferencesSet(_ key: UnsafePointer<CChar>, _ value: UnsafePointer<CChar>) {
    preferences.set(String(cString: value), forKey: String(cString: key))
}

@_cdecl("watchious_preferences_remove")
public func watchiousPreferencesRemove(_ key: UnsafePointer<CChar>) {
    preferences.removeObject(forKey: String(cString: key))
}

@_cdecl("watchious_preferences_contains")
public func watchiousPreferencesContains(_ key: UnsafePointer<CChar>) -> Int32 {
    preferences.object(forKey: String(cString: key)) == nil ? 0 : 1
}

@_cdecl("watchious_preferences_clear")
public func watchiousPreferencesClear() {
    preferences.removePersistentDomain(forName: preferencesSuite)
}
