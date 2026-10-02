using System.ComponentModel;
using System.Text.Json.Serialization;
using SalesWorkflow.Models;

namespace SalesWorkflow.AiTools;

public sealed class AbandonedCartInfo
{
    [JsonPropertyName("customer_id")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonPropertyName("cart_id")]
    public string CartId { get; set; } = string.Empty;

    [JsonPropertyName("abandoned_at")]
    public DateTimeOffset AbandonedAt { get; set; }

    [JsonPropertyName("items")]
    public List<ProductInfo> Items { get; set; } = [];

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("items_still_available")]
    public bool ItemsStillAvailable { get; set; }
}

public static class CartTools
{
    [Description("Recupera os produtos do carrinho de compras abandonado de um cliente.")]
    public static async Task<AbandonedCartInfo> GetAbandonedCart(
        [Description("Identificador do cliente (email, CPF, telefone ou nome)")] string customerId,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Buscando carrinho abandonado de: {customerId}");
        await Task.Delay(100, cancellationToken);

        var catalog = CatalogTools.LoadCatalog();
        var items = catalog.Take(2).ToList();

        return new AbandonedCartInfo
        {
            CustomerId = customerId,
            CartId = "CART-8821",
            AbandonedAt = DateTimeOffset.UtcNow.AddDays(-3),
            Items = items,
            Total = items.Sum(i => i.Price),
            ItemsStillAvailable = items.All(i => i.InStock)
        };
    }

    [Description("Envia um lembrete com link de recuperação de carrinho para o cliente.")]
    public static async Task<bool> SendCartReminder(
        [Description("Identificador do cliente")] string customerId,
        [Description("Canal de contato: 'whatsapp', 'email' ou 'sms'")] string channel,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Disparando lembrete de carrinho via {channel} para: {customerId}");
        await Task.Delay(80, cancellationToken);
        return true;
    }
}
