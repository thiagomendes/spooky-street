"""Style exploration for the visual identity (owner choice: handcrafted & stitched).

Renders one small, fully original scene: Pip on a hand-painted porch corner with a felt pumpkin, a pleated
paper lantern, a paper bat pinned to the post, clapboard wall with a felt-curtained window, at twilight.

    blender -b --python tools/blender/style/style_sample.py -- <out_prefix> [intensity ...]

Each intensity (0.5 = subtle craft, 1.0 = pronounced craft) writes <out_prefix>_<intensity>.png.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Euler, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "pip"))

import craft as craft_lib  # noqa: E402
import pip_model  # noqa: E402

hex_rgb = craft_lib.hex_rgb
INDIGO = hex_rgb(0x394461)
TWILIGHT = hex_rgb(0x344767)
AMBER = hex_rgb(0xF6AD58)
PAPER = hex_rgb(0xF4E6C8)
SILVER = hex_rgb(0xDAE2EE)
TIMBER = hex_rgb(0x7A5C48)
HEDGE = hex_rgb(0x2E4A43)
INK = hex_rgb(0x1E2233)
PUMPKIN = hex_rgb(0xE0873A)
STEM = hex_rgb(0x6B4A36)
CURTAIN = hex_rgb(0x8E4E52)


def link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj


def mesh_obj(name, bm, mat, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = smooth
    obj = bpy.data.objects.new(name, me)
    obj.data.materials.append(mat)
    return link(obj)


def board(name, mat, centre, size, rot=(0, 0, 0), bevel=0.006):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    obj = mesh_obj(name, bm, mat, smooth=False)
    obj.location = centre
    obj.rotation_euler = rot
    b = obj.modifiers.new("Bevel", "BEVEL")
    b.width = bevel
    b.segments = 3
    obj.modifiers.new("WN", "WEIGHTED_NORMAL")
    return obj


def pip_factory(craft):
    def make(key, rgb, roughness, emission, sheen):
        if key in ("fuzz", "fuzz_shadow", "brow"):
            return craft.felt(rgb)
        if key in ("stripe", "tights", "sock_a", "sock_b"):
            return craft.knit(rgb)
        if key in ("cape", "cape_trim"):
            return craft.waxed(rgb)
        if key in ("wing", "amber"):
            return craft.paper(rgb, glow=emission)
        if key in ("skin", "cheek"):
            return craft.smooth(rgb, 0.55, subsurface=0.2)
        if key in ("ink", "white"):
            return craft.smooth(rgb, roughness, emission)
        return craft.smooth(rgb, 0.5)
    return make


def build_porch(craft, rnd):
    wood = craft.painted_wood(TIMBER)
    trim = craft.painted_wood(PAPER)
    wall = craft.painted_wood(INDIGO)
    # Floor planks, each slightly uneven: hand-built, not CAD.
    y = -0.7
    while y < 0.78:
        board("Plank", wood, (rnd.uniform(-0.03, 0.03), y + 0.075, 0.27 + rnd.uniform(-0.004, 0.004)),
              (2.5 + rnd.uniform(-0.04, 0.04), 0.148, 0.06), (0, 0, math.radians(rnd.uniform(-0.5, 0.5))))
        y += 0.158
    board("Fascia", craft.painted_wood(INDIGO), (0, -0.72, 0.15), (2.56, 0.04, 0.3))
    for i, (yy, zz) in enumerate(((-0.86, 0.19), (-1.0, 0.08))):
        board("Step", wood, (0.0, yy, zz), (1.1, 0.16, 0.05), (0, 0, math.radians(rnd.uniform(-1, 1))))
        board("StepRiser", craft.painted_wood(INDIGO), (0.0, yy + 0.02, zz / 2), (1.06, 0.12, zz))
    # Clapboard wall with overlapping boards.
    z = 0.3
    while z < 2.5:
        board("Clapboard", wall, (0, 0.82, z + 0.1), (2.6, 0.035, 0.21), (math.radians(-5), 0, 0))
        z += 0.175
    # Window with painted frame, warm pane and felt curtains.
    wx, wz = -0.55, 1.32
    for dx, dz, sx, sz in ((0, 0.42, 0.78, 0.08), (0, -0.42, 0.86, 0.1), (-0.38, 0, 0.08, 0.84), (0.38, 0, 0.08, 0.84),
                           (0, 0, 0.04, 0.8), (0, 0, 0.76, 0.04)):
        board("WindowFrame", trim, (wx + dx, 0.775, wz + dz), (sx, 0.05, sz))
    pane = board("WindowPane", craft.smooth(AMBER, 0.3, emission=1.6), (wx, 0.79, wz), (0.72, 0.01, 0.78))
    pane.modifiers.clear()
    for side in (-1, 1):
        bm = bmesh.new()
        cols, rows = 10, 8
        verts = [[bm.verts.new((wx + side * 0.36 - side * 0.22 * c / cols, 0.765 + 0.012 * math.sin(c * 1.9),
                                wz + 0.38 - 0.76 * r / rows)) for c in range(cols + 1)] for r in range(rows + 1)]
        for r in range(rows):
            for c in range(cols):
                bm.faces.new((verts[r][c], verts[r][c + 1], verts[r + 1][c + 1], verts[r + 1][c]))
        curtain = mesh_obj("Curtain", bm, craft.felt(CURTAIN))
        curtain.modifiers.new("Thick", "SOLIDIFY").thickness = 0.01
        curtain.modifiers.new("Smooth", "SUBSURF").levels = 1
    # Corner post, beam and side railing.
    board("Post", trim, (1.15, -0.62, 1.25), (0.12, 0.12, 1.9))
    board("PostCap", trim, (1.15, -0.62, 2.22), (0.17, 0.17, 0.05))
    board("Beam", trim, (0.0, -0.62, 2.28), (2.5, 0.1, 0.1))
    board("RailTop", trim, (1.15, 0.1, 0.92), (0.07, 1.38, 0.05))
    board("RailBottom", trim, (1.15, 0.1, 0.42), (0.05, 1.38, 0.04))
    yy = -0.48
    while yy < 0.78:
        board("Baluster", trim, (1.15, yy, 0.67), (0.035, 0.035, 0.48), (0, 0, math.radians(rnd.uniform(-2, 2))))
        yy += 0.13


def build_pumpkin(craft, name, centre, radius, rnd, colour=PUMPKIN):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=1.0)
    k = craft.k
    for v in bm.verts:
        a = math.atan2(v.co.y, v.co.x)
        lobe = 1.0 - 0.1 * (1.0 - abs(math.cos(4 * a))) ** 0.8
        flat = 0.72
        v.co = Vector((v.co.x * radius * lobe, v.co.y * radius * lobe, v.co.z * radius * flat))
        if v.co.z > radius * 0.6:
            v.co.z -= (v.co.z - radius * 0.6) * 0.6  # dimple at the stem
    obj = mesh_obj(name, bm, craft.felt(colour))
    obj.modifiers.new("Smooth", "SUBSURF").levels = 1
    obj.location = centre
    # Stitched seams down each lobe valley.
    for s in range(8):
        a = (s + 0.5) * math.pi / 4
        pts = []
        for j in range(15):
            phi = -math.pi / 2 * 0.85 + math.pi * 0.85 * j / 14
            r = radius * 0.905 * math.cos(phi) + 0.004
            pts.append((centre[0] + r * math.cos(a), centre[1] + r * math.sin(a),
                        centre[2] + radius * 0.72 * math.sin(phi) * (0.92 if phi > 0 else 1.0)))
        craft.stitches(name + "Seam", pts, hex_rgb(0x8A4A20), spacing=0.02, dash=0.011, radius=0.0028)
    # Twisted felt stem and a felt leaf.
    stem_pts = [(centre[0], centre[1], centre[2] + radius * 0.48), (centre[0] + 0.01, centre[1], centre[2] + radius * 0.75),
                (centre[0] + 0.04, centre[1] - 0.01, centre[2] + radius * 0.92)]
    cu = bpy.data.curves.new(name + "Stem", "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius * 0.12
    cu.bevel_resolution = 3
    sp = cu.splines.new("BEZIER")
    sp.bezier_points.add(len(stem_pts) - 1)
    for i, p in enumerate(stem_pts):
        sp.bezier_points[i].co = p
        sp.bezier_points[i].handle_left_type = sp.bezier_points[i].handle_right_type = "AUTO"
        sp.bezier_points[i].radius = 1.0 - 0.35 * i / 2
    stem = bpy.data.objects.new(name + "Stem", cu)
    stem.data.materials.append(craft.felt(STEM))
    link(stem)
    leaf = pip_model.ellipsoid(name + "Leaf", craft.felt(HEDGE),
                               (centre[0] - radius * 0.35, centre[1] + 0.02, centre[2] + radius * 0.62),
                               (radius * 0.34, radius * 0.2, radius * 0.03), rotation=(0.2, -0.25, 0.6), subsurf=2)
    return obj


def build_lantern(craft, centre, radius=0.15, height=0.36):
    """Pleated paper lantern glowing amber, hung from the beam by a cord."""
    bm = bmesh.new()
    segs, rings = 40, 72
    grid = []
    for r in range(rings + 1):
        t = r / rings
        base = math.sin(math.pi * (0.08 + 0.84 * t)) ** 0.55
        pleat = 1.0 + 0.045 * craft.k * abs(math.sin(math.pi * 14 * t))
        rad = radius * base * pleat
        z = centre[2] - height / 2 + height * t
        grid.append([bm.verts.new((centre[0] + rad * math.cos(2 * math.pi * s / segs),
                                   centre[1] + rad * math.sin(2 * math.pi * s / segs), z)) for s in range(segs)])
    for r in range(rings):
        for s in range(segs):
            bm.faces.new((grid[r][s], grid[r][(s + 1) % segs], grid[r + 1][(s + 1) % segs], grid[r + 1][s]))
    mesh_obj("PaperLantern", bm, craft.paper(AMBER, glow=0.9))
    # Thin bamboo ribs make the pleats read even when the paper glows.
    for i in range(1, 8):
        t = i / 8
        base = math.sin(math.pi * (0.08 + 0.84 * t)) ** 0.55
        pip_model.torus("LanternRib", craft.smooth(hex_rgb(0x9A6A3A), 0.6),
                        (centre[0], centre[1], centre[2] - height / 2 + height * t), radius * base * 1.02, 0.0028,
                        segments=40, sides=6)
    for dz in (-height / 2 + 0.012, height / 2 - 0.012):
        pip_model.torus("LanternRim", craft.smooth(INK, 0.5), (centre[0], centre[1], centre[2] + dz),
                        radius * 0.42, 0.012, segments=32, sides=8)
    pip_model.curve_tube("LanternCord", craft.smooth(INK, 0.7), [
        (centre[0], centre[1], centre[2] + height / 2), (centre[0], centre[1], 2.23)], 0.004, taper=(1, 1))
    light = bpy.data.lights.new("LanternLight", "POINT")
    light.energy = 70
    light.color = (1.0, 0.68, 0.32)
    light.shadow_soft_size = 0.12
    obj = bpy.data.objects.new("LanternLight", light)
    obj.location = centre
    link(obj)


def build_paper_bat(craft, centre, scale=0.22):
    """Ink paper bat cut-out pinned to the post (spec §1: the bat that falls sideways)."""
    half = [(0.0, 0.18), (0.06, 0.2), (0.12, 0.12), (0.28, 0.22), (0.5, 0.14), (0.62, 0.02), (0.52, -0.02),
            (0.44, -0.1), (0.33, -0.04), (0.24, -0.14), (0.14, -0.06), (0.05, -0.16), (0.0, -0.12)]
    outline = [(x, z) for x, z in half] + [(-x, z) for x, z in reversed(half[1:-1])]
    bm = bmesh.new()
    verts = [bm.verts.new((x * scale, 0.0, z * scale)) for x, z in outline]
    bm.faces.new(verts)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bat = mesh_obj("PaperBat", bm, craft.paper(INK), smooth=False)
    bat.location = centre
    bat.rotation_euler = (math.radians(6), 0, math.radians(-14))
    bat.modifiers.new("Thick", "SOLIDIFY").thickness = 0.003
    bend = bat.modifiers.new("Curl", "SIMPLE_DEFORM")
    bend.deform_method = "BEND"
    bend.angle = math.radians(35 * craft.k)
    bend.deform_axis = "Z"
    for s in (-1, 1):
        pip_model.ellipsoid("BatEye", craft.smooth(AMBER, 0.4, emission=0.6),
                            (centre[0] + s * 0.018, centre[1] - 0.006, centre[2] + 0.01), (0.008, 0.003, 0.008),
                            subsurf=0)
    pip_model.ellipsoid("BatPin", craft.smooth(SILVER, 0.2), (centre[0], centre[1] - 0.008, centre[2] + 0.03),
                        (0.01, 0.006, 0.01), subsurf=0)


def build_stage(craft):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 64
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance = 0.25
    scene.eevee.use_soft_shadows = True
    scene.eevee.use_bloom = True
    scene.eevee.bloom_intensity = 0.06
    scene.eevee.bloom_threshold = 1.2
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    world = bpy.data.worlds.new("Twilight")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = craft_lib.lin(TWILIGHT)
    bg.inputs[1].default_value = 0.45
    scene.world = world
    # Felt lawn in front of the porch.
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=4.0)
    lawn = mesh_obj("Lawn", bm, craft.felt(hex_rgb(0x243A35)))
    lawn.location = (0, -1.5, 0)
    # Felt grass tufts along the porch edge.
    rnd = random.Random(3)
    for i in range(26):
        x = -1.25 + 2.5 * i / 25 + rnd.uniform(-0.04, 0.04)
        y = -0.78 - rnd.uniform(0.0, 0.25)
        if -0.6 < x < 0.6 and y < -0.78:
            y -= 0.35  # keep the steps clear
        for blade in range(3):
            a = rnd.uniform(0, math.tau)
            h = rnd.uniform(0.08, 0.16)
            pip_model.curve_tube("GrassBlade", craft.felt(HEDGE), [
                (x, y, 0.0), (x + 0.02 * math.cos(a), y + 0.02 * math.sin(a), h * 0.6),
                (x + 0.05 * math.cos(a), y + 0.05 * math.sin(a), h)], 0.012, taper=(1.0, 0.15), resolution=4)

    def light(name, kind, loc, energy, colour, size=1.0, target=(0, 0, 0.8)):
        data = bpy.data.lights.new(name, kind)
        data.energy = energy
        data.color = colour
        if kind == "AREA":
            data.size = size
        else:
            data.shadow_soft_size = size
        obj = bpy.data.objects.new(name, data)
        obj.location = loc
        obj.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        return link(obj)

    light("Moon", "AREA", (2.6, 3.2, 3.4), 420, (0.6, 0.7, 1.0), 2.5)          # cool rim from behind
    light("Key", "AREA", (-2.2, -2.6, 2.6), 140, (1.0, 0.82, 0.62), 1.8)       # warm street lamp
    light("Fill", "AREA", (2.8, -2.4, 1.2), 45, (0.65, 0.72, 1.0), 3.0)
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 38
    cam_data.dof.use_dof = True
    cam_data.dof.aperture_fstop = 4.0
    cam = bpy.data.objects.new("Cam", cam_data)
    cam.location = (-1.75, -3.35, 1.35)
    target = Vector((0.12, 0.0, 0.88))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.dof.focus_distance = (target - cam.location).length - 0.2
    link(cam)
    scene.camera = cam
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720


def build_scene(intensity):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    craft = craft_lib.Craft(intensity)
    rnd = random.Random(7)
    pip_model.MATERIAL_FACTORY = pip_factory(craft)
    root = pip_model.build_pip(reset=False)
    root.location = (0.12, -0.22, 0.3)
    root.rotation_euler = (0, 0, math.radians(-24))
    # Extra handmade detail on Pip: a running stitch along the cape trim.
    ring = [(0.262 * math.cos(a), 0.262 * math.sin(a), 0.505)
            for a in [(-math.pi / 2 + math.radians(32)) + (2 * math.pi - math.radians(64)) * i / 80 for i in range(81)]]
    seam = craft.stitches("CapeSeam", ring, hex_rgb(0xF4E6C8), spacing=0.018, dash=0.01, radius=0.0022)
    if seam is not None:
        seam.parent = root
    build_porch(craft, rnd)
    build_pumpkin(craft, "Pumpkin", (-0.72, -0.3, 0.3 + 0.2 * 0.72), 0.2, rnd)
    build_pumpkin(craft, "PumpkinSmall", (-0.42, -0.5, 0.3 + 0.11 * 0.72), 0.11, rnd, colour=hex_rgb(0xF0A548))
    build_lantern(craft, (0.62, -0.6, 1.72))
    build_paper_bat(craft, (1.15, -0.69, 1.55))
    build_stage(craft)


def render_to_array(path):
    import numpy as np
    scene = bpy.context.scene
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return px


def main():
    import numpy as np
    argv = sys.argv[sys.argv.index("--") + 1:]
    prefix = argv[0]
    levels = [float(v) for v in argv[1:]] or [0.5, 1.0]
    columns = []
    for level in levels:
        build_scene(level)
        scene = bpy.context.scene
        wide = render_to_array(f"{prefix}_{level:.1f}_wide.png")
        cam = scene.camera
        cam.location = (-0.62, -1.75, 1.22)
        target = Vector((-0.12, -0.2, 0.95))
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam.data.lens = 50
        cam.data.dof.focus_distance = (target - cam.location).length
        cam.data.dof.aperture_fstop = 2.8
        close = render_to_array(f"{prefix}_{level:.1f}_close.png")
        columns.append(np.concatenate([close, wide], axis=0))  # image rows run bottom-up
        print("style sample ->", level)
    grid = np.concatenate(columns, axis=1)
    out = bpy.data.images.new("StyleGrid", grid.shape[1], grid.shape[0], alpha=True)
    out.pixels.foreach_set(grid.ravel())
    out.filepath_raw = f"{prefix}_grid.png"
    out.file_format = "PNG"
    out.save()
    print("style grid ->", out.filepath_raw)


if __name__ == "__main__":
    main()
