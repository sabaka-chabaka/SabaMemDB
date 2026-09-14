using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SabaMemDb.Health;

public static class HealthCheckResponseWriter
{
    public static async Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        using var stream = new MemoryStream();
        await using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("status", report.Status.ToString());
            writer.WriteString("totalDuration", report.TotalDuration.ToString());

            writer.WriteStartObject("entries");
            foreach (var entry in report.Entries)
            {
                writer.WriteStartObject(entry.Key);
                writer.WriteString("status", entry.Value.Status.ToString());
                writer.WriteString("description", entry.Value.Description);
                writer.WriteString("duration", entry.Value.Duration.ToString());

                if (entry.Value.Exception != null)
                {
                    writer.WriteString("exception", entry.Value.Exception.Message);
                }

                writer.WriteStartObject("data");
                foreach (var item in entry.Value.Data)
                {
                    writer.WritePropertyName(item.Key);
                    WriteDataValue(writer, item.Value);
                }
                writer.WriteEndObject();

                writer.WriteEndObject();
            }
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        context.Response.ContentLength = stream.Length;
        stream.Position = 0;
        await stream.CopyToAsync(context.Response.Body);
    }

    private static void WriteDataValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case float f:
                writer.WriteNumberValue(f);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}
