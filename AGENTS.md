# AGENTS.md

This folder (`Astronomical/`) is a game-design subproject inside an Obsidian vault.

## Default multi-agent workflow (required)
When handling design/documentation requests in this folder, prefer the project chains in `.pi/agents/` over ad-hoc single-agent responses.

### Chain routing
- Use **`design-pipeline-lite`** for fast ideation and feedback (research + design options, no full doc drafting).
- Use **`design-pipeline`** for full design work that should end with ready-to-paste documentation updates.
- Use **`docs-cleanup`** for polishing existing docs (clarity, consistency, structure, stale content cleanup).

## Hand-off expectations between chain steps
- `obsidian-scout` must cite note paths for non-obvious claims.
- `game-designer` should ask only high-value clarifying questions and make trade-offs explicit.
- `design-doc-writer` must preserve accepted intent, avoid inventing major decisions, and include:
  - What changed
  - Open questions
  - Suggested next review

## Obsidian conventions (inherit + reinforce)
- Respect wikilinks, aliases, heading anchors, block refs, embeds, and frontmatter.
- Keep edits path-scoped to this project unless asked otherwise.
- Flag ambiguous or unresolved note references explicitly.
