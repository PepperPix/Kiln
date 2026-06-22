namespace Kiln.Core.Tests.Services;

using Kiln.Abstractions;
using Kiln.Services;

public class NuglifyAssetMinifierTests
{
    private static readonly NuglifyAssetMinifier Minifier = new();

    [Test]
    public async Task Id_IsNuglify()
    {
        await Assert.That(Minifier.Id).IsEqualTo("nuglify");
    }

    [Test]
    public async Task CanMinify_SupportsCssJsHtmlSvg_NotOther()
    {
        await Assert.That(Minifier.CanMinify(AssetType.Css)).IsTrue();
        await Assert.That(Minifier.CanMinify(AssetType.Js)).IsTrue();
        await Assert.That(Minifier.CanMinify(AssetType.Html)).IsTrue();
        await Assert.That(Minifier.CanMinify(AssetType.Svg)).IsTrue();
        await Assert.That(Minifier.CanMinify(AssetType.Other)).IsFalse();
    }

    [Test]
    public async Task MinifyCss_RemovesWhitespace()
    {
        const string input = "body {\n    color:  #ffffff;\n}\n";
        var result = Minifier.Minify(input, AssetType.Css);
        await Assert.That(result.Length).IsLessThan(input.Length);
        await Assert.That(result).DoesNotContain("\n");
    }

    [Test]
    public async Task MinifyJs_RemovesWhitespace()
    {
        const string input = "var x = 5;\nvar y = 6;\n";
        var result = Minifier.Minify(input, AssetType.Js);
        await Assert.That(result.Length).IsLessThan(input.Length);
    }

    [Test]
    public async Task MinifyHtml_CollapsesWhitespaceOutsidePre_PreservesPre()
    {
        const string input = "<html><body><p>Hello   world</p><pre>a   b</pre></body></html>";
        var result = Minifier.Minify(input, AssetType.Html);
        await Assert.That(result).Contains("Hello world");
        await Assert.That(result).DoesNotContain("Hello   world");
        await Assert.That(result).Contains("a   b");
    }

    [Test]
    public async Task MinifySvg_StripsComments()
    {
        const string input = "<svg><!-- a comment --><rect/></svg>";
        var result = Minifier.Minify(input, AssetType.Svg);
        await Assert.That(result).DoesNotContain("a comment");
    }

    [Test]
    public async Task NoOpMinifier_ReturnsInputUnchanged()
    {
        var noop = new NoOpAssetMinifier();
        const string input = "body {  color: red;  }";
        await Assert.That(noop.Id).IsEqualTo("noop");
        await Assert.That(noop.Minify(input, AssetType.Css)).IsEqualTo(input);
    }
}
