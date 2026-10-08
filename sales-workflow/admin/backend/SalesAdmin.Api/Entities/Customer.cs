using System.Text.Json.Serialization;
using Pgvector;

namespace SalesAdmin.Api.Entities;

public sealed class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? DocumentNumber { get; set; }
    public string? DocumentType { get; set; } = "cpf"; // "cpf" | "cnpj"
    public string CustomerType { get; set; } = "individual"; // "individual" | "business"
    public string? Notes { get; set; }

    [JsonIgnore]
    public Vector? Embedding { get; set; }

    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<CustomerAddress> Addresses { get; set; } = [];

    [JsonIgnore]
    public List<FollowUpRecord> FollowUps { get; set; } = [];

    [JsonIgnore]
    public List<Order> Orders { get; set; } = [];

    [JsonIgnore]
    public List<Conversation> Conversations { get; set; } = [];
}
