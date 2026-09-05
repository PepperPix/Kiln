namespace Kiln.Services;

public interface IPluginLockFile
{
    Task<IReadOnlyDictionary<string, PluginLockEntry>> ReadAsync(string projectPath, CancellationToken ct = default);
    Task SetAsync(string projectPath, string name, PluginLockEntry entry, CancellationToken ct = default);
    Task RemoveAsync(string projectPath, string name, CancellationToken ct = default);
}
