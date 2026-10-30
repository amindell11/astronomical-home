# Hangar blockout at the final camera

Open [the review page](index.html) for the composition, preserved preview flights and seven-panel arrival strip. [hangar-blockout.blend](hangar-blockout.blend) is the editable source, saved at frame 46 in Blender 5.1.

This pass follows the October 30 [hangar premise](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6517317847), [spec sheet](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6518561665) and [framing ruling](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6518926277). It replaces the first blockout for review. No paint-over has run.

## Edit the layers

The single camera establishes the 1920 by 1080 composition. The scene contains flat editable polygons, curves and native text at successive depths. It has no room, legs or pedestal. The screen faces are parallel to the camera. They represent the future uGUI layout; this Blender mockup does not instantiate Unity UI.

Collections run from space through the dock edge, arrival lane, hologram and projector, console frame, two displays and Call control. The frame has 40-degree chamfers, visible dock anchors, cable curves and a separate projector housing. The main console runs beyond the left and bottom crop. Edit its polygon vertices or move the named parent objects in camera view.

Frames 1 to 180 retain the real preview-flight sequence. Review Valis at 46, Nightshade at 106 and the return at 166. The selected ship's labels and six bars follow the captured selection changes. The hologram's opacity is a keyed custom property. The next 210 frames hold the selected ship for the arrival storyboard.

| Frame | Arrival beat |
| --- | --- |
| 181 | Idle with the hologram visible |
| 211 | Call pressed |
| 241 | Hologram powering down |
| 271 | A light in deep space |
| 301 | The real hull banks past the console edge |
| 331 | Glitch cut |
| 361 | Canopy HUD powers on |

The arrival uses a scaled and banked capture of the real Valis hull. It is a storyboard proposal, not a new flight implementation or a continuous camera move. The camera stays fixed; the console and deck occlude the approaching hull. The final panel uses the combat session's existing canopy review. It illustrates the destination without making new combat HUD choices.

Fonts and still images are packed. Keep `inputs/preview-flight/` beside the blend file. After the owner edits the file, that file is the source of record. The builder refuses to overwrite it.

## Data and provenance

`inputs/spec-data.json` records the authored values and sources at commit `4d23b8e5aaf0c9e085758648d1151f6793af022b`. The six ships in ItemCatalog set the fleet maxima. The bars divide each value by its fleet maximum, with a provisional five-percent minimum. Acceleration is engine thrust divided by ship mass. Non-integer readouts round to two decimals; the data file retains their full precision.

| Stat | Valis | Nightshade | Fleet maximum |
| --- | ---: | ---: | ---: |
| Hull | 80 | 85 | 400 |
| Shield capacity | 50 | 45 | 150 |
| Shield recharge per second | 10 | 15 | 18 |
| Top speed | 32 | 26 | 32 |
| Acceleration | 10 | 8.142857 | 10 |
| Turn, degrees per second | 220 | 210 | 240 |

Engine, shield and keystone are fitted parts without cyclers. Valis has Overdrive with a 9-second cooldown; Nightshade has Stealth with an 8-second cooldown. The weapon lines use authored projectile damage, cooldown, heat and ammunition settings. Ship and weapon prose is visibly marked as placeholder.

Plex Sans and Plex Mono come from the combat session's shared files. Plex Mono readouts follow the explicit spec-sheet ruling. Weapon glyph shapes and the provisional keystone diamond reuse the shared canopy geometry. The diamond does not propose a new ability glyph. The active combat font revision does not select new menu typography.

The 180 flight frames are unchanged copies of the shipped [PR 1076](https://github.com/amindell11/astronomical-home/pull/1076) preview capture used by the first blockout. Its [capture provenance](../hangar-blockout-01/README.md) still applies. The background is an empty crop of the real hangar capture. All grey hardware and screen layout are new Blender geometry. Lighting C remains the later paint-over reference; these flat grey values are for layout review.

The owner reviews the composition and arrival strip before any paint-over. No game code, game assets, shared tuner files or primary-tree files changed. No game merge is proposed.
