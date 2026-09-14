

## Codely Structured Memories

### User

### Feedback
- [2026-09-10 15:52:55] 用户要求"输出方案"时：只交付方案并等待明确的实施指令（用户原话"暂时不实施，等我发实施指令"），不要顺手实现。**Why:** 用户希望先评审方案再决定动代码。**How to apply:** 所有"方案/设计/分析"类请求，输出后停在方案态，除非用户随后发出实施命令。
- [2026-09-10 15:53:02] UI动效偏好（升级标签案例）：对目标对象本体做整体动画（上浮缩小淡出→弹回+文字切换新等级），不要生成幽灵/克隆副本对象；动效时长与既有动画对齐（如LevelUp VFX≈0.833s=clipLength÷状态speed0.1），且必须沿用既有字体样式不引入新字体。**Why:** 用户明确否决了ghost克隆方案并要求"英雄升级动画和字体效果匹配"。**How to apply:** 本项目做UI文字/标签类特效时，默认真实对象自驱补间（AddComponent自驱组件+authored状态精确还原+OnDisable自清），时长派生自对应既有动画。

### Project
- [2026-09-10 15:52:49] 已知未修复问题（截至2026-09-07）：Main场景MerchantPanel"两次对局后自动弹出"仅在Local后端可用；当前ClientServiceConfig.asset backendMode=1（GoUnary），GoUnaryGameplayRunGateway硬编码 CountCompletedRun=false（L273/L397）导致 GameSettlementCoordinator 永不调用 Merchant.RecordCompletedRunAsync → MerchantPresentationStore.MarkPending 从不执行 → MainMerchantController.Start 的 TryConsumePending 恒false，面板从不自动弹出。修复需客户端补桥接 + Go服务器在 /v1/shop/merchant 实现2局门槛（GoMerchantGateway.RecordCompletedRunAsync 只查询不计数，Applied=false）。**Why:** 切GoUnary后端时漏掉商贩展示桥接。**How to apply:** 用户要求实现/修复此功能时直接按此根因链入手，勿重新全量排查。
### Reference

