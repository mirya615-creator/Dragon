# UI 路径绑定盘点报告

> 扫描时间：2026-09-14
> 扫描范围：`Assets/**/*.cs`（已排除 `TextMesh Pro/Examples & Extras`、`Library`、`obj`、`Temp`）
> 扫描脚本：`tools/scan_ui_path_binding.py`、`tools/scan_resources_load.py`
> 明细数据：`tools/ui_path_binding_report.json`、`tools/resources_load_report.json`

## 一、口径说明

"按路径访问绑定 UI"指**不通过 Inspector 序列化引用**，而是用**字符串层级路径 / 子物体名 / 子节点序号**在运行时反查控件。共分四类：

| 代号 | 写法 | 说明 | 脆弱度 |
|---|---|---|---|
| **A** | `transform.Find("A/B/C")` | 多段路径，任意一层改名/改层级即断链 | 高 |
| **B** | `transform.Find("ChildName")` | 单段子物体名，依赖名字 | 中 |
| **C** | `GetChild(0)` / `GetChild(i)` | 纯序号，插入/排序变动即错位 | 高 |
| **D** | `Resources.Load<T>("Path/Name")` | 资产路径绑定（非节点，附带统计） | 中 |

> 注：`FindObjectOfType<T>()`（94 处）按**类型**查找、不依赖路径，未计入本次统计。

## 二、总量概览

### 2.1 节点路径访问（A + B + C）

| 指标 | 数值 |
|---|---|
| **总调用点** | **328** |
| 涉及文件 | **49** |
| 去重字面量 | 198 |
| 其中 A 类（多段路径） | **62 处 / 54 个不同路径** |
| 其中 C 类（`GetChild` 序号） | 24 处 |
| 已做 null 保护（`?.`）的调用点 | 81 / 328（24.7%） |

按访问方式拆分：

| 方式 | 数量 |
|---|---|
| `Transform.Find("...")` | 299 |
| `GetChild(index)` | 24 |
| `GameObject.Find("...")` | 5 |

按代码归属拆分：

| 分类 | 调用点 | 文件数 |
|---|---|---|
| **运行时** | **198** | 38 |
| 测试 | 95 | 7 |
| 编辑器工具 | 35 | 4 |
| 合计 | 328 | 49 |

### 2.2 资产路径访问（D 类，附带）

| 分类 | 数量 |
|---|---|
| 运行时 | 86 |
| 测试 | 54 |
| 编辑器工具 | 3 |
| **合计** | **143**（Sprite 98 / Animator 27 / GameObject 13 / 其他 5） |

## 三、运行时重点文件排行（A + B + C）

| 排名 | 文件 | 调用点 | 其中多段路径 | 主要绑定对象 |
|---:|---|---:|---:|---|
| 1 | `Assets/Scripts/Merchant/MainMerchantController.cs` | 27 | 11 | 商城主面板整棵树（Tab、抽奖、物品列、删除弹窗） |
| 2 | `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 23 | 2 | Greybox HUD（资源/波次/暂停/结算/Boss 警告） |
| 3 | `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 15 | 3 | 排行榜（周/月榜、名次、星级） |
| 4 | `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 14 | 1 | 符文武器面板（分页、武器格、数量） |
| 5 | `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 11 | 0 | 敌人血条 Track/Fill、overHp、Animator |
| 6 | `Assets/DragonBound/Runtime/Presentation/DragonBoundScreenView.cs` | 9 | 4 | 战斗主屏（Overlay、营地、招募按钮、HpBg） |
| 7 | `Assets/Scripts/Auth/Energy/UI/MainEnergyController.cs` | 8 | 6 | 体力面板（Add/Panel/Close/Video/Share/数量） |
| 8 | `Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs` | 7 | 1 | 棋盘沙滩容器、BeachItem、选中态 |
| 9 | `Assets/DragonBound/Runtime/Presentation/GridCellView.cs` | 7 | 0 | 格子锁遮罩、调试标签、输入接收器 |
| 10 | `Assets/DragonBound/Runtime/Presentation/FixedBoardCanvasView.cs` | 6 | 1 | 固定棋盘各 ART 图层 |
| 11 | `Assets/Scripts/Rank/UI/GameRankResultController.cs` | 6 | 0 | 结算面板（金币、领取、双倍） |
| 12 | `Assets/Scripts/Rank/UI/MainRankController.cs` | 6 | 0 | 段位面板（三/四/五星、Img） |
| 13 | `Assets/Scripts/SignIn/UI/MainSignInController.cs` | 6 | 0 | 签到面板 |
| 14 | `Assets/Scripts/UI/PlayerAvatarView.cs` | 6 | 3 | 头像（BG/MyPart/EnemyPart 的 Image） |
| 15 | `Assets/Scripts/UI/GameplayLoadingPanelController.cs` | 5 | 6 | Loading 面板胜率文本（**全为 4-5 段深路径**） |
| 16 | `Assets/DragonBound/Runtime/Presentation/CampPanelView.cs` | 4 | 0 | 营地面板 + 3 处 `GetChild(i)` |
| 17 | `Assets/Scripts/Rank/UI/GreyboxSettlementRewardController.cs` | 4 | 0 | 结算奖励按钮 |
| 18 | `Assets/Scripts/UI/MainNavTabController.cs` | 4 | 0 | 底部导航三按钮 + `GetChild` |
| 19 | 其余 20 个文件 | 各 1–3 | — | 见 JSON 明细 |

## 四、多段路径（A 类）全清单 —— 最高风险

任何一层父节点改名、加中间节点、调整层级都会静默断链。

### 4.1 三级及以上

| 文件 | 路径 |
|---|---|
| `MainMerchantController.cs` | `Bg/LotteryContainer/LotteryBtn` |
| `MainMerchantController.cs` | `Bg/MyItemBg/ItemContainer` |
| `GameplayLoadingPanelController.cs` | `BG/MyPart/MyItem/Text (TMP)/RateText` |
| `GameplayLoadingPanelController.cs` | `BG/PlayerPart/PlayerItem/Text (TMP)/RateText` |
| `GameplayLoadingPanelController.cs` | `BG/EnemyPart/EnemyItem/Text (TMP)/RateText` |
| `PlayerAvatarView.cs` | `BG/MyPart/Image` |
| `PlayerAvatarView.cs` | `BG/EnemyPart/Image` |
| `UiImageFadeLoop.cs` | `ImageA/ImageB` |

> 注意 `GameplayLoadingPanelController` 同一个控件写了 **两套路径**（带/不带 `BG/` 前缀）做 fallback，说明层级本身已经不稳定。

### 4.2 二级路径

| 文件 | 路径 |
|---|---|
| `MainMerchantController.cs` | `Bg/ChatItemCon`、`Bg/LotteryContainer`、`Bg/ChantBtn`、`Bg/LotteryBtn`、`Bg/ItemContainer`、`Bg/CancleItemPanel`、`Bg/CancleBtn`、`Bg/ConfirmBtn` |
| `DragonBoundScreenView.cs` | `ART_ScreenBackground/GameOverlayController`、`ART_ScreenBackground/campPanel`、`ART_ScreenBackground/RecruitmentButtonController`、`ART_ScreenBackground/ART_RecruitButton` |
| `MainEnergyController.cs` | `EnergyBg/AddBtn`、`EnergyBg/RAmount`、`EnergyBg/MaxAmount`、`BG/CloseBtn`、`BG/VideoBtn`、`BG/ShareBtn` |
| `MainLeaderboardController.cs` | `LeaderLimit/LeaderContainer`、`LeaderImg/Text`、`RankText/StarAct` |
| `GreyboxHudView.cs` | `Bg/PauseBtn`、`Bg/ContinueBtn` |
| `ForgekeepersGiftPanelController.cs` | `Bg/VideoReward`、`Bg/CloseBtn` |
| `LoadingPanelEntranceAnimator.cs` | `BG/EnemyPart`、`BG/MyPart` |
| `PlayerAvatarView.cs` | `BG/Image` |
| `GreyboxBoardView.cs` | `ART_ScreenBackground/BeachContainer` |
| `RuneWeaponPanelController.cs` | `MyHeroBg/HeroContainer` |
| 编辑器 | `DragonBoundPortraitUiBuilder.cs`：`ART_BossTrack/ART_BossFill`、`ART_PauseButton/PauseLabel`、`AiUnitLayer/ART_AiRangePreview`、`PlayerUnitLayer/ART_PlayerRangePreview` |

## 五、按序号访问（C 类，24 处）

最容易在 UI 增删时静默错位：

| 文件 | 行号 | 表达式 |
|---|---|---|
| `MainMerchantController.cs` | 109 / 113 | `GetChild(0)` / `GetChild(1)` 兜底 Active/PassiveColumn |
| `MainMerchantController.cs` | 226 / 659 / 948 / 963 | 循环遍历子节点 |
| `GreyboxBoardView.cs` | 2121 | `GetChild(childIndex)` |
| `MainLeaderboardController.cs` | 197 | `GetChild(index)` |
| `RuneWeaponPanelController.cs` | 652 / 730 | `GetChild(index)` |
| `MainRankController.cs` | 184 | `GetChild(index)` |
| `PlayerAvatarView.cs` | 94 / 185 | `GetChild(index)` |
| `CampPanelView.cs` | 501 / 522 / 548 | `GetChild(i)` ×3 |
| `MainNavTabController.cs` | 201 | `GetChild(index)` |
| `DragFillController.cs` | 78 | `GetChild(0)` |
| `AnalyticsConsentUiController.cs` | 252 | `GetChild(index)` |
| `MainRuneUnlockController.cs` | 201 | `GetChild(index)` |
| `VoidShovelRewardController.cs` | 536 | `GetChild(index)` |

## 六、`GameObject.Find` 全局查找（5 处）

全部集中在测试文件中，且都是按**根节点名**查找，风险低：

| 文件 | 行号 | 目标 |
|---|---|---|
| `BootstrapPlayModeTests.cs` | 70 / 82 | `AnalyticsBootstrap` |
| `BootstrapPlayModeTests.cs` | 92 | `Canvas` |
| `BootstrapPlayModeTests.cs` | 182 | `SafeAreaRoot` |
| `BootstrapPlayModeTests.cs` | 183 | `RIVER` |

生产代码中**没有** `GameObject.Find`，这一点是好的。

## 七、测试与编辑器

- **测试**：95 处，集中在 `BootstrapPlayModeTests.cs`（40）、`PortraitUiPrefabTests.cs`（28）、`FixedBoardCanvasPlayModeTests.cs`（16）。这些测试**用路径断言 UI 结构**，即"路径即契约"——改层级会同时打断测试，属于**良性耦合**，是当前唯一的路径回归保护网。
- **编辑器工具**：35 处，主要是 `DragonBoundPortraitUiBuilder.cs`（26，构建 Greybox UI 时反查刚创建的节点）、`GreyboxMerchantLoadoutSceneMigration.cs`（5）、`SignInFeatureValidation.cs`（3）、`AuthoredGreyboxUiMigration.cs`（1）。

## 八、风险与建议

### 主要风险

1. **静默断链**：A/B 类路径找不到时返回 null，运行期不抛错，只表现为"按钮没反应 / 文字空白"。328 处里仅 **81 处使用了 `?.` 空条件访问（24.7%）**，其余 75% 需要依赖外层 `if (x == null)` 或 NRE 才能发现问题——而多数控制器只在 `Awake` 里做一次整体校验，运行中再断链就完全不报。
2. **层级即契约**：预制体/场景重排会让 `Bg/...` 前缀整体失效。`GameplayLoadingPanelController` 已经出现双路径 fallback 这类"打补丁"痕迹。
3. **`GetChild` 序号绑定**：24 处，UI 顺序一变就指向错误控件，且不会报错。
4. **跨场景重复绑定**：`MainMerchantController` 是场景内控制器，路径硬编码到 `MerchantPanel/Bg/...`，任何面板重构都是全量返工。

### 建议（按性价比排序）

1. **短期**：给所有 A/B 类 Find 加统一的 `ResolveOrLog(path)` 辅助方法，找不到时 `Debug.LogError` 带完整路径，把静默失败变成显式失败。改动小、收益大。
2. **中期**：把 `GetChild(0)/GetChild(1)` 兜底换成按名字查找（`Find("ActiveColumn")`），或直接删掉兜底分支，只保留显式命名。
3. **中期**：把高频面板（Merchant、Leaderboard、RuneWeaponPanel、Energy）的 `[SerializeField]` 引用替代路径查找——这些控制器本身就是场景/prefab 上的 MonoBehaviour，Inspector 拖引用即可，完全没有理由用路径。
4. **长期**：若继续走路径方案，把路径提成 `const`（项目里已部分这么做，如 `SelectedTabSpritePath`）并集中到各面板一个 `XxxPaths` 静态类，让"契约"可被测试直接复用，避免测试和运行时各写一份字面量。

### 无需处理

- 测试文件的路径绑定：它们是**故意**的断言手段，保留。
- 编辑器 Builder 的路径查找：构建流程内的一次性查找，风险可控。

---

*扫描脚本可重复运行，路径：`tools/scan_ui_path_binding.py` / `tools/scan_resources_load.py`，输出 JSON 便于后续 diff。*
