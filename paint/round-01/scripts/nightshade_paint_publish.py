from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import uuid

ROOT = Path('D:/amind/git/agent-1')
SCRATCH = Path(__file__).parent
OUT = SCRATCH / 'paint-round-01'
PREFIX = 'paint/round-01'
EXPECTED = 'e20f5e081ed6b7bb868999f802ff2a5e3bd5f76f'
REMOTE_EXPECTED = 'c1fdb2746421408761f4202dfdef32422c554fdc'

def git(*args, cwd=ROOT):
    result = subprocess.run(['git', *map(str, args)], cwd=cwd, check=True, capture_output=True, text=True)
    if result.stderr:
        print(result.stderr.strip(), flush=True)
    return result.stdout.strip()

sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
assert git('rev-parse', 'HEAD') == EXPECTED
assert git('branch', '--show-current') == 'agent-1'
assert not git('diff', '--name-only') and not git('diff', '--cached', '--name-only')
assert git('ls-files', '--others', '--exclude-standard') == 'art/ships/nightshade/Nightshade1.blend'
assert sha(ROOT / 'art/ships/nightshade/Nightshade.blend') == 'bf15def6d4d91f4c9744262f3920b0f9e0ac58466a9aa362993ef9f8f61cf4d2'
assert sha(ROOT / 'art/ships/nightshade/Nightshade1.blend') == '76ede191ee65466a3092a7fbcebf330f47028c9466ac8167c4b28037978df32b'
assert git('ls-remote', 'origin', 'refs/heads/task/nightshade-blockout').split()[0] == REMOTE_EXPECTED
git('fetch', 'origin', 'evidence/nightshade')
parent = git('rev-parse', 'FETCH_HEAD')
assert not git('ls-tree', '--name-only', parent, '--', PREFIX)
checkout = Path(os.environ['TEMP']) / ('nightshade-paint-evidence-' + uuid.uuid4().hex[:12])
git('worktree', 'add', '--detach', '--no-checkout', checkout, parent)
git('read-tree', 'HEAD', cwd=checkout)
git('restore', '--source=HEAD', '--worktree', '--', '.gitattributes', 'README.md', cwd=checkout)
dest = checkout / PREFIX
shutil.copytree(OUT, dest)
(dest / 'scripts').mkdir()
for filename in ('nightshade_paint_render.py', 'nightshade_paint_package.py', 'nightshade_paint_publish.py'):
    shutil.copy2(SCRATCH / filename, dest / 'scripts' / filename)
shutil.copy2(ROOT / 'results/nightshade-secondary-rail-01/round-03/check/ship_check.json', dest / 'source-check.json')
(dest / 'README.md').write_text('''# Nightshade paint concept — round 01

Owner review pending. The owner approved starting this paint-concept pass after the detail-model review. The render source is the approved `c1fdb2746421408761f4202dfdef32422c554fdc` Nightshade model.

![Paint concept](concept.png)

![Game-scale concept thumbnails](game-scale.png)

Dark slate panels, charcoal recesses, restrained cool edge light, blue-violet glass and magenta tips follow the approved concepts. The illustration proposes surface paint over four fresh model renders. The source model, material assignments, UVs, modifiers and scales were not edited. The generated illustration is a paint reference, not geometric validation or finished masks.

- [Exact rendered edit target](flat-contact-sheet.png)
- [Prompt](prompt.txt) and [generation provenance](concept.json)
- [Source audit](render-audit.json)
- [Existing source check for the identical source hash](source-check.json): zero findings, nine active owner-approved scale exemptions.

The game-scale image reduces the concept's top view to 32, 48, 64 and 96 pixels of ship length; actual-size thumbnails sit above nearest-neighbour enlargements. These are concept thumbnails on the reference background, not in-game captures.

The detail model is approved. Owner flight approval, a formal geometry lock, UV approval and paint-concept approval remain outstanding. No final masks, Unity integration, legacy hull changes, ShipLegacyList changes, breakup work or PR are included.

The shared mask-projection and palette-material generators described by arc #868 slice 8 are absent from current main (`25785b3`). Valis's grayscale masks, palette/settings files, layered ORA and archived authoring helpers were inspected as references; no generic tool completion is claimed.
''', encoding='utf-8')
with (checkout / 'README.md').open('a', encoding='utf-8') as f:
    f.write('\n- [Nightshade paint concept — round 01; owner review pending](paint/round-01/README.md)\n')
git('add', '--', 'README.md', PREFIX, cwd=checkout)
print(git('commit', '-m', 'evidence(nightshade): first paint concept and game-scale review', cwd=checkout), flush=True)
evidence = git('rev-parse', 'HEAD', cwd=checkout)
assert git('rev-parse', 'HEAD^', cwd=checkout) == parent
assert not git('diff-tree', '--no-commit-id', '--name-only', '--diff-filter=D', '-r', 'HEAD', cwd=checkout)
git('lfs', 'push', 'origin', 'HEAD', cwd=checkout)
git('push', 'origin', 'HEAD:refs/heads/evidence/nightshade', cwd=checkout)
assert git('ls-remote', 'origin', 'refs/heads/evidence/nightshade').split()[0] == evidence

target = ROOT / 'art/ships/nightshade/paint'
assert sha(target / 'concept-01.png') == sha(OUT / 'concept.png')
provenance = json.loads((target / 'concept-01.json').read_text(encoding='utf-8-sig'))
provenance['evidence_url'] = 'https://github.com/amindell11/astronomical-home/tree/' + evidence + '/' + PREFIX
provenance['references'][0]['path'] = provenance['evidence_url'] + '/flat-contact-sheet.png'
(target / 'concept-01.json').write_text(json.dumps(provenance, indent=2), encoding='utf-8')
assert sha(ROOT / 'art/ships/nightshade/Nightshade.blend') == provenance['source_sha256']
git('add', '--', 'art/ships/nightshade/paint/concept-01.png', 'art/ships/nightshade/paint/concept-01.json')
print(git('commit', '-m', 'art(nightshade): link published paint review evidence'), flush=True)
head = git('rev-parse', 'HEAD')
git('push', 'origin', 'HEAD:refs/heads/task/nightshade-blockout')
assert git('ls-remote', 'origin', 'refs/heads/task/nightshade-blockout').split()[0] == head
state = {'task_commit': head, 'evidence_commit': evidence, 'evidence_parent': parent,
         'prefix': PREFIX, 'published': True}
(OUT / 'publication.json').write_text(json.dumps(state, indent=2), encoding='utf-8')
print(json.dumps(state), flush=True)
resolved = checkout.resolve()
assert resolved.parent == Path(os.environ['TEMP']).resolve() and resolved.name.startswith('nightshade-paint-evidence-')
assert git('rev-parse', 'HEAD', cwd=resolved) == evidence
git('worktree', 'remove', '--force', resolved)
print(git('status', '--short'), flush=True)
