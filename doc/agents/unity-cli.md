# Unity CLI — repo contract & gotchas

Repo-side companion to the machine-generated `unity-cli` skill
(`~/.claude/skills/unity-cli/`, rendered from the binary by `unity skill refresh` —
never edit it; repo deltas live here). The editor-side surface is experimental
(`com.unity.pipeline`) on a beta CLI: re-verify the bugs below after any version bump.
Coordination (leases, boot policy, routing into a held editor):
`.claude/skills/unity-access/SKILL.md`. Capture lanes (clips, live-editor stills):
`.claude/skills/game-capture/SKILL.md`.

## Targeting & readiness

- Always pass `--project-path <proj>` — per-project lockfile routing is reliable;
  discovery is not.
- Gate readiness on `unity command editor_status --project-path <proj>`
  (status / compiling / domainReloadInProgress / playMode). `unity status` and
  `pipeline list` are unreliable in both directions — blind to live unfocused editors
  AND listing phantom dead ones. Polling them cost one session 7 minutes.

- Live inspector edits are not on disk. When the user is tuning ScriptableObject
  assets in an open editor, ask them to save before you read or commit those
  assets.

## Command discovery — read, don't guess

`unity command --format json` prints the full catalog with typed schemas; read a
command's schema before first use. Params are `--flag value` (`key=value` is rejected)
and flag names are inconsistent across siblings (`--enabled` vs `--enable`,
`delete_asset --asset`), so each guess costs a round-trip. On the listing form, query
flags use underscores (`--group_by`) and become command params once a command name is
present.

## Attaching vs booting

`unity command` is the attach path. `unity run --command` does NOT reuse a live editor
(the docs contradict themselves; integration-advanced.md is right) — it fresh-boots, blocks
on the same-project lock behind a resident editor, and dies at `--timeout`.

## Running tests

Sync PlayMode `run_tests` is a silent no-op: it returns in ~0.2 s with `success:true` and an
all-zeros summary having run nothing, so a gate trusting it goes green on zero tests. Use
`--async_tests true` plus `test_status` polling (results also land in
`Temp/pipeline_test_status.json`); `test_status` returns its payload sometimes as an object,
sometimes as a JSON string — parse both.

## Output paths

- `capture_game_view` / `capture_scene_view` take `save_path`: project-relative,
  rejects `..`, lands under `Assets/` — triggering imports and polluting the tree.
  Delete the folder (e.g. `Assets/Screenshots`) when done.
- `screenshot` takes `--output` and accepts absolute paths — prefer it when it can do
  the job.

## eval / eval_file

- Snippets are method-body-wrapped: `using` directives are compile errors — fully
  qualify every type. Grep the repo for the exact namespace before writing
  (`Game.GamePlane`, `Movement.MPC.Navigator`; guesses cost a round-trip each).
- `internal` members need reflection.
- On an unfocused/background editor, run `set_autotick --enable true` first — without
  it, main-thread ops time out at 5000 ms. Autotick resets on EVERY domain reload, so
  re-arm it after each one; a starved editor reads as a wedged server (30 s timeouts).
- PowerShell mangles embedded double-quotes in inline snippets: write the snippet to a
  file and use `eval_file` for anything nontrivial.
- Keep stderr visible and check the result's error fields: `2>$null | ConvertFrom-Json`
  eats the error JSON, so a failed eval prints nothing and reads as success (this
  produced captures with debug toggles believed off).
- CLI JSON nests the payload under `data.result` — an envelope `success:true` can wrap an
  inner `success:false`. Always check the inner result.

## Selection

`set_selection --instance_ids` with negative editor instance-ids reports success and
selects NOTHING. Select via eval instead: `UnityEditor.Selection.objects = ...`.

## Domain-reload dead zones

Both play-mode transitions reload the domain: play-*enter* gives a ~2 s window of
failing commands; after `editor_stop`, asset ops (`delete_asset`) time out and
`editor_status` is briefly unreachable. Poll `editor_status` until it answers before
firing follow-ups.

## Latency envelope

Plain commands ≈100 ms; `eval` 0.5–1.5 s (server-side Roslyn compile per snippet), so a
select→capture round-trip is ~0.5–1 s. Sub-second subjects (laser bolts) cannot be
caught from outside the editor — that needs an editor-side `[CliCommand]` primitive
(`capture.gizmo_still`, carded #446).

## Warm-capture lane

`capture_lane_attach` / `capture_lane_release` (journaled no-reload play for a
lane session) and `capture_request_scenario` (one-shot scenario dispatch to the
routed capture runner) are this repo's `[CliCommand]`s on `CaptureLaneSession`.
Recipe and constraints: game-capture skill §"Warm lane (attach to a resident
editor)".
