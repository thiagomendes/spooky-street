"""Pip, modelled from scratch for Spooky Street (spec §2, §6; docs/art-bible.md). No external assets.

Pip is a child of about 8–12 in a homemade moth costume under a short amber rain cape: a cream fuzzy hood
with oversized feathery antennae, stitched paper wings with eyespots, a striped moth sweater, striped socks,
round boots, a satchel with a little torch. Shapes are built from code (skin-modifier body, lathed cape,
outlined wings, curve antennae) and smoothed with subdivision so the silhouette reads as a soft, premium toy.

Usage (headless):
    blender -b --python tools/blender/pip/pip_model.py -- --turnaround <out.png>

Conventions: metres, Z up, Pip faces -Y, feet on Z = 0, total height ≈ 1.1 m without antennae.
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Vector

# --- Palette (docs/art-bible.md) -------------------------------------------------------------------------

def hex_rgb(value):
    return ((value >> 16) & 255) / 255.0, ((value >> 8) & 255) / 255.0, (value & 255) / 255.0


PALETTE = {
    "fuzz": hex_rgb(0xF4E6C8),        # warm paper: hood, sweater, ruff
    "fuzz_shadow": hex_rgb(0xE2CCA2),
    "stripe": hex_rgb(0x9C6B45),      # moth-body stripes
    "cape": hex_rgb(0xD48C3E),        # amber rain cape
    "cape_trim": hex_rgb(0x9E5F2A),
    "skin": hex_rgb(0xF0C9A0),
    "cheek": hex_rgb(0xEE9F82),
    "ink": hex_rgb(0x1E2233),
    "brow": hex_rgb(0x5A3A28),
    "white": (1.0, 1.0, 1.0),
    "wing": hex_rgb(0xD9C49C),
    "wing_rim": hex_rgb(0x7A5A40),
    "amber": hex_rgb(0xF6AD58),
    "tights": hex_rgb(0x2B3044),
    "boot": hex_rgb(0x6B4A36),
    "sole": hex_rgb(0x3A2A22),
    "sock_a": hex_rgb(0xF6AD58),
    "sock_b": hex_rgb(0x394461),
    "leather": hex_rgb(0x7D5236),
    "torch": hex_rgb(0x394461),
}


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


_MATERIALS = {}

# Optional surface treatment: callable(key, rgb, roughness, emission, sheen) -> Material or None.
# Set by tools/blender/style/* to render the same design in an art-direction style.
MATERIAL_FACTORY = None


def material(key, roughness=0.75, emission=0.0, sheen=0.0):
    if key in _MATERIALS:
        return _MATERIALS[key]
    rgb = PALETTE[key]
    if MATERIAL_FACTORY is not None:
        styled = MATERIAL_FACTORY(key, rgb, roughness, emission, sheen)
        if styled is not None:
            _MATERIALS[key] = styled
            return styled
    mat = bpy.data.materials.new("Pip_" + key)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    lin = tuple(srgb_to_linear(c) for c in rgb) + (1.0,)
    bsdf.inputs["Base Color"].default_value = lin
    bsdf.inputs["Roughness"].default_value = roughness
    if sheen > 0 and "Sheen Weight" in bsdf.inputs:
        bsdf.inputs["Sheen Weight"].default_value = sheen
    if emission > 0:
        bsdf.inputs["Emission Color"].default_value = lin
        bsdf.inputs["Emission Strength"].default_value = emission
    mat.diffuse_color = lin
    _MATERIALS[key] = mat
    return mat


# --- Object helpers ---------------------------------------------------------------------------------------

def link(obj, parent=None):
    bpy.context.scene.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def new_mesh_object(name, bm, mat, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    obj = bpy.data.objects.new(name, me)
    obj.data.materials.append(mat)
    return link(obj)


def ellipsoid(name, mat, center, radii, rotation=(0, 0, 0), segments=24, rings=16, subsurf=1):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)
    rot = Euler(rotation).to_matrix()
    for v in bm.verts:
        v.co = rot @ Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2])) + Vector(center)
    obj = new_mesh_object(name, bm, mat)
    if subsurf:
        obj.modifiers.new("Smooth", "SUBSURF").levels = subsurf
    return obj


def torus(name, mat, center, major, minor, rotation=(0, 0, 0), segments=48, sides=14, fluff=0.0, squash=1.0):
    bm = bmesh.new()
    verts = []
    for i in range(segments):
        a = 2 * math.pi * i / segments
        ring = []
        for j in range(sides):
            b = 2 * math.pi * j / sides
            r = major + minor * math.cos(b)
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), minor * squash * math.sin(b))))
        verts.append(ring)
    for i in range(segments):
        for j in range(sides):
            a = verts[i][j]
            b = verts[(i + 1) % segments][j]
            c = verts[(i + 1) % segments][(j + 1) % sides]
            d = verts[i][(j + 1) % sides]
            bm.faces.new((a, b, c, d))
    rot = Euler(rotation).to_matrix()
    for v in bm.verts:
        v.co = rot @ v.co + Vector(center)
    obj = new_mesh_object(name, bm, mat)
    if fluff > 0:
        add_fluff(obj, fluff)
    obj.modifiers.new("Smooth", "SUBSURF").levels = 1
    return obj


def add_fluff(obj, strength, scale=0.035):
    tex = bpy.data.textures.get("PipFluff") or bpy.data.textures.new("PipFluff", "CLOUDS")
    tex.noise_scale = scale
    tex.noise_depth = 2
    disp = obj.modifiers.new("Fluff", "DISPLACE")
    disp.texture = tex
    disp.strength = strength
    disp.mid_level = 0.5


def curve_tube(name, mat, points, radius, taper=(1.0, 0.6), resolution=10, cyclic=False):
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius
    cu.bevel_resolution = 4
    cu.resolution_u = resolution
    cu.use_fill_caps = True
    spline = cu.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    spline.use_cyclic_u = cyclic
    for i, p in enumerate(points):
        bp = spline.bezier_points[i]
        bp.co = Vector(p)
        bp.handle_left_type = bp.handle_right_type = "AUTO"
        t = i / max(1, len(points) - 1)
        bp.radius = taper[0] + (taper[1] - taper[0]) * t
    obj = bpy.data.objects.new(name, cu)
    obj.data.materials.append(mat)
    return link(obj)


# --- Body: one skin-modifier mesh, materials assigned by region after evaluation --------------------------

def build_body():
    nodes = {
        "pelvis": ((0.0, 0.0, 0.40), (0.125, 0.10)),
        "belly": ((0.0, -0.005, 0.50), (0.142, 0.118)),
        "chest": ((0.0, 0.0, 0.60), (0.128, 0.10)),
        "neck": ((0.0, 0.0, 0.70), (0.05, 0.05)),
    }
    for side, s in (("L", 1), ("R", -1)):
        nodes["hip" + side] = ((s * 0.072, 0.0, 0.37), (0.058, 0.058))
        nodes["knee" + side] = ((s * 0.082, -0.005, 0.22), (0.044, 0.044))
        nodes["ankle" + side] = ((s * 0.085, 0.0, 0.085), (0.036, 0.036))
        nodes["shoulder" + side] = ((s * 0.118, 0.0, 0.635), (0.046, 0.046))
        nodes["elbow" + side] = ((s * 0.198, -0.01, 0.53), (0.036, 0.036))
        nodes["wrist" + side] = ((s * 0.252, -0.025, 0.435), (0.03, 0.03))
    edges = [("pelvis", "belly"), ("belly", "chest"), ("chest", "neck")]
    for side in "LR":
        edges += [("pelvis", "hip" + side), ("hip" + side, "knee" + side), ("knee" + side, "ankle" + side),
                  ("chest", "shoulder" + side), ("shoulder" + side, "elbow" + side), ("elbow" + side, "wrist" + side)]
    names = list(nodes)
    me = bpy.data.meshes.new("PipBodySkin")
    me.from_pydata([nodes[n][0] for n in names], [(names.index(a), names.index(b)) for a, b in edges], [])
    obj = bpy.data.objects.new("PipBodySkin", me)
    link(obj)
    skin = obj.modifiers.new("Skin", "SKIN")
    skin.use_smooth_shade = True
    skin.branch_smoothing = 0.6
    if not me.skin_vertices:
        me.skin_vertices.new()
    for i, n in enumerate(names):
        sv = me.skin_vertices[0].data[i]
        sv.radius = nodes[n][1]
        sv.use_root = n == "pelvis"
    obj.modifiers.new("Smooth", "SUBSURF").levels = 2

    # Bake the modifiers into a real mesh, then paint regions with materials.
    deps = bpy.context.evaluated_depsgraph_get()
    baked = bpy.data.meshes.new_from_object(obj.evaluated_get(deps))
    bpy.data.objects.remove(obj, do_unlink=True)
    body = bpy.data.objects.new("Pip_Body", baked)
    link(body)
    mats = ["fuzz", "stripe", "tights"]
    for key in mats:
        body.data.materials.append(material(key, roughness=0.85 if key != "tights" else 0.6, sheen=0.4))
    for poly in baked.polygons:
        c = poly.center
        is_arm = abs(c.x) > 0.125 and c.z > 0.40
        if c.z < 0.345 and not is_arm:
            poly.material_index = 2  # tights
        elif not is_arm and (0.43 < c.z < 0.462 or 0.50 < c.z < 0.532):
            poly.material_index = 1  # moth stripes on the sweater
        elif is_arm and 0.455 < c.z < 0.47:
            poly.material_index = 1  # cuff stripe
        else:
            poly.material_index = 0
        poly.use_smooth = True
    return body


# --- Head and face -------------------------------------------------------------------------------------------

HEAD_C = Vector((0.0, 0.0, 0.875))
HEAD_R = 0.185


def build_head():
    head = ellipsoid("Pip_Head", material("skin", 0.6), HEAD_C, (HEAD_R * 1.02, HEAD_R * 0.96, HEAD_R * 0.94),
                     segments=32, rings=20, subsurf=1)
    face_y = HEAD_C.y - HEAD_R * 0.96
    # Big expressive eyes: glossy ink with two highlights.
    for s in (1, -1):
        ex = s * 0.07
        ellipsoid("Pip_Eye" + ("L" if s > 0 else "R"), material("ink", 0.12), (ex, face_y + 0.022, 0.878),
                  (0.034, 0.016, 0.046), rotation=(0, 0, -s * 0.12), subsurf=1)
        ellipsoid("Pip_EyeShine", material("white", 0.2, emission=0.2), (ex + s * 0.012, face_y + 0.006, 0.897),
                  (0.011, 0.006, 0.013), subsurf=0)
        ellipsoid("Pip_EyeShineSmall", material("white", 0.2, emission=0.2), (ex - s * 0.012, face_y + 0.008, 0.861),
                  (0.006, 0.004, 0.006), subsurf=0)
        # Brows: short soft arcs, slightly raised (curious).
        curve_tube("Pip_Brow", material("brow", 0.7), [
            (ex - s * 0.03, face_y + 0.032, 0.935), (ex, face_y + 0.024, 0.946), (ex + s * 0.028, face_y + 0.03, 0.94)],
            0.0075, taper=(0.7, 1.0))
        ellipsoid("Pip_Cheek", material("cheek", 0.8), (s * 0.108, face_y + 0.045, 0.835), (0.026, 0.008, 0.016),
                  rotation=(0, 0, -s * 0.5), subsurf=0)
    ellipsoid("Pip_Nose", material("skin", 0.6), (0.0, face_y - 0.004, 0.848), (0.017, 0.014, 0.013), subsurf=1)
    curve_tube("Pip_Mouth", material("ink", 0.5), [
        (-0.026, face_y + 0.018, 0.818), (0.0, face_y + 0.008, 0.808), (0.026, face_y + 0.018, 0.818)],
        0.0045, taper=(0.8, 0.8))
    return head


def build_hood():
    """Cream fuzzy moth hood: a shell around the head with the face cut out, fluffy rim and ruff."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=26, radius=1.0)
    centre = HEAD_C + Vector((0.0, 0.016, 0.022))
    radii = (HEAD_R * 1.15, HEAD_R * 1.1, HEAD_R * 1.1)
    for v in bm.verts:
        v.co = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2])) + centre
    # Face opening: an oval on the front.
    doomed = [f for f in bm.faces if f.calc_center_median().y < centre.y - 0.07 and
              (f.calc_center_median().x / 0.142) ** 2 + ((f.calc_center_median().z - 0.865) / 0.15) ** 2 < 1.0]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    # Neck opening at the bottom.
    doomed = [f for f in bm.faces if f.calc_center_median().z < centre.z - radii[2] * 0.82]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    hood = new_mesh_object("Pip_Hood", bm, material("fuzz", 0.9, sheen=0.6))
    sol = hood.modifiers.new("Thickness", "SOLIDIFY")
    sol.thickness = 0.018
    sol.offset = 1.0
    add_fluff(hood, 0.006, scale=0.03)
    hood.modifiers.new("Smooth", "SUBSURF").levels = 2
    # Fluffy rim around the face and a ruff collar: the moth's signature fuzz.
    torus("Pip_HoodRim", material("fuzz", 0.95, sheen=0.7), (0.0, centre.y - 0.155, 0.866), 0.148, 0.026,
          rotation=(math.radians(90), 0, 0), fluff=0.012, squash=1.0)
    torus("Pip_Ruff", material("fuzz", 0.95, sheen=0.7), (0.0, 0.0, 0.705), 0.105, 0.045, fluff=0.018, squash=0.75)
    # Fuzzy moth crest down the back of the hood, and stitched panel seams (handmade costume).
    hood_c = HEAD_C + Vector((0.0, 0.016, 0.022))
    for k, (theta, r) in enumerate(((8, 0.042), (32, 0.05), (58, 0.052), (84, 0.048), (108, 0.04))):
        t = math.radians(theta)
        pos = hood_c + Vector((0.0, math.sin(t) * HEAD_R * 1.16, math.cos(t) * HEAD_R * 1.14))
        tuft = ellipsoid("Pip_HoodCrest", material("fuzz_shadow", 0.95, sheen=0.7), pos, (r * 0.85, r * 0.75, r),
                         rotation=(t, 0, 0), subsurf=1)
        add_fluff(tuft, 0.012, scale=0.02)
    for sx in (0.085, -0.085):
        seam = [(sx * 0.9, -0.12, 1.03), (sx * 1.15, 0.02, 1.075), (sx * 1.2, 0.14, 1.0), (sx * 1.15, 0.2, 0.86),
                (sx, 0.19, 0.76)]
        curve_tube("Pip_HoodSeam", material("fuzz_shadow", 0.9), seam, 0.0035, taper=(1.0, 1.0))
    return hood


def build_antennae():
    """Oversized feathery moth antennae (spec: ~30% larger than life)."""
    for s in (1, -1):
        base = Vector((s * 0.07, -0.01, 1.05))
        pts = [base, base + Vector((s * 0.03, -0.035, 0.11)), base + Vector((s * 0.1, -0.06, 0.2)),
               base + Vector((s * 0.15, -0.045, 0.27))]
        curve_tube("Pip_AntennaStem", material("brow", 0.6), pts, 0.011, taper=(1.0, 0.55))
        tip_c = pts[-1] + Vector((s * 0.035, 0.0, 0.045))
        # Feathered tip: a soft leaf with comb-like barbs on both sides.
        ellipsoid("Pip_AntennaLeaf", material("fuzz", 0.9, sheen=0.5), tip_c, (0.024, 0.009, 0.092),
                  rotation=(0, math.radians(-s * 38), 0), subsurf=1)
        axis = Euler((0, math.radians(-s * 38), 0)).to_matrix() @ Vector((0, 0, 1))
        side_axis = Euler((0, math.radians(-s * 38), 0)).to_matrix() @ Vector((1, 0, 0))
        for k in range(11):
            t = (k - 5) / 5.0
            root = tip_c + axis * (t * 0.08)
            length = 0.05 * (1 - abs(t) * 0.55)
            for side_sign in (1, -1):
                tip = root + side_axis * side_sign * length + axis * 0.012
                curve_tube("Pip_AntennaBarb", material("fuzz_shadow", 0.9), [root, tip], 0.0045, taper=(1.0, 0.4),
                           resolution=2)


def build_cape():
    """Short amber rain cape, open at the front, scalloped hem with a darker stitched trim."""
    profile = [(0.07, 0.715), (0.135, 0.69), (0.2, 0.64), (0.232, 0.575), (0.248, 0.51), (0.255, 0.465)]
    segments = 64
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        ring = []
        for i in range(segments + 1):
            open_half = math.radians(32)
            # Leave the front open: skip angles near the front by squeezing the parameter range.
            a = -math.pi / 2 + open_half + (2 * math.pi - 2 * open_half) * i / segments
            zz = z
            if (r, z) == profile[-1]:
                zz += 0.014 * math.cos(9 * a)  # scalloped hem
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), zz)))
        rings.append(ring)
    for k in range(len(rings) - 1):
        for i in range(segments):
            bm.faces.new((rings[k][i], rings[k][i + 1], rings[k + 1][i + 1], rings[k + 1][i]))
    cape = new_mesh_object("Pip_Cape", bm, material("cape", 0.55))
    cape.data.materials.append(material("cape_trim", 0.6))
    for poly in cape.data.polygons:
        if poly.center.z < 0.5:
            poly.material_index = 1
    sol = cape.modifiers.new("Thickness", "SOLIDIFY")
    sol.thickness = 0.012
    sol.offset = 1.0
    bev = cape.modifiers.new("Soft", "SUBSURF")
    bev.levels = 2
    # Toggle fastenings at the throat.
    for s in (1, -1):
        ellipsoid("Pip_CapeToggle", material("leather", 0.5), (s * 0.04, -0.075, 0.69), (0.012, 0.01, 0.022),
                  rotation=(0, 0, 0), subsurf=1)
    curve_tube("Pip_CapeCord", material("leather", 0.5), [(-0.04, -0.08, 0.69), (0.0, -0.088, 0.684),
                                                         (0.04, -0.08, 0.69)], 0.004)
    return cape


def wing_outline(points, thickness, mat, name):
    bm = bmesh.new()
    verts = [bm.verts.new((x, 0.0, z)) for x, z in points]
    bm.faces.new(verts)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    res = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
    moved = [e for e in res["geom"] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, verts=moved, vec=(0.0, thickness, 0.0))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    obj = new_mesh_object(name, bm, mat, smooth=False)
    bev = obj.modifiers.new("Bevel", "BEVEL")
    bev.width = thickness * 0.45
    bev.segments = 3
    return obj


def smooth_outline(control, samples=12):
    """Closed Catmull-Rom through control points, for organic wing shapes."""
    pts = []
    n = len(control)
    for i in range(n):
        p0, p1, p2, p3 = (Vector(control[(i + k - 1) % n] + (0,)) for k in range(4))
        for s in range(samples):
            t = s / samples
            t2, t3 = t * t, t * t * t
            p = 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
            pts.append((p.x, p.y))
    return pts


def build_wings():
    fore = smooth_outline([(0.0, 0.0), (0.09, 0.09), (0.22, 0.155), (0.31, 0.135), (0.32, 0.06), (0.24, -0.02),
                           (0.1, -0.04)])
    hind = smooth_outline([(0.0, -0.02), (0.13, -0.05), (0.21, -0.12), (0.17, -0.21), (0.08, -0.19), (0.02, -0.1)])
    wings = []
    for s in (1, -1):
        label = "L" if s > 0 else "R"
        pivot = bpy.data.objects.new("Pip_Wing_" + label, None)
        link(pivot)
        pivot.location = (s * 0.035, 0.205, 0.6)
        pivot.rotation_euler = (math.radians(-12), math.radians(s * 0), math.radians(s * 22))
        for outline, tag, veins in ((fore, "Fore", [(0.27, 0.13), (0.3, 0.05), (0.2, -0.02)]),
                                    (hind, "Hind", [(0.18, -0.15), (0.1, -0.17)])):
            mirrored = [(s * x, z) for x, z in outline]
            body = wing_outline(mirrored, 0.01, material("wing", 0.8, sheen=0.3), f"Pip_Wing{tag}_{label}")
            body.parent = pivot
            # Stitched piping around the edge, visible from both sides.
            piping = curve_tube(f"Pip_Wing{tag}Piping_{label}", material("wing_rim", 0.7),
                                [(x, 0.005, z) for x, z in mirrored[::2]], 0.0065, taper=(1.0, 1.0), resolution=3,
                                cyclic=True)
            piping.parent = pivot
            # Painted veins from the root, on both faces.
            for vx, vz in veins:
                for face_y in (-0.002, 0.012):
                    vein = curve_tube("Pip_WingVein", material("wing_rim", 0.7), [
                        (0.0, face_y, 0.0), (s * vx * 0.5, face_y, vz * 0.55 + 0.01), (s * vx * 0.92, face_y, vz * 0.92)],
                        0.0028, taper=(1.0, 0.3), resolution=6)
                    vein.parent = pivot
        # Eyespot on the forewing: ink ring, amber iris, paper glint (rhymes with the round attic window).
        for face_y, out in ((-0.004, -1), (0.014, 1)):
            spot = Vector((s * 0.19, face_y, 0.075))
            for radius, key, dy in ((0.05, "ink", 0.0), (0.034, "amber", 0.002), (0.011, "white", 0.004)):
                disc = ellipsoid("Pip_WingSpot_" + key, material(key, 0.5), spot + Vector((0, out * dy, 0)),
                                 (radius, 0.004, radius), subsurf=0)
                disc.parent = pivot
        wings.append(pivot)
    return wings


def build_hands_and_feet():
    for s in (1, -1):
        label = "L" if s > 0 else "R"
        ellipsoid("Pip_Hand_" + label, material("skin", 0.6), (s * 0.272, -0.035, 0.4), (0.043, 0.038, 0.049),
                  rotation=(0, math.radians(-s * 15), 0))
        ellipsoid("Pip_Thumb_" + label, material("skin", 0.6), (s * 0.25, -0.066, 0.416), (0.017, 0.016, 0.024),
                  rotation=(0, 0, 0))
        # Striped socks peeking above round boots.
        for k, key in enumerate(("sock_a", "sock_b", "sock_a")):
            torus("Pip_Sock", material(key, 0.8), (s * 0.085, 0.0, 0.1 + k * 0.022), 0.036, 0.011, segments=24,
                  sides=8)
        ellipsoid("Pip_Boot_" + label, material("boot", 0.45), (s * 0.088, -0.022, 0.05), (0.052, 0.082, 0.05))
        ellipsoid("Pip_Sole_" + label, material("sole", 0.7), (s * 0.088, -0.022, 0.016), (0.055, 0.086, 0.016),
                  subsurf=1)


def build_satchel():
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * 0.085, v.co.y * 0.045, v.co.z * 0.075)) + Vector((-0.165, -0.025, 0.43))
    bag = new_mesh_object("Pip_Satchel", bm, material("leather", 0.55), smooth=True)
    bev = bag.modifiers.new("Bevel", "BEVEL")
    bev.width = 0.016
    bev.segments = 4
    ellipsoid("Pip_SatchelFlap", material("boot", 0.5), (-0.165, -0.05, 0.452), (0.046, 0.008, 0.03), subsurf=1)
    ellipsoid("Pip_SatchelButton", material("amber", 0.4), (-0.165, -0.06, 0.44), (0.009, 0.005, 0.009), subsurf=0)
    curve_tube("Pip_Strap", material("leather", 0.55), [(0.11, -0.04, 0.66), (0.05, -0.115, 0.58),
                                                         (-0.06, -0.135, 0.5), (-0.15, -0.06, 0.465)], 0.008)
    # Little torch clipped to the strap; its lens glows amber.
    ellipsoid("Pip_Torch", material("torch", 0.4), (0.055, -0.128, 0.585), (0.016, 0.03, 0.016),
              rotation=(math.radians(20), 0, 0))
    ellipsoid("Pip_TorchLens", material("amber", 0.3, emission=4.0), (0.055, -0.158, 0.59), (0.013, 0.004, 0.013),
              subsurf=0)


def build_pip(reset=True):
    if reset:
        bpy.ops.wm.read_factory_settings(use_empty=True)
    _MATERIALS.clear()
    root = bpy.data.objects.new("Pip", None)
    link(root)
    build_body()
    build_head()
    build_hood()
    build_antennae()
    build_cape()
    build_wings()
    build_hands_and_feet()
    build_satchel()
    for obj in list(bpy.context.scene.objects):
        if obj is not root and obj.parent is None:
            obj.parent = root
    return root


# --- Turnaround render -------------------------------------------------------------------------------------

def setup_stage():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 48
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance = 0.15
    scene.eevee.use_soft_shadows = True
    scene.eevee.use_bloom = True
    scene.eevee.bloom_intensity = 0.03
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    world = bpy.data.worlds.new("Stage")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.045, 0.06, 0.11, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    scene.world = world

    def area(name, loc, energy, color, size):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = color
        data.size = size
        obj = bpy.data.objects.new(name, data)
        link(obj)
        obj.location = loc
        direction = Vector((0, 0, 0.6)) - Vector(loc)
        obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

    area("Key", (-1.6, -2.4, 2.2), 260, (1.0, 0.86, 0.7), 1.6)       # warm porch light
    area("Rim", (1.8, 2.4, 2.0), 320, (0.62, 0.72, 1.0), 1.2)        # cool moonlight from behind
    area("Fill", (2.4, -1.5, 0.9), 70, (0.8, 0.85, 1.0), 2.0)
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, segments=64, radius=1.2)
    ground = new_mesh_object("Ground", bm, material_ground())
    ground.location = (0, 0, 0)


def material_ground():
    mat = bpy.data.materials.new("Stage_Ground")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.06, 0.1, 1.0)
    mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
    return mat


def render_turnaround(path, width=420, height=600):
    scene = bpy.context.scene
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    cam_data = bpy.data.cameras.new("TurnCam")
    cam_data.lens = 85
    cam = bpy.data.objects.new("TurnCam", cam_data)
    link(cam)
    scene.camera = cam
    target = Vector((0, 0, 0.66))
    tiles = []
    tmp = os.path.splitext(path)[0]
    for i, yaw in enumerate((0, 35, 90, 145, 180)):
        a = math.radians(yaw)
        dist = 4.1
        cam.location = target + Vector((dist * math.sin(a), -dist * math.cos(a), 0.35))
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = f"{tmp}_{i}.png"
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(scene.render.filepath)
        px = np.array(img.pixels[:], dtype=np.float32).reshape(height, width, 4)
        tiles.append(px)
        bpy.data.images.remove(img)
        os.remove(scene.render.filepath)
    sheet = np.concatenate(tiles, axis=1)
    out = bpy.data.images.new("Turnaround", sheet.shape[1], sheet.shape[0], alpha=True)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    print("turnaround ->", path)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    build_pip()
    if "--turnaround" in argv:
        setup_stage()
        render_turnaround(argv[argv.index("--turnaround") + 1])


if __name__ == "__main__":
    main()
