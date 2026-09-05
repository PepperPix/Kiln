# [1.2.0-beta.7](https://github.com/PepperPix/Kiln/compare/v1.2.0-beta.6...v1.2.0-beta.7) (2026-09-05)


### Features

* add content plugin shortcodes ([#25](https://github.com/PepperPix/Kiln/issues/25)) ([f224eb1](https://github.com/PepperPix/Kiln/commit/f224eb1fd2f86b21a395a14dc85153d344e83b4d))

# [1.2.0-beta.6](https://github.com/PepperPix/Kiln/compare/v1.2.0-beta.5...v1.2.0-beta.6) (2026-08-16)


### Bug Fixes

* ensure pagefind indexes during kiln serve rebuilds ([a6f328f](https://github.com/PepperPix/Kiln/commit/a6f328f1c619edefa04f28cb2af86b6be1cf8865))
* migrate default theme search to component ui ([4b77263](https://github.com/PepperPix/Kiln/commit/4b7726392be99a830b59cc6497109e657e93ea53))
* run generated deploy workflows via dotnet tool run ([c354233](https://github.com/PepperPix/Kiln/commit/c354233ad1ac248d7534f0a430de605deec3d586))
* support base-url override and subfolder base-path for internal links ([11f9168](https://github.com/PepperPix/Kiln/commit/11f91681cd6d7790f2f384e7671b6bcc7c095433))

# [1.2.0-beta.5](https://github.com/PepperPix/Kiln/compare/v1.2.0-beta.4...v1.2.0-beta.5) (2026-08-16)


### Bug Fixes

* **cli:** use build metadata for CLI version ([e4fce35](https://github.com/PepperPix/Kiln/commit/e4fce35cdd0a639e6a7d861ca9f0d55a2d818e2b))

# [1.2.0-beta.4](https://github.com/PepperPix/Kiln/compare/v1.2.0-beta.3...v1.2.0-beta.4) (2026-08-05)


### Bug Fixes

* **core:** prevent parallel-execution race in PagefindBinaryProviderTests ([0595816](https://github.com/PepperPix/Kiln/commit/0595816755cc20561f3ce6e796fc202a35245d2c))

# [1.2.0-beta.3](https://github.com/PepperPix/Kiln/compare/v1.2.0-beta.2...v1.2.0-beta.3) (2026-07-20)


### Bug Fixes

* **core:** align SkiaSharp version with Avalonia.Skia 12.1 ([bcae5cb](https://github.com/PepperPix/Kiln/commit/bcae5cb97172f20af20a4570500877d074110534))

# [1.2.0-beta.2](https://github.com/PepperPix/Kiln/compare/v1.2.0-beta.1...v1.2.0-beta.2) (2026-07-19)


### Features

* **cli:** report live progress during kiln build ([d7f5fc7](https://github.com/PepperPix/Kiln/commit/d7f5fc7fc3780c882578f2c7489d4e4641ded046))
* **core:** extract shared asset reference index service ([a7b0a05](https://github.com/PepperPix/Kiln/commit/a7b0a05690575f7326340d192695dd32a910fee1))


### Performance Improvements

* **core:** parallelize content file reads within a section ([24bca59](https://github.com/PepperPix/Kiln/commit/24bca5904ca92ced99afe5e883570b71e1a2e6ad))
* **core:** use O(1) slug index for cross-collection reference resolution ([a7d4139](https://github.com/PepperPix/Kiln/commit/a7d41396f763c344a0c46ef747b8e890fd3a0dfa))

# [1.2.0-beta.1](https://github.com/PepperPix/Kiln/compare/v1.1.0...v1.2.0-beta.1) (2026-07-18)


### Bug Fixes

* **core:** encode spaces in link/image destinations before Markdig parsing ([ae27e40](https://github.com/PepperPix/Kiln/commit/ae27e402f5dd5f4a77a53c83a31c4e63ec596ecc))
* **core:** normalize collection directory separators for cross-platform path equality ([201c16d](https://github.com/PepperPix/Kiln/commit/201c16dee675aea3b603629ad51532f41b6db299))
* **core:** use camelCase for image_optimization/teaser_words keys ([9c0b426](https://github.com/PepperPix/Kiln/commit/9c0b426f5ddb58345985fdad93c31f91b8c25a27))
* **core:** use SkiaSharp.NativeAssets.Linux.NoDependencies ([2ce4008](https://github.com/PepperPix/Kiln/commit/2ce40083bad74277327997ed6d30bf57976539ef))


### Features

* **core:** image optimization pipeline for content images (ADR-051) ([b3f8ce0](https://github.com/PepperPix/Kiln/commit/b3f8ce0fb10ffa2da673cd716e3b7cb15389b521))

# [1.1.0-beta.3](https://github.com/PepperPix/Kiln/compare/v1.1.0-beta.2...v1.1.0-beta.3) (2026-07-12)


### Bug Fixes

* **core:** use camelCase for image_optimization/teaser_words keys ([9c0b426](https://github.com/PepperPix/Kiln/commit/9c0b426f5ddb58345985fdad93c31f91b8c25a27))
* **core:** use SkiaSharp.NativeAssets.Linux.NoDependencies ([2ce4008](https://github.com/PepperPix/Kiln/commit/2ce40083bad74277327997ed6d30bf57976539ef))


### Features

* **core:** image optimization pipeline for content images (ADR-051) ([b3f8ce0](https://github.com/PepperPix/Kiln/commit/b3f8ce0fb10ffa2da673cd716e3b7cb15389b521))

# [1.1.0-beta.2](https://github.com/PepperPix/Kiln/compare/v1.1.0-beta.1...v1.1.0-beta.2) (2026-07-10)


### Bug Fixes

* **core:** encode spaces in link/image destinations before Markdig parsing ([ae27e40](https://github.com/PepperPix/Kiln/commit/ae27e402f5dd5f4a77a53c83a31c4e63ec596ecc))
* **core:** normalize collection directory separators for cross-platform path equality ([201c16d](https://github.com/PepperPix/Kiln/commit/201c16dee675aea3b603629ad51532f41b6db299))

# [1.1.0-beta.1](https://github.com/PepperPix/Kiln/compare/v1.0.1-beta.1...v1.1.0-beta.1) (2026-07-07)


### Features

* **core:** content teaser fallback chain (description -> more-marker -> auto-truncate) ([#5](https://github.com/PepperPix/Kiln/issues/5)) ([3f86394](https://github.com/PepperPix/Kiln/commit/3f863944083f0a942c04a5be6e206a1595de6963))

## [1.0.1-beta.1](https://github.com/PepperPix/Kiln/compare/v1.0.0...v1.0.1-beta.1) (2026-07-07)


### Bug Fixes

* **core:** read taxonomies generically (PLAN-061, ADR-046) ([#3](https://github.com/PepperPix/Kiln/issues/3)) ([65b6d4a](https://github.com/PepperPix/Kiln/commit/65b6d4a0c2aee3aaabe2bbe9ca0aa6934bf776c1))

# 1.0.0 (2026-07-06)


### Bug Fixes

* address inspectcode findings and isolate search-index smoke test from real pagefind cache ([64cccbd](https://github.com/PepperPix/Kiln/commit/64cccbd260f3206baa325954db433da583b1f5d9))
* correct case-sensitive filename assertions in doc-generator tests ([0ed450e](https://github.com/PepperPix/Kiln/commit/0ed450e0ba9d904ed97cc24f51a769d77406d02d))
* correct CI badge repository owner in README (CScharf -> PepperPix) ([7a604ab](https://github.com/PepperPix/Kiln/commit/7a604ab772a4ce4a9144852bb70a6863e8a5fa16))
* correct demo showcase image path to /assets/favicon.svg ([e2b4320](https://github.com/PepperPix/Kiln/commit/e2b432073c09c03828942045f36ca9620eef645e))
* include search partial in scaffolded sites ([3d2171b](https://github.com/PepperPix/Kiln/commit/3d2171b6261416372f029975557b092ea557e5a4))
* normalize ContentItem.RelativePath and widen TestConsole (CI failures) ([3826195](https://github.com/PepperPix/Kiln/commit/3826195f63e4e48caf132dfb7a76bb0f7788c861))
* remove Spectre.Console dependency from Kiln.Core (layering) ([535392c](https://github.com/PepperPix/Kiln/commit/535392cc5246e880e0e33a43ff9e9e5f6ef3315b))


### Features

* add generator CLI foundation (build, serve, new) ([dfc6ba6](https://github.com/PepperPix/Kiln/commit/dfc6ba6848486ad832ed2a6922f3994c9cbbbc34))
* add homepage and 404 page to default theme and scaffold ([a81c9c8](https://github.com/PepperPix/Kiln/commit/a81c9c809b44c6be0ffbb3a531999843c4d8f2c3))
* add homepage, 404 page and limit filter to build engine ([0152469](https://github.com/PepperPix/Kiln/commit/0152469b9cf40d9e5abc09acd6313f5a69637897))
* add Kiln.Abstractions and DI builder for extensibility ([73a2eac](https://github.com/PepperPix/Kiln/commit/73a2eacf5ee58113a975c724c8acda914d8bc2fd))
* add live-reload to the dev server ([635caf9](https://github.com/PepperPix/Kiln/commit/635caf94290a94e1499b20282e97979e8fc28eb9))
* add opt-in pagefind search ui to default theme ([acb3f2d](https://github.com/PepperPix/Kiln/commit/acb3f2d074a153eb85d0bce82009ae9e00194864))
* add production asset pipeline with minify, fingerprinting and link-check ([f3147e3](https://github.com/PepperPix/Kiln/commit/f3147e34fad67055e53d71d593534a0fac9c4521))
* asset namespace /assets/ with page bundle support ([0d69532](https://github.com/PepperPix/Kiln/commit/0d695328a6e74e3da5db5427d552225852148127))
* build pagefind search index with on-demand binary acquisition ([7a4c2f2](https://github.com/PepperPix/Kiln/commit/7a4c2f24d32dcbb53383e38d04796a8579fb6bbb))
* collection-based domain model (SPEC-002) ([79040f6](https://github.com/PepperPix/Kiln/commit/79040f6e6317aca0f39bd3f49c3f833657e7f83b))
* demo content showcasing all Kiln features ([800a757](https://github.com/PepperPix/Kiln/commit/800a757cbe20b461f6deac696e2938ef17550132))
* Ember default theme with animated kiln logo, embedded resources ([bc90423](https://github.com/PepperPix/Kiln/commit/bc90423aaaee36ac9802f2292acd3bb6981e2c46))
* expose navigation tree and breadcrumb ancestors to templates ([23df9bb](https://github.com/PepperPix/Kiln/commit/23df9bbd0955f40ea0ea459118423aa494af2650))
* generate reference docs from dotnet xml documentation (kiln gen dotnet-xml) ([a2eebe1](https://github.com/PepperPix/Kiln/commit/a2eebe122aaffcc816e93adefbaf69f9181d1ee6))
* generate reference docs from openapi specs (kiln gen docs) ([7b5d4d9](https://github.com/PepperPix/Kiln/commit/7b5d4d914ab54288f9be6f6dff06681d734b6b8f))
* kiln deploy command for GitHub Pages and Azure SWA ([16fd0d4](https://github.com/PepperPix/Kiln/commit/16fd0d452818c60007695889a2ad401797c2917e))
* menus, sitemap, atom feed, robots.txt ([bae427c](https://github.com/PepperPix/Kiln/commit/bae427cff50be6444ab21328a80f4a2338ee2c55))
* read nested content sections with path-based urls ([ea7ccd8](https://github.com/PepperPix/Kiln/commit/ea7ccd8452cf0ed4865c41e229923ba0e0b6a8a4))
* taxonomies, pagination, collection indexes, cross-references ([b581718](https://github.com/PepperPix/Kiln/commit/b581718fc44640ce2495155c5a593ef99f48c3f5))
* template slots and plugin filesystem ([57bf73a](https://github.com/PepperPix/Kiln/commit/57bf73a8bbb0e03ac359c2a0c450716864853dfe))

# Changelog

All notable changes to this project are documented in this file. The format is based on
[Conventional Commits](https://www.conventionalcommits.org) and releases are generated by
semantic-release.
