# -*- coding: utf-8 -*-
"""把 ui_path_binding_report.json / resources_load_report.json 渲染成
可人工查阅的全量 Markdown 清单 + 机器可读 CSV。
用法: python tools/gen_path_binding_list.py
"""
import json
import os
import re
import collections

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")
EXCLUDE_DIRS = ("TextMesh Pro/Examples & Extras",)
UI_JSON = os.path.join(ROOT, "tools", "ui_path_binding_report.json")
RES_JSON = os.path.join(ROOT, "tools", "resources_load_report.json")
OUT_MD = os.path.join(ROOT, "Docs", "UI路径绑定全量清单.md")
OUT_CSV = os.path.join(ROOT, "tools", "ui_path_binding_list.csv")
OUT_RES_CSV = os.path.join(ROOT, "tools", "resources_load_list.csv")
OUT_DB_CSV = os.path.join(ROOT, "tools", "assetdatabase_list.csv")
OUT_SC_CSV = os.path.join(ROOT, "tools", "scene_load_list.csv")

CAT_ORDER = ["运行时", "编辑器工具", "测试"]
CAT_TITLE = {
    "运行时": "运行时（生产代码，需重点维护）",
    "编辑器工具": "编辑器工具（构建期一次性查找）",
    "测试": "测试（UI 结构断言，改动层级时会一并打断，属良性耦合）",
}
KIND_ORDER = ["Transform.Find", "GameObject.Find", "GetChild"]


def esc(s):
    return (s or "").replace("|", "\\|").replace("\n", " ").strip()


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


RE_ASSETDB = re.compile(r'AssetDatabase\.(LoadAssetAtPath|LoadMainAssetAtPath)\s*(?:<[^>]*>)?\s*\(\s*([^,)\n]+)')
RE_SCENE = re.compile(r'SceneManager\.(?:LoadScene|LoadSceneAsync)\s*\(\s*"([^"]*)"')
RE_SCENE_VAR = re.compile(r'SceneManager\.(?:LoadScene|LoadSceneAsync)\s*\(\s*([A-Za-z_][A-Za-z0-9_.]*)\s*[,)]')
RE_CONST = re.compile(r'(?:const|static\s+readonly)\s+string\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?);', re.S)
RE_QSTR = re.compile(r'"([^"]*)"')
RE_TOKEN = re.compile(r'[A-Za-z_][A-Za-z0-9_.]*')


def build_consts(txt):
    """把同文件里的 const/static readonly string 常量化简成可读值。
    支持 `Root + "Suffix"` 形式的同文件常量拼接；无法完全解析时用 … 前缀标记。"""
    raw = {k: " ".join(v.split()) for k, v in RE_CONST.findall(txt)}
    resolved = {}

    def resolve(name, depth=0):
        if name in resolved:
            return resolved[name]
        if depth > 8 or name not in raw:
            return None
        chunks = re.split(r'("(?:[^"\\]|\\.)*")', raw[name])
        out, unresolved = [], False
        for c in chunks:
            if not c:
                continue
            if c.startswith('"') and c.endswith('"'):
                out.append(c[1:-1])
            else:
                for tok in RE_TOKEN.findall(c):
                    sub = resolve(tok, depth + 1)
                    if sub is not None:
                        out.append(sub)
                    else:
                        unresolved = True
                if re.search(r'[A-Za-z_]', RE_TOKEN.sub("", c)) is None:
                    pass
        val = "".join(out)
        if unresolved:
            val = "…" + val
        resolved[name] = val
        return val

    for k in list(raw):
        resolve(k)
    return resolved


def scan_extra():
    """补充：编辑器资产路径 AssetDatabase.* 与场景名加载 SceneManager.LoadScene。
    参数为字面量时取字面量；为标识符时回填同文件 const/static readonly string 常量值。"""
    db, sc = [], []
    for dp, dn, fn in os.walk(ASSETS):
        dn[:] = [d for d in dn if d not in ("Library", "obj", "Temp")]
        rel = os.path.relpath(dp, ASSETS).replace("\\", "/")
        if any(rel.startswith(x) for x in EXCLUDE_DIRS):
            continue
        for n in fn:
            if not n.endswith(".cs"):
                continue
            p = os.path.join(dp, n)
            rp = os.path.relpath(p, ROOT).replace("\\", "/")
            try:
                txt = open(p, encoding="utf-8-sig").read()
            except Exception:
                continue
            cat = "测试" if "/Tests/" in rp else ("编辑器工具" if "/Editor/" in rp else "运行时")
            consts = build_consts(txt)

            def arg_of(raw):
                raw = raw.strip()
                if raw.startswith('"') and raw.endswith('"'):
                    return raw.strip('"'), "字面量"
                key = raw.split(".")[-1]
                if key in consts:
                    return consts[key], "常量"
                return raw, "变量"

            for m in RE_ASSETDB.finditer(txt):
                ln = txt[:m.start()].count("\n") + 1
                val, src = arg_of(m.group(2))
                db.append({"file": rp, "line": ln, "kind": m.group(1) + "(" + src + ")",
                           "arg": val, "cat": cat})
            for m in RE_SCENE.finditer(txt):
                ln = txt[:m.start()].count("\n") + 1
                sc.append({"file": rp, "line": ln, "kind": "LoadScene", "arg": m.group(1), "cat": cat})
            for m in RE_SCENE_VAR.finditer(txt):
                ln = txt[:m.start()].count("\n") + 1
                val, _ = arg_of(m.group(1))
                sc.append({"file": rp, "line": ln, "kind": "LoadScene(变量)", "arg": val, "cat": cat})
    return db, sc


def main():
    ui = load(UI_JSON)
    res = load(RES_JSON)
    db, sc = scan_extra()

    # ---- 只保留路径型查找 ----
    rows = [r for r in ui["rows"] if r["kind"] in KIND_ORDER and not r.get("commented")]
    by_cat = collections.defaultdict(list)
    for r in rows:
        by_cat[r["cat"]].append(r)

    lines = []
    A = lines.append
    A("# UI / 资产路径绑定全量清单")
    A("")
    A("> 本文由 `tools/gen_path_binding_list.py` 从扫描结果自动生成，可重复运行。")
    A("> 扫描对象：`Assets/**/*.cs`（排除 `TextMesh Pro/Examples & Extras`）。")
    A("> 路径型查找 = `Transform.Find(\"A/B/C\")` / `GameObject.Find(\"...\")` / `GetChild(index)`；")
    A("> 资产路径 = `Resources.Load<T>(\"路径\")`。`FindObjectOfType` 不按路径，已排除。")
    A("")

    # ---- 总览 ----
    A("## 一、总览")
    A("")
    A("| 项目 | 数量 |")
    A("|---|---:|")
    A("| 节点路径访问总处数 | %d |" % len(rows))
    A("| 涉及文件数 | %d |" % len(set(r["file"] for r in rows)))
    A("| 去重路径字面量 | %d |" % len(set(r["arg"] for r in rows if r["arg"])))
    for k in KIND_ORDER:
        A("| %s | %d |" % (k, sum(1 for r in rows if r["kind"] == k)))
    A("| `Resources.Load<T>` 资产路径 | %d |" % len(res))
    A("| 编辑器 `AssetDatabase` 资产路径 | %d |" % len(db))
    A("| 场景名加载 `SceneManager.LoadScene` | %d |" % len(sc))
    A("")
    A("按归属分布：")
    A("")
    A("| 归属 | 节点路径 | 文件数 | 资产路径 |")
    A("|---|---:|---:|---:|")
    for c in CAT_ORDER:
        rr = by_cat.get(c, [])
        rc = sum(1 for r in res if r.get("cat") == c)
        A("| %s | %d | %d | %d |" % (c, len(rr), len(set(r["file"] for r in rr)), rc))
    A("")

    # ---- 深路径摘要 ----
    deep = [r for r in rows if r["kind"] in ("Transform.Find", "GameObject.Find") and "/" in (r["arg"] or "")]
    deep_lit = collections.Counter(r["arg"] for r in deep)
    A("## 二、多段深路径（改层级即断链，%d 处 / %d 个不同路径）" % (len(deep), len(deep_lit)))
    A("")
    A("| 路径字面量 | 出现次数 | 段数 |")
    A("|---|---:|---:|")
    for p, c in sorted(deep_lit.items(), key=lambda kv: (-kv[1], kv[0])):
        A("| `%s` | %d | %d |" % (esc(p), c, p.count("/") + 1))
    A("")

    # ---- 单段查找（弱契约） ----
    single = [r for r in rows if r["kind"] in ("Transform.Find", "GameObject.Find") and "/" not in (r["arg"] or "")]
    A("## 三、单段名称查找（%d 处）" % len(single))
    A("")
    A("> 只按名字在**直接子节点**里找，父子关系一变就找不到；同名节点还会取错。")
    A("")
    A("| 文件 | 行 | 方式 | 目标名 |")
    A("|---|---:|---|---|")
    for r in sorted(single, key=lambda r: (r["file"], r["line"])):
        A("| `%s` | %d | %s | `%s` |" % (esc(r["file"]), r["line"], r["kind"], esc(r["arg"])))
    A("")

    # ---- GetChild ----
    gc = [r for r in rows if r["kind"] == "GetChild"]
    A("## 四、按序号访问 `GetChild(index)`（%d 处）" % len(gc))
    A("")
    A("> UI 子节点顺序一变就静默指向错误控件，且不报错。")
    A("")
    A("| 文件 | 行 | 序号 | 代码 |")
    A("|---|---:|---:|---|")
    for r in sorted(gc, key=lambda r: (r["file"], r["line"])):
        A("| `%s` | %d | %s | `%s` |" % (esc(r["file"]), r["line"], esc(r["arg"]), esc(r["expr"])))
    A("")

    # ---- 分类明细 ----
    A("## 五、节点路径访问全量明细")
    A("")
    for c in CAT_ORDER:
        rr = by_cat.get(c, [])
        A("### 5.%d %s —— 共 %d 处" % (CAT_ORDER.index(c) + 1, CAT_TITLE[c], len(rr)))
        A("")
        byfile = collections.defaultdict(list)
        for r in rr:
            byfile[r["file"]].append(r)
        for f in sorted(byfile, key=lambda k: (0 if not k.startswith("Assets/DragonBound/Editor") else 1, -len(byfile[k]), k)):
            items = sorted(byfile[f], key=lambda r: r["line"])
            A("**`%s`** —— %d 处" % (esc(f), len(items)))
            A("")
            A("| 行 | 方式 | 目标 | 代码 |")
            A("|---:|---|---|---|")
            for r in items:
                A("| %d | %s | `%s` | `%s` |" % (r["line"], r["kind"], esc(r["arg"]) or "—", esc(r["expr"]) or "—"))
            A("")

    # ---- 资产路径 ----
    A("## 六、`Resources.Load<T>` 资产路径全量明细（%d 处）" % len(res))
    A("")
    kind_cnt = collections.Counter(r["kind"] for r in res)
    A("类型分布：" + "、".join("%s %d" % (k, v) for k, v in kind_cnt.most_common()))
    A("")
    for c in CAT_ORDER:
        rr = [r for r in res if r.get("cat") == c]
        if not rr:
            continue
        A("### 6.%d %s —— 共 %d 处" % (CAT_ORDER.index(c) + 1, CAT_TITLE[c], len(rr)))
        A("")
        A("| 文件 | 行 | 类型 | 路径 |")
        A("|---|---:|---|---|")
        for r in sorted(rr, key=lambda r: (r["file"], r["line"])):
            A("| `%s` | %d | %s | `%s` |" % (esc(r["file"]), r["line"], r["kind"], esc(r["arg"]) or "—"))
        A("")

    # ---- 补充：编辑器资产路径 ----
    A("## 七、编辑器 `AssetDatabase` 资产路径（%d 处）" % len(db))
    A("")
    A("> 按字符串路径读写工程内资产，改文件位置即断链；仅编辑器/测试使用。")
    A("")
    A("| 文件 | 行 | 方式 | 路径 |")
    A("|---|---:|---|---|")
    for r in sorted(db, key=lambda r: (r["file"], r["line"])):
        A("| `%s` | %d | %s | `%s` |" % (esc(r["file"]), r["line"], r["kind"], esc(r["arg"]) or "—"))
    A("")

    # ---- 补充：场景名加载 ----
    A("## 八、场景名路径加载 `SceneManager.LoadScene`（%d 处）" % len(sc))
    A("")
    seen_scenes = collections.Counter(r["arg"] for r in sc)
    A("涉及场景：" + "、".join("`%s`(%d)" % (k, v) for k, v in seen_scenes.most_common()))
    A("")
    A("| 文件 | 行 | 场景名 |")
    A("|---|---:|---|")
    for r in sorted(sc, key=lambda r: (r["file"], r["line"])):
        A("| `%s` | %d | `%s` |" % (esc(r["file"]), r["line"], esc(r["arg"])))
    A("")

    with open(OUT_MD, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    # ---- CSV ----
    with open(OUT_CSV, "w", encoding="utf-8-sig", newline="") as f:
        f.write("file,line,kind,arg,cat,expr\n")
        for r in sorted(rows, key=lambda r: (r["file"], r["line"])):
            expr = (r["expr"] or "").replace('"', '""')
            A2 = '"%s",%d,"%s","%s","%s","%s"\n' % (
                r["file"], r["line"], r["kind"], (r["arg"] or "").replace('"', '""'), r["cat"], expr)
            f.write(A2)

    with open(OUT_RES_CSV, "w", encoding="utf-8-sig", newline="") as f:
        f.write("file,line,kind,arg,cat\n")
        for r in sorted(res, key=lambda r: (r["file"], r["line"])):
            f.write('"%s",%d,"%s","%s","%s"\n' % (
                r["file"], r["line"], r["kind"], (r["arg"] or "").replace('"', '""'), r["cat"]))

    with open(OUT_DB_CSV, "w", encoding="utf-8-sig", newline="") as f:
        f.write("file,line,kind,arg,cat\n")
        for r in sorted(db, key=lambda r: (r["file"], r["line"])):
            f.write('"%s",%d,"%s","%s","%s"\n' % (
                r["file"], r["line"], r["kind"], (r["arg"] or "").replace('"', '""'), r["cat"]))

    with open(OUT_SC_CSV, "w", encoding="utf-8-sig", newline="") as f:
        f.write("file,line,kind,arg,cat\n")
        for r in sorted(sc, key=lambda r: (r["file"], r["line"])):
            f.write('"%s",%d,"%s","%s","%s"\n' % (
                r["file"], r["line"], r["kind"], (r["arg"] or "").replace('"', '""'), r["cat"]))

    print("MD  ->", OUT_MD)
    print("CSV ->", OUT_CSV)
    print("CSV ->", OUT_RES_CSV)
    print("CSV ->", OUT_DB_CSV)
    print("CSV ->", OUT_SC_CSV)
    print("节点路径 %d 处 / 文件 %d / 深路径 %d / 单段 %d / GetChild %d / 资产路径 %d / AssetDatabase %d / 场景 %d" % (
        len(rows), len(set(r["file"] for r in rows)), len(deep), len(single), len(gc), len(res), len(db), len(sc)))


if __name__ == "__main__":
    main()
