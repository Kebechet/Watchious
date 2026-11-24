import CoreLocation
import Foundation

// CLLocationManager behind IGeolocation. Token 0 is the foreground listening; any other token is a one-shot request. A
// position goes to C# as 8 doubles: latitude, longitude, altitude, horizontal and vertical accuracy, speed, course and
// the time in seconds since 1970, where a negative accuracy, speed or course is unknown, as in Core Location. The
// manager lives on the main thread, whose run loop delivers its updates.
final class WatchiousLocation: NSObject, CLLocationManagerDelegate, @unchecked Sendable {
    static let shared = WatchiousLocation()

    private lazy var manager: CLLocationManager = {
        let manager = CLLocationManager()
        manager.delegate = self
        return manager
    }()
    private var authorizationTokens: [Int64] = []
    private var requestTokens: [Int64] = []
    private var isListening = false

    var isSupported: Bool {
        CLLocationManager.locationServicesEnabled()
    }

    // Completes with 1 when allowed, 0 when denied, 2 when the app's Info.plist has no
    // NSLocationWhenInUseUsageDescription, without which watchOS never asks.
    func requestAuthorization(_ token: Int64) {
        guard Bundle.main.object(forInfoDictionaryKey: "NSLocationWhenInUseUsageDescription") != nil else {
            WatchiousEssentialsCallbacks.shared.complete(token, 2)
            return
        }
        switch manager.authorizationStatus {
        case .notDetermined:
            authorizationTokens.append(token)
            manager.requestWhenInUseAuthorization()
        case .authorizedWhenInUse, .authorizedAlways:
            WatchiousEssentialsCallbacks.shared.complete(token, 1)
        default:
            WatchiousEssentialsCallbacks.shared.complete(token, 0)
        }
    }

    func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        let status = manager.authorizationStatus
        guard status != .notDetermined else {
            return
        }
        let result: Int32 = status == .authorizedWhenInUse || status == .authorizedAlways ? 1 : 0
        let tokens = authorizationTokens
        authorizationTokens.removeAll()
        for token in tokens {
            WatchiousEssentialsCallbacks.shared.complete(token, result)
        }
    }

    // While listening, the next update answers it.
    func request(_ token: Int64, accuracy: CLLocationAccuracy) {
        requestTokens.append(token)
        if !isListening {
            manager.desiredAccuracy = accuracy
            manager.requestLocation()
        }
    }

    func cancel(_ token: Int64) {
        requestTokens.removeAll { $0 == token }
    }

    func startListening(accuracy: CLLocationAccuracy) {
        manager.desiredAccuracy = accuracy
        isListening = true
        manager.startUpdatingLocation()
    }

    func stopListening() {
        isListening = false
        manager.stopUpdatingLocation()
    }

    func lastKnown(_ values: UnsafeMutablePointer<Double>) -> Int32 {
        guard let location = manager.location else {
            return 0
        }
        write(location, to: values)
        return 1
    }

    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        guard let location = locations.last, let callback = WatchiousEssentialsCallbacks.shared.location else {
            return
        }
        let tokens = requestTokens
        requestTokens.removeAll()
        var values = [Double](repeating: 0, count: 8)
        values.withUnsafeMutableBufferPointer { buffer in
            write(location, to: buffer.baseAddress!)
        }
        values.withUnsafeBufferPointer { buffer in
            for token in tokens {
                callback(token, buffer.baseAddress!)
            }
            if isListening {
                callback(0, buffer.baseAddress!)
            }
        }
    }

    // Code 1 is denied, 2 no position. While listening, Core Location keeps trying after locationUnknown, so that
    // fails only the one-shot requests.
    func locationManager(_ manager: CLLocationManager, didFailWithError error: Error) {
        let code = (error as? CLError)?.code
        let result: Int32 = code == .denied ? 1 : 2
        let tokens = requestTokens
        requestTokens.removeAll()
        for token in tokens {
            WatchiousEssentialsCallbacks.shared.locationFailed?(token, result)
        }
        if isListening && code != .locationUnknown {
            WatchiousEssentialsCallbacks.shared.locationFailed?(0, result)
        }
    }

    private func write(_ location: CLLocation, to values: UnsafeMutablePointer<Double>) {
        values[0] = location.coordinate.latitude
        values[1] = location.coordinate.longitude
        values[2] = location.altitude
        values[3] = location.horizontalAccuracy
        values[4] = location.verticalAccuracy
        values[5] = location.speed
        values[6] = location.course
        values[7] = location.timestamp.timeIntervalSince1970
    }
}

@_cdecl("watchious_location_is_supported")
public func watchiousLocationIsSupported() -> Int32 {
    WatchiousLocation.shared.isSupported ? 1 : 0
}

@_cdecl("watchious_location_request_authorization")
public func watchiousLocationRequestAuthorization(_ token: Int64) {
    DispatchQueue.main.async {
        WatchiousLocation.shared.requestAuthorization(token)
    }
}

@_cdecl("watchious_location_request")
public func watchiousLocationRequest(_ token: Int64, _ accuracy: Double) {
    DispatchQueue.main.async {
        WatchiousLocation.shared.request(token, accuracy: accuracy)
    }
}

@_cdecl("watchious_location_cancel")
public func watchiousLocationCancel(_ token: Int64) {
    DispatchQueue.main.async {
        WatchiousLocation.shared.cancel(token)
    }
}

@_cdecl("watchious_location_start")
public func watchiousLocationStart(_ accuracy: Double) {
    DispatchQueue.main.async {
        WatchiousLocation.shared.startListening(accuracy: accuracy)
    }
}

@_cdecl("watchious_location_stop")
public func watchiousLocationStop() {
    DispatchQueue.main.async {
        WatchiousLocation.shared.stopListening()
    }
}

// Fills 8 doubles as a position update does; 0 when there is no position yet.
@_cdecl("watchious_location_last_known")
public func watchiousLocationLastKnown(_ values: UnsafeMutablePointer<Double>) -> Int32 {
    onMainThreadSync {
        WatchiousLocation.shared.lastKnown(values)
    }
}
