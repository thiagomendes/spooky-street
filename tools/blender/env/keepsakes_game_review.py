"""Review sheet of the GAME versions of the S004 seeds and keepsakes: re-imports the FBX files written by
`env_export_keepsakes.py` and renders them on the baked textures with back-face culling on (like URP Lit) and
emission on `*_Glow` objects, in place: the troupe photo in the hall (the play camera's distance on a phone-shaped
frame, then closer, then the two puppets and the mount crest the seed cutscene pushes in on), the crest sign on
the owl house from the plaza and close, the bat closed / fluttering / open, the mobile empty and full, and the
trunk shut and open.

Run: blender -b -t 2 --python tools/blender/env/keepsakes_game_review.py -- out.png [draft|final] [models_dir] [view ...]"""
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
import env_interior_review as ir
import interior as it
import keepsakes as ks
import owl_house as oh
from env_lib import H

ATLAS = {"TroupePhoto": "TroupePhoto", "OwlCrest": "OwlCrest", "PaperBatKeepsake": "Keepsakes", "BatMobile": "Keepsakes", "DressUpTrunk": "Keepsakes"}
LANTERN = 0.9  # GlowLevels.KeepsakeLantern: under the bloom threshold
CREST_SIGN = 0.6  # GlowLevels.CrestSign: lit paint
PHOTO_AT = it.MECH @ it.PHOTO  # the photo's frame centre in owl-house builder space
PROPS_X = 300.0


class Library(ir.Library):
    def load(self, model, group, place, tag=None):
        if model in ATLAS:
            ir.NEW[model] = ATLAS[model]
        for k in (1, 2, 3):
            ir.GLOW[f"KeepsakeLantern{k}"] = LANTERN
        ir.GLOW["CrestSign"] = CREST_SIGN
        return super().load(model, group, place, tag)


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
    cam.data.clip_start = 0.02
    el.night_world(0.7, top=H(0x241B4A), horizon=H(0x4A3378))
    el.sun_light("Moon", (1.2, 2.2, -2.0), 2.0, (0.72, 0.76, 1.0), 6.0, 90.0)
    el.sun_light("Rim", (-2.2, -1.6, -1.2), 1.6, (0.6, 0.55, 1.0), 6.0, 90.0)
    el.sun_light("Fill", (2.4, 1.2, -0.4), 0.55, (1.0, 0.8, 0.7), 6.0, 90.0)
    el.sun_light("PKey", (1.6, 2.6, -1.9), 3.0, (1.0, 0.88, 0.76), 8.0, 30.0)
    el.sun_light("PRim", (-1.9, -2.4, -1.4), 2.2, (0.66, 0.66, 1.0), 8.0, 30.0)
    el.sun_light("PFill", (-2.4, 1.6, -0.5), 0.7, (0.8, 0.8, 1.0), 8.0, 30.0)
    hp = ir.house_pt
    # the owl house with its crest sign (builder space -> review scene, as the game places it)
    eg = lib.load("OwlHouse", "Ext", ir.HOUSE) + lib.load("OwlHouseDressing", "Ext", ir.HOUSE)
    c = hp(*oh.CREST_AT)
    lib.load("OwlCrest", "Ext", (c.x, c.z, c.y, 0.0))
    for tag, o in eg:
        p, _s = gr.centre(o)
        if tag == "AtticWindow":
            el.point_light("ExtAttic", p + Vector((0, -0.85, 0)), 35, (1.0, 0.78, 0.45), 0.3, shadow=False)
        elif tag == "Lamp":
            el.point_light("ExtPorch", p + Vector((0.0, -0.1, -0.42)), 55, (1.0, 0.7, 0.38), 0.12, shadow=True)
    # the hall, with the photo on the partition (it hangs in the mechanism's frame, facing the foyer)
    ir.lamps(lib.load("OwlHouseInterior", "Int", ir.PLACEMENTS["OwlHouseInterior"]), "Int")
    lib.load("WorkshopMechanism", "Int", ir.PLACEMENTS["WorkshopMechanism"])
    ir.HIDDEN.add(lib.parts["WorkshopMechanism"]["LinkArm_Seated"])
    p = hp(*PHOTO_AT)
    lib.load("TroupePhoto", "Int", (p.x, p.z, p.y, 90.0))
    el.point_light("IntNook", hp(2.2, 3.1, 1.9), 9, (0.75, 0.8, 1.0), 0.2, shadow=False)
    el.point_light("IntFill", hp(-0.6, 3.0, 1.9), 14, (1.0, 0.8, 0.6), 0.5, shadow=False)
    el.point_light("IntPhoto", p + Vector((-1.1, -0.3, 0.5)), 6, (1.0, 0.82, 0.6), 0.4, shadow=False)  # the hall lamp that catches the gilt
    # the keepsakes on their own
    x = PROPS_X
    for i, fold in enumerate((168.0, 80.0, 0.0)):
        tag = f"Bat{i}"
        lib.load("PaperBatKeepsake", "Bats", (x - 0.42 + 0.42 * i, 0.1, 0.0, 0.0), tag)
        ir.turn(lib, tag, "Wing_L", "Z", fold)
        ir.turn(lib, tag, "Wing_R", "Z", -fold)
    for i, bats in enumerate((0, 30)):
        tag = f"Mobile{bats}"
        mx = x + 6.0 + 1.6 * i
        lib.load("BatMobile", "Mobiles", (mx, 1.3, 0.0, 0.0), tag)
        if not bats:
            for name, o in lib.parts[tag].items():
                if name.startswith("Hook_") or name.endswith("_Close") or name.endswith("_Glow"):
                    ir.HIDDEN.add(o)
        else:
            for n, (_k, top, bottom) in enumerate(ks.hook_points()):
                bt = f"MobileBat{n}"
                yaw = math.degrees(math.atan2(top.y, top.x)) + 90.0
                s = ks.MOBILE_BAT
                lib.load("PaperBatKeepsake", "Mobiles", (mx + bottom.x, 1.3 + bottom.z - (0.36 * ks.BAT_K + 0.012) * s, bottom.y, -yaw), bt)
                holder = lib.parts[bt]["PaperBatKeepsake"].parent
                holder.scale = (s, s, s)
                ir.turn(lib, bt, "Wing_L", "Z", 18.0)
                ir.turn(lib, bt, "Wing_R", "Z", -18.0)
    for i, deg in enumerate((0.0, -105.0)):
        tag = f"Trunk{i}"
        lib.load("DressUpTrunk", "Trunks", (x + 12.0 - 0.5 + i, 0.0, 0.0, (-18.0, 22.0)[i]), tag)
        ir.turn(lib, tag, "TrunkLid", "X", deg)
    k = 1.0 if final else 0.5
    tmp = os.path.splitext(out)[0]
    world = bpy.context.scene.world.node_tree.nodes["Background"]
    inside = (["Int"], ("Int",), (), 0.5, 0.35)
    outside = (["Ext"], ("Ext",), ("Moon", "Rim", "Fill"), 0.7, 0.35)
    prop = ((), ("PKey", "PRim", "PFill"), 0.9, -0.1)
    west = Vector((-1.0, 0.0, 0.0))  # the photo faces the foyer (-X in the house)

    def at_photo(dx, dz, dist):
        """Camera looking square at a point of the photo (dx along the frame, dz up), `dist` away."""
        q = p + Vector((0.0, -dx, dz))
        return q + west * dist, q

    views = {
        # name: (camera, target, lens, w, h, groups, point-light prefixes, suns, world, exposure)
        "hall_phone": (hp(-0.9, 3.3, 2.15), hp(2.2, 5.0, 1.75), 22, 844, 390, *inside),  # the play camera behind Pip in the hall
        "hall_close": (*at_photo(0.0, -0.05, 1.5), 28, 844, 390, *inside),
        "photo": (*at_photo(0.0, 0.0, 1.0), 50, 1100, 760, *inside),
        "owl": (*at_photo(it.PUPPETS["Puppet_Owl"][0] + 0.03, 0.04, 0.42), 50, 500, 500, *inside),
        "moon": (*at_photo(it.PUPPETS["Puppet_Moon"][0] - 0.02, 0.05, 0.42), 50, 500, 500, *inside),
        "mount": (*at_photo(it.PHOTO_CREST[0] - 0.03, it.PHOTO_CREST[1] + 0.02, 0.36), 50, 500, 500, *inside),
        "plaza_phone": (Vector((0.6, 0.0, 2.3)), hp(0.0, 0.0, 2.6), 35, 844, 390, *outside),  # the play camera in the plaza
        "gate_phone": (Vector((-0.5, 5.5, 2.0)), c, 30, 844, 390, *outside),
        "crest": (c + Vector((-0.5, -1.6, -0.25)), c, 50, 600, 600, *outside),
        "bats": (Vector((x, -2.6, 0.5)), Vector((x, 0.0, 0.17)), 80, 1200, 400, ["Bats"], *prop),
        "bats_side": (Vector((x - 0.7, -1.0, 0.6)), Vector((x + 0.05, 0.0, 0.16)), 50, 600, 400, ["Bats"], *prop),
        "mobiles": (Vector((x + 6.8 + 0.3, -3.3, 1.0)), Vector((x + 6.8, 0.0, 0.8)), 50, 1200, 800, ["Mobiles"], *prop),
        "trunks": (Vector((x + 12.0, -2.4, 1.6)), Vector((x + 12.0, 0.0, 0.42)), 50, 1200, 760, ["Trunks"], *prop),
    }
    tiles = {}
    for name, (loc, target, lens, w, h, groups, lights, suns, amb, expo) in views.items():
        if todo and name not in todo:
            continue
        ir.show(groups, lights)
        gr.set_lights(set(suns))
        world.inputs[1].default_value = amb
        bpy.context.scene.view_settings.exposure = expo
        tiles[name] = el.render_view(cam, loc, target, lens, int(w * k), int(h * k), f"{tmp}_{name}.png")
        print("rendered", name, flush=True)
    order = [["hall_phone", "hall_close"], ["photo", "owl"], ["moon", "mount", "crest"], ["plaza_phone", "gate_phone"], ["bats", "bats_side"],
             ["mobiles", "trunks"]]
    rows = []
    for row in order:
        got = [tiles[n] for n in row if n in tiles]
        if got:
            hh = max(t.shape[0] for t in got)
            rows.append(np.concatenate([el.pad(t, t.shape[1], hh) for t in got], axis=1))
    width = max(r.shape[1] for r in rows)
    rows = [el.pad(r, width, r.shape[0]) for r in rows]
    el.co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
    print("keepsakes game review sheet ->", out)


if __name__ == "__main__":
    main()
