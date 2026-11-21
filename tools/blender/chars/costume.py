"""Shared costume kit for the Spooky Street cast: the vivid Halloween palette, lathed cloth, felt cut-outs,
shader variations of craft materials, and the review-sheet stage (spec §2 v1.3, D17/D18)."""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars"):
    if os.path.join(TOOLS, sub) not in sys.path:
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
              folds=0, fold_depth=0.0, fold_power=1.5):
    """Elliptical lathe of rings (rx, z[, yscale[, open_deg]]) around Z, optionally open at the front (-Y).

    `hem(t)` offsets the last ring's height along the span t = 0..1; `folds` ripples the cloth that many times
    across the span, deepening toward the last ring (a small `fold_power` keeps them nearly even); `inner` lines
    the inside with a second material."""
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
            fold = 1.0 + fold_depth * t_ring ** fold_power * math.cos(2 * math.pi * folds * t) if folds else 1.0
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


def leaf_outline(length):
    half = [(0.0, 0.3), (0.3, 0.12), (0.32, -0.2), (0.14, -0.5), (0.0, -0.7)]
    pts = [(x * length, z * length) for x, z in half] + [(-x * length, z * length) for x, z in reversed(half[1:-1])]
    return pm.smooth_outline(pts, 4)


def rods(name, mat, segments, radius):
    """One mesh of thin straight rods (spokes, fringe, loose threads): `segments` is a list of (start, end)."""
    bm = bmesh.new()
    for a, b in segments:
        a, b = Vector(a), Vector(b)
        d = b - a
        matrix = Matrix.Translation((a + b) / 2) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=radius, radius2=radius, depth=d.length,
                              matrix=matrix)
    return pm.new_mesh_object(name, bm, mat)


def band(name, mat, centre, rx, width, ys=1.0, tilt=(0, 0, 0), thickness=0.005):
    """A strip of cloth wrapped once around something: a belt, a boot shaft, a bandage turn."""
    obj = ell_lathe(name, mat, [(rx, width / 2, ys), (rx * 1.03, 0.0, ys), (rx, -width / 2, ys)],
                    thickness=thickness, segments=40)
    obj.location = centre
    obj.rotation_euler = tilt
    return obj


def applique(name, mat, cast, outline, at, thickness=0.004, craft=None, stitch=None):
    """Felt cut-out sewn onto a front-facing surface: `outline` (x, z) around `at` is projected along +Y with
    `cast` (see anatomy.probe). `stitch` adds a running stitch in that colour just inside the edge."""
    def project(scale, lift):
        hits = [cast((at[0] + x * scale, -3.0, at[1] + z * scale), (0, 1, 0)) for x, z in outline]
        return [point + normal * lift for point, normal in hits]
    obj = an.patch(name, mat, project(1.0, thickness + 0.0005), thickness, fan=False)
    if stitch is not None:
        craft.stitches(name + "Stitch", project(0.74, thickness + 0.0015), stitch, spacing=0.012, dash=0.007,
                       radius=0.0016, closed=True)
    return obj


def cap(name, mat, P, radii=(1.1, 1.12, 1.1), offset=(0.0, 0.04, 0.1), fringe_z=0.3, length_z=-0.6, face_half=50.0,
        thickness=0.09, lobes=0, lobe_depth=0.0, fall=None, stretch=1.0, segments=48):
    """A shell over the skull, open around the face: a head of hair, a helmet, a scarf, a bandage cap.

    All sizes are in head radii: `fringe_z` and `length_z` are the heights of the front and the lower edge
    relative to the head centre, `face_half` the half-angle of the face opening in degrees, `fall` keeps the
    sides at least that wide below the widest point (hair hanging straight), `stretch` lengthens the lower half
    (long hair), `lobes` adds that many soft ridges.
    Returns (object, on): on(lon, el, lift) is a point on the outside at longitude `lon` (0 = front)."""
    r, hc = P.head_r, P.head_c
    centre = hc + Vector(offset) * r

    def shape(unit, lift=1.0):
        flat = math.hypot(unit.x, unit.y)
        k = 1.0
        if flat > 1e-6:
            if fall and unit.z < 0:
                k = max(flat, fall) / flat
            if lobes:
                lon = math.atan2(unit.x, -unit.y)
                k *= 1 + lobe_depth * min(1.0, 1 - unit.z) * (abs(math.cos(lobes * lon / 2)) ** 0.7 - 0.6)
        zz = unit.z * (stretch if unit.z < 0 else 1.0)
        return Vector((unit.x * k * radii[0], unit.y * k * radii[1], zz * radii[2])) * r * lift + centre

    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=32, radius=1.0)
    doomed = []
    for f in bm.faces:
        c = f.calc_center_median()
        rel_z = c.z * (stretch if c.z < 0 else 1.0) * radii[2] + offset[2]
        in_face = abs(math.atan2(c.x, -c.y)) < math.radians(face_half)
        if rel_z < length_z or (in_face and rel_z < fringe_z):
            doomed.append(f)
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    for v in bm.verts:
        v.co = shape(v.co)
    obj = pm.new_mesh_object(name, bm, mat)
    sol = obj.modifiers.new("Thick", "SOLIDIFY")
    sol.thickness = r * thickness
    sol.offset = 1.0
    obj.modifiers.new("Smooth", "SUBSURF").levels = 2

    def on(lon, el, lift=1.0):
        a = lon - math.pi / 2
        unit = Vector((math.cos(el) * math.cos(a), math.cos(el) * math.sin(a), math.sin(el)))
        return shape(unit, lift * (1 + thickness / max(radii)))
    return obj, on


def hang(name, mat, grip, top, radius=0.0035):
    """Wire handle from two points on a held thing up through the gripping hand."""
    a, b = Vector(top[0]), Vector(top[1])
    lift = Vector((0, 0, (grip.z - a.z) * 0.6))
    pm.curve_tube(name, mat, [a, a + lift, grip, b + lift, b], radius, taper=(1, 1))


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


def stage(lineup=False):
    """Night stage, lights and camera. `lineup` lights a whole row of characters evenly instead of one."""
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
    bmesh.ops.create_circle(bm, cap_ends=True, segments=64, radius=80.0 if lineup else 2.0)
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

    def sun(name, direction, strength, colour):
        data = bpy.data.lights.new(name, "SUN")
        data.energy, data.color, data.angle = strength, colour, math.radians(12)
        obj = bpy.data.objects.new(name, data)
        obj.rotation_euler = Vector(direction).to_track_quat("-Z", "Y").to_euler()
        pm.link(obj)

    if lineup:  # the same three directions as the single-character lights, as parallel light
        sun("Key", (1.8, 2.6, -1.7), 2.6, (1.0, 0.86, 0.72))
        sun("Rim", (-1.9, -2.4, -1.4), 2.6, (0.72, 0.66, 1.0))
        sun("Fill", (-2.4, 1.6, -0.3), 0.9, (0.8, 0.82, 1.0))
    else:
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


def save_sheet(sheet, out):
    """Write an RGBA float array (rows bottom-up) as a PNG."""
    img = bpy.data.images.new("Sheet", sheet.shape[1], sheet.shape[0], alpha=True)
    img.pixels.foreach_set(sheet.ravel())
    img.filepath_raw = out
    img.file_format = "PNG"
    img.save()
