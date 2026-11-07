# Console composition and palette

The latest [composition study](composition.html) gives the hologram more breathing room, shows a thin physical viewport rim across the top, and simplifies the monitor and controls. Broad housing planes, larger simple fasteners, plain screen separators and fewer knob markings replace the dense detail that made the left console look miniaturized. The [final image](composition-viewport.png) keeps the console in the left third and the ship presentation in the right two-thirds.

The built-in image tool edited [the previous composition](composition-thirds.png) using the [viewport and detail prompt](composition-viewport-prompt.txt). That [first pass](composition-viewport-first.png) reduced the ship too far. A [targeted correction](composition-viewport-scale-prompt.txt) restored a modest reduction and the display vent's three broad openings. These image studies have not been applied to Blender. The palette controls below remain available for the earlier layout.

The preceding [one-third composition](composition-thirds.png) came from the darker warm [palette preview](palette-default-preview.png), using this [prompt](composition-thirds-prompt.txt). It established the smaller console footprint and larger ship presentation area.

Open the [palette editor](palette.html) to adjust each material's color and brightness over [neutral-materials.png](neutral-materials.png). The initial palette darkens the bright shell and warms the silver trim. Housing, trim, covers, dark structure, selector caps, and knobs and cables have separate controls. The projection indicator lights have an optional color control. The ship, sky, display content, projection beam and Launch cap keep their original pixels.

Use **Show original** to compare, **Reset darker palette** to return to the darker warm defaults, and the download controls to save the palette or image. Choices also persist in this browser when browser storage is available. These are image color studies; they do not change Blender materials or game assets. The editor reuses the combat HUD's shared type and role styles and palette export conventions without changing those files.

The [default preview](palette-default-preview.png) is the browser's image export. The [editor capture](palette-editor-preview.png) shows its controls. [Verification results](palette-qa.json) cover exact original comparison, protected pixels, saved choices, downloads, and desktop and mobile layouts. The editor opens directly from local files without a server.

The [image history](history.html) preserves earlier studies. The owner rejected the blue-green [station palette](station-palette.png) because it clashed with the ship, then requested fully neutral materials with variation beyond monochrome. The [neutral-image prompt](neutral-materials-prompt.txt) produced the source image for these controls. That image was still too bright white and cold, which prompted direct palette editing.

The earlier [ship-language.png](ship-language.png) followed the owner's request for less clutter, clearer purpose and more of the ships' arcade aesthetic. The built-in image tool edited the control concept using real Vanguard art as a shape and color reference. Its [prompt](ship-language-prompt.txt) requests broad armor planes, fewer incidental details and concise display and projection control groups. The owner subsequently asked to reduce the ship-like screen edges and avoid any single ship's color scheme. Their control functions remain concept proposals, not implemented behavior.

The preceding [physical-controls.png](physical-controls.png) followed the owner's request for physical buttons, cockpit-style control banks and switches, and a physical Launch button. Its [prompt](physical-controls-prompt.txt) keeps selection buttons outside the glass and the main screen as a readout.

The [history page](history.html) shows the [owner-edited Blender capture](owner-capture.png) and the [refinement image](refined-console.png). The image pass proposes details and lighting before any further Blender modeling.

The owner changed the main console angle, camera and other scene objects in the live Blender session. These changes were unsaved. [inputs/owner-edited.blend](inputs/owner-edited.blend) preserves that state before intervention. The space collection was empty because its background plane had been deleted; the nebula image and material were still packed. Restoring only that plane moved none of the existing objects. [inputs/capture-scene.blend](inputs/capture-scene.blend) preserves the restored scene used for the 2048 by 1152 camera render. The live Blender session also has the restored background.

The image model received the new camera render as its edit source and lighting candidate C as the finish and lighting reference. The [exact prompt](prompt.txt) locks the owner's camera, monitor tilt, principal object positions and crop while requesting more coherent chamfered housings, joints, vents, cable routing and electronic light.

The repo's `imagegen.py` request to Nano Banana 2 failed with HTTP 402 because its prepaid credits were depleted; it returned no image. The built-in image-generation tool produced `refined-console.png` using the same prompt and image inputs. The original generated file remains in the Codex generated-images folder. No image-model changes have been applied to the Blender geometry.

The screen labels remain reference artwork, not a revised UI or gameplay specification. The refinement is ready for review before taking selected details back into Blender. No game code or assets changed.
