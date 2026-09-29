namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class PluginContentHasherTests
{
    [Test]
    public async Task ComputeDirectoryHash_IsIndependentOfCreationOrder()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        first.Write("b.txt", "B");
        first.Write("a/c.txt", "C");
        second.Write("a/c.txt", "C");
        second.Write("b.txt", "B");

        await Assert.That(PluginContentHasher.ComputeDirectoryHash(first.Path)).IsEqualTo(PluginContentHasher.ComputeDirectoryHash(second.Path));
    }

    [Test]
    public async Task ComputeDirectoryHash_ChangesWhenContentChanges()
    {
        using var dir = new TempDirectory();
        dir.Write("a.txt", "one");
        var before = PluginContentHasher.ComputeDirectoryHash(dir.Path);

        dir.Write("a.txt", "two");

        await Assert.That(PluginContentHasher.ComputeDirectoryHash(dir.Path)).IsNotEqualTo(before);
    }

    [Test]
    public async Task ComputeDirectoryHash_ChangesWhenFileIsRenamedOrAdded()
    {
        using var dir = new TempDirectory();
        dir.Write("a.txt", "same");
        var original = PluginContentHasher.ComputeDirectoryHash(dir.Path);

        File.Move(Path.Combine(dir.Path, "a.txt"), Path.Combine(dir.Path, "b.txt"));
        var renamed = PluginContentHasher.ComputeDirectoryHash(dir.Path);
        dir.Write("extra.txt", "x");
        var added = PluginContentHasher.ComputeDirectoryHash(dir.Path);

        await Assert.That(renamed).IsNotEqualTo(original);
        await Assert.That(added).IsNotEqualTo(renamed);
    }

    [Test]
    public async Task ComputeDirectoryHash_ReturnsLowercaseHexSha256()
    {
        using var dir = new TempDirectory();
        dir.Write("a.txt", "x");

        var hash = PluginContentHasher.ComputeDirectoryHash(dir.Path);

        await Assert.That(hash.Length).IsEqualTo(64);
        await Assert.That(hash).IsEqualTo(hash.ToLowerInvariant());
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kiln-hash-{Guid.NewGuid():N}");
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
