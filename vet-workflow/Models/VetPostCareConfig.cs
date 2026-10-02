using System.Text.Json;
using System.Text.Json.Serialization;

namespace VetWorkflow;

public sealed class VetPostCareConfig
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("templates")]
    public List<PostCareTemplateItem> Templates { get; set; } = new();

    public static async Task<VetPostCareConfig> LoadAsync(string path = "vet_post_care_templates.json", CancellationToken ct = default)
    {
        if (!File.Exists(path))
        {
            return new VetPostCareConfig();
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<VetPostCareConfig>(json) ?? new VetPostCareConfig();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Erro ao ler {path}: {ex.Message}");
            return new VetPostCareConfig();
        }
    }
}

public sealed class PostCareTemplateItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("procedureType")]
    public string ProcedureType { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("redFlags")]
    public string RedFlags { get; set; } = string.Empty;
}
