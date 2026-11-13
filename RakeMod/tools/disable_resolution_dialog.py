import UnityPy, os, shutil, json, datetime

data_dir = r'D:\Rake Multiplayer\Rake\Rake_Data'
path = os.path.join(data_dir, 'mainData')
backup = os.path.join(data_dir, 'mainData.bak')

if not os.path.isfile(path):
    raise SystemExit('mainData not found')

# 1. backup (never overwrite an existing good backup)
if not os.path.isfile(backup):
    shutil.copy2(path, backup)
    print('backup created:', backup, os.path.getsize(backup))
else:
    print('backup already exists:', backup, os.path.getsize(backup))

env = UnityPy.load(path)
target = None
for o in env.objects:
    if o.type.name == 'PlayerSettings':
        target = o
        break

if target is None:
    raise SystemExit('PlayerSettings not found')

tree = target.read_typetree()
old_value = tree.get('displayResolutionDialog')
print('displayResolutionDialog before =', old_value)
if old_value == 0:
    print('already disabled')
    raise SystemExit(0)
if old_value != 1:
    raise SystemExit('unexpected value: %r' % (old_value,))

orig = target.get_raw_data()
tree['displayResolutionDialog'] = 0
target.save_typetree(tree)
new = target.data
if len(orig) != len(new):
    raise SystemExit('object size changed: %d -> %d' % (len(orig), len(new)))

diffs = [(i, orig[i], new[i]) for i in range(len(orig)) if orig[i] != new[i]]
print('object diffs:', diffs)
if len(diffs) != 1 or diffs[0][1] != 1 or diffs[0][2] != 0:
    raise SystemExit('expected exactly one 1->0 byte, got %r' % (diffs,))

rel_off, _, _ = diffs[0]
abs_off = target.byte_start + rel_off
print('absolute offset:', abs_off)

with open(path, 'rb') as f:
    data = bytearray(f.read())
print('file byte before patch:', data[abs_off], 'size:', len(data))
if data[abs_off] != 1:
    raise SystemExit('file byte at offset is not 1')

data[abs_off] = 0
with open(path, 'wb') as f:
    f.write(data)
print('file patched, size:', os.path.getsize(path))

# verify by reloading
env2 = UnityPy.load(path)
for o in env2.objects:
    if o.type.name == 'PlayerSettings':
        t2 = o.read_typetree()
        print('displayResolutionDialog after =', t2['displayResolutionDialog'])
        print('defaultScreenWidth =', t2['defaultScreenWidth'])
        print('defaultScreenHeight =', t2['defaultScreenHeight'])
        print('companyName =', repr(t2['companyName']))
        print('productName =', repr(t2['productName']))
        break

report = {
    'data_dir': data_dir,
    'file': path,
    'backup': backup,
    'backup_size': os.path.getsize(backup),
    'field': 'displayResolutionDialog',
    'old': old_value,
    'new': 0,
    'absolute_offset': abs_off,
    'file_size_after': os.path.getsize(path)
}
open(os.path.join(os.path.dirname(path), '..', 'resolution_dialog_patch.json'), 'w', encoding='utf-8').write(
    json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
