# Drakeforge Analytics Release Acceptance V1

状态：C16 客户端启动链路与 Firebase 真机验收清单  
适用包名：`com.drakeforge.mergedefense`  
SDK 基线：Firebase Unity SDK `13.14.0`

## 1. 发布前自动检查

在 Unity 执行：

`Tools > DragonBound > Analytics > Validate Android Release Readiness`

必须满足：

- Android 包名与 `google-services.json` 包名一致。
- Minimum API Level 不低于 23，Release 使用 IL2CPP 并包含 ARM64。
- Android 与 Unity Editor 生成的 Firebase 配置属于同一 Project ID。
- Firebase Project ID 与三方批准的正式项目一致。
- Build Settings 至少有一个启用场景，首场景包含 `FirebaseAnalyticsBootstrap`。
- Build Lane 只能是 `development`、`qa`、`staging` 或 `production`。
- Analytics 默认保持 `Unknown` 同意状态；明确同意前不记录、不缓存、不发送。

当前正式配置：首场景为 `Assets/Scenes/Login.unity`，Bootstrap 已挂载；Android 和桌面配置、发布批准值均为 `drakeforge`。

## 2. 自动化回归

Unity Test Runner 选择 EditMode，搜索并执行：

- `AnalyticsReleaseReadinessValidatorV2Tests`
- `AnalyticsRunSessionV2Tests`（包含 Firebase Sink、同意门控、缓冲与故障注入）
- `DrakeforgeAnalyticsAdapterV1Tests`

通过标准：目标测试全部通过，Console 无新增编译错误。发布候选版本还需执行完整 EditMode 和关键 PlayMode 回归。

## 3. Android DebugView 验收

1. 使用 `development` 或 `qa` Lane 构建并安装测试包，禁止使用 production 数据做调试演练。
2. 在测试设备开启该包的 Firebase Analytics 调试模式。
3. 首次启动时保持 Analytics 同意状态为 `Unknown`，确认 DebugView 没有自定义事件。
4. 明确授权后开始一局，完成招募、成型、波次推进、道具或符文操作，并正常或失败结束。
5. 在 DebugView 按时间顺序核对公共字段、参数类型、`run_id` 和连续 `sequence`。
6. 撤回授权，再执行玩法操作并切换前后台，确认不再出现新事件。
7. 关闭设备调试模式，避免后续测试持续进入 DebugView。

最小事件链：

`run_start → formation_snapshot → wave_start → wave_end → run_end`

按实际操作追加验证：

- 招募与成型：`recruit_result`、`hero_formed`、`formation_snapshot`
- 敌人与 Boss：`enemy_spawn`、`last_hit`、`boss_spawn`、`boss_skill`、`boss_kill/boss_goal`
- 道具与符文：`item_*`、`rune_*`
- 权威响应：`energy_*`、`settlement_gold`、`merchant_*`、`rank_*`、`ledger_result`

## 4. 故障与隐私验收

| 场景 | 通过标准 |
| --- | --- |
| Firebase 初始化延迟 | 玩法不阻塞；授权事件按顺序暂存并在可用后发送 |
| Firebase 持续失败 | 单事件最多尝试 3 次；队列最多 256 条；不产生无限重试 |
| 队列满 | 丢弃新事件并增加计数；不影响主线程玩法 |
| 切后台/退出 | 执行 Flush；不改变事件事实或重复创建终局 |
| 同意 Unknown/Denied | 不记录、不缓存、不发送 |
| 撤回同意 | 清空待发送队列，之后停止采集 |
| 敏感字段 | 不含姓名、邮箱、令牌、原始账号或原始交易号 |
| 服务端交易引用 | 只允许 `sha256:` 哈希或批准的稳定非敏感字段 |

## 5. 三方对账

同一测试局必须保存以下证据：

- 客户端：版本、Build Lane、设备、测试时间、`run_id`、DebugView 截图或事件导出。
- 服务端：相同 `run_id`/`operation_id` 的请求结果、幂等状态和账本结果。
- Firebase：Project ID、应用包名、事件顺序、关键参数类型和数据流目标。

对账通过标准：客户端事实、服务端权威结果和 Firebase 接收事件可用 `run_id`/`operation_id` 一一对应；拒绝、重复和失败原因使用三方冻结的稳定原因码。

## 6. 发布 Gate

- Schema：未知事件、字段和原因码为 0。
- Lifecycle：`run_start/run_end` 完整率达到要求，波次起止可配对。
- Sequence：单局序号连续且不同 Adapter 不冲突。
- Authority：资产、结算、交易和排行只记录服务端权威结果。
- Privacy：未同意和撤回同意场景均无采集；无 PII。
- Reliability：Firebase 故障不影响玩法，队列和重试有界。
- Build：Android Release IL2CPP/ARM64 构建与真机启动成功。
- Quality：灰度期终局完整率不低于 99.5%，无效率低于 0.1%，且无 Lane 污染。

只有自动检查、目标回归、DebugView、三方对账和灰度质量 Gate 全部有证据时，才可将埋点闭环标记为正式完成。

## 7. 验收记录模板

| 项目 | 记录 |
| --- | --- |
| 验收日期/负责人 |  |
| App 版本/Version Code |  |
| Build Lane |  |
| Firebase Project ID |  |
| 包名/设备/Android 版本 |  |
| 测试 `run_id` |  |
| 自动检查结果 |  |
| EditMode/PlayMode 结果 |  |
| DebugView 证据位置 |  |
| 服务端对账证据位置 |  |
| 隐私与故障测试结果 |  |
| 未解决问题/风险 |  |
| 最终结论与三方签字 |  |
