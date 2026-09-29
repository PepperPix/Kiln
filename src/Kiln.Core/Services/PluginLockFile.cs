namespace Kiln.Services;

using System.Text.Json;

public sealed class PluginLockFile : IPluginLockFile
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    public async Task<IReadOnlyDictionary<string, PluginLockEntry>> ReadAsync(string projectPath, CancellationToken ct = default)
    {
        var lockFilePath = GetLockFilePath(projectPath);
        if (!File.Exists(lockFilePath))
            return new Dictionary<string, PluginLockEntry>(StringComparer.OrdinalIgnoreCase);

        using var stream = File.OpenRead(lockFilePath);
        var doc = await JsonSerializer.DeserializeAsync<PluginLockFileDocument>(stream, SerializerOptions, ct).ConfigureAwait(false)
            ?? new PluginLockFileDocument();

        var result = new Dictionary<string, PluginLockEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in doc.Plugins)
            result[pair.Key] = pair.Value;

        return result;
    }

    public async Task SetAsync(string projectPath, string name, PluginLockEntry entry, CancellationToken ct = default)
    {
        var lockFilePath = GetLockFilePath(projectPath);
        var directory = Path.GetDirectoryName(lockFilePath)!;
        Directory.CreateDirectory(directory);

        var current = await ReadAsync(projectPath, ct).ConfigureAwait(false);
        var updated = new Dictionary<string, PluginLockEntry>(current, StringComparer.OrdinalIgnoreCase)
        {
            [name] = entry
        };

        var document = new PluginLockFileDocument
        {
            Plugins = updated
        };

        var json = JsonSerializer.Serialize(document, SerializerOptions);
        await File.WriteAllTextAsync(lockFilePath, json + Environment.NewLine, ct).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string projectPath, string name, CancellationToken ct = default)
    {
        var lockFilePath = GetLockFilePath(projectPath);
        if (!File.Exists(lockFilePath))
            return;

        var current = await ReadAsync(projectPath, ct).ConfigureAwait(false);
        if (!current.ContainsKey(name))
            return;

        var updated = new Dictionary<string, PluginLockEntry>(current, StringComparer.OrdinalIgnoreCase);
        updated.Remove(name);

        var document = new PluginLockFileDocument
        {
            Plugins = updated
        };

        var json = JsonSerializer.Serialize(document, SerializerOptions);
        await File.WriteAllTextAsync(lockFilePath, json + Environment.NewLine, ct).ConfigureAwait(false);
    }

    private static string GetLockFilePath(string projectPath)
        => Path.Combine(projectPath, ".kiln", "plugins.lock.json");

    private sealed class PluginLockFileDocument
    {
        public Dictionary<string, PluginLockEntry> Plugins { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
