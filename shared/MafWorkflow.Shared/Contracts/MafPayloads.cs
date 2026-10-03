using System.Text.Json.Serialization;

namespace MafWorkflow.Shared;

public sealed class MafEventEnvelope
{
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = string.Empty;
    [JsonPropertyName("eventType")] public string EventType { get; set; } = string.Empty;
    [JsonPropertyName("payload")] public object? Payload { get; set; }
    [JsonPropertyName("occurredAt")] public DateTime OccurredAt { get; set; }
    [JsonPropertyName("sequenceId")] public string SequenceId { get; set; } = string.Empty;
}

public sealed class MafImagePayload
{
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    [JsonPropertyName("alt")] public string Alt { get; set; } = string.Empty;
    [JsonPropertyName("sku")] public string? Sku { get; set; }
}

public sealed class MafMessagePayload
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("side")] public string Side { get; set; } = string.Empty;
    [JsonPropertyName("senderType")] public string SenderType { get; set; } = string.Empty;
    [JsonPropertyName("agentId")] public string? AgentId { get; set; }
    [JsonPropertyName("systemStyle")] public string? SystemStyle { get; set; }
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    [JsonPropertyName("tools")] public IReadOnlyList<object> Tools { get; set; } = Array.Empty<object>();
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("splitMirror")] public bool SplitMirror { get; set; }
    [JsonPropertyName("audience")] public string Audience { get; set; } = MessageAudience.Both;
    [JsonPropertyName("images")] public IReadOnlyList<MafImagePayload>? Images { get; set; }
}

public sealed class MafToolCallPayload
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("args")] public string Args { get; set; } = string.Empty;
    [JsonPropertyName("ok")] public bool Ok { get; set; } = true;
}

public sealed class MafTracePayload
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("time")] public DateTime Time { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("level")] public string Level { get; set; } = "info";
}

public sealed class MafTypingPayload
{
    [JsonPropertyName("container")] public string Container { get; set; } = "msgs";
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("on")] public bool On { get; set; }
}

public sealed class MafAgentPayload
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("state")] public string State { get; set; } = string.Empty;
    [JsonPropertyName("tag")] public string Tag { get; set; } = string.Empty;
    [JsonPropertyName("activeTools")] public IReadOnlyList<object> ActiveTools { get; set; } = Array.Empty<object>();
}

public sealed class MafContextPayload
{
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("chatTitle")] public string ChatTitle { get; set; } = string.Empty;
    [JsonPropertyName("chatSubtitle")] public string ChatSubtitle { get; set; } = string.Empty;
    [JsonPropertyName("activeAgentId")] public string ActiveAgentId { get; set; } = string.Empty;
    [JsonPropertyName("humanMode")] public bool HumanMode { get; set; }
}

public sealed class MafKbPayload
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    [JsonPropertyName("score")] public double Score { get; set; }
    [JsonPropertyName("summary")] public string Summary { get; set; } = string.Empty;
    [JsonPropertyName("resolutionType")] public string ResolutionType { get; set; } = string.Empty;
    [JsonPropertyName("tags")] public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
}
