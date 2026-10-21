# Keystones

A keystone is the locked ability in a ship's keystone slot (see [[Ships]]):
each ship carries exactly one, fixed to its chassis, and the boost key fires
it. One kit per ship, no choice, retuned by playtest. The why behind this
mapping and the rules below lives on #1028.

| Ship | Keystone | Pole it owns |
|---|---|---|
| Vanguard | dash with a brief invulnerability window | protected escape |
| Bulwark | dash that deals damage on contact | commit |
| Valis | overdrive window: raised speed cap and thrust, no impulse | sustained mobility |
| Crimson | blink toward the cursor, clamped to a range | free reposition |
| Nightshade | short stealth; the first shot out of it is buffed | information |

## Rules

- **Blink preserves velocity.** Zeroing it would be a free brake, and braking
  is the hard part of Newtonian [[Flight]]; blinking against your own momentum
  is the mistake that makes it a skill. No protection during the blink.
- **Stealth does not dodge projectiles.** That would be Vanguard's window in
  disguise. Stealth is a visibility predicate: scanner, tracker, lock-on and
  missile seeker all lose the ship on the same tick.
- **The ram lands once per dash**, on first contact, and its immunity — to
  collision damage only, never lasers or missiles — ends at that instant.
  Against rocks a mass threshold decides: the Bulwark shatters rocks lighter
  than itself and bounces off the rest. The free brake into a big rock is part
  of the kit, not a leak.
- **Every keystone ships with a tell** — the player-visible cue that it is
  ready, fired or active. Anything the AI may later read off an enemy's
  keystone needs this counterpart first.

## For a sixth ship

Pairs still on the list, unassigned:

- Headstrong — divert power to the front shields (full in front, none behind)
- double dash — dash twice in succession
- Precision — root in place for more damage from every weapon for a short time
  / Reloaders — weapons charge faster, heat up slower, lock on faster for a
  short time
- Gravity well (event horizon) — pulls everything toward you
  / Relentless — speed toward enemies is boosted, away from them reduced
