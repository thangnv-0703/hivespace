using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HiveSpace.BuildingBlocks.Messaging;

/// <summary>The event envelope from <c>contracts/events/envelope.schema.json</c> (ADR-0002).</summary>
public sealed record EventEnvelope(
    Guid EventId,
    string EventType,
    int EventVersion,
    DateTimeOffset OccurredAt,
    string Producer,
    string CorrelationId,
    string? Traceparent,
    JsonElement Data)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Creates an envelope with a UUID v7 id (ADR-0009) and the current trace context (NFR-OBS-001).</summary>
    public static EventEnvelope Create<T>(string eventType, int eventVersion, string producer, string correlationId, T data)
    {
        var traceparent = Activity.Current?.Id;
        return new EventEnvelope(
            Guid.CreateVersion7(),
            eventType,
            eventVersion,
            DateTimeOffset.UtcNow,
            producer,
            correlationId,
            traceparent,
            JsonSerializer.SerializeToElement(data, JsonOptions));
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static EventEnvelope FromJson(string json) =>
        JsonSerializer.Deserialize<EventEnvelope>(json, JsonOptions)
        ?? throw new JsonException("Empty event envelope.");
}
