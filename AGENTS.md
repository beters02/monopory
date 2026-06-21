# Agent Instructions
- Rely strictly on LeanCTX's Cross-Session Context Protocol (CCP) for project state.
- Do NOT read files globally upon opening or restarting this project workspace. 
- Assume previously cached structures remain completely valid.
- If you need to refresh a file, strictly append `fresh=true` to your `ctx_read` call.
- You MUST use lean-ctx -c "git" when you are running git commands.

<!-- lean-ctx -->
## lean-ctx

Prefer lean-ctx MCP tools over native equivalents for token savings.
Full rules: @LEAN-CTX.md
<!-- /lean-ctx -->

<!-- lean-ctx-compression -->
OUTPUT STYLE: expert-terse
- Telegraph format: subject-verb-object, drop articles/prepositions
- Symbolic vocabulary: → cause, ∵ because, ∴ therefore, ⊕ add, ⊖ remove, Δ change, ≈ similar, ≠ different, ∈ in/member, ∅ empty/none, ✓ ok, ✗ fail
- Code blocks: untouched (never compress code syntax)
- Each line: max 80 chars
- Zero narration, zero filler
- BUDGET: ≤100 tokens per non-code response
- DO NOT NARRATE WHAT YOU ARE DOING unless it is important (debugging, etc) - The ENTIRE non-code response from task start to task finish MUST be <= 100 tokens.
PLAN MODE
- While in plan mode, you are allowed to be more discriptive with the actions you are taking. If writing something that is vital to the plan, you may provide small narration during that time. Your task start to task finish budget is raised to 300 tokens in that case.
- Go back to non-plan mode restrictions after finished with plan.
<!-- /lean-ctx-compression -->
