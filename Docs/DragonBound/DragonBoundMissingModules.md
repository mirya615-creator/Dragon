# DragonBound 灰盒缺失模块

本清单基于 `F:\unity文件\My project\Assets\BlackClockAlpha` 的实际代码、配置和已运行测试；策划文档只用于定义 DragonBound 目标，不作为“已实现”证据。

## 当前缺口

| DragonBound 模块 | 现状 | 结论 |
|---|---|---|
| Core / MatchState / RunController | 只有 BlackClock `RunPhase`、`IRunController` 等契约，没有运行时控制器、胜负结算或双方面状态 | 重新实现 |
| Grid | 有 12 槽 Workbench 和 3 路 6 部署位快照，没有 6 战斗格 + 5 营格、扩格、二维占用索引 | 重新实现；仅借鉴事务/快照模式 |
| Input / Drag | `IWorkbench.TryDrop`、`TrySwap` 是逻辑命令，没有拖拽指针、合法高亮、非法回退 UI | 重新实现输入层，复用命令式接口思路 |
| Recruitment / Economy | Supply 实现存在于源工程其他目录，但属于医院补给；列举目录内没有 DragonBound 招募池、费用、双池、五连 | 重新实现 |
| Basic Units / Heroes | 只有四个 BlackClock 职业及一/二星配置；没有四个基础单位、Lv1-Lv5、英雄组件池、英雄经验 | 重新实现数据与运行时 |
| Recipe / Same-unit merge | BlackClock Workbench 有 8 条配方并有测试；没有 DragonBound 基础单位同类型同等级合成 | 只借鉴 `RecipeMergeResolver` 原子提交和测试方式，重新实现规则 |
| Combat scheduler | 有可调用的职业 `TryAttack` 和 `AttackCooldown`，没有统一自动攻击调度器或单位生命周期 | 重新实现调度边界，复用冷却算法 |
| 2D circular range | 目标判定使用 lane 优先级 + 一维位置差，不是二维欧氏圆 | 重新实现 |
| Attack patterns | 有 Blackpowder 贯穿和 Miasma 范围伤害；没有 DragonBound 通用单体/范围/贯穿/眩晕模式枚举或执行器 | 重新实现通用攻击模型 |
| StatusEffects / Stun | 源工程没有 `Stun`、`StatusEffect` 或状态接口实现 | 重新实现 |
| Enemies / Path | 有三路线性敌人移动、漏怪和路障耦合；没有 DragonBound 独立路径图、Boss 或通用终点结算 | 重新实现路径与结算；只借鉴快照/事件 |
| Waves | `NormalWaveCatalog` 强制 3 波，控制器要求场上敌人清空后才可启动下一波；无残怪继承 | 重新实现 15 波、Boss、残怪继承 |
| Boss | 目标目录只有 `ReportBossDefeated` 契约，未发现 Boss 定义/运行时/生成流程 | 重新实现 |
| AI | 没有 DragonBound AI 决策、独立资源/棋盘/敌人实例或三种性格 | 重新实现 |
| Match settlement | 只有 `Victory`/`Defeat` 枚举和失败上报契约，没有可运行胜负优先级实现 | 重新实现 |
| Diagnostics / RunSeed | `RunSeed`、PCG32 `RunRandom`、诊断访问策略和测试真实存在 | 迁移到 GameShared，改命名/路径 |
| Telemetry | JSONL writer、序列号、schema 校验、Android/PC 路径和失败隔离真实存在；schema 含 BlackClock 事件名 | 提取通用 writer/session，重写 DragonBound schema |
| Tests | EditMode 125/125、PlayMode 23/23 通过；测试程序集仍引用 BlackClock asmdef | 复制测试结构并改为 DragonBound 测试，不复制业务断言 |
| Project scaffolding | DragonBound 项目只有基础 Assets/Scenes，没有 GameShared/DragonBound asmdef、Runtime、Tests、主场景玩法代码 | 第一批任务建立目录、asmdef 和空白可运行场景 |

## 禁止迁移边界

以下源模块即使测试通过，也不得复制到 DragonBound：感染及感染事件/阶段、黑钟及能量/停敌、治疗/Lumen、路障/Barricade、医院补给/Supply、Core Slot，以及绑定这些概念的契约、配置、职业行为和测试。它们在文件矩阵中标为 D；仅允许在报告中作为缺口或负面证据出现。

## 未解决的真实证据

- 目标工程当前没有 DragonBound 玩法实现，因此不能宣称上述缺口已在目标工程运行。
- 源工程的测试只证明 BlackClock Alpha 代码当前可编译并通过现有测试，不证明 DragonBound 规则已经实现。
- Unity 测试使用源工程现有未提交 UI/ProjectSettings 改动的工作树；本审计未修改这些改动。
