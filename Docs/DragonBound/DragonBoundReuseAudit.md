# DragonBound 代码复用审计

审计对象：`F:\unity文件\My project\Assets\BlackClockAlpha`  
交付工程：`F:\unity文件\dragon`  
审计日期：2026-08-01  
审计性质：只读审计；没有修改源工程、源场景、ProjectSettings，也没有创建 DragonBound 玩法代码。

## 结论

黑钟灰盒中真正适合进入 DragonBound `GameShared` 的，是随机数、日志/埋点的基础设施模式，以及少量无业务的快照、端口、冷却和确定性排序算法。它们都需要把 namespace、asmdef、日志目录和事件 schema 改成 DragonBound，因此矩阵以 B 为主，不把“通过测试”误判为“可原样迁移”。

WorkBench、三路部署、职业战斗、敌人移动和三波控制器是真实可运行的 BlackClock Alpha 代码，但它们分别绑定 12 槽工作台、三路 24 格、医生职业、Lumen/路障/感染或三波约束，不能作为 DragonBound 玩法实现直接复制。感染、黑钟、治疗、路障、医院补给和 Core Slot 相关文件统一标为 D，DragonBound 禁止迁移。

DragonBound 当前工程没有玩法代码、GameShared asmdef、DragonBound asmdef 或测试程序集。因此本次交付是“可复用边界 + 缺失模块 + 第一批任务”，不是灰盒实现。

## Git 审计

执行位置：`F:\unity文件\My project`。

- `git status --short --branch`：分支为 `master`，工作树非 clean；已有 UI、字体、场景、ProjectSettings 和审查文档改动。它们与本审计无关，全部保留。
- `8162942` 存在：`chore: freeze Alpha shared contracts`，冻结共享契约。
- `4a7b4dc` 存在：`feat: implement C03 diagnostics foundation`，实现 C03 诊断基础。
- C01 后续真实提交存在于 refs：`c5801db`/`5623561`（C01A Workbench 合成）、`278a4e5`/`fb352ba`（C01B 职业战斗）、`aaf9251`/`65c0c0b`（C01B 感染事实）。当前 `master` 包含可运行的相应代码，但不能把其他 ref 的提交状态当成当前分支状态。
- C02 后续真实提交存在于 refs：`f713965`/`66a059c`（C02A 三路波次战场）、`9b66338`/`5b24d7b`（C02A 计时修复）、`bde9d0d`/`35709d3`（C02B 简化 Boss）、`6f64f3e`（C02B 计时修复）、`74ca787`/`bd3e9f4`（C02 感染事实）。`git merge-base --is-ancestor 35709d3 master` 为 false，Boss 提交不在当前 `master`；当前工作树也没有 Boss Runtime/Config/Tests 文件，这正是“不能假定已实现”的证据。
- C03：`4a7b4dc` 在当前 `master`，但未发现其之后的 C03 运行时代码实现提交；后续只有 C03 审查/治理文档。当前可运行证据仅覆盖现有诊断基础设施。

## 枚举范围与证据

矩阵覆盖：

- Runtime 代码 55 个 `.cs`，包括用户指定的 Contracts、Random、Diagnostics、Telemetry、Data、Workbench、Combat、Deployment、Waves。
- Runtime asmdef 1 个。
- Tests 代码 45 个 `.cs`，包含 EditMode 和 PlayMode；测试 asmdef 2 个。
- 44 个被实际引用的 `Assets/BlackClockAlpha/Config/**/*.asset` 也纳入矩阵。它们不在列出的 Runtime 子目录内，但决定了运行时配置是否真实存在。

完整逐文件字段见 [DragonBoundReuseFileMatrix.csv](DragonBoundReuseFileMatrix.csv)。

分类含义：

- A：行为和依赖均可直接复制；本次没有足够证据把任何文件判为 A。
- B：复制后只需改 namespace、asmdef、日志路径、端口或少量依赖。
- C：只能复用设计/测试思路，DragonBound 需要重写规则或数据模型。
- D：包含 DragonBound 明令禁止的 BlackClock 业务概念，禁止迁移。

## 功能真实性核验

| 功能 | 源工程证据 | 结果 |
|---|---|---|
| 固定 RunSeed | `RunSeed.Create(int? requestedSeed)`、PCG32 `RunRandom`；`RunSeedAndRunRandomTests`、`RandomCallTelemetryTests` | 真实存在；EditMode 通过 |
| 本地日志和埋点 | `JsonlTelemetry`、`FileTelemetryLineWriter`、`TelemetryPathResolver`、JSONL schema；路径和失败隔离测试 | 真实存在；EditMode 通过。事件 schema 含 BlackClock 名称，不能整文件迁移 |
| EditMode/PlayMode 测试 | 两个测试 asmdef；源工程全量测试结果 EditMode 125/125、PlayMode 23/23 | 真实可运行 |
| 格子占用 | Workbench 12 槽和 Deployment 6 位有快照/占用事务；没有 DragonBound 6 战斗格 + 5 营格二维占用 | 部分存在；目标需重写 |
| 拖拽放置 | `IWorkbench.TryDrop`、`TrySwap`、`IDeployment.TryDeploy/TryMove` 是逻辑命令；无拖拽输入/高亮/回退 UI | 逻辑存在，交互缺失 |
| 相同单位合成 | `RecipeMergeResolver` 解析配置配方；一星到二星测试通过 | BlackClock 配方真实存在；DragonBound 同类型同等级规则缺失 |
| 配方合成 | 8 条 BlackClock 配方、验证器、原子 Workbench 事务，EditMode/PlayMode 测试通过 | 真实存在但业务绑定；复用算法思路 |
| 自动攻击 | Blackpowder/Miasma/Purifier/Warden 有 `TryAttack`/`TryCast`，AttackCooldown 可推进；没有统一自动攻击调度器 | 单位攻击逻辑真实；DragonBound 调度器缺失 |
| 圆形射程 | `CombatTargetEligibility` 用 lane 优先级和一维 `Math.Abs(position)`，不是二维欧氏距离 | 不存在 |
| 单体、范围、贯穿攻击 | Blackpowder 贯穿、Miasma 同 lane 区域选择、Purifier 单目标存在；均是医生职业专用 | 模式样例存在；通用 DragonBound 攻击模型缺失 |
| 眩晕状态 | 源 Runtime/Tests 无 `Stun`、`StatusEffect` 或状态接口 | 不存在 |
| 敌人路径移动 | `EnemyField.Advance` 沿三条 lane 线性移动，含漏怪和路障判断；EnemyFieldTests/PlayMode 通过 | BlackClock 线性路径真实；DragonBound 路径需重写 |
| 波次和 Boss | `NormalWaveCatalog` 强制 3 波；控制器有启动、生成、清波；Boss 只有接口/枚举和源工程其他提交记录，指定目录没有 Boss 运行时 | 三波真实；15 波/Boss 在审计范围内不完整 |
| 残怪继承 | `StartConfiguredWave` 要求 `enemyField.ActiveCount == 0`，清空后才可开始 | 不存在，且当前规则相反 |
| 胜负结算 | 只有 `RunPhase.Victory/Defeat`、`ReportFailure/ReportBossDefeated` 接口，没有运行时结算器 | 不存在 |

## 唯一迁移建议

### 复制到 GameShared（改名后复用）

只复制矩阵中 B 类的通用基础设施，优先包括：`RunSeed`/`RunRandom`/`RandomCallRecord`、`ITelemetry` 形状、JSONL line writer、路径解析的“项目日志/持久化目录”算法、Telemetry session 的序列化模式、`AttackCooldown`、确定性 ID/排序和纯快照/端口模式。复制后必须改 namespace、asmdef、日志目录和事件 schema，并移除所有 BlackClock 事件名。

### 复制后改造

Workbench 的原子事务和配方解析、Enemy 快照/事件、目标锁与确定性排序、Wave 定义的配置验证、Deployment 的“命令-快照-事件”模式可作为 C/B 混合参考。它们不能直接保留 12 槽、三路 24 格、医生职业、Lumen、路障或三波限制；先改成 DragonBound 数据模型再写实现。

### 重新实现

DragonBound 必须重新实现 Grid/占用/扩格、拖拽输入、招募双池与五连、基础单位 Lv1-Lv5、英雄组件与等级、二维圆形射程、单体/范围/贯穿/眩晕通用攻击、路径图、15 波/Boss/残怪继承、AI 双方独立状态和胜负结算。缺口明细见 [DragonBoundMissingModules.md](DragonBoundMissingModules.md)。

### DragonBound 第一批可执行开发任务

1. 在目标工程创建 `GameShared.Runtime`、`DragonBound.Runtime`、`DragonBound.Tests.EditMode`、`DragonBound.Tests.PlayMode` 四个 asmdef；只迁移 RunSeed/Telemetry 最小骨架并先写固定种子、JSONL、失败隔离测试。
2. 建立 DragonBound Core 的 `MatchState`、`TeamState`、可序列化 `RunSnapshot` 和胜负结算接口；先不接 UI 和美术。
3. 实现 6 战斗格 + 5 营格的二维 Grid/占用事务、扩格、拖拽命令和非法回退；为占用覆盖、跨区、稳定事件各写 EditMode 测试。
4. 实现基础单位配置、招募费用/双池/五连和同类同等级合成；固定 seed 重放必须覆盖招募序列和合成结果。
5. 实现单体/范围/贯穿/眩晕的通用攻击与圆形射程，再接自动攻击调度器；用纯 C# EditMode 测试验证范围边界和状态持续时间。
6. 实现敌人路径、残怪继承、3 波最小闭环和最小胜负结算；3 波闭环通过后再扩展到 15 波和 Boss，最后补 PlayMode 主场景烟测。

## 测试执行结果

执行 Unity：`F:\Unity\2022.3.62f3c1\Editor\Unity.exe`（版本 `2022.3.62f3c1`）。

| 阶段 | 命令结果 | 统计 |
|---|---|---|
| 编译/项目加载 | 成功 | 无编译错误 |
| EditMode | Passed | 125 total / 125 passed / 0 failed / 0 skipped |
| PlayMode | Passed | 23 total / 23 passed / 0 failed / 0 skipped（其中 `BlackClock.Alpha.Tests.PlayMode.dll` 5/5；项目既有 `ShopV5.PlayModeTests.dll` 18/18） |

测试结果文件保存在临时目录，仅作为本次审计证据；源工程未新增或修改玩法文件。EditMode 结果来自 `BlackClock.Alpha.Tests.EditMode.dll` 的 125 个测试。由于目标工程目前没有 DragonBound 测试程序集，不能声称目标工程已通过同等测试。
