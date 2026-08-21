# Item 客户端审计 V1

日期：2026-08-18
范围：Unity 客户端 Item 代码、客户端契约、现有测试与交接文档。
不包含：服务端 API、Ledger、广告验证、账号经济实现、未冻结道具数值。

## 结论

当前状态是 **Item Foundation + 两个效果的客户端竖切**，不是完整 Item 产品闭环。可以继续并行做道具效果与客户端 UI/适配层，但 Merchant、广告、Gold/Energy、DayKey 和持久化权威必须等待服务端契约接入；不能把当前状态标记为可发行。

## 已实现

| 能力 | 证据 | 状态 |
| --- | --- | --- |
| 20 个稳定 ItemId、Active/Passive 分类、稀有度、实现状态 | `Assets/DragonBound/Runtime/Items/ItemDefinitions.cs` | 已实现 |
| 第五次正常完成对局解锁；胜负计入，异常退出不计入 | `ItemProfile.cs:13-20, 400-478` | 已实现（客户端消费权威计数） |
| DayKey 变化清空当日库存/装备，解锁状态保留 | `ItemProfile.cs:417-442` | 已实现（DayKey 由外部注入） |
| 每个 Item 每日最多一份、Active 2 格、Passive 6 格、重复/未拥有/Pending 拒绝 | `ItemProfile.cs:81-386` | 已实现 |
| RunStart 生成不可变快照；玩家与 AI 独立快照和运行时 | `ItemProfile.cs:461-477`、`ItemRuntime.cs:188-307`、`TwentyWavePressureRuntime.cs:198-228` | 已实现 |
| `Drakeheart Relic`：RunStart Max/Current Heart +3 | `ItemRuntime.cs:74-103` | 已实现 |
| `Winterveil Rune`：自路线存活敌人减速 10%/5s，CD 30s；无目标不进入 CD | `ItemRuntime.cs:105-165` | 已实现 |
| 灰盒 HUD 两个 Active 槽绑定统一命令入口 | `GreyboxHudView.cs:269-307` | 已实现（占位 UI） |
| Item Contracts 独立程序集及 Merchant/广告/Ledger DTO | `Assets/DragonBound/Runtime/Items/Contracts/*` | 契约已实现，未接入权威实现 |

## 客户端缺失或未接入

### 0. 持久化恢复遗漏完成局数（已修复）

`ItemProfile.TryRestorePersistentData`（`ItemProfile.cs:494-532`）此前校验并恢复了 `DayKey`、库存和装备，但没有把 `data.NormalCompletedMatchCount` 写回 `NormalCompletedMatchCount`。该问题已修复，并增加“保存 -> 恢复 -> `IsUnlocked` 仍为 true、完成局数保持”的回归测试。

### 1. 生产启动没有真实 Item Profile -> Snapshot 适配器（已补客户端接缝）

`DragonBoundBootstrap` 现在保留外部预配置的 `ItemRunSnapshotProvider`，并支持通过
`IItemValidatedProfileSnapshotSource` 注入服务端已验证的玩家 `ItemProfile` 与 AI
`ItemRunSnapshot`。`ItemProfileRunSnapshotProvider` 只调用现有 Profile 校验生成玩家不可变
快照，不计算完成局数、DayKey、库存或奖励；未配置外部 provider 时仍保持原有空快照灰盒行为。

服务端仍需提供实现 `IItemValidatedProfileSnapshotSource` 的接入层；Merchant、Ledger、广告
和奖励权威不在本轮范围内。

本轮新增 `ItemProfileSnapshotProviderTests`，覆盖已验证 Profile 转换、锁定拒绝和上游
服务端错误透传。Unity Targeted/Fast 因当前批处理环境未生成 XML，静态编译已通过。

### 2. 18 个道具仍 Pending

当前正式候选只有 `ITEM_DRAKEHEART_RELIC` 和 `ITEM_WINTERVEIL_RUNE`。其余 18 个在 `ItemDefinitions.cs` 中状态为 `Pending`，Factory 没有对应效果，装备会返回 `PendingImplementation`。这与策划基线一致，不能提前暴露或用占位数值实现。

### 3. Merchant、Lottery、广告、Gold/Energy 没有游戏内实现

`ItemsModuleBoundaryV1.md` 明确声明这些是非目标；`Docs/Handoff/00_StartHere.md` 和 `05_KnownIssues.md` 也明确 Merchant 只是 mock presentation。当前没有真实 Merchant 触发（每 2 次正常完成对局）、3 选 1 失效、广告暂停/验证失败不扣次数、Lottery、Gold 购买、Energy/Expedition Reserve 或 Ledger 幂等流程。

### 4. Item UI 仍是灰盒/占位，不是正式交付 UI

当前 HUD 使用运行时创建的 `UnityEngine.UI.Text` 和 ItemId 字符串；独立 `UI_Handoff` 只提供布局与交互预览。最终图标、Merchant 状态、加载/错误/已领取/过期、广告中断和服务端结果仍待接入。

## 规则/文档冲突

1. `Docs/Drakeforge_Full_Config_Baseline_V2_2026-08-14.md:233,285` 仍写 Item schema version `1`；当前实现和 `ItemSystemV1FoundationVerified.md` 已使用 schema `2`，并显式拒绝 schema 1。必须在主配置基线中统一版本，避免后端按旧结构发送。
2. `Docs/AnalyticsQaValidationReportV1.md:33` 仍称 `ItemProfile` 使用过时解锁条件；当前代码已按第五次正常完成对局实现。该 QA 文档需要标记为历史记录或更新为当前事实。
3. `ItemDailyInventory` 保留 `FragmentCount` 与 `TryAddFragments`（`ItemProfile.cs:62-149`），但 Item 冻结规则是每日单份、Gold/广告领取，不包含 Item 碎片。若不是为未来兼容保留，应从 Item API/持久化结构移除；在确认前不建议改动，以免影响现有测试和契约。

## 测试审计

现有日志（2026-08-17）显示：

| 测试范围 | 结果 | 说明 |
| --- | ---: | --- |
| Item Foundation 10 项（修复前记录） | 10/10 | `Logs/ItemGameplay-Main-Foundation.xml` |
| 持久化恢复回归 | 1/1 | `Logs/ItemPersistenceFix-Targeted.xml` |
| Item Gameplay Integration | 4/4 | `Logs/ItemGameplay-Main-Integration.xml` |
| Fast EditMode（修复后） | 533/533 | `Logs/ItemPersistenceFix-FastEditMode.xml` |
| PlayMode | 27/27 | `Logs/ItemGameplay-Main-PlayMode.xml` |
| ItemGameplay Targeted | **0/0** | `Logs/ItemGameplay-Main-Targeted.xml`；这是筛选未命中，不是有效通过 |

已覆盖：两项效果、解锁/DayKey、快照隔离、冷却、灰盒 HUD；持久化恢复现在覆盖完成局数和恢复后的解锁状态。
未覆盖：真实 Profile/服务器适配、Merchant 完整生命周期、广告暂停和验证失败、Gold/Energy Ledger/幂等、18 个 Pending 效果、最终 UI 与设备布局。

## 建议执行顺序

1. 先修复 Targeted 测试筛选并新增真实 `Profile -> Snapshot` 客户端适配测试；保持当前 10+4 行为不变。
2. 按效果族实现剩余冻结道具，每个道具配 targeted EditMode + 至少一条压力赛集成测试；不改 Boss Production HP。
3. 服务端同事提供 Profile/DayKey/Ledger/广告结果契约后，再接 Merchant、Lottery、Energy、Gold 和幂等结果。
4. UI 同事把占位 HUD/交接 Merchant 接到只读快照与结果状态；最后做 Item+Rune+Boss 的统一平衡回归。
