# Test categories

The authoritative reference lives in the repo-root testing guide:
**[TESTING.md → NUnit Categories](../../../../../../TESTING.md#nunit-categories)**.

Quick version: every fixture is tagged on two axes —
- **Domain** (required, one per fixture): `AI`, `MPC`, `Sectors`, `Weapons`,
  `Targeting`, `Objectives`, `Camera`, `UI`, `Damage`, `Physics`, `Core`,
  `Services`, `Bootstrap`, `Ships` (`Movement` reserved).
- **Speed** (optional overlay): `Smoke` (fast gating subset) and `Slow`.

Run a slice with `-TestCategory <Domain>` — see the guide for details.
