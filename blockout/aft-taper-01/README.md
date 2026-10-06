# Nightshade — central fuselage aft taper

Source: [72828f0b](https://github.com/amindell11/astronomical-home/commit/72828f0bd5e48b26be2a322fb300526c6b5363a5). **Round 02 is the current blockout candidate; owner approval pending.**

The owner isolated the central fuselage in Blender against its side reference and identified an overly deep aft section whose upper contour drooped toward the end. The requested shape has a gradual taper and an upward finish. Round 01 overcorrected this into a pinched narrow stalk and was explicitly rejected. The owner clarified the desired continuous transition using a helicopter fuselage as the analogy.

Round 02 carries volume through the middle, tapers progressively from the body shoulder, and lets the aft centerline rise. The top-view footprint stays as the owner edited it. Only the existing hull, its aft spine armor and its end collar changed. The rear wing pair and the owner's canopy edits remain untouched.

## Current review — round 02

![Owner source versus current gradual taper](round-02/aft-before-after.jpg)
![Full ship side](round-02/review-side.jpg)
![Isometric](round-02/review-iso.jpg)

- [Current hull outline over the owner's packed side reference](round-02/aft-reference-overlay.jpg)
- [True side orthographic](round-02/material/side_orthographic.png)
- [Top orthographic](round-02/material/top.png)
- [Clay](round-02/clay/iso_front.png)
- [Game scale](round-02/scale/scale.png)
- [Sanctioned comparison](round-02/compare/compare_side.png)

The isolated before/after uses the same camera, scale and position as the reference plane. The before is the owner's saved checkpoint `63c4bbd2`. The live source matched that saved fingerprint before editing; work was applied to live parts without reloading Blender. Round 01 corresponds to rejected commit `24634362` and remains here as evidence, not an approved candidate.

## Validation and preservation

`ship_check` reports **one unchanged pre-existing finding**: unapplied scale on `Upper canopy metal bezel` `(0.658016, 0.827789, 0.658016)`. There are no new findings. No scale or modifier was applied, and no exemption was silently added. [Final report](round-02/check/ship_check.json), [baseline](precheck/ship_check.json).

[Fingerprint delta](round-02/fingerprint-delta.json): exactly three intended parts changed; no added or removed parts, no unclaimed changes. [Edit audit](round-02/edit-audit.json): all existing topology and modifier stacks retained; all changed parts retain their X/Y coordinates. Mirrors remain live.

Source SHA-256: `9443b057fe70dfb728e278ec9e71f5acacd0bca2d6fff116d66901c038f0fd8d`. Production legacy mesh, ShipLegacyList, Unity integration and the breakup-slot deferral remain untouched. No PR.

Appended to evidence parent `3c1e251915c51571d43d939873c7c4665cf16aa9` without replacing earlier rounds.
