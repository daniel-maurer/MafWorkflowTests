using System.Text.Json;
using System.Text.Json.Serialization;

namespace VetWorkflow;

public sealed class VetAlertSignsConfig
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("emergencySigns")]
    public List<AlertSignItem> EmergencySigns { get; set; } = new();

    public static async Task<VetAlertSignsConfig> LoadAsync(string path = "vet_alert_signs.json", CancellationToken ct = default)
    {
        if (!File.Exists(path))
        {
            return new VetAlertSignsConfig();
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<VetAlertSignsConfig>(json) ?? new VetAlertSignsConfig();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Erro ao ler {path}: {ex.Message}");
            return new VetAlertSignsConfig();
        }
    }
}

public sealed class AlertSignItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "CRITICAL";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
