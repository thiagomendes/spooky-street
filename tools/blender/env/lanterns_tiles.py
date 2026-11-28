"""The paper lantern picker's pictures (living-street §4.2): one small transparent picture per shape and one per
colour, 256 x 256, rendered from the exported lantern FBX files and the committed `Lanterns` atlas.

  lantern_shape_<bat|cat|ghost|leaf>.png      the lantern lit, in NEUTRAL near-white paper with its ink face and an
                                              ink outline: the picker multiplies it by the chosen colour
                                              (`Image.color`), so one picture serves every colour; the ink stays ink
  lantern_colour_<orange|plum|green|yellow>.png   a square of that tissue paper, lit, with one corner folded over and
                                              an ink outline, in its own colour (draw it with `Image.color` white)

Run: blender -b -t 2 --python tools/blender/env/lanterns_tiles.py -- [out_dir] [models_dir] [size=256]"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

import env_export as ex
import env_lib as el
import lanterns as ln
from env_lib import lin

REPO = ex.REPO
OUT = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "UI", "LanternPicker")
INK = (0.118, 0.102, 0.149)  # #1E1A26 (sRGB, as the tile's pixels)
OUTLINE = 5  # px at 256


def material(folder, glow):
    mat = bpy.data.materials.new(f"Tile_{glow}")
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    base = nodes.new("ShaderNodeTexImage")
    base.image = bpy.data.images.load(os.path.join(folder, "Lanterns_BaseColor.png"), check_existing=True)
    if glow:  # the lit paper: its emission map, as it is (neutral)
        te = nodes.new("ShaderNodeTexImage")
        te.image = bpy.data.images.load(os.path.join(folder, "Lanterns_Emission.png"), check_existing=True)
        bsdf.inputs["Base Color"].default_value = (0, 0, 0, 1)
        links.new(te.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 1.0
    else:
        links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.9
    return mat


def outlined(px, size):
    """Composite the picture over an ink silhouette grown by OUTLINE pixels (the cards' ink edge)."""
    a = px[:, :, 3]
    grown = a.copy()
    r = OUTLINE
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r:
                grown = np.maximum(grown, np.roll(np.roll(a, dy, 0), dx, 1))
    out = np.zeros_like(px)
    out[:, :, :3] = px[:, :, :3] * a[..., None] + np.array(INK) * (1 - a[..., None])
    out[:, :, 3] = np.maximum(a, grown)
    return out


def paper_square(rgb):
    """A square of tissue with one corner folded over toward the viewer, lit from behind like the lanterns."""
    s = 0.1
    bm = bmesh.new()
    sheet = [bm.verts.new(p) for p in ((-s, 0, -s), (s, 0, -s), (s, 0, s * 0.25), (s * 0.25, 0, s), (-s, 0, s))]
    bm.faces.new(sheet)
    flap = [bm.verts.new(p) for p in ((s * 0.25, -0.004, s), (s, -0.004, s * 0.25), (s * 0.08, -0.03, s * 0.08))]
    bm.faces.new(flap)
    me = bpy.data.meshes.new("Swatch")
    bm.to_mesh(me)
    obj = bpy.data.objects.new("Swatch", me)
    el.pm.link(obj)
    mat = bpy.data.materials.new("Swatch_" + str(rgb))
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    coord = nodes.new("ShaderNodeTexCoord")
    dist = nodes.new("ShaderNodeVectorMath")
    dist.operation = "LENGTH"
    links.new(coord.outputs["Object"], dist.inputs[0])
    fall = nodes.new("ShaderNodeMapRange")
    fall.inputs["From Max"].default_value = 0.15
    fall.inputs["To Min"].default_value = 1.0
    fall.inputs["To Max"].default_value = 0.62
    links.new(dist.outputs["Value"], fall.inputs["Value"])
    col = nodes.new("ShaderNodeMix")
    col.data_type, col.blend_type = "RGBA", "MULTIPLY"
    col.inputs[0].default_value = 1.0
    col.inputs[6].default_value = lin(rgb)
    links.new(fall.outputs["Result"], col.inputs[7])
    links.new(col.outputs[2], bsdf.inputs["Emission Color"])
    bsdf.inputs["Base Color"].default_value = (0, 0, 0, 1)
    bsdf.inputs["Emission Strength"].default_value = 1.0
    me.materials.append(mat)
    # the fold: the flap is the paper's back, a shade deeper
    me.polygons[1].material_index = 1
    back = mat.copy()
    back.node_tree.nodes["Mix"].inputs[6].default_value = lin(el.shade(rgb, 0.78))
    me.materials.append(back)
    return obj


def main():
    rest = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = dict(a.split("=", 1) for a in rest if "=" in a)
    dirs = [a for a in rest if "=" not in a]
    out = dirs[0] if dirs else OUT
    folder = dirs[1] if len(dirs) > 1 else ex.OUT_DIR
    size = int(opts.get("size", 256))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cam = el.setup_render(samples=32)
    scene = bpy.context.scene
    scene.render.film_transparent = True
    scene.eevee.use_bloom = False
    scene.eevee.use_gtao = False
    world = el.night_world(0.9, top=(0.9, 0.9, 1.0), horizon=(0.9, 0.9, 1.0))
    el.sun_light("Key", (0.6, 1.5, -1.0), 2.5, (1.0, 0.95, 0.9), 6.0, 10.0)
    os.makedirs(out, exist_ok=True)
    mats = {g: material(folder, g) for g in (False, True)}
    inner = int(size - 2 * (OUTLINE + 4))
    for shape in ln.SHAPES:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(folder, ln.MODELS[shape] + ".fbx"))
        new = [o for o in bpy.data.objects if o not in before]
        for o in new:
            if o.type == "MESH":
                o.data.materials.clear()
                o.data.materials.append(mats[o.name.split(".")[0].endswith(ex.GLOW_SUFFIX)])
        bpy.context.view_layer.update()
        pts = [o.matrix_world @ Vector(c) for o in new if o.type == "MESH" for c in o.bound_box]
        lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        centre = (lo + hi) / 2
        span = max(hi.x - lo.x, hi.z - lo.z) * size / inner
        a = math.radians(16)
        loc = centre + 3.0 * Vector((math.sin(a), -math.cos(a), 0.12))
        px = el.render_view(cam, loc, centre, 50, size, size, os.path.join(out, "_tmp.png"), ortho=span * 1.02)
        el.co.save_sheet(outlined(px, size), os.path.join(out, f"lantern_shape_{shape}.png"))
        for o in new:
            bpy.data.objects.remove(o, do_unlink=True)
        print("tile", shape, flush=True)
    for cid, (_paper, light) in ln.COLOURS.items():
        sq = paper_square(light)
        sq.rotation_euler = (0, math.radians(-8), 0)
        bpy.context.view_layer.update()
        px = el.render_view(cam, Vector((0.25, -3.0, 0.2)), Vector((0, 0, 0)), 50, size, size, os.path.join(out, "_tmp.png"),
                            ortho=0.2 * size / inner * 1.12)
        el.co.save_sheet(outlined(px, size), os.path.join(out, f"lantern_colour_{cid}.png"))
        bpy.data.objects.remove(sq, do_unlink=True)
        print("tile", cid, flush=True)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.9


if __name__ == "__main__":
    main()
