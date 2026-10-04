import collections, sys
def load(p, skip_july=True):
    rows = []
    for l in open(p, encoding='utf-8'):
        f, doc, field, fid, logical = l.rstrip('\n').split('\t')
        if skip_july and 'Ship_1_Vanguard' in f: continue
        key = (f, field)
        rows.append((key, logical, fid))
    return rows
b = load(sys.argv[1]); a = load(sys.argv[2])
cb = collections.Counter((k, l) for k, l, _ in b); ca = collections.Counter((k, l) for k, l, _ in a)
print(f'before rows={len(b)} after rows={len(a)}')
print('resolved before:', sum(1 for _, l, _ in b if l != 'UNRESOLVED'), 'unresolved before:', sum(1 for _, l, _ in b if l == 'UNRESOLVED'))
print('resolved after :', sum(1 for _, l, _ in a if l != 'UNRESOLVED'), 'unresolved after :', sum(1 for _, l, _ in a if l == 'UNRESOLVED'))
missing = cb - ca; extra = ca - cb
print('in before, not after (by file+field+logical):', dict(missing) or 'none')
print('in after, not before:', dict(extra) or 'none')
# dead ones must keep the same fileID
db = sorted((k, fid) for k, l, fid in b if l == 'UNRESOLVED'); da = sorted((k, fid) for k, l, fid in a if l == 'UNRESOLVED')
print('dead entries identical (file, field, fileID):', db == da, len(db))
per = collections.Counter(k[0] for k, _, _ in b)
for f, n in per.items(): print(f'  {f}: {n} before / {sum(1 for k,_,_ in a if k[0]==f)} after')
