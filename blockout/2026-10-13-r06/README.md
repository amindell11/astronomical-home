# Bulwark — paired top and side blockout review

Stage: blockout. The owner has not approved the proportions. One neutral gray material; no surface-detail stage.

## Shape changes

The earlier tail correction established armor around recessed engines, but the owner rejected the thin connections, boxy ends, and the following top-only fix's cost to the side profile.

This revision reads both concept views together:
- Long, straight outer flank angles flow into swept tail armor.
- The upper deck narrows substantially toward its rear, with broad sloping sides.
- The cockpit keeps its rounded forward bulb, with a tighter outline and sharper rear taper.
- A separately editable layer of broad side armor sits on each flank.
- The body-to-tail lower edge rises over a long diagonal, replacing the abrupt step.
- Twin engines remain recessed within the tail shielding.

The existing parts were edited in place. All Mirrors stay live. The cockpit retains its editable cage and smoothing.

## Editing groups

The original seven groups remain. Tail tips contains 05 Tail tips and its child 08 Nested engines. The added Side armor group under Body contains 09 Side armor. Both new mirrored pairs remain separately editable. There are nine mesh parts and nine live Mirrors.

## Validation

Source commit: bb303d4e679d339cfa86de898566cab5b4818195.
Source SHA256: 11f7bba2e0b0c30538daaa76538737fee15ae83fdab62828417e8390b4baf821.

Orthographic, studio and game-scale renders completed and verified an unchanged source. Top and side were inspected facing the same direction as the drawing. Closed source tail, engine and armor meshes have positive volumes.

The source check has three findings, all inherited owner-authored object scales: 02 Upper hull, 03 Lower hull, and 04 Tail booms. No transforms or modifiers were applied and no exemptions were invented. Those findings and proportion approval remain open before the blockout stage can be completed.

ReSharper skipped automatically because no C# changed. Unity tests are not applicable to this source-only art revision. The Size-S quality subagent was skipped; touched metadata was inspected locally.

[Top](top-aligned.png) · [Side](side-aligned.png) · [Rear](rear.png) · [Source check](ship_check.json) · [Edit audit](edit-audit.json)

Blender blockout previews only.
