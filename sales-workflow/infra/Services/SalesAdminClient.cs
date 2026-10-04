using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MafWorkflow.Shared.Contracts;
using SalesWorkflow.Models;

namespace SalesWorkflow.Services;

public sealed class SalesAdminClient : IDisposable
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SalesAdminClient(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "mock-token:sales-workflow-worker");
    }

    // ── Configurações de Agentes ──

    public async Task<List<AgentInstructionDto>> GetAgentInstructionsAsync(string? workflowType = null, CancellationToken ct = default)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(workflowType) ? "agent-instructions" : $"agent-instructions?workflowType={Uri.EscapeDataString(workflowType)}";
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<List<AgentInstructionDto>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesAdminClient] Erro ao buscar instruções de agente: {ex.Message}");
            return [];
        }
    }

    // ── Produtos ──

    public async Task<List<ProductInfo>> SearchProductsSemanticAsync(string query, int top = 5, CancellationToken ct = default)
    {
        try
        {
            var url = $"search/products?q={Uri.EscapeDataString(query)}&top={top}";
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<List<ProductInfo>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesAdminClient] Erro ao buscar produtos semanticamente: {ex.Message}");
            return [];
        }
    }

    public async Task<ProductInfo?> GetProductBySkuAsync(string sku, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"products/sku/{Uri.EscapeDataString(sku)}", ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<ProductInfo>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesAdminClient] Erro ao obter produto por SKU '{sku}': {ex.Message}");
            return null;
        }
    }

    public async Task<List<ProductInfo>> GetProductsBySkusAsync(IEnumerable<string> skus, CancellationToken ct = default)
    {
        var result = new List<ProductInfo>();
        foreach (var sku in skus.Distinct())
        {
            var prod = await GetProductBySkuAsync(sku, ct);
            if (prod is not null)
            {
                result.Add(prod);
            }
        }
        return result;
    }

    // ── Descontos & Cupons ──

    public async Task<DiscountInfo?> ValidateCouponAsync(string code, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"discounts/code/{Uri.EscapeDataString(code)}", ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<DiscountInfo>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesAdminClient] Erro ao validar cupom '{code}': {ex.Message}");
            return null;
        }
    }

    public async Task<List<DiscountInfo>> GetActiveDiscountsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("discounts?validNow=true", ct);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<List<DiscountInfo>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesAdminClient] Erro ao listar descontos ativos: {ex.Message}");
            return [];
        }
    }

    // ── Condições de Pagamento ──

    public async Task<List<PaymentConditionInfo>> GetPaymentConditionsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("payment-conditions?active=true", ct);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<List<PaymentConditionInfo>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesAdminClient] Erro ao listar condições de pagamento: {ex.Message}");
            return [];
        }
    }

    // ── Clientes ──

    public async Task<CustomerInfo?> FindCustomerAsync(string identifier, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"customers/find?identifier={Uri.EscapeDataString(identifier)}", ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<CustomerInfo>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesAdminClient] Erro ao buscar cliente '{identifier}': {ex.Message}");
            return null;
        }
    }

    public async Task<CustomerInfo?> GetCustomerByIdAsync(string id, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"customers/{Uri.EscapeDataString(id)}", ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<CustomerInfo>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesAdminClient] Erro ao buscar cliente por ID '{id}': {ex.Message}");
            return null;
        }
    }

    public async Task<CustomerInfo?> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        try
        {
            var content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("customers", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                Console.WriteLine($"[SalesAdminClient] Falha ao criar cliente: {response.StatusCode} - {err}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<CustomerInfo>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesAdminClient] Erro ao criar cliente '{request.Name}': {ex.Message}");
            return null;
        }
    }

    public async Task<CustomerInfo?> UpdateCustomerAsync(string id, UpdateCustomerRequest request, CancellationToken ct = default)
    {
        try
        {
            var content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _http.PutAsync($"customers/{Uri.EscapeDataString(id)}", content, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<CustomerInfo>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesAdminClient] Erro ao atualizar cliente '{id}': {ex.Message}");
            return null;
        }
    }

    public async Task<CustomerAddressInfo?> AddCustomerAddressAsync(string customerId, CreateAddressRequest request, CancellationToken ct = default)
    {
        try
        {
            var content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"customers/{Uri.EscapeDataString(customerId)}/addresses", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                Console.WriteLine($"[SalesAdminClient] Falha ao adicionar endereço para cliente '{customerId}': {response.StatusCode} - {err}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<CustomerAddressInfo>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesAdminClient] Erro ao adicionar endereço ao cliente '{customerId}': {ex.Message}");
            return null;
        }
    }

    // ── Carrinhos Abandonados ──

    public async Task<AbandonedCartInfo?> GetAbandonedCartAsync(string customerId, CancellationToken ct = default)
    {
        // Se a API tiver carrinho abandonado implementado ou via simulação
        await Task.CompletedTask;
        return new AbandonedCartInfo
        {
            CustomerId = customerId,
            CartId = $"CART-{customerId.GetHashCode() & 0xFFFF}",
            AbandonedAt = DateTimeOffset.UtcNow.AddDays(-2),
            Items = [],
            Total = 0,
            ItemsStillAvailable = true
        };
    }

    // ── Follow-Ups ──

    public async Task<bool> CreateFollowUpAsync(
        string customerId,
        string module,
        string followUpType,
        string? referenceId,
        string? referenceTitle,
        DateTimeOffset scheduledFor,
        string channel,
        string messageText,
        string? notes = null,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                CustomerId = Guid.TryParse(customerId, out var cid) ? (Guid?)cid : null,
                CustomerIdentifier = customerId,
                Module = module,
                FollowUpType = followUpType,
                ReferenceId = referenceId,
                ReferenceTitle = referenceTitle,
                ScheduledFor = scheduledFor,
                Channel = channel,
                MessageText = messageText,
                Notes = notes
            };

            var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("follow-ups", content, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesAdminClient] Erro ao registrar follow-up: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
