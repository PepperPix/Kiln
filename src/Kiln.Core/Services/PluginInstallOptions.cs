namespace Kiln.Services;

/// <summary>
/// Options for installing a plugin package.
/// </summary>
public sealed record PluginInstallOptions
{
    /// <summary>
    /// Gets the options used when none are supplied: no lock information, no forced overwrite.
    /// </summary>
    public static PluginInstallOptions Default { get; } = new();

    /// <summary>
    /// Gets a value indicating whether an existing plugin directory is replaced even if it has local changes
    /// or is not tracked in the lock file.
    /// </summary>
    public bool Force { get; init; }

    /// <summary>
    /// Gets the current lock entries by plugin name. When set, an existing plugin directory is only replaced
    /// if it is tracked and unmodified (or <see cref="Force"/> is set). When <c>null</c>, an existing directory is replaced.
    /// </summary>
    public IReadOnlyDictionary<string, PluginLockEntry>? ExistingLockEntries { get; init; }
}
