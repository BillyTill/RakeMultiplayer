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

`RakePlayerModel.bin`, `RakePlayerRifle.bin` and the textures, placed next to
`RakeMultiplayer.dll` in `BepInEx/plugins`.

These files are **derived from retail game assets** and are intentionally *not* committed
to the repository (see `.gitignore`). Re-run the pipeline locally to regenerate them.

## Steps

### 1. Export the game assets

Use AssetRipper on the *retail* install (the one that contains the body) and export a
Unity project. Only a handful of files are needed:

| Asset | Source path |
| --- | --- |
| Player prefab (contains the body rig) | `Assets/GameObject/MP_HunterController.prefab` |
| Mesh + skin weights + bindposes | `Assets/Mesh/casual12_m_highpoly.asset` |
| Shotgun mesh | `Assets/Mesh/Rem870_3rd.asset` |
| Humanoid avatar | `Assets/Avatar/Main_charAvatar.asset` |
| Animation clips | `Assets/AnimationClip/{Idle_10,Walk_2,Run_0,Walk Left,Walk Right,RakeAttack_TPS,Take 001_15}.anim` |
| Skin texture | `Assets/Texture2D/human map.png` |
| Gun texture | `Assets/Texture2D/rifle.png` |

Only humanoid clips (127 muscle curves, classID 95) are usable. The first-person camera
clips (`Walk.anim`, `Kick.anim`, ...) contain 1–2 object curves and are unrelated.
`Jump.anim` and `Take 001_1.anim` are humanoid but completely static, so they are unused.

### 2. Extract the skeleton

```sh
python parse_skeleton.py
```

Walks the `Main_char` subtree of `MP_HunterController.prefab` and writes `skeleton.json`
(85 nodes, 42 bones, local TRS and parent indices, plus the rifle node index).

> **Do not use `human 1.prefab`.** Its rig looks similar but does **not** match the
> bindposes of `casual12_m_highpoly`. Measured with
> `max |boneWorld * bindpose - smrWorld|`: `human 1.prefab` gives **1.5**,
> `MP_HunterController.prefab` gives **0.0009**. Baking with the wrong rig produces a
> body rotated ~90° and ~15 % too short, because the avatar retargets against a skeleton
> it was not built for.

### 3. Bake with Unity

Create a Unity 6 project, copy the assets from step 1 into `Assets/RakeBake/`, copy
`BakePlayer.cs` and `RifleBake.cs` into `Assets/RakeBake/Editor/`, then:

```sh
Unity.exe -batchmode -nographics -quit \
  -projectPath <project> \
  -executeMethod RakeBake.BakePlayer.Run \
  -logFile bake.log
```

The script builds the hierarchy, adds the `SkinnedMeshRenderer` and an `Animator` with
the humanoid avatar, samples every clip at 30 fps with
`AnimationMode.SampleAnimationClip`, then post-processes the pelvis:

1. **Root motion** — the clips carry baked-in forward translation on the pelvis
   (Walk 1.56 m, Run 3.34 m, WalkLeft/Right ~3 m per cycle). The linear drift is removed
   so the clip plays in place.
2. **Re-basing** — detrending alone still leaves the offset that the root motion
   accumulated at frame 0 (Idle 2.4 m, Walk 1.3 m, Run 2.1 m away from the owner). Every
   animated node is shifted so its mean position equals its rest position, which centres
   all clips on the same origin. A no-op for nodes whose position is constant.

> Note: `Unity.exe` must be started with `Start-Process -Wait` from PowerShell. Using
> `&` returns immediately without running the editor.

### 4. Compute placement and scale

```sh
python canonical_placement.py
```

Reads the baked binary and prints the three constants for
`RemotePlayerModelData.RootPosition/RootRotation/RootScale`:

* the yaw needed to face +Z (derived from eight symmetric bone pairs: feet, toes, hands,
  clavicles, upper arms, forearms, thighs, calves — with the correct rig this comes out
  as exactly 0, so the rotation is identity);
* a uniform scale so the animated Idle pose is 1.80 m tall (the retail player capsule is
  1.8 m: `CharacterController.height = 1.8`, `centre = 0`, camera at `+0.8`);
* a root offset that puts the feet on y = 0, centred on the midpoint of both feet.

Expected verification output:

```
Idle       feet y=+0.000  top y=1.799  height=1.799
Walk       feet y=+0.028  top y=1.814  height=1.786
Run        feet y=+0.044  top y=1.768  height=1.724
RifleHold  feet y=+0.003  top y=1.800  height=1.797
```

### 5. Validate

```sh
python validate_bin.py
```

Round-trips the binary, checks the byte count, prints the hierarchy, the mesh bounds and
per-clip rotation deltas.

## Weapon

The body carries the shotgun as a plain `MeshRenderer` under
`Bip01 R Hand -> rifle -> Rem870_3rd` (scale 0.0124 * 1449.7 = 18.0 against a 6.3 cm
mesh, i.e. a 1.14 m weapon). `RifleBake.cs` exports it to `RakePlayerRifle.bin`
(1878 verts, 1 submesh, texture `rifle.png`).

`RemotePlayerWeaponData` rebuilds the mesh and adds `MeshFilter` + `MeshRenderer` to the
same node. No skinning is involved, so the weapon follows the hand automatically.

`Take 001_15` is baked as the `RifleHold` clip: a static pose (0° variation over 10 s)
that raises both arms up to 72.8° from rest. With the correct rig the gun then points
forward along +Z at chest height, i.e. it is the aiming pose. It plays when the synced
aiming bit is set and the player is standing still.

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

`RakePlayerRifle.bin` (`"RKRF"`, version 1): node index, vertex/submesh/index counts,
per-submesh index counts, vertices, normals, uvs, indices, then one texture file name per
submesh.

## Runtime side

`RemotePlayerModelData` parses the binary and builds the `Mesh`; `RemotePlayerModel`
builds the transform hierarchy and plays the clips procedurally with quaternion
interpolation. Clip selection uses movement speed plus the animation bitmask that
`NetworkedPlayer` already synchronises (bit0 = running, bit2 = aiming); the playback rate
is `speed / reference` where the reference speeds (1.58 m/s walk, 5.66 m/s run) come from
the root motion that was baked into the clips, so the feet do not slide.

`RemotePlayerVisual` falls back to the old primitive proxy if the binary is missing.
