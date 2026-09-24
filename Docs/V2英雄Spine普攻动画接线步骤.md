# V2 英雄 Spine 普攻动画接线步骤（Oathcrown Blademaster / 誓冠剑士）

> 目标资源：`Assets/DragonBound/UI/Variants/V2/Content/Resources/Animations/Hero/Oathcrown Blademaster/normal/Spine/`
> 本文件为**手工修改步骤清单**，未代改代码。所有行号以 2026-09-23 的 `codex/ui-v1-v2-isolation` 分支现状为准。

---

## 一、现状（先理解为什么现在不播）

| 项 | V1 | V2 |
|---|---|---|
| 场景引用的预制体 | `V1/Content/UI/Prefabs/Components/HeroFormation.prefab`（guid `24dcae52…`） | `V2/Content/UI/Prefabs/Components/HeroFormation.prefab`（guid `88d393f74fcdd06468c3271aebfe3a08`） |
| `ART_ComponentConnector` 节点上的组件 | `Animator`（type 95） | `SkeletonGraphic`（`skeletonDataAsset` 为空、`startingAnimation: Attack`、`startingLoop: 1`） |
| `HeroFormationView.heroAttackAnimator` | 已赋值 | **空（fileID: 0）** |

V1/V2 共用 `HeroFormationView.cs`。`SetHeroAnimation`（`:247`）第 251 行 `if (heroAttackAnimator == null || …) return;` —— V2 里它为 null，所以**直接 return，Spine 永远拿不到 skeletonData**。
同理 `PlayAttackAnimation`（`:565`）第 570 行也因 `heroAttackAnimator == null` 返回 false。

**结论：V2 的 SkeletonGraphic 是预留好的空壳，需要新增代码分支 + 预制体拖引用。**

### 已具备的有利条件

- 新资源**已经在 V2 注册表里**（`UiAssetRegistryV2.asset` 第 151–179 行，7 个 key）。核心 key：
  `Animations/Hero/Oathcrown Blademaster/normal/Spine/誓冠剑士 拆_SkeletonData`
  （yaml 里中文是 `\u8A93\u51A0\u5251\u58EB \u62C6` 转义，即「誓冠剑士 拆」，含空格）
- `DragonBound.Runtime.asmdef` 已引用 `spine-unity`，`EnemyView.cs` 已用 `SkeletonGraphic` 做过同样的事（运行时换 `skeletonDataAsset` + `SetAnimation`），可直接照抄。

---

## 二、步骤 0（必做）：把 Spine 动画名统一成 `Attack`

- 现状：`誓冠剑士 拆.json` 里是 `"animations":{"animation":{…}}`，动画名 = **`animation`**
- 参照：`WindclawRanger.json` 的动画名 = **`Attack`**，且预制体 `startingAnimation: Attack`

两者不一致，必须对齐（否则运行时找不到动画）。二选一：

- **A（推荐，快）**：直接改 json —— 把 `"animations":{"animation":` 改成 `"animations":{"Attack":`
- **B（干净）**：Spine 里把动画重命名为 `Attack` 后重新导出

> 该动画时长 0.8333s（最后一帧 `time: 0.8333`）。Spine 运行时按 json 读动画名，改 key 即可生效，`_SkeletonData.asset` 不缓存动画名，安全。

---

## 三、代码修改（仅 1 个文件：`HeroFormationView.cs`）

路径：`Assets/DragonBound/Runtime/Presentation/HeroFormationView.cs`

### 步骤 1 — 加 using（`:5` 之后）

```csharp
// 改前
using DragonBound.Recruitment;
using UnityEngine;
using UnityEngine.UI;

// 改后
using DragonBound.Recruitment;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;
```

### 步骤 2 — 加序列化字段（`:34` 之后）

```csharp
// 改前
        [SerializeField] private Image particalBgImage;

// 改后
        [SerializeField] private Image particalBgImage;
        [SerializeField] private SkeletonGraphic heroAttackSkeleton;
```

> V1 预制体不拖这个字段 → 保持 null → 行为完全不变。

### 步骤 3 — 加私有状态（`:38` 附近，和 `configuredAnimationHeroId` 放在一起）

```csharp
// 改前
        private string configuredAnimationHeroId = string.Empty;

// 改后
        private string configuredAnimationHeroId = string.Empty;
        private string configuredSpineHeroId = string.Empty;
```

### 步骤 4 — `SetHeroAnimation` 里应用 Spine（`:247-255`）

```csharp
// 改前
        public void SetHeroAnimation(string heroId)
        {
            ApplyParticalBackground(heroId);

            if (heroAttackAnimator == null ||
                string.Equals(configuredAnimationHeroId, heroId, StringComparison.Ordinal))
            {
                return;
            }

// 改后
        public void SetHeroAnimation(string heroId)
        {
            ApplyParticalBackground(heroId);
            ApplyHeroSpineArt(heroId);

            if (heroAttackAnimator == null ||
                string.Equals(configuredAnimationHeroId, heroId, StringComparison.Ordinal))
            {
                return;
            }
```

### 步骤 5 — 新增 `ApplyHeroSpineArt`（紧跟 `SetHeroAnimation` 之后，即 `:281` 的 `}` 后插入）

```csharp
        private void ApplyHeroSpineArt(string heroId)
        {
            if (heroAttackSkeleton == null ||
                string.Equals(configuredSpineHeroId, heroId, StringComparison.Ordinal))
            {
                return;
            }

            configuredSpineHeroId = heroId ?? string.Empty;
            var skeleton = HeroSpineArtCatalog.Load(heroId);
            if (skeleton == null)
            {
                heroAttackSkeleton.gameObject.SetActive(false);
                return;
            }

            heroAttackSkeleton.gameObject.SetActive(true);
            heroAttackSkeleton.skeletonDataAsset = skeleton;
            heroAttackSkeleton.Initialize(true);
            heroAttackSkeleton.AnimationState.SetAnimation(
                0, HeroSpineArtCatalog.AttackAnimationName, true);
        }
```

> `Initialize(true)` 必须在赋值后调用，否则 `AnimationState` 为 null。第三个参数 `true` = 循环播放（与预制体 `startingLoop: 1` 一致）。

### 步骤 6 — `PlayAttackAnimation` 分流（`:565-570`）

```csharp
// 改前
        public bool PlayAttackAnimation(bool useSkillAnimation = false)
        {
            var desiredController = useSkillAnimation && skillAttackController != null

// 改后
        public bool PlayAttackAnimation(bool useSkillAnimation = false)
        {
            if (heroAttackAnimator == null)
            {
                return PlaySpineAttackAnimation();
            }

            var desiredController = useSkillAnimation && skillAttackController != null
```

并在本方法末尾（`:620` 的 `}` 之后）新增：

```csharp
        private bool PlaySpineAttackAnimation()
        {
            if (heroAttackSkeleton == null ||
                !heroAttackSkeleton.gameObject.activeInHierarchy ||
                heroAttackSkeleton.Skeleton == null)
            {
                return false;
            }

            // Restart the authored attack loop so every observed attack reads as a fresh swing.
            heroAttackSkeleton.AnimationState.SetAnimation(
                0, HeroSpineArtCatalog.AttackAnimationName, true);
            return true;
        }
```

### 步骤 7 — 新建英雄 Spine 资源目录类（文件末尾，`HeroAnimationControllerCatalog` 之后追加）

```csharp
    /// <summary>
    /// Maps formal hero ids to the authored Spine skeleton used by the V2 hero attack art. Keys
    /// live under the active variant's Resources/Animations/Hero folder, so V1 keeps resolving
    /// Animator controllers while V2 resolves Spine skeletons from the same hero id.
    /// </summary>
    public static class HeroSpineArtCatalog
    {
        private const string DefaultAttackAnimationName = "Attack";

        private static readonly IReadOnlyDictionary<string, string> SkeletonResourcePaths =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DragonBoundHeroIds.CrownSwordLeader] =
                    "Animations/Hero/Oathcrown Blademaster/normal/Spine/誓冠剑士 拆_SkeletonData",
                [DragonBoundHeroIds.WindclawRanger] =
                    "Animations/Hero/Windclaw Ranger/normal/Spine/WindclawRanger_SkeletonData"
            };

        private static readonly Dictionary<string, SkeletonDataAsset> SkeletonCache =
            new Dictionary<string, SkeletonDataAsset>(StringComparer.Ordinal);
        private static readonly HashSet<string> MissingSkeletonWarnings =
            new HashSet<string>(StringComparer.Ordinal);

        public static string AttackAnimationName => DefaultAttackAnimationName;

        public static SkeletonDataAsset Load(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId) ||
                !SkeletonResourcePaths.TryGetValue(heroId.Trim(), out var resourcePath))
            {
                return null;
            }

            if (SkeletonCache.TryGetValue(resourcePath, out var cached))
            {
                return cached;
            }

            // Probe silently: UiAssets.Load logs an error on a miss, and most heroes have no
            // Spine art yet, so an unregistered key is an expected state rather than a failure.
            var registry = UiAssets.Active;
            var skeleton = registry != null ? registry.Load<SkeletonDataAsset>(resourcePath) : null;
            if (skeleton == null && MissingSkeletonWarnings.Add(resourcePath))
            {
                Debug.LogWarning(
                    $"Hero Spine skeleton '{resourcePath}' is unavailable for hero '{heroId}'.");
            }

            SkeletonCache[resourcePath] = skeleton;
            return skeleton;
        }
    }
```

> `DragonBoundHeroIds.CrownSwordLeader = "HERO_CROWN_SWORD_LEADER"`，中文名「誓冠剑士」、英文名 "Oathcrown Blademaster"（`FrozenHeroConfiguration.cs:621`）。
> `WindclawRanger` 那行是顺带接上的（它的 Spine 资源已在注册表里且同样闲置），不需要就删掉。

### 步骤 8（推荐，否则 AI 侧英雄朝向不对）— `SetArtMirrored` 加 Spine 分支

在 `:301` 的 `if (particalBgImage != null)` 之前插入：

```csharp
            if (heroAttackSkeleton != null)
            {
                var spineScale = heroAttackSkeleton.transform.localScale;
                spineScale.x = mirrored ? -Mathf.Abs(spineScale.x) : Mathf.Abs(spineScale.x);
                heroAttackSkeleton.transform.localScale = spineScale;
            }
```

> 用 `Mathf.Abs` 保证反复调用不出错。V1 不受影响（字段为 null）。

---

## 四、预制体接线（Unity 里做）

1. 打开 `Assets/DragonBound/UI/Variants/V2/Content/UI/Prefabs/Components/HeroFormation.prefab`
2. 选中根节点 **`HeroFormation`** → `HeroFormationView` 组件
3. 把子节点 **`ART_ComponentConnector`** 拖到新增的 **Hero Attack Skeleton** 字段
4. **Ctrl+S 保存**
5. V1 的 `HeroFormation.prefab` **不用动**（字段留空即可）

> 可选：把 `ART_ComponentConnector` 上 SkeletonGraphic 的 **Raycast Target** 取消勾选（现在是 1），避免挡住棋盘点击。

---

## 五、验证

1. Unity 打开 **V2** 的 `Greybox_Main`（`DragonBoundUiVariantSceneSelector` 会自动激活 V2 注册表）
2. 让「誓冠剑士 / Oathcrown Blademaster」成型（或用已有的调试生成入口）
3. 预期：英雄立绘由 Spine 渲染，持续播放 `Attack`；每次攻击该动画从第 0 帧重新开始
4. 控制台检查：
   - 出现 `Hero Spine skeleton '...' is unavailable` → key 拼错或注册表未收录（重跑 `DragonBound / Versioning / Regenerate V2 Asset Registry`）
   - 出现 Spine 的 `Animation not found` → 步骤 0 的动画名没改成 `Attack`
5. 切到 **V1** 的 `Greybox_Main` 确认行为与改动前一致（走 Animator）

---

## 六、已知注意点

- **不要**用 `UiAssets.Load<T>()` 探测 Spine 资源：miss 时 `UiAssetRegistry.cs` 会 `Debug.LogError`，12 个英雄里只有 1~2 个有 Spine，会疯狂刷红错。上面用的 `UiAssets.Active?.Load<T>()` 是静默探测（与 `EnemyView.cs:766` 的 `LoadFirstRegistered` 同一思路）。
- `SkeletonCache` 是静态缓存，编辑器里切换 V1/V2 后需**重进一次 Play Mode** 才会重新解析。
- 注册表 key 含中文与空格（`誓冠剑士 拆_SkeletonData`），C# 源文件需 UTF-8 保存（项目里 `ResourcesCampComponentArtProvider.cs` 已有中文字符串，按同样方式即可）。
- 本次只接「普攻」。将来加技能动画：在 `Animations/Hero/<Name>/skill/Spine/` 放资源 → Regenerate → `HeroSpineArtCatalog` 加一张 `SkillSkeletonResourcePaths`，`PlaySpineAttackAnimation` 里按 `useSkillAnimation` 取不同动画名。
