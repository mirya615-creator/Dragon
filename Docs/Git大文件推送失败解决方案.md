# Git 推送失败（大文件超限）解决方案

> 涉及仓库：`https://github.com/mirya615-creator/Dragon.git`
> 分支：`codex/merge-dragonbound`
> 报错时间：2026-09-14 15:06
> 文档版本：v1（2026-09-14）

---

## 一、问题定位

推送被 GitHub 服务端拒绝：

```
remote: error: File Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_14_0.bundle is 106.36 MB;
               this exceeds GitHub's file size limit of 100.00 MB
remote: error: GH001: Large files detected.
! [remote rejected] codex/merge-dragonbound -> codex/merge-dragonbound (pre-receive hook declined)
error: failed to push some refs to 'https://github.com/mirya615-creator/Dragon.git'
```

**800 秒的上传全部白费** —— GitHub 的 `pre-receive` 钩子在收完整包后才做体积校验，发现超限就整体拒收。

核实现状（`git rev-list --left-right --count`）：

| 项 | 值 |
|---|---|
| 本地领先远程 | **10 个 commit 未推送** |
| 远程该分支已收到的 commit | **0 个** |
| 远程分支地址 | `origin/codex/merge-dragonbound` = `ca27491` |

---

## 二、根因：2 个超过 GitHub 100 MB 硬限的文件

用 `git rev-list --objects <range> \| git cat-file --batch-check` 精确枚举本次要推送的全部对象，**超标的是 2 个**（截图只报了第一个，实际不止）：

| 文件 | 大小 | 引入 commit | 性质 | 处置 |
|---|---|---|---|---|
| `.codely-cli/UnityInsight/index.f7a0888f-5433-4e9a-bae5-81ac1ae5c3ea.db` | **311.5 MB** | `2fe4d3a` | Codex/codely IDE 索引缓存 | **剔除** |
| `Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_14_0.bundle` | **106.4 MB** | `b902c9c` | Firebase macOS 原生库 | **剔除** |
| `Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_14_0.so` | 77 MB | `b902c9c` | Firebase Linux 原生库 | 顺带剔除（非必须） |
| `Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_14_0.dll` | 17 MB | `b902c9c` | **Windows Editor 必需** | **保留** |

> 说明：`.codely-cli` 那个 311 MB 的 `.db` 比截图报的 bundle **还大 3 倍**，之前只是被报错刷屏遮住了。

### 为什么 `.bundle` / `.so` 可以直接扔

读它们的 `.meta` 平台开关：

| 文件 | Win64 | Android | OSX | Linux64 | 结论 |
|---|---|---|---|---|---|
| `*.bundle` | 0 | 0 | **1** | 0 | 只有 macOS 用 |
| `*.so` | 0 | 0 | 0 | **1** | 只有 Linux 用 |
| `*.dll` | **1** (+Editor=1) | 0 | 0 | 0 | **Windows 开发必需，必须留** |

本项目是 **Windows Editor + Android 出货**，`bundle` / `so` 这两个平台库 100% 用不到，是 Firebase `.unitypackage` 导入时无脑带进来的。

---

## 三、为什么必须"重写历史"（单纯删文件没用）

这 2 个文件已经在**历史提交**里了。Git 的提交是内容寻址的，只要它们仍存在于任何一个历史 commit 中，推送时就会被一并打包上传。

- ❌ 只 `git rm` 再提交一个新 commit → **无效**，旧 commit 里还有，照样超限
- ❌ 只 `git reset --soft` → 同上
- ✅ 必须把这些路径从**全部相关 commit** 中抹掉（`filter-branch` 之类）

---

## 四、三个关键前提（决定了本次操作是"零风险"）

核对结果：

| 前提 | 核查命令 | 结果 |
|---|---|---|
| ① 远程分支是空的 | `git rev-list --left-right --count origin/codex/merge-dragonbound...codex/merge-dragonbound` | `0  10` → 远程一个 commit 都没有 |
| ② 大文件不在 `master` 上 | `git cat-file -e master:<大文件路径>` | **不存在** → `master` 干净，无需重写 |
| ③ `master` 已推送、不受影响 | `git rev-parse master origin/master` | 都是 `6209bd6` → 一致 |

**结论：只需重写 `codex/merge-dragonbound` 这一条分支，不触碰 `master`，不影响任何协作者。**

---

## 五、执行方案（保留 10 个 commit 的分步历史）

### 前置：先看清当前工作区

当前有 18 项未提交改动（`Assets/UI/` 新资源、`Docs/UI路径绑定*.md`、`tools/*.py`、
`.workbuddy/memory/*`、`Assets/Resources/V2UI.meta` 删除等）。
**`git filter-branch` 要求工作区干净**，所以必须先提交它们。

```bash
cd "D:/Codex project/Dragon"
```

### Step 0 — 备份（必做）

```bash
git branch backup-before-vc-clean
```

本地分支备份，保留原始 10 个 commit 和所有大文件对象，是整个操作的"后悔药"。

### Step 1 — 更新 `.gitignore`（防复发）

在 `.gitignore` 末尾追加：

```gitignore
# ── Codex/codely IDE 本地状态（勿入库）──
.codely-cli/
.com-unity-codely.json

# ── Firebase 非开发平台原生库（本项目仅 Windows Editor + Android）──
Assets/Firebase/Plugins/x86_64/*.bundle
Assets/Firebase/Plugins/x86_64/*.bundle.meta
Assets/Firebase/Plugins/x86_64/*.so
Assets/Firebase/Plugins/x86_64/*.so.meta
```

> `!/[Aa]ssets/**/*.meta` 那条既有规则是"不要忽略 meta"，但 ignore 规则**后写的覆盖先写的**，
> 所以上面针对特定 meta 的忽略依然生效，不会误伤其他 meta。

### Step 2 — 提交当前工作区改动（让工作区干净）

```bash
git add -A
git commit -m "chore: UI 资源目录迁移 + 路径绑定扫描脚本/报告；忽略 codely 缓存与非开发平台原生库"
```

### Step 3 — 从全部历史中剔除那些路径

```bash
git filter-branch --force --index-filter \
  'git rm -r --cached --ignore-unmatch --quiet \
     .codely-cli \
     Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_14_0.bundle \
     Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_14_0.bundle.meta \
     Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_14_0.so \
     Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_14_0.so.meta \
     Assets/Firebase/Plugins/x86_64/FirebaseCppAnalytics.bundle \
     Assets/Firebase/Plugins/x86_64/FirebaseCppAnalytics.bundle.meta \
     Assets/Firebase/Plugins/x86_64/FirebaseCppAnalytics.so \
     Assets/Firebase/Plugins/x86_64/FirebaseCppAnalytics.so.meta' \
  --prune-empty -- codex/merge-dragonbound
```

- `--cached` = 只动 Git 索引，不动磁盘文件（**但 filter-branch 收尾会 `reset --hard`，见 Step 4**）
- `--prune-empty` = 若某 commit 清完变成空提交就丢掉
- 只针对 `codex/merge-dragonbound`，**不碰 `master`**

### Step 4 — 恢复 `.codely-cli` 到磁盘（重要！）

`filter-branch` 结束时会 `git reset --hard`，把已从历史移除的 `.codely-cli/`（672 个文件）
**从磁盘一并删掉**。它是 Codex/codely 插件正在用的索引缓存，需要从备份分支恢复回来：

```bash
git checkout backup-before-vc-clean -- .codely-cli
git reset --quiet -- .codely-cli      # 取消暂存，让 .gitignore 接管（文件仍在磁盘）
```

Firebase 的 `.bundle` / `.so` **不需要恢复** —— 反正用不到，顺手省下 184 MB 磁盘。

### Step 5 — 清理旧引用（先别 gc）

```bash
git for-each-ref --format="%(refname)" refs/original/ | xargs -n 1 git update-ref -d
```

### Step 6 — 校验：确认没有 >100 MB 的 blob 了

```bash
git rev-list --objects codex/merge-dragonbound | \
  git cat-file --batch-check='%(objecttype) %(objectsize) %(rest)' | \
  awk '$1=="blob" && $2>100000000 {printf "%7.1f MB  %s\n", $2/1048576, $3}'
```

**输出为空** = 通过。（顺便跑一遍 `$2>50000000` 看看最大的几个是谁，应该最大只剩 22 MB 的 `.aar` / `m2repository`。）

### Step 7 — 推送

```bash
git push origin codex/merge-dragonbound
```

### Step 8 — 确认远程 OK 后，再回收本地空间

备份分支还链着那 495 MB 的对象，所以 **必须先确认 Step 7 成功**，再：

```bash
git branch -D backup-before-vc-clean
git reflog expire --expire=now --all
git gc --prune=now --aggressive
```

---

## 六、逐步说明表

| Step | 动作 | 风险 | 可跳过？ |
|---|---|---|---|
| 0 | 建备份分支 | 无 | ❌ 必做 |
| 1 | 改 `.gitignore` | 无 | ❌ |
| 2 | 提交工作区改动 | 低（新增 1 个 commit） | ❌（filter-branch 要求工作区干净） |
| 3 | `filter-branch` 剔路径 | **中**（重写历史） | ❌ |
| 4 | 恢复 `.codely-cli` | 低 | ❌（不恢复则 IDE 缓存丢失） |
| 5 | 删 `refs/original` | 低 | 建议做 |
| 6 | 校验 blob 体积 | 无（只读） | ❌ |
| 7 | push | 无 | ❌ |
| 8 | gc 回收空间 | 低（**前提是 Step 7 已成功**） | 可延后 |

---

## 七、风险与回退

| 风险 | 概率 | 应对 |
|---|---|---|
| Unity 因缺 `.bundle`/`.so` 报警 | 低 | 它们是 macOS/Linux 格式，Windows Unity 不会加载；真出问题 `git checkout backup-before-vc-clean -- <路径>` 恢复 |
| `.codely-cli` 被删 → IDE 需重建索引 | 中 | Step 4 已恢复；万一漏了，同样从备份分支 checkout |
| `filter-branch` 中途失败 | 低 | 10 个 commit 数据量极小，秒级完成；失败则 `git reset --hard backup-before-vc-clean` 回到原点 |
| 推完发现要改内容 | 低 | 远程分支本来就是空的，直接改完重新 push 即可 |

**一键回退**（任何一步出问题）：

```bash
git reset --hard backup-before-vc-clean
```

---

## 八、备选方案：不保留历史（30 秒版）

如果你**不在乎那 10 个 commit 的分步记录**，有更快的做法 —— 直接压缩成一个干净提交：

```bash
cd "D:/Codex project/Dragon"
git branch backup-before-vc-clean                 # 备份
git reset --soft origin/codex/merge-dragonbound   # 所有改动回到暂存区（不动磁盘文件）
# 改好 .gitignore（同 Step 1）
git rm -r --cached --quiet .codely-cli .com-unity-codely.json
git rm --cached --quiet Assets/Firebase/Plugins/x86_64/*.bundle Assets/Firebase/Plugins/x86_64/*.bundle.meta
git rm --cached --quiet Assets/Firebase/Plugins/x86_64/*.so     Assets/Firebase/Plugins/x86_64/*.so.meta
git add -A
git commit -m "feat: DragonBound 阶段成果（UI/广告/排行/商品/埋点等）"
git push origin codex/merge-dragonbound
```

**优点**：不用 `filter-branch`，**不会删磁盘文件**，不会碰 `master`，几乎零风险。
**缺点**：10 个 commit 的历史合并成 1 个。

> 二选一即可。**推荐主方案**（保留历史），因为远程分支是空的、操作零风险，
> 而分步历史对后续定位问题有实际价值。

---

## 九、为什么不选 Git LFS

| 维度 | 说明 |
|---|---|
| 免费额度 | GitHub 只给 1 GB 存储 + 1 GB/月流量；这两个文件就吃掉 ~490 MB |
| 协作成本 | 每个协作者都必须装 `git-lfs` 并 `git lfs install`，否则 clone 下来是 130 字节的指针文件 |
| 长期维护 | 以后每次升 Firebase SDK，大二进制都要走 LFS，额度持续消耗 |
| **收益** | 而这两个平台（macOS / Linux）**本项目根本不用** —— 为一个用不到的库付这些代价不划算 |

环境里 `git-lfs 3.7.1` 已装（`git-filter-repo` **未装**，所以方案用原生 `filter-branch`）。

---

## 十、防复发清单

- [ ] `.gitignore` 已加 `.codely-cli/`、`.com-unity-codely.json`
- [ ] `.gitignore` 已加 `Assets/Firebase/Plugins/x86_64/*.bundle`、`*.so`（含 `.meta`）
- [ ] 提交前习惯跑一次体积自检：
      `git ls-files -z | xargs -0 du -h 2>/dev/null | sort -rh | head -20`
- [ ] 升级 Firebase / 导入任何 `.unitypackage` 后，检查是否又带进了非目标平台的大二进制

---

## 附：本次操作对 `.com-unity-codely.json` 的说明

`com-unity-codely.json` 当前**已被 Git 跟踪**（且处于已修改状态）。它是 codely 插件的本地配置，
上面 Step 1 的 `.gitignore` 加了它，但 **`.gitignore` 对已跟踪文件无效**，需要显式取消跟踪：

```bash
git rm --cached --quiet .com-unity-codely.json
```

（已包含在 Step 2 的 `git add -A` + Step 3 的流程里；如单独处理，注意别把磁盘文件删了。）
