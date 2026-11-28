namespace Breathing;

// Where a session is at a moment: which breath (from 1), which step of it and how far into that step (0 to 1).
public readonly record struct SessionMoment(int Breath, int StepIndex, PatternStep Step, double StepProgress, int SecondsLeft,
    bool IsFinished);

// A session's timeline, computed from the time since it started rather than counted tick by tick, so a late timer tick
// (or the app paused by the system) never puts it out of step with the clock.
public sealed class BreathingSession(BreathingPattern pattern, int breaths)
{
    public BreathingPattern Pattern { get; } = pattern;

    public int Breaths { get; } = breaths;

    public TimeSpan Duration => TimeSpan.FromSeconds(Pattern.SecondsPerBreath * Breaths);

    public SessionMoment At(TimeSpan elapsed)
    {
        var lastStep = Pattern.Steps[^1];
        if (elapsed >= Duration)
        {
            return new SessionMoment(Breaths, Pattern.Steps.Count - 1, lastStep, 1, 0, IsFinished: true);
        }

        var seconds = Math.Max(elapsed.TotalSeconds, 0);
        var breath = (int)(seconds / Pattern.SecondsPerBreath);
        var intoBreath = seconds - breath * Pattern.SecondsPerBreath;
        for (var index = 0; index < Pattern.Steps.Count; index++)
        {
            var step = Pattern.Steps[index];
            if (intoBreath < step.Seconds)
            {
                return new SessionMoment(breath + 1, index, step, intoBreath / step.Seconds,
                    (int)Math.Ceiling(step.Seconds - intoBreath), IsFinished: false);
            }

            intoBreath -= step.Seconds;
        }

        // Only a rounding error ends up here, at the very end of a breath.
        return new SessionMoment(breath + 1, Pattern.Steps.Count - 1, lastStep, 1, 0, IsFinished: false);
    }
}
