import CoreMotion
import Foundation
#if WATCHIOUS_LOCATION
import CoreLocation
#endif

// Core Motion, and Core Location's heading for the compass, behind the sensors. Kinds are Watchious.Hosts.watchOS's
// WatchOSSensorKind: 0 accelerometer, 1 gyroscope, 2 magnetometer, 3 compass, 4 barometer, 5 orientation, 6 pedometer. Readings
// arrive on a queue of their own and go to C# as up to four numbers.
//
// As for location (Location.swift), the compass is built in only with WATCHIOUS_LOCATION, and the barometer and the
// pedometer, which read motion data, only with WATCHIOUS_MOTION_DATA, which link-watch-app.sh defines when the app's Info.plist
// has NSMotionUsageDescription: App Store Connect refuses code that uses them without the text (ITMS-90683). The
// accelerometer, gyroscope, magnetometer and device motion need no permission.
final class WatchiousSensors: NSObject, @unchecked Sendable {
    static let shared = WatchiousSensors()

    private let motion = CMMotionManager()
    #if WATCHIOUS_MOTION_DATA
    private let altimeter = CMAltimeter()
    private let pedometer = CMPedometer()
    #endif
    private let queue: OperationQueue = {
        let queue = OperationQueue()
        queue.name = "watchious.sensors"
        queue.maxConcurrentOperationCount = 1
        return queue
    }()
    #if WATCHIOUS_LOCATION
    private lazy var headingManager: CLLocationManager = {
        let manager = CLLocationManager()
        manager.delegate = self
        return manager
    }()
    #endif
    // The gyroscope (without a raw one) and the orientation share device motion's single stream.
    private let lock = NSLock()
    private var deviceMotionIntervals: [Int32: Double] = [:]

    // 1 when this watch has it, 0 when it doesn't, 2 when the host was built without it (see above).
    func support(_ kind: Int32) -> Int32 {
        switch kind {
        case 0:
            return motion.isAccelerometerAvailable ? 1 : 0
        case 1:
            return motion.isGyroAvailable || motion.isDeviceMotionAvailable ? 1 : 0
        case 2:
            return motion.isMagnetometerAvailable ? 1 : 0
        case 3:
            #if WATCHIOUS_LOCATION
            return CLLocationManager.headingAvailable() ? 1 : 0
            #else
            return 2
            #endif
        case 4:
            #if WATCHIOUS_MOTION_DATA
            return CMAltimeter.isRelativeAltitudeAvailable() ? 1 : 0
            #else
            return 2
            #endif
        case 5:
            return motion.isDeviceMotionAvailable ? 1 : 0
        case 6:
            #if WATCHIOUS_MOTION_DATA
            return CMPedometer.isStepCountingAvailable() ? 1 : 0
            #else
            return 2
            #endif
        default:
            return 0
        }
    }

    // 0 when started, 2 when the host was built without it.
    func start(_ kind: Int32, interval: Double) -> Int32 {
        switch kind {
        case 0:
            motion.accelerometerUpdateInterval = interval
            motion.startAccelerometerUpdates(to: queue) { data, _ in
                guard let acceleration = data?.acceleration else {
                    return
                }
                // Core Motion reads a watch lying face up as (0, 0, -1) g, Android as (0, 0, 1): flipped to read the
                // same on both.
                report(0, -acceleration.x, -acceleration.y, -acceleration.z, 0)
            }
        case 1 where motion.isGyroAvailable:
            motion.gyroUpdateInterval = interval
            motion.startGyroUpdates(to: queue) { data, _ in
                guard let rate = data?.rotationRate else {
                    return
                }
                report(1, rate.x, rate.y, rate.z, 0)
            }
        case 1, 5:
            startDeviceMotion(kind, interval: interval)
        case 2:
            motion.magnetometerUpdateInterval = interval
            motion.startMagnetometerUpdates(to: queue) { data, _ in
                guard let field = data?.magneticField else {
                    return
                }
                report(2, field.x, field.y, field.z, 0)
            }
        case 3:
            #if WATCHIOUS_LOCATION
            DispatchQueue.main.async {
                self.headingManager.startUpdatingHeading()
            }
            #else
            return 2
            #endif
        case 4:
            #if WATCHIOUS_MOTION_DATA
            altimeter.startRelativeAltitudeUpdates(to: queue) { data, _ in
                guard let pressure = data?.pressure else {
                    return
                }
                // Kilopascals.
                report(4, pressure.doubleValue * 10, 0, 0, 0)
            }
            #else
            return 2
            #endif
        case 6:
            #if WATCHIOUS_MOTION_DATA
            // The steps since now, whenever the count changes.
            pedometer.startUpdates(from: Date()) { data, error in
                guard let steps = data?.numberOfSteps else {
                    if let error {
                        NSLog("[Watchious] The pedometer stopped: %@", error.localizedDescription)
                    }
                    return
                }
                report(6, steps.doubleValue, 0, 0, 0)
            }
            #else
            return 2
            #endif
        default:
            break
        }
        return 0
    }

    #if WATCHIOUS_MOTION_DATA
    // Core Motion asks for Motion & Fitness on the first query: a query for the last minute's steps shows the question
    // and answers once the user has, with 1 when allowed and 0 when not.
    func requestPedometerAccess(_ token: Int64) {
        let now = Date()
        pedometer.queryPedometerData(from: now.addingTimeInterval(-60), to: now) { _, _ in
            let isAllowed = CMPedometer.authorizationStatus() == .authorized
            WatchiousEssentialsCallbacks.shared.complete(token, isAllowed ? 1 : 0)
        }
    }
    #endif

    func stop(_ kind: Int32) {
        switch kind {
        case 0:
            motion.stopAccelerometerUpdates()
        case 1 where motion.isGyroAvailable:
            motion.stopGyroUpdates()
        case 1, 5:
            stopDeviceMotion(kind)
        case 2:
            motion.stopMagnetometerUpdates()
        case 3:
            #if WATCHIOUS_LOCATION
            DispatchQueue.main.async {
                self.headingManager.stopUpdatingHeading()
            }
            #endif
        case 4:
            #if WATCHIOUS_MOTION_DATA
            altimeter.stopRelativeAltitudeUpdates()
            #endif
        case 6:
            #if WATCHIOUS_MOTION_DATA
            pedometer.stopUpdates()
            #endif
        default:
            break
        }
    }

    private func startDeviceMotion(_ kind: Int32, interval: Double) {
        lock.lock()
        deviceMotionIntervals[kind] = interval
        let fastest = deviceMotionIntervals.values.min() ?? interval
        lock.unlock()
        motion.deviceMotionUpdateInterval = fastest
        guard !motion.isDeviceMotionActive else {
            return
        }
        motion.startDeviceMotionUpdates(to: queue) { [weak self] data, _ in
            guard let self, let data else {
                return
            }
            self.lock.lock()
            let kinds = Set(self.deviceMotionIntervals.keys)
            self.lock.unlock()
            if kinds.contains(1) {
                report(1, data.rotationRate.x, data.rotationRate.y, data.rotationRate.z, 0)
            }
            if kinds.contains(5) {
                let quaternion = data.attitude.quaternion
                report(5, quaternion.x, quaternion.y, quaternion.z, quaternion.w)
            }
        }
    }

    private func stopDeviceMotion(_ kind: Int32) {
        lock.lock()
        deviceMotionIntervals[kind] = nil
        let isEmpty = deviceMotionIntervals.isEmpty
        lock.unlock()
        if isEmpty {
            motion.stopDeviceMotionUpdates()
        }
    }
}

#if WATCHIOUS_LOCATION
extension WatchiousSensors: CLLocationManagerDelegate {
    func locationManager(_ manager: CLLocationManager, didUpdateHeading newHeading: CLHeading) {
        report(3, newHeading.magneticHeading, 0, 0, 0)
    }
}
#endif

private func report(_ kind: Int32, _ x: Double, _ y: Double, _ z: Double, _ w: Double) {
    WatchiousEssentialsCallbacks.shared.sensor?(kind, x, y, z, w)
}

// 1 when this watch has it, 0 when not, 2 when the host was built without it.
@_cdecl("watchious_sensor_is_supported")
public func watchiousSensorIsSupported(_ kind: Int32) -> Int32 {
    WatchiousSensors.shared.support(kind)
}

@_cdecl("watchious_sensor_start")
public func watchiousSensorStart(_ kind: Int32, _ interval: Double) -> Int32 {
    WatchiousSensors.shared.start(kind, interval: interval)
}

@_cdecl("watchious_sensor_stop")
public func watchiousSensorStop(_ kind: Int32) {
    WatchiousSensors.shared.stop(kind)
}

// CMAuthorizationStatus: 0 not asked yet, 1 restricted, 2 denied, 3 allowed; -1 when the host was built without the
// pedometer.
@_cdecl("watchious_pedometer_request")
public func watchiousPedometerRequest(_ token: Int64) {
    #if WATCHIOUS_MOTION_DATA
    WatchiousSensors.shared.requestPedometerAccess(token)
    #else
    WatchiousEssentialsCallbacks.shared.complete(token, 0)
    #endif
}

@_cdecl("watchious_pedometer_authorization")
public func watchiousPedometerAuthorization() -> Int32 {
    #if WATCHIOUS_MOTION_DATA
    return Int32(CMPedometer.authorizationStatus().rawValue)
    #else
    return -1
    #endif
}
