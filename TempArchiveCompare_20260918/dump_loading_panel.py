"""Read-only Unity YAML dumper for the LoadingPanel subtree.

Usage:
    python dump_loading_panel.py <path-to-.unity-or-.prefab> [rootName]

Prints, per node: name, m_IsActive, RectTransform sizeDelta/anchoredPosition,
component types (Animator / CanvasGroup / Image / MonoBehaviour script), and
whether the node is a prefab instance root/stripped child.
"""
import re
import sys

SCRIPT_NAMES = {
    # filled lazily from .meta files if requested
}


def load_scripts(assets_root):
    """Map script guid -> file name for readable MonoBehaviour labels."""
    import glob
    import os
    mapping = {}
    for meta in glob.glob(os.path.join(assets_root, '**', '*.cs.meta'), recursive=True):
        try:
            text = open(meta, encoding='utf-8', errors='replace').read()
        except OSError:
            continue
        guid = re.search(r'guid:\s*([0-9a-f]+)', text)
        if guid:
            mapping[guid.group(1)] = os.path.basename(meta)[:-8]
    return mapping


def parse(path):
    text = open(path, encoding='utf-8', errors='replace').read()
    blocks = re.split(r'^--- ', text, flags=re.M)
    go = {}
    rt = {}
    comps = {}          # fileID -> (classId, label)
    for b in blocks:
        m = re.match(r'!u!(\d+) &(-?\d+)', b)
        if not m:
            continue
        cls, fid = m.group(1), m.group(2)
        if cls == '1':
            name = re.search(r'm_Name:\s*(.*)', b)
            active = re.search(r'm_IsActive:\s*(\d)', b)
            go[fid] = (name.group(1).strip() if name else '?',
                       active.group(1) if active else '?',
                       re.findall(r'component:\s*\{fileID:\s*(-?\d+)\}', b))
        elif cls == '224':
            father = re.search(r'm_Father:\s*\{fileID:\s*(-?\d+)\}', b)
            size = re.search(r'm_SizeDelta:\s*\{x:\s*([-\d.eE]+),\s*y:\s*([-\d.eE]+)\}', b)
            pos = re.search(r'm_AnchoredPosition:\s*\{x:\s*([-\d.eE]+),\s*y:\s*([-\d.eE]+)\}', b)
            owner = re.search(r'm_GameObject:\s*\{fileID:\s*(-?\d+)\}', b)
            rt[fid] = (father.group(1) if father else None, size.groups() if size else None,
                       pos.groups() if pos else None, owner.group(1) if owner else None)
        elif cls == '95':
            owner = re.search(r'm_GameObject:\s*\{fileID:\s*(-?\d+)\}', b)
            ctrl = re.search(r'm_Controller:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]+)', b)
            comps[fid] = ('Animator', owner.group(1) if owner else None,
                          'ctrl=%s' % (ctrl.group(2)[:8] if ctrl else 'NULL'))
        elif cls == '225':
            owner = re.search(r'm_GameObject:\s*\{fileID:\s*(-?\d+)\}', b)
            comps[fid] = ('CanvasGroup', owner.group(1) if owner else None, '')
        elif cls == '114':
            owner = re.search(r'm_GameObject:\s*\{fileID:\s*(-?\d+)\}', b)
            script = re.search(r'm_Script:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]+)', b)
            comps[fid] = ('MonoBehaviour', owner.group(1) if owner else None,
                          'scriptGUID=%s' % (script.group(2)[:8] if script else '?'))
        elif cls == '111':
            owner = re.search(r'm_GameObject:\s*\{fileID:\s*(-?\d+)\}', b)
            comps[fid] = ('Animation', owner.group(1) if owner else None, '')
        elif cls == '222':
            owner = re.search(r'm_GameObject:\s*\{fileID:\s*(-?\d+)\}', b)
            comps[fid] = ('CanvasRenderer', owner.group(1) if owner else None, '')
    return go, rt, comps, blocks


def main():
    path = sys.argv[1]
    root_name = sys.argv[2] if len(sys.argv) > 2 else None
    go, rt, comps, blocks = parse(path)
    go2rt = {v[3]: k for k, v in rt.items()}

    def label_for(gid):
        out = []
        for cid in go[gid][2]:
            if cid in comps:
                kind, owner, extra = comps[cid]
                if kind == 'MonoBehaviour':
                    script = re.search(r'm_Script:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]+)',
                                       next(b for b in blocks if b.startswith('!u!114 &' + cid)))
                    out.append('MB(%s %s)' % (extra, script.groups()[0] if script else '?'))
                else:
                    out.append(kind + (' %s' % extra if extra else ''))
        return ','.join(out)

    def children_of(gid):
        r = go2rt.get(gid)
        if r is None:
            return []
        return [c for c in go if go2rt.get(c) and rt[go2rt[c]][0] == r]

    def walk(gid, depth=0):
        name, active, _ = go[gid]
        r = go2rt.get(gid)
        size = rt[r][1] if r else None
        pos = rt[r][2] if r else None
        print('%s%s  [active=%s] size=%s pos=%s  {%s}' %
              ('  ' * depth, name, active, size, pos, label_for(gid)))
        for c in children_of(gid):
            walk(c, depth + 1)

    if root_name:
        matches = [g for g in go if go[g][0] == root_name]
        print('=== matched %d node(s) named %r ===' % (len(matches), root_name))
        for g in matches:
            walk(g)
    else:
        roots = [g for g in go
                 if go2rt.get(g) and rt[go2rt[g]][0] in (None, '0')]
        for r in roots:
            walk(r)

    print('\n=== prefab instance roots (!u!1001) ===')
    for b in blocks:
        if b.startswith('!u!1001'):
            src = re.search(r'm_SourcePrefab:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]+)', b)
            mod = re.search(r'propertyPath:\s*(.*)', b)
            print('  guid=%s modified=%s' % (src.group(2)[:12] if src else '?',
                                             mod.group(1).strip() if mod else '-'))


if __name__ == '__main__':
    main()
