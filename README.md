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
