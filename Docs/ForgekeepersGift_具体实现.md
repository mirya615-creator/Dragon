# Forgekeeper's Gift — 具体实现文档

> 道具 ID:`ITEM_FORGEKEEPERS_GIFT`(运行时)/ `ITEM_FORGEGIFTERS_GIFT`(商店/旧版拼写,带 typo)
> 显示名:Forgekeeper's Gift(英)/ 锻造者之礼(中)
> 稀有度:Legendary | 类型:Passive | 价格:120 金(列表价)/ **仅广告可购**(`GoldPurchasable=false`)
> 图标:`ItemUI/18`

---

## 1. 系统全景

```
                              ┌─────────────────────────────────────┐
                              │  ItemEffectRuntimeFactory           │
                              │   :465 → () => new                  │
                              │       ForgekeepersGiftEffect()      │
                              └────────────────┬────────────────────┘
                                               │
        ┌──────────────────────────────────────┼──────────────────────────────────────┐
        ▼                                      ▼                                      ▼
 ItemDefinitions.cs          ItemIdentityContracts.cs              MerchantItemCatalog.cs
  :177-178                     :60,:110                              :25,:50,:74
 Passive/Legendary/           Passive(ForgekeepersGift,             Legendary/Passive/120
 Implemented                  Configured)                           goldPurchasable=false
 ItemEffectKind.              ────────────────────────────────────   "Generate one Forge
 ForgekeepersGift                                                       Pick every 90s"
 IconKey="ItemUI/18"
        │                                      │                                      │
        └──────────────────────┬───────────────┴──────────────────────┬───────────────┘
                               ▼                                      ▼
                  ┌────────────────────────┐              ┌──────────────────────────────┐
                  │ ItemRunRuntime         │              │ MainMerchantController.cs   │
                  │  (TwentyWavePressure   │              │  :475-503                   │
                  │   Runtime.cs:626-645)  │              │  PaymentType==RewardedAd →  │
                  │  ⚠ 未注入 forgePick:  │              │   rewardedAdService.Show +   │
                  │   端口实际为 null      │              │   ClaimRewardedAdProduct    │
                  └────────────┬───────────┘              └──────────────────────────────┘
                               │
                               ▼
                ┌────────────────────────────────┐
                │ ItemRunContext.ForgePick       │
                │ (IItemForgePickPort)           │
                │ ⚠ 接口无生产实现,仅测试桩:    │
                │   ItemEconomyFlowEffectsTests  │
                │   .cs:109 ForgePickPort        │
                └────────────────────────────────┘
```

---

## 2. 核心效果实现

### 2.1 `Assets/DragonBound/Runtime/Items/ItemEconomyFlowEffects.cs:64-109`

```csharp
/// <summary>Ad-gated Forge Pick schedule. The provider decides authority and locked-cell state.</summary>
public sealed class ForgekeepersGiftEffect : IItemEffectRuntime
{
    public const float FirstForgePickSeconds = 90f;
    public const float RepeatForgePickSeconds = 90f;

    private float nextDueSeconds = FirstForgePickSeconds;
    private bool stoppedForNoLockedCell;

    public string ItemId => Items.ItemIds.ForgekeepersGift;
    public int AttemptCount { get; private set; }
    public int GrantedCount { get; private set; }
    public bool StoppedForNoLockedCell => stoppedForNoLockedCell;

    public void OnRunStart(ItemRunContext context)
    {
        nextDueSeconds = FirstForgePickSeconds;
        stoppedForNoLockedCell = false;
    }

    public void Tick(ItemRunContext context, float deltaSeconds)
    {
        if (stoppedForNoLockedCell || context.ForgePick == null || deltaSeconds <= 0f) return;
        var target = context.ElapsedSeconds;
        while (target + 0.0001f >= nextDueSeconds)
        {
            AttemptCount++;
            var result = context.ForgePick.TryGrantForgePick(requiresAdvertisement: true);
            if (result.Granted) GrantedCount++;
            if (result.Kind == ItemForgePickResultKind.NoLockedCell)
            {
                stoppedForNoLockedCell = true;
                break;
            }
            nextDueSeconds += RepeatForgePickSeconds;
        }
    }

    public bool TryActivate(ItemRunContext context, out string reason)
    {
        reason = "PassiveOnly";
        return false;
    }

    public void HandleCombatEvent(ItemRunContext context, ItemCombatEvent combatEvent) { }
}
```

**关键设计要点**:

| 要点 | 行为 |
|---|---|
| **首次触发** | Run 开始后 90 秒(由 `OnRunStart` 重置 `nextDueSeconds = 90f`) |
| **循环触发** | 每 90 秒一次,`while (target + 0.0001f >= nextDueSeconds)` 累积循环 — 即一帧内补齐多次累积触发 |
| **停止条件** | 一旦端口返回 `NoLockedCell` 即**永久停止**本 Run 后续所有 Forge Pick 请求(`stoppedForNoLockedCell = true`,`OnRunStart` 才重置) |
| **广告契约** | 始终以 `requiresAdvertisement: true` 调用 — 强制走广告入口 |
| **主动激活** | 永远 `PassiveOnly` — 玩家不能主动点击触发 |
| **战斗事件** | 不响应任何战斗事件(`HandleCombatEvent` 空实现) |

---

## 3. 接口契约(Authority Boundary)

### 3.1 `Assets/DragonBound/Runtime/Items/ItemRuntime.cs:248-273`

```csharp
public interface IItemForgePickPort
{
    ItemForgePickResult TryGrantForgePick(bool requiresAdvertisement);
}

public enum ItemForgePickResultKind
{
    Granted,            // 成功发奖
    NoLockedCell,       // 棋盘无锁定格 → 触发停止
    AdvertisementRequired,  // 广告未看完/未上报
    AuthorityUnavailable,   // 服务端/Ledger/Merchant 未连接
    Rejected                // 其他拒绝
}

public readonly struct ItemForgePickResult
{
    public ItemForgePickResult(ItemForgePickResultKind kind, string reason = "") { ... }
    public ItemForgePickResultKind Kind { get; }
    public string Reason { get; }
    public bool Granted => Kind == ItemForgePickResultKind.Granted;
}
```

**5 种返回码含义**(注释推断,基于契约设计意图):

| Kind | 何时返回 | 道具侧反应 |
|---|---|---|
| `Granted` | 实际发放 1 个 Forge Pick | `GrantedCount++`,继续 90s 后再次尝试 |
| `NoLockedCell` | 棋盘上没有 Locked Cell 可投放 | `stoppedForNoLockedCell=true`,**永久停止** |
| `AdvertisementRequired` | 广告未完整观看/未上报成功事件 | 不计入 Granted,继续 90s 后重试 |
| `AuthorityUnavailable` | 服务端/Ledger/Merchant 未连接 | 同上,继续重试 |
| `Rejected` | 其他业务校验失败 | 同上,继续重试 |

---

## 4. 注册与工厂

### 4.1 Item 标识符(`Assets/DragonBound/Runtime/Items/Runtime/ItemDefinitions.cs`)

```csharp
// L70
public const string ForgekeepersGift = "ITEM_FORGEKEEPERS_GIFT";

// L48
ItemEffectKind { ..., ForgekeepersGift }

// L136
{ ItemIds.ForgekeepersGift, "Forgekeeper's Gift" }

// L177-178 — 唯一一条事实源
Define(ItemIds.ForgekeepersGift, ItemCategory.Passive, ItemRarity.Legendary,
    ItemImplementationStatus.Implemented, ItemEffectKind.ForgekeepersGift, "ItemUI/18"),
```

### 4.2 合约层(`Assets/DragonBound/Runtime/Items/Contracts/ItemIdentityContracts.cs`)

```csharp
// L60
public static readonly ItemId ForgekeepersGift = new ItemId("ITEM_FORGEKEEPERS_GIFT");

// L69 - 顺序排列在 Legendary 区间
ForgeTreasury, BattlefieldCommand, ForgekeepersGift, DragonfallJudgment, ...

// L110
Passive(ItemIds.ForgekeepersGift, ItemConfigurationState.Configured),
```

### 4.3 工厂装配(`Assets/DragonBound/Runtime/Items/ItemRuntime.cs:465`)

```csharp
{ ItemEffectKind.ForgekeepersGift, () => new ForgekeepersGiftEffect() }
```

### 4.4 构造函数签名(`ItemRuntime.cs:492-507`)

```csharp
public ItemRunRuntime(
    ItemRunSnapshot snapshot,
    TeamState ownTeam,
    EnemyRegistry ownRouteEnemies,
    ItemCombatUnitRegistry unitRegistry = null,
    int runSeed = 0,
    TeamState opposingTeam = null,
    EnemyRegistry opposingRouteEnemies = null,
    ItemCombatUnitRegistry opposingUnitRegistry = null,
    IItemBenchCapacityPort benchCapacity = null,
    IItemSpellbreakerPort spellbreaker = null,
    IItemRunResourcePort runResource = null,
    IItemFreeRecruitPort freeRecruit = null,
    IItemForgePickPort forgePick = null,             // ← 关键端口(默认 null)
    IItemEnemyDamagePort itemEnemyDamage = null,
    bool startActiveItemsOnCooldown = false)
```

`ItemRunContext` 通过该构造器把 `forgePick` 存到 `public IItemForgePickPort ForgePick { get; }`(`ItemRuntime.cs:319`),再由 `ForgekeepersGiftEffect.Tick` 读取。

---

## 5. 商店 / 旧版 ID 兼容

### 5.1 `Assets/Scripts/Merchant/MerchantItemCatalog.cs:74`

```csharp
Item("ITEM_FORGEGIFTERS_GIFT", "Forgekeeper's Gift", "Forgekeeper's Gift",
     "Legendary", "Passive", 120,
     "Generate one Forge Pick every 90 seconds",
     false),     // ← goldPurchasable=false,只走广告
```

| 行号 | 内容 |
|---|---|
| `MerchantItemCatalog.cs:25` | `{ "ITEM_FORGEGIFTERS_GIFT", "ItemUI/18" }`(IconKey) |
| `MerchantItemCatalog.cs:50` | `{ "ITEM_FORGEGIFTERS_GIFT", "Generate one Forge Pick every 90 seconds" }`(英文描述) |
| `MerchantItemCatalog.cs:74` | 唯一一条 `Item(...)` 记录,**最后一个参数 `false` = `goldPurchasable=false`** |

### 5.2 `MerchantItemCatalog.cs:116-124` — `GetGoldCandidates()`

```csharp
public static List<MerchantProduct> GetGoldCandidates()
{
    var candidates = new List<MerchantProduct>();
    foreach (MerchantProduct product in Products)
    {
        if (product.GoldPurchasable) candidates.Add(Clone(product));
    }
    return candidates;
}
```

→ 因为 `GoldPurchasable=false`,Forgekeeper's Gift **不会进入金币候选池**。

### 5.3 广告候选选择(`LocalMerchantGateway.cs:567-606`)

```csharp
private static MerchantProduct TakeWeightedRewardedAdProduct(List<MerchantProduct> candidates)
{
    string[] rarities = { "Rare", "Excellent", "Epic", "Legendary" };
    int[] weights = { 10, 20, 30, 40 };   // Legendary 权重最高 40
    ...
    int roll = UnityEngine.Random.Range(0, availableWeight);
    ...
    int candidateIndex = rarityCandidateIndices[
        UnityEngine.Random.Range(0, rarityCandidateIndices.Count)];
    ...
}
```

`CreateOffer`(`LocalMerchantGateway.cs:482-487`)先调 `TakeWeightedRewardedAdProduct` 抽 1 个广告位商品,被抽中的会强制改 `PaymentType = MerchantPaymentType.RewardedAd`、`AdPlacementId = "merchant_rewarded_item"`。**Forgekeeper's Gift 因 `GoldPurchasable=false` 而留在候选池中,有概率(随 Legendary 权重)被抽成广告位商品**。

### 5.4 旧版 ID → 新版 ID 字典

**5.4.1 加载用**(`Assets/DragonBound/Runtime/Items/DevelopmentItemRunSnapshotProvider.cs:16`)

```csharp
{ "ITEM_FORGEGIFTERS_GIFT", ItemIds.ForgekeepersGift },   // ← 旧拼写自动映射
```

同文件 L11-15 还映射了其它 5 个旧 ID:

| 旧 ID | → 新 ID |
|---|---|
| `ITEM_MANABURST_MINE` | `RuneburstMine` |
| `ITEM_RUNE_TEMPERING` | `RuneOfTempering` |
| `ITEM_PACT_ENDURANCE` | `PactOfEndurance` |
| `ITEM_VETERAN_MARK` | `VeteransMark` |
| `ITEM_QUARTERMASTER_SATCHEL` | `QuartermastersSatchel` |
| **`ITEM_FORGEGIFTERS_GIFT`** | **`ForgekeepersGift`** |

加载器:`GreyboxMerchantLoadoutController.cs`(`Assets/Scripts/Merchant/`)通过 `MerchantItemSnapshotFactory.LegacyToRuntime` 字典自动兼容。

---

## 6. 商店购买流程(广告兑换路径)

### 6.1 UI 层(`Assets/Scripts/Merchant/MainMerchantController.cs:475-503`)

```csharp
if (product.PaymentType == MerchantPaymentType.RewardedAd)
{
    string placementId = string.IsNullOrWhiteSpace(product.AdPlacementId)
        ? MerchantAdPlacement      // "merchant_rewarded_item"
        : product.AdPlacementId;
    RewardedAdResult adResult = await rewardedAdService.ShowAsync(
        placementId, lifetimeCancellation.Token);
    if (adResult != RewardedAdResult.Completed) return;

    string rewardScope = offer.OfferId + "|" + product.ProductId;
    string adEventId = PendingAdEventStore.GetOrCreate(
        playerId, placementId, rewardScope);
    result = await merchantGateway.ClaimRewardedAdProductAsync(
        playerId, offer.OfferId, product.ProductId,
        placementId, adEventId, adEventId, lifetimeCancellation.Token);
    ...
}
```

### 6.2 价格显示(`MainMerchantController.cs:439-441`)

```csharp
priceText.text = product.PaymentType == MerchantPaymentType.RewardedAd
    ? "Video"                  // 广告位显示 "Video" 而非金币数
    : product.GoldPrice.ToString(CultureInfo.InvariantCulture);
```

### 6.3 广告位 ID 常量(`LocalMerchantGateway.cs:19-20`)

```csharp
private const string MerchantAdPlacement = "merchant_rewarded_item";     // 商品
private const string MerchantLotteryAdPlacement = "merchant_lottery";    // 抽奖
```

---

## 7. 端口注入路径(关键缺位)

### 7.1 接口消费方

```csharp
// ItemEconomyFlowEffects.cs:85
if (stoppedForNoLockedCell || context.ForgePick == null || deltaSeconds <= 0f) return;
//                                ^^^^^^^^^^^^^^^^^^^^^
//                                没有实现就静默退出,什么都不发生
```

### 7.2 生产入口(`Assets/DragonBound/Runtime/Core/TwentyWavePressureRuntime.cs:626-645`)

```csharp
playerItems = new ItemRunRuntime(
    playerSnapshot,
    match.Player,
    player.Registry,
    player.ItemUnits,
    runSeed: runSeed,
    opposingTeam: match.AI,
    opposingRouteEnemies: ai.Registry,
    itemEnemyDamage: player,
    startActiveItemsOnCooldown: true);     // ← 没传 forgePick:,默认 null

aiItems = new ItemRunRuntime(
    aiSnapshot,
    match.AI,
    ai.Registry,
    ai.ItemUnits,
    runSeed: runSeed,
    opposingTeam: match.Player,
    opposingRouteEnemies: player.Registry,
    itemEnemyDamage: ai,
    startActiveItemsOnCooldown: true);     // ← 没传 forgePick:,默认 null
```

→ **生产代码从未注入任何 `IItemForgePickPort` 实现**,道具虽然已装备,但 `Tick` 在每帧 `context.ForgePick == null` 处提前退出,**永远不会触发任何广告或 Forge Pick 申请**。

### 7.3 服务端网关占位(`Assets/Scripts/Services/Go/GoServerInventoryGateways.cs:320-352`)

```csharp
public async Task<MerchantPurchaseResult> ClaimRewardedAdProductAsync(...)
{
    await GoPlayerEnergyGateway.SendClientAdSuccessAsync(...);  // ← 仅广告事件上报
    return await PurchaseAsync(...);                             // ← 走金币购买同条路径
}
```

→ 这是**商店兑换**路径,不是 Run 中广告型 Forge Pick 的实现。

---

## 8. 测试覆盖

### 8.1 `Assets/DragonBound/Tests/EditMode/ItemEconomyFlowEffectsTests.cs:66-85`

```csharp
[Test]
public void ForgekeepersGift_RequestsAtNinetySecondIntervalsAndStopsOnNoLockedCell()
{
    var forgePick = new ForgePickPort();
    var runtime = new ItemRunRuntime(
        CreateForgekeepersSnapshot(),
        new TeamState(TeamSide.Player), new EnemyRegistry(),
        forgePick: forgePick);

    Assert.IsTrue(runtime.StartRun(out var reason), reason);
    runtime.Tick(89.9f);
    Assert.AreEqual(0, forgePick.RequestCount);          // 89.9s: 还没到 90s
    runtime.Tick(0.1f);
    Assert.AreEqual(1, forgePick.RequestCount);          // 90.0s: 第 1 次
    Assert.AreEqual(1, forgePick.GrantedCount);
    runtime.Tick(90f);
    Assert.AreEqual(2, forgePick.RequestCount);          // 180s: 第 2 次 → NoLockedCell
    Assert.IsTrue(forgePick.NoLockedCellReturned);
    runtime.Tick(90f);
    Assert.AreEqual(2, forgePick.RequestCount);          // 270s: 停止后续调用
}
```

### 8.2 测试桩(`ItemEconomyFlowEffectsTests.cs:109-127`)

```csharp
private sealed class ForgePickPort : IItemForgePickPort
{
    public int RequestCount { get; private set; }
    public int GrantedCount { get; private set; }
    public bool NoLockedCellReturned { get; private set; }

    public ItemForgePickResult TryGrantForgePick(bool requiresAdvertisement)
    {
        RequestCount++;
        if (RequestCount == 1)
        {
            GrantedCount++;
            return new ItemForgePickResult(ItemForgePickResultKind.Granted);
        }
        NoLockedCellReturned = true;
        return new ItemForgePickResult(ItemForgePickResultKind.NoLockedCell);
    }
}
```

> **⚠ 这是项目里 `IItemForgePickPort` 接口的唯一一个实现,且仅在测试中**。
> 生产代码需要补一个真实实现,把 Forge Pick 生成路由到 `MerchantItemCatalog` 的广告兑换或独立的 `ShovelRecruitmentState` 衍生路径。

### 8.3 快照准备(`ItemEconomyFlowEffectsTests.cs:87-96`)

```csharp
private static ItemRunSnapshot CreateForgekeepersSnapshot()
{
    var profile = new ItemProfile();
    Assert.IsTrue(profile.RefreshDay(new FixedDayKey(), out _));
    Assert.IsTrue(profile.RefreshAuthoritativeAccountProgress(new FixedProgress(), out _));
    Assert.IsTrue(profile.Inventory.TryGrantOwned(ItemIds.ForgekeepersGift));
    Assert.IsTrue(profile.Loadout.TryEquip(ItemIds.ForgekeepersGift, profile.Inventory, out _));
    Assert.IsTrue(profile.TryCreateRunSnapshot(out var snapshot, out _));
    return snapshot;
}
```

---

## 9. 设计文档对账

### 9.1 `Docs/DragonBound/ItemSystemV1FoundationVerified.md:46`(原文)

> Battlefield Command and Forgekeeper's Gift are not connected to a server, advertising, Ledger, Merchant, or live Recruit authority: **absent or rejected ports never report a grant**.

→ 这条已确认:**接口契约已就位、工厂已注册、但端口从未被生产代码连接**。

### 9.2 `Docs/道具清单与作用.md:122`(原文)

> `ITEM_FORGEKEEPERS_GIFT` | Forgekeeper's Gift | 锻造者之礼 | Legendary | **RunStart 90s 后,每 90s 生成一次广告型 Forge Pick**;无锁定格则停止 | `ItemEconomyFlowEffects.cs:64-109`(`goldPurchasable=false`,只能广告兑换)

→ 与代码完全一致。

---

## 10. 上下游对照表

| 主题 | 上游 | 本道具 | 下游 |
|---|---|---|---|
| **商店列表价** | `MerchantItemCatalog.cs:74`(120 金 / `goldPurchasable=false`) | — | — |
| **商店购买** | `MainMerchantController.cs:475-503` 调 `rewardedAdService.ShowAsync` + `ClaimRewardedAdProductAsync` | — | `LocalMerchantGateway.cs:408-471`(`AdVerificationFailed` 校验 placementId) |
| **服务端广告兑换** | `GoServerInventoryGateways.cs:320-352`(`SendClientAdSuccessAsync` 上报 + `PurchaseAsync` 走金币同条) | — | 服务端 Ledger |
| **旧 ID 兼容** | `DevelopmentItemRunSnapshotProvider.cs:16`(`ITEM_FORGEGIFTERS_GIFT → ForgekeepersGift`) | — | `GreyboxMerchantLoadoutController.cs` |
| **Run 中触发** | `TwentyWavePressureRuntime.cs:626-645` 创建 `ItemRunRuntime`(**未注入 forgePick**) | `ItemEconomyFlowEffects.cs:64-109` 每 90s 调一次 `IItemForgePickPort` | ❌ **缺失生产实现**,见 §7.2 |
| **测试桩** | `ItemEconomyFlowEffectsTests.cs:109-127`(`ForgePickPort`) | 同 | 单测 `ForgekeepersGift_RequestsAtNinetySecondIntervalsAndStopsOnNoLockedCell` |

---

## 11. 与其它经济型 Passive 道具的对比

| 道具 | 接口 | 端口来源 | 触发条件 | 端口未注入时的行为 |
|---|---|---|---|---|
| `ForgeTreasury`(锻造金库) | `IItemRunResourcePort.TryGrant` | `TeamStateRunResourcePort`(默认,`ItemRuntime.cs:301`) | 每 10 个合法击杀 | **仍生效**(默认端口内置) |
| `BattlefieldCommand`(战场指挥) | `IItemFreeRecruitPort.TryGrantFreeRecruit` | **无默认**(`ItemRuntime.cs:302` `freeRecruit = null`) | 首个 Hero 编队完成 | `context.FreeRecruit == null` 时早 return,AttemptCount++ 但不计入 Granted |
| **`ForgekeepersGift`(本道具)** | `IItemForgePickPort.TryGrantForgePick` | **无默认**(`ItemRuntime.cs:303` `forgePick = null`) | Run Start 后每 90s | **静默退出**,完全不触发 |

→ 三个经济型 Passive 中,只有 `ForgeTreasury` 是自给自足(`TeamState` 内置端口),另外两个都需要外部注入端口才能产生效果。

---

## 12. 待办与缺口

> 这是设计契约层已就位、但生产层未连通的典型"接口孤岛"。

1. **P0 — 补生产 `IItemForgePickPort` 实现**
   - 决定走哪条权威:`MerchantItemCatalog` 现有广告兑换链 / 新建独立的 `ShovelForgePickService` / 服务端路由
   - 在 `TwentyWavePressureRuntime.StartItemRuntimes`(`TwentyWavePressureRuntime.cs:626-645`)创建 `ItemRunRuntime` 时注入
   - 同步在 `MainMerchantController` 商店兑换流程中,Run 中的 Forge Pick 申请要走相同的 Ledger 通道(避免重复发奖)

2. **P1 — 确认广告契约一致性**
   - 商店兑换(`MainMerchantController.cs:480`)placementId=`"merchant_rewarded_item"`
   - Run 中触发(`ItemEconomyFlowEffects.cs:90`)仅以 `requiresAdvertisement: true` 标记,未指定 placementId
   - 两者是否走同一广告配额?同一 `PendingAdEventStore`?需要确认 `MerchantAdPlacement` 与 Run-side 的广告配额是否独立计数

3. **P2 — 服务端对账**
   - 现有 `GoRunFinishRequest` 上报 `event_summary` 是否包含 Forge Pick 生成次数?
   - 服务端校验逻辑是否依赖该字段?
   - 见 `Docs/随机流协议分析.md`(若有)

---

## 13. 速查表(代码定位)

| 概念 | 文件 | 行号 |
|---|---|---|
| ID 常量 | `Assets/DragonBound/Runtime/Items/Runtime/ItemDefinitions.cs` | 70 |
| 枚举值 | `Assets/DragonBound/Runtime/Items/Runtime/ItemDefinitions.cs` | 48 |
| 英文名 | `Assets/DragonBound/Runtime/Items/Runtime/ItemDefinitions.cs` | 136 |
| 目录注册 | `Assets/DragonBound/Runtime/Items/Runtime/ItemDefinitions.cs` | 177-178 |
| 合约 ID | `Assets/DragonBound/Runtime/Items/Contracts/ItemIdentityContracts.cs` | 60 |
| 合约注册 | `Assets/DragonBound/Runtime/Items/Contracts/ItemIdentityContracts.cs` | 110 |
| 工厂注册 | `Assets/DragonBound/Runtime/Items/ItemRuntime.cs` | 465 |
| 效果实现 | `Assets/DragonBound/Runtime/Items/ItemEconomyFlowEffects.cs` | 64-109 |
| 端口接口 | `Assets/DragonBound/Runtime/Items/ItemRuntime.cs` | 248-251 |
| 结果枚举 | `Assets/DragonBound/Runtime/Items/ItemRuntime.cs` | 253-260 |
| 结果 struct | `Assets/DragonBound/Runtime/Items/ItemRuntime.cs` | 262-273 |
| Context 字段 | `Assets/DragonBound/Runtime/Items/ItemRuntime.cs` | 289,303,319 |
| 构造器签名 | `Assets/DragonBound/Runtime/Items/ItemRuntime.cs` | 492-507 |
| 商店 ID / IconKey | `Assets/Scripts/Merchant/MerchantItemCatalog.cs` | 25 |
| 商店描述 | `Assets/Scripts/Merchant/MerchantItemCatalog.cs` | 50 |
| 商店商品行 | `Assets/Scripts/Merchant/MerchantItemCatalog.cs` | 74 |
| 金币候选过滤 | `Assets/Scripts/Merchant/MerchantItemCatalog.cs` | 116-124 |
| 旧 ID 映射 | `Assets/DragonBound/Runtime/Items/DevelopmentItemRunSnapshotProvider.cs` | 16 |
| 广告位常量 | `Assets/Scripts/Merchant/LocalMerchantGateway.cs` | 19-20 |
| 广告商品选择 | `Assets/Scripts/Merchant/LocalMerchantGateway.cs` | 567-606 |
| 商店兑换路径 | `Assets/Scripts/Merchant/MainMerchantController.cs` | 439-441, 475-503 |
| 广告兑换服务端 | `Assets/Scripts/Services/Go/GoServerInventoryGateways.cs` | 320-352 |
| 生产入口(未注入) | `Assets/DragonBound/Runtime/Core/TwentyWavePressureRuntime.cs` | 626-645 |
| 测试用例 | `Assets/DragonBound/Tests/EditMode/ItemEconomyFlowEffectsTests.cs` | 66-85 |
| 测试桩 | `Assets/DragonBound/Tests/EditMode/ItemEconomyFlowEffectsTests.cs` | 109-127 |

---

**总结**:Forgekeeper's Gift 在 V1 道具系统中是接口契约最完整、但生产实现最欠缺的代表 — 它有一个清晰的「每 90s 请求一次广告型 Forge Pick,棋盘无锁定格就停止」的效果,但因为没有生产代码实现 `IItemForgePickPort` 接口,实际运行时这个道具的 `Tick` 在 `context.ForgePick == null` 处提前返回,装备了也不会有任何效果。这是 `Docs/DragonBound/ItemSystemV1FoundationVerified.md:46` 明确指出的「absent or rejected ports never report a grant」现象。
