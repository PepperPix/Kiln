namespace Kiln.Services;

using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Kiln.Abstractions;
using Kiln.Models;

public sealed class DevServer(ISiteBuilder siteBuilder, ISiteConfigLoader siteConfigLoader) : IDevServer
{
    private const string LiveReloadEndpoint = "/__kiln/livereload";
    private const int DebounceMilliseconds = 200;
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private readonly ConcurrentDictionary<int, HttpListenerResponse> _sseClients = new();
    private int _nextClientId;

    public async Task RunAsync(string projectPath, int port = 5555, bool includeDrafts = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var initialBuild = await siteBuilder
            .BuildAsync(projectPath, includeDrafts, BuildEnvironment.Development, ct)
            .ConfigureAwait(false);
        if (!initialBuild.Success)
            throw new InvalidOperationException(BuildErrorMessage(initialBuild));

        var config = siteConfigLoader.Load(projectPath);
        var outputDir = Path.Combine(projectPath, config.OutputDir);
        var outputDirFullPath = Path.GetFullPath(outputDir);
        var outputRelativePath = config.OutputDir
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Trim(Path.DirectorySeparatorChar);

        var pendingChanges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var rebuildSync = new SemaphoreSlim(1, 1);
        var debounceLock = new object();
        var rebuildContext = new RebuildContext
        {
            Server = this,
            SiteBuilder = siteBuilder,
            PendingChanges = pendingChanges,
            DebounceLock = debounceLock,
            RebuildSync = rebuildSync,
            ProjectPath = projectPath,
            IncludeDrafts = includeDrafts,
            CancellationToken = ct
        };

        using var debounceTimer = new Timer(static state =>
        {
            _ = ExecuteRebuildAsync((RebuildContext)state!);
        }, rebuildContext, Timeout.Infinite, Timeout.Infinite);
        rebuildContext.DebounceTimer = debounceTimer;

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        using var watcher = new FileSystemWatcher(projectPath);
        watcher.IncludeSubdirectories = true;
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

        watcher.Changed += OnChange;
        watcher.Created += OnChange;
        watcher.Deleted += OnChange;
        watcher.Renamed += (_, e) =>
        {
            if (ShouldIgnorePath(e.FullPath))
                return;
            EnqueueChange(e.FullPath);
        };
        watcher.EnableRaisingEvents = true;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync().WaitAsync(ct).ConfigureAwait(false);
                _ = Task.Run(() => ServeRequestAsync(context, outputDir, ct), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
        }

        listener.Stop();
        debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
        await CloseSseClientsAsync().ConfigureAwait(false);
        return;

        void OnChange(object sender, FileSystemEventArgs e)
        {
            if (ShouldIgnorePath(e.FullPath))
                return;

            EnqueueChange(e.FullPath);
        }

        bool ShouldIgnorePath(string fullPath)
        {
            var normalizedFullPath = Path.GetFullPath(fullPath);
            if (normalizedFullPath.StartsWith(outputDirFullPath, StringComparison.OrdinalIgnoreCase))
                return true;

            var relativePath = Path.GetRelativePath(projectPath, normalizedFullPath);
            if (relativePath.StartsWith("..", StringComparison.Ordinal))
                return false;

            var normalizedRelativePath = relativePath
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Trim(Path.DirectorySeparatorChar);

            if (!string.IsNullOrEmpty(outputRelativePath)
                && (normalizedRelativePath.Equals(outputRelativePath, StringComparison.OrdinalIgnoreCase)
                    || normalizedRelativePath.StartsWith(outputRelativePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            foreach (var segment in normalizedRelativePath.Split(Path.DirectorySeparatorChar))
            {
                if (string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        void EnqueueChange(string changedPath)
        {
            lock (debounceLock)
            {
                pendingChanges.Add(changedPath);
                rebuildContext.DebounceTimer!.Change(DebounceMilliseconds, Timeout.Infinite);
            }
        }

        static async Task ExecuteRebuildAsync(
            RebuildContext state)
        {
            HashSet<string> changedPaths;
            lock (state.DebounceLock)
            {
                changedPaths = [.. state.PendingChanges];
                state.PendingChanges.Clear();
            }

            if (changedPaths.Count == 0)
                return;

            await state.RebuildSync.WaitAsync(state.CancellationToken).ConfigureAwait(false);
            try
            {
                var result = await state.SiteBuilder
                    .BuildAsync(state.ProjectPath, state.IncludeDrafts, BuildEnvironment.Development, state.CancellationToken)
                    .ConfigureAwait(false);

                if (!result.Success)
                {
                    await state.Server.BroadcastEventAsync("error", BuildErrorMessage(result), state.CancellationToken).ConfigureAwait(false);
                    return;
                }

                var eventName = ShouldSendCssEvent(changedPaths, state.ProjectPath) ? "css" : "reload";
                await state.Server.BroadcastEventAsync(eventName, string.Empty, state.CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Shutdown path.
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                await state.Server.BroadcastEventAsync("error", ex.Message, state.CancellationToken).ConfigureAwait(false);
            }
            finally
            {
                state.RebuildSync.Release();
                lock (state.DebounceLock)
                {
                    if (state.PendingChanges.Count > 0)
                        state.DebounceTimer!.Change(DebounceMilliseconds, Timeout.Infinite);
                }
            }
        }
    }

    private async Task ServeRequestAsync(HttpListenerContext context, string outputDir, CancellationToken ct)
    {
        const int httpNotFound = 404;
        var requestPath = context.Request.Url?.LocalPath ?? "/";

        if (string.Equals(requestPath, LiveReloadEndpoint, StringComparison.Ordinal))
        {
            await RegisterSseClientAsync(context, ct).ConfigureAwait(false);
            return;
        }

        if (requestPath == "/")
            requestPath = "/index.html";

        var filePath = Path.Combine(outputDir, requestPath.TrimStart('/'));
        if (!File.Exists(filePath))
            filePath = Path.Combine(outputDir, requestPath.TrimStart('/'), "index.html");

        if (File.Exists(filePath))
        {
            var contentType = GetMimeType(filePath);
            var content = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);

            if (contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                var html = Utf8NoBom.GetString(content);
                content = Utf8NoBom.GetBytes(InjectLiveReloadScript(html));
            }

            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = content.Length;
            await context.Response.OutputStream.WriteAsync(content, ct).ConfigureAwait(false);
        }
        else
        {
            context.Response.StatusCode = httpNotFound;
            var notFound = Utf8NoBom.GetBytes("404 - Not Found");
            await context.Response.OutputStream.WriteAsync(notFound, ct).ConfigureAwait(false);
        }

        context.Response.Close();
    }

    private static bool ShouldSendCssEvent(IEnumerable<string> changedPaths, string projectPath)
    {
        var hasChanges = false;
        foreach (var path in changedPaths)
        {
            hasChanges = true;
            if (!Path.GetExtension(path).Equals(".css", StringComparison.OrdinalIgnoreCase))
                return false;

            var relative = Path.GetRelativePath(projectPath, path)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (relative.StartsWith("..", StringComparison.Ordinal))
                return false;

            if (!(relative.StartsWith($"themes{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith($"static{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return hasChanges;
    }

    private async Task RegisterSseClientAsync(HttpListenerContext context, CancellationToken ct)
    {
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers[HttpResponseHeader.CacheControl] = "no-cache";
        context.Response.Headers[HttpResponseHeader.Connection] = "keep-alive";
        context.Response.SendChunked = true;
        context.Response.KeepAlive = true;

        var id = Interlocked.Increment(ref _nextClientId);
        _sseClients.TryAdd(id, context.Response);

        try
        {
            var connectedBytes = Utf8NoBom.GetBytes(": connected\n\n");
            await context.Response.OutputStream.WriteAsync(connectedBytes, ct).ConfigureAwait(false);
            await context.Response.OutputStream.FlushAsync(ct).ConfigureAwait(false);
            await WaitForCancellationAsync(ct).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Response closed while disconnecting.
        }
        catch (HttpListenerException)
        {
            // Client disconnected.
        }
        finally
        {
            RemoveSseClient(id);
        }
    }

    private async Task BroadcastEventAsync(string eventName, string data, CancellationToken ct)
    {
        var payload = Utf8NoBom.GetBytes(BuildSsePayload(eventName, data));
        foreach (var pair in _sseClients)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await pair.Value.OutputStream.WriteAsync(payload, ct).ConfigureAwait(false);
                await pair.Value.OutputStream.FlushAsync(ct).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                RemoveSseClient(pair.Key);
            }
            catch (HttpListenerException)
            {
                RemoveSseClient(pair.Key);
            }
            catch (IOException)
            {
                RemoveSseClient(pair.Key);
            }
        }
    }

    private static string BuildSsePayload(string eventName, string data)
    {
        var safeData = data.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
        return $"event: {eventName}\ndata: {safeData}\n\n";
    }

    private void RemoveSseClient(int id)
    {
        if (!_sseClients.TryRemove(id, out var response))
            return;

        using (response)
        {
            try
            {
                response.Close();
            }
            catch (ObjectDisposedException)
            {
                // Already closed.
            }
            catch (HttpListenerException)
            {
                // Already disconnected.
            }
        }
    }

    private Task CloseSseClientsAsync()
    {
        foreach (var clientId in _sseClients.Keys.ToArray())
            RemoveSseClient(clientId);

        return Task.CompletedTask;
    }

    private static async Task WaitForCancellationAsync(CancellationToken ct)
    {
        if (!ct.CanBeCanceled)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ct.Register(static state =>
        {
            ((TaskCompletionSource<bool>)state!).TrySetResult(true);
        }, tcs);

        await tcs.Task.ConfigureAwait(false);
    }

    private static string BuildErrorMessage(BuildResult result)
    {
        if (result.Errors.Count == 0)
            return "Build failed.";

        return string.Join("\n", result.Errors);
    }

    private static string InjectLiveReloadScript(string html)
    {
        const string script =
            "<script>(function(){"
            + "var es=new EventSource('/__kiln/livereload');"
            + "es.addEventListener('reload',function(){location.reload();});"
            + "es.addEventListener('css',function(){"
            + "document.querySelectorAll('link[rel=\"stylesheet\"]').forEach(function(l){"
            + "var u=new URL(l.href,location.href);u.searchParams.set('_kiln',Date.now());l.href=u.href;});"
            + "var el=document.getElementById('__kiln_overlay');if(el){el.remove();}});"
            + "es.addEventListener('error',function(e){"
            + "var id='__kiln_overlay',el=document.getElementById(id);"
            + "if(!el){el=document.createElement('div');el.id=id;"
            + "el.style.cssText='position:fixed;inset:0;background:#1c1917;color:#fb923c;font:14px/1.5 ui-monospace,monospace;padding:2rem;white-space:pre-wrap;z-index:99999;overflow:auto';"
            + "document.body.appendChild(el);}"
            + "el.textContent='Build error:\n\n'+(e.data||'unknown');});"
            + "es.addEventListener('reload',function(){"
            + "var el=document.getElementById('__kiln_overlay');if(el){el.remove();}});"
            + "})();</script>";

        var bodyIndex = html.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyIndex >= 0)
            return html.Insert(bodyIndex, script);

        return string.Concat(html, script);
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

    private sealed class RebuildContext
    {
        public required DevServer Server { get; init; }

        public required ISiteBuilder SiteBuilder { get; init; }

        public required HashSet<string> PendingChanges { get; init; }

        public required object DebounceLock { get; init; }

        public required SemaphoreSlim RebuildSync { get; init; }

        public required string ProjectPath { get; init; }

        public required bool IncludeDrafts { get; init; }

        public required CancellationToken CancellationToken { get; init; }

        public Timer? DebounceTimer { get; set; }
    }
}
