# Art Asset Contract

The first slice intentionally uses neutral dark panels and TMP labels. It is not a visual direction for the shipped game.

`HandoffItemHudView` Inspector hooks: `Icon Sprite`, `Icon Material`, `Animator Controller`, `Select Sfx`, `Use Vfx Prefab`.

`HandoffMerchantOfferView` Inspector hooks: `Item Sprite`, `Card Material`, `Animator Controller`, `Claim Vfx Prefab`, `Select Sfx`.

The art lead may assign these assets directly to the `HandoffMerchantOffer` Prefab and to the `ItemHud` object nested in `UI_HandoffScreen`. New text must remain English and use TMP font assets. Layout uses the 1080x1920 Canvas reference, Safe Area fitting, and capped responsive content; fixed-format content is capped rather than stretched on tablets.
