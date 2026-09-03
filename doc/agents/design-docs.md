# Design docs & work tracking

> STATUS: living — branch-triggered reference for design/doc work, tracker usage, and the doc lifecycle; pointed at from `AGENTS.md`. Tracker label/body/board mechanics: `issue-tracker.md`.

This game is designed in an Obsidian vault **outside** the repo. Treat these
two notes as primary context — for design/doc work, and for knowing what to
build next:

- **Design overview:** `D:/amind/Documents/Obsidian Vault/Astronomical/OVERVIEW.md`
  — the game's vision and core pillars (skill-ceiling combat, loot/rewards,
  LLM-driven AI dialogue; 2.5D top-down rogue-like). Read it, and the notes it
  wikilinks (`[[Sectors]]`, `[[Combat]]`, `[[World]]`, …), before proposing or
  implementing design-facing changes. If it conflicts with an assumption,
  follow the doc and call out the conflict.
- **Issue tracker:** GitHub Issues on this repo — the source of truth for
  backlog, bugs, and deferrals. Label vocabulary, body law, dependency/frontier
  mechanics and Projects-board sync: `doc/agents/issue-tracker.md`.

How to use them to track work:
- When starting a task, check whether it maps to an issue
  (`gh issue list --label bug`, `--label pri:now`, …) and whether a closed one
  already tried it (`gh issue list --state closed --search <term>` — negative
  results and design records live there). Ground the task in the issue's
  wording rather than inventing scope.
- For design/doc requests, research the vault directly (don't speculate) and
  cite note/file paths for non-obvious claims. Respect Obsidian conventions
  (wikilinks, embeds, aliases, anchors, frontmatter).
- **The tracker is a first-class, agent-writable artifact — actively
  maintain it, don't merely suggest.** Create, label, close, and comment via
  `gh` as work progresses.
- **Deferrals live on the tracker.** When the user says to defer / punt /
  park something, capture it as an issue — that issue is the deferred work's
  canonical home, and its body carries the why. Labels, body law, and
  sub-issue/blocking mechanics: `issue-tracker.md`.

## Where design lives

Code is the single source of truth. Prose explains only what cannot be
determined from the code at all: the why behind a decision, alternatives that
were measured and rejected, experimental results, rulings, open forks. It has
three homes, none of them under `doc/`:

- **The arc's issue** carries the brief — the design an agent builds against —
  before the first implementing slot is acquired. Unbuilt design, open forks
  and user rulings stay on that issue while the arc is open.
- **The PR description** carries the why of what shipped *and* the alternatives
  tried and rejected on the way. `git blame` → commit → PR (squash merges keep
  the number in the subject) is the sanctioned hop from a line to its why;
  make it when you're curious, don't ask for a doc.
- **The closed issue** carries what shipped nothing: negative results,
  postmortems, dropped slices. Their distilled rules go to `doc/agents/`
  (only after a repeated observed failure — the AGENTS.md bar). Nothing in code
  links to these; discovery is `gh issue list --state closed --search <term>`
  at task start, which is why that search is part of grounding a task.

**Reading design.** Records and PR bodies are read by section, never whole:
ask the `design-lookup` agent (`.claude/agents/design-lookup.md`) a list of
direct questions and take its cited answers; fetch a body yourself only for a
citation you already hold (`gh issue view N --json body -q .body | awk
'/^## <heading>/,/^## /'`). **Writing design.** A record or brief is
sectioned by question — one `##` per ruling, result, or fork — so a citation
names a section, not a document.

`doc/Feature_Plans/` and `doc/Postmortems/` no longer exist (migrated to
`design-record` issues 2026-09-02). Session handoffs are memory material,
never repo docs or issues; the consuming session deletes them. What remains
under `doc/` — `doc/agents/`, `Glossary.md`, `Diagnosis_Loop_Cookbook.md` —
is agent law and vocabulary, held to the same bar: derivable text gets cut in
the PR that touches it.
