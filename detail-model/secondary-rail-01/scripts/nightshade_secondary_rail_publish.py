from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import subprocess,os,shutil,json,uuid,hashlib
repo=Path('D:/amind/git/agent-1');scratch=Path(__file__).parent
results=repo/'results/nightshade-secondary-rail-01';out=results/'round-03'
prior=repo/'results/nightshade-cockpit-frame-01/round-01'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25)
im=Image.new('RGB',(1400,775),(28,33,44));d=ImageDraw.Draw(im)
for i,(folder,label) in enumerate([(prior,'PREVIOUS CANOPY'),(out,'FITTED GLASS AND PANE DIVIDER')]):
    pic=Image.open(folder/'closeups/cockpit.png').convert('RGB').resize((700,700),Image.Resampling.LANCZOS)
    im.paste(pic,(i*700,75));d.text((i*700+22,22),label,font=font,fill='white')
im.save(out/'comparison-canopy.jpg',quality=95)
Image.open(out/'closeups/cockpit.png').convert('RGB').save(out/'review-canopy.jpg',quality=95)
baseline=json.loads((prior/'check/ship_check.json').read_text())
report=json.loads((out/'check/ship_check.json').read_text())
before=baseline['fingerprint']['parts'];after=report['fingerprint']['parts']
changed=sorted(n for n in before if before[n]!=after[n]);new=sorted(set(after)-set(before))
assert changed==['Lower canopy glazing','Upper canopy glazing']
assert new==['Cockpit inset secondary rail'] and not set(before)-set(after)
assert not report['findings'] and report['exempted']==baseline['exempted']
source=repo/'art/ships/nightshade/Nightshade.blend'
assert hashlib.sha256(source.read_bytes()).hexdigest()==report['source_sha256']
(out/'fingerprint-delta.json').write_text(json.dumps({'baseline_source_commit':'2a7f4cf57787e1a0f9274c5a87c11c61d57d9116',
    'existing_parts_changed':changed,'new_parts':new,'all_other_part_fingerprints_unchanged':True,
    'findings':[],'exemptions_unchanged':True},indent=2))
def git(*args,cwd=repo):
    p=subprocess.run(['git',*map(str,args)],cwd=cwd,check=True,capture_output=True,text=True)
    if p.stderr:print(p.stderr.strip(),flush=True)
    return p.stdout.strip()
assert git('rev-parse','HEAD')=='5f9f217dc41e4d2a0d49026bea1e07960fda5694'
assert git('diff','--name-only')=='art/ships/nightshade/Nightshade.blend'
assert not git('diff','--cached','--name-only')
git('add','--','art/ships/nightshade/Nightshade.blend')
print(git('commit','-m','art(nightshade): raise and taper the canopy pane divider'),flush=True)
source_sha=git('rev-parse','HEAD');git('push','origin','HEAD:refs/heads/task/nightshade-blockout')
assert git('ls-remote','origin','refs/heads/task/nightshade-blockout').split()[0]==source_sha
git('fetch','origin','evidence/nightshade');parent=git('rev-parse','FETCH_HEAD')
prefix='detail-model/secondary-rail-01';assert not git('ls-tree','--name-only',parent,'--',prefix)
checkout=Path(os.environ['TEMP'])/('nightshade-evidence-'+uuid.uuid4().hex[:12])
git('worktree','add','--detach','--no-checkout',checkout,parent);git('read-tree','HEAD',cwd=checkout)
git('restore','--source=HEAD','--worktree','--','.gitattributes','README.md',cwd=checkout)
dest=checkout/prefix;dest.mkdir(parents=True)
shutil.copytree(prior/'check',dest/'precheck')
for folder in ('round-01','round-02','round-03'):shutil.copytree(results/folder,dest/folder)
(dest/'scripts').mkdir()
for name in ('nightshade_secondary_rail_live.py','nightshade_glass_fit_live.py','nightshade_pane_rail_fit_live.py',
             'nightshade_secondary_rail_render.py','nightshade_secondary_rail_publish.py'):
    shutil.copy2(scratch/name,dest/'scripts'/name)
(dest/'README.md').write_text(f'''# Nightshade — canopy pane divider and glass fit

Source: [{source_sha[:8]}](https://github.com/amindell11/astronomical-home/commit/{source_sha}). **Detail-model candidate; owner review pending.**

The owner identified the inset secondary rail in the approved concept, clarified that it represents the division between glass panes, and asked for the glass to fit the frame cleanly. The divider now sits higher along the glass shoulders, broadens gradually toward the rear collar, and leaves an exposed outer glass band.

![Canopy review](round-03/review-canopy.jpg)

![Before and after](round-03/comparison-canopy.jpg)

<details><summary>Additional review views and validation</summary>

- [Cockpit side](round-03/closeups/cockpit_side.png)
- [Cockpit top](round-03/closeups/cockpit_top.png)
- [Whole ship](round-03/material/iso_front.png)
- [Underside](round-03/material/underside.png)
- [Review sheet](round-03/sheet/sheet.png)
- [Game scale](round-03/scale/scale.png)
- [Source comparison](round-03/compare/compare_top.png)
- [Source check](round-03/check/ship_check.json)
- [Edit audit](round-03/edit-audit.json)
- [Fingerprint delta](round-03/fingerprint-delta.json)

</details>

Round 01 adds the inset rail. Round 02 smooths the upper/lower glazing with live subdivision, adjusts the upper rear shoulder into the collar, and fits the rail to the evaluated glass. Round 03 moves the divider higher on the glass and grows its width from 0.008 at the front to 0.01136 at the rear. Its raised edge also grows slightly toward the rear. The glazing remains one editable surface beneath the pane-divider geometry.

The source control topology, object transforms, owner visibility and live Mirror modifiers are retained. Only the upper glazing's rear control vertices changed; the approved rounded nose and long taper remain. Both glazing pieces gained a live Catmull-Clark level-2 modifier between Mirror and Solidify. The new divider is a separate mesh with live Mirror and Solidify. All other part fingerprints, including the detailed outer frame, hull and fins, match `2a7f4cf5`.

`ship_check` passes with zero unresolved findings and the same nine active owner-approved scale exemptions. The manifest is unchanged. The source is saved in Object Mode. These are Blender previews; no flight, paint or completed detail-stage approval is claimed.

Source SHA-256: `{report['source_sha256']}`. Source and existing manifest are committed on the current task branch. Production legacy hull, ShipLegacyList, Unity integration, breakup-slot deferral and the owner's separate `Nightshade1.blend` are untouched. No PR.

Appended to evidence parent `{parent}`, preserving previous rounds.
''',encoding='utf-8')
with (checkout/'README.md').open('a',encoding='utf-8') as f:f.write(f'\n- [Nightshade canopy pane divider and glass fit — owner review pending]({prefix}/README.md)\n')
git('add','--','README.md',prefix,cwd=checkout)
print(git('commit','-m','evidence(nightshade): canopy pane divider and glass fit',cwd=checkout),flush=True)
head=git('rev-parse','HEAD',cwd=checkout);assert git('rev-parse','HEAD^',cwd=checkout)==parent
assert not git('diff-tree','--no-commit-id','--name-only','--diff-filter=D','-r','HEAD',cwd=checkout)
git('lfs','push','origin','HEAD',cwd=checkout);git('push','origin','HEAD:refs/heads/evidence/nightshade',cwd=checkout)
remote=git('ls-remote','origin','refs/heads/evidence/nightshade').split()[0];assert remote==head
state={'source_commit':source_sha,'evidence_commit':head,'parent':parent,'prefix':prefix,'published':True}
(results/'publication.json').write_text(json.dumps(state,indent=2));print(json.dumps(state),flush=True)
resolved=checkout.resolve();assert resolved.parent==Path(os.environ['TEMP']).resolve() and resolved.name.startswith('nightshade-evidence-')
assert git('rev-parse','HEAD',cwd=resolved)==remote
git('worktree','remove','--force',resolved);print('EVIDENCE_PUBLISHED_AND_TEMP_CHECKOUT_REMOVED',flush=True)
