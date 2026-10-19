# Bulwark independent-paint readiness

Vanguard's signed-space approach is verified and owner-approved in its task. Tested painter revision a21a67317 (held/vanguard-redo) against an isolated copy of Bulwark commit 4358664c6, with paint_space signed, 30 degree sweep, and shared brush contrast 3.2.

The painter refused before writing masks: Armor - Front plate face 95 shares UVs between opposite sides. Read-only inspection across every visual part found overlap on Armor - Front plate and Armor - Side plates: 135 triangle records, 78,797 sample hits (not unique pixels). Full records are in uv-overlap.json. This is substantial overlap, not merely a boundary precision artifact.

The owner protected geometry, UVs and base coat. No canonical source, mask, baseline, settings or gameplay asset changed. The live Blender source was clean, with no dirty images. The Bulwark slot is still agent-3 / bulwark-blockout / PR989.

Asked the owner whether to separate only the overlapping armor UVs while preserving shape and visible base coat, or hold the pass with UVs unchanged. No UV edits are authorized yet. Do not bypass the shared painter's overlap refusal. Flight and UV-density approval remain pending.
