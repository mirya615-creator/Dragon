# CLAUDE.md

本文件为 Claude Code 提供项目上下文，每次会话自动加载。修改本文件时请保持信息准确、简洁，不要写能从代码/文档直接推导出的临时细节。

## 项目概述

DragonBound 是一款 **Unity 自走棋（auto-battler）** 游戏。核心玩法循环：

- **招募（Recruitment）**：从确定性的组件/卡牌池招募单位，UNIQUE 组件招募后从池中永久移除
- **布阵（Board / Grid）**：在网格棋盘上拖拽放置单位，相邻单位可合成为英雄（Hero）
- **战斗（Combat）**：自走棋式自动战斗，含英雄战斗状态、目标选择、Boss 战、多目标/范围攻击
- **成长系统**：符文（Runes）、道具（Items）、英雄经验/升级
- **波次推进（Wave）**：从 3 波切片到 20 波压力测试，含 Boss 关卡

项目当前处于 **DragonBound 模块合并** 阶段（分支 `codex/merge-dragonbound`），同时存在旧游戏框架代码与新的 DragonBound 代码。

## 技术栈

- Unity **2022.3.62f3c1**（见 `ProjectSettings/ProjectVersion.txt`）
- 语言：C#
- UI：UGUI + TextMeshPro + Coffee.UIParticle + com.nobi.roundedcorners
- 测试：Unity Test Framework（EditMode + PlayMode）
- 无外部 CI 配置（无 `.github/`），测试在编辑器内/命令行运行，日志输出到 `Logs/`

## 目录结构

| 路径 | 说明 |
|---|---|
| `Assets/DragonBound/Runtime/` | 核心游戏逻辑（主程序集 `DragonBound.Runtime`） |
| `Assets/DragonBound/Editor/` | 编辑器工具、场景构建器（`DragonBound.Editor`） |
| `Assets/DragonBound/UI/` | UI prefab（如 `UI/Prefabs/Components/`） |
| `Assets/DragonBound/Tests/` | 测试（`EditMode/` 与 `PlayMode/`） |
| `Assets/GameShared/Runtime/` | 跨游戏共享：随机数、设置、遥测（`GameShared.Runtime`） |
| `Assets/Scripts/` | 平台/元游戏层：认证、广告、金币、排行、商城、符文账号等 |
| `Assets/Resources/` | 运行时资源（大量动画 `.anim` / `.controller`） |
| `Assets/Scenes/` | 场景（`Game`、`Greybox_Main`、`HeroSlice_Main`、`Login`、`Main`、`UI_Handoff`） |
| `Docs/DragonBound/` | 设计文档（波次校准、Boss、符文、道具、英雄切片、QA 等） |
| `tools/` | 辅助脚本（如分析文档生成） |

### `Assets/DragonBound/Runtime/` 内部模块

- `Core/`（最大，~33 文件）：敌人运行时、波次压力运行时、多波 runtime 等
- `Presentation/`（~30 文件）：各 View（棋盘、HUD、招募面板、英雄阵型等）
- `Recruitment/`（~17 文件）：招募定义、招募服务、招募牌组
- `Runes/`（~14 文件）：符文系统
- `Items/`（~11 文件）：道具系统
- `Grid/`：网格棋盘、拖拽放置控制器
- `Bosses/`：Boss 系统
- `Combat/`：英雄战斗状态、目标选择系统、英雄目录
- `AI/`：单位 AI 控制器、策略配置
- `Analytics/`：事件 schema、Drakeforge 分析适配器
- `Bootstrap/`、`Board/`、`Enemies/`、`Heroes/`、`Foundation/`、`HandoffUi/`、`Services/`

## 架构约定（重要）

采用 **asmdef 模块化**，多数业务域有独立的 **Contracts（契约）+ Runtime（实现）** 分离：

- `DragonBound.<Module>.Contracts` — 纯接口/数据契约，被测试与实现共同引用
- `DragonBound.<Module>.Runtime` — 具体实现
- `DragonBound.Runtime` — 主程序集，聚合 Core/AI/Grid/Presentation 等

依赖方向：Runtime → Contracts，**禁止反向依赖**。修改代码时请保持各程序集边界，新增类型先想清楚归属哪个 asmdef。

示例：`Bosses` / `Items` / `Runes` 同时有 Contracts 和 Runtime；`Board` / `Combat` / `Enemies` / `Heroes` / `Foundation` 目前只有 Contracts；`Recruitment` 目前只有 Runtime。

## 测试

- 位置：`Assets/DragonBound/Tests/EditMode/` 与 `Assets/DragonBound/Tests/PlayMode/`
- 测试命名：每个被测类/系统一个文件，命名 `<Domain><Concern>Tests.cs`（如 `GameplayRunGatewayTests.cs`）
- 运行方式：Unity Test Runner（编辑器内，或命令行 `-runTests -testPlatform EditMode/PlayMode -batchmode -nographics`）
- 结果日志输出到 `Logs/`（XML + log，PlayMode 可能含截图 PNG）

## 代码规范与注意事项

- 提交信息使用**中文**，前缀 `fix：` / `feat：`（注意是全角冒号 `：`），如 `feat：新增局内英雄符文UI呈现`
- Unity 会为每个资产生成 `.meta` 文件，改动/新增资产时需保留对应 `.meta`，删除资产需一并删除 `.meta`
- 大量 `.csproj` / `.sln` 由 Unity 生成，已在 `.gitignore` 忽略（`*.csproj`、`*.sln`）；`Library/`、`Temp/`、`Logs/` 亦忽略，无需提交
- 运行时资源通过 `Resources/` 目录按名称加载（如动画按英雄名，如 `Abyssal Harpooner.anim`）

## 关键文档索引

设计/校准文档集中在 `Docs/DragonBound/`，按需查阅：

- `DragonBoundHeroSliceAcceptance.md` — 英雄垂直切片验收（招募、合成、战斗数值）
- `Drakeforge_Boss_System_V1_*.md` — Boss 系统设计
- `DragonBoundReuseAudit.md` / `DragonBoundReuseFileMatrix.csv` — 复用审计
- `TestLanes.md` / `QARegressionMatrixV1.md` — 测试车道与回归矩阵
