# Audio pipeline

> STATUS: living — branch-triggered reference, read before audio work (FMOD, anything under `audio/`, code touching `FMODUnity`); pointed at from `AGENTS.md`. Arc: #987.

The owner designs every sound in FMOD Studio; agents wire events and parameters. The Studio project is `audio/fmod/` (`Astronomical.fspro`), its built Desktop banks are committed under `audio/fmod/Build/`, and `audio/fmod/Build/GUIDs.txt` is the export of every event, bus and parameter path the banks hold.

## Studio and `fmodstudiocl`

- **`fmodstudiocl` runs only while no `FMOD Studio.exe` process is running.** On an open project it deletes Studio's unsaved-changes backup, and Studio never sees the CLI's edits. Check the process list first.
- **Agent scripts live in `audio/tools/`, never `audio/fmod/Scripts/`.** Studio runs every script in that folder on each project load; `audio/tools/scaffold_laser_fire.js` is the model.
- **`-script` persists only what `project.save()` writes.** A script that throws before the save leaves the project untouched: validate first, save last.
- **Banks are built by the CLI and committed.** A *new* bank also needs an editor bank refresh and a commit of `Assets/Resources/FMOD/FMODStudioSettings.asset`: the runtime loads banks from the `MasterBanks`/`Banks` name lists stored there, so an unlisted bank silently never loads in fresh slots and CI.
- **Pre-create events and parameters with `-script`**, so code and Studio agree on event paths and parameter names before the owner designs the sound.

## The FMOD Setup Wizard

Decline every page and close it. "Disable built in audio" silences every sound not yet ported (it waits for the cut-over slice), and the wizard can switch Source Type away from "Single Platform Build" (`../../audio/fmod/Build/Desktop`). The committed settings hide it; when it reappears, close it and check Source Type before committing `FMODStudioSettings.asset`.

## Runtime rules

- Prefabs reference events through `EventReference` (an `event:/` path plus its GUID), so FMOD's picker works locally. `FmodWiringEditModeTests` checks every referenced path against `GUIDs.txt` on the hosted suite, where the stub cannot; a Studio rename therefore fails CI, not the game.
- A 3D event started without `set3DAttributes` goes virtual: silent, with no error. `WeaponFireAudio` sets them at the fire point.
- Batch `-executeMethod` tooling runs before FMOD's editor startup: call `FMODUnity.EventManager.Startup()` before `EventReference.Find`.
- Live Update is on in every editor run (port 9264) and cannot be switched off per run: FMOD fixes its init flags before the platform callback handler runs. A port collision restarts FMOD without it, harmlessly.
- `BatchModeAudioOutput` (the Default platform's callback handler) selects `NOSOUND` in batch mode; `ASTRO_FMOD_OUTPUT=<FMOD.OUTPUTTYPE>` overrides it for one run.

## The FMOD stub

`Packages/manifest.json` names the real package by its local vendor tarball; the hosted runner cannot reach that path, so `headless-suite.yml` points the manifest at `src/fmod-stub/` before Unity boots. The stub compiles and never plays: only the FMOD types the game touches, every method a no-op, package version `0.0.1` so `ASTRO_FMOD` (the real-package define) stays off.

- **A new FMOD member the game uses → the same member in the stub**: same type, namespace and signature, no-op body, copied from the vendor package in `D:\amind\git\vendor\fmod\fmodstudio20400.tgz`. The compiler enforces it: the hosted suite fails to compile until the member exists.
- **A new FMOD component placed on a prefab → its stub `.cs.meta` carries FMOD's real script GUID** (from the same tarball). `FmodWiringEditModeTests` fails on the missing script otherwise.
- The stub grows when the hosted suite demands a member, never ahead of use.
- A parameter rename in Studio is caught only by the local evidence test (`LaserFireAudioPlayModeTests`, compiled under `ASTRO_FMOD`): run it after renaming.

## Verifying sound

- **Batch evidence**, with the real package: `run-tests <slot> -Mode PlayMode -TestCategory Audio`. The evidence test logs `[FMOD] … output=NOSOUND`, one instance `PLAYING`, and the `Heat` parameter equal to the weapon's heat fraction.
- **Audio evidence**: `ASTRO_FMOD_OUTPUT=WAVWRITER` writes `fmodoutput.wav` into the Unity project folder (gitignored; fixed name, so one run at a time). The writer mixes in realtime, so a test must keep the event alive for its length.
- **By ear**: play in the editor and tune in Studio through Live Update. Live Update edits persist only after save, bank build and a play-mode restart.
