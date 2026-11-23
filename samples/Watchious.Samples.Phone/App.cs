namespace Watchious.Samples.Phone;

public sealed class App(MainPage mainPage) : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new NavigationPage(mainPage));
    }
}
