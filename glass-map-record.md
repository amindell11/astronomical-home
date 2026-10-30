# Glass distortion map

The owner supplied owner-settings.json and asked to see a cockpit-shaped distortion map. The twelve personality values are saved in the projection profile at cb419ae69cf222089b76d5ffc38d9b12dfc5339b. Colors and typography remain reference data for the separate instrument-redraw arc. The owner also ruled that glass distortion and CRT curvature should not duplicate the bend.

## Current field

The left map evaluates the production shader's geometry at the owner's settings: curvature 0.65 followed by glass distortion 0.96. It includes the exact sequential x and y sine offsets and excludes jitter, shudder and transition events. It maps sampling-offset magnitude in pixels at 1600 by 900. The peak on a 161 by 91 sample grid is 51.5 pixels.

## Proposed single canopy field

The right map is a design proposal, not a shipped shader. Its center is at UV 0.5, 0.46. A radius function blends a rounded rectangle at the lower lip into an elliptical upper arch. Distortion follows the outward normal of those contours, with a broad quiet center and a smooth increase toward the rim. At strength 0.96 its maximum displacement is 28.8 pixels at 1600 by 900. The proposed field replaces both existing geometry terms with one canopy warp. It does not stack a CRT bend on the glass bend. The dashed contour is a shape guide, not proposed cockpit artwork or an occlusion mask.

The neutral grid shows undistorted positions. The darker grid solves the inverse sampling map, so it shows where HUD lines appear. Its displacement is displayed at four times actual size by default; the button changes this to actual size. The heatmap and numeric offsets always report actual values. The map does not distort the world and does not propose a visible cockpit frame.

## Checks and status

The interactive map runs without JavaScript errors. The magnification control changes the grid, and the mobile layout has no horizontal overflow. The exact twelve profile values were checked against the owner's JSON. Current rendering code still uses the original preview equations. The owner can review the single-canopy proposal before its geometry replaces them.

The afterglow review finding is fixed in 7bb70b5c9e55d07976449ad2d7d89ddbce5d44be. A real-camera test failed before the RGBA16F history change and passes afterward. Five projection tests and the local ReSharper check pass for that code. Later changes only replace profile values. Gameplay captures from the earlier retune are historical comparisons, not captures of the owner's latest preset.
