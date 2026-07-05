namespace Kiln.Cli.Tests.Fakes;

using Kiln.Models;
using Kiln.Services;

public sealed class FakeSiteConfigLoader : ISiteConfigLoader
{
    public SiteConfiguration Config { get; set; } = new SiteConfiguration
    {
        Title = "Test Site",
        BaseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", 5555).Uri,
    };

    public SiteConfiguration Load(string projectPath) => Config;
}
