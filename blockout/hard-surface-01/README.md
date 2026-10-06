# Nightshade — hard-surface revision

Source: [31d200fc](https://github.com/amindell11/astronomical-home/commit/31d200fc4631c3b6429f5e7809fb57a8b748a445). **Owner approval pending.**

The owner found the shape roughly right but the surfaces too sculpted. Nightshade's organic profile still needs the fleet's hard-surface mechanical construction: mostly angular surfaces with deliberate key curves. The approved Nightshade top and turnaround remain the shape references; side and isometric views take priority.

This revision edits the existing clean parts in place. Broad wing and small-fin faces are planar sections with narrow geometric chamfers. The aft hull has angular cross-sections; central plates use flat decks and defined bevel bands. Existing seams were moved onto the revised plates. The nose, canopy, and tail curves remain. This is a geometry change, not only a shading switch.

## Review

![Nightshade reference comparison](round-01/nightshade-concept-comparison.jpg)
![Surface before and after](round-01/surface-before-after.jpg)
![Current isometric](round-01/review-iso.jpg)

- [Side](round-01/review-side.jpg)
- [Clay](round-01/review-clay.jpg)
- [Orthographic and turnaround sheet](round-01/sheet/sheet.png)
- [Game scale](round-01/scale/scale.png)
- [Committed source comparison](round-01/compare/compare_side.png)

## Validation and preservation

- `ship_check` passes with zero findings or exemptions: [report](round-01/check/ship_check.json).
- Source SHA-256: `3c54c6e3bb4654ba09ba4a1da91410e27bd29218022030f60a53e286fcad9bd5`.
- [Fingerprint delta](round-01/fingerprint-delta.json): 13 intended parts changed, no added or removed parts, no unclaimed changes. Canopy and tail geometry unchanged.
- [Surface edit audit](round-01/surface-edit-audit.json): every original face/vertex index and part retained; no regeneration, merged parts, or applied modifiers. Mirrors remain live.
- The owner confirmed saving before this pass. The saved source matched committed `7892d87b`; the comparison uses that committed file.
- Only the art source changed in this pass. Production legacy mesh, Unity integration, ShipLegacyList, and the breakup deferral remain untouched. No PR.

Appended to evidence parent `0b526bcd1a68367421d6f91571ebae141704b868` without deleting or replacing previous rounds. Material colors remain provisional.
