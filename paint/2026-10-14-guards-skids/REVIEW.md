# Guard and skid paint alignment

The owner requested white shoulder-guard paint aligned to the plate edges and a matching scheme on the lower skids.

White leading faces and chamfer trim now surround plum inset panels on both assemblies. Face material regions follow the existing mesh edges exactly. `Bulwark Guard Bone` and `Bulwark Guard Plum` expose their colors through a Paint color node. Existing image masks remain intact; this trim is edited through face material assignment and those material colors.

Only the material slots and face assignments of `Armor - Upper shoulder guards` and `Armor - Lower skids` changed. All geometry, evaluated positions, UVs, transforms, parents, modifiers, and image pixels match the owner checkpoint. Fresh Blender reload passes. Source validation retains exactly the same 51 inherited findings; the old geometry lock remains superseded by owner edits.

![Matching guard and skid paint](guards-skids.png)

![Whole ship](ship.png)

Blender material renders, not in-game validation.
