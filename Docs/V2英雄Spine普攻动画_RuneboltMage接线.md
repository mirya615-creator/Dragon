# V2 新增英雄 Runebolt Mage 普攻 Spine 动画 — 接线步骤

资源路径：`Assets/DragonBound/UI/Variants/V2/Content/Resources/Animations/Hero/Runebolt Mage/normal/Spine`
状态：**仅步骤，未执行**。

---

## 前置核查（已核对完毕，可直接用）

| 检查项 | 结果 |
| --- | --- |
| Spine 资源是否已被 Unity 导入 | ✅ 目录内已有 `Runebolt Mage.json / .atlas.txt / Runebolt Mage.png / Runebolt Mage2.png / Runebolt Mage_Atlas.asset / Runebolt Mage_SkeletonData.asset / 2 个 .mat` |
| Spine 版本 | ✅ `3.8.0`，与工程 spine-unity 3.8 一致 |
| 动画名 | ✅ **只有 `Attack`**，时长 **0.8333s**，skin `default` → 与 `HeroSpineArtCatalog.AttackAnimationName`（`"Attack"`）一致，**无需改名** |
| V2 注册表 key | ✅ `UiAssetRegistryV2.asset:221` 已存在 `Animations/Hero/Runebolt Mage/normal/Spine/Runebolt Mage_SkeletonData`（无需 Regenerate；若你刚拷入文件请见步骤 1） |
| V2 prefab 是否走 Spine 分支 | ✅ `V2/…/HeroFormation.prefab:492` `heroAttackAnimator: {fileID: 0}`（null）→ `PlayAttackAnimation`(:598-603) 必然进 `PlaySpineAttackAnimation` |
| V2 prefab Spine 节点 | ✅ 同 prefab `:494` `heroAttackSkeleton: {fileID: 8251840913189477176}`（节点 `ART_ComponentConnector`，已接线，本次**无需改 prefab**） |
| 英雄 id 常量 | ✅ `DragonBoundHeroIds.RuneboltMage = "HERO_RUNEBOLT_MAGE"`（`FrozenHeroConfiguration.cs:66`），`HeroSliceCatalog.RuneboltMageHeroId` 指向它 |

结论：**代码只需加 1 条字典映射**。

---

## 步骤 1（条件执行）— 刷新 V2 资源注册表

若资源文件是本次才拷进目录的，或 Unity 尚未生成过 key：

菜单 **`DragonBound/Versioning/Regenerate V2 Asset Registry (Preserve Aliases)`**，然后确认
`Assets/DragonBound/UI/Variants/V2/Config/UiAssetRegistryV2.asset` 里含：

```
Animations/Hero/Runebolt Mage/normal/Spine/Runebolt Mage_SkeletonData
```

（当前已存在，可跳过本步。）

## 步骤 2（唯一代码改动）— 加一条英雄 → 骨骼映射

文件：`Assets/DragonBound/Runtime/Presentation/HeroFormationView.cs`
位置：`HeroSpineArtCatalog.SkeletonResourcePaths`（`:1596-1603`）

```diff
                 [DragonBoundHeroIds.CrownSwordLeader] =
                     "Animations/Hero/Oathcrown Blademaster/normal/Spine/誓冠剑士 拆_SkeletonData",
                 [DragonBoundHeroIds.WindclawRanger] =
                     "Animations/Hero/Windclaw Ranger/normal/Spine/WindclawRanger_SkeletonData"
+                ,
+                [DragonBoundHeroIds.RuneboltMage] =
+                    "Animations/Hero/Runebolt Mage/normal/Spine/Runebolt Mage_SkeletonData"
             };
```

key 必须等于「相对 `Variants/V2/Content/Resources` 的路径、去扩展名」，与注册表一致，大小写/空格都要完全匹配（`Runebolt Mage` 中间有空格）。

生效链路：`SetHeroAnimation`（`:250`）→ `ApplyHeroSpineArt`（`:253`/`:555`）→ `HeroSpineArtCatalog.Load(heroId)` → 命中后 `skeletonDataAsset` 赋值 + `Initialize(true)` + `SetAnimation(0,"Attack",true)`。

## 步骤 3（强烈建议）— 配合「每次攻击播一次」

若不做这一步，Runebolt Mage 上场后会**一直循环挥砍**（prefab `startingAnimation: Attack`/`startingLoop: 1` + 两处 `loop=true`）。
执行 `Docs/V2英雄Spine普攻_每次攻击播一次方案.md` 的 A/B/C：

- **A** prefab `:732-733`：`startingAnimation` 清空、`startingLoop: 0`
- **B** `ApplyHeroSpineArt:574-575` → `SetEmptyAnimation(0,0f)` + `Skeleton.SetToSetupPose()` + `Update(0f)`
- **C** `PlaySpineAttackAnimation:671-672` → `SetAnimation(..., false)` + `AddEmptyAnimation(0, 0f, 0f)`

只想最小改动的话，至少做 **C**（`true` → `false`）能实现「播完停住」。

## 步骤 4（实机调参）— 尺寸 / 位置

所有 Spine 英雄**共用同一个 SkeletonGraphic 节点** `ART_ComponentConnector`（V2 prefab，当前 `localScale = 0.06`）。
Runebolt Mage 的骨骼尺寸可能与该 scale 不匹配，实机若过大/过小/偏移：

1. 首选：调该节点 RectTransform 的 **Width/Height**，并在 `ApplyHeroSpineArt` 的 `Initialize(true)` 后补一行
   `heroAttackSkeleton.MatchRectTransformWithBounds();` 让 Spine 自动贴合矩形。
2. 若三个英雄尺寸差异大、需要各自 scale：在 `ApplyHeroSpineArt` 里按 heroId 覆盖（可选）：
   ```csharp
   heroAttackSkeleton.transform.localScale =
       HeroSpineArtCatalog.ResolveScale(heroId, heroAttackSkeleton.transform.localScale);
   ```
   （新增 `ResolveScale` 字典，默认回退当前 authored scale。）
3. 注意 `SetArtMirrored`（`:305-309`）会按 `Mathf.Abs` 改 `localScale.x` 做镜像，不要在别处硬编码负号。

## 步骤 5 — 验证清单

- [ ] V2 战斗上场 Runebolt Mage：显示 Spine 立绘（不再是静态图）。
- [ ] 每次普攻播放一次挥砍（≈0.83s），之后回到静止；连续普攻每次重播。
- [ ] Console 无 `Hero Spine skeleton 'Animations/Hero/Runebolt Mage/...' is unavailable for hero 'HERO_RUNEBOLT_MAGE'`。
- [ ] V1 出包回归：V1 `HeroFormation.prefab` 无 `heroAttackSkeleton`（反序列化为 null）→ 完全不受影响。
- [ ] 其余未接 Spine 的英雄：`HeroSpineArtCatalog.Load` 返回 null → Spine 节点 `SetActive(false)`，行为不变。

## 备注（非本次范围）

- `Runebolt Mage/normal/VFX/` 已有 `RM nor Muzzle / RM nor Projectile / RM nor Impact` 三个特效，属于弹道/命中特效，走 `CombatFxView`（`RuneboltMageBoltControllerPath` 等），与本次「英雄本体 Spine 立绘」是两条独立链路，互不冲突。
- Runebolt Mage 的普攻是弹道型（CombatFxView `QueueRuneboltMageCast`），英雄本体动画仍由 `GreyboxBoardView.ObserveAttackSequence`（`:2008`）统一驱动，无需额外接线。
