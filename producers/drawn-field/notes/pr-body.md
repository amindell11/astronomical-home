Prototype for #685 (map #684): three matched captures let the user compare the existing fighter and asteroid against broad surface shading (A) and that shading plus selective geometry contours (B), during real banking, spin, damage/flash and native hangar inspection. The user selected B and requested thicker contours. B now defaults to 3.2 pixels (previously 1.6); the local review page retains both widths for comparison. The ticket remains open for the remaining production implications.

The editor-only scenarios reuse the capture coordinator, native ship visual rig, hangar stage and committed Environment_2. Materials are temporary clones; existing asset materials, collision roles, physics and the roster are unchanged. Two opt-in shaders live under Visuals/Shaders/DrawnComparison.

A small shared capture change is necessary for valid footage: the scenario configures the coordinator-owned camera/light, and a no-gizmo profile now disables GameView/collider guides. The first control recording exposed guides despite Profile.None; a failing graphics regression reproduced it. Root cause: capture preparation unconditionally enabled guides. Fix ladder rung 1: profile selection is carried through a required Prepare argument.

Validation:
- Final control, A and B routed PlayMode captures each passed bank, spin and damage assertions. All 900 motion samples match exactly across treatments; bank spans approximately -37 to +37 degrees.
- Thirteen capture EditMode tests and four native gizmo recovery tests passed. Plain-capture regression demonstrated red then green.
- Three 1080p MP4s decoded successfully with 899 frames each at the existing 50 Hz simulation cadence. Original PNG stills and motion traces accompany the local review page.
- ReSharper changed-line ratchet passed (9 report-only findings). Combined quality subagent found no required changes; final three-line contour attenuation is recorded below.
- These scoped tests are not full-suite merge proof. No merge is requested.

Evidence is delivered in the Codex task's local comparison page, outside the recyclable slot. Reproduce by attaching the warm capture lane, queuing DrawnControlScenario / DrawnSurfaceScenario / DrawnContourScenario, and running CaptureScenarioPlayModeTests through the routed test runner. The capture producer emits frame directories with motion.csv and rendering.txt; assemble clips with scripts/capture/assemble.py --keep-frames.

Limits and alternatives: screen-space/full-frame outlines were not added because this first comparison targets one mesh pair and existing URP wiring. A keeps authored marks and B adds light-weighted shell contours. A matched probe attenuating frontal shell expansion changes a small number of pixels but does not remove fragmented asteroid interior lines; that limitation remains visible for the user's decision. Welding source meshes, depth-bias workarounds and production rollout were not pursued. The existing meshes retain fine forms/texture detail, so a match to the reference silhouettes is not established.

The known magenta gameplay asteroid defect #619 remains. Inspection uses 768x768 textures. The initial 1080p/60 fps target is unmeasured; frame dumping and another live editor preclude a solo performance conclusion. Additional-light and quality-tier coverage are unverified. Arc #678 owns background/lighting work; recheck a chosen treatment against #681 when available.

Scope conservation: the diff is limited to prototype shaders/scenarios, their test assembly references, and the capture setup/profile fix required by observed footage. Generated editor/quality settings are excluded. B is the selected direction; production rollout is not included and #685 is not closed by this PR.

Vocab: none
