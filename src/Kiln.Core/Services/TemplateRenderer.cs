namespace Kiln.Services;

using Kiln.Models;
using Scriban;
using Scriban.Runtime;

public sealed class TemplateRenderer : ITemplateRenderer
{
    public string Render(ContentItem item, SiteConfiguration site, string themePath)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(site);
        var layoutName = item.FrontMatter.Layout ?? "default";
        var layoutPath = Path.Combine(themePath, "layouts", $"{layoutName}.html");

        if (!File.Exists(layoutPath))
            throw new FileNotFoundException($"Layout '{layoutName}' not found at: {layoutPath}");

        var templateSource = File.ReadAllText(layoutPath);
        var template = Template.Parse(templateSource, layoutPath);

        if (template.HasErrors)
            throw new InvalidOperationException(
                $"Template errors in '{layoutPath}': {string.Join(", ", template.Messages)}");

        var context = new TemplateContext();
        var scriptObject = new ScriptObject();

        // Expose site and page data to templates
        scriptObject.Add("site", new
        {
            title = site.Title,
            description = site.Description,
            base_url = site.BaseUrl,
            language = site.Language
        });

        scriptObject.Add("page", new
        {
            title = item.FrontMatter.Title,
            date = item.FrontMatter.Date,
            description = item.FrontMatter.Description,
            tags = item.FrontMatter.Tags,
            categories = item.FrontMatter.Categories,
            content = item.HtmlContent,
            url = item.Url
        });

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
