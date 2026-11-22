using Android.App;
using Watchious.Hosting;
using Watchious.Hosts.WearOS;

namespace Watchious.Samples.Counter.App;

[Activity(
    Name = "com.kebechet.watchious.samples.counter.MainActivity",
    Label = "Watchious Counter",
    MainLauncher = true,
    Exported = true)]
public class MainActivity : WatchiousActivity
{
    protected override WatchiousApp CreateWatchiousApp()
    {
        return CounterApp.CreateWatchiousApp();
    }
}
