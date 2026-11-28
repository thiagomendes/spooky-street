"""Review sheets of the crowd costume kit, rendered from the RE-IMPORTED FBX files and committed textures (the files
the game loads), beside Pip's committed model.

    blender -b -t 2 --python tools/blender/crowd/crowd_review.py -- <out_dir> [--crowd DIR] [--only A,B]
                                                                   [--samples 24] [--no-lineup] [--no-walkers]

Writes:
  crowd_lineup.png     every walker of the slice beside Pip: the GAMEPLAY camera (3.8 m, 50 degrees vertical, a
                       phone-shaped 844 x 390 frame) from the front and from behind, its greyscale, the LOD1
                       meshes at 12 m in the same camera; then the close lineup from the front and from behind
  walk_<Walker>.png    one per walker: eight Walk frames from the front three-quarter and eight in profile, four
                       from behind and four at the gameplay camera (Pip's view of a walker crossing), Wave and
                       Talk key frames, the face open and mid-blink, and each clip's last frame beside Idle 0
A walker is assembled the way the game merges it: the body's skeleton with only the walker's parts.
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars", "export", "crowd"):
    p = os.path.join(TOOLS, sub)
    if p not in sys.path:
        sys.path.insert(0, p)

import costume as co  # noqa: E402
import crowd_kit as kit  # noqa: E402
import witness_review as wr  # noqa: E402
from pip_review import label  # noqa: E402

W = 1688
GAME_DISTANCE, GAME_FOV, GAME_PITCH = 3.8, 50.0, 4.0
PHONE = (844, 390)
SPACING = 0.74
RECIPES = {}  # the walkers, or the door neighbours with --neighbours


def game_lens(aspect):
    half_w = math.atan(math.tan(math.radians(GAME_FOV / 2)) * aspect)
    return 18.0 / math.tan(half_w)


def crowd_material(folder, atlas="Crowd"):
    base = bpy.data.images.load(os.path.join(folder, atlas + "_BaseColor.png"))
    normal = bpy.data.images.load(os.path.join(folder, atlas + "_Normal.png"))
    normal.colorspace_settings.name = "Non-Color"
    emission = bpy.data.images.load(os.path.join(folder, atlas + "_Emission.png"))
    mat = bpy.data.materials.new("Review_Crowd")
    mat.use_nodes = True
    mat.use_backface_culling = True  # as URP Lit
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    t = nodes.new("ShaderNodeTexImage")
    t.image = base
    links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    tn = nodes.new("ShaderNodeTexImage")
    tn.image = normal
    nm = nodes.new("ShaderNodeNormalMap")
    links.new(tn.outputs["Color"], nm.inputs["Color"])
    links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    te = nodes.new("ShaderNodeTexImage")
    te.image = emission
    links.new(te.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.0
    bsdf.inputs["Roughness"].default_value = 0.9
    return mat


def walker(folder, wid, mat, lod1=False):
    body, names = RECIPES[wid]
    m = wr.import_model(folder, body, leaf_bones=True)
    want = {n + (kit.LOD1_SUFFIX if lod1 else "") for n in names}
    keep = []
    for o in m["meshes"]:
        if o.name.split(".")[0] in want:
            o.data.materials.clear()
            o.data.materials.append(mat)
            keep.append(o)
        else:
            bpy.data.objects.remove(o, do_unlink=True)
    m["meshes"] = keep
    m["all"] = [m["rig"]] + keep
    for o in keep:
        o.data.calc_loop_triangles()
    m["tris"] = sum(len(o.data.loop_triangles) for o in keep)
    m["id"] = wid
    found = {o.name.split(".")[0] for o in keep}
    if found != want:
        raise RuntimeError(f"{wid}: missing parts {sorted(want - found)}")
    return m


def place(m, x, yaw=0.0, y=0.0):
    """Move the whole model through an empty above its rig (the takes key the rig object itself)."""
    if "mover" not in m:
        mover = bpy.data.objects.new("Mover", None)
        bpy.context.scene.collection.objects.link(mover)
        top = m["rig"]
        while top.parent is not None:
            top = top.parent
        top.parent = mover
        m["mover"] = mover
    m["mover"].location = (x, y, 0)
    m["mover"].rotation_euler = (0, 0, math.radians(yaw))


def turn(m, yaw):
    m["mover"].rotation_euler = (0, 0, math.radians(yaw))


def set_pose(m, clip, frame):
    rig = m["rig"]
    rig.animation_data_create()
    act = m["actions"][clip]
    rig.animation_data.action = act
    bpy.context.scene.frame_set(int(act.frame_range[0]) + frame)


def clip_len(m, clip):
    a = m["actions"][clip]
    return int(a.frame_range[1] - a.frame_range[0])


def orbit(target, dist, yaw, pitch):
    a, e = math.radians(yaw), math.radians(pitch)
    return Vector(target) + dist * Vector((-math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))


def row(tiles, width=W):
    h = max(t.shape[0] for t in tiles)
    padded = []
    for t in tiles:
        if t.shape[0] < h:
            t = np.pad(t, ((0, h - t.shape[0]), (0, 0), (0, 0)), constant_values=0.07)
            t[:, :, 3] = 1.0
        padded.append(t)
    r = np.concatenate(padded, axis=1)
    if r.shape[1] < width:
        r = np.pad(r, ((0, 0), (0, width - r.shape[1]), (0, 0)), constant_values=0.07)
        r[:, :, 3] = 1.0
    return r[:, :width]


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out_dir = argv[0]

    def opt(name, default):
        return argv[argv.index(name) + 1] if name in argv else default
    folder = opt("--crowd", os.path.join(wr.MODELS, "Characters", "Crowd"))
    neighbours = "--neighbours" in argv
    RECIPES.update(kit.NEIGHBOURS if neighbours else kit.WALKERS)
    only = [w for w in opt("--only", ",".join(RECIPES)).split(",") if w]
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cam = co.stage(lineup=True)
    scene = bpy.context.scene
    scene.eevee.taa_render_samples = int(opt("--samples", "24"))
    tmp = os.path.join(out_dir, "_tile")
    count = [0]

    def shot(location, target, lens, w, h, text="", size=0.03):
        count[0] += 1
        scene.render.resolution_x, scene.render.resolution_y = w, h
        cam.data.lens = lens
        cam.location = Vector(location)
        cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
        lab = label(cam, text, size) if text else None
        px = co.render_view(cam, location, target, lens, w, h, f"{tmp}_{count[0]}.png")
        if lab:
            bpy.data.objects.remove(lab, do_unlink=True)
        return px

    mat = crowd_material(folder, kit.NEIGHBOUR_BODY if neighbours else "Crowd")
    pip = wr.import_model(os.path.join(wr.MODELS, "Characters", "Pip"), "Pip", leaf_bones=True)
    models = {w: walker(folder, w, mat) for w in only}
    lods = {}
    phone_lens = game_lens(PHONE[0] / PHONE[1])

    def show(visible):
        for m in list(models.values()) + list(lods.values()) + [pip]:
            for o in m["all"]:
                o.hide_render = m not in visible

    if "--no-lineup" not in argv:
        order = list(models.values())
        n = len(order) + 1
        xs = [(i - (n - 1) / 2) * SPACING for i in range(n)]
        pip_at = n // 2
        lineup = order[:pip_at] + [pip] + order[pip_at:]
        for m, x in zip(lineup, xs):
            place(m, x)
            set_pose(m, "Idle", 0)
        show(lineup)
        look = Vector((0, 0, 0.66))
        front = shot(orbit(look, GAME_DISTANCE, 0, GAME_PITCH + 8), look, phone_lens, *PHONE,
                     "gameplay camera (3.8 m, 50 deg), front: the crowd with Pip in the middle", 0.03)
        for m in lineup:
            turn(m, 180)
        back = shot(orbit(look, GAME_DISTANCE, 0, GAME_PITCH + 8), look, phone_lens, *PHONE,
                    "gameplay camera, from behind", 0.03)
        for m in lineup:
            turn(m, 0)
        grey = front.copy()
        g = (0.299 * grey[:, :, 0] + 0.587 * grey[:, :, 1] + 0.114 * grey[:, :, 2])
        grey[:, :, 0] = grey[:, :, 1] = grey[:, :, 2] = g
        lods.update({w: walker(folder, w, mat, lod1=True) for w in only})
        lord = list(lods.values())
        lineup_lo = lord[:pip_at] + [pip] + lord[pip_at:]
        for m, x in zip(lineup_lo, xs):
            place(m, x, y=0.0)
            set_pose(m, "Walk", 6)
        show(lineup_lo)
        far = shot(orbit(look, 12.0, 0, GAME_PITCH + 8), look, phone_lens, *PHONE,
                   "LOD1 meshes at 12 m, gameplay camera (Pip keeps her LOD0)", 0.03)
        show(lineup)
        for m in lineup:
            set_pose(m, "Idle", 0)
        mid = Vector((0, 0, 0.82))
        close_f = shot(orbit(mid, 14.0, 0, 3), mid, 66, W, 640,
                       "close lineup, Idle frame 0: " + ", ".join(m.get("id", "Pip") for m in lineup), 0.02)
        close_b = shot(orbit(mid, 14.0, 180, 3), mid, 66, W, 640, "from behind", 0.02)
        info = "  ".join(f"{m['id']} {m['tris']}/{lods[m['id']]['tris']}" for m in order)
        print("[crowd_review] triangles LOD0/LOD1:", info)
        sheet = [row([front, back]), row([grey, far]), close_f, close_b]
        out = os.path.join(out_dir, "neighbour_lineup.png" if neighbours else "crowd_lineup.png")
        co.save_sheet(np.concatenate(sheet[::-1], axis=0), out)
        print("[crowd_review] sheet ->", out)
        for m in lods.values():
            for o in m["all"]:
                o.hide_render = True

    if "--no-walkers" not in argv:
        for wid, m in models.items():
            place(m, 0)
            show([m])
            adult = RECIPES[wid][0] != "CrowdChild"
            look = Vector((0, 0, 0.95 if adult else 0.58))
            dist = 5.0 if adult else 3.4
            rows = []
            n = clip_len(m, "Walk")
            frames = [round(i * n / 8) for i in range(8)]
            for yaw, note in ((28, f"{wid}  Walk {n} frames"), (90, "profile")):
                tiles = []
                for i, f in enumerate(frames):
                    set_pose(m, "Walk", f)
                    tiles.append(shot(orbit(look, dist, yaw, 6), look, 85, 211, 330,
                                      f"{f}" + (f"  {note}" if i == 0 else ""), 0.07))
                rows.append(row(tiles))
            tiles = []
            for f in frames[::2]:
                set_pose(m, "Walk", f)
                tiles.append(shot(orbit(look, dist, 160, 6), look, 85, 211, 330, f"back {f}", 0.07))
            pip_look = Vector((0, -1.6, 0.66))
            for f in frames[1::4]:
                set_pose(m, "Walk", f)
                turn(m, 90)
                tiles.append(shot(orbit(pip_look, GAME_DISTANCE, 180, GAME_PITCH + 8), pip_look, phone_lens, 422,
                                  195, f"game camera {f}", 0.05))
                turn(m, 0)
            rows.append(row(tiles))
            tiles = []
            for clip, fr in (("Wave", (8, 16, 24, 34)), ("Talk", (0, 18, 36, 54))):
                for f in fr:
                    set_pose(m, clip, f)
                    tiles.append(shot(orbit(look, dist, 20, 6), look, 85, 211, 330, f"{clip} {f}", 0.07))
            rows.append(row(tiles))
            tiles = []
            head = m["rig"].matrix_world @ m["rig"].pose.bones["Head"].head
            for clip, f, note in (("Idle", 0, "face"), ("Idle", 52, "blink")):
                set_pose(m, clip, f)
                head = m["rig"].matrix_world @ m["rig"].pose.bones["Head"].head
                at = head + Vector((0, 0, 0.1 if adult else 0.08))
                tiles.append(shot(at + Vector((0.12, -1.0, 0.05)), at, 120, 211, 330, note, 0.07))
            for clip in ("Walk", "Wave", "Talk"):
                set_pose(m, clip, clip_len(m, clip))
                tiles.append(shot(orbit(look, dist, 20, 6), look, 85, 211, 330, f"{clip} last", 0.07))
            set_pose(m, "Idle", 0)
            tiles.append(shot(orbit(look, dist, 20, 6), look, 85, 211, 330, "Idle 0", 0.07))
            set_pose(m, "Idle", 0)
            tiles.append(shot(orbit(look, dist, 200, 6), look, 85, 211, 330, "Idle 0 back", 0.07))
            rows.append(row(tiles))
            out = os.path.join(out_dir, (f"neighbour_{wid}.png" if neighbours else f"walk_{wid}.png"))
            co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
            print("[crowd_review] sheet ->", out, "tris", m["tris"])


if __name__ == "__main__":
    main()
