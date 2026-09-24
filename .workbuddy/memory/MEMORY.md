# Dragon 项目长期记忆

## UI 版本化：V1/V2（分支 `codex/ui-v1-v2-isolation`）
- 目标：字符串路径 → 「资源键 + 语义节点键」，V1/V2 共用业务代码，构建期强制隔离。
- 基础设施：`UiNodeBindingMap.FindUi`（显式绑定 → Find(name) → 子树叶子名唯一匹配）；`UiAssetRegistry`（SO + `UiAssets.Load`，靠 `PlayerSettings.preloadedAssets` 激活，编辑器下 fallback `Resources.Load`）；`UiVariantProjectPaths`（路径常量，仅测试/工具）；`Editor/Versioning/DragonBoundUiVersionTools.cs`（菜单 `DragonBound/Versioning/*`、`DragonBound/Build/*`）；`DragonBoundUiBuildGuard.cs`（未选版本直接 BuildFailed）；`DragonBoundUiVariantSceneSelector.cs`（按打开场景自动 SetActiveV1/V2）；`BottomNavigationSelection.cs`。
- 布局：`Assets/DragonBound/UI/Variants/{V1,V2}/{Config,Content,Scenes}`；运行时配置已外移到 `Shared/Resources/Configuration/`。
- **V2 资源键** = 相对 `Variants/V2/Content/Resources` 的路径去扩展名；放文件后跑 `Regenerate V2 Asset Registry`。V2 注册表曾仅 42 key（V1 1305）。
- 待办：`FindUi` 未命中一律 LogError（可选绑定会红噪）；泛型键("Text"/"Image")被叶子名兜底误命中；无负缓存全树扫描；`DebugRangeBandLabel` 是死键。

## 通用坑（高频）
- **`[SerializeField]` 场景实例值覆盖代码默认值**：改代码默认不生效，必须开对应场景改 Inspector 并 Ctrl+S（V1/V2 各一份）。Bootstrap 挂在场景根节点 **`Systems`** 上（V1/V2 fileID 均 649599053），搜 `t:DragonBoundBootstrap` 直达。
- 手改"删 if 条件"会留下孤立 `{...}` 块，不报编译错但静默改控制流（曾导致特效整个不出现）。
- `AiStrategyProfile.cs` 无 UnityEngine using → 不能用 `Mathf`，用三元。
- 敌人/特效动画实际播放速度 = `Animator.speed × AnimatorState.m_Speed`；代码里 `clip.length` 不含 state speed，两者不一致会导致动画只播到前几帧就随 FX 销毁（Runebolt Projectile 的 state speed=0.1，实测只播到 30%）。
- 诊断工具：`Temp/imgtool.py`（纯 zlib PNG 解码，**RGBA 4 字节/像素**）、`Temp/bbox_frames.py`、`Temp/sheet_projectile.py` —— 可从截图量出光柱/爆闪/英雄坐标与角度。
- 文档追加：heredoc 含 ``` 代码块会解析失败 → 用 Write 写临时文件再 python 追加，追加后 grep 计数校验防重复；`rm` 被 safe-delete 拦截 → 用 `os.remove`。

## 统一提示 TipText（方案见 Docs/TipText统一提示迁移方案.md）
- `Assets/Resources/prefabs/TipText.prefab` + `Scripts/UI/TipTextController.cs` + 静态 `TipTextService`（懒加载/单实例/替换式/跨场景自愈）。
- 迁移手法：Controller 私有 `ShowTip` 先"壳转发"到 `TipTextService.Show`，再删场景节点与旧协程。时长：Greybox 1.5s、Main 商人/体力 3s。
- 旧实现 6 套/60+ 调用点；不迁移：Login ErrorText、GoogleConfirmPanel、BossWarning。

## Main 场景约定
- Tab 双态底图：改 Controller 单一切换入口切 sprite（targetGraphic → GetComponent → 子节点 Image，`transition=None`）。已落地：`MainLeaderboardController.SelectPeriod()`、`MainMerchantController.ShowTab()`。
- `Scripts/UI/MainNavTabController.cs`：BagBtn onClick 运行期被 MainRuneUnlockController 重建；RankingBtn 持久化 onClick 先于 AddListener → 勿依赖监听顺序。

## 结算 / 段位 / 调试
- 总闸 `Scripts/Rank/Services/GameSettlementCoordinator.cs`：`FinishRunAsync` → `ApplyRank && !ledger.RankApplied` → `RecordVictory/DefeatAsync` → 升段写 `RankPromotionStore`；Ledger 存 PlayerPrefs `dragonbound.settlement-ledger.<sha256(runId)>` 保幂等。
- 网关二选一：`LocalPlayerRankGateway` / `GoPlayerRankGateway`（`GoServerResourceGateways.cs:439-487`，Record* 空壳）；装配在 `GoUnaryServiceModule.cs:26` / `LocalServiceModule.cs:12`。
- 规则 `Rank/Rules`：L1-3 不掉星、L10 扣1不降段、L4-9 扣1可降段；每段 3/4/5 星。
- **阻断点**：在线模式 `GoUnaryGameplayRunGateway.cs:271/397` 把 `ApplyRank` 硬编码 `false`，加星全靠 Go 服务端 `/v1/runs/{id}/finish`（后端 PENDING）。
- `Scripts/Rank/UI/DebugDirectFinishController.cs`：Greybox_Main F9/F10 强制 Victory。Greybox_Main 由 TwentyWavePressureRuntime + MatchController.TryTransition 状态机驱动；WAVE1 直接 Victory 曾触发服务端 409。
- 加载链路：场景激活 → `DragonBoundBootstrap.InitializeRuntime`（可 defer 等 snapshot）→ Match Ready（停 1s）→ `TwentyWave.StartRun()` → BeginWave(1)。`Scripts/UI/GameplayLoadingPanelController.cs` 自动挂 LoadingPanel（复用 `Canvas/LoadingPanel/BG`），Running 隐藏，20s 兜底；SceneLoader 仅 Login 解析 LoadingImg。

## V2 英雄特效：Runebolt Mage 线路光柱（见 Docs/V2英雄普攻特效_RuneboltMage线路Projectile接线步骤.md 第 1~17 节）
- 玩法：普攻 `AttackKind.RuneboltPierce`，`PierceLength=5`、`PierceWidth=0.35`、`MaxTargetsByLevel={4,5,6}`（`FrozenHeroConfiguration.cs:489-495`）；走廊 = 5 格长 × **0.35 格宽**，方向朝"最前"敌人；1 格 = 110 单位；`IsInsideLine` 侧向阈值 0.175 格（≈±10px@59px/格）。
- 代码：`CombatFxView.cs` 常量 :101(V1 高 300)/:104(V2 高 100)/:108 padding 0.96/:109 max 550/:110 hold 0.12/:112 fade/:113 fallback；`SpawnRuneboltMageBolt` :864→(:983 `Play(0,0,1f/6f)`、:985 `speed=0` 定格帧2)；`SpawnRuneboltMageImpact` :1028；`CompleteRuneboltMageImpact` :1108 取**实时**位置。
- **决定性坑（已量化）**：`Animator` 实际速度 = `animator.speed × AnimatorState.m_Speed`，而 `clip.length` 不含后者。实测 `RM nor Projectile` clip 0.1s/6帧 但 state **speed=0.1** → 0.3s 寿命里只走 0.03s，永远停在帧1~2（帧1 覆盖 56%、左空 39%）；`RM nor Impact` 0.133s/8帧 state **speed=0.5** → 只播到"实心盘"帧4；三套 clip 的 `m_LoopTime` 全 = 1（Projectile 会在 hold 期间循环回最空的帧1）。→ 只调 padding/hold 永远治不好。
- 批处理模型：`QueueRuneboltMageCast` 按 **帧** 分批（`CreatedFrame != Time.frameCount` 就 release 旧的）；光柱终点 = 本批最远受伤敌人（`RefreshRuneboltMageTargetPositions` 刷实时位置 + `SortByDistance` 升序取 `Shots[Count-1]`）。**爆闪可比重光柱多活 0.133s**（在光柱 onComplete 才补完）→ 截图上"更远敌人有爆闪但光柱没到"多为上一发残影，不一定是同批。
- **玩法侧才是"到不了最远敌人"的主因**：走廊半宽 = `PierceWidth/2` = 0.175 格 → 实测常常只命中 1 个（光柱只到 2.13 格）。改 `FrozenHeroConfiguration.cs:615` `PierceWidth` 0.35→1.0~1.2（±0.5~0.6 格 = 收整排）才能真正"穿过中间"；只想视觉穿过则用 `RuneboltMageMinimumPathLength`(330f=3格) 做长度下限（代价：末端越过目标、爆闪提前）。
- 已完成（用户手改）：`RM nor Projectile.controller m_Speed 0.1→1`、`RM nor Projectile.anim m_LoopTime 1→0`、V2 场景 fallback 0.16 / hold 0.12。
- 未完成项：①`:985` 的 `animator.speed = 0f` 定格**仍在** → 删掉即恢复动态（保留 `Play(0,0,1f/6f)` 从帧2起播，帧2 覆盖 92%、帧6 85% 且 m_LoopTime=0 停末帧）；②padding 应回 0.9（定格解除后按帧6 可见段 [7.3%,91.3%] 推导，0.9 → 末端 1.014D）；③`RM nor Impact.controller m_Speed 0.5→1` + Impact duration 改 `clip.length/stateSpeed`；④起点偏移 24→8 收英雄侧空档；⑤彻底方案 B：要美术出首尾满幅可平铺的光带（`Image.type=Tiled` + 末端头光）。

## Forgekeeper's Gift（2026-09-12 落地）
- 商店 ID `ITEM_FORGEGIFTERS_GIFT`（typo）／运行时 `ITEM_FORGEKEEPERS_GIFT`（`DevelopmentItemRunSnapshotProvider.cs:16` 兼容）。120 金、Legendary、Passive、金币可买可带入 Run。
- 链路：`ForgekeepersGiftEffect.Tick`(90s) → `IItemForgePickPort.TryBeginForgePickClaim` → `ForgekeepersGiftPanelController`；注入链 Bootstrap.PlayerForgePickPort → TwentyWavePressureRuntime → ItemRunRuntime。
- 遗留 bug：`MerchantItemCatalog.cs:74` 末尾 `false` 应为 `true`。文档：`Docs/ForgekeepersGift_修改方案.md` 有效，`...具体实现.md` 已过时。

## Git / GitHub
- 远程 `https://github.com/mirya615-creator/Dragon.git`；分支 `codex/merge-dragonbound`、`codex/ui-v1-v2-isolation`、`master`；单文件硬限 100MB。
- **必忽略**：`.codely-cli/`（.db 311MB）、`.codely/clipboard/`；保留 `.codelyignore`。`Firebase/Plugins/x86_64/*.bundle|*.so` 不入库；**保留** `FirebaseCppApp-13_14_0.dll`、`m2repository/`、`GeneratedLocalRepo/`。
- 大文件排查：`git rev-list --objects <range> | git cat-file --batch-check`。
- 非交互推送报 `could not read Username`（凭据在 Windows 凭据管理器）：`git credential fill` 取 U/P 后 `git -c credential.helper= push "https://$U:$P@github.com/..." <br>`，勿改 remote。LFS 已启用，push 加 `-c lfs...locksverify=false`。
- 历史重写：本机无 `git filter-repo`，有 `filter-branch`；曾用 `reset --soft` 压缩 10 commit 后 fast-forward。Codex checkpoint ref 只占本地，不影响推送。

## Google 登录 / Android 桥接
- 唯一注入点 `Services/Composition/GoUnaryServiceModule.cs:22` 是 `UnavailableGoogleOAuthProvider`（backendMode=1）→ Google 按钮必失败。契约 `Auth/Contracts/IGoogleOAuthProvider.cs` 够用。
- 坑：`LoginController.cs:248-255` 要求 email 非空且 verified，`GoogleIdTokenCredential` 不给 email → 需自行 Base64Url 解 ID Token JWT。`/v1/auth/google`、`/link/google`、`/refresh` 已实现；`LinkGoogleAsync` 无调用方；刷新仅 401 被动（`RefreshingUnaryTransport.cs:31`）。
- androidlib 唯一正确形式：`Xxx.androidlib` + 自带 `build.gradle` + `src/main/java/<pkg>/Xxx.java`。禁止散放 .java 到 `Assets/Plugins/Android/`、禁止用 EDM4U 给 androidlib 注入依赖。引擎 团结 2022.3.62t14；`compileSdk 35`/`minSdk 24`；必须 `consumerProguardFiles`。依赖：`androidx.credentials`+`credentials-play-services-auth` 1.6.0、`googleid 1.2.0`、`androidx.core 1.15.0`；异常类 `GetCredentialCancelationException`（单 l）。本项目零 JNI 先例；`serverClientId` 必须传 Web Client ID。详见 Docs/AndroidGoogleLogin_P0S2_Java桥接实施步骤.md。
