import re, sys, io, collections

def parse(path):
    txt = io.open(path, encoding='utf-8', errors='replace').read()
    blocks = re.split(r'\n--- ', txt)
    go = {}           # fileID -> name
    tr = {}           # fileID -> (goId, fatherId, [childIds])
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
    return go, tr

def dump(path, rootname, out):
    go, tr = parse(path)
    # goId -> trId
    bygo = {}
    for tid, (gid, father, kids) in tr.items():
        if gid:
            bygo.setdefault(gid, tid)

    def walk(tid, depth, lines, path_str):
        gid, father, kids = tr[tid]
        name = go.get(gid, '?')
        attrs = ''
        lines.append(('  ' * depth) + name + attrs)
        for k in kids:
            if k in tr:
                walk(k, depth + 1, lines, path_str + '/' + name)

    # find all nodes named rootname
    found = False
    for gid, nm in go.items():
        if nm == rootname and gid in bygo:
            lines = []
            walk(bygo[gid], 0, lines, '')
            out.append('=== %s :: root=%s (go %s) ===' % (path, rootname, gid))
            out.extend(lines)
            out.append('')
            found = True
    if not found:
        out.append('=== %s :: NO node named %s ===' % (path, rootname))

res = []
base = 'D:/Codex project/Dragon/Assets/DragonBound/UI/Variants/'
for v in ('V1', 'V2'):
    for rel in ('Content/Resources/prefabs/LoadingPanel.prefab',
                'Scenes/Greybox_Main.unity',
                'Scenes/Main.unity',
                'Scenes/Game.unity'):
        p = base + v + '/' + rel
        try:
            open(p, 'rb').close()
        except Exception:
            continue
        dump(p, 'LoadingPanel', res)

io.open('D:/Codex project/Dragon/TempArchiveCompare_20260918/hierarchy_out.txt', 'w', encoding='utf-8').write('\n'.join(res))
print('\n'.join(res))
