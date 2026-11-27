# Shoal region plan

Reviewed the approved `*-codex-1.png` sheets (all four views) and all six flat references per ship as images. The real model boundaries and the reference color blocks agree with the concepts. No new region is needed.

Region names are legacy slot identifiers, not color names. All seven base materials receive the brief's linear RGB palette; elite Lavender uses #ffefa9. No magenta, weathering region, face split, geometry, UV, modifier, or transform edit is introduced.

## ValisMinion

Complete assignment: 12 visual parts, all in role.hull; each has exactly one region. See region-plan.json for every part and its view evidence.

No region assignment changes. The existing assignments already match the references and the approved concept.

Engine/frame separation confirmed: `60 engine spine` stays Engine; `01 central fuselage` and structural spars/housings stay Graphite. Rust raised ribs and bone side fairings are armor on the engine, not the engine-housing region.

Deliberately excluded: the top and side concept's subdivided wing/cheek plates, rivets and scuffs are value detail, not region boundaries. The top-view wing surfaces remain uninterrupted rust; bone tips belong to the existing underside pieces visible in the back reference. No verdigris is added. The thin bright side outlines are treated as edge light over existing surfaces, not an orange stripe region.

## ValisRival

Complete assignment: 26 visual parts, all in role.hull; each has exactly one region. See region-plan.json for every part and its view evidence.

No region assignment changes. The existing assignments already match the references and the approved concept.

Engine/frame separation confirmed: `60 engine spine` stays Engine; `01 central fuselage` and structural spars/housings stay Graphite. Rust raised ribs and bone side fairings are armor on the engine, not the engine-housing region.

Deliberately excluded: top/side/three-quarter painted panel subdivisions do not create parts. Tiny cream-colored nicks scattered across rust or verdigris are treated as value-only wear, not additional bone marks. The underside gray planes and bone tip pieces remain as modeled even where the concept's back view simplifies their boundaries. No colored decal or consistently located new marking is shown.

## ValisElite

Complete assignment: 32 visual parts, all in role.hull; each has exactly one region. See region-plan.json for every part and its view evidence.

| Part | Current | Planned | Concept evidence |
|---|---|---|---|
| 40 forward outriggers.001 | Ivory.001 | Ivory | Top, side and three-quarter: rust forward outrigger plates around the canopy and outer shoulders. Fold the duplicate into its base region; no distinct color is shown. |
| 70 shoulder pivot housing.001 | Graphite.001 | Graphite | Top and three-quarter: dark circular housing around the bone lamp cap. Fold the duplicate into its base region; no distinct color is shown. |
| 71 shoulder lavender cap.001 | Lavender.001 | Lavender | Top and three-quarter: bone round lamps; elite uses the brighter bone palette. Fold the duplicate into its base region; no distinct color is shown. |

The actual file has three affected visual parts (listed above), not four. Material datablocks are remapped globally and all `.001` materials removed; object names ending in `.001` are preserved because these are distinct real parts.

Engine/frame separation confirmed: `60 engine spine` stays Engine; `01 central fuselage` and structural spars/housings stay Graphite. Rust raised ribs and bone side fairings are armor on the engine, not the engine-housing region.

Deliberately excluded: the concept's top-view green panels are divided into many invented plates; keep the real upper-wing planes and lower blades whole. Cream flecks along green/rust edges in all views become value-only highlights, not a new wear region. The side and back views simplify/reshape some wing overlaps; preserve real parts and transforms. Brighter bone, existing extra green panels and orange sockets provide rank emphasis; no new hue.

## Owner review

No decision blocks this region pass. One optional art-direction decision remains for a later paint pass: some concept chips/highlights look bone-colored over rust or verdigris. This delivery follows the hard value-only rule and treats them as lighter values of their underlying region. A deliberate, repeatable colored marking would need explicit region coverage; none is sufficiently consistent across the approved views to warrant adding one here.

The brief calls the middle-right angle “three-quarter rear”; the supplied `turn225` reference and all approved sheets show the nose/cheek side at that angle. Comparison sheets use `turn225` to match the images. The house sheets retain both `turn045` and `turn225` for inspection.
