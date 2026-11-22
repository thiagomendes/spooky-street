"""Review sheet for the exported witnesses, rendered from the RE-IMPORTED FBX files (proof that the export works).

    blender -b -t 2 --python tools/blender/export/witness_review.py -- <out.png> [--chars DIR] [--env DIR]
                                                                        [--only rows] [--samples 32]

Imports DIR/Bea/Bea.fbx, DIR/Vale/Vale.fbx and ENV/BeaBicycle.fbx (defaults: the game's Resources folders) into an
empty scene, gives the meshes plain Principled materials using the baked textures, and renders, per character: a
five-view turnaround in the bind pose, a face close-up and a detail, then key poses from every clip. Bea is
shown next to her bicycle; Mr. Vale's seated clips are shown on a stand-in bench with its seat at
witness_anims.SEAT. `--only` takes a comma list of row names (turn, face, clips, extra) for quick looks.
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
import pip_model as pm  # noqa: E402
import witness_anims as anims  # noqa: E402
from pip_review import label  # noqa: E402

MODELS = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models")
W = 2000  # sheet width

# clip, frame, camera yaw (0 = front, 90 = the character's right side), camera pitch
STRIPS = {
    "Bea": [("Idle", 0, 20, 8), ("Idle", 27, 20, 8), ("Idle", 65, 60, 6), ("Idle", 71, 180, 8),
            ("Talk", 0, -25, 8), ("Talk", 15, -25, 8), ("Talk", 30, 60, 6), ("Talk", 45, -100, 8),
            ("Wave", 11, 15, 8), ("Wave", 19, 15, 8), ("Wave", 24, 150, 8), ("Wave", 34, -60, 8)],
    "Vale": [("SitIdle", 0, 25, 10), ("SitIdle", 45, -35, 10), ("SitIdle", 22, 90, 6), ("SitIdle", 67, 150, 14),
             ("SitTalk", 0, 25, 10), ("SitTalk", 15, -60, 8), ("SitTalk", 30, 90, 6), ("SitTalk", 45, -25, 30),
             ("Wave", 11, 20, 10), ("Wave", 19, 20, 10), ("Wave", 26, -70, 8), ("Wave", 33, 160, 12)],
}
# Where the bicycle stands next to Bea in the review (the prop faces -Y at its origin): to her right, turned so
# its front wheel is by her hand, as in the approved lineup.
BIKE_AT, BIKE_YAW = Vector((-0.86, 0.36, 0.0)), 66.0
BENCH_X = -0.28  # the stand-in bench's centre relative to Mr. Vale (he sits a little to one side of it)


def import_model(folder, name, textures=("BaseColor", "Normal", "Face")):
    before = set(bpy.data.objects)
    before_actions = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=os.path.join(folder, name + ".fbx"), automatic_bone_orientation=False,
                             use_anim=True, ignore_leaf_bones=True)
    new = [o for o in bpy.data.objects if o not in before]
    rig = next((o for o in new if o.type == "ARMATURE"), None)
    meshes = [o for o in new if o.type == "MESH"]
    if rig is not None:
        # Blender's importer re-connects bones whose head sits on the parent's tail, which would discard their
        # translation keys here (Unity has no such notion): disconnect them all.
        bpy.context.view_layer.objects.active = rig
        bpy.ops.object.mode_set(mode="EDIT")
        for eb in rig.data.edit_bones:
            eb.use_connect = False
        bpy.ops.object.mode_set(mode="OBJECT")

    def image(suffix, data=False):
        path = os.path.join(folder, f"{name}_{suffix}.png")
        if not os.path.exists(path):
            return None
        img = bpy.data.images.load(path)
        if data:
            img.colorspace_settings.name = "Non-Color"
        return img
    base, normal, face = image("BaseColor"), image("Normal", True), image("Face")
    for obj in meshes:
        is_face = obj.name.endswith("_Face")
        mat = bpy.data.materials.new("Review_" + obj.name)
        mat.use_nodes = True
        mat.use_backface_culling = True  # as in game (URP Lit): a missing face shows as a hole here too
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        bsdf = nodes["Principled BSDF"]
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = face if is_face else base
        links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.5 if is_face else 0.9
        if not is_face and normal is not None:
            tn = nodes.new("ShaderNodeTexImage")
            tn.image = normal
            nm = nodes.new("ShaderNodeNormalMap")
            links.new(tn.outputs["Color"], nm.inputs["Color"])
            links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        obj.data.calc_loop_triangles()
    actions = {a.name.split("|")[-1]: a for a in bpy.data.actions if a not in before_actions}
    tris = sum(len(o.data.loop_triangles) for o in meshes)
    sizes = (base.size[0] if base else 0, face.size[0] if face else 0)
    print(f"[witness_review] imported {name}: {[o.name for o in meshes]} tris {tris} actions {sorted(actions)}")
    return {"name": name, "rig": rig, "meshes": meshes, "all": new, "actions": actions, "tris": tris, "sizes": sizes}


def stand_in_bench():
    """A plain bench with its seat at anims.SEAT, the depth and back of the game's Bench.fbx."""
    mat = bpy.data.materials.new("BenchStandIn")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.16, 0.1, 0.22, 1)
    mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
    bm = bmesh.new()
    seat = anims.SEAT

    def box(size, centre, tilt=0.0):
        M = Matrix.Translation(centre) @ Matrix.Rotation(math.radians(tilt), 4, "X") @ Matrix.Diagonal((*size, 1.0))
        bmesh.ops.create_cube(bm, size=1.0, matrix=M)
    box((1.5, 0.46, 0.04), (BENCH_X, -0.022, seat - 0.02))
    box((1.5, 0.03, 0.36), (BENCH_X, 0.26, seat + 0.26), -10)
    for sx in (-1, 1):
        box((0.07, 0.08, seat - 0.04), (BENCH_X + sx * 0.65, -0.2, (seat - 0.04) / 2))
        box((0.07, 0.08, seat + 0.45), (BENCH_X + sx * 0.65, 0.27, (seat + 0.45) / 2), -10)
    return pm.new_mesh_object("BenchStandIn", bm, mat, smooth=False)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out = argv[0]

    def opt(name, default):
        return argv[argv.index(name) + 1] if name in argv else default
    chars, env = opt("--chars", os.path.join(MODELS, "Characters")), opt("--env", os.path.join(MODELS, "Environment"))
    only = set(opt("--only", "turn,face,clips,extra").split(","))
    who = opt("--who", "Bea,Vale").split(",")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cam = co.stage()
    scene = bpy.context.scene
    scene.eevee.taa_render_samples = int(opt("--samples", "32"))
    ground = bpy.data.objects["Ground"]
    ground.scale = (2.5, 2.5, 1)
    models = {n: import_model(os.path.join(chars, n), n) for n in who}
    bike = import_model(env, "BeaBicycle") if "Bea" in who else None
    if bike:
        for o in bike["all"]:
            if o.parent is None:
                o.matrix_world = Matrix.Translation(BIKE_AT) @ Matrix.Rotation(math.radians(BIKE_YAW), 4, "Z") @ o.matrix_world
    bench = stand_in_bench()
    tmp = os.path.splitext(out)[0]
    count = [0]

    def show(name, with_bike=False, with_bench=False):
        for n, m in models.items():
            for o in m["all"]:
                o.hide_render = n != name
        if bike:
            for o in bike["all"]:
                o.hide_render = not with_bike
        bench.hide_render = not with_bench

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
    frames = {"Bea": dict(look=0.74, dist=4.6, head=1.2, face_d=1.5), "Vale": dict(look=0.9, dist=5.4, head=1.575, face_d=1.6)}
    for name in who:
        m, fr = models[name], frames[name]
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
                tiles.append(shot(loc, target, 85, 400, 560, text, 0.026))
            rows.append(row(tiles))
        if "face" in only:
            show(name)
            set_pose(m, None, 0)
            hz = fr["head"]
            tiles = [shot((-0.42, -fr["face_d"], hz + 0.1), (0.0, 0.0, hz + 0.005), 85, 760, 520, name + " face"),
                     shot((0.75, -1.25, hz + 0.12), (0.0, 0.0, hz), 85, 500, 520, "3/4")]
            if name == "Bea":
                tiles.append(shot((-0.9, -1.5, 0.75), (-0.05, -0.05, 0.6), 50, 740, 520, "costume + hands"))
            else:
                tiles.append(shot((-1.0, -1.5, 1.05), (-0.12, -0.05, 0.86), 50, 740, 520, "cauldron + hands"))
            rows.append(row(tiles))
        if "clips" in only:
            seated = name == "Vale"
            show(name, with_bench=seated)
            tiles = []
            for clip, frame, yaw, pitch in STRIPS[name]:
                set_pose(m, clip, frame)
                target = (0, -0.1, 0.72) if seated else (0, 0, fr["look"])
                tiles.append(shot(orbit(target, 4.9, yaw, pitch), target, 85, 333, 430, f"{clip} {frame}", 0.06))
            rows.append(row(tiles[:6]))
            rows.append(row(tiles[6:]))
        if "extra" in only:
            if name == "Bea":
                show(name, with_bike=True)
                set_pose(m, "Idle", 0)
                tiles = [shot(orbit((-0.45, 0.1, 0.7), 6.6, 10, 9), (-0.45, 0.1, 0.7), 85, 800, 560, "Bea + BeaBicycle.fbx (Idle 0)"),
                         shot(orbit((-0.45, 0.1, 0.7), 6.6, 120, 12), (-0.45, 0.1, 0.7), 85, 600, 560, ""),
                         shot(orbit((-0.86, 0.36, 0.42), 4.2, 66 + 90, 14), (-0.86, 0.36, 0.4), 85, 600, 560, "BeaBicycle.fbx")]
            else:
                show(name, with_bench=True)
                set_pose(m, "SitIdle", 0)
                t = (BENCH_X * 0.4, -0.1, 0.72)
                tiles = [shot(orbit(t, 5.6, 28, 10), t, 85, 800, 560, f"Vale on a stand-in bench, seat {anims.SEAT} m (SitIdle 0)"),
                         shot(orbit(t, 5.6, 90, 4), t, 85, 600, 560, "side"),
                         shot(orbit(t, 5.6, -150, 16), t, 85, 600, 560, "behind")]
            rows.append(row(tiles))
    sheet = np.concatenate(rows[::-1], axis=0)  # image rows run bottom-up
    co.save_sheet(sheet, out)
    print("[witness_review] sheet ->", out, sheet.shape)


if __name__ == "__main__":
    main()
