# Item + Rune + Boss 联合数值校准矩阵 V1

状态：`BARE FORMAL EXECUTED / STANDARD AND FULL INPUTS PENDING`

日期：2026-08-18

## 1. 目的

本矩阵用于在机制回归通过后，统一测量 Item、Rune、四个固定 Boss、AI 和 W1-W20 压力。
本阶段只采集和比较数据，不修改 Boss 技能语义，不把灰盒 HP 直接冻结为 Production。

当前基线：

- Item + Rune + Boss Targeted：`137/137`
- Fast EditMode：`567/567`
- PlayMode：`29/29`
- W6 Soulchain Binder HP：`600`，已冻结
- W12 Stormcaller Priest HP：`1200`，候选值
- W16 Bloodcrown Tyrant HP：`2400`，候选值
- W20 Worldeater Wyrm HP：`5000`，Greybox 候选值
- Boss 移速：`0.20 cells/s`
- W20 每次合法召唤：固定 `4` 只 Minion

## 2. 固定输入

### Seed

| 阶段 | Seed 范围 | 用途 |
|---|---:|---|
| Smoke | `1..50` | 快速检查矩阵、CSV、统计字段和确定性 |
| Formal | `1..1000` | 生产候选比较，不丢弃提前失败样本 |

同一候选值、同一构筑、同一 AI 配置必须使用完全相同的 Seed 集。每个 Seed 只运行一次原始样本，不能为 Boss 生成失败重新抽样。

### 构筑包络

| Build ID | Item | Rune | 用途 |
|---|---|---|---|
| `BARE` | 关闭 | 关闭 | 测量基础 W1-W20 生存压力，不用于证明玩家真实上线体验 |
| `STANDARD` | 产品允许的标准 Item 组合 | 产品允许的标准 Rune 组合 | 主要生产候选判定 |
| `FULL` | 当前客户端合法上限 | 当前客户端合法上限 | 测量叠加上限、Boss 反制能力和极端压力 |

`STANDARD` 必须由已确认的产品配置提供；如果服务端权威 Profile、Reward、Ledger 尚未接入，使用本地诊断快照并在报告中标记 `CALIBRATION_FIXTURE`，不得写成线上权威结果。

### AI

当前可执行基线只有 `BasicUnitAiController` V0。AI 1/2/3、连败降档和匹配 Item 携带差异 `<=3` 尚未成为运行时可切换参数，因此本轮记录：

- `AI_V0_BASELINE`：现有控制器，作为唯一实际基线。
- `AI_LEVEL_1/2/3`：`PENDING_IMPLEMENTATION`，不得用人工标签代替实际控制逻辑。
- 连败降档：`PENDING_IMPLEMENTATION`，待有真实难度参数后再加入矩阵。

## 3. 每局必须记录的指标

### 结果指标

- `run_seed`、`build_id`、`ai_profile`
- 首次失败波次、失败原因、双方先后顺序
- W1-W20 各波到达率和结束率
- W20 Boss 到达率、Boss 击杀率、Boss 到 Goal 率
- W20 是否因 Minion / SubBoss / Boss 本体触发 `InstantDefeat`

### Board / AI 指标

- 每波 BoardQuality、占用格、空闲格、Bench、Camp
- Basic 数量、Hero 数量、Hero 合成尝试/成功次数
- 招募次数、失败原因、Forge Pick 使用次数
- Player 与 AI 的同 Seed 差异

### Boss 指标

- Boss Spawn / Kill / Goal 时间
- 仅统计已生成且被击杀样本的 TTK `P25/P50/P75`
- Boss 生成后 3 秒、5 秒伤害
- 技能首次施法时间、成功/失败次数、技能持续时间
- Spellbreaker 触发次数和反噬伤害
- W20 Devour 次数、吞噬目标类型、MaxHP/CurrentHP 增量
- W20 每次召唤生成 `4` 只、存活数量、跨波残留数量

### Item / Rune 指标

- Item 使用次数、命中目标、实际伤害/控制/经济结果
- Rune 装备、卸下、合成、奖励和战斗触发次数
- 同一 Run 的 Item/Rune 快照是否保持不变
- Item 与 Rune 同时生效时的最终效果，不记录“理论值”替代实际值

## 4. 首轮执行顺序

1. `BARE + AI_V0`：先确认 W1-W5 生存漏斗和双方输入差异。
2. `STANDARD + AI_V0`：测量 Item/Rune 对 W6、W12、W16、W20 的真实增益。
3. `FULL + AI_V0`：测量极端叠加和 Boss 反制上限。
4. 在相同 Seed 和 Build 下比较 W12/W16/W20 候选 HP，不改技能。
5. 仅当机制指标全部正常时，才调整 HP、ItemPowerCost 或其他数值。

## 5. 暂定判定窗口

以下是测量目标，不是已经冻结的生产值：

| Boss | 目标 TTK |
|---|---:|
| W6 Soulchain Binder | 由已冻结 HP=600 的正式样本记录，不再用旧 28-32 秒目标单独判定 |
| W12 Stormcaller Priest | `32-36s` |
| W16 Bloodcrown Tyrant | 待完成标准构筑样本后确定窗口 |
| W20 Worldeater Wyrm | `55-62s`，且不得让 W20 成为普遍完整通关 |

判定必须同时看：Boss TTK、Boss 到 Goal 率、W1-W20 失败曲线和残怪压力，不能只优化单一 TTK 中位数。

## 6. Promote / Reject 规则

### 可以进入下一轮

- Targeted、Fast EditMode、PlayMode 全部通过；
- 固定 Seed 可复现；
- Player/AI 没有固定坐标或输入流导致的系统性单边偏差；
- Item/Rune 的效果在快照、目标归属和 Boss 规则下符合已冻结规则；
- Boss 失败率和 W1-W20 失败曲线符合压力竞速定位。

### 不能冻结

- 只在 Direct-W12 或人工 Full Build 场景下成立的数值；
- 只看成功击杀样本而忽略提前失败样本；
- 用提高/降低 AI 代替修正 Boss HP；
- 用 Boss 机制改动掩盖 Item/Rune 叠加问题；
- 使用服务端尚未提供权威结果的本地奖励或 Ledger 数据。

## 7. 产出物

每轮必须保存：

- 原始逐 Seed CSV；
- 汇总 Markdown；
- 候选值、Build、AI 配置和 Seed 范围；
- 测试命令和 Unity 日志；
- 明确标记 `BASELINE`、`CANDIDATE` 或 `PRODUCTION_CANDIDATE`。

只有完成 BARE、STANDARD、FULL 三组 Formal 样本并通过联合回归后，才允许进入模块迁移阶段。

## 8. 基线执行记录

2026-08-18 当前主分支基线：

| 检查 | 结果 | 产物 |
|---|---:|---|
| Fast EditMode | `567/567` | `Logs/TestLane-FastEditMode-20260818-224811.xml` |
| PlayMode | `29/29` | `Logs/TestLane-PlayMode-20260818-224913.xml` |
| W12 build-envelope smoke | `3/3` | `Logs/TestLane-Targeted-20260818-225152.xml` |
| `git diff --check` | 通过 | 当前工作树无空白错误 |

这些结果只证明当前机制和 W12 构筑包络烟测可运行，不代表任何 W12/W16/W20 HP 已经达到 Production。

## 9. 当前可执行入口

已加入 Editor 诊断入口：

`DragonBound.Editor.JointBalanceCalibrationBatch.Run`

该入口当前执行：

- W6 `BARE + AI_V0 + HP=600`，Seed `1..50`；
- W12 现有两 Item 诊断夹具，HP `1100/1200/1300`，Seed `1..50`；
- 输出 `Logs/JointBalanceCalibrationSmoke-W6.csv`、`Logs/JointBalanceCalibrationSmoke-W12.csv` 和 `Docs/JointBalanceCalibrationSmokeV1.md`；
- 完整 W1-W20 的 W6/W12/W16/W20 生命周期、TTK、伤害和 W20 召唤数；
- 标准 Rune 构筑、AI 1/2/3 和连败降档仍明确输出为 `PENDING`。

入口已通过 Fast EditMode 编译验证。当前已完成 50 Seed Smoke、1000 Seed BARE Formal，以及 W16/W20 Direct Boss 候选包络。

## 10. 已执行结果

### 1000 Seed BARE Formal

| 指标 | Player | AI |
|---|---:|---:|
| W20 到达率 | `0.00%` | `0.00%` |
| 结束波次 P50 | `W7` | `W7` |
| W6 Spawn / Kill / TTK P50 | `76.90% / 40.60% / 22.90s` | `76.90% / 44.10% / 24.80s` |
| W12 Spawn / Kill / TTK P50 | `15.20% / 10.10% / 27.20s` | `15.20% / 11.10% / 25.20s` |
| W16 Spawn / Kill / TTK P50 | `5.20% / 2.20% / 31.09s` | `5.20% / 3.00% / 30.19s` |

逐 Seed 原始数据：`Logs/JointBalanceCalibrationFormalV1.csv`。

### Direct W16/W20 包络

- W16 `2400`：Player/AI 击杀样本 TTK P50 为 `42.10s / 39.90s`，接近 40-45s 目标，但击杀率只有 `6% / 6%`，只能保留为候选。
- W16 `2800`：TTK P50 为 `43.40s / 43.40s`，但击杀率降到 `2% / 4%`，样本过少，不能因中位数落窗而 Promote。
- W16 `3200`：双方均无击杀样本，应从下一轮候选中剔除。
- W20 `4000/5000/6000/7000`：四档均无 Boss 击杀；Direct BARE 夹具先被 Worldeater Minion 的 InstantDefeat 压力终止。此结果证明当前缺口是合法 Full Build，而不是继续扩大 HP 扫描。

### 当前判定

- W6 `600`：保持用户已冻结值，不因本轮 BARE TTK 回滚。
- W12 `1200`：保留 Greybox；标准两 Item 烟测仍未满足 32-36s 双边目标。
- W16 `2400`：保留下一轮 Candidate，不冻结 Production。
- W20 `5000`：保留 Greybox，不冻结 Production。
- 在标准 Rune 构筑、FULL 构筑和 AI 1/2/3 可执行输入出现前，不进行 Production Promote，也不开始模块迁移。
