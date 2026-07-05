namespace Kiln.Cli.Tests;

using Kiln.Cli.Commands;
using Kiln.Cli.Tests.Fakes;
using Kiln.Models;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;

public class GenDocsCommandAppTests
{
    private static (CommandAppTester App, TestConsole Console, FakeOpenApiDocGenerator Generator) CreateApp()
    {
        var generator = new FakeOpenApiDocGenerator();

        var (app, console) = CommandAppTesterFactory.Create(services =>
        {
            services.AddSingleton<IOpenApiDocGenerator>(generator);
        });

        app.Configure(config => config.AddCommand<GenDocsCommand>("docs"));

        return (app, console, generator);
    }

    [Test]
    public async Task GenDocsCommand_MissingOpenApiOption_ExitsOneWithRequiredMessage()
    {
        var (app, console, _) = CreateApp();

        var result = await app.RunAsync(["docs"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("required");
    }

    [Test]
    public async Task GenDocsCommand_SpecFileNotFound_ExitsOneWithNotFoundMessage()
    {
        var (app, console, _) = CreateApp();
        var missingSpecPath = Path.Combine(Path.GetTempPath(), $"kiln-missing-{Guid.NewGuid():N}.yaml");

        var result = await app.RunAsync(["docs", "--openapi", missingSpecPath]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("not found");
    }

    [Test]
    public async Task GenDocsCommand_Success_ShowsAllReportCategoriesAndSummary()
    {
        var (app, console, generator) = CreateApp();
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-gendocs-apptest-{Guid.NewGuid():N}");
        var specPath = Path.Combine(tempDir, "spec.yaml");

        try
        {
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(specPath, "openapi: 3.0.0");

            generator.ResultFactory = () => new DocGenReport(
                Written: ["content/api/items/list.md"],
                Skipped: ["content/api/items/adopted.md"],
                Conflicts: ["content/api/items/conflict.md"],
                Warnings: ["operation missing summary"]);

            var result = await app.RunAsync(["docs", "--openapi", specPath, "--project", tempDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(console.Output).Contains("operation missing summary");
            await Assert.That(console.Output).Contains("written");
            await Assert.That(console.Output).Contains("skipped (adopted)");
            await Assert.That(console.Output).Contains("conflict");
            await Assert.That(console.Output).Contains("1 written, 1 skipped, 1 conflicts");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task GenDocsCommand_SiteYamlWithoutMatchingCollection_ShowsAdditionalWarning()
    {
        var (app, console, generator) = CreateApp();
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-gendocs-apptest-{Guid.NewGuid():N}");
        var specPath = Path.Combine(tempDir, "spec.yaml");
        var siteYamlPath = Path.Combine(tempDir, "site.yaml");

        try
        {
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(specPath, "openapi: 3.0.0");
            await File.WriteAllTextAsync(siteYamlPath, "title: Test\nbaseUrl: http://localhost\n");

            generator.ResultFactory = () => new DocGenReport([], [], [], []);

            var result = await app.RunAsync(["docs", "--openapi", specPath, "--project", tempDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(console.Output).Contains("Add a collection for");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
