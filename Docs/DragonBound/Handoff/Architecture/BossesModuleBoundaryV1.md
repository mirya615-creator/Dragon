# Bosses Module Boundary V1

状态：`CONTRACTS ESTABLISHED / W16-W20 RUNTIME ISOLATED AND GREYBOX-INTEGRATED / W6-W12 RUNTIME UNMIGRATED / PRODUCTION HP PENDING`

## Ownership

`DragonBound.Bosses.Contracts` 是后续 Boss Runtime 的唯一纯契约入口。它描述 Boss 身份、配置输入、技能生命周期、施法尝试与结果、Boss-to-Goal 结果、Spellbreaker 阻断/反噬、最后一击 Hero XP 归属，以及 BossSummon 的生命周期和奖励策略。契约程序集不创建实体、不驱动波次、不修改战斗、AI、Item、Rune、UI 或 Bootstrap。

Boss 的 `MaxHitPoints` 和 `MoveSpeed` 只能由未来组合根或 Boss Runtime 作为配置输入提供。契约不包含 W6/W12 灰盒值，也不包含 W16/W20 Production 值；Boss XP 也由配置/结算结果携带，不在契约内复制现有运行时数值表。

## Dependency direction

```text
Foundation.Contracts  <- Bosses.Contracts -> Enemies.Contracts
                                     \-----> Combat.Contracts
```

`Foundation.Contracts` 提供 `WaveNumber` 等稳定标识；`Enemies.Contracts` 提供召唤的敌人类别；`Combat.Contracts` 提供正式最后一击的 `CombatDamageOwner`。Bosses.Contracts 不反向引用 `DragonBound.Runtime`，也不引用 Unity API。后续 Boss Runtime 只能依赖本契约和既有模块适配器，不能把 Boss 逻辑写回单体 Runtime。

## Contract surface

- `FixedBossIds` / `FixedBosses`：固定的 W6 Soulchain Binder、W12 Stormcaller Priest、W16 Bloodcrown Tyrant、W20 Worldeater Wyrm 身份与波次。
- `BossDefinition`：BossId、波次、HP、移速、GoalEffect 和 XP 作为输入。
- `BossSkillLifecycleEvent`：`Start`、`Windup`、`Resolve`、`Blocked`、`Cooldown` 生命周期。
- `BossCastAttempt` / `BossCastResult`：目标锁定、Spellbreaker eligibility、阻断、反噬、GoalEffect 和奖励结果。
- `BossLastHitXpAward`：只有正式最后一击且 owner 是有效 Hero 时才可授予 Hero XP；Basic、Item 或无效归属不会满足 `GrantedToHero`。
- `BossSummonPolicy` / `BossSummonDefinition`：`SpawnSource`、Hero XP、Run Resource、`DespawnOnBossDeath`、`BlocksWaveScheduleCompletion`、`PersistsAcrossWave`，以及召唤数量、HP、移速和 GoalEffect。

## Runtime ownership still pending

本阶段没有移动或改写下列现有实现：`Assets/DragonBound/Runtime/Core/SoulchainBinderRuntime.cs`、`Assets/DragonBound/Runtime/Enemies/StormcallerPriestRuntime.cs`、`Assets/DragonBound/Runtime/Core/BossExperienceRewards.cs`、`Assets/DragonBound/Runtime/Core/W6SoulChainDamageTelemetry.cs`、`Assets/DragonBound/Runtime/Core/W6BareFullScheduleCalibration.cs`、`Assets/DragonBound/Runtime/Core/W12BuildEnvelopeCalibration.cs`、`Assets/DragonBound/Runtime/Core/TwentyWavePressureRuntime.cs`、`Assets/DragonBound/Runtime/Core/PressureRaceSideRuntime.cs` 和 `Assets/DragonBound/Runtime/Core/EnemyRuntime.cs`。W6/W12 仍由这些单体 Runtime 所有；W20 仍没有 Runtime 实现。

## Phase C-2 W16 isolated runtime

`DragonBound.Bosses.Runtime` 现在拥有 `BloodcrownTyrantConfiguration`、`BloodcrownTyrantRuntime` 和 `BloodcrownBasicCombatPolicy`。它只依赖 `Bosses.Contracts` 与 `Combat.Contracts`，并通过三个端口与宿主连接：Boss HP/死亡目标、Spellbreaker 判定、以及当前/未来 Basic 的有效等级和 Merge policy。Runtime 实现固定 W16 时序：8.0s 开始、1.0s 前摇；Spellbreaker 阻断按实时 Boss MaxHP 的 10% 反噬并在 12.0s 后重试；成功后启用 EffectiveCombatLevel=1 和 Merge 封锁；Boss 死亡时清理两项状态。Basic combat policy 以 Lv1 Attack/AttackSpeed 作为现有 Item/Rune modifier pipeline 的输入，同时保留 StoredLevel 对应 Range。XP 只返回 `BossLastHitXpAward`，不直接修改 Hero progression。

当前单体 `BoardRecruitDestination` 没有 StoredLevel/EffectiveCombatLevel 分离端口，`PressureRaceSideRuntime` 也直接按 `RecruitCard.Level` 读取 Basic stats；因此本提交不伪造 Match 接入，也不改变 W6/W12 行为。下一次 Integration 适配必须让 Basic 注册、Basic stats 计算、所有 Merge 入口和正式最后一击结算共同消费上述端口，才能把该 Runtime 接入 `TwentyWavePressureRuntime` 的 W16 双侧 Boss slot。

## Current W20 status and next step

W16 的独立 Boss slot、Basic policy port、Merge 封锁/恢复和生产 Tick 接入已由 `W16BloodcrownIntegrationTests` 覆盖。W20 的独立 Runtime、四只召唤物、Boss 死亡后召唤物残留、Minion 到 Goal 的 InstantDefeat、Boss XP 映射和 Spellbreaker 端口已由 `W20WorldeaterIntegrationTests` 覆盖。下一步不再重复创建 W20 Runtime，而是完成 W16/W20 的正式数值与 AI 压力校准，并在 Item + Rune 联合构筑下验证后再冻结 Production HP。W6/W12 仍保留在单体 Runtime，迁移前必须先完成对应适配器和回归门禁。
