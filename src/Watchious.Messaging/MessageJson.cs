using System.Text.Json;

namespace Watchious.Messaging;

// Messages are string dictionaries, as Plugin.Maui.WearableMessaging's MessageReceived gives them on the phone.
internal static class MessageJson
{
    public static byte[] Encode(IReadOnlyDictionary<string, string> message)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string>(message), MessagingJsonContext.Default.DictionaryStringString);
    }

    // Values that aren't strings (the plugin's application context is Dictionary<string, object> on the phone)
    // arrive as their JSON text.
    public static Dictionary<string, string>? Decode(ReadOnlySpan<byte> json)
    {
        try
        {
            var reader = new Utf8JsonReader(json);
            using var document = JsonDocument.ParseValue(ref reader);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var values = new Dictionary<string, string>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()!
                    : property.Value.GetRawText();
            }

            return values;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
