import Foundation
import UserNotifications

// UNUserNotificationCenter behind ILocalNotifications. As its delegate, it shows a notification also while the app is
// open, which watchOS otherwise keeps to the notification list.
final class WatchiousNotifications: NSObject, UNUserNotificationCenterDelegate, @unchecked Sendable {
    static let shared = WatchiousNotifications()

    let center = UNUserNotificationCenter.current()

    func attach() {
        if center.delegate !== self {
            center.delegate = self
        }
    }

    func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        willPresent notification: UNNotification,
        withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void
    ) {
        completionHandler([.banner, .list, .sound])
    }
}

// Completes with 1 when notifications are allowed; asks the user only the first time.
@_cdecl("watchious_notifications_request_permission")
public func watchiousNotificationsRequestPermission(_ token: Int64) {
    let notifications = WatchiousNotifications.shared
    notifications.attach()
    notifications.center.requestAuthorization(options: [.alert, .sound]) { isGranted, _ in
        WatchiousEssentialsCallbacks.shared.complete(token, isGranted ? 1 : 0)
    }
}

// A delay of 0 or less shows it now. Completes with 1 when it's added.
@_cdecl("watchious_notifications_show")
public func watchiousNotificationsShow(
    _ id: Int32,
    _ title: UnsafePointer<CChar>,
    _ body: UnsafePointer<CChar>?,
    _ delay: Double,
    _ token: Int64
) {
    let content = UNMutableNotificationContent()
    content.title = String(cString: title)
    if let body {
        content.body = String(cString: body)
    }
    content.sound = .default
    let trigger = delay > 0 ? UNTimeIntervalNotificationTrigger(timeInterval: delay, repeats: false) : nil
    let request = UNNotificationRequest(identifier: String(id), content: content, trigger: trigger)
    let notifications = WatchiousNotifications.shared
    notifications.attach()
    notifications.center.add(request) { error in
        if let error {
            NSLog("[Watchious] notification %d: %@", id, error.localizedDescription)
        }
        WatchiousEssentialsCallbacks.shared.complete(token, error == nil ? 1 : 0)
    }
}

@_cdecl("watchious_notifications_cancel")
public func watchiousNotificationsCancel(_ id: Int32) {
    let identifiers = [String(id)]
    let center = WatchiousNotifications.shared.center
    center.removePendingNotificationRequests(withIdentifiers: identifiers)
    center.removeDeliveredNotifications(withIdentifiers: identifiers)
}

@_cdecl("watchious_notifications_cancel_all")
public func watchiousNotificationsCancelAll() {
    let center = WatchiousNotifications.shared.center
    center.removeAllPendingNotificationRequests()
    center.removeAllDeliveredNotifications()
}
