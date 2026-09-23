# AI 侧开局 3 秒后才开始部署 —— 实现方案

> 目标：AI 在「开局」后延迟 3 秒才执行第一个部署动作。
> 本文只做设计，未改任何代码。代码位置以 2026-09-23 的 `codex/ui-v1-v2-isolation` 现状为准。

---

## 一、先定义「开局」是哪一刻

`DragonBoundBootstrap.Update()`（`Bootstrap.cs:1008`）里有三个时间锚点，必须先选一个，否则 3 秒无从安放：

| 锚点 | 位置 | 含义 |
|---|---|---|
| 场景激活 / `InitializeRuntime` | `:588` 起 | 还没进战斗，Loading 面板还在 |
| `MatchState.Ready` 起 | `:799` | 停 `InitializationPromptSeconds = 1f`（`:115`），AI 被明令禁止动棋盘（`:786-796` 注释：Twenty-wave AI 首发决策必须由 Running 释放） |
| **`MatchState.Running` 起**（推荐） | `:1033` `TwentyWave.StartRun()` → `:319` `EnsureRunning()` | Loading 面板消失、玩家可操作的那一刻 |

**推荐锚点：`MatchState.Running`**。因为 Ready 那一秒玩家看到的是 Loading/准备提示，把 3 秒压在玩家真正能操作之后才符合「开局」的直觉。

两个场景均已确认 `useTwentyWavePressureRuntime: 1`、`enableAiSurvivalController: 1`，所以走的正是上表第三行的 Twenty-wave 链路。

---

## 二、现状：AI 第一次动作到底在什么时候

倒计时只在 Running 分支推进（`Bootstrap.cs:1056-1069`）：

```csharp
if (enableAiSurvivalController && Match.State == MatchState.Running)
{
    ProcessAiStepwiseSequence(Time.deltaTime);
    bool canDecide = !AiController.IsStepwiseCycleActive &&
                     AiDecisionScheduler != null &&
                     AiDecisionScheduler.Tick(Time.deltaTime, true);
    if (canDecide)
    {
        if (AiController.BeginStepwiseCycle(TwentyWave.CurrentWaveIndex))
        {
            aiBoardActionRemaining = 0f;
            ProcessAiStepwiseSequence(0f);   // 首个动作立即执行
        }
    }
}
```

`AiDecisionScheduler`（`AiStrategyProfile.cs:89-129`）的第一次 `ScheduleNext()` 在构造函数里就跑了，所以它的倒计时是一个**满的决策间隔**：

```
首次决策时刻 = DecisionIntervalSeconds × rand[0.85, 1.15]
```

| 难度 | `DecisionIntervalSeconds` | **现状首次动作（Running 后）** |
|---|---|---|
| Beginner（默认，`Bootstrap.cs:81`） | 1.2s | **1.02 – 1.38s** |
| Veteran | 0.7s | 0.60 – 0.80s |
| Elite | 0.4s | 0.34 – 0.46s |
| Master | 0.2s | **0.17 – 0.23s（几乎秒首发）** |

同期参照：**第一只敌兵在 Running 后 `StartPreparationSeconds = 4.0s` 才刷出**（`TwentyWavePressureConfiguration.cs:84`，`:179` `wave == 1 ? StartPreparationSeconds : 0f`）。

结论：现在 AI 起手比玩家还快（高难度下 0.2s 就落第一步），而且**随难度漂移**——这正是「看起来很假」的另一半来源。

---

## 三、推荐方案 A：给决策调度器一个「首决策延迟」

在 `AiDecisionScheduler` 里把**第一次** `ScheduleNext()` 换成固定延迟。后续节奏一个字节都不动。

### 改法（3 处，约 12 行）

**① `AiStrategyProfile.cs:96-101` — 构造函数收一个延迟参数**

```csharp
// 改前
public AiDecisionScheduler(AiStrategyProfile profile, int decisionSeed)
{
    this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
    random = new RunRandom(decisionSeed);
    Reset();
}

// 改后
public AiDecisionScheduler(AiStrategyProfile profile, int decisionSeed, float openingDelaySeconds = 0f)
{
    this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
    this.openingDelaySeconds = Mathf.Max(0f, openingDelaySeconds);
    random = new RunRandom(decisionSeed);
    Reset();
}
```

类里补一个字段 `private readonly float openingDelaySeconds;`（与 `intervalOrdinal` 并列，`:94`）。

**② `AiStrategyProfile.cs:123-129` — 首次用固定延迟，第二次起照旧**

```csharp
// 改前
private void ScheduleNext()
{
    float unit = random.NextUnit("ai.decision.interval." + intervalOrdinal++);
    float multiplier = 0.85f + (unit * 0.30f);
    CurrentIntervalSeconds = profile.DecisionIntervalSeconds * multiplier;
    remainingSeconds = CurrentIntervalSeconds;
}

// 改后
private void ScheduleNext()
{
    // First decision of the run can be held back by a fixed opening delay so the AI does not
    // start deploying the instant the player gains control. ordinal is still consumed here so
    // every later decision keeps drawing the same random sequence as before.
    if (intervalOrdinal == 0 && openingDelaySeconds > 0f)
    {
        intervalOrdinal++;
        CurrentIntervalSeconds = openingDelaySeconds;
        remainingSeconds = openingDelaySeconds;
        return;
    }

    float unit = random.NextUnit("ai.decision.interval." + intervalOrdinal++);
    float multiplier = 0.85f + (unit * 0.30f);
    CurrentIntervalSeconds = profile.DecisionIntervalSeconds * multiplier;
    remainingSeconds = CurrentIntervalSeconds;
}
```

> ⚠️ **`intervalOrdinal++` 不能省**。玩家竞技/恢复对局靠 seed 复现 AI 行为；如果首次分支不消耗 ordinal，后续每次决策都会错位取到上一个随机数，**回放就对不上了**。加上这一自增后，第 2 次及以后的决策仍对应 ordinal 1、2、3…，与改造前完全一致。

**③ `DragonBoundBootstrap.cs:41` 附近 — 加可调字段并传进去**

```csharp
// 新增（与 aiFlightSeconds 并列）
[SerializeField, Min(0f)] private float aiOpeningDecisionDelaySeconds = 3f;
```

```csharp
// :682 改前
AiDecisionScheduler = new AiDecisionScheduler(AiProfile, aiDecisionSeed);
// :682 改后
AiDecisionScheduler = new AiDecisionScheduler(AiProfile, aiDecisionSeed, aiOpeningDecisionDelaySeconds);
```

> 这个字段是**新增**的 `[SerializeField]`，与 `aiBoardActionIntervalSeconds` 不同：场景文件里没有它时，Unity 会取代码里的默认值 3，**不需要手动去 Inspector 改**。想让 V1/V2 用不同值才需要进场景。

### 改后效果

| | 现状 | 方案 A |
|---|---|---|
| AI 首个动作 | 1.02–1.38s（Beginner）/ 0.17–0.23s（Master） | **精确 3.000s，且不再随难度漂移** |
| 第二个决策 | +interval | 不变（3.0s 之后再 +interval） |
| 单步演出 / `GetAiActionIntervalSeconds` 分级 | — | **完全不受影响** |
| 敌兵首刷 4.0s 前 AI 有几步 | Beginner 约 3–4 个周期 | **1 个周期**（约 3–5 个 step） |

---

## 四、被否决的两个备选（供对比）

**方案 B — 在 Bootstrap 的 Update 里加倒计时闸门**

```csharp
if (aiOpeningGateRemaining > 0f) { aiOpeningGateRemaining -= Time.deltaTime; return; }
```

不推荐：`Tick` 在闸门期间不被调用，倒计时不会推进（`Tick` 第一条就是 `if (!canDecide || deltaTime <= 0f) return false;`），等闸门打开时调度器仍顶着一个满的 `DecisionIntervalSeconds`，实际首发变成 **gate + 1.2s = 4.2s**，反而越过第一只敌兵的 4.0s。要修正就得把 gate 写成「目标 − 当前难度的决策间隔」——跨难度无法用一个常量表达。

**方案 C — 把延迟塞进 `AiStrategyProfile`**

不推荐。`AiStrategyProfile` 的类注释（`:14-17`）明确写着它是 **decision-quality configuration only**，且不得影响资源/棋盘规则；加时间参数违反该契约，还会波及 `AiProfile` 的所有断言测试。

---

## 五、副作用与验证

**1. AI 客观上会变弱一点点。** 原本 Beginner 在敌兵到来前能跑完 3~4 个决策周期（每周期最多 `stepwiseBasicDeployLimit` 次部署），改后只剩 1 个周期的起步时间。但因为 **4.0s 前一只敌兵都没有**，丢的只是「抢跑」，不是实际应付第一波的产能 —— 预计对成型进度的影响远小于上一轮抬 `aiBoardActionIntervalSeconds` 的代价。真要补，调 `AiStrategyProfile.Get()` 里 Beginner 的 `DecisionIntervalSeconds`（1.2 → 1.0）即可。

**2. 暂停不消耗这 3 秒。** `Tick` 只在 `MatchState.Running` 被调用（`Bootstrap.cs:1056`），`BossPrompt` / `Paused` 期间停止推进 —— 语义正确（玩家暂停时 AI 不该偷跑）。

**3. 非 Twenty-wave 模式没被覆盖。** `Bootstrap.cs:788-796` 的 `BeginOpeningSequence` 是在 Ready 期间就启动的（`Update` 的 Ready 分支 `:1024` 也在推进）。当前两个场景都是 `useTwentyWavePressureRuntime: 1`，这条路径用不上；若将来打开，需要在 Ready 分支的 `ProcessAiStepwiseSequence` 前补同样的闸门。

**4. 验证清单**
- 打开 V1 或 V2 的 `Greybox_Main`，进入战斗后盯 AI 半场：Loading 消失起约 3 秒才出现第一次动作（飞行/合并/部署）；
- 用秒表对比：第一只敌兵 4.0s 出现时，AI 应已有 1 个卡的雏形而不是空场或一堆；
- `BootstrapPlayModeTests`（尤其 `:568` 断言 `AiDecisionScheduler != null`）跑一遍；
- 若有用三种难度，各验一次——改后三档首发现象应完全一致（都是 3.0s）。

---

## 六、可调衍生（后续想要再补）

- **每波开波也想留白**：订阅 `TwentyWave.WaveStarted`，`AiDecisionScheduler.Reset()` 时重新套一层 short delay（需要给 Reset 加参数，约 5 行）。
- **延迟后 AI 起手太慢**：把 `aiOpeningDecisionDelaySeconds` 降到 2.0–2.5s；或保留 3s 但把 Beginner 的决策间隔降到 1.0s 补偿。
- **想让玩家侧也同步（AI 与玩家同时解除封禁）**：当前 Ready 只有 1s，玩家其实早已能操作，一般不需要同步。
