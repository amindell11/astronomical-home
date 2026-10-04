"""Every occurrence of a prefab GUID in the given files, resolved through an editor fileID map.
usage: refreport.py <guid> <fileids.txt> <root> <files...>  -> rows on stdout"""
import re, sys
guid, idmap, root, files = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4:]
ids = {}
for line in open(idmap, encoding='utf-8'):
    k, _, v = line.rstrip('\n').partition(' ')
    ids[int(k)] = v
ids[100100000] = '<prefab asset>'
HDR = re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?$', re.M)
REF = re.compile(r'\{fileID: (-?\d+), guid: %s, type: \d\}' % guid)
for f in files:
    text = open(f'{root}/{f}', encoding='utf-8').read()
    flat = re.sub(r',\s*\n\s+type', ', type', text)
    hs = list(HDR.finditer(flat))
    for i, h in enumerate(hs):
        body = flat[h.end(): hs[i + 1].start() if i + 1 < len(hs) else len(flat)]
        doc = f'&{h.group(2)}'
        if int(h.group(1)) == 1001:
            src = re.search(r'm_SourcePrefab: \{fileID: (-?\d+), guid: (\w+)', body)
            if src and src.group(2) == guid:
                print(f'{f}\t{doc}\tPrefabInstance.m_SourcePrefab\t{src.group(1)}\t{ids.get(int(src.group(1)), "UNRESOLVED")}')
            for m in re.finditer(r'- target: \{fileID: (-?\d+), guid: (\w+), type: \d\}\n\s+propertyPath: (.*)\n\s+value: ?(.*)\n\s+objectReference: (\{[^}]*\})', body):
                if m.group(2) != guid: continue
                print(f'{f}\t{doc}\tMOD {m.group(3)}={m.group(4)} ref={m.group(5)}\t{m.group(1)}\t{ids.get(int(m.group(1)), "UNRESOLVED")}')
            for sec in ('m_RemovedComponents', 'm_RemovedGameObjects'):
                part = re.search(sec + r':(.*?)\n    m_', body, re.S)
                if part:
                    for m in REF.finditer(part.group(1)):
                        print(f'{f}\t{doc}\t{sec}\t{m.group(1)}\t{ids.get(int(m.group(1)), "UNRESOLVED")}')
            for m in re.finditer(r'targetCorrespondingSourceObject: \{fileID: (-?\d+), guid: (\w+)', body):
                if m.group(2) == guid: print(f'{f}\t{doc}\tadded-target\t{m.group(1)}\t{ids.get(int(m.group(1)), "UNRESOLVED")}')
            continue
        for m in REF.finditer(body):
            line = body[:m.start()].rsplit('\n', 1)[-1].strip()
            field = 'stripped.m_CorrespondingSourceObject' if 'm_CorrespondingSourceObject' in line else line.rstrip(':- ').split(':')[0] or 'list-entry'
            print(f'{f}\t{doc}{" stripped" if h.group(3) else ""}\t{field}\t{m.group(1)}\t{ids.get(int(m.group(1)), "UNRESOLVED")}')
