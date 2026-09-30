namespace Kiln.Services;

/// <summary>
/// Reads and writes the plugin lock file of a site (<c>.kiln/plugins.lock.json</c>), which records the installed plugin packages.
/// </summary>
public interface IPluginLockFile
{
    /// <summary>
    /// Reads the lock entries of a site.
    /// </summary>
    /// <param name="projectPath">The project directory.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The entries keyed by plugin name (case-insensitive); empty when the lock file does not exist.</returns>
    Task<IReadOnlyDictionary<string, PluginLockEntry>> ReadAsync(string projectPath, CancellationToken ct = default);

    /// <summary>
    /// Adds or replaces the lock entry of a plugin, creating the lock file if necessary.
    /// </summary>
    /// <param name="projectPath">The project directory.</param>
    /// <param name="name">The plugin name.</param>
    /// <param name="entry">The entry to store.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task that completes when the lock file has been written.</returns>
    Task SetAsync(string projectPath, string name, PluginLockEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Removes the lock entry of a plugin. Nothing happens when there is no such entry.
    /// </summary>
    /// <param name="projectPath">The project directory.</param>
    /// <param name="name">The plugin name.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task that completes when the lock file has been updated.</returns>
    Task RemoveAsync(string projectPath, string name, CancellationToken ct = default);
}
