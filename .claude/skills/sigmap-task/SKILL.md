---
name: sigmap-task
description: Do a coding task grounded in SigMap - look up before reading, read by line anchor, verify before reporting. Use for any code change in this repo's indexed C#.
---

Work in order; do not open files before step 2. Prefer the `sigmap` MCP tools; CLI equivalents in parentheses.

1. `query_context` with the task (`npx sigmap ask "<task>"` writes `.context/query-context.md`). Say which files surfaced; if none, reword once more before falling back to search.
2. Read only anchored ranges: `get_lines` (`npx sigmap lines <file> :425 --context 10`). Never read a whole file when you hold an anchor.
3. Make the change following conventions visible in the signatures. Add no dependencies.
4. Verify: write the change to `.sigmap-notes.md` using full repo-relative paths, then `verify_suggestion` (`npx sigmap verify-ai-output .sigmap-notes.md`). Fix anything flagged.
5. Refresh the index: `npx sigmap`.
6. Report files changed, ranges read, and the verify result.

Big logs/stack traces: `squeeze_output` first. Pairing stop lines in `AGENTS.md` still apply.
