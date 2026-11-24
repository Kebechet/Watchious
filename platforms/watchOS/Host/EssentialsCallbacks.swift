import Foundation

// The C# callbacks of Watchious.Hosts.watchOS's watch APIs (Essentials/WatchOSNative.cs), registered once before the
// app starts. A completion carries the token C# passed to the call it finishes and a result.
public typealias WatchiousEssentialsCompletionCallback = @convention(c) (Int64, Int32) -> Void
public typealias WatchiousEssentialsStateCallback = @convention(c) (Int32) -> Void
public typealias WatchiousLocationCallback = @convention(c) (Int64, UnsafePointer<Double>) -> Void
public typealias WatchiousSensorCallback = @convention(c) (Int32, Double, Double, Double, Double) -> Void

final class WatchiousEssentialsCallbacks: @unchecked Sendable {
    static let shared = WatchiousEssentialsCallbacks()

    var completion: WatchiousEssentialsCompletionCallback?
    var appState: WatchiousEssentialsStateCallback?
    var ambient: WatchiousEssentialsStateCallback?
    var location: WatchiousLocationCallback?
    var locationFailed: WatchiousEssentialsCompletionCallback?
    var sensor: WatchiousSensorCallback?

    func complete(_ token: Int64, _ result: Int32) {
        completion?(token, result)
    }
}

@_cdecl("watchious_essentials_set_callbacks")
public func watchiousEssentialsSetCallbacks(
    _ completion: WatchiousEssentialsCompletionCallback,
    _ appState: WatchiousEssentialsStateCallback,
    _ ambient: WatchiousEssentialsStateCallback,
    _ location: WatchiousLocationCallback,
    _ locationFailed: WatchiousEssentialsCompletionCallback,
    _ sensor: WatchiousSensorCallback
) {
    let callbacks = WatchiousEssentialsCallbacks.shared
    callbacks.completion = completion
    callbacks.appState = appState
    callbacks.ambient = ambient
    callbacks.location = location
    callbacks.locationFailed = locationFailed
    callbacks.sensor = sensor
}

// Runs on the main thread and waits for it, for state that belongs to the main thread.
func onMainThreadSync<T>(_ body: () -> T) -> T {
    Thread.isMainThread ? body() : DispatchQueue.main.sync(execute: body)
}
