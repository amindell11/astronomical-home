# Issue tracker: GitHub

> STATUS: living — the tracker binding consulted by tracker-shaped skills (wayfinder, to-tickets) and any agent writing issues.

Issues live in this repo's GitHub Issues; use the `gh` CLI. The repo is
**public** — every issue body and comment is world-readable.

## Body law

Deep rationale, trade-offs, and file-level detail never go in issue bodies or
comments — they live in agent memory or a plan doc, linked from the issue
(`Detail:` link); the memory file names the issue number. The tracker says *what / for-when*; memory says *why / how*;
live in-flight claims go in the active-work ledger (see `AGENTS.md`). Three
body shapes are legal:

- **Deferral issue** (default): scannable title + `Detail:` link. No essay.
- **Slice issue** (published by to-tickets): `What to build` (end-to-end
  behaviour) + acceptance criteria + `Blocked by`. Behavioural spec is
  public-safe; the design rationale stays in the plan doc it came from.
  The tracker owns an arc's slice breakdown — plan docs carry design and
  point at the issues, never a duplicate slice list.
- **Wayfinder map / ticket**: map body is an index (gists + links); ticket
  body is the `## Question`. A resolution comment carries the gist and, when
  the answer runs deep, a link to the memory/plan doc holding it.

## Labels

- **Priority axis** (how soon): `pri:now` / `pri:next` / `pri:later`. One per
  issue; `bug` marks defects and can ride alongside.
- **Triage states**: `needs-triage` — agent-created, awaiting user review
  (**default on every deferral issue an agent mints mid-task**; the user
  clears it to a priority/readiness label on review). `ready-for-agent` —
  fully specified, an AFK agent can take it. `ready-for-human` — needs human
  judgment or hands. `wontfix` — closed, not actioned; the closing comment
  links the memory file recording why.
- **Wayfinder family**: `wayfinder:map` on maps; `wayfinder:research` /
  `wayfinder:prototype` / `wayfinder:grilling` / `wayfinder:task` on tickets.
- **Domain labels** (`RL`, `Ship`, `Testing`, …) and `arc` (umbrella issue
  for a multi-PR arc) as today. Rename freely, don't proliferate.

## Operations

- Create / read / list / comment / close: plain `gh issue …`. Multi-line
  bodies via heredoc.
- **Sub-issues and blocking** (gh ≥ 2.94): `gh issue edit <n> --parent <map>`,
  `--add-blocked-by <n>`. API fallback needs the blocker's **database id**
  (`gh api repos/{owner}/{repo}/issues/<n> --jq .id`, not the `#number`):
  `gh api --method POST repos/{owner}/{repo}/issues/<n>/dependencies/blocked_by -F issue_id=<db-id>`.
- **Frontier query** (open, unblocked) — MUST pass `advanced_search=true`,
  or `is:blocked`-family qualifiers are silently ignored:
  `gh api -X GET search/issues -f q='repo:amindell11/astronomical-home is:issue is:open -is:blocked' -f advanced_search=true`.
  Robust fallback: GraphQL scan for `issueDependenciesSummary.blockedBy == 0`.
- **Rate limits** that bite a fleet: search 30 req/min; content creation
  80/min + 500/hr per account. Reads (REST 5k/hr) are ample.

## Projects board sync

The "Astronomical" user project (https://github.com/users/amindell11/projects/1,
private) mirrors the tracker for the human. New issues are NOT auto-added —
**every flow that creates an issue adds it to the board and sets Status**:

```
gh api graphql -f query='mutation { addProjectV2ItemById(input: {projectId: "PVT_kwHOAJsCkc4BfiTv", contentId: "<issue-node-id>"}) { item { id } } }'
gh api graphql -f query='mutation { updateProjectV2ItemFieldValue(input: {projectId: "PVT_kwHOAJsCkc4BfiTv", itemId: "<item-id>", fieldId: "PVTSSF_lAHOAJsCkc4BfiTvzhZ0hiE", value: {singleSelectOptionId: "<option-id>"}}) { projectV2Item { id } } }'
```

(`<issue-node-id>` via `gh issue view <n> --json id --jq .id`.)

Status option from labels: `needs-triage` → Triage `d6567434`; `bug` → Bugs
`76914216`; `pri:now` → Now `291743a0`; `pri:next` → Next `4dbdbff5`;
`pri:later` → Later `225f15fa`; Doing `772cf1a0` and Done `165b6aec` are
human/close-time states. First match in that order wins.

## Wayfinding operations

Used by the wayfinder skill; body law above applies.

- **Map**: one issue labelled `wayfinder:map` holding the
  Destination / Notes / Decisions-so-far / fog body.
- **Child ticket**: `gh issue edit <n> --parent <map>` + a `wayfinder:<type>`
  label and the map's priority label (default `pri:now` — a live effort's
  tickets are near-term by definition). Add to the Projects board like any
  new issue.
- **Blocking**: native dependencies (`--add-blocked-by`), wired in a second
  pass after creation.
- **Frontier**: the map's open children, minus blocked
  (`issue_dependencies_summary.blocked_by > 0`) and assigned; first in map
  order wins. **Claim** = `gh issue edit <n> --add-assignee @me`, before any
  work.
- **Resolve**: gist comment (+ memory link when deep) → close → append the
  context pointer to the map's Decisions-so-far.
