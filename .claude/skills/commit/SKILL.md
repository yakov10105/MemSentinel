---
name: commit
invocation: explicit
description: >
  Review staged changes and create a conventional commit message.
  Only runs when the user types /commit.
---

# /commit — Review and Commit

## Step 1: See What's Staged

```bash
git diff --staged
git status
```

## Step 2: Check for Prohibited Files

Verify the staged set does NOT include:
- `.env` or any secrets file
- `appsettings.Development.json` with real credentials
- `bin/`, `obj/`, `*.gcdump`, `*.dmp`
- `CLAUDE.local.md`

If any prohibited files are staged, alert the user and stop.

## Step 3: Draft Commit Message

Use the Conventional Commits format:

```
<type>(<scope>): <short summary, imperative mood, max 72 chars>

<optional body — explain WHY, not what; reference PRD task if applicable>
```

**Types:** `feat`, `fix`, `perf`, `refactor`, `test`, `docs`, `chore`, `ci`
**Scopes:** `agent`, `core`, `dashboard`, `storage`, `analysis`, `collectors`, `api`, `tests`

Suggest a message based on the diff content.

## Step 4: Confirm

Present the draft message to the user:

```
Proposed commit:

  feat(analysis): add HeapDiffEngine with type-level delta calculation

  Implements the core diff logic comparing two ClrMD heap snapshots.
  Returns top N leaking types ranked by object count growth velocity.
  PRD: Phase 2, Task 2.1

Proceed? (yes / edit message / cancel)
```

Wait for confirmation before running `git commit`.

## Step 5: Commit

Only after user confirms:

```bash
git commit -m "<confirmed message>"
```

## Step 6: PRD Reminder

After committing, ask: "Was `docs/prd.md` updated to mark this task complete?"

If not, do it now before moving on.
