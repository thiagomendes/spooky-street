"""Review frames of the closed, camera-sized spaces (decisions D24 / D24.1), rendered from the re-imported FBX
files on their baked textures, with back-face culling on (like URP Lit) and emission on `*_Glow` objects.

  interior  `OwlHouseInterior` + `WorkshopMechanism` in the interior's own frame: frames from where a 2.4 m boom
            camera behind a 1.1 m Pip sits in the hall, on the stair and in the attic, the two thresholds, the
            nook, and cut plans / sections (an orthographic camera whose near clip slices the model).
  exterior  `OwlHouse` + `OwlHouseDressing`: the three-quarter view with the lit window cards, the open door and
            June's open window (nothing behind them but their closed view boxes).
  cabin     `TreehouseCabin` on the `Maple` and `TreehousePlatform`: outside and from inside.

Run: blender -b -t 2 --python tools/blender/env/env_house_v3_review.py -- out_dir [draft|final] [models_dir] [view ...]
A view name may be a group name (`interior`, `exterior`, `cabin`)."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
from mathutils import Matrix, Vector

import env_lib as el
import env_export as ex
import env_game_review as gr
import env_interior_review as ir
import interior as it
import routes as rt
from env_lib import H

# as Core/World/GlowLevels.cs
ir.GLOW.update(AtticWindow=4.0, SideWindow=1.6, Lamp=1.3, Face=1.8, OwlLantern=1.5, AtticWindowIn=4.0, AtticStar=1.5, StreetView=1.5, RoomCard=1.5)
FAR = 200.0  # where the interior stands in the review scene, away from the street
BOOM, PIP = 2.4, 1.1
LENS = 17.5  # 60 degrees vertical on a 2.17:1 phone frame


def boom(pip, facing_deg, dist=BOOM, pitch=18.0):
    """Camera and target of the gameplay camera behind Pip: `pip` are her feet, she faces `facing_deg` (0 = +Y)."""
    a, p = math.radians(facing_deg), math.radians(pitch)
    fwd = Vector((math.sin(a), math.cos(a), 0.0))
    pivot = Vector(pip) + Vector((0, 0, PIP * 0.95))
    return pivot - fwd * dist * math.cos(p) + Vector((0, 0, dist * math.sin(p))), pivot + fwd * 0.6


def standee(name, pos, facing_deg, group):
    """A Pip-sized figure for scale: capsule body and head."""
    root = bpy.data.objects.get(group)
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (0.16, 0.1, 0.3, 1)
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.16, 0.1, 0.3, 1)
    for r, z, sz in ((0.2, 0.42, 2.1), (0.19, PIP - 0.19, 1.0)):
        me = bpy.data.meshes.new(name)
        import bmesh
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=8, radius=r)
        for v in bm.verts:
            v.co.z *= sz
        bm.to_mesh(me)
        bm.free()
        me.materials.append(mat)
        o = bpy.data.objects.new(name, me)
        o.location = Vector(pos) + Vector((0, 0, z))
        el.pm.link(o, root)
    return root


def main():
    out, quality, rest = el.args()
    out_dir = out if not out.lower().endswith(".png") else os.path.dirname(out)
    os.makedirs(out_dir, exist_ok=True)
    folder = next((r for r in rest if os.path.isdir(r)), ex.OUT_DIR)
    todo = [r for r in rest if not os.path.isdir(r)]
    final = quality == "final"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if os.path.exists(os.path.join(folder, "OwlHouse.fbx")):
        ir.NEW["OwlHouse"] = "OwlHouse"
    if os.path.exists(os.path.join(folder, "Maple.fbx")):
        ir.NEW["Maple"] = "Maple"
    lib = ir.Library(folder, ex.OUT_DIR)
    craft = el.EnvCraft(1.2)
    el.stage_ground(craft, 400.0, H(0x211D3C))
    cam = el.setup_render(samples=48 if final else 12, exposure=0.0)
    cam.data.clip_start = 0.05
    el.night_world(0.7, top=H(0x241B4A), horizon=H(0x4A3378))
    el.sun_light("Moon", (1.2, 2.2, -2.0), 2.0, (0.72, 0.76, 1.0), 6.0, 90.0)
    el.sun_light("Rim", (-2.2, -1.6, -1.2), 1.6, (0.6, 0.55, 1.0), 6.0, 90.0)
    el.sun_light("Fill", (2.4, 1.2, -0.4), 0.55, (1.0, 0.8, 0.7), 6.0, 90.0)

    def hp(x, y, z):  # interior frame -> review scene
        return Vector((x + FAR, y, z))

    need = {t[:3].lower() for t in todo} or {"int", "ext", "cab"}

    def want(group):
        return group[:3].lower() in need

    if want("Int"):
        place = (FAR, 0.0, 2.0, 0.0)
        ir.lamps(lib.load("OwlHouseInterior", "Int", place), "Int", 0.9)
        lib.load("WorkshopMechanism", "Int", (FAR + it.NOOK_X, it.NOOK_Z, it.MECH_Y, 90.0))
        ir.HIDDEN.add(lib.parts["WorkshopMechanism"]["LinkArm_Seated"])
        for name, p, w in (("IntHall", (0.0, 2.6, 3.2), 26), ("IntHallN", (0.6, 5.0, 2.9), 12), ("IntBay", (-2.9, 4.6, 5.2), 14), ("IntNook", (2.95, 4.6, 2.6), 7),
                           ("IntAtticA", (0.0, 2.2, 6.6), 26), ("IntAtticB", (0.3, 5.2, 6.6), 20), ("IntDormer", (-2.6, 1.5, 5.6), 5)):
            el.point_light(name, hp(*p), w, (1.0, 0.82, 0.62), 0.6, shadow=False)
        standee("PipHall", hp(0.2, 3.3, it.FL), 0, "PipA")
        standee("PipAttic", hp(0.4, 3.3, it.LOFT), 0, "PipB")
    if want("Ext"):
        house = (0.0, 0.0, 13.0, 0.0)
        eg = lib.load("OwlHouse", "Ext", house) + lib.load("OwlHouseDressing", "Ext", house)
        for tag, o in eg:
            c, _s = gr.centre(o)
            if tag == "AtticWindow":
                el.point_light("ExtAttic", c + Vector((0, -0.85, 0)), 35, (1.0, 0.78, 0.45), 0.3, shadow=False)
            elif tag == "Lamp":
                el.point_light("ExtPorch", c + Vector((0.0, -0.1, -0.42)), 55, (1.0, 0.7, 0.38), 0.12, shadow=True)
    if want("Cab"):
        for name in ("Maple", "TreehousePlatform"):
            lib.load(name, "Cab", ir.MAPLE)
        ir.lamps(lib.load("TreehouseCabin", "Cab", ir.MAPLE), "Cab", 1.5)
        ir.lamps(lib.load("ShedRamp", "Cab", ir.PLACEMENTS["ShedRamp"]), "Cab", 1.5)
    k = 1.0 if final else 0.5
    world = bpy.context.scene.world.node_tree.nodes["Background"]
    W, Hh = 1280, 590
    inside = (["Int"], ("Int",), ())
    FLr, LO = it.FL, it.LOFT
    june = math.degrees(math.atan2(it.STOOL[0] - 0.5, it.STOOL[1] - 3.6))
    views = {  # name: (camera, target, lens, w, h, groups, point lights, suns, world, exposure, ortho, clip_start, hide parts)
        "int_hall_from_door": (*boom(hp(0.0, 3.0, FLr), 0), LENS, W, Hh, ["Int", "PipA"], ("Int",), (), 0.4, 0.3),
        "int_hall_to_door": (*boom(hp(0.2, 3.3, FLr), 180), LENS, W, Hh, ["Int", "PipA"], ("Int",), (), 0.4, 0.3),
        "int_hall_to_nook": (*boom(hp(0.2, 3.3, FLr), 80), LENS, W, Hh, ["Int", "PipA"], ("Int",), (), 0.4, 0.3),
        "int_hall_to_stair": (*boom(hp(-0.5, 3.0, FLr), -75, 2.0), LENS, W, Hh, ["Int", "PipA"], ("Int",), (), 0.4, 0.3),
        "int_stair_lower": (*boom(hp(-2.85, 4.2, FLr + 4 * it.RISE), 0), LENS, W, Hh, *inside, 0.4, 0.3),
        "int_stair_upper": (*boom(hp(-0.9, 6.3, it.LAND_Z + 5 * it.RISE), 90, 2.0), LENS, W, Hh, *inside, 0.4, 0.3),
        "int_attic_june": (*boom(hp(0.4, 3.3, LO), june + 24), LENS, W, Hh, ["Int", "PipB"], ("Int",), (), 0.4, 0.3),
        "int_attic_to_front": (*boom(hp(0.4, 3.3, LO), 180), LENS, W, Hh, ["Int", "PipB"], ("Int",), (), 0.4, 0.3),
        "int_attic_to_back": (*boom(hp(0.4, 3.3, LO), 10), LENS, W, Hh, ["Int", "PipB"], ("Int",), (), 0.4, 0.3),
        "int_attic_dormer": (*boom(hp(-1.4, 1.5, LO), -90), LENS, W, Hh, *inside, 0.4, 0.3),
        "int_door_open": (hp(0.5, 3.4, FLr + 1.7), hp(-0.1, 0.0, FLr + 1.2), 22, W, Hh, *inside, 0.4, 0.3),
        "int_nook_cutaway": (hp(0.3, 4.9, FLr + 1.5), hp(2.9, 4.5, FLr + 0.9), 16, W, Hh, *inside, 0.4, 0.45, None, None, ("NookWall",)),
        "int_plan_hall": (hp(0, 3.5, 30), hp(0, 3.501, 0), 50, 900, 900, *inside, 0.6, 0.5, 9.0, 30 - (it.UNDER - 0.25)),
        "int_plan_attic": (hp(0, 3.5, 30), hp(0, 3.501, 0), 50, 900, 900, *inside, 0.6, 0.5, 9.0, 30 - (LO + 2.3)),
        "int_section_south": (hp(0, -30, 4.4), hp(0, 0, 4.4), 50, 1000, 1000, *inside, 0.6, 0.5, 9.6, 30 + 3.0),
        "int_section_west": (hp(-30, 3.5, 4.4), hp(0, 3.5, 4.4), 50, 1000, 1000, *inside, 0.6, 0.5, 9.6, 30 - 2.9),
        "int_outside_shell": (hp(-9, -9, 9), hp(0, 3.5, 3.5), 30, 1000, 800, *inside, 1.0, 0.6),
        "ext_three_quarter": (Vector((-10.5, -4.5, 4.6)), Vector((-0.1, 12.2, 4.3)), 44, 1150, 900, ["Ext"], ("Ext",), ("Moon", "Rim", "Fill"), 0.7, 0.0),
        "ext_front": (Vector((0.0, -1.0, 2.6)), Vector((0.0, 11.0, 3.2)), 30, 1150, 900, ["Ext"], ("Ext",), ("Moon", "Rim", "Fill"), 0.7, 0.0),
        "ext_door_open": (Vector((-0.9, 6.6, 1.75)), Vector((-0.05, 11.0, 1.62)), 40, 800, 760, ["Ext"], ("Ext",), ("Moon", "Rim", "Fill"), 0.7, 0.0),
        "ext_dormer_open": (Vector((-7.0, 14.6, 3.6)), Vector((-3.0, 14.0, 3.2)), 40, 800, 760, ["Ext"], ("Ext",), ("Moon", "Rim", "Fill"), 0.7, 0.0),
        "cab_outside": (Vector((-4.6, 9.0, 4.6)), Vector((-9.3, 12.2, 3.6)), 30, 1000, 800, ["Cab"], ("Cab",), ("Moon", "Rim", "Fill"), 0.7, 0.35),
        "cab_west": (Vector((-14.5, 14.5, 5.0)), Vector((-9.5, 12.0, 3.6)), 30, 1000, 800, ["Cab"], ("Cab",), ("Moon", "Rim", "Fill"), 0.7, 0.35),
    }
    views.update(CABIN_VIEWS(boom))
    for name, spec in views.items():
        if todo and name not in todo and name[:3] not in {t[:3] for t in todo if t in ("interior", "exterior", "cabin")}:
            continue
        loc, target, lens, w, h, grp, lights, suns, amb, expo, *more = spec
        ortho, clip, hide = (list(more) + [None, None, ()])[:3]
        if not all(bpy.data.objects.get(g) for g in grp if not g.startswith("Pip")):
            continue
        hidden = []
        for o in bpy.data.objects:
            if o.name.split(".")[0] in (hide or ()):
                hidden.append(o)
        if name == "int_door_open":
            hinge = Matrix.Translation(lib.parts["OwlHouseInterior"]["InnerDoor"].matrix_local.translation)
            for part in ("InnerDoor", "DoorLatch"):
                o = lib.parts["OwlHouseInterior"][part]
                o.matrix_local = hinge @ Matrix.Rotation(math.radians(100), 4, "Z") @ hinge.inverted() @ o.matrix_local
        if name in ("ext_door_open", "ext_dormer_open"):
            parts = lib.parts["OwlHouse"]
            if "FrontDoor" in parts and name == "ext_door_open":
                ir.turn(lib, "OwlHouse", "FrontDoor", "Z", 100)
            hidden += [parts[p] for p in ("SideWindowPane", "SideWindow_Glow") if p in parts and name == "ext_dormer_open"]
        ir.show(grp, lights)
        for o in hidden:
            o.hide_render = True
        gr.set_lights(set(suns))
        world.inputs[1].default_value = amb
        bpy.context.scene.view_settings.exposure = expo
        cam.data.clip_start = clip or 0.05
        path = os.path.join(out_dir, name + ".png")
        px = el.render_view(cam, loc, target, lens, int(w * k), int(h * k), path, ortho=ortho)
        el.co.save_sheet(px, path)
        print("rendered", name, flush=True)


def CABIN_VIEWS(boom_fn):
    c = rt.cabin_centre_world()
    out = {}
    yaw = math.radians(rt.MAPLE_YAW)

    def world(x, y):  # cabin frame (x, y) -> street
        return Vector((c[0] + x * math.cos(yaw) + y * math.sin(yaw), c[1] - x * math.sin(yaw) + y * math.cos(yaw), rt.ZT))
    for name, at, facing, dist in (("cab_inside_bay", (0.3, 1.25), 90.0 + rt.MAPLE_YAW, 1.2), ("cab_inside_door", (0.0, 1.0), 250.0 + rt.MAPLE_YAW, 0.95),
                                   ("cab_inside_window", (-0.3, -0.75), 90.0 + rt.MAPLE_YAW, 0.75)):
        loc, target = boom_fn(world(*at), facing, dist, 12.0)
        out[name] = (loc, target, 14, 1280, 590, ["Cab"], ("Cab",), ("Moon", "Rim", "Fill"), 0.7, 0.45)
    return out


if __name__ == "__main__":
    main()
