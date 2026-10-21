# Audio pipeline

> STATUS: living — branch-triggered reference, read before audio work (FMOD, anything under `audio/`, code touching `FMODUnity`); pointed at from `AGENTS.md`. Arc: #987.

The owner designs every sound in FMOD Studio; agents wire events and parameters. The Studio project is `audio/fmod/` (`Astronomical.fspro`), its built Desktop banks are committed under `audio/fmod/Build/`, and `audio/fmod/Build/GUIDs.txt` is the export of every event, bus and parameter path the banks hold.

## Where sound work happens

Sound work happens in the primary tree, on main, as uncommitted edits beside the owner's (owner ruling, 2026-10-19): the Studio project, the clips under `audio/fmod/Assets/`, the scaffolds and banks, and the C# that wires events. Studio, Ableton and the owner's Unity editor keep fixed paths there, and Live Update plays the code the owner is hearing.

- **Main stays checked out.** Every session runs the pool script from the primary tree and pulls main there after each merge, so a sound branch there would be switched out from under the open Studio and editor.
- **C# compiles in a slot first**, then is copied in: the owner's editor recompiles on focus, and a compile error there blocks play mode.
- **Scaffolds and bank builds run on the primary tree's project**, with Studio closed, so the primary tree stays the one source of the design.
- **Ship from a slot.** Copy the sound paths into a slot and take them through the normal loop; the evidence tests run there. A clip ships when the project imports it (`audio/fmod/Metadata/AudioFile/`).
- **After the merge,** run `./scripts/agent_worktree_pool.sh sync-primary`. Anything that still differs from main stays the owner's uncommitted edit.
- **The stray report after each sync counts untracked sound files as this work in progress**, not strays; they wait for the ship.

## Studio and `fmodstudiocl`

- **`fmodstudiocl` runs only while no `FMOD Studio.exe` process is running.** On an open project it deletes Studio's unsaved-changes backup, and Studio never sees the CLI's edits. Check the process list first.
- **Agent scripts live in `audio/tools/`, never `audio/fmod/Scripts/`.** Studio runs every script in that folder on each project load; `audio/tools/scaffold_laser_fire.js` is the model.
- **`-script` persists only what `project.save()` writes.** A script that throws before the save leaves the project untouched: validate first, save last.
- **Banks are built by the CLI and committed.** A *new* bank also needs an editor bank refresh and a commit of `Assets/Resources/FMOD/FMODStudioSettings.asset`: the runtime loads banks from the `MasterBanks`/`Banks` name lists stored there, so an unlisted bank silently never loads in fresh slots and CI.
- **Pre-create events and parameters with `-script`**, so code and Studio agree on event paths and parameter names before the owner designs the sound.

## The FMOD Setup Wizard

Decline every page and close it. "Disable built in audio" silences every sound not yet ported (it waits for the cut-over slice), and the wizard can switch Source Type away from "Single Platform Build" (`../../audio/fmod/Build/Desktop`). The committed settings hide it; when it reappears, close it and check Source Type before committing `FMODStudioSettings.asset`.

## Runtime rules

- Prefabs reference events through `EventReference` (an `event:/` path plus its GUID), so FMOD's picker works locally; code names a parameter through a `const string …Parameter = "Name"`. `FmodWiringEditModeTests` checks every referenced event path, and every such constant as `parameter:/Name`, against `GUIDs.txt` on the hosted suite, where the stub cannot; a Studio rename of either therefore fails CI, not the game.
- A 3D event started without `set3DAttributes` goes virtual: silent, with no error. `WeaponFireAudio` sets them at the fire point.
- A sustained instance on a moving object is attached with `RuntimeManager.AttachInstanceToGameObject`, so FMOD moves it with the object every frame; `ChargeWeaponAudio` attaches to the weapon and detaches when it stops the instance.
- Doppler needs a velocity on the instance. Attached through the `Rigidbody` overload, the instance gets that body's `linearVelocity` from `RuntimeManager` every frame (`MissileAudio` passes the missile's); the plain `GameObject` overload sends position and orientation only, and its `nonRigidbodyVelocity` flag derives velocity from frame-to-frame motion, clamped to 20 units/s. A one-shot placed with position-only 3D attributes (`PlayOneShot(event, position)`, `position.To3DAttributes()`) has no velocity.
- A charge weapon's event is keyed off on the shot (`keyOff`), never stopped: the timeline runs on from its sustain point into the release. Any other return of the charge-up to zero stops it with fade-out.
- The camera's `StudioListener` sits on `ListenerPanPoint` and attenuates from `ListenerPlaneAnchor`, the point on the game plane beneath the camera. The pan point's height scales with the orthographic zoom, so a sound at the screen's side edge pans the same at every zoom. Event ranges are in plane units: FMOD's default 1–20 silences the zoomed-out screen edge, about 30 units from centre, so a new 3D event takes the weapon events' max distance of 115.
- Batch `-executeMethod` tooling runs before FMOD's editor startup: call `FMODUnity.EventManager.Startup()` before `EventReference.Find`.
- Live Update is on in every editor run (port 9264) and cannot be switched off per run: FMOD fixes its init flags before the platform callback handler runs. A port collision restarts FMOD without it, harmlessly.
- `BatchModeAudioOutput` (the Default platform's callback handler) selects `NOSOUND` in batch mode; `ASTRO_FMOD_OUTPUT=<FMOD.OUTPUTTYPE>` overrides it for one run.

## The FMOD stub

`Packages/manifest.json` names the real package by its local vendor tarball; the hosted runner cannot reach that path, so `headless-suite.yml` points the manifest at `src/fmod-stub/` before Unity boots. The stub compiles and never plays: only the FMOD types the game touches, every method a no-op, package version `0.0.1` so `ASTRO_FMOD` (the real-package define) stays off.

- **A new FMOD member the game uses → the same member in the stub**: same type, namespace and signature, no-op body, copied from the vendor package in `D:\amind\git\vendor\fmod\fmodstudio20400.tgz`. The compiler enforces it: the hosted suite fails to compile until the member exists. An overload whose extra parameter is a `UnityEngine.Object` is the exception: it binds silently to a `bool` overload (`Object` converts to `bool`), so add that stub overload by hand.
- **A new FMOD component placed on a prefab → its stub `.cs.meta` carries FMOD's real script GUID** (from the same tarball). `FmodWiringEditModeTests` fails on the missing script otherwise.
- The stub grows when the hosted suite demands a member, never ahead of use.

## Verifying sound

- **Batch evidence**, with the real package: `run-tests <slot> -Mode PlayMode -TestCategory Audio`. The evidence test logs `[FMOD] … output=NOSOUND`, one instance `PLAYING`, and the `Heat` parameter equal to the weapon's heat fraction.
- **Audio evidence**: `ASTRO_FMOD_OUTPUT=WAVWRITER` writes `fmodoutput.wav` into the Unity project folder (gitignored; fixed name, so one run at a time). The writer mixes in realtime, so a test must keep the event alive for its length.
- **By ear**: play in the editor with the Game view's Mute Audio off (FMOD mutes its master bus while `EditorUtility.audioMasterMute` is set, batch runs included) and tune in Studio through Live Update. Live Update edits persist only after save, bank build and a play-mode restart.
