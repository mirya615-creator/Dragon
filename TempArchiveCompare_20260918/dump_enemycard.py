import re, io

BASE = 'D:/Codex project/Dragon/Assets/DragonBound/UI/Variants/'
OUT = []

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
            ch = re.search(r'\n  m_Children:\n((?:  - \{fileID: \d+\}\n)*)', b)
            kids = re.findall(r'- \{fileID: (\d+)\}', ch.group(1)) if ch else []
            tr[fid] = (g.group(1) if g else None, f.group(1) if f else '0', kids)
    return go, tr, blocks


def walk(tr, go, tid, depth, lines):
    gid, father, kids = tr[tid]
    lines.append(('  ' * depth) + go.get(gid, '?'))
    for k in kids:
        if k in tr:
            walk(tr, go, k, depth + 1, lines)


CLS = {'1': 'GameObject', '4': 'Transform', '224': 'RectTransform', '114': 'MonoBehaviour',
       '212': 'SpriteRenderer', '95': 'Animator', '222': 'CanvasRenderer', '223': 'Canvas'}

WANT = ('ART_EnemyAnimation', 'Image', 'Animator', 'ART_EnemyHpTrack')

for v in ('V1', 'V2'):
    p = BASE + v + '/Content/UI/Prefabs/Components/EnemyCard.prefab'
    go, tr, blocks = parse(p)
    OUT.append('===== %s EnemyCard.prefab =====' % v)
    roots = [t for t, (g, f, k) in tr.items() if f == '0']
    for r in roots:
        walk(tr, go, r, 0, OUT)
    OUT.append('')
    for b in blocks:
        m = re.match(r'!u!(\d+) &(\d+)', b)
        if not m:
            continue
        if m.group(1) != '1':
            continue
        nm = re.search(r'\n  m_Name: (.*)', b)
        nm = nm.group(1).strip() if nm else ''
        if nm != 'ART_EnemyAnimation':
            continue
        comps = re.findall(r'- component: \{fileID: (\d+)\}', b)
        OUT.append('  [GO %s] name=%s comps=%s' % (m.group(2), nm, comps))
        for c in comps:
            for b2 in blocks:
                m2 = re.match(r'!u!(\d+) &%s\b' % c, b2)
                if m2:
                    head = '    class=%s' % CLS.get(m2.group(1), m2.group(1))
                    if m2.group(1) == '114':
                        script = re.search(r'\n  m_Script: \{fileID: \d+, guid: (\w+)', b2)
                        head += ' scriptGuid=%s' % (script.group(1) if script else '?')
                    if m2.group(1) == '224':
                        ap = re.search(r'\n  m_AnchoredPosition: (.*)', b2)
                        sd = re.search(r'\n  m_SizeDelta: (.*)', b2)
                        am = re.search(r'\n  m_AnchorMin: (.*)', b2)
                        ax = re.search(r'\n  m_AnchorMax: (.*)', b2)
                        head += ' anchorMin=%s anchorMax=%s pos=%s size=%s' % (
                            am.group(1) if am else '?', ax.group(1) if ax else '?',
                            ap.group(1) if ap else '?', sd.group(1) if sd else '?')
                    if m2.group(1) == '1':
                        ia = re.search(r'\n  m_IsActive: (\d+)', b2)
                        head += ' isActive=%s' % (ia.group(1) if ia else '?')
                    OUT.append(head)
    OUT.append('')

io.open('D:/Codex project/Dragon/TempArchiveCompare_20260918/enemycard_out.txt', 'w', encoding='utf-8').write('\n'.join(OUT))
print('\n'.join(OUT))
