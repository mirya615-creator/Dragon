# 随机流协议与 Stage 配置分析报告

> 2026-09-03 更新：本文记录的是 P0 实施前基线。Unity 客户端 P0 已落地，当前契约与黄金向量请以《客户端P0随机流与Stage契约.md》为准；服务端未作任何修改。

> 数据来源：Unity 客户端仓库 `Dragon`（2026-09-02 核查）。
> 关键结论先行：**`recruit_random`、`wave_rng` 的二进制格式与"目标数据库中的已发布 Stage"不属于本仓库**——它们定义在 Go 后端服务（本工作区无该工程，仅有 `D:\Codex project\` 下的 Unity 系列目录）。本报告给出客户端侧已实现/已核实的全部对应物，并明确标注缺口。

---

## 1. recruit_random 的二进制格式

**仓库内不存在名为 `recruit_random` 的字段或接口**（全仓库 0 命中）。它是 Go 后端 `/v1/runs/start` 协议规划的共享随机流字段，客户端对应的实现是**种子派生 + 分流**方案：

### 1.1 线上（Go 网关）种子格式 — `GoUnaryGameplayRunGateway.cs`

| 项 | 格式 |
|---|---|
| `run_seed`（服务器下发） | **十进制无符号 64 位整数字符串**（`ulong.TryParse`，`NumberStyles.None`） |
| 折叠为客户端种子 | `int seed = unchecked((int)(parsed ^ (parsed >> 32)))`（高低 32 位异或折叠，L423-428） |
| `run_nonce`（服务器下发） | **Base64URL 编码，解码后必须恰好 16 字节**（`DecodeBase64Url` L396-404，校验失败抛 `INVALID_RUN_NONCE`） |
| 子流派生 | FNV-1a(32)：`hash = FNV(stream) ^ (uint)seed`（**无末次乘法**，L430-442） |

派生出 4 条子流（L82-91）：

```
PlayerRecruitSeed = DeriveSeed(runSeed, "player.recruit")
AiRecruitSeed     = DeriveSeed(runSeed, "ai.recruit")
CombatSeed        = DeriveSeed(runSeed, "combat")
AiDecisionSeed    = DeriveSeed(runSeed, "ai.decision")
```

### 1.2 离线（本地网关）种子格式 — `LocalGameplayRunGateway.cs`（`GameplayRunGateway.cs` L328-525）

```
PlayerRecruitSeed = runSeed ^ 0x13579BDF
AiRecruitSeed     = runSeed ^ 0x2468ACE0
CombatSeed        = runSeed
AiDecisionSeed    = DeriveSeed(runSeed, "ai.decision")   // FNV-1a，但带末次乘法
```

种子来源：`RandomNumberGenerator` 4 字节 CSPRNG；诊断模式 `UseDiagnosticSeed` 直接用 `DiagnosticSeed`，RunId 固定为 `diagnostic.<seed>`（L353-356）。

> ⚠️ **线上/离线子流派生算法不一致**（Go 版 `hash ^ seed` 无末乘，本地版 `（hash ^ seed) * 16777619`）——同一个 runSeed 在两种网关下产生不同子流。跨端复现随机序列时必须二选一固定。

### 1.3 随机数发生器本体 — `Assets/GameShared/Runtime/Random/RunRandom.cs`

PCG 家族（64 位 LCG 状态 + XSH-RR 32 位输出）：

- **状态转移**：`state = state * 6364136223846793005 + 1442695040888963407`（Knuth MMIX 乘数/增量）
- **输出函数**：`xorShifted = (uint)(((prev >> 18) ^ prev) >> 27)`，`rot = prev >> 59`，输出 `rotr32(xorShifted, rot)`
- **播种**：`state = 0` → 丢弃 1 个输出 → `state += (uint)seed` → 再丢弃 1 个输出（**消耗 2 次原始输出后才进入可用状态**）
- **`NextInt(min, maxEx)`**：拒绝采样 + 模运算（`threshold = (0-range) % range`，无偏）
- **`NextUnit()`**：`(NextUIntRaw() >> 8) * (1/2^24)` —— **取高 24 位**，粒度 2^-24

招募流的消费上下文（`RecruitDeck.cs`）：`recruit.basic.config`、`recruit.guarantee.purple`、`recruit.guarantee.gold`、`recruit.guarantee.gold.exposure`、`recruit.guarantee.gold.completion`、`RecruitComponentBag.shuffle`。

**结论**：若指 Go 服务器里序列化为二进制 blob 的 `recruit_random`，需到后端仓库/OpenAPI 文档核实；客户端没有消费二进制随机包——招募完全由客户端确定性算法本地执行（`GoUnaryGameplayRunGateway.RecruitAsync` L97-104 明确注释：OpenAPI 无 Recruit 端点，共享随机流协议冻结前保留客户端算法）。

---

## 2. wave_rng 的二进制格式

**仓库内不存在 `wave_rng` 字段**（0 命中）。客户端的波次随机为**按波独立派生、按阵营分流**：

`TwentyWavePressureRuntime.cs` L550-567（`GetWaveComposition`）：

```csharp
var random = new RunRandom(DeriveCompositionSeed(runSeed, wave));  // 每波一个新实例
var stream = side == TeamSide.Player ? "EnemyComposition.Player" : "EnemyComposition.AI";
for (var index = 0; index < count; index++)
    var roll = random.NextUnit(stream + ".W" + wave + "." + index) * TotalWeight;
    // roll < NormalWeight → Normal；< Normal+Fast → Fast；否则 Elite
```

- **派生键**：`DeriveCompositionSeed(runSeed, wave)`（L891）——FNV-1a 混入 runSeed 与 wave，与其它系统隔离；
- **两侧同种子**：Player/AI 用同一 canonical seed 但不同流标签，保证压力对称且互不干扰（注释 L545-549 明示）；
- **逐敌上下文**：`EnemyComposition.Player.W<wave>.<index>`——每个敌人一格 roll，可独立复算；
- 当前生产权重 `Normal=1, Fast=0, Elite=0`（`TwentyWavePressureConfiguration.GetProductionWeights` L222-230：正式内容只有 Normal/Boss/BossSummon 三种实体，普通波全 Normal）。

同构的波级随机还有：符文掉落 `RuneDrops.cs`（`PlayerRuneReward.Chance/Rarity/Pool/EpicForm`，种子 FNV 混入 `RuneContent.V1` 算法版本）、魂链 `SoulChainBinderRuntime`（按 side 派生）。

---

## 3. critical_random_sequence 的编码方式

**当前客户端编码为空字符串**：`GoUnaryGameplayRunGateway.cs` L168，`critical_random_sequence = string.Empty`。

- 传输类型：`GoRunFinishRequest.critical_random_sequence` 为 JSON **string** 字段（`GoApiContracts.cs` L134）；
- **战斗运行时不存在暴击系统**（`Assets/DragonBound/Runtime` 全目录无 `critical` 命中）——客户端没有可上报的暴击随机序列；
- 仓库中唯一定义完整的"序列编码"是**事件哈希链**（供 `final_event_hash` 使用，`GoUnaryGameplayRunGateway.cs` L349-382）：

```
InitialHash = SHA256( "DragonBound/EventChain/v1\0" || base64url_decode(run_nonce)[16B] )
EventHash   = SHA256( "DragonBound/Event/v1\0"
                    || u16be(len(nonce)) || nonce_ascii
                    || u64be(sequence)
                    || u16be(len(type)) || type_ascii
                    || payload_digest[32B] || previous_hash[32B] )
输出：64 字符小写 hex
```

（域分隔字符串 + 大端长度前缀 + SHA-256。）
**结论**：暴击随机序列的真实编码规范（后端预期格式）需查 Go 服务端/OpenAPI；客户端处于"占位空串"状态，待暴击系统与共享随机流协议解冻后实现。

---

## 4. 随机流消费顺序（客户端全景）

```
run_seed (服务器十进制串 / 本地 CSPRNG 4B)
 │  ParseSeed 折叠 → int32 RunSeed
 ├─ FNV-1a 派生 ──► player.recruit ─► RecruitDeck（上下文见 1.3）
 │                  ai.recruit      ─► AI 侧招募（同构）
 │                  combat          ─► ItemActive/PassiveCombatEffects（直接 new RunRandom(runSeed)）
 │                  ai.decision     ─► AiStrategyProfile："ai.decision.interval.<n>"（序号递增）
 ├─ 每波派生 ──────► EnemyComposition.Player/AI ".W<波>.<敌序>"
 │                  PlayerRuneReward（波完成后 Chance→Rarity→Pool→EpicForm 顺序消费）
 ├─ 按 side 派生 ──► SoulChain.Region / SoulChain.Target
 ├─ 按版本派生 ────► LimitedComponentBag（contentVersion）
 └─ 按符文派生 ────► HeroCombatState L2077（sourceRuntimeId + runeId）
```

要点：
- **逻辑调用计数** `CallIndex` 只计 `NextInt/NextUnit`（每次 +1）；**原始输出消耗**可能更多——`NextInt` 拒绝采样会重抽原始输出但不计 CallIndex，且播种期固定消耗 2 次原始输出；
- 每条流一个独立 `RunRandom` 实例，流间零串扰（测试 `TwentyWavePressureTests` L34、`LimitedComponentBagTests` L102 专门验证过旁路消费不影响目标流）;
- 同一流内**消费顺序即代码执行顺序**（如符文：Chance 失败即停止，不消费 Rarity/Pool）。

---

## 5. Unity 固定测试向量

**不存在黄金测试向量表（golden vectors）**。现状：

| 项 | 位置 | 内容 |
|---|---|---|
| 唯一随机性测试 | `RunRandomTests.cs` | **种子 73 的自洽性测试**：两个 `RunSeed(73)` 实例连续 16 次 `NextInt("test.sequence", -10, 50)` 结果相等——只验证确定性，**没有断言任何具体期望数值** |
| 诊断种子通道 | `StartGameplayRunRequest.UseDiagnosticSeed / DiagnosticSeed` | 运行期可注入固定种子，RunId = `diagnostic.<seed>`，用于人工复现，非自动断言 |
| 大量固定种子 EditMode 测试 | RecruitDeckTests、TwentyWavePressureTests、RuneSystemV1Tests 等 | 均以固定 seed 驱动，但断言的是业务结果（分布/上限），非 RNG 原始输出向量 |

> **缺口提示**：跨端（Unity ↔ Go）要对齐随机流，必须先补一组黄金向量（建议：seed=73 与若干边界 seed，对每个上下文记录 `NextInt/NextUnit` 前 N 个期望值，双端共用）。当前仓库不具备。

---

## 6. 正式 Stage 内容和奖励配置

### 6.1 Stage 标识（客户端侧）

- 唯一 Stage 引用：`ClientServiceConfig.asset` → `defaultStageId: stage-001`（`Resources/Configuration/`）；
- `/v1/runs/start` 请求携带 `stage_id`、`client_version=0.1`、`content_version=2026081301`、`requested_config_version=2026081501`；
- Stage 内容对客户端是**服务器权威**：响应只回 `stage_snapshot_digest`（摘要），客户端不持有 Stage 全量数据。

### 6.2 客户端本地"Stage"等价物（Greybox 压力赛配置，`TwentyWavePressureConfiguration.cs`）

`CreateCoreLoopV2()`（ConfigurationId=`PressureRaceGreyboxV2`）：

- **20 波**（`BattleSettlementDefinition.MaxScheduledWave=20`，W20 后不再生成）；
- **每边敌人数**：10,11,12,13,15,16,18,19,21,23,25,27,29,31,33,35,37,39,41,43；
- **R1 生产 HP 曲线**（有效最大 HP）：25.5, 26.1, 26.7, 35, 45, 63, 95, 120, 145, 175, 205, 240, 275, 315, 360, 410, 465, 525, 590, 660；
- **Boss 波**：W6 魂链 / W12 风暴召唤者 / W16 血冠暴君 / W20 吞世龙（`hasBossSlot`，Boss 走独立日程）；
- **节奏**：首波准备 4s；刷怪间隔 1.5s；波间隔 6.5s；移速 Normal 0.60 / Fast 0.80 / Elite 0.58 格每秒；
- **结算常量**（`BattleSettlementDefinition`）：初始 3 心、普通怪破家 1 心、Boss 破家即时判负、W20 为最终波。

### 6.3 奖励配置

- **金币/符文碎片**：完全由服务器结算下发（`GoRunSettlement.base_gold / total_gold / rune_fragments / rune_drops`），客户端 `GoldReward = settlement.total_gold` 直读，无本地公式；W1-W20 玩家胜利统一由 `/finish` 结算 20 金币，正常失败和主动投降由 `/quit` 结算 10 金币；超时仍为 `ExpiredNoReward`；结算页 `DoubleBtn` 暂时禁用；
- **符文掉落规则**（客户端权威，`RuneDrops.cs` `RuneDropRules`）：
  - 资格波次 ≥3；掉率：W3-6 12% / W7-12 18% / W13-16 28% / W17+ 40%；
  - 稀有度分布按波次分档（例 W17+：普通 15% / 精良 25% / 史诗 40% / 传说 20%）；
  - 每局最多 4 次成功掉落（`MaxSuccessfulRewardsPerRun=4`）；史诗仅 25% 概率整卡、其余碎片；普通/精良必整卡；
  - 随机流 `PlayerRuneReward.*`，种子混入 `RuneContent.V1` 与算法版本，按波独立派生。

---

## 7. 目标数据库中的已发布 Stage

**无法从本仓库回答**。核查结论：

- 本仓库无数据库、无 migration、无 Stage 种子数据文件；`D:\Codex project\` 下也只有 Unity 系列工程（Dragon、Dragon L1、Game、备份目录等），**没有 Go 后端仓库**；
- 客户端可见的"已发布"痕迹仅三个间接信号：`stage-001`（defaultStageId）、`content_version=2026081301`、`config_version=2026081501`（响应还回 `stage_snapshot_digest` 用于校验 Stage 快照未变）；
- 获取真实答案需：Go 后端仓库的 Stage 表/migration，或直接查询目标数据库（按 `stage_id`、`content_version` 过滤发布态记录），或调用后端管理接口。

---

## 附：缺口清单（按优先级）

1. `critical_random_sequence` 客户端恒为空串——暴击系统未实现，协议字段占位；
2. 无黄金随机测试向量——跨端随机流对齐的前置阻塞项；
3. 线上/离线子流派生算法不一致（`hash ^ seed` vs `(hash ^ seed) * 16777619`）——同一 runSeed 两网关结果分叉；
4. Stage 内容（正式服）全部服务器权威，客户端仅 greybox 配置——本地无法验证 Stage 数值；
5. Go 后端工程不在本工作区——`recruit_random`/`wave_rng` 二进制格式与已发布 Stage 数据需后端侧核实。
