# Hangar carousel review clips (#1059)

- `single-press-side-by-side`: top is #1024 on main (its own `hangar-directional` capture, frames 0–180), placed in its real 1200-unit rect on a 1920×1080 layout; bottom is this branch. Next at 2 s, Previous at 4 s.
- `bursts`: this branch. Alternating every 0.3 s, alternating at 10 Hz, Next at 10 Hz, Previous at 10 Hz (four offered ships, so each one-way burst wraps), then settle.
- `bursts-round1`: the first sign-off clip, rejected by the owner: one-way spam crowded the screen (single-index catch-up swapped on-screen hulls; 8 ships/s cap).

The ship pixels are the actual preview render texture. The menu panel strip (55% darkening) and the dashed old-rect outline are painted by `compose.py`; the real menu text is not shown. `HangarCarouselCapture.cs` is the temporary capture fixture, not part of the PR.
