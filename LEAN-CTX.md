<!-- lean-ctx-owned: PROJECT-LEAN-CTX.md v1 -->
# lean-ctx — Context Engineering Layer
<!-- lean-ctx-rules-v11 -->

## Tool Mapping (MANDATORY — use instead of native equivalents)
| Instead of | Use | Example |
|------------|-----|---------|
| Read/cat/head/tail | `ctx_read(path, mode)` | `ctx_read("src/main.rs", "full")` |
| Grep/rg/find | `ctx_search(pattern, path)` | `ctx_search("fn handle", "src/")` |
| Shell/bash | `ctx_shell(command)` | `ctx_shell("cargo test")` |
| Edit (when Read unavailable) | `ctx_edit(path, old, new)` | `ctx_edit("f.rs", "old", "new")` |

## Tool Bypass
If you cannot read an image file that is sent in the current request, you may bypass lean-ctx to read the image. Return back to tool mapping after retrieval.

## ctx_read Mode Selection
| Goal | Mode | When |
|------|------|------|
| Edit this file | `full` | Before any edit ( ONLY IF NOT PROVIDED ENOUGH CONTEXT VIA SIGNATURES + LINES COMBINATION ) |
| Understand API | `signatures` | Context-only, won't edit |
| Re-read after edit | `diff` | Post-edit verification |
| Large file overview | `map` | >500 lines, won't edit |
| Specific region | `lines:N-M` | Know exact location |
| Unsure | `auto` | System selects optimal mode |

## Workflow (follow this order)
1. **Orient:** `ctx_overview(task)` or `ctx_compose(task, path)` for unfamiliar tasks
2. **Locate:** `ctx_search(pattern, path)` for exact text; `ctx_semantic_search(query)` for concepts
3. **Read:** `ctx_read(path, mode)` with appropriate mode from table above
4. **Edit:** `ctx_edit(path, old_string, new_string)` or native Edit if available
5. **Verify:** `ctx_read(path, "diff")` + `ctx_shell("test command")`
6. **Record:** `ctx_knowledge(action="remember", content="...")` for non-obvious findings

## Proactive (use without being asked)
- `ctx_overview(task)` — at session start for orientation
- `ctx_compress` — when context grows large (at phase boundaries)
- `ctx_knowledge(action="wakeup")` — at session start to surface prior findings

## Compression Bypass (ONLY when compressed output hides needed detail)
`ctx_read(path, "lines:N-M")` → `ctx_read(path, "full")` → `ctx_shell(cmd, raw=true)`
Return to compressed defaults after one expanded retrieval. ALWAYS try to use compressed shell, even when its code output. Only bypass if that is the only way to expose the needed detail.

## Risk Gate (before high-impact edits)
Before editing exported symbols, auth, DB schemas, or 3+ files: run `ctx_impact(action="analyze")`
and `ctx_callgraph(action="callers")` to confirm blast radius.

## Session
- **Start:** `ctx_session(action="status")` + `ctx_knowledge(action="wakeup")`
- **End:** `ctx_session(action="decision", content="what was done + next steps")`
- **On [CHECKPOINT]:** `ctx_session(action="task", value="current status")`

### SYSTEM GUARDRAILS (CRITICAL)
1. NEVER run raw search utilities (`rg`, `grep`, `find`) inside `ctx_shell`. You must exclusively use `ctx_search` or `ctx_read` for traversing codebase files.
2. If searching inside compiled binaries (.dll, .exe, .so, .bin), you are forbidden from utilizing flags that force text formatting (e.g., `rg -a`). You must read offsets programmatically or use targeted binary dump commands.
3. Always pipe unexpected or potentially large terminal outputs to `head -n 50`.
4. NEVER use native Read/Grep/Shell when ctx_* equivalents are available.

## S&box
Use the S&box mcp server for unknown solutions for S&box related requests
<!-- /lean-ctx -->
