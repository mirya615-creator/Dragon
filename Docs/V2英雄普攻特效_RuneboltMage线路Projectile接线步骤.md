# V2 新增 Runebolt Mage 普攻特效：线路（穿透线）上播放 `RM nor Projectile`

资源：`Assets/DragonBound/UI/Variants/V2/Content/Resources/Animations/Hero/Runebolt Mage/normal/VFX/RM nor Projectile.anim`（+ 同名 `.controller`）
状态：**仅方案，未执行**。

---

## 1. 现有链路（已核对）

`AttackKind.RuneboltPierce`（`CombatFxView.cs:590`）→ `QueueRuneboltMageCast`(:704) 收集同帧所有命中目标 → `ReleaseRuneboltMageCast`(:764) → `SpawnRuneboltMageBolt`(:776)：

- 建一个 `"Runebolt Mage Path"` 节点（`RectTransform + CanvasRenderer + Image + Animator`）挂在 `fixedBoardCanvas.CombatFxLayer`；
- `pivot=(0,0.5)`、`sizeDelta=(travelDistance, 300)`、`rotation=atan2(dir)` → **整条穿透线**；
- `image.preserveAspect = false`（刻意拉伸满全程）；
- Animator 播 `Animation/Runebolt MageBoom`（**V1 常量** `RuneboltMageBoltControllerPath`，:30）；
- `clipLength = controller.animationClips[0].length` 既决定动画时长，**也决定伤害沿线路推进的节奏**（`CompleteRuneboltMageHitsThrough(pending, elapsed/clipLength)`，:876）；
- 总时长 = `clipLength + runeboltMagePathHoldDuration(0.04) + runeboltMagePathFadeDuration(0.08)`。

即：**"线路伤害"的载体就是这个 RectTransform，改它播的 controller 即可。**

## 2. 资源核查（已核对）

| 项 | 值 | 是否需要处理 |
| --- | --- | --- |
| V2 注册表 key | `Animations/Hero/Runebolt Mage/normal/VFX/RM nor Projectile`（`UiAssetRegistryV2.asset:236`） | ✅ 已存在，不用 Regenerate |
| clip 时长 | 6 帧 @60fps，`m_StopTime: 0.1` → **0.1s** | 与 V1 旧资源一致，节奏不用改 |
| controller state speed | **`m_Speed: 0.2`** | ⚠️ **必须改成 1**（见下） |
| 循环 | `m_LoopTime: 1`（勾选） | ⚠️ 建议取消勾选 |
| 参照：V1 `Runebolt MageBoom` | clip 0.1s、`m_LoopTime: 0`、controller `m_Speed: 1` | — |

### 必须做的两处资源修正

1. **`RM nor Projectile.controller` → AnimatorState `m_Speed: 0.2` 改为 `1`**
   代码用 `clipLength`（=0.1s）驱动伤害推进并在其后淡出销毁；state speed 0.2 会让动画实际要 0.5s 才播完 → 伤害 0.1s 结算完时画面才播了 1/5 就被 fade 掉，看起来像"闪一下就没了"。（与之前 `EnemyDie` 同一个坑。）
   在 Unity 里：选中 controller → Animator 窗口 → 状态 `RM nor Projectile` → **Speed = 1**（或改 YAML 第 10 行 `m_Speed: 1`）。

2. **`RM nor Projectile.anim` → 取消 Loop Time**（`m_LoopTime: 1` → `0`）
   FX 是按时长销毁的一次性对象，循环会在末尾重复/闪烁。

---

## 3. 修改步骤（代码）

### 步骤 1 — 新增 V2 常量

`CombatFxView.cs:30` 之后：

```csharp
        private const string RuneboltMageBoltControllerPath = "Animation/Runebolt MageBoom";
+       private const string RuneboltMageNormalProjectileControllerPathV2 =
+           "Animations/Hero/Runebolt Mage/normal/VFX/RM nor Projectile";
```

### 步骤 2 — 新增解析方法（V2 优先、V1 回退）

放在 `SpawnRuneboltMageBolt`（:776）之前：

```csharp
        private RuntimeAnimatorController ResolveRuneboltMagePathController()
        {
            if (runeboltMageBoltController != null)
            {
                return runeboltMageBoltController;
            }

            // Probe silently: UiAssets.Load logs an error on a miss, and V1 has no V2 key, so an
            // absent clip is an expected state rather than a failure. (UiAssetRegistry.Load<T> is
            // the silent instance API; the static UiAssets.Load<T> logs.)
            var registry = DragonBound.Presentation.UiAssets.Active;
            var projectile = registry != null
                ? registry.Load<RuntimeAnimatorController>(RuneboltMageNormalProjectileControllerPathV2)
                : null;
            if (projectile != null)
            {
                runeboltMageBoltController = projectile;
                return runeboltMageBoltController;
            }

            runeboltMageBoltController =
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(RuneboltMageBoltControllerPath);
            return runeboltMageBoltController;
        }
```

> 复用现有字段 `runeboltMageBoltController`（:227）作缓存即可，无需新增字段。

### 步骤 3 — 改 `SpawnRuneboltMageBolt` 的取用方式（:783-792）

```diff
-           if (runeboltMageBoltController == null)
-           {
-               runeboltMageBoltController =
-                   DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(RuneboltMageBoltControllerPath);
-           }
-           if (runeboltMageBoltController == null)
+           var pathController = ResolveRuneboltMagePathController();
+           if (pathController == null)
            {
                CompleteRuneboltMageCast(pending);
                return;
            }
```

并把后续两处改成用局部变量（:846、:850-851）：

```diff
-           animator.runtimeAnimatorController = runeboltMageBoltController;
+           animator.runtimeAnimatorController = pathController;
            ...
-           var clipLength = runeboltMageBoltController.animationClips.Length > 0
-               ? runeboltMageBoltController.animationClips[0].length
+           var clipLength = pathController.animationClips.Length > 0
+               ? pathController.animationClips[0].length
                : 0.1f;
```

其余（节点创建、sizeDelta、rotation、ActiveFx 注册、progress 回调）**保持不变** → 视觉行为与 V1 完全一致，只是换了动画资源。

---

## 4. 可选：若 `Projectile` 是"飞行弹体"而非"整条线"

名字 `Projectile` 暗示弹体；若实机看发现拉伸变形/不像弹丸，改成沿线飞行（用 `ActiveFx` 已有的 `Projectile` 模式）：

在 `SpawnRuneboltMageBolt` 里改这几处：

```csharp
            rect.pivot = new Vector2(0.5f, 0.5f);            // 原 (0, 0.5)
            rect.sizeDelta = runeboltMageProjectileSize;     // 新增字段，默认 (110,110)
            rect.position = start;
            // 删掉 rect.localRotation = Quaternion.Euler(...)  → 由 OrientToTrajectory 每帧设置
            image.preserveAspect = true;                      // 原 false
```

`ActiveFx` 注册改为：

```csharp
            active.Add(new ActiveFx(
                rect,
                image,
                null,
                start,
                start + (direction * travelDistance),
                true,                                  // projectile: 沿线飞行
                false,
                duration,
                false,
                () => CompleteRuneboltMageCast(pending),
                0f,                                    // arcHeight
                true,                                  // orientToTrajectory
                0f,
                normalized =>
                {
                    // 用 normalized（= 弹体当前位置进度）而不是 elapsed/clipLength，
                    // 保证命中判定与弹体位置同步。
                    CompleteRuneboltMageHitsThrough(pending, normalized);
                    var fadeStart = clipLength + holdDuration;
                    var alpha = ...;                   // 淡出逻辑不变
                }));
```

并新增字段（`[SerializeField]` 放 :97 附近）：
```csharp
        [SerializeField] private Vector2 runeboltMageProjectileSize = new Vector2(110f, 110f);
```

---

## 5. 影响面 / 验证

- **V1 完全不变**：V1 注册表无 `Animations/Hero/Runebolt Mage/...` key → `UiAssets.Active.Load` 静默返回 null → 回退 `UiAssets.Load("Animation/Runebolt MageBoom")`，行为与改动前一致（且不会 LogError）。
- **V2**：命中新 controller；若未做第 2 节的 speed/loop 修正，会看到"动画没播完就消失"。
- 验证清单：
  1. V2 战斗 Runebolt Mage 普攻：线路上出现 Projectile 动画，伤害随线路推进逐个结算。
  2. Console 无 `UI asset is not registered ... Key=Animations/Hero/Runebolt Mage/normal/VFX/RM nor Projectile`。
  3. V1 回归：仍是旧的 `Runebolt MageBoom` 表现。
  4. 若动画太短（0.1s 一闪而过）：把 `runeboltMagePathHoldDuration`(0.04) / `runeboltMagePathFadeDuration`(0.08) 调大，或给 clip 加帧延长。

## 6. 后续（非本次范围）

同一目录还有 `RM nor Muzzle`（枪口，speed 1）与 `RM nor Impact`（命中，speed 0.5，同样需要把 speed 改 1）：
- Muzzle → 在 `SpawnRuneboltMageBolt` 起点 `start` 处再生成一个独立 FX；
- Impact → 在 `CompleteRuneboltMageImpact`(:909) 里按 `shot.TargetPosition` 生成。
两者都照抄步骤 1-3 的「V2 静默探测 + V1 回退」模式即可。

---

## 7. 实施记录 / 踩坑（2026-09-23）

首次手工替换步骤 3 时，只删掉了第二个 `if` 的**条件行**，块体被留下变成**无条件块**：

```csharp
            var pathController = ResolveRuneboltMagePathController();
            if (pathController == null)
            {
                CompleteRuneboltMageCast(pending);
                return;
            }

            {                                   // ← 遗留的孤立块（原 if 的条件行被删）
                CompleteRuneboltMageCast(pending);
                return;
            }
```

后果：`SpawnRuneboltMageBolt` 每次都在创建 `"Runebolt Mage Path"` 节点**之前**就 return，同时 `CompleteRuneboltMageCast` 把全部命中立即结算（伤害、受击抖动、飘字照常）→ **伤害正常但完全没有线路特效**。

已修复：删除该孤立块（`CombatFxView.cs:819-822`）。排查同类问题的经验：手改「删条件」时务必整块删掉，或让编译器（Unity Console）直接报未使用标签以外的告警——孤立块不会报错，只会静默改变控制流。

---

## 8. 现象分析：特效已出，但「长度和形状不太对」（2026-09-24，仅分析，未改代码）

### 8.1 关键事实：新旧两套美术的「画布内容占比」完全不同

| 美术 | 画布 | 实体笔画 bbox（alpha>160） | 占画布宽 | 占画布高 |
| --- | --- | --- | --- | --- |
| V1 `VFX/Runebolt Mage/road/符文雷矢法师普攻特效_0000x.png` | 1080×1080 | 393×54 / 665×90 / 832×24（0/3/5 帧） | 0.36~0.77 | **0.022~0.083** |
| V2 `UIResources/VFX/Hero/Runebolt Mage/normal/Projectile/sdj0001~0006.png` | 150×72 | 74×24 → 129×44（逐帧） | 0.81~0.86 | **0.33~0.61**（均值 0.54） |

- V1：「细线贯穿方形画布」——线只占画布高度的 3~8%，这正是它在 300 高的盒子里看起来是**一条细线**的原因。
- V2：「实心彗星/箭形」——笔画几乎填满 150×72 画布（宽 85%、高 54%）。

### 8.2 根因一：盒子高度写死 300，是为 V1 调的

`SpawnRuneboltMageBolt`（`CombatFxView.cs:855-867`）：

```csharp
rect.pivot = new Vector2(0f, 0.5f);
rect.sizeDelta = new Vector2(travelDistance, RuneboltMagePathVisualHeight);  // 300f，:98
image.preserveAspect = false;   // 刻意非等比拉伸满全程
```

于是两套美术在同一个盒子里的表现完全不同（1 格 = 110 单位）：

| | 纵向缩放 | 笔画实际厚度 |
| --- | --- | --- |
| V1（高占比 0.05） | 1080 → 300，0.28× | 0.05 × 300 ≈ **15 单位 ≈ 0.14 格**（细线 ✅） |
| V2（高占比 0.54） | 72 → 300，**4.17×** | 0.54 × 300 ≈ **162 单位 ≈ 1.47 格**（厚带 ❌） |

→ 同一个特效盒子里，V2 的笔画比 V1 粗了约 **11 倍**，观感从「线路」变成「糊成一片的厚带」。

### 8.3 根因二：宽高比失真倍数随目标距离变化（形状每次都不一样）

纵向缩放固定在 `300/72 = 4.17×`，横向缩放 = `travelDistance/150`：

| travelDistance | 横向缩放 | 失真倍数（纵向/横向） | 观感 |
| --- | --- | --- | --- |
| 110（1 格） | 0.73× | **5.7×** | 被压扁 + 拉厚，最难看 |
| 220（2 格） | 1.47× | 2.8× | 明显变形 |
| 330（3 格） | 2.20× | 1.9× | 仍偏厚 |
| 550（5 格，上限） | 3.67× | 1.14× | 接近美术原比例，尚可 |

即：**只有打满 5 格时比例才接近原始美术**；日常 1~3 格距离下被纵向拉伸 2~5.7 倍。截图里那条又短又粗的斜向糊带，正是短距离（1~2 格）+ 固定 300 高的组合结果。

### 8.4 根因三：「长度」由目标摆位决定，且美术本身左右留白

- `travelDistance = Mathf.Clamp(farthestDistance, 1f, RuneboltMageMaximumPathLength)`（:836-844）——长度 = **到最远命中目标**的投影距离，而不是技能线的 5 格（`PierceLength = 5`，`HeroCombatState.cs:737`）。目标站位近，线就短。
- 美术内容只占画布宽的 81~86%（左右各有约 3~10% 空白），且**第 1 帧内容只有 55% 宽**（`sdj0001` bbox 83×30）→ 0.1s 动画里「线」的长度本身在变。
- FX 总时长只有 `0.1(clip) + 0.04(hold) + 0.08(fade) ≈ 0.22s`，抓拍大概率停在第一帧（最短那一帧），进一步强化「长度不对」。

### 8.5 加剧项：美术语义与用法不匹配

`RM nor Projectile` 是**弹体**（彗星/箭形，6 帧内头部与拖尾在画布内推进），而不是「整条线的能量流」。把它按整条线非等比铺开，等于把一张「小弹体」图放大 2~3 格并纵向再拉 4 倍——多道细碎笔触被放大后必然读成「散乱的裂纹」，而不是一条连贯的线。

### 8.6 修复方向（三选一，未执行）

- **A. 高度随长度等比（最小改动）**：`sizeDelta.y = travelDistance * (72f / 150f)`（≈ 0.48×travelDistance）→ 形状永远与美术一致；代价是长线更粗、短线更细（粗细随距离变）。
- **B. 按美术本意当「飞行弹体」**（推荐，最贴 `Projectile` 语义）：rect 固定为美术等比（150×72 或 2× → 300×144），`ActiveFx(projectile: true, orientToTrajectory: true)`、`End = start + direction * travelDistance`；同时把伤害推进从 `elapsed / clipLength` 改为按弹体位置归一化（见第 4 节）。需同步调伤害到达节奏。
- **C. 仍要「整条线」**：让美术重出「细线贯穿画布」型的 5 格能量流序列（像 V1 那样），代码维持现有盒子，或配合方案 A 改等比高度。

临时调试手法（不改逻辑即可判断主因）：把 `RuneboltMagePathVisualHeight`（:98）从 300 改成 120，若形状立刻正常，则确认是 8.2/8.3 的等比问题。

---

## 9. 最优修改方案：对齐「正确效果」参考图（2026-09-24，仅方案未执行）

> 结论先行：**不是等比问题，是盒子高度按变体取值的问题**。`RuneboltMagePathVisualHeight` 从 300 改成 **36** 即可对齐参考图；但 300 是 V1 美术的正确值，必须按变体分开。

### 9.1 参考图的量化指标（对 650×370 截图实测）

- 棋盘格间距 ≈ 80px；`FixedBoardCanvasView.cs:22` 定义 1 格 = 110 世界单位 → **1 世界单位 ≈ 0.727px**。
- 光柱**水平**贯穿，中线恒在 y≈259（= 施法者/目标所在行的行心），全长 ≈ 227px ≈ **2.8 格**（起 x≈283、止 x≈510）。
- 厚度：亮核 **1~3px**（≈0.02~0.04 格），柔光 **7~18px**（均 ≈12px ≈ **0.15 格**）；整体可见带 20~26px ≈ **0.25~0.33 格**。
- 颜色：核心 `#cbe7ff → #ffffff`，外围淡蓝；末端/命中敌人处有 ≈35px（0.44 格）白色爆闪。
- 起点：从**英雄立绘轮廓之外**开始，不是从英雄中心。

### 9.2 结论：盒子高度应为 ≈36 世界单位（0.33 格）

把 V2 的 6 帧按不同盒子高度双线性拉伸后，与参考图并排对比（见 `Docs/attachments/Runebolt线路特效_盒子高度模拟对比.png`，行序自上而下：参考 / 300 / 72 / 36 / 24）：

| 盒子高度 | 截图像素高 | 观感 |
|---|---|---|
| 300 world（当前） | 218px | 巨大糊带，完全不像线 ✗ |
| 72 world | 52px | 仍明显偏厚 ✗ |
| **36 world** | **26px** | **与参考最接近 ✓** |
| 24 world | 17px | 略细，也可接受 |

换算依据：V2 美术内容只占画布高 54%，36 × 0.54 ≈ **19.5 世界单位 ≈ 0.18 格 ≈ 14px**，与参考实测的柔光 12px 吻合。
推荐 **36f**，可微调区间 **28~44**。

### 9.3 为什么必须「按变体取高度」

同一个 `RuneboltMagePathVisualHeight = 300f`（`CombatFxView.cs:98`）现在同时服务两套美术：

| 美术 | 画布 | 笔画占画布高 | 300 高盒子里的实际粗细 |
|---|---|---|---|
| V1 `符文雷矢法师普攻特效` | 1080×1080 | 2.2%~8.3% | ≈7~25 单位（细线）✓ |
| V2 `RM nor Projectile` | 150×72 | **33%~61%（均 54%）** | ≈100~180 单位（1~1.6 格糊带）✗ |

两者差约 10 倍。既然 controller 已按变体解析（`:779-802`），高度也必须跟着变体走，否则 V1/V2 只能顾一头。

### 9.4 主方案（推荐）：把「路径样式」变体化（共 4 处）

**① 常量（`:98` 处替换）**
```csharp
// V1 美术（1080² 画布，线条极细）：保持原值
private const float RuneboltMagePathVisualHeightV1 = 300f;
// V2 美术（150×72 画布，笔画占 54%）：0.33 格，实测对齐参考图
private const float RuneboltMagePathVisualHeightV2 = 36f;
```

**② 新增缓存字段（与已有的 `runeboltMageBoltController` 并列）**
```csharp
private float runeboltMageBoltVisualHeight = -1f;   // <0 = 尚未解析
```

**③ `ResolveRuneboltMagePathController()`（`:779-802`）两个分支各写一次高度**
```csharp
var projectile = registry != null
    ? registry.Load<RuntimeAnimatorController>(RuneboltMageNormalProjectileControllerPathV2)
    : null;
if (projectile != null)
{
    runeboltMageBoltController = projectile;
    runeboltMageBoltVisualHeight = RuneboltMagePathVisualHeightV2;   // ← 新增
    return runeboltMageBoltController;
}

runeboltMageBoltController =
    DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(RuneboltMageBoltControllerPath);
runeboltMageBoltVisualHeight = RuneboltMagePathVisualHeightV1;       // ← 新增
return runeboltMageBoltController;
```

**④ `SpawnRuneboltMageBolt`（`:857-859`）改用解析出的高度**
```csharp
var visualHeight = runeboltMageBoltVisualHeight > 0f
    ? runeboltMageBoltVisualHeight
    : RuneboltMagePathVisualHeightV1;
rect.sizeDelta = new Vector2(travelDistance, visualHeight);
```

其余（起点/旋转/播放/伤害推进/淡出）**一行不动**。V1 走 V1 分支，行为完全不变。

### 9.5 配套项（还原参考图的其余特征，可选，按优先级）

1. **起点偏移**（`:828`）：`start = pending.AttackerPosition + (direction * 24f)`。
   24 单位（0.22 格）通常仍在英雄立绘内部；参考图里光柱是从立绘轮廓**外**开始的。若实机看到「光柱从法师身体里长出来」，把 24f 提到 **120~150**（1.1~1.4 格）；要保 V1 不变就同样做成变体化常量。
2. **命中爆闪**：参考图里每个被命中敌人身上都有 0.44 格白爆。V2 的 `RM nor Impact` 尚未接线；先把 controller `m_Speed` 0.5→1、anim `m_LoopTime` 1→0，再接到 `CompleteRuneboltMageImpact`（`:909`），照抄「V2 静默探测 + V1 回退」模式。
3. **时长**：整段 `0.1(clip)+0.04(hold)+0.08(fade) ≈ 0.22s` 偏短，肉眼几乎抓不到。把 `runeboltMagePathHoldDuration`（`:100`）提到 **0.10~0.15**，更接近参考的「一条稳定存在的光束」。

### 9.6 不推荐的方案

| 方案 | 为什么不选 |
|---|---|
| 等比拉伸：`preserveAspect=true`，`height = length × 72/150` | 2 格时 0.96 格厚、5 格时 2.4 格厚 → **越远越粗**，与参考（细线、粗细不随距离变）相反 |
| 改成飞行弹体（固定尺寸沿线飞） | 参考明确是「贯穿光束」，不是飞行物 |
| 9-slice（头/中/尾） | 需给 6 张 png 加 border，且 150px 宽的美术中间可拉伸区太窄会撕裂/重复；整段只有 0.22s，收益不抵成本 |

### 9.7 验证清单

1. V2 打一次普攻：细线（柔光 ≈0.18 格、亮核 1~3px），水平贯穿到**最远命中敌人**（不是固定 5 格）。
2. 近距离（1 格）与远距离（满 5 格）各打一次：**厚度应基本一致**（恒定 36 世界单位）。
3. V1 回归：仍是原来的细线（300 不变）。
4. Console 无 `UI asset is not registered ... RM nor Projectile`。
5. 想先快速验证：直接把 `RuneboltMagePathVisualHeight` 由 300 改成 36 看一眼（此时 V1 也会变细），确认后再按 9.4 分变体。

---

## 10. 手动修改步骤（2026-09-24，可直接照抄）

全部改动集中在 `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs`，共 4 处必改 + 2 处可选。长度逻辑（travelDistance / 550 上限 / PierceLength）**一行不动**，5 格穿透完整保留（长度是 X 轴 `sizeDelta.x`，本次只改 Y 轴）。

### 步骤 1（:98）—— 常量按变体拆分

改前：

```csharp
private const float RuneboltMagePathVisualHeight = 300f;
```

改后：

```csharp
// V1 art (1080x1080 canvas, stroke occupies only 2.2%-8.3% of the height) keeps the original value.
private const float RuneboltMagePathVisualHeightV1 = 300f;
// V2 art (150x72 canvas, stroke occupies ~54% of the height): 0.33 cell, measured against the reference shot.
private const float RuneboltMagePathVisualHeightV2 = 36f;
```

（该常量全文只有 :98 定义、:859 使用两处，无其他引用，可安全改名。）

### 步骤 2（:230 后）—— 新增高度缓存字段

改前：

```csharp
private RuntimeAnimatorController runeboltMageBoltController;
```

改后：

```csharp
private RuntimeAnimatorController runeboltMageBoltController;
private float runeboltMageBoltVisualHeight = -1f;
```

### 步骤 3（:793-801）—— 解析时按分支记录高度

改前：

```csharp
            if (projectile != null)
            {
                runeboltMageBoltController = projectile;
                return runeboltMageBoltController;
            }

            runeboltMageBoltController =
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(RuneboltMageBoltControllerPath);
            return runeboltMageBoltController;
```

改后：

```csharp
            if (projectile != null)
            {
                runeboltMageBoltController = projectile;
                runeboltMageBoltVisualHeight = RuneboltMagePathVisualHeightV2;
                return runeboltMageBoltController;
            }

            runeboltMageBoltController =
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(RuneboltMageBoltControllerPath);
            runeboltMageBoltVisualHeight = RuneboltMagePathVisualHeightV1;
            return runeboltMageBoltController;
```

### 步骤 4（:857-859）—— 盒子高度改用解析值

改前：

```csharp
            rect.sizeDelta = new Vector2(
                travelDistance,
                RuneboltMagePathVisualHeight);
```

改后：

```csharp
            var visualHeight = runeboltMageBoltVisualHeight > 0f
                ? runeboltMageBoltVisualHeight
                : RuneboltMagePathVisualHeightV1;
            rect.sizeDelta = new Vector2(
                travelDistance,
                visualHeight);
```

`travelDistance`（X 轴）保持原样，长度不变。

### 可选 A（:828）—— 起点偏移

```csharp
var start = pending.AttackerPosition + (direction * 24f);
```

若实机看到「光柱从法师身体里长出来」，把 `24f` 调到 **70f**。不要超过 110f（1 格），否则贴脸敌人（第 1 格）会被起点吃掉、光束几乎不可见。此项 V1/V2 共用，改前先确认 V1 观感可接受。

### 可选 B（:100）—— 延长停留时间

```csharp
[SerializeField, Min(0f)] private float runeboltMagePathHoldDuration = 0.04f;
```

整段 `0.1(clip) + 0.04(hold) + 0.08(fade) ≈ 0.22s` 偏短。代码默认改 `0.10f` **之后还必须在场景里改**：这是 `[SerializeField]`，已有场景实例的值会覆盖代码默认值（本项目已知坑）。Hierarchy 搜 `t:CombatFxView` 找到挂载对象，在 Inspector 里把 Hold Duration 同步改成 0.1 并 Ctrl+S。

### 验收

1. V2 打一次普攻：细线（柔光约 0.18 格），水平贯穿到**最远命中敌人**。
2. 1 格近距离与满 5 格远距离各打一次：**厚度应基本一致**（恒定 36 世界单位）；长度随敌人位置变化是对的，厚度不变才对。
3. V1 回归：仍是原来的细线（300 不变）。
4. Console 无 `UI asset is not registered ... RM nor Projectile`。
5. 快速自检：可先跳过步骤 1-3，直接把 :859 的 `RuneboltMagePathVisualHeight` 临时写死成 `36f` 看一眼（此时 V1 也变细），确认观感后再补回变体分支。

---

## 11. 实测反馈：太细、太短、没挨住敌人（2026-09-24，仅分析未改码）

改完高度后实机截图（396×252，格距 ≈61px）：光柱细且斜向延伸，末端停在最右敌人脚下约 0.2~0.3 格处。逐项实测与归因：

### 11.1 盒子末端其实落在敌人身上，是"内容画不到边"（主因）

`travelDistance = Clamp(max(Dot(target-start, dir)), 1, 550)`（:836-844）→ 沿 dir 方向上，盒子的右端精确落在最远命中目标的投影点。已用截图几何反推验证：法师中心 ≈(172,215)、最右敌人 ≈(229,75)、盒子末端推算 ≈(224,73)，**与敌人位置吻合**。

但 V2 美术画布 150×72，实测每一帧的内容范围：

| 帧 | alpha>32 内容 x 范围 | 宽度占比 | alpha>160 实体笔画宽 |
|---|---|---|---|
| sdj0001 | 58..140 | **55%（且左端空 58px！）** | 49% |
| sdj0002 | 7..142 | 91% | 86% |
| sdj0003 | 7..140 | 89% | 85% |
| sdj0004 | 6..136 | 87% | 84% |
| sdj0005 | 11..137 | 85% | 66% |
| sdj0006 | 12..136 | 83% | 81% |

结论：
- **两端永久留白**：即使是铺得最满的第 2 帧，右端也只到 x142（右留白 5%）+ 实体笔画到 86%，末帧实体只到 x136（**右留白 9.3%**）→ 盒子末端与可见笔触末端天然差约 **9%**，2.5 格距离上就是 **0.23 格 ≈ 14px**，正好是截图里"没挨住"的缺口量级。
- **首帧是反方向的**：sdj0001 的内容集中在 **x58..140（右半段）**，左端 39% 全空 —— 说明这组动画是"能量头先出现在末端侧、尾迹再向左回填"（彗星式），**不是**"从左端整条铺到右端"的横贯式。抓拍到越靠前的帧，"光柱"越像一小段漂在右侧的碎光。

### 11.2 细：36 单位盒高在小格距下偏细

36 世界单位盒高 × 实体笔画占高 56%~61% ≈ **20~22 单位 ≈ 0.19 格**。本截图 1 格 = 61px → 约 **11px**；上一版参考图 1 格 = 80px → 12px。比例几乎一样，但这次的 UI 缩放更小、光柱又短，观感上就更"细弱"。实测用户期望更醒目，建议 **54~72 单位（0.5~0.65 格）**。

### 11.3 缺了参考图里那个"命中爆闪"

参考图（正确效果）里每个被命中敌人身上都有 ≈0.44 格白色爆闪 —— 那颗白爆正好盖在光柱末端与敌人之间的缝隙上，视觉上就"连上了"。当前 `RM nor Impact` 尚未接线（应接 `CompleteRuneboltMageImpact`，:909），缝隙裸露 → 一眼看出没挨住。

### 11.4 潜在风险：多目标时"方向取最近、长度取最远"会歪

```csharp
var direction = pending.Shots[0].TargetPosition - pending.AttackerPosition;   // 最近目标
farthestDistance = Mathf.Max(farthestDistance, Vector3.Dot(shot.TargetPosition - start, direction));  // 最远目标投影
```
当多个命中目标**横向铺开**（例如 5 个敌人一排、法师在斜下方）时：`dir` 朝最近目标，而最远目标与 `dir` 的夹角大，其**投影**远小于真实距离 → 光柱会明显变短，末端只到"最近目标所在的那条线"上，覆盖不到最远端。本次构图（法师斜下方 + 敌人横排）正是这种情形，若这一击命中了 2 个以上横向分离的敌人，此机制就是第二重缩短。

判定方法：在 `SpawnRuneboltMageBolt` 里临时加一行日志
```csharp
Debug.Log($"[Runebolt] shots={pending.Shots.Count} attacker={pending.AttackerPosition} dir={direction} travel={travelDistance}");
```
看 `shots` 数量与各 `TargetPosition`：若只有 1 个目标，则纯粹是 11.1 的美术留白问题；若 ≥2 个且横向分离，则 11.4 也在起作用（此时应把 `direction` 改为朝**最远**目标，长度改用 `Vector3.Distance`）。

### 11.5 建议的修复组合（按性价比排序）

| # | 改动 | 位置 | 理由 |
|---|---|---|---|
| 1 | 盒高 36 → **60** | :98 常量 | 0.55 格，直接解决"细" |
| 2 | 长度补偿 `travelDistance / 0.9`（仅 V2） | :857-859 | 把 9% 美术留白补回来，让可见末端真正贴到敌人 |
| 3 | `runeboltMagePathHoldDuration` 0.04 → **0.12** | :100 + 场景 Inspector | 让"完整光束"有一帧可读，不再一闪而过 |
| 4 | 接 `RM nor Impact` 到 `CompleteRuneboltMageImpact`(:909) | CombatFxView | 复原参考图的敌人爆闪，缝隙自然被盖住 |
| 5 | 起点偏移 24 → 70 | :828 | 光柱从立绘外起，穿透感更强 |
| 6 | （根治）重出美术：首帧即满宽、左右无留白、末帧最亮的横贯式 6 帧 | 美术 | 当前 150×72 是彗星推进式，天生不适合"整线贯穿" |

第 2 项若不想动长度逻辑，等价的最小做法是把 `rect.sizeDelta.x` 直接写成 `travelDistance * 1.11f`（V2 分支内），伤害推进仍用未补偿的 `travelDistance`。

---

## 12. 手动修改步骤：修复「细 / 短 / 没挨住」（2026-09-24，可直接照抄）

目标：执行 11.5 的 **1~4 项**（第 5 项作为可选增强附在最后）。
文件：`Assets/DragonBound/Runtime/Presentation/CombatFxView.cs`（唯一文件，共 5 处代码改动 + 1 处场景改动）。

> 补充实测（写步骤时顺带核对）：
> - `RM nor Projectile.anim` = 6 帧 / 60fps，末帧 0.0833 → **clip length ≈ 0.10s**（所以"一闪而过"是真的，需要 hold 拉长）。
> - `RM nor Impact.anim` = 8 帧 / 60fps，末帧 0.1167 → **clip length ≈ 0.133s**，贴图 128×137（近似正方）。
> - `V2/Scenes/Greybox_Main.unity` 里 `runeboltMagePathHoldDuration: 0.04` 有两处：**14104**（对象 `ContentAnchor`）与 **41208**。

### 改动 1 —— 常量区：新增 Impact 资源路径（现 :31-32 之后）

定位：
```csharp
        private const string RuneboltMageNormalProjectileControllerPathV2 =
            "Animations/Hero/Runebolt Mage/normal/VFX/RM nor Projectile";
```
改为（在其后追加两行）：
```csharp
        private const string RuneboltMageNormalProjectileControllerPathV2 =
            "Animations/Hero/Runebolt Mage/normal/VFX/RM nor Projectile";
        private const string RuneboltMageNormalImpactControllerPathV2 =
            "Animations/Hero/Runebolt Mage/normal/VFX/RM nor Impact";
```

### 改动 2 —— 常量/序列化值：高度 36→60、新增留白系数、hold 0.04→0.12（现 :99-103）

原：
```csharp
        // V1 art (1080x1080 canvas, stroke occupies only 2.2%-8.3% of the height) keeps the original value.
        private const float RuneboltMagePathVisualHeightV1 = 300f;
        // V2 art (150x72 canvas, stroke occupies ~54% of the height): 0.33 cell, measured against the reference shot.
        private const float RuneboltMagePathVisualHeightV2 = 36f;
        private const float RuneboltMageMaximumPathLength = 550f;
        [SerializeField, Min(0f)] private float runeboltMagePathHoldDuration = 0.04f;
```
改为：
```csharp
        // V1 art (1080x1080 canvas, stroke occupies only 2.2%-8.3% of the height) keeps the original value.
        private const float RuneboltMagePathVisualHeightV1 = 300f;
        // V2 art (150x72 canvas, stroke occupies ~54%-61% of the height): 0.55 cell.
        // 36 was too thin at small board scale; 60 keeps the beam readable without covering the row.
        private const float RuneboltMagePathVisualHeightV2 = 60f;
        // V2 stroke never reaches the canvas edges: even the fullest frame ends at x142/150 and the
        // last frame at x136/150 (~9.3% blank on each side). Divide the gameplay length by this
        // ratio so the *visible* stroke actually lands on the enemy. Visual only.
        private const float RuneboltMagePathVisualPaddingRatio = 0.9f;
        private const float RuneboltMageMaximumPathLength = 550f;
        [SerializeField, Min(0f)] private float runeboltMagePathHoldDuration = 0.12f;
        [SerializeField] private Vector2 runeboltMageImpactSize = new Vector2(72f, 72f);
```

### 改动 3 —— 缓存字段：新增 Impact 控制器（现 :233-234）

原：
```csharp
        private RuntimeAnimatorController runeboltMageBoltController;
        private float runeboltMageBoltVisualHeight = -1f;
```
改为：
```csharp
        private RuntimeAnimatorController runeboltMageBoltController;
        private RuntimeAnimatorController runeboltMageNormalImpactController;
        private float runeboltMageBoltVisualHeight = -1f;
```

### 改动 4 —— `SpawnRuneboltMageBolt`：视觉长度补偿（现 :844-848）

原：
```csharp
            var travelDistance = Mathf.Clamp(
                farthestDistance,
                1f,
                RuneboltMageMaximumPathLength);
            pending.ConfigurePath(start, direction, travelDistance);
```
改为：
```csharp
            var travelDistance = Mathf.Clamp(
                farthestDistance,
                1f,
                RuneboltMageMaximumPathLength);
            // Visual compensation for the V2 art's ~9% blank padding: overshoot the drawn box so
            // the visible stroke reaches the enemy. Damage/hit progress below still uses
            // travelDistance, so the five-cell pierce (550) is untouched.
            var visualLength = travelDistance / RuneboltMagePathVisualPaddingRatio;
            pending.ConfigurePath(start, direction, travelDistance);
```

### 改动 5 —— `SpawnRuneboltMageBolt`：盒子宽度用补偿值（现 :866-868）

原：
```csharp
            rect.sizeDelta = new Vector2(
                travelDistance,          // ← X 轴保持原样，长度/5 格穿透不变
                visualHeight);
```
改为：
```csharp
            rect.sizeDelta = new Vector2(
                visualLength,            // ← 视觉长度；伤害判定仍是 travelDistance（5 格穿透不变）
                visualHeight);
```

### 改动 6 —— 新增 `SpawnRuneboltMageImpact` 方法（插在 :921 `SpawnRuneboltMageBolt` 结束花括号之后、`CompleteRuneboltMageHitsThrough` 之前）

```csharp
        private void SpawnRuneboltMageImpact(Vector3 position)
        {
            if (runeboltMageNormalImpactController == null)
            {
                // Probe silently: V1 has no V2 key, so a miss is expected, not an error.
                var registry = DragonBound.Presentation.UiAssets.Active;
                runeboltMageNormalImpactController = registry != null
                    ? registry.Load<RuntimeAnimatorController>(RuneboltMageNormalImpactControllerPathV2)
                    : null;
            }

            if (runeboltMageNormalImpactController == null)
            {
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;

            var root = new GameObject(
                "Runebolt Mage Impact",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator));

            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.position = position;
            rect.sizeDelta = runeboltMageImpactSize;

            var image = root.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = runeboltMageNormalImpactController;
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);
            animator.speed = 1f;

            var duration = runeboltMageNormalImpactController.animationClips.Length > 0
                ? runeboltMageNormalImpactController.animationClips[0].length
                : 0.15f;
            duration = Mathf.Max(0.01f, duration);

            active.Add(new ActiveFx(
                rect,
                image,
                null,
                position,
                position,
                false,
                false,
                duration));
        }
```

### 改动 7 —— 在命中结算里播放爆闪（现 :943-953，`CompleteRuneboltMageImpact`）

原：
```csharp
            if (combatEvent.Damage > 0f)
            {
                lane?.PlayEnemyHitShake(combatEvent.TargetRuntimeId);
            }
```
改为：
```csharp
            // Reference shot: every pierced enemy gets a ~0.44 cell white burst, which is what
            // visually "connects" the beam tip to the enemy.
            SpawnRuneboltMageImpact(position);
            if (combatEvent.Damage > 0f)
            {
                lane?.PlayEnemyHitShake(combatEvent.TargetRuntimeId);
            }
```
（`position` 是上面已解析出的敌人最新位置，变量已在作用域内，直接用即可。）

### 改动 8（可选）—— 起点偏移 24 → 70（现 :834）

```csharp
            var start = pending.AttackerPosition + (direction * 24f);
```
→
```csharp
            var start = pending.AttackerPosition + (direction * 70f);
```
让光柱从立绘外侧起手，穿透感更强。若改完发现起点悬空，回退到 48。

### 改动 9 —— 场景里的 `runeboltMagePathHoldDuration`（**必做，否则改动 2 的 0.12 不生效**）

`[SerializeField]` 值被场景覆盖，改代码默认值不会自动同步。必须手动改：

- `Assets/DragonBound/UI/Variants/V2/Scenes/Greybox_Main.unity`
  - 第 **14104** 行（对象 `ContentAnchor`）：`runeboltMagePathHoldDuration: 0.04` → `0.12`
  - 第 **41208** 行：`runeboltMagePathHoldDuration: 0.04` → `0.12`
- `Assets/DragonBound/UI/Variants/V1/Scenes/Greybox_Main.unity`（13210 / 40313）**保持 0.04 不动** —— V1 是 300 高的横贯式大特效，拉长 hold 会显得拖沓。

改法二选一：
1. Unity 里打开 V2 场景 → Hierarchy 搜索框输入 `t:CombatFxView` → 选中 → Inspector 把 `Runebolt Mage Path Hold Duration` 改成 `0.12` → **Ctrl+S 保存场景**。
2. 直接用文本编辑器改上面两行（Unity 会热重载）。

### 验证清单

1. 编译通过（新增字段/方法无重名）。
2. V2 场景进战斗，Runebolt 普攻：光柱应比之前粗（0.55 格）、末端压在最后一名敌人身上、每个被命中敌人身上有白色爆闪。
3. 5 格穿透不变：`travelDistance` 与 `pending.ConfigurePath(...)` 未改，最远仍截到 550；`visualLength` 只喂给 `sizeDelta.x`。
4. 若还差一点：把 `RuneboltMagePathVisualPaddingRatio` 从 `0.9` 调到 `0.85`（再补 5% 长度），**不要动** `RuneboltMageMaximumPathLength`。
5. 若爆闪太大/太小：改 Inspector 里新增的 `Runebolt Mage Impact Size`（默认 72×72，范围建议 48~110）。
6. 仍嫌短且命中数 ≥2：按 11.4 加一行日志确认 `shots` 数量，若多目标横向分离，再把 `direction` 改为朝最远目标、长度改用 `Vector3.Distance`（属另一次改动，本轮未包含）。

---

## 13. 现象分析：改完 1~4 项后「线路依然到不了最远目标」（2026-09-24，仅分析未改码）

### 13.1 新证据：Runebolt 的释放事件**永远不会触发**，每次都走 0.38s 兜底

两处硬事实：

1. **V2 的 `Runebolt Mage.json` 根本没有 Spine 事件**
   - `Variants/V2/.../Hero/Runebolt Mage/normal/Spine/Runebolt Mage.json`：顶层无 `events` 字段，`Attack` 动画内也无 `events`（已用脚本逐个 json 解析验证）。
   - 对照：`WindclawRanger.json` 有 `"events":{"AttackRelease":{},"SkillRelease":{}}`，且 `Attack` 动画含 `{"time":0.18,"name":"AttackRelease"}`；`DragonRider.json` 同样有（`Attack` 在 0.22s）。
   - 另两个（`誓冠剑士 拆.json`、`雷霆领主 拆.json`）也无 events。
2. **项目里没有任何业务代码订阅 Spine 事件**：全仓搜 `AnimationState.Event` / `.Event +=` / `TrackEntry`，只在 `Assets/Spine/Runtime/spine-csharp/` 运行时内部出现。`HeroFormationView` 只用了 `heroAttackSkeleton.AnimationState.SetAnimation / AddEmptyAnimation`（:575/674/680），**没注册监听器**。

结论：`HeroFormationView.OnRuneboltMageBoltRelease()`（:1360，注释写 "Called by the authored Runebolt Mage clip after its fourteenth frame"）在 V2 的 Spine 路径下**不可能被调用**，`RuneboltMageBoltReleased` 事件从不触发，Runebolt 的 FX **100% 靠兜底释放**：

```csharp
// CombatFxView.cs:749-778 TickPendingRuneboltMageCasts
entry.Value.Elapsed += deltaSeconds;
if (entry.Value.Elapsed < runeboltMageReleaseFallbackDelay) continue;   // 0.38s
```

即：**每一发都在伤害结算之后整整 0.38s 才画出来**（Runebolt 的 Attack 动画时长 0.833s，光柱出现在动作播到约 45% 时，本就与动作脱节）。

### 13.2 直接后果：光柱用的是 0.38 秒前的敌人坐标快照

`QueueRuneboltMageCast`（:720-742）在伤害结算那一帧把 `targetPosition` 存进 `PendingRuneboltMageShot.TargetPosition`（**只读属性**，:5093），之后 `SpawnRuneboltMageBolt` 的 `direction`、`travelDistance` 全部基于这份快照。

而敌人在持续移动（`TwentyWavePressureConfiguration.cs:87-89`）：

| 类型 | 速度 |
|---|---|
| Normal | 0.60 格/秒 |
| Fast | 0.80 格/秒 |
| Elite | 0.58 格/秒 |

0.38s × 0.6 格/s = **0.228 格 ≈ 25 单位**（Fast 型 0.30 格 ≈ 33 单位）。敌人若朝远离法师的方向走，光柱末端正好差这么多；再加光柱播放 + hold 期间（0.1 + 0.12s）又走掉约 0.07 格，缺口接近 **0.3 格**。

更麻烦的是**参照物不一致**：`CompleteRuneboltMageImpact`（:943-953）取的是**实时**位置：

```csharp
if (lane != null && lane.TryGetEnemyPosition(combatEvent.TargetRuntimeId, out var currentPosition))
    position = currentPosition;      // 爆闪画在"现在"的位置
```

于是：**爆闪在敌人身上，光柱末端停在 0.3 格开外的旧位置** —— 接上 Impact 之后，这个"差一截"反而比之前更显眼。这就是"改完还是没挨住"的观感来源。

### 13.3 与上一轮补偿的叠加关系

| 来源 | 缺口 | 上一轮是否已解决 |
|---|---|---|
| 美术两端留白 9%（末帧 sdj0006 右留白 9.3%） | 2.5 格距离上 ≈0.23 格 | 已用 `/0.9` 补偿（第 12 节改动 4/5） |
| **位置快照过期 0.38s** | **0.23~0.30 格** | **未解决（本轮新发现）** |
| 多目标横向分离的 Dot 投影 | 通常 <0.05 格 | 未解决（0.35 格宽度内影响很小） |

两者量级相当且**叠加**，所以补完 9% 之后仍差约 0.3 格（截图 61px/格 ≈ 18px），肉眼就是"到不了最远目标"。

### 13.4 一键验证（临时日志，确认落在哪一档）

在 `SpawnRuneboltMageBolt` 里 `travelDistance` 算完之后临时加：

```csharp
foreach (var shot in pending.Shots)
{
    var stale = shot.TargetPosition;
    var fresh = lane != null &&
                lane.TryGetEnemyPosition(shot.CombatEvent.TargetRuntimeId, out var p)
        ? p
        : stale;
    Debug.Log($"[Runebolt] id={shot.CombatEvent.TargetRuntimeId} " +
              $"stale={stale} fresh={fresh} drift={Vector3.Distance(stale, fresh):0.#}");
}
Debug.Log($"[Runebolt] shots={pending.Shots.Count} travel={travelDistance:0.#} visual={visualLength:0.#}");
```

判读：
- `drift` ≈ 25~35 → 13.2 成立（位置快照过期），是主因。
- `drift` ≈ 0 但依然短 → 回到第 12 节改动 5：`visualLength` 是否真的写进了 `sizeDelta.x`。
- `shots ≥ 2` 且 drift 大 → 同时存在 11.4 的投影问题。

### 13.5 修复方向（按根治程度排序，未执行）

| # | 方案 | 位置 | 说明 |
|---|---|---|---|
| 1 | **绘制前刷新目标实时位置** | `SpawnRuneboltMageBolt` 开头遍历 `pending.Shots`，用 `lane.TryGetEnemyPosition(shot.CombatEvent.TargetRuntimeId, out var p)` 覆盖；需把 `PendingRuneboltMageShot.TargetPosition`（:5093）改成 `{ get; private set; }` 或加 setter | **根治**：方向、长度、爆闪三者统一到同一时刻坐标，末端恒等于最远敌人的当前位置，滞后多久都不影响 |
| 2 | **兜底延迟 0.38 → 0.12** | `CombatFxView.cs:113` + V2 场景 `Greybox_Main.unity` 第 14108 / 41214 行 `runeboltMageReleaseFallbackDelay`（V1 的 13212/40315 可视情况保留 0.38） | 缺口从 0.3 格降到约 0.09 格；顺便让光柱贴合攻击动作（现在晚 0.38s 才出，动作都快播完了） |
| 3 | 让 Release 真正触发 | 订阅 `heroAttackSkeleton.AnimationState.Event`，并在 `Runebolt Mage.json` 的 `Attack` 里补 `{"time":0.18,"name":"AttackRelease"}`（与 Windclaw 对齐） | 最"正确"，但要改 JSON + 写订阅代码，工作量最大 |
| 4 | padding 0.9 → 0.8 | 常量 | 只是再多补 12% 长度掩盖问题，方向仍可能歪，不推荐单独用 |

推荐组合：**1 + 2**（1 保证末端贴合，2 保证时机贴动作）。

---

## 14. 具体修改步骤：① 绘制前刷新实时位置（根治）+ ② 兜底延迟 0.38→0.16（2026-09-24）

目标：让光柱末端**恒等于绘制那一刻最远敌人的真实位置**，并把"晚 0.38 秒才画"压到 0.16 秒。
全部改动仍在 `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs`（行号为本次改动前，建议**自下往上**改，或按锚点文本搜）。

### 改动 1 — 给 `TargetPosition` 加可写入口（:5093）

```csharp
            public CombatEvent CombatEvent { get; }
            public Vector3 TargetPosition { get; private set; }     // ← 去掉 get-only
            public float PathProgress { get; set; }
            public bool ImpactCompleted { get; set; }

            public void SetTargetPosition(Vector3 position)         // ← 新增
            {
                TargetPosition = position;
            }
```

> 构造函数里 `TargetPosition = targetPosition;` 对 `private set` 依然合法，无需改。

### 改动 2 — 新增刷新方法（放在 `SpawnRuneboltMageBolt` 之前，:820 上方）

```csharp
        private void RefreshRuneboltMageTargetPositions(PendingRuneboltMageCast pending)
        {
            if (lane == null)
            {
                return;
            }

            for (var i = 0; i < pending.Shots.Count; i++)
            {
                var shot = pending.Shots[i];
                if (shot.ImpactCompleted)
                {
                    continue;
                }

                // 敌人可能已死亡/离场：查不到就保留伤害结算时的快照，不动。
                if (lane.TryGetEnemyPosition(
                        shot.CombatEvent.TargetRuntimeId,
                        out var currentPosition))
                {
                    shot.SetTargetPosition(currentPosition);
                }
            }
        }
```

### 改动 3 — 在 `SpawnRuneboltMageBolt` 里，排序之前调用（:834）

```csharp
            // Draw against where the enemies are RIGHT NOW: the FX is released by a fallback
            // timer, so the damage-frame snapshot can be a quarter-cell stale.
            RefreshRuneboltMageTargetPositions(pending);            // ← 新增
            pending.SortByDistance();
```

必须放在 `SortByDistance()` **之前**，让"最远目标"也按实时位置重新排。之后的 `direction`、`travelDistance`、`ConfigurePath` 全部自动跟着新坐标走，**无需再改下面任何一行**。

### 改动 4 — 兜底延迟 0.38 → 0.16（:113）

```csharp
        [SerializeField, Min(0.1f)] private float runeboltMageReleaseFallbackDelay = 0.16f;
```

取值理由：Windclaw / DragonRider 的真事件 `AttackRelease` 分别在 0.18s / 0.22s；Runebolt 的 Spine 没有事件（见第 13.1 节），所以兜底值取 **0.16** 最接近"出手那一帧"，同时把位置滞后从 0.228 格降到 0.096 格。

### 改动 5 — 场景同步（必做，否则改动 4 被序列化值覆盖）

`[SerializeField]` 被场景实例值覆盖。改 `Assets/DragonBound/UI/Variants/V2/Scenes/Greybox_Main.unity`：

| 行号 | 字段 | 现值 → 新值 |
|---|---|---|
| 14108 | `runeboltMageReleaseFallbackDelay` | `0.38` → `0.16` |
| 41214 | `runeboltMageReleaseFallbackDelay` | `0.38` → `0.16` |

（V1 场景 13212 / 40315 建议**保持 0.38**，V1 观感已定型。）

或在 Unity 里：打开 V2 的 `Greybox_Main`，Hierarchy 搜 `t:CombatFxView`，把 **Runebolt Mage Release Fallback Delay** 改成 `0.16` 后 Ctrl+S —— 上面两处会被同时写回。

> 已核实：上一轮的 `runeboltMagePathHoldDuration` 在 V2 场景已是 `0.12`（14105 / 41211），本轮不用再动。

### 改动 6（可选）— 光柱播放期间继续跟随最远敌人

做完 1~5 后仍嫌"差一点点"，是因为光柱画完就不再动，而播放 + hold 共约 0.22s 内敌人又走约 0.13 格。在 `SpawnRuneboltMageBolt` 末尾 `active.Add(...)` 的 `normalized => { ... }` 回调**开头**插入：

```csharp
                RefreshRuneboltMageTargetPositions(pending);
                pending.SortByDistance();
                var tip = pending.Shots[pending.Shots.Count - 1].TargetPosition;
                var liveLength = Mathf.Clamp(
                    Vector3.Distance(tip, start) / RuneboltMagePathVisualPaddingRatio,
                    1f,
                    RuneboltMageMaximumPathLength * 1.5f);
                rect.sizeDelta = new Vector2(liveLength, rect.sizeDelta.y);
                rect.localRotation = Quaternion.Euler(
                    0f, 0f,
                    Mathf.Atan2(tip.y - start.y, tip.x - start.x) * Mathf.Rad2Deg);
```

代价：光柱会随敌人移动轻微伸缩/摆动，且 `PathProgress`（伤害推进时机）仍是开局算的一次值，可能与画面不同步。**建议先上 1~5 看效果，再决定要不要它。**

### 验证（改完后打一次日志即可）

在改动 3 之后、`direction` 计算之前临时加：

```csharp
            foreach (var shot in pending.Shots)
            {
                var fresh = lane != null &&
                            lane.TryGetEnemyPosition(shot.CombatEvent.TargetRuntimeId, out var p)
                    ? p : shot.TargetPosition;
                Debug.Log($"[Runebolt] drift={Vector3.Distance(shot.TargetPosition, fresh):0.###} " +
                          $"travel={travelDistance:0.#} visual={visualLength:0.#} shots={pending.Shots.Count}");
            }
```

- 想确认"改之前差多少"：把这段日志放在改动 3 **之前**打一次，应看到 drift ≈ 25~35（未刷新时的缺口，即 0.23~0.30 格）。
- 放在改动 3 **之后**打则 drift ≈ 0（刷新后自然相等），真正的判据是**末端是否贴住敌人**。
- 仍短且 drift ≈ 0 → 回查第 12 节改动 5：`sizeDelta.x` 是否真的写成了 `visualLength`。

### 影响面确认

- **5 格穿透不变**：`RuneboltMageMaximumPathLength`(550) 的 clamp 与 `ConfigurePath` 都在补偿之外，改动 1~5 只改变"用哪个坐标算"。
- **其它英雄不受影响**：`RefreshRuneboltMageTargetPositions` 只被 Runebolt 路径调用，`PendingRuneboltMageShot` 也只用于 Runebolt。
- **V1 安全**：`lane` 为 null 或查不到敌人时直接保留旧快照，行为与改动前完全一致。

---

## 15. 重新分析：为什么"线路到不了最远目标"（2026-09-24，结合截图 + 美术帧实测）

需求重述：**光柱应是一条从法师出发、穿过所有受伤敌人、末端咬住最远目标的完整直线。**

### 15.1 截图实测（对 clipboard-2026-09-24 那张 464x404 截图做像素聚类）

| 元素 | 实测位置（截图像素） | 说明 |
|---|---|---|
| 英雄（Runebolt Mage） | 蓝色团块 bbox (256,240)-(346,339)，中心 ≈ **(301,290)** | 法师本体 |
| 命中爆闪（青色环） | bbox (203,81)-(245,126)，中心 **(224,103)**，直径 ≈ 43px | = `runeboltMageImpactSize (72,72)` × 0.55 px/单位 ≈ 40px ✔ 尺寸正确，位置就在最上方敌人的血条下方 |
| 光柱（可见部分） | 蓝色能量像素从 **(270,218) 到 (223,136)**，跨度 ≈ 92px，轴角 ≈ **60°（屏幕）** | 屏幕上只有宝箱一小段 |

推论：

1. **应有长度**：start（英雄前方 48 单位 ≈ 27px）→ 爆闪中心的距离 ≈ 202 − 27 = **175px**。
2. **实测可见长度** ≈ **92px，仅为应有的 53%**。
3. 光柱轴线 60° 与"英雄→爆闪"方向 68° 差约 8°（小），说明**方向基本对**；**长度明显短**。
4. 英雄侧也有空档：光柱下端 218 距英雄中心 290 有 72px（≈1.2 格）。

### 15.2 美术帧实测（决定性证据）

对 `V2/.../Runebolt Mage/normal/Projectile/sdj0001..0006.png`（150x72）逐帧统计（alpha>24 的包围盒）：

| 帧 | 内容 x 范围 | 横向覆盖 | 左留白 | 右留白 | 高度占比 |
|---|---|---|---|---|---|
| sdj0001 | 58..141 | **56%** | **39%** | 5% | 44% |
| sdj0002 | 6..143 | **92%** | 4% | 4% | 68% |
| sdj0003 | 7..141 | 90% | 5% | 5% | 69% |
| sdj0004 | 6..137 | 88% | 4% | 8% | 67% |
| sdj0005 | 10..138 | 86% | 7% | 7% | 60% |
| sdj0006 | 11..137 | **85%** | 7% | **8%** | 62% |

两条硬结论：

1. **没有一帧是"满幅贯穿线"**，最满的帧 2 也只有 92%，且每帧的起止位置都在漂移。光柱 box 被拉伸成长条后，屏幕上那条线的可见长度 = `boxLength × 当前帧覆盖率`，所以**任何时刻都不是一条完整的线**。
2. `animator.Play(0,0,0f)` 从 **帧 1** 开始，而帧 1 的内容**横向只占 56%、左留白 39%** —— 播放的头 1/6 时间（≈17ms）光柱是"半截、且靠英雄一侧空 39%"的。这与 15.1 里"英雄侧空 1.2 格"完全对应。

对照 V1 的美术（`V1/.../VFX/Runebolt Mage/road/符文雷矢法师普攻特效_*.png`，1080x1080）：覆盖 36%~77%，两端留白更多（最满帧左空 15%、右空 8%），**换贴图并不能解决**，只是把"细线"换回"细线"。

### 15.3 根因排序

| # | 根因 | 量级 | 证据 |
|---|---|---|---|
| 1 | **`travelDistance` 被投影缩水** … `direction` 取的是 `pending.Shots[0]`（`SortByDistance` 升序 → **最近**目标），长度却取所有目标在该方向上的 `Dot` 投影。多目标方位不一致时，投影 = 真实距离 × cosθ | 实测只剩 **53%** → 夹角约 58° | 光柱 92px vs 应有 175px；方向只差 8° 但长度差一半 |
| 2 | **美术帧内容 56%~92% 波动**，首帧仅 56% 且左空 39% | 播放期间 0~44% 的缺口 | 15.2 表格 |
| 3 | 末帧（hold 阶段显示）右留白 8% → 敌人侧固定缺口 | `0.08 × L`，被 paddingRatio 0.9 抵消后 ≈ −2%（**这一项其实已解决**） | 15.2 + 现值 `RuneboltMagePathVisualPaddingRatio = 0.9` |
| 4 | 起点偏移 48 单位（≈0.44 格）+ 帧 1 左空 39% | 英雄侧空 1.2 格 | 15.1 |
| 5 | 位置快照过期（已由第 14 节刷新修掉） | 已解决 | — |

> 关键：**第 3 项的数学刚好抵消**，所以"末端差一截"不是 padding 不够，而是第 1 项（长度算小）和第 2 项（帧不满幅）叠加的结果。继续调 padding / height / fallback 都不会再改善。

### 15.4 修复方案（结合"到达最远目标 + 穿过中间受伤敌人"）

#### 方案 A（推荐，纯代码，改动小）— 方向朝最远目标 + 光柱定格/循环满幅帧

1. **方向与长度改用最远目标**（`SpawnRuneboltMageBolt`，刷新与排序之后）：

```csharp
            pending.SortByDistance();                                  // 升序：最后一个是最远
            var farthestShot = pending.Shots[pending.Shots.Count - 1];
            var direction = farthestShot.TargetPosition - pending.AttackerPosition;
            if (direction.sqrMagnitude <= 0.0001f) { CompleteRuneboltMageCast(pending); return; }
            direction.Normalize();

            var start = pending.AttackerPosition + (direction * 24f);
            // 末端必须落在最远目标上：方向已朝它，投影 == 距离。
            var travelDistance = Mathf.Clamp(
                Vector3.Distance(farthestShot.TargetPosition, start),
                1f,
                RuneboltMageMaximumPathLength);
            var visualLength = travelDistance / 0.92f;   // 见第 3 步
```

   > `Vector3.Distance` 替代 `Dot` 的好处：即便个别目标偏离轴线（多目标散开），长度也**保证覆盖最远目标**，不会被投影吃掉。伤害推进仍走 `ConfigurePath(start, direction, travelDistance)`。
   >
   > 注意：若命中目标分散到一条直线覆盖不了（截图里横向散布很大），**优先满足"到达最远目标"**——这也是需求里明确的那一条。

2. **不播 6 帧的"生长"过程，改成定格在帧 2**（92% 覆盖，两端各 4%）：

```csharp
            animator.runtimeAnimatorController = pathController;
            animator.Rebind();
            animator.speed = 0f;                    // 定格
            animator.Play(0, 0, 1f / 6f);           // 直接跳到第 2 帧
            animator.Update(0f);
```

   若要保留一点动感：先按原样播 0.04s（到帧 2 附近）再把 `animator.speed = 0f`，或在 update 回调里把时间循环限制在 `[1/6, 3/6]`（帧 2~4，覆盖 88%~92%）：

```csharp
                    var loopT = 1f / 6f + Mathf.Repeat(elapsed / Mathf.Max(0.01f, clipLength), 2f / 6f);
                    animator.Play(0, 0, loopT);
```

3. **padding 用帧 2 的真实覆盖率反算**：可见段是 `[0.04L, 0.96L]`，要让"可见右端"落在距离 D 处 → `L = D / 0.92`。所以把

```csharp
        private const float RuneboltMagePathVisualPaddingRatio = 0.92f;   // 由 0.9 改
```

   （数值含义从"补留白"变成"帧覆盖率的倒数"。）

4. 起点偏移 `48f → 24f`（或 0），把英雄侧空档收掉一半以上。

#### 方案 B（彻底满足"一条完整贯穿线"）— 换"无缝光带"贴图

现美术（无论 V1/V2）都不是"两端无留白的贯穿线"，只要还用这套帧，就永远存在"某帧缺一截"。根治做法：

- 让美术出一条 **128×64、首尾满幅、可无缝平铺** 的光带（或直接把现有最满帧的两端空白裁掉、做成无缝 tile）；
- 代码里 `image.type = Image.Type.Tiled`，`sprite` 用该 tile，box 拉伸到 `start → 最远目标`；
- 末端再叠一个"头光"（可直接复用 `Muzzle/41006_MuzzleNormal1_*` 或 `Impact` 的首帧）补出"咬住敌人"的亮点。

这就得到需求描述的完整效果：**一条连续光带，从法师出发，穿过中间所有受伤敌人，末端打在 / 停在最远目标身上。**

#### 方案 C（最小改动，只治"长度"）— 只做 A 的第 1 步

不动美术、不改播放方式，仅把方向/长度改成朝最远目标。收益：长度不再缩水（从 53% → 100%）；帧覆盖率问题仍在（播放期间仍会看到"半截"）。

### 15.5 一键验证（确认第 1 项根因）

在 `SpawnRuneboltMageBolt` 里 `travelDistance` 算完后：

```csharp
            Debug.Log($"[Runebolt] shots={pending.Shots.Count} " +
                      $"dir={(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg):0.#} " +
                      $"travel={travelDistance:0.#} " +
                      $"toFarthest={Vector3.Distance(pending.Shots[pending.Shots.Count - 1].TargetPosition, start):0.#} " +
                      $"list=[{string.Join(",", pending.Shots.ConvertAll(s => $"({s.TargetPosition.x:0},{s.TargetPosition.y:0})"))}]");
```

判读：
- `shots=1` 且 `travel ≈ toFarthest` → 长度本该正确，问题全在美术帧（走方案 A 第 2/3 步或 B）。
- `shots ≥ 2` 且 `travel < toFarthest`（比如 0.5 倍）→ **第 1 项根因成立**，按方案 A 第 1 步修。
- `list` 里各坐标方向散开 → 目标确实不共线，此时"到达最远目标"只能靠朝最远目标绘制。

---

## 16. 方案 A 具体修改步骤（2026-09-24，用户手动改）

目标：**光柱末端 = 最远敌人的当前位置**，中间的受伤敌人自然被同一条线穿过；同时把"看得见的那一段"拉满。

主战场仍是 `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs`（行号 = 本次改动前，建议**自下往上**改）。

---

### 改动 1（核心）— 方向朝最远目标 + 长度用真实距离（替换 :879-907）

把 `RefreshRuneboltMageTargetPositions` 之后到 `pending.ConfigurePath(...)` 这一段**整块替换**成：

```csharp
            // Draw against where the enemies are RIGHT NOW: the FX is released by a fallback
            // timer, so the damage-frame snapshot can be a quarter-cell stale.
            RefreshRuneboltMageTargetPositions(pending);
            pending.SortByDistance();
            // Shots is sorted NEAREST-FIRST, so the last entry is the farthest target.
            // Aim the beam at it: everything in between lies on the same ray and is
            // therefore covered by the same stroke.
            var farthestShot = pending.Shots[pending.Shots.Count - 1];
            var direction = farthestShot.TargetPosition - pending.AttackerPosition;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                CompleteRuneboltMageCast(pending);
                return;
            }
            direction.Normalize();

            var start = pending.AttackerPosition + (direction * 24f);
            // Real distance (NOT a dot projection): with `direction` pointing at the farthest
            // target this equals its projection, so no more cos-theta shrinkage when the
            // targets are spread apart.
            var travelDistance = Mathf.Clamp(
                Vector3.Distance(farthestShot.TargetPosition, start),
                1f,
                RuneboltMageMaximumPathLength);
            // Visual compensation for the V2 art's blank padding: overshoot the drawn box so
            // the visible stroke reaches the enemy. Damage/hit progress still uses
            // travelDistance, so the five-cell pierce (550) is untouched.
            var visualLength = travelDistance / RuneboltMagePathVisualPaddingRatio;
            pending.ConfigurePath(start, direction, travelDistance);
```

被删掉的旧代码（供核对）：`direction = Shots[0]...`、`start = AttackerPosition + direction * 48f`、遍历 `farthestDistance = Mathf.Max(..., Vector3.Dot(...))`。

为什么这样就"穿过中间敌人"：`ConfigurePath` 仍按同一 `direction` 给每个 shot 算 `PathProgress = Dot(target-start, dir)/travelDistance`，共线的敌人落在这条线段上，伤害推进时机自动排队；而画面上那条 box 从 `start` 一直画到最远目标，视觉与判定完全重合。

---

### 改动 2 — 起点偏移 48 → 24（已包含在改动 1 的代码里）

`var start = pending.AttackerPosition + (direction * 24f);`

原因：定格帧左右各只有 4% 留白，线几乎从 box 左边缘开始，48 单位的空档会明显露出来；24 单位刚好从法师身前出发。

---

### 改动 3 — `RuneboltMagePathVisualPaddingRatio` 0.9 → 0.96（:108）

```csharp
        // Frozen frame 2 spans x7..x142 of 150 -> 4% blank on each side.
        private const float RuneboltMagePathVisualPaddingRatio = 0.96f;
```

含义变成「所选帧的覆盖率」：`L = D / 0.96`，末端可见边 ≈ `0.96L = D`，正好落在最远敌人身上。

---

### 改动 4 — 动画定格在覆盖率最高的那一帧（替换 :938-947 的播放部分）

```csharp
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = pathController;
            animator.Rebind();
            // Freeze on frame 2 (normalized 1/6 = 0.1667): the only frame whose stroke covers
            // ~92% of the canvas with just 4% blank on each side. Frame 1 covers only 56%
            // (39% blank on the left) and frame 6 ends at x136/150 (~9% blank on the right).
            animator.speed = 1f;
            animator.Play(0, 0, 1f / 6f);
            animator.Update(0f);
            animator.speed = 0f;          // ← 停在帧 2，不再推进
            var clipLength = pathController.animationClips.Length > 0
                ? pathController.animationClips[0].length
                : 0.1f;
            clipLength = Mathf.Max(0.01f, clipLength);
```

**为什么必须这么改（新发现）**：`RM nor Projectile.controller` 的 AnimatorState 里
`m_Speed: 0.1`（不是 1！）。所以 0.1s 的 clip 实际要 **1 秒**才播完，而 `ActiveFx` 的寿命只有 `clipLength(0.1) + hold(0.12) + fade(0.08) = 0.30s` —— 光柱在死掉时只播到 clip 的 **30%**，也就是说它一生都停在**帧 1（覆盖 56%、左侧空 39%）**和帧 2 之间。这就是"细、短、没挨住"里"短"的第二重来源（第一重是改动 1 修掉的投影缩水）。

> 想保留一点"飞行生长"的动感，见本节末尾**变体 A2**。

---

### 改动 5（可选，推荐）— 定格后多看 0.06 秒

定格帧是静止的，hold 0.12s + fade 0.08s 略短。若嫌一闪而过：V2 `Greybox_Main.unity` 的
`runeboltMagePathHoldDuration: 0.12` → `0.2`（**14105 / 41211** 两处，或 Unity 里搜 `t:CombatFxView` 改 Inspector 后 Ctrl+S）。
注意 `[SerializeField]` 被场景值覆盖，改代码默认值无效。

---

### 改动 6（可选，仅为代码默认值对齐）

`:113` 的 `runeboltMageReleaseFallbackDelay = 0.38f` → `0.16f`。
场景里**已经是 0.16**（14108 / 41214），所以这条不改也能跑；改了只是让新场景/新预制体不再继承 0.38。

---

### 变体 A2：保留"生长动画"（与改动 4 二选一）

如果希望看到光柱从法师手里"甩出去"而不是直接定格：

1. 用文本编辑器打开
   `Assets/DragonBound/UI/Variants/V2/Content/Resources/Animations/Hero/Runebolt Mage/normal/VFX/RM nor Projectile.controller`
   - `m_Speed: 0.1` → `m_Speed: 1`（让 6 帧在 0.1s 内播完，与代码算的 `clipLength` 一致）
   - `m_LoopTime: 1` → `m_LoopTime: 0`（播完停在末帧，否则 hold 期间会循环回空白的帧 1）
2. 代码里**不做**改动 4，保持原来的 `animator.Play(0, 0, 0f)` + `animator.speed = 1f;`。
3. `RuneboltMagePathVisualPaddingRatio` 保持 **0.9**（末帧右留白 ≈9.3%，`L = D/0.9` 正好补掉）。
4. 起点偏移仍是 24（改动 2）。

代价：起播瞬间（≈0.017s）是帧 1，英雄侧会有一闪的空档；末帧覆盖 85% 略细于帧 2。
**建议先用改动 1~4（定格），确认"能挨住最远敌人"之后再试 A2。**

---

### 验证（一次日志即可判定）

在改动 1 的 `travelDistance` 算完之后临时加：

```csharp
            Debug.Log($"[Runebolt] shots={pending.Shots.Count} " +
                      $"start={start} dir={direction} " +
                      $"travel={travelDistance:0.#} visual={visualLength:0.#} " +
                      $"toFarthest={Vector3.Distance(farthestShot.TargetPosition, start):0.#}");
            foreach (var shot in pending.Shots)
            {
                Debug.Log($"[Runebolt]   t={shot.TargetPosition} " +
                          $"proj={(Vector3.Dot(shot.TargetPosition - start, direction)):0.#} " +
                          $"lateral={Vector3.Cross(direction, shot.TargetPosition - start).magnitude:0.#}");
            }
```

判读：
- `visual ≈ travel / 0.96` 且 `travel ≈ toFarthest` → 改动 1 生效；末端应正好压在最远敌人身上。
- `lateral` 各目标都应 ≲ 19（0.35 格宽度的一半 × 110）；若某个明显大于 19，说明它不在玩法判定的那条线上（玩法 `IsInsideLine` 侧向阈值 0.175 格），画面会"擦过它边上"——这是数据问题，不是特效问题。
- 画面仍短：回查 `:926` 的 `sizeDelta.x` 是否真写成 `visualLength`、`preserveAspect` 是否仍为 `false`。

---

### 影响面

- **5 格穿透不变**：`RuneboltMageMaximumPathLength`(550) 的 clamp 与 `ConfigurePath` 都用的是未补偿的 `travelDistance`。
- **V1 不受影响**：V1 走 `RuneboltMageBoltControllerPath`（`Animation/Runebolt MageBoom`），state speed 是 1；改动 4 的 `1f/6f` 对 6 帧序列同样落在帧 2（V1 帧 2 覆盖 ≈49%，比 V1 帧 1 好），若 V1 观感变了，把 `1f/6f` 改成只在 V2 分支生效即可（用 `runeboltMageBoltVisualHeight > 0f` 之外的 V2 标记，或直接读 `UiAssets.Active` 的 variantId）。
- **其它英雄不受影响**：`PendingRuneboltMageCast` / `SpawnRuneboltMageBolt` 只服务 Runebolt。

---

## 17. 整合分析：为什么"依然到不了最远敌人"+"没有动态"（2026-09-24，仅分析）

这次把三层（玩法判定 / FX 批处理 / 美术资源播放）一起对齐后，问题的构成才清楚。以下所有数字都是从截图逐像素量出来的，不是推测。

### 17.1 截图实测（453x364，棋盘格 59.5px = 110 单位，1 单位 = 0.541px）

| 元素 | 实测 | 换算 |
|---|---|---|
| 法师格子中心 | ≈ (196, 277) | —— |
| 光柱可见段 | (203,266) → (255,161)，长 **127px** | **2.13 格** |
| 光柱轴角 | 57°±3°（指向"中间行最右"那个敌人） | —— |
| 该敌人身上 | 青色**环**（Impact 第 2~3 帧） | 最年轻的一次爆闪 |
| 上排右敌 (318,107) | 青色**实心盘**（Impact 第 4 帧） | 更老的爆闪，**光柱没有到它** |
| 上排左敌 (260,96) | 青色盘 | 同上 |

"法师 → 上排右敌"这条射线做亮度剖面：前 127px 是光柱（lum 200~255、b-r 30~120），**127px 之后到敌人身上之前只有棋盘的高光十字（纯白小斑，b-r≈0）** —— 即光柱的 box 确实只到 2.13 格处，这一点是确定的。

### 17.2 结论一：上排敌人的爆闪不是"这一发"打的

`OnCombat` → `QueueRuneboltMageCast` 的批是按 **帧** 分的（`pending.CreatedFrame != Time.frameCount`），而玩法侧一次 `ResolveRuneboltAttack` 会在**同一帧**把走廊内所有敌人一次性打伤。所以：

- 同一发的所有 shot 一定在同一批里；光柱终点 = 这批里最远的那个；
- 截图里光柱终点在 2.13 格，说明**这一发的最远受伤敌人就是它**；
- 上排那两个的爆闪只能来自**更早的攻击批次**。

爆闪的寿命是 `clip.length = 0.133s`，而它是在光柱播到自己 `PathProgress` 的进度时才生成的；最远那一个（PathProgress=1）会在光柱的 clip 阶段末尾生成，被 `CompleteRuneboltMageCast`（光柱 `onComplete`）补完的话更晚。**所以"爆闪还在、光柱已经消失"最多可以差 0.133s** —— 你截图里看到的就是这个错位：上排的爆闪是上一发（甚至上两发）的残影，光柱是这一发的。

### 17.3 结论二：上排那两个敌人**本来就不该受伤**（玩法口径）

`HeroCombatState.ResolveRuneboltAttack`（:721-760）：
```
direction = 朝 SelectFrontmostInRange 选出的"最前"敌人
length = PierceLength = 5      （5 格）
width  = PierceWidth  = 0.35   （走廊宽 0.35 格）
IsInsideLine: forward ∈ [0, 5] 且 lateral ≤ width*0.5 = 0.175 格（≈ ±10px）
maxTargets = MaxTargetsByLevel[0] = 4
```
即**一条 5 格长、0.35 格宽的细走廊**，方向朝"最前"敌人。实测：上排右敌相对"朝中间行敌人"的走廊，侧向偏 **0.78 格**（远大于 0.175）；同一排里另外三个敌人侧向偏 0.3~0.5 格 → **都没受伤**。

所以"光柱没有挨住上排那个敌人"在玩法上是正确的，只有"爆闪"会误导你以为它被打了。玩家视觉上认为"这一排都在线上"，但规则说不在 —— 这就是需求与规则的冲突点。

### 17.4 结论三：三套美术资源的播放速度是错的（这是"没有动态"的真凶）

代码永远用 `clip.length` 算时长，而 **`AnimatorState.m_Speed` 不在 `clip.length` 里**（`Animator` 实际播放 = `animator.speed × state.m_Speed`，`animator.speed` 才是代码里那个 1）。实测三套：

| 资源 | clip 时长 / 帧数 | controller `m_Speed` | 代码给的寿命 | 实际播到 |
|---|---|---|---|---|
| `RM nor Projectile` | 0.100s / 6 帧 | **0.1** | `clipLength+hold+fade` = 0.30s | 只走 0.03s → **帧 1~2**（帧1 覆盖仅 56%、左空 39%） |
| `RM nor Impact` | 0.133s / 8 帧 | **0.5** | `= clip.length` = 0.133s | 只走 0.066s → **帧 ~4（实心盘）**，8 帧永远播不完 |
| `RM nor Muzzle` | 0.133s / 8 帧 | 1 | 代码里**没有任何引用** | 未使用 |
| clip 的 `m_LoopTime` | 三套全部 = **1** | —— | —— | Projectile 在 hold 期间会**循环回帧 1**（最空的那帧） |

这解释了你看到的两件事：
1. **没有动态**：你把 `animator.speed` 设成 0 定格在帧 2 之后，画面当然不动（这是上一轮方案 A 的代价）；但即使不定格，因为 state speed = 0.1，0.3s 的寿命里动画也走不到 1/3，看起来就是"卡着不动"。
2. **细/短**：一生停在帧 1（左空 39%）～帧 2，可见笔画 = box × 覆盖率；两端留白再叠上 box 本身，就永远"差一截"。

### 17.5 结论四：方向/长度已经对，但方向取错了参考物

现在 `direction = 最远受伤敌人 - 攻击者`。而玩法走廊的方向是 **朝"最前"敌人**。当最远那个受伤敌人偏在走廊边缘时，光柱的轴会相对真正的走廊转一点角度 —— 于是**近处的受伤敌人反而落在光柱外**（表现上"穿过"就不成立）。

正确做法：**方向取走廊方向（= 英雄当前目标方向，和 `FaceAttackerForCombatEvent` 同一个来源），长度取这批最远受伤敌人的距离**。两者一起才能既"穿过中间"又"咬住最远"。

### 17.6 修复清单（三层，缺一不可）

**A. 资源层（必做，两行文本）**
| 文件 | 改动 | 作用 |
|---|---|---|
| `RM nor Projectile.controller` | `m_Speed: 0.1` → `1` | 6 帧在 0.1s 内正常播完，动画活了 |
| `RM nor Projectile.anim`（clip 设置） | `m_LoopTime: 1` → `0` | hold 期间停在末帧，而不是循环回"左空 39%"的帧 1 |
| `RM nor Impact.controller` | `m_Speed: 0.5` → `1` | 8 帧爆闪在 0.133s 内完整播完（不再是实心盘定格） |
| `RM nor Impact.anim`（可选） | `m_LoopTime: 1` → `0` | 停在末帧淡出，避免循环闪烁 |

**B. 代码层**
- 不再定格：`animator.speed = 1f; animator.Play(0, 0, 1f/6f);`（从帧 2 起播 → 全程覆盖率 ≥85%，永不回到 56% 的帧 1）；
- `RuneboltMagePathVisualPaddingRatio` 回到 **0.9**（末帧右留白 9.3% 由它补掉）；
- 起点偏移 **24**（保留）；
- 方向改用走廊方向（见 17.5），长度仍用 `Vector3.Distance(最远受伤敌人, start)`；
- 爆闪 `SpawnRuneboltMageImpact` 的 `duration` 改成 `clip.length / stateSpeed`（否则改完 `m_Speed` 后爆闪会被提前销毁）—— 或者干脆把 duration 乘 2。

**C. 玩法层（要不要做取决于需求取舍）**
- 若要求"玩家看到的整排敌人都被线穿过"：把 `PierceWidth` 从 **0.35** 放宽（同排相邻两敌侧向差 0.4~0.8 格，要覆盖整排需要 ≈1.6~2.0 格宽，等于"横扫一排"）。
- 若保持 0.35 的"细线穿透"手感：那就接受"光柱只到受伤的最远者"，并把爆闪做成更强的"命中锚点"，让玩家读作"打中了"而不是"没到"。
- 折中：`PierceWidth` 0.35 → **0.6**，能额外吃到侧向 0.3 格内的近邻（同一格半内的敌人），视觉上更像"擦着穿过"。

### 17.7 一次性判定日志（确认 17.2）

在 `SpawnRuneboltMageBolt` 的 `travelDistance` 之后临时加：

```csharp
            var attackerId = pending.Shots[0].CombatEvent.AttackerRuntimeId;
            var corridorTarget = string.Empty;
            if (board != null &&
                board.TryGetHeroCurrentTargetRuntimeId(attackerId, out var primaryId))
            {
                corridorTarget = primaryId;
            }
            Debug.Log($"[RB] frame={Time.frameCount} shots={pending.Shots.Count} " +
                      $"corridorTarget={corridorTarget} travel={travelDistance:0.#} " +
                      $"visual={visualLength:0.#} toFarthest=" +
                      $"{Vector3.Distance(farthestShot.TargetPosition, start):0.#}");
            foreach (var shot in pending.Shots)
            {
                Debug.Log($"[RB]   id={shot.CombatEvent.TargetRuntimeId} " +
                          $"dmg={shot.CombatEvent.Damage:0.#} pos={shot.TargetPosition} " +
                          $"proj={Vector3.Dot(shot.TargetPosition - start, direction):0.#} " +
                          $"lateral={Mathf.Abs(Vector3.Cross(direction, shot.TargetPosition - start).magnitude):0.#} " +
                          $"progress={shot.PathProgress:0.##}");
            }
```

（`attackerId` 直接从 `pending.Shots[0].CombatEvent.AttackerRuntimeId` 取，不用给 `PendingRuneboltMageCast` 加字段。）

判读：
- 每次攻击只打出 1 行 `[RB]`，且 `shots` 里**只有同排/同轴上的敌人**（lateral ≤ 10）→ 证实 17.3：上排敌人的爆闪属于更早的批次；
- 若出现 `shots=1` 但画面上有多个爆闪 → 证实 17.2 的"残影错位"；
- `lateral > 10` 的 shot 还能出现在日志里 → 走廊判定与表现不一致，需要按 17.5 统一方向。

---

---

## 18. 具体修改步骤（整合版：资源层 + 代码层 + 玩法层，2026-09-24）

目标：① 恢复光柱/爆闪的**播放动画**；② 让光柱**末端恒落在最远受伤敌人身上**；③ 让"中间穿过受伤敌人"在玩法上真的成立。

### 18.0 先核对当前状态（我刚实测过，别跳过）

| 对象 | 位置 | 现值 | 本轮是否要改 |
|---|---|---|---|
| `RM nor Projectile.controller` | `m_Speed` | **1** | ❌ 已改好（A2 已做） |
| `RM nor Projectile.anim` | `m_LoopTime` | **0** | ❌ 已改好 |
| `RM nor Impact.controller` | `m_Speed` | **0.5** | ✅ 改动 1 |
| `RM nor Impact.anim` | `m_LoopTime` | **1** | ⭕ 可选 |
| `CombatFxView.cs:985` | `animator.speed = 0f;`（定格） | 仍在 | ✅ 改动 2（删除） |
| `CombatFxView.cs:108` | `RuneboltMagePathVisualPaddingRatio` | **0.96** | ✅ 改动 3 → 0.9 |
| `CombatFxView.cs:911` | 起点偏移 | **24f** | ⭕ 改动 4 → 8f |
| `CombatFxView.cs:1072` | Impact 寿命 | `clip.length` | ✅ 改动 5 |
| `FrozenHeroConfiguration.cs:615` | `PierceWidth` | **0.35** | ✅ 改动 6（关键） |
| V2 场景 `Greybox_Main.unity` | `runeboltMageReleaseFallbackDelay` | **0.16**（14108/41214） | ❌ 已改 |
| V2 场景 | `runeboltMagePathHoldDuration` | **0.12**（14105/41211） | ❌ 已改 |

> 结论：上一轮"没有动态"的直接原因是 `:985` 的定格仍在（资源层你已经改好了）；"到不了最远敌人"的直接原因是**玩法侧走廊只有 0.35 格宽，本来就没打到更远的敌人**（改动 6）。

---

### 改动 1（资源层，1 行）＋—— Impact 播放速度

文件：`Assets/DragonBound/UI/Variants/V2/Content/Resources/Animations/Hero/Runebolt Mage/normal/VFX/RM nor Impact.controller`

```yaml
  m_Speed: 0.5      ← 改成
  m_Speed: 1
```

（可选）同目录 `RM nor Impact.anim`：`m_LoopTime: 1` → `m_LoopTime: 0`。
不改也行——`duration = clip.length` 正好在循环点销毁，视觉差异极小。

> 不改这行的后果：clip 0.133s / state 0.5 = 实际播放 0.266s，而代码给的寿命是 0.133s → 爆闪**一辈子只播到一半**，看起来就是"一个不动的青色实心盘"（这正是截图里那个"老态爆闪"）。

---

### 改动 2（代码层，核心）—— 删除定格，恢复动画

`CombatFxView.cs:979-985`，把这一整段：

```csharp
            animator.speed = 1f;

            animator.Play(0, 0, 1f / 6f);

            animator.Update(0f);

            animator.speed = 0f;          // ← 停在帧 2，不再推进
```

改成：

```csharp
            animator.speed = 1f;
            animator.Play(0, 0, 1f / 6f);   // 从帧 2 起播（覆盖 92%）
            animator.Update(0f);
```

**只删最后一行**即可。现在 `m_Speed=1`、`m_LoopTime=0`，光柱会在 0.0833s 内从帧 2 播到帧 6，然后停在帧 6 等 hold + fade。

起播归一化时间 → 帧映射（clip 6 帧 / 0.1s）：

| normalized | 帧 | 横向覆盖率 | 左留白 | 右留白 | 观感 |
|---|---|---|---|---|---|
| 0 | 1 | **56%** | **39%** | 5% | 远端先出现一截短光（"彗星头"） |
| **0.1667** | **2** | **92%** | 4% | 4% | **满幅（推荐起播点）** |
| 0.3333 | 3 | 90% | 5% | 5% | 满幅 |
| 0.5 | 4 | 88% | 4% | 8% | 尾部开始收 |
| 0.6667 | 5 | 86% | 7% | 7% | 消散中 |
| 0.8333 | 6 | **85%** | **7%** | **8%** | 停住（hold 期间看到的最终形态） |

想要"射出感"可以把 `1f / 6f` 改成 `0f`（从帧 1 起播）——代价是最初一帧（17ms）光带只覆盖 56% 且偏在远端，会有一闪的错位感。**建议先按 0.1667 试。**

---

### 改动 3（代码层）—— padding 回到 0.9

`CombatFxView.cs:108`：

```csharp
        private const float RuneboltMagePathVisualPaddingRatio = 0.9f;   // 原 0.96
```

推导（改动 2 生效后，画面最终停在**帧 6**）：

- 帧 6 内容 x[11..137] / 150 → 可见段占 box 的 **[7.3%, 91.3%]**；
- 令末端 = 到最远目标的真实距离 `D`：`0.913 × L = D` → `L = 1.095 D`；
- 取 `padding = 0.9` → `L = 1.111 D` → 末端落在 **1.014 D**（超出 1.4%，正好盖住敌人而不是擦边），起点落在 **0.081 D**。

> `0.96` 是给"定格在帧 2（覆盖 92%、右留白 4%）"算的，解除定格后就不适用了。

---

### 改动 4（可选）—— 收掉英雄侧的空档

`CombatFxView.cs:911`：

```csharp
            var start = pending.AttackerPosition + (direction * 8f);   // 原 24f
```

可见段起点本身就在 box 的 7.3% 处，再叠加 24 单位偏移 → 英雄侧总空档 `0.081D + 24`。以 D=234（2.13 格）算约 43 单位 ≈ **0.39 格**，肉眼很明显。改 8f 后空档降到 ≈0.20 格。

---

### 改动 5（代码层）—— 爆闪寿命与真实播放速度对齐

`CombatFxView.cs:111` 后面新增一个字段：

```csharp
        [SerializeField, Min(0.01f)] private float runeboltMageImpactStateSpeed = 1f;
```

`CombatFxView.cs:1072-1075` 改成：

```csharp
            var duration = runeboltMageNormalImpactController.animationClips.Length > 0
                ? runeboltMageNormalImpactController.animationClips[0].length
                : 0.15f;
            duration = Mathf.Max(0.01f, duration / Mathf.Max(0.01f, runeboltMageImpactStateSpeed));
```

- 做了改动 1（`m_Speed=1`）→ 字段保持 `1f`，`duration = 0.133s`，正好播完。
- 不做改动 1 → 字段填 `0.5`，`duration = 0.266s`，也能播完。
- 场景里搜 `t:CombatFxView` 把 **Runebolt Mage Impact State Speed** 设成与 controller 的 `m_Speed` 一致即可（新字段默认 1f，V1 因为根本没这个资源，走不到这段代码）。

---

### 改动 6（玩法层，本轮的**关键**）—— 加宽穿透走廊

`Assets/DragonBound/Runtime/Recruitment/FrozenHeroConfiguration.cs:615`：

```csharp
                    new Dictionary<string, float> { { "PierceLength", 5f }, { "PierceWidth", 0.35f } },
```
改成
```csharp
                    new Dictionary<string, float> { { "PierceLength", 5f }, { "PierceWidth", 1.2f } },
```

`PierceWidth` 是**走廊总宽**（`IsInsideLine` 里判定 `lateral <= width * 0.5f`，即半宽 = width/2，单位=格）。1 格 = 110 单位。

| 取值 | 半宽 | 能收进来什么 |
|---|---|---|
| 0.35（现状） | ±0.175 格 ≈ ±19 单位 | 只收几乎严格共线的敌人，**实测常常只有 1 个** |
| 0.6 | ±0.3 格 | 同排紧邻的能进 |
| **1.0 ~ 1.2（推荐）** | ±0.5 ~ 0.6 格 | **整排都能进**，仍不会误伤隔壁排 |
| 2.0 | ±1.0 格 | 上下三排全收，等于横扫一片，伤害会明显超模 |

为什么这是"到不了最远敌人"的主因：`ResolveRuneboltAttack`（`HeroCombatState.cs:722-760`）先取 `frontmost` 敌人定方向，再用 `IsInsideLine` 过滤；走廊只有 0.35 格宽时，**更远的敌人只要偏离轴线一点点就不算命中**，于是 `Shots` 里只有最近那一两个 → 光柱末端自然只到 2 格左右（截图实测 2.13 格）。

配套：`MaxTargetsByLevel`（:616）已是 `{4,5,6}`，加宽后不会被截断；`SelectLineTargets` 按 frontmost 排序后截断，够用。

不改这个文件也能改：在同文件 `:737-738` 的 fallback 是 `skill.Length/skill.Width`，但字典优先级更高（`GetAttackParameter` 先查 `definition.AttackParameters`），所以**改字典即可**。

---

### 改动 7（可选）—— 代码默认值对齐场景

`CombatFxView.cs:113`：

```csharp
        [SerializeField, Min(0.1f)] private float runeboltMageReleaseFallbackDelay = 0.16f;   // 原 0.38
```

V2 场景已是 0.16，这条只影响新建场景/新 prefab 实例，属于收尾项。

---

### 改动 8（可选）—— 让光柱至少画满 N 格（视觉下限）

如果加宽走廊后偶尔仍只有 1 个目标、你又希望"线路感"更强，可以加一个长度下限。`:108` 后新增：

```csharp
        private const float RuneboltMageMinimumPathLength = 330f;   // 3 格
```

`:919-925` 改成：

```csharp
            var travelDistance = Mathf.Clamp(
                Mathf.Max(
                    Vector3.Distance(farthestShot.TargetPosition, start),
                    RuneboltMageMinimumPathLength),
                1f,
                RuneboltMageMaximumPathLength);
```

代价：末端会**越过**最后一名受伤者；且 `ConfigurePath` 用 `travelDistance` 归一化，光柱变长后爆闪会比光柱末端"早到"。只在确认玩法宽度已经调好之后再用它做视觉兜底。

---

### 18.9 验证（改完打一次日志）

在 `travelDistance` 算完之后（`:933` 附近）临时加：

```csharp
            Debug.Log($"[RB] shots={pending.Shots.Count} travel={travelDistance:0.#} " +
                      $"visual={visualLength:0.#} start={start}");
            foreach (var shot in pending.Shots)
            {
                var rel = shot.TargetPosition - pending.AttackerPosition;
                Debug.Log($"[RB]   id={shot.CombatEvent.TargetRuntimeId} " +
                          $"dist={rel.magnitude:0.#} pos={shot.TargetPosition}");
            }
```

判读：

- `shots` 从 1 变成 2~4 → 改动 6 生效（走廊真的收进更多敌人）。
- `travel` 与最远那条 `dist` 基本相等（差 ≤ 起点偏移 + 25）→ 改动 1/2/3 生效。
- 屏幕上光柱末端应该正好压在最远那个有爆闪的敌人身上。
- 若某敌人身上有爆闪但不在 `shots` 列表里 → 那是**别的英雄 / 上一发**留下的，不是这一发漏打。

动画是否真的在播：Play 模式下选中运行时生成的 `Runebolt Mage Path` 对象，看 Animator 窗口的进度条是否在 0.0167→0.0833 之间移动（不应停在 0.1667 不动）。

### 18.10 影响面

- **5 格穿透不变**：`RuneboltMageMaximumPathLength`(550) 的 clamp 与 `ConfigurePath` 都作用在**未补偿**的 `travelDistance` 上；改动 3 只改 `sizeDelta.x`。
- **只影响 Runebolt Mage**：`PierceWidth` 写在它自己的 `AttackParameters` 字典里，`LeviathanHunter` 用的是 `0.40`（:653，独立字典），不受影响。
- **V1 不受影响**：`RM nor Impact` / `RM nor Projectile` 的 V2 资源键在 V1 注册表里不存在，`ResolveRuneboltMagePathController` 与 `SpawnRuneboltMageImpact` 都会静默返回。
- **平衡影响**：改动 6 会让 Runebolt Mage 的 DPS 明显上升（从"单体"变成"一排"）。如果只想视觉上穿过、不想加伤害，可以反过来做：保持 `PierceWidth=0.35`，只用改动 8 把光柱画到 3~5 格（末端会越过目标，但判定不变）。
