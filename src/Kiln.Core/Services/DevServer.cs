namespace Kiln.Services;

using System.Net;
using Kiln.Abstractions;

public sealed class DevServer(ISiteBuilder siteBuilder) : IDevServer
{
    public async Task RunAsync(string projectPath, int port = 5555, bool includeDrafts = false, CancellationToken ct = default)
    {
        // Initial build
        await siteBuilder.BuildAsync(projectPath, includeDrafts, BuildEnvironment.Development, ct).ConfigureAwait(false);

        var config = new SiteConfigLoader().Load(projectPath);
        var outputDir = Path.Combine(projectPath, config.OutputDir);

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        // File watcher for auto-rebuild
        var watcher = new FileSystemWatcher(projectPath);
        watcher.IncludeSubdirectories = true;
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        using var watcherScope = watcher;

        watcher.Changed += OnChange;
        watcher.Created += OnChange;
        watcher.Deleted += OnChange;
        watcher.Renamed += (_, _) => TriggerRebuild();
        watcher.EnableRaisingEvents = true;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync().WaitAsync(ct).ConfigureAwait(false);
                ServeRequest(context, outputDir);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        listener.Stop();
        return;

        void OnChange(object sender, FileSystemEventArgs e)
        {
            // Ignore changes in output directory
            if (e.FullPath.Contains(config.OutputDir, StringComparison.Ordinal))
                return;
            TriggerRebuild();
        }

        void TriggerRebuild()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await siteBuilder.BuildAsync(projectPath, includeDrafts, BuildEnvironment.Development, ct).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // Intentional: background rebuild errors must not crash the server
                catch (Exception)
#pragma warning restore CA1031
                {
                    // Swallow rebuild errors during serve — will show on next request
                }
            }, ct);
        }
    }

    private static void ServeRequest(HttpListenerContext context, string outputDir)
    {
        const int httpNotFound = 404;
        var requestPath = context.Request.Url?.LocalPath ?? "/";
        if (requestPath == "/")
            requestPath = "/index.html";

        // Try exact path, then path/index.html
        var filePath = Path.Combine(outputDir, requestPath.TrimStart('/'));
        if (!File.Exists(filePath))
            filePath = Path.Combine(outputDir, requestPath.TrimStart('/'), "index.html");

        if (File.Exists(filePath))
        {
            var content = File.ReadAllBytes(filePath);
            context.Response.ContentType = GetMimeType(filePath);
            context.Response.ContentLength64 = content.Length;
            context.Response.OutputStream.Write(content);
        }
        else
        {
            context.Response.StatusCode = httpNotFound;
            var notFound = System.Text.Encoding.UTF8.GetBytes("404 - Not Found");
            context.Response.OutputStream.Write(notFound);
        }

        context.Response.Close();
    }

    private static string GetMimeType(string path) => Path.GetExtension(path).ToUpperInvariant() switch
    {
        ".HTML" => "text/html; charset=utf-8",
        ".CSS" => "text/css",
        ".JS" => "application/javascript",
        ".JSON" => "application/json",
        ".PNG" => "image/png",
        ".JPG" or ".JPEG" => "image/jpeg",
        ".GIF" => "image/gif",
        ".SVG" => "image/svg+xml",
        ".WEBP" => "image/webp",
        ".ICO" => "image/x-icon",
        ".XML" => "application/xml",
        _ => "application/octet-stream"
    };
}
