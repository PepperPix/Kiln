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
    [Arguments("..")]
    [Arguments(".")]
    [Arguments("../x")]
    [Arguments("a/b")]
    [Arguments("a\\b")]
    [Arguments("{victim}")]
    [Arguments("Upper")]
    [Arguments("with space")]
    public async Task PluginRemoveCommand_WithInvalidName_FailsAndLeavesFileSystemUntouched(string nameArgument)
    {
        var container = Path.Combine(Path.GetTempPath(), $"kiln-plugin-remove-unsafe-{Guid.NewGuid():N}");
        var projectDir = Path.Combine(container, "project");
        var victim = Path.Combine(container, "victim");

        try
        {
            Directory.CreateDirectory(Path.Combine(projectDir, "plugins", "email-protect"));
            Directory.CreateDirectory(Path.Combine(projectDir, "plugins", "a", "b"));
            Directory.CreateDirectory(Path.Combine(projectDir, "x"));
            Directory.CreateDirectory(victim);
            await File.WriteAllTextAsync(Path.Combine(projectDir, "site.yaml"), "title: precious");
            await File.WriteAllTextAsync(Path.Combine(projectDir, "plugins", "email-protect", "plugin.yaml"), "name: email-protect");
            await File.WriteAllTextAsync(Path.Combine(projectDir, "plugins", "a", "b", "file.txt"), "keep");
            await File.WriteAllTextAsync(Path.Combine(projectDir, "x", "file.txt"), "keep");
            await File.WriteAllTextAsync(Path.Combine(victim, "file.txt"), "keep");
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "email-protect", new PluginLockEntry("Kiln.Plugin.EmailProtect", "1.2.3", "nuget"));
            var before = SnapshotPaths(container);

            var (app, _) = CreateApp(new FakeNuGetPluginClient(), lockFile);
            var name = nameArgument.Replace("{victim}", victim, StringComparison.Ordinal);
            var result = await app.RunAsync(["plugin", "remove", name, projectDir, "--yes"]);

            await Assert.That(result.ExitCode).IsNotEqualTo(0);
            await Assert.That(SnapshotPaths(container)).IsEquivalentTo(before);
            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries).ContainsKey("email-protect");
        }
        finally
        {
            Directory.Delete(container, recursive: true);
        }
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

    [Test]
    [Arguments("Legacy_Name")]
    [Arguments("My Plugin")]
    [Arguments("Upper[red]")]
    public async Task PluginRemoveCommand_WithLegacyDirectoryName_RemovesFolderAndLockEntry(string name)
    {
        var container = Path.Combine(Path.GetTempPath(), $"kiln-plugin-remove-legacy-{Guid.NewGuid():N}");
        var projectDir = Path.Combine(container, "project");

        try
        {
            Directory.CreateDirectory(Path.Combine(projectDir, "plugins", name));
            Directory.CreateDirectory(Path.Combine(projectDir, "plugins", "keep"));
            await File.WriteAllTextAsync(Path.Combine(projectDir, "plugins", name, "file.txt"), "x");
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, name, new PluginLockEntry("Kiln.Plugin.Legacy", "1.0.0", "nuget"));

            var (app, console) = CreateApp(new FakeNuGetPluginClient(), lockFile);
            var result = await app.RunAsync(["plugin", "remove", name, projectDir, "--yes"]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(console.Output).Contains(name);
            await Assert.That(Directory.Exists(Path.Combine(projectDir, "plugins", name))).IsFalse();
            await Assert.That(Directory.Exists(Path.Combine(projectDir, "plugins", "keep"))).IsTrue();
            await Assert.That(await lockFile.ReadAsync(projectDir)).DoesNotContainKey(name);
        }
        finally
        {
            Directory.Delete(container, recursive: true);
        }
    }

    [Test]
    public async Task PluginRemoveCommand_WhenPluginDoesNotExist_ExitsNonZero()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-remove-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectDir, "plugins"));

        try
        {
            var (app, console) = CreateApp(new FakeNuGetPluginClient(), new PluginLockFile());
            var result = await app.RunAsync(["plugin", "remove", "ghost", projectDir, "--yes"]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("was not found");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginAddCommand_PassesForceAndLockEntries_AndStoresHash()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-add-opts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "other", new PluginLockEntry("Kiln.Plugin.Other", "1.0.0", "nuget"));
            var client = new FakeNuGetPluginClient
            {
                InstallResult = new PluginPackageInstallResult("Contoso.Widget", "2.0.0", "widget", Path.Combine(projectDir, "plugins", "widget"))
                {
                    ContentHash = "hash-1",
                },
            };

            var (app, _) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "add", "Contoso.Widget", "--force", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(client.LastOptions!.Force).IsTrue();
            await Assert.That(client.LastOptions.ExistingLockEntries!).ContainsKey("other");
            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries["widget"].ContentHash).IsEqualTo("hash-1");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginAddCommand_WithoutFlags_DoesNotForce()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-add-default-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var client = new FakeNuGetPluginClient();
            var (app, _) = CreateApp(client, new PluginLockFile());
            var result = await app.RunAsync(["plugin", "add", "Kiln.Plugin.Good", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(client.LastOptions!.Force).IsFalse();
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginAddCommand_ForUntaggedPackage_ExitsNonZeroAndDoesNotWriteLock()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-add-untagged-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var client = new FakeNuGetPluginClient
            {
                AddException = new InvalidOperationException("Package 'Contoso.Widget' does not carry the 'kiln-plugin' tag and cannot be installed as a Kiln plugin."),
            };
            var lockFile = new PluginLockFile();
            var (app, console) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "add", "Contoso.Widget", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("kiln-plugin");
            await Assert.That(console.Output).DoesNotContain("Use --");
            await Assert.That(await lockFile.ReadAsync(projectDir)).IsEmpty();
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginAddCommand_WhenInstallIsRejected_PrintsEscapedMessageAndDoesNotWriteLock()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-add-reject-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var client = new FakeNuGetPluginClient { AddException = new InvalidOperationException("Plugin 'x[red]y' has local changes. Use --force.") };
            var lockFile = new PluginLockFile();
            var (app, console) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "add", "Kiln.Plugin.Good", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("x[red]y");
            await Assert.That(console.Output).Contains("--force");
            await Assert.That(await lockFile.ReadAsync(projectDir)).IsEmpty();
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginUpdateCommand_PassesForce_AndStoresNewHash()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-update-opts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "widget", new PluginLockEntry("Contoso.Widget", "1.0.0", "nuget") { ContentHash = "old" });
            var client = new FakeNuGetPluginClient
            {
                LatestVersion = "2.0.0",
                InstallResult = new PluginPackageInstallResult("Contoso.Widget", "2.0.0", "widget", Path.Combine(projectDir, "plugins", "widget"))
                {
                    ContentHash = "new",
                },
            };

            var (app, _) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "update", "widget", "--force", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(client.LastOptions!.Force).IsTrue();
            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries["widget"].ContentHash).IsEqualTo("new");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginUpdateCommand_WithMarkupCharactersInLockEntry_PrintsThemLiterally()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-update-markup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "x[red]y", new PluginLockEntry("Kiln.Plugin.A[b]", "1.0.0", "nuget"));
            var client = new FakeNuGetPluginClient { LatestVersion = "2.0.0" };

            var (app, console) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "update", "x[red]y", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(console.Output).Contains("Checking x[red]y");
            await Assert.That(console.Output).Contains("A[b]");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginUpdateCommand_WhenInstallIsRejected_ExitsNonZeroAndKeepsLockEntry()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-update-reject-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "good", new PluginLockEntry("Kiln.Plugin.Good", "1.0.0", "nuget"));
            var client = new FakeNuGetPluginClient
            {
                LatestVersion = "2.0.0",
                AddException = new InvalidOperationException("Plugin 'good' has local changes. Use --force to overwrite them."),
            };

            var (app, console) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "update", "good", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("--force");
            await Assert.That((await lockFile.ReadAsync(projectDir))["good"].Version).IsEqualTo("1.0.0");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginListCommand_MarksLocallyModifiedPlugin()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-list-modified-{Guid.NewGuid():N}");
        var pluginDir = Path.Combine(projectDir, "plugins", "email-protect");
        Directory.CreateDirectory(pluginDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(pluginDir, "plugin.yaml"), "name: email-protect\nversion: 1.2.3\n");
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "email-protect", new PluginLockEntry("Kiln.Plugin.EmailProtect", "1.2.3", "nuget")
            {
                ContentHash = PluginContentHasher.ComputeDirectoryHash(pluginDir),
            });

            var (firstApp, firstConsole) = CreateApp(new FakeNuGetPluginClient(), lockFile);
            var unmodified = await firstApp.RunAsync(["plugin", "list", projectDir]);
            await File.AppendAllTextAsync(Path.Combine(pluginDir, "plugin.yaml"), "description: edited\n");
            var (secondApp, secondConsole) = CreateApp(new FakeNuGetPluginClient(), lockFile);
            var modified = await secondApp.RunAsync(["plugin", "list", projectDir]);

            await Assert.That(unmodified.ExitCode).IsEqualTo(0);
            await Assert.That(firstConsole.Output).DoesNotContain("modified");
            await Assert.That(modified.ExitCode).IsEqualTo(0);
            await Assert.That(secondConsole.Output).Contains("modified");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginListCommand_WithMarkupCharactersInManifest_PrintsThemLiterally()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-list-markup-{Guid.NewGuid():N}");
        var pluginDir = Path.Combine(projectDir, "plugins", "odd");
        Directory.CreateDirectory(pluginDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(pluginDir, "plugin.yaml"), "name: odd\nversion: 1.0.0\ndescription: 'has [red]markup[/]'\n");

            var (app, console) = CreateApp(new FakeNuGetPluginClient(), new PluginLockFile());
            var result = await app.RunAsync(["plugin", "list", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(console.Output).Contains("[red]markup[/]");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    private static List<string> SnapshotPaths(string root)
    {
        var paths = Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();
        paths.Sort(StringComparer.Ordinal);
        return paths;
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

        public PluginInstallOptions? LastOptions { get; private set; }

        public Exception? AddException { get; set; }

        public Task<IReadOnlyList<PluginSearchResult>> SearchAsync(string query, CancellationToken ct = default)
            => Task.FromResult(SearchResults);

        public Task<string?> GetLatestVersionAsync(string packageId, CancellationToken ct = default)
            => Task.FromResult(LatestVersion);

        public Task<bool> IsUpdateAvailableAsync(string packageId, string currentVersion, CancellationToken ct = default)
            => Task.FromResult(!string.Equals(LatestVersion, currentVersion, StringComparison.OrdinalIgnoreCase));

        public Task<PluginPackageInstallResult> AddAsync(string packageId, string? version, string projectPath, PluginInstallOptions options, CancellationToken ct = default)
        {
            LastOptions = options;
            return AddAsync(packageId, version, projectPath, ct);
        }

        public Task<PluginPackageInstallResult> AddAsync(string packageId, string? version, string projectPath, CancellationToken ct = default)
        {
            AddCallCount++;
            if (AddException is not null)
                throw AddException;

            var pluginName = packageId.Split('.').Last();
            var installPath = Path.Combine(projectPath, "plugins", pluginName);

            return Task.FromResult(
                InstallResult ?? new PluginPackageInstallResult(packageId, version ?? "1.0.0", pluginName, installPath));
        }
    }
}
