import CoreLocation
import CoreMotion
import Foundation

// Core Motion, and Core Location's heading for the compass, behind the sensors. Kinds are Watchious.Hosts.watchOS's
// WatchOSSensorKind: 0 accelerometer, 1 gyroscope, 2 magnetometer, 3 compass, 4 barometer, 5 orientation. Readings
// arrive on a queue of their own and go to C# as up to four numbers.
final class WatchiousSensors: NSObject, CLLocationManagerDelegate, @unchecked Sendable {
    static let shared = WatchiousSensors()

    private let motion = CMMotionManager()
    private let altimeter = CMAltimeter()
    private let queue: OperationQueue = {
        let queue = OperationQueue()
        queue.name = "watchious.sensors"
        queue.maxConcurrentOperationCount = 1
        return queue
    }()
    private lazy var headingManager: CLLocationManager = {
        let manager = CLLocationManager()
        manager.delegate = self
        return manager
    }()
    // The gyroscope (without a raw one) and the orientation share device motion's single stream.
    private let lock = NSLock()
    private var deviceMotionIntervals: [Int32: Double] = [:]

    func isSupported(_ kind: Int32) -> Bool {
        switch kind {
        case 0:
            return motion.isAccelerometerAvailable
        case 1:
            return motion.isGyroAvailable || motion.isDeviceMotionAvailable
        case 2:
            return motion.isMagnetometerAvailable
        case 3:
            return CLLocationManager.headingAvailable()
        case 4:
            return CMAltimeter.isRelativeAltitudeAvailable()
        case 5:
            return motion.isDeviceMotionAvailable
        default:
            return false
        }
    }

    // 0 when started, 2 when the barometer needs NSMotionUsageDescription in the app's Info.plist, without which
    // watchOS ends the app.
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
            DispatchQueue.main.async {
                self.headingManager.startUpdatingHeading()
            }
        case 4:
            guard Bundle.main.object(forInfoDictionaryKey: "NSMotionUsageDescription") != nil else {
                return 2
            }
            altimeter.startRelativeAltitudeUpdates(to: queue) { data, _ in
                guard let pressure = data?.pressure else {
                    return
                }
                // Kilopascals.
                report(4, pressure.doubleValue * 10, 0, 0, 0)
            }
        default:
            break
        }
        return 0
    }

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
            DispatchQueue.main.async {
                self.headingManager.stopUpdatingHeading()
            }
        case 4:
            altimeter.stopRelativeAltitudeUpdates()
        default:
            break
        }
    }

    func locationManager(_ manager: CLLocationManager, didUpdateHeading newHeading: CLHeading) {
        report(3, newHeading.magneticHeading, 0, 0, 0)
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

private func report(_ kind: Int32, _ x: Double, _ y: Double, _ z: Double, _ w: Double) {
    WatchiousEssentialsCallbacks.shared.sensor?(kind, x, y, z, w)
}

@_cdecl("watchious_sensor_is_supported")
public func watchiousSensorIsSupported(_ kind: Int32) -> Int32 {
    WatchiousSensors.shared.isSupported(kind) ? 1 : 0
}

@_cdecl("watchious_sensor_start")
public func watchiousSensorStart(_ kind: Int32, _ interval: Double) -> Int32 {
    WatchiousSensors.shared.start(kind, interval: interval)
}

@_cdecl("watchious_sensor_stop")
public func watchiousSensorStop(_ kind: Int32) {
    WatchiousSensors.shared.stop(kind)
}
