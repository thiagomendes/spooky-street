"""Review sheet of the GAME street dressing: re-imports the FBX files written by `env_export_street.py` and
renders them on the baked textures with back-face culling on (like URP Lit) and emission on `*_Glow` objects.

Top row: a mock of the corner (3 x 3 lawn tiles on a 12 x 12 m grid, path strips, a sidewalk run, boundary hedges,
leaf cards, the owl house for scale, the three neighbours behind) and the ground seen from above. Then one tile
per model, and a flat-lit seam check of tiles laid edge to edge.

Run: blender -b -t 2 --python tools/blender/env/env_street_review.py -- out.png [draft|final] [street_dir] [view ...]
(the owl house always comes from the game folder)."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np
from mathutils import Vector

import env_lib as el
import env_export as ex
import env_game_review as gr
from env_lib import H

STREET = ("PathStones", "Sidewalk", "LeafScatterA", "LeafScatterB", "LeafScatterC", "BoundaryHedgeLong", "NeighbourHouseA",
          "NeighbourHouseB", "NeighbourHouseC")
GLOW = dict(gr.GLOW, NeighbourWindow=1.5, Face=1.8)  # as GlowLevels gives them in game (Default, JackFace)
# name, frame width (m), look-at height, yaw, elevation
TILES = [("NeighbourHouseA", 15.0, 6.2, 28, 8), ("NeighbourHouseB", 15.0, 4.0, 28, 8), ("NeighbourHouseC", 15.0, 5.6, 28, 8),
         ("NeighbourHouseA", 15.0, 6.2, -40, 8), ("NeighbourHouseB", 15.0, 4.0, -40, 8), ("NeighbourHouseC", 15.0, 5.6, -40, 8),
         ("BoundaryHedgeLong", 7.4, 0.5, 24, 14), ("Lawn", 5.6, 0.0, 20, 38), ("PathStones", 2.9, 0.0, 20, 38), ("Sidewalk", 3.3, 0.0, 20, 38),
         ("LeafScatterA", 1.9, 0.0, 15, 42), ("LeafScatterC", 2.9, 0.0, 15, 42)]
SPACING = 40.0


class Library(gr.Library):
    def __init__(self, street, game):
        super().__init__(game)
        self.street, self.count = street, {}

    def material(self, atlas, glow=0.0):
        key = (atlas, glow)
        if key not in self.mats:
            folder = self.street if atlas in ("Lawn", "Street") else self.folder
            self.mats[key] = gr.atlas_material(folder, atlas, glow)
        return self.mats[key]

    def load(self, model, group, at=(0, 0, 0), yaw=0.0):
        atlas = "Lawn" if model == "Lawn" else "Street" if model in STREET else "OwlHouse" if model == "OwlHouse" else "Env"
        folder = self.street if atlas in ("Lawn", "Street") else self.folder
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(folder, model + ".fbx"))
        new = [o for o in bpy.data.objects if o not in before]
        root = bpy.data.objects.get(group)
        if root is None:
            root = bpy.data.objects.new(group, None)
            el.pm.link(root)
        holder = bpy.data.objects.new(f"{model}_at", None)
        el.pm.link(holder, root)
        holder.location, holder.rotation_euler.z = at, math.radians(yaw)
        glows, tris = [], 0
        for o in new:
            if o.parent is None:
                o.parent = holder
            if o.type != "MESH":
                continue
            tris += len(o.data.polygons)
            o.data.materials.clear()
            name = o.name.split(".")[0]
            if name.endswith(ex.GLOW_SUFFIX):
                tag = name[:-len(ex.GLOW_SUFFIX)]
                o.data.materials.append(self.material(atlas, GLOW.get(tag, 1.5)))
                glows.append((tag, o))
            else:
                o.data.materials.append(self.material(atlas))
        if model not in self.count:
            self.count[model] = tris
            print(f"IMPORTED {model}: {tris} tris, objects {sorted(o.name for o in new)}", flush=True)
        bpy.context.view_layer.update()
        return root, glows


def face_lights(glows, k=1.0):
    for tag, o in glows:
        if tag == "Face":
            c, size = gr.centre(o)
            el.point_light("FaceLight", c + Vector((0, -0.5, 0.1)), 4.0 * k, (1.0, 0.62, 0.25), 0.08, shadow=False)


def mock(lib):
    """The corner as the game lays it out, on a 2 m grid: x across, the street toward -Y."""
    g = "Mock"
    for i in (-4, 0, 4):
        for j in (-4, 0, 4, 8, 12):
            lib.load("Lawn", g, (i, j, 0))
    for j in (-5, -3):
        lib.load("PathStones", g, (0, j, 0))
    for i in range(-7, 9, 2):
        lib.load("Sidewalk", g, (i, -7, 0))
    for sx in (-1, 1):
        lib.load("BoundaryHedgeLong", g, (sx * 6.5, -3, 0), 90 * sx)
        lib.load("BoundaryHedgeLong", g, (sx * 6.5, 3, 0), -90 * sx)
    lib.load("LeafScatterA", g, (-2.6, -3.8, 0), 20)
    lib.load("LeafScatterB", g, (2.3, -4.9, 0), 0)
    lib.load("LeafScatterC", g, (1.2, -6.6, 0), -8)
    lib.load("LeafScatterA", g, (3.9, -2.4, 0), 140)
    lib.load("LeafScatterB", g, (-4.4, -5.2, 0), 70)
    _r, glows = lib.load("OwlHouse", g, (0, 2.0, 0))
    gr.house_lights([g for g in glows if g[0] != "Lamp"])
    for o in [o for o in bpy.data.objects if o.name.startswith("JackGlow")]:  # the dressing's pumpkins are not in this mock
        bpy.data.objects.remove(o, do_unlink=True)
    for model, at, yaw in (("NeighbourHouseB", (-13.0, 17.0, 0), -8), ("NeighbourHouseA", (10.5, 19.0, 0), 6),
                           ("NeighbourHouseC", (22.0, 13.0, 0), 14)):
        _r, glows = lib.load(model, g, at, yaw)
        face_lights(glows, 2.0)


def seams(lib):
    g = "Seams"
    x0 = 300.0
    for i in (0, 4):
        for j in (0, 4):
            lib.load("Lawn", g, (x0 + i, j, 0))
    for j in (-1, 1, 3):
        lib.load("PathStones", g, (x0 + 2, j + 1, 0))
    for i in (-1, 1, 3, 5):
        lib.load("Sidewalk", g, (x0 + i, -3, 0))
    return x0


def main():
    out, quality, rest = el.args()
    street = rest[0] if rest and os.path.isdir(rest[0]) else ex.OUT_DIR
    todo = [r for r in rest if not os.path.isdir(r)]
    final = quality == "final"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    lib = Library(street, ex.OUT_DIR)
    craft = el.EnvCraft(1.2)
    el.stage_ground(craft, 400.0, H(0x211D3C))
    cam = el.setup_render(samples=40 if final else 10, exposure=0.0)
    el.night_world(0.7, top=H(0x241B4A), horizon=H(0x4A3378))
    el.sun_light("Moon", (1.2, 2.2, -2.0), 2.0, (0.72, 0.76, 1.0), 6.0, 90.0)
    el.sun_light("Rim", (-2.2, -1.6, -1.2), 1.6, (0.6, 0.55, 1.0), 6.0, 90.0)
    el.sun_light("Fill", (2.4, 1.2, -0.4), 0.55, (1.0, 0.8, 0.7), 6.0, 90.0)
    el.sun_light("PKey", (1.6, 2.6, -1.9), 3.0, (1.0, 0.88, 0.76), 8.0, 40.0)
    el.sun_light("PRim", (-1.9, -2.4, -1.4), 2.2, (0.66, 0.66, 1.0), 8.0, 40.0)
    el.sun_light("PFill", (-2.4, 1.6, -0.5), 0.7, (0.8, 0.8, 1.0), 8.0, 40.0)
    el.sun_light("Flat", (0.25, 0.2, -1.0), 3.2, (1.0, 1.0, 1.0), 20.0, 40.0)
    mock(lib)
    sx0 = seams(lib)
    for i, (name, *_r) in enumerate(TILES):
        if "G_" + name not in bpy.data.objects:
            _root, glows = lib.load(name, "G_" + name, ((i + 2) * SPACING, 200.0, 0.0))
            face_lights(glows)
    k = 1.0 if final else 0.5
    tmp = os.path.splitext(out)[0]
    world = bpy.context.scene.world.node_tree.nodes["Background"]
    tiles = {}
    views = {
        "street": ((1.2, -15.5, 2.3), (0.6, 2.0, 3.9), 30, 1400, 800),
        "ground": ((-3.4, -10.6, 2.6), (0.2, -4.4, 0.0), 30, 1000, 800),
        "skyline": ((0.0, -30.0, 6.0), (3.0, 16.0, 5.0), 40, 1400, 560),
        "yard": ((-9.0, -11.5, 6.5), (0.5, -1.5, 0.6), 34, 1000, 560),
    }
    gr.set_lights({"Moon", "Rim", "Fill"})
    for name, (loc, target, lens, w, h) in views.items():
        if todo and name not in todo:
            continue
        el.solo(["Mock"])
        tiles[name] = el.render_view(cam, loc, target, lens, int(w * k), int(h * k), f"{tmp}_{name}.png")
        print("rendered", name, flush=True)
    gr.set_lights({"PKey", "PRim", "PFill"})
    world.inputs[1].default_value = 0.9
    px = int(400 * k)
    grid = []
    for i, (name, frame, look, yaw, elev) in enumerate(TILES):
        if todo and name not in todo and "grid" not in todo:
            continue
        el.solo(["G_" + name])
        root = bpy.data.objects["G_" + name].children[0]
        lens = 70
        dist = frame * lens / 36
        e, a = math.radians(elev), math.radians(yaw)
        target = Vector((root.location.x, 200.0, look))
        loc = target + Vector((-math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * dist
        grid.append(el.render_view(cam, loc, target, lens, px, px, f"{tmp}_{name}_{i}.png"))
        print("rendered", name, flush=True)
    seam = []
    if not todo or "seams" in todo:
        gr.set_lights({"Flat"})
        world.inputs[1].default_value = 1.2
        el.solo(["Seams"])
        q = int(800 * k)
        seam.append(el.render_view(cam, (sx0 + 2, 2, 30), (sx0 + 2, 2.001, 0), 50, q, q, f"{tmp}_seam_top.png", ortho=8.6))
        seam.append(el.render_view(cam, (sx0 + 2, -1.2, 30), (sx0 + 2, -1.199, 0), 50, q, q, f"{tmp}_seam_close.png", ortho=4.4))
        gr.set_lights({"Moon", "Rim", "Fill"})
        seam.append(el.render_view(cam, (sx0 - 2.5, -7.0, 1.7), (sx0 + 2.4, 1.5, 0.0), 28, q, q, f"{tmp}_seam_low.png"))
    rows = []
    if "street" in tiles and "ground" in tiles:
        rows.append(np.concatenate([tiles["street"], tiles["ground"]], axis=1))
        rows.append(np.concatenate([tiles["skyline"], tiles["yard"]], axis=1))
    else:
        rows.extend(tiles.values())
    cols = 6
    while grid and len(grid) % cols:
        blank = np.zeros_like(grid[0])
        blank[:, :, :3], blank[:, :, 3] = (0.02, 0.016, 0.05), 1
        grid.append(blank)
    rows.extend(np.concatenate(grid[r:r + cols], axis=1) for r in range(0, len(grid), cols))
    if seam:
        rows.append(np.concatenate(seam, axis=1))
    width = max(r.shape[1] for r in rows)
    rows = [el.pad(r, width, r.shape[0]) for r in rows]
    el.co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
    print("street review sheet ->", out)


if __name__ == "__main__":
    main()
