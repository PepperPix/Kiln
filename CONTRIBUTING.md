# Contributing to Kiln

Thanks for your interest in contributing. This document covers the conventions and checks a
change is expected to satisfy before it can be merged.

## Commit Messages

This repository uses [semantic-release](https://semantic-release.gitbook.io/) to determine version
bumps and changelog entries from commit messages. All commits on `main`/`beta` must follow
[Conventional Commits](https://www.conventionalcommits.org):

```
type(scope): short, imperative description
```

- `fix: ...` — patch release (`0.1.0` → `0.1.1`)
- `feat: ...` — minor release (`0.1.0` → `0.2.0`)
- `feat!: ...` or a `BREAKING CHANGE:` footer — major release
- `chore:`, `docs:`, `refactor:`, `test:`, `build:`, `ci:` — no release bump

`commitlint` and `husky` enforce this locally via a commit-msg hook; commits that do not match the
format will be rejected.

## Building and Testing

```bash
dotnet build
dotnet test
```

The build treats warnings as errors (`TreatWarningsAsErrors`), so a change with build warnings will
fail CI. All tests must pass, including any new tests added for the change.

## Code Quality

Before opening a pull request, the codebase is expected to be clean under static analysis
(InspectCode). Please fix any findings reported for files you touch rather than suppressing them,
unless there is a clear, documented reason not to (leave a short inline comment explaining why).

## Dependency Lockfiles

This repository uses `RestorePackagesWithLockFile`. If your change adds or updates a NuGet package
reference, regenerate and commit the corresponding `packages.lock.json` file(s):

```bash
dotnet restore --force-evaluate
```

## Pull Requests

- Keep changes focused; prefer several small, reviewable commits over one large one.
- Describe the change and its motivation in the pull request description.
- Ensure `dotnet build` and `dotnet test` pass locally before requesting review.

## Branching and Merging

This repository uses a three-tier branch model:

```
feature/* → beta → main
```

- **Feature branches** are where all new work happens. Open a pull request against `beta`.
- **`beta`** is the integration/prerelease branch (semantic-release publishes `x.y.z-beta.N`
  prerelease packages from it). Feature PRs into `beta` are merged via **squash or rebase** — both
  keep history linear, which is enforced by branch protection (`Require linear history` on `beta`).
- **`main`** is the stable release branch. Once `beta` is in a good state, open a pull request from
  `beta` into `main` and merge it as a **regular merge commit** (the "Merge pull request" button —
  not squash, not rebase). This is intentional, not an oversight: `main` accumulates its own
  release-only commits from semantic-release (changelog/version bump) that `beta` never sees, so a
  plain fast-forward or rebase would eventually be blocked by that divergence. A merge commit
  reconciles it automatically and does not affect semantic-release's version calculation, since it
  walks the full commit ancestry (not just first-parent) and still sees every individual commit's
  type. Squashing the `beta → main` PR is a no-go: it would collapse multiple typed commits into one
  message and could produce a wrong or missing version bump.
- Both `main` and `beta` require a pull request and passing CI status checks before merging — direct
  pushes are blocked for everyone except the release automation identity, which needs to push its own
  version-bump/changelog commits directly to `main`.
- By convention (not GitHub-enforced), `main` should only ever receive pull requests from `beta`.

