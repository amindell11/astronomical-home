## What motion is approved?

The user approved the first Blender GIF and explicitly instructed this chat to proceed in Unity. Valis has forward-thrust, idle, and reverse-thrust poses. The forward pose follows the user's open Blender transforms. Reverse starts from the approved original hull. Main wings have substantial idle-to-forward motion and only a small idle-to-reverse change; fins react visibly in both directions. The approved first pass uses main-wing idle at 8% of forward travel, fin idle at 50%, and smooth half-second transitions. Both sides move together.

## What changes in Unity?

Add a visual-rig component that observes the existing ShipView pilot command and animates rigid wing parts. Author rigid bone weights on the existing combined hull, retaining one hull renderer so paint, contour submeshes, damage tint, and iris bloom continue through the existing rendering path. Preserve the approved source geometry and paint; only moving assemblies receive transforms. Keep the physical collider, weapon mounts, movement, engine settings, AI, and other ship prefabs unchanged. Keep throwaway authoring and preview scripts outside the production diff.

## Why preserve one renderer?

HullVisuals currently targets one renderer. Splitting the hull into renderers would require changing shared damage rendering; rigid bones retain that existing arrangement while allowing separate poses. Flexible mesh deformation and an Animator state machine are unnecessary for these rigid authored transforms.

## What proves acceptance?

Verify the saved skinned hull preserves all original vertices, UVs, submesh indices, materials, and the collider. Verify movement command changes drive the three approved poses and that the visual component rests at idle when presentation is shown without a flight command. Run scoped Unity tests and visually check the saved prefab in a coordinated editor. Open a PR; merge remains a separate explicit user action.

## Where is the approved reference?

First-pass GIF and independently saved animation: results/valis-wing-motion/v01/Valis-wing-motion-v01.gif and Valis-wing-motion-v01.blend in the primary workspace. Numeric forward transforms are preserved in forward-reference.json; manifest.json records the motion parameters. The paint prerequisite landed as PR #843. These scratch paths describe local provenance and will be replaced with commit-pinned visual evidence on the PR.
