# V2 英雄 Spine 普攻：改为「每攻击一次，播放一次」

适用：Windclaw Ranger（同时适用于已接线的 Oathcrown Blademaster，同一套代码）。
状态：**仅方案，未执行**。

---

## 1. 目标行为

| 场景 | 现在 | 改后 |
| --- | --- | --- |
| 英雄上场/待机 | `Attack` 一直循环播放（不停挥砍） | 静止在 setup pose（Spine 初始姿势） |
| 每次普攻/技能攻击 | 重启一次 `Attack` 循环，之后继续无限循环 | 完整播放一次 `Attack`（0.833s），播完自动回到静止姿势 |
| 连续攻击（间隔 < 0.833s） | 循环播放 | 新的攻击从头重播一次（正常打断） |

---

## 2. 根因：三个 `loop = true`

| # | 位置 | 现状 | 说明 |
| --- | --- | --- | --- |
| A | `V2/Content/UI/Prefabs/Components/HeroFormation.prefab:732-733` | `startingAnimation: Attack`、`startingLoop: 1` | `SkeletonGraphic.Initialize()`（`SkeletonGraphic.cs:533-542`）在组件初始化时自动 `SetAnimation(0,"Attack",true)`，即使代码不改也会一直循环 |
| B | `HeroFormationView.cs:571-575` `ApplyHeroSpineArt` | `SetAnimation(0, AttackAnimationName, true)` | 英雄上场后再起一次循环 |
| C | `HeroFormationView.cs:661-674` `PlaySpineAttackAnimation` | `SetAnimation(0, AttackAnimationName, true)` | 每次攻击只是「重启循环」，不停止 |

Windclaw Ranger 的 Spine 只有 **一个**动画 `Attack`，时长 **0.8333s**（`WindclawRanger.json`，无 idle/stand），所以待机只能停在 setup pose。

---

## 3. 修改步骤（2 处代码 + 1 处预制体）

### 步骤 A — 预制体：关掉自动循环播放

文件：`Assets/DragonBound/UI/Variants/V2/Content/UI/Prefabs/Components/HeroFormation.prefab`（第 732-733 行）

```diff
-  startingAnimation: Attack
-  startingLoop: 1
+  startingAnimation:
+  startingLoop: 0
```

说明：留空后 `Initialize()` 不会自动起动画，skeleton 保持 setup pose；`SkeletonGraphic.LateUpdate()`（`:308-310`）在 `!wasUpdatedAfterInit` 时会 `Update(0)` 刷新网格，所以不会显示空白。

> 也可在 Inspector 里改：`ART_ComponentConnector` → SkeletonGraphic → **Starting Animation** 清空、**Starting Loop** 取消勾选，然后 Ctrl+S。

### 步骤 B — `ApplyHeroSpineArt`：上场即静止

文件：`Assets/DragonBound/Runtime/Presentation/HeroFormationView.cs:571-575`

```csharp
            heroAttackSkeleton.gameObject.SetActive(true);
            heroAttackSkeleton.skeletonDataAsset = skeleton;
            heroAttackSkeleton.Initialize(true);
-           heroAttackSkeleton.AnimationState.SetAnimation(
-               0, HeroSpineArtCatalog.AttackAnimationName, true);
+           // Idle: hold the authored setup pose; the attack clip is triggered per attack.
+           heroAttackSkeleton.AnimationState.SetEmptyAnimation(0, 0f);
+           heroAttackSkeleton.Skeleton.SetToSetupPose();
+           heroAttackSkeleton.Update(0f);
```

（`HeroFormationView.cs` 已 `using Spine.Unity;`；`SkeletonGraphic.AnimationState`、`SkeletonGraphic.Skeleton`、`Update(float)` 均为 public。）

### 步骤 C — `PlaySpineAttackAnimation`：播放一次并自动归位

文件：`Assets/DragonBound/Runtime/Presentation/HeroFormationView.cs:661-674`

```csharp
        private bool PlaySpineAttackAnimation()
        {
            if (heroAttackSkeleton == null ||
                !heroAttackSkeleton.gameObject.activeInHierarchy ||
                heroAttackSkeleton.Skeleton == null)
            {
                return false;
            }

-           // Restart the authored attack loop so every observed attack reads as a fresh swing.  
-           heroAttackSkeleton.AnimationState.SetAnimation(
-               0, HeroSpineArtCatalog.AttackAnimationName, true);
-           return true;
+           // Play the authored swing once, then fall back to the setup pose so the hero reads
+           // as idle until the next attack.
+           var entry = heroAttackSkeleton.AnimationState.SetAnimation(
+               0, HeroSpineArtCatalog.AttackAnimationName, false);
+           if (entry == null)
+           {
+               return false;
+           }
+
+           heroAttackSkeleton.AnimationState.AddEmptyAnimation(0, 0f, 0f);
+           return true;
        }
```

`AddEmptyAnimation(0, 0f, 0f)` 在 Attack 结束后 0 秒、0 混合地回到 setup pose，避免停在挥砍末帧。
后续 `SetAnimation` 会清空该 track 的排队 entry，不会堆积。

---

## 4. 可选增强

### 4.1 同帧去重（建议）
一次攻击可能被 `GreyboxBoardView.ObserveAttackSequence`（`:2008`）和 `PlayHeroFormationAttackAnimation`（`:373`，CombatFx 侧）在同一帧各触发一次，导致动画被重启、出现抖动。Animator 分支已有该保护（`:636-639`），Spine 分支可对齐：

```csharp
        private int lastSpineAttackFrame = -1;   // 类字段，放在 heroAttackSkeleton 附近
```
在 `PlaySpineAttackAnimation` 的 null 检查之后、`SetAnimation` 之前插入：
```csharp
            if (lastSpineAttackFrame == Time.frameCount)
            {
                return true;
            }
            lastSpineAttackFrame = Time.frameCount;
```

### 4.2 若美术要求待机呼吸
当前 Spine 只有 `Attack`，无 idle。若后续补了 `Idle` 动画：
- `ApplyHeroSpineArt` 末行改为 `SetAnimation(0, "Idle", true)`
- `PlaySpineAttackAnimation` 末行改为 `AddAnimation(0, "Idle", true, 0f)` 替掉 `AddEmptyAnimation`

### 4.3 攻击节奏过快
`Attack` 0.833s。若英雄攻速高于 1.2 次/秒，连续攻击会不断从头重播、看不到完整动作。可按需给 entry 加速：
```csharp
entry.TimeScale = Mathf.Max(1f, 0.8333f / attackIntervalSeconds);
```
（需要拿实际攻击间隔，当前不建议加，先观察手感。）

---

## 5. 影响面与验证

- **V1 不受影响**：V1 的 `HeroFormation.prefab` 没有 `heroAttackSkeleton` 字段（反序列化为 null），所有 Spine 分支 early-return，仍走 Animator。
- **Oathcrown Blademaster 同步受益**：共用同一套代码，前提是它的 json 里动画名已是 `Attack`（此前已要求把 `animation` 改名）。
- **EditMode 测试不受影响**：`PortraitUiPrefabTests.cs:25` 用的是 V1 prefab（`UiVariantProjectPaths.V1Ui(...)`），两个用例都断言 `HeroAttackAnimator`，不走 Spine 分支。
- **验证清单**：
  1. 进战斗但不开打：英雄静止不动，不再循环挥砍。
  2. 打一次：完整播放一次挥砍（≈0.83s）后回到静止。
  3. 连续普攻：每次都重新完整播一次；间隔很短时表现为连续挥砍。
  4. V1 出包回归：英雄行为与改动前完全一致。

## 6. 回滚

还原步骤 B/C 的 `true`、把 prefab 的 `startingAnimation` 改回 `Attack`、`startingLoop` 改回 `1` 即可。
