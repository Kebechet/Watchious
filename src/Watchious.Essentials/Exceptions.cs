namespace Watchious.Essentials;

// As MAUI Essentials: the user didn't allow what the call needs (location, notifications).
public sealed class PermissionException(string message) : UnauthorizedAccessException(message);

// As MAUI Essentials: the watch has no such sensor or service, as IsSupported tells beforehand.
public sealed class FeatureNotSupportedException(string message) : NotSupportedException(message);
