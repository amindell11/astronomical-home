# Orbitron and Rajdhani combat HUD verification

The six final PNGs came from the actual game in a coordinated windowed Unity test run. The runtime was main d8e33218f4326b515fec2ad54649585237d24e24 plus the font delta later committed unchanged as 2a6e142b2ce8bc993444656cb72ce24f6ac663dc. Canopy tuning PR #1262 and events PR #1264 were still open at capture time. Their visual changes are absent from these images.

## Setup

The scratch HudFontPairingScenario uses the existing PreviewHost Combat Playtest and invokes the Hangar launch button. The running images show the first sector after 1.5 real seconds, paused for legible capture. The scenario centers the observer on the player.

For the final phase and extraction states, the scenario seeds WaveDirector elapsed time to 99.5 and 100 seconds, then lets the normal objective and HUD updates run. It does not inject text or HUD state. The notification capture waits 0.8 seconds into the existing 2.05-second notification lifetime. The later open image follows another 2.3 real seconds. For eligibility, the scenario moves the player into the real extraction zone, lets physics run for 0.25 seconds, then pauses again. Locale_1 supplies the alternate background for the 720p jump image.

This is staged state coverage in actual gameplay, not an uninterrupted 100-second playthrough. The current canopy effect remains enabled and produces visible color separation and old-ink trails. Its trail is visible behind the new status during the notification. The font replacement does not alter that effect.

## Capture and tests

Command from agent-2:

```text
./scripts/agent_worktree_pool.sh run-tests agent-2 -Mode PlayMode -TestFilter "ObjectivePresentationPlayModeTests;CaptureScenarioPlayModeTests" -WithGraphics -Windowed -CaptureScenario HudFontPairingScenario
```

Final result: 4 passed, 0 failed, 0 skipped. Three are existing presentation checks and one is the scratch capture scenario. An earlier headless run of the presentation fixture passed 3/3. The capture scenario confirms native glyph import, uses PrefabUtility to author the font references, and checks actual 1920x1080 and 1280x720 output dimensions. The accompanying per-state text logs record the active assets, font sizes and Normal styles. The imported binaries independently verify static OS/2 weight 500, with upstream hashes in font-provenance.json.

The final scenario uses GameViewEpisodeCapture.Prepare directly. PrepareHud1280x720 disabled the canopy HUD by selecting compatibility rendering; the paired result is recorded in #1269. The final scenario calls the existing presentation snapshot, debug-overlay hide and restore helpers separately, preserving RenderGraph. No capture code is part of the font PR.

## Frames

- running-1080.png and running-720.png: Orbitron timer and ship values, Rajdhani caption and weapon label.
- final-720.png: the final countdown and EXTRACTION caption, with previous ink still decaying.
- notification-1080.png: centered Michroma notice plus persistent Rajdhani status.
- open-1080.png: persistent status after the centered notice has faded.
- jump-dark-720.png: persistent status and contextual jump prompt over the alternate locale.
