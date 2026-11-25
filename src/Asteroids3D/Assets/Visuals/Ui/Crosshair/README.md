# Crosshair artwork

Edit the SVG masters here. The shared 64-unit artboard puts the aim point at its
centre, including the pointer's tip. The resource and Keystone shapes are white
so the HUD can tint and reveal them independently.

Regenerate every PNG with Inkscape 1.4.2 from the repository root:

```powershell
./scripts/export_crosshair.ps1
```

Pass `-InkscapePath` for a different installation location. The exporter writes
four-times resolution PNGs under `Generated/`. Commit those files with the SVG
changes. Generated PNGs are never edited by hand.

The crosshair prefab uses the complete artboard with centred pivots and scales
with the existing HUD canvas. Sprite imports use uncompressed colour, bilinear
filtering and no mipmaps.

The bottom-centre WeaponKeystoneHud uses a 92-unit UI rectangle. At the approved
1672px-wide reference scale, its outer arcs have a 10px stroke and their radius
is 24px larger than the Keystone ring. The Keystone arrow is a placeholder.
