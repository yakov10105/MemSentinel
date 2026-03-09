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
2. Read @docs/prd.md and confirm understanding
3. Read existing project structure
4. Load relevant skills based on layers touched
5. Write the plan to docs/current-task.md and wait for approval
