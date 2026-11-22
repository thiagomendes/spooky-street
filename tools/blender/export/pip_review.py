"""Review sheet for the exported Pip, rendered from the RE-IMPORTED FBX (proof that the export works).

    blender -b -t 2 --python tools/blender/export/pip_review.py -- <out.png> [--dir DIR] [--report FILE.json]

Imports DIR/Pip.fbx (default: the game's Resources folder) into an empty scene, gives the meshes plain Principled
materials using the baked textures, and renders: a five-view turnaround, a portrait and a hands-and-shoes detail
(the same cameras as the approved pip_vampire.py sheet), then a contact strip of key poses from every clip.
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOLS))
for sub in ("style", "pip", "chars"):
    sys.path.insert(0, os.path.join(TOOLS, sub))

import costume as co  # noqa: E402

DEFAULT_DIR = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models", "Characters", "Pip")

# clip, frame, camera yaw (0 = front, 90 = Pip's right side), camera pitch, look-at height
STRIP = [
    ("Idle", 0, 25, 8, 0.62), ("Idle", 37, 25, 8, 0.62),
    ("Walk", 0, 70, 6, 0.62), ("Walk", 5, 70, 6, 0.62), ("Walk", 10, 70, 6, 0.62),
    ("Run", 0, 70, 6, 0.62), ("Run", 3, 70, 6, 0.62), ("Run", 6, 70, 6, 0.62),
    ("JumpStart", 0, 60, 6, 0.62), ("JumpStart", 4, 60, 6, 0.7), ("JumpAir", 0, 60, 6, 0.7),
    ("Fall", 0, 40, 4, 0.7), ("Land", 2, 50, 8, 0.55), ("Land", 6, 50, 8, 0.62),
    ("Glide", 0, 60, 14, 0.75), ("Glide", 15, 180, 50, 0.75),
    ("WallSlide", 0, 100, 6, 0.7), ("LedgeHang", 0, 40, 4, 0.72),
    ("Roll", 3, 80, 8, 0.45), ("Roll", 8, 80, 8, 0.45), ("Roll", 12, 80, 8, 0.45),
    ("Interact", 0, 60, 6, 0.62), ("Interact", 13, 60, 6, 0.62), ("Glide", 8, 0, 10, 0.75),
]


def import_pip(folder):
    bpy.ops.import_scene.fbx(filepath=os.path.join(folder, "Pip.fbx"), automatic_bone_orientation=False,
                             use_anim=True, ignore_leaf_bones=True)
    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]

    def image(name, data=False):
        img = bpy.data.images.load(os.path.join(folder, name))
        if data:
            img.colorspace_settings.name = "Non-Color"
        return img
    base, normal, face = image("Pip_BaseColor.png"), image("Pip_Normal.png", True), image("Pip_Face.png")
    for obj in meshes:
        is_face = "Face" in obj.name
        mat = bpy.data.materials.new("Review_" + obj.name)
        mat.use_nodes = True
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        bsdf = nodes["Principled BSDF"]
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = face if is_face else base
        links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.5 if is_face else 0.9
        if not is_face:
            tn = nodes.new("ShaderNodeTexImage")
            tn.image = normal
            nm = nodes.new("ShaderNodeNormalMap")
            links.new(tn.outputs["Color"], nm.inputs["Color"])
            links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
        obj.data.materials.clear()
        obj.data.materials.append(mat)
    return rig, meshes, (base.size[0], face.size[0])


def label(cam, text, size=0.03):
    """Text pinned to the lower-left corner of the camera frame."""
    scene = bpy.context.scene
    cu = bpy.data.curves.new("Label", "FONT")
    cu.body = text
    cu.size = 1.0
    obj = bpy.data.objects.new("Label", cu)
    scene.collection.objects.link(obj)
    mat = bpy.data.materials.get("LabelMat")
    if mat is None:
        mat = bpy.data.materials.new("LabelMat")
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = (0, 0, 0, 1)
        bsdf.inputs["Emission Color"].default_value = (1, 0.93, 0.8, 1)
        bsdf.inputs["Emission Strength"].default_value = 1.0
    cu.materials.append(mat)
    obj.parent = cam
    frame = cam.data.view_frame(scene=scene)  # corners at depth 1 … scaled to the lens
    lo = Vector((min(c.x for c in frame), min(c.y for c in frame), frame[0].z))
    width = max(c.x for c in frame) - lo.x
    depth = 0.6 / abs(lo.z)
    obj.location = Vector((lo.x + width * 0.03, lo.y + width * 0.035, lo.z)) * depth
    obj.scale = (width * size * depth,) * 3
    obj.visible_shadow = False
    return obj


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out = argv[0]
    folder = argv[argv.index("--dir") + 1] if "--dir" in argv else DEFAULT_DIR
    report = json.load(open(argv[argv.index("--report") + 1])) if "--report" in argv else None
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rig, meshes, sizes = import_pip(folder)
    for o in meshes:
        o.data.calc_loop_triangles()
    tris = sum(len(o.data.loop_triangles) for o in meshes)
    actions = {a.name.split("|")[-1]: a for a in bpy.data.actions}
    print("[pip_review] imported", [o.name for o in meshes], "tris", tris, "actions", sorted(actions))
    cam = co.stage()
    scene = bpy.context.scene
    scene.eevee.taa_render_samples = 32
    tmp = os.path.splitext(out)[0]

    def set_pose(clip, frame):
        rig.animation_data_create()
        rig.animation_data.action = actions[clip] if clip else None
        if clip is None:
            for pb in rig.pose.bones:
                pb.matrix_basis.identity()
        scene.frame_set(int(actions[clip].frame_range[0]) + frame if clip else 0)

    def shot(location, target, lens, w, h, text, tag, size=0.03):
        scene.render.resolution_x, scene.render.resolution_y = w, h
        cam.data.lens = lens
        lab = label(cam, text, size)
        px = co.render_view(cam, location, target, lens, w, h, f"{tmp}_{tag}.png")
        bpy.data.objects.remove(lab, do_unlink=True)
        return px

    # --- turnaround in the rest pose (same cameras as the approved sheet) ---
    set_pose(None, 0)
    target = Vector((0, 0, 0.64))
    tiles = []
    for i, yaw in enumerate((0, 35, 90, 145, 180)):
        a = math.radians(yaw)
        loc = target + Vector((4.0 * math.sin(a), -4.0 * math.cos(a), 0.3))
        text = f"FBX re-import  {tris} tris  atlas {sizes[0]} px  face {sizes[1]} px" if i == 0 else ""
        tiles.append(shot(loc, target, 85, 320, 472, text, f"t{i}", 0.028))
    rows = [np.concatenate(tiles, axis=1)]
    portrait = shot((-0.42, -1.4, 1.14), (0.0, 0.0, 1.045), 85, 960, 472, "", "face")
    detail = shot((-0.62, -1.2, 0.42), (0.03, -0.05, 0.25), 50, 640, 472, "", "detail")
    rows.append(np.concatenate([portrait, detail], axis=1))
    # --- contact strip ---
    tw, th, per_row = 200, 250, 8
    strip = []
    for i, (clip, frame, yaw, pitch, look) in enumerate(STRIP):
        set_pose(clip, frame)
        a, e = math.radians(yaw), math.radians(pitch)
        target = Vector((0, 0, look))
        loc = target + 6.2 * Vector((-math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
        strip.append(shot(loc, target, 85, tw, th, f"{clip} {frame}", f"s{i}", 0.085))
    for r in range(0, len(strip), per_row):
        rows.append(np.concatenate(strip[r:r + per_row], axis=1))
    sheet = np.concatenate(rows[::-1], axis=0)  # image rows run bottom-up
    co.save_sheet(sheet, out)
    print("[pip_review] sheet ->", out, sheet.shape)
    if report:
        print("[pip_review] report triangles", report["triangles"])


if __name__ == "__main__":
    main()
