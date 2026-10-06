from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import subprocess,os,shutil,json,uuid,hashlib
repo=Path('D:/amind/git/agent-1');scratch=Path(__file__).parent
results=repo/'results/nightshade-frame-fit';out=results/'round-02'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19);bg=(28,33,44)
for kind in ('iso','top'):
    im=Image.new('RGB',(1400,775),bg);d=ImageDraw.Draw(im)
    for i,(phase,label) in enumerate([('before','YOUR EDITED FRAME'),('round-02','FITTED HULL + ROUNDED NOSE')]):
        pic=Image.open(results/phase/'cockpit'/f'{kind}.png').convert('RGB').resize((700,700),Image.Resampling.LANCZOS)
        im.paste(pic,(i*700,75));d.text((i*700+22,22),label,font=font,fill='white')
    im.save(out/f'comparison-{kind}.jpg',quality=95)
for name,file,title,subtitle in [
    ('review-side.jpg','side.png','SIDE — LOWER FRAME PRESERVED','Hull and rim geometry adjusted around your existing glass pod'),
    ('review-rims.jpg','rim_fit_iso.png','REMAINING RIMS — FIT INSPECTION','Hidden trim shown for this render; source visibility is preserved')]:
    pic=Image.open(out/'cockpit'/file).convert('RGB')
    if file=='side.png':pic=pic.crop((40,290,1175,960))
    im=Image.new('RGB',(pic.width,pic.height+84),bg);im.paste(pic,(0,84));d=ImageDraw.Draw(im)
    d.text((22,12),title,font=font,fill='white');d.text((22,49),subtitle,font=small,fill=(190,198,214))
    im.save(out/name,quality=95)
report=json.loads((out/'check/ship_check.json').read_text())
baseline=json.loads((results/'precheck/ship_check.json').read_text())
before=baseline['fingerprint']['parts'];after=report['fingerprint']['parts']
changed=[n for n in before if before[n]!=after.get(n)]
assert set(changed)=={'Rebuilt pitched hull','Upper canopy frame','Upper canopy inner seal','Lower canopy metal bezel'}
assert set(before)==set(after) and report['findings']==baseline['findings'] and len(report['findings'])==13
assert all(before[n]==after[n] for n in before if 'fin' in n.lower() or 'glazing' in n)
assert hashlib.sha256((repo/'art/ships/nightshade/Nightshade.blend').read_bytes()).hexdigest()==report['source_sha256']
(out/'fingerprint-delta.json').write_text(json.dumps({'changed_parts':changed,'parts_added':[],
    'parts_removed':[],'all_unclaimed_parts_unchanged':True,'all_fin_and_glass_meshes_unchanged':True,
    'existing_findings':report['findings'],'new_findings':[]},indent=2))
def git(*args,cwd=repo):
    p=subprocess.run(['git',*map(str,args)],cwd=cwd,check=True,capture_output=True,text=True)
    if p.stderr:print(p.stderr.strip(),flush=True)
    return p.stdout.strip()
git('add','--','art/ships/nightshade/Nightshade.blend','art/ships/nightshade/ship.json')
print(git('commit','-m','art(nightshade): seat remaining rims around lowered frame'),flush=True)
source_sha=git('rev-parse','HEAD');git('push','origin','HEAD:refs/heads/task/nightshade-blockout')
assert git('ls-remote','origin','refs/heads/task/nightshade-blockout').split()[0]==source_sha
git('fetch','origin','evidence/nightshade');parent=git('rev-parse','FETCH_HEAD')
prefix='blockout/frame-fit-01';assert not git('ls-tree','--name-only',parent,'--',prefix)
checkout=Path(os.environ['TEMP'])/('nightshade-evidence-'+uuid.uuid4().hex[:12])
git('worktree','add','--detach','--no-checkout',checkout,parent);git('read-tree','HEAD',cwd=checkout)
git('restore','--source=HEAD','--worktree','--','.gitattributes','README.md',cwd=checkout)
dest=checkout/prefix;dest.mkdir(parents=True)
for folder in ('precheck','before','round-01','round-02'):shutil.copytree(results/folder,dest/folder)
(dest/'scripts').mkdir()
for name in ('nightshade_frame_fit.py','nightshade_frame_rims.py','nightshade_frame_render.py',
             'nightshade_frame_inspect.py','nightshade_frame_publish.py','frame-owner-geometry.json',
             'frame-owner-fingerprint.json','frame-prior-hull.json'):
    shutil.copy2(scratch/name,dest/'scripts'/name)
(dest/'README.md').write_text(f'''# Nightshade — lowered frame fit and rounded nose

Source: [{source_sha[:8]}](https://github.com/amindell11/astronomical-home/commit/{source_sha}). **Current candidate: round 02. Owner approval pending.**

The owner lowered and reshaped the hull frame to expose more side glass, then asked for the surrounding hull geometry and rim pieces to follow it. They also requested a rounder front instead of the pointed hull nose. While away, they explicitly authorized exiting Edit Mode and saving on their behalf. Their saved frame, lower-fin and other edits were checkpointed as `c1c0a7bd` before changes.

The cockpit aperture follows the glass at the owner's lowered boundary heights. The front frame is broader and rounder, with two additional support loops through the existing nose surfaces. The nearby rail surfaces were redistributed to remove the previous folded/extended surfaces. The remaining upper seat, inner seal, and lower bezel are fitted to the new opening; the lower bezel cross-section was reduced in round 02.

## Visual evidence

Owner checkpoint compared with the fitted hull. Both show the owner's saved visibility state.

![Frame comparison](round-02/comparison-iso.jpg)

Top view shows the rounder nose and closer aperture fit.

![Top comparison](round-02/comparison-top.jpg)

Side glass remains exposed above the lower frame.

![Side](round-02/review-side.jpg)

Remaining trim shown only for fit inspection; these pieces remain hidden in the source, as the owner left them.

![Rim fit](round-02/review-rims.jpg)

<details><summary>Additional current views and checks</summary>

- [Whole ship](round-02/material/iso_front.png)
- [Underside](round-02/material/underside.png)
- [Orthographic top](round-02/ortho/top.png)
- [Orthographic side](round-02/ortho/side.png)
- [Game scale](round-02/scale/scale.png)
- [Opening with glass hidden](round-02/cockpit/seat_without_glass.png)
- [Rims from the side](round-02/cockpit/rim_fit_side.png)
- [Source check](round-02/check/ship_check.json)
- [Hull edit audit](round-01/edit-audit.json)
- [Rim edit audit](round-02/edit-audit.json)
- [Preservation audit](round-02/fingerprint-delta.json)

</details>

Blender blockout previews only; no detail-stage or in-engine approval is claimed.

## Validation and preservation

`ship_check` reports the same 13 findings as the owner checkpoint: unapplied object scales, the duplicate origin/fin's parentage, and that fin's missing visual role. No new findings or exemptions. The stage has not passed validation.

Only the main hull and three existing rim/seat meshes changed. All glass, fins, other objects, source visibility, object transforms and modifier stacks are retained. The original upper fin stays hidden and the owner's visible duplicate is included in the review captures. The owner-deleted upper metal bezel was not recreated. Hull vertices aft of the cockpit are unchanged; the two new nose loops add 34 vertices and 32 faces. Mirror modifiers remain live.

Source SHA-256: `{report['source_sha256']}`. The source and existing manifest are committed. Legacy production hull, ShipLegacyList, Unity integration, breakup-slot deferral and the separate owner file `Nightshade1.blend` remain untouched. No PR.

Appended to evidence parent `{parent}` without replacing earlier rounds.
''',encoding='utf-8')
with (checkout/'README.md').open('a',encoding='utf-8') as f:
    f.write(f'\n- [Nightshade lowered frame fit and rounded nose — owner review pending]({prefix}/README.md)\n')
git('add','--','README.md',prefix,cwd=checkout)
git('commit','-m','evidence(nightshade): lowered frame fit and rounded nose review',cwd=checkout)
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
