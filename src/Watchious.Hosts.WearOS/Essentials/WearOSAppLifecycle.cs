using Android.App;
using Android.OS;
using Watchious.Essentials;

namespace Watchious.Hosts.WearOS.Essentials;

// The activity's state from its lifecycle callbacks: resumed is Active, started is Inactive, stopped is Background.
// Created in the activity's OnCreate, before it starts.
internal sealed class WearOSAppLifecycle : Java.Lang.Object, Application.IActivityLifecycleCallbacks, IAppLifecycle
{
    private readonly Activity _activity;

    public WearOSAppLifecycle(Activity activity)
    {
        _activity = activity;
        activity.RegisterActivityLifecycleCallbacks(this);
    }

    public AppState State { get; private set; } = AppState.Background;

    public event EventHandler<AppStateChangedEventArgs>? StateChanged;

    public void OnActivityStarted(Activity activity)
    {
        SetState(AppState.Inactive);
    }

    public void OnActivityResumed(Activity activity)
    {
        SetState(AppState.Active);
    }

    public void OnActivityPaused(Activity activity)
    {
        SetState(AppState.Inactive);
    }

    public void OnActivityStopped(Activity activity)
    {
        SetState(AppState.Background);
    }

    public void OnActivityDestroyed(Activity activity)
    {
        _activity.UnregisterActivityLifecycleCallbacks(this);
    }

    public void OnActivityCreated(Activity activity, Bundle? savedInstanceState)
    {
    }

    public void OnActivitySaveInstanceState(Activity activity, Bundle outState)
    {
    }

    private void SetState(AppState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, new AppStateChangedEventArgs(state));
    }
}
