using System.Collections.Concurrent;
using Android.App;
using Android.Content.PM;

namespace Watchious.Hosts.WearOS.Essentials;

// Runtime permissions through the app's activity, which hands the answers back in OnRequestPermissionsResult.
internal sealed class WearOSPermissions(Activity activity)
{
    // Request codes must fit in 16 bits; this range stays clear of codes an app might use itself.
    private const int FirstRequestCode = 0x7300;

    private readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> _pending = new();
    private int _lastRequestCode = FirstRequestCode;

    public bool IsAnyGranted(params string[] permissions)
    {
        return permissions.Any(permission => activity.CheckSelfPermission(permission) == Permission.Granted);
    }

    // True when at least one of them ends up granted: location works with an approximate (coarse) grant too. A
    // permission the app's manifest doesn't declare is denied without asking.
    public Task<bool> RequestAnyAsync(params string[] permissions)
    {
        if (IsAnyGranted(permissions))
        {
            return Task.FromResult(true);
        }

        var requestCode = FirstRequestCode + ((Interlocked.Increment(ref _lastRequestCode) - FirstRequestCode) % 0xFF);
        var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestCode] = result;
        activity.RunOnUiThread(() => activity.RequestPermissions(permissions, requestCode));
        return result.Task;
    }

    public void OnRequestPermissionsResult(int requestCode, Permission[] grantResults)
    {
        if (_pending.TryRemove(requestCode, out var result))
        {
            result.TrySetResult(grantResults.Any(grant => grant == Permission.Granted));
        }
    }
}
