namespace Kiln.Cli.Tests;

using Kiln.Cli.Commands;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;

public class PluginCommandAppTests
{
    [Test]
    public async Task PluginSearchCommand_ShowsResultsFromNuGet()
    {
        var client = new FakeNuGetPluginClient
        {
            SearchResults =
            [
                new PluginSearchResult("Kiln.Plugin.EmailProtect", "1.2.3", "Protect emails from scrapers")
            ]
        };

        var (app, console) = CreateApp(client, new PluginLockFile());

        var result = await app.RunAsync(["plugin", "search", "email-protect"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Kiln.Plugin.EmailProtect");
        await Assert.That(console.Output).Contains("1.2.3");
    }

    [Test]
    public async Task PluginAddCommand_WritesLockFileAndWarnsAboutSecurity()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-add-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var client = new FakeNuGetPluginClient
            {
                InstallResult = new PluginPackageInstallResult("Kiln.Plugin.EmailProtect", "1.2.3", "email-protect", Path.Combine(projectDir, "plugins", "email-protect"))
            };

            var (app, console) = CreateApp(client, new PluginLockFile());
            var result = await app.RunAsync(["plugin", "add", "Kiln.Plugin.EmailProtect", "--version", "1.2.3", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(console.Output).Contains("trustworthy source");
            var lockFile = new PluginLockFile();
            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries["email-protect"].PackageId).IsEqualTo("Kiln.Plugin.EmailProtect");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginUpdateCommand_WhenLockEntryMissing_ExitsNonZero()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var client = new FakeNuGetPluginClient();
            var (app, console) = CreateApp(client, new PluginLockFile());
            var result = await app.RunAsync(["plugin", "update", "email-protect", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("kein Lock-Eintrag");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginUpdateCommand_AlreadyCurrent_SkipsDownload()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-update-current-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "email-protect", new PluginLockEntry("Kiln.Plugin.EmailProtect", "1.2.3", "nuget"));

            var client = new FakeNuGetPluginClient
            {
                LatestVersion = "1.2.3"
            };

            var (app, console) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "update", "email-protect", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(console.Output).Contains("bereits aktuell");
            await Assert.That(client.AddCallCount).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginRemoveCommand_RemovesFolderAndLockEntry()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-remove-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(projectDir, "plugins", "email-protect"));
        var lockFile = new PluginLockFile();
        await lockFile.SetAsync(projectDir, "email-protect", new PluginLockEntry("Kiln.Plugin.EmailProtect", "1.2.3", "nuget"));

        var client = new FakeNuGetPluginClient();
        var (app, console) = CreateApp(client, lockFile);
        var result = await app.RunAsync(["plugin", "remove", "email-protect", projectDir, "--yes"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(Directory.Exists(Path.Combine(projectDir, "plugins", "email-protect"))).IsFalse();
        var entries = await lockFile.ReadAsync(projectDir);
        await Assert.That(entries).DoesNotContainKey("email-protect");
        await Assert.That(console.Output).Contains("site.yaml");
    }

    [Test]
    public async Task PluginListCommand_ShowsSourceColumn()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-list-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectDir, "plugins", "email-protect"));
        await File.WriteAllTextAsync(Path.Combine(projectDir, "plugins", "email-protect", "plugin.yaml"), "name: Email Protect\nversion: 1.2.3\ndescription: Protect emails\n");

        var lockFile = new PluginLockFile();
        await lockFile.SetAsync(projectDir, "email-protect", new PluginLockEntry("Kiln.Plugin.EmailProtect", "1.2.3", "nuget"));

        var (app, console) = CreateApp(new FakeNuGetPluginClient(), lockFile);
        var result = await app.RunAsync(["plugin", "list", projectDir]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Kiln.Plugin.EmailProtect");
        await Assert.That(console.Output).Contains("nuget");

        Directory.Delete(projectDir, recursive: true);
    }

    private static (CommandAppTester App, TestConsole Console) CreateApp(
        INuGetPluginClient client,
        IPluginLockFile lockFile)
    {
        var (app, console) = CommandAppTesterFactory.Create(services =>
        {
            services.AddSingleton(client);
            services.AddSingleton(lockFile);
            services.AddSingleton<IPluginLoader, PluginLoader>();
        });

        app.Configure(config =>
        {
            config.AddBranch("plugin", p =>
            {
                p.AddCommand<PluginSearchCommand>("search");
                p.AddCommand<PluginAddCommand>("add");
                p.AddCommand<PluginUpdateCommand>("update");
                p.AddCommand<PluginRemoveCommand>("remove");
                p.AddCommand<PluginListCommand>("list");
            });
        });

        return (app, console);
    }

    private sealed class FakeNuGetPluginClient : INuGetPluginClient
    {
        public IReadOnlyList<PluginSearchResult> SearchResults { get; set; } = [];

        public string? LatestVersion { get; set; }

        public PluginPackageInstallResult? InstallResult { get; set; }

        public int AddCallCount { get; private set; }

        public Task<IReadOnlyList<PluginSearchResult>> SearchAsync(string query, CancellationToken ct = default)
            => Task.FromResult(SearchResults);

        public Task<string?> GetLatestVersionAsync(string packageId, CancellationToken ct = default)
            => Task.FromResult(LatestVersion);

        public Task<bool> IsUpdateAvailableAsync(string packageId, string currentVersion, CancellationToken ct = default)
            => Task.FromResult(!string.Equals(LatestVersion, currentVersion, StringComparison.OrdinalIgnoreCase));

        public Task<PluginPackageInstallResult> AddAsync(string packageId, string? version, string projectPath, CancellationToken ct = default)
        {
            AddCallCount++;

            var pluginName = packageId.Split('.').Last();
            var installPath = Path.Combine(projectPath, "plugins", pluginName);

            return Task.FromResult(
                InstallResult ?? new PluginPackageInstallResult(packageId, version ?? "1.0.0", pluginName, installPath));
        }
    }
}
