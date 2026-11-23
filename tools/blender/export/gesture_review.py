"""Contact sheets for the testimony gestures and hint glances (S003a), rendered from the RE-IMPORTED FBX files
(the files the game loads).

    blender -b -t 2 --python tools/blender/export/gesture_review.py -- <out_dir> [--chars DIR] [--env DIR]
                                    [--only Bea.ShadeEyes,Vale.CheckWatch,...] [--samples 32] [--regress]

One sheet per gesture, <out_dir>/<Character>_<Clip>.png, four rows:
  1. eight frames across the clip from the front three-quarter, close
  2. the same eight frames from the other side (or from behind for Pip, whom the game camera follows)
  3. four frames at the GAMEPLAY camera: PipMovementTuning's follow distance (3.8 m) and field of view (50 degrees
     vertical) on a phone-shaped frame (844 x 390 points), so the silhouette is judged at the size a child sees
  4. three close-ups of the hand at work, then the clip's last frame beside its rest loop's frame 0
`--regress` also writes <out_dir>/<Character>_existing.png: key frames of every clip the character already had,
to compare with the earlier review sheets. Mr. Vale sits on a stand-in bench, Bea stands by her bicycle.
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars", "export"):
    sys.path.insert(0, os.path.join(TOOLS, sub))

import costume as co  # noqa: E402
import witness_review as wr  # noqa: E402
from pip_review import label  # noqa: E402

W = 2000
GAME_DISTANCE, GAME_FOV, GAME_PITCH = 3.8, 50.0, 4.0  # PipMovementTuning / CameraOrbitTuning defaults
PHONE = (844, 390)

# per gesture: rest loop, eight frames, close-up frames, close-up camera (offset from the hand, lens), the bone
# the close-up follows, what the hand must not go through (for the eye), second-row yaw
GESTURES = {
    "Bea.ShadeEyes": dict(rest="Idle", frames=(0, 5, 9, 12, 24, 32, 39, 58), close=(12, 24, 39),
                          follow="Head", cam=((-0.5, -1.25, 0.2), 85), yaw2=-55),
    "Vale.CheckWatch": dict(rest="SitIdle", frames=(0, 7, 13, 20, 27, 35, 48, 64), close=(20, 27, 48),
                            follow="Hand_L", cam=((0.55, -1.2, 0.35), 85), yaw2=-60),
    "June.Listen": dict(rest="Idle", frames=(0, 6, 11, 16, 26, 36, 46, 58), close=(16, 26, 46),
                        follow="Head", cam=((-0.75, -1.15, 0.1), 85), yaw2=-50),
    "Kit.Ponder": dict(rest="Idle", frames=(0, 7, 13, 18, 28, 38, 48, 60), close=(18, 28, 48),
                       follow="Head", cam=((0.6, -1.25, 0.05), 85), yaw2=-50),
    "Pip.GlanceLeft": dict(rest="Idle", frames=(0, 3, 6, 9, 14, 22, 30, 38), close=(9, 14, 22),
                           follow="Head", cam=((0.5, -1.3, 0.1), 85), yaw2=180),
    "Pip.GlanceRight": dict(rest="Idle", frames=(0, 3, 6, 9, 14, 22, 30, 38), close=(9, 14, 22),
                            follow="Head", cam=((-0.5, -1.3, 0.1), 85), yaw2=180),
}
LOOK = {"Bea": 0.78, "Vale": 0.86, "June": 0.9, "Kit": 0.88, "Pip": 0.66}  # look-at height
DIST = {"Bea": 5.2, "Vale": 5.4, "June": 5.9, "Kit": 5.9, "Pip": 4.7}
EXISTING = {
    "Bea": [("Idle", 0), ("Idle", 27), ("Idle", 65), ("Talk", 0), ("Talk", 15), ("Talk", 45),
            ("Wave", 11), ("Wave", 24), ("Point", 7), ("Point", 14), ("Point", 25), ("Point", 47)],
    "Vale": [("SitIdle", 0), ("SitIdle", 45), ("SitTalk", 0), ("SitTalk", 15), ("SitTalk", 30), ("SitTalk", 45),
             ("Wave", 11), ("Wave", 19), ("Wave", 26), ("Wave", 33)],
    "June": [("Idle", 0), ("Idle", 56), ("Talk", 0), ("Talk", 15), ("KneeTap", 6), ("KneeTap", 36),
             ("Wave", 11), ("Wave", 24), ("Point", 14), ("Point", 24), ("Walk", 0), ("Walk", 8)],
    "Kit": [("Idle", 0), ("Idle", 45), ("Talk", 0), ("Talk", 15), ("Talk", 30), ("OpenBooth", 9),
            ("OpenBooth", 22), ("OpenBooth", 40), ("Wave", 11), ("Wave", 24)],
    "Pip": [("Idle", 0), ("Idle", 37), ("Walk", 0), ("Walk", 5), ("Run", 3), ("JumpStart", 4), ("Fall", 0),
            ("Land", 2), ("Glide", 0), ("LedgeHang", 0), ("Roll", 8), ("Interact", 13)],
}


def game_lens(aspect):
    """Blender lens (36 mm sensor across the width) for the game's vertical field of view on that frame."""
    half_w = math.atan(math.tan(math.radians(GAME_FOV / 2)) * aspect)
    return 18.0 / math.tan(half_w)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out_dir = argv[0]

    def opt(name, default):
        return argv[argv.index(name) + 1] if name in argv else default
    chars = opt("--chars", os.path.join(wr.MODELS, "Characters"))
    env = opt("--env", os.path.join(wr.MODELS, "Environment"))
    only = [g for g in opt("--only", ",".join(GESTURES)).split(",") if g]
    regress = "--regress" in argv
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cam = co.stage()
    scene = bpy.context.scene
    scene.eevee.taa_render_samples = int(opt("--samples", "32"))
    bpy.data.objects["Ground"].scale = (8, 8, 1)
    who = sorted({g.split(".")[0] for g in only})
    models = {n: wr.import_model(os.path.join(chars, n), n, leaf_bones=True) for n in who}
    bike = wr.import_model(env, "BeaBicycle") if "Bea" in who else None
    if bike:
        for o in bike["all"]:
            if o.parent is None:
                o.matrix_world = (Matrix.Translation(wr.BIKE_AT) @ Matrix.Rotation(math.radians(wr.BIKE_YAW), 4, "Z")
                                  @ o.matrix_world)
    bench = wr.stand_in_bench()
    tmp = os.path.join(out_dir, "_tile")
    count = [0]

    def show(name):
        for n, m in models.items():
            for o in m["all"]:
                o.hide_render = n != name
        if bike:
            for o in bike["all"]:
                o.hide_render = name != "Bea"
        bench.hide_render = name != "Vale"

    def set_pose(m, clip, frame):
        rig = m["rig"]
        rig.animation_data_create()
        act = m["actions"][clip]
        rig.animation_data.action = act
        scene.frame_set(int(act.frame_range[0]) + frame)
        bpy.context.view_layer.update()

    def joint(m, bone):
        return m["rig"].matrix_world @ m["rig"].pose.bones[bone].head

    def shot(location, target, lens, w, h, text="", size=0.03):
        count[0] += 1
        lab = None
        if text:
            scene.render.resolution_x, scene.render.resolution_y = w, h
            cam.data.lens = lens
            cam.location = Vector(location)
            cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
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

    for key in only:
        name, clip = key.split(".")
        g, m = GESTURES[key], models[name]
        if clip not in m["actions"]:
            print(f"[gesture_review] {name}.fbx has no {clip} take: skipped")
            continue
        show(name)
        length = int(m["actions"][clip].frame_range[1] - m["actions"][clip].frame_range[0])
        target = Vector((0, -0.05 if name == "Vale" else 0, LOOK[name]))
        rows = []
        front = 25 if g["cam"][0][0] < 0 else -25  # the working hand's side toward the camera
        for yaw, note in ((front, f"{name} {clip}  {length} frames at 30 fps"), (g["yaw2"], "")):
            tiles = []
            for i, frame in enumerate(g["frames"]):
                set_pose(m, clip, frame)
                tiles.append(shot(orbit(target, DIST[name], yaw, 8), target, 85, 250, 400,
                                  f"{frame}" + (f"  {note}" if i == 0 and note else ""), 0.075))
            rows.append(row(tiles))
        # the gameplay camera: behind and above the player, the character where Pip's talk distance puts them
        tiles = []
        lens = game_lens(PHONE[0] / PHONE[1])
        for frame in (g["frames"][0], g["frames"][3], g["frames"][4], g["frames"][6]):
            set_pose(m, clip, frame)
            if name == "Pip":  # the camera follows her from behind
                look = Vector((0, 0, 0.9))
                at = orbit(look, GAME_DISTANCE, 180, GAME_PITCH + 8)
            else:  # Pip stands about 1.6 m in front of the witness; the camera is behind Pip, off to one side
                look = Vector((0, -1.0, 0.9))
                at = orbit(look, GAME_DISTANCE, -22 if front < 0 else 22, GAME_PITCH + 8)
            tiles.append(shot(at, look, lens, 500, 231, f"game camera  {frame}", 0.035))
        rows.append(row(tiles))
        tiles = []
        offset, lens = g["cam"]
        for frame in g["close"]:
            set_pose(m, clip, frame)
            at = joint(m, g["follow"]) + Vector((0, 0, 0.1 if g["follow"] == "Head" else 0.0))
            tiles.append(shot(at + Vector(offset), at, lens, 400, 400, f"{frame}", 0.05))
        set_pose(m, clip, length)
        tiles.append(shot(orbit(target, DIST[name], front, 8), target, 85, 250, 400, f"last frame {length}", 0.075))
        set_pose(m, g["rest"], 0)
        tiles.append(shot(orbit(target, DIST[name], front, 8), target, 85, 250, 400, f"{g['rest']} 0", 0.075))
        rows.append(row(tiles))
        out = os.path.join(out_dir, f"{name}_{clip}.png")
        co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
        print("[gesture_review] sheet ->", out)
    if regress:
        for name in who:
            m = models[name]
            show(name)
            target = Vector((0, -0.05 if name == "Vale" else 0, LOOK[name]))
            tiles = []
            for i, (clip, frame) in enumerate(EXISTING[name]):
                set_pose(m, clip, frame)
                tiles.append(shot(orbit(target, DIST[name], 25 if i % 2 == 0 else -40, 8), target, 85, 250, 400,
                                  f"{clip} {frame}", 0.075))
            rows = [row(tiles[i:i + 8]) for i in range(0, len(tiles), 8)]
            out = os.path.join(out_dir, f"{name}_existing.png")
            co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
            print("[gesture_review] sheet ->", out)


if __name__ == "__main__":
    main()
