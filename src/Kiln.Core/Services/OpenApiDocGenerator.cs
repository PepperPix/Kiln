namespace Kiln.Services;

using System.Text;
using Kiln.Models;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

public sealed class OpenApiDocGenerator(IGeneratedContentWriter writer) : IOpenApiDocGenerator
{
    private readonly IGeneratedContentWriter _writer = writer;

    public DocGenReport Generate(string specPath, string outputDir)
    {
        ArgumentNullException.ThrowIfNull(specPath);
        ArgumentNullException.ThrowIfNull(outputDir);

        var warnings = new List<string>();

        OpenApiDocument? doc;
        try
        {
            using var stream = File.OpenRead(specPath);
            var reader = new OpenApiStreamReader();
            doc = reader.Read(stream, out var diagnostic);

            foreach (var error in diagnostic.Errors)
                warnings.Add($"OpenAPI parse error: {error.Message}");
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (IOException ex)
        {
            warnings.Add($"Failed to load OpenAPI spec: {ex.Message}");
            return new DocGenReport([], [], [], warnings);
        }
        catch (UnauthorizedAccessException ex)
        {
            warnings.Add($"Failed to load OpenAPI spec: {ex.Message}");
            return new DocGenReport([], [], [], warnings);
        }

        if (doc?.Paths is null)
            return new DocGenReport([], [], [], warnings);

        var written = new List<string>();
        var skipped = new List<string>();
        var conflicts = new List<string>();
        var weight = 0;

        var sortedPaths = doc.Paths.OrderBy(p => p.Key, StringComparer.Ordinal);

        foreach (var (path, pathItem) in sortedPaths)
        {
            if (pathItem.Operations is not { Count: > 0 })
                continue;

            var sortedOps = pathItem.Operations.OrderBy(op => (int)op.Key);

            foreach (var (operationType, operation) in sortedOps)
            {
                weight++;

                var tagSlug = GetTagSlug(operation, path);
                var opSlug = GetOperationSlug(operation, operationType, path);
                var relativePath = $"{tagSlug}/{opSlug}.md";

                var method = operationType.ToString().ToUpperInvariant();
                var title = operation.Summary ?? operation.OperationId ?? $"{method} {path}";

                var frontMatter = new List<(string Key, object Value)>
                {
                    ("title", title),
                    ("weight", weight),
                    ("generated", true),
                    ("extra", new Dictionary<string, string>
                    {
                        ["method"] = method,
                        ["path"] = path,
                    }),
                };

                var body = BuildBody(operation, operationType, path);
                var file = new GeneratedContentFile(relativePath, frontMatter, body);

                var result = _writer.Write(outputDir, file);

                switch (result)
                {
                    case WriteResult.Written:
                        written.Add(relativePath);
                        break;
                    case WriteResult.SkippedAdopted:
                        skipped.Add(relativePath);
                        break;
                    case WriteResult.Conflict:
                        conflicts.Add(relativePath);
                        break;
                }
            }
        }

        return new DocGenReport(written, skipped, conflicts, warnings);
    }

    private static string BuildBody(OpenApiOperation operation, OperationType operationType, string path)
    {
        var method = operationType.ToString().ToUpperInvariant();
        var title = operation.Summary ?? operation.OperationId ?? $"{method} {path}";
        var sb = new StringBuilder();

        sb.Append("# ").Append(title).Append('\n');
        sb.Append('\n');
        sb.Append('`').Append(method).Append(' ').Append(path).Append("`\n");

        var description = operation.Description;
        if (!string.IsNullOrWhiteSpace(description) &&
            !string.Equals(description.Trim(), operation.Summary, StringComparison.Ordinal))
        {
            sb.Append('\n');
            sb.Append(description.Trim());
            sb.Append('\n');
        }

        if (operation.Parameters is { Count: > 0 })
        {
            sb.Append('\n');
            sb.Append("## Parameters\n");
            sb.Append('\n');
            sb.Append("| Name | In | Required | Type |\n");
            sb.Append("|------|----|----------|------|\n");

            foreach (var param in operation.Parameters.OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                var paramIn = ParameterLocationToString(param.In);
                var required = param.Required ? "yes" : "no";
                var type = param.Schema?.Type ?? "";
                sb.Append("| ").Append(param.Name)
                  .Append(" | ").Append(paramIn)
                  .Append(" | ").Append(required)
                  .Append(" | ").Append(type)
                  .Append(" |\n");
            }
        }

        if (operation.RequestBody is not null)
        {
            sb.Append('\n');
            sb.Append("## Request Body\n");

            if (!string.IsNullOrWhiteSpace(operation.RequestBody.Description))
            {
                sb.Append('\n');
                sb.Append(operation.RequestBody.Description.Trim());
                sb.Append('\n');
            }

            foreach (var (contentType, mediaType) in operation.RequestBody.Content
                .OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                sb.Append('\n');
                sb.Append("**Content-Type:** `").Append(contentType).Append("`\n");

                if (mediaType.Schema?.Properties is { Count: > 0 } properties)
                {
                    sb.Append('\n');
                    sb.Append("| Property | Type |\n");
                    sb.Append("|----------|------|\n");

                    foreach (var (propName, propSchema) in properties
                        .OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        sb.Append("| ").Append(propName)
                          .Append(" | ").Append(propSchema.Type ?? "")
                          .Append(" |\n");
                    }
                }
            }
        }

        if (operation.Responses is { Count: > 0 })
        {
            sb.Append('\n');
            sb.Append("## Responses\n");
            sb.Append('\n');
            sb.Append("| Status | Description |\n");
            sb.Append("|--------|-------------|\n");

            foreach (var (statusCode, response) in operation.Responses
                .OrderBy(r => r.Key, StringComparer.Ordinal))
            {
                sb.Append("| ").Append(statusCode)
                  .Append(" | ").Append(response.Description ?? "")
                  .Append(" |\n");
            }
        }

        return sb.ToString();
    }

    private static string GetTagSlug(OpenApiOperation operation, string path)
    {
        var tag = operation.Tags?.FirstOrDefault()?.Name;
        if (!string.IsNullOrWhiteSpace(tag))
            return Slugify(tag);

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0)
            return Slugify(segments[0]);

        return "misc";
    }

    private static string GetOperationSlug(OpenApiOperation operation, OperationType operationType, string path)
    {
        if (!string.IsNullOrWhiteSpace(operation.OperationId))
            return Slugify(operation.OperationId);

        var method = operationType.ToString();
        var pathSlug = path
            .Replace('{', '-')
            .Replace('}', '-')
            .Replace('/', '-')
            .Trim('-');
        return Slugify($"{method}-{pathSlug}");
    }

    private static string Slugify(string input)    {
        if (string.IsNullOrWhiteSpace(input))
            return "misc";

        var sb = new StringBuilder();

        foreach (var c in input)
        {
            var lower = char.ToLowerInvariant(c);
            if (char.IsLetterOrDigit(lower))
                sb.Append(lower);
            else if (c is ' ' or '-' or '_' or '/')
            {
                if (sb.Length > 0 && sb[^1] != '-')
                    sb.Append('-');
            }
        }

        while (sb.Length > 0 && sb[^1] == '-')
            sb.Length--;

        return sb.Length > 0 ? sb.ToString() : "misc";
    }

    private static string ParameterLocationToString(ParameterLocation? location) => location switch
    {
        ParameterLocation.Query => "query",
        ParameterLocation.Header => "header",
        ParameterLocation.Path => "path",
        ParameterLocation.Cookie => "cookie",
        _ => "",
    };
}
