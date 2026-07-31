---
name: agent-worktree-pr-loop
description: Default workflow for coding tasks in this repo — scope with the user, build/test in a warm agent-N worktree, open a PR, iterate on review, then merge and reset on explicit approval. Use for any new implementation task, not only when the user names a slot or worktree explicitly.
metadata:
  project: astronomical-home
  primary-script: scripts/agent_worktree_pool.sh
---

# Agent Worktree + PR Loop

## Applicability

Default for ANY coding task (bug fix, feature, refactor — not pure Q&A or
read-only exploration), without the user naming the pool, a slot, or "PR".
Exceptions: trivial doc/comment-only edits the user explicitly asks to be made
directly, or explicit instruction to work in place.

**Docs-only landing (direct-to-main, no PR).** A change may skip this loop and
be committed/pushed to main directly when ALL of: (1) the diff touches only
documentation paths (`doc/**`, `*.md`, `.claude/**.md` — no code, no assets,
nothing that executes); (2) the content was explicitly user-approved in the
session landing it — that approval IS the review; (3) the commit message
carries the story a PR body would have. Verify (1) mechanically
(`git diff --cached --stat`) before pushing. These landings are cited by
commit SHA, not PR number. Anything touching code takes the full loop.
(Decided 2026-07-31: the merge gate never ran tests on docs-only deltas, so
the PR ceremony added review the session had already performed.)

## Pool commands

- `./scripts/agent_worktree_pool.sh status`
- `./scripts/agent_worktree_pool.sh acquire <lease-id> [slot]` — name a slot when you have a reason (warm Unity Library from related work, the ledger/dashboard shows affinity, or avoiding a slot with an open editor); a named slot that isn't free fails rather than falling back, so pick from the dashboard, don't guess. Omit for auto-pick (free slots before stale reclaims).
- `./scripts/agent_worktree_pool.sh prepare <slot> origin/main` — never during feedback rounds unless the user explicitly asks to restart from main.
- `./scripts/agent_worktree_pool.sh run-tests <slot> <test args>` — forwards args straight to the runner (no `--`; see the cheat-sheet)
- `./scripts/agent_worktree_pool.sh create-pr <slot> --title "<text>" (--body "<text>" | --body-file <path>)` — title/body are required (validated before anything runs); pushes to the same `task/<lease>` branch as `submit`, just without a test run.
- `./scripts/agent_worktree_pool.sh submit <slot> origin/main --title "<text>" (--body "<text>" | --body-file <path>) -- <test args>` — same required flags; only a passing full run (`-Mode Both -ScopeType Workspace`, unfiltered) records merge-grade proof; scoped runs still open the PR but never satisfy the gate. `-ScopeType Auto` is the recommended scope for iteration and submit runs.
- `./scripts/agent_worktree_pool.sh review-comments <slot>`
- `./scripts/agent_worktree_pool.sh revise <slot> -- <test args>` — pull/rebase + tests + push.
- `./scripts/agent_worktree_pool.sh revise <slot> --no-test` — push without a test run and without recording proof; the gate then does the single full run on the exact landing tree.
- `./scripts/agent_worktree_pool.sh merge <slot>` — the ONLY merge path; see Step 6.
- `./scripts/agent_worktree_pool.sh finalize <slot> origin/main`
- `./scripts/agent_worktree_pool.sh release <slot>`

Branch naming: each task gets its own remote branch `task/<lease-id>` and its
own PR. Lease ids are the arc path — descriptive, branch-style names, one or
two words per level (`vocab`, `vocab-docfix`), so the arc is inside the
identifier rather than assumed from context. Slices additionally carry a
positional label in their plan (`Slice-C`, `PR-4`) — used in chat titles and
references, never in git refs. A leaf number appears only when one named unit
spans several PRs (`vocab-docfix-1`), and only at build time. Max three
levels. See `doc/Glossary.md` → *arc & PR naming*.
The local worktree stays on the `agent-N` branch; `submit` and
`create-pr` push to the task-specific remote branch automatically. Never run
two agents in the same slot at once. Both take an optional base after the slot:
`submit` normalizes an `origin/` prefix (`submit <slot> origin/main`), but
`create-pr` passes the base straight to `gh --base`, so give it a plain branch
name (`create-pr <slot> main`).

Visibility: `./scripts/worktree_dashboard.sh` (add `--watch` for auto-refresh)
shows all slots — lock status, branch, changed files, PRs, ahead/behind main.
For interactive review suggest `lazygit -p D:/amind/git/agent-<n>` (press `w`
to switch worktrees). For non-interactive diff reporting:

```bash
git -C <slot-path> diff --stat origin/main   # summary vs main
git -C <slot-path> diff origin/main          # full diff
git -C <slot-path> log --oneline origin/main..HEAD
```

## Invocation & args cheat-sheet

- **Bash tool only.** The pool script is bash — never run it through the
  PowerShell tool (`CantActivateDocumentInPipeline`), never pipe it into
  `Select-Object`. The Bash tool's cwd resets between calls, so start every
  pool call with `cd D:/amind/git/astronomical-home &&` (or the absolute script
  path); a bare `./scripts/...` fails `exit 127`.
- **Long runs go in the background.** A full `-Mode Both` run outlives the Bash
  tool's 2-minute default and is killed (`exit 143`). Run `run-tests`,
  `submit`, `revise`, and `merge` with `run_in_background: true` (or `timeout`
  ≥ 1800s).
- **Test args are two independent axes:** `-Mode {Both|EditMode|PlayMode}` and
  `-ScopeType {Workspace|Feature|Module|Smoke|Auto}`. `Smoke` is a **ScopeType,
  never a Mode** — a smoke run is `-Mode EditMode -ScopeType Smoke`. Also:
  `-ScopeName`, `-TestFilter`, `-TestCategory`, `-AssemblyNames`.
- **Where `--` goes:** `submit`/`revise` take `-- <test args>` *after* their
  base_ref; `run-tests` forwards test args **directly, no `--`**.
- **BurstCache before a run:**
  `rm -rf D:/amind/git/<slot>/src/Asteroids3D/Library/BurstCache/`.

## When a pool/test command fails

| Symptom | Cause | Recovery |
|---|---|---|
| A pool command reports exit 0 but clearly didn't finish | You piped it through `tee`/`head`/`tail` — a pipeline's exit code is the last stage's, not the command's | Redirect to a file (`> log 2>&1`) or run it in the background; never pipe a pool command whose exit code you rely on. |
| `STATUS=infra_error total=0` | Compile failure — no tests ran | The runner prints the `error CS…` lines inline; fix and re-run. After a main-fold, suspect a dropped source file. |
| runner: `parameter name '' is ambiguous` | A stray `--` reached `run-tests` | Drop it — `run-tests` takes args directly. |
| runner: `Cannot validate argument on parameter 'Mode' … "Smoke"` | `-Mode Smoke` | Smoke is a `-ScopeType`; use `-Mode EditMode -ScopeType Smoke`. |
| `REFUSING to prepare … uncommitted change(s)` on a lone `ProjectSettings.asset` / editor noise | Not real work | `git -C <slot> checkout -- <file>`, then re-`prepare` — don't push+`--force`. |
| `revise`/`prepare` trips on `Assets/InitTestScene*.unity` | Scaffold from a killed run | `rm` the `InitTestScene*.unity*` and re-run — never real work. |
| merge: `CONFLICT (content) … .unity`/`.prefab` | Gate merged main; Unity YAML doesn't auto-merge | Resolve in the slot, `revise` (re-test+push), re-`merge`. |
| merge prints "…moved since… merging it in" then exits non-zero | Concurrent merge re-synced main | Re-run `merge <slot>` until it prints "squash-merged" — a mid-sequence exit is a re-sync, not a failure. |
| `create-pr` push `! [rejected] … non-fast-forward` | Stale remote slot branch | `finalize`/`release` the slot (or `submit`, which re-preps) and retry. |
| Child PR silently `CLOSED`, can't reopen/retarget | It was stacked on a task branch that got squash-merged + deleted | Retarget the child to `main` **before** merging its base, or `create-pr` a fresh one. |
| `git checkout main` → `'main' is already used by worktree` | You're inside an `agent-N` worktree | Sync from the primary tree: `cd D:/amind/git/astronomical-home && git checkout main && git pull`. |
| post-merge `pull --ff-only` aborts on an untracked file | A merged PR made a primary-tree untracked file tracked | Diff it vs `origin/main:<path>`; if identical, remove the untracked copy and pull. |
| parsing `results/.../*-summary.json` → `UnicodeDecodeError` | UTF-8 file with non-ASCII test messages | Open with `encoding='utf-8'`. |

## Shared Unity access

`scripts/unity_access.ps1` coordinates Unity with per-project ownership: batch
test runs in different worktrees run in parallel; only Unity **startup**
serializes through a short machine-wide boot lane (concurrent boots were the
D6 deadlock hazard). `unity_test_agent.ps1` drives the whole protocol
automatically — you only queue when another run holds *your* project. Prefer
batch tests; use `-Action StartEditor` only for graphics, interaction, or MCP
verification that batch mode cannot cover, then `-Action Release -CloseEditor`
as soon as the check finishes. An untracked editor on the primary worktree
belongs to the user: report its PID and ask them to close it — never close it
automatically. The durable MCP server on port 8081 is shared and remains
running between owners.

## Chat title lifecycle

Chat titles surface each session's phase in the sessions list, so the user
sees "blocked on #234" without opening the chat. A session cannot retitle
itself; a standing **Title concierge** chat does it on request.

Every lifecycle-tracked chat uses ONE template — same slots, same order:

`[icon] <stage> | <slot-label> | <word-id> | #<pr>`

- `<stage>` — always present, always leads; icons only on the attention
  states (⛔ blocked, 🔀 merging, ✅ merged). Stage words: `prep`, `build`,
  `review`, `blocked`, `merging`, `merged`.
- `<slot-label>` — the plan's positional label (`Slice-C`, `PR-4`); the
  literal `Arc` for an arc-orchestrator chat.
- `<word-id>` — the descriptive branch-style name (`probe-clients`,
  `harness-lane`).
- `#<pr>` — the GitHub PR number; this slot appears once a PR exists.
- An optional trailing ` — <detail>` carries what the stage needs said:
  the blocker for ⛔ blocked (mandatory — name the PR, user decision, or run
  being waited on), `<now> → next <step>` for Arc chats (mandatory),
  `brief frozen` when prep locks before build.

Stage examples:
- `prep | Slice-C | onnx-slot`
- `prep | Slice-C | onnx-slot — brief frozen`
- `build | Slice-D | probe-clients`
- `review | Slice-B | capture-painters | #237`
- `⛔ blocked | Slice-D | probe-clients — waiting on #236 merge`
- `🔀 merging | Slice-B | capture-painters | #237`
- `✅ merged | Slice-B | capture-painters | #237`
- `build | Arc | harness-lane — B/C/D building → next PR-4`
  (an Arc chat's stage word is the arc's current overall stage)

A title starting with none of the stage words is a design-discussion chat —
those never retitle. Retitle at every transition that writes the ledger
(claim, PR-open, block, merge/finalize).

Fresh chats are born titled: when breaking out a new session for a slice —
a spawn chip, a handoff, a launch prompt you draft for the user — give it its
lifecycle title from the start (`prep | <slot-label> | <word-id>`) instead of
a freeform title plus a later retitle.

Requesting a retitle:
1. Your session id is the UUID in your scratchpad directory path, prefixed
   `local_`.
2. Find the concierge via `list_sessions` (title starts `Title concierge`).
3. `send_message` it one line: `RETITLE local_<id> → <new title>`.

No concierge found, or session tools unavailable (subagents, unattended runs)
→ skip silently; titles never block or delay work.

## Step 1 — Scope

Restate the task to the user: what changes, which files/systems are touched,
what's out of scope. Get explicit confirmation — always, even for tasks that
look small. Anti-churn gate: if the build is estimated over ~300 changed
lines, additionally confirm the FINAL shape before building v1, and the
presented options must include do-nothing/defer.

## Step 2 — Build

Read the work ledger before acquiring
(`C:\Users\amind\.claude\projects\D--amind-git-astronomical-home\memory\active_work_ledger.md`
— worktree agents must use this exact absolute path) and claim a row. Acquire
a slot; build and test there — directly, or via a sub-agent scoped to the
slot's worktree path when the task is large enough to benefit from an isolated
context. Clear `src/Asteroids3D/Library/BurstCache/` before test runs. Iterate
with scoped runs (`-ScopeType Auto`, or Feature/Module scopes).

## Step 3 — Pre-review quality pass

Once tests are green and BEFORE the PR is presented for review, run ONE
combined quality sub-agent over the diff with this charter:
(a) simplification/reuse/efficiency fixes — flag only what affects correctness
or the stated scope, no new abstractions, no bug-hunting, no speculative
findings; (b) comment hygiene on TOUCHED HUNKS ONLY per CLAUDE.md's comment
rules. Its edits become part of the tree the user reviews. Summarize its
changes in the PR body.

## Step 4 — Submit

`submit` with an explicit `--title` (conventional-commit style; it must
describe the actual payload) and a real `--body`. The PR body carries the
build story: what changed and why, test proof, quality-pass changes, and a
scope-conservation check — read the diff back against the Step-1 scope
statement; anything a scope-reader wouldn't expect either comes out or is
flagged in the body for confirmation. An arc-completing PR also settles its
plan doc — delete the transient brief, or update the living doc's STATUS
header (convention in `AGENTS.md`). The body also carries one bookkeeping line,
`Vocab: <new/changed terms | none>`; anything but `none` means `doc/Glossary.md`
moves in this same PR. Flip the ledger row to in-review with the PR number.

## Step 5 — Review round-trip

The automated review bot is currently DISABLED — do not wait for a bot round;
present the PR for the user's review as soon as submit is green. (If the bot
is re-enabled, restore the old protocol: wait for and triage its round before
requesting user review, and never request merge approval mid-round.)

Run EVERY review comment (bot or human) through the CLAUDE.md fix ladder —
its entry gate is the triage:
- **Speculative** → rebut with an on-thread reply, no code.
- **Real but outside this change's scope** → defer (board card + on-thread reply).
- **Real and in scope** → fix at the rung the ladder selects, escalating to
  the user at the cost gate.

After each round, post ONE PR comment containing a disposition table —
`| # | Comment | Disposition | Where |` — with a row for every comment in the
round (dispositions: Fixed (rung N) / Rebutted / Deferred; Where = commit
hash, thread reply, or board card). No comment may lack a row. Use `revise`
to re-push fixes.

## Step 6 — Merge

Only on an explicit user merge instruction. Consent = an explicit instruction
to merge ("merge it", "ship it", "land it"); praise of the code ("looks
good", "LGTM") is NOT consent. Approval binds the tree: record the branch
HEAD at the moment of consent; if ANYTHING lands on the branch after that
(including hygiene), present the delta and re-confirm before merging.

Immediately before merging, re-check for unresolved comments (they can land
between approval and merge). One check, not a wait: an empty result is clear
to merge — never poll or delay waiting for comments to appear. Triage
newcomers as in
Step 5: rebut/defer outcomes proceed (reply + table row — the tree is
unchanged, approval stands); a fix outcome changes the tree and reopens
approval.

Merge exclusively via `./scripts/agent_worktree_pool.sh merge <slot>` — never
raw `gh pr merge`, never force-push, never skip the gate's test run. The gate
re-tests against current main when main moved after the branch's last test
run; it skips only on full-suite proof for the exact landing tree; it extends
proof over docs-only deltas with no run; it downgrades C#-comment-only deltas
to an EditMode Smoke compile refresh. Scoped runs (`-ScopeType Auto`) are fine
for iteration but record no merge proof.

## Step 7 — Finalize

`./scripts/agent_worktree_pool.sh finalize <slot> origin/main`, then pull
`origin/main` in the primary worktree (`git checkout main && git pull`).
Delete the ledger row — the story lives in the PR body and the memory topic
file, not the ledger.

## Preconditions & known hazards

- `revise` cannot rebase a branch carrying a merge commit (it replays main's
  commits and resurrects resolved conflicts) → manual `git push` + `merge`
  (the gate runs tests itself).
- `-SkipUnityAccess` is acceptable only when the lane is blocked by a
  cross-project interactive editor — never to dodge concurrent batch startup.
- When two slot branches conflict, the second merger adapts.
- After an asmdef-restructuring merge, do a clean recompile
  (`rm -rf Library/ScriptAssemblies Library/Bee Library/BurstCache`) before
  trusting any test result.
- `submit` does not commit — commit in the slot first.
