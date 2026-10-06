from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import subprocess,os,shutil,json,uuid,hashlib
repo=Path('D:/amind/git/agent-1');scratch=Path(__file__).parent
results=repo/'results/nightshade-cockpit-frame-01';out=results/'round-01'
prior=repo/'results/nightshade-detail-01/round-01'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
im=Image.new('RGB',(1400,775),(28,33,44));d=ImageDraw.Draw(im)
for i,(folder,label) in enumerate([(prior,'PREVIOUS FRAME'),(out,'DETAILED METAL CRADLE')]):
    pic=Image.open(folder/'closeups/cockpit.png').convert('RGB').resize((700,700),Image.Resampling.LANCZOS)
    im.paste(pic,(i*700,75));d.text((i*700+22,22),label,font=font,fill='white')
im.save(out/'comparison-frame.jpg',quality=95)
Image.open(out/'closeups/cockpit.png').convert('RGB').save(out/'review-frame.jpg',quality=95)
baseline=json.loads((results/'precheck/ship_check.json').read_text());report=json.loads((out/'check/ship_check.json').read_text())
audit=json.loads((out/'edit-audit.json').read_text());before=baseline['fingerprint']['parts'];after=report['fingerprint']['parts']
assert all(before[n]==after[n] for n in before)
assert set(after)-set(before)==set(audit['new_parts'])
assert not report['findings'] and report['exempted']==baseline['exempted']
assert hashlib.sha256((repo/'art/ships/nightshade/Nightshade.blend').read_bytes()).hexdigest()==report['source_sha256']
(out/'fingerprint-delta.json').write_text(json.dumps({'existing_parts_changed':[],
    'new_parts':audit['new_parts'],'all_previous_fingerprints_unchanged':True,
    'findings':[],'exemptions_unchanged':True},indent=2))
def git(*args,cwd=repo):
    p=subprocess.run(['git',*map(str,args)],cwd=cwd,check=True,capture_output=True,text=True)
    if p.stderr:print(p.stderr.strip(),flush=True)
    return p.stdout.strip()
assert git('rev-parse','HEAD')=='f12664a4d3c6fbc1e599c0df0f904d2aa58aba88'
git('add','--','art/ships/nightshade/Nightshade.blend','art/ships/nightshade/ship.json')
print(git('commit','-m','art(nightshade): detail the canopy retaining frame'),flush=True)
source_sha=git('rev-parse','HEAD');git('push','origin','HEAD:refs/heads/task/nightshade-blockout')
assert git('ls-remote','origin','refs/heads/task/nightshade-blockout').split()[0]==source_sha
git('fetch','origin','evidence/nightshade');parent=git('rev-parse','FETCH_HEAD')
prefix='detail-model/cockpit-frame-01';assert not git('ls-tree','--name-only',parent,'--',prefix)
checkout=Path(os.environ['TEMP'])/('nightshade-evidence-'+uuid.uuid4().hex[:12])
git('worktree','add','--detach','--no-checkout',checkout,parent);git('read-tree','HEAD',cwd=checkout)
git('restore','--source=HEAD','--worktree','--','.gitattributes','README.md',cwd=checkout)
dest=checkout/prefix;dest.mkdir(parents=True)
for folder in ('precheck','round-01'):shutil.copytree(results/folder,dest/folder)
(dest/'scripts').mkdir()
for name in ('nightshade_cockpit_frame_live.py','nightshade_cockpit_frame_render.py','nightshade_cockpit_frame_publish.py','cockpit-detail-before.json'):
    shutil.copy2(scratch/name,dest/'scripts'/name)
(dest/'README.md').write_text(f'''# Nightshade — canopy frame detail

Source: [{source_sha[:8]}](https://github.com/amindell11/astronomical-home/commit/{source_sha}). **Detail-model candidate; owner review pending.**

The owner noted that the first detail pass left the cockpit under-detailed, clarifying that they meant the canopy frame. This round details the metal cradle around the approved glass pod: upper/lower glazing gaskets, segmented beveled retaining rails, fitted outer frame plating, and a stepped rear collar with latch recesses.

![Frame detail](round-01/review-frame.jpg)

![Before and after](round-01/comparison-frame.jpg)

<details><summary>Additional review views and validation</summary>

- [Cockpit side](round-01/closeups/cockpit_side.png)
- [Cockpit top](round-01/closeups/cockpit_top.png)
- [Whole ship](round-01/material/iso_front.png)
- [Underside](round-01/material/underside.png)
- [Review sheet](round-01/sheet/sheet.png)
- [Game scale](round-01/scale/scale.png)
- [Source comparison](round-01/compare/compare_top.png)
- [Source check](round-01/check/ship_check.json)
- [Edit audit](round-01/edit-audit.json)
- [Fingerprint delta](round-01/fingerprint-delta.json)

</details>

Blender previews only; no in-engine, flight, paint or completed detail-stage approval is claimed.

All previously existing part fingerprints are identical to `f12664a4`. The glass, hull, fins, prior detail parts, transforms, materials, source visibility and modifier stacks remain intact. Nine new frame parts are separate meshes with live Mirror and Solidify modifiers, in the hull role. The hidden earlier trim remains untouched in `ignore`.

`ship_check` passes with zero unresolved findings and the same nine active owner-approved scale exemptions. The manifest is unchanged. The source is saved in Object Mode.

Source SHA-256: `{report['source_sha256']}`. Source and existing manifest are committed on the current task branch. Production legacy hull, ShipLegacyList, Unity integration, breakup-slot deferral and the owner's separate `Nightshade1.blend` are untouched. No PR.

Appended to evidence parent `{parent}`, preserving previous rounds.
''',encoding='utf-8')
with (checkout/'README.md').open('a',encoding='utf-8') as f:f.write(f'\n- [Nightshade canopy frame detail — owner review pending]({prefix}/README.md)\n')
git('add','--','README.md',prefix,cwd=checkout);git('commit','-m','evidence(nightshade): canopy frame detail review',cwd=checkout)
head=git('rev-parse','HEAD',cwd=checkout);assert git('rev-parse','HEAD^',cwd=checkout)==parent
assert not git('diff-tree','--no-commit-id','--name-only','--diff-filter=D','-r','HEAD',cwd=checkout)
git('lfs','push','origin','HEAD',cwd=checkout);git('push','origin','HEAD:refs/heads/evidence/nightshade',cwd=checkout)
remote=git('ls-remote','origin','refs/heads/evidence/nightshade').split()[0];assert remote==head
state={'source_commit':source_sha,'evidence_commit':head,'parent':parent,'prefix':prefix,'published':True}
(results/'publication.json').write_text(json.dumps(state,indent=2));print(json.dumps(state),flush=True)
resolved=checkout.resolve();assert resolved.parent==Path(os.environ['TEMP']).resolve() and resolved.name.startswith('nightshade-evidence-')
assert git('rev-parse','HEAD',cwd=resolved)==remote
git('worktree','remove','--force',resolved);print('EVIDENCE_PUBLISHED_AND_TEMP_CHECKOUT_REMOVED',flush=True)
