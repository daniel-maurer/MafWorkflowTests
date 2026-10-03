using System.Text.Json.Serialization;

namespace SalesWorkflow.Models;

public sealed class FollowUpResult
{
    [JsonPropertyName("follow_up_scheduled")]
    public bool FollowUpScheduled { get; set; }

    [JsonPropertyName("scheduled_at")]
    public DateTimeOffset? ScheduledAt { get; set; }

    [JsonPropertyName("follow_up_type")]
    public string FollowUpType { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "whatsapp";

    [JsonPropertyName("message_for_user")]
    public string MessageForUser { get; set; } = string.Empty;
}
