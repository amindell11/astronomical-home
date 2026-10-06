# Nightshade — blockout form refinement

Source: [e7a26bd5](https://github.com/amindell11/astronomical-home/commit/e7a26bd50e464bac9858dee676116928e1b9c08d). **Owner approval pending.** This remains a blockout review with medium-detail construction cues; final detailing, UVs and paint are unfinished.

The owner found the proportions roughly right and asked for flared wingtip shapes with a restrained downward curve, chunkier and more integrated tail forks, more defined upper fins and fuselage, and a single canopy seated in a metal frame with an inner rim. The slimmer top-view fuselage was welcome. The approved Nightshade turnaround and top remain the references; side and isometric take priority.

The existing parts were edited in place. Wingtip ends flare outward from narrowed necks and have a 0.014 m added downward bend, less than one percent of ship length. The tail cross-sections are fuller, with wider roots extended into the hull shoulders and no added height at the tips. The swept fins have stronger edges and broader chisel ends; the hull has stronger angular shoulders and modest extra depth while retaining its top-view width. Each canopy is one continuous glazing piece seated within a separate metal bezel and inner seal. Review materials are provisional.

## Review

![Nightshade isometric](round-02/review-iso.jpg)
![Nightshade side](round-02/review-side.jpg)
![Approved Nightshade concept comparison](round-02/nightshade-concept-comparison.jpg)

- [Clay](round-02/review-clay.jpg)
- [Top](round-02/review-top.jpg)
- [Orthographic and turnaround sheet](round-02/sheet/sheet.png)
- [Game scale](round-02/scale/scale.png)
- [Before and after — top](round-02/compare/compare_top.png)
- [Before and after — side](round-02/compare/compare_side.png)

## Validation and source preservation

`ship_check` passes with zero findings and zero exemptions: [final report](round-02/check/ship_check.json). Source SHA-256: `e27684748405e3f015809aaff7f0e716ebaeab8718fd9a055ec8dee083c83d4f`.

The [fingerprint delta](round-02/fingerprint-delta.json) records 12 intended existing parts changed, four new bezel/seal parts, no removed parts and no unclaimed changes. All original vertices, faces and live modifier stacks were retained; no parts were regenerated or merged and no modifiers were applied. The [first edit audit](round-01/form-edit-audit.json) and [wingtip follow-up audit](round-02/tip-edit-audit.json) record those checks.

The owner confirmed saving before this pass. The source matched committed `31d200fc`; that committed file supplies the before comparison. Intermediate geometry was checkpointed at `9ffe4cd3` before the last wingtip adjustment. Round 01 is the initial refinement; round 02 is the review candidate.

Production legacy mesh, ShipLegacyList, Unity integration and the existing breakup-slot deferral are unchanged. No PR. Appended to evidence parent `b46faf26b84bb17686bf70577a2b8d966f862548` without deleting or replacing previous rounds.
