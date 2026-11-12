import re, json, os, glob

PREFAB = r"D:\Rake\RipMP\ExportedProject\Assets\GameObject\MP_HunterController.prefab"
OUT = r"C:\Users\KOMPUHTER\AppData\Local\Temp\opencode\skeleton.json"
BODY_ROOT = "Main_char"

txt = open(PREFAB, encoding="utf-8", errors="ignore").read()

docs = {}
for m in re.finditer(r"^--- !u!(\d+) &(-?\d+)\s*$", txt, re.M):
    cls = int(m.group(1)); fid = int(m.group(2))
    start = m.end()
    nxt = re.search(r"^--- !u!", txt[start:], re.M)
    end = start + nxt.start() if nxt else len(txt)
    docs[fid] = (cls, txt[start:end])

def field(b, name):
    m = re.search(r"^\s{2}" + re.escape(name) + r":\s*(.*)$", b, re.M)
    return m.group(1).strip() if m else None

def fref(b, name):
    v = field(b, name)
    if not v: return 0
    m = re.search(r"fileID:\s*(-?\d+)", v)
    return int(m.group(1)) if m else 0

def vec(b, name, n=3):
    v = field(b, name)
    if not v: return [0.0]*n
    nums = re.findall(r"([-\d.eE+]+)", v)
    return [float(x) for x in nums[:n]]

def refs_list(b, name):
    out = []
    m = re.search(r"^\s{2}" + re.escape(name) + r":\s*$", b, re.M)
    if not m: return out
    for line in b[m.end():].splitlines():
        s = line.strip()
        if s.startswith("- {fileID:"):
            out.append(int(re.search(r"fileID:\s*(-?\d+)", s).group(1)))
        elif s and not s.startswith("-"):
            break
    return out

def guid_of(b, name):
    v = field(b, name) or ""
    m = re.search(r"guid:\s*([0-9a-f]+)", v)
    return m.group(1) if m else None

go_name = {fid: (field(b, "m_Name") or "") for fid, (c, b) in docs.items() if c == 1}
tr = {}
for fid, (c, b) in docs.items():
    if c in (4, 224):
        tr[fid] = dict(go=fref(b, "m_GameObject"), father=fref(b, "m_Father"),
                       pos=vec(b, "m_LocalPosition"), rot=vec(b, "m_LocalRotation", 4),
                       scale=vec(b, "m_LocalScale"), order=int(field(b, "m_RootOrder") or 0))

def name_of(tid):
    return go_name.get(tr[tid]['go'], '')

children = {}
for tid, t in tr.items():
    children.setdefault(t['father'], []).append(tid)
for k in children:
    children[k].sort(key=lambda x: tr[x]['order'])

# body root
roots = [tid for tid in tr if name_of(tid) == BODY_ROOT]
if not roots:
    raise SystemExit("body root not found")
root = roots[0]
print("body root:", root, name_of(root), "local", tr[root]['pos'], tr[root]['rot'], tr[root]['scale'])

# DFS
order = []
def walk(tid):
    if tid in order:
        return
    order.append(tid)
    for ch in children.get(tid, []):
        walk(ch)
walk(root)
print("subtree nodes:", len(order))

# SMR with 42 bones
smr = None
for fid, (c, b) in docs.items():
    if c == 137:
        bones = refs_list(b, "m_Bones")
        if len(bones) == 42:
            smr = dict(id=fid, go=fref(b, "m_GameObject"), bones=bones,
                       rootBone=fref(b, "m_RootBone"), mesh=guid_of(b, "m_Mesh"))
if smr is None:
    raise SystemExit("42-bone SMR not found")

bone_set = set(smr["bones"])
missing = [b for b in smr["bones"] if b not in order]
print("bones outside subtree:", len(missing))
for b in missing:
    order.append(b)

idx = {tid: i for i, tid in enumerate(order)}
skeleton = []
for tid in order:
    t = tr[tid]
    skeleton.append({
        "name": name_of(tid),
        "parent": idx.get(t['father'], -1) if t['father'] in idx else -1,
        "pos": t['pos'],
        "rot": t['rot'],
        "scale": t['scale'],
        "bone": tid in bone_set,
    })

data = {
    "root_index": 0,
    "nodes": skeleton,
    "boneNodes": [idx[b] for b in smr["bones"]],
    "smrRootBone": idx.get(smr["rootBone"], -1),
}
json.dump(data, open(OUT, "w", encoding="utf-8"), indent=1)

smr_tr = next(t for t in tr if tr[t]['go'] == smr['go'])
print("smr node:", idx[smr_tr], name_of(smr_tr))
print("smrRootBone:", idx.get(smr["rootBone"]), name_of(smr["rootBone"]))
print("nodes:", len(skeleton), "bones:", len(data["boneNodes"]))
print("names:", [s['name'] for s in skeleton])
print("wrote", OUT)

# rifle
for tid in order:
    if name_of(tid).startswith("Rem870"):
        print("RIFLE node:", idx[tid], name_of(tid), "local", tr[tid]['pos'], tr[tid]['rot'], tr[tid]['scale'])
        # find MeshFilter on that GameObject
        go = tr[tid]['go']
        for fid, (c, b) in docs.items():
            if c == 33 and fref(b, "m_GameObject") == go:
                g = guid_of(b, "m_Mesh")
                print("  MeshFilter mesh guid:", g)
                for meta in glob.glob(r"D:\Rake\RipMP\ExportedProject\Assets\**\*.meta", recursive=True):
                    try:
                        t2 = open(meta, encoding="utf-8", errors="ignore").read()
                    except Exception:
                        continue
                    if g and g in t2 and "guid:" in t2.split("\n")[1]:
                        if re.search(r"^guid:\s*" + g, t2, re.M):
                            print("  mesh file:", meta[:-5])
                            break
