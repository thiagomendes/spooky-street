"""Review sheet of the split owl house: re-imports `OwlHouse.fbx` (children `OwlHouseBody`, `Porch`,
`SideWindowPane` > `SideWindow_Glow`, `FrontDoor`, `DoorRecessCard`, ...) and `OwlHouseInterior.fbx` and renders
them on the baked textures with back-face culling on, the way the game shows and hides the parts.

Rows: the street view before (when `before_dir` holds the previous export) and after / the west side before and
after, pane closed / looking out of the foyer with the body hidden and the porch kept / June's window from the
bridge with the pane hidden and a Pip-sized capsule (1.2 m tall, 0.28 m radius) / the inner and outer window
frames from outside, from the attic and in a plan cut 0.75 m above the sill.

Run: blender -b -t 2 --python tools/blender/env/env_split_review.py -- out.png [draft|final] [models_dir] [before=dir] [view ...]"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

import env_lib as el
import env_export as ex
import env_game_review as gr
import env_interior_review as ir
import owl_house as oh
from env_lib import H

PIP_H, PIP_R = 1.2, 0.28
DOOR_OPEN = 105.0


class Library(ir.Library):
    """Takes a model from `new` when it is there (a draft export of the house alone), else from the game folder."""

    def load(self, model, group, place, tag=None):
        new = self.new
        if not os.path.exists(os.path.join(new, model + ".fbx")):
            self.new = self.game
        try:
            return super().load(model, group, place, tag)
        finally:
            self.new = new


def capsule(name, at, group, rgb):
    """A Pip-sized stand-in, feet at `at`."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=PIP_R)
    for v in bm.verts:
        v.co.z += PIP_R if v.co.z < 0 else PIP_H - PIP_R
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = 0.9
    me.materials.append(mat)
    obj = bpy.data.objects.new(name, me)
    root = bpy.data.objects.get(group)
    if root is None:
        root = bpy.data.objects.new(group, None)
        el.pm.link(root)
    el.pm.link(obj, root)
    obj.location = at
    return obj


def main():
    out, quality, rest = el.args()
    opts = dict(a.split("=", 1) for a in rest if "=" in a)
    rest = [a for a in rest if "=" not in a]
    folder = rest[0] if rest and os.path.isdir(rest[0]) else ex.OUT_DIR
    todo = [r for r in rest if not os.path.isdir(r)]
    before = opts.get("before")
    final = quality == "final"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ir.NEW["OwlHouse"] = "OwlHouse"
    lib = Library(folder, ex.OUT_DIR)
    craft = el.EnvCraft(1.2)
    el.stage_ground(craft, 400.0, H(0x211D3C))
    cam = el.setup_render(samples=48 if final else 12, exposure=0.0)
    cam.data.clip_start = 0.05
    el.night_world(0.7, top=H(0x241B4A), horizon=H(0x4A3378))
    el.sun_light("Moon", (1.2, 2.2, -2.0), 2.0, (0.72, 0.76, 1.0), 6.0, 90.0)
    el.sun_light("Rim", (-2.2, -1.6, -1.2), 1.6, (0.6, 0.55, 1.0), 6.0, 90.0)
    el.sun_light("Fill", (2.4, 1.2, -0.4), 0.55, (1.0, 0.8, 0.7), 6.0, 90.0)
    hp = ir.house_pt

    def house_lights(glows, prefix):
        for tag, o in glows:
            c, _s = gr.centre(o)
            if tag == "AtticWindow":
                el.point_light(prefix + "Attic", c + Vector((0, -0.85, 0)), 35, (1.0, 0.78, 0.45), 0.3, shadow=False)
            elif tag == "SideWindow":
                el.point_light(prefix + "PaneSide", c + Vector((-0.6, 0, 0)), 10, (1.0, 0.7, 0.4), 0.2, shadow=False)
            elif tag == "Lamp":
                el.point_light("DressPorch", c + Vector((0.0, -0.1, -0.42)), 55, (1.0, 0.7, 0.38), 0.12, shadow=True)

    house_lights(lib.load("OwlHouse", "Ext", ir.HOUSE), "Ext")
    house_lights(lib.load("OwlHouseDressing", "Dress", ir.HOUSE), "Dress")
    if before:
        old = Library(before, ex.OUT_DIR)
        house_lights(old.load("OwlHouse", "Old", ir.HOUSE), "Old")
    ir.lamps(lib.load("OwlHouseInterior", "Int", ir.PLACEMENTS["OwlHouseInterior"]), "Int")
    lib.load("WorkshopMechanism", "Int", ir.PLACEMENTS["WorkshopMechanism"])
    el.point_light("IntFill", hp(-0.6, 3.0, 1.9), 14, (1.0, 0.8, 0.6), 0.5, shadow=False)
    el.point_light("IntAtticFill", hp(-1.2, 3.2, 3.6), 10, (1.0, 0.8, 0.6), 0.5, shadow=False)
    rg = []
    for name in ("ShedRamp", "SillBridge", "ClothSnag"):
        rg += lib.load(name, "Routes", ir.PLACEMENTS[name])
    ir.lamps(rg, "Route", 1.5)
    capsule("PipOnBridge", hp(-oh.HALF - 1.7, oh.DORMER_Y, oh.SILL_Z - 0.07), "Pip", (0.9, 0.42, 0.1))
    capsule("PipInWindow", hp(-oh.HALF + 0.05, oh.DORMER_Y, oh.SILL_Z + 0.03), "Pip", (0.95, 0.75, 0.3))

    ext, inner = lib.parts["OwlHouse"], lib.parts["OwlHouseInterior"]
    print("CHILDREN", {n: (o.parent.name.split(".")[0] if o.parent else None, [round(c, 4) for c in o.matrix_local.translation])
                       for n, o in ext.items()}, flush=True)
    doors = [(ext["FrontDoor"], ext["FrontDoor"].matrix_local.copy()), (inner["InnerDoor"], inner["InnerDoor"].matrix_local.copy()),
             (inner["DoorLatch"], inner["DoorLatch"].matrix_local.copy())]
    hinge = ext["FrontDoor"].matrix_local.translation.copy()
    pane = [ext["SideWindowPane"], ext["SideWindow_Glow"]]
    body = [ext[n] for n in ("OwlHouseBody", "OwlPuppet", "WindWheel", "AtticWindow_Glow", *("SideWindowPane", "SideWindow_Glow"))]
    card = [ext["DoorRecessCard"]]

    def pose(door_open, hidden, groups, lights):
        swing = Matrix.Translation(hinge) @ Matrix.Rotation(math.radians(DOOR_OPEN if door_open else 0.0), 4, "Z") @ Matrix.Translation(-hinge)
        for o, rest_m in doors:
            o.matrix_local = swing @ rest_m
        el.solo(groups)
        for o in hidden:
            o.hide_render = True
        for o in bpy.data.objects:
            if o.type == "LIGHT" and o.data.type == "POINT":
                o.hide_render = not any(o.name.startswith(p) for p in lights)
            if o.type == "LIGHT" and o.name.startswith("ExtPaneSide") and ext["SideWindow_Glow"] in hidden:
                o.hide_render = True
        bpy.context.view_layer.update()

    k = 1.0 if final else 0.5
    tmp = os.path.splitext(out)[0]
    world = bpy.context.scene.world.node_tree.nodes["Background"]
    suns = ("Moon", "Rim", "Fill")
    street = (hp(-5.5, -12.5, 2.3), hp(-0.2, 1.0, 3.9), 30, 1100, 900)
    side = (hp(-12.5, -1.5, 3.4), hp(-3.0, 2.6, 3.5), 40, 900, 900)
    wx = -oh.HALF
    views = {
        # name: (camera, target, lens, w, h, door open, hidden parts, groups, point-light prefixes, world, exposure, ortho)
        "street_before": (*street, False, [], ["Old", "Dress"], ("Old", "Dress"), 0.7, 0.35, None),
        "street": (*street, False, [], ["Ext", "Dress"], ("Ext", "Dress"), 0.7, 0.35, None),
        "side_before": (*side, False, [], ["Old", "Dress"], ("Old", "Dress"), 0.7, 0.35, None),
        "side": (*side, False, [], ["Ext", "Dress"], ("Ext", "Dress"), 0.7, 0.35, None),
        "foyer_out": (hp(0.25, 2.3, 1.75), hp(-0.05, -2.0, 1.45), 17, 1100, 900, True, body + card, ["Ext", "Dress", "Int"],
                      ("Int", "Dress"), 0.7, 0.45, None),
        "door_in": (hp(-0.9, -4.6, 1.9), hp(0.1, 1.0, 1.6), 30, 900, 900, True, card, ["Ext", "Dress", "Int"], ("Ext", "Int", "Dress"),
                    0.7, 0.35, None),
        "bridge": (hp(wx - 3.9, 0.9, 4.1), hp(wx - 0.3, 3.0, 3.1), 30, 1100, 900, False, pane + card, ["Ext", "Dress", "Int", "Routes", "Pip"],
                   ("Ext", "Int", "Route", "Dress"), 0.7, 0.4, None),
        "frames_out": (hp(wx - 2.3, 2.1, 3.0), hp(wx + 0.05, 3.0, 3.25), 50, 900, 900, False, pane + card, ["Ext", "Int", "Routes"],
                       ("Ext", "Int", "Route"), 0.7, 0.4, None),
        "frames_in": (hp(wx + 2.0, 3.5, 3.35), hp(wx, 3.0, 3.2), 22, 900, 900, False, pane + card, ["Ext", "Int", "Routes"],
                      ("Ext", "Int", "Route"), 0.9, 0.5, None),
        "frames_plan": (hp(wx + 0.05, 3.0, oh.SILL_Z + 0.75), hp(wx + 0.05, 3.001, 0.0), 50, 900, 900, False, pane + card,
                        ["Ext", "Int", "Routes"], ("Ext", "Int", "Route"), 1.6, 0.9, 2.2),
    }
    tiles = {}
    for name, (loc, target, lens, w, h, door_open, hidden, groups, lights, amb, expo, ortho) in views.items():
        if (todo and name not in todo) or (name.endswith("_before") and not before):
            continue
        pose(door_open, hidden, groups, lights)
        gr.set_lights(set(suns))
        world.inputs[1].default_value = amb
        bpy.context.scene.view_settings.exposure = expo
        tiles[name] = el.render_view(cam, loc, target, lens, int(w * k), int(h * k), f"{tmp}_{name}.png", ortho=ortho)
        print("rendered", name, flush=True)
    order = [["street_before", "street"], ["side_before", "side", "door_in"], ["foyer_out", "bridge"], ["frames_out", "frames_in", "frames_plan"]]
    rows = [np.concatenate([tiles[n] for n in row if n in tiles], axis=1) for row in order if any(n in tiles for n in row)]
    width = max(r.shape[1] for r in rows)
    rows = [el.pad(r, width, r.shape[0]) for r in rows]
    el.co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
    print("split review sheet ->", out)


if __name__ == "__main__":
    main()
