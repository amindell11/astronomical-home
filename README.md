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
