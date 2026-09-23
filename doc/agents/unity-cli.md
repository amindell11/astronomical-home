# Unity CLI — repo contract & gotchas

Repo-side companion to the machine-generated `unity-cli` skill
(`~/.claude/skills/unity-cli/`, rendered from the binary by `unity skill refresh` —
never edit it; repo deltas live here). The editor-side surface is experimental
(`com.unity.pipeline`) on a beta CLI. Claims below were last verified live on CLI
`1.0.0-beta.11` + `com.unity.pipeline 0.7.0-exp.1` (2026-09-22): re-verify them after
bumping either one.
Coordination (leases, boot policy, routing into a held editor):
`.claude/skills/unity-access/SKILL.md`. Capture lanes (clips, live-editor stills):
`.claude/skills/game-capture/SKILL.md`.

## CLI ↔ package version coupling

The CLI and the project's `com.unity.pipeline` pin version independently, and a CLI
upgrade can outrun the package. beta.11 refuses every parameterized `unity command` on
a package older than `0.6.0-exp.1` ("package is too old to parse command lines");
parameterless commands keep working, so `editor_status` still looks healthy while the
routed test lane and eval are dead. After any CLI upgrade, run one parameterized
command (`set_window_title --label x`) before trusting the lanes.

- `unity pipeline upgrade` stepped only 0.5 → 0.6 and then reported `alreadyLatest`
  with 0.7 published; pin explicitly: `unity pipeline install --package-version <v>`.
- An unfocused editor does not resolve a manifest edit on its own — restart it through
  `unity-access` so the boot resolves the package.

## `unity` plugin skills

`unity@claude-plugins-official` (user scope) adds `unity:*` task skills plus its own
`unity:unity-cli` copy, which lags the binary. "`unity-cli`" in this repo means the
binary-rendered user skill above — prefer it over the plugin copy.

- Plugin skills that run `unity command eval` still need a `unity-access` lease and
  `--project-path`; their snippets omit both.
- `unity:unity-package-management`'s direct `-batchmode` Editor launch is barred —
  every boot goes through `unity-access`.
- `unity:generate-editor-search-query` fires on generic "find/locate" wording; repo
  lookups stay on Grep, not the editor Search window.

## Targeting & readiness

- Always pass `--project-path <proj>` — per-project routing (the editor's
  `Library/Pipeline/.unity-pipeline-port`) is reliable; discovery is not.
- Gate readiness on `unity command editor_status --project-path <proj>`
  (status / compiling / domainReloadInProgress / playMode; `--result-only` drops the
  envelope). `unity status` and `pipeline list` are blind to live coordinator-launched
  editors: `STATUS_NO_INSTANCES` / zero instances while `editor_status` answers `ready`.
  (Listing phantom dead editors was not reproduced on beta.11 after a clean close.)
- Live inspector edits are not on disk. When the user is tuning ScriptableObject
  assets in an open editor, ask them to save before you read or commit those
  assets.

## Command discovery — read, don't guess

`unity command --format json` prints the full catalog with typed schemas; read a
command's schema before first use. Params are `--flag value`; `key=value` is rejected.
Flag names are inconsistent across siblings (`set_autotick --enable` vs
`add_scene_to_build --enabled`, `delete_asset --asset`), but a wrong name is now caught
client-side (exit 2) with a `Did you mean --enable?` hint. Array params take JSON
(`--instance_ids "[-3036]"`); a bare scalar is rejected. On the listing form, query
flags use underscores (`--group_by`; `--group-by` is silently ignored) and become
command params once a command name is present.

## Attaching vs booting

`unity command` is the attach path. `unity run --command` is a one-shot fresh batch
boot (beta.11 help and docs agree) — it never reuses a live editor and launches Unity
outside `unity-access`, so it is barred here like any direct launch.

## Running tests

Sync PlayMode `run_tests` does not run: the inner result carries `success:false` and
"PlayMode tests cannot run synchronously over HTTP", but the envelope is `success:true`,
exit 0, with an all-zeros summary — a gate trusting the exit code goes green on zero
tests. Use `--async_tests true` plus `test_status` polling (results also land in
`Temp/pipeline_test_status.json`). `test_status` puts its payload in `data.result` as a
JSON string; `--result-only` returns it parsed.

## Output paths

- `capture_game_view` / `capture_scene_view` take `save_path`: project-relative,
  rejects `..` and absolute paths, lands under `Assets/` — triggering imports and
  polluting the tree. Delete the folder (e.g. `Assets/Screenshots`) when done.
- `screenshot` takes `--output` and accepts absolute paths (default:
  `Temp/pipeline-screenshots/`) — prefer it when it can do the job. `--view scene`
  honors a scene-camera pose set over eval (`SceneView.LookAtDirect` + `Repaint`), but
  `sv.camera.transform` reads the old pose until the next repaint.

## eval / eval_file

- Snippets are method-body-wrapped: `using` directives are compile errors — fully
  qualify every type. Grep the repo for the exact namespace before writing
  (`Substrate.GamePlane`, `AI.Navigation.MPC.Cost`; guesses cost a round-trip each).
  `run_script` compiles a whole `.cs` file (usings, types) with no domain reload and
  calls a static entry point — use it when a snippet outgrows a method body.
- `internal` members need reflection.
- Autotick (keeps an unfocused editor servicing commands) is on by default and
  `set_autotick` persists across domain reloads (`--persist false` for a one-off).
  An unfocused editor with autotick off still answered evals in ~300 ms.
- PowerShell mangles embedded double-quotes in inline snippets (the string splits into
  the next flag: `--timeout expects Int32 but got there;`): write the snippet to a file
  and use `eval_file` for anything nontrivial.
- A failed eval (compile or runtime) fails the envelope: `success:false`, exit 6,
  message in `errors[]`. With `--format json`/`--result-only` that JSON is on stdout;
  in human format a failure prints only to stderr, so `2>$null` there reads as silence.
  Check the exit code.

## Selection

`set_selection --instance_ids "[<id>]"` selects scene objects, negative instance-ids
included (pass a JSON array).

## Domain-reload dead zones

Play-*enter* reloads the domain: the first command after `editor_play` can fail with
`Connection reset by server`, and the next blocks ~5 s until the reload ends. Retry once
or poll `editor_status`. A forced script reload (`RequestScriptReload`) makes
`editor_status` fail for ~1 s rather than report `domainReloadInProgress:true`. After
`editor_stop`, `delete_asset` and `editor_status` answered immediately.

## Latency envelope

End to end from the CLI, plain commands and warm evals both take ≈270–350 ms; the first
eval after a reload ≈1.1 s. `wait_for` with `--on_met '{"capture":{…}}'` captures in the
editor frame its condition first holds — the atomic primitive sub-second subjects
(laser bolts) need; gizmo composition through it is untested (#446 is benched; its
reopen condition decides whether this gets evaluated).

## Warm-capture lane

`capture_lane_attach` / `capture_lane_release` (journaled no-reload play for a
lane session) and `capture_request_scenario` (one-shot scenario dispatch to the
routed capture runner) are this repo's `[CliCommand]`s on `CaptureLane`.
Recipe and constraints: game-capture skill §"Warm lane (attach to a resident
editor)".
