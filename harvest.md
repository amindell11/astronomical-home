# Preview workflow harvest — 2026-10-22

Eleven sessions mined for how agents got "in-game eyes" and where the loop broke. Six Codex
(sector jump #1099, recap #1065/#1129/#1146, hit effects #792, camera flight #1149, weapon
explosions #1091, Crimson paint #1031, hull lighting #1029), five Claude (laser bolt #767,
weapon glyphs #994, hangar parallax #1020 + frame drops #1022/#1049, ship lineup #1015,
asteroid playtest #263, gizmo eyeball #357, Gizmo View #470–480). Condensed transcripts are in
`transcripts/` beside this file.

## Effort, by session (reader estimates from timestamps)

| Session | Feature | Getting eyes | Infra fighting | Owner-judged rounds |
|---|---|---|---|---|
| Sector jump day 1 | 28% | 42% | 13% (+17% design) | 6, then owner asked to fly it |
| Sector jump day 2 | 7% | 12% | 80% (one 42-min editor boot never reached ready) | live tuning attempt |
| Recap day 1 (#1065) | 20% | 55% | 25% | 20 (12 HTML widgets, 8 Unity PNGs) |
| Recap day 2 (#1129, #1146) | 25% | 30% | 45% | 2 rulings, rest found by playing after merge |
| Hit effects (#792) | 0 landed | 65% of active | 17% | 5, then "let me fly the first one" |
| Camera flight (#1149) | 15% | 25% | 60% | 0 flyable in 21 h; "I would want to try other angles myself" |
| Weapon explosions (#1091) | 40% | 30% | 30% | 11, seven of them after the PR opened |
| Crimson paint (#1031) | 50% | 30% | 20% (+4.4 h approval wait) | 0 visual; merged "visual review still required" |
| Laser bolt (#767) | 25% | 30% | 30% | tuner page; merged before any in-engine animation |
| Weapon glyphs (#994) | 55% | 35% | 10% | 13 verbal rounds, parked |
| Hangar parallax (#1020) | 25% | 30% | 15% | "slightly" never judged; owner watched live |
| Hangar frame drops (#1049) | 30% | 55% | 15% | 2 contact sheets |
| Ship lineup (#1015) | 35% | 45% | 10% | owner caught what the manager missed |
| Asteroid playtest (#263) | 45% | 35% | 20% | 3 clip rounds, then "I'll tune it myself" |
| Gizmo eyeball (#357) | 0 | 100% | ~40% of CLI lane | owner was the eyes for half |

## Hack families, each rebuilt per session

1. **Staged-moment scratch scenarios** (`scratch/capture/*.cs` subclassing `CaptureScenario`):
   SectorJumpMotion/Polish/Authored/HybridProbe, RecapResponseScenario, WeaponExplosionPreview,
   FieldDensityFlythroughProbe, CollisionCostProbe, CameraFlightVisualProbe. Each hand-wrote the
   same moves: load InitScene additively or compose a Session; reflect into `GameHost.runRecords`
   and `HangarScreen.launchButton`; `SetInvulnerability(100f)`; `TestDamage.Kill` at frame N;
   `InputTestFixture` press at frame M; hide the FMOD debug overlay; force 1280×720; write a
   `timing.csv`. In the sector jump the *animation itself* (turn, travel, exhaust, HUD fade) lived
   only in probe code with no path into game code.
2. **Sample-data screen entry points** (`RecapCapture.Victory/Defeat/Sector/Size`,
   `RecapPreview.Show`) run via `unity command run_script` to build the production screen with
   fake data over a live game. No production hooks; deleted with the slot (81 files left in one
   scratch dir, three capture variants made by PowerShell `.Replace()`).
3. **Live-editor eval snippets**: `hide-debug.cs` (needed in every capture session, 9 refs),
   `launch_hangar.cs`, `select_ships.cs`, `gameview_gizmos_on.cs`, `recap-ready.cs`,
   `recap-pause-import.cs`, `frames_start/read.cs` (ProfilerRecorders across evals via
   `AppDomain.SetData`), `view_mode.cs`, `kill_player.cs`, `import_*.cs` (no asset_refresh
   command). A few promoted to `game-capture/cli-eval/`; most rediscovered.
4. **Batch stills lanes** (`RunBatch -executeMethod`): `SkyStills.Capture`, `PaintPreviewLineup`,
   `NightshadeLineup`, `CrimsonPaintCapture` (derived from `CanopyCapture.cs`), `bolt_still.cs`.
   The matched-capture harness (sync shader compile, warm-up frame, baseline-repeat determinism,
   old/new in one session via material toggle, neutral key light, game-scale thumbnail) exists in
   at least three copies. Without a canonical rig: engine-view instead of top view, "rendered
   tiny", dim warm light, Performant render scale 1.252 in agent editors, colour-space scares.
5. **HTML mockups and tuner pages** (Codex `visualize` widgets with variant carousel; Claude
   artifact tuners with knobs; Playwright self-checks). 1.5–5 min per round and the owner engages
   ("specific knobs… a good way to work"). But: no real game background ("the background is
   still schematic"), no direct manipulation (widget `edits` always empty; owner re-sent the
   agent's own capture from a phone to point), no machine-readable return path (settings in the
   owner's browser storage, first export lost), agent cannot watch the page run (SendUserFile HTML
   runs no JS; browser pane refuses `file://` and agent-started localhost), and drift from the
   engine (RNG, 3/4 resolution, hard-cut vs shipped crossfade, oblique sketch vs overhead game,
   0.8 s authored sweep vs 0.67 s after a balance merge).
6. **Evidence-branch plumbing by hand**: 11 detached worktrees in one session, `.githooks/`
   stubs deleted ~10 times, hand-written LFS attributes, HEAD-checked URLs, `EVIDENCE_SHA`
   placeholder substitution in PR bodies, three different recipes (orphan worktree under
   `.codex`, `git init` in `%TEMP%`, slot `scratch/`). Yet SHA-pinned hosted media was the ONLY
   delivery that reliably reached a remote owner; local paths, `file://`, `open_in_codex` and
   in-chat widgets cost ~2 h of "I can't see it on remote" stalls in one session alone.
7. **Clip encoding hacks**: per-session encoders (`encode_intro.py` timing GIF frames from a
   `seconds` column, `encode_jump_preview.py` with MAXCOVERAGE quantizer), #1103 (GIF duration
   truncation) and #1104 (palette loss turned red paint blue-grey and produced a false bug
   report), 9.6 MB GIFs, "MP4 is the reference" disclaimers in every PR.
8. **Preview-only renderers**: `SectorJumpPreviewWindow` with hidden preview scenes and a baked
   `Ship.png`. Owner: "did you reinvent the wheel here when we could have just made this as a new
   simple scene? or is this a genuinely useful tool we could generalize". Agent: "a simple tuning
   scene would have been the better foundation… the useful part is Replay, Loop, time scrubbing
   and a saved settings profile… they should drive the same effect and ship rendering that the
   game uses." The window was verified by reflection, never by pixels; its GUI-into-RenderTexture
   bug reached the owner.
9. **Comparison toggles shipped as production code** (F6 camera cycle with an `OnGUI` label) with
   no sunset, and **test-only production hooks** (`PooledVFX.EndsAt`) added in one PR and removed
   in the next.
10. **The owner's editor as blocker**: "Could you close the Unity editor… process 12120" (8 min),
    "open your own for that", a `-CoexistWithUserEditor` patch improvised in a slot and never
    landed, dev-player builds (6 min, take over the desktop for 20 min, closed from outside),
    40+ min editor boots for the owner to fly a branch, all slots full for 21 h.

## Judgment failures (wrong, not just slow)

- **Verification encoded the agent's reading, not the owner's target.** #1129's
  `death-to-breakdown-verification.json`: `deathSeconds 2.16, bannerStartsSeconds 7.80`; pass
  criterion `effectsFinishBeforeBanner: true`. The 5.6 s wait is exactly what the owner reported
  after playing, 9.5 h after merge, together with "camera doesn't go to the ship" (the agent's
  reading of "hold framing") and "other ships get frozen" (by design).
- **Clips cannot carry pacing or feel.** Capture samples fixed sim steps while UI timing uses
  unscaled time; a 3× scripted cruise for field density; "slightly" for parallax. Owner rulings
  across sessions: "maybe I will just tune it myself. can you set up a tuning sector for me and
  open an editor with it"; "let me fly the first one and get it right first"; "is this something
  i can fly? i don't really like the angle you chose, I would want to try other ones myself";
  "What can I tune about it (when I'm home)". The owner's own asteroid field ended up denser than
  the clip-tuned one.
- **Agent committed and gated before the owner's verdict** (density iteration 2), then: "I won't
  re-commit or re-gate until you've okayed the look".
- **Pictures over-trusted**: "Vanguard has colours now" (owner: "Vanguard looks completely wrong
  still"); Nightshade depth lost to an evidence-only `paint.json` reset;
  Crimson merged at 9–12% pixels changed with no visual sign-off; "visual review still required".
- **No real gameplay backdrop on disk**: laser and glyph candidates composited over style frames
  and vault mockups; the owner asked twice to "move to an in game mockup overlaid on the game
  screen" and "see the full screen for placement eval".
- **Hidden editor state** silently hides drawers (AnnotationManager, GizmoView default-off); a
  gate that asserts "frames exist" passed a gizmo-less capture.

## What worked (keep)

- Two-tier loop: cheap outside-engine candidates → in-engine only for placement, motion, final.
- Contact sheets at **real** game scale (px/unit from ortho size, true LANCZOS downscale), knob
  grids the owner picks a cell from, reference row first.
- Generator as single source of truth: `--tuner` bakes the page from the script's constants,
  `--export` writes the engine asset, re-export checked byte-identical.
- Real-asset bridge: `RecapSilhouettes` baked from prefabs into mockups, then shipped.
- Fixture-driven preview entry points (sample victory/defeat/sector; empty and overflowing
  lists; alternate ranks) and the #1098 proposal: authored prefab + preview of the production
  screen, phase select and scrub, "a timing change must not require replaying a whole run".
- The staged-moment recipe (invulnerable, deterministic kill at t, scripted input at t, fixed
  res, overlay hidden, phase telemetry CSV) as the way to reach a moment deterministically.
- Numeric tripwires beside pictures (padding 12/12, centre within 0.1 px, changed-pixel share,
  replay frame hash, luminance stats), never as acceptance.
- Immutable per-round baselines on `evidence/<lease>` with README, source, MP4, stills; owner
  rulings quoted on the arc issue. The owner leaned on these for "go back to the version before".
- Capture transaction pattern: snapshot → drive → restore, journaled for crashes; profile +
  gizmoScope as the single agent knob.
- Designer-facing SO profile with replay/scrub/loop/save and a separate tuning copy (the
  *controls* of the sector-jump window were right; the renderer was wrong).
- Batch `RunBatch -executeMethod` stills run with the owner's editor open.
- Tuning sector + owner's editor + knob cheat sheet: where feel actually got judged.
- Reproduce instead of argue (ReloadCheck, colour-space byte probe).
- Honest labels on stand-ins ("modelled offline, not an engine capture") and provenance JSON
  (`captureSource`, `captureData`, timing basis).

## Hull lighting (#1029, #1046) — the drift case

- Round 1 was a matched side-by-side motion clip (current vs prototype; object motion, light
  sweep, camera-only) close up and at game size. The owner approved from it.
- Revisions 2–6 each got one frontal-light still compared against the **previous revision**,
  never against main or the approved clip. Motion was dropped after round 1.
- Captures ran in an empty scene (flat ambient .24, fog off, inconsistent quality level), not the
  game scene. The agent picked the brush scale itself from an options sheet the owner never saw.
- 11 h after merge the owner rolled back to the round-1 state: "I actually don't like the changes
  we made after this". Reader inference: the merged look read differently live than in stills.
- Keep: ablation contact sheets (shader rewritten into hidden variants + material-ID map) for
  attributing an artifact; previous shader compiled under a new name so one capture shows
  before/after; graphics batch Unity via `RunBatch` so the owner's editor stays open; render-pixel
  tests that shipped and survived the rollback.

## Readings that cut across all eleven

1. The owner needs four distinct judgment forms and the loop broke whenever one stood in for
   another: a **sheet** (taste/shape, outside the engine), a **still** (placement, scale, colour
   in context), a **clip** (sequence and motion, with honest timing), and **flight** (feel,
   pacing, camera). Clips were asked to judge feel five times and failed each time.
2. Every session paid for the same five things again: a staged moment, a game-scale capture rig,
   a sample-data entry point, a real-time clip encode, and an evidence push. Each is past the
   rule-of-three.
3. The in-engine forms are the ones the owner actually needs and the slowest to reach (6–14 min
   per still, 7–20 min per clip, 40+ min to hand the owner a flyable editor).
4. Where the agent built a preview renderer beside the game (sector-jump window, HTML mockups,
   empty-scene captures) it drifted from the game and hid or produced defects.
5. Verification that passes while the experience is wrong: criteria encoded the agent's reading
   of the brief, not the owner's target; stills compared to the previous round, not a baseline.
