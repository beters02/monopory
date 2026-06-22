# LEAN-CTX — monopory project rules

This file mirrors `.cursor/rules/lean-ctx.mdc` for agents that read `LEAN-CTX.md`.

## Mandatory tool mapping

| Instead of | Use |
|------------|-----|
| Read | `ctx_read(path, mode)` |
| Grep/rg/find | `ctx_search(pattern, path)` |
| Shell/bash | `ctx_shell(command)` |
| ls/find dirs | `ctx_tree(path, depth)` |
| Edit/Write/StrReplace | `ctx_edit(path, old_string, new_string)` |
| New file | `ctx_edit(path, new_string, create=true)` |

**Never** use native Read, Grep, Shell, Edit, Write, or StrReplace when a ctx_* tool exists.

## Workflow

1. `ctx_overview` / `ctx_search` / `ctx_read` to orient
2. `ctx_edit` to change files
3. `ctx_read(path, "diff")` to verify

## Bypass

Images attached in chat may use native Read; return to ctx_* immediately after.
