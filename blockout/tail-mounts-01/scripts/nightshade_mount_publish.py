from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import subprocess,os,shutil,json,uuid,hashlib
repo=Path('D:/amind/git/agent-1');scratch=Path(__file__).parent
results=repo/'results/nightshade-tail-mounts';out=results/'round-01'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
for name,path,box,title in [
    ('review-mounts.jpg','attachment/closeup.png',(50,210,1320,1190),'NIGHTSHADE — TAIL ROOT MOUNTING BLOCKS'),
    ('review-iso.jpg','material/iso_front.png',(205,265,1285,1080),'NIGHTSHADE — TAIL ATTACHMENTS IN CONTEXT')]:
    pic=Image.open(out/path).convert('RGB').crop(box)
    im=Image.new('RGB',(pic.width,pic.height+84),(28,33,44));im.paste(pic,(0,84));d=ImageDraw.Draw(im)
    d.text((24,13),title,font=font,fill='white')
    d.text((24,49),'Angular shoulder blocks connect the fork roots to the fuselage',font=small,fill=(181,188,207))
    im.save(out/name,quality=95)
report=json.loads((out/'check/ship_check.json').read_text())
before=json.loads((out/'owner-baseline-fingerprint.json').read_text())['parts']
after=report['fingerprint']['parts']
assert all(before[n]==after[n] for n in before)
assert set(after)-set(before)=={'Tail root mounting blocks'}
assert not report['findings']
assert hashlib.sha256((repo/'art/ships/nightshade/Nightshade.blend').read_bytes()).hexdigest()==report['source_sha256']
(out/'fingerprint-delta.json').write_text(json.dumps({'changed_existing_parts':[],
    'added_parts':['Tail root mounting blocks'],'removed_parts':[],'findings':[],
    'all_existing_owner_parts_unchanged':True},indent=2))
def git(*args,cwd=repo):
    p=subprocess.run(['git',*map(str,args)],cwd=cwd,check=True,capture_output=True,text=True)
    if p.stderr:print(p.stderr.strip(),flush=True)
    return p.stdout.strip()
git('add','--','art/ships/nightshade/Nightshade.blend','art/ships/nightshade/ship.json')
print(git('commit','-m','art(nightshade): connect tail forks with angular mounting blocks'),flush=True)
git('push','origin','HEAD:refs/heads/task/nightshade-blockout')
source_sha=git('rev-parse','HEAD')
assert git('ls-remote','origin','refs/heads/task/nightshade-blockout').split()[0]==source_sha
git('fetch','origin','evidence/nightshade');parent=git('rev-parse','FETCH_HEAD')
prefix='blockout/tail-mounts-01'
assert not git('ls-tree','--name-only',parent,'--',prefix)
checkout=Path(os.environ['TEMP'])/('nightshade-evidence-'+uuid.uuid4().hex[:12])
git('worktree','add','--detach','--no-checkout',checkout,parent);git('read-tree','HEAD',cwd=checkout)
git('restore','--source=HEAD','--worktree','--','.gitattributes','README.md',cwd=checkout)
dest=checkout/prefix;dest.mkdir(parents=True)
shutil.copytree(out,dest/'round-01');(dest/'scripts').mkdir()
for name in ('nightshade_tail_mounts.py','nightshade_mount_render.py','nightshade_mount_publish.py'):
    shutil.copy2(scratch/name,dest/'scripts'/name)
(dest/'README.md').write_text(f'''# Nightshade — tail root mounting blocks

Source: [{source_sha[:8]}](https://github.com/amindell11/astronomical-home/commit/{source_sha}). **Blockout candidate; owner approval pending.**

The owner asked for chunky mechanical blocks connecting the straight tail forks to the central fuselage, showing the exposed gap in their Blender viewport. Their latest saved fork and lower-fin edits were checkpointed as `bb5ab4df` before this pass.

A mirrored pair of angular shoulder blocks now overlaps both the fuselage and fork roots. Each side has broad planar faces, chamfered edges, and a tapered aft end. The attachment is a separate editable part with a live X Mirror. All existing owner geometry, transforms, topology and modifiers are unchanged.

![Attachment close-up](round-01/review-mounts.jpg)
![Full ship](round-01/review-iso.jpg)

- [Top view of attachment](round-01/attachment/top.png)
- [Full side](round-01/material/side.png)
- [Rear isometric](round-01/material/iso_rear.png)
- [Orthographic review](round-01/ortho/ship_render.json)
- [Game scale](round-01/scale/scale.png)
- [Source check](round-01/check/ship_check.json)
- [Edit audit](round-01/edit-audit.json)
- [Preservation audit](round-01/fingerprint-delta.json)

`ship_check` passes with zero findings. One new part, `Tail root mounting blocks`, has 40 cage vertices and 44 faces before the live Mirror. The fingerprint comparison confirms zero changes to existing parts. Source SHA-256: `{report['source_sha256']}`.

The source and existing manifest are committed. The owner's separate `Nightshade1.blend`, production legacy mesh, ShipLegacyList, Unity integration and breakup-slot deferral are untouched. No PR or detail-stage approval.

Appended to evidence parent `{parent}` without replacing earlier rounds.
''',encoding='utf-8')
with (checkout/'README.md').open('a',encoding='utf-8') as f:
    f.write(f'\n- [Nightshade tail mounting blocks — awaiting owner review]({prefix}/README.md)\n')
git('add','--','README.md',prefix,cwd=checkout)
git('commit','-m','evidence(nightshade): tail mounting blocks review',cwd=checkout)
head=git('rev-parse','HEAD',cwd=checkout)
assert git('rev-parse','HEAD^',cwd=checkout)==parent
assert not git('diff-tree','--no-commit-id','--name-only','--diff-filter=D','-r','HEAD',cwd=checkout)
git('lfs','push','origin','HEAD',cwd=checkout);git('push','origin','HEAD:refs/heads/evidence/nightshade',cwd=checkout)
remote=git('ls-remote','origin','refs/heads/evidence/nightshade').split()[0];assert remote==head
state={'source_commit':source_sha,'evidence_commit':head,'parent':parent,'prefix':prefix,'published':True}
(results/'publication.json').write_text(json.dumps(state,indent=2));print(json.dumps(state),flush=True)
resolved=checkout.resolve()
assert resolved.parent==Path(os.environ['TEMP']).resolve() and resolved.name.startswith('nightshade-evidence-')
assert git('rev-parse','HEAD',cwd=resolved)==remote
git('worktree','remove','--force',resolved)
print('EVIDENCE_PUBLISHED_AND_TEMP_CHECKOUT_REMOVED',flush=True)
