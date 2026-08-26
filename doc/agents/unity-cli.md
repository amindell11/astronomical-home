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

## Command discovery — read, don't guess

`unity command --format json` prints the full catalog with typed schemas; read a
command's schema before first use. Params are `--flag value` (`key=value` is rejected)
and flag names are inconsistent across siblings (`--enabled` vs `--enable`,
`delete_asset --asset`), so each guess costs a round-trip. On the listing form, query
flags use underscores (`--group_by`) and become command params once a command name is
present.

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
  it, main-thread ops time out at 5000 ms.
- PowerShell mangles embedded double-quotes in inline snippets: write the snippet to a
  file and use `eval_file` for anything nontrivial.
- Keep stderr visible and check the result's error fields: `2>$null | ConvertFrom-Json`
  eats the error JSON, so a failed eval prints nothing and reads as success (this
  produced captures with debug toggles believed off).

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
(need recorded in the warm-capture arc, #414).
