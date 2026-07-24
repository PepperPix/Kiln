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

### File Organization

Every `.cs` file must contain exactly one top-level type (class, record, struct, interface, or
enum). Nested types are allowed only as a deliberate encapsulation aid (for example,
Spectre.Console.Cli `CommandSettings`). If a file currently contains multiple top-level types,
split them into separate files rather than introducing new nested types.

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
  **Pick squash vs. rebase based on the branch's commit history, not by default:**
  - **Squash** when the branch is a single logical change (even if split into several WIP commits
    like "wip", "fix typo", "address review") — semantic-release only sees the squash commit's own
    message, so this is safe as long as one changelog entry is actually correct for the whole
    branch.
  - **Rebase** when the branch contains multiple commits that each deserve their own changelog
    entry (e.g. an independent `fix:` alongside a `feat:`, or several unrelated `feat:`/`fix:`
    commits bundled in one PR) — squashing would collapse them into a single message and
    semantic-release would only pick up one type/bump instead of several. Rebase preserves each
    commit's own message, so semantic-release analyzes them individually.
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

### Keeping beta in sync with main

Every stable release adds a `chore(release): x.y.z [skip ci]` commit (and tag) to `main` that
`beta` never sees. If that commit never makes it back into `beta`'s history, semantic-release has
no way of knowing a stable release already happened when it next runs on `beta` — it just keeps
incrementing the prerelease counter off of `beta`'s own last tag (e.g. `1.1.0-beta.3` →
`1.1.0-beta.4`). Per semver, `1.1.0-beta.4` still sorts **below** `1.1.0`, even though it was
published later and contains newer commits — so NuGet (and anything else resolving "latest") keeps
recommending the older stable release over the newer prerelease. This isn't a NuGet bug, it's a
missing merge; this exact scenario happened once (`v1.1.0-beta.2`/`v1.1.0-beta.3` were both cut
without `main`'s `v1.1.0` release commit ever being merged back into `beta`).

To prevent it, the [release workflow](.github/workflows/release.yml) fast-forwards `beta` to
`main`'s tip immediately after every `main` release ("Sync main back into beta" step). This only
works as a plain fast-forward (no merge commit, so it doesn't trip `beta`'s "require linear
history" rule) as long as `beta` has no commits ahead of the release that was just promoted, which
is the normal case right after a `beta → main` promotion.

If new commits already landed on `beta` before the sync step runs, the fast-forward is skipped and
CI logs a `::warning::` instead of failing the release. In that case, sync manually:

```bash
git checkout beta
git pull
git merge origin/main
```

This produces a real merge commit, which violates `beta`'s linear-history branch protection, so it
must be pushed by (or bypassed for) someone with bypass rights — this should be rare, not routine.
Resolve any conflicts in `CHANGELOG.md`/`Directory.Build.props` by keeping `beta`'s side (`git
checkout --ours CHANGELOG.md Directory.Build.props`): semantic-release recomputes both from git
tags on the next release regardless of what's currently in those files, so `beta`'s more advanced
prerelease content is the more accurate one to keep in the meantime.

