---
name: design-consult
description: Hand a decision brief, design, or risky diff to a fresh second-opinion agent (the Codex, Muse or Gemini CLI, or an Opus/Fable subagent) for design feedback or a targeted adversarial pass, and route the results back through the fix-ladder triage. Use when the user asks for a codex/gemini/muse/second-opinion/design review or a consult on a plan or diff, or asks for a multi-model debate/panel on a contested fork.
---

# Design consult

A protocol for getting a second opinion on a design or diff from a fresh
agent. The packet is assembled the same way for every consultant; only the
delivery differs.

## 1. The consult packet

Assemble these four parts, in order, into one self-contained document:

1. **Scope statement** — the work under review, as scoped with the user in the
   task's Step-1 scoping. The consultant judges fit against this, not against
   what it imagines the task to be.
2. **The artifact** — the decision brief, plan-doc section, or diff under
   review, verbatim.
3. **Design-values preamble** — copy, at consult time, from the repo's
   `AGENTS.md` (NOT from this file — AGENTS.md is the single source of truth):
   - the **Fix ladder** section, verbatim;
   - the **Dependency & wiring philosophy** section, verbatim;
   - plus this one line of project context: "solo-developer project; machinery
     must earn its place; simpler means fewer moving parts, not more elegant
     abstraction."
4. **The question** — exactly one of the modes below (A, B, or C), including
   its output contract, stated in the packet so the consultant knows the
   required shape.

## 2. Mode A — design consult (default)

State this output contract in the packet:

1. **Verdict** on the current design measured against the supplied values —
   fit or misfit, with reasons.
2. **At most ONE alternative architecture** — sketched with trade-offs and
   migration cost. Not a menu.
3. **The simplification challenge** (mandatory): what would this look like
   with fewer moving parts? What can be deleted or collapsed outright?
4. **Passing red flags** — correctness issues noticed along the way, capped
   at 3, each tagged observed-mechanism vs hypothetical.

## 3. Mode B — targeted adversarial pass

Only with a NAMED risk to hunt (e.g. "find ways this reset seam breaks
determinism") — never an open-ended "review comprehensively."

Output contract, stated in the packet: a findings list capped at 5, each with
- the claim,
- severity,
- observed-vs-hypothetical tag,
- the concrete failing scenario.

Overflow is handled by requesting another round, never a bigger batch.

## 4. Mode C — panel debate (fork-shaped questions)

Mode choice is by question shape, not sequence:

- **Review-shaped** ("is this design sound", "find how X breaks") →
  Mode A/B. A panel adds little over one consultant there.
- **Fork-shaped** — named alternatives, consequential, and our own analysis
  yields no dominant option → start at Mode C directly.
- **Escalation** — a Mode A/B consult unexpectedly surfaces a fork, or the
  user explicitly asks for a debate/panel.

Never run consults to manufacture a disagreement precondition, and never
re-run a settled consult as a debate. One deliberation per fork — never a
standing default.

Delivery: two rounds over the consultants picked in section 5, all of them in
parallel within a round.

1. **Positions.** Send the full consult packet (all four parts), with the fork
   stated as an explicit choice between the named positions.
2. **Rebuttal.** Once every consultant has answered or failed, resend the
   packet with every round-1 answer appended under its consultant's name. Each
   consultant holds or switches, and answers the strongest objection raised
   against its position. One rebuttal round, then stop.

Output contract, stated in the packet: each consultant gives
1. a position on the fork — one of the named options, no new alternatives;
2. its strongest argument and the strongest objection to the rival position;
3. what evidence would change its mind.

Reading the result: take each consultant's position from its own round-2
text. A consultant that errored or returned nothing is absent and counts
toward no option.

Return path unchanged (section 6): final positions are consultant output —
fix-ladder triage, disposition table, decision terminates at the user.

## 5. Consultants

Ask the user which consultants to include before sending anything: one
multi-select question over this roster, your pick first. For Mode A/B pick one
consultant from a different model family than the session that built the
artifact; for Mode C pick three across families.

| Consultant | Delivery |
|---|---|
| Codex 6.2 Astra | `codex exec --skip-git-repo-check --cd <tree> --sandbox read-only --model gpt-6.2-astra -c 'model_reasoning_effort="high"' - < packet.md > codex.md` |
| Muse Wonder Full | `muse.cmd exec --model muse-wonder-full-1.0 --workspace <tree> --disable-write --disable-shell --prompt-file packet.md > muse.md` |
| Gemini 4 Neon | `(cd <tree> && ~/.gemini/bin/agy.exe --print "Read <packet> and answer it per its output contract." --model gemini-4-neon-high --mode plan) > gemini.md` |
| Opus 5.5, Fable 5.2 | Agent tool with `model: "opus"` or `model: "fable"`. The packet goes in the prompt verbatim; the subagent gets no other conversation context. |

Write the packet to `packet.md` in a scratch directory and run each CLI from
there through the Bash tool with `run_in_background: true`. `<tree>` is the
worktree holding the work under review and `<packet>` is `packet.md`, both as
absolute paths in the form `cygpath -m` prints. The flags above keep each CLI
read-only in `<tree>`. What the commands can't say (CLI versions, PATH):
`doc/agents/environment.md` → Consult CLIs.

## 6. Return path (hard rules)

- Every consultant output routes through the AGENTS.md fix-ladder triage,
  exactly like PR review comments. Produce the same disposition table —
  `| # | Item | Disposition | Where |` — and report it to the user BEFORE any
  code is written.
- Architecture proposals (Mode A item 2) always terminate at the user via the
  ladder's cost gate — never auto-adopted, never partially implemented "while
  we're here."
- A consult's output is input to triage, never a work order.
