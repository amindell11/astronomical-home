from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import subprocess,os,shutil,json,uuid,hashlib
repo=Path('D:/amind/git/agent-1');scratch=Path(__file__).parent
results=repo/'results/nightshade-detail-01';out=results/'round-01'
prior=repo/'results/nightshade-rounded-canopy/round-03'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
bg=(28,33,44)
im=Image.new('RGB',(1400,775),bg);d=ImageDraw.Draw(im)
for i,(folder,label) in enumerate([(prior,'APPROVED MEDIUM DETAIL'),(out,'DETAIL CANDIDATE 01')]):
    pic=Image.open(folder/'material/iso_front.png').convert('RGB').resize((700,700),Image.Resampling.LANCZOS)
    im.paste(pic,(i*700,75));d.text((i*700+22,22),label,font=font,fill='white')
im.save(out/'comparison-iso.jpg',quality=95)
pic=Image.open(out/'material/iso_front.png').convert('RGB')
pic.save(out/'review-iso.jpg',quality=95)
im=Image.new('RGB',(1400,780),bg);d=ImageDraw.Draw(im)
for i,(name,label) in enumerate([('wing','ARMOR PANELS + COOLING LOUVERS'),('cockpit','COCKPIT FRAME HARDWARE')]):
    pic=Image.open(out/f'closeups/{name}.png').convert('RGB').resize((700,700),Image.Resampling.LANCZOS)
    im.paste(pic,(i*700,80));d.text((i*700+20,22),label,font=font,fill='white')
im.save(out/'review-details.jpg',quality=95)
report=json.loads((out/'check/ship_check.json').read_text());baseline=json.loads((results/'precheck/ship_check.json').read_text())
before=baseline['fingerprint']['parts'];after=report['fingerprint']['parts'];audit=json.loads((out/'edit-audit.json').read_text())
changed=[n for n in before if before[n]!=after.get(n)]
assert changed==['SymmetryOrigin.001'] and not report['findings']
assert set(after)-set(before)==set(audit['new_parts']) and set(before)<=set(after)
assert all(e['rule']=='scale' for e in report['exempted'])
assert hashlib.sha256((repo/'art/ships/nightshade/Nightshade.blend').read_bytes()).hexdigest()==report['source_sha256']
(out/'fingerprint-delta.json').write_text(json.dumps({'changed_existing_parts':changed,'new_parts':audit['new_parts'],
    'existing_meshes_byte_identical':True,'existing_parent_edit':'SymmetryOrigin.001 parented under identity SymmetryOrigin',
    'no_parts_removed':True,'findings':report['findings'],'approved_exemptions':report['exempted']},indent=2))
def git(*args,cwd=repo):
    p=subprocess.run(['git',*map(str,args)],cwd=cwd,check=True,capture_output=True,text=True)
    if p.stderr:print(p.stderr.strip(),flush=True)
    return p.stdout.strip()
assert git('rev-parse','HEAD')=='3bc865e2b2d8003ed76e236a106098fdf6791377'
git('add','--','art/ships/nightshade/Nightshade.blend','art/ships/nightshade/ship.json')
print(git('commit','-m','art(nightshade): add editable mechanical detail layers'),flush=True)
source_sha=git('rev-parse','HEAD');git('push','origin','HEAD:refs/heads/task/nightshade-blockout')
assert git('ls-remote','origin','refs/heads/task/nightshade-blockout').split()[0]==source_sha
git('fetch','origin','evidence/nightshade');parent=git('rev-parse','FETCH_HEAD')
prefix='detail-model/mechanical-01';assert not git('ls-tree','--name-only',parent,'--',prefix)
checkout=Path(os.environ['TEMP'])/('nightshade-evidence-'+uuid.uuid4().hex[:12])
git('worktree','add','--detach','--no-checkout',checkout,parent);git('read-tree','HEAD',cwd=checkout)
git('restore','--source=HEAD','--worktree','--','.gitattributes','README.md',cwd=checkout)
dest=checkout/prefix;dest.mkdir(parents=True)
for folder in ('precheck','round-01'):shutil.copytree(results/folder,dest/folder)
(dest/'scripts').mkdir()
for name in ('nightshade_detail_live.py','nightshade_detail_render.py','nightshade_detail_publish.py','nightshade-detail-before.json'):
    shutil.copy2(scratch/name,dest/'scripts'/name)
(dest/'approval.json').write_text(json.dumps({'approved_stage':'medium detail / proportions','approved_source':'124a71cf4c050b2232d6f19b815233980fa9fa97',
    'approved_evidence':'724e44eddb8b0bfe69ebcbedf4437420ef3a36ab/blockout/rounded-canopy-01',
    'owner_instruction':'Yeah that’s good, let’s lock the medium detail pass and move on to the detail work',
    'scale_exemption_approval':'Preserve scales; record exemptions','stage_3':'detail model in progress; candidate awaits owner review',
    'geometry_lock':'Not written; waits for owner flight-check approval'},indent=2),encoding='utf-8')
(dest/'README.md').write_text(f'''# Nightshade — first mechanical detail candidate

Source: [{source_sha[:8]}](https://github.com/amindell11/astronomical-home/commit/{source_sha}). **Detail-model stage; owner review pending.**

The owner approved the medium-detail proportions at `124a71cf` and asked to continue with detail modeling. That approval is recorded in [arc #868](https://github.com/amindell11/astronomical-home/issues/868) and [approval.json](approval.json). The current straight fork pair is retained. The pipeline geometry lock remains reserved for an approved flight check.

## Visual evidence

The approved form, with shallow angular armor plates, panel joints, wing cooling louvers, cockpit latches, service covers and an aft exhaust insert. These are editable geometric parts, not paint textures.

![Detail candidate](round-01/review-iso.jpg)

![Approved source and detail candidate](round-01/comparison-iso.jpg)

![Close-ups](round-01/review-details.jpg)

<details><summary>Turnaround, game scale and validation</summary>

![Review sheet](round-01/sheet/sheet.png)

![Game scale](round-01/scale/scale.png)

- [Top](round-01/material/top.png)
- [Side](round-01/material/side.png)
- [Rear isometric](round-01/material/iso_rear.png)
- [Underside](round-01/material/underside.png)
- [Tail detail](round-01/closeups/tail.png)
- [Sanctioned source comparison](round-01/compare/compare_top.png)
- [Source check](round-01/check/ship_check.json)
- [Edit audit](round-01/edit-audit.json)
- [Fingerprint preservation](round-01/fingerprint-delta.json)

</details>

Blender previews only; no in-engine, flight, paint or completed detail-stage approval is claimed.

## Preservation and validation

Every original mesh retains its exact fingerprint: stored vertices, topology, transforms, material assignments and live modifiers. New detail parts have their own live Mirrors. The existing duplicate editing origin was parented under the common identity origin without changing world transforms. Its visible fin is now in the hull role. The hidden older fin and three hidden trim parts remain editable and hidden, in `ignore`, so export matches the owner's visible shape.

`ship_check` passes with zero unresolved findings and {len(report['exempted'])} active scale exemptions explicitly approved by the owner. The manifest retains all approved scale exemptions, including the hidden trim. No transforms or modifiers were applied. Original source visibility is preserved. No original parts were deleted or regenerated.

This candidate keeps the straight forks rigid, with no moving assemblies proposed. `Engine.Main` marks the new exhaust insert. Weapon socket placement and flight integration remain to be resolved before a flight-check candidate; this first detail round does not change the production loadout or mounts.

Source SHA-256: `{report['source_sha256']}`. Source and manifest are committed on the existing task branch. Production legacy hull, ShipLegacyList, Unity integration, breakup-slot deferral and the owner's separate `Nightshade1.blend` are untouched. No PR.

Appended to evidence parent `{parent}`; previous blockout evidence and pins are preserved.
''',encoding='utf-8')
with (checkout/'README.md').open('a',encoding='utf-8') as f:
    f.write(f'\n- [Medium detail approved; first mechanical detail candidate — owner review pending]({prefix}/README.md)\n')
git('add','--','README.md',prefix,cwd=checkout)
git('commit','-m','evidence(nightshade): approved proportions and first mechanical detail review',cwd=checkout)
head=git('rev-parse','HEAD',cwd=checkout);assert git('rev-parse','HEAD^',cwd=checkout)==parent
assert not git('diff-tree','--no-commit-id','--name-only','--diff-filter=D','-r','HEAD',cwd=checkout)
git('lfs','push','origin','HEAD',cwd=checkout);git('push','origin','HEAD:refs/heads/evidence/nightshade',cwd=checkout)
remote=git('ls-remote','origin','refs/heads/evidence/nightshade').split()[0];assert remote==head
state={'source_commit':source_sha,'evidence_commit':head,'parent':parent,'prefix':prefix,'published':True}
(results/'publication.json').write_text(json.dumps(state,indent=2));print(json.dumps(state),flush=True)
resolved=checkout.resolve();assert resolved.parent==Path(os.environ['TEMP']).resolve() and resolved.name.startswith('nightshade-evidence-')
assert git('rev-parse','HEAD',cwd=resolved)==remote
git('worktree','remove','--force',resolved)
print('EVIDENCE_PUBLISHED_AND_TEMP_CHECKOUT_REMOVED',flush=True)
