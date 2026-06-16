# Agent Instructions
- Rely strictly on LeanCTX's Cross-Session Context Protocol (CCP) for project state.
- Do NOT read files globally upon opening or restarting this project workspace. 
- Assume previously cached structures remain completely valid.
- If you need to refresh a file, strictly append `fresh=true` to your `ctx_read` call.

<!-- lean-ctx -->
## lean-ctx

Prefer lean-ctx MCP tools over native equivalents for token savings.
Full rules: @LEAN-CTX.md
<!-- /lean-ctx -->
