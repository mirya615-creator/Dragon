# VoidShovel 每局一次奖励：服务端发布改造方案

文档日期：2026-09-14  
客户端项目：DragonBound Unity  
目标服务端仓库：`Chalice/DragonBound-server`

## 1. 目标

玩家在游戏场景点击 `ART_ScreenBackground/VoidShovel`，完整观看一次激励广告后获得两个铲子，并满足以下发布要求：

- 每个玩家、每局游戏最多成功领取一次。
- 只有服务端确认广告有效后才能发奖。
- 断网重试、超时重试和重复点击不得重复发奖。
- 客户端重进场景、重启应用或修改本地变量不能重置领取次数。
- 服务端返回的两个单位运行时 ID 必须稳定，客户端重复收到同一结果时不得重复生成铲子。

本文中的“铲子”沿用客户端显示名称；服务端稳定奖励类型统一使用 `forge_pick`。

## 2. 当前客户端状态与发布风险

当前实现位于：

`Assets/Scripts/UI/VoidShovelRewardController.cs`

现状：

- 广告位 ID 为 `void_shovel_double`。
- `rewardClaimed` 只保存在当前场景组件内。
- 广告完成后，客户端直接向招募区加入两个铲子。
- 运行时 ID 由客户端使用 `GameplayRunId` 拼接生成。
- 服务端未记录本局是否已经领取，也未验证广告事件。

因此当前实现不能用于正式发布。重新加载场景、篡改客户端、伪造广告完成结果或并发请求都可能绕过“每局一次”。

## 3. 冻结业务规则

| 项目 | 正式规则 |
| --- | --- |
| 服务端广告点 ID | `run_forge_pick` |
| 客户端旧 ID | `void_shovel_double`，仅开发期兼容，不作为正式权威 ID |
| 奖励 | `forge_pick × 2` |
| 限制 | 每个 `player_id + run_id` 最多成功领取一次 |
| 可领取阶段 | Run 已开始、属于当前玩家、尚未结算或终止 |
| 广告要求 | 必须通过广告平台服务端验证 |
| 失败/跳过/无填充 | 不发奖、不消耗本局次数 |
| 成功后重复请求 | 返回第一次成功结果，`applied=false`、`replayed=true` |
| 客户端时间 | 不可信，不参与限次判断 |

服务端不得接受客户端传入奖励数量、奖励类型或单位 ID。奖励固定由服务端配置解析。

## 4. 推荐接口流程

采用“创建广告意图 → 广告平台回调验证 → 领取奖励”的三段流程。不要只相信客户端上报 `Completed`。

### 4.1 创建广告意图

```http
POST /v1/runs/{run_id}/ad-rewards/forge-pick/intents
Authorization: Bearer <access_token>
Idempotency-Key: run.<run_id>.forge-pick.intent.<request-id>
Content-Type: application/json
```

请求：

```json
{
  "ad_point_id": "run_forge_pick",
  "client_event_id": "3b2a3a71bf8d4c67a8474d4448df92cf",
  "platform": "android"
}
```

响应：

```json
{
  "claim_id": "0a18ebc0-857c-4c78-b03e-65263d1c7be5",
  "run_id": "c777b86b-65c5-4c44-9a82-57dcacdf962c",
  "ad_point_id": "run_forge_pick",
  "placement_id": "run_forge_pick",
  "ad_custom_data": "<服务端签名的短期数据>",
  "expires_at": "2026-09-14T10:10:00Z",
  "already_claimed": false
}
```

要求：

- 从 Access Token 获取玩家身份，忽略请求体中的任何玩家 ID。
- 校验 Run 存在、属于当前玩家且状态允许领取。
- 若本局已经成功领取，返回 `409 RUN_REWARD_ALREADY_CLAIMED`。
- `ad_custom_data` 必须绑定 `claim_id`、`player_id`、`run_id`、`ad_point_id` 和过期时间，并由服务端签名。
- 同一幂等键重复调用必须返回相同 `claim_id`。

### 4.2 广告平台服务端验证回调

```http
POST /v1/ad-callbacks/{provider}/rewarded
```

该接口供广告平台调用，不使用玩家 Access Token。必须按具体广告平台规范执行：

- 验证回调签名、证书或共享密钥。
- 验证广告单元、应用包名、平台和环境。
- 解析并校验 `ad_custom_data`。
- 检查广告事件确实为完整观看并可奖励。
- 对广告平台 transaction ID 做全局去重。
- 将 `claim_id` 状态更新为 `verified`。
- 原始广告凭证禁止写普通日志；数据库仅保存必要字段或不可逆摘要。

无效签名、未知广告位、过期意图、重复 transaction ID 均不得进入可发奖状态。

### 4.3 领取两个铲子

```http
POST /v1/runs/{run_id}/ad-rewards/forge-pick/claim
Authorization: Bearer <access_token>
Idempotency-Key: run.<run_id>.forge-pick.claim.<claim-id>
Content-Type: application/json
```

请求：

```json
{
  "claim_id": "0a18ebc0-857c-4c78-b03e-65263d1c7be5"
}
```

首次成功响应：

```json
{
  "claim_id": "0a18ebc0-857c-4c78-b03e-65263d1c7be5",
  "run_id": "c777b86b-65c5-4c44-9a82-57dcacdf962c",
  "status": "granted",
  "reward_type": "forge_pick",
  "reward_count": 2,
  "unit_runtime_ids": [
    "run-reward:0a18ebc0-857c-4c78-b03e-65263d1c7be5:1",
    "run-reward:0a18ebc0-857c-4c78-b03e-65263d1c7be5:2"
  ],
  "applied": true,
  "replayed": false,
  "ledger_reference": "opaque-server-reference"
}
```

同一成功请求重放响应：

```json
{
  "claim_id": "0a18ebc0-857c-4c78-b03e-65263d1c7be5",
  "run_id": "c777b86b-65c5-4c44-9a82-57dcacdf962c",
  "status": "granted",
  "reward_type": "forge_pick",
  "reward_count": 2,
  "unit_runtime_ids": [
    "run-reward:0a18ebc0-857c-4c78-b03e-65263d1c7be5:1",
    "run-reward:0a18ebc0-857c-4c78-b03e-65263d1c7be5:2"
  ],
  "applied": false,
  "replayed": true,
  "ledger_reference": "opaque-server-reference"
}
```

注意：上方重放示例中的第二个 `unit_runtime_ids` 必须与首次响应完全一致。实现时应从已保存的奖励快照返回，不得重新生成。

### 4.4 查询本局领取状态

用于重连、场景重建或领取响应丢失后的恢复：

```http
GET /v1/runs/{run_id}/ad-rewards/forge-pick
Authorization: Bearer <access_token>
```

未领取：

```json
{
  "run_id": "c777b86b-65c5-4c44-9a82-57dcacdf962c",
  "status": "available",
  "can_claim": true,
  "reward_count": 2
}
```

已领取时返回完整的已保存奖励快照，并设置 `can_claim=false`。

## 5. 数据库修改

以下为 PostgreSQL 示例；实际文件名按服务端 migration 框架调整。

```sql
CREATE TABLE run_ad_reward_claims (
    claim_id UUID PRIMARY KEY,
    player_id UUID NOT NULL,
    run_id UUID NOT NULL,
    ad_point_id VARCHAR(64) NOT NULL,
    status VARCHAR(24) NOT NULL,
    client_event_id VARCHAR(128) NOT NULL,
    provider VARCHAR(32),
    provider_transaction_hash VARCHAR(128),
    intent_idempotency_hash VARCHAR(128) NOT NULL,
    claim_idempotency_hash VARCHAR(128),
    intent_request_hash VARCHAR(128) NOT NULL,
    claim_request_hash VARCHAR(128),
    reward_type VARCHAR(32),
    reward_count INTEGER,
    reward_snapshot JSONB,
    ledger_reference VARCHAR(128),
    expires_at TIMESTAMPTZ NOT NULL,
    verified_at TIMESTAMPTZ,
    granted_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT run_ad_reward_status_check
        CHECK (status IN ('pending', 'verified', 'granted', 'rejected', 'expired')),
    CONSTRAINT run_ad_reward_count_check
        CHECK (reward_count IS NULL OR reward_count > 0)
);

CREATE UNIQUE INDEX run_ad_reward_one_grant_per_run
    ON run_ad_reward_claims (player_id, run_id, ad_point_id)
    WHERE status = 'granted';

CREATE UNIQUE INDEX run_ad_reward_provider_transaction_unique
    ON run_ad_reward_claims (provider, provider_transaction_hash)
    WHERE provider_transaction_hash IS NOT NULL;

CREATE UNIQUE INDEX run_ad_reward_intent_idempotency_unique
    ON run_ad_reward_claims (player_id, intent_idempotency_hash);
```

如果现有服务端已提供统一 Ledger/幂等表，应复用统一设施，不重复建设新的幂等系统。必须保证“写入 granted + 写入奖励快照 + 写入 Ledger”处于同一数据库事务中。

## 6. 服务端事务逻辑

领取接口必须按以下顺序执行：

1. 从 Access Token 解析 `player_id`。
2. 校验 `run_id` 格式并查询 Run。
3. 校验 Run 属于当前玩家且未结算、未取消、未过期。
4. 校验 `claim_id` 属于同一玩家、同一 Run 和 `run_forge_pick`。
5. 校验广告意图状态为 `verified` 且未过期。
6. 读取并锁定本局奖励记录。
7. 若已经 `granted`，返回保存的奖励快照，标记为重放。
8. 从服务端配置读取 `forge_pick × 2`，不使用客户端数量。
9. 生成并保存两个稳定的 `unit_runtime_ids`。
10. 原子写入 Ledger 和 `granted` 状态。
11. 提交事务后返回成功响应。

并发请求必须依赖数据库唯一约束和事务锁，不可只使用 Go 进程内互斥锁。

## 7. 幂等规则

- 所有写接口要求 `Idempotency-Key`，长度建议 16～160 字符。
- 幂等键按“玩家 + 接口操作”隔离。
- 保存请求体规范化摘要；同一幂等键配不同请求体返回 `409 IDEMPOTENCY_CONFLICT`。
- 相同请求重试返回原响应快照，不再次验证广告、不重新生成 ID、不再次写 Ledger。
- 客户端超时后必须复用原 `claim_id` 和原幂等键，不得自动换键。
- 即使客户端换了幂等键，`player_id + run_id + ad_point_id` 唯一约束仍必须阻止第二次发奖。

## 8. 错误码

| HTTP | code | 客户端处理 |
| ---: | --- | --- |
| 400 | `INVALID_ARGUMENT` | 不重试，记录 trace ID |
| 401 | `UNAUTHENTICATED` | 刷新登录态后重试原请求 |
| 403 | `RUN_NOT_OWNED` | 禁用按钮，不发奖 |
| 404 | `RUN_NOT_FOUND` | 禁用按钮，不发奖 |
| 409 | `RUN_REWARD_ALREADY_CLAIMED` | 查询状态并恢复第一次结果 |
| 409 | `IDEMPOTENCY_CONFLICT` | 不换键重试，记录异常 |
| 409 | `RUN_NOT_ACTIVE` | 禁用按钮，不发奖 |
| 425 | `AD_VERIFICATION_PENDING` | 短暂退避后使用原请求重试 |
| 422 | `AD_VERIFICATION_FAILED` | 提示广告验证失败，不发奖 |
| 410 | `AD_INTENT_EXPIRED` | 本次不发奖；允许按产品策略重新看广告 |
| 429 | `RATE_LIMITED` | 按 `Retry-After` 退避 |
| 503 | `REWARD_AUTHORITY_UNAVAILABLE` | 保持未领取状态，使用原请求重试 |

所有错误响应沿用项目统一 envelope，并返回 `trace_id`。错误中不得回传广告密钥、原始平台 token 或内部数据库信息。

## 9. 服务端配置

在各环境广告奖励配置中增加：

```yaml
ad_points:
  - ad_point_id: run_forge_pick
    enabled: true
    trigger_type: in_run_button
    trigger_value: 0
    reward_type: forge_pick
    reward_value: 2
    daily_limit: -1
    per_run_limit: 1
    cooldown_seconds: 0
    server_verify_required: true
    content_version: "DragonBound.Gameplay.v1"
```

发布环境必须关闭“客户端声明广告成功即发奖”的开发兼容模式。`void_shovel_double` 可以在一个版本内映射到 `run_forge_pick` 作为迁移别名，但服务端响应和数据落库统一使用 `run_forge_pick`。

## 10. Go 服务端建议修改清单

服务端具体目录以现有模块边界为准，至少需要以下修改：

1. `openapi.yaml`
   - 增加 intent、claim、status 三个玩家接口。
   - 增加广告平台 SSV 回调接口。
   - 增加请求、成功响应、重放响应和统一错误模型。
2. `cmd/api/main.go`
   - 注入 Run Repository、广告验证器、奖励 Ledger 和 Claim Repository。
   - 正式环境禁止注入本地/永远成功的广告验证器。
3. Run reward domain/service
   - 实现 Run 所有权与状态校验。
   - 实现每局一次、配置解析、稳定奖励快照和幂等重放。
4. Repository/migration
   - 新增 `run_ad_reward_claims`，或扩展现有广告奖励 Ledger。
   - 添加局内奖励唯一约束和广告 transaction 去重约束。
5. Ad provider adapter
   - 按生产广告平台实现 SSV 签名校验。
   - 区分 development/staging/production 的应用和广告单元配置。
6. Observability
   - 增加结构化日志、指标和 trace，但不得记录原始广告凭证。

## 11. 客户端后续对接要求

服务端完成后，Unity 客户端需要同步：

- 将正式广告点改为 `run_forge_pick`。
- 扩展生产广告 SDK 适配器，使其能够接收服务端 `ad_custom_data`。
- 点击前请求 intent；广告完整播放后调用 claim。
- 只有收到 `status=granted` 才生成两个铲子。
- 使用服务端返回的 `unit_runtime_ids`，不再由客户端自行拼接。
- 场景初始化时调用 status；已领取则保持按钮禁用。
- `AD_VERIFICATION_PENDING` 使用原 `claim_id` 和原幂等键退避重试。
- 网络结果不确定时不得本地先发奖。
- 客户端仍负责广告期间暂停、两个招募空位预检、按钮视觉和招募区刷新。

建议客户端在开始广告前锁定两个空位；广告期间游戏已暂停，因此正常情况下空位不会发生变化。若服务端已成功而客户端展示失败，应通过 status 恢复同一奖励，而不是再次申请。

## 12. 测试清单

### 单元测试

- 有效广告首次领取返回两个稳定 ID。
- 相同幂等键重复请求返回相同快照且不重复写 Ledger。
- 更换幂等键重复领取仍被每局唯一约束拦截。
- 同一幂等键配不同请求体返回冲突。
- 广告 pending、failed、expired 均不发奖。
- Run 不存在、不属于玩家、已结算或已取消均不发奖。
- 客户端伪造数量、奖励类型或玩家 ID 无效。
- 同一广告 transaction 不能用于另一个玩家或另一局。

### 并发与集成测试

- 20 个并发 claim 请求最终只产生一条 granted 和一次 Ledger 发放。
- 服务端在事务提交后、响应前断开，重试可恢复同一结果。
- SSV 回调重复或乱序到达不会重复发奖。
- status 在重连后返回完整奖励快照。
- Run 结束与 claim 并发时结果确定且不会在结束后越权发奖。

### 发布验收

- Android 真机完成广告后得到两个铲子。
- 跳过、关闭、无填充、断网均不发奖。
- 重进 `Greybox_Main` 后按钮仍不可点击。
- 重启应用恢复同一 Run 时仍不可再次领取。
- 抓包重复调用或修改客户端 `rewardClaimed` 不会获得第二次奖励。
- 服务端日志与监控中不存在原始广告 token、签名或玩家敏感信息。

## 13. 监控指标

建议增加：

- `run_ad_reward_intent_total{ad_point_id,result}`
- `run_ad_reward_verification_total{provider,result}`
- `run_ad_reward_claim_total{ad_point_id,result}`
- `run_ad_reward_replay_total{ad_point_id}`
- `run_ad_reward_verification_latency_seconds`
- `run_ad_reward_claim_latency_seconds`
- `run_ad_reward_unique_conflict_total{ad_point_id}`

若出现“客户端广告 completed 数量明显大于服务端 verified 数量”或“同一 Run 唯一冲突突然升高”，应触发告警。

## 14. 完成定义

只有同时满足以下条件才可标记完成：

1. OpenAPI、handler、service、repository、migration 和生产广告验证器均已上线。
2. 发奖与每局唯一约束在数据库事务内完成。
3. 客户端不再根据本地 `rewardClaimed` 自行判定权威结果。
4. 真机广告 SSV 验证通过。
5. 并发、重试、重连、场景重载和应用重启测试全部通过。
6. 服务端明确证明同一 `player_id + run_id` 最多存在一次 `run_forge_pick` 成功发放。
