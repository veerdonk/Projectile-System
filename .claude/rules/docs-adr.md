---
paths:
  - "docs/**/*.md"
---

# Docs and ADRs

- Vocabulary stays in `REQUIREMENTS.md`. Runtime design stays in `ARCHITECTURE.md`. Work plan stays in `PHASES.md`.
- Changing stages, API names, pooling, or hit rules requires a new entry in `DECISIONS.md` before implementation.
- Do not reintroduce Noita wrap or a `Timer` modifier; see existing ADRs.
- Keep process (git, pairing) in root `AGENTS.md`, not in requirements.
