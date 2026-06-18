namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class NewCommand : Command<NewCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Name of the new site project (becomes directory name).")]
        public required string Name { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(settings.Name);

        if (Directory.Exists(projectPath) && Directory.EnumerateFileSystemEntries(projectPath).Any())
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Directory '{settings.Name}' already exists and is not empty.");
            return 1;
        }

        // Create directory structure
        Directory.CreateDirectory(projectPath);
        Directory.CreateDirectory(Path.Combine(projectPath, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(projectPath, "content", "pages"));
        Directory.CreateDirectory(Path.Combine(projectPath, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(projectPath, "themes", "default", "partials"));
        Directory.CreateDirectory(Path.Combine(projectPath, "themes", "default", "static", "css"));
        Directory.CreateDirectory(Path.Combine(projectPath, "static"));

        // site.yaml with collections
        File.WriteAllText(Path.Combine(projectPath, "site.yaml"),
            $"""
            title: {settings.Name}
            description: A new Kiln site
            baseUrl: http://localhost:5555
            language: en
            theme: default

            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
                sort: date desc
                feed: true
                paginate: 10
                taxonomies:
                  - tags
                  - categories
                layout: post

              pages:
                directory: content/pages
                permalink: /:slug/
                sort: weight asc
                layout: page

            taxonomies:
              tags:
                permalink: /tags/:slug/
              categories:
                permalink: /categories/:slug/
            """);

        var helloWorldId = Guid.NewGuid().ToString("D");
        var aboutId = Guid.NewGuid().ToString("D");
        var today = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        // Sample post
        File.WriteAllText(Path.Combine(projectPath, "content", "posts", "hello-world.md"),
            $"""
            ---
            id: {helloWorldId}
            title: Hello World
            date: {today}
            tags:
              - kiln
              - getting-started
            ---

            Welcome to your new **Kiln** site!

            This is your first post. Edit it or create new `.md` files in `content/posts/`.
            """);

        // Sample page
        File.WriteAllText(Path.Combine(projectPath, "content", "pages", "about.md"),
            $"""
            ---
            id: {aboutId}
            title: About
            weight: 10
            ---

            This is the about page.
            """);

        // Post layout
        File.WriteAllText(Path.Combine(projectPath, "themes", "default", "layouts", "post.html"),
            """
            <!DOCTYPE html>
            <html lang="{{ site.language }}">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{{ page.title }} — {{ site.title }}</title>
                {{ include 'head' }}
            </head>
            <body>
                <header>
                    <h1><a href="/">{{ site.title }}</a></h1>
                </header>
                <main>
                    <article>
                        <h1>{{ page.title }}</h1>
                        {{ if page.date }}<time>{{ page.date | date.to_string '%Y-%m-%d' }}</time>{{ end }}
                        {{ page.content }}
                    </article>
                </main>
            </body>
            </html>
            """);

        // Default (page) layout
        File.WriteAllText(Path.Combine(projectPath, "themes", "default", "layouts", "default.html"),
            """
            <!DOCTYPE html>
            <html lang="{{ site.language }}">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{{ page.title }} — {{ site.title }}</title>
                {{ include 'head' }}
            </head>
            <body>
                <header>
                    <h1><a href="/">{{ site.title }}</a></h1>
                </header>
                <main>
                    {{ page.content }}
                </main>
            </body>
            </html>
            """);

        // Head partial
        File.WriteAllText(Path.Combine(projectPath, "themes", "default", "partials", "head.html"),
            """
            <meta name="description" content="{{ page.description ?? site.description }}">
            <link rel="stylesheet" href="{{ asset_url 'css/style.css' }}">
            """);

        // Default stylesheet
        File.WriteAllText(Path.Combine(projectPath, "themes", "default", "static", "css", "style.css"),
            """
            :root { font-family: system-ui, sans-serif; line-height: 1.6; }
            body { max-width: 48rem; margin: 2rem auto; padding: 0 1rem; }
            """);

        AnsiConsole.MarkupLine($"[green]Created[/] new site at [blue]{projectPath}[/]");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("Next steps:");
        AnsiConsole.MarkupLine($"  cd {settings.Name}");
        AnsiConsole.MarkupLine("  kiln serve");

        return 0;
    }
}
