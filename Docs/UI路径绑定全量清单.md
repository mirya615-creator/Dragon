# UI / 资产路径绑定全量清单

> 本文由 `tools/gen_path_binding_list.py` 从扫描结果自动生成，可重复运行。
> 扫描对象：`Assets/**/*.cs`（排除 `TextMesh Pro/Examples & Extras`）。
> 路径型查找 = `Transform.Find("A/B/C")` / `GameObject.Find("...")` / `GetChild(index)`；
> 资产路径 = `Resources.Load<T>("路径")`。`FindObjectOfType` 不按路径，已排除。

## 一、总览

| 项目 | 数量 |
|---|---:|
| 节点路径访问总处数 | 328 |
| 涉及文件数 | 49 |
| 去重路径字面量 | 198 |
| Transform.Find | 299 |
| GameObject.Find | 5 |
| GetChild | 24 |
| `Resources.Load<T>` 资产路径 | 143 |
| 编辑器 `AssetDatabase` 资产路径 | 40 |
| 场景名加载 `SceneManager.LoadScene` | 60 |

按归属分布：

| 归属 | 节点路径 | 文件数 | 资产路径 |
|---|---:|---:|---:|
| 运行时 | 198 | 38 | 86 |
| 编辑器工具 | 35 | 4 | 3 |
| 测试 | 95 | 7 | 54 |

## 二、多段深路径（改层级即断链，62 处 / 54 个不同路径）

| 路径字面量 | 出现次数 | 段数 |
|---|---:|---:|
| `ART_ScreenBackground/BeachContainer` | 3 | 2 |
| `ART_EnemyAnimation/Image` | 2 | 2 |
| `ART_ScreenBackground/ART_RecruitButton` | 2 | 2 |
| `ART_ScreenBackground/campPanel` | 2 | 2 |
| `Bg/ContinueBtn` | 2 | 2 |
| `Bg/PauseBtn` | 2 | 2 |
| `RankText/StarAct` | 2 | 2 |
| `ART_BossTrack/ART_BossFill` | 1 | 2 |
| `ART_EnemyMarker/ART_EnemyHpTrack/ART_EnemyHpFill` | 1 | 3 |
| `ART_EnemyMarker/EnemyRuntimeLabel` | 1 | 2 |
| `ART_FixedBoardCellLayer/BoardBackgroundClickSurface` | 1 | 2 |
| `ART_PauseButton/PauseLabel` | 1 | 2 |
| `ART_ScreenBackground/GameOverlayController` | 1 | 2 |
| `ART_ScreenBackground/RecruitmentButtonController` | 1 | 2 |
| `AiUnitLayer/ART_AiRangePreview` | 1 | 2 |
| `BG/CloseBtn` | 1 | 2 |
| `BG/EnemyPart` | 1 | 2 |
| `BG/EnemyPart/EnemyItem/Text (TMP)/RateText` | 1 | 5 |
| `BG/EnemyPart/Image` | 1 | 3 |
| `BG/Image` | 1 | 2 |
| `BG/MyPart` | 1 | 2 |
| `BG/MyPart/Image` | 1 | 3 |
| `BG/MyPart/MyItem/Text (TMP)/RateText` | 1 | 5 |
| `BG/PlayerPart/PlayerItem/Text (TMP)/RateText` | 1 | 5 |
| `BG/ShareBtn` | 1 | 2 |
| `BG/VideoBtn` | 1 | 2 |
| `Bg/CancleBtn` | 1 | 2 |
| `Bg/CancleItemPanel` | 1 | 2 |
| `Bg/ChantBtn` | 1 | 2 |
| `Bg/ChatItemCon` | 1 | 2 |
| `Bg/CloseBtn` | 1 | 2 |
| `Bg/ConfirmBtn` | 1 | 2 |
| `Bg/ItemContainer` | 1 | 2 |
| `Bg/LotteryBtn` | 1 | 2 |
| `Bg/LotteryContainer` | 1 | 2 |
| `Bg/LotteryContainer/LotteryBtn` | 1 | 3 |
| `Bg/MyItemBg/ItemContainer` | 1 | 3 |
| `Bg/VideoReward` | 1 | 2 |
| `BtnImg/CollectionBtn` | 1 | 2 |
| `BtnImg/DeckBtn` | 1 | 2 |
| `CampBg/CollectionPart` | 1 | 2 |
| `CampBg/DeckPart` | 1 | 2 |
| `EnemyPart/EnemyItem/Text (TMP)/RateText` | 1 | 4 |
| `EnergyBg/AddBtn` | 1 | 2 |
| `EnergyBg/MaxAmount` | 1 | 2 |
| `EnergyBg/RAmount` | 1 | 2 |
| `ImageA/ImageB` | 1 | 2 |
| `LeaderImg/Text` | 1 | 2 |
| `LeaderLimit/LeaderContainer` | 1 | 2 |
| `MyHeroBg/HeroContainer` | 1 | 2 |
| `MyPart/MyItem/Text (TMP)/RateText` | 1 | 4 |
| `PlayerUnitLayer/ART_PlayerRangePreview` | 1 | 2 |
| `RouteWaypoints/DragonGoal` | 1 | 2 |
| `campPanel/CampBg/CollectionPart` | 1 | 3 |

## 三、单段名称查找（242 处）

> 只按名字在**直接子节点**里找，父子关系一变就找不到；同名节点还会取错。

| 文件 | 行 | 方式 | 目标名 |
|---|---:|---|---|
| `Assets/DragonBound/Editor/AuthoredGreyboxUiMigration.cs` | 207 | Transform.Find | `RangeDismissSurface` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 113 | Transform.Find | `ART_StarfallWarning` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 587 | Transform.Find | `ART_LockOverlay` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 600 | Transform.Find | `ART_Background` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 610 | Transform.Find | `RouteWaypoints` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 646 | Transform.Find | `ART_EnemyMarker` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 656 | Transform.Find | `SideLabel` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 663 | Transform.Find | `HatchlingLabel` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 664 | Transform.Find | `EnemyProgressLabel` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 705 | Transform.Find | `ART_LockOverlay` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 797 | Transform.Find | `ART_DragArrow` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 798 | Transform.Find | `ART_DragArrow` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 804 | Transform.Find | `ART_PauseButton` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 806 | Transform.Find | `ResourceLabel` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 807 | Transform.Find | `WaveLabel` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 808 | Transform.Find | `DebugLabel` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 809 | Transform.Find | `EnemyDebugLabel` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 862 | Transform.Find | `AiUnitLayer` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 866 | Transform.Find | `ART_DragArrow` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 870 | Transform.Find | `PlayerUnitLayer` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 874 | Transform.Find | `ART_DragArrow` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 976 | Transform.Find | `ART_DragArrow` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 1163 | Transform.Find | `ART_RangeOutline` |
| `Assets/DragonBound/Runtime/Presentation/BoardDebugOverlay.cs` | 69 | Transform.Find | `DEV_BoardDebugOverlay` |
| `Assets/DragonBound/Runtime/Presentation/CampPanelView.cs` | 262 | Transform.Find | `Img` |
| `Assets/DragonBound/Runtime/Presentation/DraggableUnitView.cs` | 604 | Transform.Find | `ART_SoulChainOverlay` |
| `Assets/DragonBound/Runtime/Presentation/DragonBoundScreenView.cs` | 276 | Transform.Find | `GameOverlayController` |
| `Assets/DragonBound/Runtime/Presentation/DragonBoundScreenView.cs` | 290 | Transform.Find | `campPanel` |
| `Assets/DragonBound/Runtime/Presentation/DragonBoundScreenView.cs` | 440 | Transform.Find | `HpBg` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 201 | Transform.Find | `ART_EnemyHpTrack` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 260 | Transform.Find | `ART_EnemyHpTrack` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 319 | Transform.Find | `ART_EnemyHpTrack` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 953 | Transform.Find | `ART_EnemyHpTrack` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 955 | Transform.Find | `ART_EnemyHpFill` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 985 | Transform.Find | `ART_EnemyHpTrack` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 986 | Transform.Find | `overHp` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 1001 | Transform.Find | `ART_EnemyHpTrack` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 1057 | Transform.Find | `ART_EnemyHpTrack` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 1065 | Transform.Find | `Image` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 1082 | Transform.Find | `Animator` |
| `Assets/DragonBound/Runtime/Presentation/FixedBoardCanvasView.cs` | 121 | Transform.Find | `ART_FixedBoardCanvasRuntime` |
| `Assets/DragonBound/Runtime/Presentation/FixedBoardCanvasView.cs` | 288 | Transform.Find | `ART_DeploymentFxLayer` |
| `Assets/DragonBound/Runtime/Presentation/FixedBoardCanvasView.cs` | 329 | Transform.Find | `ART_DeploymentGuideLayer` |
| `Assets/DragonBound/Runtime/Presentation/FixedBoardCanvasView.cs` | 834 | Transform.Find | `AiUnitLayer` |
| `Assets/DragonBound/Runtime/Presentation/FixedBoardCanvasView.cs` | 873 | Transform.Find | `ART_CenterDivider` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs` | 2133 | Transform.Find | `BeachItem` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs` | 2157 | Transform.Find | `Text (TMP)` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs` | 2158 | Transform.Find | `Text` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs` | 2159 | Transform.Find | `Image` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs` | 2270 | Transform.Find | `Select` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 302 | Transform.Find | `ART_ScreenBackground` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 311 | Transform.Find | `ItemContainer` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 323 | Transform.Find | `Active` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 327 | Transform.Find | `Active` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 328 | Transform.Find | `Active0` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 330 | Transform.Find | `Active1` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 345 | Transform.Find | `ActiveItemContainer` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 352 | Transform.Find | `ResourceLabel` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 358 | Transform.Find | `WaveLabel` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 364 | Transform.Find | `ART_PauseButton` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 368 | Transform.Find | `PauseLabel` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 374 | Transform.Find | `PausePanel` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 375 | Transform.Find | `PausePanel` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 387 | Transform.Find | `SettlementPanel` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 391 | Transform.Find | `SettleImg` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 395 | Transform.Find | `BossWarning` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 399 | Transform.Find | `ConfirmBtn` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 403 | Transform.Find | `Debug` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 406 | Transform.Find | `DebugLabel` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 412 | Transform.Find | `EnemyDebugLabel` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 1814 | Transform.Find | `CooldownMask` |
| `Assets/DragonBound/Runtime/Presentation/GridCellView.cs` | 52 | Transform.Find | `ART_LockOverlay` |
| `Assets/DragonBound/Runtime/Presentation/GridCellView.cs` | 58 | Transform.Find | `DebugRangeBandLabel` |
| `Assets/DragonBound/Runtime/Presentation/GridCellView.cs` | 97 | Transform.Find | `ART_LockOverlay` |
| `Assets/DragonBound/Runtime/Presentation/GridCellView.cs` | 102 | Transform.Find | `DebugRangeBandLabel` |
| `Assets/DragonBound/Runtime/Presentation/GridCellView.cs` | 128 | Transform.Find | `ART_CellSurface` |
| `Assets/DragonBound/Runtime/Presentation/GridCellView.cs` | 160 | Transform.Find | `ART_LockOverlay` |
| `Assets/DragonBound/Runtime/Presentation/GridCellView.cs` | 357 | Transform.Find | `InputReceiver` |
| `Assets/DragonBound/Runtime/Presentation/HeroFormationView.cs` | 527 | Transform.Find | `ParticalBg` |
| `Assets/DragonBound/Runtime/Presentation/PauseRuneRewardPresenter.cs` | 36 | Transform.Find | `Bg` |
| `Assets/DragonBound/Runtime/Presentation/PauseRuneRewardPresenter.cs` | 133 | Transform.Find | `ItemImg` |
| `Assets/DragonBound/Runtime/Presentation/TipTextController.cs` | 122 | Transform.Find | `Text` |
| `Assets/DragonBound/Runtime/Presentation/TipTextController.cs` | 123 | Transform.Find | `Text` |
| `Assets/DragonBound/Runtime/Presentation/UnitInformController.cs` | 113 | Transform.Find | `device` |
| `Assets/DragonBound/Tests/EditMode/AuthoredGreyboxUiTests.cs` | 26 | Transform.Find | `RangeDismissSurface` |
| `Assets/DragonBound/Tests/EditMode/AuthoredGreyboxUiTests.cs` | 27 | Transform.Find | `ItemEntryButton` |
| `Assets/DragonBound/Tests/EditMode/AuthoredGreyboxUiTests.cs` | 28 | Transform.Find | `ART_ItemLoadout` |
| `Assets/DragonBound/Tests/EditMode/AuthoredGreyboxUiTests.cs` | 29 | Transform.Find | `ART_HeroWorkshop` |
| `Assets/DragonBound/Tests/EditMode/AuthoredGreyboxUiTests.cs` | 30 | Transform.Find | `ART_RuneLoadout` |
| `Assets/DragonBound/Tests/EditMode/AuthoredGreyboxUiTests.cs` | 31 | Transform.Find | `Versus` |
| `Assets/DragonBound/Tests/EditMode/ItemGameplayIntegrationTests.cs` | 287 | Transform.Find | `CooldownMask` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 145 | Transform.Find | `ART_SoulChainOverlay` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 206 | Transform.Find | `ART_UnitPortrait` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 349 | Transform.Find | `weapon` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 736 | Transform.Find | `SideLabel` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 737 | Transform.Find | `SideLabel` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 755 | Transform.Find | `ART_PathLeft` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 756 | Transform.Find | `ART_PathRight` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 757 | Transform.Find | `ART_PathTop` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 758 | Transform.Find | `ART_PathBottom` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 759 | Transform.Find | `ART_Spawn` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 760 | Transform.Find | `ART_Hatchling` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 764 | Transform.Find | `ART_EnemyMarker` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 766 | Transform.Find | `ART_AttackLine` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 767 | Transform.Find | `ART_BowProjectile` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 768 | Transform.Find | `ART_SpearPierceLine` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 769 | Transform.Find | `ART_RiderSweepCircle` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 770 | Transform.Find | `ART_StarfallWarning` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 773 | Transform.Find | `DamageNumber` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 774 | Transform.Find | `SuppliesGain` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 793 | Transform.Find | `ART_PathLeft` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 794 | Transform.Find | `ART_PathRight` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 795 | Transform.Find | `ART_PathTop` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 796 | Transform.Find | `ART_PathBottom` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 851 | Transform.Find | `ART_RangeOutline` |
| `Assets/DragonBound/Tests/EditMode/RecruitItemColorTests.cs` | 93 | Transform.Find | `ART_SoulChainOverlay` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 70 | Transform.Find | `AnalyticsBootstrap` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 70 | GameObject.Find | `AnalyticsBootstrap` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 82 | Transform.Find | `AnalyticsBootstrap` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 82 | GameObject.Find | `AnalyticsBootstrap` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 92 | Transform.Find | `Canvas` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 92 | GameObject.Find | `Canvas` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 99 | Transform.Find | `Text` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 124 | Transform.Find | `Text` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 149 | Transform.Find | `TipText` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 182 | Transform.Find | `SafeAreaRoot` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 182 | GameObject.Find | `SafeAreaRoot` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 183 | Transform.Find | `RIVER` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 183 | GameObject.Find | `RIVER` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 263 | Transform.Find | `BoardSelect` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 414 | Transform.Find | `campPanel` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 496 | Transform.Find | `Img1` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 497 | Transform.Find | `Img2` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 502 | Transform.Find | `HeroContainer` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 768 | Transform.Find | `ART_RangeOutline` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 842 | Transform.Find | `BeachItem` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 866 | Transform.Find | `Name` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 869 | Transform.Find | `MaxLv` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 870 | Transform.Find | `EXP` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 871 | Transform.Find | `device` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 872 | Transform.Find | `Rune` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 899 | Transform.Find | `InputReceiver` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 969 | Transform.Find | `RangeDismissSurface` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 1004 | Transform.Find | `BeachSourceSelect` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 1020 | Transform.Find | `BoardSelect` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 1034 | Transform.Find | `BoardSelect` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 109 | Transform.Find | `Versus` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 131 | Transform.Find | `ART_ScreenBackground` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 132 | Transform.Find | `ART_PauseButton` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 133 | Transform.Find | `PausePanel` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 152 | Transform.Find | `SettlementPanel` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 154 | Transform.Find | `SettleImg` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 159 | Transform.Find | `GoldText` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 160 | Transform.Find | `ReciveBtn` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 161 | Transform.Find | `DoubleBtn` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 176 | Transform.Find | `SettlementPanel` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 178 | Transform.Find | `SettleImg` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 183 | Transform.Find | `GoldText` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 184 | Transform.Find | `ReciveBtn` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 185 | Transform.Find | `DoubleBtn` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1740 | Transform.Find | `ART_StormShieldVFX` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1784 | Transform.Find | `ART_EnemyDeathVFX` |
| `Assets/Editor/GreyboxMerchantLoadoutSceneMigration.cs` | 89 | Transform.Find | `Active` |
| `Assets/Editor/GreyboxMerchantLoadoutSceneMigration.cs` | 90 | Transform.Find | `Passtive` |
| `Assets/Editor/GreyboxMerchantLoadoutSceneMigration.cs` | 147 | Transform.Find | `Active` |
| `Assets/Editor/GreyboxMerchantLoadoutSceneMigration.cs` | 147 | Transform.Find | `Passtive` |
| `Assets/Editor/GreyboxMerchantLoadoutSceneMigration.cs` | 170 | Transform.Find | `CooldownMask` |
| `Assets/Editor/SignInFeatureValidation.cs` | 57 | Transform.Find | `SignPanel` |
| `Assets/Editor/SignInFeatureValidation.cs` | 58 | Transform.Find | `Image` |
| `Assets/Editor/SignInFeatureValidation.cs` | 59 | Transform.Find | `ContentCon` |
| `Assets/Scripts/Auth/Energy/UI/MainEnergyController.cs` | 569 | Transform.Find | `StartBtn` |
| `Assets/Scripts/Auth/Energy/UI/MainEnergyController.cs` | 571 | Transform.Find | `AddEnergyPanel` |
| `Assets/Scripts/Auth/UI/LoginController.cs` | 89 | Transform.Find | `SignUpPanel` |
| `Assets/Scripts/Auth/UI/LoginController.cs` | 410 | Transform.Find | `TipText` |
| `Assets/Scripts/Gold/UI/GoldBalanceController.cs` | 22 | Transform.Find | `CoinQua` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 89 | Transform.Find | `MerchantPanel` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 105 | Transform.Find | `ActiveColumn` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 106 | Transform.Find | `PassiveColumn` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 120 | Transform.Find | `CancleBtn` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 122 | Transform.Find | `ConfirmBtn` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 290 | Transform.Find | `Image` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 680 | Transform.Find | `BuyBtn` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 682 | Transform.Find | `Text (TMP)` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 692 | Transform.Find | `CItemImg` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 895 | Transform.Find | `ItemImg` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 903 | Transform.Find | `DelBtn` |
| `Assets/Scripts/Rank/UI/GameRankResultController.cs` | 43 | Transform.Find | `LoadingPanel` |
| `Assets/Scripts/Rank/UI/GameRankResultController.cs` | 412 | Transform.Find | `SettlementPanel` |
| `Assets/Scripts/Rank/UI/GameRankResultController.cs` | 419 | Transform.Find | `Text` |
| `Assets/Scripts/Rank/UI/GameRankResultController.cs` | 420 | Transform.Find | `GoldText` |
| `Assets/Scripts/Rank/UI/GameRankResultController.cs` | 421 | Transform.Find | `ReciveBtn` |
| `Assets/Scripts/Rank/UI/GameRankResultController.cs` | 422 | Transform.Find | `DoubleBtn` |
| `Assets/Scripts/Rank/UI/GreyboxSettlementRewardController.cs` | 36 | Transform.Find | `SettleImg` |
| `Assets/Scripts/Rank/UI/GreyboxSettlementRewardController.cs` | 37 | Transform.Find | `GoldText` |
| `Assets/Scripts/Rank/UI/GreyboxSettlementRewardController.cs` | 38 | Transform.Find | `ReciveBtn` |
| `Assets/Scripts/Rank/UI/GreyboxSettlementRewardController.cs` | 39 | Transform.Find | `DoubleBtn` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 57 | Transform.Find | `Bg` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 58 | Transform.Find | `WeekBtn` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 59 | Transform.Find | `MonthBtn` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 61 | Transform.Find | `MyLeaderItemBg` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 62 | Transform.Find | `AvatarImg` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 64 | Transform.Find | `RankText` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 209 | Transform.Find | `AvatarImg` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 221 | Transform.Find | `RankText` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 233 | Transform.Find | `LeaderImg` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 235 | Transform.Find | `Text` |
| `Assets/Scripts/Rank/UI/MainRankController.cs` | 157 | Transform.Find | `RankText` |
| `Assets/Scripts/Rank/UI/MainRankController.cs` | 159 | Transform.Find | `threeStar` |
| `Assets/Scripts/Rank/UI/MainRankController.cs` | 160 | Transform.Find | `fourStar` |
| `Assets/Scripts/Rank/UI/MainRankController.cs` | 161 | Transform.Find | `fiveStar` |
| `Assets/Scripts/Rank/UI/MainRankController.cs` | 185 | Transform.Find | `Img` |
| `Assets/Scripts/Rune/RuneDragItem.cs` | 29 | Transform.Find | `AcText` |
| `Assets/Scripts/Rune/RuneDragItem.cs` | 31 | Transform.Find | `Text (TMP)` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 57 | Transform.Find | `WeaponContainer` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 63 | Transform.Find | `PageLeft` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 64 | Transform.Find | `PageRight` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 65 | Transform.Find | `page` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 228 | Transform.Find | `Name` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 233 | Transform.Find | `count` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 254 | Transform.Find | `BG` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 358 | Transform.Find | `AcText` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 369 | Transform.Find | `Text (TMP)` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 653 | Transform.Find | `weapon` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 656 | Transform.Find | `Name` |
| `Assets/Scripts/SceneLoader.cs` | 201 | Transform.Find | `FillImg` |
| `Assets/Scripts/Settings/DamageNumberToggleController.cs` | 15 | Transform.Find | `State` |
| `Assets/Scripts/Settings/DragFillController.cs` | 55 | Transform.Find | `FillImg` |
| `Assets/Scripts/Settings/VisualStateToggleController.cs` | 15 | Transform.Find | `State` |
| `Assets/Scripts/SignIn/UI/MainSignInController.cs` | 116 | Transform.Find | `SignPanel` |
| `Assets/Scripts/SignIn/UI/MainSignInController.cs` | 117 | Transform.Find | `Image` |
| `Assets/Scripts/SignIn/UI/MainSignInController.cs` | 118 | Transform.Find | `ContentCon` |
| `Assets/Scripts/SignIn/UI/MainSignInController.cs` | 120 | Transform.Find | `SignBtn` |
| `Assets/Scripts/SignIn/UI/MainSignInController.cs` | 121 | Transform.Find | `closeBtn` |
| `Assets/Scripts/SignIn/UI/MainSignInController.cs` | 137 | Transform.Find | `Image (6)` |
| `Assets/Scripts/UI/ForgekeepersGiftPanelController.cs` | 806 | Transform.Find | `ForgePanel` |
| `Assets/Scripts/UI/LoadingPanelEntranceAnimator.cs` | 184 | Transform.Find | `Image` |
| `Assets/Scripts/UI/MainNavTabController.cs` | 61 | Transform.Find | `BagBtn` |
| `Assets/Scripts/UI/MainNavTabController.cs` | 62 | Transform.Find | `RankingBtn` |
| `Assets/Scripts/UI/MainNavTabController.cs` | 63 | Transform.Find | `MainBtn` |
| `Assets/Scripts/UI/PlayerAvatarView.cs` | 123 | Transform.Find | `ProfileFire` |
| `Assets/Scripts/UI/UiGoldExampleEffect.cs` | 32 | Transform.Find | `GoldParticleExample` |
| `Assets/Scripts/UI/UiGoldExampleEffect.cs` | 343 | Transform.Find | `ImageA` |

## 四、按序号访问 `GetChild(index)`（24 处）

> UI 子节点顺序一变就静默指向错误控件，且不报错。

| 文件 | 行 | 序号 | 代码 |
|---|---:|---:|---|
| `Assets/DragonBound/Runtime/Presentation/CampPanelView.cs` | 501 | i | `var child = parent.GetChild(i);` |
| `Assets/DragonBound/Runtime/Presentation/CampPanelView.cs` | 522 | i | `var child = collectionPart.GetChild(i);` |
| `Assets/DragonBound/Runtime/Presentation/CampPanelView.cs` | 548 | i | `result.Add(parent.GetChild(i));` |
| `Assets/DragonBound/Runtime/Presentation/DragonBoundScreenView.cs` | 498 | index | `var child = heartRoot.GetChild(index);` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs` | 2121 | childIndex | `var slot = beachContainer.GetChild(childIndex) as RectTransform;` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 477 | index | `var slot = componentContainer.GetChild(index);` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 509 | index | `var slot = heroContainer.GetChild(index);` |
| `Assets/Scripts/Analytics/AnalyticsConsentUiController.cs` | 252 | index | `var found = FindRecursive(parent.GetChild(index), objectName);` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 109 | 0 | `activeItemColumn = ownedItemContainer.GetChild(0);` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 113 | 1 | `passiveItemColumn = ownedItemContainer.GetChild(1);` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 226 | index | `Transform child = containerTransform.GetChild(index);` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 659 | index | `GameObject existing = itemContainer.GetChild(index).gameObject;` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 948 | index | `GameObject item = column.GetChild(index).gameObject;` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 963 | index | `GameObject existing = column.GetChild(index).gameObject;` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 197 | index | `GameObject previousItem = container.GetChild(index).gameObject;` |
| `Assets/Scripts/Rank/UI/MainRankController.cs` | 184 | index | `Transform star = group.GetChild(index);` |
| `Assets/Scripts/Rune/MainRuneUnlockController.cs` | 201 | index | `Transform found = FindDescendant(root.GetChild(index), objectName);` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 652 | index | `Transform hero = heroContainer.GetChild(index);` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 730 | index | `GameObject child = weaponContainer.GetChild(index).gameObject;` |
| `Assets/Scripts/Settings/DragFillController.cs` | 78 | 0 | `handle = fillTransform.GetChild(0) as RectTransform;` |
| `Assets/Scripts/UI/MainNavTabController.cs` | 201 | index | `Transform found = FindDescendant(root.GetChild(index), objectName);` |
| `Assets/Scripts/UI/PlayerAvatarView.cs` | 94 | index | `PlayerAvatarView view = slot.GetChild(index).GetComponent<PlayerAvatarView>();` |
| `Assets/Scripts/UI/PlayerAvatarView.cs` | 185 | index | `Transform match = FindDescendant(root.GetChild(index), objectName);` |
| `Assets/Scripts/UI/VoidShovelRewardController.cs` | 536 | index | `Transform result = FindDescendant(root.GetChild(index), objectName);` |

## 五、节点路径访问全量明细

### 5.1 运行时（生产代码，需重点维护） —— 共 198 处

**`Assets/Scripts/Merchant/MainMerchantController.cs`** —— 27 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 89 | Transform.Find | `MerchantPanel` | `Transform panelTransform = transform.Find("MerchantPanel");` |
| 90 | Transform.Find | `Bg/ChatItemCon` | `itemContainer = panelTransform?.Find("Bg/ChatItemCon");` |
| 91 | Transform.Find | `Bg/LotteryContainer` | `lotteryContainer = panelTransform?.Find("Bg/LotteryContainer")?.gameObject;` |
| 92 | Transform.Find | `Bg/ChantBtn` | `chantButton = panelTransform?.Find("Bg/ChantBtn")?.GetComponent<Button>();` |
| 93 | Transform.Find | `Bg/LotteryBtn` | `lotteryButton = panelTransform?.Find("Bg/LotteryBtn")?.GetComponent<Button>();` |
| 98 | Transform.Find | `Bg/LotteryContainer/LotteryBtn` | `lotteryDrawButton = panelTransform?.Find("Bg/LotteryContainer/LotteryBtn")` |
| 101 | Transform.Find | `Bg/ItemContainer` | `ownedItemContainer = panelTransform?.Find("Bg/ItemContainer") ??` |
| 102 | Transform.Find | `Bg/MyItemBg/ItemContainer` | `panelTransform?.Find("Bg/MyItemBg/ItemContainer");` |
| 105 | Transform.Find | `ActiveColumn` | `activeItemColumn = ownedItemContainer.Find("ActiveColumn");` |
| 106 | Transform.Find | `PassiveColumn` | `passiveItemColumn = ownedItemContainer.Find("PassiveColumn");` |
| 109 | GetChild | `0` | `activeItemColumn = ownedItemContainer.GetChild(0);` |
| 113 | GetChild | `1` | `passiveItemColumn = ownedItemContainer.GetChild(1);` |
| 118 | Transform.Find | `Bg/CancleItemPanel` | `Transform cancelPanelTransform = panelTransform?.Find("Bg/CancleItemPanel");` |
| 120 | Transform.Find | `CancleBtn` | `cancelItemButton = (cancelPanelTransform?.Find("CancleBtn") ??` |
| 121 | Transform.Find | `Bg/CancleBtn` | `cancelPanelTransform?.Find("Bg/CancleBtn"))?.GetComponent<Button>();` |
| 122 | Transform.Find | `ConfirmBtn` | `confirmItemButton = (cancelPanelTransform?.Find("ConfirmBtn") ??` |
| 123 | Transform.Find | `Bg/ConfirmBtn` | `cancelPanelTransform?.Find("Bg/ConfirmBtn"))?.GetComponent<Button>();` |
| 226 | GetChild | `index` | `Transform child = containerTransform.GetChild(index);` |
| 290 | Transform.Find | `Image` | `return button.transform.Find("Image")?.GetComponent<Image>();` |
| 659 | GetChild | `index` | `GameObject existing = itemContainer.GetChild(index).gameObject;` |
| 680 | Transform.Find | `BuyBtn` | `Transform buyTransform = itemObject.transform.Find("BuyBtn");` |
| 682 | Transform.Find | `Text (TMP)` | `TMP_Text priceText = buyTransform?.Find("Text (TMP)")?.GetComponent<TMP_Text>();` |
| 692 | Transform.Find | `CItemImg` | `Image itemImage = itemObject.transform.Find("CItemImg")?.GetComponent<Image>();` |
| 895 | Transform.Find | `ItemImg` | `Image targetImage = itemObject.transform.Find("ItemImg")?.GetComponent<Image>();` |
| 903 | Transform.Find | `DelBtn` | `Button deleteButton = itemObject.transform.Find("DelBtn")?.GetComponent<Button>();` |
| 948 | GetChild | `index` | `GameObject item = column.GetChild(index).gameObject;` |
| 963 | GetChild | `index` | `GameObject existing = column.GetChild(index).gameObject;` |

**`Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs`** —— 23 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 302 | Transform.Find | `ART_ScreenBackground` | `? screen.transform.Find("ART_ScreenBackground")` |
| 311 | Transform.Find | `ItemContainer` | `var authoredItemContainer = screen.transform.Find("ItemContainer");` |
| 323 | Transform.Find | `Active` | `var authoredActiveContainer = authoredItemContainer?.Find("Active");` |
| 327 | Transform.Find | `Active` | `authoredActiveContainer.Find("Active")?.GetComponent<Button>() ??` |
| 328 | Transform.Find | `Active0` | `authoredActiveContainer.Find("Active0")?.GetComponent<Button>();` |
| 330 | Transform.Find | `Active1` | `authoredActiveContainer.Find("Active1")?.GetComponent<Button>();` |
| 345 | Transform.Find | `ActiveItemContainer` | `activeItemContainer = background.Find("ActiveItemContainer") as RectTransform;` |
| 352 | Transform.Find | `ResourceLabel` | `var authoredResourceLabel = background.Find("ResourceLabel")?.GetComponent<Text>();` |
| 358 | Transform.Find | `WaveLabel` | `var authoredWaveLabel = background.Find("WaveLabel")?.GetComponent<Text>();` |
| 364 | Transform.Find | `ART_PauseButton` | `var authoredButton = background.Find("ART_PauseButton")?.GetComponent<Button>();` |
| 368 | Transform.Find | `PauseLabel` | `pauseLabel = authoredButton.transform.Find("PauseLabel")?.GetComponent<Text>();` |
| 374 | Transform.Find | `PausePanel` | `var authoredPanel = screen.transform.Find("PausePanel") ??` |
| 375 | Transform.Find | `PausePanel` | `background.Find("PausePanel");` |
| 382 | Transform.Find | `Bg/PauseBtn` | `finishMatchButton = authoredPanel.Find("Bg/PauseBtn")?.GetComponent<Button>();` |
| 383 | Transform.Find | `Bg/ContinueBtn` | `continueButton = authoredPanel.Find("Bg/ContinueBtn")?.GetComponent<Button>();` |
| 387 | Transform.Find | `SettlementPanel` | `var authoredSettlement = screen.transform.Find("SettlementPanel");` |
| 391 | Transform.Find | `SettleImg` | `settlementResultImage = authoredSettlement.Find("SettleImg")?.GetComponent<Image>();` |
| 395 | Transform.Find | `BossWarning` | `var authoredBossWarning = screen.transform.Find("BossWarning");` |
| 399 | Transform.Find | `ConfirmBtn` | `bossWarningConfirmButton = authoredBossWarning.Find("ConfirmBtn")?.GetComponent<Button>();` |
| 403 | Transform.Find | `Debug` | `var debugRoot = background.Find("Debug");` |
| 406 | Transform.Find | `DebugLabel` | `var authoredDebugLabel = debugRoot.Find("DebugLabel")?.GetComponent<Text>();` |
| 412 | Transform.Find | `EnemyDebugLabel` | `var authoredEnemyDebugLabel = debugRoot.Find("EnemyDebugLabel")?.GetComponent<Text>();` |
| 1814 | Transform.Find | `CooldownMask` | `var existing = slot.Find("CooldownMask")?.GetComponent<Image>();` |

**`Assets/Scripts/Rank/UI/MainLeaderboardController.cs`** —— 15 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 57 | Transform.Find | `Bg` | `Transform background = transform.Find("Bg");` |
| 58 | Transform.Find | `WeekBtn` | `weekButton = background?.Find("WeekBtn")?.GetComponent<Button>();` |
| 59 | Transform.Find | `MonthBtn` | `monthButton = background?.Find("MonthBtn")?.GetComponent<Button>();` |
| 60 | Transform.Find | `LeaderLimit/LeaderContainer` | `container = background?.Find("LeaderLimit/LeaderContainer");` |
| 61 | Transform.Find | `MyLeaderItemBg` | `myLeaderItem = background?.Find("MyLeaderItemBg");` |
| 62 | Transform.Find | `AvatarImg` | `myAvatarImage = myLeaderItem?.Find("AvatarImg")?.GetComponent<Image>();` |
| 63 | Transform.Find | `LeaderImg/Text` | `myLeaderboardPositionText = myLeaderItem?.Find("LeaderImg/Text")?.GetComponent<TMP_Text>();` |
| 64 | Transform.Find | `RankText` | `myRankText = myLeaderItem?.Find("RankText")?.GetComponent<TMP_Text>();` |
| 65 | Transform.Find | `RankText/StarAct` | `myTotalStarsText = myLeaderItem?.Find("RankText/StarAct")?.GetComponent<TMP_Text>();` |
| 197 | GetChild | `index` | `GameObject previousItem = container.GetChild(index).gameObject;` |
| 209 | Transform.Find | `AvatarImg` | `Image avatar = item.transform.Find("AvatarImg")?.GetComponent<Image>();` |
| 221 | Transform.Find | `RankText` | `SetText(item.transform.Find("RankText"), rankName);` |
| 222 | Transform.Find | `RankText/StarAct` | `SetText(item.transform.Find("RankText/StarAct"), player.TotalRankStars.ToString());` |
| 233 | Transform.Find | `LeaderImg` | `Transform leaderImageTransform = item.Find("LeaderImg");` |
| 235 | Transform.Find | `Text` | `Transform positionTextTransform = leaderImageTransform?.Find("Text");` |

**`Assets/Scripts/Rune/RuneWeaponPanelController.cs`** —— 14 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 57 | Transform.Find | `WeaponContainer` | `weaponContainer = transform.Find("WeaponContainer");` |
| 62 | Transform.Find | `MyHeroBg/HeroContainer` | `heroContainer = transform.Find("MyHeroBg/HeroContainer");` |
| 63 | Transform.Find | `PageLeft` | `pageLeftButton = GetButton(transform.Find("PageLeft"));` |
| 64 | Transform.Find | `PageRight` | `pageRightButton = GetButton(transform.Find("PageRight"));` |
| 65 | Transform.Find | `page` | `pageText = GetText(transform.Find("page"));` |
| 228 | Transform.Find | `Name` | `SetRuneName(instance.transform.Find("Name"), entry.Definition);` |
| 233 | Transform.Find | `count` | `instance.transform.Find("count"),` |
| 254 | Transform.Find | `BG` | `Transform background = instance.transform.Find("BG");` |
| 358 | Transform.Find | `AcText` | `Transform amountRoot = item.Find("AcText");` |
| 369 | Transform.Find | `Text (TMP)` | `SetText(amountRoot.Find("Text (TMP)"), availableCompleteRunes.ToString());` |
| 652 | GetChild | `index` | `Transform hero = heroContainer.GetChild(index);` |
| 653 | Transform.Find | `weapon` | `Transform weapon = hero.Find("weapon");` |
| 656 | Transform.Find | `Name` | `TMP_Text heroName = GetText(hero.Find("Name"));` |
| 730 | GetChild | `index` | `GameObject child = weaponContainer.GetChild(index).gameObject;` |

**`Assets/DragonBound/Runtime/Presentation/EnemyView.cs`** —— 11 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 201 | Transform.Find | `ART_EnemyHpTrack` | `var healthTrack = transform.Find("ART_EnemyHpTrack");` |
| 260 | Transform.Find | `ART_EnemyHpTrack` | `var healthTrack = transform.Find("ART_EnemyHpTrack");` |
| 319 | Transform.Find | `ART_EnemyHpTrack` | `var healthTrack = transform.Find("ART_EnemyHpTrack");` |
| 953 | Transform.Find | `ART_EnemyHpTrack` | `var healthTrack = transform.Find("ART_EnemyHpTrack");` |
| 955 | Transform.Find | `ART_EnemyHpFill` | `? healthTrack.Find("ART_EnemyHpFill")` |
| 985 | Transform.Find | `ART_EnemyHpTrack` | `var track = transform.Find("ART_EnemyHpTrack");` |
| 986 | Transform.Find | `overHp` | `var overHpTransform = track != null ? track.Find("overHp") : null;` |
| 1001 | Transform.Find | `ART_EnemyHpTrack` | `var track = transform.Find("ART_EnemyHpTrack");` |
| 1057 | Transform.Find | `ART_EnemyHpTrack` | `var healthTrack = transform.Find("ART_EnemyHpTrack");` |
| 1065 | Transform.Find | `Image` | `var imageTransform = animationRoot.Find("Image");` |
| 1082 | Transform.Find | `Animator` | `var authoredAnimatorTransform = animationRoot.Find("Animator");` |

**`Assets/DragonBound/Runtime/Presentation/DragonBoundScreenView.cs`** —— 9 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 275 | Transform.Find | `ART_ScreenBackground/GameOverlayController` | `var host = transform.Find("ART_ScreenBackground/GameOverlayController") ??` |
| 276 | Transform.Find | `GameOverlayController` | `transform.Find("GameOverlayController");` |
| 290 | Transform.Find | `campPanel` | `var campPanel = transform.Find("campPanel") ??` |
| 291 | Transform.Find | `ART_ScreenBackground/campPanel` | `transform.Find("ART_ScreenBackground/campPanel");` |
| 311 | Transform.Find | `ART_ScreenBackground/RecruitmentButtonController` | `var host = transform.Find("ART_ScreenBackground/RecruitmentButtonController") ??` |
| 312 | Transform.Find | `ART_ScreenBackground/ART_RecruitButton` | `transform.Find("ART_ScreenBackground/ART_RecruitButton");` |
| 328 | Transform.Find | `ART_ScreenBackground/ART_RecruitButton` | `var target = transform.Find("ART_ScreenBackground/ART_RecruitButton");` |
| 440 | Transform.Find | `HpBg` | `var heartRoot = goal?.Find("HpBg") as RectTransform;` |
| 498 | GetChild | `index` | `var child = heartRoot.GetChild(index);` |

**`Assets/Scripts/Auth/Energy/UI/MainEnergyController.cs`** —— 8 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 569 | Transform.Find | `StartBtn` | `startButton = transform.Find("StartBtn")?.GetComponent<Button>();` |
| 570 | Transform.Find | `EnergyBg/AddBtn` | `addEnergyButton = transform.Find("EnergyBg/AddBtn")?.GetComponent<Button>();` |
| 571 | Transform.Find | `AddEnergyPanel` | `addEnergyPanel = transform.Find("AddEnergyPanel")?.gameObject;` |
| 573 | Transform.Find | `BG/CloseBtn` | `closeEnergyPanelButton = addEnergyRoot?.Find("BG/CloseBtn")?.GetComponent<Button>();` |
| 574 | Transform.Find | `BG/VideoBtn` | `videoButton = addEnergyRoot?.Find("BG/VideoBtn")?.GetComponent<Button>();` |
| 575 | Transform.Find | `BG/ShareBtn` | `shareButton = addEnergyRoot?.Find("BG/ShareBtn")?.GetComponent<Button>();` |
| 576 | Transform.Find | `EnergyBg/RAmount` | `currentAmountText = transform.Find("EnergyBg/RAmount")?.GetComponent<TMP_Text>();` |
| 577 | Transform.Find | `EnergyBg/MaxAmount` | `maximumAmountText = transform.Find("EnergyBg/MaxAmount")?.GetComponent<TMP_Text>();` |

**`Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs`** —— 7 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 2109 | Transform.Find | `ART_ScreenBackground/BeachContainer` | `? screen.transform.Find("ART_ScreenBackground/BeachContainer")` |
| 2121 | GetChild | `childIndex` | `var slot = beachContainer.GetChild(childIndex) as RectTransform;` |
| 2133 | Transform.Find | `BeachItem` | `var beachItem = slot.Find("BeachItem");` |
| 2157 | Transform.Find | `Text (TMP)` | `var nameTransform = beachItem.Find("Text (TMP)");` |
| 2158 | Transform.Find | `Text` | `var levelTransform = beachItem.Find("Text");` |
| 2159 | Transform.Find | `Image` | `var artTransform = beachItem.Find("Image");` |
| 2270 | Transform.Find | `Select` | `var selection = cellView.transform.Find("Select")?.gameObject;` |

**`Assets/DragonBound/Runtime/Presentation/GridCellView.cs`** —— 7 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 52 | Transform.Find | `ART_LockOverlay` | `var overlay = transform.Find("ART_LockOverlay");` |
| 58 | Transform.Find | `DebugRangeBandLabel` | `var label = transform.Find("DebugRangeBandLabel");` |
| 97 | Transform.Find | `ART_LockOverlay` | `var overlay = transform.Find("ART_LockOverlay");` |
| 102 | Transform.Find | `DebugRangeBandLabel` | `var label = transform.Find("DebugRangeBandLabel");` |
| 128 | Transform.Find | `ART_CellSurface` | `var surface = artImage != null ? artImage.transform : transform.Find("ART_CellSurface");` |
| 160 | Transform.Find | `ART_LockOverlay` | `var overlay = transform.Find("ART_LockOverlay");` |
| 357 | Transform.Find | `InputReceiver` | `var receiver = transform.Find("InputReceiver");` |

**`Assets/DragonBound/Runtime/Presentation/FixedBoardCanvasView.cs`** —— 6 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 121 | Transform.Find | `ART_FixedBoardCanvasRuntime` | `var existing = targetScreenRoot.Find("ART_FixedBoardCanvasRuntime");` |
| 288 | Transform.Find | `ART_DeploymentFxLayer` | `deploymentFxLayer = parent.Find("ART_DeploymentFxLayer") as RectTransform;` |
| 329 | Transform.Find | `ART_DeploymentGuideLayer` | `deploymentGuideLayer = parent.Find("ART_DeploymentGuideLayer") as RectTransform;` |
| 562 | Transform.Find | `ART_ScreenBackground/campPanel` | `var campPanel = screenRoot.Find("ART_ScreenBackground/campPanel");` |
| 834 | Transform.Find | `AiUnitLayer` | `var aiUnits = screenRoot.Find("AiUnitLayer");` |
| 873 | Transform.Find | `ART_CenterDivider` | `: terrainLayer.Find("ART_CenterDivider") as RectTransform;` |

**`Assets/Scripts/Rank/UI/GameRankResultController.cs`** —— 6 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 43 | Transform.Find | `LoadingPanel` | `loadingPanel = transform.Find("LoadingPanel")?.gameObject;` |
| 412 | Transform.Find | `SettlementPanel` | `Transform panelTransform = transform.Find("SettlementPanel");` |
| 419 | Transform.Find | `Text` | `Transform resultTransform = panelTransform.Find("Text");` |
| 420 | Transform.Find | `GoldText` | `Transform goldTransform = panelTransform.Find("GoldText");` |
| 421 | Transform.Find | `ReciveBtn` | `Transform receiveTransform = panelTransform.Find("ReciveBtn");` |
| 422 | Transform.Find | `DoubleBtn` | `Transform doubleTransform = panelTransform.Find("DoubleBtn");` |

**`Assets/Scripts/Rank/UI/MainRankController.cs`** —— 6 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 157 | Transform.Find | `RankText` | `Transform rankTextTransform = transform.Find("RankText");` |
| 159 | Transform.Find | `threeStar` | `threeStar = transform.Find("threeStar");` |
| 160 | Transform.Find | `fourStar` | `fourStar = transform.Find("fourStar");` |
| 161 | Transform.Find | `fiveStar` | `fiveStar = transform.Find("fiveStar");` |
| 184 | GetChild | `index` | `Transform star = group.GetChild(index);` |
| 185 | Transform.Find | `Img` | `Transform image = star.Find("Img");` |

**`Assets/Scripts/SignIn/UI/MainSignInController.cs`** —— 6 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 116 | Transform.Find | `SignPanel` | `Transform panel = transform.Find("SignPanel");` |
| 117 | Transform.Find | `Image` | `Transform imageRoot = panel?.Find("Image");` |
| 118 | Transform.Find | `ContentCon` | `Transform content = imageRoot?.Find("ContentCon");` |
| 120 | Transform.Find | `SignBtn` | `openButton = transform.Find("SignBtn")?.GetComponent<Button>();` |
| 121 | Transform.Find | `closeBtn` | `closeButton = imageRoot?.Find("closeBtn")?.GetComponent<Button>();` |
| 137 | Transform.Find | `Image (6)` | `dayRoot = imageRoot?.Find("Image (6)");` |

**`Assets/Scripts/UI/PlayerAvatarView.cs`** —— 6 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 94 | GetChild | `index` | `PlayerAvatarView view = slot.GetChild(index).GetComponent<PlayerAvatarView>();` |
| 123 | Transform.Find | `ProfileFire` | `RectTransform frameRect = profileRect.Find("ProfileFire") as RectTransform;` |
| 164 | Transform.Find | `BG/Image` | `Transform target = loadingPanel.Find("BG/Image") ?? loadingPanel.Find("BG/MyPart/Image");` |
| 164 | Transform.Find | `BG/MyPart/Image` | `Transform target = loadingPanel.Find("BG/Image") ?? loadingPanel.Find("BG/MyPart/Image");` |
| 170 | Transform.Find | `BG/EnemyPart/Image` | `Transform enemyTarget = loadingPanel.Find("BG/EnemyPart/Image");` |
| 185 | GetChild | `index` | `Transform match = FindDescendant(root.GetChild(index), objectName);` |

**`Assets/Scripts/UI/GameplayLoadingPanelController.cs`** —— 5 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 219 | Transform.Find | `BG/MyPart/MyItem/Text (TMP)/RateText` | `playerRateText = (root.Find("BG/MyPart/MyItem/Text (TMP)/RateText") ??` |
| 220 | Transform.Find | `MyPart/MyItem/Text (TMP)/RateText` | `root.Find("MyPart/MyItem/Text (TMP)/RateText") ??` |
| 221 | Transform.Find | `BG/PlayerPart/PlayerItem/Text (TMP)/RateText` | `root.Find("BG/PlayerPart/PlayerItem/Text (TMP)/RateText"))?` |
| 223 | Transform.Find | `BG/EnemyPart/EnemyItem/Text (TMP)/RateText` | `aiRateText = (root.Find("BG/EnemyPart/EnemyItem/Text (TMP)/RateText") ??` |
| 224 | Transform.Find | `EnemyPart/EnemyItem/Text (TMP)/RateText` | `root.Find("EnemyPart/EnemyItem/Text (TMP)/RateText"))?` |

**`Assets/DragonBound/Runtime/Presentation/CampPanelView.cs`** —— 4 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 262 | Transform.Find | `Img` | `image = slot.Find("Img")?.GetComponent<Image>();` |
| 501 | GetChild | `i` | `var child = parent.GetChild(i);` |
| 522 | GetChild | `i` | `var child = collectionPart.GetChild(i);` |
| 548 | GetChild | `i` | `result.Add(parent.GetChild(i));` |

**`Assets/Scripts/Rank/UI/GreyboxSettlementRewardController.cs`** —— 4 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 36 | Transform.Find | `SettleImg` | `resultImage = transform.Find("SettleImg")?.GetComponent<Image>();` |
| 37 | Transform.Find | `GoldText` | `goldText = transform.Find("GoldText")?.GetComponent<TMPro.TMP_Text>();` |
| 38 | Transform.Find | `ReciveBtn` | `receiveButton = transform.Find("ReciveBtn")?.GetComponent<Button>();` |
| 39 | Transform.Find | `DoubleBtn` | `doubleButton = transform.Find("DoubleBtn")?.GetComponent<Button>();` |

**`Assets/Scripts/UI/MainNavTabController.cs`** —— 4 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 61 | Transform.Find | `BagBtn` | `bagImage = navigation?.Find("BagBtn")?.GetComponent<Image>();` |
| 62 | Transform.Find | `RankingBtn` | `rankingImage = navigation?.Find("RankingBtn")?.GetComponent<Image>();` |
| 63 | Transform.Find | `MainBtn` | `mainImage = navigation?.Find("MainBtn")?.GetComponent<Image>();` |
| 201 | GetChild | `index` | `Transform found = FindDescendant(root.GetChild(index), objectName);` |

**`Assets/Scripts/UI/ForgekeepersGiftPanelController.cs`** —— 3 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 806 | Transform.Find | `ForgePanel` | `Transform panel = transform.Find("ForgePanel");` |
| 811 | Transform.Find | `Bg/VideoReward` | `videoRewardButton = root?.Find("Bg/VideoReward")?.GetComponent<Button>();` |
| 813 | Transform.Find | `Bg/CloseBtn` | `closeButton = root?.Find("Bg/CloseBtn")?.GetComponent<Button>();` |

**`Assets/Scripts/UI/LoadingPanelEntranceAnimator.cs`** —— 3 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 165 | Transform.Find | `BG/EnemyPart` | `Transform enemyPart = transform.Find("BG/EnemyPart");` |
| 166 | Transform.Find | `BG/MyPart` | `Transform playerPart = transform.Find("BG/MyPart");` |
| 184 | Transform.Find | `Image` | `Transform image = part.Find("Image");` |

**`Assets/DragonBound/Runtime/Presentation/PauseRuneRewardPresenter.cs`** —— 2 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 36 | Transform.Find | `Bg` | `var background = transform.Find("Bg");` |
| 133 | Transform.Find | `ItemImg` | `var icon = instance.transform.Find("ItemImg")?.GetComponent<Image>() ??` |

**`Assets/DragonBound/Runtime/Presentation/TipTextController.cs`** —— 2 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 122 | Transform.Find | `Text` | `if (label == null && transform.Find("Text") != null)` |
| 123 | Transform.Find | `Text` | `label = transform.Find("Text").GetComponent<TextMeshProUGUI>();` |

**`Assets/Scripts/Auth/UI/LoginController.cs`** —— 2 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 89 | Transform.Find | `SignUpPanel` | `signUpPanel = mainPanel.Find("SignUpPanel")?.gameObject;` |
| 410 | Transform.Find | `TipText` | `var authoredTip = googleRoot != null ? googleRoot.Find("TipText") : null;` |

**`Assets/Scripts/Rune/RuneDragItem.cs`** —— 2 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 29 | Transform.Find | `AcText` | `amountRoot = transform.Find("AcText");` |
| 31 | Transform.Find | `Text (TMP)` | `? amountRoot.Find("Text (TMP)")?.GetComponent<TMP_Text>()` |

**`Assets/Scripts/Settings/DragFillController.cs`** —— 2 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 55 | Transform.Find | `FillImg` | `Transform fillTransform = transform.Find("FillImg");` |
| 78 | GetChild | `0` | `handle = fillTransform.GetChild(0) as RectTransform;` |

**`Assets/Scripts/UI/UiGoldExampleEffect.cs`** —— 2 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 32 | Transform.Find | `GoldParticleExample` | `if (targetRect == null \|\| transform.Find("GoldParticleExample") != null)` |
| 343 | Transform.Find | `ImageA` | `var imageA = canvases[canvasIndex].transform.Find("ImageA");` |

**`Assets/DragonBound/Runtime/Presentation/BoardDebugOverlay.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 69 | Transform.Find | `DEV_BoardDebugOverlay` | `var existing = parent.Find("DEV_BoardDebugOverlay");` |

**`Assets/DragonBound/Runtime/Presentation/DraggableUnitView.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 604 | Transform.Find | `ART_SoulChainOverlay` | `var existing = transform.Find("ART_SoulChainOverlay");` |

**`Assets/DragonBound/Runtime/Presentation/HeroFormationView.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 527 | Transform.Find | `ParticalBg` | `var particalBg = transform.Find("ParticalBg");` |

**`Assets/DragonBound/Runtime/Presentation/UnitInformController.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 113 | Transform.Find | `device` | `divider = divider != null ? divider : transform.Find("device")?.gameObject;` |

**`Assets/Scripts/Analytics/AnalyticsConsentUiController.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 252 | GetChild | `index` | `var found = FindRecursive(parent.GetChild(index), objectName);` |

**`Assets/Scripts/Gold/UI/GoldBalanceController.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 22 | Transform.Find | `CoinQua` | `amountText = transform.Find("CoinQua")?.GetComponent<TMP_Text>();` |

**`Assets/Scripts/Rune/MainRuneUnlockController.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 201 | GetChild | `index` | `Transform found = FindDescendant(root.GetChild(index), objectName);` |

**`Assets/Scripts/SceneLoader.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 201 | Transform.Find | `FillImg` | `Transform fillTransform = loadingTransform.Find("FillImg");` |

**`Assets/Scripts/Settings/DamageNumberToggleController.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 15 | Transform.Find | `State` | `Transform stateTransform = transform.Find("State");` |

**`Assets/Scripts/Settings/VisualStateToggleController.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 15 | Transform.Find | `State` | `Transform stateTransform = transform.Find("State");` |

**`Assets/Scripts/UI/UiImageFadeLoop.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 98 | Transform.Find | `ImageA/ImageB` | `var imageB = canvases[canvasIndex].transform.Find("ImageA/ImageB");` |

**`Assets/Scripts/UI/VoidShovelRewardController.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 536 | GetChild | `index` | `Transform result = FindDescendant(root.GetChild(index), objectName);` |

### 5.2 编辑器工具（构建期一次性查找） —— 共 35 处

**`Assets/Editor/GreyboxMerchantLoadoutSceneMigration.cs`** —— 5 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 89 | Transform.Find | `Active` | `Transform activeContainer = itemContainer.Find("Active");` |
| 90 | Transform.Find | `Passtive` | `Transform passiveContainer = itemContainer.Find("Passtive");` |
| 147 | Transform.Find | `Active` | `if (candidate.Find("Active") != null && candidate.Find("Passtive") != null)` |
| 147 | Transform.Find | `Passtive` | `if (candidate.Find("Active") != null && candidate.Find("Passtive") != null)` |
| 170 | Transform.Find | `CooldownMask` | `Transform existing = slot.Find("CooldownMask");` |

**`Assets/Editor/SignInFeatureValidation.cs`** —— 3 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 57 | Transform.Find | `SignPanel` | `Transform panel = controller.transform.Find("SignPanel");` |
| 58 | Transform.Find | `Image` | `Transform imageRoot = panel != null ? panel.Find("Image") : null;` |
| 59 | Transform.Find | `ContentCon` | `Transform content = imageRoot != null ? imageRoot.Find("ContentCon") : null;` |

**`Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs`** —— 26 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 113 | Transform.Find | `ART_StarfallWarning` | `var warningTransform = root.transform.Find("ART_StarfallWarning");` |
| 587 | Transform.Find | `ART_LockOverlay` | `var lockOverlay = view.transform.Find("ART_LockOverlay");` |
| 600 | Transform.Find | `ART_Background` | `var background = instance.transform.Find("ART_Background").GetComponent<Image>();` |
| 610 | Transform.Find | `RouteWaypoints` | `var points = instance.transform.Find("RouteWaypoints").GetComponentsInChildren<RectTransform>(true);` |
| 646 | Transform.Find | `ART_EnemyMarker` | `var marker = instance.transform.Find("ART_EnemyMarker").GetComponent<RectTransform>();` |
| 656 | Transform.Find | `SideLabel` | `var sideLabel = instance.transform.Find("SideLabel").GetComponent<Text>();` |
| 663 | Transform.Find | `HatchlingLabel` | `instance.transform.Find("HatchlingLabel").GetComponent<Text>(),` |
| 664 | Transform.Find | `EnemyProgressLabel` | `instance.transform.Find("EnemyProgressLabel").GetComponent<Text>(),` |
| 665 | Transform.Find | `ART_BossTrack/ART_BossFill` | `instance.transform.Find("ART_BossTrack/ART_BossFill").GetComponent<Image>(),` |
| 705 | Transform.Find | `ART_LockOverlay` | `var lockOverlay = instance.transform.Find("ART_LockOverlay");` |
| 797 | Transform.Find | `ART_DragArrow` | `var aiDragArrow = aiBattlefieldObject.transform.Find("ART_DragArrow").GetComponent<DragArrowPreviewView>();` |
| 798 | Transform.Find | `ART_DragArrow` | `var playerDragArrow = playerBattlefieldObject.transform.Find("ART_DragArrow").GetComponent<DragArrowPreviewView>();` |
| 804 | Transform.Find | `ART_PauseButton` | `hudObject.transform.Find("ART_PauseButton").GetComponent<Button>(),` |
| 805 | Transform.Find | `ART_PauseButton/PauseLabel` | `hudObject.transform.Find("ART_PauseButton/PauseLabel").GetComponent<Text>(),` |
| 806 | Transform.Find | `ResourceLabel` | `hudObject.transform.Find("ResourceLabel").GetComponent<Text>(),` |
| 807 | Transform.Find | `WaveLabel` | `hudObject.transform.Find("WaveLabel").GetComponent<Text>(),` |
| 808 | Transform.Find | `DebugLabel` | `hudObject.transform.Find("DebugLabel").GetComponent<Text>(),` |
| 809 | Transform.Find | `EnemyDebugLabel` | `hudObject.transform.Find("EnemyDebugLabel").GetComponent<Text>());` |
| 862 | Transform.Find | `AiUnitLayer` | `screenObject.transform.Find("AiUnitLayer").GetComponent<RectTransform>(),` |
| 864 | Transform.Find | `AiUnitLayer/ART_AiRangePreview` | `screenObject.transform.Find("AiUnitLayer/ART_AiRangePreview").GetComponent<Image>(),` |
| 866 | Transform.Find | `ART_DragArrow` | `screenView.AiBattlefieldView.transform.Find("ART_DragArrow").GetComponent<DragArrowPreviewView>());` |
| 870 | Transform.Find | `PlayerUnitLayer` | `screenObject.transform.Find("PlayerUnitLayer").GetComponent<RectTransform>(),` |
| 872 | Transform.Find | `PlayerUnitLayer/ART_PlayerRangePreview` | `screenObject.transform.Find("PlayerUnitLayer/ART_PlayerRangePreview").GetComponent<Image>(),` |
| 874 | Transform.Find | `ART_DragArrow` | `screenView.PlayerBattlefieldView.transform.Find("ART_DragArrow").GetComponent<DragArrowPreviewView>());` |
| 976 | Transform.Find | `ART_DragArrow` | `var existing = battlefield.Find("ART_DragArrow");` |
| 1163 | Transform.Find | `ART_RangeOutline` | `var outlineTransform = target.Find("ART_RangeOutline");` |

**`Assets/DragonBound/Editor/AuthoredGreyboxUiMigration.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 207 | Transform.Find | `RangeDismissSurface` | `var existing = parent.Find("RangeDismissSurface");` |

### 5.3 测试（UI 结构断言，改动层级时会一并打断，属良性耦合） —— 共 95 处

**`Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs`** —— 40 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 70 | Transform.Find | `AnalyticsBootstrap` | `var analyticsHost = GameObject.Find("AnalyticsBootstrap");` |
| 70 | GameObject.Find | `AnalyticsBootstrap` | `var analyticsHost = GameObject.Find("AnalyticsBootstrap");` |
| 82 | Transform.Find | `AnalyticsBootstrap` | `Assert.AreSame(analyticsHost, GameObject.Find("AnalyticsBootstrap"));` |
| 82 | GameObject.Find | `AnalyticsBootstrap` | `Assert.AreSame(analyticsHost, GameObject.Find("AnalyticsBootstrap"));` |
| 92 | Transform.Find | `Canvas` | `var canvas = GameObject.Find("Canvas");` |
| 92 | GameObject.Find | `Canvas` | `var canvas = GameObject.Find("Canvas");` |
| 99 | Transform.Find | `Text` | `Assert.IsNotNull(tip.Find("Text"));` |
| 124 | Transform.Find | `Text` | `Assert.IsNotNull(tipController.transform.Find("Text"));` |
| 149 | Transform.Find | `TipText` | `var legacyTip = screen.transform.Find("TipText");` |
| 182 | Transform.Find | `SafeAreaRoot` | `var safeAreaRoot = GameObject.Find("SafeAreaRoot")?.transform as RectTransform;` |
| 182 | GameObject.Find | `SafeAreaRoot` | `var safeAreaRoot = GameObject.Find("SafeAreaRoot")?.transform as RectTransform;` |
| 183 | Transform.Find | `RIVER` | `var river = GameObject.Find("RIVER")?.transform as RectTransform;` |
| 183 | GameObject.Find | `RIVER` | `var river = GameObject.Find("RIVER")?.transform as RectTransform;` |
| 231 | Transform.Find | `ART_ScreenBackground/BeachContainer` | `var beachContainer = FindScreen().transform.Find("ART_ScreenBackground/BeachContainer");` |
| 263 | Transform.Find | `BoardSelect` | `var boardSelection = targetCell.ContentAnchor.Find("BoardSelect");` |
| 414 | Transform.Find | `campPanel` | `var campPanel = screen.transform.Find("campPanel");` |
| 416 | Transform.Find | `CampBg/DeckPart` | `var deckPart = campPanel.Find("CampBg/DeckPart");` |
| 417 | Transform.Find | `CampBg/CollectionPart` | `var collectionPart = campPanel.Find("CampBg/CollectionPart");` |
| 418 | Transform.Find | `BtnImg/DeckBtn` | `var deckButton = campPanel.Find("BtnImg/DeckBtn").GetComponent<Button>();` |
| 419 | Transform.Find | `BtnImg/CollectionBtn` | `var collectionButton = campPanel.Find("BtnImg/CollectionBtn").GetComponent<Button>();` |
| 477 | GetChild | `index` | `var slot = componentContainer.GetChild(index);` |
| 488 | Transform.Find | `campPanel/CampBg/CollectionPart` | `var collectionPart = screen.transform.Find("campPanel/CampBg/CollectionPart");` |
| 496 | Transform.Find | `Img1` | `Assert.AreSame(expectedTop, collectionPart.Find("Img1").GetComponent<Image>().sprite);` |
| 497 | Transform.Find | `Img2` | `Assert.AreSame(expectedBottom, collectionPart.Find("Img2").GetComponent<Image>().sprite);` |
| 502 | Transform.Find | `HeroContainer` | `var heroContainer = collectionPart.Find("HeroContainer");` |
| 509 | GetChild | `index` | `var slot = heroContainer.GetChild(index);` |
| 768 | Transform.Find | `ART_RangeOutline` | `var rangeOutline = range.transform.Find("ART_RangeOutline").GetComponent<Image>();` |
| 842 | Transform.Find | `BeachItem` | `var beachItem = benchCell.transform.Find("BeachItem")?.GetComponent<DraggableUnitView>();` |
| 866 | Transform.Find | `Name` | `ReadTmpText(inform.transform.Find("Name")));` |
| 869 | Transform.Find | `MaxLv` | `ReadTmpText(inform.transform.Find("MaxLv")));` |
| 870 | Transform.Find | `EXP` | `Assert.IsFalse(inform.transform.Find("EXP").gameObject.activeSelf);` |
| 871 | Transform.Find | `device` | `Assert.IsFalse(inform.transform.Find("device").gameObject.activeSelf);` |
| 872 | Transform.Find | `Rune` | `Assert.IsFalse(inform.transform.Find("Rune").gameObject.activeSelf);` |
| 899 | Transform.Find | `InputReceiver` | `var inputReceiver = emptyCell.transform.Find("InputReceiver") as RectTransform;` |
| 935 | Transform.Find | `ART_FixedBoardCellLayer/BoardBackgroundClickSurface` | `.Find("ART_FixedBoardCellLayer/BoardBackgroundClickSurface")` |
| 969 | Transform.Find | `RangeDismissSurface` | `var receiver = FindScreen().transform.Find("RangeDismissSurface")` |
| 1004 | Transform.Find | `BeachSourceSelect` | `var sourceSelection = sourceCell.ContentAnchor.Find("BeachSourceSelect");` |
| 1020 | Transform.Find | `BoardSelect` | `var mapTargetSelection = mapTargetCell.ContentAnchor.Find("BoardSelect");` |
| 1034 | Transform.Find | `BoardSelect` | `var benchSelection = benchCell.ContentAnchor.Find("BoardSelect");` |
| 1089 | Transform.Find | `ART_ScreenBackground/BeachContainer` | `var beachContainer = FindScreen().transform.Find("ART_ScreenBackground/BeachContainer");` |

**`Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs`** —— 28 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 145 | Transform.Find | `ART_SoulChainOverlay` | `var soulChainOverlay = unitCard.transform.Find("ART_SoulChainOverlay")?.GetComponent<Image>();` |
| 166 | Transform.Find | `ART_EnemyAnimation/Image` | `.Find("ART_EnemyAnimation/Image")` |
| 206 | Transform.Find | `ART_UnitPortrait` | `var portrait = instance.transform.Find("ART_UnitPortrait");` |
| 349 | Transform.Find | `weapon` | `Transform weapon = prefab.transform.Find("weapon");` |
| 736 | Transform.Find | `SideLabel` | `Assert.AreEqual("AI", view.AiBattlefieldView.transform.Find("SideLabel").GetComponent<Text>().text);` |
| 737 | Transform.Find | `SideLabel` | `Assert.AreEqual("PLAYER", view.PlayerBattlefieldView.transform.Find("SideLabel").GetComponent<Text>().text);` |
| 755 | Transform.Find | `ART_PathLeft` | `Assert.IsNotNull(battlefield.transform.Find("ART_PathLeft"));` |
| 756 | Transform.Find | `ART_PathRight` | `Assert.IsNotNull(battlefield.transform.Find("ART_PathRight"));` |
| 757 | Transform.Find | `ART_PathTop` | `Assert.IsNotNull(battlefield.transform.Find("ART_PathTop"));` |
| 758 | Transform.Find | `ART_PathBottom` | `Assert.IsNotNull(battlefield.transform.Find("ART_PathBottom"));` |
| 759 | Transform.Find | `ART_Spawn` | `Assert.IsNotNull(battlefield.transform.Find("ART_Spawn"));` |
| 760 | Transform.Find | `ART_Hatchling` | `Assert.IsNotNull(battlefield.transform.Find("ART_Hatchling"));` |
| 761 | Transform.Find | `RouteWaypoints/DragonGoal` | `Assert.IsNotNull(battlefield.transform.Find("RouteWaypoints/DragonGoal"));` |
| 762 | Transform.Find | `ART_EnemyMarker/ART_EnemyHpTrack/ART_EnemyHpFill` | `Assert.IsNotNull(battlefield.transform.Find("ART_EnemyMarker/ART_EnemyHpTrack/ART_EnemyHpFill"));` |
| 763 | Transform.Find | `ART_EnemyMarker/EnemyRuntimeLabel` | `Assert.IsNotNull(battlefield.transform.Find("ART_EnemyMarker/EnemyRuntimeLabel"));` |
| 764 | Transform.Find | `ART_EnemyMarker` | `Assert.IsNotNull(battlefield.transform.Find("ART_EnemyMarker").GetComponent<EnemyView>());` |
| 766 | Transform.Find | `ART_AttackLine` | `Assert.IsNotNull(battlefield.transform.Find("ART_AttackLine"));` |
| 767 | Transform.Find | `ART_BowProjectile` | `Assert.IsNotNull(battlefield.transform.Find("ART_BowProjectile"));` |
| 768 | Transform.Find | `ART_SpearPierceLine` | `Assert.IsNotNull(battlefield.transform.Find("ART_SpearPierceLine"));` |
| 769 | Transform.Find | `ART_RiderSweepCircle` | `Assert.IsNotNull(battlefield.transform.Find("ART_RiderSweepCircle"));` |
| 770 | Transform.Find | `ART_StarfallWarning` | `var starfallWarning = battlefield.transform.Find("ART_StarfallWarning")?.GetComponent<Image>();` |
| 773 | Transform.Find | `DamageNumber` | `Assert.IsNotNull(battlefield.transform.Find("DamageNumber"));` |
| 774 | Transform.Find | `SuppliesGain` | `Assert.IsNotNull(battlefield.transform.Find("SuppliesGain"));` |
| 793 | Transform.Find | `ART_PathLeft` | `battlefield.transform.Find("ART_PathLeft").GetComponent<RectTransform>(),` |
| 794 | Transform.Find | `ART_PathRight` | `battlefield.transform.Find("ART_PathRight").GetComponent<RectTransform>(),` |
| 795 | Transform.Find | `ART_PathTop` | `battlefield.transform.Find("ART_PathTop").GetComponent<RectTransform>(),` |
| 796 | Transform.Find | `ART_PathBottom` | `battlefield.transform.Find("ART_PathBottom").GetComponent<RectTransform>()` |
| 851 | Transform.Find | `ART_RangeOutline` | `var outline = fill.transform.Find("ART_RangeOutline")?.GetComponent<Image>();` |

**`Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs`** —— 16 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 109 | Transform.Find | `Versus` | `Assert.IsNull(screen.transform.Find("Versus"));` |
| 131 | Transform.Find | `ART_ScreenBackground` | `var background = screen.transform.Find("ART_ScreenBackground");` |
| 132 | Transform.Find | `ART_PauseButton` | `var openButton = background.Find("ART_PauseButton").GetComponent<Button>();` |
| 133 | Transform.Find | `PausePanel` | `var panel = background.Find("PausePanel").gameObject;` |
| 134 | Transform.Find | `Bg/PauseBtn` | `var finishButton = panel.transform.Find("Bg/PauseBtn").GetComponent<Button>();` |
| 135 | Transform.Find | `Bg/ContinueBtn` | `var continueButton = panel.transform.Find("Bg/ContinueBtn").GetComponent<Button>();` |
| 152 | Transform.Find | `SettlementPanel` | `var settlement = screen.transform.Find("SettlementPanel");` |
| 154 | Transform.Find | `SettleImg` | `var settlementImage = settlement.Find("SettleImg").GetComponent<Image>();` |
| 159 | Transform.Find | `GoldText` | `Assert.IsFalse(settlement.Find("GoldText").gameObject.activeSelf);` |
| 160 | Transform.Find | `ReciveBtn` | `Assert.IsFalse(settlement.Find("ReciveBtn").gameObject.activeSelf);` |
| 161 | Transform.Find | `DoubleBtn` | `Assert.IsFalse(settlement.Find("DoubleBtn").gameObject.activeSelf);` |
| 176 | Transform.Find | `SettlementPanel` | `var settlement = screen.transform.Find("SettlementPanel");` |
| 178 | Transform.Find | `SettleImg` | `var settlementImage = settlement.Find("SettleImg").GetComponent<Image>();` |
| 183 | Transform.Find | `GoldText` | `Assert.IsFalse(settlement.Find("GoldText").gameObject.activeSelf);` |
| 184 | Transform.Find | `ReciveBtn` | `Assert.IsFalse(settlement.Find("ReciveBtn").gameObject.activeSelf);` |
| 185 | Transform.Find | `DoubleBtn` | `Assert.IsFalse(settlement.Find("DoubleBtn").gameObject.activeSelf);` |

**`Assets/DragonBound/Tests/EditMode/AuthoredGreyboxUiTests.cs`** —— 6 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 26 | Transform.Find | `RangeDismissSurface` | `Assert.IsNotNull(prefab.transform.Find("RangeDismissSurface"));` |
| 27 | Transform.Find | `ItemEntryButton` | `Assert.IsNull(prefab.transform.Find("ItemEntryButton"));` |
| 28 | Transform.Find | `ART_ItemLoadout` | `Assert.IsNull(prefab.transform.Find("ART_ItemLoadout"));` |
| 29 | Transform.Find | `ART_HeroWorkshop` | `Assert.IsNull(prefab.transform.Find("ART_HeroWorkshop"));` |
| 30 | Transform.Find | `ART_RuneLoadout` | `Assert.IsNull(prefab.transform.Find("ART_RuneLoadout"));` |
| 31 | Transform.Find | `Versus` | `Assert.IsNull(prefab.transform.Find("Versus"));` |

**`Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs`** —— 3 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 101 | Transform.Find | `ART_EnemyAnimation/Image` | `enemyView.transform.Find("ART_EnemyAnimation/Image").position,` |
| 1740 | Transform.Find | `ART_StormShieldVFX` | `var shield = root.transform.Find("ART_StormShieldVFX");` |
| 1784 | Transform.Find | `ART_EnemyDeathVFX` | `var deathVfx = root.transform.Find("ART_EnemyDeathVFX");` |

**`Assets/DragonBound/Tests/EditMode/ItemGameplayIntegrationTests.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 287 | Transform.Find | `CooldownMask` | `var cooldownMask = first.transform.Find("CooldownMask").GetComponent<Image>();` |

**`Assets/DragonBound/Tests/EditMode/RecruitItemColorTests.cs`** —— 1 处

| 行 | 方式 | 目标 | 代码 |
|---:|---|---|---|
| 93 | Transform.Find | `ART_SoulChainOverlay` | `Assert.IsNull(root.transform.Find("ART_SoulChainOverlay"));` |

## 六、`Resources.Load<T>` 资产路径全量明细（143 处）

类型分布：Sprite 98、Animator 27、GameObject 13、其他 5

### 6.1 运行时（生产代码，需重点维护） —— 共 86 处

| 文件 | 行 | 类型 | 路径 |
|---|---:|---|---|
| `Assets/DragonBound/Runtime/Presentation/CampPanelView.cs` | 486 | Sprite | `resourcePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 781 | Animator | `RuneboltMageBoltControllerPath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 1098 | Sprite | `StoneboundWarlockSkillRockSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 1103 | Sprite | `StoneboundWarlockNormalRockSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 1347 | Sprite | `ThunderlordMainChainSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 1351 | Sprite | `ThunderlordFirstChainSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 1355 | Sprite | `ThunderlordSecondChainSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 2510 | Sprite | `AbyssalHarpoonChainSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 2511 | Sprite | `AbyssalHarpoonHookSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 2512 | Sprite | `AbyssalHarpoonPortalSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 2519 | Sprite | `AbyssalHarpoonChainSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 2520 | Sprite | `AbyssalHarpoonHookSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 2521 | Sprite | `AbyssalHarpoonPortalSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 2528 | Sprite | `AbyssalHarpoonChainSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 2529 | Sprite | `AbyssalHarpoonHookSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 2530 | Sprite | `AbyssalHarpoonPortalSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 2624 | Sprite | `EmberShamanFireballSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 3091 | Sprite | `BowSwordProjectileSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 3107 | Sprite | `WindclawImpactSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 3371 | Sprite | `SkyborneValkyrieArrowSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 3743 | Sprite | `FlameDrakeSkillFireballSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 3755 | Sprite | `FlameDrakeNormalFireballSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 3771 | Animator | `FlameDrakeSkillExplosionControllerPath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 3783 | Animator | `FlameDrakeNormalExplosionControllerPath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 3797 | Animator | `FlameDrakeBurningGroundControllerPath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 4044 | Animator | `NightfangExplosionControllerPath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 4329 | Animator | `StarfallExplosionControllerPath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 4459 | Sprite | `StarfallNormalSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/CombatFxView.cs` | 4583 | Animator | `"Animation/Flame Drake Rider S"` |
| `Assets/DragonBound/Runtime/Presentation/DragArrowPreviewView.cs` | 84 | Sprite | `DragPathSpriteResourcePath` |
| `Assets/DragonBound/Runtime/Presentation/DraggableUnitView.cs` | 976 | Animator | `resourcePath` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 195 | Sprite | `FrostcrownMarkSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 254 | Sprite | `FrostMireMarkSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 313 | Sprite | `WinterveilMarkSpritePath` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 463 | Sprite | `BossHpResourcePath` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 712 | Animator | `DeathControllerResourcePath` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 868 | Animator | `StormShieldControllerResourcePath` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 869 | Sprite | `StormShieldFirstFrameResourcePath` |
| `Assets/DragonBound/Runtime/Presentation/EnemyView.cs` | 1160 | Animator | `"Animation/" + controllerName` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs` | 1404 | GameObject | `InformPrefabResourcePath` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs` | 2116 | GameObject | `"prefabs/BeachItem"` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs` | 2182 | Sprite | `ShovelSpriteResourcePath` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs` | 2447 | GameObject | `BoardSelectPrefabResourcePath` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs` | 2463 | Sprite | `BeachSelectSpriteResourcePath` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxBoardView.cs` | 2468 | Sprite | `BoardSelectSpriteResourcePath` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 594 | Sprite | `resourcePath` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 1436 | Animator | `ArcaneThunderburstControllerPath` |
| `Assets/DragonBound/Runtime/Presentation/GreyboxHudView.cs` | 1760 | Sprite | `definition.IconKey` |
| `Assets/DragonBound/Runtime/Presentation/GridCellView.cs` | 290 | Sprite | `"GameUI/Lock"` |
| `Assets/DragonBound/Runtime/Presentation/GridCellView.cs` | 300 | Sprite | `"GameUI/UnLock"` |
| `Assets/DragonBound/Runtime/Presentation/HeroFormationView.cs` | 774 | Animator | `LevelUpControllerResourcePath` |
| `Assets/DragonBound/Runtime/Presentation/HeroFormationView.cs` | 817 | Animator | `controllerPath` |
| `Assets/DragonBound/Runtime/Presentation/HeroFormationView.cs` | 1229 | Sprite | `"GameUI/HeroPurple"` |
| `Assets/DragonBound/Runtime/Presentation/HeroFormationView.cs` | 1230 | Sprite | `"GameUI/HeroGold"` |
| `Assets/DragonBound/Runtime/Presentation/HeroFormationView.cs` | 1387 | Sprite | `resourcePath` |
| `Assets/DragonBound/Runtime/Presentation/HeroFormationView.cs` | 1467 | Animator | `resourcePath` |
| `Assets/DragonBound/Runtime/Presentation/HeroFormationView.cs` | 1510 | Animator | `resourcePath` |
| `Assets/DragonBound/Runtime/Presentation/PauseRuneRewardPresenter.cs` | 160 | GameObject | `NoRewardPrefabPath` |
| `Assets/DragonBound/Runtime/Presentation/PauseRuneRewardPresenter.cs` | 166 | GameObject | `FallbackRewardPrefabPath` |
| `Assets/DragonBound/Runtime/Presentation/ResourcesCampComponentArtProvider.cs` | 135 | Sprite | `resourcePath` |
| `Assets/DragonBound/Runtime/Presentation/TipTextService.cs` | 44 | GameObject | `PrefabPath` |
| `Assets/Scripts/Auth/UI/LoginController.cs` | 420 | GameObject | `TipTextPrefabPath` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 96 | Sprite | `SelectedTabSpritePath` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 97 | Sprite | `UnselectedTabSpritePath` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 116 | GameObject | `OfferItemPrefabPath` |
| `Assets/Scripts/Merchant/MainMerchantController.cs` | 117 | GameObject | `OwnedItemPrefabPath` |
| `Assets/Scripts/Merchant/MerchantModels.cs` | 250 | 其他 | `iconKey` |
| `Assets/Scripts/Rank/UI/GreyboxSettlementRewardController.cs` | 40 | Sprite | `VictorySpritePath` |
| `Assets/Scripts/Rank/UI/GreyboxSettlementRewardController.cs` | 41 | Sprite | `DefeatSpritePath` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 66 | GameObject | `ItemResourcePath` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 70 | Sprite | `WeekSelectedSpritePath` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 71 | Sprite | `WeekUnselectedSpritePath` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 72 | Sprite | `FirstPlaceSpritePath` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 73 | Sprite | `SecondPlaceSpritePath` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 74 | Sprite | `ThirdPlaceSpritePath` |
| `Assets/Scripts/Rank/UI/MainLeaderboardController.cs` | 75 | Sprite | `OtherPlaceSpritePath` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 66 | GameObject | `WeaponPrefabPath` |
| `Assets/Scripts/Rune/RuneWeaponPanelController.cs` | 67 | GameObject | `Weapon0PrefabPath` |
| `Assets/Scripts/Services/Composition/ClientCompositionRoot.cs` | 23 | 其他 | `ConfigResourcePath` |
| `Assets/Scripts/SignIn/UI/MainSignInController.cs` | 122 | Sprite | `TodaySpritePath` |
| `Assets/Scripts/SignIn/UI/MainSignInController.cs` | 123 | Sprite | `OtherSpritePath` |
| `Assets/Scripts/UI/MainNavTabController.cs` | 80 | Sprite | `SelectSpritePath` |
| `Assets/Scripts/UI/MainNavTabController.cs` | 81 | Sprite | `NoSelectSpritePath` |
| `Assets/Scripts/UI/PlayerAvatarProfile.cs` | 58 | Sprite | `ResourceRoot + index` |
| `Assets/Scripts/UI/PlayerAvatarProfile.cs` | 61 | Sprite | `ResourceRoot + FirstAvatarIndex` |
| `Assets/Scripts/UI/PlayerAvatarView.cs` | 103 | GameObject | `PrefabResourcePath` |

### 6.2 编辑器工具（构建期一次性查找） —— 共 3 处

| 文件 | 行 | 类型 | 路径 |
|---|---:|---|---|
| `Assets/Editor/ClientServiceCompositionValidation.cs` | 13 | 其他 | `ConfigPath` |
| `Assets/Editor/ProductionRunContractValidation.cs` | 37 | 其他 | `ClientConfigPath` |
| `Assets/Editor/ProductionRunContractValidation.cs` | 45 | 其他 | `StageManifestPrefix + config.DefaultStageId` |

### 6.3 测试（UI 结构断言，改动层级时会一并打断，属良性耦合） —— 共 54 处

| 文件 | 行 | 类型 | 路径 |
|---|---:|---|---|
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 233 | Sprite | `"VFX/Unit/road"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 234 | Animator | `"Animation/UnitAni/BOW"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 435 | Sprite | `"VFX/Windclaw Ranger/road"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 449 | Sprite | `"VFX/Ember Shaman/road"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 482 | Sprite | `"VFX/Stonebound Warlock/rood"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 483 | Sprite | `"VFX/Stonebound Warlock/roodS"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 499 | Sprite | `"VFX/Thunderlord/road/Main"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 500 | Sprite | `"VFX/Thunderlord/road/Froad"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 501 | Sprite | `"VFX/Thunderlord/road/Sroad"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 535 | Sprite | `"VFX/Abyssal Harpooner/road"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 536 | Sprite | `"VFX/Abyssal Harpooner/boom"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 556 | Sprite | `"VFX/Abyssal Harpooner/start"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 572 | Animator | `"Animation/Flame Drake Rider S"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 581 | Sprite | `"VFX/Flame Drake Rider/Sroad"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 582 | Sprite | `"VFX/Flame Drake Rider/road"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 628 | Animator | `"Animation/boomBoard"` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 636 | Sprite | `"VFX/Skyborne Valkyrie/road"` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 258 | Sprite | `"GameUI/BeachSelect"` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 266 | Sprite | `"GameUI/BoardSelect"` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 272 | Sprite | `"GameUI/SlectRoad"` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 422 | Sprite | `"GameUI/CampUI/Deck"` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 423 | Sprite | `"GameUI/CampUI/DeckClick"` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 424 | Sprite | `"GameUI/CampUI/Collection"` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 425 | Sprite | `"GameUI/CampUI/CollectionClick"` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 1007 | Sprite | `"GameUI/BeachSelect"` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 1023 | Sprite | `"GameUI/BoardSelect"` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 1038 | Sprite | `"GameUI/BoardSelect"` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 1087 | Sprite | `"ComponentUI/shovel"` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 156 | Sprite | `"GameUI/SettlementUI/Defeat"` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 180 | Sprite | `"GameUI/SettlementUI/Victory"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 109 | Sprite | `"VFX/Frostcrown Hunter/road"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 144 | Sprite | `"VFX/Item/微信图片_20260909164612_605_101"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 182 | Sprite | `"VFX/Item/微信图片_20260909164615_606_101"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 542 | Sprite | `"VFX/Flame Drake Rider/Sroad"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 561 | Animator | `"Animation/FlameDrakeRiderBoomS"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 579 | Animator | `"Animation/boomBoard"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 659 | Sprite | `"VFX/Flame Drake Rider/road"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 753 | Sprite | `"VFX/Ember Shaman/road"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 823 | Animator | `"Animation/Runebolt MageBoom"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 918 | Sprite | `"VFX/Unit/road"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 990 | Sprite | `"VFX/Stonebound Warlock/rood"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1031 | Sprite | `"VFX/Stonebound Warlock/roodS"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1108 | Sprite | `"VFX/Thunderlord/road/Main"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1124 | Sprite | `"VFX/Thunderlord/road/Froad"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1136 | Sprite | `"VFX/Thunderlord/road/Sroad"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1207 | Animator | `"Animation/ThunderlordBoom"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1281 | Sprite | `"VFX/Abyssal Harpooner/road"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1288 | Sprite | `"VFX/Abyssal Harpooner/boom"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1380 | Sprite | `"VFX/Skyborne Valkyrie/road"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1503 | Animator | `"Animation/StarfallArchmageBoom"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1563 | Animator | `"Animation/NightfangAssassinBoom"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1621 | Sprite | `"VFX/Windclaw Ranger/road"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1747 | Animator | `"Animation/ShieldShieldW"` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1790 | Animator | `"Animation/DieBoom"` |

## 七、编辑器 `AssetDatabase` 资产路径（40 处）

> 按字符串路径读写工程内资产，改文件位置即断链；仅编辑器/测试使用。

| 文件 | 行 | 方式 | 路径 |
|---|---:|---|---|
| `Assets/DragonBound/Editor/AuthoredGreyboxUiMigration.cs` | 69 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab` |
| `Assets/DragonBound/Editor/DragonBoundHeroSliceSceneBuilder.cs` | 18 | LoadAssetAtPath(常量) | `Assets/Scenes/Greybox_Main.unity` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 61 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 74 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 648 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Components/EnemyCard.prefab` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 898 | LoadAssetAtPath(变量) | `scenePath` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 920 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Components/UnitCard.prefab` |
| `Assets/DragonBound/Editor/DragonBoundPortraitUiBuilder.cs` | 1101 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Art/Range/RangeOutlineThin.png` |
| `Assets/DragonBound/Editor/HandoffUiAssetBuilder.cs` | 29 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Handoff/Prefabs/UI_HandoffScreen.prefab` |
| `Assets/DragonBound/Editor/HandoffUiAssetBuilder.cs` | 30 | LoadAssetAtPath(常量) | `Assets/DragonBound/Scenes/UI_Handoff.unity` |
| `Assets/DragonBound/Editor/HandoffUiAssetBuilder.cs` | 53 | LoadAssetAtPath(常量) | `Assets/TextMesh Pro/Resources/TMP Settings.asset` |
| `Assets/DragonBound/Editor/HandoffUiAssetBuilder.cs` | 71 | LoadAssetAtPath(常量) | `Assets/TextMesh Pro/Resources/TMP Settings.asset` |
| `Assets/DragonBound/Editor/HandoffUiAssetBuilder.cs` | 85 | LoadAssetAtPath(常量) | `Assets/TextMesh Pro/Resources/TMP Settings.asset` |
| `Assets/DragonBound/Tests/EditMode/AuthoredGreyboxUiTests.cs` | 18 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab` |
| `Assets/DragonBound/Tests/EditMode/AuthoredGreyboxUiTests.cs` | 37 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab` |
| `Assets/DragonBound/Tests/EditMode/AuthoredGreyboxUiTests.cs` | 64 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab` |
| `Assets/DragonBound/Tests/EditMode/HandoffUiEditModeTests.cs` | 18 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Handoff/Prefabs/HandoffMerchantOffer.prefab` |
| `Assets/DragonBound/Tests/EditMode/HandoffUiEditModeTests.cs` | 19 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Handoff/Prefabs/UI_HandoffScreen.prefab` |
| `Assets/DragonBound/Tests/EditMode/HandoffUiEditModeTests.cs` | 39 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Handoff/Prefabs/UI_HandoffScreen.prefab` |
| `Assets/DragonBound/Tests/EditMode/HandoffUiEditModeTests.cs` | 57 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Handoff/Prefabs/UI_HandoffScreen.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 126 | LoadAssetAtPath(变量) | `path` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 138 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Components/UnitCard.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 160 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Components/EnemyCard.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 202 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Components/UnitCard.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 246 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Components/UnitCard.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 247 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Components/HeroFormation.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 267 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 279 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Components/HeroFormation.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 324 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Components/HeroFormation.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 346 | LoadAssetAtPath(常量) | `Assets/Resources/prefabs/Hero.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 379 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Components/HeroFormation.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 407 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Components/HeroFormation.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 728 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 748 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Modules/Battlefield.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 784 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Modules/Battlefield.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 823 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Modules/Bench.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 824 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Modules/Recruitment.prefab` |
| `Assets/DragonBound/Tests/EditMode/PortraitUiPrefabTests.cs` | 839 | LoadAssetAtPath(常量) | `Assets/DragonBound/UI/Prefabs/Screens/DragonBoundPortraitScreen.prefab` |
| `Assets/Editor/SignInFeatureValidation.cs` | 73 | LoadAssetAtPath(字面量) | `Assets/Resources/Main/Signin/Today.png` |
| `Assets/Editor/SignInFeatureValidation.cs` | 77 | LoadAssetAtPath(字面量) | `Assets/Resources/Main/Signin/other.png` |

## 八、场景名路径加载 `SceneManager.LoadScene`（60 处）

涉及场景：`HeroSlice_Main`(30)、`Greybox_Main`(24)、`Login`(3)、`Main`(2)、`sceneName`(1)

| 文件 | 行 | 场景名 |
|---|---:|---|
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 64 | `Login` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 75 | `Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 89 | `Login` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 105 | `Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 133 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 145 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 177 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 222 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 330 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 381 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 410 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 464 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 522 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 645 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 674 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 742 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 829 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 878 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 915 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 950 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 984 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 1070 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/BootstrapPlayModeTests.cs` | 1120 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 19 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 32 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 45 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 124 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/FixedBoardCanvasPlayModeTests.cs` | 167 | `Greybox_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 23 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 50 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 85 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 127 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 164 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 207 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 239 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 262 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 337 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 360 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 384 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 436 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 456 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 504 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 614 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 690 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 769 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 874 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 941 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1041 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1144 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1220 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1311 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1389 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1455 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1517 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1578 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1804 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1904 | `HeroSlice_Main` |
| `Assets/DragonBound/Tests/PlayMode/HeroSlicePlayModeTests.cs` | 1938 | `HeroSlice_Main` |
| `Assets/Scripts/Auth/Session/AuthSessionCoordinator.cs` | 109 | `Login` |
| `Assets/Scripts/SceneLoader.cs` | 110 | `sceneName` |

