"""Pip as a little vampire (spec §2 v1.3, D18): the anatomy and detail benchmark for the whole cast.

    blender -b --python tools/blender/chars/pip_vampire.py -- <out.png>

Writes a review sheet: a five-view turnaround on top, a portrait and a hands-and-shoes detail below.
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars"):
    sys.path.insert(0, os.path.join(TOOLS, sub))

import anatomy as an  # noqa: E402
import craft as craft_lib  # noqa: E402
import pip_model as pm  # noqa: E402

H = craft_lib.hex_rgb
V = {  # vivid Halloween palette (D17/D18)
    "night": H(0x241B4A), "orange": H(0xFF7B1C), "purple": H(0x7A2FBF), "violet": H(0x3B1E6B), "lime": H(0x8EE04A),
    "black": H(0x17141F), "candy": H(0xFFD23F), "bone": H(0xF6EBD2), "berry": H(0xC2185B),
    "skin": H(0xF7CDA8), "cheek": H(0xF58A8E), "nail": H(0xF9CDBD), "hair": H(0x241642), "hair_dark": H(0x170F2E),
    "iris": H(0xC9701F), "iris_dark": H(0x5A2A0E), "line": H(0x4A2A2A), "mouth": H(0x6E1F33), "tongue": H(0xF0727F),
    "white": (1.0, 1.0, 1.0),
}


def ell_lathe(name, base, profile, open_deg=0.0, segments=72, hem=None, inner=None, yscale=0.8, thickness=0.01,
              folds=0, fold_depth=0.0):
    """Elliptical lathe of rings (rx, z[, yscale[, open_deg]]) around Z, optionally open at the front (-Y).

    `hem(t)` offsets the last ring's height along the span t = 0..1; `folds` ripples the cloth that many times
    across the span, deepening toward the last ring; `inner` lines the inside with a second material."""
    bm = bmesh.new()
    is_open = open_deg > 0 or any(len(e) > 3 for e in profile)
    count = segments if is_open else segments - 1
    rings = []
    for ri, entry in enumerate(profile):
        rx, z = entry[0], entry[1]
        ys = entry[2] if len(entry) > 2 else yscale
        gap = math.radians(entry[3] if len(entry) > 3 else open_deg)
        t_ring = ri / max(1, len(profile) - 1)
        ring = []
        for i in range(count + 1):
            t = i / segments
            a = -math.pi / 2 + gap + (2 * math.pi - 2 * gap) * t
            zz = z + (hem(t) if hem and ri == len(profile) - 1 else 0.0)
            fold = 1.0 + fold_depth * t_ring ** 1.5 * math.cos(2 * math.pi * folds * t) if folds else 1.0
            ring.append(bm.verts.new((rx * fold * math.cos(a), rx * fold * ys * math.sin(a), zz)))
        rings.append(ring)
    rising = profile[-1][1] > profile[0][1]
    for k in range(len(rings) - 1):
        n = len(rings[k])
        for i in range(n - 1 if is_open else n):
            j = (i + 1) % n
            quad = (rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i])
            bm.faces.new(quad if rising else quad[::-1])  # normals always point outward
    obj = pm.new_mesh_object(name, bm, base)
    sol = obj.modifiers.new("Thick", "SOLIDIFY")
    sol.thickness = thickness
    if inner is not None:
        obj.data.materials.append(inner)
        sol.material_offset = 1
        sol.material_offset_rim = 1
    obj.modifiers.new("Smooth", "SUBSURF").levels = 2
    return obj


def flat(name, mat, outline, thickness, location=(0, 0, 0), rotation=(0, 0, 0)):
    obj = pm.wing_outline(outline, thickness, mat, name)
    obj.location = location
    obj.rotation_euler = rotation
    return obj


def bat_outline(scale):
    half = [(0.0, 0.18), (0.06, 0.2), (0.12, 0.12), (0.28, 0.22), (0.5, 0.14), (0.62, 0.02), (0.52, -0.02),
            (0.44, -0.1), (0.33, -0.04), (0.24, -0.14), (0.14, -0.06), (0.05, -0.16), (0.0, -0.12)]
    return [(x * scale, z * scale) for x, z in half] + [(-x * scale, z * scale) for x, z in reversed(half[1:-1])]


def star_outline(radius, points=5):
    return [(radius * (1.0 if i % 2 == 0 else 0.48) * math.sin(math.pi * i / points),
             radius * (1.0 if i % 2 == 0 else 0.48) * math.cos(math.pi * i / points)) for i in range(2 * points)]


def striped(mat, rgb, period):
    """Copy of a craft material with horizontal bands of `rgb` every `period` metres of height."""
    mat = mat.copy()
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    base = nodes["Principled BSDF"].inputs["Base Color"]
    coord = nodes.new("ShaderNodeTexCoord")
    split = nodes.new("ShaderNodeSeparateXYZ")
    links.new(coord.outputs["Object"], split.inputs[0])
    wave = nodes.new("ShaderNodeMath")
    wave.operation = "PINGPONG"
    wave.inputs[1].default_value = period / 2
    links.new(split.outputs["Z"], wave.inputs[0])
    band = nodes.new("ShaderNodeMath")
    band.operation = "GREATER_THAN"
    band.inputs[1].default_value = period / 4
    links.new(wave.outputs[0], band.inputs[0])
    mix = nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    links.new(band.outputs[0], mix.inputs[0])
    links.new(base.links[0].from_socket, mix.inputs[6])
    mix.inputs[7].default_value = craft_lib.lin(rgb)
    links.new(mix.outputs[2], base)
    sheen = nodes["Principled BSDF"].inputs["Sheen Weight"]  # dark bands: little sheen, or the ribs streak white
    calm = nodes.new("ShaderNodeMath")
    calm.operation = "MULTIPLY_ADD"
    links.new(band.outputs[0], calm.inputs[0])
    calm.inputs[1].default_value = -0.85 * sheen.default_value
    calm.inputs[2].default_value = sheen.default_value
    links.new(calm.outputs[0], sheen)
    return mat


def blushed(mat, rgb, centres, radius, strength=0.6):
    """Copy of a smooth skin material with soft round blush painted around `centres` (object space)."""
    mat = mat.copy()
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    coord = nodes.new("ShaderNodeTexCoord")
    total = None
    for c in centres:
        dist = nodes.new("ShaderNodeVectorMath")
        dist.operation = "DISTANCE"
        links.new(coord.outputs["Object"], dist.inputs[0])
        dist.inputs[1].default_value = c
        fade = nodes.new("ShaderNodeMapRange")
        fade.interpolation_type = "SMOOTHSTEP"
        fade.inputs["From Min"].default_value = radius * 0.25
        fade.inputs["From Max"].default_value = radius
        fade.inputs["To Min"].default_value = strength
        fade.inputs["To Max"].default_value = 0.0
        links.new(dist.outputs["Value"], fade.inputs["Value"])
        if total is None:
            total = fade.outputs["Result"]
        else:
            add = nodes.new("ShaderNodeMath")
            add.operation = "MAXIMUM"
            links.new(total, add.inputs[0])
            links.new(fade.outputs["Result"], add.inputs[1])
            total = add.outputs[0]
    mix = nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    links.new(total, mix.inputs[0])
    mix.inputs[6].default_value = bsdf.inputs["Base Color"].default_value
    mix.inputs[7].default_value = craft_lib.lin(rgb)
    links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    return mat


def graded(mat, rgb, z0, z1):
    """Copy of a smooth material whose colour blends to `rgb` from height z0 to z1 (object space)."""
    mat = mat.copy()
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    coord = nodes.new("ShaderNodeTexCoord")
    split = nodes.new("ShaderNodeSeparateXYZ")
    links.new(coord.outputs["Object"], split.inputs[0])
    fade = nodes.new("ShaderNodeMapRange")
    fade.inputs["From Min"].default_value = z0
    fade.inputs["From Max"].default_value = z1
    links.new(split.outputs["Z"], fade.inputs["Value"])
    mix = nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    links.new(fade.outputs["Result"], mix.inputs[0])
    mix.inputs[6].default_value = bsdf.inputs["Base Color"].default_value
    mix.inputs[7].default_value = craft_lib.lin(rgb)
    links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    return mat


def build_head(craft, P):
    r, hc = P.head_r, P.head_c
    skin = craft.smooth(V["skin"], 0.5, subsurface=0.25)
    face_skin = blushed(skin, V["cheek"], [hc + Vector((s * r * 0.56, -r * 0.8, -r * 0.4)) for s in (1, -1)], r * 0.3)
    iris = graded(craft.smooth(V["iris_dark"], 0.2), V["iris"], r * 0.1, -r * 0.13)
    line = craft.smooth(V["line"], 0.6)
    an.head("Pip_", P, face_skin, craft.smooth(V["white"], 0.15), iris, craft.smooth(V["black"], 0.3), line,
            craft.smooth(V["mouth"], 0.6), craft.smooth(V["tongue"], 0.5), fang=craft.smooth(V["white"], 0.3),
            brow=craft.felt(V["hair"]), ears=False)

    # Hair: one clean bob with a straight fringe, the ends curling in under the jaw, and combed strand grooves.
    hair = craft.felt(V["hair"])
    groove = craft.felt(V["hair_dark"])
    centre = hc + Vector((0, r * 0.04, r * 0.1))
    radii = Vector((r * 1.13, r * 1.15, r * 1.12))
    fringe_z, length_z, face_half = r * 0.3, -r * 0.62, math.radians(50)

    locks = 13
    low = length_z / radii.z - 0.1  # unit height of the hem

    def lock_phase(lon):
        """0 in the valley between two locks, 1 on the crest of a lock."""
        return abs(math.sin(locks * (lon + 0.21) / 2))

    def bob(unit, lift=1.0):
        """Unit-sphere point -> bob surface: a dome at the crown, then chunky locks falling straight to a
        softly uneven hem that flips out a little."""
        flat = math.hypot(unit.x, unit.y)
        if flat < 1e-6:
            return Vector((0, 0, unit.z * radii.z)) * lift + centre
        lon = math.atan2(unit.x, -unit.y)
        depth = min(1.0, max(0.0, (0.9 - unit.z) / (0.9 - low)))  # 0 at the crown … 1 at the hem
        fall = max(flat, 0.97 - 0.25 * unit.z * unit.z) / flat if unit.z < 0 else 1.0
        clump = 1 + 0.2 * depth * (lock_phase(lon) ** 0.6 - 0.62)
        flip = 1 + 0.07 * max(0.0, (depth - 0.8) / 0.2) ** 2
        ragged = 0.07 * lock_phase(lon) + 0.04 * math.sin(3 * lon + 0.8)  # each lock ends in its own soft point
        zz = unit.z - (0.04 + ragged) * max(0.0, (depth - 0.6) / 0.4) ** 2
        k = fall * clump * flip
        return Vector((unit.x * k * radii.x, unit.y * k * radii.y, zz * radii.z)) * lift + centre

    def on_hair(lon, el, lift=1.075):
        """Point on the bob's outer surface at longitude `lon` (0 = front, counter-clockwise) and elevation `el`."""
        a = lon - math.pi / 2
        return bob(Vector((math.cos(el) * math.cos(a), math.cos(el) * math.sin(a), math.sin(el))), lift)

    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=78, v_segments=32, radius=1.0)
    doomed = []
    for f in bm.faces:
        c = f.calc_center_median()
        rel = Vector((c.x * radii.x, c.y * radii.y, c.z * radii.z)) + centre - hc
        in_face = abs(math.atan2(c.x, -c.y)) < face_half
        if rel.z < length_z or (in_face and rel.z < fringe_z):
            doomed.append(f)
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    for v in bm.verts:
        v.co = bob(v.co)
    hair_obj = pm.new_mesh_object("Pip_Hair", bm, hair)
    sol = hair_obj.modifiers.new("Thick", "SOLIDIFY")
    sol.thickness = r * 0.09
    sol.offset = 1.0
    hair_obj.modifiers.new("Smooth", "SUBSURF").levels = 2
    el_low = math.asin((length_z + r * 0.06 - r * 0.1) / radii.z)
    el_fringe = math.asin((fringe_z + r * 0.05 - r * 0.1) / radii.z)
    for k in range(locks):  # a deep groove in every valley between locks, fanning from the crown …
        lon = 2 * math.pi * k / locks - 0.21
        lon = (lon + math.pi) % (2 * math.pi) - math.pi
        bottom = el_fringe if abs(lon) < face_half else el_low
        pts = [on_hair(lon, math.radians(80) + (bottom - math.radians(80)) * j / 9, 1.04) for j in range(10)]
        an.hair_clump("Pip_HairGroove", groove, pts, r * 0.03, taper=(0.2, 1.0))
        crest = lon + math.pi / locks  # … and a fine strand line down the crest of each lock
        crest = (crest + math.pi) % (2 * math.pi) - math.pi
        if abs(abs(crest) - face_half) < 0.12:
            continue  # keep the edge of the side locks clean
        bottom = el_fringe if abs(crest) < face_half else el_low
        pts = [on_hair(crest, math.radians(62) + (bottom - math.radians(62)) * j / 7) for j in range(8)]
        an.hair_clump("Pip_HairStrand", groove, pts, r * 0.009, taper=(0.3, 1.0))
    # A tiny felt bat hair clip on the side of the bob (the only bat motif on the head).
    clip_at = on_hair(math.radians(38), math.radians(27), 1.115)
    normal = on_hair(math.radians(38), math.radians(27), 1.5) - clip_at
    an.mount("Pip_BatClip", clip_at, normal,
             [flat("Pip_BatClipFelt", craft.felt(V["orange"]), bat_outline(0.066), 0.005)], roll=math.radians(-14))
    return skin


def build_outfit(craft, P, skin):
    z, T = P.z, P.torso
    shirt = craft.felt(V["bone"])
    tights = striped(craft.knit(V["purple"]), V["black"], 0.09)
    black, purple, orange = craft.felt(V["black"]), craft.felt(V["purple"]), craft.felt(V["orange"])
    def region(c):
        if abs(c.x) < 0.14 and c.z < z["hip"] + 0.02:
            return 1
        return 2 if c.z > z["neck"] + 0.012 else 0  # bare neck above the shirt
    body = an.body("Pip_Body", P, [shirt, tights, skin], region)
    on_body = an.probe(body)

    # Black felt shorts: a waist shell and two puffed leg cuffs with orange hems.
    ell_lathe("Pip_Shorts", black, [(T * 1.04, z["pelvis"] + 0.07, 0.8), (T * 1.15, z["pelvis"] + 0.03, 0.8),
                                    (T * 1.17, z["hip"] - 0.03, 0.8), (T * 1.08, z["hip"] - 0.06, 0.78)],
              thickness=0.006)
    for s in (1, -1):
        cuff = ell_lathe("Pip_ShortsLeg", black, [(0.058, z["hip"] - 0.01, 1.0), (0.07, z["hip"] - 0.05, 1.0),
                                                  (0.066, z["hip"] - 0.085, 1.0)], thickness=0.006, segments=32)
        cuff.location.x = s * P.hip_x * 1.08
        pm.torus("Pip_ShortsHem", orange, (s * P.hip_x * 1.08, 0, z["hip"] - 0.085), 0.064, 0.006, segments=32,
                 sides=8)

    # Purple felt vest: V-neck opening to the shoulders, buttoned at the waist, two points at the hem,
    # orange piping along the front edges.
    def vest_hem(t):
        return -0.03 * max(0.0, 1 - min(t, 1 - t) / 0.1)
    vest_profile = [(T * 0.6, z["shoulder"] + 0.05, 0.8, 58), (T * 1.03, z["shoulder"] - 0.01, 0.74, 32),
                    (T * 1.14, z["chest"] - 0.012, 0.77, 15), (T * 1.07, z["waist"], 0.79, 3),
                    (T * 1.12, z["pelvis"] + 0.035, 0.8, 3)]
    vest = ell_lathe("Pip_Vest", purple, vest_profile, hem=vest_hem, thickness=0.007)
    on_vest = an.probe(vest)
    for s in (1, -1):
        edge = []
        for rx, zz, ys, gap in vest_profile:
            a = -math.pi / 2 + s * math.radians(gap)
            edge.append(Vector((rx * math.cos(a), rx * ys * math.sin(a) - 0.002, zz)))
        edge[-1].z -= 0.026
        pm.curve_tube("Pip_VestPiping", orange, edge, 0.0045, taper=(1, 1))
    for k in range(3):
        point, normal = on_vest((0.02, -1, z["waist"] + 0.045 - k * 0.04), (0, 1, 0))
        an.mount("Pip_VestButton", point, normal,
                 [pm.ellipsoid("Pip_VestButtonDisc", craft.smooth(V["orange"], 0.35), (0, 0, 0),
                               (0.008, 0.004, 0.008), subsurf=1)])

    # Shirt jabot: three tiers of gathered ruffles tumbling down the V-neck, pinned with a berry brooch.
    white = craft.felt(V["white"])
    for k in (2, 1, 0):
        top_z = z["neck"] - 0.03 - k * 0.034
        anchor = on_body((0, -1, top_z), (0, 1, 0))[0] + Vector((0, -0.008 - 0.004 * (2 - k), 0))
        drop, half = 0.05 + 0.004 * k, math.radians(62)
        bm = bmesh.new()
        rows = []
        for j in range(4):
            f = j / 3
            row = []
            for i in range(33):
                u = i / 32 * 2 - 1
                rad = drop * (0.12 + 0.88 * f)
                ripple = 0.007 * f * math.cos(u * math.pi * 5)
                row.append(bm.verts.new(anchor + Vector((rad * math.sin(u * half) * 1.25,
                                                         -0.012 * f * (1 - 0.5 * u * u) - ripple,
                                                         -rad * math.cos(u * half)))))
            rows.append(row)
        for a, b in zip(rows, rows[1:]):
            for i in range(32):
                bm.faces.new((a[i], a[i + 1], b[i + 1], b[i]))
        ruffle = pm.new_mesh_object("Pip_Jabot", bm, white)
        ruffle.modifiers.new("Thick", "SOLIDIFY").thickness = 0.003
        ruffle.modifiers.new("Smooth", "SUBSURF").levels = 1
        if k == 0:
            pm.ellipsoid("Pip_Brooch", craft.smooth(V["berry"], 0.15), anchor + Vector((0, -0.006, -0.004)),
                         (0.011, 0.007, 0.012), subsurf=1)

    for s in (1, -1):  # soft folds where the sleeve bunches at the elbow
        el, wr = P.elbow(s) + Vector((0, 0.014, 0)), P.wrist(s)
        sh = Vector((s * P.shoulder_x, 0, z["shoulder"]))
        for towards, t, tilt in ((sh, 0.1, 0.22), (wr, 0.07, -0.16), (wr, 0.2, 0.12)):
            pm.torus("Pip_SleeveFold", shirt, el.lerp(towards, t), P.limb * 0.56, 0.0065,
                     rotation=(tilt, -s * P.arm_out, 0), segments=24, sides=8)
    for s in (1, -1):  # gathered shirt cuffs
        pm.torus("Pip_Cuff", white, P.wrist(s) + Vector((0, 0, 0.002)), P.limb * 0.56, 0.011,
                 rotation=(0, -s * P.arm_out, 0), segments=24, sides=10)


def build_cape(craft, P):
    """Vampire cape: matte black velvet, berry lining, bat-wing hem; it hangs behind the arms, open at the front."""
    z = P.z
    velvet, lining = craft.felt(V["black"]), craft.felt(V["berry"])
    scallops = 5

    def bat_hem(t):
        return 0.055 * abs(math.sin(math.pi * scallops * t)) ** 0.7
    profile = [(0.068, z["neck"] + 0.012, 0.9, 50), (0.13, z["shoulder"] + 0.066, 0.74, 74),
               (0.188, z["shoulder"] + 0.016, 0.66, 84), (0.2, z["chest"] - 0.03, 0.64, 90),
               (0.216, z["pelvis"], 0.62, 92), (0.232, z["knee"] - 0.02, 0.62, 92)]
    cape = ell_lathe("Pip_Cape", velvet, profile, hem=bat_hem, inner=lining, thickness=0.007, folds=scallops,
                     fold_depth=0.1)
    on_cape = an.probe(cape)

    def hem_tip(t):
        return 0.01 * max(0.0, 1 - min(t, 1 - t) / 0.12)
    ell_lathe("Pip_CapeCollar", velvet, [(0.105, z["neck"] - 0.012), (0.134, z["neck"] + 0.016),
                                         (0.158, z["neck"] + 0.04)], open_deg=64, yscale=0.95, hem=hem_tip,
              inner=lining, thickness=0.007)
    # Clasp: an orange button on each front edge joined by a candy-yellow cord.
    ends = []
    for s in (1, -1):
        a = -math.pi / 2 + s * math.radians(52)
        p = Vector((0.07 * math.cos(a), 0.07 * 0.9 * math.sin(a) - 0.004, z["neck"] + 0.002))
        ends.append(p)
        pm.ellipsoid("Pip_ClaspButton", craft.smooth(V["orange"], 0.35), p, (0.011, 0.007, 0.011), subsurf=1)
    mid = (ends[0] + ends[1]) * 0.5 + Vector((0, -0.012, -0.012))
    pm.curve_tube("Pip_ClaspCord", craft.smooth(V["candy"], 0.4), [ends[0], mid, ends[1]], 0.003, taper=(1, 1))

    # Homemade touches: felt stars and a moon sewn on the back, an orange running stitch above the hem.
    candy = craft.felt(V["candy"])
    for x, zz, size, roll in ((-0.07, 0.74, 0.03, 0.2), (0.085, 0.68, 0.022, -0.3), (0.01, 0.58, 0.018, 0.5),
                              (-0.11, 0.5, 0.024, -0.1), (0.1, 0.44, 0.03, 0.35), (-0.03, 0.4, 0.02, 0.1)):
        point, normal = on_cape((x, 1, zz), (0, -1, 0))
        an.mount("Pip_CapeStar", point + normal * 0.004, normal,
                 [flat("Pip_CapeStarFelt", candy, star_outline(size), 0.004)], roll=roll)
    stitch = []
    for i in range(161):
        t = i / 160
        a = math.radians(-90 + 92 + (360 - 184) * (0.012 + 0.976 * t))
        zz = z["knee"] - 0.02 + bat_hem(0.012 + 0.976 * t) * 0.8 + 0.024
        hit = on_cape((math.cos(a), math.sin(a), zz), (-math.cos(a), -math.sin(a), 0))
        if hit:
            stitch.append(hit[0] + hit[1] * 0.0015)
    craft.stitches("Pip_CapeStitch", stitch, V["orange"], spacing=0.02, dash=0.011, radius=0.0022)


def build_pail(craft, grip):
    """Jack-o'-lantern treat pail hanging from the gripping hand."""
    pail = grip + Vector((0, 0, -0.15))
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=20, radius=1.0)
    for v in bm.verts:
        a = math.atan2(v.co.y, v.co.x)
        lobe = 1 - 0.08 * (1 - abs(math.cos(4 * a))) ** 0.8
        v.co = Vector((v.co.x * 0.075 * lobe, v.co.y * 0.075 * lobe, max(-0.9, min(0.75, v.co.z)) * 0.064)) + pail
    pm.new_mesh_object("Pip_Pail", bm, craft.felt(V["orange"])).modifiers.new("S", "SUBSURF").levels = 1
    pm.torus("Pip_PailRim", craft.felt(V["lime"]), pail + Vector((0, 0, 0.047)), 0.05, 0.006, segments=32, sides=8)
    face = craft.felt(V["black"])
    for s in (1, -1):
        flat("Pip_PailEye", face, [(-0.012, -0.008), (0.012, -0.008), (0.0, 0.012)], 0.003,
             pail + Vector((s * 0.022, -0.075, 0.012)))
    flat("Pip_PailGrin", face, [(-0.03, 0.0), (-0.015, -0.008), (0.0, 0.0), (0.015, -0.008), (0.03, 0.0),
                                (0.015, -0.02), (-0.015, -0.02)], 0.003, pail + Vector((0, -0.074, -0.016)))
    pm.curve_tube("Pip_PailHandle", craft.smooth(V["black"], 0.4),
                  [pail + Vector((0, -0.05, 0.047)), pail + Vector((0, -0.042, 0.1)), grip,
                   pail + Vector((0, 0.042, 0.1)), pail + Vector((0, 0.05, 0.047))], 0.0035, taper=(1, 1))


def build_pip(craft):
    P = an.Proportions(h=1.2, child=True)
    skin = build_head(craft, P)
    build_outfit(craft, P, skin)
    nail = craft.smooth(V["nail"], 0.3)
    an.hand("Pip_HandL", P, 1, skin, nail, curl=0.4, size=1.15)
    _, grip = an.hand("Pip_HandR", P, -1, skin, nail, curl=1.5, size=1.15)
    for side in (1, -1):
        an.sneaker(f"Pip_Shoe{'L' if side > 0 else 'R'}", P, side, craft.felt(V["orange"]),
                   craft.smooth(V["bone"], 0.6), craft.felt(V["purple"]), craft.smooth(V["white"], 0.7))
    build_cape(craft, P)
    build_pail(craft, grip)
    return P


def stage():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 64
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance = 0.2
    scene.eevee.use_soft_shadows = True
    scene.eevee.use_bloom = True
    scene.eevee.bloom_intensity = 0.04
    scene.eevee.bloom_threshold = 1.2
    scene.view_settings.view_transform = "Standard"  # keeps the palette as saturated as it will be in-engine
    scene.view_settings.exposure = -0.3
    world = bpy.data.worlds.new("Night")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = craft_lib.lin(V["night"])
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    scene.world = world
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, segments=64, radius=2.0)
    ground = bpy.data.materials.new("Ground")
    ground.use_nodes = True
    ground.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = craft_lib.lin(H(0x1E1830))
    ground.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.95
    pm.new_mesh_object("Ground", bm, ground)

    def area(name, loc, energy, colour, size, target=(0, 0, 0.7)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.color, data.size = energy, colour, size
        obj = bpy.data.objects.new(name, data)
        obj.location = loc
        obj.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        pm.link(obj)

    area("Key", (-1.8, -2.6, 2.4), 230, (1.0, 0.86, 0.72), 1.8)
    area("Rim", (1.9, 2.4, 2.1), 380, (0.72, 0.66, 1.0), 1.4)
    area("Fill", (2.4, -1.6, 1.0), 90, (0.8, 0.82, 1.0), 2.2)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    pm.link(cam)
    scene.camera = cam
    return cam


def render_view(cam, location, target, lens, width, height, path):
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = width, height
    cam.data.lens = lens
    cam.location = Vector(location)
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    px = np.array(img.pixels[:], dtype=np.float32).reshape(height, width, 4)
    bpy.data.images.remove(img)
    os.remove(path)
    return px


def main():
    out = sys.argv[sys.argv.index("--") + 1:][0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    craft = craft_lib.Craft(1.2)
    build_pip(craft)
    cam = stage()
    tmp = os.path.splitext(out)[0]
    tiles = []
    target = Vector((0, 0, 0.64))
    for i, yaw in enumerate((0, 35, 90, 145, 180)):
        a = math.radians(yaw)
        loc = target + Vector((4.0 * math.sin(a), -4.0 * math.cos(a), 0.3))
        tiles.append(render_view(cam, loc, target, 85, 420, 620, f"{tmp}_t{i}.png"))
    top = np.concatenate(tiles, axis=1)
    portrait = render_view(cam, (-0.42, -1.4, 1.14), (0.0, 0.0, 1.045), 85, 1260, 620, f"{tmp}_face.png")
    detail = render_view(cam, (-0.62, -1.2, 0.42), (0.03, -0.05, 0.25), 50, 840, 620, f"{tmp}_detail.png")
    bottom = np.concatenate([portrait, detail], axis=1)
    sheet = np.concatenate([bottom, top], axis=0)  # image rows run bottom-up
    img = bpy.data.images.new("Sheet", sheet.shape[1], sheet.shape[0], alpha=True)
    img.pixels.foreach_set(sheet.ravel())
    img.filepath_raw = out
    img.file_format = "PNG"
    img.save()
    print("pip sheet ->", out)


if __name__ == "__main__":
    main()
