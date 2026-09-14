# DragonBound 服务端对接输入：随机流与 Stage V1

状态：客户端冻结稿  
协议版本：`DragonBound.Random.v1`  
随机算法：`pcg32-xsh-rr-v1`  
适用 Stage：`stage-001`  
客户端内容版本：`2026081301`  
客户端配置版本：`2026081501`

本文是客户端提供给服务端的实现与验收输入。随机格式、消费顺序、测试向量、Stage 内容和奖励规则由客户端确定；数据库表名、记录主键、发布时间等实际发布信息由服务端回填确认。

对接状态说明：随机信封、客户端校验和测试向量已经实现；金币与符文表是正式配合目标。当前在线客户端仍保留局内符文本地落地逻辑，服务端启用权威 `rune_drops` 前，客户端还需切换为“局内仅预览、结算后落账”，否则不得同时开启双端发奖。

## 1. 公共随机规则

### 1.1 `run_seed`

服务端通过 `/v1/runs/start` 返回十进制无符号 64 位整数字符串。Unity 按下式折叠为后续协议使用的 int32：

```text
u64 = parse_uint64_decimal(run_seed)
seed32 = int32(uint32(u64 XOR (u64 >> 32)))
```

int32 均按二进制补码解释。所有二进制整数均使用大端序。

### 1.2 子流种子派生

```text
hash = 2166136261                         // FNV-1a offset basis
for byte in UTF8(stream_name):
    hash = (hash XOR byte) * 16777619     // uint32 溢出回绕
derived_seed = int32(hash XOR uint32(seed32))
```

固定子流：

| 用途 | `stream_name` |
|---|---|
| 玩家招募 | `player.recruit` |
| AI 招募 | `ai.recruit` |
| 战斗 | `combat` |
| AI 决策 | `ai.decision` |

### 1.3 PCG32

```text
state = state * 6364136223846793005 + 1442695040888963407  // uint64 回绕
xorshifted = uint32(((previous_state >> 18) XOR previous_state) >> 27)
rotation = previous_state >> 59
output = rotr32(xorshifted, rotation)
```

播种顺序：`state=0`，丢弃一次输出，`state += uint32(seed32)`，再丢弃一次输出，然后才产生第一个业务样本。

- `NextInt(min,maxExclusive)`：使用 `threshold = uint32(0-range) % range` 做拒绝采样，再返回 `min + sample % range`。
- `NextUnit()`：`(sample >> 8) * 2^-24`，即只使用原始样本高 24 位。
- `context` 字符串只用于稳定命名和诊断，不参与当前 PCG 状态或输出计算。
- 一次 `NextInt` 可能因拒绝采样消耗多个原始 uint32，但逻辑调用计数只增加 1。

## 2. 通用二进制信封

三类载荷都先写入以下 6 字节头，然后整体编码为无 `=` 填充的 Base64URL 字符串：

| 偏移 | 长度 | 类型 | 值 |
|---:|---:|---|---|
| 0 | 4 | byte[4] | ASCII `DBRS`，十六进制 `44 42 52 53` |
| 4 | 1 | uint8 | Schema 版本 `01` |
| 5 | 1 | uint8 | `01` 招募、`02` 波次、`03` 暴击 |

服务端必须拒绝错误 Magic、未知 Schema、类型不符、长度不符及非法 Base64URL。

## 3. `recruit_random` 二进制格式

用途：`/v1/runs/start` 响应字段。总长度固定 14 字节。

| 偏移 | 长度 | 类型 | 内容 |
|---:|---:|---|---|
| 0 | 6 | header | 通用头，kind=`01` |
| 6 | 4 | int32be | `DeriveSeed(seed32,"player.recruit")` |
| 10 | 4 | int32be | `DeriveSeed(seed32,"ai.recruit")` |

seed=73：

```text
bytes:     44 42 52 53 01 01 B0 77 4A 41 27 89 15 16
Base64URL: REJSUwEBsHdKQSeJFRY
player:    -1334359487
ai:         663295254
```

客户端会重新从 `run_seed` 计算两个种子；任一不一致则以 `RUN_RANDOM_SEED_MISMATCH` 拒绝进入对局。

## 4. `wave_rng` 二进制格式

用途：`/v1/runs/start` 响应字段。总长度固定 10 字节。

| 偏移 | 长度 | 类型 | 内容 |
|---:|---:|---|---|
| 0 | 6 | header | 通用头，kind=`02` |
| 6 | 4 | int32be | 折叠后的 `seed32`，即波次根种子 |

seed=73：

```text
bytes:     44 42 52 53 01 02 00 00 00 49
Base64URL: REJSUwECAAAASQ
```

客户端要求解码值与折叠后的 `run_seed` 完全相等。

## 5. `critical_random_sequence` 编码

用途：`POST /v1/runs/{run_id}/finish` 请求字段。长度为 `10 + count*4` 字节。

| 偏移 | 长度 | 类型 | 内容 |
|---:|---:|---|---|
| 0 | 6 | header | 通用头，kind=`03` |
| 6 | 4 | uint32be | 样本数量 `count` |
| 10 | `count*4` | uint32be[] | 按暴击判定发生时间排序的 PCG32 原始样本 |

当前正式战斗尚未启用暴击判定，因此客户端发送合法空序列，而不是空字符串：

```text
bytes:     44 42 52 53 01 03 00 00 00 00
Base64URL: REJSUwEDAAAAAA
```

四样本编码示例：

```text
samples:   [0, 1, 4294967295, 305419896]
bytes:     44 42 52 53 01 03 00 00 00 04 00 00 00 00 00 00 00 01 FF FF FF FF 12 34 56 78
Base64URL: REJSUwEDAAAABAAAAAAAAAAB_____xI0Vng
```

服务端 V1 必须接受 `count=0`。未来启用暴击时，每次候选暴击判定恰好追加一个原始样本；未发生候选判定不得消费或上报样本。

## 6. 随机流消费顺序

### 6.1 Run 启动

1. 解析并折叠 `run_seed` 得到 `seed32`。
2. 派生 `player.recruit`、`ai.recruit`、`combat`、`ai.decision`。
3. 校验 `recruit_random` 和 `wave_rng`。
4. 各子系统分别创建 PCG32 实例；不同实例之间不得共享状态。

### 6.2 招募

- 玩家和 AI 使用独立招募根种子，互不推进对方状态。
- 当前正式招募策略为 V3。每次招募 `recruitmentNumber` 从 1 开始。
- V3 单次招募的业务顺序固定为：组件数量判定 → 从有限组件袋取件 → 铲子判定 → 无放回补齐基础单位 → 五张卡牌洗牌 → 成功后提交状态。
- 每个 V3 批次用途使用独立派生实例：

```text
hash = FNV1a32(runtimePrefix) continued with UTF8(streamId)
hash = (hash XOR uint32(recruitSeed)) * 16777619
hash = (hash XOR uint32(recruitmentNumber)) * 16777619
batchSeed = int32(hash)
```

`runtimePrefix` 分别为 `player`、`ai`。现有稳定上下文包括组件数量、`basic-unit`、`slot-order`、铲子流及 `RecruitSlotOrder.v1`。Fisher-Yates 洗牌从末位向前，每一位调用一次 `NextInt(0,index+1)`。

- 有限组件袋初始化仍是独立流：玩家袋使用 `seed32`，AI 袋使用 `ai.recruit` 种子；袋内容版本为 `DragonBound.HeroComponents.v1`。
- 非有限模式的稳定调用顺序为：紫色保证配方 → 两个紫色槽位 → 金色保证配方 → 金色曝光槽位 → 金色完成槽位 → 早期组件洗牌；基础单位使用 `recruit.basic.config`。

服务端若只保存/签名根种子，可不逐步持久化 PCG 状态，但复算时必须遵循上述条件分支；未命中的分支不得提前消费随机数。

### 6.3 波次敌人

每波重新创建实例，不跨波延续状态：

```text
hash = 2166136261
hash = (hash XOR uint32(waveRootSeed)) * 16777619
for char in "PressureComposition.v1": hash = (hash XOR char) * 16777619
hash = (hash XOR uint32(wave)) * 16777619
waveSeed = int32(hash)
```

玩家侧和 AI 侧分别用同一个 `waveSeed` 创建实例，各自按敌人索引 `0..count-1` 调用一次 `NextUnit`。上下文分别为：

```text
EnemyComposition.Player.W{wave}.{index}
EnemyComposition.AI.W{wave}.{index}
```

当前权重固定为 Normal=1、Fast=0、Elite=0；仍必须执行每个敌人的 roll，以保持未来权重变更后的消费位置兼容。

### 6.4 波后符文奖励

每个已完成波次重新派生独立种子。派生材料依次为：`seed32`、波次、ASCII `RuneContent.V1.RuneDrop.V1.PlayerRuneReward`，每项均按 FNV-1a 乘法回绕。

消费顺序：

1. `PlayerRuneReward.Chance`；失败立即结束。
2. `PlayerRuneReward.Rarity`。
3. `PlayerRuneReward.Pool`。
4. 仅 Epic 调用 `PlayerRuneReward.EpicForm`；其他稀有度不得消费该样本。

未解锁、波次小于 3 或本局已成功 4 次时，不创建随机实例、不消费样本。

### 6.5 其他独立流

- AI：`ai.decision.interval.{ordinal}`，ordinal 递增。
- 魂链：`SoulChain.Region` 后按需要执行 `SoulChain.Target` Fisher-Yates。
- 道具、符文战斗效果分别持有自身实例和稳定上下文，不得推进招募或波次实例。
- `critical_random_sequence` 只记录暴击专用流，不能混入上述任一业务 roll。

## 7. Unity 固定测试向量

### 7.1 子流派生

| seed32 | player.recruit | ai.recruit | combat | ai.decision |
|---:|---:|---:|---:|---:|
| 0 | -1334359544 | 663295327 | 484666913 | 1977970329 |
| 1 | -1334359543 | 663295326 | 484666912 | 1977970328 |
| 73 | -1334359487 | 663295254 | 484666984 | 1977970384 |
| -1 | 1334359543 | -663295328 | -484666914 | -1977970330 |

### 7.2 PCG32 原始 uint32

| seed32 | 前 8 个原始输出 |
|---:|---|
| 0 | 3894649422, 2055130073, 2315086854, 2925816488, 3443325253, 1644475139, 428639621, 1241310737 |
| 73 | 1536588234, 1786307495, 4138217344, 867260190, 642825941, 2092487798, 2502112947, 3283318321 |
| -1 | 1690806306, 1175666736, 601713809, 1455133790, 2659000460, 4270424894, 2374250840, 2397061969 |

### 7.3 公共 API 输出（seed=73）

```text
NextInt(-10,50), 前 8 次:
44, 25, -6, 20, 31, 28, 17, -9

NextUnit(), 前 6 次:
0.3577647805213928
0.4159070849418640
0.9635037779808044
0.20192474126815796
0.14966952800750732
0.4871952533721924
```

### 7.4 载荷向量

| seed/样本 | recruit_random | wave_rng | critical_random_sequence |
|---|---|---|---|
| seed=0 / empty | `REJSUwEBsHdKCCeJFV8` | `REJSUwECAAAAAA` | `REJSUwEDAAAAAA` |
| seed=73 / empty | `REJSUwEBsHdKQSeJFRY` | `REJSUwECAAAASQ` | `REJSUwEDAAAAAA` |
| seed=-1 / empty | `REJSUwEBT4i199h26qA` | `REJSUwEC_____w` | `REJSUwEDAAAAAA` |

服务端 CI 必须使用同一组向量，并至少覆盖非法 Magic、错误 kind、错误长度、未知版本以及 seed 不一致。

## 8. 正式 Stage 内容

客户端正式清单为 `Assets/Resources/Configuration/Stages/stage-001.json`，其规范化 JSON（首尾空白移除）的 SHA-256 为：

```text
a555903b1f717480e79dc29c4a28c8ec09bd18fc4316a6328cfc2e672b6d05eb
```

### 8.1 基础配置

| 字段 | 值 |
|---|---|
| `stage_id` | `stage-001` |
| `status` | `published` |
| `configuration_id` | `PressureRaceGreyboxV2` |
| `content_version` | `2026081301` |
| `config_version` | `2026081501` |
| 波数 | 20，W20 后不生成新波次 |
| 初始资源 | 20 |
| 招募费用 | 第 N 次成功招募为 `10 + 2*(N-1)` |
| 普通怪击杀资源 | +1 |
| 初始基地生命 | 3 |
| 普通怪进点伤害 | 1 |
| Boss 进点 | 立即失败 |
| 首波准备 | 4.0 秒 |
| 刷怪间隔 | 1.5 秒 |
| 波间尾间隔 | 6.5 秒 |
| 移速（格/秒） | Normal 0.60 / Fast 0.80 / Elite 0.58 |
| 普通波权重 | Normal 1 / Fast 0 / Elite 0 |

### 8.2 每波配置（每个战斗侧）

| 波次 | 敌人数 | 普通怪最大 HP | Boss |
|---:|---:|---:|---|
| 1 | 10 | 25.5 | - |
| 2 | 11 | 26.1 | - |
| 3 | 12 | 26.7 | - |
| 4 | 13 | 35 | - |
| 5 | 15 | 45 | - |
| 6 | 16 | 63 | `BOSS_SOULCHAIN_BINDER` |
| 7 | 18 | 95 | - |
| 8 | 19 | 120 | - |
| 9 | 21 | 145 | - |
| 10 | 23 | 175 | - |
| 11 | 25 | 205 | - |
| 12 | 27 | 240 | `BOSS_STORMCALLER_PRIEST` |
| 13 | 29 | 275 | - |
| 14 | 31 | 315 | - |
| 15 | 33 | 360 | - |
| 16 | 35 | 410 | `BOSS_BLOODCROWN_TYRANT` |
| 17 | 37 | 465 | - |
| 18 | 39 | 525 | - |
| 19 | 41 | 590 | - |
| 20 | 43 | 660 | `BOSS_WORLDEATER_WYRM` |

Boss 最后一击局内 XP 分别为 W6=6、W12=10、W16=15、W20=20；这不是账号金币或符文结算。

## 9. 正式奖励配置

### 9.1 金币结算

| 结果 | 接口顺序 | `settlement_status` | 基础金币 | 可广告翻倍 |
|---|---|---|---:|---|
| 玩家胜利（W1-W20） | `/finish` | `Settled` | 20 | 否，客户端暂时禁用 `DoubleBtn` |
| 正常失败 | `/quit` | `Settled` | 10 | 否，客户端暂时禁用 `DoubleBtn` |
| 主动投降 | `/quit` | `Settled` | 10 | 否，客户端暂时禁用 `DoubleBtn` |
| Run 过期 | - | `ExpiredNoReward` | 0 | 否 |
| 拒绝/校验失败 | 对应请求接口 | `RejectedNoReward` | 0 | 否 |
| 服务端故障/No Contest | 对应请求接口 | 非正常结算 | 0 | 否 |

服务端负责账本、幂等和最终返回：`base_gold`、`ad_multiplied`、`total_gold`。`/quit` 使用 `{run_id}:quit`，`/finish` 使用 `{run_id}:finish` 作为幂等键。W1-W20 玩家胜利的 20 金币统一由 `/finish` 原子结算；失败和主动投降的 10 金币由 `/quit` 原子结算。客户端不再调用 `/victory`。

### 9.2 符文掉落

前提：账号日龄至少 3 天；仅完成波次触发；每 Run 最多 4 次成功掉落。

| 完成波次 | 掉落概率 | Common | Excellent | Epic | Legendary |
|---|---:|---:|---:|---:|---:|
| W1-W2 | 0% | - | - | - | - |
| W3-W6 | 12% | 75% | 25% | 0% | 0% |
| W7-W12 | 18% | 45% | 30% | 15% | 10% |
| W13-W16 | 28% | 30% | 30% | 25% | 15% |
| W17-W20 | 40% | 15% | 25% | 40% | 20% |

奖品形态：

- Common、Excellent：必定完整符文。
- Epic：25% 完整符文，75% 为 1 个碎片；3 碎片可合成。
- Legendary：固定为 1 个碎片；5 碎片可合成。

奖池顺序必须稳定，Pool roll 以此顺序取模：

| 稀有度 | Rune ID 顺序 |
|---|---|
| Common | `Might` |
| Excellent | `Farreach`, `Power`, `Longshot`, `Frostbite` |
| Epic | `Ricochet`, `Volley`, `BladeTempest`, `Ambush`, `Windhawk` |
| Legendary | `Skybreaker`, `Wyrmguard`, `Dragonbloom`, `Warcry` |

服务端结算返回 `rune_drops[]`，每项包含 `rune_id`、`rune_instance_id`、`full`、`fragments`；`rune_fragments` 为该次结算增加的碎片总数。服务端账本为最终权威，客户端只按结算结果展示和落地。

## 10. 目标数据库中的已发布 Stage

客户端要求目标环境中存在且仅存在一个与下列组合匹配的有效发布版本：

| 检查项 | 客户端期望值 |
|---|---|
| Stage ID | `stage-001` |
| 发布状态 | `published` |
| Configuration ID | `PressureRaceGreyboxV2` |
| Content Version | `2026081301` |
| Config Version | `2026081501` |
| RNG Version | `DragonBound.Random.v1` |
| Stage Snapshot SHA-256 | `a555903b1f717480e79dc29c4a28c8ec09bd18fc4316a6328cfc2e672b6d05eb` |
| Reward Policy | 本文第 9 节 |

本客户端仓库无法读取目标数据库，因此“已经实际发布”目前不能由客户端单方确认。服务端完成后必须回填以下验收证据：

```text
environment:             <dev / staging / production>
database/schema/table:   <实际位置>
record_primary_key:      <实际主键>
stage_id:                stage-001
status:                  published
content_version:         2026081301
config_version:          2026081501
rng_version:             DragonBound.Random.v1
stage_snapshot_digest:   a555903b1f717480e79dc29c4a28c8ec09bd18fc4316a6328cfc2e672b6d05eb
reward_policy_version:   <服务端实际版本>
published_at_utc:        <ISO-8601>
verified_by:             <负责人/流水线>
```

在上述信息未回填前，项目状态应写为“客户端规范已冻结，目标数据库发布待服务端确认”，不能写成“正式 Stage 已发布”。

## 11. 服务端配合验收清单

- `/v1/runs/start` 返回 `rng_version=DragonBound.Random.v1`、合法 `recruit_random`、合法 `wave_rng` 和匹配的 `stage_snapshot_digest`。
- 服务端使用第 7 节黄金向量通过 CI；字节序、补码、溢出回绕不得依赖机器默认行为。
- `/finish` 接受合法空暴击序列，并拒绝错误头、版本、数量和长度。
- 相同 Run、相同结束请求重复提交只产生一笔结算。
- 胜负金币、广告翻倍、退出/过期零奖励符合第 9.1 节。
- 符文概率、条件消费、奖池顺序和每局上限符合第 6.4、9.2 节。
- 目标数据库发布证据按第 10 节回填，并用一次真实 `/runs/start` 响应验证摘要。
