# Agent Instructions

## Scope
These are **workspace-only** instructions for the monopory project. Ignore global/user Cursor rules that duplicate or conflict with files in this repo (`.cursor/rules/`, `AGENTS.md`, `LEAN-CTX.md`).

## LeanCTX CCP
- Rely strictly on LeanCTX's Cross-Session Context Protocol (CCP) for project state.
- Do NOT read files globally upon opening or restarting this project workspace.
- Assume previously cached structures remain completely valid.
- If you need to refresh a file, strictly append `fresh=true` to your `ctx_read` call.
- You MUST use `ctx_shell("git ...")` when running git commands.

## lean-ctx
Prefer lean-ctx MCP tools over native equivalents for token savings.
Full rules: `.cursor/rules/lean-ctx.mdc`.
