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

## During Implementation

- Check off `[ ]` → `[x]` in `docs/current-task.md` as each step completes.
- Run build after each step — stop and report if it fails.

---

## Finishing a Task

When all steps are done:

1. Update `docs/prd.md` task status to `✅ Done`.
2. Run final build and confirm 0 warnings, 0 errors.
