namespace Watchious.Navigation;

// A stack of pages, shown with each platform's navigation: SwiftUI's NavigationStack (back button and edge swipe)
// on watchOS, swipe-to-dismiss and the back button on Wear OS. The page of the app (UseWatchiousApp) is the root.
public interface INavigation
{
    // The routes of the pages on the stack, the root page first.
    IReadOnlyList<string> Stack { get; }

    bool CanGoBack { get; }

    // Pushes the page whose @page route matches, e.g. Navigation.GoToAsync(Routes.Workout(exerciseId: 5)).
    Task GoToAsync(string route);

    Task GoBackAsync();

    Task GoBackToRootAsync();
}
