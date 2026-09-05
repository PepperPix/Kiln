namespace Kiln.Services;

public sealed record PluginPackageInstallResult(string PackageId, string Version, string PluginName, string InstallPath);
