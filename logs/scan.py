import re, os, sys, collections
sys.argv = [sys.argv[0], sys.argv[1]]
from resolve import resolve, guid2path, ROOT
REF = re.compile(r'\{fileID: (-?\d+), guid: (\w{32}), type: (\d)\}')
watch = sys.stdin.read().split()  # basenames of interest, e.g. Ship_1.prefab
hits = collections.Counter(); unresolved = []
for dp, dn, fn in os.walk(os.path.join(ROOT, 'Assets')):
    for f in fn:
        if not f.endswith(('.prefab', '.unity', '.asset', '.mat', '.controller', '.overrideController', '.playable', '.anim')): continue
        p = os.path.join(dp, f)
        try: text = open(p, encoding='utf-8').read()
        except Exception: continue
        if not text.startswith('%YAML'): continue
        for m in REF.finditer(re.sub(r',\s*\n\s+type', ', type', text)):
            fid, g = int(m.group(1)), m.group(2)
            gp = guid2path.get(g)
            if not gp or not gp.endswith('.prefab'): continue
            if fid == 100100000: 
                if os.path.basename(gp) in watch: hits[(os.path.relpath(p, ROOT), os.path.basename(gp), 'ASSET')] += 1
                continue
            r = resolve(g).get(fid)
            prov = r[1] if r else 'UNRESOLVED'
            chain = os.path.basename(gp) + ':' + prov
            if any(w in chain for w in watch):
                hits[(os.path.relpath(p, ROOT), chain.split(':')[0] + ':' + str(fid), prov if r else 'UNRESOLVED')] += 1
for k, v in sorted(hits.items()): print(v, *k)
