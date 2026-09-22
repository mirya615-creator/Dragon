import re, io

def parse(path):
    txt = io.open(path, encoding='utf-8', errors='replace').read()
    blocks = re.split(r'\n--- ', txt)
    go = {}
    tr = {}
    for b in blocks:
        m = re.match(r'!u!(\d+) &(\d+)', b)
        if not m:
            continue
        cls, fid = m.group(1), m.group(2)
        if cls == '1':
            nm = re.search(r'\n  m_Name: (.*)', b)
            go[fid] = nm.group(1).rstrip() if nm else '?'
        elif cls in ('4', '224'):
            g = re.search(r'\n  m_GameObject: \{fileID: (\d+)\}', b)
            f = re.search(r'\n  m_Father: \{fileID: (\d+)\}', b)
            info = {'go': g.group(1) if g else None, 'father': f.group(1) if f else '0'}
            for key, pat in (('ap', r'\n  m_AnchoredPosition: (.*)'), ('sd', r'\n  m_SizeDelta: (.*)'),
                             ('amin', r'\n  m_AnchorMin: (.*)'), ('amax', r'\n  m_AnchorMax: (.*)'),
                             ('ls', r'\n  m_LocalScale: (.*)'), ('lp', r'\n  m_LocalPosition: (.*)')):
                mm = re.search(pat, b)
                info[key] = mm.group(1).strip() if mm else None
            tr[fid] = info
    return go, tr

def path_of(tr, go, tid):
    names = []
    seen = set()
    cur = tid
    while cur in tr and cur not in seen:
        seen.add(cur)
        names.append(go.get(tr[cur]['go'], '?'))
        cur = tr[cur]['father']
    return '/'.join(reversed(names))

TARGETS = ('Spawn', 'PathPoint_1', 'PathPoint_2', 'PathPoint_3', 'DragonGoal')

for v in ('V1', 'V2'):
    p = 'D:/Codex project/Dragon/Assets/DragonBound/UI/Variants/%s/Scenes/Greybox_Main.unity' % v
    go, tr = parse(p)
    print('===== %s Greybox_Main.unity =====' % v)
    for tid, info in tr.items():
        name = go.get(info['go'], '?')
        if name not in TARGETS:
            continue
        print('  %-42s ap=%s sd=%s scale=%s' % (path_of(tr, go, tid), info['ap'], info['sd'], info['ls']))
    print()
