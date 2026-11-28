using static Breathing.BreathStep;

namespace Breathing;

public enum BreathStep
{
    BreatheIn,
    HoldIn,
    BreatheOut,
    HoldOut,
}

public sealed record PatternStep(BreathStep Step, int Seconds);

// A breathing exercise: its steps make one breath, which a session repeats as many times as the user picks.
public sealed record BreathingPattern(string Id, string Name, string Purpose, IReadOnlyList<PatternStep> Steps)
{
    public static IReadOnlyList<BreathingPattern> All { get; } =
    [
        new("box", "Box", "Focus", [new(BreatheIn, 4), new(HoldIn, 4), new(BreatheOut, 4), new(HoldOut, 4)]),
        new("relax", "4-7-8", "Wind down", [new(BreatheIn, 4), new(HoldIn, 7), new(BreatheOut, 8)]),
        new("calm", "Calm", "Slow down", [new(BreatheIn, 5), new(BreatheOut, 5)]),
    ];

    public int SecondsPerBreath => Steps.Sum(step => step.Seconds);

    // "4 · 7 · 8": the seconds of each step.
    public string Rhythm => string.Join(" · ", Steps.Select(step => step.Seconds));

    public static BreathingPattern? Find(string id)
    {
        return All.FirstOrDefault(pattern => pattern.Id == id);
    }
}
