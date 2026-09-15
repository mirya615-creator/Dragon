# Dragon 项目长期记忆

## UI 版本化：V1/V2 双版本（进行中，分支 `codex/ui-v1-v2-isolation`）
- 目标：`Resources.Load`/`Transform.Find` 字符串路径 → 「稳定资源键 + 语义节点键」，V1/V2 共用业务代码，构建期强制隔离。
- 基础设施：
  - `Runtime/Presentation/UiNodeBindingMap.cs`：`FindUi` 三级查找 = 显式绑定 → `Find(name)` → 子树叶子名唯一匹配。
  - `Runtime/Presentation/UiAssetRegistry.cs`：SO(`variantId`+entries) + 门面 `UiAssets.Load/LoadAll`；激活靠 `PlayerSettings.preloadedAssets`（OnEnable → Install）。**运行时无注册表才抛异常；编辑器下 fallback `Resources.Load`**。
  - `Runtime/Presentation/UiVariantProjectPaths.cs`：`V1Root/V2Root` 等路径常量（测试与工具用，运行时禁止）。
  - `Editor/Versioning/DragonBoundUiVersionTools.cs`（菜单 `DragonBound/Versioning/*`）：Prepare 基础设施 / Regenerate V1|V2 Registry / Migrate Runtime Config To Shared / Create V2 Initial Scene / Migrate UI To V1 / Set Active V1|V2 / Validate V1|V2 / Configure V2 Main Navigation；`DragonBound/Build/*` 出 V1|V2|Both。
  - `Editor/Versioning/DragonBoundUiBuildGuard.cs`：`IPreprocessBuildWithReport`，未选版本或无匹配 BuildProfile 直接 `BuildFailedException`。
  - `Editor/Versioning/DragonBoundUiVariantSceneSelector.cs`（2026-09-15 新增）：`InitializeOnLoad`，编辑器里按当前打开场景路径自动 SetActiveV1/V2。
  - `Runtime/Presentation/BottomNavigationSelection.cs`（2026-09-15 新增）：底部导航 Ranking/Main/Bag 选中态，用 Image alpha 1/0 + `transition=None`；配套 `Tests/EditMode/BottomNavigationSelectionTests.cs`。
- 布局：`Assets/DragonBound/UI/Variants/{V1,V2}/{Config,Content,Scenes}`，注册表 `Config/UiAssetRegistryV1|V2.asset`；**运行时配置已外移**到 `Assets/DragonBound/Shared/Resources/Configuration/`（ClientServiceConfig + Stages）。
- **V2 资源键规则**：key = 相对 `Variants/V2/Content/Resources` 的路径去扩展名（例：`Main/Merchant/图层 72` ← `V2/Content/Resources/Main/Merchant/图层 72.png`），放好文件后跑 `Regenerate V2 Asset Registry`。V2 注册表目前仅 42 key（V1 1305），缺全部 `Main/*`，改 V2 UI 前先补。
- 已知待办：`FindUi` 未命中一律 LogError（可选绑定语义会红噪）+ 泛型键("Text"/"Image")会被叶子名兜底静默命中错误节点 + 无负缓存全树扫描；`DebugRangeBandLabel` 是死键（仅 `GridCellView.cs:59/103`），建议连字段删除。

## 统一提示 TipText（方案见 Docs/TipText统一提示迁移方案.md）
- `Assets/Resources/prefabs/TipText.prefab` + `Scripts/UI/TipTextController.cs`（Show(msg, seconds=3, pos?)）；新增静态 `TipTextService`（懒加载、单实例、替换式、跨场景自愈）。
- 迁移手法：各 Controller 私有 `ShowTip` 先"壳转发"到 `TipTextService.Show`（调用点零改动），再删场景节点与旧协程。时长：Greybox 1.5s、Main 商人/体力 3s。
- 旧实现 6 套/60+ 调用点：`MainMerchantController`、`MainEnergyController`（运行时 new GameObject，且 `MainRuneUnlockController.Awake` 依赖该动态节点 → 时序隐患）、`MainRuneUnlockController`、`GreyboxHudView`(24)、`DragonBoundScreenView`、征兵两套。不迁移：Login ErrorText、GoogleConfirmPanel、BossWarning。

## Main 场景约定
- Tab 双态底图：改对应 Controller 的单一切换入口切 sprite，Image 解析顺序 targetGraphic → GetComponent → 子节点"Image"，`transition=None`。已落地：LeaderPanel 周/月榜 `MainLeaderboardController.SelectPeriod()`（Main/Rank/图层 26|27）、Merchant 商店/抽奖 `MainMerchantController.ShowTab()`（Main/Merchant/图层 72|73）。
- `Scripts/UI/MainNavTabController.cs`：BagBtn 的 onClick 运行期被 MainRuneUnlockController 整体重建；RankingBtn 的持久化 onClick 先于 AddListener 执行，需重建 onClick。勿依赖持久化监听顺序。

## 结算 / 段位 / 调试
- `Scripts/Rank/Services/GameSettlementCoordinator.cs` 是总闸：`FinishRunAsync` → `if (result.ApplyRank && !ledger.RankApplied)` → `RecordVictory/DefeatAsync` → 升段写 `RankPromotionStore`；Ledger 存 PlayerPrefs `dragonbound.settlement-ledger.<sha256(runId)>` 保幂等。
- 网关：`LocalPlayerRankGateway` 与 `GoServerResourceGateways.cs:439-487` 的 `GoPlayerRankGateway`（Record* 空壳，只 GET /v1/rank）；装配二选一（`GoUnaryServiceModule.cs:26` / `LocalServiceModule.cs:12`）。
- 规则 `Rank/Rules`：Level1-3 不掉星、Level10 扣1不降段、Level4-9 扣1可降段；每段星数 3/4/5。
- **阻断点**：在线模式 `Services/Gameplay/GoUnaryGameplayRunGateway.cs:271/397` 把 `ApplyRank` 硬编码 `false`，客户端永不加星，全依赖 Go 服务端 `/v1/runs/{id}/finish`（文档标注后端 PENDING）。
- `Scripts/Rank/UI/DebugDirectFinishController.cs`：Greybox_Main F9/F10 强制 Victory。Greybox_Main 是 TwentyWavePressureRuntime + MatchController.TryTransition 状态机驱动，勿按普通场景写。WAVE1 直接 Victory 曾触发服务端 409。

## Greybox_Main 加载链路
场景激活 → `DragonBoundBootstrap.InitializeRuntime`（可 defer 等 snapshot）→ Match 状态机 Ready（停 1s）→ `TwentyWave.StartRun()` → TryTransition(Running) → BeginWave(1)。API：`bootstrap.Match/State/StateChanged/IsInitialized/InitializationFailed`。
`Scripts/UI/GameplayLoadingPanelController.cs` 自动挂 LoadingPanel（复用 `Canvas/LoadingPanel/BG`）→ Running 隐藏，20s 兜底。SceneLoader 仅 Login 解析 LoadingImg。

## Forgekeeper's Gift（2026-09-12 落地）
- ID：商店 `ITEM_FORGEGIFTERS_GIFT`（typo 保留）/ 运行时 `ITEM_FORGEKEEPERS_GIFT`，`DevelopmentItemRunSnapshotProvider.cs:16` 字典兼容。120 金、Legendary、Passive、**金币可买可带入 Run**。
- 链路已通：`ForgekeepersGiftEffect.Tick`（90s）→ `IItemForgePickPort.TryBeginForgePickClaim` → `ForgekeepersGiftPanelController`（激励视频发铲 / 关闭不发，都重置 CD）。注入：`DragonBoundBootstrap.PlayerForgePickPort` → `TwentyWavePressureRuntime` 构造参 → `ItemRunRuntime`。
- 遗留 bug：`MerchantItemCatalog.cs:74` 末尾 `false` 应为 `true`（`Item()` 默认 `goldPurchasable=true`，此处显式传 false 与契约矛盾）。
- 文档：`Docs/ForgekeepersGift_修改方案.md` 有效；`...具体实现.md` 已过时（旧同步接口）。

## Git / GitHub 约束
- 远程 `https://github.com/mirya615-creator/Dragon.git`；分支 `codex/merge-dragonbound`、`codex/ui-v1-v2-isolation`、`master`；单文件硬限 100MB。
- **必忽略**：`.codely-cli/`（Codex 索引缓存，单 .db 311MB）、`.codely/clipboard/`；保留 `.codelyignore`。`Assets/Firebase/Plugins/x86_64/*.bundle|*.so`（macOS/Linux 无用）不入库；**保留** `FirebaseCppApp-13_14_0.dll`、`Firebase/m2repository/`、`GeneratedLocalRepo/`。
- 排查大文件：`git rev-list --objects <range> | git cat-file --batch-check`。
- 非交互推送会报 `could not read Username`（凭据在 Windows 凭据管理器）：`U/P=$(git credential fill <<< "protocol=https\nhost=github.com")`，然后 `git -c credential.helper= push "https://$U:$P@github.com/..." <br>`，勿改 remote。
- LFS 已启用（`*.dll` 等）：push 加 `-c lfs...locksverify=false` 静默。
- 历史重写：本机无 `git filter-repo`，有 `filter-branch`；曾成功用 `reset --soft` 压缩 10 commit 为 1 个后 fast-forward。
- Codex checkpoint ref（`refs/codex/turn-diffs/...`）会保护旧大对象，只占本地，不影响推送。

## Unity Android 原生桥接（Google 登录 P0，详见 Docs/AndroidGoogleLogin_P0S2_Java桥接实施步骤.md）
- 唯一正确形式：`Xxx.androidlib` + 自带 `build.gradle`，源码 `src/main/java/<pkg>/Xxx.java`，manifest 顶层。
- 禁止：散放 `.java` 到 `Assets/Plugins/Android/`；用 EDM4U 给 androidlib 注入依赖（只注入 unityLibrary，Gradle 不跨模块传递）。
- 引擎 团结(Tuanjie) 2022.3.62t14；build.gradle 硬编码 `compileSdk 35`/`minSdk 24`；仓库靠 `settingsTemplate.gradle`。必须用 `consumerProguardFiles` 保留包名。
- 依赖版本（2026-09 实查）：`androidx.credentials` + `credentials-play-services-auth` **1.6.0** 同版本、`googleid 1.2.0`、`androidx.core 1.15.0`；异常类 `GetCredentialCancelationException`（单 l）。

## Google 登录接入现状
- 线上唯一注入点 `Services/Composition/GoUnaryServiceModule.cs:22` 是 `UnavailableGoogleOAuthProvider`（`ClientServiceConfig.asset` backendMode=1）→ 当前 Google 按钮必失败。
- 契约 `Auth/Contracts/IGoogleOAuthProvider.cs` 够用（SignInAsync / CancelPendingSignIn）。
- 坑：`LoginController.cs:248-255` 要求 Email 非空且 verified，而 `GoogleIdTokenCredential` 不给 email → 需自行 Base64Url 解 ID Token JWT 取 `email`/`email_verified`。
- `/v1/auth/google`、`/link/google`、`/refresh` 已实现；`LinkGoogleAsync` 无调用方；刷新仅 401 被动（`RefreshingUnaryTransport.cs:31`）。本项目零 JNI 先例，Google 桥接是第一处；Credential Manager 的 `serverClientId` 必须传 Web Client ID。
