---
name: git-workflow
description: >
  Git conventions for MemSentinel. Auto-invoke when committing, branching,
  writing PR descriptions, or when discussing version control workflow.
---

# MemSentinel Git Workflow

## Branch Tree — Multi-Agent Strategy

The branch hierarchy mirrors the PRD structure. This allows multiple agents
to work in parallel without conflicts: one agent per task branch, never on
`main` or a phase branch directly.

```
main
├── phase/0-foundation          (complete — merged)
├── phase/1-plumbing
│   ├── task/1.1-shared-volume  (complete — merged into phase/1)
│   ├── task/1.2-process-namespace
│   ├── task/1.3-uds-client
│   └── task/1.4-proc-parser
├── phase/2-watchdog
│   ├── task/2.1-sliding-window
│   ├── task/2.2-multi-threshold
│   └── task/2.3-...
├── phase/3-analysis
│   └── task/3.x-...
└── phase/4-dashboard
    └── task/4.x-...
```

### Rules

- **`main`** — stable, always builds. Never commit directly. Only phase branches merge here.
- **`phase/N-<name>`** — cut from `main` when a phase begins. Receives task merges. Merges to `main` when all tasks in the phase are done and the Phase DoD is met.
- **`task/N.M-<name>`** — cut from its parent `phase/N-<name>`. One agent per task branch. Merges back into the phase branch (not main) when complete.

### Starting a Phase

```bash
git checkout main && git pull
git checkout -b phase/2-watchdog
git push -u origin phase/2-watchdog
```

### Starting a Task (always from the phase branch)

```bash
git checkout phase/1-plumbing
git pull origin phase/1-plumbing
git checkout -b task/1.2-process-namespace
git push -u origin task/1.2-process-namespace
```

### Finishing a Task (merge task → phase)

```bash
# On the task branch, after dotnet build passes:
git checkout phase/1-plumbing
git pull origin phase/1-plumbing
git merge --no-ff task/1.2-process-namespace -m "feat(collectors): merge task/1.2-process-namespace"
git push origin phase/1-plumbing
```

### Finishing a Phase (merge phase → main)

Only when all tasks in the phase are ✅ Done in `docs/prd.md`:

```bash
git checkout main
git pull
git merge --no-ff phase/1-plumbing -m "feat: merge phase/1-plumbing — Phase 1 complete"
git push origin main
```

### Parallel Agent Rules

- Each agent is assigned **exactly one task branch**. Never cross branches.
- Agents must `git pull` their phase branch before cutting a new task branch to pick up merged sibling tasks.
- If two tasks in the same phase touch the same file, sequence them — do not parallelize.
- `docs/prd.md` and `docs/current-task.md` are high-conflict files: only one agent should touch them at a time per phase.

---

## Branch Naming Reference

| Pattern | Purpose |
|---|---|
| `phase/N-<slug>` | Phase integration branch (e.g. `phase/2-watchdog`) |
| `task/N.M-<slug>` | Single task (e.g. `task/1.3-uds-client`) |
| `fix/<slug>` | Hotfix to `main` or a phase branch outside of normal task flow |
| `chore/<slug>` | Maintenance, deps, config not tied to a PRD task |

Slugs: lowercase, hyphen-separated, ≤ 30 chars.

---

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

---

## PR Description Template

```markdown
## Summary
- What changed and why (2-4 bullet points)
- PRD task: X.Y

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

---

## What to Never Commit

- `.env` files or any file containing secrets, connection strings, or API keys.
- `appsettings.Development.json` with real credentials.
- Build output: `bin/`, `obj/`, `dist/`, `.next/`.
- Snapshot artifacts: `*.gcdump`, `*.dmp`.
- Personal IDE config: `.vs/`, `.idea/`, `.vscode/settings.json` (user-specific only).
- `CLAUDE.local.md` (already gitignored).
- `.claude/memory/` (already gitignored).

---

## PRD Update Obligation

After every completed task:
1. Open `docs/prd.md`.
2. Change the task from `⬜ Pending` to `✅ Done`.
3. If all tasks in a phase are complete, mark the Phase as `✅ Complete`.
4. Include this change in the same commit as the work, or as an immediate follow-up commit.
