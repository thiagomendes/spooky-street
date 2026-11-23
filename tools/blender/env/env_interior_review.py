"""Review sheet of the GAME interior and route assets: re-imports the FBX files written by
`env_export_interior.py` (plus the approved owl house, maple, treehouse platform, crate and awning from the game
folder) and renders them on the baked textures with back-face culling on (like URP Lit) and emission on
`*_Glow` objects.

The scene is laid out in game coordinates (Blender X = Unity X, Blender Y = Unity Z): `PLACEMENTS` is the
suggested layout, as (x, y up, z, yaw) in Unity terms.

Rows: attic / stair and foyer / foyer toward the door; the mechanism slipped and seated (panel open) / through the
grille; the cutaway (exterior hidden, near walls culled) / the routes with the owl house and the maple / bridge
and cabin close-ups.

Its interior views were framed for the first interior; the closed rooms of D24 are reviewed by
`env_house_v3_review.py`, which reuses this module's loader and lamps.

Run: blender -b -t 2 --python tools/blender/env/env_interior_review.py -- out.png [draft|final] [models_dir] [view ...]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np
from mathutils import Matrix, Vector

import env_lib as el
import env_export as ex
import env_game_review as gr
import interior as it
import routes as rt
from env_lib import H

NEW = {"OwlHouseInterior": "OwlHouseInterior", "WorkshopMechanism": "WorkshopMechanism", "ShedRamp": "Routes", "SillBridge": "Routes",
       "TreehouseCabin": "Routes", "ClothSnag": "Routes"}
GLOW = dict(gr.GLOW, OwlLantern=3.0, AtticWindowIn=1.6, AtticStar=2.0, ShedWindow=1.3)
HOUSE = (0.0, 0.0, 13.0, 0.0)
MAPLE = (-9.5, 0.0, 12.0, 200.0)
# model -> (x, y, z, yaw) in Unity terms: the suggested placements
PLACEMENTS = {
    "OwlHouseInterior": HOUSE,
    "WorkshopMechanism": (it.NOOK_X, it.NOOK_Z, 13.0 + it.MECH_Y - 2.0, 90.0),
    "ShedRamp": (-9.5, 0.0, 12.0, 0.0),
    "SillBridge": (-9.5 + rt.ROOF_E + rt.BRIDGE_L / 2, 0.0, 12.0 + rt.BRIDGE_Y, 0.0),
    "TreehouseCabin": MAPLE,
    "ClothSnag": (-9.5 + rt.ROOF_E - 0.07 - 0.05, rt.ZT + 0.98, 12.0 + rt.BRIDGE_Y + rt.BRIDGE_W / 2 + 0.1, 90.0),
    "Awning": (-9.5 + (rt.EDGE_S.x + (rt.ROOF_S - rt.EDGE_S.y) * 0.363 + rt.ROOF_E) / 2 + 0.02, 0.0, 12.0 + rt.ROOF_S - 0.13, 0.0),
    "Crate": (-9.5 + 2.4, 0.0, 12.0 + rt.ROOF_S - 0.13 - 1.95, 12.0),
}


class Library:
    def __init__(self, new, game):
        self.new, self.game, self.mats, self.parts = new, game, {}, {}

    def material(self, folder, atlas, glow=0.0):
        key = (atlas, glow)
        if key not in self.mats:
            self.mats[key] = gr.atlas_material(folder, atlas, glow)
        return self.mats[key]

    def load(self, model, group, place, tag=None):
        """Import under the root empty `group`, at a Unity-style placement. Returns the glow objects."""
        atlas = NEW.get(model) or ("OwlHouse" if model == "OwlHouse" else "Env")
        folder = self.new if model in NEW else self.game
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(folder, model + ".fbx"))
        new = [o for o in bpy.data.objects if o not in before]
        root = bpy.data.objects.get(group)
        if root is None:
            root = bpy.data.objects.new(group, None)
            el.pm.link(root)
        holder = bpy.data.objects.new(f"{model}_at", None)
        el.pm.link(holder, root)
        x, y, z, yaw = place
        holder.location, holder.rotation_euler.z = (x, z, y), -math.radians(yaw)
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
                g = name[:-len(ex.GLOW_SUFFIX)]
                o.data.materials.append(self.material(folder, atlas, GLOW.get(g, 2.0)))
                glows.append((g, o))
            else:
                o.data.materials.append(self.material(folder, atlas))
        print(f"IMPORTED {model}: {tris} tris, objects {sorted(o.name for o in new)}", flush=True)
        self.parts[tag or model] = {o.name.split(".")[0]: o for o in new}
        bpy.context.view_layer.update()
        return glows


def turn(lib, model, part, axis, deg):
    o = lib.parts[model][part]
    o.matrix_local = o.matrix_local @ Matrix.Rotation(math.radians(deg), 4, axis)


def lamps(glows, prefix, k=1.0):
    """Practical lights at the glow parts, as the game would place them."""
    power = {"OwlLantern": (90, 0.9), "Lamp": (22, 0.5), "Face": (5, 0.3), "AtticStar": (30, 0.6), "ShedWindow": (6, 0.5), "AtticWindowIn": (0, 0)}
    for tag, o in glows:
        me = o.data
        pts = [o.matrix_world @ v.co for v in me.vertices]
        islands = [pts]
        if tag in ("Face", "Lamp"):  # several separate lamps share one glow object: one light per cluster
            islands = []
            for p in pts:
                for isl in islands:
                    if (isl[0] - p).length < 0.7:
                        isl.append(p)
                        break
                else:
                    islands.append([p])
        w, off = power.get(tag, (10, 0.4))
        for isl in islands:
            c = sum(isl, Vector()) / len(isl)
            if w:
                el.point_light(prefix + tag, c + Vector((0, 0, 0.02)), w * k, (1.0, 0.72, 0.42), 0.12, shadow=tag in ("OwlLantern", "AtticStar"))


HIDDEN = set()


def show(groups, lights):
    el.solo(groups)
    for o in HIDDEN:
        o.hide_render = True
    for o in bpy.data.objects:
        if o.type == "LIGHT" and o.data.type == "POINT":
            o.hide_render = not any(o.name.startswith(p) for p in lights)


def house_pt(x, y, z):
    """Owl-house builder coordinates -> review scene."""
    return Vector((x, y - 2.0 + 13.0, z))


def main():
    out, quality, rest = el.args()
    folder = rest[0] if rest and os.path.isdir(rest[0]) else ex.OUT_DIR
    todo = [r for r in rest if not os.path.isdir(r)]
    final = quality == "final"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    lib = Library(folder, ex.OUT_DIR)
    craft = el.EnvCraft(1.2)
    el.stage_ground(craft, 400.0, H(0x211D3C))
    cam = el.setup_render(samples=48 if final else 12, exposure=0.0)
    cam.data.clip_start = 0.05
    el.night_world(0.7, top=H(0x241B4A), horizon=H(0x4A3378))
    el.sun_light("Moon", (1.2, 2.2, -2.0), 2.0, (0.72, 0.76, 1.0), 6.0, 90.0)
    el.sun_light("Rim", (-2.2, -1.6, -1.2), 1.6, (0.6, 0.55, 1.0), 6.0, 90.0)
    el.sun_light("Fill", (2.4, 1.2, -0.4), 0.55, (1.0, 0.8, 0.7), 6.0, 90.0)
    el.sun_light("PKey", (1.6, 2.6, -1.9), 3.0, (1.0, 0.88, 0.76), 8.0, 30.0)
    el.sun_light("PRim", (-1.9, -2.4, -1.4), 2.2, (0.66, 0.66, 1.0), 8.0, 30.0)
    el.sun_light("PFill", (-2.4, 1.6, -0.5), 0.7, (0.8, 0.8, 1.0), 8.0, 30.0)
    # exterior, for scale and for the split
    eg = lib.load("OwlHouse", "Ext", HOUSE) + lib.load("OwlHouseDressing", "Ext", HOUSE)
    for tag, o in eg:
        c, _s = gr.centre(o)
        if tag == "AtticWindow":
            el.point_light("ExtAttic", c + Vector((0, -0.85, 0)), 35, (1.0, 0.78, 0.45), 0.3, shadow=False)
        elif tag == "Lamp":
            el.point_light("ExtPorch", c + Vector((0.0, -0.1, -0.42)), 55, (1.0, 0.7, 0.38), 0.12, shadow=True)
    lamps(lib.load("OwlHouseInterior", "Int", PLACEMENTS["OwlHouseInterior"]), "Int")
    lib.load("WorkshopMechanism", "Int", PLACEMENTS["WorkshopMechanism"])
    HIDDEN.add(lib.parts["WorkshopMechanism"]["LinkArm_Seated"])  # the case starts with the arm slipped
    el.point_light("IntNook", house_pt(2.2, 3.1, 1.9), 9, (0.75, 0.8, 1.0), 0.2, shadow=False)
    el.point_light("IntFill", house_pt(-0.6, 3.0, 1.9), 14, (1.0, 0.8, 0.6), 0.5, shadow=False)
    el.point_light("IntAtticFill", house_pt(-1.2, 3.2, 3.6), 10, (1.0, 0.8, 0.6), 0.5, shadow=False)
    for name in ("Maple", "TreehousePlatform"):
        lib.load(name, "Routes", MAPLE)
    rg = []
    for name in ("ShedRamp", "SillBridge", "TreehouseCabin", "ClothSnag", "Awning", "Crate"):
        rg += lib.load(name, "Routes", PLACEMENTS[name])
    lamps(rg, "Route", 1.5)
    # the mechanism alone, slipped and seated, both with the service panel open
    mx = 300.0
    for i, state in enumerate(("Slipped", "Seated")):
        tag = "Mech" + state
        lib.load("WorkshopMechanism", tag, (mx + 6.0 * i, 0.0, 0.0, 0.0), tag)
        turn(lib, tag, "ServicePanel", "Z", 112)
        HIDDEN.add(lib.parts[tag]["LinkArm_" + ("Seated" if state == "Slipped" else "Slipped")])
        if state == "Seated":  # a lobe under the roller: follower up, the arm riding on its cradle
            lift = 6.6
            turn(lib, tag, "Cam", "Z", 76)
            turn(lib, tag, "Follower", "Y", -lift)
            arm = lib.parts[tag]["LinkArm_Seated"]  # rides on the cradle: moves with that point of the follower
            seat = Vector((it.TIP_X - it.HINGE.x, 0, it.CRADLE_Z - it.HINGE.z))
            arm.matrix_local = Matrix.Translation(Matrix.Rotation(math.radians(-lift), 4, "Y") @ seat - seat) @ arm.matrix_local
            turn(lib, tag, "Crank", "Y", 140)
    k = 1.0 if final else 0.5
    tmp = os.path.splitext(out)[0]
    world = bpy.context.scene.world.node_tree.nodes["Background"]
    hp = house_pt
    inside = (["Int"], ("Int",))
    views = {
        # name: (camera, target, lens, w, h, groups, point-light prefixes, suns, world, exposure)
        "attic": (hp(1.9, 5.3, 3.75), hp(-1.3, 1.6, 3.35), 15, 1100, 760, *inside, (), 0.5, 0.35),
        "stair": (hp(-0.9, 0.55, 1.75), hp(0.5, 5.0, 1.75), 14, 1100, 760, *inside, (), 0.5, 0.35),
        "door": (hp(-1.7, 2.3, 4.0), hp(0.15, 0.1, 2.75), 15, 800, 760, *inside, (), 0.5, 0.35),
        "entry": (hp(1.0, 2.9, 1.55), hp(-0.15, 0.1, 1.6), 20, 700, 620, *inside, (), 0.5, 0.35),
        "grille": (hp(-0.1, 2.95, 1.35), hp(1.6, 3.1, 1.25), 24, 800, 620, *inside, (), 0.5, 0.45),
        "slipped": (Vector((mx - 1.0, -2.3, 2.35)), Vector((mx + 0.02, 0.55, 1.0)), 36, 720, 620, ["MechSlipped"], (), ("PKey", "PRim", "PFill"), 0.9, -0.1),
        "seated": (Vector((mx + 6 - 1.0, -2.3, 2.35)), Vector((mx + 6.02, 0.55, 1.0)), 36, 720, 620, ["MechSeated"], (), ("PKey", "PRim", "PFill"), 0.9, -0.1),
        "cutaway": (Vector((-5.4, 3.6, 7.2)), hp(0.1, 2.9, 2.3), 30, 1300, 900, ["Int", "Routes"], ("Int", "Route"), ("Moon", "Rim", "Fill"), 0.9, 0.55),
        "routes": (Vector((-19.5, 0.5, 9.5)), Vector((-6.3, 13.0, 1.9)), 30, 1700, 900, ["Ext", "Routes"], ("Ext", "Route"), ("Moon", "Rim", "Fill"), 0.7, 0.35),
        "bridge": (Vector((-6.9, 8.4, 3.3)), Vector((-5.0, 14.0, 2.5)), 26, 850, 700, ["Ext", "Routes", "Int"], ("Ext", "Route", "Int"), ("Moon", "Rim", "Fill"), 0.7, 0.35),
        "cabin": (Vector((-4.8, 9.2, 4.4)), Vector((-8.6, 12.4, 2.9)), 30, 850, 700, ["Ext", "Routes"], ("Ext", "Route"), ("Moon", "Rim", "Fill"), 0.7, 0.35),
        "plan": (Vector((-6.5, 12.0, 60.0)), Vector((-6.5, 12.001, 0.0)), 50, 850, 700, ["Ext", "Routes"], ("Ext", "Route"), ("Moon", "Rim", "Fill"), 1.2, 0.6),
    }
    tiles = {}
    for name, (loc, target, lens, w, h, groups, lights, suns, amb, expo) in views.items():
        if todo and name not in todo:
            continue
        show(groups, lights)
        gr.set_lights(set(suns))
        world.inputs[1].default_value = amb
        bpy.context.scene.view_settings.exposure = expo
        tiles[name] = el.render_view(cam, loc, target, lens, int(w * k), int(h * k), f"{tmp}_{name}.png", ortho=17.0 if name == "plan" else None)
        print("rendered", name, flush=True)
    order = [["attic", "stair", "door"], ["slipped", "seated", "grille", "entry"], ["cutaway", "routes"], ["bridge", "cabin", "plan"]]
    rows = [np.concatenate([tiles[n] for n in row if n in tiles], axis=1) for row in order if any(n in tiles for n in row)]
    width = max(r.shape[1] for r in rows)
    rows = [el.pad(r, width, r.shape[0]) for r in rows]
    el.co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
    print("interior review sheet ->", out)


if __name__ == "__main__":
    main()
