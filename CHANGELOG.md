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
