using System.ComponentModel;
using System.Text.Json;

namespace VetWorkflow;

public static class VetDataCollectionTools
{
    [Description("Analisa os dados já fornecidos pelo tutor e retorna a lista de campos ainda pendentes para completar a ficha.")]
    public static async Task<string> RequestMissingData(
        [Description("JSON com os dados já coletados do paciente")] string currentDataJson,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Verificando dados faltantes para: {currentDataJson}");
        await Task.Delay(20, cancellationToken);

        var missing = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(currentDataJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("pet_name", out var pet) || string.IsNullOrWhiteSpace(pet.GetString()))
                missing.Add("nome_do_animal");

            if (!root.TryGetProperty("species", out var sp) || string.IsNullOrWhiteSpace(sp.GetString()))
                missing.Add("espécie");

            if (!root.TryGetProperty("age", out var age) || string.IsNullOrWhiteSpace(age.GetString()))
                missing.Add("idade");

            if (!root.TryGetProperty("weight_kg", out var wt) || wt.GetDecimal() <= 0)
                missing.Add("peso");
        }
        catch
        {
            missing.AddRange(new[] { "nome_do_animal", "espécie", "idade", "peso" });
        }

        return JsonSerializer.Serialize(new
        {
            isComplete = missing.Count == 0,
            missingFields = missing,
            suggestedQuestion = missing.Count > 0 
                ? $"Poderia me informar: {string.Join(", ", missing)}?" 
                : "Todos os dados básicos foram coletados com sucesso."
        });
    }

    [Description("Valida e estrutura os dados do paciente coletados (espécie, raça, idade, peso, sintomas, medicação).")]
    public static async Task<string> ValidatePatientData(
        [Description("JSON com dados brutos coletados da conversa")] string rawDataJson,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Validando dados cadastrais do paciente: {rawDataJson}");
        await Task.Delay(30, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            valid = true,
            status = "Dados validados e prontos para armazenamento na ficha do paciente."
        });
    }
}
