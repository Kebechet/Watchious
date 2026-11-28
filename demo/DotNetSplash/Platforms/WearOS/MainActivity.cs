using Android.App;
using Android.Content.PM;
using Watchious.Hosting;
using Watchious.Hosts.WearOS;

namespace DotNetSplash;

// Compose redraws itself on these changes; without them Android would restart the activity, and with it the app.
[Activity(Label = ".NET", MainLauncher = true, Exported = true,
    ConfigurationChanges = ConfigChanges.Density | ConfigChanges.FontScale | ConfigChanges.Keyboard
        | ConfigChanges.KeyboardHidden | ConfigChanges.LayoutDirection | ConfigChanges.Locale | ConfigChanges.Navigation
        | ConfigChanges.Orientation | ConfigChanges.ScreenLayout | ConfigChanges.ScreenSize
        | ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode)]
public class MainActivity : WatchiousActivity
{
    protected override WatchiousApp CreateWatchiousApp()
    {
        return WatchiousProgram.CreateWatchiousApp();
    }
}
