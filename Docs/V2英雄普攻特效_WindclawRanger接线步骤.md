# V2 英雄 Windclaw Ranger 普攻攻击特效接线步骤

> 新资源：`Assets/DragonBound/UI/Variants/V2/Content/Resources/Animations/Hero/Windclaw Ranger/normal/VFX/`
>   - `Windclaw Ranger norVFX.anim`（9 帧序列帧，改 `Image.sprite`，`m_StopTime: 0.15`）
>   - `Windclaw Ranger norVFX.controller`（单 state，`m_Speed: 1`）
> 状态核查时间：2026-09-23

---

## 零、落点结论

**普攻目前完全没有特效**，这是既定的空分支：

```csharp
// CombatFxView.cs:577-578
case AttackKind.WindclawShot:
    break;          // ← 普攻，直接跳过
```

（`HeroCombatState.cs:651`：`powerShot ? AttackKind.WindclawPowerShot : AttackKind.WindclawShot`
→ `WindclawShot` 就是普攻；`WindclawPowerShot` 才走 `:579-584` 的技能队列。）

**要改的只有 `CombatFxView.cs` 一个文件，共 4 处编辑。**

现有 `WindclawImpactSpritePath`（`:26`）、`pendingWindclawSkillCasts`（`:187`）、
`SpawnWindclawImpact`（`:2802`）、`ResolveWindclawImpactSprite`（`:3102`）
**全部属于技能链**，普攻不经过，不要在那里改。

---

## 步骤 0（必做，两件事）

### 0-1 关掉 Loop Time

`Windclaw Ranger norVFX.anim` 现在是 **`m_LoopTime: 1`**（第 82 行）。
一次性命中特效循环播会在原地反复闪，必须关掉：

1. Project 里选中 `Windclaw Ranger norVFX.anim`；
2. Inspector 右上角 **⋮ → Debug** 模式（或直接在 Animation 窗口），
   取消 **Loop Time**；
3. 保存。改完文件里应为 `m_LoopTime: 0`。

> 顺带确认一下 `m_Speed` 保持 **1**（controller 里已经是 1，没问题）。
> 之前 EnemyDie 踩过 `m_Speed: 0.2` 导致代码算出的销毁时长对不上的坑，这里没有。

### 0-2 重新生成 V2 注册表

我查过，**这个 key 目前不在注册表里**（`UiAssetRegistryV2.asset` 中 `norVFX` 命中 0）：

菜单：**`DragonBound / Versioning / Regenerate V2 Asset Registry (Preserve Aliases)`**

`.anim` 与 `.controller` 同名，去扩展名后合并成同一个 key：

```
Animations/Hero/Windclaw Ranger/normal/VFX/Windclaw Ranger norVFX
```

验证（编辑器外执行，返回 ≥1 才算成功）：

```bash
grep -c "norVFX" "Assets/DragonBound/UI/Variants/V2/Config/UiAssetRegistryV2.asset"
```

---

## 步骤 1 — 加资源 key 常量（`CombatFxView.cs:26` 之后）

```csharp
// 改前
        private const string WindclawImpactSpritePath = "VFX/Windclaw Ranger/road";

// 改后
        private const string WindclawImpactSpritePath = "VFX/Windclaw Ranger/road";
        private const string WindclawNormalVfxControllerPath =
            "Animations/Hero/Windclaw Ranger/normal/VFX/Windclaw Ranger norVFX";
```

---

## 步骤 2 — 加缓存字段与尺寸字段（两处）

### 2-A 缓存字段（`:47` 之后）

```csharp
// 改前
        private Sprite windclawImpactSprite;

// 改后
        private Sprite windclawImpactSprite;
        private RuntimeAnimatorController windclawNormalVfxController;
```

### 2-B Inspector 尺寸（`:83` 之后）

```csharp
// 改前
        [SerializeField] private Vector2 windclawImpactSize = new Vector2(110f, 110f);

// 改后
        [SerializeField] private Vector2 windclawImpactSize = new Vector2(110f, 110f);
        [SerializeField] private Vector2 windclawNormalVfxSize = new Vector2(110f, 110f);
```

> 110×110 与技能 impact 同尺寸（棋盘一格）。实机觉得大/小直接改 Inspector 这个字段。

---

## 步骤 3 — 普攻分支接线（`:577-578`）

```csharp
// 改前
                case AttackKind.WindclawShot:
                    break;

// 改后
                case AttackKind.WindclawShot:
                    SpawnWindclawNormalVfx(combatEvent, target);
                    break;
```

> 保持 `break` 而不是 `return`：后面的 `lane.PlayEnemyHitShake`（`:609`）
> 和伤害数字（`:612-619`）还要继续执行，用 `return` 会把它们吃掉。
> `combatEvent` 与 `target`（敌人位置）在 `OnCombat` 里都已在作用域内（`:474-481`）。

---

## 步骤 4 — 新增播放方法（插在 `:2858` 之后，即 `SpawnWindclawImpact` 结束、`CompleteWindclawImpact` 之前）

照抄 `SpawnWindclawImpact`（`:2802-2858`）的骨架 + `SpawnRuneboltMageBolt`（`:840-844`）的 Animator 部分：

```csharp
        private void SpawnWindclawNormalVfx(CombatEvent combatEvent, Vector3 targetPosition)
        {
            if (windclawNormalVfxController == null)
            {
                // Probe silently: UiAssets.Load logs an error on a miss, and V1 has no such
                // key, so an absent clip is an expected state rather than a failure.
                var registry = DragonBound.Presentation.UiAssets.Active;
                windclawNormalVfxController = registry != null
                    ? registry.Load<RuntimeAnimatorController>(WindclawNormalVfxControllerPath)
                    : null;
            }

            if (windclawNormalVfxController == null)
            {
                return;
            }

            var position = targetPosition;

            // 敌人在动画期间可能继续移动，所以优先读取最新位置。
            if (lane != null &&
                lane.TryGetEnemyPosition(
                    combatEvent.TargetRuntimeId,
                    out var currentPosition))
            {
                position = currentPosition;
            }

            var parent =
                fixedBoardCanvas != null &&
                fixedBoardCanvas.CombatFxLayer != null
                    ? fixedBoardCanvas.CombatFxLayer
                    : transform;

            var root = new GameObject(
                "Windclaw Ranger Normal VFX",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator));

            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.position = position;
            rect.sizeDelta = windclawNormalVfxSize;

            var image = root.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = windclawNormalVfxController;
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);
            animator.speed = 1f;

            var duration = windclawNormalVfxController.animationClips.Length > 0
                ? windclawNormalVfxController.animationClips[0].length
                : 0.15f;
            duration = Mathf.Max(0.01f, duration);

            active.Add(new ActiveFx(
                rect,
                image,
                null,
                position,
                position,
                false,
                false,
                duration));
        }
```

### 为什么这样写（对应现有约定）

| 写法 | 依据 |
|---|---|
| `UiAssets.Active` 静默探测而非 `UiAssets.Load` | `UiAssetRegistry.cs:189` miss 时 `Debug.LogError`；V1 无此 key 会刷屏。先例：`HeroFormationView.cs:1627-1628` |
| 创建 `RectTransform + CanvasRenderer + Image + Animator` | 先例 `SpawnRuneboltMageBolt` `:817-822`（EnemyView 的死亡 VFX 也一样） |
| `animator.Rebind(); Play(0,0,0f); Update(0f);` | 先例 `:842-844`，保证首帧立即生效 |
| `parent = fixedBoardCanvas.CombatFxLayer ?? transform` | 先例 `:814-816`、`:2823-2827` |
| `TryGetEnemyPosition` 刷新位置 | 先例 `:2815-2821`（技能 impact 同样处理） |
| `active.Add(new ActiveFx(...))` 自动销毁 | `Update()` `:444-451` 在 `Elapsed >= Duration` 时 `Destroy`；`fade=false` 时不改 alpha（`:435`） |

> **位置默认挂在敌人身上**（远程射手命中点）。
> 如果你要的是英雄身上的"射击闪光"而非命中特效，把 `SpawnWindclawNormalVfx(combatEvent, target)`
> 改成传 `attacker`（`:479` 已算好英雄位置）即可，其余代码不动。

---

## 验证

1. 打开 **V2** 的 `Greybox_Main`（Windclaw Ranger 是调试默认英雄，`Bootstrap.cs:266` 一进场就有）；
2. 普攻命中敌人时，敌人位置应闪出 9 帧特效，约 0.15s 后消失；
3. 控制台**不应出现** `UI asset is not registered` 或 `Windclaw Ranger impact sprite is missing`；
   ——有前者说明 Regenerate 没跑，有后者说明误改到技能链了；
4. **V1 场景**应完全无此特效、且**零报错**（静默降级）。

## 可调参数

| 想调什么 | 改哪里 |
|---|---|
| 特效大小 | Inspector 的 `Windclaw Normal Vfx Size`（默认 110×110） |
| 特效持续时间 | 由 clip 长度决定（0.15s）。改 `.anim` 的关键帧间距即可，代码自动跟随 |
| 改为英雄位置 | 步骤 3 传 `attacker` 代替 `target` |

## 已知缺口

1. **普攻与技能特效不共享**：技能仍用旧的 `road.png`（`:26`），普攻用新的 norVFX。
   将来想统一，把 `ResolveWindclawImpactSprite` 那套也换成 Animator 即可。
2. **特效不跟随敌人移动**：spawn 时取一次位置后固定（与技能 impact 行为一致）。
   若敌人跑得快导致特效"脱靶"，可给 `ActiveFx` 传 `resolveEnd` 每帧刷新（该参数已存在，`Update` 里 `:409` 会调用）；当前 `Projectile=false` 不生效，需要另行处理。
3. **AI 侧同样生效**：`CombatFxView` 是 V1/V2 共用代码，且按 `side` 过滤（`OnCombat:469`），
   AI 半场的 Windclaw Ranger 普攻也会播此特效 —— 与项目「AI 与玩家共用反馈」的约定一致。
