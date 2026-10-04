using System;

namespace MafWorkflow.Shared.Contracts;

public class AgentInstructionDto
{
    public Guid Id { get; set; }
    public string WorkflowType { get; set; } = string.Empty;
    public string AgentRole { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
