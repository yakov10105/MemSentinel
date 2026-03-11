---
name: task
user-invocable: true
disable-model-invocation: true
description: >
  Structured start for a new development task. Only runs when user types /task.
---

# /task — Start a New Development Task

The user wants to begin a new task from @docs/prd.md.

Follow the Task Workflow defined in CLAUDE.md exactly:

1. Ask the user which task they want to work on if not specified
2. Read @docs/prd.md and confirm understanding of scope, layers, and acceptance criteria
3. Read existing project structure — understand what already exists before proposing anything
4. Load relevant skills based on layers touched
5. Write the plan to docs/current-task.md with `[ ]` checkboxes and wait for approval

**Never start coding until the user explicitly says "go" or "approved".**

---

## Branch Setup (MANDATORY before coding)

After the user approves the plan, the FIRST action before writing any code is to
create and push the task branch. Follow the branch tree from the git-workflow skill.

### Step A — Ensure the phase branch exists

```bash
# Check if the phase branch exists locally or remotely
git branch -a | grep phase/N-<name>
```

If it does not exist, create it from main:

```bash
git checkout main && git pull
git checkout -b phase/N-<name>
git push -u origin phase/N-<name>
```

If it already exists, pull latest:

```bash
git checkout phase/N-<name>
git pull origin phase/N-<name>
```

### Step B — Create the task branch from the phase branch

```bash
git checkout -b task/N.M-<slug>
git push -u origin task/N.M-<slug>
```

Branch naming: `task/N.M-<slug>` where slug is lowercase, hyphen-separated,
≤ 30 chars. Examples: `task/1.2-process-namespace`, `task/2.1-sliding-window`.

### Step C — Confirm to the user

Report the active branch before writing any code:
> "On branch `task/1.2-process-namespace`. Starting implementation."

---

## During Implementation

- Check off `[ ]` → `[x]` in `docs/current-task.md` as each step completes.
- Run `dotnet build` after each step — stop and report if it fails.
- All commits go to the task branch only.

---

## Finishing a Task

When all steps are done:

1. Update `docs/prd.md` task status to `✅ Done`.
2. Run final `dotnet build` and confirm 0 warnings, 0 errors.
3. Commit all changes to the task branch.
4. Merge the task branch back into its parent phase branch:

```bash
git checkout phase/N-<name>
git pull origin phase/N-<name>
git merge --no-ff task/N.M-<slug> -m "feat(<scope>): merge task/N.M-<slug>"
git push origin phase/N-<name>
```

5. Report the merge to the user and suggest the next task.
