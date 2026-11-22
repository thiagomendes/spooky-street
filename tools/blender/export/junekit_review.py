"""Review sheet for June and Aunt Kit, rendered from the RE-IMPORTED FBX files (proof that the export works).

    blender -b -t 2 --python tools/blender/export/junekit_review.py -- <out.png> [--chars DIR] [--who June,Kit]
                                                                        [--only rows] [--samples 32] [--tiles a,b] [--tile WxH]

Imports DIR/June/June.fbx and DIR/Kit/Kit.fbx (default: the game's Resources folder) into an empty scene with
plain Principled materials using the baked textures (see witness_review.import_model), and renders, per
character: a five-view turnaround in the bind pose, face close-ups and costume details (June's torn cape; Aunt
Kit's chin, knotted scarf, owl brooch and lantern), then key poses from every clip. June's seated KneeTap is
shown on a stand-in stool with its seat at junekit_anims.JUNE_SEAT. `--only` takes a comma list of row names
(turn, face, clips, extra); `--tiles a,b,c` keeps only those clip tiles (quick looks).
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
REPO = os.path.dirname(os.path.dirname(TOOLS))
for sub in ("style", "pip", "chars", "export"):
    sys.path.insert(0, os.path.join(TOOLS, sub))

import costume as co  # noqa: E402
import junekit_anims as anims  # noqa: E402
import pip_model as pm  # noqa: E402
from pip_review import label  # noqa: E402
from witness_review import import_model  # noqa: E402

MODELS = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models")
W = 2000  # sheet width

# clip, frame, camera yaw (0 = front, 90 = the character's right side, negative = her left), camera pitch
STRIPS = {
    "June": [("Idle", 0, 20, 8), ("Idle", 56, -30, 8), ("Idle", 56, 150, 10), ("Talk", 0, 25, 8),
             ("Talk", 15, 70, 8), ("Talk", 30, -40, 8),
             ("KneeTap", 3, 30, 12), ("KneeTap", 6, 30, 12), ("KneeTap", 33, 90, 8), ("KneeTap", 36, -40, 14),
             ("KneeTap", 54, 150, 16), ("Wave", 14, 15, 8),
             ("Wave", 22, 15, 8), ("Wave", 22, 150, 10), ("Point", 14, 30, 10), ("Point", 24, 90, 8),
             ("Point", 30, -30, 10), ("Point", 24, 180, 12)],
    "Kit": [("Idle", 0, 20, 8), ("Idle", 45, -35, 8), ("Idle", 22, 160, 10), ("Talk", 0, -25, 8),
            ("Talk", 15, -70, 8), ("Talk", 30, 30, 8),
            ("OpenBooth", 9, 20, 8), ("OpenBooth", 22, 0, 8), ("OpenBooth", 22, 60, 8), ("OpenBooth", 30, -40, 10),
            ("OpenBooth", 30, 180, 10), ("OpenBooth", 48, 20, 8),
            ("Wave", 11, -15, 8), ("Wave", 19, -15, 8), ("Wave", 24, -110, 8), ("Wave", 30, 160, 10)],
}
FRAMES = {"June": dict(look=0.86, dist=6.4, head=1.36, face_d=1.55), "Kit": dict(look=0.86, dist=6.0, head=1.5, face_d=1.6)}
SEATED = {("June", "KneeTap")}
TAP = anims.TAP_FRAMES[0]


def stand_in_stool():
    """A plain round stool with its seat at anims.JUNE_SEAT."""
    mat = bpy.data.materials.new("StoolStandIn")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.16, 0.1, 0.22, 1)
    mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
    bm = bmesh.new()
    seat = anims.JUNE_SEAT
    bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=0.15, radius2=0.15, depth=0.04,
                          matrix=Matrix.Translation((0, 0.05, seat - 0.02)))
    for k in range(3):
        a = math.tau * k / 3 + 0.5
        bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.018, radius2=0.018, depth=seat - 0.04,
                              matrix=Matrix.Translation((0.1 * math.cos(a), 0.05 + 0.1 * math.sin(a), (seat - 0.04) / 2)))
    return pm.new_mesh_object("StoolStandIn", bm, mat, smooth=False)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out = argv[0]

    def opt(name, default):
        return argv[argv.index(name) + 1] if name in argv else default
    chars = opt("--chars", os.path.join(MODELS, "Characters"))
    only = set(opt("--only", "turn,face,clips,extra").split(","))
    who = opt("--who", "June,Kit").split(",")
    keep = opt("--tiles", "")
    keep = {int(k) for k in keep.split(",")} if keep else None
    tw, th = (int(x) for x in opt("--tile", "333x440").split("x"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cam = co.stage()
    scene = bpy.context.scene
    scene.eevee.taa_render_samples = int(opt("--samples", "32"))
    bpy.data.objects["Ground"].scale = (2.5, 2.5, 1)
    models = {n: import_model(os.path.join(chars, n), n) for n in who}
    stool = stand_in_stool()
    tmp = os.path.splitext(out)[0]
    count = [0]

    def show(name, with_stool=False):
        for n, m in models.items():
            for o in m["all"]:
                o.hide_render = n != name
        stool.hide_render = not with_stool

    def set_pose(m, clip, frame):
        rig = m["rig"]
        rig.animation_data_create()
        rig.animation_data.action = m["actions"][clip] if clip else None
        if clip is None:
            for pb in rig.pose.bones:
                pb.matrix_basis.identity()
        scene.frame_set(int(m["actions"][clip].frame_range[0]) + frame if clip else 0)

    def shot(location, target, lens, w, h, text="", size=0.03):
        count[0] += 1
        scene.render.resolution_x, scene.render.resolution_y = w, h
        cam.data.lens = lens
        lab = label(cam, text, size) if text else None
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

    rows = []
    for name in who:
        m, fr = models[name], FRAMES[name]
        hz = fr["head"]
        if "turn" in only:
            show(name)
            set_pose(m, None, 0)
            tiles = []
            for i, yaw in enumerate((0, 35, 90, 145, 180)):
                a = math.radians(yaw)
                target = Vector((0, 0, fr["look"]))
                loc = target + Vector((fr["dist"] * math.sin(a), -fr["dist"] * math.cos(a), 0.3))
                text = (f"{name}.fbx re-import  {m['tris']} tris  atlas {m['sizes'][0]} px  face {m['sizes'][1]} px"
                        if i == 0 else "")
                tiles.append(shot(loc, target, 85, 400, 600, text, 0.026))
            rows.append(row(tiles))
        if "face" in only:
            show(name)
            set_pose(m, None, 0)
            tiles = [shot((-0.42, -fr["face_d"], hz + 0.1), (0.0, 0.0, hz + 0.005), 85, 560, 520, name + " face"),
                     shot((0.75, -1.25, hz + 0.12), (0.0, 0.0, hz), 85, 440, 520, "3/4")]
            if name == "June":
                tiles += [shot((1.2, -2.6, 0.75), (0.2, -0.1, 0.42), 85, 560, 520, "cape tear (her left front panel)"),
                          shot(orbit((0, 0, 1.3), 2.5, -55, 10), (0, 0, 1.27), 85, 440, 520, "hat, hair, braid")]
            else:
                tiles += [shot(orbit((0, 0, hz - 0.02), 1.45, 100, 6), (0, 0, hz - 0.02), 85, 440, 520,
                               "chin, neck, knotted scarf"),
                          shot(orbit((0, 0, hz - 0.02), 1.5, -150, 14), (0, 0, hz - 0.02), 85, 260, 520, "scarf, behind"),
                          shot((-0.85, -2.3, 1.15), (-0.1, 0.0, 0.93), 60, 300, 520, "brooch, lantern")]
            rows.append(row(tiles))
        if "clips" in only:
            tiles = []
            for i, (clip, frame, yaw, pitch) in enumerate(STRIPS[name]):
                if keep is not None and i not in keep:
                    continue
                seated = (name, clip) in SEATED
                show(name, with_stool=seated)
                set_pose(m, clip, frame)
                target = (0, -0.1, 0.62) if seated else (0, 0, fr["look"])
                tiles.append(shot(orbit(target, 6.2, yaw, pitch), target, 85, tw, th, f"{clip} {frame}", 0.06))
            per = max(1, W // tw)
            for k in range(0, len(tiles), per):
                rows.append(row(tiles[k:k + per]))
        if "extra" in only:
            if name == "June":
                show(name, with_stool=True)
                set_pose(m, "KneeTap", TAP)
                t = (0, -0.12, 0.6)
                tiles = [shot(orbit(t, 4.3, 22, 12), t, 85, 700, 620, f"June on a stand-in stool, seat {anims.JUNE_SEAT} m "
                                                                       f"(KneeTap {TAP}: the tap lands)"),
                         shot(orbit(t, 4.3, -70, 8), t, 85, 650, 620, "her left: torn panel"),
                         shot(orbit(t, 4.3, 160, 18), t, 85, 650, 620, "behind")]
            else:
                show(name)
                set_pose(m, "OpenBooth", 26)
                t = (0, 0, 1.0)
                tiles = [shot(orbit(t, 5.0, 12, 8), t, 85, 760, 620, "Aunt Kit, OpenBooth 26 (the ta-da)"),
                         shot(orbit(t, 5.0, 70, 8), t, 85, 620, 620, "her right: lantern"),
                         shot(orbit(t, 5.0, -150, 12), t, 85, 620, 620, "behind")]
            rows.append(row(tiles))
    sheet = np.concatenate(rows[::-1], axis=0)  # image rows run bottom-up
    co.save_sheet(sheet, out)
    print("[junekit_review] sheet ->", out, sheet.shape)


if __name__ == "__main__":
    main()
