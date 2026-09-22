import re, io

def parse(path):
    txt = io.open(path, encoding='utf-8', errors='replace').read()
    blocks = re.split(r'\n--- ', txt)
    go, rt = {}, {}
    for b in blocks:
        m = re.match(r'!u!(\d+) &(\d+)', b)
        if not m:
            continue
        cls, fid = m.group(1), m.group(2)
        if cls == '1':
            nm = re.search(r'\n  m_Name: (.*)', b)
            ia = re.search(r'\n  m_IsActive: (\d)', b)
            go[fid] = (nm.group(1).rstrip() if nm else '?', ia.group(1) if ia else '?')
        elif cls in ('4', '224'):
            def g(k):
                mm = re.search(r'\n  %s: \{x: ([-\d.eE]+), y: ([-\d.eE]+)\}' % k, b)
                return (float(mm.group(1)), float(mm.group(2))) if mm else None
            own = re.search(r'\n  m_GameObject: \{fileID: (\d+)\}', b)
            fa = re.search(r'\n  m_Father: \{fileID: (\d+)\}', b)
            rt[fid] = {'cls': cls, 'go': own.group(1) if own else None,
                       'father': fa.group(1) if fa else None,
                       'aMin': g('m_AnchorMin'), 'aMax': g('m_AnchorMax'),
                       'size': g('m_SizeDelta'), 'pos': g('m_AnchoredPosition')}
    return go, rt

TARGETS = ('Spawn', 'PathPoint_1', 'PathPoint_2', 'PathPoint_3', 'DragonGoal', 'ART_FixedBoardLaneLayer')

for v in ('V1', 'V2'):
    p = 'D:/Codex project/Dragon/Assets/DragonBound/UI/Variants/%s/Scenes/Greybox_Main.unity' % v
    go, rt = parse(p)
    print('===== %s =====' % v)
    go2rt = {}
    for tid, info in rt.items():
        go2rt.setdefault(info['go'], tid)
    for gid, (nm, act) in go.items():
        if nm not in TARGETS:
            continue
        tid = go2rt.get(gid)
        if tid is None:
            print('  %-20s (no rect, class=%s)' % (nm, '?'))
            continue
        info = rt[tid]
        pn = go.get(rt[info['father']]['go'], ('?',))[0] if info['father'] in rt else 'ROOT'
        print('  %-22s fid=%-20s father=%-22s active=%s aMin=%s aMax=%s pos=%s size=%s' % (
            nm, tid, pn, act, info['aMin'], info['aMax'], info['pos'], info['size']))
    print()
