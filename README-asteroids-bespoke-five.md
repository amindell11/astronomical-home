# Five asteroids per locale

This set contains 25 individually authored Blender asteroids, five for each of the five approved locales. Copper01 and Violet01 are the accepted reference pieces. Their painted Blender files are byte-for-byte copies of the accepted originals. The other 23 are new art candidates built under the owner's instruction to continue.

The owner's Copper acceptance and continuation ruling is recorded on [issue 1377](https://github.com/amindell11/astronomical-home/issues/1377#issuecomment-6714478600). This set-level status supersedes the historical review labels in the copied prototype folders.

Start with `asteroid-set-overview.png`. Each family has a larger five-model board with the original concept inset. `catalog.json` declares every source, preview, material and validation path. Each model has a painted hero, native 150 by 110 pixel preview, clay view, side and reverse. The native label describes the canvas, not the exact asteroid width.

The editable sources retain separate material components and hidden construction volumes. New forms use independently specified silhouettes and fracture arrangements; they do not reuse the rejected rounded asteroid catalog. Directional UVs map the shared painted swatches onto actual mesh faces. Material nodes grade the unmodified source paint. Each painted source packs its active textures; adjacent source maps and their generation prompts are also included.

The strong outer line is Blender Freestyle and is render-specific. Selected internal fracture marks and accents are actual mesh geometry. No Unity shader, runtime integration, production export or non-asteroid debris is included. These are editable art sources, not a claim of production readiness.

The broad planes remain simpler and cleaner than the original concepts. The accepted Violet has a more orderly crystal grouping than its concept. These differences are visible in the boards; successful source checks do not establish aesthetic equivalence.

`validation-summary.json` separates closed solid topology, one-piece primary hosts, UVs and packed images, newly authored buried roots, immutable accepted-source hashes and visual review. Drawing ribbons are intentionally open and are excluded from closed-solid checks. The accepted Violet01 retains its previously documented 9 of 24 root-ring sample exception unchanged. Newly authored roots are checked separately and do not inherit that exception.

The reproducible new-model authoring scripts are under `scripts`. Run Blender in background with `--python scripts/build_model.py -- FAMILY INDEX` from a copy of the package; it writes that model's source and five renders. Run `scripts/validate_family.py -- FAMILY` through Blender to reopen a family without editing it. The ordinary Python contact-sheet scripts use Pillow. Do not regenerate the immutable accepted sources: `render_accepted.py` only renders them and asserts that their bytes remain unchanged.

Only files named in `deliverable-files.json` are curated delivery contents. Build logs, caches, diagnostic scripts and redundant historical proof outputs remain in the working directory and are excluded from that list.
