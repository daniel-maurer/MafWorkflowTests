using System.ComponentModel;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow.AiTools;

public sealed class CampaignTools
{
    private readonly SalesAdminClient _client;

    public CampaignTools(SalesAdminClient client)
    {
        _client = client;
    }

    [Description("Obtém as regras das campanhas promocionais ativas no momento (ex: frete grátis, descontos progressivos por quantidade de itens, descontos de Black Friday ou aniversário). Use esta ferramenta para verificar se pode oferecer condições especiais para o cliente.")]
    public async Task<string> GetActiveCampaigns(CancellationToken cancellationToken = default)
    {
        var json = await _client.GetActiveCampaignsJsonAsync(cancellationToken);
        return json;
    }
}
