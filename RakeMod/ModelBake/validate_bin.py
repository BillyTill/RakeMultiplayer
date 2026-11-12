import struct, os, re, json

BIN = r"C:\Users\KOMPUHTER\AppData\Local\Temp\opencode\RakePlayerModel.bin"
data = open(BIN, "rb").read()
off = 0

def u8():
    global off
    v = data[off]; off += 1; return v
def u16():
    global off
    v = struct.unpack_from("<H", data, off)[0]; off += 2; return v
def u32():
    global off
    v = struct.unpack_from("<I", data, off)[0]; off += 4; return v
def i32():
    global off
    v = struct.unpack_from("<i", data, off)[0]; off += 4; return v
def f32():
    global off
    v = struct.unpack_from("<f", data, off)[0]; off += 4; return v
def f32s(n):
    global off
    v = list(struct.unpack_from("<%df" % n, data, off)); off += 4 * n; return v
def u8s(n):
    global off
    v = list(data[off:off+n]); off += n; return v

magic = data[:4]
off = 4
ver = u32()
nodeCount = u32(); boneCount = u32(); animCount = u32(); clipCount = u32()
vertCount = u32(); indexCount = u32()
smrNode = i32(); smrRootBone = i32()
print("magic", magic, "ver", ver)
print("nodes", nodeCount, "bones", boneCount, "animNodes", animCount,
      "clips", clipCount, "verts", vertCount, "indices", indexCount,
      "smrNode", smrNode, "smrRootBone", smrRootBone)

nodes = []
for i in range(nodeCount):
    n = u16()
    name = data[off:off+n].decode("utf-8"); off += n
    parent = i32()
    pos = f32s(3); rot = f32s(4); scale = f32s(3)
    isbone = u8()
    nodes.append(dict(i=i, name=name, parent=parent, pos=pos, rot=rot, scale=scale, bone=isbone))

boneNodes = [i32() for _ in range(boneCount)]
animNodes = [i32() for _ in range(animCount)]

verts = [f32s(3) for _ in range(vertCount)]
norms = [f32s(3) for _ in range(vertCount)]
uvs = [f32s(2) for _ in range(vertCount)]
wts = [f32s(4) for _ in range(vertCount)]
bidx = [u8s(4) for _ in range(vertCount)]
bindposes = [f32s(16) for _ in range(boneCount)]
indices = list(struct.unpack_from("<%dH" % indexCount, data, off)); off += 2 * indexCount

print("after mesh, offset", off, "of", len(data))
xs = [v[0] for v in verts]; ys = [v[1] for v in verts]; zs = [v[2] for v in verts]
print("mesh bounds x", round(min(xs),3), round(max(xs),3),
      "y", round(min(ys),3), round(max(ys),3),
      "z", round(min(zs),3), round(max(zs),3))
print("max index", max(indices), "vertCount", vertCount)
badw = sum(1 for w in wts if abs(sum(w) - 1.0) > 0.02)
print("verts with non-normalized weights:", badw)

print("hierarchy:")
for n in nodes:
    print(f"  {n['i']:2d} {n['name']:28s} parent={n['parent']:3d} bone={n['bone']}")

print("boneNodes:", boneNodes[:8], "...")
print("animNodes:", animNodes[:8], "...")

for c in range(clipCount):
    n = u16()
    name = data[off:off+n].decode("utf-8"); off += n
    fps = f32()
    frames = u32()
    frames_data = []
    for f in range(frames):
        row = []
        for k in range(animCount):
            row.append((f32s(3), f32s(4)))
        frames_data.append(row)
    # variation check
    maxang = 0.0
    import math
    for k in range(animCount):
        q0 = frames_data[0][k][1]
        for f in range(0, frames, max(1, frames // 12)):
            q = frames_data[f][k][1]
            dot = abs(sum(a*b for a, b in zip(q0, q)))
            dot = max(-1.0, min(1.0, dot))
            ang = 2 * math.degrees(math.acos(dot))
            maxang = max(maxang, ang)
    print(f"clip {name:10s} fps={fps} frames={frames} maxRotDelta={maxang:.1f}deg "
          f"root0pos={tuple(round(x,3) for x in frames_data[0][0][0])}")

print("final offset", off, "file", len(data), "OK" if off == len(data) else "MISMATCH")

# find material texture
matdir = r"D:\Rake\RipMP\ExportedProject\Assets\Material"
target = "85fe652e5777ad94cb93f1267730c0b2"
found = None
for root, dirs, files in os.walk(matdir):
    for f in files:
        if f.endswith(".meta"):
            p = os.path.join(root, f)
            if target in open(p, encoding="utf-8", errors="ignore").read():
                found = p[:-5]
print("material file:", found)
if found:
    txt = open(found, encoding="utf-8", errors="ignore").read()
    print(txt[:2500])
