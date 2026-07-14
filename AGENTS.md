# AGENTS.md

Instructions for AI coding agents collaborating on the Hokai project.

---

## 1. Memory Bank

The memory bank helps agents maintain context across sessions in a growing codebase.

### Location

```
.agents/
├── productContext.md    # Why the project exists, problems solved, user personas
├── activeContext.md     # Current focus, recent changes, next steps, blockers
├── systemPatterns.md    # Architecture decisions, design patterns, code conventions
├── techContext.md       # Technologies, dev setup, technical constraints
└── progress.md          # What works, what's left, known issues
```

### Agent Behavior

**First interaction**: Create `.agents/` with all 5 files populated from `README.md`, `.docs/`, and source code. Each file must be concise (≤200 lines). Use bullet points and short paragraphs. Date-stamp entries in `activeContext.md` and `progress.md`.

**Subsequent interactions**:
1. Read all 5 memory bank files at session start
2. Update `activeContext.md` and `progress.md` as work advances
3. Keep other files current when architecture decisions or patterns change
4. Reference `systemPatterns.md` before proposing architectural changes
5. Reference `techContext.md` before introducing new dependencies or tools

---

## 2. Design Documents

Design docs live in `.docs/` and serve as the single source of truth for architecture, decisions, and workflows.

### Structure

```
.docs/
├── architecture.md      # Application design, data model, services
├── daemonization.md     # OS service integration (systemd, launchd, Windows)
├── installation.md      # Install methods, scripts, Docker
├── pt-BR/               # Portuguese translations
│   ├── architecture.md
│   ├── daemonization.md
│   └── installation.md
```

### Rules

**Code must align with docs** — every code change MUST be consistent with the existing design documents. If a change introduces a new pattern, a new component, or deviates from the documented architecture, the relevant `.docs/` file MUST be updated in the same PR.

**New topics get new files** — when exploring a new domain (e.g. authentication, metrics, plugins), create a new doc file in `.docs/`. Do not append unrelated content to existing documents.

**Avoid duplication** — prefer linking to another doc section over rewriting content. If a concept is already explained in `architecture.md`, reference it via `[Architecture](.docs/architecture.md#section)` instead of restating it.

**Write in all supported languages** — the default documentation is English at `.docs/*.md`. Every new or updated document MUST also be translated into Portuguese at `.docs/pt-BR/`. Use the same structure, file names, and section anchors so cross-doc links remain valid across locales.

**Community standards** — documents should use clear section headers, tables for structured data, code blocks for examples, and a `Future Improvements` checklist for planned work. Keep paragraphs short and scannable.

---

## 3. Version Control

### Conventional Commits

All commits MUST follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/).

**Format**: `<type>[optional scope]: <description>`

**Allowed types**:

| Type | Usage |
|---|---|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Formatting, whitespace (no code change) |
| `refactor` | Code restructuring without bug fixes or features |
| `perf` | Performance improvement |
| `test` | Adding or correcting tests |
| `build` | Build system or dependencies |
| `ci` | CI/CD pipeline changes |
| `chore` | Maintenance (version bumps, tooling) |
| `revert` | Revert a previous commit |

**Scopes**: `cli`, `daemon`, `storage`, `notifications`, `build`, `docs`

**Examples**:
```
feat(cli): add endpoint add command
fix(daemon): handle periodic timer overflow
docs: update installation instructions for Windows
refactor(storage): use pooled JSON serializer context
```

### Branching Strategy

| Branch | Purpose | Direct Push | Merges from |
|---|---|---|---|
| `main` | Stable releases only | ❌ Locked in GitHub | `dev` via PR |
| `dev` | Default upstream, integration | ❌ Locked in GitHub | Feature branches via PR |

Feature branches: `feat/<short-description>`, `fix/<short-description>`, `docs/<short-description>`, `refactor/<short-description>` — created from and merged back into `dev`.

### Worktree Isolation

Every change — code, tests, documentation, configuration, or tooling — MUST be developed in a dedicated git worktree. The primary checkout is for coordination only and MUST NOT be used for implementation.

- Use one worktree, one branch, and one task per agent. Never attach the same branch to multiple worktrees.
- Create the branch from its intended merge target, normally `dev`, and use the existing branch naming conventions.
- Run `git worktree list` and inspect the target branch before creating a worktree to avoid path and branch collisions.
- Keep the worktree outside another repository worktree. Place all worktrees under `../hokai-worktrees/<task-slug>/`.
- Agents MUST modify, test, commit, and push only from their assigned worktree. Do not edit another agent's worktree or move uncommitted changes between worktrees.
- After a branch is merged or abandoned, remove its worktree with `git worktree remove <path>` and prune stale metadata with `git worktree prune` when necessary.

Example:

```bash
git fetch origin
git worktree add ../hokai-worktrees/feat-endpoint-store -b feat/endpoint-store origin/dev
```

#### VS Code Multi-Root Workspace

A multi-root workspace file at `../hokai.code-workspace` aggregates the coordination checkout and active worktrees so they appear together in the Explorer and Source Control views.

- Open the project with `code ../hokai.code-workspace` as the single entry point.
- The `hokai-worktrees` folder is a **permanent root** in the workspace. Every worktree created directly under it is discovered automatically — no `code --add` needed.
- New worktrees appear in the Explorer and Source Control views as soon as they are created. Removing a worktree with `git worktree remove` cleans up both Explorer and Git views automatically.
- Do not edit the `.code-workspace` JSON file manually while it is open. Use `File > Add Folder to Workspace...` or `code --add` if you need to add a single-purpose root temporarily.
- The workspace file is personal infrastructure and lives outside the Git repository. It must not be committed.
- To initially add the aggregator root to an active window without reloading:

```bash
code --add "../hokai-worktrees"
```

#### Conflict Resolution

Merge and rebase conflicts MUST be resolved semantically, not mechanically.

- Understand the intent of the target branch and the expected behavior of the feature before editing conflict markers.
- Preserve existing target-branch functionality while retaining the feature's required behavior. Integrate both sides when they are compatible.
- Do not accept `ours` or `theirs` wholesale unless inspection proves that the discarded side is obsolete.
- Preserve relevant tests, documentation, configuration, and public contracts from both sides.
- Add or update regression tests when a conflict affects behavior, then run the impacted tests and the full suite before pushing.

#### Reintegration and Cleanup

Changes made in a worktree are not automatically integrated into the target branch. Complete the following lifecycle before removing a worktree:

1. Validate the change and commit it on the worktree's feature branch using Conventional Commits.
2. Synchronize the feature branch with the latest target branch and resolve conflicts according to the semantic conflict-resolution rules above.
3. Push the feature branch and merge it through a pull request when the target branch is protected or a remote repository is involved.
4. For explicitly requested local-only integration, return to the clean coordination checkout and fast-forward or squash the feature branch into its target. Never copy changed files between worktrees manually.
5. Run the relevant verification on the integrated target branch and confirm the working tree is clean.
6. Remove the worktree and delete its local feature branch after the merge is confirmed:

```bash
git worktree remove ../hokai-worktrees/<task-slug>
git branch -d <branch>
git worktree prune
```

Example for explicitly requested local integration:

```bash
# In the task worktree
git add <files>
git commit -m "docs: update agent collaboration rules"
git rebase dev

# In the coordination checkout
git switch dev
git merge --ff-only docs/agent-collaboration
git worktree remove ../hokai-worktrees/agent-collaboration
git branch -d docs/agent-collaboration
```

### Pull Requests

PRs MUST use `.github/PULL_REQUEST_TEMPLATE.md` and include every section.

**LOC limit**: ≤400 lines changed per PR. Exceptions (must be justified in the description):
- Scaffold / initial commit
- Generated code
- Lockfile updates
- Bulk renames

*Rationale: Research (SmartBear, Cisco) shows reviewer effectiveness drops significantly beyond 200-400 LOC.*

#### Labels

Every PR MUST have at least one type label and MAY have component labels.

| Category | Labels |
|---|---|
| **Required — type** | `type/feature`, `type/bugfix`, `type/docs`, `type/refactor`, `type/chore`, `type/breaking` |
| **Optional — component** | `component/cli`, `component/daemon`, `component/storage`, `component/notifications`, `component/build` |
| **Optional — priority** | `priority/high`, `priority/medium`, `priority/low` |

#### Merge Strategy

**Squash and merge** is the default for every PR. The squashed commit message must follow Conventional Commits format.

#### Draft PR Queue for Multi-Phase Releases

When a release spans multiple atomic phases, each phase becomes a **draft PR targeting `dev`**. The queue ensures every phase is independently validated, reviewed, and merged before the next phase begins.

**Queue workflow:**

1. Create the release milestone on GitHub (e.g. `v0.2.0-alpha.1`).
2. Attach every phase PR to the milestone.
3. Implement each phase in its own worktree and feature branch.
4. Push the branch and open a **draft PR** targeting `dev` using the PR template.
5. Mark the PR as ready for review only after all acceptance criteria are met.
6. Merge phases sequentially into `dev` in dependency order.
7. After a phase is merged, rebase the next unmerged phase onto the updated `dev`.
8. Keep `main` untouched until every release phase is complete.
9. When all phases are merged and `dev` is green, integrate `dev` into `main` and tag the release.

**Draft PR rules:**

- Each draft PR stays under the 400-line change limit.
- The PR description must list the phase number, dependencies, and acceptance criteria.
- All dependent PRs must reference the same GitHub milestone.
- Draft PRs may be opened with failing tests (RED phase of TDD).
- A PR must not be marked ready for review until its CI is green.
- Only the reviewer (not the agent) merges the PR.

### Workflow Summary

```
main (stable, locked in GitHub)
  ↑ squash merge PR
dev (integration, locked in GitHub)
  ↑ squash merge PR
feat/<description> (feature branch)
```

### Release and Versioning

**Every commit on `main` must belong to a tagged release.** There are no orphan commits on `main` — every push to `main` must be accompanied by a semver tag (e.g. `v0.1.0`, `v0.1.0-rc.1`) and a GitHub Release with a user-facing description.

**Default integration target is `dev`.** Unless the user explicitly requests a release or the active context/plan states that a release is in progress, all feature branches, fixes, and documentation changes must be merged into `dev` only. Do not merge or push to `main` outside of the release flow.

**Release flow:**

1. Accumulate changes on `dev` through feature branches and PRs
2. When ready to cut a release, integrate the relevant commits into `main`
3. Create an annotated semver tag on the `main` integration commit
4. Push `main` and the tag together — the release workflow creates a draft
5. Edit the draft with a description following the established pattern (features, known limitations, per-platform installation, CLI reference, configuration, verification, documentation links)
6. Publish the release — this triggers Docker image publication

**Tag format**: `v<major>.<minor>.<patch>[-<prerelease>]` (e.g. `v0.1.0`, `v0.1.0-rc.2`, `v0.2.0-alpha.1`)

**Stable vs pre-release**:

| Type | Tag example | Moves `latest` Docker tag? |
|---|---|---|
| Stable | `v1.0.0` | Yes |
| Pre-release (rc, alpha, beta) | `v0.1.0-rc.1` | No |

**Release descriptions** must include: what's new since the previous release, features summary, known limitations, per-platform installation instructions, CLI reference, configuration reference, verification command, and documentation links. Follow the pattern established in previous releases.

---

## 4. Code Quality

### Code Comment Standards

Comments are part of the implementation and MUST be reviewed with the code. When writing or changing code, actively identify places where a concise comment improves maintainability.

- Explain intent, invariants, constraints, tradeoffs, concurrency behavior, platform differences, security considerations, and non-obvious error handling.
- Prefer comments that explain **why** a decision exists. Do not narrate syntax, assignments, or control flow that is already clear from the code.
- Add XML documentation to public APIs when their contract, side effects, exceptions, units, nullability, or ownership semantics are not obvious from names and types.
- Place comments immediately above the code they describe and keep them synchronized when behavior changes. Remove stale or misleading comments.
- Use concise, factual, professional English consistent with established open-source projects.
- Do not use emoji, decorative banners, conversational notes, author signatures, or commented-out code.
- TODO comments MUST describe a concrete action and reference a tracked issue when one exists.

---

## 5. Testing

### TDD Standard

Every code change MUST follow **Red-Green-Refactor**:

1. **Red** — Write a failing test that describes the expected behavior *before* writing any implementation code
2. **Green** — Write the minimum implementation code to make the test pass
3. **Refactor** — Clean up the code (both test and implementation) while keeping tests green

This applies to all changes: new features, bug fixes, and refactors.

### Test Project

Tests live in `tests/Hokai.Tests/` using **xUnit** (recommended — Microsoft-owned, industry standard for .NET).

```
hokai/
├── src/Hokai/
│   └── ...
└── tests/Hokai.Tests/
    ├── Hokai.Tests.csproj      # <PackageReference xUnit + coverlet>
    ├── Services/
    │   ├── HealthCheckServiceTests.cs
    │   ├── NotificationServiceTests.cs
    │   ├── EndpointStoreTests.cs
    │   └── CheckStoreTests.cs
    └── Commands/
        └── EndpointCommandsTests.cs
```

- Use `Microsoft.NET.Test.Sdk` + `xunit` + `xunit.runner.visualstudio`
- Use `coverlet.collector` for code coverage
- No mocking framework by default — prefer fakes/stubs. If mocking is required, use the built-in `Microsoft.Extensions.DependencyInjection` container to inject test doubles.

### What to Test

| Layer | What | Examples |
|---|---|---|
| **Stores** | File I/O, serialization, edge cases | EndpointStore.Add persists JSON; CheckStore.GetUptime handles empty window |
| **Services** | Business logic, state machines, transitions | MonitorService detects UP→DOWN transition; HealthCheckService maps HTTP status codes |
| **Commands** | CLI parsing, validation, output formatting | `endpoint add` with invalid URL returns error; `service status` prints correct table |
| **Notifications** | Email formatting, SMTP error handling | NotifyDownAsync builds correct subject; SmtpClient failure does not crash service |
| **ServiceManager** | Platform abstraction, shell execution, idempotency | InstallAsync copies binary; UninstallAsync removes files; second run is no-op |

### Regression Analysis

For every PR, the agent MUST:

1. **Identify affected tests** — review the change and determine which existing tests could be impacted
2. **Run the full suite** — execute `dotnet test` before merging
3. **Evaluate coverage impact** — run `dotnet test --collect:"XPlat Code Coverage"` and review uncovered paths
4. **Check for new edge cases** — if the change introduces a new branch, condition, or error path, there must be a test covering it

If a change lacks test coverage for new or modified logic, **comment on the PR** requesting the missing tests before proceeding with merge.

### Test Quality Rules

| Rule | Description |
|---|---|
| **Arrange-Act-Assert** | Every test follows AAA structure with blank line separation |
| **One assertion per logical behavior** | A test verifies one concept — multiple assertions on the same object are acceptable if they validate the same behavior |
| **No test interdependency** | Tests must run in any order. Never share mutable state between tests |
| **Meaningful names** | `AddEndpoint_PersistsToJsonFile` not `Test1` |
| **Avoid test logic** | No `if`, `switch`, `for` in tests — use parameterized theories (`[Theory]` / `[InlineData]`) instead |

### TDD in the PR Workflow

```
1. Feature request / bug report
2. Write failing test(s)                      ← RED
3. Push branch, open draft PR with failing CI ← RED CI
4. Write implementation                        ← GREEN
5. Push, CI passes                             ← GREEN CI
6. Refactor, CI stays green                    ← REFACTOR
7. Mark PR ready for review
```

Draft PRs with failing tests are encouraged — they communicate intent before implementation exists.

### Dependency in PR Description

Any PR that adds tests MUST mention the testing approach in the description:

```markdown
## Testing

- [x] Unit tests for new HealthCheckService timeout logic
- [x] Manual test: endpoint timeout returns check result with error
- [x] Regression: existing EndpointStoreTests pass
- [x] Code coverage: 92% on changed files
```

---

## 6. Security & Secrets

### Pre-Merge Check

Before merging ANY PR, the agent MUST scan every changed file for:

| What to look for | Examples |
|---|---|
| API keys / tokens | Strings matching `[a-zA-Z0-9_\-]{20,}` near `key`, `secret`, `token`, `password` |
| SMTP credentials | Real `Username` or `Password` values in `appsettings.json` (not placeholders) |
| Connection strings | Hardcoded `Server=...;User Id=...;Password=...` |
| Private keys | Files containing `-----BEGIN.*PRIVATE KEY-----` |

**If any potential secret is found** → **BLOCK THE MERGE**:
1. Comment on the PR explaining exactly what was found and in which file
2. Suggest the appropriate fix:
   - Environment variables
   - .NET User Secrets
   - Placeholder values for config templates
3. Do not proceed with the merge until the secret is removed

**Additional checks**:
- `appsettings.Development.json` must NOT be tracked by git (must be in `.gitignore`)
- No file should contain real credentials — only template/placeholder values are allowed in committed files

---

## 7. Dependency Policy

### Priority Order

```
1. System.* / Microsoft.Extensions.*  (built-in SDK — no NuGet needed)
2. Microsoft NuGet packages             (first-party, e.g. System.CommandLine)
3. Third-party NuGet packages           (last resort only)
```

### Pre-Merge Dependency Review

Before merging a PR that adds a NuGet package, the agent MUST:

1. Verify whether the functionality can be implemented with built-in `System.*` or `Microsoft.Extensions.*` APIs
2. If not built-in, check whether a Microsoft-owned NuGet package fills the gap
3. If a third-party package is proposed, **comment on the PR** requesting:
   - Why no built-in or Microsoft alternative suffices
   - What specific functionality the new package provides
   - Whether a lighter-weight alternative exists

### Approved Dependencies

| Package | Status | Justification |
|---|---|---|
| `System.CommandLine` | Approved | CLI parsing; no built-in equivalent |
| `Microsoft.Extensions.Http` | Approved | `IHttpClientFactory` lifecycle and connection pooling |
| `Microsoft.Extensions.Hosting.Systemd` | Approved | systemd lifecycle; no built-in equivalent |
| `Microsoft.Extensions.Hosting.WindowsServices` | Approved | Windows Service lifecycle; no built-in equivalent |

**Any addition beyond these 4 packages is exceptional** and must be explicitly justified in the PR description under a "Dependencies Added" section.

### Dependency Section in PR Description

```markdown
## Dependencies Added

| Package | Version | Justification |
|---|---|---|
| `Example.Package` | 1.2.3 | Required because... |

## Why Not Built-in?
- `System.Something` lacks feature X
- No Microsoft package provides Y
```
