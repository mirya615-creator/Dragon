# Data Binding Contracts

Views accept only immutable snapshots from `DragonBound.HandoffUi`:

- `ItemHudSnapshot(State, Title, Detail, CooldownSeconds)`
- `MerchantSnapshot(Offers, Status)` where each offer is `MerchantOfferSnapshot(Id, Title, Detail, State)`

Views emit only `HandoffUiCommands.ItemRequested` and `HandoffUiCommands.MerchantOfferRequested(string)`. The preview uses `HandoffPreviewPresenter` with mock data; it is not an integration template for game state mutation.

Live adapter ownership belongs to the gameplay lead. It may observe the existing Item unlock state after a normally completed fifth run, availability/cooldown, and Merchant response states, then publish a fresh snapshot. It must process commands through existing rule services. Do not pass `TeamState`, `ItemProfile`, Gold, Energy, Ledger, `BasicUnitAiController`, `BoardGrid`, or `TwentyWavePressureRuntime` into these views. Do not wire this phase through Core or Bootstrap.

Dynamic Merchant cards are instantiated solely from the serialized `HandoffMerchantOffer` Prefab and its serialized `OfferContainer`; view code never constructs card hierarchy.
