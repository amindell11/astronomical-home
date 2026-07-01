# Test categories

Every test fixture is tagged on **two orthogonal axes** so an agent (or CI) can
run a single feature slice instead of the whole suite.

## Axis 1 — Domain (required, exactly one per fixture)

The feature area under test. Pick the *primary* one even if a test brushes
against others (the test name carries the finer detail).

| Domain       | Covers                                                        |
|--------------|---------------------------------------------------------------|
| `AI`         | Perception + utility/state selection (Scout, UtilityChooser)  |
| `MPC`        | Model-predictive nav: solver, boost, LOS cost, navigator loop |
| `Sectors`    | Sector composition/lifecycle, manifest sync, respawn policy   |
| `Weapons`    | Weapon dispatch, heat, missile guidance                       |
| `Targeting`  | Lock-on sensor/registry wiring                                |
| `Objectives` | Objective tracker/channel, key pickup pipeline                |
| `Camera`     | Camera follow + camera utils                                  |
| `UI`         | HUD/indicator lifecycle, event-driven UI/audio                |
| `Damage`     | DamageController routing, respawn health/shield               |
| `Physics`    | Forces, fragnetics, collision-damage math                     |
| `Movement`   | Ship sim/kinematics contract (reserved — lands with sim tests)|
| `Core`       | Foundational spatial/context plumbing (GamePlane, decoupling) |
| `Services`   | Game service contracts                                        |
| `Bootstrap`  | Bootstrap/wiring contracts                                    |
| `Ships`      | Ship composition/activation lifecycle                         |

## Axis 2 — Speed (optional overlay)

| Tag     | Meaning                                                             |
|---------|--------------------------------------------------------------------|
| `Smoke` | Curated, fast, representative — the gating subset. Usually method-level. |
| `Slow`  | Multi-second wall-clock. Skip in tight iteration loops.            |

A fixture may carry `Slow` while still exposing one `Smoke` method (the smoke
test is the fast representative of an otherwise-slow suite).

## Not used

- `Regression` — dropped. Every test guards a regression; the tag carried no
  selective value.
- `Integration` — dropped. It mirrored "is a PlayMode test"; select that with
  `-Mode PlayMode` instead.

## Running a slice

The runner forwards `-TestCategory` straight to Unity's NUnit filter:

```powershell
# One domain, both assemblies
scripts/unity_test_agent.ps1 -Mode Both -TestCategory Sectors

# Fast gating subset only
scripts/unity_test_agent.ps1 -Mode Both -TestCategory Smoke

# A domain, PlayMode only, excluding slow tests is done via NUnit filter
# expressions if needed; by default -TestCategory selects by inclusion.
```

`scripts/unity_test_scopes.json` maps friendly scope names
(`-ScopeType Feature -ScopeName weapons`) to test-name regex filters as a
convenience layer; keep it in sync when adding fixtures, or prefer
`-TestCategory <Domain>` which needs no map upkeep.
