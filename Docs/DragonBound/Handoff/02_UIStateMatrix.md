# UI State Matrix

| Surface | State | Required presentation | Command availability |
|---|---|---|---|
| Item HUD | Locked | `LOCKED | FIRST 4 RUNS` | Disabled |
| Item HUD | UnlockNotice | `UNLOCKED AFTER RUN 5` | Disabled |
| Item HUD | Empty | `NO ITEM EQUIPPED` | Disabled |
| Item HUD | Available | `AVAILABLE` | `ItemRequested` |
| Item HUD | Selected | `SELECTED` | `ItemRequested` |
| Item HUD | Cooldown | `COOLDOWN` | Disabled |
| Item HUD | Disabled | `DISABLED` | Disabled |
| Merchant offer | Normal | `SELECT` | `MerchantOfferRequested(id)` |
| Merchant offer | Ad | `WATCH AD` | `MerchantOfferRequested(id)` |
| Merchant offer | Unavailable | `UNAVAILABLE` | Disabled |
| Merchant offer | Claimed | `CLAIMED` | Disabled |
| Merchant offer | Expired | `EXPIRED` | Disabled |
| Merchant offer | Loading | `LOADING` | Disabled |
| Merchant offer | Error | `RETRY LATER` | Disabled |

A Merchant snapshot normally contains exactly three distinct IDs. At most one may be `Ad`. Selecting one in the view disables the other two visually; the game layer remains authoritative for the next snapshot and any actual claim.
