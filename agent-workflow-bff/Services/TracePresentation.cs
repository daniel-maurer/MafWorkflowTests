using AgentWorkflow.Bff.Contracts;

namespace AgentWorkflow.Bff.Services;

/// <summary>
/// Maps semantic trace levels to presentation attributes (icon + color).
/// Centralizes all trace presentation logic that was previously in the backend's TraceConstants.
/// </summary>
public static class TracePresentation
{
    public static (string Icon, string Color) FromLevel(string? level) => level switch
    {
        "success" => ("check-circle", "success"),
        "warning" => ("alert-triangle", "warning"),
        "error"   => ("siren", "error"),
        _         => ("terminal", "primary"), // "info" or any default
    };

    /// <summary>
    /// Maps a system message style to a presentation icon.
    /// </summary>
    public static string IconForSystemStyle(string? systemStyle) => systemStyle switch
    {
        "escalate" => "siren",
        "resolved" => "check-circle",
        "handoff"  => "user-check",
        _          => "info",
    };

    /// <summary>
    /// Maps slim BackendMessageDto to fully-enriched MessageDto using workflow configuration.
    /// </summary>
    public static MessageDto EnrichMessage(BackendMessageDto backend, WorkflowDefinitionDto? workflow)
    {
        string senderName = "MAF Agent";
        string icon = "git-branch";
        string? bubbleStyle = "agent";

        if (backend.SenderType == "user")
        {
            senderName = "Customer";
            icon = "user";
            bubbleStyle = "user";
        }
        else if (backend.SenderType == "human" || backend.AgentId == "human-support")
        {
            var agent = workflow?.Agents.FirstOrDefault(a => a.Id == "human-support");
            senderName = agent?.Title ?? "Human Agent";
            icon = agent?.Icon ?? "headphones";
            bubbleStyle = "human";
        }
        else if (backend.SenderType == "system")
        {
            senderName = "System";
            icon = IconForSystemStyle(backend.SystemStyle);
            bubbleStyle = "system";
        }
        else if (backend.SenderType == "agent")
        {
            if (!string.IsNullOrEmpty(backend.AgentId))
            {
                var agent = workflow?.Agents.FirstOrDefault(a => a.Id.Equals(backend.AgentId, StringComparison.OrdinalIgnoreCase));
                if (agent is not null)
                {
                    senderName = agent.Title;
                    icon = agent.Icon;
                    bubbleStyle = agent.Id;
                }
            }
        }

        return new MessageDto(
            backend.Id,
            backend.Type,
            backend.Side,
            backend.SenderType,
            senderName,
            icon,
            bubbleStyle,
            backend.SystemStyle,
            backend.Text,
            backend.Tools,
            backend.CreatedAt,
            backend.SplitMirror,
            backend.Audience);
    }
}

