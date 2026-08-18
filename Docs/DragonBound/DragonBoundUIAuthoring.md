# DragonBound UI 编辑约定

DragonBound 的固定界面结构保存在 `Assets/DragonBound/UI/Prefabs`，不是运行时由代码搭建。美术和前端应直接在 Unity Prefab Mode 中调整布局、Sprite、文字、颜色和按钮状态。

## Prefab 分工

- `Screens/DragonBoundPortraitScreen.prefab`：只负责 HUD、战场、营栏和征兵区的整体锚点与占比。
- `Modules/HUD.prefab`：顶部资源、波次和暂停入口。
- `Modules/Battlefield.prefab`：战场底图、状态边栏、固定战斗格和扩格预留。
- `Modules/Bench.prefab`：五个固定营栏格。
- `Modules/Recruitment.prefab`：征兵按钮、消耗和状态文本。
- `Components/BoardCell.prefab`、`BenchSlot.prefab`：格子外观模板。
- `Components/UnitCard.prefab`：唯一允许在运行时重复实例化的 UI Prefab。

## 可编辑节点

- 名称以 `ART_` 开头的 `Image` 是美术贴图槽，可直接替换 Source Image。
- 固定格子、按钮和文字均已存在于 Prefab，可在 Inspector 中修改锚点、尺寸、字体、颜色及 Sprite State。
- `UnitLayer` 是动态单位卡容器；不要在这里手工摆放正式单位。
- `ContentAnchor` 是单位吸附点；调整格子视觉时应同步检查该锚点。
- `OverlayRoot` 预留给弹窗、引导和战斗浮层，不参与主界面布局。

## 编辑边界

- 不要 Unpack 场景中的 Screen Prefab。
- 整区位置在 Screen Prefab 中调整，模块内部视觉在对应 Module Prefab 中调整。
- 不要删除挂有 `GridCellView`、`GreyboxBoardView`、`GreyboxHudView`、`GreyboxRecruitmentPanel` 的节点或清空其序列化引用。
- 固定战斗格为 6 个、营栏格为 5 个、扩格预留为 4 个；美术调整不能改变这些逻辑数量和坐标。
- `DragonBoundProjectBootstrap` 只用于首次生成或重建模板。美术开始覆盖模板后，不要再次执行重建命令。
