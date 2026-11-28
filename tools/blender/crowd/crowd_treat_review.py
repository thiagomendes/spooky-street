"""Review sheets of the trick-or-treat clips, rendered from the EXPORTED crowd FBX files and the committed atlases (the
files the game loads), with Pip's committed model (docs/living-street-crowd-art.md "Trick-or-treat clips").

    blender -b -t 2 --python tools/blender/crowd/crowd_treat_review.py -- <out_dir> [--crowd DIR] [--pip DIR]
                                                                         [--samples 24] [--only a,b] [--night]

Sheets (each one also written small enough to read on a phone):
  treat_<Clip>_<Walker>.png  ten frames across the clip from the front three-quarter and in profile, five from
                             behind, the gameplay camera (3.8 m, 50 degrees, phone frame) at three moments, and the
                             last frame beside Idle frame 0. WalkStart / WalkStop: in profile under a fixed camera with
                             the root moved by the clip's travel (crowd_anims.root_travel), a post at the planted
                             foot, and the Walk frames that come before (WalkStop) or after (WalkStart)
  treat_lineup_<Clip>.png    every walker or neighbour of the body at the clip's key frame (the costumes and props
                             against the motion): GiveTreat at the pick and the release, OpenDoorPeek at the door,
                             HoldPail held out, Cheer at the top of the hop
  treat_give_<Pair>.png      a door neighbour gives a child a sweet: both clips start together, the child stands where
                             CrowdClipTimings puts it, the sweet rides the giver's knuckles from the pick to the release
                             and drops into the pail; then the child cheers. Close from the side, three-quarter, and the
                             gameplay camera from the street
  treat_door_<Neighbour>.png OpenDoorPeek against a door leaf swung by a simple schedule (closed until the hand meets
                             its edge at PEEK_DOOR, then swinging out): the hand on the edge
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars", "export", "crowd"):
    p = os.path.join(TOOLS, sub)
    if p not in sys.path:
        sys.path.insert(0, p)

import costume as co  # noqa: E402
import craft as craft_lib  # noqa: E402
import crowd_anims as anims  # noqa: E402
import crowd_kit as kit  # noqa: E402
import crowd_review as cr  # noqa: E402
import pip_model as pm  # noqa: E402
import witness_review as wr  # noqa: E402
from crowd_kit import PAL  # noqa: E402
from pip_review import label  # noqa: E402

FPS = 30
TILE = (211, 330)
FAR = 1000  # the scene frame every pose is evaluated at (each rig's strip is offset to its own clip frame)
CHILDREN = ["SkeletonKid", "GhostKid", "RobotKid", "CatKid", "CandyCornKid", "QueenKid"]
NEIGHBOURS = ["Neighbour_Fogg", "Neighbour_Pearl", "Neighbour_AbbottA", "Neighbour_AbbottB"]
# step distances and Walk phases exactly as crowd_anims computes them (crowd_takes.py prints them)
STEP = {}


class Scene:
    def __init__(self, out_dir, samples):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.cam = co.stage(lineup=True)
        self.scene = bpy.context.scene
        self.scene.eevee.taa_render_samples = samples
        self.tmp = os.path.join(out_dir, "_tile")
        self.n = 0
        self.phone_lens = cr.game_lens(cr.PHONE[0] / cr.PHONE[1])

    def shot(self, location, target, lens, w, h, text="", size=0.07):
        self.n += 1
        cam, scene = self.cam, self.scene
        scene.render.resolution_x, scene.render.resolution_y = w, h
        cam.data.lens = lens
        cam.location = Vector(location)
        cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
        lab = label(cam, text, size) if text else None
        px = co.render_view(cam, location, target, lens, w, h, f"{self.tmp}_{self.n}.png")
        if lab:
            bpy.data.objects.remove(lab, do_unlink=True)
        return px


def pose(m, clip, frame):
    """Shows clip `clip` at its frame `frame` on model m (an NLA strip offset so every rig can show its own frame at
    the one scene frame FAR)."""
    rig = m["rig"]
    ad = rig.animation_data_create()
    ad.action = None
    for t in list(ad.nla_tracks):
        ad.nla_tracks.remove(t)
    act = m["actions"][clip]
    track = ad.nla_tracks.new()
    strip = track.strips.new(clip, int(FAR - 200), act)
    strip.frame_start = FAR - frame
    strip.frame_end = strip.frame_start + (act.frame_range[1] - act.frame_range[0])
    strip.extrapolation = "HOLD"
    strip.blend_type = "REPLACE"
    m["pose"] = (clip, frame)


def update():
    bpy.context.scene.frame_set(FAR)
    bpy.context.view_layer.update()


def bone(m, name):
    rig = m["rig"]
    return rig.matrix_world @ rig.pose.bones[name].head


def show(models, visible):
    for m in models:
        for o in m["all"]:
            o.hide_render = m not in visible


def length(m, clip):
    return cr.clip_len(m, clip)


def row(tiles, width=1688):
    return cr.row(tiles, width)


def save(rows, out):
    sheet = np.concatenate(rows[::-1], axis=0)
    co.save_sheet(sheet, out)
    print("[treat_review] sheet ->", out, flush=True)


def candy_object():
    mat = bpy.data.materials.new("Candy")
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = craft_lib.lin(PAL["candy"])
    b.inputs["Emission Color"].default_value = craft_lib.lin(PAL["candy"])
    b.inputs["Emission Strength"].default_value = 0.3
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=0.022)
    obj = pm.new_mesh_object("Sweet", bm, mat)
    obj.scale = (1.3, 1.0, 0.8)
    return obj


def post_object(height=0.45):
    """A thin post marking where a planted foot stands (for the no-slide strips)."""
    mat = bpy.data.materials.new("Post")
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0.9, 0.35, 0.1, 1.0)
    b.inputs["Emission Color"].default_value = (0.9, 0.35, 0.1, 1.0)
    b.inputs["Emission Strength"].default_value = 0.6
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.004, radius2=0.004, depth=height,
                          matrix=Matrix.Translation((0, 0, height / 2)))
    return pm.new_mesh_object("Post", bm, mat)


def door_leaf():
    """A plain door leaf 1.14 m wide (LivingStreetLayout.DoorWidth) and 2.12 m tall, pivot on its hinge edge."""
    mat = bpy.data.materials.new("Leaf")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = craft_lib.lin(PAL["plum"])
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((0.57, 0, 1.06)) @ Matrix.Diagonal((1.14, 0.05, 2.12, 1)))
    leaf = pm.new_mesh_object("Leaf", bm, mat)
    hinge = bpy.data.objects.new("Hinge", None)
    bpy.context.scene.collection.objects.link(hinge)
    leaf.parent = hinge
    return hinge, leaf


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out_dir = argv[0]

    def opt(name, default):
        return argv[argv.index(name) + 1] if name in argv else default
    folder = opt("--crowd", os.path.join(wr.MODELS, "Characters", "Crowd"))
    only = [s for s in opt("--only", "").split(",") if s]
    os.makedirs(out_dir, exist_ok=True)
    S = Scene(out_dir, int(opt("--samples", "24")))
    cr.RECIPES.update(kit.WALKERS)
    cr.RECIPES.update(kit.NEIGHBOURS)
    mats = {"Crowd": cr.crowd_material(folder, "Crowd"), kit.NEIGHBOUR_BODY: cr.crowd_material(folder, kit.NEIGHBOUR_BODY)}
    models = {}

    def model(wid):
        if wid not in models:
            body = cr.RECIPES[wid][0]
            models[wid] = cr.walker(folder, wid, mats[kit.ATLAS[body]])
            cr.place(models[wid], 0)
        return models[wid]

    def want(name):
        return not only or any(o in name for o in only)
    sweet = candy_object()
    sweet.hide_render = True
    post = post_object()
    post.hide_render = True
    phone = S.phone_lens

    def everyone():
        return list(models.values())

    def solo(m):
        show(everyone(), [m])
        sweet.hide_render = True
        post.hide_render = True

    # --- per clip ------------------------------------------------------------------------------------------------
    plan = [("GiveTreat", "Neighbour_Fogg"), ("OpenDoorPeek", "Neighbour_Pearl"), ("HoldPail", "SkeletonKid"),
            ("Cheer", "CatKid"), ("WalkStart", "SkeletonKid"), ("WalkStop", "SkeletonKid"), ("WalkStart", "SkeletonGrownUp"),
            ("WalkStop", "SkeletonGrownUp")]
    for clip, wid in plan:
        if not want(f"treat_{clip}_{wid}"):
            continue
        m = model(wid)
        solo(m)
        adult = cr.RECIPES[wid][0] != "CrowdChild"
        look = Vector((0, -0.12, 0.92 if adult else 0.56))
        dist = 4.6 if adult else 3.2
        n = length(m, clip)
        frames = sorted({round(i * n / 9) for i in range(10)})
        rows = []
        if clip in ("WalkStart", "WalkStop"):
            body = "CrowdChild" if not adult else "CrowdAdult"
            d, phase, k = STEP[(clip, body)]
            walk_n = length(m, "Walk")
            # in profile under a fixed camera: the root really travels, a post marks the planted foot
            tiles = []
            mid = Vector((0, -d / 2, 0.5 if not adult else 0.8))
            cam_at = mid + Vector((-(dist * 0.9), 0, 0.1))
            speed = STEP[("speed", body)]
            pre = []
            if clip == "WalkStop":
                for j in (3, 2, 1):  # the Walk frames before frame 0: the root travels at Walk speed
                    pre.append(("Walk", walk_n - j, -j / FPS * speed))
            post_frames = []
            if clip == "WalkStart":
                for j in (1, 2, 3):
                    post_frames.append(("Walk", round(phase * walk_n) + j, d + j / FPS * speed))
            steps = pre + [(clip, f, anims_travel(clip, f / n, d, k)) for f in range(n + 1)] + post_frames
            # the planted foot's post (from the rig in its first planted frame)
            pose(m, clip, 0 if clip == "WalkStart" else 0)
            update()
            foot = "Toe_R" if clip == "WalkStart" else "Foot_L"
            at = bone(m, foot)
            post.location = (at.x + 0.03 * (1 if clip == "WalkStart" else -1), at.y, 0)
            if clip == "WalkStop":
                post.location = (at.x, at.y - 0.05 * (1.7 if adult else 1.0), 0)
            post.hide_render = False
            for ci, f, travel in steps:
                cr.place(m, 0, 0, y=-travel)
                pose(m, ci, f)
                update()
                tiles.append(S.shot(cam_at, mid, 70, 168, 300, f"{ci} {f}" + (f"  {wid}" if not tiles else ""), 0.08))
            for i in range(0, len(tiles), 10):
                rows.append(row(tiles[i:i + 10]))
            post.hide_render = True
            cr.place(m, 0)
            tiles = []
            for f in frames:
                pose(m, clip, f)
                update()
                tiles.append(S.shot(cr.orbit(look, dist, 28, 6), look, 85, *TILE, f"{clip} {f}", 0.07))
            rows.append(row(tiles[:8]))
        else:
            for yaw, note in ((28, "3/4"), (90, "profile")):
                tiles = []
                for i, f in enumerate(frames):
                    pose(m, clip, f)
                    update()
                    tiles.append(S.shot(cr.orbit(look, dist, yaw, 6), look, 85, 168, 300,
                                        f"{f}" + (f"  {wid} {clip} {note}" if i == 0 else ""), 0.075))
                rows.append(row(tiles))
            tiles = []
            for f in frames[1::2]:
                pose(m, clip, f)
                update()
                tiles.append(S.shot(cr.orbit(look, dist, 160, 6), look, 85, 168, 300, f"back {f}", 0.075))
            rows.append(row(tiles))
        tiles = []
        key = {"GiveTreat": (12, 26, 31), "OpenDoorPeek": (9, 15, 22), "HoldPail": (12, 29, 36), "Cheer": (5, 12, 17),
               "WalkStart": (0, 6, n), "WalkStop": (0, 6, n)}[clip]
        for f in key:
            pose(m, clip, f)
            update()
            tiles.append(game_shot(S, f"game camera, frame {f}"))
        rows.append(row(tiles))
        tiles = []
        pose(m, clip, n)
        update()
        tiles.append(S.shot(cr.orbit(look, dist, 20, 6), look, 85, *TILE, f"{clip} last ({n})", 0.07))
        pose(m, "Idle", 0)
        update()
        tiles.append(S.shot(cr.orbit(look, dist, 20, 6), look, 85, *TILE, "Idle 0", 0.07))
        rows.append(row(tiles))
        save(rows, os.path.join(out_dir, f"treat_{clip}_{wid}.png"))

    # --- lineups -------------------------------------------------------------------------------------------------
    for clip, ids, frames in (("GiveTreat", NEIGHBOURS + ["KingGrownUp"], (12, 26)), ("OpenDoorPeek", NEIGHBOURS, (9,)),
                              ("HoldPail", CHILDREN, (20, 29)), ("Cheer", CHILDREN, (6, 12))):
        if not want(f"treat_lineup_{clip}"):
            continue
        ms = [model(w) for w in ids]
        show(everyone(), ms)
        sp = 0.95 if cr.RECIPES[ids[0]][0] != "CrowdChild" else 0.62
        xs = [(i - (len(ms) - 1) / 2) * sp for i in range(len(ms))]
        rows = []
        for f in frames:
            for m, x in zip(ms, xs):
                cr.place(m, x)
                pose(m, clip, f)
            update()
            adult = cr.RECIPES[ids[0]][0] != "CrowdChild"
            mid = Vector((0, 0, 0.9 if adult else 0.55))
            tiles = [S.shot(cr.orbit(mid, 9.0 if adult else 6.5, 18, 5), mid, 50, 844, 420,
                            f"{clip} frame {f}: " + ", ".join(ids), 0.025),
                     S.shot(cr.orbit(mid, 9.0 if adult else 6.5, 75, 5), mid, 50, 844, 420, "from the side", 0.025)]
            rows.append(row(tiles))
        for m in ms:
            cr.place(m, 0)
        save(rows, os.path.join(out_dir, f"treat_lineup_{clip}.png"))

    # --- a neighbour gives a child a sweet ----------------------------------------------------------------------
    for giver, child in (("Neighbour_Fogg", "SkeletonKid"), ("Neighbour_Pearl", "GhostKid")):
        if not want(f"treat_give_{giver}"):
            continue
        g, c = model(giver), model(child)
        show(everyone(), [g, c])
        cr.place(g, 0)
        # the child faces the giver, its pail mouth under the release point
        c_y = anims.RELEASE.y + anims.MOUTH.y  # -0.45 + -0.26
        cr.place(c, anims.RELEASE.x, 180, y=c_y)
        give_n, hold_n, cheer_n = length(g, "GiveTreat"), length(c, "HoldPail"), length(c, "Cheer")
        times = [0.0, 0.25, 0.4, 0.55, 0.7, 0.8, 0.88, 0.93, 0.97, 1.05, 1.25, 1.6, 1.75, 1.9, 2.1]
        rel_t = anims.GIVE_RELEASE * give_n / FPS
        drop_t = anims.HOLD_DROP * hold_n / FPS
        pick_t = anims.GIVE_PICK * give_n / FPS
        rows = {k: [] for k in ("side", "three", "game")}
        for t in times:
            f = round(t * FPS)
            pose(g, "GiveTreat" if f <= give_n else "Idle", min(f, give_n) if f <= give_n else f - give_n)
            if f <= hold_n:
                pose(c, "HoldPail", f)
            else:
                pose(c, "Cheer", min(cheer_n, f - hold_n))
            update()
            # the sweet: in the bowl from the pick, on the knuckles to the release, falling, then in the pail
            sweet.hide_render = t < pick_t - 0.02
            if t < rel_t:
                sweet.location = bone(g, "Fingers_L") + Vector((0, 0, -0.025))
            else:
                mouth = bone(c, "Pail") + Vector((0, 0, -anims.PAIL_RIM + 0.012))
                pose(g, "GiveTreat", round(rel_t * FPS))
                update()
                start = bone(g, "Fingers_L") + Vector((0, 0, -0.025))
                pose(g, "GiveTreat" if f <= give_n else "Idle", min(f, give_n) if f <= give_n else f - give_n)
                update()
                a = min(1.0, (t - rel_t) / max(1e-3, drop_t - rel_t))
                sweet.location = start.lerp(mouth, a) if a < 1 else bone(c, "Pail") + Vector((0, 0, -anims.PAIL_RIM + 0.012))
            mid = Vector((0.0, -0.36, 0.8))
            rows["side"].append(S.shot(cr.orbit(mid, 4.2, 90, 4), mid, 60, 168, 300, f"{t:.2f}s", 0.08))
            rows["three"].append(S.shot(cr.orbit(mid, 4.2, 40, 8), mid, 60, 168, 300, f"{t:.2f}s", 0.08))
            rows["game"].append(game_shot(S, f"game camera {t:.2f}s", Vector((0.0, -0.4, 0.0)), 30))
        out_rows = [row(rows["side"][:10]), row(rows["side"][10:]), row(rows["three"][:10]), row(rows["three"][10:])]
        g_rows = [row([rows["game"][i] for i in (2, 6, 9)]), row([rows["game"][i] for i in (11, 13, 14)])]
        sweet.hide_render = True
        save(out_rows + g_rows, os.path.join(out_dir, f"treat_give_{giver}_{child}.png"))
        cr.place(c, 0)

    # --- the door ------------------------------------------------------------------------------------------------
    for wid in ("Neighbour_Fogg",):
        if not want(f"treat_door_{wid}"):
            continue
        m = model(wid)
        solo(m)
        hinge, leaf = door_leaf()
        n = length(m, "OpenDoorPeek")
        door = anims.DOOR
        # the leaf: its latch edge at the hand's point when closed; the hinge 1.14 m to the neighbour's right
        # (the leaf runs along +X from the hinge in the model's frame); it swings out (toward -Y) once touched
        hinge.location = (door.x - 1.14 + 0.02, door.y - 0.03, 0.0)
        tiles = []
        for f in range(0, n + 1, 3):
            u = f / n
            swing = 0.0 if u < anims.PEEK_DOOR else 75.0 * cr_smooth((u - anims.PEEK_DOOR) / 0.45)
            hinge.rotation_euler = (0, 0, -math.radians(swing))  # out, toward the street (-Y)
            pose(m, "OpenDoorPeek", f)
            update()
            look = Vector((0.1, -0.2, 1.0))
            # from inside the house, behind and to the right of the neighbour: the hand meets the leaf's back
            tiles.append(S.shot(cr.orbit(look, 3.4, 215, 12), look, 60, 168, 300, f"{f}", 0.08))
        rows = [row(tiles[:10]), row(tiles[10:])]
        save(rows, os.path.join(out_dir, f"treat_door_{wid}.png"))
        bpy.data.objects.remove(leaf, do_unlink=True)
        bpy.data.objects.remove(hinge, do_unlink=True)

    # --- Pip hands a sweet --------------------------------------------------------------------------------------
    import pip_anims as pa
    if want("treat_HandTreat_Pip") or want("treat_pip_give"):
        pip = wr.import_model(opt("--pip", os.path.join(wr.MODELS, "Characters", "Pip")), "Pip", leaf_bones=True)
        cr.place(pip, 0)
        models["Pip"] = pip
        n = length(pip, "HandTreat")
        frames = sorted({round(i * n / 9) for i in range(10)})
        look = Vector((0, -0.12, 0.6))
        if want("treat_HandTreat_Pip"):
            show(everyone(), [pip])
            rows = []
            for yaw, note in ((28, "3/4"), (90, "profile"), (300, "from her right front")):
                tiles = []
                for i, f in enumerate(frames):
                    pose(pip, "HandTreat", f)
                    update()
                    tiles.append(S.shot(cr.orbit(look, 3.0, yaw, 6), look, 85, 168, 300,
                                        f"{f}" + (f"  Pip HandTreat {note}" if i == 0 else ""), 0.075))
                rows.append(row(tiles))
            tiles = []
            for f in frames[1::2]:
                pose(pip, "HandTreat", f)
                update()
                tiles.append(S.shot(cr.orbit(look, 3.0, 160, 6), look, 85, 168, 300, f"back {f}", 0.075))
            rows.append(row(tiles))
            close = Vector((0.0, -0.25, 0.68))
            tiles = []
            for f in (12, 19, 26, 32):
                pose(pip, "HandTreat", f)
                update()
                tiles.append(S.shot(cr.orbit(close, 1.4, 35, 10), close, 60, 336, 300, f"close {f}", 0.05))
            rows.append(row(tiles))
            tiles = []
            pose(pip, "HandTreat", n)
            update()
            tiles.append(S.shot(cr.orbit(look, 3.0, 20, 6), look, 85, *TILE, f"HandTreat last ({n})", 0.07))
            pose(pip, "Idle", 0)
            update()
            tiles.append(S.shot(cr.orbit(look, 3.0, 20, 6), look, 85, *TILE, "Idle 0", 0.07))
            rows.append(row(tiles))
            save(rows, os.path.join(out_dir, "treat_HandTreat_Pip.png"))
        if want("treat_pip_give"):
            c = model("RobotKid")
            show(everyone(), [pip, c])
            at = pa.TREAT_AT
            cr.place(c, at.x, 180, y=at.y + anims.MOUTH.y)
            give_n, hold_n, cheer_n = n, length(c, "HoldPail"), length(c, "Cheer")
            rel_t = pa.TREAT_RELEASE * give_n / FPS
            pick_t = pa.TREAT_PICK * give_n / FPS
            drop_t = anims.HOLD_DROP * hold_n / FPS
            times = [0.0, 0.25, 0.4, 0.55, 0.7, 0.8, 0.88, 0.93, 0.97, 1.05, 1.25, 1.6, 1.75, 1.9, 2.1]
            rows = {k: [] for k in ("side", "three", "game")}

            def pip_at(f):
                pose(pip, "HandTreat" if f <= give_n else "Idle", f if f <= give_n else f - give_n)
            for t in times:
                f = round(t * FPS)
                pip_at(f)
                pose(c, "HoldPail", f) if f <= hold_n else pose(c, "Cheer", min(cheer_n, f - hold_n))
                update()
                sweet.hide_render = t < pick_t - 0.02
                if t < rel_t:
                    sweet.location = bone(pip, "Fingers_L") + Vector((0, 0, -0.025))
                else:
                    mouth = bone(c, "Pail") + Vector((0, 0, -anims.PAIL_RIM + 0.012))
                    pose(pip, "HandTreat", round(rel_t * FPS))
                    update()
                    start = bone(pip, "Fingers_L") + Vector((0, 0, -0.025))
                    pip_at(f)
                    update()
                    a = min(1.0, (t - rel_t) / max(1e-3, drop_t - rel_t))
                    sweet.location = start.lerp(mouth, a)
                mid = Vector((0.0, -0.32, 0.6))
                rows["side"].append(S.shot(cr.orbit(mid, 3.4, 90, 4), mid, 60, 168, 300, f"{t:.2f}s", 0.08))
                rows["three"].append(S.shot(cr.orbit(mid, 3.4, 40, 8), mid, 60, 168, 300, f"{t:.2f}s", 0.08))
                # the game camera behind Pip at her porch (the boom follows her): her back, the child's front
                rows["game"].append(game_shot(S, f"game camera {t:.2f}s", Vector((0.0, -0.3, -0.2)), 200))
            out_rows = [row(rows["side"][:10]), row(rows["side"][10:]), row(rows["three"][:10]), row(rows["three"][10:]),
                        row([rows["game"][i] for i in (2, 6, 9)]), row([rows["game"][i] for i in (11, 13, 14)])]
            sweet.hide_render = True
            save(out_rows, os.path.join(out_dir, "treat_pip_give_RobotKid.png"))
            cr.place(c, 0)


def game_shot(S, text, at=Vector((0, 0, 0)), yaw=24):
    """The gameplay camera as the child sees a porch: 3.8 m behind Pip, who stands about 1.2 m short of the subject
    (so about 5 m away), 12 degrees down, 50 degrees vertical, a phone-shaped frame (a third of 1688 wide)."""
    look = Vector(at) + Vector((0, 0, 0.75))
    return S.shot(cr.orbit(look, cr.GAME_DISTANCE + 1.2, yaw, cr.GAME_PITCH + 8), look, S.phone_lens, 562, 260, text,
                  0.04)


def cr_smooth(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def anims_travel(clip, u, d, k):
    u = min(1.0, max(0.0, u))
    return d * u ** k if clip == "WalkStart" else d * (1.0 - (1.0 - u) ** k)


def load_steps():
    """The walk transitions' travel, from the takes report crowd_takes.py writes (or the defaults below)."""
    import json
    path = os.environ.get("CROWD_TAKES_REPORT", "")
    data = json.load(open(path)) if path and os.path.exists(path) else {}
    for body in ("CrowdChild", "CrowdAdult"):
        r = data.get(body, {})
        speed = r.get("walk_speed", 1.167 if body == "CrowdChild" else 1.173)
        STEP[("speed", body)] = speed
        for clip, key in (("WalkStart", "walk_start_distance"), ("WalkStop", "walk_stop_distance")):
            d = r.get(key, 0.2)
            k = speed * (anims.STEP_FRAMES / FPS) / d
            STEP[(clip, body)] = (d, r.get("walk_start_phase", 0.85), k)


if __name__ == "__main__":
    load_steps()
    main()
