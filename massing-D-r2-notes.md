# Cockpit D / round 2

Rebuilt around the planes: inward-canted folded display faces, deep faceted shoulders, a continuous rising dash, and ribs that land on forward dash shoes. All geometry uses flat colours and explicit polygons. No textures, model text or bevel modifiers.

## Camera and angles

The fixed camera is unchanged: position (0.0, -18.0, 6.8), direction (0.0, 0.9936, -0.1132), vertical field of view 34.11 degrees, 1920 x 1080.

Left pod: yaw +58 degrees, lean back 40 degrees, bank -20 degrees. Right pod: yaw -58 degrees, lean back 40 degrees, bank +20 degrees. Positive yaw turns the left face inward; bank raises the outer upper corners. Yaw and lean define the initial axes, then bank rotates the plane axes. Each lower screen rotates an additional 8 degrees about its banked width axis, so it kicks forward at the bottom. These are explicit local construction angles, not Blender Euler values. Housings extend 2.15 units horizontally rearward, with a 0.62-unit chamfered shoulder; the extrusion is horizontal so the top caps remain visible.

## Screen checks

| Screen | True size | Width retained | Height retained | Visibility rays |
|---|---:|---:|---:|---:|
| SHIP | 4.25 x 3.50 | 88.95% | 81.85% | 1341/1341 clear |
| DETAIL | 3.76 x 0.92 | 87.10% | 81.55% | 1305/1305 clear |
| PRIMARY | 4.25 x 1.60 | 89.19% | 81.88% | 1329/1329 clear |
| ORDNANCE | 4.25 x 1.60 | 87.70% | 80.08% | 1329/1329 clear |

Retention is measured independently for each screen axis, perpendicular to the sightline from the screen centre to the fixed camera: sqrt(1 - dot(unit_axis, unit_sightline)^2). It removes distance scaling and includes yaw, lean and bank. All must retain at least 80% width and 75% height. Screen dimensions describe their full chamfered-outline bounding rectangles in their own planes. The blue polygons have the exact specified dimensions. Dense ray casts cover the blue faces, including points near each outer edge and the corner cuts; screens must also be entirely inside the frame. Detailed measurements and control visibility are in measurements.json.

## Dash and structure

The central upper dash rises 20 degrees from horizontal, from y=-0.95, z=0.64 to y=1.9, z=1.677. The projector pedestal grows out of this rake and keeps its lens at (0.0, 0.0, 1.25). A narrow recessed break connects the rake to the shelf; its slope is -41.17 degrees. The control shelf slopes -3 degrees from horizontal and starts at z=1.12, y=-3.05. Its front fascia drops 0.30 units, at 66.57 degrees from horizontal, then a second plane kicks back 0.37 and down 0.48 units at 52.38 degrees. These are modelled faces, not shading tricks. The upper rake, break, control shelf and front fascia are continuous across the centre. Outboard columns wrap forward by up to 0.55 units, with the shelf wings rising 1.25 to 1.65 units into the slanted pod bases. The wings are explicitly triangulated into flat facets: shelf wings range from 12.20 to 30.74 degrees, recessed wing breaks from 41.24 to 62.47 degrees, and upper wings from 17.32 to 21.05 degrees to horizontal; two broad inner buttresses meet the housings and strut shoes.

The rib landing centres are ((-3.2, 1.65, 1.68), (3.2, 1.65, 1.68)); their shoes overlap the dash buttresses. The rear sill is at y=3.65, so the feet are 2.00 units forward of it. Ribs have a 0.74 x 1 chamfered cross-section, and remain entirely outside x=+/-2.5. The ship placeholder fits inside the full 5 x 4 x 4 clearance box centred at (0, 0, 4.5). Side pillars are 1.15 units wide and step from x=+/-11.85, y=4.05 above to x=+/-9.90, y=-0.10 below. The header has a deep polygonal section with a sloping underside.

## Departures from the sketch

The specified 4.25 x 3.50 SHIP screen makes the left pod taller than the right. The right housing is raised at its hinge so the silhouettes stay related without changing any screen size. Lower screens fold separately to strengthen the keyboard-shelf language in the cockpit references. The exaggerated inward yaw uses the new foreshortening allowance. The inner shoulder corners flare 0.95 units toward the centre, exposing broad return faces; the deeper outer flanks remain visible. Weapon navigation keys occupy the outer rails so the landed canopy ribs cannot cover them.

The fixed lens and camera keep the projector lower in frame than the drawing. The dash rises behind and beside it, while the foreground shelf is raised from round 1 and all controls remain visible. The shallow pedestal is integrated into the upper rake. The sketch's star field and painterly surfaces are omitted; space is uniform dark blue and the fighter is a simple translucent volume. These are editable intersecting masses, not a final watertight production asset.

## Rebuild

Run `"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" -b --python out/build_D2.py` from this folder. The script rebuilds and verifies the model, saves D2.blend, renders four views, and assembles the comparison from the supplied local images using local Pillow. `-- --preview` renders only the fixed camera; `-- --check` performs geometry and visibility checks without rendering. No network is used.
