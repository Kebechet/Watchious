using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.RenderTree;

namespace Watchious;

// Components are transparent: only elements and text reach the native host, so a component that renders
// <Label /> shows up as a Label directly inside its parent element.
internal sealed class UiTreeWriter(Func<int, ArrayRange<RenderTreeFrame>> getCurrentFrames)
{
    public const int ProtocolVersion = 1;

    private const string EventAttributePrefix = "on";

    public string Write(int rootComponentId)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteNumber("version", ProtocolVersion);
            json.WriteStartArray("children");
            WriteComponent(json, rootComponentId);
            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteComponent(Utf8JsonWriter json, int componentId)
    {
        var frames = getCurrentFrames(componentId);
        WriteFrames(json, frames.Array, 0, frames.Count);
    }

    private void WriteFrames(Utf8JsonWriter json, RenderTreeFrame[] frames, int start, int end)
    {
        var index = start;
        while (index < end)
        {
            var frame = frames[index];
            switch (frame.FrameType)
            {
                case RenderTreeFrameType.Element:
                    WriteElement(json, frames, index);
                    index += frame.ElementSubtreeLength;
                    break;
                case RenderTreeFrameType.Component:
                    WriteComponent(json, frame.ComponentId);
                    index += frame.ComponentSubtreeLength;
                    break;
                case RenderTreeFrameType.Region:
                    WriteFrames(json, frames, index + 1, index + frame.RegionSubtreeLength);
                    index += frame.RegionSubtreeLength;
                    break;
                case RenderTreeFrameType.Text:
                    WriteText(json, frame.TextContent);
                    index++;
                    break;
                case RenderTreeFrameType.Markup:
                    WriteText(json, frame.MarkupContent);
                    index++;
                    break;
                default:
                    index++;
                    break;
            }
        }
    }

    private void WriteElement(Utf8JsonWriter json, RenderTreeFrame[] frames, int elementIndex)
    {
        var element = frames[elementIndex];
        var end = elementIndex + element.ElementSubtreeLength;
        var childrenStart = elementIndex + 1;
        while (childrenStart < end && frames[childrenStart].FrameType == RenderTreeFrameType.Attribute)
        {
            childrenStart++;
        }

        json.WriteStartObject();
        json.WriteString("type", element.ElementName);
        WriteAttributes(json, frames, elementIndex + 1, childrenStart);
        WriteEvents(json, frames, elementIndex + 1, childrenStart);
        json.WriteStartArray("children");
        WriteFrames(json, frames, childrenStart, end);
        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void WriteAttributes(Utf8JsonWriter json, RenderTreeFrame[] frames, int start, int end)
    {
        json.WriteStartObject("attributes");
        for (var index = start; index < end; index++)
        {
            var attribute = frames[index];
            if (attribute.AttributeEventHandlerId != 0)
            {
                continue;
            }

            json.WritePropertyName(attribute.AttributeName);
            WriteValue(json, attribute.AttributeValue);
        }

        json.WriteEndObject();
    }

    private static void WriteEvents(Utf8JsonWriter json, RenderTreeFrame[] frames, int start, int end)
    {
        json.WriteStartObject("events");
        for (var index = start; index < end; index++)
        {
            var attribute = frames[index];
            if (attribute.AttributeEventHandlerId == 0)
            {
                continue;
            }

            json.WriteNumber(ToEventName(attribute.AttributeName), attribute.AttributeEventHandlerId);
        }

        json.WriteEndObject();
    }

    private static string ToEventName(string attributeName)
    {
        return attributeName.StartsWith(EventAttributePrefix, StringComparison.Ordinal)
            ? attributeName[EventAttributePrefix.Length..]
            : attributeName;
    }

    // Blazor already turns every element attribute value into a string, so the protocol's attribute values
    // are always strings. Controls format numbers and booleans culture-invariant before handing them over.
    private static void WriteValue(Utf8JsonWriter json, object? value)
    {
        if (value is null)
        {
            json.WriteNullValue();
            return;
        }

        json.WriteStringValue(value as string ?? value.ToString());
    }

    private static void WriteText(Utf8JsonWriter json, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        json.WriteStartObject();
        json.WriteString("type", "#text");
        json.WriteString("text", text);
        json.WriteEndObject();
    }
}
