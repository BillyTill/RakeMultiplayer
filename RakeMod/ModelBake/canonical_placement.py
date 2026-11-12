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

def qmat(q):
    x, y, z, w = q
    n = math.sqrt(x*x+y*y+z*z+w*w) or 1.0
    x, y, z, w = x/n, y/n, z/n, w/n
    return np.array([
        [1-2*(y*y+z*z), 2*(x*y - z*w),   2*(x*z + y*w)],
        [2*(x*y + z*w), 1-2*(x*x+z*z),   2*(y*z - x*w)],
        [2*(x*z - y*w), 2*(y*z + x*w),   1-2*(x*x+y*y)],
    ])

def trs(pos, rot, scale):
    M = np.eye(4)
    M[:3, :3] = qmat(rot) @ np.diag(scale)
    M[:3, 3] = pos
    return M

def world_of(rootM):
    W = [None]*nodeCount
    W[0] = rootM
    def gw(i):
        if W[i] is not None: return W[i]
        p = nodes[i]['parent']
        M = trs(nodes[i]['pos'], nodes[i]['rot'], nodes[i]['scale'])
        if p >= 0: M = gw(p) @ M
        W[i] = M
        return M
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

R0 = list(nodes[0]['rot']); S0 = list(nodes[0]['scale'])
name2i = {n['name']: i for i, n in enumerate(nodes)}

# 1) current orientation with root pos 0
W = world_of(trs([0, 0, 0], R0, S0))
pairs = ["Foot", "Toe0", "Hand", "Clavicle", "UpperArm", "Forearm", "Thigh", "Calf"]
yaws = []
for p in pairs:
    d = W[name2i["Bip01 R " + p]][:3, 3] - W[name2i["Bip01 L " + p]][:3, 3]
    d = d.copy(); d[1] = 0
    if np.linalg.norm(d) < 1e-4: continue
    d /= np.linalg.norm(d)
    yaws.append(math.degrees(math.atan2(d[2], d[0])))
yaw_right = float(np.mean(yaws))
print("right-axis yaw:", round(yaw_right, 3), "deg  -> correction", round(-yaw_right, 3))

def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return np.array([
        aw*bx + ax*bw + ay*bz - az*by,
        aw*by - ax*bz + ay*bw + az*bx,
        aw*bz + ax*by - ay*bx + az*bw,
        aw*bw - ax*bx - ay*by - az*bz,
    ])

hy = math.radians(yaw_right) / 2.0
Ry = np.array([0.0, math.sin(hy), 0.0, math.cos(hy)])
newR0 = qmul(Ry, np.array(R0))
newR0 = newR0 / np.linalg.norm(newR0)

W2 = world_of(trs([0, 0, 0], newR0, S0))
sk = skinned(W2)
mn = sk.min(axis=0); mx = sk.max(axis=0)
print("bounds (yaw fixed, pos 0):", np.round(mn, 4), np.round(mx, 4), "size", np.round(mx-mn, 4))
canon = [float(-(mn[0]+mx[0])/2), float(-mn[1]), float(-(mn[2]+mx[2])/2)]

W3 = world_of(trs(canon, newR0, S0))
sk3 = skinned(W3)
mn3 = sk3.min(axis=0); mx3 = sk3.max(axis=0)
print("FINAL bounds:", np.round(mn3, 4), np.round(mx3, 4), "size", np.round(mx3-mn3, 4))

def bp2(nm):
    return W3[name2i[nm]][:3, 3]
for p in pairs:
    d = bp2("Bip01 R " + p) - bp2("Bip01 L " + p)
    print(f"  {p:9s} R-L = {np.round(d,3)}")
print("toe L:", np.round(bp2("Bip01 L Toe0") - bp2("Bip01 L Foot"), 3))
print("toe R:", np.round(bp2("Bip01 R Toe0") - bp2("Bip01 R Foot"), 3))
print("head:", np.round(bp2("Bip01 Head"), 3), "pelvis:", np.round(bp2("Bip01 Pelvis"), 3))
print()
print("ROOT_POS   =", [round(float(v), 5) for v in canon])
print("ROOT_ROT   =", [round(float(v), 6) for v in newR0])
print("ROOT_SCALE =", [round(float(v), 6) for v in S0])
