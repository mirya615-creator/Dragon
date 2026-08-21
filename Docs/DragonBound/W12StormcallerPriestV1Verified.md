# W12 Stormcaller Priest V1 Verified

状态：`IMPLEMENTED / GREYBOX VERIFIED / PRODUCTION HP PENDING`

## 范围

本切片接入 W12 固定 `BOSS_STORMCALLER_PRIEST`。Boss 使用独立 W12 slot，与 27 只 Normal 同时在波次开始时生成；Boss 不占 Normal 数量，可作为 Residual 跨波保留。W16、W20、SubBoss、BossSummon、AI 策略、UI、Scene/Prefab 和新的 Item/Rune 效果不在范围内。

`GreyboxMaxHitPoints=1200` 只用于接线和验证输入，`FormalHitPointsPending=true`；本提交没有把 1200 写成正式 Production HP。

## 已验证运行时

- MoveSpeed：`0.20 cells/s`；Boss 到 Goal 仍沿用 `InstantDefeat`。
- Storm Call：7.0s 开始，0.75s 前摇，结算快照半径 2.5 格，作用于结算时仍存活的己方 Normal 全部目标；Boss 自身排除，未来 BossSummon 仅保留接口位。
- 成功效果：Shield 60、MoveSpeed x1.15、持续 8.0s；同源速度只刷新时间，护盾单池补满/重建，不叠层。
- 15.75s 效果结束后进入 12.0s CD；第二次时间线为 27.75s 开始、28.50s 生效、36.50s 结束。
- 护盾先吸收伤害，溢出进入本体；打盾不结算 XP。Boss 死亡不清除目标护盾或尚未到期的速度增益，速度按剩余时间自然恢复。
- Windup 结束调用现有 `ISoulChainSpellbreakerResolver` 接缝；阻断时不施加效果，Boss 受到 MaxHP 10% 反噬，进入完整 12s CD，不产生 XP、资源、RuneDerived 或通用击杀奖励。未注入 resolver 时不新增隐藏阻断率。
- typed `StormcallerCastEvent` 由 `TwentyWavePressureRuntime.StormcallerCastEmitted` 转发，供 Analytics 适配。
- Boss XP 映射为 W6=6、W12=10、W16=15、W20=20；当前实体接入 W6/W12，最后一击必须由有效 Hero 归属，Basic/Item/无效归属/反噬不奖励。

## 数据流与边界

`TwentyWavePressureRuntime.BeginWave(12)` 通过现有 `PressureRaceSideRuntime.SpawnBoss` 创建共享规则下的双方 W12 Boss，并创建双方 `StormcallerPriestRuntime`。runtime 持有各自 `EnemyRegistry`，在每次 Tick 中处理施法生命周期；目标状态和护盾字段位于 `EnemyRuntime`，所有现有 Basic、Hero、GroundHazard、Rune 和 W6 telemetry 伤害入口统一走 `ApplyDamage`，因此溢出规则不会绕过既有战斗路径。

本切片没有新增 Scene/Prefab 配置，也没有修改 W6 Soulchain 的 HP=600、速度、2x2 选择、Merge 继承或现有测试行为。

## 验证记录

| Lane | Result | Duration | XML | Log |
| --- | --- | ---: | --- | --- |
| W12 targeted (`W12StormcallerPriestV1Tests`) | 9/9 passed | 0.185215s | `Logs/TestLane-Targeted-20260818-150117.xml` | `Logs/TestLane-Targeted-20260818-150117.log` |
| W6 regression targeted (`W6SoulChainV1Tests`) | 12/12 passed | 0.2608912s | `Logs/TestLane-Targeted-20260817-215610.xml` | `Logs/TestLane-Targeted-20260817-215610.log` |
| Fast EditMode | 485/485 passed | 8.5127673s | `Logs/TestLane-FastEditMode-20260818-150315.xml` | `Logs/TestLane-FastEditMode-20260818-150315.log` |
| Full PlayMode | 29/29 passed | 25.6304704s | `Logs/TestLane-PlayMode-20260817-215839.xml` | `Logs/TestLane-PlayMode-20260817-215839.log` |

Full EditMode 和 1000 Seed 长校准本轮未运行；没有用短烟测结果冻结 HP。

## 下一步：W12 正式 HP Build Envelope

正式校准前必须使用标准 Item Build、Item/Rune 规则按产品口径启用，并以相同 Seed Set 分别记录 Player 与 AI：Boss 生成/击杀/到终点/Residual、TTK P25/P50/P75、生成后 3s/5s 伤害、Storm Call 首次与第二次成功/失败及受影响 Normal 数、护盾伤害与本体伤害拆分、W13 残怪和两侧差异。校准应先定义合格 Build Envelope，再运行有界候选，不得把 1200 Greybox 直接 Promote，也不得通过修改 Storm Call、Boss 速度、普通怪或 AI 来补偿 HP。
