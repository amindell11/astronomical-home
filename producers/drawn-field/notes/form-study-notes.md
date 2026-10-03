# Contour study: evidence and limitations

The preceding low-poly exploration was rejected. This pair restores the asteroid's original full-detail mesh. Fighter positions and UVs are unchanged; both sides use the same smoothed visual normals and cream/orange/violet surface treatment. The thicker outer contour remains on both sides.

## What the isolation established

The earlier outer shell changed 5,390 pixels in a 768-pixel ship render but added zero ink pixels inside the eroded silhouette. Making that shell thicker cannot create canopy or hull-overlap boundaries.

The new temporary full-screen pass detects changes in depth and normals. It produces internal boundaries in the close-up ship. Two graphics tests, using overlapping white surfaces under perspective and orthographic cameras, pass with visible internal ink and clear broad surfaces. These prove the mechanism works; they do not establish art quality.

## What remains unsuccessful

- The asteroid's fine creases become scratchy, disconnected marks.
- At gameplay scale, narrow ship details fill with ink and some edges fragment.
- Turning gameplay MSAA off reduced some fringes but did not solve the main problem. Both new clips use MSAA off for the matched comparison. The installed renderer produces single-sample depth and normals; no evidence supported a UV flip or normal-renormalization fix.
- The full-detail asteroid again displays the existing magenta capture defect tracked in issue #619. It appears in both gameplay clips, even with MSAA off. The earlier low-detail mesh hid it. Native close-up renders do not show that defect. This study does not fix it.

The conclusion is provisional: screen-space edges recover missing overlap information, but indiscriminately outlining geometric detail does not reproduce the reference's deliberate line selection. No finished art-direction match is claimed.

## Capture evidence

Both videos use the same 900 recorded simulation steps, including position, bank, asteroid quaternion and health. The packaging script requires exact equality. Each clip contains 899 captured frames at the existing 50 Hz cadence, decoded after encoding for verification. Direct 1920×1080 gameplay is followed by native 768×768 hangar/asteroid previews. Stills are direct 768×768 Unity renders on a neutral background.

See form-study-verification.json for source directories and bank ranges. Frame-dump timings are not a performance benchmark; the 1080p/60 fps target remains unverified.

Implementation is isolated to comparison shaders and editor-only scenarios on PR #691: https://github.com/amindell11/astronomical-home/pull/691. Production materials are unchanged.
