# Settings Notes

Explains every decision in `.claude/settings.json`.

## Permissions — Allow List

| Command | Why Auto-Approved |
|---|---|
| `dotnet build*` | Core development loop. Safe read-only compilation. |
| `dotnet test*` | Running tests is always safe. Wildcards allow `--filter`, `--collect`, etc. |
| `dotnet run*` | Needed to start the Agent locally. Wildcards allow `--project`, `--configuration`. |
| `dotnet restore` | Downloads NuGet packages. Safe, no side effects on repo. |
| `dotnet format*` | Code formatting. Only modifies whitespace/style, never logic. |
| `dotnet publish*` | Building release artifacts. Safe locally; deploy step is separate. |
| `git status` | Read-only. Always safe. |
| `git diff*` | Read-only. Wildcards allow `--staged`, `HEAD~1`, etc. |
| `git log*` | Read-only history viewing. |
| `git add*` | Staging files is reversible (`git restore --staged`). |
| `git commit*` | Creating commits is local and reversible (`git reset HEAD~1`). |
| `git branch*` | Listing/creating branches is low-risk. |
| `git checkout*` | Switching branches. Confirm before using if there are unsaved changes. |
| `git stash*` | Temporarily saving work. Reversible. |
| `cat *` | Reading files. Always safe. |
| `ls *` | Listing directories. Always safe. |
| `find *` | Finding files. Read-only. |
| `grep *` | Searching content. Read-only. |

## Permissions — Deny List

| Command | Risk Prevented |
|---|---|
| `rm -rf *` | Irreversible mass deletion. Any file removal should be reviewed explicitly. |
| `curl *` | Prevents exfiltration of secrets or downloading untrusted scripts. |
| `wget *` | Same as curl — blocked for the same reasons. |
| `git push --force*` | Force push can overwrite remote history permanently. Require explicit user action. |
| `dotnet nuget push*` | Publishing packages to NuGet is irreversible and public. Must be intentional. |
| `kubectl delete*` | Deleting cluster resources (pods, deployments) could disrupt running services. |
| `docker rm*` | Removing containers could delete data volumes or running services. |
| `docker rmi*` | Removing images is disruptive if they're in use in a build pipeline. |

## Hooks

### PostToolUse — Auto Build Check

**Trigger:** Every time Claude edits or writes a `.cs` or other file.
**Command:** `dotnet build --no-restore 2>&1 | tail -5`
**Why:** Catches compile errors immediately after each edit, before they accumulate. `--no-restore` skips package restore for speed. `tail -5` keeps output concise — shows only the summary lines.
**Trade-off:** Adds ~2-5 seconds per file edit. Worth it to catch type errors and broken references immediately.

### Stop — Session End Reminder

**Trigger:** When Claude finishes a session (conversation ends or `/exit` is called).
**Command:** Echo a reminder to use `/clear`.
**Why:** Claude Code's context window accumulates conversation history. Starting a new unrelated task without clearing context causes irrelevant history to consume tokens and potentially confuse Claude. This reminder enforces good hygiene.

## Environment Variables

| Variable | Value | Why |
|---|---|---|
| `DOTNET_CLI_TELEMETRY_OPTOUT` | `1` | Disables Microsoft's .NET CLI telemetry. No phone-home during builds. |
| `DOTNET_NOLOGO` | `1` | Suppresses the .NET welcome banner in CLI output. Keeps build logs clean. |
