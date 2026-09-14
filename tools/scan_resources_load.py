# -*- coding: utf-8 -*-
"""Count Resources.Load(...) asset-path bindings (secondary category)."""
import os, re, collections, json

ROOT = r"D:\Codex project\Dragon\Assets"
PROJ = r"D:\Codex project\Dragon"
EXCLUDE = [os.path.join("TextMesh Pro", "Examples & Extras")]
RE_PL = re.compile(r'Resources\.Load(?:<[^>]*>)?\(\s*([^;]*?)\)\s*[;,)]')

cnt = collections.Counter()
files = collections.defaultdict(int)
by_file_kind = collections.defaultdict(collections.Counter)
rows = []
for dp, dn, fn in os.walk(ROOT):
    dn[:] = [d for d in dn if d not in ("Library", "obj", "Temp")]
    rel = os.path.relpath(dp, ROOT)
    if any(rel.startswith(x) for x in EXCLUDE):
        continue
    for n in fn:
        if not n.endswith(".cs"):
            continue
        p = os.path.join(dp, n)
        r = os.path.relpath(p, PROJ).replace("\\", "/")
        try:
            t = open(p, encoding="utf-8-sig").read()
        except Exception:
            continue
        for i, line in enumerate(t.splitlines(), 1):
            for m in RE_PL.finditer(line):
                if "/Tests/" in r:
                    cat = "测试"
                elif "/Editor/" in r:
                    cat = "编辑器工具"
                else:
                    cat = "运行时"
                kind = "Sprite" if "<Sprite>" in line else (
                    "GameObject" if "<GameObject>" in line else (
                        "Animator" if "RuntimeAnimatorController" in line else "其他"))
                cnt[cat] += 1
                files[r] += 1
                by_file_kind[r][kind] += 1
                rows.append(dict(file=r, line=i, cat=cat, kind=kind, arg=m.group(1).strip()))

print(dict(cnt), "total", sum(cnt.values()), "files", len(files))
print("by kind:", dict(collections.Counter(r["kind"] for r in rows)))
print()
for f, c in sorted(files.items(), key=lambda kv: -kv[1])[:20]:
    print(f"  {c:3d} {f}  {dict(by_file_kind[f])}")
json.dump(rows, open(os.path.join(PROJ, "tools", "resources_load_report.json"), "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)
