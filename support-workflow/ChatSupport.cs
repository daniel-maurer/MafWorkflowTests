using System.Text.Json.Serialization;

namespace SupportWorkflow;

/// <summary>
/// Represents a support chat message with user and content information.
/// </summary>
public sealed class ChatSupport
{
    /// <summary>
    /// Gets or sets the unique identifier of the user.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content of the chat message.
    /// </summary>
    [JsonPropertyName("chat_content")]
    public string ChatContent { get; set; } = string.Empty;
}