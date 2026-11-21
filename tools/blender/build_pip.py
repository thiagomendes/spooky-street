"""Build Pip's game model from the CC0 KayKit "Rogue_Hooded" base (docs/asset-provenance.md).

Run headless (Blender 4.x):
    blender -b --python tools/blender/build_pip.py -- <Rogue_Hooded.glb> <out_dir> [preview_prefix]

What it does, so the result is reviewable and reproducible:
  * removes the stock weapons and the preview sphere (Pip carries no weapons);
  * recolours the texture atlas: every green (hood, tunic, cape) becomes Pip's amber rain-cape family;
  * adds original moth-costume parts: two antennae on the head bone, two paper wings with ink eyespots on
    the chest bone (separate objects so the game can open them for the moth glide);
  * keeps only the animations the game uses and exports a binary FBX plus the recoloured atlas.
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

KEEP_ACTIONS = [
    "Idle", "Unarmed_Idle", "Walking_A", "Running_A", "Jump_Start", "Jump_Idle", "Jump_Land", "Jump_Full_Short",
    "Jump_Full_Long", "Dodge_Forward", "Interact", "PickUp", "Use_Item", "Cheer", "Sit_Chair_Down",
    "Sit_Chair_Idle", "Sit_Chair_StandUp", "Hit_A",
]
DROP_OBJECTS = ["Knife_Offhand", "1H_Crossbow", "2H_Crossbow", "Knife", "Throwable", "Icosphere"]

# Palette (docs/art-bible.md), linear-ish sRGB 0..1.
INK = (0x1E / 255, 0x22 / 255, 0x33 / 255)
WING = (0xCD / 255, 0xB8 / 255, 0x94 / 255)
PAPER = (0xF4 / 255, 0xE6 / 255, 0xC8 / 255)
CAPE_HUE = 32.0 / 360.0  # #C58A46


def solid_material(name, rgb):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*[c ** 2.2 for c in rgb], 1.0)
    bsdf.inputs["Roughness"].default_value = 0.8
    mat.diffuse_color = (*rgb, 1.0)
    return mat


def recolour_atlas(image, out_path, name, hue_target, sat_scale, sat_max, value_scale, value_add):
    w, h = image.size
    px = np.empty(w * h * 4, dtype=np.float32)
    image.pixels.foreach_get(px)
    px = px.reshape(h, w, 4)
    rgb = px[..., :3]
    maxc = rgb.max(-1)
    minc = rgb.min(-1)
    delta = maxc - minc
    sat = np.where(maxc > 0, delta / np.maximum(maxc, 1e-6), 0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.zeros_like(maxc)
    nz = delta > 1e-6
    rc = np.where(nz, (maxc - r) / np.maximum(delta, 1e-6), 0)
    gc = np.where(nz, (maxc - g) / np.maximum(delta, 1e-6), 0)
    bc = np.where(nz, (maxc - b) / np.maximum(delta, 1e-6), 0)
    hue = np.where(r == maxc, bc - gc, np.where(g == maxc, 2.0 + rc - bc, 4.0 + gc - rc))
    hue = (hue / 6.0) % 1.0
    green = nz & (hue > 70 / 360) & (hue < 175 / 360) & (sat > 0.2)
    # HSV -> RGB with the amber hue, slightly more saturated and brighter so the cape is the warm focal point.
    new_h = np.full_like(hue, hue_target)
    new_s = np.clip(sat * sat_scale, 0, sat_max)
    new_v = np.clip(maxc * value_scale + value_add, 0, 1)
    i = np.floor(new_h * 6).astype(int) % 6
    f = new_h * 6 - np.floor(new_h * 6)
    p = new_v * (1 - new_s)
    q = new_v * (1 - f * new_s)
    t = new_v * (1 - (1 - f) * new_s)
    choices_r = [new_v, q, p, p, t, new_v]
    choices_g = [t, new_v, new_v, q, p, p]
    choices_b = [p, p, t, new_v, new_v, q]
    nr = np.choose(i, choices_r)
    ng = np.choose(i, choices_g)
    nb = np.choose(i, choices_b)
    rgb[..., 0] = np.where(green, nr, r)
    rgb[..., 1] = np.where(green, ng, g)
    rgb[..., 2] = np.where(green, nb, b)
    px[..., :3] = rgb
    out = bpy.data.images.new(name, w, h, alpha=True)
    out.pixels.foreach_set(px.ravel())
    out.filepath_raw = out_path
    out.file_format = "PNG"
    out.save()
    print(f"recoloured {int(green.sum())} of {w * h} atlas pixels -> {out_path}")
    return out


def world_bbox(obj):
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    return lo, hi


def parent_to_bone(obj, arm, bone_name):
    """Parent keeping the current world transform (works in background mode, no operator context)."""
    bpy.context.view_layer.update()
    world = obj.matrix_world.copy()
    bone = arm.pose.bones[bone_name]
    tail_world = arm.matrix_world @ bone.matrix @ Matrix.Translation((0, bone.length, 0))
    obj.parent = arm
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.matrix_basis = tail_world.inverted() @ world
    bpy.context.view_layer.update()


def mesh_object(name, mesh_builder, material, location, rotation=(0, 0, 0)):
    mesh = bpy.data.meshes.new(name)
    mesh_builder(mesh)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(material)
    obj.location = location
    obj.rotation_euler = rotation
    for poly in mesh.polygons:
        poly.use_smooth = True
    return obj


def ellipsoid(radii, offset=(0, 0, 0), segments=16, rings=10):
    def build(mesh):
        import bmesh
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)
        for v in bm.verts:
            v.co = Vector((v.co.x * radii[0] + offset[0], v.co.y * radii[1] + offset[1], v.co.z * radii[2] + offset[2]))
        bm.to_mesh(mesh)
        bm.free()
    return build


def antenna(length):
    """Curved stem (ink) rising along +Z with a feathered paper tip, origin at the base."""
    def build(mesh):
        import bmesh
        bm = bmesh.new()
        steps = 8
        prev = None
        for s in range(steps + 1):
            t = s / steps
            z = length * t
            x = 0.06 * math.sin(t * math.pi * 0.9)  # gentle forward curl
            ring = bmesh.ops.create_circle(bm, cap_ends=False, radius=0.03 * (1 - 0.35 * t), segments=8)["verts"]
            for v in ring:
                v.co = Vector((v.co.x, v.co.y - x, z))
            if prev is not None:
                for k in range(len(ring)):
                    bm.faces.new((prev[k], prev[(k + 1) % len(ring)], ring[(k + 1) % len(ring)], ring[k]))
            prev = ring
        bm.to_mesh(mesh)
        bm.free()
    return build


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    src, out_dir = argv[0], argv[1]
    preview = argv[2] if len(argv) > 2 else None
    os.makedirs(out_dir, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    for name in DROP_OBJECTS:
        obj = bpy.data.objects.get(name)
        if obj:
            bpy.data.objects.remove(obj, do_unlink=True)
    for action in list(bpy.data.actions):
        clean = action.name[:-4] if action.name.endswith("_Rig") else action.name
        if clean in KEEP_ACTIONS:
            action.name = clean
        else:
            bpy.data.actions.remove(action)
    print("kept actions:", sorted(a.name for a in bpy.data.actions))

    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    arm.name = "Pip_Rig"
    # Rest pose for building; the export bakes every kept action.
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()

    # Recolour the atlas and point the shared material at it.
    atlas_mat = None
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            for slot in obj.material_slots:
                if slot.material and slot.material.use_nodes:
                    atlas_mat = slot.material
    tex_node = next(n for n in atlas_mat.node_tree.nodes if n.type == "TEX_IMAGE")
    source = tex_node.image
    # Hood and cape: bright pumpkin-amber rain cape, the warm focal point.
    cape_tex = recolour_atlas(source, os.path.join(out_dir, "pip_cape.png"), "pip_cape", CAPE_HUE, 1.0, 0.72, 1.9, 0.12)
    # Tunic and sleeves: creamy moth fuzz, so the cape reads against the body.
    fuzz_tex = recolour_atlas(source, os.path.join(out_dir, "pip_fuzz.png"), "pip_fuzz", 40 / 360, 0.35, 0.28, 1.9, 0.38)
    tex_node.image = cape_tex
    atlas_mat.name = "Pip_Cape"
    fuzz_mat = atlas_mat.copy()
    fuzz_mat.name = "Pip_Fuzz"
    next(n for n in fuzz_mat.node_tree.nodes if n.type == "TEX_IMAGE").image = fuzz_tex
    for name in ("Rogue_Body", "Rogue_ArmLeft", "Rogue_ArmRight"):
        obj = bpy.data.objects[name]
        for slot in obj.material_slots:
            slot.material = fuzz_mat

    ink = solid_material("Pip_Ink", INK)
    wing_mat = solid_material("Pip_Wing", WING)
    paper = solid_material("Pip_Paper", PAPER)

    head = bpy.data.objects["Rogue_Head_Hooded"]
    lo, hi = world_bbox(head)
    print("head bbox", tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi))
    top = hi.z
    front_y = lo.y  # the character faces -Y after glTF import
    for side in (-1, 1):
        stem = mesh_object(f"Pip_Antenna_{'L' if side < 0 else 'R'}", antenna(0.36), ink,
                           (side * 0.16, front_y * 0.25, top - 0.08), (math.radians(-12), math.radians(side * 22), 0))
        tip = mesh_object(f"Pip_AntennaTip_{'L' if side < 0 else 'R'}", ellipsoid((0.12, 0.045, 0.07)), paper,
                          (0, 0, 0))
        tip.parent = stem
        tip.location = (0, -0.05, 0.37)
        parent_to_bone(stem, arm, "head")

    body = bpy.data.objects["Rogue_Body"]
    blo, bhi = world_bbox(body)
    back_y = world_bbox(bpy.data.objects["Rogue_Cape"])[1].y
    chest_z = blo.z + (bhi.z - blo.z) * 0.72
    for side in (-1, 1):
        label = "L" if side < 0 else "R"
        # Origin at the inner edge so the game can fan the wing open around it.
        wing = mesh_object(f"Pip_Wing_{label}", ellipsoid((0.36, 0.04, 0.44), offset=(side * 0.32, 0, -0.04)),
                           wing_mat, (side * 0.12, back_y + 0.02, chest_z),
                           (0, math.radians(side * -14), math.radians(side * -24)))
        spot = mesh_object(f"Pip_WingSpot_{label}", ellipsoid((0.09, 0.012, 0.08)), ink, (0, 0, 0))
        spot.parent = wing
        spot.location = (side * 0.36, 0.035, 0.03)
        parent_to_bone(wing, arm, "chest")

    lo_all = Vector((1e9, 1e9, 1e9))
    hi_all = Vector((-1e9, -1e9, -1e9))
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            lo, hi = world_bbox(obj)
            lo_all = Vector(map(min, lo_all, lo))
            hi_all = Vector(map(max, hi_all, hi))
    print("pip bbox", tuple(round(v, 3) for v in lo_all), tuple(round(v, 3) for v in hi_all))

    if preview:
        render_previews(preview)

    out_fbx = os.path.join(out_dir, "Pip.fbx")
    bpy.ops.export_scene.fbx(
        filepath=out_fbx, use_selection=False, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
        add_leaf_bones=False, use_armature_deform_only=False, primary_bone_axis="Y", secondary_bone_axis="X",
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=1.0, path_mode="STRIP",
        embed_textures=False, mesh_smooth_type="FACE")
    print("exported", out_fbx, os.path.getsize(out_fbx), "bytes")


def render_previews(prefix):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = scene.render.resolution_y = 600
    scene.render.film_transparent = False
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 2.9
    for name, yaw in (("front", 0), ("back", 180), ("three_quarter_back", 145)):
        r = 6.0
        a = math.radians(yaw)
        cam.location = (r * math.sin(a), -r * math.cos(a), 1.2)
        cam.rotation_euler = (math.radians(88), 0, a)
        scene.render.filepath = f"{prefix}_{name}.png"
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)


main()
