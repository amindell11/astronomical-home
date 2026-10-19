# Bulwark gameplay integration

Bulwark is a direct ShipBase variant, listed in ItemCatalog and the hangar offer. The real hangar selected Bulwark and Launch spawned Bulwark(Clone) as Player in an encounter.

- hangar.png shows the actual hangar selection and preview.
- test-summary.json: 35/35 passed, 25 EditMode anatomy/catalog checks and 10 PlayMode checks. Bulwark-specific tests exercise thrust, firing, collider ray hit on the frozen plane, painted-corner continuity at death, motion/fade/cleanup and revival.
- preservation.json: every original part's geometry, UV and material-assignment fingerprint is unchanged, and all five paint masks match the previous bytes. Only the invisible collider and four sockets were added.
- source-check-comparison.json: 51 source-authoring findings, identical before and after; zero new findings. Existing authoring cleanup is separate from this integration.
- export and debris-export are shared exporter outputs; both passed. lock is the owner-authorized texture-baseline refresh, not flight approval.

The collider is an 84-triangle symmetric silhouette prism centered on the physics plane. Initial import scale is 0.33, about 3.2 units long. Eight source-declared breakup sections use the established timing and zero soot. Default engine/shield/weapons, 800 kg and 100 HP remain a first-flight baseline; inertia is explicitly 315/210/360 and not derived from the collider.

Flight feel/scale, tank balance, dedicated ramming and owner breakup/bloom approval remain pending. No merge authorized.

Reproduction scripts are evidence, not production tools. author_source.py adds the source roles once; build.cs builds the assets through Unity. open-game.cs includes the final exhaust-depth correction before opening InitScene. Import settings preserve contour vertex separation and source material bindings. Paint baseline images remain unchanged; only geometry/UV metadata was refreshed for the nonvisual additions.
