"""Review of Bea's `Point` clip, rendered from the RE-IMPORTED Bea.fbx (the file the game loads).

    blender -b -t 2 --python tools/blender/export/bea_point_review.py -- <out_dir> [--chars DIR] [--env DIR]
                                                                          [--samples 32] [--only sheet,hand,arrival]

Writes into <out_dir>:
  bea_point_sheet.png    six frames of the clip from the front three-quarter (her pointing side) and six from the
                         side, then the hand close up, the view from behind and the last frame beside Idle 0
  bea_point_hand.png     the pointing hand, large, from eight sides
  bea_point_arrival.png  one frame from about the game camera's height as Pip arrives (behind her and to her
                         right), pointing at a PLACEHOLDER round-topped window (a lit stand-in, not the owl house)
It also prints where the arm and the index finger actually aim on every review frame. The committed evidence
(docs/evidence/s002/bea_point/*.jpg) is these three sheets saved as JPEG.
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
for sub in ("style", "pip", "chars", "export"):
    sys.path.insert(0, os.path.join(TOOLS, sub))

import costume as co  # noqa: E402
import pip_model as pm  # noqa: E402
import witness_review as wr  # noqa: E402
from pip_review import label  # noqa: E402

CLIP = "Point"
FRAMES = (0, 7, 11, 14, 25, 47)  # rest, wind-up, mid-strike, overshoot, top of a bounce, stepping back
HOLD = 25
W = 2000


def placeholder_window(centre, width=1.0, spring=0.95):
    """A lit round-topped pane with a pale frame, facing the character (+Y): the owl house's side window is
    1.0 m wide with a 0.95 m spring line (tools/blender/env/owl_house.py)."""
    def shape(w, s, y):
        bm = bmesh.new()
        pts = [Vector((-w / 2, y, 0)), Vector((w / 2, y, 0))]
        pts += [Vector((w / 2 * math.cos(a), y, s + w / 2 * math.sin(a)))
                for a in (math.pi * i / 16 for i in range(17))]
        bm.faces.new([bm.verts.new(p) for p in pts])
        return bm

    def mat(name, colour, glow):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = (*colour, 1)
        b.inputs["Emission Color"].default_value = (*colour, 1)
        b.inputs["Emission Strength"].default_value = glow
        return m
    out = []
    for name, grow, y, colour, glow in (("PlaceholderFrame", 0.18, -0.02, (0.9, 0.8, 0.6), 0.25),
                                        ("PlaceholderPane", 0.0, 0.0, (1.0, 0.62, 0.2), 1.6)):
        o = pm.new_mesh_object(name, shape(width + grow, spring + grow / 2, y), mat(name, colour, glow), smooth=False)
        o.location = Vector(centre) - Vector((0, 0, (spring + width / 2) / 2 + grow / 4))
        out.append(o)
    return out


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out_dir = argv[0]

    def opt(name, default):
        return argv[argv.index(name) + 1] if name in argv else default
    chars = opt("--chars", os.path.join(wr.MODELS, "Characters"))
    env = opt("--env", os.path.join(wr.MODELS, "Environment"))
    only = set(opt("--only", "sheet,hand,arrival").split(","))
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cam = co.stage()
    scene = bpy.context.scene
    scene.eevee.taa_render_samples = int(opt("--samples", "32"))
    bpy.data.objects["Ground"].scale = (8, 8, 1)
    bea = wr.import_model(os.path.join(chars, "Bea"), "Bea", leaf_bones=True)  # FingerTips_*, Toe_* are leaves
    bike = wr.import_model(env, "BeaBicycle")
    for o in bike["all"]:
        if o.parent is None:
            o.matrix_world = Matrix.Translation(wr.BIKE_AT) @ Matrix.Rotation(math.radians(wr.BIKE_YAW), 4, "Z") @ o.matrix_world
    rig = bea["rig"]
    tmp = os.path.join(out_dir, "_tile")
    count = [0]

    def set_pose(clip, frame):
        rig.animation_data_create()
        act = bea["actions"][clip]
        rig.animation_data.action = act
        scene.frame_set(int(act.frame_range[0]) + frame)
        bpy.context.view_layer.update()

    def joint(bone, tail=False):
        pb = rig.pose.bones[bone]
        return rig.matrix_world @ (pb.tail if tail else pb.head)

    def axis(bone):
        return (rig.matrix_world.to_3x3() @ rig.pose.bones[bone].y_axis).normalized()

    def aim(v):
        """(degrees above level, degrees to her left of straight ahead) of a direction; she faces -Y."""
        return (round(math.degrees(math.asin(v.z / v.length)), 1), round(math.degrees(math.atan2(v.x, -v.y)), 1))

    def shot(location, target, lens, w, h, text="", size=0.03, bike_on=False):
        count[0] += 1
        for o in bike["all"]:
            o.hide_render = not bike_on
        lab = None
        if text:
            # label() pins the text to the camera frame, so the camera must be in place first
            scene.render.resolution_x, scene.render.resolution_y = w, h
            cam.data.lens = lens
            lab = label(cam, text, size)
        px = co.render_view(cam, location, target, lens, w, h, f"{tmp}_{count[0]}.png")
        if lab:
            bpy.data.objects.remove(lab, do_unlink=True)
        return px

    def orbit(target, dist, yaw, pitch):
        a, e = math.radians(yaw), math.radians(pitch)
        return Vector(target) + dist * Vector((-math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))

    def row(tiles):
        r = np.concatenate(tiles, axis=1)
        if r.shape[1] < W:
            r = np.pad(r, ((0, 0), (0, W - r.shape[1]), (0, 0)), constant_values=0.07)
            r[:, :, 3] = 1.0
        return r[:, :W]

    for fr in sorted(set(FRAMES) | {HOLD, 13, 20, 30, 34, 41, 54}):
        set_pose(CLIP, fr)
        arm = joint("Index_R") - joint("UpperArm_R")
        print(f"[bea_point_review] frame {fr:2d}: shoulder-to-knuckle (up, left) {aim(arm)}  index finger "
              f"{aim(axis('Index_R'))}  curled fingers {aim(axis('Fingers_R'))} tips {aim(axis('FingerTips_R'))} thumb {aim(axis('Thumb_R'))}  face {aim(rig.matrix_world.to_3x3() @ rig.pose.bones['Head'].z_axis)}"
              f"  ankles L {tuple(round(c, 3) for c in joint('Foot_L'))} R {tuple(round(c, 3) for c in joint('Foot_R'))}")

    if "sheet" in only:
        rows = []
        look = Vector((0, -0.12, 0.76))
        for yaw, pitch, name in ((42, 7, "front 3/4"), (90, 4, "side")):
            tiles = []
            for fr in FRAMES:
                set_pose(CLIP, fr)
                tiles.append(shot(orbit(look, 5.6, yaw, pitch), look, 85, 333, 430, f"{CLIP} {fr}  {name}", 0.055))
            rows.append(row(tiles))
        set_pose(CLIP, HOLD)
        hand = joint("Index_R")
        tiles = [shot(orbit(hand, 0.95, 90, 6), hand, 85, 400, 400, f"hand, side  ({CLIP} {HOLD})", 0.045),
                 shot(orbit(hand, 0.95, 35, 14), hand, 85, 400, 400, "hand, front 3/4", 0.045),
                 shot(orbit(look, 5.6, 150, 9), look, 85, 333, 400, "from behind", 0.055),
                 shot(orbit(look, 5.6, -40, 7), look, 85, 333, 400, "far side", 0.055)]
        set_pose(CLIP, 54)
        tiles.append(shot(orbit(look, 5.6, 42, 7), look, 85, 267, 400, f"{CLIP} 54", 0.07))
        set_pose("Idle", 0)
        tiles.append(shot(orbit(look, 5.6, 42, 7), look, 85, 267, 400, "Idle 0", 0.07))
        rows.append(row(tiles))
        co.save_sheet(np.concatenate(rows[::-1], axis=0), os.path.join(out_dir, "bea_point_sheet.png"))

    if "hand" in only:
        set_pose(CLIP, HOLD)
        hand = joint("Index_R")
        views = ((90, 4, f"{CLIP} {HOLD}: her right side"), (30, 10, "front 3/4"), (-90, 4, "her left side"),
                 (90, 70, "from above"), (90, -60, "from below (palm)"), (0, 0, "from the front"),
                 (140, 12, "from behind, her right"), (-35, -15, "front left, low"))
        tiles = [shot(orbit(hand, 0.5, yaw, pitch), hand, 85, 500, 500, text, 0.04) for yaw, pitch, text in views]
        co.save_sheet(np.concatenate([row(tiles[4:]), row(tiles[:4])], axis=0),
                      os.path.join(out_dir, "bea_point_hand.png"))

    if "arrival" in only:
        set_pose(CLIP, HOLD)
        shoulder = joint("UpperArm_R")
        d = (joint("Index_R") - shoulder).normalized()
        placeholder_window(shoulder + d * 7.0)
        target = Vector((-0.2, -2.0, 1.35))
        eye = Vector((-2.5, 3.3, 1.5))  # behind her and to her right, about the height of Pip's camera
        px = shot(eye, target, 28, 1400, 800, f"{CLIP} {HOLD}, camera 1.5 m up on Pip's arrival side; "
                  "placeholder window 7 m along her arm", 0.014, bike_on=True)
        co.save_sheet(px, os.path.join(out_dir, "bea_point_arrival.png"))
    print("[bea_point_review] wrote", out_dir)


if __name__ == "__main__":
    main()
