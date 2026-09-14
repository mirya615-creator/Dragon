# Dragon 项目长期记忆

## 统一提示 TipText（迁移方案已定，2026-09-09，方案见 Docs/TipText统一提示迁移方案.md）
- 目标物：`Assets/Resources/prefabs/TipText.prefab`（根挂 `Scripts/UI/TipTextController.cs`，Show(msg, seconds=3, pos?)，WaitForSecondsRealtime）。
- 规划：新增静态 `TipTextService`（懒加载 prefab、挂当前场景根 Canvas 顶层、单实例复用、替换式语义、跨场景自愈）；预制体需把 Image/TMP 的 RaycastTarget 关掉。
- 迁移手法约定：各 Controller 私有 `ShowTip` 先"壳转发"到 `TipTextService.Show`（调用点零改动），再删场景 TipText 节点与旧协程。时长按语义保留：Greybox 系 1.5s、Main 商人/体力 3s、体力常驻型用 ShowPersistent。
- 旧实现 6 套/约 60+ 调用点：MainMerchantController、MainEnergyController（含运行时 new GameObject 建 TipText，且 MainRuneUnlockController Awake 依赖该动态节点有时序隐患）、MainRuneUnlockController、GreyboxHudView（24 处）、DragonBoundScreenView、征兵两套；Greybox 三方共用节点 `DragonBoundPortraitScreen/TipText`。不迁移：Login ErrorText、GoogleConfirmPanel 静态文案、BossWarning 弹板。Main 场景有遗留静态 `MainPanel/Tip` 条（无代码引用）待删。

## Main 场景 Tab 面板"选中/未选中"双态底图切换（约定模式）
Main 场景各 Tab 面板按钮用两张成对底图表示选中/未选中态，均放在 `Assets/Resources/Main/<面板>/`，命名形如「图层 N.png」。实现统一走「修改该面板对应的 XxxController，在其单一 Tab 切换入口切换 sprite」模式：

- LeaderPanel 周/月榜：`MainLeaderboardController.cs`，`SelectPeriod()` 末尾加 `ApplyTabVisual()`；WeekBtn/MonthBtn 用 `Main/Rank/图层 26`(选中)/`图层 27`(未选中)。当前实现为：入口 SelectPeriod。
- Merchant 商店/抽奖：`MainMerchantController.cs`，`ShowTab()` 末尾调 `ApplyTabVisual(showChant)`；ChantBtn/LotteryBtn 用 `Main/Merchant/图层 72`(选中)/`图层 73`(未选中)。已实现（2026-09-04）。
- 通用要点：Image 解析顺序 targetGraphic → GetComponent<Image> → 子节点"Image"；`transition = Selectable.Transition.None`（纯 sprite 高亮，防 ColorTint 叠色）；默认打开面板的 Tab（如 Chant/Weekly）在 Awake/OnEnable 已先 ShowTab/SelectPeriod，故无需额外初始化。

## 面板开关/导航（Main 场景）
- `Assets/Scripts/UI/MainNavTabController.cs`：底部导航 BagBtn/RankingBtn/MainBtn 选中态 sprite（Main/select、Main/Noselect）按面板状态轮询切换。约束：BagBtn.onClick 运行期被 MainRuneUnlockController 整体重建，勿在场景持久化 onClick 上做双向逻辑；RankingBtn 场景持久化 onClick 先于 AddListener 执行，需重建 onClick 事件。

## 调试/结算链路
- `Assets/Scripts/Rank/UI/DebugDirectFinishController.cs`：Greybox_Main 场景 F9/F10/OnGUI 按钮强制 Victory → 状态机驱动结算，由 GreyboxSettlementRewardController.Start → GameSettlementCoordinator.PrepareAsync 完成服务端 finish。
- 注意：Greybox_Main 为 TwentyWavePressureRuntime + MatchController.TryTransition(Victory) 状态机驱动，勿按普通 Game 场景写法。WAVE 1 直接 Victory 曾触发服务端 409（Settlement conflicts with run state），需服务端确认波数/状态校验规则。

## Greybox_Main 加载/就绪链路（LoadingPanel 隐藏锚点）
- 场景激活 → `DragonBoundBootstrap`（Systems 根，可 defer 等外部 snapshot）`InitializeRuntime` → `Match` 状态机 `Ready`（固定停 `InitializationPromptSeconds=1` 秒）→ `TwentyWave.StartRun()` → `TryTransition(Running)` → `BeginWave(1)`。
- 公开 API：`bootstrap.Match`（MatchController）、`Match.State`、`Match.StateChanged`、`bootstrap.IsInitialized/InitializationFailed`。
- `Assets/Scripts/UI/GameplayLoadingPanelController.cs`(2026-09-07)：自动挂 Greybox_Main，复用场景内 `Canvas/LoadingPanel/BG`（默认 off），置顶打开 → Running 隐藏；20s 兜底。
- SceneLoader 仅对 Login 场景解析 LoadingImg（`MainPanel/LoginPanel/LoadingImg`），Main↔Greybox_Main 切换期无过渡加载 UI。

## Rank / 段位结算链路（2026-09-12 整理）
- 结算总闸：`Assets\Scripts\Rank\Services\GameSettlementCoordinator.cs`；`PrepareAsync` 顺序 `FinishRunAsync` → `if (result.ApplyRank && !ledger.RankApplied)` 调 `RecordVictoryAsync/RecordDefeatAsync` → 胜场升段才写 `RankPromotionStore`。Ledger 以 `dragonbound.settlement-ledger.<sha256(runId)>` 在 PlayerPrefs 持久化保证幂等。
- 服务接口：`Assets\Scripts\Rank\Contracts\IPlayerRankGateway.cs`；本项目实现两组——`LocalPlayerRankGateway`（按 `matchId` 哈希去重，胜 `+1` 败按规则减）与 `GoServerResourceGateways.cs:439-487` 的 `GoPlayerRankGateway`（`Record*` 为空壳，只 GET `/v1/rank` 拿快照）。装配在 `GoUnaryServiceModule.cs:26` / `LocalServiceModule.cs:12` 二选一。
- 规则：`Assets\Scripts\Rank\Infrastructure\RankProgressionRules.cs`；换算公式 L22-58；**败场扣星 L75-91**：Level 1-3（Recruit~Corporal）不掉星、Level 10（Dragon Marshal）扣 1 不降段、Level 4-9（Sergeant~General）扣 1 可能降段；每段星数 L115-120 = 1-3 级 3/段、4-6 级 4/段、7-9 级 5/段。
- **关键阻断点（2026-09-12 发现）**：在线模式 `Assets\Scripts\Services\Gameplay\GoUnaryGameplayRunGateway.cs:271/397` 把 `FinishGameplayRunResult.ApplyRank` 硬编码 `false`，导致 `Coordinator` 永远跳过客户端加星，**完全依赖 Go 服务端 `/v1/runs/{id}/finish` 处理 rank**。文档 `Docs\DragonBound\Drakeforge_Full_Config_Baseline_V2_2026-08-14.md:370-377` + `QA\Drakeforge_QA_Master_Test_Matrix_V2.md:61` 均标注 Rank 后端 "PENDING IMPLEMENTATION"——这与"新用户首胜不加星"现象吻合。修复路径见 2026-09-12.md 排查小节。

## Forgekeeper's Gift 商品契约（2026-09-12 落地）
- ID：商店 `ITEM_FORGEGIFTERS_GIFT`（typo 少 E，保留）/ 运行时 `ITEM_FORGEKEEPERS_GIFT`，通过 `DevelopmentItemRunSnapshotProvider.cs:16` 字典兼容。
- 价格：120 金 / **`GoldPurchasable=true`** / Legendary / Passive ——金币可买、可带入 Run。
- Run 内行为（已实现且联通，不要再补"接口孤岛"）：
  - `ForgekeepersGiftEffect.Tick`（`ItemEconomyFlowEffects.cs:64-138`）每 90s 倒计时；归零 → `IItemForgePickPort.TryBeginForgePickClaim(...)`。
  - `ForgekeepersGiftPanelController`（`Scripts/UI/`，实现 `IItemForgePickPort`）弹出 Forge Panel：videoRewardButton → 看完整激励视频 + TrySpawnRewardShovel 发铲；closeButton → Declined 不发；任意分支都重置 CD。
  - 端口注入路径：`DragonBoundBootstrap.PlayerForgePickPort`（`ItemForgePickPortRouter`）→ `TwentyWavePressureRuntime` 构造参数 `playerForgePick: PlayerForgePickPort`（Bootstrap.cs:727）→ `TwentyWavePressureRuntime.StartItemRuntimes` 调 `new ItemRunRuntime(..., forgePick: playerForgePick)`（TwentyWavePressureRuntime.cs:667/679）。
- 当前唯一 bug：`MerchantItemCatalog.cs:74` 末尾 `false` 应改成 `true`（2026-09-12 已出方案，待落地）。`Item()` 工厂方法签名 `bool goldPurchasable = true`（L134）—— 其他 19 件都没显式传，因为默认值就是 `true`；只有本件显式传 `false` 是**反默认**写法，与商品契约矛盾，进一步坐实是 bug 而非设计意图。
- 文档：`Docs/ForgekeepersGift_修改方案.md` 是当前方案稿；`Docs/ForgekeepersGift_具体实现.md` 已过时（描述旧同步接口 `TryGrantForgePick`，与今天的 `TryBeginForgePickClaim(Action<>)` 不符），待整体重写。

## 版本控制约束（2026-09-14，GitHub 推送踩坑）
- 远程 `https://github.com/mirya615-creator/Dragon.git`，工作分支 `codex/merge-dragonbound`；GitHub 单文件硬限 **100MB**。
- 两个必忽略项（曾致 10 个 commit 全被 `pre-receive hook declined` 拒收）：
  1. **`.codely-cli/`** —— Codex IDE 索引缓存，单个 `.db` 实测 **311MB**，被跟踪 **672 个文件**且持续增长。**永远不要入库**。
  2. **`Assets/Firebase/Plugins/x86_64/*.bundle`(107MB, macOS) / `*.so`(77MB, Linux)** —— 本项目为 **Windows 开发 + Android 构建**，两者无用（meta 里 `Win64=0`、`Android=0`）。**必须保留** `FirebaseCppApp-13_14_0.dll`（17MB，`Editor=1`+`Win64=1`）、`Assets/Firebase/m2repository/`(22MB)、`Assets/GeneratedLocalRepo/`(22MB)（Android Gradle 依赖）。
- 排查手法：`git rev-list --objects <range> | git cat-file --batch-check` 精确枚举待推送大文件（比 `find` 准）。历史重写工具：本机**无** `git filter-repo`，有 `git filter-branch` + `git-lfs 3.7.1`；远程分支为 0 commit 时重写零风险。
