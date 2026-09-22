import re, io

def parse(path):
    txt = io.open(path, encoding='utf-8', errors='replace').read()
    blocks = re.split(r'\n--- ', txt)
    go, tr, comps = {}, {}, {}
    for b in blocks:
        m = re.match(r'!u!(\d+) &(\d+)', b)
        if not m:
            continue
        cls, fid = m.group(1), m.group(2)
        if cls == '1':
            nm = re.search(r'\n  m_Name: (.*)', b)
            act = re.search(r'\n  m_IsActive: (\d)', b)
            go[fid] = (nm.group(1).rstrip() if nm else '?', act.group(1) if act else '1')
        elif cls in ('4', '224'):
            g = re.search(r'\n  m_GameObject: \{fileID: (\d+)\}', b)
            f = re.search(r'\n  m_Father: \{fileID: (\d+)\}', b)
            ch = re.search(r'\n  m_Children:\n((?:  - \{fileID: \d+\}\n)*)', b)
            kids = re.findall(r'- \{fileID: (\d+)\}', ch.group(1)) if ch else []
            sz = re.search(r'\n  m_SizeDelta: \{x: ([-\d.e]+), y: ([-\d.e]+)\}', b)
            ap = re.search(r'\n  m_AnchoredPosition: \{x: ([-\d.e]+), y: ([-\d.e]+)\}', b)
            tr[fid] = (g.group(1) if g else None, f.group(1) if f else '0', kids,
                       sz.groups() if sz else ('?', '?'), ap.groups() if ap else ('?', '?'))
        else:
            g = re.search(r'\n  m_GameObject: \{fileID: (\d+)\}', b)
            if g:
                comps.setdefault(g.group(1), []).append(cls)
    return go, tr, comps

NAMES = {'114': 'MonoBehaviour', '222': 'CanvasRenderer', '223': 'Canvas', '95': 'Animator',
         '225': 'CanvasGroup', '331': 'RectMask2D'}
MONO = {}

def dump(path, rootname):
    go, tr, comps = parse(path)
    bygo = {}
    for tid, v in tr.items():
        if v[0]:
            bygo.setdefault(v[0], tid)
    out = []
    def walk(tid, depth, prefix):
        gid, father, kids, sz, ap = tr[tid]
        name, act = go.get(gid, ('?', '1'))
        cls = []
        for c in comps.get(gid, []):
            cls.append(NAMES.get(c, '!u!' + c))
        out.append('%s%s  [size=%sx%s pos=%s,%s active=%s %s]' % (
            '  ' * depth, name, sz[0], sz[1], ap[0], ap[1], act, ','.join(cls)))
        for k in kids:
            if k in tr:
                walk(k, depth + 1, prefix + '/' + name)
    for gid, (nm, act) in go.items():
        if nm == rootname and gid in bygo:
            walk(bygo[gid], 0, '')
    return out

base = 'D:/Codex project/Dragon/Assets/DragonBound/UI/Variants/'
for v, rel in (('V1', 'Scenes/Greybox_Main.unity'), ('V2', 'Scenes/Greybox_Main.unity'),
               ('V1', 'Content/Resources/prefabs/LoadingPanel.prefab')):
    p = base + v + '/' + rel
    print('========== %s :: %s' % (v, rel))
    print('\n'.join(dump(p, 'LoadingPanel')))
    print()
