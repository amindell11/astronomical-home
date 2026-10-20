# Continuous brush projection â€” scope decision pending

Source commit: `5bb2b98c46d6e9c0e51299f8455a1b8739ef57db`. Previous UV brush input: `83aa4b156219d4577a49c00d9ed09178f26fb1e2`. PR #1029, issue #1027.

## Why the top seam appeared

The independent house brush tile was sampled through Valis's atlas UVs, whose mirrored hull halves use different coordinates. Arbitrary tiling therefore introduced a value discontinuity. The four-arm diagnosis shows the UV brush, brush disabled, tiled UV coordinates, and geometric normals. The center seam disappears without the brush; the normal field is continuous there. The neighboring geometric triangle is a separate feature.

## What the current correction demonstrates

The same source texture is sampled in renderer-local coordinates through three planar projections, blended by surface normals. Its scale is 0.8 with offset (0.23, 0.37), and mirror wrapping makes repeated tile borders continuous. No geometry, UV layout or existing paint masks were edited. The source PNG bytes are unchanged.

Left: the previous UV shader with its actual 3.3 scale, zero offset and repeat-wrapped texture. Right: the local projection. The capture clones the old texture to preserve its repeat setting. Frontal and angled closeups show the center seam removed while retaining brushwork. Motion covers whole-ship bank/turn, light sweep and camera orbit, under Performant quality. Captures are controlled Unity Editor renders, not gameplay or a performance measurement.

## What still fails

The combined quality review identified a skinning problem, then a probe reproduced it on Valis's actual four wing bones. Renderer-local positions change as the wings articulate, so the brush field slides across them. Holding the atlas texels fixed while sweeping the bones changed 41,470 of 99,006 covered texels by more than 2/255; maximum brush-value change was 24/255. `WingBrushProbe.cs` uses the same shader's brush path and renders in UV space to isolate attachment from lighting and camera changes. The two `wing-brush` images show this failure.

This correction is not ready to merge. The owner has been asked whether to expand the prototype into a separate brush-only export in the existing paint pipeline, or retain runtime projection as a limited prototype. A baked brush layer would keep continuous rest-pose brush values attached through the existing UVs during skinning. No new bake or exporter change has been made pending that choice.

## Verification and its limit

The strengthened graphics regression splits one flat/sloped panel into disconnected UV islands and requires unchanged brush values; it failed in both cases before the mapping correction (3/5 total passing) and passes after (5/5). It also checks whole-object rotation/translation, frontal brush visibility, removal of fixed painted shading, light response, fixed ink, camera independence and flash restoration. Those tests do not cover wing articulation; the separate probe above exposes that gap. ReSharper passed with zero blocking findings. These are scoped resident-editor tests, not full-suite merge proof.

## Reproduction

`SeamCapture.Revision` and `.Direct` produce the matched captures. `publish_seam.py` labels and encodes them. `WingBrushProbe.Run` reproduces the attachment failure using `v4.shader`; `v3.shader` is the previous UV shader. `seam-fix.diff` preserves the current candidate. The scale exploration predates the final offset and wrap adjustment.
