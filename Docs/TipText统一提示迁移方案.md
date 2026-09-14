# TipText 统一提示迁移方案（V1）

> 日期：2026-09-09
> 范围：全项目"提示/浮层文字"UI 统一收敛到 `Assets/Resources/prefabs/TipText.prefab`
> 现状调研基于全量代码与场景扫描（见附录），行号均为真实行号。

---

## 一、背景与目标

项目新加了提示预制体 `Assets/Resources/prefabs/TipText.prefab`（根节点挂 `TipTextController`，含背景图 `Resources/Main/Tip.png`、TMP 文本 40 号字、937×80），但目前**没有任何代码消费它**。场景里各面板仍是各写各的旧提示：每个 Controller 自己 `transform.Find("TipText")` 拿一个裸 TMP 文本，自己写协程 3 秒（或 1.5 秒）后清空隐藏，视觉不统一、代码六处重复。

**目标**：项目里所有提示内容分阶段迁移到该预制体上——有提示内容时生成（懒加载复用）预制体实例，预制体发挥显示功能，默认 3 秒后自动消失；全部调用点收敛为一行静态 API；最终删除各场景/各控制器里的旧提示实现。

---

## 二、现状盘点（摘要）

### 2.1 旧实现清单（6 套 + 2 个边界）

| # | Controller | 场景 | 旧节点路径 | 机制 | 时长 |
|---|---|---|---|---|---|
| 1 | `MainMerchantController`（:104/:757） | Main | `MainPanel/MerchantPanel/Bg/TipText` | 协程隐藏 | 3s |
| 2 | `MainEnergyController`（:581/:672） | Main | `MainPanel/AddEnergyPanel/BG/TipText` + **运行时 new GameObject("TipText")**（:600-619，红字） | 协程隐藏 + 常驻型 `ShowMainTip` | 3s |
| 3 | `MainRuneUnlockController`（:61/:174） | Main（运行时 AddComponent） | `MainPanel/TipText`（即 #2 动态建的节点，**存在装配时序依赖**） | 协程隐藏 | 1.5s |
| 4 | `GreyboxHudView`（:298/:1517） | Greybox_Main | `Canvas/SafeAreaRoot/DragonBoundPortraitScreen/TipText`（active=0） | 协程隐藏 | 1.5s |
| 5 | `DragonBoundScreenView`（:332） | Greybox_Main | 同上（与 #4、#6 **共用同一节点**） | 协程隐藏 + version 防串 | 1.5s |
| 6 | `GreyboxRecruitmentPanel` / `RecruitmentButtonController`（两套并行实现） | Greybox_Main | 同上（共用） | 协程隐藏 | 1.5s |
| 边界 | `LoginController.ErrorText`（:87/:366） | Login | `MainPanel/ErrorText` | **常驻、不自动隐藏** | — |
| 边界 | Login `GoogleConfirmPanel/TipText` | Login | 静态确认弹窗文案 "Confirm to login ?"，无代码引用 | — | — |

场景内另有遗留静态节点：Main.unity 的 `MainPanel/Tip/TipText`（带同款背景条 GO 1835889388，无任何代码引用，疑似历史"底部提示条"）；Main.unity 共 3 个 TipText GO。

### 2.2 调用点规模（迁移工作量）

- `GreyboxHudView`：约 24 处（道具拖拽教程 6 处、使用失败格式化 2 处、Boss 播报 15 处：Soul Chain / Stormcaller / Bloodcrown / Worldeater / Merge blocked）
- `MainEnergyController`：约 22 处（常驻型 `ShowMainTip` 10 处 + 加体力面板 3s 提示 12 处）
- `MainMerchantController`：约 12 处（购买/抽奖的失败、上限、中奖提示）
- `MainRuneUnlockController`：1 处（`Unlocks on Day {n}`）
- `DragonBoundScreenView`：1 处（符文掉落名）
- 征兵两套：各 2 处（`Not enough Supplies. Need {n}.` / `Recruitment is currently unavailable.`）

合计约 **60+ 处调用点、6 套重复实现**。

### 2.3 已识别问题

1. 同一逻辑（置文字→协程→清空隐藏）复制 6 份，文案/时长/显隐行为散落各处，改样式要动 6 个文件 + 多个场景。
2. 旧提示是裸 TMP 文本（无背景条）；#2 还在运行时 `new GameObject` 拼红字提示，与其它提示视觉三种风格。
3. `MainRuneUnlockController`（sceneLoaded 时 AddComponent、Awake 里硬校验 `MainPanel/TipText` 存在，:64-72）依赖 `MainEnergyController` 是否已动态建好节点，存在装配时序隐患。
4. Greybox 场景三方（HUD/符文/征兵）共用一个 TipText 节点，争用策略只是隐含的"StopCoroutine 覆盖"。
5. 新预制体的 Image/TMP `RaycastTarget=true`，全屏置顶显示期间会挡点击，接线前需关掉。

---

## 三、目标架构

### 3.1 新增静态入口 `TipTextService`（唯一门面）

```csharp
// Assets/Scripts/UI/TipTextService.cs
public static class TipTextService
{
    private const string PrefabPath = "prefabs/TipText";
    private static TipTextController instance;   // 场景销毁后自动为 null → 下次调用重建

    /// 显示一条提示，默认 3 秒后消失；重复调用覆盖上一条并刷新计时。
    public static void Show(string message, float seconds = 3f, Vector2? anchoredPosition = null);

    /// 常驻提示（不自动隐藏），用于保留旧 ShowMainTip 的语义；由 Clear 或下一条提示顶掉。
    public static void ShowPersistent(string message);

    /// 立即清空隐藏。
    public static void Clear();
}
```

实现要点：

- **懒加载生成预制体**：首次 `Show` 时 `Resources.Load<GameObject>("prefabs/TipText")` 并实例化——父节点取当前激活场景的根 Canvas（各场景均为名为 `Canvas` 的 Screen Space Overlay），`SetAsLastSibling()` 置顶；实例命名 `[TipText]` 便于排查。
- **单实例复用（替换式）**：同一时刻只显示一条，后到的覆盖先到的并重置计时——这正是现有 6 套实现的共同语义（Greybox 三方共用节点现状即如此），迁移后行为不变。后续如需多条堆叠，再在 Service 内扩展实例池，调用方 API 不变。
- **跨场景自愈**：不做 `DontDestroyOnLoad`；场景切换时实例随 Canvas 销毁，静态引用因 Unity 重载 `== null` 自动判空，新场景再次调用即重建。
- **计时用 `WaitForSecondsRealtime`**（TipTextController 已实现），暂停/时间缩放不影响提示时长。
- 建议在 `TipTextController` 上补充 `ShowPersistent()`/`Hide()` 两个小方法支撑常驻语义（现有 `Show` 传空串即隐藏的行为保留）。

### 3.2 预制体微调清单（Phase 0 一次性完成）

| 项 | 现状 | 调整 |
|---|---|---|
| Image / TMP `RaycastTarget` | true | **改为 false**（置顶期间不挡点击，关键） |
| 根节点默认位置 | anchoredPosition (0,0) 屏幕中心 | 保持中心（与 Greybox 现状一致）；Main 场景如需偏下，调用点传 `anchoredPosition` 覆盖（已支持）。注释中"屏幕底部"与实际不符，一并修正注释 |
| 默认文本 | "New Text" | 清空 |
| 根节点尺寸 | 固定 937×80 | 运行时按文案 AutoSize：宽贴合文本且 ≤580，高 = 行数 × 80（见 §六） |
| 实例名 | TipText | 运行时由 Service 改名 `[TipText]`，避免与场景遗留节点混淆 |

### 3.3 语义决策表（迁移不改行为，只换载体）

| 旧语义 | 迁移后 |
|---|---|
| Greybox 系 1.5s（Boss 播报/拖拽教程/符文/征兵） | `Show(msg, 1.5f)`，**保留 1.5s**，避免 Boss 播报变 3s |
| Main 商人/体力面板 3s | `Show(msg)`（默认 3s） |
| `MainEnergyController.ShowMainTip` 常驻错误（"Not enough" 直到能量恢复才清） | `ShowPersistent(msg)` + 时机点 `Clear()` |
| `LoginController.ErrorText` 常驻错误文本 | **不迁移**（语义是表单错误，非浮层提示，保持现状） |
| Login `GoogleConfirmPanel/TipText` 静态确认文案 | **不迁移**（弹窗固定文案，无动态行为） |
| Main 场景遗留 `MainPanel/Tip/TipText` 静态节点 | **删除**（无代码引用；其背景条样式已被预制体继承） |

### 3.4 改造手法：壳转发，调用点零改动

每个 Controller 的私有 `ShowTip` 第一步只改方法体为一行转发（`TipTextService.Show(message, 1.5f)`），几十个调用点不动，把回归风险压到最低；场景旧节点与协程随后删除。最终是否把壳也去掉（调用点直写 Service），Phase 4 视情况决定。

---

## 四、分阶段实施

### Phase 0 — 基建（全局服务 + 预制体微调）

**改动**：新增 `Assets/Scripts/UI/TipTextService.cs`；微调 `TipText.prefab`（RaycastTarget 等 §3.2 清单）；`TipTextController` 补 `ShowPersistent/Hide`。
**验证**：临时调试入口（如 Greybox_Main 的 F9 面板加一个按钮，或临时代码）在 Main / Greybox_Main / Login 各弹一条：确认实例挂到 Canvas 顶层、3 秒消失、重复触发刷新计时、不挡点击、切场景后能再次弹出。
**交付标准**：新旧提示并存互不影响（此阶段不删任何旧代码）。

### Phase 1 — Main 场景·商店与符文（低风险试点）

**改动**：
- `MainMerchantController.ShowTip`（:757）壳转发 `Show(msg)`；删除 `tipText` 字段（:42/:104）、`hideTipCoroutine`、`HideTipAfterDelay`（:778）。
- `MainRuneUnlockController.ShowTip`（:174）壳转发 `Show(LockedTip, 1.5f)`；删除 `tipText` 字段（:61）、`ShowTipRoutine`（:180）、**Awake 里对 `MainPanel/TipText` 的存在性硬校验**（:64-72，只留 BagBtn/WeaponPanel 校验）——顺带消除对 #2 动态节点的时序依赖。
- 场景：Main.unity 删除 `MerchantPanel/Bg/TipText`。

**回归点**：金币不足购买、拥有上限、广告校验失败（"Video unavailable"）、抽奖中奖（"Won: xxx"）、未解锁时点背包出 "Unlocks on Day {n}" 且 1.5s 消失。

### Phase 2 — Main 场景·体力（含动态节点清理）

**改动**：
- `MainEnergyController.ShowTip`（:672）壳转发 `Show(msg)`；`ShowMainTip`（:702）改为 `ShowPersistent/Clear` 语义（能量恢复清空处 :627-631 同步改 `Clear()`）。
- 删除 `mainTipText`/`tipText` 双字段（:31/:581-586）、`CreateTipText` 动态建节点（:600-619）、`HideTipAfterDelay`（:693）。
- 场景：Main.unity 删除 `AddEnergyPanel/BG/TipText`；运行期不再出现 `MainPanel/TipText` 动态节点。

**回归点**：开始对局体力不足（常驻 "Not enough"，能量恢复后消失）、上局未结束（"Previous game is still active."）、加体力面板广告/分享达限与领奖失败提示 3s 消失、网络异常文案。

### Phase 3 — Greybox 战斗场景（三方共用节点迁移）

**改动**：
- `GreyboxHudView.ShowTip`（:1517）壳转发 `Show(msg, 1.5f)`；删除 `tipText` 字段（:53，注意 `[SerializeField]`，DragonBoundPortraitScreen.prefab 序列化会少一个字段，属预期）、`tipHideCoroutine`、`HideTipAfterDelay`（:1650）。
- `DragonBoundScreenView`：`HandleRuneRewardGranted`（:184-202）直接调 `Show(runeName, 1.5f)`；删除 `runeTipText/runeTipHideCoroutine/runeTipVersion` 与 `ResolveTipText()`（:332）、`BindRuneDropTip` 的注入链（`Initialize` :102-120 两处传参同步收口）。
- 征兵两套：`GreyboxRecruitmentPanel.ShowUnavailableReason`（:129）与 `RecruitmentButtonController.ShowUnavailableReason`（:145）壳转发 `Show(msg, 1.5f)`；`HideTip` 改 `Clear()`；删除各自 tip 字段与协程。
- 场景：Greybox_Main.unity 删除 `DragonBoundPortraitScreen/TipText`（GO 728074375）。
- 文档：`Docs/DragonBound/DragonBoundUIAuthoring.md` 增补一节"全局提示走 TipTextService，场景/Screen prefab 内不再放 TipText 节点"（同时协调该文档"不要 Unpack Screen Prefab"约定与现状内联结构的矛盾：新方案提示不属于任何 prefab，恰好规避该问题）。

**回归点**：道具点击/拖拽的全部教程与失败提示、五个 Boss 事件播报（Soul Chain/Stormcaller/Bloodcrown/Worldeater/Merge blocked）、征兵不可用点击与恢复隐藏、符文掉落名显示；跑一遍 DragonBound EditMode 测试套件（已确认测试代码不引用 TipText）。

### Phase 4 — 收尾清理与全量回归

- 删除 Main.unity 遗留 `MainPanel/Tip/TipText` 与 `Tip` 容器（GO 1835889388/1593168708，无代码引用）。
- 视情况去掉各 Controller 的转发壳，调用点直写 `TipTextService.Show(...)`；删除残留常量（如 `TipDurationSeconds`）。
- 全场景全提示点手工回归 + EditMode 测试；确认运行期层级里只有一个 `[TipText]` 实例。
- （可选，后置增强，本期不做）淡入淡出、多条堆叠实例池、成功/警告/错误三色样式、文案本地化。

---

## 五、预期效果

**运行时表现**（用户可感知）：
1. 任何提示触发 → 场景内首次调用时生成 `TipText.prefab` 实例（懒加载、全局唯一），显示带半透明背景条的居中文字（TMP 40 号、937×80），**3 秒后自动消失**（Greybox 战斗播报保留 1.5 秒节奏）；实例保留复用，不反复建删。
2. 提示重复触发时刷新计时、后到覆盖先到；常驻型错误（体力不足等）持续显示至状态恢复。
3. 提示期间不阻挡任何点击；不受游戏暂停/时间缩放影响；场景切换后自动在新场景重建；置顶显示，不被面板遮挡。

**视觉统一**：全项目从三种提示形态（裸文本 / 动态红字 / 无提示）统一为一种带背景条的提示样式；改字号、颜色、背景、出现位置只需改预制体一处。

**代码收敛**：6 套私有 ShowTip 实现 + 3 个场景共 5 个 TipText 节点 + 1 处运行时动态建节点，收敛为 `TipTextController`（预制体上，已有）+ `TipTextService`（静态入口，新增）共 2 个文件；约 60+ 个调用点最终一行调用；新增提示点不再需要建节点、写字段、写协程。

**隐患消除**：`MainRuneUnlockController` 对运行时动态节点的装配时序依赖解除；Greybox 三方共用节点的争用策略显式化为"全局替换式"；场景遗留无引用静态节点清空。

**风险可控性**：每阶段独立可验收、可回滚（壳转发模式下单文件 diff 极小）；Login 的常驻错误文本与确认弹窗文案按语义保留，不做无谓迁移。

---

## 六、预制体尺寸自适应（2026-09-09 追加需求）

> 需求：TipText 的 Width / Height 随 Text 内容变化；Width 上限 580；Height 每行 80（1 行=80，N 行=N×80）。
> 对应实现文件：`Assets/Scripts/UI/TipTextController.cs` + `TipText.prefab`（Text 子节点布局）。

### 6.1 目标形态

| 文案 | 表现 |
|---|---|
| 短文本（单行可容纳） | 条宽 = 文本宽度（最小可设下限），高度 80 |
| 中长文本（超过 580 需折行） | 条宽锁 580，自动换行，高度 = 行数 × 80（160/240/…） |
| 含显式 `\n` | 同样按最终行数 × 80 |

### 6.2 Prefab 结构调整（一次性，随 Phase 0 微调做）

```
TipText (Image 背景)  ─ RectTransform 需改为「随内容尺寸」的容器
└── Text (TextMeshProUGUI) ─ 挂 VerticalLayoutGroup? 否，直接代码控宽高
```

**推荐做法（不引入 LayoutGroup，避免 Override/坑）：在 Controller 里代码计算并设置根 RectTransform 尺寸。** 理由：LayoutGroup + ContentSizeFitter 对 TMP 的 preferred size 依赖文本重布局时序，且“宽≤580 + 高按行×80”需要自定义上限，纯代码最可控。

具体：
1. Text 子节点改为 `anchorMin/Max = (0,0)-(1,1)` 拉伸填满根（已是这样），确保换行宽度=根宽。
2. Controller 在每次 `Show` 写文本后调用 `ResizeToText()`：
   - 用 `label.GetPreferredValues(text)` 求**不换行自然宽** `nat`（TMP 的 preferred width；注意 `enableWordWrapping` 需按测量切换，避免用当前根宽换算）。
   - `targetWidth = Mathf.Clamp(nat.x, MinWidth, 580)`。
   - 设根宽 = targetWidth → `ForceMeshUpdate()` → 取 `textInfo.lineCount`（真实折行后行数，含显式 `\n`）→ `root.sizeDelta = (targetWidth, Mathf.Max(1,lineCount) * 80)`。
   - 折行使 `GetPreferredValues` 被根宽影响，故先测自然宽再设宽再数行，分两步保证正确。
3. 隐藏/清空消息时还原默认尺寸（或置 0），下次 Show 重新计算。

### 6.3 测量细节与坑（务必遵守）

| 坑 | 规避 |
|---|---|
| TMP 的 `preferredWidth` 受 `enableWordWrapping` 影响 | 测自然宽前临时 `enableWordWrapping=false`，测完恢复 `true`（换行模式） |
| `textInfo.lineCount` 需在 `ForceMeshUpdate()` 之后才有效 | 设置文本与宽度后先 `ForceMeshUpdate()` 再读行数 |
| `ForceMeshUpdate` 在 inactive 时可能不生成 | **决策：先激活后测量**。Show 顺序固定为：写 `label.text` → `SetActive(true)` → `ResizeToText()`。SetActive 与 Resize 同帧同步完成（LateUpdate/渲染前），视觉不闪跳；隐藏仍走 `SetActive(false)`+清空文本，维持既有语义，绝不在 inactive 下依赖 `lineCount` |
| 首帧 Canvas 尚未重建 | 若需要，`Canvas.ForceUpdateCanvases()` 后可读；一般 `ForceMeshUpdate` 已足够 |

### 6.4 常量集中定义（TipTextController 顶部，已统一命名）

> ⚠️ **当前文件编译失败（CS0103）**：`TipTextController.cs` 中的 `ResizeToText()` 草案已引用 `MinTipWidth / MaxTipWidth / RowHeightPerLine`，但全项目无任何定义。**第一步必须先把下方常量区加进文件顶部**，否则 Unity 无法编译。

尺寸相关常量全部收敛为控制器私有常量，集中放在 `DefaultShowSeconds` 之后、字段之前，样式调整只改这一处：

```csharp
[DisallowMultipleComponent]
public sealed class TipTextController : MonoBehaviour
{
    private const float DefaultShowSeconds = 3f;

    // ---- 尺寸自适应常量（样式调整只改这里）----
    private const float MaxTipWidth       = 580f;  // 提示条最大宽：超过则折行
    private const float MinTipWidth       = 120f;  // 最小宽：防过窄；设为 0 则完全贴合文本
    private const float RowHeightPerLine  = 80f;   // 每行高：1 行=80，N 行=N×80
    // -------------------------------------------------

    private TMP_Text label;
    private Coroutine hideRoutine;
```

命名规则：以 `Tip` 前缀体现领域（避免与场景其它 `MaxWidth` 撞名），常量值按需求原样保留。

### 6.5 Show 接入与时序（尺寸自适应的触发点）

`ResizeToText()` 由 `Show` 统一驱动，**每次展示都重新计算**（重复触发刷新时尺寸同步刷新，天然覆盖"上一条长文案残留 580×240 → 下一条短文案要缩回贴合"）：

```csharp
public void Show(string message, float seconds, Vector2? anchoredPosition = null)
{
    if (label == null) Resolve();

    if (hideRoutine != null)          // 先取消上次计时（复用语义）
    {
        StopCoroutine(hideRoutine);
        hideRoutine = null;
    }

    if (anchoredPosition.HasValue)
        (transform as RectTransform).anchoredPosition = anchoredPosition.Value;

    if (string.IsNullOrEmpty(message))   // 空串 = 隐藏（旧调用点语义）
    {
        label.text = string.Empty;
        gameObject.SetActive(false);
        return;
    }

    label.text = message;
    gameObject.SetActive(true);      // ① 先激活：TMP inactive 时 ForceMeshUpdate 不产 mesh
    ResizeToText();                  // ② 再量尺寸：同帧完成，不闪跳
    transform.SetAsLastSibling();    // ③ 置顶
    hideRoutine = StartCoroutine(HideAfterDelay(seconds));
}

private void ResizeToText()
{
    RectTransform root = (RectTransform)transform;

    // 1) 量「不换行自然宽」：临时关折行，避免被当前根宽/上次宽度干扰
    bool wasWrap = label.enableWordWrapping;
    label.enableWordWrapping = false;
    float naturalWidth = label.GetPreferredValues(label.text).x;
    label.enableWordWrapping = wasWrap;

    // 2) 宽 = clamp(自然宽, Min, Max)
    float targetWidth = Mathf.Clamp(naturalWidth, MinTipWidth, MaxTipWidth);

    // 3) 设宽后重排文本，ForceMeshUpdate 后再按真实行数定高
    root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
    label.ForceMeshUpdate();
    int lines = Mathf.Max(1, label.textInfo.lineCount);   // 含显式 \n 折出的行
    root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, lines * RowHeightPerLine);
}
```

**三条时序铁律（对应 §6.3）**：
1. 测自然宽前**临时关 `enableWordWrapping`**，量完恢复——否则返回值是"按当前根宽折行后"而非真实文本宽。
2. `SetActive(true)` **必须在** `ForceMeshUpdate()` 之前（inactive 不产 mesh，`lineCount` 无效）。
3. 先设**宽** → `ForceMeshUpdate()` → 读**行数** → 再设**高**；顺序反了行数不准。

`root.SetSizeWithCurrentAnchors` 在预制体 anchor=(0.5,0.5) 时等价直接改 `sizeDelta`，无需关心锚点换算；Text 子节点 (0,0)-(1,1) 拉伸铺满，宽度自动跟随根宽折行。

### 6.6 验收

- Main/Greybox_Main 各弹一次单行短文案 → 条宽贴合文本、高 80。
- 弹 60 字长文案 → 条宽 580、自动折 2~3 行、高 160/240，文字完整可读。
- 弹含 `\n` 两行文案 → 高 160。
- 弹空串 → 隐藏，不残留旧尺寸。
- 反复触发刷新计时时尺寸同步刷新。

---

## 附录：调用点明细（迁移核对清单）

### GreyboxHudView（Greybox_Main，全部 1.5s）
| 行 | 触发 | 文案 |
|---|---|---|
| 811/817/823 | 点击路径/敌方/友军类道具 | Drag onto enemy path / an enemy / a basic unit |
| 834 | TryUseItem 无存活目标 | No enemies |
| 888-892 | OnEndDrag 非法落点 | 同上三选一 / Invalid placement |
| 909 | 道具使用失败 | On cooldown / Maximum stacks(level) reached / Invalid unit / No enemies / Invalid placement（FormatActiveItemFailure :958） |
| 1533 | BasicMergeBlocked | Boss : Merge blocked |
| 1541/1545-47/1551 | Soul Chain | incoming / locked {n} unit(s) / found no target / interrupted |
| 1560/1564/1568 | Stormcaller | incoming / shielded and hastened {n} / interrupted |
| 1577/1581/1585 | Bloodcrown | incoming / All Basic units are treated as Lv1… / interrupted |
| 1594-96/1600/1604-08 | Worldeater | targeting Devour / summoning / interrupted / Devoured…HP / Summoned a SubBoss / Summoned {n} minions |

### MainEnergyController（Main）
- 常驻 `ShowMainTip`（10 处）：:124 网络恢复失败、:207/:244 体力不足 Not enough、:248 上局未结束、:252-254/:260 服务端错误、:230/:295/:630 清空。
- 面板 3s `ShowTip`（12 处）：:375/:419/:427/:435 广告链路，:454/:503/:511/:526/:531-533/:539 分享链路，:73/:296/:308 清空。

### MainMerchantController（Main，3s）
:349 中奖 Won: {name}、:365/:532 Video unavailable、:369/:377 Unavailable、:464 上限、:523 Insufficient gold、:144/:320/:358/:469/:516/:527 清空（:535 默认失败走价格文本，不在迁移范围）。

### 其它
- `MainRuneUnlockController` :161 → `Unlocks on Day {n}`（1.5s）。
- `DragonBoundScreenView` :198 → 符文显示名（1.5s）。
- `GreyboxRecruitmentPanel` :71/:123 与 `RecruitmentButtonController` :83/:139 → `Not enough Supplies. Need {n}.` / `Recruitment is currently unavailable.`（1.5s）。

### 不迁移项
- `LoginController.ErrorText`（常驻表单错误）、Login `GoogleConfirmPanel/TipText`（静态确认文案）。
- Greybox 的 `BossWarning` 弹板（独立 OverlayRoot 体系，非 ShowTip）。

---

## 待确认项（实施前拍板）

1. 预制体默认位置：屏幕中心（与 Greybox 现状一致）还是偏下（Main 旧 Tip 条位置）？→ 建议：默认中心，调用点可传位置。
2. Boss 播报与道具教程共用替换式单实例（与现状一致）是否满足产品预期，还是需要后到排队？→ 建议：先与现状一致，实例池列为后置增强。
3. Main 场景遗留 `Tip` 容器条是否确认删除？→ 建议：Phase 4 删（当前无代码引用）。
