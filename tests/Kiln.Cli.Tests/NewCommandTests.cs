namespace Kiln.Cli.Tests;

using System.Diagnostics;

public class NewCommandTests
{
    [Test]
    public async Task NewCommand_Creates_Home_And_404_And_RecentPosts()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-new-{Guid.NewGuid():N}");
        try
        {
            var cliDll = Path.Combine(
                Path.GetDirectoryName(typeof(NewCommandTests).Assembly.Location)!,
                "Kiln.Cli.dll");

            var psi = new ProcessStartInfo("dotnet", $"exec \"{cliDll}\" new \"{dir}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(psi)!;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            await Assert.That(Directory.Exists(dir)).IsTrue();

            var contentIndex = Path.Combine(dir, "content", "index.md");
            var siteYaml = Path.Combine(dir, "site.yaml");
            var homeLayout = Path.Combine(dir, "themes", "default", "layouts", "home.html");
            var notfound = Path.Combine(dir, "themes", "default", "layouts", "404.html");
            var recent = Path.Combine(dir, "themes", "default", "partials", "recent-posts.html");

            await Assert.That(File.Exists(contentIndex)).IsTrue();
            await Assert.That(File.Exists(siteYaml)).IsTrue();
            await Assert.That(File.Exists(homeLayout)).IsTrue();
            await Assert.That(File.Exists(notfound)).IsTrue();
            await Assert.That(File.Exists(recent)).IsTrue();

            var content = await File.ReadAllTextAsync(contentIndex);
            await Assert.That(content).Contains("layout: home");

            var yaml = await File.ReadAllTextAsync(siteYaml);
            await Assert.That(yaml).Contains("home:");
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}
