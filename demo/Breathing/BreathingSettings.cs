using Watchious.Essentials;

namespace Breathing;

// What the app remembers on the watch, in IPreferences: the breaths picked for each pattern, whether to tap the wrist at
// each step, and the sessions finished so far.
public sealed class BreathingSettings(IPreferences preferences)
{
    public const int DefaultBreaths = 5;

    // After a finished session, for the pages that show the totals.
    public event EventHandler? Changed;

    public bool IsHapticsOn
    {
        get => preferences.Get("haptics", true);
        set => preferences.Set("haptics", value);
    }

    public int FinishedSessions => preferences.Get("sessions", 0);

    public TimeSpan TimeBreathing => TimeSpan.FromSeconds(preferences.Get("seconds", 0));

    public int BreathsFor(BreathingPattern pattern)
    {
        return preferences.Get($"breaths.{pattern.Id}", DefaultBreaths);
    }

    public void SetBreathsFor(BreathingPattern pattern, int breaths)
    {
        preferences.Set($"breaths.{pattern.Id}", breaths);
    }

    public void AddFinishedSession(TimeSpan duration)
    {
        preferences.Set("sessions", FinishedSessions + 1);
        preferences.Set("seconds", (int)(TimeBreathing + duration).TotalSeconds);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
