# Forgekeeper's Gift 服务端发布改造方案

文档日期：2026-09-14  
客户端项目：DragonBound Unity  
目标服务端仓库：`Chalice/DragonBound-server`

## 1. 目标

玩家在本局装备 `ITEM_FORGEKEEPERS_GIFT` 后，每隔 90 秒出现一次激励广告选择。玩家完整观看广告后获得 1 个铲子；关闭、跳过或播放失败不发奖，但同样进入下一轮 90 秒冷却。

正式发布必须满足：

- 只有本局确实装备 Forgekeeper's Gift 的玩家可以创建领取机会。
- 冷却、机会序号、广告验证和发奖结果均由服务端控制。
- 客户端篡改时间、重进场景、重启应用、重复请求或伪造广告完成均不能提前或重复获得铲子。
- 网络中断后可以使用原 `claim_id` 和幂等键恢复同一结果。
- 服务端返回稳定的铲子运行时 ID，客户端重复收到响应时不得重复生成。

## 2. 与 VoidShovel 的规则区别

Forgekeeper's Gift 不得直接复用 VoidShovel 的每局一次规则。

| 项目 | Forgekeeper's Gift | VoidShovel |
| --- | --- | --- |
| 广告点 ID | `item_forgekeepers_gift` | `run_forge_pick` |
| 奖励 | `forge_pick × 1` | `forge_pick × 2` |
| 触发条件 | 本局已装备指定道具 | 游戏内固定按钮 |
| 次数限制 | Run 有效期间每 90 秒一次 | 每局最多一次 |
| 冷却 | 服务端控制 90 秒 | 无重复冷却 |

## 3. 冻结业务规则

| 项目 | 正式规则 |
| --- | --- |
| 道具 ID | `ITEM_FORGEKEEPERS_GIFT` |
| 广告点 ID | `item_forgekeepers_gift` |
| 奖励类型 | `forge_pick` |
| 单次奖励数量 | 1 |
| 首次触发 | Run 开始后 90 秒 |
| 后续触发 | 上一次机会创建后 90 秒 |
| 关闭、跳过、无填充、播放失败 | 不发奖，但进入下一轮冷却 |
| 完整观看 | 必须通过广告平台 SSV 后才能领取 |
| 重放成功请求 | 返回首次成功快照，`applied=false`、`replayed=true` |
| 客户端时间 | 不可信，不参与服务端冷却和限次判定 |

服务端不得接受客户端传入奖励类型、奖励数量、玩家 ID或铲子运行时 ID。

## 4. 推荐接口流程

采用“查询状态 → 创建本轮广告机会 → 广告平台验证 → 领取奖励”流程。

### 4.1 查询状态

```http
GET /v1/runs/{run_id}/item-rewards/forgekeepers-gift
Authorization: Bearer <access_token>
```

可用响应：

```json
{
  "run_id": "c777b86b-65c5-4c44-9a82-57dcacdf962c",
  "item_id": "ITEM_FORGEKEEPERS_GIFT",
  "equipped": true,
  "status": "available",
  "can_open": true,
  "opportunity_index": 2,
  "next_available_at": "2026-09-14T10:30:00Z",
  "remaining_seconds": 0
}
```

冷却中响应仍使用成功状态码，并返回：

```json
{
  "run_id": "c777b86b-65c5-4c44-9a82-57dcacdf962c",
  "item_id": "ITEM_FORGEKEEPERS_GIFT",
  "equipped": true,
  "status": "cooldown",
  "can_open": false,
  "opportunity_index": 2,
  "next_available_at": "2026-09-14T10:30:00Z",
  "remaining_seconds": 42
}
```

客户端在进入场景、恢复网络、切回前台或重新建立 Run 时调用该接口。

### 4.2 创建本轮广告机会

客户端应在准备打开 Forgekeeper's Gift 面板时创建机会，而不是等广告播放完成后才创建：

```http
POST /v1/runs/{run_id}/item-rewards/forgekeepers-gift/intents
Authorization: Bearer <access_token>
Idempotency-Key: run.<run_id>.forgekeepers-gift.intent.<client_event_id>
Content-Type: application/json
```

请求：

```json
{
  "ad_point_id": "item_forgekeepers_gift",
  "client_event_id": "3b2a3a71bf8d4c67a8474d4448df92cf",
  "platform": "android"
}
```

响应：

```json
{
  "claim_id": "0a18ebc0-857c-4c78-b03e-65263d1c7be5",
  "run_id": "c777b86b-65c5-4c44-9a82-57dcacdf962c",
  "item_id": "ITEM_FORGEKEEPERS_GIFT",
  "ad_point_id": "item_forgekeepers_gift",
  "opportunity_index": 2,
  "placement_id": "item_forgekeepers_gift",
  "ad_custom_data": "<服务端签名的短期数据>",
  "expires_at": "2026-09-14T10:22:00Z",
  "next_available_at": "2026-09-14T10:31:30Z"
}
```

创建成功时，服务端必须在同一事务中：

1. 锁定当前玩家、Run 和道具的冷却记录。
2. 校验当前时间已经达到 `next_available_at`。
3. 创建当前 `opportunity_index` 的唯一广告意图。
4. 将下一机会序号加一。
5. 将下一可用时间推进 90 秒。

这样即使玩家随后关闭面板、跳过广告或播放失败，也符合当前产品规则中的“每次决定后重新计算 90 秒”。

### 4.3 广告平台服务端验证

```http
POST /v1/ad-callbacks/{provider}/rewarded
```

该接口只供广告平台调用。服务端必须：

- 校验平台回调签名、证书或共享密钥。
- 校验应用包名、广告单元、平台和运行环境。
- 校验 `ad_custom_data` 的签名、有效期及其绑定的玩家、Run、机会序号和 `claim_id`。
- 确认事件为完整观看并且允许奖励。
- 对广告平台 transaction ID 做全局去重。
- 将对应意图从 `pending` 更新为 `verified`。

客户端上报的 `Completed` 只能触发领取查询，不能直接成为发奖凭据。

### 4.4 领取一个铲子

```http
POST /v1/runs/{run_id}/item-rewards/forgekeepers-gift/claim
Authorization: Bearer <access_token>
Idempotency-Key: run.<run_id>.forgekeepers-gift.claim.<claim_id>
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
  "opportunity_index": 2,
  "status": "granted",
  "reward_type": "forge_pick",
  "reward_count": 1,
  "unit_runtime_ids": [
    "run-reward:0a18ebc0-857c-4c78-b03e-65263d1c7be5:1"
  ],
  "applied": true,
  "replayed": false,
  "ledger_reference": "opaque-server-reference"
}
```

重复请求必须返回完全相同的 `unit_runtime_ids`，并设置：

```json
{
  "applied": false,
  "replayed": true
}
```

## 5. 数据库修改

可以复用 VoidShovel 使用的 `run_ad_reward_claims` 表，并增加：

```sql
ALTER TABLE run_ad_reward_claims
    ADD COLUMN item_id VARCHAR(64),
    ADD COLUMN opportunity_index INTEGER,
    ADD COLUMN next_available_at TIMESTAMPTZ;

CREATE UNIQUE INDEX run_item_ad_reward_opportunity_unique
    ON run_ad_reward_claims
       (player_id, run_id, ad_point_id, opportunity_index)
    WHERE opportunity_index IS NOT NULL;

CREATE UNIQUE INDEX run_item_ad_reward_transaction_unique
    ON run_ad_reward_claims (provider, provider_transaction_hash)
    WHERE provider_transaction_hash IS NOT NULL;
```

另建冷却计划表：

```sql
CREATE TABLE run_item_reward_schedules (
    player_id UUID NOT NULL,
    run_id UUID NOT NULL,
    item_id VARCHAR(64) NOT NULL,
    next_opportunity_index INTEGER NOT NULL DEFAULT 1,
    next_available_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (player_id, run_id, item_id),
    CONSTRAINT run_item_reward_opportunity_check
        CHECK (next_opportunity_index > 0)
);
```

Run 开始时，如果服务端确认玩家装备了该道具，则创建计划记录，初始 `next_available_at = run_started_at + 90 seconds`。

## 6. 服务端事务逻辑

### 创建机会

1. 从 Access Token 获取 `player_id`。
2. 校验 Run 存在、属于当前玩家并仍在进行。
3. 从服务端保存的本局道具快照确认已装备 `ITEM_FORGEKEEPERS_GIFT`。
4. `SELECT ... FOR UPDATE` 锁定冷却计划记录。
5. 使用数据库或服务端时间校验 `next_available_at`，不接受客户端时间。
6. 检查当前机会序号是否已有 intent；重复幂等请求返回原 intent。
7. 创建带签名和有效期的 `ad_custom_data`。
8. 原子推进下一机会序号和下一可用时间。
9. 提交后返回 intent。

### 领取奖励

1. 校验 `claim_id` 属于当前玩家、当前 Run、当前道具和当前广告点。
2. 校验对应广告意图状态已经由 SSV 更新为 `verified`。
3. 校验意图未过期，广告 transaction ID 未被其他奖励使用。
4. 锁定领取记录；若已经 `granted`，返回保存的首次奖励快照。
5. 从服务端配置读取固定奖励 `forge_pick × 1`。
6. 生成并保存稳定的单位运行时 ID。
7. 在同一事务中写入 Reward Ledger、授权单位记录和 `granted` 状态。
8. 提交后返回成功响应。

并发控制必须依赖数据库事务和唯一约束，不能只依赖 Go 进程内锁。

## 7. Run 快照与结算校验

Forgekeeper's Gift 的铲子属于局内单位。服务端成功发奖时，必须把返回的 `unit_runtime_id` 加入本局“允许出现的奖励单位”记录。

后续 Run 快照或结算校验需要：

- 接受已经在 Reward Ledger 中授权的铲子 ID。
- 拒绝客户端自行构造但未被服务端授权的奖励单位 ID。
- 同一单位 ID只能在当前 Run 中出现一次。
- 不允许将其他 Run 的奖励单位带入当前 Run。

## 8. 幂等规则

- intent 和 claim 均要求 `Idempotency-Key`。
- 同一幂等键、相同请求返回原响应。
- 同一幂等键、不同请求返回 `IDEMPOTENCY_CONFLICT`。
- 客户端超时后必须复用原 `claim_id` 和原幂等键。
- 每个 `player_id + run_id + ad_point_id + opportunity_index` 只能创建一个有效机会。
- 每个 `claim_id` 只能产生一个成功奖励快照。
- 广告平台 transaction ID 全局只能使用一次。

## 9. 错误码

| HTTP | code | 客户端处理 |
| ---: | --- | --- |
| 400 | `INVALID_ARGUMENT` | 不重试，记录 trace ID |
| 401 | `UNAUTHENTICATED` | 刷新登录态后复用原请求重试 |
| 403 | `RUN_NOT_OWNED` | 关闭面板，不发奖 |
| 403 | `ITEM_NOT_EQUIPPED` | 禁用本局道具入口 |
| 404 | `RUN_NOT_FOUND` | 关闭面板，不发奖 |
| 409 | `RUN_NOT_ACTIVE` | 禁用本局入口 |
| 409 | `IDEMPOTENCY_CONFLICT` | 不换键重试，记录异常 |
| 409 | `REWARD_NOT_READY` | 使用 `remaining_seconds` 恢复冷却 |
| 410 | `AD_INTENT_EXPIRED` | 本次不发奖，等待下一机会 |
| 422 | `AD_VERIFICATION_FAILED` | 提示验证失败，不发奖 |
| 425 | `AD_VERIFICATION_PENDING` | 使用原 claim 和幂等键退避重试 |
| 429 | `RATE_LIMITED` | 按 `Retry-After` 退避 |
| 503 | `REWARD_AUTHORITY_UNAVAILABLE` | 不本地发奖，保留待恢复记录 |

错误响应沿用项目统一 envelope，并返回 `trace_id`。不得在响应或普通日志中输出广告密钥、原始回调凭据和内部数据库信息。

## 10. 服务端配置

```yaml
ad_points:
  - ad_point_id: item_forgekeepers_gift
    enabled: true
    trigger_type: equipped_item_cooldown
    trigger_value: 90
    required_item_id: ITEM_FORGEKEEPERS_GIFT
    reward_type: forge_pick
    reward_value: 1
    per_run_limit: -1
    cooldown_seconds: 90
    server_verify_required: true
    content_version: DragonBound.Gameplay.v1
```

`per_run_limit: -1` 表示没有额外固定次数限制，实际次数自然受到 Run 有效期和 90 秒冷却限制。可额外配置仅用于异常保护的安全上限，但不得静默改变产品规则。

## 11. Go 服务端修改范围

1. `openapi.yaml`
   - 增加 Forgekeeper's Gift 的 status、intent 和 claim 接口。
   - 增加机会序号、冷却时间、广告自定义数据和奖励快照模型。
2. Migration/Repository
   - 扩展广告领取表。
   - 新增局内道具奖励冷却计划表。
   - 增加机会序号、transaction ID 和幂等唯一约束。
3. Run service
   - 在 Run 开始时保存服务端权威道具装备快照。
   - 为已装备该道具的 Run 初始化 90 秒计划。
4. Forgekeeper Gift domain/service
   - 实现状态查询、机会创建、冷却推进、SSV 状态检查和幂等发奖。
5. Ad provider adapter
   - 实现正式广告平台 SSV 签名校验。
   - 正式环境禁止使用客户端声明成功或永远成功的验证器。
6. Reward ledger/Run validator
   - 保存稳定铲子 ID。
   - 将其加入当前 Run 的授权单位集合。
7. `cmd/api/main.go`
   - 注入 Run repository、冷却 repository、广告验证器和 Reward Ledger。
8. Observability
   - 增加结构化日志、指标与 trace，不记录原始广告凭据。

## 12. 客户端联调要求

当前客户端 `ForgekeepersGiftPanelController` 仍通过本地 `Seed + rewardSequence` 生成铲子。服务端上线后必须同步修改为：

- 触发时间到达时先查询或创建服务端机会，再显示面板。
- 将服务端返回的 `placement_id` 和 `ad_custom_data` 传入正式广告 SDK。
- 广告完整播放后调用 claim。
- 只有收到 `status=granted` 且恰好返回一个有效 ID 时才生成铲子。
- 使用服务端 `unit_runtime_ids[0]`，删除客户端自建运行时 ID。
- `AD_VERIFICATION_PENDING` 使用原 claim 和原幂等键退避重试。
- 将未完成 claim 保存到本地，切回前台、重连或重启后恢复。
- 服务端已经 granted 但招募区暂时没有空位时保留待展示奖励，等待空位后生成，不重新申请机会。
- 关闭、跳过、失败均不发奖，并使用服务端 `next_available_at` 恢复下一轮冷却。

## 13. 测试清单

### 单元测试

- 未装备道具不能查询为可用或创建 intent。
- Run 开始 90 秒前不能创建机会。
- 首次及后续机会序号连续且唯一。
- 关闭、跳过和广告失败不发奖，但下一机会进入冷却。
- SSV 未到达、失败或过期均不发奖。
- SSV 成功后固定发放一个铲子。
- 重复 claim 返回同一个单位 ID。
- 相同幂等键配不同请求返回冲突。
- 其他玩家、其他 Run 或其他机会的 claim 不可复用。

### 并发与恢复测试

- 并发创建机会只产生一个当前机会并只推进一次冷却。
- 20 个并发 claim 最终只产生一个 Reward Ledger 记录。
- 服务端事务提交后响应丢失，客户端重试可恢复相同 ID。
- 重复或乱序的 SSV 回调不会重复发奖。
- 重启客户端后 status 能恢复冷却、验证中或已领取状态。

### 发布验收

- 真机装备 Forgekeeper's Gift 后，首次在 90 秒时出现面板。
- 完整观看后招募区只增加一个铲子。
- 跳过、关闭、无填充和断网均不发奖。
- 上一次机会创建后 90 秒前无法再次创建。
- 修改客户端时间、重载场景或修改本地冷却变量不能提前领取。
- 抓包重复 claim 不会生成第二个铲子。
- Run 结束后不能继续创建机会或领取新奖励。
- 正式环境 SSV 和监控运行正常，日志中没有原始广告凭据。

## 14. 监控指标

- `run_item_reward_intent_total{item_id,result}`
- `run_item_reward_claim_total{item_id,result}`
- `run_item_reward_verification_total{provider,result}`
- `run_item_reward_replay_total{item_id}`
- `run_item_reward_not_ready_total{item_id}`
- `run_item_reward_verification_latency_seconds`
- `run_item_reward_unique_conflict_total{item_id}`

如果客户端广告完成数量明显高于服务端 verified 数量，或同一机会的唯一冲突突然升高，应触发告警。

## 15. 完成定义

只有同时满足以下条件才可以正式发布：

1. OpenAPI、handler、domain service、repository、migration 和 SSV 验证器已经上线。
2. 冷却推进、机会创建和奖励发放具备数据库事务及唯一约束保护。
3. Run 开始时的道具装备状态由服务端保存并验证。
4. 客户端不再自行生成 Forgekeeper's Gift 奖励 ID。
5. 真机广告可以携带 `ad_custom_data` 并通过 SSV。
6. 并发、重试、断网、重连、场景重载和应用重启测试全部通过。
7. 服务端能够证明每个机会最多产生一个 `forge_pick` 奖励。
