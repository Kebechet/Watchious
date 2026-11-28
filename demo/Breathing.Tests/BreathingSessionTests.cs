using Xunit;

namespace Breathing.Tests;

public sealed class BreathingSessionTests
{
    private static readonly BreathingPattern Relax = BreathingPattern.Find("relax")!;

    [Theory]
    [InlineData(0, 1, BreathStep.BreatheIn, 4)]
    [InlineData(3.5, 1, BreathStep.BreatheIn, 1)]
    [InlineData(4, 1, BreathStep.HoldIn, 7)]
    [InlineData(11, 1, BreathStep.BreatheOut, 8)]
    [InlineData(18.9, 1, BreathStep.BreatheOut, 1)]
    [InlineData(19, 2, BreathStep.BreatheIn, 4)]
    public void At_FollowsThePatternBreathByBreath(double seconds, int breath, BreathStep step, int secondsLeft)
    {
        // Arrange
        var session = new BreathingSession(Relax, 2);

        // Act
        var moment = session.At(TimeSpan.FromSeconds(seconds));

        // Assert
        Assert.Equal(breath, moment.Breath);
        Assert.Equal(step, moment.Step.Step);
        Assert.Equal(secondsLeft, moment.SecondsLeft);
        Assert.False(moment.IsFinished);
    }

    [Fact]
    public void At_EndOfTheLastBreath_IsFinished()
    {
        // Arrange
        var session = new BreathingSession(Relax, 2);

        // Act
        var moment = session.At(session.Duration);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(38), session.Duration);
        Assert.True(moment.IsFinished);
        Assert.Equal(2, moment.Breath);
    }
}
