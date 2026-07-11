# Unity Tooling Pathology — Post-Mortem

A consolidated pathology of the recurring failure modes in this repo's agent
tooling: **Unity MCP**, the **batch test runner** (`scripts/unity_test_agent.ps1`),
the **worktree pool** (`scripts/agent_worktree_pool.sh`), and the **Burst/MPC**
diagnosis traps. It exists so future agents (and humans) stop re-discovering the
same walls, and so the hardening work has a single reference.

Scope note: this is a *pathology*, not live state. Specific file/line claims
were true when written (2026-07-08/09); verify against current code. Live,
right-now claims live in the active-work ledger; durable project narrative in
agent memory.

---

## 1. The cross-cutting root causes

Almost every specific failure below reduces to one of three structural facts.
Fixing *these* is what actually shrinks the failure surface.

### RC1 — Single-holder contention
Exactly one Editor instance can hold the Unity project and bind the MCP port
(8081). The batch test runner *also* needs exclusive project access. So
**in-Editor MCP work and batch testing are mutually exclusive**, and the two
verification paths (in-Editor GPU vs headless `-nographics`) do not agree. This
produces both the "batch can't run, project open" errors and the "passes
in-Editor, fails in batch" flakies.

### RC2 — Ephemeral-shell statelessness
Each Bash/PowerShell tool call is a fresh, short-lived process. Anything that
stores state keyed to a process — a bound MCP session, a pid-scoped lock —
silently breaks between calls.

### RC3 — Editor-state drift as a hidden global
The Editor carries mutable global state (`EnterPlayModeOptions`, the loaded
scene, prefab assets, the Burst cache, inspector values) that persists across
MCP calls, test runs, and even into git. Agents treat each call as stateless;
it is not.

### Meta-cause — the documentation itself drifts
The corrective knowledge repeatedly rotted: port 8080 vs **8081**;
`execute_code` "disabled" vs "the workhorse"; which server `mcp__unity-mcp__*`
actually is. Multiple mistakes were re-made on 2026-07-08 that the notes had
"documented" wrongly. Section 9 is the authoritative correction.

---

## 2. Cluster A — MCP server identity & connection

| # | Failure mode | Root cause | Status |
|---|---|---|---|
| A1 | Agent calls `mcp__unity-mcp__Unity_*` thinking it's the live server | Those are the **fallback relay** to Unity's official AI Assistant (dead by default). The real CoplayDev server is `mcp__UnityMCP__*` / `mcp__unity-http__*` on `127.0.0.1:8081` | Recurring |
| A2 | MCP tools absent for a whole session even after Unity comes up | Claude Code binds HTTP MCP servers **once at session start**. Server-not-up-then ⇒ tools gone all session; need `/mcp` reconnect or restart | Known, unfixed |
| A3 | Port confusion (8080 vs 8081) | Docs said 8080; actual is **8081**. Config lives in user-level `~/.claude.json`, **not** committed `.mcp.json` ⇒ fresh clone needs the wizard re-run | Corrected (§9) |
| A4 | "Failed to connect" mid-session | Unity-side server/pipe **dies or rebinds on every domain reload**; plugin-spawned server sometimes dies permanently. Stale endpoint, not "Unity closed" | Partial workaround (§9) |
| A5 | Blind tool call stalls ~12 min | The fallback bridge exposes **no real per-tool schemas** (`additionalProperties:true`); a probe call hangs | Mitigated: always ToolSearch the real schema first |
| A6 | Wrong editor instance answers | Both transports attach to **whichever editor instance runs the plugin**, not "the main project". A worktree editor serves that worktree; the port is first-to-bind | Understood, unguarded |

**Chunk-down:** `scripts/unity_doctor.ps1` (shipped, §8) resolves A1–A4/A6
deterministically instead of from memory.

---

## 3. Cluster B — `execute_code` workflow

| # | Failure mode | Root cause | Status |
|---|---|---|---|
| B1 | Contradictory guidance: "disabled by default" vs "ENABLED and is the workhorse" | Doc drift. In practice `execute_code` (Roslyn) **is enabled** and authored the whole uGUI hangar prefab + SerializedObject wiring | Corrected (§9) |
| B2 | Inline C# to `execute_code` fails / garbles | Shell quoting mangles inline JSON; **non-ASCII glyphs get garbled**. Pass code via file→python; ASCII only | Resolved workflow |

The ASCII-only trap is not hypothetical: PR #96's first draft of
`unity_doctor.ps1` contained a single em-dash and Windows PowerShell 5.1 (which
reads BOM-less files as ANSI) mangled it into quote-like bytes that broke the
parser. **Author agent-facing scripts ASCII-only.**

---

## 4. Cluster C — Editor-state hygiene (the "hidden global" cluster)

| # | Failure mode | Root cause | Status |
|---|---|---|---|
| C1 | In-Editor PlayMode suite: bogus failures ("GamePlane already configured", ships won't move) | `EnterPlayModeOptions` with the **DisableDomainReload** bit leaks statics/pool state. The in-Editor MCP `run_tests` path needs domain reload ON | Detected by doctor (§8) |
| C2 | Spurious "already configured" even with reload ON | Running **EditMode then PlayMode in one editor session** leaks; run PlayMode solo | Known |
| C3 | Editor churn leaks into commits | `Minimap.renderTexture`, `EditorSettings.asset`, transient test scenes, and **accidental inspector edits** (e.g. Hauler maxSpeed 19→1) get staged | Pre-commit warn hook (§8) |
| C4 | Debug overlay duplicated 11× on the rig | `Build` runs on the **prefab asset** in-editor and mutated it; `FindObjectsByType` never finds it at runtime | Fixed (#91) |

Note the **batch runner is structurally immune to C1/C2**: it spawns a *separate
fresh Unity process per platform*, so every batch run gets a clean domain
reload. The trap only bites the in-Editor MCP `run_tests` path.

---

## 5. Cluster D — Batch test runner (`unity_test_agent.ps1`)

| # | Failure mode | Root cause | Status |
|---|---|---|---|
| D1 | `HangarShipSwapPlayModeTests` (3) fail in batch, pass in-Editor | `-nographics` URP render loop can't create camera RTs (`RenderTexture.Create failed`); NUnit treats the logged error as failure | **Quarantined (#95)** |
| D2 | "Project appears open… infra_error" (exit 32) | The runner **can't run while an interactive editor holds the project** (RC1), surfaced by `Test-ProjectAlreadyOpen` | By design; doctor warns |
| D3 | `FullLoop_NoEnemy_PatrolStateSelected` cited as a blocker | Actually already `[Ignore]`d in code — not a live blocker; the memory note was stale | Resolved (verify before citing) |
| D4 | GamePlane test order-dependent flaky | A leaked **pooled** `ProjectileBase` from an earlier combat test ticks during an unconfigured frame | Fixed (#60) |
| D4b | *Failed fix — do not retry* | Destroying leaked projectiles in `SetUp` breaks the pool ⇒ `MissingReferenceException` in later weapon tests | Documented dead-end |
| D5 | Stale/cold Burst cache ⇒ wrong controls or slow first run | Struct-layout changes want a `Library/BurstCache` clear; a **cold** cache makes the first run minutes-long and can push perf-probe tests past their timeout | Doctor warns; clear before struct changes |
| D6 | Concurrent multi-agent batch runs deadlock EditMode at boot | Licensing / global cache contention; force-kill then leaves a stale `Temp/UnityLockfile` ⇒ instant `infra_error` until cleared | Avoid concurrent batch; clear lockfile |

**D5 in the wild:** during PR #95 verification a full PlayMode run took 27 min
(cold Burst) and `MpcEightShip_PerfProbe_LogsSolveCost` timed out at 180 s;
re-running warm passed 1/1. A cold-Burst perf-probe timeout is environmental,
not a regression.

**The compounding effect:** D1 kept the batch suite red on `main` for reasons
unrelated to any given PR, and `submit`/`revise` run with `set -e` — so the
whole pool submit path was effectively blocked until D1 was quarantined (#95).

---

## 6. Cluster E — Worktree pool lock lifecycle

| # | Failure mode | Root cause | Status |
|---|---|---|---|
| E1 | Abandoned locks never free | `acquire` is an atomic `mkdir`; nothing reclaims a lock whose owner is long dead. (The often-repeated "pid-scoped, reclaimed as stale" story is itself a misdiagnosis — the script never checks the pid) | **De-pid + TTL (PR4)** |
| E2 | `submit` pushes an empty diff | `submit` does **not commit** — it pushes the committed `agent-N` HEAD | Gotcha |
| E3 | CLOBBER: `prepare` `reset --hard`s another task's unpushed WIP | `prepare` resets unconditionally; a reused slot may hold unpushed commits | **Guarded (PR4)** |
| E4 | REVISE: rebases the branch onto ancient `origin/agent-N` (100+ commits) | `revise` falls back to the slot name when the lease/task-branch is missing ⇒ `git pull --rebase origin agent-N` | **Guarded (PR4)** |
| E5 | `set -e` blocks push on any pre-existing failure | Interacts with D1/D3 | Mitigated by #95 |
| E6 | LEASE RACE: `submit`/`revise` pushes to another task's branch | PR4's durable lease was written with plain `git config`, but agent worktrees share one `.git/config` — any concurrent `acquire` clobbered every slot's lease, and `lease_for` prefers config over the per-slot lock file. Bit two sessions on 2026-07-10 (#105, #106) | **Fixed: lease now `--worktree`-scoped (`extensions.worktreeConfig`)** |

**Established manual workaround (pre-PR4):** don't rely on `submit`/`revise`
end-to-end. Work in the slot, then `git add -A && commit` → `git push -u origin
agent-N:refs/heads/task/<lease>` → `gh pr create`. Before any `prepare`, check
`git -C <slot> log --oneline origin/main..HEAD` and `status --short` for
someone else's WIP.

---

## 7. Cluster F — Diagnostic misattribution (the cautionary tale)

| # | Failure mode | Root cause | Status |
|---|---|---|---|
| F1 | Weeks of MPC failures blamed on Burst cache / struct-layout corruption | **Actual cause:** `CombatAICommander.TryInitializeSystems()` → `UtilitySelector.Initialize()` transitions to Attack and runs `Attack.Enter` **during test setup**; tests disable the selector *after*, so `goalMode` state persists. Fix: reset navigator state after setup | Root-caused |

The old `mpc-goalmode-handoff.md` note is a monument to this: eight "Burst
layout" fixes tried, all failing, because the diagnosis was wrong. **Lesson:**
when a whole class of tests fails identically and "logical" fixes don't move the
needle, suspect **shared setup/teardown state ordering** before engine-level
corruption.

---

## 8. Hardening backlog — status

Ranked by leverage. This is the actionable list; strike items as they land.

1. **Quarantine the known-red baseline** — ✅ **shipped (#95).** HangarShipSwap
   tagged `[Category("RequiresGraphics")]`; `unity_test_agent.ps1` gained
   `-ExcludeCategory` (default `RequiresGraphics`) → negated Unity
   `-testCategory` filter. Tests still run in-Editor; skipped only headless.
   Interim: drop the category when the real headless-RT fix lands.
2. **`unity doctor` preflight** — ✅ **shipped (#96).** `scripts/unity_doctor.ps1`
   reports MCP server/port, project-holding editor, `EnterPlayModeOptions`
   trap, and Burst cache state. Run it at the top of any Unity-MCP/test session.
3. **Runner owns `EnterPlayModeOptions`** — ⏸️ **subsumed.** The batch runner is
   already immune (separate per-platform processes); the doctor covers the
   in-Editor warning. No standalone change needed.
4. **De-pid the worktree locks** — 🔵 **PR4.** Lease + TTL staleness instead of
   an unreclaimable `mkdir`; bake the pre-`prepare` WIP guard (E3) and kill the
   `revise` slot-name fallback (E4). Merge only when the pool is idle.
5. **Commit-hygiene gate** — 🔵 **PR5.** Warn-only pre-commit hook for
   churn-prone tracked files (`EditorSettings.asset`, `*.renderTexture`) + a
   defensive gitignore for transient test scenes.
6. **This document + MCP runbook reconciliation** — 🔵 **PR6 / memory.** §9 is
   the authoritative runbook; the drifted MCP memory files are being corrected
   to match.

---

## 9. Authoritative runbook (the corrections)

The single source of truth for the facts that kept drifting. When memory and
this section disagree, prefer this section (and re-verify against live state via
`unity_doctor.ps1`).

- **Primary MCP server:** CoplayDev **UnityMCP**, HTTP, `http://127.0.0.1:8081/mcp`
  (**8081**, not 8080). Tools appear as `mcp__UnityMCP__*` / `mcp__unity-http__*`.
- **`mcp__unity-mcp__Unity_*` is the FALLBACK relay** to Unity's official AI
  Assistant, not the primary. Don't confuse them.
- **Session binding:** Claude Code connects HTTP MCP servers once at session
  start. If the server wasn't up then, run `/mcp` to reconnect (or restart the
  session) — ToolSearch alone won't surface the tools.
- **Reload survival:** the plugin-spawned server can die on domain reload.
  Starting it manually makes it survive reloads:
  `uvx --offline --from "mcpforunityserver==10.0.0" mcp-for-unity --transport
  http --http-url http://127.0.0.1:8081 --project-scoped-tools`; then only the
  WS reconnect matters (user focus kicks it).
- **`execute_code` is ENABLED and is the workhorse.** Author C# to a file and
  pass it via python (inline shell quoting mangles JSON). **ASCII only** —
  non-ASCII glyphs garble, and BOM-less scripts break Windows PowerShell 5.1.
- **In-Editor test runs** via `run_tests`/`get_test_job` work while the user's
  Editor holds the project (the batch runner can't run then). The PlayMode
  suite needs **domain reload ON**; if `EnterPlayModeOptions` has the
  DisableDomainReload bit, flip it to None for the run and restore after.
- **Burst:** clear `Library/BurstCache` before testing after struct-layout
  changes; expect a cold cache to make the first run minutes-long.
- **Don't run concurrent batch suites** across multiple agents (boot deadlock);
  if one is force-killed, clear the stale `Temp/UnityLockfile`.

---

## 10. Working principles distilled

- **Preflight, don't remember.** Run `unity_doctor.ps1` instead of
  reconstructing the environment from notes.
- **Verify in the mode you ship in.** In-Editor GPU runs mask `-nographics`
  failures; the batch path is the gate.
- **Suspect setup ordering before engine corruption** when tests fail as a
  class (Cluster F).
- **One editor, one port, one project holder** — sequence Unity-MCP work and
  batch testing; never assume they coexist.
- **Author agent-facing scripts ASCII-only** and validate parsing under Windows
  PowerShell 5.1.
