from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import subprocess,os,shutil,json,uuid,hashlib
repo=Path('D:/amind/git/agent-1');scratch=Path(__file__).parent
results=repo/'results/nightshade-sharp-fins';out=results/'round-02'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
bg=(28,33,44)
def card(name,path,box,title,subtitle):
    pic=Image.open(out/path).convert('RGB').crop(box)
    im=Image.new('RGB',(pic.width,pic.height+84),bg);im.paste(pic,(0,84));d=ImageDraw.Draw(im)
    d.text((24,13),title,font=font,fill='white')
    d.text((24,49),subtitle,font=small,fill=(181,188,207));im.save(out/name,quality=95)
card('review-iso.jpg','material/iso_front.png',(205,250,1320,1100),
     'NIGHTSHADE — COMPACT, SHARP FINS','Reduced footprint • Straighter sweep • Clipped blade tips')
card('review-side.jpg','material/side.png',(35,500,1370,895),
     'NIGHTSHADE — SIDE PROFILE','Fin thickness and the owner’s lower-fin angle retained')
for kind,box in [('iso_front',(205,250,1320,1100)),('top',(25,25,1375,1375))]:
    pics=[]
    for phase in ('before','round-02'):
        pic=Image.open(results/phase/'material'/f'{kind}.png').convert('RGB').crop(box)
        pic=pic.resize((660,round(pic.height*660/pic.width)),Image.Resampling.LANCZOS);pics.append(pic)
    im=Image.new('RGB',(1320,pics[0].height+60),bg);d=ImageDraw.Draw(im)
    for i,label in enumerate(('BEFORE','COMPACT + SHARP')):
        d.text((i*660+20,17),label,font=font,fill='white');im.paste(pics[i],(i*660,60))
    im.save(out/f'comparison-{kind}.jpg',quality=95)
report=json.loads((out/'check/ship_check.json').read_text())
baseline=json.loads((results/'owner-baseline.json').read_text())
before=baseline['fingerprint']['parts'];after=report['fingerprint']['parts']
changed=[n for n in before if before[n]!=after.get(n)]
assert set(changed)=={'Upper small swept fins','Lower small swept fins','Upper fin armor joint','Lower fin armor joint'}
assert set(before)==set(after)
assert len(report['findings'])==1
assert before['Tail root mounting blocks']==after['Tail root mounting blocks']
assert report['findings']==json.loads((results/'round-01/check/ship_check.json').read_text())['findings']
assert hashlib.sha256((repo/'art/ships/nightshade/Nightshade.blend').read_bytes()).hexdigest()==report['source_sha256']
(out/'fingerprint-delta.json').write_text(json.dumps({'changed_parts':changed,'parts_added':[],
    'parts_removed':[],'all_unclaimed_parts_unchanged':True,'existing_findings':report['findings'],
    'finding_part_unchanged_since_owner_checkpoint':True,'new_findings':[]},indent=2))
def git(*args,cwd=repo):
    p=subprocess.run(['git',*map(str,args)],cwd=cwd,check=True,capture_output=True,text=True)
    if p.stderr:print(p.stderr.strip(),flush=True)
    return p.stdout.strip()
git('add','--','art/ships/nightshade/Nightshade.blend','art/ships/nightshade/ship.json')
print(git('commit','-m','art(nightshade): reduce swept fins to compact sharp blades'),flush=True)
git('push','origin','HEAD:refs/heads/task/nightshade-blockout');source_sha=git('rev-parse','HEAD')
assert git('ls-remote','origin','refs/heads/task/nightshade-blockout').split()[0]==source_sha
git('fetch','origin','evidence/nightshade');parent=git('rev-parse','FETCH_HEAD')
prefix='blockout/compact-fins-01';assert not git('ls-tree','--name-only',parent,'--',prefix)
checkout=Path(os.environ['TEMP'])/('nightshade-evidence-'+uuid.uuid4().hex[:12])
git('worktree','add','--detach','--no-checkout',checkout,parent);git('read-tree','HEAD',cwd=checkout)
git('restore','--source=HEAD','--worktree','--','.gitattributes','README.md',cwd=checkout)
dest=checkout/prefix;dest.mkdir(parents=True)
for folder in ('before','round-01','round-02'):shutil.copytree(results/folder,dest/folder)
shutil.copy2(results/'owner-baseline.json',dest/'owner-baseline.json')
(dest/'scripts').mkdir()
for name in ('nightshade_sharp_fins.py','nightshade_compact_fins.py','nightshade_lower_fin_seam.py',
             'nightshade_fin_render.py','nightshade_fin_publish.py'):
    shutil.copy2(scratch/name,dest/'scripts'/name)
(dest/'README.md').write_text(f'''# Nightshade — compact, sharper upper and lower fins

Source: [{source_sha[:8]}](https://github.com/amindell11/astronomical-home/commit/{source_sha}). **Round 02 is the current blockout candidate; owner approval pending.**

The owner approved the tail mounting direction, then identified the upper and lower fins as too soft and cute compared with the reference. During the first shape pass, they also asked for substantially smaller fins because the main top silhouette already worked without them. Their saved source was checkpointed as `e77f6110` before editing.

Round 01 sharpens the shoulders, straightens the swept edges and narrows the rear ends into clipped blade tips. Round 02 additionally reduces that planform to 62% in local X/Y around its central attachment, while retaining the existing thickness, live Mirrors and the owner's lower-fin transform. The lower fin's existing seam was reseated on its tilted surface. The full-size sharp pass is retained as intermediate evidence (`9060885a`), not the current candidate.

![Before and after](round-02/comparison-iso_front.jpg)
![Top comparison](round-02/comparison-top.jpg)
![Current side](round-02/review-side.jpg)

- [Current isometric](round-02/review-iso.jpg)
- [Underside](round-02/material/underside.png)
- [Isolated fin profile](round-02/fin-only/upper_top.png)
- [Orthographic review](round-02/ortho/ship_render.json)
- [Game scale](round-02/scale/scale.png)
- [Source check](round-02/check/ship_check.json)
- [Edit audit](round-02/edit-audit.json)
- [Preservation audit](round-02/fingerprint-delta.json)

`ship_check` reports one existing finding: the owner's unapplied scale on `Tail root mounting blocks`, `(0.756041, 0.754301, 0.490019)`. That part has the identical fingerprint it had in the owner checkpoint; no exemption or transform application was added. There are no new findings. This is a review candidate, not a passed stage.

Only the two swept-fin meshes and their existing seam strips changed. Their topology, object transforms and modifier stacks remain intact. Every other part, including the owner's resized mounting blocks and edited rear fork, is unchanged. Source SHA-256: `{report['source_sha256']}`.

The source and existing manifest are committed. Production legacy hull, ShipLegacyList, Unity integration, breakup-slot deferral and the owner's separate `Nightshade1.blend` remain untouched. No PR or detail-stage approval.

Appended to evidence parent `{parent}` without replacing earlier rounds.
''',encoding='utf-8')
with (checkout/'README.md').open('a',encoding='utf-8') as f:
    f.write(f'\n- [Nightshade compact, sharper fins — round 02 awaiting owner review]({prefix}/README.md)\n')
git('add','--','README.md',prefix,cwd=checkout)
git('commit','-m','evidence(nightshade): compact sharp fin comparisons and review',cwd=checkout)
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
