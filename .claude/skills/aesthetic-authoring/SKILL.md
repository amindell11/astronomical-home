---
name: aesthetic-authoring
description: Design a game look — a VFX sprite, an effect's motion, a material or UI treatment — from references to a tuned Unity asset, with the user steering through named knobs on a live tuner page. Use when the user wants a visual designed or restyled, not just wired up.
metadata:
  project: astronomical-home
---

# Aesthetic Authoring

The user steers the look; you build the instruments. Every stage ends on the user's
pick, and the final stage hands them **knobs**: named constants for the design's key
aspects, turned live on a tuner page (a published artifact that re-renders the asset as
each knob moves and exports a settings block). Worked example: `art/vfx/laser-bolt/`
(its README lists every file; the PR that shipped it carries the dead ends).
Ships take the `ship-art-pipeline` skill instead.

Show every candidate as a picture (SendUserFile, `display: "render"`), never as prose.
Judge every candidate at **game scale** (the on-screen size in play, often 60–150 px) as
well as large; detail that won't read at game scale is cut, however good it looks big.

## Steps

1. **Read the house style.** Look at shipped drawn assets (`Assets/Visuals/Vfx/`, the
   ships, `art/gameplay/experiments/` style frames) and the user's references. Done when
   you can state the style's rules in one line each (tone bands, edge language,
   palette, what stays soft) and name the one main inspiration.
2. **Sketch cheap.** Generate throwaway concept layers procedurally (SVG or canvas from a
   script, several variants side by side) and let the user point at what they like.
   Done when the user has picked a shape language, even if it is one small layer.
3. **Refine with imagegen** (`art/tools/imagegen/README.md`). Pass every reference, each
   with its role stated in the prompt, and the user's pick as Image 1. State scale and
   proportion in concrete terms (a short bolt ~6× longer than thick), and name any detail
   too fine for game scale as absent. Run `nb2` and `pro` in parallel; expect two
   rounds. Done when the user names a favourite.
4. **Hand cleanup.** The user flattens or corrects the favourite; that file becomes the
   generator's source, never regenerated.
5. **Generator with knobs.** A script parses the source into the structure the drawing
   really has (the laser: nested flat colour bands) and varies that structure
   procedurally. Every design decision is a named module constant with a one-line
   comment; frame 0 reproduces the source exactly. Before tuning, render a debug view
   of what the parse found. Done when previews at large and game scale show the
   intended motion.
6. **Tuner page.** Port the generator to a single self-contained HTML template; the
   script bakes it with its own constants as the page's defaults, so script and page
   share one settings block. Publish with the Artifact tool (a local HTML file sent with
   SendUserFile runs no script). The page's export block pastes over the script's
   constants. Done when the user sends back their settings.
7. **Bake to Unity** in a pool slot (`agent-worktree-pr-loop`). The script's export
   writes the textures (premultiplied downsample, oriented to the mesh's travel axis);
   a small shader plays them; a new material keeps shared materials untouched; the
   visual rides a child object so colliders and gameplay components keep their scale.
   Evidence is an in-game capture (`game-capture`).

## Folder shape

Main carries what the generator runs on: `art/<category>/<asset>/` holds `README.md`
(role of each file, how to regenerate), the hand-cleaned source, any file the script
reads, and the generator folder (script, tuner template; its `out/` is ignored).
Everything before the source (references, sketches, imagegen rounds with prompts and
sidecars) goes on the PR's `evidence/<lease>` branch under `history/`, linked from the
README by a commit-pinned URL. Film stills and other third-party references stay out
of git entirely (the repo is public); the history README describes them.
