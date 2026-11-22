"""Review of June's Walk clip, rendered and measured from the RE-IMPORTED June.fbx.

    blender -b -t 2 --python tools/blender/export/june_walk_review.py -- <out dir> [--chars DIR] [--samples 32]
                                                                      [--only sheet,ground] [--before OLD.fbx]

Writes into <out dir>:
  june_walk_sheet.png    eight frames across one cycle: her right side, front three-quarter, the legs with the
                         cape hidden, and from behind (how the cape trails)
  june_walk_ground.png   the same clip with the root moved at junekit_anims.JUNE_WALK_SPEED past a fixed camera,
                         cape hidden; the bars on the floor are fixed footprints (amber = left, teal = right)
  june_walk_contact.json measured from the imported bones: how far each foot's contact point drifts on the
                         floor while it is down, the lowest sole point, knee angles, and loop closure
`--before` names the previous June.fbx: the script then also checks that the mesh and every older clip are
unchanged (same vertices, same keys).
"""
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOLS))
for sub in ("style", "pip", "chars", "export"):
    sys.path.insert(0, os.path.join(TOOLS, sub))

import costume as co  # noqa: E402
import junekit_anims as anims  # noqa: E402
import pip_model as pm  # noqa: E402
from pip_review import label  # noqa: E402
from witness_review import import_model  # noqa: E402

MODELS = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models")
CLIP = "Walk"
N = anims.JUNE_WALK_FRAMES
SPEED = anims.JUNE_WALK_SPEED
SHEET_FRAMES = (0, 4, 8, 11, 15, 19, 23, 26)
GROUND_FRAMES = (0, 4, 8, 11, 15, 19, 23, 26, 30)
HEEL = Vector((0.0, 0.049, -0.09))  # the heel's sole point from the ankle (armature space, rest)
TIP = Vector((0.0, -0.0615, -0.0233))  # the tip of the sole from the toe joint


def orbit(target, dist, yaw, pitch):
    a, e = math.radians(yaw), math.radians(pitch)
    return Vector(target) + dist * Vector((-math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))


def bar(name, y0, y1, x0, x1, colour):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0, 0, 0, 1)
    bsdf.inputs["Emission Color"].default_value = (*colour, 1)
    bsdf.inputs["Emission Strength"].default_value = 1.0
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, 0.001))
                          @ Matrix.Diagonal((x1 - x0, y1 - y0, 0.002, 1.0)))
    o = pm.new_mesh_object(name, bm, mat, smooth=False)
    o.visible_shadow = False
    return o


def measure(m, scene):
    """Bone positions per frame of the Walk action, and what they say about the feet."""
    rig = m["rig"]
    rig.animation_data.action = m["actions"][CLIP]
    start = int(m["actions"][CLIP].frame_range[0])
    rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
    track = {}
    for f in range(N + 1):
        scene.frame_set(start + f)
        bpy.context.view_layer.update()
        for S in "LR":
            foot, toe = rig.pose.bones["Foot_" + S], rig.pose.bones["Toe_" + S]
            Mf = rig.matrix_world @ foot.matrix @ rest["Foot_" + S].inverted()
            Mt = rig.matrix_world @ toe.matrix @ rest["Toe_" + S].inverted()
            ankle0, toe0 = rest["Foot_" + S].translation, rest["Toe_" + S].translation
            knee = rig.matrix_world @ rig.pose.bones["LowerLeg_" + S].head
            hip = rig.matrix_world @ rig.pose.bones["UpperLeg_" + S].head
            ankle = Mf @ ankle0
            bend = math.degrees((knee - hip).angle(ankle - knee))
            track.setdefault(S, []).append({
                "ankle": ankle, "heel": Mf @ (ankle0 + HEEL), "ball": Mt @ Vector((toe0.x, toe0.y, 0.0)),
                "tip": Mt @ (toe0 + TIP), "knee": bend})
        track.setdefault("all", []).append({pb.name: (rig.matrix_world @ pb.matrix).copy() for pb in rig.pose.bones})
    duty = anims.JUNE_WALK_DUTY
    out = {"clip": CLIP, "frames": N, "fps": anims.FPS, "speed_mps": SPEED, "stride_m": anims.JUNE_WALK_STRIDE,
           "step_m": anims.JUNE_WALK_STRIDE / 2, "duty": duty, "feet": {}}
    for S, shift in (("L", 0.0), ("R", 0.5)):
        down = [f for f in range(N) if ((f / N + shift) % 1.0) < duty - 1e-6]
        # unwrap the stance frames so they are in time order from the heel strike
        strike = round(((1.0 - shift) % 1.0) * N) % N
        order = sorted(down, key=lambda f: (f - strike) % N)
        rows = []
        for f in order:
            k = (f - strike) % N
            t = k / anims.FPS
            p = track[S][f]
            flat_heel = p["heel"].z < 0.004
            flat_ball = p["ball"].z < 0.004
            # world position of a point = clip position minus the root's travel (she walks toward -Y)
            rows.append({"frame": f, "since_strike": k, "heel_down": flat_heel, "ball_down": flat_ball,
                         "heel_y": p["heel"].y - SPEED * t, "ball_y": p["ball"].y - SPEED * t,
                         "heel_z": p["heel"].z, "ball_z": p["ball"].z, "tip_z": p["tip"].z,
                         "x": p["ankle"].x})

        def drift(key, flag):
            ys = [r[key] for r in rows if r[flag]]
            return (max(ys) - min(ys)) * 1000 if ys else None
        xs = [r["x"] for r in rows]
        lows = [min(p["heel"].z, p["ball"].z, p["tip"].z) for p in track[S][:N]]
        knees = [p["knee"] for p in track[S][:N]]
        out["feet"][S] = {
            "stance_frames": order, "heel_drift_mm": drift("heel_y", "heel_down"),
            "ball_drift_mm": drift("ball_y", "ball_down"), "sideways_drift_mm": (max(xs) - min(xs)) * 1000,
            "lowest_sole_mm": min(lows) * 1000, "highest_sole_low_point_mm": max(lows) * 1000,
            "footprint_y": float(np.mean([r["ball_y"] for r in rows if r["ball_down"]])),
            "knee_deg": [round(k, 1) for k in knees],
            "knee_min_deg": min(knees), "knee_max_deg": max(knees),
            "knee_max_step_deg": max(abs(knees[(i + 1) % N] - knees[i]) for i in range(N)),
        }
    a, b = track["all"][0], track["all"][N]
    out["loop_closure_max"] = max(max(abs(x - y) for ra, rb in zip(a[k], b[k]) for x, y in zip(ra, rb)) for k in a)
    hips = [fr["Hips"].translation.z for fr in track["all"][:N]]
    out["hips_bounce_mm"] = (max(hips) - min(hips)) * 1000
    return out, track


def compare(m, before_path, scene):
    """The mesh and the older clips of the new FBX against the previous file."""
    folder, name = os.path.split(before_path)
    old = import_model(folder, os.path.splitext(name)[0], leaf_bones=True)
    res = {"mesh_max_delta": 0.0, "clips": {}}
    new_meshes = {o.name.split(".")[0]: o for o in m["meshes"]}
    for o in old["meshes"]:
        n = new_meshes[o.name.split(".")[0]]
        if len(o.data.vertices) != len(n.data.vertices):
            res["mesh_max_delta"] = float("inf")
            continue
        a = np.empty(len(o.data.vertices) * 3)
        b = np.empty(len(o.data.vertices) * 3)
        o.data.vertices.foreach_get("co", a)
        n.data.vertices.foreach_get("co", b)
        res["mesh_max_delta"] = max(res["mesh_max_delta"], float(np.abs(a - b).max()))
    for clip, act in old["actions"].items():
        other = m["actions"].get(clip)
        if other is None:
            res["clips"][clip] = "MISSING"
            continue
        worst = 0.0
        theirs = {(fc.data_path, fc.array_index): fc for fc in other.fcurves}
        same_range = tuple(act.frame_range) == tuple(other.frame_range)
        for fc in act.fcurves:
            g = theirs.get((fc.data_path, fc.array_index))
            if g is None or len(g.keyframe_points) != len(fc.keyframe_points):
                worst = float("inf")
                break
            a = np.empty(len(fc.keyframe_points) * 2)
            b = np.empty(len(fc.keyframe_points) * 2)
            fc.keyframe_points.foreach_get("co", a)
            g.keyframe_points.foreach_get("co", b)
            worst = max(worst, float(np.abs(a - b).max()))
        res["clips"][clip] = {"max_key_delta": worst, "same_range": same_range}
    for o in old["all"]:
        bpy.data.objects.remove(o, do_unlink=True)
    return res


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out = argv[0]

    def opt(name, default):
        return argv[argv.index(name) + 1] if name in argv else default
    chars = opt("--chars", os.path.join(MODELS, "Characters"))
    only = set(opt("--only", "sheet,ground").split(","))
    before = opt("--before", "")
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cam = co.stage()
    scene = bpy.context.scene
    scene.eevee.taa_render_samples = int(opt("--samples", "32"))
    bpy.data.objects["Ground"].scale = (4, 4, 1)
    m = import_model(os.path.join(chars, "June"), "June", leaf_bones=True)
    rig = m["rig"]
    rig.animation_data_create()
    report, track = measure(m, scene)
    if before:
        report["unchanged"] = compare(m, before, scene)
    with open(os.path.join(out, "june_walk_contact.json"), "w") as fh:
        json.dump(report, fh, indent=1)
    print("[june_walk_review]", json.dumps({k: v for k, v in report.items()}, indent=1))
    cape = [o for o in m["meshes"] if "Cape" in o.name]
    start = int(m["actions"][CLIP].frame_range[0])
    tmp = os.path.join(out, "_tile")
    count = [0]

    def shot(location, target, lens, w, h, text="", size=0.05):
        count[0] += 1
        scene.render.resolution_x, scene.render.resolution_y = w, h
        cam.data.lens = lens
        lab = label(cam, text, size) if text else None
        px = co.render_view(cam, location, target, lens, w, h, f"{tmp}_{count[0]}.png")
        if lab:
            bpy.data.objects.remove(lab, do_unlink=True)
        return px

    def pose(frame, travel=False):
        rig.animation_data.action = m["actions"][CLIP]
        scene.frame_set(start + frame)
        # her root would be here; the clip stays in place, so the floor marks and the camera move instead
        return anims.JUNE_WALK_STRIDE / 2 - SPEED * frame / anims.FPS if travel else 0.0

    def hide_cape(hide):
        for o in cape:
            o.hide_render = hide

    if "sheet" in only:
        rows = []
        for name, yaw, pitch, no_cape, target, dist, th in (
                ("her right side", 90, 4, False, (0, 0, 0.84), 9.0, 420),
                ("front three-quarter", 35, 8, False, (0, 0, 0.84), 9.0, 420),
                ("legs (cape hidden)", 90, 2, True, (0, 0, 0.5), 9.0, 300),
                ("behind", 155, 12, False, (0, 0, 0.84), 9.0, 420)):
            hide_cape(no_cape)
            tiles = []
            for f in SHEET_FRAMES:
                pose(f)
                lens = 120 if th == 420 else 190
                tiles.append(shot(orbit(target, dist, yaw, pitch), target, lens, 250, th,
                                  f"{CLIP} {f}" + (f"  {name}" if f == 0 else ""), 0.055))
            rows.append(np.concatenate(tiles, axis=1))
        hide_cape(False)
        co.save_sheet(np.concatenate(rows[::-1], axis=0), os.path.join(out, "june_walk_sheet.png"))
    if "ground" in only:
        hide_cape(True)
        half = anims.JUNE_WALK_STRIDE / 2
        marks = []
        for S, colour, shift in (("L", (1.0, 0.62, 0.1), 0.0), ("R", (0.1, 0.85, 0.8), 0.5)):
            ball = report["feet"][S]["footprint_y"]  # where the ball of the foot stands, from its root at strike
            for k in (-1, 0, 1):
                y = ball + half - SPEED * shift * N / anims.FPS - k * anims.JUNE_WALK_STRIDE
                # the sole: from the tip (0.0615 ahead of the ball) to the heel (0.157 behind it)
                marks.append((bar(f"Print{S}{k}", y - 0.0615, y + 0.157, -0.34, 0.13, colour), y))
        tiles = []
        for f in GROUND_FRAMES:
            root = pose(f, travel=True)
            for o, _y in marks:
                o.location.y = -root
            target = (0, -root, 0.4)
            tiles.append(shot(orbit(target, 14.0, 90, 5), target, 225, 660, 330,
                              f"{CLIP} {f}   root at {SPEED * f / anims.FPS:.2f} m", 0.03))
        rows = [np.concatenate(tiles[k:k + 3], axis=1) for k in range(0, 9, 3)]
        co.save_sheet(np.concatenate(rows[::-1], axis=0), os.path.join(out, "june_walk_ground.png"))
        for o, _y in marks:
            bpy.data.objects.remove(o, do_unlink=True)
        hide_cape(False)
    print("[june_walk_review] done ->", out)


if __name__ == "__main__":
    main()
