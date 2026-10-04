using System.Text.Json;

namespace AgentWorkflow.Bff.Services;

public interface IBffNoSqlStore
{
    Task<T?> GetDocumentAsync<T>(string collectionName, string id, CancellationToken cancellationToken = default) where T : class;
    Task UpsertDocumentAsync<T>(string collectionName, string id, T document, CancellationToken cancellationToken = default) where T : class;
    Task DeleteDocumentAsync(string collectionName, string id, CancellationToken cancellationToken = default);
}

public sealed class JsonBffNoSqlStore : IBffNoSqlStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public JsonBffNoSqlStore(IWebHostEnvironment env)
    {
        var infraDir = Path.Combine(env.ContentRootPath, "infra", "data");
        if (!Directory.Exists(infraDir))
        {
            Directory.CreateDirectory(infraDir);
        }
        _filePath = Path.Combine(infraDir, "bff_nosql_db.json");
    }

    private async Task<Dictionary<string, Dictionary<string, JsonElement>>> LoadDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json, _jsonOptions)
                   ?? new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveDatabaseAsync(Dictionary<string, Dictionary<string, JsonElement>> db, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(db, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }

    public async Task<T?> GetDocumentAsync<T>(string collectionName, string id, CancellationToken cancellationToken = default) where T : class
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var db = await LoadDatabaseAsync(cancellationToken);
            if (db.TryGetValue(collectionName, out var collection) && collection.TryGetValue(id, out var element))
            {
                return JsonSerializer.Deserialize<T>(element.GetRawText(), _jsonOptions);
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpsertDocumentAsync<T>(string collectionName, string id, T document, CancellationToken cancellationToken = default) where T : class
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var db = await LoadDatabaseAsync(cancellationToken);
            if (!db.TryGetValue(collectionName, out var collection))
            {
                collection = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                db[collectionName] = collection;
            }

            var docJson = JsonSerializer.Serialize(document, _jsonOptions);
            using var doc = JsonDocument.Parse(docJson);
            collection[id] = doc.RootElement.Clone();

            await SaveDatabaseAsync(db, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteDocumentAsync(string collectionName, string id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var db = await LoadDatabaseAsync(cancellationToken);
            if (db.TryGetValue(collectionName, out var collection) && collection.Remove(id))
            {
                await SaveDatabaseAsync(db, cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}
