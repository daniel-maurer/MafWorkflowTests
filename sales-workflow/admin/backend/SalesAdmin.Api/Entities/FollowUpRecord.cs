using System.Text.Json.Serialization;

namespace SalesAdmin.Api.Entities;

public sealed class FollowUpRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    [JsonIgnore]
    public Customer? Customer { get; set; }

    public string Module { get; set; } = "sales"; // "sales", "vet", "support"
    public string FollowUpType { get; set; } = string.Empty; // "quote_reminder", "cart_recovery", "foto_ferida", "retorno_vacina", etc.
    public string? ReferenceId { get; set; } // Id do orçamento, pedido, paciente ou ticket
    public string? ReferenceTitle { get; set; } // Ex: "Orçamento #1042 - R$ 340,00" ou "Paciente: Pipoca (Felino)"

    public DateTimeOffset ScheduledFor { get; set; }
    public string Channel { get; set; } = "WhatsApp"; // "WhatsApp", "Email", "SMS", "Notification"
    public string MessageText { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending"; // "Pending", "Sent", "Cancelled", "Completed"
    public DateTimeOffset? SentAt { get; set; }
    public string? Notes { get; set; }
    public string? CustomDataJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
