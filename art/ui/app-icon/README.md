# App icon

The game's icon: Vanguard, drawn as a cream A whose crossbar is a planetary ring,
over a dark planet with an amber rim light. The build uses it from
`src/Asteroids3D/Assets/Visuals/Ui/AppIcon/AppIcon.png` (Player Settings default
icon and the `Main` build profile reference that texture). The design rounds and
the owner's GIMP passes are on the `evidence/app-icon` branch:
[design history](https://github.com/amindell11/astronomical-home/tree/90935d8965c87e7f3e088a8a6ca78d794d59fe85).

## Files

| File | Role |
|---|---|
| `icon.svg` | The app icon. Source of every PNG. |
| `mark-mono.svg` | The one-colour mark: same shape, amber rim turned cream. For headers and splash art. |
| `export.py` | Renders the SVGs to PNGs. |
| `export/` | 512 and 256 px icon and a 512 px mark for the itch.io page. |

## Regenerate

```bash
uv run art/ui/app-icon/export.py
```

It overwrites `AppIcon.png` in place, so the texture keeps its GUID and every
reference to it. Unity scales that one 1024 px texture to each Windows icon size; no
per-size art exists yet.
