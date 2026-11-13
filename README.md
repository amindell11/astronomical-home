# App icon design history (#1517)

Everything before the locked SVGs in `art/ui/app-icon/`. Order of the rounds:

| Sheet | Round |
|---|---|
| sheet1_compositions, sheet2_styles, sheet3_colors_framing | First sketches: Vanguard's in-game silhouette (top-down from `Vanguard.fbx`) as a negative over a planet or asteroid; styles and colour pairs. |
| sheet4_disc_swoosh, sheet5_a_approaches | The disc becomes the icon; ways to build an A (swoosh, ring, contrail legs). |
| icon_pieces.ora / .xcf | Owner's first hand layout in GIMP: bigger ship, swoosh crossbar, centre spike erased. |
| sheet6_owner_layout, sheet7_rings, sheet8_stylized, sheet9_glass | Layout rebuilt as vectors; swoosh replaced by planetary rings; canopy from the `Canopy` mesh. |
| sheet10_mono, sheet11_mono, sheet12 | One-colour knockout versions; ship stays dark past the planet with a lit-side edge. |
| sheet13_ladder | Simplification ladder for taskbar sizes (parked: full detail ships for now). |
| icon_ship_edit.ora / .xcf, r12_large | Owner's second GIMP pass: simplified silhouette, inverted-V counter, new canopy. |
| sheet15_mute, r13_large | Ship and ring as one light shape with a one-sided gap where they cross (owner's mute-icon reference); dark planet, light rim. |
| sheet16_taper, r14_large | Wing outer edges tapered into straight A legs (t=1 locked). |
| final_sheet | The locked icon and mark at real sizes. |

`*.py` are the throwaway generators, run in order `sketch*.py`, `build.py` … `build11.py`;
they read the owner's GIMP layer exports and `vanguard_tris.json` (mesh triangles from
`dump_tris.py` in Blender), which are not kept here.
