import SwiftUI

// The scene phase and the dimmed always-on display, as RootView sees them, for IAppLifecycle and IAmbientMode.
// Phases are Watchious.Essentials.AppState's values: 0 active, 1 inactive, 2 background.
final class WatchiousAppState: @unchecked Sendable {
    static let shared = WatchiousAppState()

    private(set) var phase: Int32 = 2
    private(set) var isLuminanceReduced = false

    func update(phase scenePhase: ScenePhase) {
        let value: Int32
        switch scenePhase {
        case .active:
            value = 0
        case .inactive:
            value = 1
        case .background:
            value = 2
        @unknown default:
            value = 1
        }
        guard value != phase else {
            return
        }
        phase = value
        WatchiousEssentialsCallbacks.shared.appState?(value)
    }

    func update(isLuminanceReduced isReduced: Bool) {
        guard isReduced != isLuminanceReduced else {
            return
        }
        isLuminanceReduced = isReduced
        WatchiousEssentialsCallbacks.shared.ambient?(isReduced ? 1 : 0)
    }
}

@_cdecl("watchious_app_state")
public func watchiousAppState() -> Int32 {
    WatchiousAppState.shared.phase
}

@_cdecl("watchious_is_luminance_reduced")
public func watchiousIsLuminanceReduced() -> Int32 {
    WatchiousAppState.shared.isLuminanceReduced ? 1 : 0
}
