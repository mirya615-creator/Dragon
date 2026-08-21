# Known Issues

- `UI_Handoff` is not in Build Settings by design: Project Settings are frozen for this phase. Open it in the Unity Editor for review and use the targeted PlayMode test to validate its asset path.
- Screenshot capture is an Editor visual artifact, not device hardware validation. Device teams must validate actual cutouts and 16:9 through 20:9 devices before release.
- Merchant selection applies the required visual disablement immediately, but actual claim, ad completion, expiry, and error recovery remain mock presentation states.
- The fifth-run rule is displayed only. This phase does not calculate or change the unlock rule.
- Existing Greybox UI still contains runtime-created hierarchy and legacy `UnityEngine.UI.Text`; its replacement is intentionally deferred.
