# -*- coding: utf-8 -*-
"""Scan the Dragon project for UI bindings done by hierarchy-path access.

Targets:
  - Transform.Find("<child or A/B/C path>")
  - GameObject.Find("...")
  - GetChild(<index>) used to reach UI nodes
  - FindObjectOfType / FindGameObjectsWithTag style global lookups (marked separately)
Excludes third-party sample code (TextMesh Pro Examples, Library, obj, Temp).
"""
import os
import re
import json
import collections

ROOT = r"D:\Codex project\Dragon\Assets"
EXCLUDE_DIRS = [
    os.path.join("TextMesh Pro", "Examples & Extras"),
    os.path.join("TextMesh Pro", "Examples"),
    "Plugins",
    "ThirdParty",
]

RE_FIND = re.compile(r'\.Find\(\s*"([^"]*)"\s*(?:,|\))')
RE_GO_FIND = re.compile(r'GameObject\.Find\(\s*"([^"]*)"')
RE_GETCHILD = re.compile(r'\.GetChild\(\s*([^)]+?)\s*\)')
RE_FIND_OF_TYPE = re.compile(r'\bFindObjectOfType<|\bFindObjectsOfType<|\bFindObjectsByType<')
RE_TAG_FIND = re.compile(r'GameObject\.FindWithTag\(\s*"([^"]*)"')
RE_GO_FIND_TAG = re.compile(r'GameObject\.FindGameObjectWithTag\(\s*"([^"]*)"')

UI_HINT_RE = re.compile(
    r'Button|Image|Text|TMP|RectTransform|Canvas|Panel|Slider|Toggle|Scroll|Dropdown|Input|Child|transform|Transform'
)

rows = []

for dirpath, dirnames, filenames in os.walk(ROOT):
    dirnames[:] = [d for d in dirnames if d not in ("Library", "obj", "Temp", "Logs", ".git")]
    rel_dir = os.path.relpath(dirpath, ROOT)
    if any(rel_dir.startswith(x) for x in EXCLUDE_DIRS):
        continue
    for name in filenames:
        if not name.endswith(".cs"):
            continue
        full = os.path.join(dirpath, name)
        rel = os.path.relpath(full, r"D:\Codex project\Dragon").replace("\\", "/")
        try:
            with open(full, "r", encoding="utf-8-sig") as fh:
                lines = fh.readlines()
        except Exception:
            continue
        for i, line in enumerate(lines, 1):
            stripped = line.strip()
            if stripped.startswith("//") or stripped.startswith("*"):
                # keep comment-only lines out of the count, they are documentation
                commented = True
            else:
                commented = False

            for m in RE_FIND.finditer(line):
                arg = m.group(1)
                rows.append(dict(file=rel, line=i, kind="Transform.Find",
                                 arg=arg, expr=stripped, commented=commented,
                                 pathish="/" in arg))
            for m in RE_GO_FIND.finditer(line):
                rows.append(dict(file=rel, line=i, kind="GameObject.Find",
                                 arg=m.group(1), expr=stripped, commented=commented,
                                 pathish="/" in m.group(1)))
            for m in RE_GETCHILD.finditer(line):
                rows.append(dict(file=rel, line=i, kind="GetChild",
                                 arg=m.group(1), expr=stripped, commented=commented,
                                 pathish=False))
            if RE_FIND_OF_TYPE.search(line):
                rows.append(dict(file=rel, line=i, kind="FindObjectOfType",
                                 arg="", expr=stripped, commented=commented, pathish=False))
            for m in RE_TAG_FIND.finditer(line):
                rows.append(dict(file=rel, line=i, kind="FindWithTag",
                                 arg=m.group(1), expr=stripped, commented=commented, pathish=False))
            for m in RE_GO_FIND_TAG.finditer(line):
                rows.append(dict(file=rel, line=i, kind="FindGameObjectWithTag",
                                 arg=m.group(1), expr=stripped, commented=commented, pathish=False))


def category(rel):
    if "/Tests/" in rel:
        return "测试"
    if "/Editor/" in rel:
        return "编辑器工具"
    return "运行时"


for r in rows:
    r["cat"] = category(r["file"])

# ---- aggregate ----
by_cat = collections.Counter(r["cat"] for r in rows)
by_kind = collections.Counter(r["kind"] for r in rows)
by_file = collections.defaultdict(collections.Counter)
for r in rows:
    by_file[r["file"]][r["kind"]] += 1

pathish = [r for r in rows if r["kind"] in ("Transform.Find", "GameObject.Find") and "/" in r["arg"]]

out = {
    "total": len(rows),
    "by_cat": dict(by_cat),
    "by_kind": dict(by_kind),
    "files": {k: dict(v) for k, v in sorted(by_file.items(), key=lambda kv: -sum(kv[1].values()))},
    "path_literal_count": len(pathish),
    "rows": rows,
}
with open(r"D:\Codex project\Dragon\tools\ui_path_binding_report.json", "w", encoding="utf-8") as fh:
    json.dump(out, fh, ensure_ascii=False, indent=1)

print("TOTAL:", len(rows))
print("BY KIND:", dict(by_kind))
print("BY CAT:", dict(by_cat))
print("MULTI-SEGMENT PATH LITERALS:", len(pathish))
print()
print("=== top files ===")
for f, c in sorted(by_file.items(), key=lambda kv: -sum(kv[1].values()))[:70]:
    print(f"{sum(c.values()):4d}  {f}   {dict(c)}")
