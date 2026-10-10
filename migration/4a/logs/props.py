import re,sys,collections
def load(p):
    comps=collections.OrderedDict(); cur=None; go={}
    for line in open(p,encoding='utf-8'):
        line=line.rstrip('\n')
        if line.startswith('== /'):
            m=re.match(r'== /(.*?) active=(\S+) layer=(\S+) tag=(\S+)',line); go[m.group(1)]=(m.group(2),m.group(3),m.group(4)); continue
        if line.startswith('  -- '):
            cur=line[5:]; comps[cur]={}; continue
        if line.startswith('     ') and cur:
            k,_,v=line[5:].partition(' = '); comps[cur][k]=v
    return comps,go
