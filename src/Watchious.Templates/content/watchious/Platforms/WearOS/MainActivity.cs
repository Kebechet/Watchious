using Android.App;
using Watchious.Hosting;
using Watchious.Hosts.WearOS;

namespace WatchiousApp1;

[Activity(Label = "WatchiousApp1", MainLauncher = true, Exported = true)]
public class MainActivity : WatchiousActivity
{
    protected override WatchiousApp CreateWatchiousApp()
    {
        return WatchiousProgram.CreateWatchiousApp();
    }
}
