# Nightshade concept round 1

Ship-art stage 1 of #796, arc #868 slice 9. No view is approved.

The owner asked to capture the existing mesh and distinguish the rear prongs from Valis,
then allowed wider exploration. All three generated candidates were shown in chat.
Owner response: "Those went a bit too far". The next round returns to the original wings,
canopy, small fins and body, exploring restrained changes at the rear prong ends.

| Candidate | Picture | Prompt | Provenance |
| --- | --- | --- | --- |
| A: familiar hull | [top](nightshade-a-top.jpg) | [prompt](top.prompt.txt) | [sidecar](nightshade-a-top.json) |
| B: bat-like wings | [top](nightshade-b-top.jpg) | [prompt](explore-b.prompt.txt) | [sidecar](nightshade-b-top.json) |
| C: crescent | [top](nightshade-c-top.jpg) | [prompt](explore-c.prompt.txt) | [sidecar](nightshade-c-top.json) |

[Game-scale comparison](game-scale.png) shows the full images at 100 and 150 pixels.
The generator did not fully follow the tail instructions: A and C retain much of the
old fork, and C reverses the original wing sweep. These are proposals, not model projections.

## Which mesh views were used?

The first [captures](references/) use the legacy prefab's nonuniform scale. The owner
observed the stretch. The [native captures](references/native/) omit that scale and were
the actual generator inputs. In the first captures, length relative to span is multiplied
by 180/127, about 1.42. Both sets retain the imported FBX geometry and use the current
albedo. Reference lighting differs from Unity. Embedded FBX lights were omitted to avoid
a Blender 5.1 importer error. The helper and capture manifests record the setup.

The two reference `.blend` files are temporary imported-mesh inspection scenes, not
an authored new hull or a blockout. The native scene was opened in Blender for the owner.

Input provenance records the paths supplied to the generator in pool slot `agent-2`.
Copies of the original Nightshade image and Valis style/differentiation image are in
`references/`; the mesh captures and their packed albedo are retained here as well.
Production source assets were read from main commit `961d753dfcefbd2ed34e874a9ace3d4cb9d0e24c`.
