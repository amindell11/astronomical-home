from pathlib import Path
path = Path('D:/amind/git/agent-5/art/ships/valis/README.md')
text = path.read_text()
old = text.split('\n\n')[1]
new = '`Valis.blend` is the approved editable source: 30 mesh parts under nested body and flight-surface parents, with live bilateral symmetry, forward -Y and up +Z. Expand each parent to edit individual panels, charcoal trailing spars, tips, and pivots. The wing and aft-prong profile keys retain the original plate thickness alongside the approved tapered shapes. `mesh-approval.json` locks the evaluated geometry. Paint is independent on the two sides: the atlas\'s upper half carries right-side charts and the lower half the mirrored left-side charts.'
text = text.replace(old, new, 1)
text = text.replace('The saved Unity hull uses seven palette regions plus a contour submesh.', 'The saved Unity hull uses seven palette regions plus a contour submesh, with four rigid wing bones in one skinned renderer. Wings rest forward and sweep back over half a second under forward thrust; idle and reverse thrust share the same rest pose. The Blender source is authored in the swept flight pose.')
path.write_text(text)
