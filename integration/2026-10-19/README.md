# Vanguard runtime substitution

Approved source SHA256: e4c3dbf9552873ddf0a461f361d59245da13edf76208f27b0f0e56b3920fa031.
The eight Unity textures are byte-identical to the approved clean-canopy-rim bundle.

`authoring/verification.json` checks every retained transform, unchanged nonvisual prefab overrides, matching hull/canopy/core bounds, the fourteen breakup pivots, unchanged animation, and source/texture identity. The temporary Editor producers import and bind the canonical role export, rebuild saved breakup meshes in place, and restore decorative ink's disabled shadow casting. They are not runtime code.

The breakup export assigns ownership temporarily in memory; it never saves or changes the approved Blender source. Existing flat ink submeshes, motion, piece identities, and soot behavior remain. The retired runtime assets are preserved in the repository at main commit 0a8d3dd6391393918a5fc056a68d2f69113ad505; AssetDatabase confirmed no retained asset depended on them before deletion.

The initial direct contour-test invocation reproduces its one-surface-slot assumption. The corrected assertion locates the contour material and checks the sum of all surface submeshes. The namespace compilation log records the one exact-name conflict fixed with a global namespace qualifier.

Flight check was waived and interactive Unity review deferred by the owner. This evidence proves asset import and structural preservation; it does not claim an in-engine visual review.