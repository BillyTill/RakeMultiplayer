# Player model bake pipeline

The retail game only ships a **first-person** player prefab (camera + hands + weapons).
There is no third-person body, so remote players would be invisible.

The body (`casual12_m_highpoly`, 42-bone Bip01 rig) *does* exist inside the game asset
files, but the animations are **humanoid muscle curves** (classID 95, attribute 7..133).
Those cannot be decoded without Unity's humanoid retargeting, so they are baked offline
into a compact binary that the mod loads at runtime.

This is the "variant 2" route: **no Unity 5 install and no AssetBundle**, because a
Unity 6 AssetBundle cannot be loaded by the Unity 5.1.2f1 game.

## Output

`RakePlayerModel.bin` + `RakePlayerSkin.png`, placed next to `RakeMultiplayer.dll` in
`BepInEx/plugins`.

These files are **derived from retail game assets** and are intentionally *not* committed
to the repository (see `.gitignore`). Re-run the pipeline locally to regenerate them.

## Steps

### 1. Export the game assets

Use AssetRipper on the *retail* install (the one that contains the body) and export a
Unity project. Only a handful of files are needed:

| Asset | Source path |
| --- | --- |
| Body prefab (42 bones, 1 SkinnedMeshRenderer) | `Assets/GameObject/human 1.prefab` |
| Mesh + skin weights + bindposes | `Assets/Mesh/casual12_m_highpoly.asset` |
| Humanoid avatar | `Assets/Avatar/Main_charAvatar.asset` |
| Animation clips | `Assets/AnimationClip/{Idle_10,Walk_2,Run_0,Walk Left,Walk Right,RakeAttack_TPS}.anim` |
| Skin texture | `Assets/Texture2D/human map.png` |

Only humanoid clips (127 muscle curves, classID 95) are usable. The first-person camera
clips (`Walk.anim`, `Kick.anim`, ...) contain 1–2 object curves and are unrelated.

`Jump.anim` is humanoid but completely static (0° bone delta), so it is not used.

### 2. Extract the skeleton

```sh
python parse_skeleton.py
```

Parses `human 1.prefab` and writes `skeleton.json` (60 nodes, 42 bones, local TRS and
parent indices). The prefab root transform contains leftovers from the scene it was
authored in (position ~100, -3.6, -6.6, scale 0.82 and a ~30° yaw), so the root placement
is recomputed afterwards.

### 3. Bake with Unity

Create a Unity 6 project, copy the assets from step 1 into `Assets/RakeBake/`, copy
`BakePlayer.cs` into `Assets/RakeBake/Editor/`, then:

```sh
Unity.exe -batchmode -nographics -quit \
  -projectPath <project> \
  -executeMethod RakeBake.BakePlayer.Run \
  -logFile bake.log
```

The script builds the hierarchy, adds the `SkinnedMeshRenderer` and an `Animator` with
the humanoid avatar, samples every clip at 30 fps with
`AnimationMode.SampleAnimationClip`, strips root motion, and writes the binary plus a
report.

> Note: `Unity.exe` must be started with `Start-Process -Wait` from PowerShell. Using
> `&` returns immediately without running the editor.

### 4. Strip root motion, then compute placement and scale

```sh
python canonical_placement.py
```

**Root motion.** The humanoid clips carry baked-in forward translation on the pelvis
(Walk 1.56 m, Run 3.34 m, WalkLeft/Right ~3 m per cycle). Played back as a pure pose that
makes the body slide away from its owner. `BakePlayer.cs` removes the linear drift from
every animated node, keeping only the natural bob/sway, so the clips play in place.

**Placement and scale.** The baked root transform still carries the scene leftovers, so
this script derives the "right" axis from eight symmetric bone pairs (feet, toes, hands,
clavicles, upper arms, forearms, thighs, calves), yaws the model so it faces +Z, then
scales it so the animated Idle pose is 1.80 m tall (the retail player capsule is 1.8 m)
and computes the root position that puts the feet on y = 0.

The resulting constants are hard-coded in
`RemotePlayerModelData.RootPosition/RootRotation/RootScale`.

Expected verification output:

```
Idle       feet y=+0.000  top y=1.799  height=1.799
Walk       feet y=-0.038  top y=1.746  height=1.784
Run        feet y=-0.019  top y=1.704  height=1.723
```

### 5. Validate

```sh
python validate_bin.py
```

Round-trips the binary, checks the byte count, prints the hierarchy, the mesh bounds and
per-clip rotation deltas. A clip with a delta of 0° is static and useless.

## Binary format (version 1, little-endian)

```
char[4]  "RKPL"
u32      version = 1
u32      nodeCount, boneCount, animNodeCount, clipCount, vertexCount, indexCount
i32      smrNodeIndex, smrRootBoneIndex

node[nodeCount]:
  u16 len, utf8 name, i32 parent,
  f32 pos[3], f32 rot[4] (xyzw), f32 scale[3], u8 isBone

i32 boneNode[boneCount]
i32 animNode[animNodeCount]

f32 vertices[vertexCount*3]
f32 normals[vertexCount*3]
f32 uv[vertexCount*2]
f32 boneWeights[vertexCount*4]
u8  boneIndices[vertexCount*4]
f32 bindposes[boneCount*16]     (row major m00..m33)
u16 indices[indexCount]

clip[clipCount]:
  u16 len, utf8 name, f32 fps, u32 frameCount,
  frame[frameCount]: animNode[animNodeCount] { f32 pos[3], f32 rot[4] }
```

## Runtime side

`RemotePlayerModelData` parses the binary and builds the `Mesh`; `RemotePlayerModel`
builds the transform hierarchy and plays the clips procedurally with quaternion
interpolation. Clip selection uses movement speed plus the animation bitmask that
`NetworkedPlayer` already synchronises (bit0 = running); the playback rate is
`speed / reference` where the reference speeds (1.58 m/s walk, 5.66 m/s run) come from
the root motion that was baked into the clips, so the feet do not slide.

`RemotePlayerVisual` falls back to the old primitive proxy if the binary is missing.
