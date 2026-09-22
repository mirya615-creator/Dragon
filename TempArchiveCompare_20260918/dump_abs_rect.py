"""Read-only: compute absolute (canvas-space) rects for a UI subtree in a
Unity .unity/.prefab YAML file.

Usage:
    python dump_abs_rect.py <file> <rootNodeName> [canvasW] [canvasH]

Assumes the named root node is a full-screen stretch anchored at the canvas
center (origin at the canvas centre, +y up), which matches how the authored
LoadingPanel is set up.
"""
import re
import sys


def parse(path):
    text = open(path, encoding='utf-8', errors='replace').read()
    blocks = re.split(r'^--- ', text, flags=re.M)
    go, rt, comps, name_by_fid = {}, {}, {}, {}
    for b in blocks:
        m = re.match(r'!u!(\d+) &(-?\d+)', b)
        if not m:
            continue
        cls, fid = m.group(1), m.group(2)
        if cls == '1':
            name = re.search(r'm_Name:\s*(.*)', b)
            active = re.search(r'm_IsActive:\s*(\d)', b)
            go[fid] = (name.group(1).strip() if name else '?',
                       active.group(1) if active else '?')
        elif cls == '224':
            def g(k):
                mm = re.search(r'%s:\s*\{x:\s*([-\d.eE]+),\s*y:\s*([-\d.eE]+)\}' % k, b)
                return (float(mm.group(1)), float(mm.group(2))) if mm else None
            owner = re.search(r'm_GameObject:\s*\{fileID:\s*(-?\d+)\}', b)
            rt[fid] = {
                'father': (re.search(r'm_Father:\s*\{fileID:\s*(-?\d+)\}', b) or [None, None])[1]
                if re.search(r'm_Father:\s*\{fileID:\s*(-?\d+)\}', b) else None,
                'go': owner.group(1) if owner else None,
                'aMin': g('m_AnchorMin'), 'aMax': g('m_AnchorMax'),
                'pivot': g('m_Pivot'), 'size': g('m_SizeDelta'),
                'pos': g('m_AnchoredPosition'), 'scale': g('m_LocalScale'),
            }
        elif cls == '114':
            owner = re.search(r'm_GameObject:\s*\{fileID:\s*(-?\d+)\}', b)
            script = re.search(r'm_Script:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]+)', b)
            comps.setdefault(owner.group(1) if owner else None, []).append(
                'MB:' + (script.group(2)[:12] if script else '?'))
        else:
            owner = re.search(r'm_GameObject:\s*\{fileID:\s*(-?\d+)\}', b)
            if owner:
                comps.setdefault(owner.group(1), []).append('cls' + cls)
    go2rt = {v['go']: k for k, v in rt.items()}
    return go, rt, comps, go2rt


def main():
    path, root_name = sys.argv[1], sys.argv[2]
    cw = float(sys.argv[3]) if len(sys.argv) > 3 else 1080.0
    ch = float(sys.argv[4]) if len(sys.argv) > 4 else 1920.0
    go, rt, comps, go2rt = parse(path)

    def children_of(gid):
        r = go2rt.get(gid)
        return [c for c in go if go2rt.get(c) and rt[go2rt[c]]['father'] == r]

    def walk(gid, rect, depth=0):
        name, active = go[gid]
        r = go2rt.get(gid)
        if r is None:
            print('%s%s (no RectTransform)' % ('  ' * depth, name))
            return
        t = rt[r]
        ax, ay = t['aMin'] or (0.5, 0.5)
        bx, by = t['aMax'] or (0.5, 0.5)
        px, py = t['pivot'] or (0.5, 0.5)
        sx, sy = t['size'] or (0, 0)
        ox, oy = t['pos'] or (0, 0)
        pw, ph = rect[2] - rect[0], rect[3] - rect[1]
        w = (bx - ax) * pw + sx
        h = (by - ay) * ph + sy
        # anchor reference point = Lerp(anchorMin, anchorMax, pivot) of the anchor rect
        refx = rect[0] + ax * pw + (bx - ax) * pw * px
        refy = rect[1] + ay * ph + (by - ay) * ph * py
        minx = refx + ox - px * w
        miny = refy + oy - py * h
        box = (minx, miny, minx + w, miny + h)
        print('%s%-14s active=%s  x[%8.1f,%8.1f] y[%8.1f,%8.1f]  anchors=(%.2f,%.2f)-(%.2f,%.2f)  %s'
              % ('  ' * depth, name, active, box[0], box[2], box[1], box[3],
                 ax, ay, bx, by, ','.join(comps.get(gid, []))))
        for c in children_of(gid):
            walk(c, box, depth + 1)

    for gid in [g for g in go if go[g][0] == root_name]:
        # Root is treated as a full-screen stretch panel centred on the canvas.
        walk(gid, (-cw / 2.0, -ch / 2.0, cw / 2.0, ch / 2.0))


if __name__ == '__main__':
    main()
