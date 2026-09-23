# AI 侧部署节奏优化方案

> 目标：解决「AI 部署间隔太短、连发像机关枪、看起来很假」的观感问题。
> 本方案仅设计，未落地。所有行号以 2026-09-23 的 `codex/ui-v1-v2-isolation` 分支现状为准。

---

## 一、现状：AI 一步到底有多快

时间参数链（已核实）：

| 环节 | 位置 | 值 |
|---|---|---|
| 决策周期发起间隔 | `AiStrategyProfile.cs:49-55` | Beginner 1.2s / Veteran 0.7s / Elite 0.4s / Master 0.2s |
| 决策间隔随机抖动 | `AiStrategyProfile.cs:123-129` `ScheduleNext()` | ×[0.85, 1.15] |
| **单个 step 冷却** | `DragonBoundBootstrap.cs:40` + V1/V2 `Greybox_Main.unity` | **`aiBoardActionIntervalSeconds = 0.2`** |
| step 驱动 | `DragonBoundBootstrap.cs:1087-1097` | `aiBoardActionRemaining` 倒计时，到点推进一步 |
| 部署飞行时长 | `GreyboxBoardView.cs:26` | `DeploymentFlightDuration = 0.20f` |
| 落地反馈 | `GreyboxBoardView.cs:29`（已按方向 C 改造） | `LandingPressPixels = 3f`，单帧下压 ≈0.017s |
| **单个部署的实际演出** | 飞行 + 1 帧 press | **≈0.217s** |

> 注：`LandingSettleDuration = 0.06f`（`:30`）现在**已不参与部署动画本体**，仅被 `PlayRecruitDropStagger`（`:1347` `settleTime = perCardDurationSeconds + LandingSettleDuration`）用来计算招募错峰时每张卡的可见窗口。旧的 squash/rebound/settle 三段式按「方向 C」已拆除，改为单帧位移。

默认 `localPlayerRankLevel = 1` → `AiRankProfileMapping` → **Beginner**，决策周期每 1.02~1.38s 发起一次（`DragonBoundBootstrap.cs:1058-1068`）。

一个典型决策周期内的步骤序列（`BasicUnitAiController.cs:335-383` 状态机）：
`InitialShovel → PreRecruitMaintenance → Recruit → PostRecruitMaintenance → Maintain（Deploy / Merge / FormHero 若干步）`

测试 `AiSurvivalControllerTests.cs:67-80` 里 `BeginOpeningSequence(1, 3)` 就产出 Recruit ×1 + DeployBasicUnit ×3，即**一个周期内约 4~6 个 step**，全部以 **0.2s 等间隔**推进。

---

## 二、为什么会「假」：五个根因

### R1 — step 冷却（0.20s）≈ 单步演出（0.217s）：**零留白**

step 0.20s vs 演出 0.217s → 上一步的落地 press 帧刚过（超出 1 帧 ≈0.017s），下一步**立刻**起飞。动作与动作之间没有任何空隙：单位 A 落地的一瞬间单位 B 就出发了。一个周期连做 4~6 步，就是一条**完全没有节拍点的流水线** —— 人眼找不到「这一次动作」的边界，无法把它读成一次一次的决策，只能读成机器在刷。

> 注：方向 C 把旧的 squash/rebound/settle 三段式拆除后，演出从 0.59s 压到 0.217s。这解决了「落地回弹」问题，但副作用是让 0.20s 的 step 间隔变得**刚好严丝合缝** —— 这是时序紧凑化的次生效应，需要在节奏层（而非动画层）解决。

### R2 — `transitionSeconds` 参数被丢弃（明确的遗留 bug）

`DragonBoundBootstrap.cs:1095-1097` 的意图很清楚：**让本次动画时长略短于 step 间隔（0.85×），从而在下一步开始前播完**。

```csharp
var interval = Mathf.Max(0.05f, aiBoardActionIntervalSeconds);
AiBoardView?.RefreshUnits(action, interval * 0.85f);   // ← 意图：0.17s 动画 vs 0.2s 间隔
aiBoardActionRemaining = interval;
```

但 `GreyboxBoardView.cs:699-701` 第一行就把参数扔了：

```csharp
public void RefreshUnits(AiBoardAction action, float transitionSeconds)
{
    _ = transitionSeconds;                              // ← 丢弃
    ...
    PlayDeploymentFlight(..., DeploymentArcHeightInCells);  // 恒用 0.20s
```

结果：**精心设计的错峰从未生效过**。而且即便生效，0.85× 的 0.17s 也压不到能让下一步开始前留白的位置（0.20s 间隔里占 0.17s，仍只剩 0.03s ≈ 2 帧）。所以只修这个 bug 还不够，但它是必做项 —— 不修的话后面所有节奏参数都传不进演出层。

### R3 — 所有动作一刀切同速，与各自演出量严重不匹配

| `AiBoardActionType` | 实际演出量 | 现给的时间 | 匹配度 |
|---|---|---|---|
| `Recruit` | 几乎无（列表刷新） | 0.20s | 过慢（白等） |
| `MoveComponent` | 飞行 0.217s | 0.20s | 严丝合缝 |
| `DeployBasicUnit` | 飞行 0.217s | 0.20s | 严丝合缝 |
| `MergeBasicUnit` | 0.217s + 升星 fx 起头 | 0.20s | 略挤 |
| `FormHero` | 合成演出 + 英雄卡出现（最重） | 0.20s | 严重不足 |
| **`UseForgePick`** | **挖掘 0.55s + 擦除 0.4s（0.07s 起）→ 实际感受 ≈0.62s** | **0.20s** | **严重穿帮** |

最后一行是**上一轮刚加的功能**：AI 用铲子解锁格子时走 `AiShovelUnlocks` → `AiBoardView.HandleShovelUsed` 播完整动画（0.62s），但 0.20s 后下一个 step 就来了 —— 动画会被后续 step 直接骑在头上播完，是当前唯一一处**真·重叠**（而非零留白）。

### R4 — 等间隔匀速推进，没有「决策 → 行动 → 消化」的节奏曲线

真实对手是「想一下 → 连做几步 → 停一下」，机器是恒定 0.2s 节拍器。这正是「假」的本质。

### R5 — AI 由定时器驱动，缺少人手操作的轻重重音

玩家部署是人手速驱动，天然有快慢停走；AI 只有一条不变的周期，对比之下机械感被放大。

---

## 三、优化方案

分四层，**建议全部做 P0+P1，P2 视强度评估结果决定**。

### P0 — 修 bug（必做，零平衡影响）

**文件**：`Runtime/Presentation/GreyboxBoardView.cs`

把 `transitionSeconds` 真正用起来：

- `RefreshUnits(AiBoardAction, float)` → 存到字段 `pendingAiTransitionSeconds`
- `AnimateDeploymentFlight(...)` 增加 `duration` 参数，`DeploymentFlightDuration` 仅作兜底默认值
- 注意 Presse 帧是**额外的 1 帧**（`yield return null`，`:1210`），建议 `flight = max(0.05f, transitionSeconds - 0.02f)` 给它留位置，保证「总演出 ≤ step 间隔」

> 补充：`AnimateDeploymentScale`（`:1216`）在方向 C 之后已无调用方，本次不需要动它，保留即可。

这一层不动任何节奏数值，纯粹让既有意图生效。

### P1 — 按动作类型的节奏表 + 拟人抖动（推荐主体）

**文件**：新增 `Runtime/AI/AiActionPacing.cs`，`DragonBoundBootstrap.ProcessAiStepwiseSequence` 改用之

```csharp
public static float StepInterval(AiBoardAction action, IRunRandom random, int ordinal)
{
    var base = action.Type switch
    {
        AiBoardActionType.Recruit         => 0.22f,   // 无演出，别拖
        AiBoardActionType.MoveComponent   => 0.34f,
        AiBoardActionType.DeployBasicUnit => 0.42f,   // 演出 0.217 + 留白 0.20
        AiBoardActionType.MergeBasicUnit  => 0.50f,   // 等升星 fx 起头
        AiBoardActionType.FormHero        => 0.70f,   // 合成高光要被看见
        AiBoardActionType.UseForgePick    => 0.80f,   // 挖掘 0.55 + 擦除
        _                                 => 0.25f
    };
    return base * (0.85f + random.NextUnit($"ai.step.{ordinal}") * 0.30f);  // ×[0.85,1.15]
}
```

配套改动：
- `aiBoardActionIntervalSeconds` 语义从「所有 step 统一间隔」变为「**最小下限**」（`Mathf.Max(0.05f, ...)` 逻辑保留），实际间隔由节奏表决定
- 序列 seed 走现有 `aiDecisionSeed` 的 `RunRandom`，保证回放/诊断可复现（`AiSurvivalDiagnostics` 依赖确定性）
- `GreyboxBoardView.RefreshUnits(action, duration * 0.9f)` 让 P0 修好后真正错峰

**效果**：同一次 3 连部署不再等速机关枪，招募快、部署稳、合成慢，有轻重缓解。

### P2 — 逻辑与演出解耦（唯一不伤平衡的做法，推荐但需评估）

**核心思路**：AI 逻辑推进速度**完全不变**（AI 强度 100% 保持），只把「视图播放」拉成串行队列。

**现状**的问题根源是：逻辑提交的瞬间就调用了 `RefreshUnits`，所以「逻辑节奏」被迫等于「演出节奏」。P1 通过拉长逻辑间隔来给演出腾时间，**必然拖慢 AI 成型速度**；P2 则彻底拆开两者。

设计：

```
TryExecuteStepwiseCycleStep()  // 逻辑照旧 0.2s 一步，立即提交到 board
        ↓ 不直接刷新视图
AiActionPresentationQueue.Enqueue(action)
        ↓ 每帧 Tick：当前演出播到 overlap 阈值后，取下一个
AiBoardView.RefreshUnits(action, duration)   // 真正播放
```

- 每个 action 有自己的 `PresentationSeconds`（同 P1 表）
- 允许 overlap 0.2~0.3（前演出播到 70~80% 时下一个开始），像人手连贯操作而非 queue 串行

**⚠️ P2 的主要技术难点（必须处理）**

`board.Changed` 事件在逻辑提交瞬间就触发 `HandleBoardChanged` → 无参 `RefreshUnits()` / `RefreshCellStates()`，单位会**先直接出现在目标格**，随后 ghost 才起飞 → 「先到位再飞行」的倒闪。

解决办法：AI 侧视图增加 `suppressBoardRefreshForPresentation` 标志——队列非空时，`HandleBoardChanged` 跳过全量刷新（格子态仍即时更新，只押后**单位列表**刷新），由队列播放时统一驱动。

参考 `GreyboxBoardView.cs:829-841` 现有的 `deploymentAnimations` / `pendingHiddenFlightIds` 保护机制，这套机制本来就是为「视图滞后于逻辑」设计的，P2 是把它推广到整个 step 序列。

**代价**：AI 视图状态落后逻辑 ≈0.5~1s。AI 棋盘是观赏侧，`GreyboxRunStatistics` / wave 结算读的都是 board（逻辑），战斗暂停已有 `SetDeploymentAnimationCombatSuspended` 机制兜底，**功能上安全**。

### P3 — 拟人化微节奏（可选，锦上添花）

1. **起手延迟**：周期开始先停 0.15~0.5s（profile 越强越短，Beginner 0.5 / Master 0.15），视觉上就是「对手在想」
2. **同类连招加速**：第 2、3 个连续 Deploy 间隔 ×0.85 / ×0.72（人熟练后手速变快），第 4 个起回归 1.0
3. **高光留白**：`FormHero` 前后各留 0.2s 停顿，让合成演出被真正看到

---

## 四、数值建议汇总

| 动作 | 现状 | P1 建议间隔 | 相对倍数 |
|---|---|---|---|
| Recruit | 0.20 | 0.22 | ×1.1 |
| MoveComponent | 0.20 | 0.34 | ×1.7 |
| DeployBasicUnit | 0.20 | 0.42 | ×2.1 |
| MergeBasicUnit | 0.20 | 0.50 | ×2.5 |
| FormHero | 0.20 | 0.70 | ×3.5 |
| UseForgePick | 0.20 | 0.80 | ×4.0 |

---

## 五、占空比与强度补偿（P1 必须评估）

引入**占空比**概念：`单个决策周期的演出总时长 / 决策间隔`。舒适区间约 **0.70~0.85**（有留白但不拖沓）。

以 Beginner 为例（决策间隔 1.2s，典型周期 = Recruit + 3 Deploy + 1 Merge ≈ 5 步）：

| 方案 | 周期总时长 | 占空比 | AI 强度 |
|---|---|---|---|
| 现状 | 5 × 0.20 = **1.00s** | 83% | 基准 |
| P1（不动决策间隔） | 0.22+0.42×3+0.50 = **1.98s** | **165%** | ⚠️ 变成「永动」，且感知超载 |
| P1 + 决策间隔同步提到 2.4s | 1.98s | 83% | ⚠️ **每波动作数减半 → 明显变弱** |

**结论**：P1 单独使用必然改变 AI 强度，需要二选一补偿：

- **保守派**：P1 + 仅轻拉 decision interval（如 Beginner 1.2 → 1.6s），接受 AI 略弱，适合 Beginner/Veteran 档
- **推荐派**：上 **P2**，逻辑密度零变化，只改演出，理论上完全不伤平衡 —— 这是唯一干净的解法

> 波次是定长的（`TwentyWavePressureRuntime.WaveDuration`），所以「动作更慢」=「单位时间做的事更少」= 变弱，这一点无法靠缩放决策间隔绕开，只能靠 P2 解耦绕开。

---

## 六、建议落地顺序

| 阶段 | 内容 | 改动量 | 风险 | 是否影响平衡 |
|---|---|---|---|---|
| ① | P0：让 `transitionSeconds` 生效 | ~10 行 | 无 | 否 |
| ② | P1：动作类型节奏表 + 抖动 | ~60 行（新文件 1 + Bootstrap 改 1 处） | 低 | **是**，需评估补偿 |
| ③ | 跑 `AiSurvivalControllerTests` + 实机看一波 | — | — | 判定是否升级到 P2 |
| ④ | P2：演出队列解耦 | ~80 行 | 中（`board.Changed` 抢跑） | 否 |
| ⑤ | P3：拟人化微节奏 | ~20 行 | 低 | 否 |

**验收清单**
1. AI 一侧的视野里，单位落地后有明显间隙才出现下一个，不再首尾相接
2. 招募 → 部署 → 合成的速度差异可以被肉眼分辨
3. AI 用铲子解锁时，挖掘 + 擦除动画能完整播完不被挤掉
4. `AiSurvivalControllerTests` 三个用例全绿（它们校验的是 stepwise 语义，不该被节奏改动破坏）
5. 一波结束后对比 AI 单位成型数（对拍几次），确认强度符合预期

---

## 七、风险清单

1. **`board.Changed` 抢跑**（P2 主风险）：见第三节 P2 的技术难点
2. **确定性回放**：抖动必须走 `RunRandom` + 固定 seed，不能用 `UnityEngine.Random`，否则 `AiSurvivalDiagnostics` 与诊断回放不可复现
3. **`AiDeploymentFormationSymmetryBatch` 编辑器批量工具**：它跑的是无渲染纯逻辑 trace，节奏改动只影响 Bootstrap 的驱动层，tracé 侧不受影响 —— 但如果把间隔表写进 `BasicUnitAiController` 会影响它，所以建议放在独立的 `AiActionPacing.cs` / Bootstrap 层
4. **V1/V2 对称性**：节奏是 Runtime 共享逻辑，两个版本自动一致；但 `aiBoardActionIntervalSeconds` 在两个场景 prefab 各存一份（V1/V2 `Greybox_Main.unity`），改默认值后记得两处同步，否则会出现「V1 快 V2 慢」
5. **`min 0.05` 下限**：`[SerializeField, Min(0.05f)]` 会限制拖 Inspector 的下界，如果 P1 后这个字段语义变成「下限」，改 `[Min(0.05f)]` 不受影响；但若要把它提建议值 0.42，需要同步改 Inspector 值

---

## 八、最小修改版（M0 / M1，总计 ≤ 12 行）

### 8.0 一个被排除的根因：飞行不会互相打断

核实 `RefreshUnitsCore` `:829-841` 与 `PlayDeploymentFlight` `:1120-1145` 后确认：每个飞行动画是挂在 `EnsureDeploymentFxLayer()` 上的**独立 ghost**，被飞的单位本体通过 `SetDeploymentVisualHidden(true)` 隐藏；下一次 `RefreshUnitsCore` 又会因 `deploymentAnimations.TryGetValue(...) && HideCommittedView` 继续保持隐藏。

**所以多个单位可以同时飞，下一步不会终止上一步。** 上一节 P2 的「串行播放队列」因此不是必需品——「假」的唯一来源是**零留白 + 一刀切时长**，不需要解耦逻辑与演出。这是能做「最小修改」的前提。

### 8.1 核心公式

```
留白 = step 间隔 − 单步演出（飞行 + 1 帧按压）
现在：0.20 − 0.217 = −0.017s   ← 负值，落地与起飞同帧
```

要拿到 0.10s 留白，有两条路：① 把间隔抬 0.10；② **把飞行压短 0.10，间隔只微调**。
AI 半场距离短（bench ↔ 战场同侧），0.20s 飞行本就有冗余，**路径 ② 的平衡代价小得多**。

### 8.2 M0：只改 1 个数字（先试这个）

`DragonBoundBootstrap.cs:40`

```csharp
[SerializeField, Min(0.05f)] private float aiBoardActionIntervalSeconds = 0.2f;  →  0.30f
```

- `_ = transitionSeconds;`（`:701`）目前仍在丢弃参数，飞行锁死在 `DeploymentFlightDuration = 0.20f` —— 这个 bug 此刻反而是我们想要的行为；
- 留白 = 0.30 − 0.217 = **0.083s**（约 5 帧 @60fps，勉强可读出节拍）。

### 8.3 M1：+11 行，修 bug 并压短 AI 飞行（推荐）

**(1) `GreyboxBoardView.cs:701` —— 删丢弃，透传时长（1 行删 + 1 行加）**

```csharp
public void RefreshUnits(AiBoardAction action, float transitionSeconds)
{
    var flightSeconds = transitionSeconds > 0f ? transitionSeconds : DeploymentFlightDuration;
    ...
    PlayDeploymentFlight(runtimeId, sourceWorld, targetWorld, DeploymentArcHeightInCells, flightSeconds);
    PlayDeploymentFlight(swappedUnitId, targetWorld, sourceWorld, SwapReturnArcHeightInCells, flightSeconds);
}
```

**(2) `PlayDeploymentFlight` 增加可选时长参数（~3 行）**

签名加 `float? durationSeconds = null`，内部 `AnimateDeploymentFlight(..., durationSeconds ?? DeploymentFlightDuration)`；
`AnimateDeploymentFlight` 的 `while` 与 `elapsed / X` 中把 `DeploymentFlightDuration` 换成传入变量。

**(3) `DragonBoundBootstrap.cs:1095-1097` —— 分级间隔 + 压短飞行（~7 行）**

```csharp
var interval = GetAiActionInterval(action.Type);
AiBoardView?.RefreshUnits(action, Mathf.Min(DeploymentFlightDuration, interval * 0.55f));
aiBoardActionRemaining = interval;
```

```csharp
private float GetAiActionInterval(AiBoardActionType type)
{
    switch (type)
    {
        case AiBoardActionType.UseForgePick:    return 0.85f;  // 挖掘 0.55 + 擦除（0.07 起）≈0.62
        case AiBoardActionType.FormHero:        return 0.70f;  // 合成演出 + 英雄卡出现
        case AiBoardActionType.MergeBasicUnit:  return 0.50f;  // 飞行 + 升星 fx
        case AiBoardActionType.Recruit:         return 0.12f;  // 几乎无演出，现在白等 0.2s
        default:                                return aiBoardActionIntervalSeconds;  // Deploy/Move = 0.25
    }
}
```

配套把 `:40` 的基数 `0.2f → 0.25f`。

### 8.4 数值与平衡代价对比

| 动作 | 原间隔 | M1 间隔 | 飞行 | 留白 |
|---|---|---|---|---|
| Recruit | 0.20 | **0.12** | 无 | — |
| Deploy / Move | 0.20 | **0.25** | 0.14 | **0.11** |
| MergeBasicUnit | 0.20 | **0.50** | 0.14 | 0.36 |
| FormHero | 0.20 | **0.70** | — | 0.70 |
| UseForgePick | 0.20 | **0.85** | — | 0.85 |

一个典型周期（Recruit + Deploy×3 + Merge = 5 步）：

- 原：`5 × 0.20 = 1.00s`
- M1：`0.12 + 0.25×3 + 0.50 = 1.37s`（**+37%**）
- 若全按 M0 统一 0.30：`5 × 0.30 = 1.50s`（+50%）

→ **M1 比 M0 更慢得少、留白反而更多**，因为把时间花在了刀刃上（Recruit 加速、有演出的动作才等）。

代价仍然是：波次定长 ⇒ AI 每波动作数约减少 27%（1/1.37）。补偿三选一，均 1~2 行：
- **a（推荐）**：接受，用 `AiStrategyProfile` 难度档位吸收（相当于整体降 ~1/4 档，可用 profile 数值补回）；
- **b**：提高 `stepwiseBasicDeployLimit`，让每周期多部署一步抵消；
- **c**：缩短周期间的决策冷却（字段名待确认，改动前需先定位 `BasicUnitAiController` 的周期冷却字段）。

### 8.5 验证

- `AiSurvivalControllerTests` 应全绿（校验的是 stepwise 语义，与节奏无关）；
- 实机看：AI 每个部署之间应能明显数出「一下、一下」，铲子解锁不再被下一步骑住；
- 注意 V1/V2 的 `Greybox_Main.unity` 各自存了一份 `aiBoardActionIntervalSeconds`，改默认值后两处 Inspector 需同步，否则会出现「V1 快 V2 慢」。
