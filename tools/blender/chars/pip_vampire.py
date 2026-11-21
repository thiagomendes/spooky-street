"""Pip as a little vampire-bat (spec §2 v1.3, D18): the anatomy and detail benchmark for the whole cast.

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
    "skin": H(0xF2C6A0), "skin_shade": H(0xE5A88A), "cheek": H(0xF28C8C), "hair": H(0x1F1830), "iris": H(0x8A4B1F),
    "white": (1.0, 1.0, 1.0), "nail": H(0xF7D9C4),
}


def ell_lathe(name, base, profile, open_deg=0.0, segments=72, hem=None, inner=None, yscale=0.8, thickness=0.01,
              folds=0, fold_depth=0.0):
    """Elliptical lathe: rings of (rx, z) with ry = rx * yscale; optional open front, hem function, lining."""
    bm = bmesh.new()
    span = 2 * math.pi - 2 * math.radians(open_deg)
    start = -math.pi / 2 + math.radians(open_deg)
    count = segments if open_deg > 0 else segments - 1
    rings = []
    for ri, entry in enumerate(profile):
        rx, z = entry[0], entry[1]
        ys = entry[2] if len(entry) > 2 else yscale
        t_ring = ri / max(1, len(profile) - 1)
        ring = []
        for i in range(count + 1):
            a = start + span * i / segments
            zz = z + (hem(a, i / segments) if hem and ri == len(profile) - 1 else 0.0)
            fold = 1.0 + fold_depth * t_ring ** 1.5 * math.sin(folds * (a - start)) if folds else 1.0
            ring.append(bm.verts.new((rx * fold * math.cos(a), rx * fold * ys * math.sin(a), zz)))
        rings.append(ring)
    for k in range(len(rings) - 1):
        n = len(rings[k])
        for i in range(n - 1 if open_deg > 0 else n):
            j = (i + 1) % n
            bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
    obj = pm.new_mesh_object(name, bm, base)
    sol = obj.modifiers.new("Thick", "SOLIDIFY")
    sol.thickness = thickness
    if inner is not None:
        obj.data.materials.append(inner)
        sol.material_offset = 1
        sol.material_offset_rim = 1
    obj.modifiers.new("Smooth", "SUBSURF").levels = 2
    return obj


def flat(name, mat, outline, thickness, location, rotation=(0, 0, 0)):
    obj = pm.wing_outline(outline, thickness, mat, name)
    obj.location = location
    obj.rotation_euler = rotation
    return obj


def bat_outline(scale):
    half = [(0.0, 0.18), (0.06, 0.2), (0.12, 0.12), (0.28, 0.22), (0.5, 0.14), (0.62, 0.02), (0.52, -0.02),
            (0.44, -0.1), (0.33, -0.04), (0.24, -0.14), (0.14, -0.06), (0.05, -0.16), (0.0, -0.12)]
    return [(x * scale, z * scale) for x, z in half] + [(-x * scale, z * scale) for x, z in reversed(half[1:-1])]


def build_pip(craft):
    P = an.Proportions(h=1.2, child=True)
    z = P.z
    hc, r = P.head_c, P.head_r
    shirt = craft.knit(V["bone"])
    shorts = craft.felt(V["black"])
    tights_a, tights_b = craft.knit(V["purple"]), craft.knit(V["black"])
    skin = craft.smooth(V["skin"], 0.5, subsurface=0.25)

    def regions(c):
        if abs(c.x) < 0.135 and c.z < z["hip"] - 0.015:
            return 2 + int(c.z / 0.034) % 2  # striped tights
        if abs(c.x) < 0.135 and c.z < z["pelvis"] + 0.045:
            return 1
        return 0
    an.body("Pip_Body", P, [shirt, shorts, tights_a, tights_b], regions)
    for side in (1, -1):
        an.hand(f"Pip_Hand{'L' if side > 0 else 'R'}", P, side, skin, craft.smooth(V["nail"], 0.3), curl=0.45)
        an.sneaker(f"Pip_Shoe{'L' if side > 0 else 'R'}", P, side, craft.felt(V["orange"]), craft.smooth(V["bone"], 0.6),
                   craft.felt(V["black"]), craft.smooth(V["bone"], 0.7))
        # Shirt cuffs at the wrist.
        pm.torus("Pip_Cuff", shirt, P.wrist(side) + Vector((0, 0, 0.012)), P.limb * 0.52, 0.009,
                 rotation=(0, -side * P.arm_out, 0), segments=24, sides=8)
    an.head("Pip_", P, skin, craft.smooth(V["white"], 0.15), craft.smooth(V["iris"], 0.2),
            craft.smooth(V["black"], 0.3), lid=craft.smooth(V["skin_shade"], 0.5), mouth="smile",
            fangs=craft.smooth(V["white"], 0.25), lips=craft.smooth(H(0x8E3A3A), 0.5),
            brow=craft.felt(V["hair"]), cheek=craft.smooth(V["cheek"], 0.8))

    # Hair: one clean bob volume with a straight fringe, the ends curling in at the jaw, and strand grooves.
    hair = craft.felt(V["hair"])
    groove = craft.felt(H(0x120E1E))
    centre = hc + Vector((0, r * 0.04, r * 0.08))
    radii = Vector((r * 1.1, r * 1.12, r * 1.12))
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=32, radius=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * radii.x, v.co.y * radii.y, v.co.z * radii.z)) + centre
    doomed = []
    for f in bm.faces:
        c = f.calc_center_median() - hc
        if c.y < -r * 0.15 and c.z < r * 0.4 and abs(c.x) < r * 0.8:
            doomed.append(f)  # the face opening, with a straight fringe line at brow height
        elif c.z < -r * 0.62:
            doomed.append(f)  # bob length: jaw line
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    for v in bm.verts:
        rel = v.co - hc
        if rel.z < -r * 0.3:
            k = 1 - 0.1 * min(1.0, (-r * 0.3 - rel.z) / (r * 0.32))  # ends curl inward
            v.co.x = hc.x + rel.x * k
            v.co.y = hc.y + rel.y * k
    bob = pm.new_mesh_object("Pip_Hair", bm, hair)
    sol = bob.modifiers.new("Thick", "SOLIDIFY")
    sol.thickness = r * 0.09
    sol.offset = 1.0
    bob.modifiers.new("Smooth", "SUBSURF").levels = 2
    for k in range(18):
        a = -math.pi / 2 + math.radians(48) + (2 * math.pi - math.radians(96)) * k / 17
        pts = []
        for j in range(8):
            t = j / 7
            el = math.radians(80) - t * math.radians(118)
            p = Vector((math.cos(el) * math.cos(a) * radii.x * 1.09, math.cos(el) * math.sin(a) * radii.y * 1.09,
                        math.sin(el) * radii.z * 1.06)) + centre
            if (p - hc).z < -r * 0.3:
                p = hc + Vector(((p - hc).x * 0.92, (p - hc).y * 0.92, (p - hc).z))
            pts.append(p)
        an.hair_clump("Pip_HairGroove", groove, pts, r * 0.014, taper=(0.3, 1.0))
    for k in range(9):
        x = (k - 4) / 4 * r * 0.72
        an.hair_clump("Pip_FringeGroove", groove, [hc + Vector((x * 0.7, -r * 0.7, r * 0.95)),
                                                   hc + Vector((x, -r * 1.02, r * 0.62)),
                                                   hc + Vector((x * 1.02, -r * 1.04, r * 0.44))],
                      r * 0.013, taper=(0.3, 1.0))

    # Bat-ear headband.
    band = [hc + Vector((r * 1.12 * math.cos(a), r * 0.05, r * 1.2 * math.sin(a))) for a in
            [math.radians(15 + 150 * i / 12) for i in range(13)]]
    pm.curve_tube("Pip_Headband", craft.smooth(V["black"], 0.4), band, r * 0.04, taper=(1, 1))
    ear = [(0.0, 0.0), (0.14, 0.04), (0.2, 0.2), (0.17, 0.4), (0.06, 0.66), (0.0, 0.42), (-0.08, 0.36),
           (-0.15, 0.18), (-0.1, 0.03)]
    for s in (1, -1):
        out = [(s * x * r * 2.2, zz * r * 2.0) for x, zz in ear]
        inner = [(s * x * r * 1.4 + s * r * 0.03, zz * r * 1.3 + r * 0.08) for x, zz in ear]
        base = hc + Vector((s * r * 0.55, r * 0.0, r * 1.08))
        flat("Pip_BatEar", craft.felt(V["black"]), pm.smooth_outline(out, 5), r * 0.06, base,
             (math.radians(-8), s * math.radians(-22), 0))
        flat("Pip_BatEarInner", craft.felt(V["berry"]), pm.smooth_outline(inner, 5), r * 0.03,
             base + Vector((0, -r * 0.05, 0)), (math.radians(-8), s * math.radians(-22), 0))

    # Violet vest: fitted, open at the front, two points at the hem, orange piping and buttons.
    T = P.torso
    def vest_hem(a, t):
        # Drop into two points either side of the opening.
        return -0.035 * max(0.0, 1 - min(t, 1 - t) / 0.12)
    vest_profile = [(T * 1.1, z["chest"] + 0.055, 0.78), (T * 1.14, z["chest"], 0.8), (T * 1.04, z["waist"], 0.82),
                    (T * 1.1, z["pelvis"] + 0.035, 0.84)]
    ell_lathe("Pip_Vest", craft.felt(V["purple"]), vest_profile, open_deg=28, hem=vest_hem, thickness=0.008)
    for s in (1, -1):
        edge = []
        for rx, zz, ys in vest_profile:
            a = -math.pi / 2 + s * math.radians(28)
            edge.append((rx * 1.03 * math.cos(a), rx * 1.03 * ys * math.sin(a), zz))
        edge[-1] = (edge[-1][0], edge[-1][1], edge[-1][2] - 0.035)
        pm.curve_tube("Pip_VestPiping", craft.felt(V["orange"]), edge, 0.0045, taper=(1, 1))
        for k in range(3):
            pm.ellipsoid("Pip_VestButton", craft.smooth(V["orange"], 0.35),
                         (s * T * 0.62, -T * 0.86, z["waist"] + 0.05 - k * 0.032), (0.0065, 0.004, 0.0065), subsurf=1)
    # Shirt jabot: three tiers of ruffles down the chest, the vampire signature.
    for k in range(3):
        cz = z["neck"] - 0.035 - k * 0.032
        bm = bmesh.new()
        ring = []
        for i in range(25):
            a = math.pi * i / 24
            rr = 0.032 - 0.004 * k + 0.006 * math.sin(a * 9) ** 2
            ring.append(bm.verts.new((rr * math.cos(a) - 0.0, -T * 0.92 - 0.006 - rr * 0.35 * math.sin(a),
                                      cz - rr * 0.6 * math.sin(a))))
        centre = bm.verts.new((0.0, -T * 0.88, cz + 0.006))
        for i in range(24):
            bm.faces.new((centre, ring[i], ring[i + 1]))
        ruff = pm.new_mesh_object("Pip_Jabot", bm, shirt)
        ruff.modifiers.new("Thick", "SOLIDIFY").thickness = 0.003
        ruff.modifiers.new("Smooth", "SUBSURF").levels = 2
        ruff.rotation_euler = (0, 0, 0)
    for s in (1, -1):
        flat("Pip_Collar", shirt, [(0, 0), (s * 0.045, -0.005), (s * 0.06, -0.05), (s * 0.012, -0.03)], 0.004,
             (s * 0.01, -P.limb * 0.66, z["neck"] - 0.005), (math.radians(-25), 0, 0))
    pm.torus("Pip_Waistband", shorts, (0, 0, z["pelvis"] + 0.04), T * 1.08, 0.01, squash=1.0, segments=40, sides=8)

    # The vampire-bat cape: matte black velvet outside, berry lining inside (two shells), scalloped bat-wing hem,
    # open wide at the front so the costume and arms read; a tall stand-up collar behind the head.
    def bat_hem(a, t):
        return 0.06 * abs(math.sin(math.pi * 5 * t)) ** 0.7
    cape_profile = [(0.075, z["neck"] + 0.02, 0.85), (0.165, z["shoulder"] + 0.0, 0.62), (0.205, z["chest"] - 0.04, 0.7),
                    (0.24, z["pelvis"] + 0.0, 0.8), (0.28, z["knee"] + 0.06, 0.9)]
    velvet = craft.felt(V["black"])
    ell_lathe("Pip_Cape", velvet, [(rx * 1.04, zz, ys) for rx, zz, ys in cape_profile], open_deg=96, hem=bat_hem,
              thickness=0.006, folds=9, fold_depth=0.07)
    ell_lathe("Pip_CapeLining", craft.felt(V["berry"]), [(rx * 0.985, zz + 0.004, ys) for rx, zz, ys in cape_profile],
              open_deg=96, hem=bat_hem, thickness=0.004, folds=9, fold_depth=0.07)
    collar_profile = [(0.075, z["neck"] + 0.005), (0.1, z["neck"] + 0.06), (0.135, z["neck"] + 0.12)]
    ell_lathe("Pip_CapeCollar", velvet, [(rx * 1.05, zz) for rx, zz in collar_profile], open_deg=70, yscale=0.92,
              thickness=0.006)
    ell_lathe("Pip_CapeCollarLining", craft.felt(V["berry"]), collar_profile, open_deg=70, yscale=0.92,
              thickness=0.004)
    flat("Pip_Clasp", craft.smooth(V["orange"], 0.35), bat_outline(0.075), 0.006,
         (0, -P.limb * 0.74 - 0.02, z["neck"] + 0.005))
    pm.curve_tube("Pip_ClaspChain", craft.smooth(V["candy"], 0.3), [(-0.055, -0.05, z["neck"] + 0.01),
                                                                   (0.0, -P.limb * 0.74 - 0.02, z["neck"] - 0.01),
                                                                   (0.055, -0.05, z["neck"] + 0.01)], 0.003)

    # Little torch in the vest pocket.
    pm.ellipsoid("Pip_Torch", craft.smooth(V["violet"], 0.35), (-T * 0.65, -T * 0.95, z["chest"] - 0.01),
                 (0.012, 0.012, 0.03), subsurf=1)
    pm.ellipsoid("Pip_TorchLens", craft.smooth(V["candy"], 0.3, emission=3.0), (-T * 0.65, -T * 0.97, z["chest"] + 0.02),
                 (0.011, 0.006, 0.005), subsurf=0)

    # Jack-o'-lantern treat pail hanging from the right hand.
    wr = P.wrist(-1)
    pail = wr + Vector((-0.02, -0.02, -0.17))
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=20, radius=1.0)
    for v in bm.verts:
        a = math.atan2(v.co.y, v.co.x)
        lobe = 1 - 0.08 * (1 - abs(math.cos(4 * a))) ** 0.8
        v.co = Vector((v.co.x * 0.075 * lobe, v.co.y * 0.075 * lobe, max(v.co.z, -0.9) * 0.062)) + pail
    pm.new_mesh_object("Pip_Pail", bm, craft.felt(V["orange"])).modifiers.new("S", "SUBSURF").levels = 1
    face = craft.felt(V["black"])
    for s in (1, -1):
        flat("Pip_PailEye", face, [(-0.012, -0.008), (0.012, -0.008), (0.0, 0.012)], 0.003,
             pail + Vector((s * 0.022, -0.075, 0.012)))
    flat("Pip_PailGrin", face, [(-0.03, 0.0), (-0.015, -0.008), (0.0, 0.0), (0.015, -0.008), (0.03, 0.0),
                                (0.015, -0.02), (-0.015, -0.02)], 0.003, pail + Vector((0, -0.074, -0.016)))
    pm.curve_tube("Pip_PailHandle", craft.smooth(V["black"], 0.4), [pail + Vector((-0.06, 0, 0.035)),
                                                                    wr + Vector((0.0, -0.01, -0.07)),
                                                                    pail + Vector((0.06, 0, 0.035))], 0.004,
                  taper=(1, 1))
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
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
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

    area("Key", (-1.8, -2.6, 2.4), 300, (1.0, 0.86, 0.72), 1.8)
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
    portrait = render_view(cam, (-0.35, -1.15, 1.12), (0.0, 0.0, 1.0), 85, 1260, 620, f"{tmp}_face.png")
    detail = render_view(cam, (-0.55, -1.0, 0.45), (0.05, -0.05, 0.3), 50, 840, 620, f"{tmp}_detail.png")
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
