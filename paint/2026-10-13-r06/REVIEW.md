# Bulwark — metal side canisters

The round side canisters now use a separate neutral pewter-gray metal material. They previously shared the cockpit's slate-blue glass material, which made the hardware read as glazing. The owner clarified their role; only the cockpit retains the blue glass coat.

## Native shader views

![Metal canisters and blue cockpit](https://media.githubusercontent.com/media/amindell11/astronomical-home/69f839d37f435e0ad05cc1769833c09d9bffc7ad/paint/2026-10-13-r06/lineup/Bulwark-concept-aligned-quarter.png)

| Previous glass assignment | Separate metal finish |
|---|---|
| ![Previous blue canisters](https://media.githubusercontent.com/media/amindell11/astronomical-home/d3b662fa9acbadc5cdb06ccdd5270019dfab134d/paint/2026-10-13-r05/lineup/Bulwark-side-quarter.png) | ![Neutral metal canisters](https://media.githubusercontent.com/media/amindell11/astronomical-home/69f839d37f435e0ad05cc1769833c09d9bffc7ad/paint/2026-10-13-r06/lineup/Bulwark-side-quarter.png) |

The canister base color is neutral gray `#999999`, with neutral shader shadows and a modest highlight. The Blender source has its own metal finish; the current illustrated Unity shader uses its existing specular control. No production shader changes are included.

## Current game fleet

![Current fleet](https://media.githubusercontent.com/media/amindell11/astronomical-home/69f839d37f435e0ad05cc1769833c09d9bffc7ad/paint/2026-10-13-r06/lineup/Bulwark-fleet-quarter.png)

![96 pixels per ship](https://media.githubusercontent.com/media/amindell11/astronomical-home/69f839d37f435e0ad05cc1769833c09d9bffc7ad/paint/2026-10-13-r06/lineup/Bulwark-fleet-96px.png)

Crimson, Vanguard, Nightshade and Valis use their current game prefab meshes and materials. Cargo is excluded. Shared cameras and lighting, with equal display size, support palette/readability comparison; these views do not represent relative gameplay sizes.

## Preserved paint and checks

Every existing paint mask is byte-identical to the preceding revision, including the broader mid-body accents, pointed nose-coat boundary and fin wedges. The new Metal material is separate from Glass, so recolouring the hardware no longer recolours the canopy.

- [Geometry lock](lock-after/ship_lock.json): pass; every geometry fingerprint is unchanged.
- [UV density](uv-after/ship_uv.json): pass, max/min 1.000032 against 2.0; every UV fingerprint matches the approved cockpit-seam layout.
- [Paint validation](paint-validation.json): all four masks unchanged, separate metal/glass assignments verified, unchanged Unity scene state and selection, no existing materials dirtied.
- [Source check](check-after/ship_check.json): the same 60 inherited findings, none added or exempted; full source validation still fails on those findings.
- [Source audit](paint-source-audit.json): SHA256 `5465bbcdc30a3ca5102717f6ad2513feaa50d2dc55223d21325dd230bb659bb2`; source revision `bf80138faf38d36d354b13cbfa6274ab76452df6`.

This material revision awaits owner review. Captures use temporary meshes and materials through the existing Unity shader. Gameplay-prefab integration, flight approval and merge remain separate.

[Editable source](Bulwark.blend) · [Palette](palettes.json) · [Finish settings](paint-settings.json) · [Previous accent review](../2026-10-13-r05/REVIEW.md) · [PR #989](https://github.com/amindell11/astronomical-home/pull/989) · [Scope](https://github.com/amindell11/astronomical-home/issues/963#issuecomment-6158148549)
