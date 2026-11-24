namespace Watchious.Essentials;

// MAUI Essentials' ITextToSpeech: AVSpeechSynthesizer on watchOS, Android's TextToSpeech on Wear OS. SpeakAsync
// completes once the text has been spoken; cancelling stops the speech and cancels the task. A watch without a speech
// engine throws FeatureNotSupportedException.
public interface ITextToSpeech
{
    Task<IEnumerable<Locale>> GetLocalesAsync();

    Task SpeakAsync(string text, SpeechOptions? options = null, CancellationToken cancelToken = default);
}

public sealed class SpeechOptions
{
    // From 0 to 2; 1 is the voice's own.
    public float? Pitch { get; set; }

    // From 0 to 1.
    public float? Volume { get; set; }

    // One of GetLocalesAsync's, or the watch's language when null.
    public Locale? Locale { get; set; }
}

// A language the watch can speak, such as Language "en" and Country "US".
public sealed record Locale(string Language, string? Country, string Name, string Id);
