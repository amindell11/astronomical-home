from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import subprocess,os,shutil,json,uuid,hashlib
repo=Path('D:/amind/git/agent-1');scratch=Path(__file__).parent
results=repo/'results/nightshade-rounded-canopy';out=results/'round-03'
prior=repo/'results/nightshade-frame-fit/round-02'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
bg=(28,33,44)
for kind in ('iso','top'):
    im=Image.new('RGB',(1400,775),bg);d=ImageDraw.Draw(im)
    for i,(folder,label) in enumerate([(prior,'BEFORE — POINTED GLASS'),(out,'ROUNDED CANOPY FRONT')]):
        pic=Image.open(folder/'cockpit'/f'{kind}.png').convert('RGB').resize((700,700),Image.Resampling.LANCZOS)
        im.paste(pic,(i*700,75));d.text((i*700+22,22),label,font=font,fill='white')
    im.save(out/f'comparison-{kind}.jpg',quality=95)
report=json.loads((out/'check/ship_check.json').read_text())
baseline=json.loads((prior/'check/ship_check.json').read_text())
before=baseline['fingerprint']['parts'];after=report['fingerprint']['parts']
changed=[n for n in before if before[n]!=after.get(n)]
assert set(changed)=={'Upper canopy glazing','Lower canopy glazing','Rebuilt pitched hull',
    'Upper canopy frame','Upper canopy inner seal','Lower canopy metal bezel'}
assert set(before)==set(after) and report['findings']==baseline['findings'] and len(report['findings'])==13
assert all(before[n]==after[n] for n in before if 'fin' in n.lower())
assert hashlib.sha256((repo/'art/ships/nightshade/Nightshade.blend').read_bytes()).hexdigest()==report['source_sha256']
(out/'fingerprint-delta.json').write_text(json.dumps({'changed_parts':changed,'parts_added':[],
    'parts_removed':[],'all_unclaimed_parts_unchanged':True,'all_fin_meshes_unchanged':True,
    'existing_findings':report['findings'],'new_findings':[]},indent=2))
def git(*args,cwd=repo):
    p=subprocess.run(['git',*map(str,args)],cwd=cwd,check=True,capture_output=True,text=True)
    if p.stderr:print(p.stderr.strip(),flush=True)
    return p.stdout.strip()
assert git('rev-parse','HEAD')=='73ca2f1d8a71944fab14a1bc70fa53dcd6e5673d'
git('add','--','art/ships/nightshade/Nightshade.blend','art/ships/nightshade/ship.json')
print(git('commit','-m','art(nightshade): round canopy front within approved nose'),flush=True)
source_sha=git('rev-parse','HEAD');git('push','origin','HEAD:refs/heads/task/nightshade-blockout')
assert git('ls-remote','origin','refs/heads/task/nightshade-blockout').split()[0]==source_sha
git('fetch','origin','evidence/nightshade');parent=git('rev-parse','FETCH_HEAD')
prefix='blockout/rounded-canopy-01';assert not git('ls-tree','--name-only',parent,'--',prefix)
checkout=Path(os.environ['TEMP'])/('nightshade-evidence-'+uuid.uuid4().hex[:12])
git('worktree','add','--detach','--no-checkout',checkout,parent);git('read-tree','HEAD',cwd=checkout)
git('restore','--source=HEAD','--worktree','--','.gitattributes','README.md',cwd=checkout)
dest=checkout/prefix;dest.mkdir(parents=True)
for folder in ('round-01','round-02','round-03'):shutil.copytree(results/folder,dest/folder)
(dest/'before/cockpit').mkdir(parents=True)
for name in ('iso.png','top.png','side.png'):shutil.copy2(prior/'cockpit'/name,dest/'before/cockpit'/name)
shutil.copy2(prior/'check/ship_check.json',dest/'before/ship_check.json')
(dest/'scripts').mkdir()
for name in ('nightshade_canopy_round.py','nightshade_canopy_seat.py','nightshade_canopy_rails.py',
             'nightshade_canopy_render.py','nightshade_canopy_publish.py','canopy-round-before.json'):
    shutil.copy2(scratch/name,dest/'scripts'/name)
(dest/'README.md').write_text(f'''# Nightshade — rounded canopy front

Source: [{source_sha[:8]}](https://github.com/amindell11/astronomical-home/commit/{source_sha}). **Current candidate: round 03. Owner approval pending.**

The owner approved the outer nose shape and requested a rounder canopy front to follow it. The upper and lower glass noses are broader and slightly shorter, retaining their longitudinal taper and the owner's side-window exposure. Existing canopy vertices were reshaped in place. The opening's inner edge, adjacent rail faces, and remaining hidden rim pieces were fitted to the new glass. The approved outer nose silhouette is unchanged.

## Review

![Top comparison](round-03/comparison-top.jpg)

![Isometric comparison](round-03/comparison-iso.jpg)

- [Cockpit side](round-03/cockpit/side.png)
- [Whole ship isometric](round-03/material/iso_front.png)
- [Underside](round-03/material/underside.png)
- [Orthographic top](round-03/ortho/top.png)
- [Orthographic side](round-03/ortho/side.png)
- [Game scale](round-03/scale/scale96_top.png)
- [Remaining rims, visible only for inspection](round-03/cockpit/rim_fit_iso.png)
- [Hull opening without glass](round-03/cockpit/seat_without_glass.png)
- [Source check](round-03/check/ship_check.json)
- [Preservation audit](round-03/fingerprint-delta.json)

Round 01 reshaped the glass; round 02 fitted the inner opening and remaining trims; round 03 blended that opening adjustment through adjacent existing rail faces.

## Validation and preservation

`ship_check` reports the same 13 pre-existing findings: unapplied scales, the duplicate origin/fin's parentage, and that fin's missing visual role. No new findings or exemptions. Validation has not passed; these are blockout review renders, with no detail-stage or in-engine approval claimed.

Only the two glass meshes, the cockpit's inner hull edge/adjacent faces, and three existing rim/seat meshes changed. Mesh topology, object transforms, live Mirror modifiers, source visibility and all other parts are retained. The glass aft of Y=0.707 is unchanged. All fins, the owner's visible duplicate fin and deleted bezel state are preserved. The approved outer nose and hull aft of the cockpit are unchanged.

Source SHA-256: `{report['source_sha256']}`. The source and existing manifest are committed on the task branch. Legacy production hull, ShipLegacyList, Unity integration, breakup-slot deferral and the owner's separate `Nightshade1.blend` remain untouched. No PR.

Appended to evidence parent `{parent}` without replacing earlier rounds.
''',encoding='utf-8')
with (checkout/'README.md').open('a',encoding='utf-8') as f:
    f.write(f'\n- [Nightshade rounded canopy front — owner review pending]({prefix}/README.md)\n')
git('add','--','README.md',prefix,cwd=checkout)
git('commit','-m','evidence(nightshade): rounded canopy front review',cwd=checkout)
head=git('rev-parse','HEAD',cwd=checkout);assert git('rev-parse','HEAD^',cwd=checkout)==parent
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
