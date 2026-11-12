import struct, math, sys
import numpy as np

BIN = r"C:\Users\KOMPUHTER\AppData\Local\Temp\opencode\RakePlayerModel.bin"
data = open(BIN, "rb").read()
off = 4

def u32():
    global off
    v = struct.unpack_from("<I", data, off)[0]; off += 4; return v
def i32():
    global off
    v = struct.unpack_from("<i", data, off)[0]; off += 4; return v
def u16():
    global off
    v = struct.unpack_from("<H", data, off)[0]; off += 2; return v
def f32s(n):
    global off
    v = list(struct.unpack_from("<%df" % n, data, off)); off += 4 * n; return v
def u8s(n):
    global off
    v = list(data[off:off+n]); off += n; return v

u32()
nodeCount = u32(); boneCount = u32(); animCount = u32(); clipCount = u32()
vertCount = u32(); indexCount = u32()
smrNode = i32(); smrRootBone = i32()
nodes = []
for i in range(nodeCount):
    n = u16(); name = data[off:off+n].decode(); off += n
    parent = i32(); pos = f32s(3); rot = f32s(4); scale = f32s(3); bone = u8s(1)[0]
    nodes.append(dict(name=name, parent=parent, pos=pos, rot=rot, scale=scale, bone=bone))
boneNodes = [i32() for _ in range(boneCount)]
animNodes = [i32() for _ in range(animCount)]
verts = np.array([f32s(3) for _ in range(vertCount)], dtype=np.float64)
f32s(vertCount*3); f32s(vertCount*2)
wts = np.array([f32s(4) for _ in range(vertCount)], dtype=np.float64)
bidx = np.array([u8s(4) for _ in range(vertCount)], dtype=np.int32)
bindposes = np.array([f32s(16) for _ in range(boneCount)], dtype=np.float64).reshape(boneCount, 4, 4)
off += indexCount*2

clips = {}
for c in range(clipCount):
    n = u16(); name = data[off:off+n].decode(); off += n
    fps = struct.unpack_from("<f", data, off)[0]; off += 4
    frames = u32()
    pos = np.zeros((frames, animCount, 3)); rot = np.zeros((frames, animCount, 4))
    for f in range(frames):
        for k in range(animCount):
            pos[f, k] = f32s(3); rot[f, k] = f32s(4)
    clips[name] = (fps, frames, pos, rot)

# root motion should be gone now
pk = animNodes.index(3)
print("=== root motion check (pelvis drift per clip) ===")
for nm, (fps, frames, pos, rot) in clips.items():
    d = pos[-1, pk] - pos[0, pk]
    print(f"  {nm:10s} drift={np.round(d,4)} |{np.linalg.norm(d):.4f}|")

ROOT_ROT = np.array([-0.709104, 0.003993, 0.004015, 0.705081])
BASE_SCALE = 0.822672
name2i = {n['name']: i for i, n in enumerate(nodes)}

def qmat(q):
    x, y, z, w = q
    n = math.sqrt(x*x+y*y+z*z+w*w) or 1.0
    x, y, z, w = x/n, y/n, z/n, w/n
    return np.array([
        [1-2*(y*y+z*z), 2*(x*y - z*w),   2*(x*z + y*w)],
        [2*(x*y + z*w), 1-2*(x*x+z*z),   2*(y*z - x*w)],
        [2*(x*z - y*w), 2*(y*z + x*w),   1-2*(x*x+y*y)],
    ])

def trs(p, r, s):
    M = np.eye(4); M[:3, :3] = qmat(r) @ np.diag(s); M[:3, 3] = p
    return M

def world_for(clip_name, frame, root_pos, root_scale):
    lp = [list(nd['pos']) for nd in nodes]
    lr = [list(nd['rot']) for nd in nodes]
    ls = [list(nd['scale']) for nd in nodes]
    if clip_name:
        fps, frames, pos, rot = clips[clip_name]
        f = frame % frames
        for k, ni in enumerate(animNodes):
            if ni == 0: continue
            lp[ni] = list(pos[f, k]); lr[ni] = list(rot[f, k])
    lp[0] = list(root_pos); lr[0] = list(ROOT_ROT); ls[0] = [root_scale]*3
    W = [None]*nodeCount
    W[0] = trs(lp[0], lr[0], ls[0])
    def gw(i):
        if W[i] is not None: return W[i]
        p = nodes[i]['parent']
        M = trs(lp[i], lr[i], ls[i])
        if p >= 0: M = gw(p) @ M
        W[i] = M; return M
    for i in range(nodeCount): gw(i)
    return W

def skinned(W):
    sk = np.zeros((vertCount, 3))
    for b in range(boneCount):
        bw = W[boneNodes[b]] @ bindposes[b]
        m = bw[:3, :3]; t = bw[:3, 3]
        for k in range(4):
            w = wts[:, k]
            sel = (bidx[:, k] == b) & (w > 0)
            if not sel.any(): continue
            sk[sel] += (verts[sel] @ m.T + t) * w[sel][:, None]
    return sk

ZERO = np.array([0.0, 0.0, 0.0])

def bounds_over(clip_name, root_pos, root_scale, step=1):
    mn = np.array([1e9]*3); mx = np.array([-1e9]*3)
    frames = clips[clip_name][1]
    for f in range(0, frames, step):
        sk = skinned(world_for(clip_name, f, root_pos, root_scale))
        mn = np.minimum(mn, sk.min(axis=0))
        mx = np.maximum(mx, sk.max(axis=0))
    return mn, mx

# 1) height of the Idle pose with the current base scale
mn, mx = bounds_over("Idle", ZERO, BASE_SCALE, 8)
h = mx[1] - mn[1]
print()
print(f"Idle height at base scale: {h:.4f}  (min y {mn[1]:.4f}, max y {mx[1]:.4f})")

TARGET = 1.80
k = TARGET / h
scale = BASE_SCALE * k
print(f"scale factor k = {k:.5f} -> ROOT_SCALE = {scale:.6f}")

# 2) recompute root position: feet on 0, centred on the idle foot midpoint
mn, mx = bounds_over("Idle", ZERO, scale, 8)
foot_l = np.zeros(3); foot_r = np.zeros(3); n = 0
fps, frames, pos, rot = clips["Idle"]
for f in range(0, frames, 8):
    W = world_for("Idle", f, ZERO, scale)
    foot_l += W[name2i["Bip01 L Foot"]][:3, 3]
    foot_r += W[name2i["Bip01 R Foot"]][:3, 3]
    n += 1
mid = (foot_l + foot_r) / (2.0 * n)
root_pos = np.array([-mid[0], -mn[1], -mid[2]])
print("idle foot midpoint:", np.round(mid, 4))
print(f"ROOT_POS = [{root_pos[0]:.5f}, {root_pos[1]:.5f}, {root_pos[2]:.5f}]")

# 3) verify every clip
print()
print("=== verification (feet should be ~0, height ~1.7-1.8) ===")
for nm in clips:
    mn, mx = bounds_over(nm, root_pos, scale, max(1, clips[nm][1] // 12))
    print(f"  {nm:10s} feet y={mn[1]:+.3f}  top y={mx[1]:.3f}  height={mx[1]-mn[1]:.3f}")

print()
print("C# constants:")
print(f"  RootPosition = new Vector3({root_pos[0]:.5f}f, {root_pos[1]:.5f}f, {root_pos[2]:.5f}f);")
print(f"  RootRotation = new Quaternion({ROOT_ROT[0]:.6f}f, {ROOT_ROT[1]:.6f}f, {ROOT_ROT[2]:.6f}f, {ROOT_ROT[3]:.6f}f);")
print(f"  RootScale    = new Vector3({scale:.6f}f, {scale:.6f}f, {scale:.6f}f);")
