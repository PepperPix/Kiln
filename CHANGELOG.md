# [1.3.0-beta.2](https://github.com/PepperPix/Kiln/compare/v1.3.0-beta.1...v1.3.0-beta.2) (2026-09-29)


### Bug Fixes

* **core:** kill child process on cancellation ([ab06146](https://github.com/PepperPix/Kiln/commit/ab06146c931ca4d6511a675e15d52b7c5aa98208))
* **core:** load plugins in deterministic order ([0266d1c](https://github.com/PepperPix/Kiln/commit/0266d1c37a55b9c5da05d3d0cb3813600f5d9c44))
* **core:** observe dev server rebuild task exceptions ([c968e4f](https://github.com/PepperPix/Kiln/commit/c968e4f4b751a42a4feea366676051e6e4c10886))
* **core:** require pagefind 1.5.0 for path binaries ([cf77952](https://github.com/PepperPix/Kiln/commit/cf77952ea987ed5ec9fd49f1ea2fcac621b8ee50))
* **core:** skip inline code in shortcodes ([4295638](https://github.com/PepperPix/Kiln/commit/429563826a01597ab2fa7c992532618c0f6e1937))


### Features

* **cli:** keep existing deploy files unless --force ([77d09c7](https://github.com/PepperPix/Kiln/commit/77d09c76dce8bea83db2a51b025536a058a5e856))


### Performance Improvements

* **core:** cache parsed templates ([d43d093](https://github.com/PepperPix/Kiln/commit/d43d0939a7cc9d8f9924e667180b6779984750ff))

# [1.3.0-beta.1](https://github.com/PepperPix/Kiln/compare/v1.2.0...v1.3.0-beta.1) (2026-09-29)


### Bug Fixes

* **plugins:** allow removing legacy plugin directories ([b69328a](https://github.com/PepperPix/Kiln/commit/b69328a3b6e9a3519bb18f6845cf077d311c7f43))
* **plugins:** keep ignored-handler constructors as obsolete instead of removing them ([476d3c2](https://github.com/PepperPix/Kiln/commit/476d3c29bf1f998fa1c6f8d18a9246eca1d4ffa3))
* **plugins:** require only the kiln-plugin tag and drop the id prefix requirement ([a813483](https://github.com/PepperPix/Kiln/commit/a813483c437d22df1a94208b59654b31492386f8))


### Features

* **plugins:** add allow-any-package and force options and escape markup output ([efa9577](https://github.com/PepperPix/Kiln/commit/efa95779f684f674b581adad232df0fdf9428fdf))
* **plugins:** add plugin info command ([fd96005](https://github.com/PepperPix/Kiln/commit/fd960057e0de6b97ede991507ccf0d56bdafa1ca))
* **plugins:** classify plugin trust level and add package inspection ([4e8b11f](https://github.com/PepperPix/Kiln/commit/4e8b11ffb14c07f7e1fb1fc337c11f4783623c23))
* **plugins:** confirm community plugin installs ([0152105](https://github.com/PepperPix/Kiln/commit/015210593f7d204f34f7e166a7c1653aded40a66))
* **plugins:** confirm significant plugin updates ([8c615bb](https://github.com/PepperPix/Kiln/commit/8c615bb68f33f548ac9fbcf43d6a62733f132de8))
* **plugins:** enforce plugin convention, record content hash and protect local changes ([fb10d8d](https://github.com/PepperPix/Kiln/commit/fb10d8d43a3eef0cdee8daa5aefef1f294d1b675))
* **plugins:** show trust level in plugin search and list ([e25c8e7](https://github.com/PepperPix/Kiln/commit/e25c8e758bfcc4e2b20b5139fd89668dbc2704a5))


### Reverts

* remove unused http handler constructors from nuget plugin client ([df89e61](https://github.com/PepperPix/Kiln/commit/df89e61529f24d8ee6d10da83422e264d0c3985a))

# [1.2.0](https://github.com/PepperPix/Kiln/compare/v1.1.0...v1.2.0) (2026-09-29)


### Bug Fixes

* **cli:** reject unknown options ([b8bcafd](https://github.com/PepperPix/Kiln/commit/b8bcafd50fcf90e016ba2de92725d226bc5c621e))
* **cli:** use build metadata for CLI version ([e4fce35](https://github.com/PepperPix/Kiln/commit/e4fce35cdd0a639e6a7d861ca9f0d55a2d818e2b))
* **core:** align SkiaSharp version with Avalonia.Skia 12.1 ([bcae5cb](https://github.com/PepperPix/Kiln/commit/bcae5cb97172f20af20a4570500877d074110534))
* **core:** confine dev server requests to output dir ([5f1022d](https://github.com/PepperPix/Kiln/commit/5f1022d4acf726a204d8e107c065c3789897d30a))
* **core:** encode spaces in link/image destinations before Markdig parsing ([ae27e40](https://github.com/PepperPix/Kiln/commit/ae27e402f5dd5f4a77a53c83a31c4e63ec596ecc))
* **core:** lift scriban loop and string limits for templates ([2cd733a](https://github.com/PepperPix/Kiln/commit/2cd733ac22a87bd5a915ae248511cf8f29799fa3))
* **core:** normalize collection directory separators for cross-platform path equality ([201c16d](https://github.com/PepperPix/Kiln/commit/201c16dee675aea3b603629ad51532f41b6db299))
* **core:** prevent parallel-execution race in PagefindBinaryProviderTests ([0595816](https://github.com/PepperPix/Kiln/commit/0595816755cc20561f3ce6e796fc202a35245d2c))
* **core:** reject unsafe output directories ([9453845](https://github.com/PepperPix/Kiln/commit/94538454d51b71f55770b3058efe4a09dcbc472d))
* **core:** use camelCase for image_optimization/teaser_words keys ([9c0b426](https://github.com/PepperPix/Kiln/commit/9c0b426f5ddb58345985fdad93c31f91b8c25a27))
* **core:** use SkiaSharp.NativeAssets.Linux.NoDependencies ([2ce4008](https://github.com/PepperPix/Kiln/commit/2ce40083bad74277327997ed6d30bf57976539ef))
* **core:** write pagefind binary atomically ([c4435ec](https://github.com/PepperPix/Kiln/commit/c4435ec77d1faca2475d57ec1b92b14f01f612fa))
* ensure pagefind indexes during kiln serve rebuilds ([a6f328f](https://github.com/PepperPix/Kiln/commit/a6f328f1c619edefa04f28cb2af86b6be1cf8865))
* migrate default theme search to component ui ([4b77263](https://github.com/PepperPix/Kiln/commit/4b7726392be99a830b59cc6497109e657e93ea53))
* **plugins:** validate plugin names and archive paths ([7b64ce4](https://github.com/PepperPix/Kiln/commit/7b64ce4ce27bfc93454555d06b557c6103f99441))
* run generated deploy workflows via dotnet tool run ([c354233](https://github.com/PepperPix/Kiln/commit/c354233ad1ac248d7534f0a430de605deec3d586))
* support base-url override and subfolder base-path for internal links ([11f9168](https://github.com/PepperPix/Kiln/commit/11f91681cd6d7790f2f384e7671b6bcc7c095433))


### Features

* add content plugin shortcodes ([#25](https://github.com/PepperPix/Kiln/issues/25)) ([f224eb1](https://github.com/PepperPix/Kiln/commit/f224eb1fd2f86b21a395a14dc85153d344e83b4d))
* add noIndex front matter field with guaranteed meta injection ([#27](https://github.com/PepperPix/Kiln/issues/27)) ([680eb87](https://github.com/PepperPix/Kiln/commit/680eb870c23d7a480cf90d22b88202435035b474))
* **cli:** report live progress during kiln build ([d7f5fc7](https://github.com/PepperPix/Kiln/commit/d7f5fc7fc3780c882578f2c7489d4e4641ded046))
* **core:** extract shared asset reference index service ([a7b0a05](https://github.com/PepperPix/Kiln/commit/a7b0a05690575f7326340d192695dd32a910fee1))
* **core:** image optimization pipeline for content images (ADR-051) ([b3f8ce0](https://github.com/PepperPix/Kiln/commit/b3f8ce0fb10ffa2da673cd716e3b7cb15389b521))
* kiln plugin search|add|update|remove|list + official NuGet SDK client ([#26](https://github.com/PepperPix/Kiln/issues/26)) ([ea86769](https://github.com/PepperPix/Kiln/commit/ea86769d8a21f7d4bbb1e0694d91639dfbc386a6))


### Performance Improvements

* **core:** parallelize content file reads within a section ([24bca59](https://github.com/PepperPix/Kiln/commit/24bca5904ca92ced99afe5e883570b71e1a2e6ad))
* **core:** use O(1) slug index for cross-collection reference resolution ([a7d4139](https://github.com/PepperPix/Kiln/commit/a7d41396f763c344a0c46ef747b8e890fd3a0dfa))

# [1.2.0-beta.11](https://github.com/PepperPix/Kiln/compare/v1.2.0-beta.10...v1.2.0-beta.11) (2026-09-29)


### Bug Fixes

* **core:** confine dev server requests to output dir ([5f1022d](https://github.com/PepperPix/Kiln/commit/5f1022d4acf726a204d8e107c065c3789897d30a))
* **core:** reject unsafe output directories ([9453845](https://github.com/PepperPix/Kiln/commit/94538454d51b71f55770b3058efe4a09dcbc472d))
* **core:** write pagefind binary atomically ([c4435ec](https://github.com/PepperPix/Kiln/commit/c4435ec77d1faca2475d57ec1b92b14f01f612fa))
* **plugins:** validate plugin names and archive paths ([7b64ce4](https://github.com/PepperPix/Kiln/commit/7b64ce4ce27bfc93454555d06b557c6103f99441))

# [1.2.0-beta.10](https://github.com/PepperPix/Kiln/compare/v1.2.0-beta.9...v1.2.0-beta.10) (2026-09-29)


### Bug Fixes

* **cli:** reject unknown options ([b8bcafd](https://github.com/PepperPix/Kiln/commit/b8bcafd50fcf90e016ba2de92725d226bc5c621e))
* **core:** lift scriban loop and string limits for templates ([2cd733a](https://github.com/PepperPix/Kiln/commit/2cd733ac22a87bd5a915ae248511cf8f29799fa3))

# [1.2.0-beta.9](https://github.com/PepperPix/Kiln/compare/v1.2.0-beta.8...v1.2.0-beta.9) (2026-09-05)


### Features

* add noIndex front matter field with guaranteed meta injection ([#27](https://github.com/PepperPix/Kiln/issues/27)) ([680eb87](https://github.com/PepperPix/Kiln/commit/680eb870c23d7a480cf90d22b88202435035b474))

# [1.2.0-beta.8](https://github.com/PepperPix/Kiln/compare/v1.2.0-beta.7...v1.2.0-beta.8) (2026-09-05)


### Features

* kiln plugin search|add|update|remove|list + official NuGet SDK client ([#26](https://github.com/PepperPix/Kiln/issues/26)) ([ea86769](https://github.com/PepperPix/Kiln/commit/ea86769d8a21f7d4bbb1e0694d91639dfbc386a6))

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
