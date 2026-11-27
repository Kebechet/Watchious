using Android.App;
using Android.OS;
using AndroidX.Lifecycle;
using AndroidX.Wear.Ambient;
using Watchious.Essentials;

namespace Watchious.Hosts.WearOS.Essentials;

// Wear OS's AmbientLifecycleObserver, which keeps the activity on the dimmed screen and reports ambient changes. Ambient
// support changes what the watch does when the wrist is lowered, so it is turned on only once the app uses this service
// (reads IsAmbient or subscribes). The observer needs a lifecycle, which the activity, a plain Activity, doesn't have:
// it gets one of its own that follows the activity's lifecycle callbacks.
internal sealed class WearOSAmbientMode(Activity activity, WearOSAppLifecycle appLifecycle) : IAmbientMode, IDisposable
{
    private readonly Lock _lock = new();
    private AmbientOwner? _owner;
    private EventHandler<AmbientModeChangedEventArgs>? _ambientModeChanged;

    public bool IsSupported => true;

    public bool IsAmbient
    {
        get
        {
            EnsureStarted();
            return _owner?.IsAmbient ?? false;
        }
    }

    public event EventHandler<AmbientModeChangedEventArgs>? AmbientModeChanged
    {
        // Under the lock, as a field-like event would change it atomically: a lost remove would keep a disposed page's
        // handler running.
        add
        {
            EnsureStarted();
            lock (_lock)
            {
                _ambientModeChanged += value;
            }
        }
        remove
        {
            lock (_lock)
            {
                _ambientModeChanged -= value;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_owner is null)
            {
                return;
            }

            var owner = _owner;
            _owner = null;
            activity.RunOnUiThread(owner.Destroy);
        }
    }

    private void EnsureStarted()
    {
        lock (_lock)
        {
            if (_owner is not null)
            {
                return;
            }

            var owner = new AmbientOwner(activity, OnAmbientChanged);
            _owner = owner;
            activity.RunOnUiThread(() => owner.Start(appLifecycle.State));
        }
    }

    private void OnAmbientChanged(bool isAmbient)
    {
        ComposeWatchiousHost.SetAmbient(activity, isAmbient);
        _ambientModeChanged?.Invoke(this, new AmbientModeChangedEventArgs(isAmbient));
    }

    // Main thread only, as Lifecycle requires.
    private sealed class AmbientOwner : Java.Lang.Object, ILifecycleOwner, IAmbientLifecycleObserverAmbientLifecycleCallback,
        Application.IActivityLifecycleCallbacks
    {
        private readonly Activity _activity;
        private readonly LifecycleRegistry _registry;
        private readonly IAmbientLifecycleObserver _observer;
        private readonly Action<bool> _changed;
        private bool _isDestroyed;

        public AmbientOwner(Activity activity, Action<bool> changed)
        {
            _activity = activity;
            _changed = changed;
            _registry = new LifecycleRegistry(this);
            _observer = AmbientLifecycleObserverKt.AmbientLifecycleObserver(activity, this);
        }

        public Lifecycle Lifecycle => _registry;

        // Kept from the callbacks: the observer's own isAmbient changes only after they return, so a page that rendered
        // on the change would read the old value.
        public bool IsAmbient { get; private set; }

        // From the activity's state when the app first uses ambient mode; its lifecycle callbacks take over from there.
        public void Start(AppState state)
        {
            if (_isDestroyed)
            {
                return;
            }

            _registry.AddObserver(_observer);
            _activity.RegisterActivityLifecycleCallbacks(this);
            Follow(state);
        }

        public void Destroy()
        {
            if (_isDestroyed)
            {
                return;
            }

            _isDestroyed = true;
            _activity.UnregisterActivityLifecycleCallbacks(this);
            _registry.SetCurrentState(Lifecycle.State.Destroyed!);
        }

        public void OnEnterAmbient(AmbientLifecycleObserverAmbientDetails ambientDetails)
        {
            IsAmbient = true;
            _changed(true);
        }

        public void OnExitAmbient()
        {
            IsAmbient = false;
            _changed(false);
        }

        public void OnUpdateAmbient()
        {
        }

        public void OnActivityStarted(Activity activity)
        {
            Follow(AppState.Inactive);
        }

        public void OnActivityResumed(Activity activity)
        {
            Follow(AppState.Active);
        }

        public void OnActivityPaused(Activity activity)
        {
            Follow(AppState.Inactive);
        }

        public void OnActivityStopped(Activity activity)
        {
            Follow(AppState.Background);
        }

        public void OnActivityDestroyed(Activity activity)
        {
            Destroy();
        }

        public void OnActivityCreated(Activity activity, Bundle? savedInstanceState)
        {
        }

        public void OnActivitySaveInstanceState(Activity activity, Bundle outState)
        {
        }

        private void Follow(AppState state)
        {
            _registry.SetCurrentState(state switch
            {
                AppState.Active => Lifecycle.State.Resumed!,
                AppState.Inactive => Lifecycle.State.Started!,
                _ => Lifecycle.State.Created!,
            });
        }
    }
}
