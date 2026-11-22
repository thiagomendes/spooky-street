"""Review sheet of the GAME environment assets: re-imports the FBX files written by `env_export.py` and renders
them with plain Principled materials on the baked atlases (back-face culling on, like URP Lit), plus emission on
`*_Glow` objects, in the lighting of the approved review sheets.

Run: blender -b -t 2 --python tools/blender/env/env_game_review.py -- out.png [draft|final] [models_dir] [view ...]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np
from mathutils import Matrix, Vector

import env_lib as el
import env_export as ex
import owl_house as oh
from env_lib import H

GLOW = {"AtticWindow": 3.4, "SideWindow": 1.3, "Lamp": 2.6, "Face": 2.6, "PaperBat": 1.7}
# name, frame width (m), look-at height, yaw, exposure
TILES = [
    ("Bench", 2.3, 0.5, 30), ("CandyBowl", 1.0, 0.27, 20), ("FenceSection", 3.0, 0.55, 24), ("FenceGate", 2.0, 0.55, 24),
    ("Hedge", 3.3, 0.6, 30),
    ("StreetLamp", 4.0, 1.75, 20), ("PartyTable", 2.4, 0.55, 28), ("PaperBat", 0.5, 0.0, 16), ("Crate", 1.3, 0.3, 32),
    ("Awning", 4.0, 1.3, 34),
    ("PlankBridge", 3.7, 0.6, 34), ("Maple", 10.5, 3.9, 22), ("TreehousePlatform", 5.0, 2.6, 22), ("Pumpkin", 0.7, 0.16, 14),
    ("JackOLantern", 1.0, 0.27, 14),
    ("Mailbox", 2.0, 0.75, 30), ("Kit_Floor", 3.2, 0.4, 28), ("Kit_Steps", 2.8, 0.3, 28), ("Kit_Post", 2.8, 1.1, 20),
    ("Kit_Rail", 2.8, 0.5, 24),
    ("Kit_Wall", 3.3, 1.1, 24), ("Kit_Window", 3.3, 1.1, 24), ("Kit_Door", 3.3, 1.1, 24), ("Kit_Roof", 3.6, 0.8, 30),
    ("Kit_Corner", 2.8, 1.1, 40),
]
SPACING = 16.0


def image(path, colour=True):
    img = bpy.data.images.load(path)
    if not colour:
        img.colorspace_settings.name = "Non-Color"
    return img


def atlas_material(folder, atlas, glow=0.0):
    mat = bpy.data.materials.new(f"{atlas}_{glow}")
    mat.use_nodes = True
    mat.use_backface_culling = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.8
    emit = nodes.new("ShaderNodeTexImage")
    emit.image = image(os.path.join(folder, atlas + "_Emission.png"))
    links.new(emit.outputs["Color"], bsdf.inputs["Emission Color"])
    if glow:
        bsdf.inputs["Base Color"].default_value = (0, 0, 0, 1)
        bsdf.inputs["Emission Strength"].default_value = glow
        return mat
    bsdf.inputs["Emission Strength"].default_value = 1.0
    base = nodes.new("ShaderNodeTexImage")
    base.image = image(os.path.join(folder, atlas + "_BaseColor.png"))
    links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
    normal_path = os.path.join(folder, atlas + "_Normal.png")
    if os.path.exists(normal_path):
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = image(normal_path, colour=False)
        nm = nodes.new("ShaderNodeNormalMap")
        links.new(tex.outputs["Color"], nm.inputs["Color"])
        links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


class Library:
    def __init__(self, folder):
        self.folder, self.mats, self.parts = folder, {}, {}

    def material(self, atlas, glow=0.0):
        key = (atlas, glow)
        if key not in self.mats:
            self.mats[key] = atlas_material(self.folder, atlas, glow)
        return self.mats[key]

    def load(self, model, group):
        """Import <model>.fbx under a new root empty named `group`; returns (root, [glow objects])."""
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(self.folder, model + ".fbx"))
        new = [o for o in bpy.data.objects if o not in before]
        root = bpy.data.objects.get(group)
        if root is None:
            root = bpy.data.objects.new(group, None)
            el.pm.link(root)
        glows, tris = [], 0
        atlas = "OwlHouse" if model == "OwlHouse" else "Env"
        for o in new:
            if o.parent is None:
                o.parent = root
            if o.type != "MESH":
                continue
            tris += len(o.data.polygons)
            o.data.materials.clear()
            if o.name.split(".")[0].endswith(ex.GLOW_SUFFIX):
                tag = o.name.split(".")[0][:-len(ex.GLOW_SUFFIX)]
                o.data.materials.append(self.material(atlas, GLOW.get(tag, 2.0)))
                glows.append((tag, o))
            else:
                o.data.materials.append(self.material(atlas))
        names = sorted(o.name for o in new)
        print(f"IMPORTED {model}: {tris} tris, objects {names}", flush=True)
        self.parts[model] = {o.name.split(".")[0]: o for o in new}
        return root, glows


def centre(obj):
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    return sum(pts, Vector()) / 8, max((p - pts[0]).length for p in pts)


def prop_lights(glows):
    for tag, o in glows:
        c, size = centre(o)
        if tag == "Lamp":
            el.point_light("LampLight", c + Vector((0, 0, -0.8)), 60, (1.0, 0.74, 0.42), 0.12, shadow=True)
        elif tag == "Face":
            el.point_light("FaceLight", c + Vector((0, -size * 0.9, 0)), 2.5 * (size / 0.4) ** 2, (1.0, 0.62, 0.25), 0.06, shadow=False)
        elif tag == "PaperBat":
            el.point_light("BatLight", c + Vector((0, -0.2, 0.04)), 0.35, (1.0, 0.8, 0.4), 0.03, shadow=False)


def house_lights(glows):
    for tag, o in glows:
        c, _size = centre(o)
        if tag == "AtticWindow":
            el.point_light("AtticGlow", c + Vector((0, -0.85, 0)), 35, (1.0, 0.78, 0.45), 0.3, shadow=False)
        elif tag == "SideWindow":
            el.point_light("SideWindowGlow", c + Vector((-0.6, 0, 0)), 10, (1.0, 0.7, 0.4), 0.2, shadow=False)
        elif tag == "Lamp":
            el.point_light("PorchLamp", c + Vector((0.0, -0.1, -0.42)), 55, (1.0, 0.7, 0.38), 0.12, shadow=True)
    for (x, y, z), rad in (((-0.62, -2.45, 0.2), 0.135), ((0.62, -2.16, 0.4), 0.125), ((-1.45, -2.8, 0.0), 0.3), ((1.38, -2.62, 0.0), 0.2),
                           ((1.5, -0.55, 0.6), 0.19), ((-2.45, -1.2, 0.6), 0.16)):
        el.point_light("JackGlow", (x, y - rad * 1.5, z + rad * 0.8), 1.2 + 14 * rad * rad, (1.0, 0.6, 0.22), 0.05, shadow=False)


def set_lights(names):
    for o in bpy.data.objects:
        if o.type == "LIGHT" and o.data.type == "SUN":
            o.hide_render = o.name.split(".")[0] not in names


def main():
    out, quality, rest = el.args()
    folder = rest[0] if rest and os.path.isdir(rest[0]) else ex.OUT_DIR
    todo = [r for r in rest if not os.path.isdir(r)]
    final = quality == "final"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    lib = Library(folder)
    craft = el.EnvCraft(1.2)
    el.stage_ground(craft, 400.0)
    cam = el.setup_render(samples=40 if final else 10, exposure=0.0)
    el.night_world(0.7, top=H(0x241B4A), horizon=H(0x4A3378))
    # house sheet lights (owl_house.sheet_lights) and prop sheet lights (props.main), switched per view
    el.sun_light("Moon", (1.2, 2.2, -2.0), 2.0, (0.72, 0.76, 1.0), 6.0, 60.0)
    el.sun_light("Rim", (-2.2, -1.6, -1.2), 1.6, (0.6, 0.55, 1.0), 6.0, 60.0)
    el.sun_light("Fill", (2.4, 1.2, -0.4), 0.55, (1.0, 0.8, 0.7), 6.0, 60.0)
    el.sun_light("PKey", (1.6, 2.6, -1.9), 3.0, (1.0, 0.88, 0.76), 8.0, 30.0)
    el.sun_light("PRim", (-1.9, -2.4, -1.4), 2.2, (0.66, 0.66, 1.0), 8.0, 30.0)
    el.sun_light("PFill", (-2.4, 1.6, -0.5), 0.7, (0.8, 0.8, 1.0), 8.0, 30.0)
    house, glows = lib.load("OwlHouse", "House")
    _same, dglows = lib.load("OwlHouseDressing", "House")
    house.location = ex.HOUSE_ORIGIN
    bpy.context.view_layer.update()
    house_lights(glows + dglows)
    for i, (name, *_r) in enumerate(TILES):
        root, pglows = lib.load(name, "G_" + name)
        root.location = (i * SPACING, 100.0, 0.0)
        bpy.context.view_layer.update()
        prop_lights(pglows)
    k = 1.0 if final else 0.5
    tmp = os.path.splitext(out)[0]
    views = {
        "hero": ((-10.5, -15.5, 4.6), (-0.1, 1.2, 4.3), 44, 1150, 900),
        "front": ((0.0, -40.0, 3.4), (0.0, 0.0, 4.75), 116, 850, 900),
        "side": ((-42.0, 2.0, 3.4), (0.0, 2.0, 4.75), 104, 1000, 720),
        "door": ((-1.6, -7.2, 1.75), (-0.05, 0.0, 1.62), 60, 500, 720),
        "owl": ((-3.4, -7.0, 6.9), (0.0, 0.3, 7.65), 50, 500, 720),
    }
    tiles = {}
    set_lights({"Moon", "Rim", "Fill"})
    world = bpy.context.scene.world.node_tree.nodes["Background"]
    for name, (loc, target, lens, w, h) in views.items():
        if todo and name not in todo:
            continue
        el.solo(["House"])
        tiles[name] = el.render_view(cam, loc, target, lens, int(w * k), int(h * k), f"{tmp}_{name}.png")
        print("rendered", name, flush=True)
    set_lights({"PKey", "PRim", "PFill"})
    world.inputs[1].default_value = 0.9
    bpy.context.scene.view_settings.exposure = -0.1
    px = int(400 * k)
    grid = []
    for i, (name, frame, look, yaw) in enumerate(TILES):
        if todo and name not in todo and "grid" not in todo:
            continue
        el.solo(["G_" + name])
        lens = 70
        dist = frame * lens / 36
        elev, a = math.radians(13), math.radians(yaw)
        target = Vector((i * SPACING, 100.0, look))
        loc = target + Vector((-math.sin(a) * math.cos(elev), -math.cos(a) * math.cos(elev), math.sin(elev))) * dist
        grid.append(el.render_view(cam, loc, target, lens, px, px, f"{tmp}_{name}.png"))
        print("rendered", name, flush=True)
    # pivots: the animated children posed about their own origins
    posed = []
    if not todo or "pose" in todo:
        def turn(model, part, axis, deg):
            o = lib.parts[model][part]
            print(f"PIVOT {model}/{part}: local {tuple(round(c, 3) for c in o.matrix_local.translation)}", flush=True)
            o.matrix_local = o.matrix_local @ Matrix.Rotation(math.radians(deg), 4, axis)
        turn("OwlHouse", "FrontDoor", "Z", -75)
        turn("OwlHouse", "OwlPuppet", "X", -28)
        turn("OwlHouse", "WindWheel", "Y", 22)
        turn("FenceGate", "Gate", "Z", -60)
        turn("Kit_Door", "FrontDoor", "Z", -50)
        set_lights({"Moon", "Rim", "Fill"})
        world.inputs[1].default_value = 0.7
        bpy.context.scene.view_settings.exposure = 0.0
        el.solo(["House"])
        q = int(500 * k)
        posed.append(el.render_view(cam, (-1.6, -7.2, 1.75), (-0.05, 0.0, 1.62), 60, q, q, f"{tmp}_p1.png"))
        posed.append(el.render_view(cam, (-3.4, -7.0, 6.9), (0.0, 0.3, 7.65), 50, q, q, f"{tmp}_p2.png"))
        set_lights({"PKey", "PRim", "PFill"})
        world.inputs[1].default_value = 0.9
        for name in ("FenceGate", "Kit_Door"):
            i = [t[0] for t in TILES].index(name)
            _n, frame, look, yaw = TILES[i]
            el.solo(["G_" + name])
            dist, elev, a = frame * 70 / 36, math.radians(13), math.radians(yaw + 25)
            target = Vector((i * SPACING, 100.0, look))
            loc = target + Vector((-math.sin(a) * math.cos(elev), -math.cos(a) * math.cos(elev), math.sin(elev))) * dist
            posed.append(el.render_view(cam, loc, target, 70, q, q, f"{tmp}_p_{name}.png"))
    rows = []
    if all(n in tiles for n in views):
        rows.append(np.concatenate([tiles["hero"], tiles["front"]], axis=1))
        rows.append(np.concatenate([tiles["side"], tiles["door"], tiles["owl"]], axis=1))
    elif tiles:
        width = int(2000 * k)
        rows.extend(el.pad(t, width, t.shape[0]) for t in tiles.values())
    cols = 5
    while grid and len(grid) % cols:
        blank = np.zeros_like(grid[0])
        blank[:, :, :3], blank[:, :, 3] = (0.02, 0.016, 0.05), 1
        grid.append(blank)
    rows.extend(np.concatenate(grid[r:r + cols], axis=1) for r in range(0, len(grid), cols))
    if posed:
        rows.append(np.concatenate(posed, axis=1))
    width = max(r.shape[1] for r in rows)
    rows = [el.pad(r, width, r.shape[0]) for r in rows]
    el.co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
    print("game review sheet ->", out)


if __name__ == "__main__":
    main()
