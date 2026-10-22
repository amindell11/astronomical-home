# Adjustable authored jump preview

The owner approved the adjustable preview first on 2026-10-21, before hangar launch or sector Continue integration. The brief and decisions live on issue #1099. This is a visual prototype, not a production transition or a merged feature.

[Watch the MP4](authored-preview.mp4) for the color and motion reference. The reduced GIF has visible palette banding in the nebula gradients and should not be used to judge rendering glitches.

## Try it

The working preview is in the agent-8 Unity project. Open **Tools > Presentation > Sector Jump Preview**. Replay, pause, loop, scrub time, or change the heading. The profile is `Assets/Settings/Game/SectorJumpProfile.asset`; edit it in the window and use **Save profile**. New profiles can be created through **Create > Game > Sector Jump Profile**.

`Assets/` preserves the complete preview source, profile, baked locale and ship inputs, and Unity metadata. Copy these paths into a compatible project to restore the prototype. The scratch `SectorJumpAuthoredProbe.cs` records the real HUD and ship status bars using the same shader/profile. The editor window shows labeled sample bars for quick tuning without Play Mode.

## What changed and why

The prior experiment moved the real ship and camera far through the locale and used URP motion blur. The owner reported glitches in both background and ship, while wanting to keep adjustable ship blur. This preview samples the actual outgoing and incoming locale images independently, draws continuous directional streaks, and composites the hull with a separate configurable trail. No temporal scene history is involved.

The first ship bake accidentally included the enlarged minimap marker. Selecting the existing hull silhouette list removes that cause by construction (fix-ladder rung 1). A later arrival frame exposed clamped image borders. The sampling transform now fits its full blur footprint inside the source image and returns drift to zero on arrival (rung 1). An open editor window lost its temporary material across Play Mode; explicitly owned HideAndDontSave resources now survive until the window releases them (rung 1).

## Evidence and limits

The final routed graphics capture passed 1/1 with the preview window open. It checks transparent ship surround, visible hull pixels, shared HUD/status-bar fade, HUD invisibility during travel, unchanged hull and position, and completed locale blending. Raw preparation, cruise and arrival frames were inspected. The warm editor run is prototype proof, not merge-grade coverage.

The blur control changed 6,018 ship-region pixels and zero distant-background pixels. Editor playback advanced after the capture, its resources remained alive, and time could be set while paused. A read-only quality review found no further material preview issues.

For these particular raw captures, mean background RGB change across frames 85-105 fell from 36.87 to 1.54 (0-255 scale); the largest adjacent-frame mean fell from 77.82 to 1.58. This describes the change from chaotic background movement to calmer authored motion. It is not a general smoothness score and does not prove all profile settings artifact-free.

The preview uses one baked ship and two baked locales. Live capture at launch, destination loading, player-control restoration, gameplay handoff, audio, and production shader performance remain outside this approved pass. The authored smear is deliberately visible and adjustable; whether this visual direction feels better remains the owner's call.

The pre-authored version is unchanged at commit `1ec67fdb4859a7b00742c2b9222026088d97c8b3`, under `polish-v1/`. The earlier baseline remains at `475829a379b342b293bc3d2f83ffa42ec226743e`, under `hud-fade/`.
