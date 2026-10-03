from pathlib import Path
import hashlib
import json
import shutil

repo = Path('D:/amind/git/astronomical-home')
archive = repo / 'results/valis-wing-motion/archive-update'
snapshot = archive / 'history/2026-10-02-wing-motion-and-profiles'
records = []

def preserve(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    content = source.read_bytes()
    shutil.copyfile(source, destination)
    digest = hashlib.sha256(content).hexdigest()
    if hashlib.sha256(destination.read_bytes()).hexdigest() != digest:
        raise RuntimeError(f'Archive copy differs: {source}')
    records.append({'source': source.relative_to(repo).as_posix(), 'archive': destination.relative_to(archive).as_posix(), 'bytes': len(content), 'sha256': digest})

for source in (repo / 'art/ships/valis/concepts').rglob('*'):
    if source.is_file():
        preserve(source, archive / 'history/concepts' / source.relative_to(repo / 'art/ships/valis/concepts'))

for source in (repo / 'art/ships/valis').rglob('*'):
    if source.is_file() and 'concepts' not in source.relative_to(repo / 'art/ships/valis').parts:
        preserve(source, snapshot / 'approved-source' / source.relative_to(repo / 'art/ships/valis'))

extensions = {'.png', '.jpg', '.jpeg', '.gif', '.mp4', '.blend', '.blend1', '.ora', '.py', '.cs', '.json', '.zip'}
excluded_names = {'unity-catalog.json', 'Main-local-before.asset', 'Valis-local-before.prefab', 'Valis-staged.prefab', 'archive_valis.py', 'publish_evidence.py'}
excluded_parts = {'frames', 'archive-update', 'evidence-update-v02', 'hosted-results', 'validation', 'primary-before-paint-landing', '__pycache__'}
for folder in ('valis-wing-motion', 'valis-texture-review', 'valis-texturing-restart'):
    root = repo / 'results' / folder
    for source in root.rglob('*'):
        if not source.is_file():
            continue
        relative = source.relative_to(root)
        if any(part in excluded_parts for part in relative.parts):
            if source.suffix not in {'.gif', '.mp4'} or 'archive-update' in relative.parts:
                continue
        if source.name in excluded_names or source.name.endswith('-summary.json') or source.name.startswith('merge-'):
            continue
        if source.suffix in extensions or source.name in {'README.md', 'vertex-proof.txt'}:
            preserve(source, snapshot / folder / relative)

snapshot.mkdir(parents=True, exist_ok=True)
(snapshot / 'archive-manifest.json').write_text(json.dumps({'productionCommit': '1fe918ce10a5ceb3aa5ae91ed5ebc447381f611a', 'files': records}, indent=2) + '\n')
(snapshot / 'README.md').write_text('''# Valis wing, profile and paint authoring archive

The approved model from PR #852 is in approved-source/. It retains individually editable nested parts, live mirrors, profile shape keys, painted UVs and the restored charcoal trailing spars. The archive is a historical snapshot; it does not overwrite the artist source.

valis-wing-motion/ preserves both animation passes, original and intermediate Blender revisions, profile/nesting/alignment helpers, geometry exports, checks, previews and final Unity clips. Earlier profile and assembly experiments are historical, not the approved production model.

valis-texture-review/ and valis-texturing-restart/ preserve the available palette/paint concepts, paint masks, base models and authoring helpers. Original concept images remain at history/concepts/ in this branch. archive-manifest.json records source paths, archived paths, sizes and SHA-256 hashes; every copy was checked against its source bytes.

Raw per-frame capture intermediates, build/test logs, handoff text, PR drafts and unrelated local editor backups are excluded. Encoded clips and concept/preview images are retained.

Earlier geometry stages remain in the existing history/ folders of evidence/valis-geometry. Additional paint authoring tools/tests remain at codex/valis-paint-authoring-archive, commit 2d433ca2e6790ab6f767198a59f7d6d3657405ad. Published motion evidence remains on evidence/valis-wings, commit fceaa62bd3600df147fb513418839de80e05dfbd. These archive/evidence branches are retained separately from main.
''')
attributes = archive / '.gitattributes'
text = attributes.read_text()
for extension in ('.blend1', '.ora', '.zip'):
    pattern = '*' + extension
    if not any(line.startswith(pattern + ' ') for line in text.splitlines()):
        text += f'{pattern} filter=lfs diff=lfs merge=lfs -text\n'
attributes.write_text(text)
readme = archive / 'README.md'
readme.write_text(readme.read_text() + '\n## Wing animation, tapered profiles and paint concepts\n\nhistory/2026-10-02-wing-motion-and-profiles/ preserves the approved PR #852 source, editable intermediate models, profile and wing authoring helpers, palette/paint concepts, masks, previews and Unity clips. Its archive-manifest.json records byte-checked copies. Original concept images remain at history/concepts/.\n')
print(f'Preserved and byte-checked {len(records)} files ({sum(x["bytes"] for x in records) / 1_000_000:.1f} MB).')
