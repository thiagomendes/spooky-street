"""Compares two exports of a character FBX: proof that a clip-only change left the mesh, the skin and the other
clips alone.

    blender -b -t 2 --python tools/blender/export/pip_fbx_diff.py -- <old.fbx> <new.fbx>

Imports both files and prints the largest difference in vertex positions, UVs and skin weights per mesh, the bone
lists, and for every clip its length and the largest difference in any animation curve (or "new" / "removed").

A rebuild from scratch (no saved --cache scene) writes the same model with its vertices in another order, so a
mesh that differs vertex by vertex is compared again WITHOUT order: as the set of its corners (position + UV) and
the set of its skinned vertices (position + bone weights), rounded to 0.1 mm / 1e-4. "same, reordered" means every
corner and every weighted vertex of one file is in the other. Bones or meshes that only the new file has are
listed as added (S003a: Mr. Vale's `Watch` bone and `Vale_Watch` mesh) and do not count as a change to what was
there; a clip whose only difference is curves for an added bone is reported as unchanged for the old bones.
"""
import sys

import bpy
import numpy as np


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False, use_anim=True, ignore_leaf_bones=True)
    meshes = {}
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        me = o.data
        co = np.zeros(len(me.vertices) * 3)
        me.vertices.foreach_get("co", co)
        uv = np.zeros(len(me.loops) * 2)
        me.uv_layers[0].data.foreach_get("uv", uv)
        names = {g.index: g.name for g in o.vertex_groups}
        weights = sorted((v.index, names[g.group], round(g.weight, 5)) for v in me.vertices for g in v.groups)
        at = [tuple(round(c, 4) for c in v.co) for v in me.vertices]
        loop_vert = np.zeros(len(me.loops), dtype=np.int32)
        me.loops.foreach_get("vertex_index", loop_vert)
        corners = sorted(at[int(v)] + (round(float(uv[2 * i]), 4), round(float(uv[2 * i + 1]), 4))
                         for i, v in enumerate(loop_vert))
        skin = sorted(at[v.index] + tuple(sorted((names[g.group], round(g.weight, 4)) for g in v.groups
                                                 if g.weight > 5e-5))
                      for v in me.vertices)
        meshes[o.name] = (co, uv, weights, len(me.polygons), corners, skin)
    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    bones = [(b.name, b.parent.name if b.parent else None, tuple(round(c, 5) for c in b.head_local))
             for b in rig.data.bones]
    actions = {}
    for a in bpy.data.actions:
        lo, hi = int(a.frame_range[0]), int(a.frame_range[1])
        curves = {(fc.data_path, fc.array_index): np.array([fc.evaluate(f) for f in range(lo, hi + 1)])
                  for fc in a.fcurves}
        actions[a.name.split("|")[-1]] = (hi - lo, curves)
    return meshes, bones, actions


def main():
    old_path, new_path = sys.argv[sys.argv.index("--") + 1:][:2]
    old, new = load(old_path), load(new_path)
    same = True
    for name in sorted(set(old[0]) | set(new[0])):
        if name not in old[0]:
            print(f"[pip_fbx_diff] mesh {name}: ADDED, {len(new[0][name][0]) // 3} verts, {new[0][name][3]} faces")
            continue
        if name not in new[0]:
            print(f"[pip_fbx_diff] mesh {name}: REMOVED")
            same = False
            continue
        a, b = old[0][name], new[0][name]
        if a[0].shape != b[0].shape or a[1].shape != b[1].shape or a[3] != b[3]:
            print(f"[pip_fbx_diff] mesh {name}: topology differs")
            same = False
            continue
        d_co, d_uv = float(np.abs(a[0] - b[0]).max()), float(np.abs(a[1] - b[1]).max())
        ok = d_co < 1e-6 and d_uv < 1e-6 and a[2] == b[2]
        if ok:
            print(f"[pip_fbx_diff] mesh {name}: {len(a[0]) // 3} verts, {a[3]} faces, max vertex diff {d_co:.2e}, "
                  f"max uv diff {d_uv:.2e}, weights identical")
            continue
        corners, skin = a[4] == b[4], a[5] == b[5]
        if corners and skin:
            print(f"[pip_fbx_diff] mesh {name}: {len(a[0]) // 3} verts, {a[3]} faces: same, reordered "
                  f"({len(a[4])} corners (position + uv) and {len(a[5])} weighted vertices all match)")
        else:
            miss_c = len(set(a[4]) ^ set(b[4]))
            miss_s = len(set(a[5]) ^ set(b[5]))
            print(f"[pip_fbx_diff] mesh {name}: DIFFERS ({miss_c} corners and {miss_s} weighted vertices do not match)")
            same = False
    old_bones, new_bones = {b[0]: b for b in old[1]}, {b[0]: b for b in new[1]}
    added = sorted(set(new_bones) - set(old_bones))
    kept = all(new_bones.get(n) == b for n, b in old_bones.items())
    print(f"[pip_fbx_diff] bones: {len(old[1])} -> {len(new[1])}, "
          f"{'the old ones identical' if kept else 'DIFFER'}" + (f", added {added}" if added else ""))
    same &= kept
    for name in sorted(set(old[2]) | set(new[2])):
        if name not in old[2]:
            print(f"[pip_fbx_diff] clip {name}: new, {new[2][name][0]} frames")
        elif name not in new[2]:
            print(f"[pip_fbx_diff] clip {name}: REMOVED")
        else:
            (n0, c0), (n1, c1) = old[2][name], new[2][name]
            extra = sorted({k[0].split('"')[1] for k in set(c1) - set(c0) if '"' in k[0]})
            if n0 != n1 or set(c0) - set(c1) or any(b not in added for b in extra):
                print(f"[pip_fbx_diff] clip {name}: changed ({n0} -> {n1} frames)")
            else:
                d = max(float(np.abs(c0[k] - c1[k]).max()) for k in c0)
                print(f"[pip_fbx_diff] clip {name}: {n0} frames, max curve diff {d:.2e}"
                      + ("" if d < 1e-5 else "  CHANGED") + (f" (plus curves for {extra})" if extra else ""))
    print("[pip_fbx_diff] mesh, skin and skeleton", "unchanged" if same else "CHANGED")


if __name__ == "__main__":
    main()
