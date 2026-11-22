// .NET for iOS's runtime glue listens for UIApplicationDidReceiveMemoryWarningNotification to collect garbage
// when memory runs low. On watchOS that name exists only in a private part of UIKit, and the App Store rejects an
// app that references it ("The app references non-public symbols"). Defining it in the app makes the glue use this
// copy instead. watchOS never posts the notification, so the glue simply never receives it.
#include <CoreFoundation/CoreFoundation.h>

const CFStringRef UIApplicationDidReceiveMemoryWarningNotification = CFSTR("UIApplicationDidReceiveMemoryWarningNotification");
