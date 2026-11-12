import struct, math
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
i32(); i32()
nodes = []
for i in range(nodeCount):
    n = u16(); name = data[off:off+n].decode(); off += n
    parent = i32(); pos = f32s(3); rot = f32s(4); scale = f32s(3); bone = u8s(1)[0]
    nodes.append(dict(name=name, parent=parent, pos=pos, rot=rot, scale=scale, bone=bone))
boneNodes = [i32() for _ in range(boneCount)]
animNodes = [i32() for _ in range(animCount)]
off += vertCount*3*4 + vertCount*3*4 + vertCount*2*4 + vertCount*4*4 + vertCount*4
off += boneCount*16*4 + indexCount*2

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

ROOT_POS = [-0.02963, 0.00571, -0.12195]
ROOT_SCALE = 0.97728
name2i = {n['name']: i for i, n in enumerate(nodes)}
PELVIS = name2i["Bip01 Pelvis"]

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

def world_for(clip, frame):
    lp = [list(n['pos']) for n in nodes]; lr = [list(n['rot']) for n in nodes]; ls = [list(n['scale']) for n in nodes]
    if clip:
        fps, frames, pos, rot = clips[clip]
        f = frame % frames
        for k, ni in enumerate(animNodes):
            if ni == 0: continue
            lp[ni] = list(pos[f, k]); lr[ni] = list(rot[f, k])
    lp[0] = ROOT_POS; lr[0] = [0.0, 0.0, 0.0, 1.0]; ls[0] = [ROOT_SCALE]*3
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

def normalize_rot(M):
    R = M[:3, :3].copy()
    for c in range(3):
        n = np.linalg.norm(R[:, c])
        if n > 1e-9: R[:, c] /= n
    return R

Wb = world_for(None, 0)
Rb = normalize_rot(Wb[PELVIS])

print(f"{'clip':10s} {'pelvis yaw vs bind':>18s} {'correction':>11s}")
out = {}
for name in clips:
    fps, frames, pos, rot = clips[name]
    yaws = []
    for f in range(0, frames, max(1, frames // 8)):
        W = world_for(name, f)
        Rc = normalize_rot(W[PELVIS])
        D = Rc @ Rb.T
        yaws.append(math.degrees(math.atan2(D[0, 2], D[2, 2])))
    # unwrap-ish: take the median of the circular values
    yaws = np.array(yaws)
    med = float(np.median(yaws))
    spread = float(yaws.max() - yaws.min())
    corr = -med
    out[name] = corr
    print(f"{name:10s} {med:+18.2f} {corr:+11.2f}   (spread {spread:.1f})")

print()
print("C# per-clip yaw:")
for k, v in out.items():
    print(f'  "{k}" -> {v:.2f}f')
