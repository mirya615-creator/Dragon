# Unity 客户端 P0：随机流与 Stage 契约

本次 P0 仅落地 Unity 客户端，不包含服务端代码、数据库迁移或部署。

## 已冻结的客户端契约

- 随机协议版本：`DragonBound.Random.v1`
- 随机算法：`pcg32-xsh-rr-v1`
- 子流派生：对 UTF-8 流名称执行 FNV-1a 32 位，再与 `runSeed` 的 32 位补码异或。
- 线上与本地网关使用完全相同的派生方式。
- 所有二进制整数采用大端序；二进制结果通过无填充 Base64URL 放入 JSON 字符串。

### 通用头

| 偏移 | 长度 | 值 |
|---|---:|---|
| 0 | 4 | ASCII `DBRS` |
| 4 | 1 | Schema 版本 `1` |
| 5 | 1 | 类型：`1` 招募、`2` 波次、`3` 暴击 |

### `recruit_random`

通用头后依次为 `player.recruit` 与 `ai.recruit` 的两个 int32 种子，共 14 字节。seed=73 的黄金值为：

```text
Base64URL: REJSUwEBsHdKQSeJFRY
player.recruit: -1334359487
ai.recruit: 663295254
```

### `wave_rng`

通用头后为一个 int32 波次根种子，共 10 字节。seed=73 的黄金值为 `REJSUwECAAAASQ`。

### `critical_random_sequence`

通用头后为 uint32 样本数，再按顺序写入每个 uint32 原始样本。当前战斗没有暴击判定，因此 Finish 发送“合法空序列”`REJSUwEDAAAAAA`，不再发送含义不明确的空字符串。

## Stage 客户端发布清单

客户端发布清单位于 `Assets/Resources/Configuration/Stages/stage-001.json`，内容与 `PressureRaceGreyboxV2` 的 20 波运行时配置逐项校验。当前规范化 JSON 的 SHA-256 为：

```text
a555903b1f717480e79dc29c4a28c8ec09bd18fc4316a6328cfc2e672b6d05eb
```

在线 Run 开始时，若启用 `requireAuthoritativeRunContract`，客户端要求：

1. `rng_version` 等于 `DragonBound.Random.v1`；
2. `recruit_random` 与 `wave_rng` 存在且能严格解码；
3. `stage_snapshot_digest` 与客户端发布清单摘要一致。

4. 解码出的招募种子及波次根种子必须能由 `run_seed` 精确复算。

任一条件不满足都会中止进入对局，并给出稳定错误码；校验失败前不会写入本地 Active Run，避免错误 Run 阻塞下一次启动。

## 客户端验证

Unity 菜单执行 `DragonBound > Validation > Validate Production Run Contract`（快捷键 Ctrl+Shift+P），可检查 Stage 清单、摘要、20 波配置及三种随机载荷的往返编码。EditMode 的 `RunRandomTests` 固定验证 PCG32、子流派生和二进制编码黄金向量。
