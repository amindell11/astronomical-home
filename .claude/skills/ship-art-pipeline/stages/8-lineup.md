# Ship art stage 8: lineup review

The finished ship beside every other ship at one scale, lighting and camera. This is the
owner's final approval.

## Inputs

- The finished ship and every other chassis in `ItemCatalog`.

## Commands

A lineup tool is unscheduled (#1014 took slice 8). Until one lands, the lineup is a batch
render: a static capture method dropped into the slot's `Assets/Scripts/Editor/` for the run,
launched through `scripts/unity_access.ps1 -Action RunBatch`, which is admitted while the
owner's own editor is open (that editor blocks every interactive boot). Match the capture
Crimson (#1031) and Nightshade (#1039) used, so sheets stay comparable across conversions:

1. Start from `NightshadeLineup.cs` and its launcher `nightshade_lineup.ps1` on
   `evidence/nightshade` at `836fd469`, under `paint/2026-10-19-conversion/scripts/`. The
   launcher runs `-batchmode -executeMethod <Class>.Capture -quit`; pass `-BatchLogPath` so
   the boot lane frees early. `batch_complete` exits 0 even when Unity failed: read `exitCode`.
2. The capture: every `ItemCatalog` chassis prefab at identity rotation and its prefab scale,
   hull renderers only (`ShipBaseRig/Hull/`), canvases off, particles stopped; a white
   directional key at Euler(20,-25,0), intensity 1.6, soft shadows; flat ambient 0.24; an
   orthographic camera on -Z fitted to the row; 2048×512 large and 1024×256 for game scale;
   `ShaderUtil.allowAsyncCompilation` off and two renders per readback. A second pass under
   `Assets/Scenes/Lighting.prefab`, shadows off, flat ambient (0.42,0.44,0.48), shows the
   ship under the game's light: judge there, since the key light flatters pale palettes.
3. Candidates for the owner ride the same run as in-memory clones of the imported materials,
   one labelled row each, today's prefab first.
4. Delete the method and its `.meta` from the slot before committing; the script's home is
   the ship's evidence branch beside the sheets.

## Done

- One lineup shows every ship at the same scale, lighting and camera, at game scale as
  well as large.
- The owner has approved the ship in it; the sheet is on `evidence/<name>` under
  `lineup/`.

## Owner's gate

The owner approves the ship in the lineup, in chat. A rejection names the stage to
reopen.

## Forbidden moves

- **Approval from solo renders.** The ship is judged beside the others.
