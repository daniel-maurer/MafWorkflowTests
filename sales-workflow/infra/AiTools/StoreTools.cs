using System.ComponentModel;
using SalesWorkflow.Models;
using SalesWorkflow.Services;
using System.Text.Json;

namespace SalesWorkflow.AiTools;

public sealed class StoreTools
{
    private readonly SalesAdminClient _client;

    public StoreTools(SalesAdminClient client)
    {
        _client = client;
    }

    [Description("Obtém as formas de entrega (delivery) e retirada (pickup) ativas, incluindo preços e descrições.")]
    public async Task<string> GetDeliveryMethods(CancellationToken cancellationToken = default)
    {
        var data = await _client.GetDeliveryMethodsAsync(cancellationToken);
        return JsonSerializer.Serialize(data);
    }

    [Description("Obtém informações da loja como endereço completo, telefone de contato e email. Útil para quando o cliente pergunta onde é a loja física.")]
    public async Task<string> GetStoreInfo(CancellationToken cancellationToken = default)
    {
        var data = await _client.GetStoreInfoAsync(cancellationToken);
        return JsonSerializer.Serialize(data);
    }
}
