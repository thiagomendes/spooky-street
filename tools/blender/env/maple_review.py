"""Review sheet of the GAME maple: re-imports `Maple.fbx` (as written by `env_export_maple.py`) and renders it
with plain Principled materials on its baked textures, back-face culling on, like URP Lit.

Rows, top to bottom: the tree from four sides; the previous maple beside the new one (when `old=` is given);
the owner's close views (from the treehouse deck as the game has it now, from the shed ramp with the
`TreehouseCabin` when `routes=` is given, and the cabin's roof slot and door for clipping); the arrival-porch
key frame with both maples at their game placements.

Run: blender -b -t 2 --python tools/blender/env/maple_review.py -- out.png [draft|final] [maple=dir] [old=dir] [routes=dir]
  maple=   folder with the new Maple.fbx and Maple_* textures (default: the game folder)
  old=     folder with the previous Maple.fbx and its Env_* atlas
  routes=  folder with TreehouseCabin.fbx, ShedRamp.fbx and the Routes_* atlas (branch feature/s002-interior)
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np
from mathutils import Vector

import env_export as ex
import env_game_review as gr
import env_lib as el
import maple as mp
from env_lib import H

MAPLE_YAW, PLAZA_YAW = 200.0, 160.0  # Unity yaws (EnvironmentLayout); Blender turns the other way
PLAZA, YARD, HOUSE = (-4.5, 2.0), (-9.5, 12.0), (0.0, 13.0)  # Unity (x, z) = Blender (x, y)


class Library:
    def __init__(self):
        self.mats = {}

    def material(self, folder, atlas, glow=0.0):
        key = (folder, atlas, glow)
        if key not in self.mats:
            self.mats[key] = gr.atlas_material(folder, atlas, glow)
        return self.mats[key]

    def load(self, folder, model, atlas, group, at=(0, 0, 0), unity_yaw=0.0):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(folder, model + ".fbx"))
        new = [o for o in bpy.data.objects if o not in before]
        root = bpy.data.objects.get(group)
        if root is None:
            root = bpy.data.objects.new(group, None)
            el.pm.link(root)
        holder = bpy.data.objects.new(f"{model}_at", None)
        el.pm.link(holder, root)
        holder.location, holder.rotation_euler.z = at, math.radians(-unity_yaw)
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
                o.data.materials.append(self.material(folder, atlas, gr.GLOW.get(tag, 2.0)))
                glows.append((tag, o))
            else:
                o.data.materials.append(self.material(folder, atlas))
        print(f"IMPORTED {model} from {folder}: {tris} tris", flush=True)
        bpy.context.view_layer.update()
        return root, glows


def orbit(target, frame, yaw, lens=70, elev=13):
    dist, e, a = frame * lens / 36, math.radians(elev), math.radians(yaw)
    return Vector(target) + Vector((-math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * dist


def main():
    rest = sys.argv[sys.argv.index("--") + 1:]
    out, quality = rest[0], (rest[1] if len(rest) > 1 and "=" not in rest[1] else "draft")
    opts = dict(a.split("=", 1) for a in rest if "=" in a)
    game = ex.OUT_DIR
    new, old, routes = opts.get("maple", game), opts.get("old"), opts.get("routes")
    final = quality == "final"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    lib = Library()
    craft = el.EnvCraft(1.2)
    el.stage_ground(craft, 600.0)
    cam = el.setup_render(samples=40 if final else 10, exposure=0.0)
    el.night_world(0.9, top=H(0x241B4A), horizon=H(0x3A2A66))
    el.sun_light("PKey", (1.6, 2.6, -1.9), 3.0, (1.0, 0.88, 0.76), 8.0, 40.0)
    el.sun_light("PRim", (-1.9, -2.4, -1.4), 2.2, (0.66, 0.66, 1.0), 8.0, 40.0)
    el.sun_light("PFill", (-2.4, 1.6, -0.5), 0.7, (0.8, 0.8, 1.0), 8.0, 40.0)
    el.sun_light("Moon", (1.2, 2.2, -2.0), 2.0, (0.72, 0.76, 1.0), 6.0, 90.0)
    el.sun_light("Rim", (-2.2, -1.6, -1.2), 1.6, (0.6, 0.55, 1.0), 6.0, 90.0)
    el.sun_light("Fill", (2.4, 1.2, -0.4), 0.55, (1.0, 0.8, 0.7), 6.0, 90.0)
    world = bpy.context.scene.world.node_tree.nodes["Background"]
    k = 1.0 if final else 0.5
    tmp = os.path.splitext(out)[0]

    def shot(name, loc, target, lens, w, h):
        tile = el.render_view(cam, loc, target, lens, int(w * k), int(h * k), f"{tmp}_{name}.png")
        print("rendered", name, flush=True)
        return tile

    # --- the tree alone, and beside the previous one -------------------------------------------------------
    lib.load(new, "Maple", "Maple", "New")
    xo = 60.0
    if old:
        lib.load(old, "Maple", "Env", "Old", (xo, 0, 0))
    gr.set_lights({"PKey", "PRim", "PFill"})
    bpy.context.scene.view_settings.exposure = -0.1
    el.solo(["New"])
    look = (0.3, 0.0, 3.9)
    rows = [np.concatenate([shot(f"new_{yaw}", orbit(look, 10.5, yaw), look, 70, 500, 500) for yaw in (22, 112, 202, 292)], axis=1)]
    if old:
        pair = []
        for yaw in (22, 202):
            el.solo(["Old"])
            at = (look[0] + xo, look[1], look[2])
            pair.append(shot(f"old_{yaw}", orbit(at, 10.5, yaw), at, 70, 500, 500))
            el.solo(["New"])
            pair.append(shot(f"cmp_{yaw}", orbit(look, 10.5, yaw), look, 70, 500, 500))
        rows.append(np.concatenate(pair, axis=1))

    # --- the owner's views: treehouse deck (the game today), shed ramp and cabin (S002 assets) --------------
    gr.set_lights({"Moon", "Rim", "Fill"})
    world.inputs[1].default_value = 0.8
    bpy.context.scene.view_settings.exposure = 0.5
    yo = 150.0
    lib.load(new, "Maple", "Maple", "Deck", (0, yo, 0))
    lib.load(game, "TreehousePlatform", "Env", "Deck", (0, yo, 0))
    el.point_light("DeckLantern", (mp.PLAT.x - 0.9, yo + mp.PLAT.y - 1.0, mp.ZT + 1.2), 40, (1.0, 0.74, 0.42), 0.1)
    el.solo(["Deck"])
    close = [shot("deck", (mp.PLAT.x - 1.0, yo + mp.PLAT.y + 1.05, mp.ZT + 1.35), (1.1, yo - 0.7, 5.7), 20, 700, 600)]
    if routes:
        zo = 300.0
        for model, atlas, folder in (("Maple", "Maple", new), ("TreehousePlatform", "Env", game), ("TreehouseCabin", "Routes", routes)):
            _r, glows = lib.load(folder, model, atlas, "Ramp", (0, zo, 0), MAPLE_YAW)
            gr.prop_lights(glows)
        lib.load(routes, "ShedRamp", "Routes", "Ramp", (0, zo, 0))
        el.solo(["Ramp"])
        close.append(shot("ramp", (-2.3, zo + 3.7, 3.1), (0.2, zo - 0.2, 4.9), 20, 700, 600))
        close.append(shot("slot", (-4.2, zo - 5.0, 6.6), (-0.1, zo + 0.1, 4.2), 40, 600, 600))
    rows.append(np.concatenate(close, axis=1))

    # --- key frame from the arrival porch: both maples where the game puts them ----------------------------
    ko = 450.0
    lib.load(new, "Maple", "Maple", "Key", (PLAZA[0], ko + PLAZA[1], 0), PLAZA_YAW)
    lib.load(new, "Maple", "Maple", "Key", (YARD[0], ko + YARD[1], 0), MAPLE_YAW)
    lib.load(game, "TreehousePlatform", "Env", "Key", (YARD[0], ko + YARD[1], 0), MAPLE_YAW)
    _r, glows = lib.load(game, "OwlHouse", "OwlHouse", "Key", (HOUSE[0], ko + HOUSE[1], 0))
    _r, dglows = lib.load(game, "OwlHouseDressing", "Env", "Key", (HOUSE[0], ko + HOUSE[1], 0))
    gr.house_lights(glows + dglows)
    for o in [o for o in bpy.data.objects if o.name.startswith("JackGlow")]:
        bpy.data.objects.remove(o, do_unlink=True)
    for model, x, z, yaw in (("Hedge", -1.97, 5.0, 0), ("FenceGate", -0.1, 5.0, 0), ("FenceSection", 1.7, 5.0, 0), ("FenceSection", 3.8, 5.0, 0),
                             ("Hedge", -4.17, 7.0, 0), ("Hedge", -6.5, 7.0, 180), ("Hedge", -8.83, 7.0, 0), ("StreetLamp", 2.2, -9.6, 0),
                             ("Mailbox", -1.45, -10.6, 12), ("JackOLantern", -1.5, -8.2, 20), ("Bench", 8.0, 0.0, -60)):
        _r, glows = lib.load(game, model, "Env", "Key", (x, ko + z, 0), yaw)
        gr.prop_lights(glows)
    el.solo(["Key"])
    world.inputs[1].default_value = 0.6
    bpy.context.scene.view_settings.exposure = 0.3
    rows.append(shot("key", (0.5, ko - 13.4, 1.9), (-1.6, ko + 4.0, 4.3), 26, 2000, 1000))

    width = max(r.shape[1] for r in rows)
    rows = [el.pad(r, width, r.shape[0]) for r in rows]
    el.co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
    print("maple review sheet ->", out)


if __name__ == "__main__":
    main()
