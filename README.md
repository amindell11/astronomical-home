# Ripper bullet emphasis

The owner requested a much dimmer trail and a slightly brighter bullet so the pale-gold Rippers read less like streaks. This follow-up changes only the native drawing, two palette colors, and the regenerated projectile atlas.

The six variants retain their geometry, 0.60–0.75-unit lengths, selection, and gameplay. Both trailing layers use one fifth of their earlier opacity, without the soft halo. The detached fleck is subdued. The body and core remain pale gold with a modest brightness increase.

Across all six frames, mean alpha-weighted linear source luminance is about 5% higher in the head and 87% lower in the trail. The verification file defines the sampled regions. Both impact atlases are byte-identical. The before/after image compares the previous atlas from commit `682fe0532566dc2fdfbaff46a4b092c6fa50c161` with the new bake at enlarged and reference size.

This is a baked-texture comparison, without game postprocessing. The owner's main Unity Editor was open, so it remained untouched. A separate coordinated batch imported and built the update; its receipt is included. The previous game-camera proof is preserved at evidence commit `97a102acec0c24043a9bd586a8c3519063211a42` and predates this brightness adjustment.
