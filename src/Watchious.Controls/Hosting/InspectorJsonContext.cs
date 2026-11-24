using System.Text.Json.Serialization;

namespace Watchious.Hosting;

// Source-generated, so the inspector's JSON works under NativeAOT on the watch.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(InspectorEvent))]
[JsonSerializable(typeof(InspectorEvent[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class InspectorJsonContext : JsonSerializerContext;
