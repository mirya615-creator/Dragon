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
            comps = re.findall(r'- component: \{fileID: (\d+)\}', b)
            go[fid] = (nm.group(1).rstrip() if nm else '?', comps)
        elif cls in ('4', '224'):
            g = re.search(r'\n  m_GameObject: \{fileID: (\d+)\}', b)
            f = re.search(r'\n  m_Father: \{fileID: (\d+)\}', b)
            tr[fid] = (g.group(1) if g else None, f.group(1) if f else '0')
    return go, tr, blocks

CLS = {'1': 'GameObject', '4': 'Transform', '224': 'RectTransform', '114': 'MonoBehaviour',
       '212': 'SpriteRenderer', '95': 'Animator', '222': 'CanvasRenderer', '223': 'Canvas',
       '225': 'CanvasGroup'}

for v in ('V1', 'V2'):
    p = 'D:/Codex project/Dragon/Assets/DragonBound/UI/Variants/%s/Content/UI/Prefabs/Components/EnemyCard.prefab' % v
    go, tr, blocks = parse(p)
    print('===== %s EnemyCard =====' % v)
    for gid, (nm, comps) in go.items():
        parts = []
        for c in comps:
            for b2 in blocks:
                m2 = re.match(r'!u!(\d+) &%s\b' % c, b2)
                if not m2:
                    continue
                cls = CLS.get(m2.group(1), m2.group(1))
                extra = ''
                if m2.group(1) == '114':
                    s = re.search(r'\n  m_Script: \{fileID: \d+, guid: (\w+)', b2)
                    extra = ':' + (s.group(1) if s else '?')
                if m2.group(1) == '95':
                    ctl = re.search(r'\n  m_Controller: \{fileID: \d+, guid: (\w+)|m_Controller: \{fileID: (\d+)', b2)
                    keep = re.search(r'\n  m_KeepAnimatorStateOnDisable: (\d+)', b2)
                    extra = ':ctrl=%s keepOnDisable=%s' % (
                        (ctl.group(1) or ctl.group(2)) if ctl else '?', keep.group(1) if keep else '?')
                parts.append(cls + extra)
        parent = tr.get([t for t, (g, f) in tr.items() if g == gid][0], (None, ''))[1] if any(g == gid for g, f in tr.values()) else '?'
        pname = go.get(parent, ('?', []))[0] if parent in go else 'ROOT'
        print('  %-20s parent=%-20s comps=%s' % (nm, pname, parts))
    print()
