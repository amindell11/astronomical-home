# Machine & tooling environment

> STATUS: living — machine/tooling facts the scripts cannot carry; pointed at from AGENTS.md

Facts about *this* developer's machines and installed tooling. Anything a script
already enforces lives in that script — this file points at it instead of
restating it.

## Worktree pool capacity

Pool is `agent-1..5`. Grow only when EVERY slot holds a live claim in the work
ledger (a stale lock is reclaimable by plain `acquire`):
`git worktree add -b agent-6 D:/amind/git/agent-6 origin/main` from the primary
tree — the pool script discovers slots by the `agent-N` branch pattern. **Never
past `agent-7` without asking the user.** A fresh slot is Unity-cold (full asset
import + Burst compile, plus the AnnotationManager trap in #401) — prefer a warm
slot for iteration-heavy work.

## Alastor — second Windows box (remote Unity lane)

`ssh alastor` (→ `desir@Alastor.local`; Windows PowerShell 5.1, no `||`/`&&`).
Win 11 Home, i9-13900H, 32 GB, RTX 3050. Unity 6000.1.8f1 at
`C:\Program Files\Unity\Hub\Editor\6000.1.8f1\Editor\Unity.exe`, `unity` CLI at
`C:\Users\desir\AppData\Local\Unity\bin\unity.exe`, Personal license activated.
Repo at `C:\dev\astronomical-home` — keep it off OneDrive. Power plan is a
duplicated High Performance scheme (Balanced throttled PlayMode 3×).

Dispatch is `scripts/remote_gate.sh`; the lane is proven at local parity
(815/820, 57 s exec, 2026-08-27). Two traps the scripts cannot carry:

- ⚠ `Start-Process` over ssh does NOT survive disconnect (OpenSSH kills session
  children). Detach batch work with
  `Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine='powershell -NoProfile -ExecutionPolicy Bypass -File <launcher>'}`.
- ⚠ Session-0 GUI editors are DEAD — they stall pre-project-load on an invisible
  modal. A GUI editor needs an **interactive scheduled task** with `desir` logged
  in at the console: `schtasks /Create /F /IT /TN <n> /TR '<launcher>' /SC ONCE /ST 23:59`
  then `schtasks /Run`. The task runs elevated, hence the already-applied
  `git config --global --add safe.directory C:/dev/astronomical-home`.
- ⚠ WoL to `E4-0D-36-C8-14-37` does not wake it (BIOS). After a physical wake SSH
  takes minutes while ARP already answers — loop `until ssh` rather than diagnose.
- ⚠ Both machines are mutually unreachable at L2 on the shared Wi-Fi (no ARP
  either direction; suspect AP client isolation) — this blocks #463. Fix candidates:
  Ethernet, or Tailscale.

## GitHub LFS budget exhausted (since 2026-08-27)

Fresh clones and CI fail at checkout with `smudge filter lfs failed`. Workaround:
`scp -r .git/lfs/objects <host>:<repo>/.git/lfs/` from a machine with a warm
cache, then `git checkout -f HEAD`. Delete this section when the quota is raised.

## Sentis rewrites scripting defines on every editor load

`com.unity.ai.inference` (pulled in by `com.unity.ml-agents`) has an
`[InitializeOnLoadMethod]` that adds `SENTIS_ANALYTICS_ENABLED` when
`EditorAnalytics.enabled` and removes it otherwise — either direction dirties
`ProjectSettings.asset` and forces a recompile.

Fix, with Unity closed: set BOTH
`HKCU\Software\Unity Technologies\Unity Editor 5.x\EnableEditorAnalytics_h1011414259`
and `EnableEditorAnalyticsV2_h1918497687` to `0`. ⚠ RECURS — an interactive
session resets V2 back to `1`; check both values after any interactive session.
⚠ Never commit the define: agent worktrees run batch, where the manager strips it
and leaves every merge gate dirty. (The self-consistent committed form
`SENTIS_ANALYTICS_ENABLED;FORCE_SENTIS_ANALYTICS` was rejected 2026-07-22 — it
opts batch runs into Sentis model-import telemetry.)

## git-lfs owns hooks in `core.hooksPath`

git-lfs auto-installs its four hooks into whatever `core.hooksPath` names, on the
next LFS filter run — not only on explicit `git lfs install`. Hence the stock LFS
hook bodies are committed into `.githooks/` (mode 100755; `core.filemode=false` on
Windows drops the bit, so `git update-index --chmod=+x`), and
`scripts/install_hooks.sh` owns the config. ⚠ `git config core.hooksPath` inside a
worktree writes the SHARED repo config. The `dev/null/` junk-dir mechanism and the
ban on `-c core.hooksPath=/dev/null` are in AGENTS.md § Session hygiene; the
recreator was never pinned.

## Never hand-edit `~/.claude.json` while Claude Code runs

The app holds the file in memory, rewrites it wholesale, and rotates the current
file over `~/.claude.json.backup` each time — a hand edit is clobbered within
seconds, and a degraded in-memory view propagates into both file and backup
(a ~90 s window cost 46 top-level keys + 22 project entries, 2026-07-22).
Change config from a plain terminal with the app fully quit
(`claude mcp add-json` / `claude config`). Before any recovery on a file a live
process owns, snapshot every surviving copy first. MCP registrations survive a
wipe on the session `claude.exe` command line (`Get-CimInstance Win32_Process`).
Rebuild script: `C:\Users\amind\claude-mcp-rebuild.sh`.

## Session identity & the rename channel

A session renames itself with `mcp__ccd_session_mgmt__set_session_title` and
`session_id: "self"` — passing its own explicit id is refused, and that refusal is
the id form only. Supporting facts, all measured:

- `$CLAUDE_CODE_HOST_SESSION_ID` is the CCD id (use it when another session must
  reference you); `$CLAUDE_CODE_SESSION_ID` is the CLI/transcript UUID, a
  different namespace no session tool accepts.
- An Agent-tool subagent SHARES the parent's identity and cannot act on the
  parent's session at all.
- ⚠ `create_scheduled_task`/`update_scheduled_task` are HARD-GATED — they prompt
  the user regardless of permission mode. Never design automation that arms a
  scheduled task per event.
- Dead ends: hand-editing the session-store JSON is clobbered within ~10 s;
  `send_message` is a mailbox that never wakes the target; the session server has
  no dialable endpoint.

⚠ Provenance: reading the id-form refusal as "a session cannot rename itself"
cost two weeks of dismantled workarounds in 2026-08. When a tool refuses, re-read
its parameter docs for the sanctioned form before architecting around it.

## ai-counsel (cross-model debate MCP)

Installed at `C:\Users\amind\tools\ai-counsel` (venv `.venv`), registered
user-scope with `PYTHONUTF8=1`; wired into the `design-consult` skill as Mode C.
Windows fixes in its `config.yaml`: npm `.cmd` shims aren't spawnable, so codex
points at the vendored `codex.exe` absolute path (re-point after npm updates) and
gemini at `node` + absolute `gemini-cli/dist/index.js`.

- `deliberate` needs ≥2 participants and a REQUIRED `working_directory`.
- ⚠ Model ids must exist in `config.yaml`'s `model_registry`, and the server
  **caches it at startup** — additions need an app restart (verify with
  `list_models`). This is why panels sometimes lose a leg.
- ⚠ **Read `full_debate`, not the tally.** Failed legs return `[ERROR: ...]` and
  count as ABSTAIN, so 2+ dead legs report
  `consensus_reached: true, winning_option: "ABSTAIN"`.
- Legs: codex WORKS (`gpt-5.6-sol`, `gpt-5.5` fallback); claude WORKS via a
  `claude setup-token` token in `CLAUDE_CODE_OAUTH_TOKEN` (non-interactive OAuth
  refresh is an upstream gap); gemini permanently off (account banned).

Direct codex consult, when the cached registry lacks a leg:
`codex.exe exec --skip-git-repo-check --cd <repo> --sandbox read-only --model gpt-5.6-sol -c 'model_reasoning_effort="high"' - < packet.md > out.md`
(~2–4 min/round).
