using System.Text.Json.Serialization;

namespace SalesWorkflow.Models;

public sealed class CustomerInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("phone")] public string? Phone { get; set; }
    [JsonPropertyName("documentNumber")] public string? DocumentNumber { get; set; }
    [JsonPropertyName("documentType")] public string? DocumentType { get; set; }
    [JsonPropertyName("customerType")] public string CustomerType { get; set; } = "individual";
    [JsonPropertyName("notes")] public string? Notes { get; set; }
    [JsonPropertyName("addresses")] public List<CustomerAddressInfo> Addresses { get; set; } = [];
}

public sealed class CustomerAddressInfo
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("street")] public string Street { get; set; } = string.Empty;
    [JsonPropertyName("number")] public string Number { get; set; } = string.Empty;
    [JsonPropertyName("complement")] public string? Complement { get; set; }
    [JsonPropertyName("neighborhood")] public string Neighborhood { get; set; } = string.Empty;
    [JsonPropertyName("city")] public string City { get; set; } = string.Empty;
    [JsonPropertyName("state")] public string State { get; set; } = string.Empty;
    [JsonPropertyName("zipCode")] public string ZipCode { get; set; } = string.Empty;
    [JsonPropertyName("country")] public string Country { get; set; } = "BR";
    [JsonPropertyName("isDefault")] public bool IsDefault { get; set; }
}

public sealed class CreateCustomerRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("phone")] public string? Phone { get; set; }
    [JsonPropertyName("documentNumber")] public string? DocumentNumber { get; set; }
    [JsonPropertyName("documentType")] public string? DocumentType { get; set; } = "cpf";
    [JsonPropertyName("customerType")] public string? CustomerType { get; set; } = "individual";
    [JsonPropertyName("notes")] public string? Notes { get; set; }
    [JsonPropertyName("active")] public bool Active { get; set; } = true;
    [JsonPropertyName("addresses")] public List<CreateAddressRequest>? Addresses { get; set; }
}

public sealed class UpdateCustomerRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("phone")] public string? Phone { get; set; }
    [JsonPropertyName("documentNumber")] public string? DocumentNumber { get; set; }
    [JsonPropertyName("documentType")] public string? DocumentType { get; set; }
    [JsonPropertyName("customerType")] public string? CustomerType { get; set; }
    [JsonPropertyName("notes")] public string? Notes { get; set; }
    [JsonPropertyName("active")] public bool Active { get; set; } = true;
}

public sealed class CreateAddressRequest
{
    [JsonPropertyName("label")] public string Label { get; set; } = "Entrega";
    [JsonPropertyName("street")] public string Street { get; set; } = string.Empty;
    [JsonPropertyName("number")] public string Number { get; set; } = string.Empty;
    [JsonPropertyName("complement")] public string? Complement { get; set; }
    [JsonPropertyName("neighborhood")] public string Neighborhood { get; set; } = string.Empty;
    [JsonPropertyName("city")] public string City { get; set; } = string.Empty;
    [JsonPropertyName("state")] public string State { get; set; } = string.Empty;
    [JsonPropertyName("zipCode")] public string ZipCode { get; set; } = string.Empty;
    [JsonPropertyName("country")] public string Country { get; set; } = "BR";
    [JsonPropertyName("isDefault")] public bool IsDefault { get; set; } = true;
}
