# Forgekeeper's Gift — 修改方案

> 状态：方案稿（未执行），待用户确认后落地
> 拟定日期：2026-09-12
> 关联文件：
> - 代码：`Assets/Scripts/Merchant/MerchantItemCatalog.cs`
> - 文档：`Docs/ForgekeepersGift_具体实现.md`（**整体过时，待重写**）
> - 工作日志：`.workbuddy/memory/2026-09-12.md`

---

## 0. 用户修正（推翻前一版结论）

| 来源 | 修正前的结论（错） | 修正后的真实设计意图（对） |
|---|---|---|
| 用户原话 | "`GoldPurchasable=false` 是设计意图：该商品是广告限定道具，金币买不到" | "**这件商品可以用金币买到，并且可带入游戏中**；只不过商品的作用是 Run 中每 90 秒弹一次广告页让玩家选 领 / 弃，选完再次进入 90 秒 CD" |
| 推论 | P0 是"补 `IItemForgePickPort` 生产实现" | P0 早已修完——端口已联通；真正的 bug 只有一个：catalog 里错写 `goldPurchasable=false` |

---

## 1. 当前代码事实（重头核实，不是基于旧记忆）

### 1.1 真实可购买性

`Assets/Scripts/Merchant/MerchantItemCatalog.cs:74`

```csharp
Item("ITEM_FORGEGIFTERS_GIFT", "Forgekeeper's Gift", "Forgekeeper's Gift",
     "Legendary", "Passive", 120,
     "Generate one Forge Pick every 90 seconds",
     false),     // ← 这里：goldPurchasable=false 与设计意图不符，应改成 true
```

### 1.2 Run 内 90s CD 弹广告页 / 领 / 弃 / 重置 CD —— 已完整实现

效果类（`Assets/DragonBound/Runtime/Items/ItemEconomyFlowEffects.cs:64-138`）

```
OnRunStart: cooldownRemainingSeconds = FirstForgePickSeconds (90f)
Tick:
  - 倒计时到 0 → context.ForgePick.TryBeginForgePickClaim(HandleClaimResolved)
  - awaitingDecision = true 期间冻结计数（防止双触发）
HandleClaimResolved:
  - GrantedCount++（如果 Granted）
  - 不论 Granted / Declined / Failed → cooldownRemainingSeconds = RepeatForgePickSeconds (90f)
```

UI / 端口（`Assets/Scripts/UI/ForgekeepersGiftPanelController.cs:82-209`）

- `TryBeginForgePickClaim` → 暂停 wave (`Time.timeScale=0`) + `forgePanel.SetActive(true)`
- `videoRewardButton.OnClick` → `rewardedAds.ShowAsync("forgekeepers_gift")` → 看完发铲子 `TrySpawnRewardShovel` → `Granted`
- `closeButton.OnClick` → `Declared`（立即关弹板，不发奖）
- 任何分支都调 `FinishClaim(...)` → `pendingCompletion.Invoke(result)` → 触发上面 `HandleClaimResolved` 重置 CD

### 1.3 端口已经注入（之前的"P0 接口孤岛"判断也已过期）

`Assets/DragonBound/Runtime/Core/TwentyWavePressureRuntime.cs:658-681`

```csharp
playerItems = new ItemRunRuntime(
    playerSnapshot, match.Player, player.Registry, player.ItemUnits,
    runSeed, match.AI, ai.Registry,
    freeRecruit: playerFreeRecruit,
    forgePick: playerForgePick,                       // ← 已注入
    itemEnemyDamage: player,
    startActiveItemsOnCooldown: true);
aiItems = new ItemRunRuntime(
    aiSnapshot, match.AI, ai.Registry, ai.ItemUnits,
    runSeed, match.Player, player.Registry,
    freeRecruit: aiFreeRecruit,
    forgePick: aiForgePick,                          // ← 已注入
    itemEnemyDamage: ai,
    startActiveItemsOnCooldown: true);
```

`Assets/DragonBound/Runtime/Bootstrap/DragonBoundBootstrap.cs:72` / `:720-730`

```csharp
public ItemForgePickPortRouter PlayerForgePickPort { get; } = new ItemForgePickPortRouter();
...
TwentyWave = new TwentyWavePressureRuntime(
    Match, RecruitDestination, AiRecruitDestination, waveRandomSeed,
    playerRuneRewards: PlayerRuneRewards,
    itemSnapshotProvider: ItemRunSnapshotProvider,
    playerForgePick: PlayerForgePickPort,            // ← 注入 router（非 null）
    playerFreeRecruit: Recruitment,
    aiFreeRecruit: AiRecruitment,
    aiRuneRewards: AiRuneRewards);
```

`ForgekeepersGiftPanelController.Awake`（L31-64）通过 `bootstrap.BindPlayerForgePickPort(this)` 把自身注册到 `PlayerForgePickPort.Provider`——满足先 bootstrap 构造再 UI 注册的顺序（router 模式）。

### 1.4 抽奖池当前就允许该商品

`Assets/Scripts/Merchant/LocalMerchantGateway.cs:512-549`

```
CreateLotteryOffer(state, merchantOffer):
  candidates = []
  foreach catalogProduct in MerchantItemCatalog.All:    ← 走全集
    if not in excludedIds:
      candidates.Add(MerchantItemCatalog.Find(catalogProduct.ProductId))
```

不依赖 `GoldPurchasable`——所以即便 catalog 的 flag 改成 true，**抽奖可用性不变**。

---

## 2. 修改清单（按优先级）

### ✅ P0 — 必须改

#### 2.1 `MerchantItemCatalog.cs:74` — 把 `false` 改成 `true`

```diff
-Item("ITEM_FORGEGIFTERS_GIFT", "Forgekeeper's Gift", "Forgekeeper's Gift", "Legendary", "Passive", 120, "Generate one Forge Pick every 90 seconds", false),
+Item("ITEM_FORGEGIFTERS_GIFT", "Forgekeeper's Gift", "Forgekeeper's Gift", "Legendary", "Passive", 120, "Generate one Forge Pick every 90 seconds", true),
```

**影响面（已逐条核对）：**

| 路径 | 修前 | 修后 |
|---|---|---|
| `GetGoldCandidates()` L116-124 | 不返回 | 返回，进入 8 件主商品候选池 |
| `CreateOffer` L473-510 `TakeWeightedRewardedAdProduct` (罕见，玩家用看视频抽) | 可能被抽成广告位 | 同左，但候选变多，权重不变 |
| `CreateLotteryOffer` L512-549 | 已能抽中（走 `All`） | 同左 |
| UI 价签 `MainMerchantController.cs:439-441` | "120" | "120"（无变化） |
| "点 Buy → Success" | 不进金币候选 → "点 A=Gone" | 进金币候选 → 正常扣金币 → "Sold out" |

#### 2.2 `Docs/ForgekeepersGift_具体实现.md` — 整体重写

文档当前描述的是**早期版本**的接口与代码（同步 `TryGrantForgePick(bool requiresAdvertisement)`，`ForgekeepersGiftEffect` 的 `while` 累积循环，`TwentyWavePressureRuntime` 没注入 forgePick），与今天的代码完全不一致。重写框架：

- **L5**：改"仅广告可购（GoldPurchasable=false）" → "120 金可购（GoldPurchasable=true）"
- **§2 效果实现**：改用今天 `ForgekeepersGiftEffect` 的 `cooldownRemainingSeconds + awaitingDecision + HandleClaimResolved` 模式
- **§3 接口契约**：改用 `IItemForgePickPort.TryBeginForgePickClaim(Action<ItemForgePickClaimResult>)` + `ItemForgePickRequestKind/ClaimKind` 双枚举
- **§5 商店**：加 8 件主商品位 / 抽奖池 / 广告位 三路径表格，明确"金币买 8 件主商品即可"
- **§7 端口注入路径**：旧"⚠ 未注入 forgePick"改成"✅ 已注入：router + panel controller 注册"流程图
- **§11 与其它 Passive 道具对比**：表格里 ForgekeepersGift 那行删除"静默退出"备注
- **§12 待办**：删除"P0 补生产实现"那一项（已完成）；保留 typo / 服务端对账 / 商店描述精度等小项

---

### ⚪ P1 — 强烈建议改（用户没明示，但和 P0 一起改很自然）

#### 2.3 商品描述文案精度

`MerchantItemCatalog.cs:50` 英文介绍

```diff
-{ "ITEM_FORGEGIFTERS_GIFT", "Generate one Forge Pick every 90 seconds" },
+{ "ITEM_FORGEGIFTERS_GIFT", "Every 90s, optionally watch an ad for 1 free Shovel; you may decline." },
```

中文版的描述（位于 `MainMerchantController` / Localization 字典）需要同步翻译。**商品行为已被玩家理解成"被动收铲"，文案不准确**；改成"主动看广告"避免老玩家买完后困惑"为什么我什么都没看到就在铲子 +1"。

#### 2.4 `ItemEconomyFlowEffects.cs:64-109` 暴露"决策状态" / 单元测试更新

现有 `cooldownRemainingSeconds` / `awaitingDecision` / `stoppedForNoLockedCell` 是 private 字段，只通过方法间接观察。建议：

- 增加 `bool IsAwaitingDecision { get; }` / `bool IsStopped { get; }` 让 UI 能画出"倒计时 / 决策面板 / 已停止"三态指示
- 给 `ForgekeepersGiftEffect` 写单测覆盖三路径：① Granted 路径 → CD 重置；② Declined 路径 → CD 重置；③ Failed 路径 → CD 重置
- 当前测试桩 `ItemEconomyFlowEffectsTests.cs:66-85` 是旧 API（`Tick` 累积循环）；改成基于今天的 `TryBeginForgePickClaim` 异步 / await 模式

---

### ⚪ P2 — 等用户决定，先不动

#### 2.5 typo `ITEM_FORGEGIFTERS_GIFT` → `ITEM_FORGEKEEPERS_GIFT`

当前共 4 处：
- `MerchantItemCatalog.cs:25` IconKey 字典键
- `MerchantItemCatalog.cs:50` EnglishIntroductions 字典键
- `MerchantItemCatalog.cs:74` `Item(...)` 第一参数
- `DevelopmentItemRunSnapshotProvider.cs:16` LegacyToRuntime 字典键（旧→新映射）

`DevelopmentItemRunSnapshotProvider.cs:16` 字典已经把旧拼写自动映射到 `ItemIds.ForgekeepersGift` (`ITEM_FORGEKEEPERS_GIFT`)，**所以现在功能上没事**，只是商店里看着别扭。修一遍要动 4 个地方、改 enum-lookups 的字典键、改所有依赖 catalog id 字符串的解码点，风险高于收益。**先不动**，等用户明确要求再做。

#### 2.6 服务端对账

`ItemRunRuntime.Context.ForgePick` 已联通，但 `ItemRunRuntime` 现在只在 `UseResolved` 上报商品激活结果，不上报 `ForgekeepersGiftEffect.AttemptCount/GrantedCount`。如果服务端 `GoRunFinishRequest.event_summary` 想统计该 Run 内锻造了几次铲子，需要：

- 在 `ItemRunRuntime.SnapshotLocked` 或新增事件 `ForgekeepersGiftOutcome`（在 `HandleClaimResolved` 里 fire）
- 在 `GameSettlementCoordinator` 汇总时带上这俩数字

**不确定项**：服务端目前是否要求 Forge Pick 次数上报才能 finish（需要 `@同事` 确认）。先写一个 TODO，等回答了再补。

---

## 3. 落地动作（按清单顺序）

1. **改代码**：`MerchantItemCatalog.cs:74` `false` → `true`
2. **改描述**：`MerchantItemCatalog.cs:50` 英文介绍同步更新（可选）
3. **改文档**：整段重写 `Docs/ForgekeepersGift_具体实现.md`，目标与今天的代码 1:1 对齐
4. **加单测**：覆盖 ForgekeepersGiftEffect 三路径（Granted/Declined/Failed）
5. **改 MEMORY.md**（Dragon）：在"调试/结算链路"或新加一条"商品可购买性约定"，记录：`ITEM_FORGEGIFTERS_GIFT` 是金币可买 + Run 内 90s 弹广告的混合型商品。`GoldPurchasable=true` 是设计意图，**不是**bug。
6. **更新工作日志** `2026-09-12.md`：追加"用户修正 + 修正了前次 P0 误判"
7. **GameplayVerify**：用 GreyboxMerchantLoadoutController 装载 `ITEM_FORGEGIFTERS_GIFT`，跑一局对局，等 90s 看是否弹 forge panel；点视频按钮发铲子、再点 close 验证 CD 重置

---

## 4. 风险评估

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| P0 改完，8 件面板出现该商品，与"广告位商品"广告配额冲突 | 低 | 玩家重复刷 | 抽样已有类似广告位商品（如 Dragonfall_Judgment），确认配额按 placement 隔离 |
| 改完通过 `Lottery` 抽奖中出现，与已售 8 件不冲突机制是否仍然正确 | 低 | 可能抽到重商品 | LocalMerchantGateway 已有 `state.OwnedProductIds` 过滤 |
| 文档重写没改全（行号引用过时） | 中 | 误导后续读者 | 走真实 README 草稿后再发，先加一行"最后核对日期"标注 |
| typo 不修，旧 alias 字典清理时漏改 | 中 | 编译报错 | 暂不动（用户没要求），如要动需同步 4 处 |
| 真实跑局验证发现 panel 在 wave 暂停后又自动 resume 出 bug | 低 | 广告被跳过 | ForgekeepersGiftPanelController L195-201 已经处理 |

---

## 5. 速查表（修改前 / 后对比）

| 维度 | 修改前（当前错误） | 修改后（设计意图） |
|---|---|---|
| `MerchantItemCatalog.cs:74` 第 7 参 | `false` | `true` |
| 商品在 8 件面板金币候选 | ✗ | ✓ |
| 商品在 8 件面板广告位 | ✗（never picked） | ✓（按 Legendary 权重 40% 中签） |
| 商品在抽奖池 | ✓（lottery 走 All） | ✓ |
| 商品在 Game 内 Run：装备后 Tick | ✓（端口已注入） | ✓ |
| 行为：90s CD 后 | ✓ 弹 forge panel | ✓ 弹 forge panel |
| 行为：玩家看视频 | ✓ 发铲 + 重置 CD | ✓ |
| 行为：玩家点关闭 | ✓ 不发 + 重置 CD | ✓ |
| 文档 `ForgekeepersGift_具体实现.md` | 严重过时（描述旧 API） | 待重写 |
