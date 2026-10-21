# B1 hand cleanup

The user [selected B1, Broad bands](https://github.com/amindell11/astronomical-home/issues/792#issuecomment-6325600369), on 2026-10-21. `laser-impact.png` is a working copy of that source for the user's hand edit. The original remains preserved under `../02-refinement/b1-broad-bands.png`.

## Cleanup brief

Keep the small contact core and broad fan silhouette. Flatten residual shading inside the four solid color bands and remove isolated stray pixels. Preserve transparent padding and clean antialiased edges.

The shipped laser palette is blue `#0868fa`, cyan `#00d6fa`, pale mint `#b8faf4`, and white `#fafcfc`. Keep these regions broad enough to read at small size. Save the cleaned image as an RGBA PNG in this working file, or provide the edited file separately.

The initial working copy is byte-identical to B1, with SHA-256 `b35269dd1b223cf90ff7c3ace978af92e96c77902b96c860a44e008ae5ef5ae2`. Cleanup has not happened yet. The user-owned hand edit becomes the generator's source and must not be regenerated. Animation starts from the selected 340 ms motion study after that source is ready.
