# Drakeforge Boss System V1

状态：`[SELECTION FROZEN / W6 IMPLEMENTED / W12-W20 SKILL VALUES FROZEN / PRODUCTION HP PENDING]`

本文是 V1 Boss 选择、行为规则和实现状态的权威说明。Boss 数值必须逐档验证，不能沿用旧灰盒 HP 直接冻结。

## 1. V1 固定 Boss

V1 不使用 Boss 随机池，也不设置候选 Boss 故障回退。

| Wave | Fixed Boss | Hero XP设计值 | 当前实现状态 |
| ---: | --- | ---: | --- |
| W6 | Soulchain Binder | 6 | 已实现并验证；最后一击Hero XP接线已实现 |
| W12 | Stormcaller Priest | 10 | 机制已实现并验证；1200为Greybox，Production HP待校准 |
| W16 | Bloodcrown Tyrant | 15 | 技能规则/数值已冻结；Production HP和运行时待完成 |
| W20 | Worldeater Wyrm | 20 | Devour/Minion机制已接入并验证边界；5000为Greybox，Production HP仍待校准 |

Boss XP 由造成正式最后一击的 Hero 独享。Basic、Item及无有效Hero归属的伤害不获得Hero XP。Gravewake Summoner、Coldrain Witch、Voidland Usurper、Phantom Queen、Ironhoof Warlord、Blackcloud Ruineye、Ironwall Executioner和Draconic Strategist均降级为 `Future Candidate`，不进入 V1 Production。

## 2. W6 Soulchain Binder

状态：`[IMPLEMENTED / FROZEN]`

| 字段 | 冻结值 |
| --- | ---: |
| BaseHP | 600 |
| MoveSpeed | 0.20 cells/s |
| FirstCastDelay | 8.0s |
| CastWindup | 0.5s |
| EffectDuration | 2.0s |
| Cooldown | 15.0s，从效果结束起算 |
| SelectionArea | RandomEligible2x2 |
| SelectionWithinArea | UniformRandom |
| MaxAffectedBasic | 2 |
| Effect | AttackDisabled |

SoulChain 使用 RunSeed 随机选择至少包含1个Basic的连续2x2区域。前摇结束时读取区域内仍存活的Basic；超过2个时均匀随机选择2个，不按等级、DPS或品质加权。

SoulChain只禁止攻击，不禁止拖动、Merge、Recruit、Item或其它棋盘操作。合并仍可发生；任一来源处于控制时，合并结果继承来源中的最长剩余控制时间。Boss死亡立即清除未结束的SoulChain控制。

## 3. W12 Stormcaller Priest

状态：`[IMPLEMENTED / GREYBOX HP / PRODUCTION HP PENDING]`

| 字段 | V1值 | 状态 |
| --- | ---: | --- |
| MoveSpeed | 0.20 cells/s | FROZEN |
| TargetBossKillTime | 32-36s | FROZEN TARGET；标准Item Build |
| BaseHP | 1200 | GREYBOX；按目标击杀窗口校准后再冻结Production值 |
| FirstCastDelay | 7.0s | FROZEN |
| CastWindup | 0.75s | FROZEN |
| BuffDuration | 8.0s | FROZEN |
| Cooldown | 12.0s | FROZEN；从Buff结束起算 |
| EffectRadius | 2.5 cells | FROZEN |
| TargetCap | N/A | FROZEN；范围内全部合法目标 |
| ShieldValue | 60 | FROZEN |
| MoveSpeedMultiplier | x1.15 | FROZEN；Normal 0.60变为0.69 cells/s |

Storm Call采用施法快照，不是持续光环。施法结算时只选取Boss作用范围内的合法目标；之后进入范围的单位不会自动获得本次效果。当前合法目标为Normal；接口保留未来Boss Summon扩展位但本切片不实现Boss Summon，不作用于施法Boss自身。

成功施法同时提供临时护盾和移动速度增益。同源增益不叠层：

- 目标护盾仍大于0时，再次命中会把护盾补满至本技能配置的`ShieldValue`。
- 目标护盾已经完全打破时，下一次合法Storm Call会重新生成满额`ShieldValue`。
- 同一目标始终只有一个Storm Call护盾池；补满或重新生成均不叠加额外护盾层。
- 同源移动速度增益只刷新持续时间，不重复叠加倍率。
- Boss死亡时不移除已授予的护盾。
- Boss死亡时不移除尚未到期的移动速度增益；增益按原剩余持续时间自然到期。

护盾是额外HP池，不提供护甲或减伤。护盾吸收所有合法伤害，超出剩余护盾的伤害继续扣目标本体HP。打掉护盾本身不结算Hero XP；目标正式死亡时才按最后一击规则结算。

标准施法时间线：

```text
t=7.00   第一次Storm Call开始
t=7.75   第一次Buff生效
t=15.75  第一次Buff结束，12秒CD开始
t=27.75  第二次Storm Call开始
t=28.50  第二次Buff生效
t=36.50  第二次Buff结束
```

运行时已接入W12独立Boss槽、Storm Call生命周期事件、护盾优先伤害和Boss XP映射；`BaseHP=1200` 仍只是 Greybox。必须使用标准Item Build验证32-36秒目标窗口、第二次施法覆盖率、W13残怪叠压和玩家/AI两侧公平性后再冻结Production值；不得直接把灰盒1200提升为Production值。

## 4. W16 Bloodcrown Tyrant

状态：`[SKILL VALUES FROZEN / GREYBOX HP / RUNTIME PENDING]`

| 字段 | V1值 | 状态 |
| --- | ---: | --- |
| MoveSpeed | 0.20 cells/s | FROZEN |
| TargetBossKillTime | 40-45s | FROZEN TARGET；Item+Rune+成型Hero构筑 |
| BaseHP | 2400 | GREYBOX；按目标击杀窗口校准后再冻结Production值 |
| FirstCastDelay | 8.0s | FROZEN |
| CastWindup | 1.0s | FROZEN |
| RetryCooldown | 12.0s | FROZEN；仅施法失败后使用 |
| EffectiveBasicLevel | Lv1 | FROZEN |
| DisableMerge | true | FROZEN |
| RestoreOnBossDeath | true | FROZEN |

成功施法后，当前在场以及之后新入场的全部Basic都受到Bloodcrown Decree影响，直到Boss死亡：

- `StoredLevel`保持真实值，不被Boss技能修改。
- `EffectiveCombatLevel = 1`；Attack和Attack Speed按Lv1计算。
- Range保持Basic原值，不因等级覆盖改变。
- 合法Item/Rune增益在Lv1基础Attack和Attack Speed之后计算。
- Basic无法Merge。
- Hero、Component、PairLink、Hero XP和Rune Loadout不受影响。
- 等级变化类Item仍可修改`StoredLevel`，但Boss存活期间有效战斗等级仍为Lv1。
- Boss死亡后，所有Basic立即按最新`StoredLevel`恢复，并解除Merge封锁。

技能结算成功后建立全局覆盖状态，即使当时没有Basic，之后新入场的Basic仍立即受影响；成功后不再重复施法。技能尝试经过Spellbreaker交互：第一次在`t=8.0s`开始、`t=9.0s`结算；失败时不产生等级覆盖，按Boss MaxHP的10%反噬且不产生奖励，12秒后在`t=21.0s`重试、`t=22.0s`结算。

Merge封锁覆盖玩家、AI、自动合成及其它正式入口，必须在扣除资源或消耗Item前拒绝。锁定期间招募到重复Basic时，作为独立单位进入可用战场格或备战区，不自动合成；没有容量时沿用现有招募失败规则。等级变化Item仍可修改`StoredLevel`，但Boss存活期间`EffectiveCombatLevel`保持Lv1。

Production BaseHP仍为 `PENDING`。必须使用Item+Rune+成型Hero构筑验证40-45秒目标窗口、Hero/Basic伤害占比、W17残怪叠压、Spellbreaker有无两组结果及玩家/AI公平性后再冻结；不得直接把灰盒2400提升为Production值。

## 5. W20 Worldeater Wyrm

状态：`[MECHANISM INTEGRATED / GREYBOX HP / PRODUCTION HP PENDING]`

状态：`[SKILL VALUES FROZEN / GREYBOX HP / RUNTIME PENDING]`

| 字段 | V1值 | 状态 |
| --- | ---: | --- |
| MoveSpeed | 0.20 cells/s | FROZEN |
| TargetBossKillTime | 55-62s | FROZEN TARGET；Full Build |
| BaseHP | 5000 | GREYBOX；按目标击杀窗口校准后再冻结Production值 |
| HeroXPReward | 20 | FROZEN设计值；运行时发放待实现 |
| GoalEffect | InstantDefeat | INHERITED / FROZEN；沿用全局Boss规则 |

V1第一阶段实现Devour和Worldeater Minion。Subordinate Boss只保留接口，`SubBossPool`在内容确定前保持 `PENDING`。

### Devour

| 字段 | V1值 |
| --- | ---: |
| FirstCastDelay | 10.0s |
| CastWindup | 1.0s |
| Cooldown | 15.0s，从结算后起算 |
| BasicGrowthCoefficient | 5% InitialMaxHP |
| MinionGrowthCoefficient | 3% InitialMaxHP |
| SubBossGrowthCoefficient | 10% InitialMaxHP |

合法目标优先级为：最低`StoredLevel` Basic，然后是Worldeater Minion，最后是SubBoss。同等级Basic按稳定Runtime顺序选择，不消耗随机流。吞噬Basic会永久移除该局单位并释放格子，不退款，不提供Hero XP或Run Resource，也不触发击杀Rune及通用击杀奖励。

每次吞噬使用初始MaxHP计算固定增量，禁止复利：

```text
AddedHP = InitialMaxHP * GrowthCoefficientForTargetClass
MaxHP += AddedHP
CurrentHP += AddedHP
```

因此每次吞噬同时增加MaxHP和等量CurrentHP；后续增量始终基于`InitialMaxHP`。

Devour在开始前先检查并锁定合法目标。没有合法目标时不进入前摇、不触发Spellbreaker、不产生反噬，但仍进入完整15秒CD。目标在1秒前摇期间死亡、合成、移除或变为非法时，本次技能不重新选择目标、不触发Spellbreaker、不产生反噬，并进入完整CD。

前摇结束时目标仍合法，才进行Spellbreaker判定。被阻止时不吞噬、不成长，按当时Boss MaxHP的10%反噬且不产生奖励，并进入完整CD；未被阻止时才提交吞噬和线性HP成长。标准无阻断施法起始时间约为`t=10s / 26s / 42s / 58s`。

### Worldeater Minion

| 字段 | V1值 |
| --- | ---: |
| FirstSummonDelay | 12.0s |
| CastWindup | 0.75s |
| Cooldown | 18.0s，从结算后起算 |
| SummonCountPerCast | 4 |
| AliveMinionCap | N/A |
| MinionHP | 330 |
| MinionMoveSpeed | 0.75 cells/s |
| MinionGoalEffect | InstantDefeat |

每次合法召唤成功固定生成4只，不读取场上剩余数量，也不设置同时存活上限；上批仍存活时继续生成4只，因此场上可以累计8只、12只或更多。召唤始终是合法CastAttempt，在0.75秒前摇结束时接受Spellbreaker判定。被阻止时不生成Minion，按当时Boss MaxHP的10%反噬且不产生奖励，并进入完整18秒CD；未被阻止时生成完整4只。标准无阻断施法起始时间约为`t=12s / 30.75s / 49.5s / 68.25s`。

Worldeater Minion沿用Boss Summon Rules V1，但到达终点使用`InstantDefeat`。其Hero XP、Run Resource及通用击杀奖励均为0，Boss死亡后已生成Minion不自动消失，并继续作为Residual阻塞最终清场。单只Minion在0.75 cells/s下走完15格路线约需20秒。

SubBoss池、SubBoss实体和SubBoss奖励继续保持 `PENDING`，V1第一阶段不实现。

Production BaseHP仍为 `PENDING`。必须验证Full Build在55-62秒目标窗口内的Boss TTK、Devour成功次数、Minion漏怪率、最终清场时间以及玩家/AI公平性。Spellbreaker必须至少覆盖未携带、普通构筑携带和高单体Boss构筑携带三组，判断它是否从强力Counter变成W20事实必选；不得通过Worldeater隐藏抗性修正该问题，也不得直接把灰盒5000提升为Production值。

## 6. 实现顺序

1. 保持W6当前实现不变；W6 Boss XP映射与最后一击归属已接入。
2. 实现W12 Stormcaller Priest并完成Item Build校准。
3. 实现W16 Bloodcrown Tyrant并完成Item+Rune+Hero构筑校准。
4. 实现W20 Devour和Minion，完成Full Build校准。
5. SubBoss内容和未来候选Boss不进入V1首批生产范围。
