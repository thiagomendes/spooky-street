"""Cast lineup for the visual identity (spec §2), modelled from scratch in the handcrafted style (pronounced).

    blender -b --python tools/blender/cast/cast.py -- <out.png>

Builds Pip (tools/blender/pip/pip_model.py), Bea with her moon-decked bicycle, June with the torn silver
comet cape, Aunt Kit with her painted owl pin and Mr. Vale in his cardboard knight costume holding the
candy bowl, side by side at true relative scale under twilight lighting.
Conventions: metres, Z up, characters face -Y, feet on Z = 0.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Euler, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(TOOLS, "style"))
sys.path.insert(0, os.path.join(TOOLS, "pip"))

import craft as craft_lib  # noqa: E402
import pip_model as pm  # noqa: E402
from style_sample import pip_factory  # noqa: E402

H = craft_lib.hex_rgb
INTENSITY = 1.2  # owner choice: pronounced handcrafted

C = {
    "paper": H(0xF4E6C8), "amber": H(0xF6AD58), "silver": H(0xDAE2EE), "indigo": H(0x394461), "ink": H(0x1E2233),
    "twilight": H(0x344767), "hedge": H(0x2E4A43), "white": (1.0, 1.0, 1.0),
    # Halloween night family (owner direction, D17)
    "night": H(0x2A2550), "purple": H(0x5B3A7A), "plum": H(0x7A3E6E), "orange": H(0xEE7F2D), "witch": H(0x8DBF3F),
    "moss": H(0x3E5F3A), "bone": H(0xF4E6C8), "corn": H(0xF6D365), "black": H(0x1E1A26),
    # Bea
    "bea_skin": H(0xC68B5E), "bea_hair": H(0x7A3B26), "bea_jacket": H(0x3E6B5E), "bea_scarf": H(0xD9A53B),
    "denim": H(0x3B4A6B), "pumpkin": H(0xE0873A), "stem": H(0x6B4A36), "bike": H(0x2F6F73), "wicker": H(0xB98A5A),
    # June
    "june_hair": H(0x2A2030), "june_dress": H(0x2C3550), "leggings": H(0x262B40), "cape_silver": H(0xDAE2EE),
    # Vale
    "vale_skin": H(0xEFC7A4), "tunic": H(0x7D4A55), "trousers": H(0x4A5068), "card": H(0xB98A5A),
    "grey_hair": H(0xD8D2C8), "plume": H(0x8E4E52), "slipper": H(0x6B4A36),
    # Kit
    "kit_skin": H(0x8D5A3B), "coat": H(0x2F5D62), "kit_sweater": H(0xF4E6C8), "brass": H(0xC9963E),
    "kit_hair": H(0xCFCAC4), "boot": H(0x5A3E2E), "brow_dark": H(0x3A2A22), "cheek": H(0xEE9F82),
}


def group(name, before, location, yaw=0.0):
    """Parent every new top-level object since `before` under an empty and place it."""
    root = bpy.data.objects.new(name, None)
    pm.link(root)
    for obj in list(bpy.context.scene.objects):
        if obj.parent is None and obj.name not in before and obj is not root:
            obj.parent = root
    root.location = location
    root.rotation_euler = (0, 0, math.radians(yaw))
    return root


def snapshot():
    return {o.name for o in bpy.context.scene.objects}


# --- shared anatomy ---------------------------------------------------------------------------------------

def skin_body(name, nodes, edges, classify, materials):
    names = list(nodes)
    me = bpy.data.meshes.new(name + "Skin")
    me.from_pydata([nodes[n][0] for n in names], [(names.index(a), names.index(b)) for a, b in edges], [])
    obj = bpy.data.objects.new(name + "Skin", me)
    pm.link(obj)
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
    deps = bpy.context.evaluated_depsgraph_get()
    baked = bpy.data.meshes.new_from_object(obj.evaluated_get(deps))
    bpy.data.objects.remove(obj, do_unlink=True)
    body = bpy.data.objects.new(name, baked)
    pm.link(body)
    for mat in materials:
        body.data.materials.append(mat)
    for poly in baked.polygons:
        poly.material_index = classify(poly.center)
        poly.use_smooth = True
    return body


def limb_edges():
    edges = [("pelvis", "belly"), ("belly", "chest"), ("chest", "neck")]
    for side in "LR":
        edges += [("pelvis", "hip" + side), ("hip" + side, "knee" + side), ("knee" + side, "ankle" + side),
                  ("chest", "shoulder" + side), ("shoulder" + side, "elbow" + side), ("elbow" + side, "wrist" + side)]
    return edges


def child_nodes(s, stock=1.0):
    n = {
        "pelvis": ((0, 0, 0.40 * s), (0.125 * s * stock, 0.10 * s * stock)),
        "belly": ((0, -0.005, 0.50 * s), (0.135 * s * stock, 0.112 * s * stock)),
        "chest": ((0, 0, 0.60 * s), (0.125 * s * stock, 0.098 * s * stock)),
        "neck": ((0, 0, 0.70 * s), (0.05 * s, 0.05 * s)),
    }
    for side, k in (("L", 1), ("R", -1)):
        n["hip" + side] = ((k * 0.072 * s, 0, 0.37 * s), (0.056 * s, 0.056 * s))
        n["knee" + side] = ((k * 0.08 * s, -0.005, 0.22 * s), (0.043 * s, 0.043 * s))
        n["ankle" + side] = ((k * 0.083 * s, 0, 0.085 * s), (0.036 * s, 0.036 * s))
        n["shoulder" + side] = ((k * 0.118 * s, 0, 0.635 * s), (0.045 * s, 0.045 * s))
        n["elbow" + side] = ((k * 0.195 * s, -0.01, 0.53 * s), (0.035 * s, 0.035 * s))
        n["wrist" + side] = ((k * 0.245 * s, -0.025, 0.435 * s), (0.03 * s, 0.03 * s))
    return n


def adult_nodes(s, belly=1.0, wrists=None):
    n = {
        "pelvis": ((0, 0, 0.74 * s), (0.17 * s, 0.13 * s)),
        "belly": ((0, -0.01, 0.9 * s), (0.18 * s * belly, 0.14 * s * belly)),
        "chest": ((0, 0, 1.06 * s), (0.17 * s, 0.12 * s)),
        "neck": ((0, 0, 1.22 * s), (0.065 * s, 0.065 * s)),
    }
    for side, k in (("L", 1), ("R", -1)):
        n["hip" + side] = ((k * 0.095 * s, 0, 0.7 * s), (0.082 * s, 0.082 * s))
        n["knee" + side] = ((k * 0.1 * s, -0.01, 0.4 * s), (0.064 * s, 0.064 * s))
        n["ankle" + side] = ((k * 0.1 * s, 0, 0.1 * s), (0.054 * s, 0.054 * s))
        n["shoulder" + side] = ((k * 0.18 * s, 0, 1.11 * s), (0.062 * s, 0.062 * s))
        if wrists:
            elbow, wrist = wrists
            n["elbow" + side] = ((k * elbow[0] * s, elbow[1] * s, elbow[2] * s), (0.046 * s, 0.046 * s))
            n["wrist" + side] = ((k * wrist[0] * s, wrist[1] * s, wrist[2] * s), (0.038 * s, 0.038 * s))
        else:
            n["elbow" + side] = ((k * 0.27 * s, -0.02, 0.92 * s), (0.048 * s, 0.048 * s))
            n["wrist" + side] = ((k * 0.31 * s, -0.05, 0.74 * s), (0.04 * s, 0.04 * s))
    return n


def face(craft, prefix, hc, r, skin, brow, mouth="smile", freckles=False, lashes=False, eye=1.0, nose=1.0):
    k = r / 0.185
    fy = hc.y - r * 0.96
    pm.ellipsoid(prefix + "Head", craft.smooth(skin, 0.55, subsurface=0.2), hc, (r * 1.02, r * 0.96, r * 0.94),
                 segments=32, rings=20)
    ink = craft.smooth(C["ink"], 0.12)
    shine = craft.smooth(C["white"], 0.2, emission=0.2)
    for s in (1, -1):
        ex = s * 0.07 * k
        ez = hc.z + 0.003 * k
        pm.ellipsoid(prefix + "Eye", ink, (ex, fy + 0.022 * k, ez), (0.031 * k * eye, 0.015 * k, 0.042 * k * eye),
                     rotation=(0, 0, -s * 0.12))
        pm.ellipsoid(prefix + "Shine", shine, (ex + s * 0.011 * k, fy + 0.006 * k, ez + 0.018 * k),
                     (0.01 * k, 0.005 * k, 0.012 * k), subsurf=0)
        pm.curve_tube(prefix + "Brow", craft.felt(brow), [
            (ex - s * 0.03 * k, fy + 0.032 * k, ez + 0.058 * k), (ex, fy + 0.024 * k, ez + 0.068 * k),
            (ex + s * 0.028 * k, fy + 0.03 * k, ez + 0.062 * k)], 0.008 * k, taper=(0.7, 1.0))
        pm.ellipsoid(prefix + "Cheek", craft.smooth(C["cheek"], 0.8), (s * 0.106 * k, fy + 0.045 * k, ez - 0.042 * k),
                     (0.024 * k, 0.008 * k, 0.015 * k), rotation=(0, 0, -s * 0.5), subsurf=0)
        if lashes:
            pm.curve_tube(prefix + "Lash", ink, [(ex + s * 0.024 * k, fy + 0.016 * k, ez + 0.03 * k),
                                                 (ex + s * 0.04 * k, fy + 0.016 * k, ez + 0.04 * k)], 0.004 * k)
    pm.ellipsoid(prefix + "Nose", craft.smooth(skin, 0.55, subsurface=0.2), (0, fy - 0.004 * k, hc.z - 0.027 * k),
                 (0.017 * k * nose, 0.014 * k * nose, 0.013 * k * nose))
    width = 0.034 if mouth == "grin" else 0.026
    drop = 0.016 if mouth == "grin" else 0.01
    pm.curve_tube(prefix + "Mouth", craft.smooth(C["ink"], 0.5), [
        (-width * k, fy + 0.02 * k, hc.z - 0.057 * k), (0, fy + 0.008 * k, hc.z - 0.057 * k - drop * k),
        (width * k, fy + 0.02 * k, hc.z - 0.057 * k)], 0.0045 * k, taper=(0.8, 0.8))
    if freckles:
        rnd = random.Random(5)
        for s in (1, -1):
            for i in range(5):
                pm.ellipsoid(prefix + "Freckle", craft.smooth(H(0x8A5236), 0.8),
                             (s * (0.05 + rnd.uniform(0, 0.05)) * k, fy + 0.02 * k,
                              hc.z - (0.02 + rnd.uniform(0, 0.03)) * k), (0.004 * k, 0.002 * k, 0.004 * k), subsurf=0)


def hands(craft, prefix, nodes, skin, scale=1.0):
    for side in "LR":
        w = Vector(nodes["wrist" + side][0])
        e = Vector(nodes["elbow" + side][0])
        d = (w - e).normalized()
        c = w + d * 0.035 * scale
        pm.ellipsoid(prefix + "Hand", craft.smooth(skin, 0.55, subsurface=0.2), c,
                     (0.04 * scale, 0.036 * scale, 0.046 * scale))
        sgn = 1 if side == "L" else -1
        pm.ellipsoid(prefix + "Thumb", craft.smooth(skin, 0.55, subsurface=0.2),
                     c + Vector((-sgn * 0.022 * scale, -0.03 * scale, 0.012 * scale)),
                     (0.015 * scale, 0.015 * scale, 0.022 * scale))


def shoes(craft, prefix, nodes, mat, sole, scale=1.0, length=1.0):
    for side in "LR":
        a = Vector(nodes["ankle" + side][0])
        pm.ellipsoid(prefix + "Shoe", mat, (a.x, a.y - 0.022 * scale, 0.05 * scale),
                     (0.054 * scale, 0.084 * scale * length, 0.05 * scale))
        pm.ellipsoid(prefix + "Sole", sole, (a.x, a.y - 0.022 * scale, 0.016 * scale),
                     (0.057 * scale, 0.088 * scale * length, 0.016 * scale))


def lathe(name, mat, profile, open_deg=0.0, segments=64, hem_wave=0.0, cut=None, inner=None):
    """Revolve (radius, z) rings around Z; optional front opening, scalloped hem, and face cut-outs."""
    bm = bmesh.new()
    rings = []
    span = 2 * math.pi - 2 * math.radians(open_deg)
    start = -math.pi / 2 + math.radians(open_deg)
    count = segments if open_deg > 0 else segments - 1
    for ri, (r, z) in enumerate(profile):
        ring = []
        for i in range(count + 1):
            a = start + span * i / segments
            zz = z + (hem_wave * math.cos(9 * a) if ri == len(profile) - 1 else 0.0)
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), zz)))
        rings.append(ring)
    for k in range(len(rings) - 1):
        n = len(rings[k])
        for i in range(n - 1 if open_deg > 0 else n):
            j = (i + 1) % n
            f = bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
            if cut and cut(f.calc_center_median()):
                bm.faces.remove(f)
    obj = pm.new_mesh_object(name, bm, mat)
    sol = obj.modifiers.new("Thick", "SOLIDIFY")
    sol.thickness = 0.012
    if inner is not None:
        obj.data.materials.append(inner)  # lining: the solidified inner shell and rim
        sol.material_offset = 1
        sol.material_offset_rim = 1
    obj.modifiers.new("Smooth", "SUBSURF").levels = 2
    return obj


def witch_hat(name, mat, base, brim, height, crown, bend=0.12, tilt=(0.0, 0.0)):
    """Pointed hat: a soft brim and a cone that bends over at the tip."""
    pm.ellipsoid(name + "Brim", mat, base, (brim, brim, brim * 0.06), rotation=(tilt[0], tilt[1], 0), subsurf=2)
    bm = bmesh.new()
    rings = []
    steps, segs = 14, 32
    for i in range(steps + 1):
        t = i / steps
        rad = crown * (1 - t) ** 0.9 + 0.004
        off = Vector((bend * t ** 2.4, 0, height * t - 0.25 * bend * t ** 3))
        centre = Vector(base) + off
        rings.append([bm.verts.new(centre + Vector((rad * math.cos(2 * math.pi * k / segs),
                                                    rad * math.sin(2 * math.pi * k / segs), 0)))
                      for k in range(segs)])
    for i in range(steps):
        for k in range(segs):
            bm.faces.new((rings[i][k], rings[i][(k + 1) % segs], rings[i + 1][(k + 1) % segs], rings[i + 1][k]))
    cone = pm.new_mesh_object(name + "Cone", bm, mat)
    cone.modifiers.new("Smooth", "SUBSURF").levels = 1
    return cone


def lobed(name, mat, centre, radii, lobes=8, depth=0.1):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=1.0)
    for v in bm.verts:
        a = math.atan2(v.co.y, v.co.x)
        f = 1.0 - depth * (1.0 - abs(math.cos(lobes / 2 * a))) ** 0.8
        v.co = Vector((v.co.x * radii[0] * f, v.co.y * radii[1] * f, v.co.z * radii[2])) + Vector(centre)
    obj = pm.new_mesh_object(name, bm, mat)
    obj.modifiers.new("Smooth", "SUBSURF").levels = 1
    return obj


def flat_shape(name, mat, outline, thickness, location, rotation=(0, 0, 0)):
    obj = pm.wing_outline(outline, thickness, mat, name)
    obj.location = location
    obj.rotation_euler = rotation
    return obj


def star_outline(r_out, r_in, points=5):
    pts = []
    for i in range(points * 2):
        a = math.pi / 2 + math.pi * i / points
        r = r_out if i % 2 == 0 else r_in
        pts.append((r * math.cos(a), r * math.sin(a)))
    return pts


def jack_face(prefix, mat, centre, size, yaw=0.0):
    """Carved/painted jack-o'-lantern face (two triangle eyes, a triangle nose, a zigzag grin) facing -Y."""
    eye = [(-0.5, -0.35), (0.5, -0.35), (0.0, 0.45)]
    mouth = [(-1.0, 0.1), (-0.6, -0.15), (-0.3, 0.05), (0.0, -0.2), (0.3, 0.05), (0.6, -0.15), (1.0, 0.1),
             (0.55, -0.45), (0.0, -0.55), (-0.55, -0.45)]
    c = Vector(centre)
    rot = (0, 0, yaw)
    for sx in (-1, 1):
        flat_shape(prefix + "JackEye", mat, [(x * size * 0.55, z * size * 0.55) for x, z in eye], 0.004,
                   c + Vector((sx * size * 0.42, 0, size * 0.35)), rot)
    flat_shape(prefix + "JackNose", mat, [(x * size * 0.3, z * size * 0.3) for x, z in eye], 0.004,
               c + Vector((0, 0, size * 0.02)), rot)
    flat_shape(prefix + "JackMouth", mat, [(x * size * 0.7, z * size * 0.7) for x, z in mouth], 0.004,
               c + Vector((0, 0, -size * 0.28)), rot)


def bat_outline(scale):
    half = [(0.0, 0.18), (0.06, 0.2), (0.12, 0.12), (0.28, 0.22), (0.5, 0.14), (0.62, 0.02), (0.52, -0.02),
            (0.44, -0.1), (0.33, -0.04), (0.24, -0.14), (0.14, -0.06), (0.05, -0.16), (0.0, -0.12)]
    return [(x * scale, z * scale) for x, z in half] + [(-x * scale, z * scale) for x, z in reversed(half[1:-1])]


def crescent(r):
    outer = [(r * math.cos(a), r * math.sin(a)) for a in [math.radians(60 + 240 * i / 16) for i in range(17)]]
    inner = [(0.45 * r + 0.75 * r * math.cos(a), 0.75 * r * math.sin(a))
             for a in [math.radians(300 - 240 * i / 16) for i in range(17)]]
    return outer + inner[1:-1]


# --- characters ---------------------------------------------------------------------------------------------

def build_bea(craft):
    s = 1.11
    nodes = child_nodes(s)
    mats = [craft.knit(C["orange"]), craft.knit(C["black"]), craft.knit(C["purple"])]

    def bea_regions(c):
        if c.z < 0.345 * s and not (abs(c.x) > 0.125 * s and c.z > 0.4 * s):
            return 2
        return int(c.z / (0.045 * s)) % 2  # classic Halloween stripes
    skin_body("Bea_Body", nodes, limb_edges(), bea_regions, mats)
    r = 0.185 * 1.03
    hc = Vector((0, 0, 0.875 * s))
    face(craft, "Bea_", hc, r, C["bea_skin"], C["bea_hair"], mouth="grin", freckles=True)
    hands(craft, "Bea_", nodes, C["bea_skin"], 1.05)
    shoes(craft, "Bea_", nodes, craft.felt(C["paper"]), craft.smooth(C["amber"], 0.6), 1.05)
    # Jacket zip as a running stitch, cuffs.
    # Pumpkin bike helmet with stem, leaf and chin strap.
    lobed("Bea_Helmet", craft.felt(C["orange"]), hc + Vector((0, 0.02, r * 0.6)), (r * 1.12, r * 1.08, r * 0.62))
    jack_face("Bea_Helmet", craft.felt(C["black"]), hc + Vector((0, -r * 0.98, r * 0.86)), 0.05)
    pm.curve_tube("Bea_HelmetStem", craft.felt(C["stem"]), [hc + Vector((0, 0.02, r * 1.15)),
                                                         hc + Vector((0.01, 0.01, r * 1.35)),
                                                         hc + Vector((0.045, 0.0, r * 1.45))], 0.018, taper=(1, 0.6))
    pm.ellipsoid("Bea_HelmetLeaf", craft.felt(C["hedge"]), hc + Vector((-0.06, 0.03, r * 1.2)), (0.06, 0.035, 0.01),
                 rotation=(0.3, -0.2, 0.5))
    for sx in (1, -1):
        pm.curve_tube("Bea_Strap", craft.smooth(C["stem"], 0.6), [hc + Vector((sx * r * 0.98, -0.02, -0.0)),
                                                                hc + Vector((sx * r * 0.8, -r * 0.55, -r * 0.65)),
                                                                hc + Vector((0, -r * 0.8, -r * 0.92))], 0.006)
    # Fringe under the helmet rim and a bouncy ponytail.
    hair = craft.felt(C["bea_hair"])
    for i, x in enumerate((-0.1, -0.05, 0.0, 0.05, 0.1)):
        tuft = pm.ellipsoid("Bea_Fringe", hair, hc + Vector((x, -r * 0.8, r * 0.52)), (0.032, 0.022, 0.024),
                            rotation=(0, 0.4 * (1 if x > 0 else -1), 0))
        pm.add_fluff(tuft, 0.006, 0.02)
    for i in range(6):
        t = i / 5
        pos = hc + Vector((0, r * (0.95 + 0.35 * t), r * (0.15 - 0.95 * t)))
        tuft = pm.ellipsoid("Bea_Ponytail", hair, pos, (0.055 * (1 - 0.35 * t), 0.05 * (1 - 0.35 * t), 0.065))
        pm.add_fluff(tuft, 0.008, 0.02)
    pm.torus("Bea_HairTie", craft.felt(C["amber"]), hc + Vector((0, r * 1.0, r * 0.12)), 0.035, 0.012,
             rotation=(math.radians(70), 0, 0), segments=20, sides=8)
    # Mustard scarf with a hanging tail.
    pm.torus("Bea_Scarf", craft.knit(C["witch"]), (0, 0, 0.7 * s), 0.09 * s, 0.04 * s, squash=0.8)
    pm.ellipsoid("Bea_ScarfTail", craft.knit(C["witch"]), (0.06, -0.1 * s, 0.6 * s), (0.035, 0.02, 0.1),
                 rotation=(0.15, 0, -0.2))


def build_bicycle(craft):
    paint = craft.smooth(C["purple"], 0.35)
    tyre = craft.smooth(C["ink"], 0.75)
    rim = craft.smooth(C["amber"], 0.4)
    spoke = craft.smooth(C["paper"], 0.5)
    for wx in (-0.46, 0.46):
        pm.torus("Bike_Tyre", tyre, (wx, 0, 0.29), 0.26, 0.03, rotation=(math.radians(90), 0, 0), segments=48, sides=10)
        pm.torus("Bike_Rim", rim, (wx, 0, 0.29), 0.235, 0.009, rotation=(math.radians(90), 0, 0), segments=48,
                 sides=8)
        pm.ellipsoid("Bike_Hub", rim, (wx, 0, 0.29), (0.025, 0.04, 0.025))
        for i in range(12):
            a = 2 * math.pi * i / 12
            pm.curve_tube("Bike_Spoke", spoke, [(wx, 0, 0.29), (wx + 0.23 * math.cos(a), 0, 0.29 + 0.23 * math.sin(a))],
                          0.0025, taper=(1, 1), resolution=1)
    bb, seat, head_t, head_b = (0, 0, 0.32), (-0.16, 0, 0.74), (0.33, 0, 0.76), (0.36, 0, 0.62)
    for a, b in ((bb, seat), (seat, head_t), (bb, head_b), (bb, (-0.46, 0, 0.29)), (seat, (-0.46, 0, 0.29)),
                 (head_b, (0.46, 0, 0.29)), (head_t, head_b)):
        pm.curve_tube("Bike_Frame", paint, [a, b], 0.018, taper=(1, 1), resolution=1)
    pm.curve_tube("Bike_Stem", paint, [head_t, (0.3, 0, 0.9)], 0.016, taper=(1, 1), resolution=1)
    pm.curve_tube("Bike_Bar", paint, [(0.34, 0.24, 0.94), (0.3, 0, 0.9), (0.34, -0.24, 0.94)], 0.013, taper=(1, 1))
    for sy in (0.24, -0.24):
        pm.ellipsoid("Bike_Grip", craft.felt(C["amber"]), (0.35, sy, 0.945), (0.02, 0.05, 0.02))
    pm.curve_tube("Bike_SeatPost", paint, [seat, (-0.18, 0, 0.82)], 0.012, taper=(1, 1), resolution=1)
    pm.ellipsoid("Bike_Seat", craft.felt(C["stem"]), (-0.19, 0, 0.84), (0.11, 0.06, 0.03))
    pm.curve_tube("Bike_Crank", craft.smooth(C["ink"], 0.4), [(0.06, -0.05, 0.25), (-0.06, 0.05, 0.39)], 0.01)
    pm.ellipsoid("Bike_Chainring", rim, bb, (0.07, 0.012, 0.07), rotation=(0, 0, 0))
    # Wicker basket on the handlebars, festooned with foil moons on cords.
    wicker = craft.knit(C["wicker"])
    for i in range(4):
        pm.torus("Bike_Basket", wicker, (0.5, 0, 0.66 + i * 0.045), 0.14 + i * 0.008, 0.024, segments=36, sides=8)
    pm.ellipsoid("Bike_BasketBase", wicker, (0.5, 0, 0.64), (0.14, 0.14, 0.02), subsurf=1)
    pm.curve_tube("Bike_BasketStrut", paint, [(0.36, 0, 0.62), (0.44, 0, 0.64)], 0.01, taper=(1, 1), resolution=1)
    foil = craft.smooth(C["silver"], 0.22)
    for sy in (0.26, -0.26):
        flat_shape("Bike_BatStreamer", craft.paper(C["black"]), bat_outline(0.11), 0.003, (0.36, sy, 1.0),
                   (0, 0.2 if sy > 0 else -0.2, 0))
    for i, (x, y, z, rot) in enumerate(((0.5, -0.16, 0.66, 0.2), (0.4, -0.2, 0.58, -0.3), (0.1, -0.03, 0.6, 0.4),
                                        (-0.2, -0.03, 0.62, -0.2))):
        moon = flat_shape("Bike_FoilMoon", foil, crescent(0.05), 0.004, (x, y, z), (0, rot, 0))
        pm.curve_tube("Bike_MoonCord", craft.smooth(C["ink"], 0.6), [(x, y, z + 0.05), (x, y + 0.01, z + 0.12)],
                      0.002, taper=(1, 1), resolution=1)


def build_june(craft):
    s = 1.27
    nodes = child_nodes(s, stock=0.9)
    mats = [craft.knit(C["purple"]), craft.knit(C["black"]), craft.knit(C["plum"])]

    def june_regions(c):
        if c.z < 0.345 * s and not (abs(c.x) > 0.12 * s and c.z > 0.4 * s):
            return 1 + int(c.z / (0.035 * s)) % 2  # striped witch stockings
        return 0
    skin_body("June_Body", nodes, limb_edges(), june_regions, mats)
    r = 0.185 * 1.06
    hc = Vector((0, 0, 0.875 * s))
    face(craft, "June_", hc, r, C["bea_skin"], C["june_hair"], mouth="smile", lashes=True, eye=0.92)
    hands(craft, "June_", nodes, C["bea_skin"], 1.1)
    shoes(craft, "June_", nodes, craft.smooth(C["ink"], 0.4), craft.smooth(H(0x14161F), 0.7), 1.1)
    # Felt skirt flare with appliquéd amber stars.
    lathe("June_Skirt", craft.felt(C["purple"]), [(0.13 * s, 0.44 * s), (0.17 * s, 0.38 * s), (0.22 * s, 0.29 * s)],
          hem_wave=0.006)
    for x, z, sc in ((-0.07, 0.52, 1.0), (0.05, 0.47, 0.8), (0.09, 0.36, 0.9), (-0.1, 0.34, 0.7)):
        flat_shape("June_Star", craft.felt(C["corn"]), star_outline(0.022 * sc, 0.01 * sc), 0.004,
                   (x * s, -0.128 * s if z > 0.4 else -0.2 * s, z * s))
    # Silver comet cape, long, open at the front, with the triangular tear (the clue: readable without zoom).
    def tear(c):
        a = math.degrees(math.atan2(c.y, c.x)) % 360
        zn = c.z / s
        return zn < 0.4 and abs(a - 222) < (0.4 - zn) * 260
    lathe("June_Cape", craft.waxed(C["cape_silver"]), [(0.07 * s, 0.72 * s), (0.15 * s, 0.68 * s), (0.2 * s, 0.6 * s),
                                                       (0.235 * s, 0.45 * s), (0.241 * s, 0.41 * s),
                                                       (0.245 * s, 0.37 * s), (0.248 * s, 0.33 * s),
                                                       (0.252 * s, 0.29 * s), (0.255 * s, 0.25 * s)],
          open_deg=36, cut=tear)
    pm.torus("June_CapeCollar", craft.felt(C["indigo"]), (0, 0, 0.715 * s), 0.08 * s, 0.022 * s, squash=0.7)
    flat_shape("June_Clasp", craft.smooth(C["amber"], 0.35), star_outline(0.03, 0.014), 0.008,
               (0, -0.09 * s, 0.69 * s))
    # Comet appliqué on the back of the cape.
    flat_shape("June_Comet", craft.felt(C["amber"]), star_outline(0.06, 0.028), 0.006, (0.05, 0.262 * s, 0.5 * s),
               (0, 0, math.radians(180)))
    for i, w in enumerate((0.03, 0.02, 0.012)):
        pm.curve_tube("June_CometTail", craft.felt(C["amber"] if i != 1 else C["paper"]), [
            (0.02 - 0.02 * i, 0.262 * s, 0.47 * s), (-0.06, 0.27 * s, 0.4 * s - 0.03 * i), (-0.13, 0.265 * s, 0.32 * s)],
            w * 0.5, taper=(1.0, 0.2))
    # Dark hair cap, side-swept fringe, long braid and a comet headband.
    hair = craft.felt(C["june_hair"])
    cap = pm.ellipsoid("June_HairCap", hair, hc + Vector((0, r * 0.12, r * 0.25)), (r * 1.07, r * 1.0, r * 0.9))
    pm.add_fluff(cap, 0.006, 0.03)
    pm.ellipsoid("June_Fringe", hair, hc + Vector((-0.05, -r * 0.8, r * 0.66)), (0.085, 0.03, 0.03),
                 rotation=(0.25, 0.2, -0.2))
    for i in range(8):
        t = i / 7
        pm.ellipsoid("June_Braid", hair, hc + Vector((0.02 * math.sin(i * 1.7), r * 1.02 + 0.03 * t,
                                                      -r * 0.5 - 0.5 * t * r * 2.2)),
                     (0.045 * (1 - 0.3 * t), 0.04, 0.05), rotation=(0, 0.35 * (1 if i % 2 else -1), 0))
    pm.torus("June_BraidTie", craft.felt(C["amber"]), hc + Vector((0, r * 1.06, -r * 2.75)), 0.028, 0.01,
             segments=20, sides=8)
    witch_hat("June_Hat", craft.felt(C["purple"]), hc + Vector((0, 0.02, r * 0.78)), 0.27, 0.42, 0.15, bend=0.14,
              tilt=(math.radians(-6), 0))
    pm.torus("June_HatBand", craft.felt(C["corn"]), hc + Vector((0, 0.02, r * 0.86)), 0.142, 0.018, squash=1.2,
             segments=40, sides=8)
    flat_shape("June_HatStar", craft.smooth(C["corn"], 0.35), star_outline(0.035, 0.016), 0.008,
               hc + Vector((0.0, 0.02 - 0.16, r * 0.88)))


def build_vale(craft):
    s = 1.0
    nodes = adult_nodes(s, belly=1.25, wrists=((0.26, -0.12, 0.94), (0.19, -0.27, 0.87)))
    mats = [craft.felt(C["purple"]), craft.knit(C["black"])]
    skin_body("Vale_Body", nodes, limb_edges(), lambda c: 1 if c.z < 0.66 else 0, mats)
    r = 0.2
    hc = Vector((0, 0, 1.42))
    face(craft, "Vale_", hc, r, C["vale_skin"], C["grey_hair"], mouth="smile", eye=0.85, nose=1.5)
    hands(craft, "Vale_", nodes, C["vale_skin"], 1.25)
    shoes(craft, "Vale_", nodes, craft.felt(C["slipper"]), craft.felt(H(0x3A2A22)), 1.3, 1.1)
    # Big grey felt moustache.
    grey = craft.felt(C["grey_hair"])
    fy = hc.y - r * 0.96
    for sx in (1, -1):
        m = pm.ellipsoid("Vale_Moustache", grey, (sx * 0.045, fy - 0.01, hc.z - 0.045), (0.055, 0.025, 0.022),
                         rotation=(0, sx * 0.35, 0))
        pm.add_fluff(m, 0.006, 0.015)
    # Cardboard knight: chest plate with drawn rivets, pauldrons, open-faced box helmet with raised visor, plume.
    card = craft.cardboard(C["card"])
    def card_box(name, centre, size, rot=(0, 0, 0)):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts:
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        obj = pm.new_mesh_object(name, bm, card, smooth=False)
        obj.location = centre
        obj.rotation_euler = rot
        bev = obj.modifiers.new("Bevel", "BEVEL")
        bev.width = 0.012
        bev.segments = 2
        return obj
    # Breastplate: a taped cardboard box worn on the chest, shoulder plates, a painted shield crest.
    card_box("Vale_Breastplate", (0, -0.01, 0.97), (0.46, 0.4, 0.42))
    for sx in (1, -1):
        card_box("Vale_ShoulderPlate", (sx * 0.22, 0, 1.17), (0.16, 0.2, 0.035), (0, sx * math.radians(-28), 0))
    for x in (-0.18, 0.18):
        for z in (1.14, 0.8):
            pm.ellipsoid("Vale_Rivet", craft.smooth(C["ink"], 0.6), (x, -0.213, z), (0.012, 0.006, 0.012), subsurf=0)
    shield = [(0.0, 0.09), (0.07, 0.08), (0.075, 0.0), (0.05, -0.06), (0.0, -0.1), (-0.05, -0.06), (-0.075, 0.0),
              (-0.07, 0.08)]
    flat_shape("Vale_Crest", craft.felt(C["orange"]), pm.smooth_outline(shield, 4), 0.008, (0, -0.218, 0.98))
    jack_face("Vale_Crest", craft.felt(C["black"]), (0, -0.224, 0.985), 0.04)
    tape = craft.paper(H(0xC9B28A))
    card_box("Vale_Tape", (0.15, -0.212, 1.12), (0.12, 0.004, 0.035), (0, 0.6, 0)).data.materials[0] = tape
    helm = []
    for name, centre, size in (("Top", (0, 0.0, hc.z + r * 1.02), (r * 2.25, r * 2.2, 0.03)),
                               ("Back", (0, r * 1.05, hc.z + 0.02), (r * 2.25, 0.03, r * 2.1)),
                               ("Left", (r * 1.1, 0.0, hc.z + 0.02), (0.03, r * 2.2, r * 2.1)),
                               ("Right", (-r * 1.1, 0.0, hc.z + 0.02), (0.03, r * 2.2, r * 2.1))):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts:
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])) + Vector(centre)
        obj = pm.new_mesh_object("Vale_Helm" + name, bm, card, smooth=False)
        bev = obj.modifiers.new("Bevel", "BEVEL")
        bev.width = 0.01
        bev.segments = 2
        helm.append(obj)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * r * 2.25, v.co.y * 0.03, v.co.z * r * 0.9))
    visor = pm.new_mesh_object("Vale_Visor", bm, card, smooth=False)
    visor.location = (0, -r * 1.12, hc.z + r * 1.05)
    visor.rotation_euler = (math.radians(-62), 0, 0)
    for i in range(3):
        pm.curve_tube("Vale_VisorSlit", craft.smooth(C["ink"], 0.6), [
            (-r * 0.8, -r * 1.35 - 0.01, hc.z + r * 1.1 + i * 0.035), (r * 0.8, -r * 1.35 - 0.01, hc.z + r * 1.1 + i * 0.035)],
            0.004, taper=(1, 1), resolution=1)
    plume = pm.ellipsoid("Vale_Plume", craft.felt(C["orange"]), (0, 0.06, hc.z + r * 1.45), (0.06, 0.24, 0.16),
                         rotation=(0.35, 0, 0))
    pm.add_fluff(plume, 0.015, 0.025)
    # The candy bowl from the opening shot, held at the belly.
    bowl_c = Vector((0, -0.31, 0.85))
    pot = craft.smooth(C["black"], 0.35)
    lathe("Vale_Cauldron", pot, [(0.02, bowl_c.z - 0.11), (0.13, bowl_c.z - 0.095), (0.185, bowl_c.z - 0.03),
                                 (0.18, bowl_c.z + 0.03), (0.155, bowl_c.z + 0.05), (0.172, bowl_c.z + 0.065)])
    bowl = bpy.data.objects["Vale_Cauldron"]
    bowl.location = (bowl_c.x, bowl_c.y, 0)
    for sx in (1, -1):
        pm.torus("Vale_CauldronHandle", pot, bowl_c + Vector((sx * 0.19, 0, 0.02)), 0.03, 0.008,
                 rotation=(0, math.radians(90), 0), segments=20, sides=6)
    pm.ellipsoid("Vale_CandyHeap", craft.smooth(C["witch"], 0.4), bowl_c + Vector((0, 0, 0.045)), (0.15, 0.15, 0.02))
    rnd = random.Random(11)
    colours = [C["orange"], C["corn"], C["purple"], C["witch"], C["bone"]]
    for i in range(14):
        a = rnd.uniform(0, math.tau)
        d = rnd.uniform(0, 0.12)
        pos = bowl_c + Vector((d * math.cos(a), d * math.sin(a), 0.03 + rnd.uniform(0, 0.03)))
        pm.ellipsoid("Vale_Candy", craft.smooth(colours[i % len(colours)], 0.35), pos, (0.028, 0.018, 0.018),
                     rotation=(0, 0, a))


def build_kit(craft):
    s = 0.95
    nodes = adult_nodes(s, belly=1.0, wrists=((0.27, -0.06, 0.92), (0.3, -0.16, 0.78)))
    mats = [craft.knit(C["kit_sweater"]), craft.knit(C["ink"])]
    skin_body("Kit_Body", nodes, limb_edges(), lambda c: 1 if c.z < 0.66 * s else 0, mats)
    r = 0.195
    hc = Vector((0, 0, 1.42 * s))
    face(craft, "Kit_", hc, r, C["kit_skin"], C["kit_hair"], mouth="smile", eye=0.85)
    hands(craft, "Kit_", nodes, C["kit_skin"], 1.2)
    shoes(craft, "Kit_", nodes, craft.smooth(C["boot"], 0.45), craft.smooth(H(0x2A1E18), 0.7), 1.25, 1.1)
    # Long felt coat with collar, amber buttons, sleeves.
    coat = craft.felt(C["purple"])
    lathe("Kit_Coat", craft.felt(C["orange"]), [(0.075, 1.23 * s), (0.19 * s, 1.16 * s), (0.215 * s, 1.02 * s), (0.215 * s, 0.84 * s),
                             (0.245 * s, 0.6 * s), (0.265 * s, 0.38 * s)], open_deg=16, hem_wave=0.004,
          inner=coat)  # solidify's offset shell faces outward here, so the lining material goes first
    for side in "LR":
        sh, el, wr = (Vector(nodes[k + side][0]) for k in ("shoulder", "elbow", "wrist"))
        pm.curve_tube("Kit_Sleeve", coat, [sh, el, wr], 0.062 * s, taper=(1.0, 0.85))
    pm.torus("Kit_Collar", coat, (0, 0, 1.21 * s), 0.11 * s, 0.035 * s, squash=0.8)
    for i in range(3):
        pm.ellipsoid("Kit_Button", craft.smooth(C["amber"], 0.35), (0.065, -0.205 * s, (0.98 - 0.13 * i) * s),
                     (0.016, 0.008, 0.016), subsurf=0)
    craft.stitches("Kit_CoatSeam", [(0.258 * s * math.cos(a), 0.268 * s * math.sin(a), 0.41 * s) for a in
                                    [(-math.pi / 2 + math.radians(16)) + (2 * math.pi - math.radians(32)) * i / 80
                                     for i in range(81)]], C["paper"], spacing=0.02, dash=0.011, radius=0.0024)
    # Amber knit scarf.
    for i, key in enumerate(("orange", "corn", "bone")):  # candy-corn scarf
        pm.torus("Kit_Scarf", craft.knit(C[key]), (0, 0, (1.16 + 0.035 * i) * s), 0.102 * s, 0.022 * s,
                 squash=0.9, segments=36, sides=8)
        pm.ellipsoid("Kit_ScarfTail", craft.knit(C[key]), (-0.07, -0.135, (1.08 - 0.07 * i) * s), (0.04, 0.02, 0.04),
                     rotation=(0.1, 0, 0.15))
    # The painted owl pin on the coat (spec §2).
    pin = Vector((0.11, -0.212 * s, 1.1 * s))
    pm.ellipsoid("Kit_OwlPin", craft.smooth(C["amber"], 0.4), pin, (0.03, 0.008, 0.034), subsurf=1)
    for sx in (1, -1):
        pm.ellipsoid("Kit_OwlEye", craft.smooth(C["paper"], 0.3), pin + Vector((sx * 0.012, -0.007, 0.008)),
                     (0.009, 0.003, 0.009), subsurf=0)
        pm.ellipsoid("Kit_OwlPupil", craft.smooth(C["ink"], 0.2), pin + Vector((sx * 0.012, -0.01, 0.008)),
                     (0.004, 0.002, 0.004), subsurf=0)
        pm.ellipsoid("Kit_OwlEar", craft.smooth(C["brass"], 0.4), pin + Vector((sx * 0.02, 0, 0.032)),
                     (0.008, 0.004, 0.012), rotation=(0, sx * 0.5, 0), subsurf=0)
    # Grey bun, pencil, and workshop goggles pushed up on the forehead.
    hair = craft.felt(C["kit_hair"])
    cap = pm.ellipsoid("Kit_HairCap", hair, hc + Vector((0, r * 0.12, r * 0.28)), (r * 1.06, r * 1.0, r * 0.88))
    pm.add_fluff(cap, 0.006, 0.03)
    bun = pm.ellipsoid("Kit_Bun", hair, hc + Vector((0, r * 0.75, r * 0.85)), (0.08, 0.075, 0.07))
    pm.add_fluff(bun, 0.008, 0.02)
    pm.curve_tube("Kit_Pencil", craft.smooth(C["amber"], 0.4), [hc + Vector((-0.1, r * 0.75, r * 0.7)),
                                                              hc + Vector((0.1, r * 0.8, r * 1.05))], 0.008, taper=(1, 1))
    # Wide-brimmed inventor-witch hat with an orange band, goggles strapped on and an owl feather.
    hat = craft.felt(C["black"])
    pm.ellipsoid("Kit_HatBrim", hat, hc + Vector((0, 0.02, r * 0.78)), (0.33, 0.33, 0.018), rotation=(-0.08, 0, 0),
                 subsurf=2)
    lathe("Kit_HatCrown", hat, [(0.15, hc.z + r * 0.76), (0.15, hc.z + r * 1.25), (0.13, hc.z + r * 1.55),
                                (0.02, hc.z + r * 1.62)])
    pm.torus("Kit_HatBand", craft.felt(C["orange"]), hc + Vector((0, 0, r * 0.9)), 0.152, 0.02, squash=1.3,
             segments=40, sides=8)
    brass = craft.smooth(C["brass"], 0.3)
    for sx in (1, -1):
        g = hc + Vector((sx * 0.055, -0.155, r * 0.95))
        pm.torus("Kit_GoggleRim", brass, g, 0.04, 0.012, rotation=(math.radians(90), 0, 0), segments=28, sides=8)
        pm.ellipsoid("Kit_GoggleLens", craft.smooth(H(0x8FC7A0), 0.05), g + Vector((0, -0.004, 0)),
                     (0.034, 0.008, 0.034), subsurf=0)
    feather = pm.ellipsoid("Kit_Feather", craft.felt(C["bone"]), hc + Vector((0.16, 0.02, r * 1.35)),
                           (0.03, 0.012, 0.13), rotation=(0, -0.5, 0))
    pm.curve_tube("Kit_FeatherVein", craft.smooth(C["stem"], 0.6), [hc + Vector((0.12, 0.008, r * 0.95)),
                                                                    hc + Vector((0.2, 0.008, r * 1.75))], 0.004)
    # Basket of treats in her hand.
    wr = Vector(nodes["wristR"][0])
    basket_c = wr + Vector((0, -0.05, -0.12))
    wicker = craft.knit(C["wicker"])
    for i in range(3):
        pm.torus("Kit_Basket", wicker, basket_c + Vector((0, 0, i * 0.04)), 0.1 + 0.006 * i, 0.02, segments=32, sides=8)
    pm.ellipsoid("Kit_BasketBase", wicker, basket_c + Vector((0, 0, -0.015)), (0.1, 0.1, 0.02))
    pm.curve_tube("Kit_BasketHandle", wicker, [basket_c + Vector((-0.1, 0, 0.08)), basket_c + Vector((0, 0, 0.2)),
                                               basket_c + Vector((0.1, 0, 0.08))], 0.012, taper=(1, 1))
    rnd = random.Random(2)
    for i in range(8):
        pm.ellipsoid("Kit_Treat", craft.smooth([C["amber"], C["paper"], C["plume"], C["bike"]][i % 4], 0.35),
                     basket_c + Vector((rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06), 0.1)),
                     (0.026, 0.018, 0.018), rotation=(0, 0, rnd.uniform(0, 3)))


# --- stage ----------------------------------------------------------------------------------------------------

def build_stage(craft, width=2600, height=1050):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 64
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance = 0.25
    scene.eevee.use_soft_shadows = True
    scene.eevee.use_bloom = True
    scene.eevee.bloom_intensity = 0.04
    scene.eevee.bloom_threshold = 1.2
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    world = bpy.data.worlds.new("Twilight")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = craft_lib.lin(C["night"])
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.5
    scene.world = world
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, segments=96, radius=6.0)
    pm.new_mesh_object("Ground", bm, craft.smooth(H(0x1E1B2A), 0.95))
    # Halloween night set dressing: a big full moon, paper bats in flight, glowing jack-o'-lanterns.
    moon = pm.ellipsoid("Moon", craft.smooth(H(0xF6E7B8), 0.5, emission=1.4), (2.3, 9.0, 2.55), (1.15, 0.05, 1.15),
                        segments=48, rings=24)
    for i, (x, z, sc, rot) in enumerate(((0.9, 3.9, 0.55, 0.2), (2.4, 3.0, 0.4, -0.3), (-2.8, 3.3, 0.45, 0.1),
                                         (-1.6, 3.8, 0.3, -0.2))):
        flat_shape("FlyingBat", craft.paper(C["black"]), bat_outline(sc), 0.004, (x, 6.0, z), (0, rot, 0))
    rnd = random.Random(9)
    for i, x in enumerate((-3.6, -0.8, 0.15, 2.3)):
        size = rnd.uniform(0.11, 0.16)
        centre = Vector((x, -0.5 - rnd.uniform(0, 0.3), size * 0.7))
        lobed("JackLantern", craft.felt(C["orange"]), centre, (size, size, size * 0.72))
        jack_face("JackLantern", craft.smooth(C["corn"], 0.4, emission=1.2), centre + Vector((0, -size * 1.07, 0.0)),
                  size * 0.55)
        pm.curve_tube("JackStem", craft.felt(C["stem"]), [centre + Vector((0, 0, size * 0.6)),
                                                          centre + Vector((0.02, 0, size * 0.9))], size * 0.1)
        glow = bpy.data.lights.new("JackGlow", "POINT")
        glow.energy = 10
        glow.color = (1.0, 0.6, 0.25)
        glow_obj = bpy.data.objects.new("JackGlow", glow)
        glow_obj.location = centre + Vector((0, -size * 1.5, 0))
        pm.link(glow_obj)

    def light(name, loc, energy, colour, size, target=(-0.4, 0, 0.9)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = colour
        data.size = size
        obj = bpy.data.objects.new(name, data)
        obj.location = loc
        obj.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        pm.link(obj)

    light("Key", (-3.0, -4.5, 4.0), 900, (1.0, 0.85, 0.68), 4.0)
    light("Rim", (2.5, 4.0, 3.5), 1300, (0.7, 0.66, 1.0), 4.0)
    light("Fill", (4.5, -3.5, 1.5), 260, (0.7, 0.78, 1.0), 5.0)
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 44
    cam = bpy.data.objects.new("Cam", cam_data)
    cam.location = (-0.75, -8.0, 1.5)
    target = Vector((-0.75, 0, 0.8))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    pm.link(cam)
    scene.camera = cam
    scene.render.resolution_x = width
    scene.render.resolution_y = height


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out = argv[0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    craft = craft_lib.Craft(INTENSITY)
    pm.MATERIAL_FACTORY = pip_factory(craft)
    pip_root = pm.build_pip(reset=False)
    # Pip's jack-o'-lantern treat pail, hanging from the right hand.
    before = snapshot()
    pail_c = Vector((-0.3, -0.07, 0.25))
    lobed("Pip_Pail", craft.felt(C["orange"]), pail_c, (0.075, 0.075, 0.062))
    jack_face("Pip_Pail", craft.felt(C["black"]), pail_c + Vector((0, -0.074, 0.0)), 0.026)
    pm.curve_tube("Pip_PailHandle", craft.smooth(C["black"], 0.5), [pail_c + Vector((-0.06, 0, 0.04)),
                                                                     Vector((-0.29, -0.06, 0.4)),
                                                                     pail_c + Vector((0.06, 0, 0.04))], 0.004,
                  taper=(1, 1))
    for obj in list(bpy.context.scene.objects):
        if obj.parent is None and obj.name not in before:
            obj.parent = pip_root
    pip_root.location = (-1.3, 0, 0)
    before = snapshot()
    build_bicycle(craft)
    group("Bicycle", before, (-3.05, 0.25, 0), yaw=8)
    before = snapshot()
    build_bea(craft)
    group("Bea", before, (-2.2, 0, 0), yaw=8)
    before = snapshot()
    build_june(craft)
    group("June", before, (-0.35, 0, 0), yaw=-6)
    before = snapshot()
    build_kit(craft)
    group("Kit", before, (0.65, 0, 0), yaw=-8)
    before = snapshot()
    build_vale(craft)
    group("Vale", before, (1.75, 0, 0), yaw=-12)
    build_stage(craft)
    bpy.context.scene.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print("cast lineup ->", out)


if __name__ == "__main__":
    main()
