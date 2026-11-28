"""Clipping check of the crowd costume kit, measured on the EXPORTED FBX files (the files the game loads).

    blender -b -t 2 --python tools/blender/crowd/crowd_clip_check.py -- [--crowd DIR] [--report FILE]

For every walker whose costume covers the arms (the ghost's sheet), every frame of every clip is evaluated and each
arm and hand vertex above the hem is tested against the sheet: a horizontal ray from the body's axis (the Spine
bone) to the vertex that crosses the sheet's outer surface first means the arm shows through the cloth there.
Prints, per walker and clip, the frames where an arm pokes through and how far (mm); --report writes it as JSON.
"""
import json
import os
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars", "export", "crowd"):
    p = os.path.join(TOOLS, sub)
    if p not in sys.path:
        sys.path.insert(0, p)

import crowd_kit as kit  # noqa: E402
import witness_review as wr  # noqa: E402

# walker -> (the covering part, the parts whose arm and hand vertices must stay under it)
COVERED = {"GhostKid": ("Ghost_Sheet", ("Body_Indigo", "Hands_T2"))}
ARM_GROUPS = ("UpperArm_L", "UpperArm_R", "LowerArm_L", "LowerArm_R", "Hand_L", "Hand_R")
VERBOSE = "--verbose" in sys.argv
TOLERANCE = 0.002  # m beyond the cloth's outer surface before it counts (a texel at the gameplay camera is ~4 mm)


def world_coords(obj, depsgraph):
    ev = obj.evaluated_get(depsgraph)
    me = ev.to_mesh()
    mw = obj.matrix_world
    co = [mw @ v.co for v in me.vertices]
    polys = [tuple(p.vertices) for p in me.polygons]
    ev.to_mesh_clear()
    return co, polys


def probes_of(obj):
    """Indices of the vertices that belong to the arms and hands (weight over 0.5 on an arm bone)."""
    names = {g.index: g.name for g in obj.vertex_groups}
    out = []
    for v in obj.data.vertices:
        w = sum(g.weight for g in v.groups if names.get(g.group, "").startswith(ARM_GROUPS))
        if w > 0.5:
            out.append(v.index)
    return out


def check(folder, wid, cover, probe_parts):
    body, names = kit.WALKERS[wid]
    m = wr.import_model(folder, body, leaf_bones=True)
    keep = {}
    for o in m["meshes"]:
        n = o.name.split(".")[0]
        if n in names:
            keep[n] = o
        else:
            bpy.data.objects.remove(o, do_unlink=True)
    rig = m["rig"]
    sheet = keep[cover]
    probes = [(keep[n], probes_of(keep[n])) for n in probe_parts]
    scene = bpy.context.scene
    rig.animation_data_create()
    result = {}
    for clip, act in m["actions"].items():
        rig.animation_data.action = act
        f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
        frames = {}
        for f in range(f0, f1 + 1):
            scene.frame_set(f)
            dg = bpy.context.evaluated_depsgraph_get()
            co, polys = world_coords(sheet, dg)
            tree = BVHTree.FromPolygons(co, polys)
            hem = min(c.z for c in co)
            spine = rig.matrix_world @ rig.pose.bones["Spine"].head
            worst, count, where = 0.0, 0, None
            for obj, idx in probes:
                pc, _ = world_coords(obj, dg)
                for i in idx:
                    p = pc[i]
                    if p.z < hem + 0.01:
                        continue
                    o = Vector((spine.x, spine.y, p.z))
                    d = p - o
                    dist = d.length
                    if dist < 1e-4:
                        continue
                    # the last cloth surface crossed on the way out is the outer one
                    start, last = o, None
                    travelled = 0.0
                    while True:
                        hit = tree.ray_cast(start, d.normalized(), dist - travelled)
                        if hit[0] is None:
                            break
                        step = (hit[0] - start).length
                        travelled += step + 1e-4
                        last = travelled
                        start = hit[0] + d.normalized() * 1e-4
                    if last is not None and dist - last > TOLERANCE:
                        count += 1
                        if dist - last > worst:
                            worst, where = dist - last, (obj.name.split(".")[0], tuple(round(c, 3) for c in p - spine))
            if count:
                frames[f - f0] = {"vertices": count, "depth_mm": round(worst * 1000, 1), "worst_at": where}
        result[clip] = {"frames": f1 - f0, "clipping_frames": frames}
        bad = len(frames)
        deep = max((v["depth_mm"] for v in frames.values()), default=0.0)
        print(f"[clip_check] {wid} {clip}: {bad}/{f1 - f0 + 1} frames with an arm through the {cover}, "
              f"deepest {deep} mm  {sorted(frames)}", flush=True)
        for f, v in sorted(frames.items()):
            if VERBOSE:
                print(f"[clip_check]   {clip} {f}: {v}", flush=True)
    for o in [rig] + list(keep.values()):
        bpy.data.objects.remove(o, do_unlink=True)
    return result


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(name, default):
        return argv[argv.index(name) + 1] if name in argv else default
    folder = opt("--crowd", os.path.join(wr.MODELS, "Characters", "Crowd"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    report = {wid: check(folder, wid, cover, probes) for wid, (cover, probes) in COVERED.items()}
    path = opt("--report", "")
    if path:
        with open(path, "w") as fh:
            json.dump(report, fh, indent=1)


if __name__ == "__main__":
    main()
