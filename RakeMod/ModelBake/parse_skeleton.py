import re, json, os

PREFAB = r"D:\Rake\RipMP\ExportedProject\Assets\GameObject\human 1.prefab"
MESH = r"D:\Rake\RipMP\ExportedProject\Assets\Mesh\casual12_m_highpoly.asset"
OUT = r"C:\Users\KOMPUHTER\AppData\Local\Temp\opencode\skeleton.json"

txt = open(PREFAB, encoding="utf-8", errors="ignore").read()

# split into documents: --- !u!<class> &<fileID>
docs = {}
for m in re.finditer(r"^--- !u!(\d+) &(-?\d+)\s*$", txt, re.M):
    cls = int(m.group(1))
    fid = int(m.group(2))
    start = m.end()
    nxt = re.search(r"^--- !u!", txt[start:], re.M)
    end = start + nxt.start() if nxt else len(txt)
    docs[fid] = (cls, txt[start:end])

def field(block, name):
    m = re.search(r"^\s{2}" + re.escape(name) + r":\s*(.*)$", block, re.M)
    return m.group(1).strip() if m else None

def vec3(block, name):
    v = field(block, name)
    if not v:
        return [0.0, 0.0, 0.0]
    m = re.match(r"\{x:\s*([-\d.eE+]+),\s*y:\s*([-\d.eE+]+),\s*z:\s*([-\d.eE+]+)\}", v)
    return [float(m.group(1)), float(m.group(2)), float(m.group(3))] if m else [0.0, 0.0, 0.0]

def quat(block, name):
    v = field(block, name)
    if not v:
        return [0.0, 0.0, 0.0, 1.0]
    m = re.match(r"\{x:\s*([-\d.eE+]+),\s*y:\s*([-\d.eE+]+),\s*z:\s*([-\d.eE+]+),\s*w:\s*([-\d.eE+]+)\}", v)
    return [float(m.group(i)) for i in range(1, 5)] if m else [0.0, 0.0, 0.0, 1.0]

def fref(block, name):
    v = field(block, name)
    if not v:
        return 0
    m = re.search(r"fileID:\s*(-?\d+)", v)
    return int(m.group(1)) if m else 0

# GameObject names
go_name = {}
for fid, (cls, block) in docs.items():
    if cls == 1:
        n = field(block, "m_Name")
        go_name[fid] = n if n is not None else ""

# Transforms
transforms = {}
for fid, (cls, block) in docs.items():
    if cls in (4, 224):  # Transform / RectTransform
        go = fref(block, "m_GameObject")
        father = fref(block, "m_Father")
        transforms[fid] = {
            "id": fid,
            "go": go,
            "name": go_name.get(go, ""),
            "father": father,
            "pos": vec3(block, "m_LocalPosition"),
            "rot": quat(block, "m_LocalRotation"),
            "scale": vec3(block, "m_LocalScale"),
            "order": int(field(block, "m_RootOrder") or 0),
        }

# SMR
smr = None
for fid, (cls, block) in docs.items():
    if cls == 137:
        bones = []
        bm = re.search(r"^\s{2}m_Bones:\s*$", block, re.M)
        if bm:
            rest = block[bm.end():]
            for line in rest.splitlines():
                s = line.strip()
                if s.startswith("- {fileID:"):
                    bones.append(int(re.search(r"fileID:\s*(-?\d+)", s).group(1)))
                elif s and not s.startswith("-"):
                    break
        smr = {
            "id": fid,
            "go": fref(block, "m_GameObject"),
            "rootBone": fref(block, "m_RootBone"),
            "bones": bones,
        }

print("transforms:", len(transforms), "smr bones:", len(smr["bones"]) if smr else 0)

# Build ordered list: root first, then bones in SMR order, then remaining
order = []
seen = set()

def add_bone(bid):
    if bid in seen or bid not in transforms:
        return
    seen.add(bid)
    order.append(bid)

# root of the prefab = transform with father 0
roots = [t["id"] for t in transforms.values() if t["father"] == 0]
print("roots:", [(r, transforms[r]["name"]) for r in roots])

# depth-first from root for stable hierarchy
def walk(bid, depth=0):
    if bid not in transforms or bid in seen:
        return
    seen.add(bid)
    order.append(bid)
    kids = sorted([t["id"] for t in transforms.values() if t["father"] == bid],
                  key=lambda x: transforms[x]["order"])
    for k in kids:
        walk(k, depth + 1)

for r in roots:
    walk(r)

for b in smr["bones"]:
    add_bone(b)

idx = {tid: i for i, tid in enumerate(order)}
skeleton = []
for tid in order:
    t = transforms[tid]
    father = t["father"]
    skeleton.append({
        "name": t["name"],
        "parent": idx.get(father, -1),
        "pos": t["pos"],
        "rot": t["rot"],
        "scale": t["scale"],
        "bone": tid in set(smr["bones"]),
    })

bone_map = [idx.get(b, -1) for b in smr["bones"]]

data = {
    "root_index": 0,
    "nodes": skeleton,
    "boneNodes": bone_map,
    "smrRootBone": idx.get(smr["rootBone"], -1),
}
json.dump(data, open(OUT, "w", encoding="utf-8"), indent=1)
print("nodes:", len(skeleton), "boneNodes:", len(bone_map), "unresolved:", sum(1 for b in bone_map if b < 0))
print("smrRootBone:", idx.get(smr["rootBone"]), transforms.get(smr["rootBone"], {}).get("name"))
print("names:", [s["name"] for s in skeleton])
print("wrote", OUT)
