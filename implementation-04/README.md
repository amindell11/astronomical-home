# Complete core tips and soft edges

This stage fixes source-layer clipping and opaque fringes in implementation commit
`37cfb688`. The core preserves the full flash's pointed ends by compressing its outer
sections while leaving the central burst unchanged. Foreground color recovery runs
before fire/smoke selection. The atlas exporter renders at double resolution before
downsampling, and the disc warp interpolates premultiplied color.

The close-up compares the original clipped crop with the complete-tip core. The
regression check rejects that original crop and the intermediate feathered mask.
The final source-layer grid and drawing previews show the corrected extraction.

The Unity clip contains 179 frames at 50 fps, lasting 3.58 seconds. The associated
20-test PlayMode run passed. It verifies appearance and repeated playback, not
missile flight or audio.

After this capture the user requested no core rotation and no radial spoke-like
streaks. The next stage removes those; this recording remains comparison evidence.
