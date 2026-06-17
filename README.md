# Kiln

🔥 A fast, extensible static site generator for .NET — build beautiful websites from Markdown and templates.

## Status

**Early Development** — not yet functional. See the [roadmap](#roadmap) below.

## What is Kiln?

Kiln is a static site generator (SSG) implemented as a .NET global tool. It transforms Markdown content and templates into fast, static HTML websites.

```bash
# Install (once published)
dotnet tool install -g dotnet-kiln

# Create a new site
kiln new my-site

# Build
kiln build

# Serve locally with live reload
kiln serve
```

## Project Structure

```
src/
├── Kiln.Core/          # Engine library — content pipeline, templating, themes
└── Kiln.Cli/           # CLI tool — commands, configuration, dev server
tests/
├── Kiln.Core.Tests/
└── Kiln.Cli.Tests/
```

## Roadmap

- [ ] Markdown → HTML rendering (CommonMark + extensions)
- [ ] YAML frontmatter parsing
- [ ] Template engine integration
- [ ] Theme system (layouts, partials)
- [ ] Posts, pages, tags, categories
- [ ] Sitemap, RSS/Atom feed, robots.txt
- [ ] SEO meta tags & Open Graph
- [ ] Local dev server with file watcher
- [ ] CLI commands: `new`, `build`, `serve`, `deploy`
- [ ] Image optimization
- [ ] Plugin API

## Related Projects

- **[Kiln Studio](https://github.com/PepperPix/Kiln-Studio)** — Cross-platform desktop CMS powered by Kiln

## License

[Apache-2.0](LICENSE)

Copyright 2026 Marcel Kummerow
