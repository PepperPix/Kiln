namespace Kiln.Cli.Tests;

using Kiln.Cli.Commands;
using Kiln.Cli.Tests.Fakes;
using Kiln.Models;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;

public class GenDotNetXmlCommandAppTests
{
    private static (CommandAppTester App, TestConsole Console, FakeXmlDocGenerator Generator) CreateApp()
    {
        var generator = new FakeXmlDocGenerator();

        var (app, console) = CommandAppTesterFactory.Create(services =>
        {
            services.AddSingleton<IXmlDocGenerator>(generator);
        });

        app.Configure(config => config.AddCommand<GenDotNetXmlCommand>("dotnet-xml"));

        return (app, console, generator);
    }

    [Test]
    public async Task GenDotNetXmlCommand_MissingXmlOption_ExitsOneWithRequiredMessage()
    {
        var (app, console, _) = CreateApp();

        var result = await app.RunAsync(["dotnet-xml"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("required");
    }

    [Test]
    public async Task GenDotNetXmlCommand_XmlFileNotFound_ExitsOneWithNotFoundMessage()
    {
        var (app, console, _) = CreateApp();
        var missingXmlPath = Path.Combine(Path.GetTempPath(), $"kiln-missing-{Guid.NewGuid():N}.xml");

        var result = await app.RunAsync(["dotnet-xml", "--xml", missingXmlPath]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("not found");
    }

    [Test]
    public async Task GenDotNetXmlCommand_Success_ShowsAllReportCategoriesAndSummary()
    {
        var (app, console, generator) = CreateApp();
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-genxml-apptest-{Guid.NewGuid():N}");
        var xmlPath = Path.Combine(tempDir, "doc.xml");

        try
        {
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(xmlPath, "<doc></doc>");

            generator.ResultFactory = () => new DocGenReport(
                Written: ["content/api-dotnet/Widget.md"],
                Skipped: ["content/api-dotnet/Adopted.md"],
                Conflicts: ["content/api-dotnet/Conflict.md"],
                Warnings: ["member missing summary"]);

            var result = await app.RunAsync(["dotnet-xml", "--xml", xmlPath, "--project", tempDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(console.Output).Contains("member missing summary");
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
    public async Task GenDotNetXmlCommand_SiteYamlWithoutMatchingCollection_ShowsAdditionalWarning()
    {
        var (app, console, generator) = CreateApp();
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-genxml-apptest-{Guid.NewGuid():N}");
        var xmlPath = Path.Combine(tempDir, "doc.xml");
        var siteYamlPath = Path.Combine(tempDir, "site.yaml");

        try
        {
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(xmlPath, "<doc></doc>");
            await File.WriteAllTextAsync(siteYamlPath, "title: Test\nbaseUrl: http://localhost\n");

            generator.ResultFactory = () => new DocGenReport([], [], [], []);

            var result = await app.RunAsync(["dotnet-xml", "--xml", xmlPath, "--project", tempDir]);

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
