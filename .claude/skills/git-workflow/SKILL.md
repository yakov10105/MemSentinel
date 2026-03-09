---
name: git-workflow
description: >
  Git conventions for MemSentinel. Auto-invoke when committing, branching,
  writing PR descriptions, or when discussing version control workflow.
---

# MemSentinel Git Workflow

## Branch Naming

```
feature/<short-description>     # New functionality
fix/<short-description>         # Bug fix
chore/<short-description>       # Maintenance, deps, config
docs/<short-description>        # Documentation only
perf/<short-description>        # Performance improvements
```

Examples:
- `feature/heap-diff-engine`
- `fix/snapshot-upload-retry`
- `chore/update-clrmd-version`
- `perf/arraypool-receive-buffer`

## Commit Message Format (Conventional Commits)

```
<type>(<scope>): <short summary in imperative mood>

[optional body — explain WHY, not what]

[optional footer: BREAKING CHANGE, closes #issue]
```

**Types:** `feat`, `fix`, `perf`, `refactor`, `test`, `docs`, `chore`, `ci`

**Scopes:** `agent`, `core`, `dashboard`, `storage`, `analysis`, `collectors`, `api`, `tests`

Examples:
```
feat(analysis): add root chain path-to-root analyzer

fix(collectors): handle missing /proc/[pid]/status gracefully

perf(analysis): use ArrayPool for heap walk buffers

test(core): add concurrency tests for SnapshotRegistry

chore(deps): upgrade ClrMD to 3.1.x
```

Rules:
- Summary line: 72 characters max, imperative mood ("add" not "added").
- Body explains the *why*, not the *what*. The diff shows the what.
- Reference PRD task IDs in the body when applicable.

## PR Description Template

```markdown
## Summary
- What changed and why (2-4 bullet points)
- Link to PRD task if applicable

## Changes
- `MemSentinel.Core/Analysis/` — ...
- `MemSentinel.Agent/Features/` — ...

## Test Plan
- [ ] Unit tests pass (`dotnet test`)
- [ ] New tests cover the changed code paths
- [ ] Manual test: [describe scenario]

## PRD Status
- Task X.Y: ⬜ Pending → ✅ Done
```

## What to Never Commit

- `.env` files or any file containing secrets, connection strings, or API keys.
- `appsettings.Development.json` with real credentials.
- Build output: `bin/`, `obj/`, `dist/`, `.next/`.
- Snapshot artifacts: `*.gcdump`, `*.dmp`.
- Personal IDE config: `.vs/`, `.idea/`, `.vscode/settings.json` (user-specific only).
- `CLAUDE.local.md` (already gitignored).
- `.claude/memory/` (already gitignored).

## PRD Update Obligation

After every completed task:
1. Open `docs/prd.md`.
2. Change the task from `⬜ Pending` to `✅ Done`.
3. If all tasks in a phase are complete, mark the Phase as `✅ Complete`.
4. Include this change in the same commit as the work, or as an immediate follow-up commit.
