# Spine 3.8.75 Unity runtime: centered-root build

Each character folder contains `Name.json`, `Name.atlas.txt`, and `Name.png`.

The root bone stays at `(0, 0)`. Its direct child bones have been translated by a constant amount so the setup-pose image bounds are centered on the root. All Walk animation timelines, attachment transforms, bone hierarchy, atlas regions, and PNG bytes are unchanged. This moves the character relative to its Unity GameObject pivot without changing the motion.

Import a character folder into Unity. For a centered Canvas placement, select the `SkeletonGraphic` GameObject, keep its anchors at the center, and set RectTransform `Pos X` and `Pos Y` to `0` relative to a centered parent. The screenshot supplied for AxeMinion showed `Pos X = 1011`, which independently offsets the GameObject.

Validation: all eight JSON files report Spine 3.8.75, all setup-pose image bounds center at `(0, 0)`, all original animation objects match byte-for-byte after JSON parsing, and all eight Walk animations render through the official Spine 3.8 runtime. Unity scene placement has not been tested on the user's active scene.
