import re, os, sys, functools
M = 0x7FFFFFFFFFFFFFFF
ROOT = sys.argv[1] if len(sys.argv) > 1 else r'D:/amind/git/agent-5/src/Asteroids3D'
guid2path = {}
for base in ['Assets']:
    for dp, dn, fn in os.walk(os.path.join(ROOT, base)):
        for f in fn:
            if f.endswith('.meta'):
                p = os.path.join(dp, f)
                with open(p, encoding='utf-8', errors='ignore') as h:
                    for line in h:
                        if line.startswith('guid:'):
                            guid2path[line.split()[1]] = p[:-5]; break
HDR = re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?$', re.M)
def docs(path):
    text = open(path, encoding='utf-8').read()
    hs = list(HDR.finditer(text))
    out = []
    for i, h in enumerate(hs):
        body = text[h.end(): hs[i+1].start() if i+1 < len(hs) else len(text)]
        out.append((int(h.group(1)), int(h.group(2)), bool(h.group(3)), body))
    return out
@functools.lru_cache(None)
def resolve(guid):
    """fileID -> (classID, provenance string) of every object that exists in the prefab with this guid"""
    path = guid2path.get(guid)
    res = {}
    if not path or not path.endswith('.prefab'): return res
    for cls, fid, stripped, body in docs(path):
        if cls == 1001:
            sm = re.search(r'm_SourcePrefab: \{fileID: \d+, guid: (\w+)', body)
            if not sm: continue
            src = sm.group(1)
            removed = set(int(x) for x in re.findall(r'\{fileID: (-?\d+), guid: %s' % src, body.split('m_RemovedComponents:')[1].split('m_AddedGameObjects:')[0]))
            for sid, (scls, prov) in resolve(src).items():
                if sid in removed: continue
                res[(fid ^ sid) & M] = (scls, os.path.basename(guid2path[src]) + ':' + prov)
        elif not stripped:
            name = re.search(r'm_Name: (.*)', body)
            res[fid] = (cls, f'{fid}' + (f'({name.group(1)})' if name and cls == 1 else ''))
    return res
if __name__ == '__main__':
    g = sys.argv[2]
    r = resolve(g)
    for q in sys.argv[3:]:
        print(q, r.get(int(q), 'UNRESOLVED'))
