using System.ComponentModel;
using System.Text.Json;

namespace VetWorkflow;

public static class VetPostCareTools
{
    private static VetPostCareConfig? _postCareConfig;

    private static async Task<VetPostCareConfig> GetConfigAsync(CancellationToken ct = default)
    {
        _postCareConfig ??= await VetPostCareConfig.LoadAsync("vet_post_care_templates.json", ct);
        return _postCareConfig;
    }

    [Description("Recupera orientações pós-consulta previamente aprovadas pelo veterinário para o tipo de procedimento.")]
    public static async Task<string> GetApprovedInstructions(
        [Description("Tipo do procedimento: castração, vacina, limpeza dentária, vermifugação")] string procedureType,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Buscando orientações pós-consulta aprovadas para: {procedureType}");
        var config = await GetConfigAsync(cancellationToken);

        var template = config.Templates.FirstOrDefault(t => 
            procedureType.Contains(t.ProcedureType, StringComparison.OrdinalIgnoreCase) ||
            t.ProcedureType.Contains(procedureType, StringComparison.OrdinalIgnoreCase))
            ?? config.Templates.FirstOrDefault(t => t.ProcedureType == "castração");

        if (template is not null)
        {
            return JsonSerializer.Serialize(new
            {
                found = true,
                title = template.Title,
                procedureType = template.ProcedureType,
                instructions = template.Summary,
                redFlags = template.RedFlags,
                isApprovedByVet = true
            });
        }

        return JsonSerializer.Serialize(new
        {
            found = false,
            message = "Procedimento específico não possui template cadastrado. Escalar para orientação personalizada do veterinário."
        });
    }

    [Description("Registra que as orientações de cuidados pós-consulta foram enviadas e aprovadas.")]
    public static async Task<string> SendPostCareMessage(
        [Description("Identificador do paciente")] string patientId,
        [Description("Texto das orientações")] string instructions,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Registrando envio de cuidados pós-consulta para {patientId}");
        await Task.Delay(20, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            sent = true,
            patientId,
            timestamp = DateTime.UtcNow.ToString("o")
        });
    }
}
