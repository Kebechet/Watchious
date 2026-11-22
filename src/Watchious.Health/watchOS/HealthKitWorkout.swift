import Foundation
import HealthKit

// HealthKit workouts for Watchious.Health (watchOS/HealthKitBackend.cs): an HKWorkoutSession with an
// HKLiveWorkoutBuilder, whose heart rate statistics go to C# as they arrive.

public typealias WatchiousHeartRateCallback = @convention(c) (Double, Double) -> Void
public typealias WatchiousHealthCompletionCallback = @convention(c) (Int64, UnsafePointer<CChar>?) -> Void

private final class WatchiousWorkout: NSObject, HKWorkoutSessionDelegate, HKLiveWorkoutBuilderDelegate, @unchecked Sendable {
    static let shared = WatchiousWorkout()

    let store = HKHealthStore()
    var onHeartRate: WatchiousHeartRateCallback?
    var onCompleted: WatchiousHealthCompletionCallback?

    private var session: HKWorkoutSession?
    private var builder: HKLiveWorkoutBuilder?
    private let heartRateType = HKQuantityType(.heartRate)
    private let beatsPerMinute = HKUnit.count().unitDivided(by: .minute())

    func complete(_ token: Int64, _ error: Error?) {
        guard let onCompleted else {
            return
        }
        if let error {
            error.localizedDescription.withCString { onCompleted(token, $0) }
        } else {
            onCompleted(token, nil)
        }
    }

    func requestAuthorization(readHeartRate: Bool, writeWorkouts: Bool, token: Int64) {
        var read = Set<HKObjectType>()
        var share = Set<HKSampleType>()
        if readHeartRate {
            read.insert(heartRateType)
        }
        if writeWorkouts {
            share.insert(HKObjectType.workoutType())
            read.insert(HKObjectType.workoutType())
        }
        store.requestAuthorization(toShare: share, read: read) { _, error in
            self.complete(token, error)
        }
    }

    func start(activityType: String, token: Int64) {
        let configuration = HKWorkoutConfiguration()
        configuration.activityType = Self.workoutActivityType(activityType)
        configuration.locationType = .unknown
        do {
            let session = try HKWorkoutSession(healthStore: store, configuration: configuration)
            let builder = session.associatedWorkoutBuilder()
            builder.dataSource = HKLiveWorkoutDataSource(healthStore: store, workoutConfiguration: configuration)
            session.delegate = self
            builder.delegate = self
            self.session = session
            self.builder = builder
            let start = Date()
            session.startActivity(with: start)
            builder.beginCollection(withStart: start) { _, error in
                self.complete(token, error)
            }
        } catch {
            complete(token, error)
        }
    }

    func pause() {
        session?.pause()
    }

    func resume() {
        session?.resume()
    }

    func end(token: Int64) {
        guard let session, let builder else {
            complete(token, nil)
            return
        }
        session.end()
        builder.endCollection(withEnd: Date()) { _, _ in
            builder.finishWorkout { _, error in
                self.session = nil
                self.builder = nil
                self.complete(token, error)
            }
        }
    }

    func workoutBuilder(_ workoutBuilder: HKLiveWorkoutBuilder, didCollectDataOf collectedTypes: Set<HKSampleType>) {
        guard collectedTypes.contains(heartRateType),
              let statistics = workoutBuilder.statistics(for: heartRateType),
              let quantity = statistics.mostRecentQuantity(),
              let onHeartRate else {
            return
        }
        let date = statistics.mostRecentQuantityDateInterval()?.end ?? Date()
        onHeartRate(quantity.doubleValue(for: beatsPerMinute), date.timeIntervalSince1970)
    }

    func workoutBuilderDidCollectEvent(_ workoutBuilder: HKLiveWorkoutBuilder) {
    }

    func workoutSession(
        _ workoutSession: HKWorkoutSession,
        didChangeTo toState: HKWorkoutSessionState,
        from fromState: HKWorkoutSessionState,
        date: Date
    ) {
    }

    func workoutSession(_ workoutSession: HKWorkoutSession, didFailWithError error: Error) {
        NSLog("[Watchious] workout session failed: %@", error.localizedDescription)
    }

    // Watchious.Health.ActivityType's names.
    private static func workoutActivityType(_ name: String) -> HKWorkoutActivityType {
        switch name {
        case "Running":
            return .running
        case "Walking":
            return .walking
        case "Cycling":
            return .cycling
        case "Hiking":
            return .hiking
        case "Swimming":
            return .swimming
        case "Yoga":
            return .yoga
        case "StrengthTraining":
            return .traditionalStrengthTraining
        case "Elliptical":
            return .elliptical
        case "Rowing":
            return .rowing
        case "HighIntensityIntervalTraining":
            return .highIntensityIntervalTraining
        case "Pilates":
            return .pilates
        case "StairClimbing":
            return .stairClimbing
        default:
            return .other
        }
    }
}

@_cdecl("watchious_health_set_callbacks")
public func watchiousHealthSetCallbacks(
    _ onHeartRate: WatchiousHeartRateCallback,
    _ onCompleted: WatchiousHealthCompletionCallback
) {
    WatchiousWorkout.shared.onHeartRate = onHeartRate
    WatchiousWorkout.shared.onCompleted = onCompleted
}

// The watch app's bundle identifier for WorkoutDto.DataOrigin. The caller frees the copy.
@_cdecl("watchious_health_bundle_identifier")
public func watchiousHealthBundleIdentifier() -> UnsafeMutablePointer<CChar>? {
    guard let identifier = Bundle.main.bundleIdentifier else {
        return nil
    }
    return strdup(identifier)
}

@_cdecl("watchious_health_is_available")
public func watchiousHealthIsAvailable() -> Int32 {
    HKHealthStore.isHealthDataAvailable() ? 1 : 0
}

@_cdecl("watchious_health_request_authorization")
public func watchiousHealthRequestAuthorization(_ readHeartRate: Int32, _ writeWorkouts: Int32, _ token: Int64) {
    WatchiousWorkout.shared.requestAuthorization(readHeartRate: readHeartRate != 0, writeWorkouts: writeWorkouts != 0, token: token)
}

@_cdecl("watchious_health_start_workout")
public func watchiousHealthStartWorkout(_ activityType: UnsafePointer<CChar>, _ token: Int64) {
    let name = String(cString: activityType)
    DispatchQueue.main.async {
        WatchiousWorkout.shared.start(activityType: name, token: token)
    }
}

@_cdecl("watchious_health_pause_workout")
public func watchiousHealthPauseWorkout() {
    DispatchQueue.main.async {
        WatchiousWorkout.shared.pause()
    }
}

@_cdecl("watchious_health_resume_workout")
public func watchiousHealthResumeWorkout() {
    DispatchQueue.main.async {
        WatchiousWorkout.shared.resume()
    }
}

@_cdecl("watchious_health_end_workout")
public func watchiousHealthEndWorkout(_ token: Int64) {
    DispatchQueue.main.async {
        WatchiousWorkout.shared.end(token: token)
    }
}
