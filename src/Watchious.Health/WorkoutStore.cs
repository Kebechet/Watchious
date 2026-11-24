using System.Text.Json;
using System.Text.Json.Serialization;
using Kebechet.Types.Health.Enums;

namespace Watchious.Health;

// What HealthService keeps of a workout that outlives the app, beyond what the platform keeps: its id, title and
// pauses, and the heart rate summary so far.
internal interface IWorkoutStore
{
    SavedWorkout? Load();

    void Save(SavedWorkout workout);

    void Clear();
}

internal sealed record SavedWorkout(
    string Id,
    ActivityType ActivityType,
    string? Title,
    DateTimeOffset StartTime,
    WorkoutSessionState State,
    List<SavedPause> Pauses,
    int HeartRateCount,
    double HeartRateSum,
    double? MaxHeartRate,
    double? MinHeartRate);

internal sealed record SavedPause(DateTimeOffset Start, DateTimeOffset? End);

// A JSON file in the app's own data folder. A file that can't be read counts as no workout.
internal sealed class FileWorkoutStore(string path) : IWorkoutStore
{
    public static FileWorkoutStore InAppData()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new FileWorkoutStore(Path.Combine(folder, "Watchious", "workout.json"));
    }

    public SavedWorkout? Load()
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllBytes(path), WorkoutStoreJsonContext.Default.SavedWorkout) : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(SavedWorkout workout)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(workout, WorkoutStoreJsonContext.Default.SavedWorkout));
    }

    public void Clear()
    {
        File.Delete(path);
    }
}

// Source-generated, so it works under NativeAOT on the watch.
[JsonSerializable(typeof(SavedWorkout))]
internal sealed partial class WorkoutStoreJsonContext : JsonSerializerContext;
