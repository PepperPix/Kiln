namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class ScaffolderTests
{
    private const string SiteName = "testsite";
    private const string NonEmptyErrorFragment = "already exists and is not empty";

    [Test]
    public async Task CreateSite_CreatesExpectedFilesAndReplacesPlaceholders()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), $"kiln-scaffold-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDir);

        try
        {
            var scaffolder = new Scaffolder();
            var result = scaffolder.CreateSite(SiteName, rootDir);

            await Assert.That(Directory.Exists(result.ProjectPath)).IsTrue();
            await Assert.That(result.CreatedFiles).Contains("site.yaml");
            await Assert.That(result.CreatedFiles).Contains("content/index.md");
            await Assert.That(result.CreatedFiles).Contains("themes/default/layouts/home.html");
            await Assert.That(result.CreatedFiles).Contains("themes/default/layouts/404.html");
            await Assert.That(result.CreatedFiles).Contains("themes/default/partials/recent-posts.html");

            var siteYamlPath = Path.Combine(result.ProjectPath, "site.yaml");
            var contentIndexPath = Path.Combine(result.ProjectPath, "content", "index.md");

            await Assert.That(File.Exists(siteYamlPath)).IsTrue();
            await Assert.That(File.Exists(contentIndexPath)).IsTrue();

            var siteYaml = await File.ReadAllTextAsync(siteYamlPath);
            var contentIndex = await File.ReadAllTextAsync(contentIndexPath);

            await Assert.That(siteYaml).Contains("title: testsite");
            await Assert.That(siteYaml).DoesNotContain("{{NAME}}");
            await Assert.That(contentIndex).Contains("layout: home");
            await Assert.That(contentIndex).DoesNotContain("{{DATE");
        }
        finally
        {
            Directory.Delete(rootDir, true);
        }
    }

    [Test]
    public async Task CreateSite_WhenTargetDirectoryIsNotEmpty_ThrowsInvalidOperationException()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), $"kiln-scaffold-nonempty-{Guid.NewGuid():N}");
        var targetDir = Path.Combine(rootDir, SiteName);
        Directory.CreateDirectory(targetDir);
        await File.WriteAllTextAsync(Path.Combine(targetDir, "existing.txt"), "content");

        try
        {
            var scaffolder = new Scaffolder();

            await Assert.That(() => scaffolder.CreateSite(SiteName, rootDir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining(NonEmptyErrorFragment);
        }
        finally
        {
            Directory.Delete(rootDir, true);
        }
    }
}
