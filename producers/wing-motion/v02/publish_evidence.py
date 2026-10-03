from pathlib import Path
import shutil

root = Path('D:/amind/git/astronomical-home/results/valis-wing-motion/v02')
target = root.parent / 'evidence-update-v02' / 'v02'
target.mkdir(exist_ok=True)
clip = root / 'unity/frames/20261002-210534-Valis-updated-wing-motion-unity'
for suffix in ('.gif', '.mp4'):
    shutil.copyfile(clip.with_suffix(suffix), target / ('Valis-updated-wing-motion-unity' + suffix))
for source, name in [('Unity-rest-v02.png', 'Unity-rest.png'), ('Unity-swept-v02.png', 'Unity-swept.png'), ('nested-preview.png', 'Valis-approved-profile.png')]:
    shutil.copyfile(root / source, target / name)

history = target / 'history'
history.mkdir(exist_ok=True)
excluded = {'Unity-rest-v02.png', 'Unity-swept-v02.png', 'nested-preview.png'}
stages = {'before': 'Original cross-section', 'after': 'Initial tapered profile', 'aligned': 'Restored parts aligned to the reangled wings', 'grouped': 'Earlier combined assembly preview', 'latest': 'Intermediate shape preview', 'nested': 'Approved individually editable parts'}
views = {'low': 'low angle', 'side': 'side view', 'top': 'top view', 'preview': 'view board'}
body_path = root / 'pr-body-v02.md'
body = body_path.read_text().replace('shown from above, below and the side', 'shown from above, a low angle and the side')
body += '\n<details>\n<summary>Blender authoring comparisons and previous motion pass</summary>\n\nThese are authoring-stage comparisons; the approved result and current Unity motion are shown above.\n\n'
for source in sorted(root.glob('*.png')):
    if source.name in excluded:
        continue
    shutil.copyfile(source, history / source.name)
    stage, _, view = source.stem.partition('-')
    caption = f'{stages.get(stage, source.stem.replace("-", " "))}: {views.get(view, view)}' if stage in stages else source.stem.replace('-', ' ').capitalize()
    body += f'{caption}.\n\n![{caption}](https://media.githubusercontent.com/media/amindell11/astronomical-home/EVIDENCE_SHA/v02/history/{source.name})\n\n'
old = 'c695e2eaf954dc6d45aec55834ee589ec640354c'
for name, caption in [('Valis-wing-motion-v01.gif', 'Previous Blender animation pass'), ('Valis-wing-motion-unity.gif', 'Previous Unity animation pass'), ('Unity-forward.png', 'Previous Unity forward pose'), ('Unity-reverse.png', 'Previous Unity reverse pose')]:
    body += f'{caption}.\n\n![{caption}](https://media.githubusercontent.com/media/amindell11/astronomical-home/{old}/{name})\n\n'
body += f'[Previous Unity MP4](https://github.com/amindell11/astronomical-home/blob/{old}/Valis-wing-motion-unity.mp4)\n\n</details>\n'
body_path.write_text(body)
print('Prepared final captures, approved preview and authoring comparisons.')
