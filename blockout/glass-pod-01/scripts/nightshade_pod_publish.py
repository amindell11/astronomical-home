from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import subprocess,os,shutil,json,uuid,hashlib
repo=Path('D:/amind/git/agent-1');scratch=Path(__file__).parent
results=repo/'results/nightshade-glass-pod';out=results/'round-02'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',27)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',20)
bg=(28,33,44)
im=Image.new('RGB',(1440,800),bg);d=ImageDraw.Draw(im)
for i,(file,title) in enumerate([('iso.png','TAPERED GLASS POD'),('seat_without_glass.png','CRADLE WITH GLASS HIDDEN')]):
    pic=Image.open(out/'cockpit'/file).convert('RGB').resize((720,720),Image.Resampling.LANCZOS)
    im.paste(pic,(i*720,80));d.text((i*720+22,20),title,font=font,fill='white')
im.save(out/'review-cockpit.jpg',quality=95)
pic=Image.open(out/'material/iso_front.png').convert('RGB').crop((220,140,1240,1150))
im=Image.new('RGB',(pic.width,pic.height+80),bg);im.paste(pic,(0,80));d=ImageDraw.Draw(im)
d.text((22,12),'NIGHTSHADE — TAPERED COCKPIT',font=font,fill='white')
d.text((22,49),'Owner-visible upper fins included; original hidden fin remains hidden',font=small,fill=(190,198,214))
im.save(out/'review-ship.jpg',quality=95)
pic=Image.open(out/'cockpit/side.png').convert('RGB').crop((40,300,1170,960))
im=Image.new('RGB',(pic.width,pic.height+75),bg);im.paste(pic,(0,75));d=ImageDraw.Draw(im)
d.text((22,19),'SIDE — FORWARD TAPER RESTORED',font=font,fill='white')
im.save(out/'review-side.jpg',quality=95)
report=json.loads((out/'check/ship_check.json').read_text())
baseline=json.loads((results/'precheck/ship_check.json').read_text())
before=baseline['fingerprint']['parts'];after=report['fingerprint']['parts']
changed=[n for n in before if before[n]!=after.get(n)]
expected={'Rebuilt pitched hull','Upper canopy glazing','Lower canopy glazing','Upper canopy frame',
          'Upper canopy metal bezel','Lower canopy metal bezel','Upper canopy inner seal'}
assert set(changed)==expected and set(before)==set(after)
assert report['findings']==baseline['findings'] and len(report['findings'])==13
assert all(before[n]==after[n] for n in before if 'fin' in n.lower())
assert hashlib.sha256((repo/'art/ships/nightshade/Nightshade.blend').read_bytes()).hexdigest()==report['source_sha256']
(out/'fingerprint-delta.json').write_text(json.dumps({'changed_parts':changed,'parts_added':[],
    'parts_removed':[],'all_unclaimed_parts_unchanged':True,'all_fin_meshes_unchanged':True,
    'existing_findings':report['findings'],'new_findings':[],
    'review_visibility':{'Upper small swept fins':False,'Upper small swept fins.001':True},
    'review_includes_unassigned_owner_fin':True},indent=2))
def git(*args,cwd=repo):
    p=subprocess.run(['git',*map(str,args)],cwd=cwd,check=True,capture_output=True,text=True)
    if p.stderr:print(p.stderr.strip(),flush=True)
    return p.stdout.strip()
git('add','--','art/ships/nightshade/Nightshade.blend','art/ships/nightshade/ship.json')
print(git('commit','-m','art(nightshade): seat tapered glass pod in open metal cradle'),flush=True)
source_sha=git('rev-parse','HEAD')
git('push','origin','HEAD:refs/heads/task/nightshade-blockout')
assert git('ls-remote','origin','refs/heads/task/nightshade-blockout').split()[0]==source_sha
git('fetch','origin','evidence/nightshade');parent=git('rev-parse','FETCH_HEAD')
prefix='blockout/glass-pod-01';assert not git('ls-tree','--name-only',parent,'--',prefix)
checkout=Path(os.environ['TEMP'])/('nightshade-evidence-'+uuid.uuid4().hex[:12])
git('worktree','add','--detach','--no-checkout',checkout,parent);git('read-tree','HEAD',cwd=checkout)
git('restore','--source=HEAD','--worktree','--','.gitattributes','README.md',cwd=checkout)
dest=checkout/prefix;dest.mkdir(parents=True)
for folder in ('precheck','pre-taper','before','round-01','round-01-inclusive','round-02'):
    shutil.copytree(results/folder,dest/folder)
(dest/'scripts').mkdir()
for name in ('nightshade_pod_live.py','nightshade_pod_taper.py','nightshade_pod_render.py',
             'nightshade_pod_saved_audit.py','nightshade_pod_publish.py','pod-owner-geometry.json'):
    shutil.copy2(scratch/name,dest/'scripts'/name)
(dest/'README.md').write_text(f'''# Nightshade — tapered glass pod in an open metal cradle

Source: [{source_sha[:8]}](https://github.com/amindell11/astronomical-home/commit/{source_sha}). **Round 02 is the current blockout candidate; owner approval pending.**

The owner described one elongated glass pod seated through a hole in the hull, with stronger separation from its gray metal cradle. The cockpit concept is authoritative; the legacy mesh was not used. Their saved source was checkpointed as `6610e439` before editing.

Round 01 established a through-opening with an interior wall, glass extending above and below the cradle, and a distinct metal rim. The owner found the oval too round and asked to keep the earlier tapered shape. Round 02 restores the narrow forward tip and wider rear shoulders from the owner's pre-edit canopy while retaining the separate glass volume and real opening. Glass remains two existing editable halves that meet at the concealed equator; parts were not merged.

## Visual evidence

Current tapered pod, alongside the same view with glass hidden to expose the opening.

![Current cockpit and open cradle](round-02/review-cockpit.jpg)

Current ship, including the owner's visible edited upper fin.

![Current ship](round-02/review-ship.jpg)

Forward taper in side profile.

![Current side profile](round-02/review-side.jpg)

<details><summary>Additional current views and checks</summary>

- [Orthographic top](round-02/ortho/top.png)
- [Orthographic side](round-02/ortho/side.png)
- [Front](round-02/ortho/front.png)
- [Rear](round-02/ortho/back.png)
- [Underside](round-02/material/underside.png)
- [Game scale](round-02/scale/scale.png)
- [Source check](round-02/check/ship_check.json)
- [Edit audit](round-02/edit-audit.json)
- [Preservation audit](round-02/fingerprint-delta.json)

</details>

These are Blender blockout previews, not in-engine or final glass materials.

## Fin visibility correction

The first review setup rendered role members regardless of viewport hiding, which showed the owner's hidden original `Upper small swept fins` and omitted the visible edited duplicate `Upper small swept fins.001` outside the role collections. This was a capture error, not an older source or changed fin mesh. The corrected round-02 review setup follows saved visibility and includes that existing duplicate. No source collection assignments were changed. All fin fingerprints match the owner checkpoint.

`before/` and `round-01/` retain the initial captures with that incorrect fin selection. `round-01-inclusive/` is an intermediate capture that included the duplicate but still showed the original. They are not current review images. The render script in `scripts/` is the final corrected version.

## Validation and scope

`ship_check` has the same 13 existing findings as the owner checkpoint: unapplied object scales, the duplicate origin/fin's parentage, and the duplicate fin's missing visual role. No new findings, no exemptions added, and no modifiers applied. This is a review candidate, not a passed stage.

Only seven existing cockpit/hull meshes changed. The hull opening replaced 64 surface faces with 16 interior wall faces; only the cockpit boundary moved. Existing objects and mesh datablocks, object transforms, modifier stacks, live Mirrors, and every unclaimed part are retained. The local topology was then preserved during the taper correction, with face normals recalculated. Source SHA-256: `{report['source_sha256']}`.

The source and existing manifest are committed. Production legacy hull, ShipLegacyList, Unity integration, breakup-slot deferral and the owner's separate `Nightshade1.blend` remain untouched. No PR or detail-stage approval.

Appended to evidence parent `{parent}` without replacing earlier rounds.
''',encoding='utf-8')
with (checkout/'README.md').open('a',encoding='utf-8') as f:
    f.write(f'\n- [Nightshade tapered glass pod — round 02 awaiting owner review]({prefix}/README.md)\n')
git('add','--','README.md',prefix,cwd=checkout)
git('commit','-m','evidence(nightshade): tapered glass pod and corrected owner fin captures',cwd=checkout)
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
