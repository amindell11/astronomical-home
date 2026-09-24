# Sectors

Sectors are star systems on the map with multiple [[Encounters]] inside them. Each sector has unique lore, atmosphere, difficulty, and a set of possible encounters.

Difficulty is suggested (not enforced): you can attempt anything, and you can leave if it is too hard and come back later.

## Sector win condition (baseline design)
A sector is considered “won” when the player:
1) **Acquires the Sector Objective (Key)** (intel/artifact required for progress), then
2) **Extracts** by leaving the sector through a **jump gate**.

This is designed to make **flee/extract meaningful** (not “kill everything”).

## Sector Objective (Key)
- The player’s goal in each sector is to locate a person of interest, retrieve an artifact, or acquire some piece of information.
- Sometimes the player may know exactly where to find the objective; other times they must explore and gather leads.

## Extraction rules
- Sectors have **jump gates** that allow leaving the sector.
- Extraction can be gated behind an **Extraction Challenge** (often triggered after completing the Sector Objective).

## Semi-open world
- Sectors are fully open and explorable continuous spaces.
- The map is filled with [[Points of Interest]].
- A map shows where certain encounters can be found (store, escort mission, etc.), but some encounters happen organically as you pass through an area.

## Between-sector travel reference

![[sector-transit-motion-reference.gif]]

This 0:14–0:21 excerpt suggests a future sector-travel animation: it was created by rapidly sweeping an orthographic flight camera over the tiled nebula, with the unintended streaking provisionally traced to unclamped URP camera-and-object motion blur.

## Open questions
- Early exit (before completing the sector objective): what carries forward (loot retention, story state, failure state)?
