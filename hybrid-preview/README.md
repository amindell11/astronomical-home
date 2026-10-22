# Jump preview with moving cartoon skies

The owner asked to replace the outdated destination locale, remove the excessive added stars, and recover more of the original background motion. This remains the adjustable preview approved on issue #1099. It does not integrate hangar launch or sector Continue.

[Watch the MP4](hybrid-preview.mp4). The preview moves through the current illustrated blue sky, Locale_3, and fades into the copper/turquoise sky, Locale_1. Both scenes include their current cloud and distant-star layers. Additional procedural star streak brightness defaults to zero.

## Try it

In the agent-8 Unity project, open **Tools > Presentation > Sector Jump Preview**. Replay, pause, loop, scrub time, or change the heading. The profile is `Assets/Settings/Game/SectorJumpProfile.asset`. Edit it in the window and click **Save profile**.

- **Raw Background Blend** reduces the background treatment. Zero applies the full directional softening and stretch; one shows the moving locale directly. The current setting is 0.35.
- **Sky Travel Distance** controls how far the sky cameras move along the heading. The current setting is 800 world units. The incoming camera reaches its origin on arrival.
- The ship blur strength, length, falloff and timing remain independent of the background.
- Additional star streaks remain available in the profile but are off by default.

The editor window owns two isolated preview scenes and renders their actual sky layers as the cameras move. It combines those images with the independently rendered ship. HUD and ship health bars share the fade in the recorded gameplay probe. The editor window uses labeled sample bars for tuning.

`Assets/` preserves the source, profile, preview input images and Unity metadata. Restore these paths into a compatible project containing the current Locale_1 and Locale_3 scenes. The scratch `SectorJumpHybridProbe.cs` records the preview against the real HUD. It uses reflection to reach the editor preview helper without adding an assembly dependency to the capture test assembly.

## Checks and limits

The final routed graphics capture passed 1/1 with the preview window open. It checks the transparent ship surround, visible hull pixels, shared HUD/status-bar fade, HUD invisibility during travel, unchanged hull and position, and completed locale blending. This warm editor run is prototype evidence, not merge-grade coverage.

The background control changed 330,224 pixels between its endpoints. Switching ship blur off changed 5,997 pixels around the ship and zero distant-background pixels. Editor playback advanced after the capture and paused scrubbing worked. Preparation, cruise and arrival frames were inspected.

An intermediate raw-image overlay doubled the positions of moving stars. The final shader applies one sampling transform and varies its treatment strength instead, removing that duplication by construction, fix-ladder rung 1. Increasing the background sampling from 24 to 64 taps reduced visible spacing along stretched natural stars. Extreme profile settings and production shader performance still need evaluation.

The ship remains a captured hull image in this preview. Live ship capture, destination loading, player-control restoration, gameplay handoff, audio, and production integration remain outside this pass.

The last pre-authored version remains unchanged at `1ec67fdb4859a7b00742c2b9222026088d97c8b3` under `polish-v1/`. The earlier baseline remains at `475829a379b342b293bc3d2f83ffa42ec226743e` under `hud-fade/`. The first authored preview remains at `a374721411358bde1b4ee4ae656c88cf2c4fef8f` under `authored-preview/`.
