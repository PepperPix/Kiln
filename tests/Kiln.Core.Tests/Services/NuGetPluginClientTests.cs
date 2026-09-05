namespace Kiln.Core.Tests.Services;

using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kiln.Services;

public class NuGetPluginClientTests
{
    [Test]
    public async Task SearchAsync_ParsesKilnPluginResults()
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "data": [
                    {
                      "id": "Kiln.Plugin.EmailProtect",
                      "version": "1.2.3",
                      "description": "Protect emails from scrapers",
                      "tags": ["kiln-plugin", "email", "privacy"]
                    }
                  ]
                }
                """, Encoding.UTF8, "application/json")
        });

        var client = new NuGetPluginClient("https://api.nuget.org/v3", handler);
        var results = await client.SearchAsync("email-protect");

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Id).IsEqualTo("Kiln.Plugin.EmailProtect");
        await Assert.That(results[0].Version).IsEqualTo("1.2.3");
        await Assert.That(results[0].Description).IsEqualTo("Protect emails from scrapers");
    }

    [Test]
    public async Task GetLatestVersionAsync_ResolvesLatestSemVer()
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            if (req.RequestUri!.ToString().Contains("registration5-semver2/kiln.plugin.emailprotect/index.json", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {
                          "items": [
                            {
                              "items": [
                                {
                                  "catalogEntry": {
                                    "id": "Kiln.Plugin.EmailProtect",
                                    "version": "1.2.0",
                                    "packageHash": "Zg==",
                                    "packageHashAlgorithm": "SHA512",
                                    "packageContent": "https://api.nuget.org/v3-flatcontainer/kiln.plugin.emailprotect/1.2.0/kiln.plugin.emailprotect.1.2.0.nupkg"
                                  }
                                }
                              ]
                            }
                          ]
                        }
                        """, Encoding.UTF8, "application/json")
                };
            }

            throw new InvalidOperationException($"Unexpected request: {req.RequestUri}");
        });

        var client = new NuGetPluginClient("https://api.nuget.org/v3", handler);
        var version = await client.GetLatestVersionAsync("Kiln.Plugin.EmailProtect");

        await Assert.That(version).IsEqualTo("1.2.0");
    }

    [Test]
    public async Task AddAsync_RejectsHashMismatch_BeforeWritingFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kiln-nuget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var packageBytes = CreatePackageBytesWithPluginManifest("email-protect", "1.0.0");
            _ = Convert.ToHexString(SHA512.HashData(packageBytes));
            var wrongHash = Convert.ToHexString(SHA512.HashData("tampered"u8.ToArray()));

            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.ToString().Contains("registration5-semver2/kiln.plugin.emailprotect/index.json", StringComparison.OrdinalIgnoreCase))
                {
                    var responseBody = JsonSerializer.Serialize(new
                    {
                        items = new[]
                        {
                            new
                            {
                                items = new[]
                                {
                                    new
                                    {
                                        catalogEntry = new
                                        {
                                            id = "Kiln.Plugin.EmailProtect",
                                            version = "1.0.0",
                                            packageHash = wrongHash,
                                            packageHashAlgorithm = "SHA512",
                                            packageContent = "https://api.nuget.org/v3-flatcontainer/kiln.plugin.emailprotect/1.0.0/kiln.plugin.emailprotect.1.0.0.nupkg"
                                        }
                                    }
                                }
                            }
                        }
                    });

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
                    };
                }

                if (req.RequestUri!.ToString().Contains("/kiln.plugin.emailprotect.1.0.0.nupkg"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(packageBytes)
                    };
                }

                throw new InvalidOperationException($"Unexpected request: {req.RequestUri}");
            });

            var client = new NuGetPluginClient("https://api.nuget.org/v3", handler);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.AddAsync("Kiln.Plugin.EmailProtect", "1.0.0", tempRoot));
            await Assert.That(ex!.Message).Contains("SHA512 mismatch");
            await Assert.That(Directory.Exists(Path.Combine(tempRoot, "plugins", "email-protect"))).IsFalse();
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Test]
    public async Task AddAsync_ExtractsPluginContent_IntoPluginsDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kiln-nuget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var packageBytes = CreatePackageBytesWithPluginManifest("email-protect", "1.0.0");
            var hash = Convert.ToHexString(SHA512.HashData(packageBytes));

            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.ToString().Contains("registration5-semver2/kiln.plugin.emailprotect/index.json", StringComparison.OrdinalIgnoreCase))
                {
                    var responseBody = JsonSerializer.Serialize(new
                    {
                        items = new[]
                        {
                            new
                            {
                                items = new[]
                                {
                                    new
                                    {
                                        catalogEntry = new
                                        {
                                            id = "Kiln.Plugin.EmailProtect",
                                            version = "1.0.0",
                                            packageHash = hash,
                                            packageHashAlgorithm = "SHA512",
                                            packageContent = "https://api.nuget.org/v3-flatcontainer/kiln.plugin.emailprotect/1.0.0/kiln.plugin.emailprotect.1.0.0.nupkg"
                                        }
                                    }
                                }
                            }
                        }
                    });

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
                    };
                }

                if (req.RequestUri!.ToString().Contains("/kiln.plugin.emailprotect.1.0.0.nupkg"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(packageBytes)
                    };
                }

                throw new InvalidOperationException($"Unexpected request: {req.RequestUri}");
            });

            var client = new NuGetPluginClient("https://api.nuget.org/v3", handler);
            var result = await client.AddAsync("Kiln.Plugin.EmailProtect", "1.0.0", tempRoot);

            await Assert.That(result.PluginName).IsEqualTo("email-protect");
            await Assert.That(result.Version).IsEqualTo("1.0.0");
            await Assert.That(File.Exists(Path.Combine(tempRoot, "plugins", "email-protect", "plugin.yaml"))).IsTrue();
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static byte[] CreatePackageBytesWithPluginManifest(string pluginName, string version)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = archive.CreateEntry("content/plugin.yaml");
            using (var writer = new StreamWriter(manifest.Open()))
            {
                writer.Write($"name: {pluginName}\nversion: {version}\nslots:\n  - body_end\n");
            }

            var staticFile = archive.CreateEntry("content/static/plugin.js");
            using (var writer = new StreamWriter(staticFile.Open()))
            {
                writer.Write("console.log('hello');\n");
            }
        }

        return stream.ToArray();
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

        public StubHttpMessageHandler(HttpResponseMessage response)
            : this(_ => response)
        {
        }

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
        {
            _factory = factory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_factory(request));
    }
}
