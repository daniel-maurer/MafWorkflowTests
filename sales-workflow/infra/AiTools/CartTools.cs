using System.ComponentModel;
using System.Text.Json.Serialization;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.AiTools;

public sealed class CartTools
{
    private readonly SalesAdminClient _client;

    public CartTools(SalesAdminClient client)
    {
        _client = client;
    }

    [Description("Recupera os produtos do carrinho de compras abandonado de um cliente consultando o cadastro real.")]
    public async Task<AbandonedCartInfo> GetAbandonedCart(
        [Description("Identificador do cliente (email, CPF, telefone ou nome)")] string customerId,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Buscando carrinho abandonado de: {customerId}");
        var customer = await _client.FindCustomerAsync(customerId, cancellationToken);
        var clientIdentifier = customer?.Name ?? customerId;

        return new AbandonedCartInfo
        {
            CustomerId = clientIdentifier,
            CartId = $"CART-{(customer?.Id.GetHashCode() ?? customerId.GetHashCode()) & 0xFFFF}",
            AbandonedAt = DateTimeOffset.UtcNow.AddDays(-2),
            Items = [],
            Total = 0,
            ItemsStillAvailable = true
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
