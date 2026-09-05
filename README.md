# Kiln

[![CI](https://github.com/PepperPix/Kiln/actions/workflows/ci.yml/badge.svg)](https://github.com/PepperPix/Kiln/actions/workflows/ci.yml)

Kiln is a fast, extensible static site generator for .NET. It is distributed as a .NET global tool and focuses on a collection-based content model, Scriban templates, and CDN-friendly asset namespaces.

**Status:** Early development — see the roadmap and tests for current progress.

## Quick Start

Install the global tool (once published) and create a site:

```bash
dotnet tool install -g Kiln
kiln new my-site
```

Build and serve locally:

```bash
kiln build
kiln serve
```

## Features

- Collection-based content model with nested (recursive) content sections
- Taxonomies (tags, categories)
- Pagination helpers
- Plugin slots and extensible templates
- CDN-friendly `/assets/` namespacing
- Atom/RSS feeds and sitemap generation
- Local dev server with auto-rebuild (dev server)
- Reference documentation generation from OpenAPI specs and .NET XML docs
- Built-in search indexing (Pagefind)

## Project Layout

```
src/
├── Kiln.Core/    # Engine: content pipeline, templating, builders
└── Kiln.Cli/     # CLI: commands, dev server, tooling
tests/
├── Kiln.Core.Tests/
└── Kiln.Cli.Tests/
```

## CLI Reference

Common commands (exact names):

- `kiln new <site-name>` — create a new site scaffold
- `kiln build` — build the static site (output directory: `_site`)
- `kiln serve` — start local dev server with live-reload
- `kiln deploy <target>` — initialize deployment workflows; supported targets: `github-pages`, `azure-swa`
- `kiln gen docs --openapi <spec> --output <dir>` — generate reference documentation from an OpenAPI spec
- `kiln gen dotnet-xml --xml <path> --output <dir>` — generate reference documentation from a .NET XML documentation file
- `kiln search index [path] [--no-download] [--extended] [--output <dir>]` — build a Pagefind search index over the build output

Examples:

```bash
kiln new blog
kiln build
kiln serve

# Initialize GitHub Pages deployment workflow in the current project
kiln deploy github-pages

# Initialize Azure Static Web Apps workflow and staticwebapp.config.json
kiln deploy azure-swa

# Generate API reference docs from an OpenAPI spec into content/api
kiln gen docs --openapi openapi.json --output content/api

# Generate API reference docs from a .NET XML documentation file
kiln gen dotnet-xml --xml bin/Release/net10.0/MyLib.xml --output content/api-dotnet

# Build a search index over the default output directory
kiln search index
```

Important: the `deploy` command expects a target argument, e.g. `github-pages` or `azure-swa`.

## Plugins find & install

Kiln can discover and install content plugins from NuGet packages marked with the `kiln-plugin` tag.

> Security warning: content plugins can inject arbitrary HTML and JavaScript into pages, so install only packages from trusted sources.

```bash
# Search public NuGet packages for Kiln plugins
kiln plugin search email-protect

# Install a plugin into the current project
kiln plugin add Kiln.Plugin.EmailProtect --version 1.0.0

# Update a plugin that was installed through kiln plugin add
kiln plugin update email-protect

# Update all plugins recorded in .kiln/plugins.lock.json
kiln plugin update --all

# Remove a plugin folder and its lock entry
kiln plugin remove email-protect --yes

# List local plugins and their installation source
kiln plugin list
```

Each installed plugin is recorded in `.kiln/plugins.lock.json` with the package ID, installed version, and source (`nuget`). This file is intentionally project-local and versioned so `kiln plugin update` can resolve the correct package without guessing names.

## Theme & Template Development

Kiln uses Scriban templates for layouts, partials and helpers. Basic concepts:

- Layouts: top-level HTML layout files
- Partials: reusable template fragments
- Slots: named insertion points for plugins and themes

Refer to `templates/default` in the project for a minimal theme layout and examples.

## Content Plugin Shortcodes

Content plugins can declare optional `shortcodes:` entries in `plugin.yaml` and provide matching partials under `plugins/<name>/shortcodes/<shortcode-name>.html`.

Example plugin manifest:

```yaml
name: email-protect
version: "1.1.0"
slots:
  - body_end
shortcodes:
  - email
```

A shortcode partial for the `email` shortcode would live at:

```text
plugins/email-protect/shortcodes/email.html
```

Content authors then write inline calls in Markdown body content using the `{% ... %}` syntax:

```md
Contact {% email "hello@cscharf.de" %} today.
```

The shortcode partial receives its parsed arguments, a `plugin_asset_url` helper scoped to the active plugin, and the built-in `string.base64_encode` filter. The current v1 scope intentionally does not provide a full `page`/`collection` render context; the shortcode is resolved while content is read and before the main page-render pipeline starts.

Shortcodes are not resolved inside fenced code blocks (` ``` ` / `~~~`), so example snippets remain literal text.

Template data available to layouts and partials includes:

- `page.ancestors` — the breadcrumb chain for the current page, ordered root → … → parent (the
  current page itself is not included). Each entry is `{title, url}`.
- `page.no_index` — `true` when the current page has `no_index: true` in front matter; the engine
  injects the robots meta tag automatically and does not require theme logic to re-render it.
- `navtree.<collection-name>` — the hierarchical navigation tree for a given collection. Each node
  is `{title, url, weight, is_active, is_ancestor, children}`, where `is_active`/`is_ancestor` are
  computed relative to the page currently being rendered.

Front matter can also set `no_index: true` on individual pages to keep them out of the generated
`sitemap.xml` and force a `<meta name="robots" content="noindex, nofollow">` tag in the rendered
HTML. This is intentionally separate from `robots.txt` generation; the engine handles the page-level
no-index signal without creating a route-specific `Disallow` entry.

## Reference Documentation Generation

Kiln can generate reference documentation content from two sources:

- **OpenAPI specs** (`kiln gen docs --openapi <spec> --output <dir>`) — generates one content file
  per endpoint/operation.
- **.NET XML documentation** (`kiln gen dotnet-xml --xml <path> --output <dir>`) — generates one
  content file per documented type/member from a project built with
  `<GenerateDocumentationFile>true</GenerateDocumentationFile>`.

Both adapters share a three-tier ownership model for generated content files:

1. Generated files are written with `generated: true` and a `source_hash` in their frontmatter.
2. A file with `generated: false` is treated as **adopted** — it is never touched again by
   subsequent generation runs, even if the source spec/XML changes.
3. If a generated file was edited outside of Kiln (its content no longer matches `source_hash`) and
   the source changed again, Kiln does not overwrite it — it writes `<file>.md.regenerated` next to
   it instead, so no manual edits are lost.

```bash
kiln gen docs --openapi openapi.json --output content/api
kiln gen dotnet-xml --xml bin/Release/net10.0/MyLib.xml --output content/api-dotnet
```

The output directory must be registered as a collection in `site.yaml` to be included in the build;
both commands print a warning if it is not.

## Search

Kiln integrates [Pagefind](https://pagefind.app) for static full-text search.

Enable it in `site.yaml`:

```yaml
search:
  enabled: true      # default: false
  extended: false    # use the Pagefind extended binary (multilingual support)
  binaryPath: null   # optional explicit path to a pagefind binary
```

Build the index after `kiln build`:

```bash
kiln build
kiln search index
```

The Pagefind binary is resolved in this order:

1. `KILN_PAGEFIND_PATH` environment variable, if set and the file exists.
2. The system `PATH`.
3. The local cache at `~/.kiln/tools/pagefind/<version>/` (override the cache root with
   `KILN_PAGEFIND_CACHE_DIR`).
4. Automatic download from the Pagefind GitHub releases, with SHA256 verification (use
   `--no-download` to disable this and fail instead).

Search is disabled by default and `kiln serve` never triggers indexing or a download. The default
theme ships with an opt-in search UI that is self-guarded and requires no extra setup once search is
enabled and indexed.

## Nested Content Sections

Content collections are read recursively. For a given directory:

- A directory containing an `index.md` is a **leaf bundle** and is not recursed into further.
- Any other directory is a **section** and is read recursively.

URLs of nested content mirror the directory path, e.g. `guides/advanced/install.md` becomes
`/guides/advanced/install/`. Flat sites (no subdirectories) behave exactly as before.

## CI / CD Deployment Recipes

The Kiln CLI generates example workflows for common hosting platforms. Workflows produced by `kiln deploy` use the following build pattern:

- Restore tools: `dotnet tool restore`
- Build site: `kiln build`
- Publish directory: `_site`

Below are platform-specific recipes. CI YAML snippets must keep the same build commands used by the generated workflows.

### GitHub Pages (GitHub Actions)

Kiln creates a GitHub Actions workflow at `.github/workflows/deploy.yml` when you run `kiln deploy github-pages`. The generated workflow uses `dotnet tool restore` and `kiln build` and then uploads `_site` as the pages artifact.

Example (generated by the CLI):

```yaml
name: Deploy to GitHub Pages

on:
	push:
		branches: [main]

permissions:
	contents: read
	pages: write
	id-token: write

jobs:
	build-and-deploy:
		runs-on: ubuntu-latest
		steps:
			- uses: actions/checkout@v4
			- name: Setup .NET
				uses: actions/setup-dotnet@v4
				with:
					dotnet-version: '10.0.x'
			- name: Restore tools
				run: dotnet tool restore
			- name: Build site
				run: kiln build --base-url ${{ vars.SITE_URL || 'https://username.github.io/repo' }}
			- name: Upload artifact
				uses: actions/upload-pages-artifact@v3
				with:
					path: _site
			- name: Deploy to GitHub Pages
				id: deployment
				uses: actions/deploy-pages@v4
```

### Azure Static Web Apps (GitHub Actions)

Kiln generates `.github/workflows/azure-swa.yml` and a `staticwebapp.config.json` when you run `kiln deploy azure-swa`.

Key points:

- The generated job sets `app_build_command: "dotnet tool restore && kiln build"` to ensure tools are available during the build step.
- Output folder is `_site`.
- `staticwebapp.config.json` contains a `routes`/`navigationFallback` and cache headers for `/assets/*`.

Example (generated by the CLI):

```yaml
name: Deploy to Azure Static Web Apps

on:
	push:
		branches: [main]
	pull_request:
		branches: [main]

jobs:
	build-and-deploy:
		runs-on: ubuntu-latest
		steps:
			- uses: actions/checkout@v4
			- name: Setup .NET
				uses: actions/setup-dotnet@v4
				with:
					dotnet-version: '10.0.x'
			- name: Build and Deploy
				uses: Azure/static-web-apps-deploy@v1
				with:
					azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
					app_location: "/"
					output_location: "_site"
					app_build_command: "dotnet tool restore && kiln build"
					skip_api_build: true
```

`staticwebapp.config.json` example (generated):

```json
{
	"navigationFallback": {
		"rewrite": "/index.html",
		"exclude": ["/assets/*"]
	},
	"routes": [
		{
			"route": "/assets/*",
			"headers": {
				"Cache-Control": "public, max-age=31536000, immutable"
			}
		}
	]
}
```

### Azure DevOps (YAML)

Use the following snippet in an Azure Pipelines YAML to restore tools and build. Ensure the agent has .NET 10 SDK installed.

```yaml
pool:
	vmImage: 'ubuntu-latest'

steps:
- script: |
		dotnet tool restore
		kiln build
	displayName: 'Restore tools and build'
```

### Netlify

Set the build command to the following (Netlify UI or netlify.toml):

Build command:

```bash
dotnet tool restore && kiln build
```

Publish directory: `_site`

### Cloudflare Pages

Use a build command equivalent to Netlify. Ensure the selected environment supports .NET 10 SDK.

Build command:

```bash
dotnet tool restore && kiln build
```

Publish directory: `_site`

### Generic CI

Any CI system that can run `dotnet tool restore && kiln build` and publish `_site` as the artifact/website can be used.

## NuGet Package Description

Kiln — A fast, extensible static site generator for .NET. Collection-based content model, Scriban templates, plugin slots, CDN-friendly asset namespacing, built-in Atom feeds and sitemaps.

## Roadmap

- Markdown rendering, frontmatter, templates, theme system, sitemap, feeds, dev server, plugin API, image optimization.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for developer guidelines and tests. Keep commits focused and descriptive.

## License

[Apache-2.0](LICENSE)

Copyright 2026 Marcel Kummerow
