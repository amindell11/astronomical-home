# Hangar flight motion studies

Actual Unity renders of the preview rigs, with Valis using its gameplay WingVisuals driver. Isolated presentation studies; no gameplay or performance claim.

- v1: initial side-facing study.
- v2: user-directed nose toward camera, slightly yawed left and banked; arrival approaches from depth.

Timeline (18 seconds): Valis arrival and idle 0–8 s; normal switch at 8 s; rapid selections 12–12.6 s; Valis idle through the end.

Prototype source: task/hangar-flight. Temporary capture source included here for repeatability; it is not a permanent test. Both capture runs passed and each video decodes to 540 frames at 30 fps. Production integration and PR review await motion feedback on issue #1021.

## Switching alternatives

Each seven-second study starts on Valis, switches to Nightshade at 1.5 seconds and back at 4.5 seconds. Same frontal pose and idle; only the transition differs.

- A / formation: a 0.85-second exchange, the outgoing ship peels left and back while the next arrives from the right.
- B / dissolve: a 0.35-second crossfade in place.
- C / bank cut: a 0.48-second roll, exchanging hulls at the most banked pose.

These are scratch render studies, not three new production switching modes. The shared prototype source remains at 97a491f30df06f16f73f33856274040532274bd1. The temporary capture fixture is included alongside the videos; its settled-frame visibility checks passed. Valis wings still use the gameplay driver. Dissolve and layering are composited from actual Unity-rendered textures for the study.

## Selected A: integrated runtime capture

The user selected A on issue #1021. `hangar-flight-final.gif/mp4` is an 18-second capture of the actual HangarPreviewStage and HangarPreviewFlight implementation at feature commit faebc81d (including main's updated Vanguard art). Unlike the A/B/C studies, this uses one runtime stage without compositing. It shows opening depth arrival, idle yaw/bank and Valis gameplay wings; a regular formation handoff at 8 seconds; and rapid selections at 12, 12.3 and 12.6 seconds. Both ships retain authored scale; the camera stays fixed across the offered ships.

Graphics checks: 8/8 passed, including the capture's non-uniform pixel assertion, real wing sweep/return, view direction, authored scale, left-out/right-in handoff, bounded rapid switching, relative sizing, render budget, input gating, and launch camera restoration. UI regressions: 32/32 passed. Machine-readable summaries and NUnit results are in test-results/.

The final MP4 decodes to 540 frames, 18 seconds at 30 fps. The captured surface is the production hangar preview texture with a dark backdrop for review; it does not show the surrounding menu or claim performance results. Temporary capture source is included for repeatability and is excluded from the feature PR.

## Back-and-forth stress preview

`hangar-back-and-forth.gif/mp4` captures the unchanged runtime at 9379a1286f82a78e1bf7a5b671a9aba4b3391466, alternating Valis and Nightshade. The 14-second clip shows initial arrival (0–2 s), ten switches spaced 0.3 seconds apart (2–5 s), a pause (5–7 s), thirty switches spaced 0.1 seconds apart (7–10 s), then the final ship settling (10–14 s). The top strip is an encoding-time caption; the ship texture is the actual Unity render.

Observed limitation: sustained 10 Hz switching removes the older departure and interrupts each incoming hull before it reaches the visible frame, leaving the preview empty until tapping stops. At 0.3-second spacing the interrupted ships stay near the right edge and visibly cut short. This clip deliberately preserves the current behavior for the user's decision; no runtime changes were made.

The capture passed its settled-frame visibility checks, at-most-two live previews after each frame, and final-selection assertion. Those assertions do not promise continuous visibility during spam; the empty interval is a visible limitation. MP4 decode: 420 frames, 14 seconds, 30 fps. The scratch capture fixture is included for repeatability.

## Direction-aware handoff

`hangar-directional.gif/mp4` captures the actual HangarScreen arrow handlers and preview runtime at feature commit 482f3f5bb3f40f9c4c63b90ba369ad4fffedc3d6. Next brings the incoming ship from the right and sends the old one left; Previous mirrors the path, yaw and bank. Direction follows the pressed arrow, including list wrap. The opening depth swoop, frontal idle and gameplay Valis wing driver are unchanged.

The 17-second clip shows opening Valis (0–2 s), Next at 2 s, Previous at 4 s, alternating arrows every 0.3 seconds (6–9 s), pause (9–11 s), alternating arrows every 0.1 seconds (11–14 s), then final settling (14–17 s). The capture uses the real HangarScreen with Valis and Nightshade adjacent in its in-memory offer. Only the header is added during encoding; the preview texture is the actual Unity render.

The sustained 10 Hz burst still exposes an empty interval while arrivals are repeatedly interrupted offscreen. This follow-up fixes direction only and preserves that separate visibility limitation for feedback. The test asserts bounded live previews and correct final selection, not continuous visibility during spam.

The actual Next/Previous button regression failed on the old runtime because Previous entered from the right, then passed after the direction change; both list-wrap directions are covered. Focused graphics PlayMode checks passed 9/9, including the temporary capture. Machine-readable before/after results and NUnit XML are in test-results/. The MP4 decodes to 510 frames, 17 seconds at 30 fps. The source fixture is included for repeatability and is excluded from the feature PR. These renders do not show the surrounding menu or establish performance.
