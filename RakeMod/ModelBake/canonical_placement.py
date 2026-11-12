import struct, math
import numpy as np

BIN = r"C:\Users\KOMPUHTER\AppData\Local\Temp\opencode\RakePlayerModel.bin"
TARGET_HEIGHT = 1.80

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

name2i = {n['name']: i for i, n in enumerate(nodes)}
print("nodes", nodeCount, "bones", boneCount, "clips", list(clips.keys()))

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

def qmul(a, b):
    ax, ay, az, aw = a; bx, by, bz, bw = b
    return np.array([
        aw*bx + ax*bw + ay*bz - az*by,
        aw*by - ax*bz + ay*bw + az*bx,
        aw*bz + ax*by - ay*bx + az*bw,
        aw*bw - ax*bx - ay*by - az*bz,
    ])

def world_for(clip, frame, root_pos, root_rot, root_scale):
    lp = [list(n['pos']) for n in nodes]; lr = [list(n['rot']) for n in nodes]; ls = [list(n['scale']) for n in nodes]
    if clip:
        fps, frames, pos, rot = clips[clip]
        f = frame % frames
        for k, ni in enumerate(animNodes):
            if ni == 0: continue
            lp[ni] = list(pos[f, k]); lr[ni] = list(rot[f, k])
    lp[0] = list(root_pos); lr[0] = list(root_rot); ls[0] = [root_scale]*3
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

PAIRS = ["Foot", "Toe0", "Hand", "Clavicle", "UpperArm", "Forearm", "Thigh", "Calf"]

def yaw_of_right(W):
    ys = []
    for p in PAIRS:
        l = W[name2i["Bip01 L " + p]][:3, 3]
        r = W[name2i["Bip01 R " + p]][:3, 3]
        d = r - l; d[1] = 0
        if np.linalg.norm(d) < 1e-5: continue
        d /= np.linalg.norm(d)
        ys.append(math.degrees(math.atan2(d[2], d[0])))
    return float(np.mean(ys)), ys

ZERO = np.array([0.0, 0.0, 0.0])
IDENT = np.array([0.0, 0.0, 0.0, 1.0])

# 1) base orientation of the bind pose
W0 = world_for(None, 0, ZERO, IDENT, 1.0)
sk = skinned(W0)
print()
print("bind pose (root identity): bounds", np.round(sk.min(axis=0), 3), np.round(sk.max(axis=0), 3),
      "size", np.round(sk.max(axis=0)-sk.min(axis=0), 3))
yaw, ys = yaw_of_right(W0)
print("right-axis yaw per pair:", [round(y, 1) for y in ys], "mean", round(yaw, 2))

corr = yaw
hy = math.radians(corr) / 2.0
newR0 = np.array([0.0, math.sin(hy), 0.0, math.cos(hy)])

W1 = world_for(None, 0, ZERO, newR0, 1.0)
sk1 = skinned(W1)
print("after yaw fix: bounds", np.round(sk1.min(axis=0), 3), np.round(sk1.max(axis=0), 3),
      "size", np.round(sk1.max(axis=0)-sk1.min(axis=0), 3))
yaw2, _ = yaw_of_right(W1)
print("right-axis yaw after fix:", round(yaw2, 3), "(should be ~0)")

lf = W1[name2i["Bip01 L Foot"]][:3, 3]; lt = W1[name2i["Bip01 L Toe0"]][:3, 3]
rf = W1[name2i["Bip01 R Foot"]][:3, 3]; rt = W1[name2i["Bip01 R Toe0"]][:3, 3]
print("toe L:", np.round(lt-lf, 3), " toe R:", np.round(rt-rf, 3), "(+Z expected)")
print("head:", np.round(W1[name2i["Bip01 Head"]][:3, 3], 3),
      "pelvis:", np.round(W1[name2i["Bip01 Pelvis"]][:3, 3], 3))

# 2) height of the Idle pose with scale 1
def bounds_over(clip, root_pos, root_rot, root_scale, step=1):
    mn = np.array([1e9]*3); mx = np.array([-1e9]*3)
    frames = clips[clip][1]
    for f in range(0, frames, step):
        s = skinned(world_for(clip, f, root_pos, root_rot, root_scale))
        mn = np.minimum(mn, s.min(axis=0)); mx = np.maximum(mx, s.max(axis=0))
    return mn, mx

idle_clip = "Idle" if "Idle" in clips else list(clips.keys())[0]
mn, mx = bounds_over(idle_clip, ZERO, newR0, 1.0, 8)
h = mx[1] - mn[1]
print()
print(f"{idle_clip} height at scale 1: {h:.4f}")
k = TARGET_HEIGHT / h
print(f"scale k = {k:.5f}")

mn, mx = bounds_over(idle_clip, ZERO, newR0, k, 8)
fl = np.zeros(3); fr = np.zeros(3); n = 0
frames = clips[idle_clip][1]
for f in range(0, frames, 8):
    W = world_for(idle_clip, f, ZERO, newR0, k)
    fl += W[name2i["Bip01 L Foot"]][:3, 3]
    fr += W[name2i["Bip01 R Foot"]][:3, 3]
    n += 1
mid = (fl + fr) / (2.0 * n)
root_pos = np.array([-mid[0], -mn[1], -mid[2]])
print("foot midpoint:", np.round(mid, 4))
print("ROOT_POS:", np.round(root_pos, 5))

print()
print("=== verification ===")
for nm in clips:
    mn, mx = bounds_over(nm, root_pos, newR0, k, max(1, clips[nm][1] // 12))
    print(f"  {nm:10s} feet y={mn[1]:+.3f}  top y={mx[1]:.3f}  height={mx[1]-mn[1]:.3f}")

print()
print("C# constants:")
print(f"  RootPosition = new Vector3({root_pos[0]:.5f}f, {root_pos[1]:.5f}f, {root_pos[2]:.5f}f);")
print(f"  RootRotation = new Quaternion({newR0[0]:.6f}f, {newR0[1]:.6f}f, {newR0[2]:.6f}f, {newR0[3]:.6f}f);")
print(f"  RootScale    = new Vector3({k:.6f}f, {k:.6f}f, {k:.6f}f);")
