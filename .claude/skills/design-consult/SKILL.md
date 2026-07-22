---
name: design-consult
description: Hand a decision brief, design, or risky diff to a fresh second-opinion agent (codex CLI, an Opus/Fable subagent, or another session) for design feedback or a targeted adversarial pass, and route the results back through the fix-ladder triage. Use when the user asks for a codex/second-opinion/design review or a consult on a plan or diff, or asks for a multi-model debate/panel on a contested fork.
---

# Design consult

A backend-agnostic protocol for getting a second opinion on a design or diff
from a fresh agent. The packet is assembled the same way for every backend;
only the delivery mechanism differs.

## 1. The consult packet

Assemble these four parts, in order, into one self-contained document:

1. **Scope statement** — the work under review, as scoped with the user in the
   task's Step-1 scoping. The consultant judges fit against this, not against
   what it imagines the task to be.
2. **The artifact** — the decision brief, plan-doc section, or diff under
   review, verbatim.
3. **Design-values preamble** — copy, at consult time, from the repo's
   `CLAUDE.md` (NOT from this file — CLAUDE.md is the single source of truth):
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

## 4. Mode C — panel debate (escalation only)

Enter only when (a) two independent consults materially disagree on a
consequential fork, or (b) the user explicitly asks for a debate/panel.
One deliberation per fork — never a standing default.

Delivery: `mcp__ai-counsel__deliberate` (user-scope MCP server; install
location, per-CLI auth status, and Windows notes live in memory
`reference_ai_counsel.md`). Call shape:

- **question** — the full consult packet (all four parts), with the fork
  stated as an explicit choice between the named positions.
- **mode** — `"conference"` (multi-round; critics see and rebut each other).
- **participants** — 2–3, cross-family where CLI auth allows; model ids must
  exist in the server's `config.yaml` `model_registry` or the call is
  schema-rejected.
- **working_directory** (required) — the repo root, so critics can read code.

Output contract, stated in the packet: each participant gives
1. a position on the fork — one of the named options, no new alternatives;
2. its strongest argument and the strongest objection to the rival position;
3. what evidence would change its mind.
Votes ride the tool's structured voting; the packet does not restate them.

Reading the result: a failed leg returns `[ERROR: ...]` as its response and
is counted as an ABSTAIN vote, which can flip `voting_result` — judge from
`full_debate` (or the transcript in `<working_directory>/transcripts/`),
never from the tally alone.

Return path unchanged (section 6): final positions and votes are consultant
output — fix-ladder triage, disposition table, decision terminates at the
user.

## 5. Backends (Modes A/B)

| Backend | Delivery |
|---|---|
| codex CLI | Pipe the packet to the codex CLI session. No repo-standard invocation exists yet — record the command you use so a convention can accrete. |
| Fresh Claude subagent | Spawn via the Agent tool, optionally with a model override (e.g. opus / fable). The packet goes in the prompt verbatim; the subagent gets NO other conversation context — fresh eyes are the point. |
| Manual handoff | Write the packet to a file the user can paste into any session, and tell the user where it was written. |

## 6. Return path (hard rules)

- Every consultant output routes through the CLAUDE.md fix-ladder triage,
  exactly like PR review comments. Produce the same disposition table —
  `| # | Item | Disposition | Where |` — and report it to the user BEFORE any
  code is written.
- Architecture proposals (Mode A item 2) always terminate at the user via the
  ladder's cost gate — never auto-adopted, never partially implemented "while
  we're here."
- A consult's output is input to triage, never a work order.
