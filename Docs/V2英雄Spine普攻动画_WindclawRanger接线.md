# V2 英雄 Windclaw Ranger Spine 普攻动画接线步骤

> 资源：`Assets/DragonBound/UI/Variants/V2/Content/Resources/Animations/Hero/Windclaw Ranger/normal/Spine`
> 状态核查时间：2026-09-23（基于 `codex/ui-v1-v2-isolation` 分支当前代码）

---

## 零、结论先说：代码接线**已经完成**，本轮只需验证 + 微调

上一轮做 Oathcrown Blademaster 时，映射表里**已经一并写入了 Windclaw Ranger**。逐项核查结果如下：

| 环节 | 状态 | 证据 |
|---|---|---|
| Spine 运行时版本 | ✅ 3.8.0，与 json 导出的 `3.8.0` 完全一致 | `Assets/Spine/version.txt`、`.json` 的 `skeleton.spine` |
| 动画名一致 | ✅ `Attack`，与预制体 `startingAnimation: Attack` 匹配 | `WindclawRanger.json` → `animations: ['Attack']` |
| 注册表 key | ✅ 已存在，无需 Regenerate | `UiAssetRegistryV2.asset:215` `Animations/Hero/Windclaw Ranger/normal/Spine/WindclawRanger_SkeletonData` |
| SkeletonData 内部引用 | ✅ json/atlas guid 均正确指向本目录文件 | `WindclawRanger_SkeletonData.asset` → skeletonJSON `d1323b85…` / atlasAssets `ffc7ad03…` |
| 图集双页 | ✅ Atlas 挂了 2 个 material，SkeletonGraphic 有 5 个 canvasRenderer 支持多页 | `WindclawRanger_Atlas.asset` materials ×2，`.atlas.txt` 两页 |
| heroId → key 映射 | ✅ 已写入 | `HeroFormationView.cs:1601-1602` |
| Spine 播放管线 | ✅ `ApplyHeroSpineArt` / `PlaySpineAttackAnimation` 已实现 | `HeroFormationView.cs:555-576`、`:661-674` |
| 预制体字段接线 | ✅ `heroAttackSkeleton` 已指向 SkeletonGraphic | `V2/Content/UI/Prefabs/Components/HeroFormation.prefab:494` → `{fileID: 8251840913189477176}` |
| 场景引用 | ✅ 玩家与 AI 两处都引用该 V2 预制体 | `V2/Scenes/Greybox_Main.unity:22179`、`:34396`（guid `88d393f74fcdd06468c3271aebfe3a08`） |
| V1 不受影响 | ✅ V1 预制体无 `heroAttackSkeleton` 字段，反序列化为 null → 所有 Spine 分支 early-return | `V1/…/HeroFormation.prefab:348` 只有 `heroAttackAnimator` |

**唯一的差距**：`WindclawRanger.json` 的动画名本来就是 `Attack`，所以上一轮 Oathcrown 需要的「步骤 0 改名」在这里**不需要做**（Oathcrown 是 `animation`，必须改名）。

---

## 一、验证步骤（照这个顺序做）

### 1. 确认资源可加载（不进 Play 也能查）

```bash
grep -c "Animations/Hero/Windclaw Ranger/normal/Spine/WindclawRanger_SkeletonData" \
  "Assets/DragonBound/UI/Variants/V2/Config/UiAssetRegistryV2.asset"
```
返回 ≥1 即可（当前是 1）。**不用跑 Regenerate V2 Asset Registry** —— key 已在。

### 2. 打开 V2 场景并确认激活了 V2 变体

打开 `Assets/DragonBound/UI/Variants/V2/Scenes/Greybox_Main.unity`。
`DragonBoundUiVariantSceneSelector` 会按场景路径自动 SetActiveV2，正常情况下无需手动切。
若不确定，走菜单 `DragonBound / Versioning / Set Active V2`。

### 3. 用调试入口直接出 Windclaw Ranger（最快）

Windclaw Ranger 正好是调试英雄：`DragonBoundBootstrap.cs:266`（玩家侧）与 `:338`（AI 侧）
的 `TryDebugSpawnDragonRouteHero(..., DragonBoundHeroIds.WindclawRanger)`。
所以一进 Greybox_Main 就能看到它，**不需要凑合成配方**。

### 4. 观察点（三看）

- **静止态**：英雄 formation 上应看到 Spine 骨架的 Attack 首帧（不是空白，也不是原来的静态英雄图）。
- **攻击时**：每次普攻 `PlaySpineAttackAnimation` 会 `SetAnimation(0, "Attack", true)` 重启动画循环，看到一次挥击。
- **控制台**：**不应出现** `Hero Spine skeleton '…' is unavailable for hero '…'` 警告。
  出现则说明注册表 key 或变体没激活。

---

## 二、建议补的 3 处微调（都不是必须，但建议做）

### 微调 1（推荐）关闭射线拦截

`V2/…/HeroFormation.prefab` → `ART_ComponentConnector` 节点 → **SkeletonGraphic**
`m_RaycastTarget: 1`（预制体第 722 行）→ 在 Inspector 里**取消勾选 Raycast Target**。

> 理由：Spine 网格铺在棋盘上方，会吃掉点击/拖拽射线，可能导致英雄格无法拖兵或点击无响应。
> 项目里同类处理见 `HeroFormationView.cs:246`（`runeImage.raycastTarget = false;`）与 `:551`。

### 微调 2（实机必调）尺寸与位置

当前 `ART_ComponentConnector` 的 RectTransform 是 `m_LocalScale: {x: 0.06, y: 0.06}`（预制体第 685 行），
Spine 侧 `WindclawRanger_SkeletonData.asset` 的 `scale: 0.01`。这两个叠乘决定最终显示大小。

实机若发现过小/过大/偏移：
- 优先调节点 **Scale**（0.06），或
- 调 RectTransform 的 **Width/Height** 后，在 `ApplyHeroSpineArt` 里补一句
  `heroAttackSkeleton.MatchRectTransformWithBounds();`（放在 `Initialize(true)` 之后），让 Spine 自动贴合矩形。

### 微调 3（可选）离屏更新

SkeletonGraphic 的 `updateWhenInvisible: 3`（FullUpdate，预制体第 736 行）。
英雄离屏（AI 半场滚出视野）时仍在全量更新，可改为 `2`（OnlyAnimationStatus）省开销；
改成 `0`（Nothing）会停动画，**不要用**。

---

## 三、已知缺口（本次不做，记录备查）

1. **技能动画不区分**：`PlayAttackAnimation(useSkillAnimation)` 在 Spine 分支里**忽略了该参数**，
   `HeroFormationView.cs:600-603` 直接转 `PlaySpineAttackAnimation()`，后者恒播 `Attack`。
   而 Windclaw Ranger 确有技能（`HeroCombatState.cs:651` 的 `WindclawPowerShot` / `WindclawShot`）。
   → 若要技能动画：json 里加 `Skill` 动画名，并在 `HeroSpineArtCatalog` 加 `SkillAnimationName`，
   让 `PlaySpineAttackAnimation(bool useSkillAnimation)` 按参数选。

2. **AI 侧同款动画**：V2 场景 AI 实例引用同一预制体（`:34396`），AI 英雄也会播 Spine 普攻。
   这与项目「AI 侧与玩家共用反馈」的既有约定一致，一般不需处理；若要关闭，
   在 `HeroFormationView` 加 `allowInteraction` 判断即可（该字段已在预制体上，`:34399` 为 0）。

3. **静态美术与 Spine 是否叠显**：`ART_ComponentConnector` 是共用美术容器，带 5 个子节点。
   实机若发现 Spine 与原有静态英雄图重叠，需要在 `ApplyHeroSpineArt` 里
   `SetActive(true)` 之前隐藏那 5 个子节点（具体隐藏哪几个要按实机表现定）。

---

## 四、与其他英雄的关系

`HeroSpineArtCatalog`（`HeroFormationView.cs:1592-1638`）目前只映射了 2 个英雄：

| heroId | key |
|---|---|
| `DragonBoundHeroIds.CrownSwordLeader`（`HERO_CROWN_SWORD_LEADER`） | `Animations/Hero/Oathcrown Blademaster/normal/Spine/誓冠剑士 拆_SkeletonData` |
| `DragonBoundHeroIds.WindclawRanger`（`HERO_WINDCLAW_RANGER`） | `Animations/Hero/Windclaw Ranger/normal/Spine/WindclawRanger_SkeletonData` |

其余 10 个英雄走 `Load()` 返回 null → `ApplyHeroSpineArt` 把节点 `SetActive(false)`
（`HeroFormationView.cs:565-569`），行为与加 Spine 之前完全一致，**不会出现空骨架**。

新增第三个英雄时：放资源 → Regenerate V2 Registry → 在映射表里加一行即可，代码无需再改。
