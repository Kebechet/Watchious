using System.Text.Json.Serialization;

namespace Watchious.Messaging;

// Source-generated, so the messages' JSON works under NativeAOT on the watch.
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class MessagingJsonContext : JsonSerializerContext;
