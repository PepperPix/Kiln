namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class PluginContentInspectorTests
{
    [Test]
    public async Task Scan_FindsExplicitAndProtocolRelativeHosts_NormalizedWithoutPortOrPath()
    {
        using var dir = new TempDirectory();
        dir.Write("static/a.js", "load('https://CDN.Example.com/x.js'); fetch('http://api.example.org:8080/v1?x=1');");
        dir.Write("static/b.css", "@import url(//fonts.example.net/css?family=Foo);");
        dir.Write("slots/body_end.html", "<script src=\"//cdn.example.com/lib.js\"></script>");

        var scan = PluginContentInspector.Scan(dir.Path);

        await Assert.That(scan.ExternalHosts.SequenceEqual(["api.example.org", "cdn.example.com", "fonts.example.net"])).IsTrue();
    }

    [Test]
    public async Task Scan_IgnoresNamespaceHostsCommentsAndRelativePaths()
    {
        using var dir = new TempDirectory();
        dir.Write("icon.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        dir.Write("a.js", "// note without host\n//nodot\nvar p = 'assets/img//logo.png'; var q = a + '//' + b;");

        var scan = PluginContentInspector.Scan(dir.Path);

        await Assert.That(scan.ExternalHosts).IsEmpty();
    }

    [Test]
    public async Task Scan_OnlyReadsTextualExtensions()
    {
        using var dir = new TempDirectory();
        dir.Write("notes.txt", "https://ignored.example.com/");
        dir.Write("data.json", "{\"u\":\"https://kept.example.com/\"}");

        var scan = PluginContentInspector.Scan(dir.Path);

        await Assert.That(scan.ExternalHosts).IsEquivalentTo(new[] { "kept.example.com" });
    }

    [Test]
    public async Task Scan_ReportsInventoryFilesAndSizes()
    {
        using var dir = new TempDirectory();
        dir.Write("plugin.yaml", "name: x\n");
        dir.Write("static/a.js", "aaaa");
        dir.Write("static/b.mjs", "bb");
        dir.Write("static/c.css", "c");
        dir.Write("slots/s.html", "hh");
        dir.Write("slots/t.HTM", "h");

        var scan = PluginContentInspector.Scan(dir.Path);

        await Assert.That(scan.Inventory).IsEqualTo(new PluginInventory(Scripts: 2, Styles: 1, Html: 2, Other: 1, TotalBytes: 8 + 4 + 2 + 1 + 2 + 1));
        await Assert.That(scan.Files.Select(f => f.Path).ToList()).IsEquivalentTo(new[] { "plugin.yaml", "slots/s.html", "slots/t.HTM", "static/a.js", "static/b.mjs", "static/c.css" });
        await Assert.That(scan.Files.First(f => f.Path == "static/a.js").Size).IsEqualTo(4);
    }

    [Test]
    public async Task Scan_OfEmptyDirectory_ReturnsEmptyResult()
    {
        using var dir = new TempDirectory();

        var scan = PluginContentInspector.Scan(dir.Path);

        await Assert.That(scan.Files).IsEmpty();
        await Assert.That(scan.ExternalHosts).IsEmpty();
        await Assert.That(scan.Inventory.TotalBytes).IsEqualTo(0);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kiln-inspector-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Write(string relativePath, string content)
        {
            var full = System.IO.Path.Combine(Path, relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
