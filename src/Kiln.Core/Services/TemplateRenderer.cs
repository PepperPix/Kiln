namespace Kiln.Services;

using System.Linq;
using Kiln.Models;
using Scriban;
using Scriban.Runtime;

public sealed class TemplateRenderer : ITemplateRenderer
{
    public string Render(ContentItem item, SiteConfiguration site, string themePath)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(site);
        var layoutName = item.Layout ?? item.Collection.Layout;
        var layoutPath = Path.Combine(themePath, "layouts", $"{layoutName}.html");

        if (!File.Exists(layoutPath))
        {
            var fallback = Path.Combine(themePath, "layouts", "default.html");
            if (!File.Exists(fallback))
                throw new FileNotFoundException($"Layout '{layoutName}' not found at: {layoutPath}");
            layoutPath = fallback;
        }

        var templateSource = File.ReadAllText(layoutPath);
        var template = Template.Parse(templateSource, layoutPath);

        if (template.HasErrors)
            throw new InvalidOperationException(
                $"Template errors in '{layoutPath}': {string.Join(", ", template.Messages)}");

        var context = new TemplateContext();
        var scriptObject = new ScriptObject();

        scriptObject.Add("site", new
        {
            title = site.Title,
            description = site.Description,
            base_url = site.BaseUrl.ToString().TrimEnd('/'),
            language = site.Language
        });

        scriptObject.Add("page", new
        {
            id = item.Id,
            title = item.Title,
            date = item.Date,
            content = item.HtmlContent,
            url = item.Url.OriginalString,
            slug = item.Slug,
            description = item.Description,
            draft = item.Draft,
            weight = item.Weight,
            extra = item.Extra,
            tags = item.Taxonomies.GetValueOrDefault("tags"),
            categories = item.Taxonomies.GetValueOrDefault("categories"),
            collection = new { name = item.Collection.Name, url = item.Collection.Url.OriginalString }
        });

        scriptObject.Add("collection", new
        {
            name = item.Collection.Name,
            items = item.Collection.Items.Where(static i => !i.Draft).ToList(),
            url = item.Collection.Url
        });

        var collectionsDict = site.Collections.ToDictionary(
            kvp => kvp.Key,
            kvp => (object)new
            {
                name = kvp.Value.Name,
                items = kvp.Value.Items.Where(static i => !i.Draft).ToList(),
                url = kvp.Value.Url.OriginalString
            });
        scriptObject.Add("collections", collectionsDict);
        scriptObject.Add("plugins", site.Plugins);
        scriptObject.Add("theme", site.ThemeConfig);

        // Include function for partials
        var partialsDir = Path.Combine(themePath, "partials");
        scriptObject.Import("include", new Func<string, string>(partialName =>
        {
            var partialPath = Path.Combine(partialsDir, $"{partialName}.html");
            if (!File.Exists(partialPath))
                return $"<!-- partial '{partialName}' not found -->";

            var partialTemplate = Template.Parse(File.ReadAllText(partialPath), partialPath);
            return partialTemplate.Render(context);
        }));

        context.PushGlobal(scriptObject);
        return template.Render(context);
    }
}

