# Painted shading and heavier ink

This copies ink-materials-20/console.blend. Geometry, folds, object positions, camera and light objects are unchanged. The geometry remains provisional.

Open console.blend, scene `21 - Painted planes and ink`. Console materials prefixed `21 |` use broad painted values with a small natural shadow contribution. Knobs, cables and housings use the same graphic treatment. The node named `Mostly painted with restrained light response` controls the balance. Paint ramps control the neutral palette and broad variation; `Paint edge` accents existing bevel faces. There is no displacement.

The view layer separates outer contours and internal folds into two Freestyle line sets. Their line styles control width, ink color and slight pressure variation. The hardware selection excludes screens, status lenses, projector slats and recessed supports and fittings. Ink appears in final renders; the viewport shows the editable painted materials. UI exports, shared fonts, hologram and electronic lighting remain from the previous scene. All file images remain packed.

The review page keeps the previous/current side by side and offers a reference/current comparison. The reference is for drawing style, not geometry or old display content. Both the camera render and angled inspection are available at full size.

Sol selected candidate4 and cleared both views with no remaining actionable feedback. The delivered scene and PNGs are byte-for-byte copies of that reviewed candidate. User approval remains pending. review.json records the review and the correction of misleading preview interpretations; the saved render pixels retained the full hologram and UI throughout.

apply_painted.py accepts a loaded ink-materials-20 file and --name. It refuses an existing output and writes a Blender file plus source manifest. render_review.py renders the camera and neutral inspection without saving its temporary inspection changes. verify.py compares original vertices, faces, smoothing, modifier settings, curves, transforms, visibility and cameras against ink-materials-20. It checks the object inventory and packed image dependencies.
