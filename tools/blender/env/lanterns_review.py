"""Review sheet of the GAME versions of the paper lantern art: re-imports the FBX files written by
`env_export_lanterns.py` and renders them on the committed `Lanterns` atlas with back-face culling on (like URP
Lit), the lantern paper tinted the way the game tints it (base colour times the paper colour, emission map times
the light colour times `GlowLevels.PaperLantern`), beside Pip and the shipped porch, lamp and jack-o'-lanterns at
their game glow levels, so the brightness order can be judged.

Rows: the four shapes in the four colours, front and three-quarter; the gameplay camera behind Pip at the table, the
table close and from the side; Pip's porch with the hook and her lantern lit, from the arrival camera and from the
plaza, its greyscale, and the hook close.

Run: blender -b -t 2 --python tools/blender/env/lanterns_review.py -- out.png [draft|final] [models_dir] [view ...]"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("env", "export", "style", "pip", "chars"):
    if os.path.join(TOOLS, sub) not in sys.path:
        sys.path.insert(0, os.path.join(TOOLS, sub))

import bpy
import numpy as np
from mathutils import Matrix, Vector

import env_export as ex
import env_game_review as gr
import env_lib as el
import lanterns as ln
import witness_review as wr
from env_lib import H, lin

LANTERN = 0.9  # GlowLevels.PaperLantern: the crowd's lanterns' level, well under the attic window (4.0)
GAME_GLOW = {"Lamp": 1.3, "Face": 1.8, ln.SAMPLES_TAG: LANTERN}  # GlowLevels as the game sets them
GAME_DISTANCE, GAME_FOV = 3.8, 50.0
PHONE = (844, 390)
ENV = ex.OUT_DIR

# Unity placements (x, y up, z, yaw) of the shipped arrival porch (EnvironmentLayout.Street) and the art's own
PORCH = [("Kit_Floor", (-1.0, 0.0, -13.0, 180.0)), ("Kit_Floor", (1.0, 0.0, -13.0, 180.0)), ("Kit_Steps", (0.0, 0.0, -13.0, 180.0)),
         ("Kit_Rail", (-1.95, 0.6, -14.0, 90.0)), ("Kit_Rail", (1.95, 0.6, -14.0, 90.0)), ("Crate", (-1.4, 0.6, -13.45, 15.0)),
         ("CandyBowl", (-1.4, 1.21, -13.45, -20.0)), ("Pumpkin", (1.35, 0.0, -11.7, 0.0)), ("JackOLantern", (1.8, 0.0, -11.25, -15.0)),
         ("Mailbox", (-1.45, 0.0, -10.6, 12.0)), ("StreetLamp", (2.2, 0.0, -9.6, 0.0)), ("JackOLantern", (-1.5, 0.0, -8.2, 20.0)),
         ("Pumpkin", (1.45, 0.0, -6.9, 40.0))]
HOOK_AT = (-1.95, 0.6, -13.15, 180.0)  # LanternArt.PorchHook*: the slice's crook post spot, turned like the kit
PORCH_LANTERN = ("cat", "orange")
TABLE_AT = (6.0, 0.0, -6.0, 180.0)  # front toward +Z (north), as on the Green
PIP_AT_TABLE = (6.0, 0.0, -4.95)
LINEUP_X = 20.0


def game_lens(aspect):
    half_w = math.atan(math.tan(math.radians(GAME_FOV / 2)) * aspect)
    return 18.0 / math.tan(half_w)


def to_scene(place):
    x, y, z, yaw = place
    return Matrix.Translation((x, z, y)) @ Matrix.Rotation(-math.radians(yaw), 4, "Z")


def image(path, colour=True):
    img = bpy.data.images.load(path, check_existing=True)
    if not colour:
        img.colorspace_settings.name = "Non-Color"
    return img


def lit_material(folder, atlas, paper=None, light=None, glow=0.0):
    """URP-Lit-like: base map x base colour, normal map, emission map x emission colour x intensity."""
    mat = bpy.data.materials.new(f"Review_{atlas}_{glow}_{paper}")
    mat.use_nodes = True
    mat.use_backface_culling = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.85
    base = nodes.new("ShaderNodeTexImage")
    base.image = image(os.path.join(folder, atlas + "_BaseColor.png"))
    col = base.outputs["Color"]
    if paper is not None:
        mul = nodes.new("ShaderNodeMix")
        mul.data_type, mul.blend_type = "RGBA", "MULTIPLY"
        mul.inputs[0].default_value = 1.0
        links.new(col, mul.inputs[6])
        mul.inputs[7].default_value = lin(paper)
        col = mul.outputs[2]
    links.new(col, bsdf.inputs["Base Color"])
    npath = os.path.join(folder, atlas + "_Normal.png")
    if os.path.exists(npath):
        tn = nodes.new("ShaderNodeTexImage")
        tn.image = image(npath, False)
        nm = nodes.new("ShaderNodeNormalMap")
        links.new(tn.outputs["Color"], nm.inputs["Color"])
        links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    if glow:
        te = nodes.new("ShaderNodeTexImage")
        te.image = image(os.path.join(folder, atlas + "_Emission.png"))
        e = te.outputs["Color"]
        if light is not None:
            mul = nodes.new("ShaderNodeMix")
            mul.data_type, mul.blend_type = "RGBA", "MULTIPLY"
            mul.inputs[0].default_value = 1.0
            links.new(e, mul.inputs[6])
            mul.inputs[7].default_value = lin(light)
            e = mul.outputs[2]
        links.new(e, bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = glow
    return mat


class Scene:
    def __init__(self, folder):
        self.folder, self.mats, self.groups = folder, {}, {}

    def material(self, folder, atlas, paper=None, light=None, glow=0.0):
        key = (folder, atlas, paper, light, glow)
        if key not in self.mats:
            self.mats[key] = lit_material(folder, atlas, paper, light, glow)
        return self.mats[key]

    def load(self, model, place, group, colour=None, scale=1.0):
        """Import a model at a Unity placement (or a scene matrix). Returns {child name: object}."""
        ours = model.startswith("PaperLantern") or model in (ln.TABLE, ln.HOOK)
        folder = self.folder if ours else ENV
        atlas = "Lanterns" if ours else "Env"
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(folder, model + ".fbx"))
        new = [o for o in bpy.data.objects if o not in before]
        holder = bpy.data.objects.new(f"{model}_at", None)
        el.pm.link(holder)
        holder.matrix_world = (place if isinstance(place, Matrix) else to_scene(place)) @ Matrix.Scale(scale, 4)
        self.groups.setdefault(group, []).append(holder)
        parts = {}
        for o in new:
            if o.parent is None:
                o.parent = holder
            parts[o.name.split(".")[0]] = o
            if o.type != "MESH":
                continue
            o.data.materials.clear()
            name = o.name.split(".")[0]
            if name == ln.GLOW_TAG + ex.GLOW_SUFFIX:
                paper, light = ln.COLOURS[colour] if colour else (None, None)
                o.data.materials.append(self.material(folder, atlas, paper, light, LANTERN))
            elif name.endswith(ex.GLOW_SUFFIX):
                o.data.materials.append(self.material(folder, atlas, glow=GAME_GLOW.get(name[:-len(ex.GLOW_SUFFIX)], 1.5)))
            else:
                o.data.materials.append(self.material(folder, atlas))
        bpy.context.view_layer.update()
        return parts

    def solo(self, names):
        keep = set()
        for g in names:
            for h in self.groups.get(g, []):
                keep.add(h)
        for o in bpy.data.objects:
            top = el.top_of(o)
            if o.type in ("MESH", "EMPTY", "ARMATURE"):
                o.hide_render = not (top in keep or top.name == "Stage")
            elif o.type == "LIGHT" and o.data.type == "POINT":
                o.hide_render = not any(o.name.startswith(g + "_") for g in names)


def pip(scene, at, yaw, group):
    m = wr.import_model(os.path.join(wr.MODELS, "Characters", "Pip"), "Pip", leaf_bones=True)
    holder = bpy.data.objects.new("Pip_at", None)
    el.pm.link(holder)
    for o in m["all"]:
        if o.parent is None:
            o.parent = holder
    holder.matrix_world = to_scene((at[0], at[1], at[2], yaw + 180.0))  # the model faces -Z (Blender -Y)
    rig = m["rig"]
    act = m["actions"].get("Idle")
    if rig is not None and act is not None:
        rig.animation_data_create()
        rig.animation_data.action = act
        bpy.context.scene.frame_set(int(act.frame_range[0]))
    scene.groups.setdefault(group, []).append(holder)
    return holder


def lamp(group, at, energy, colour, radius=0.1, shadow=False):
    el.point_light(f"{group}_lamp", Vector(at), energy, colour, radius, shadow)


def gameplay(target, yaw, pitch=12.0, dist=GAME_DISTANCE):
    """The gameplay camera: `dist` behind `target` (a Unity point) looking along Unity yaw `yaw`."""
    t = Vector((target[0], target[2], target[1]))
    a, e = math.radians(yaw), math.radians(pitch)
    fwd = Vector((math.sin(a), math.cos(a), 0.0))
    loc = t - fwd * dist * math.cos(e) + Vector((0, 0, dist * math.sin(e)))
    return loc, t


def grey(tile):
    g = tile.copy()
    v = 0.299 * g[:, :, 0] + 0.587 * g[:, :, 1] + 0.114 * g[:, :, 2]
    g[:, :, 0] = g[:, :, 1] = g[:, :, 2] = v
    return g


def main():
    out, quality, rest = el.args()
    folder = rest[0] if rest and os.path.isdir(rest[0]) else ex.OUT_DIR
    todo = [r for r in rest if not os.path.isdir(r)]
    final = quality == "final"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    craft = el.EnvCraft(1.2)
    el.stage_ground(craft, 400.0, H(0x232041))
    cam = el.setup_render(samples=48 if final else 12, exposure=0.0)
    cam.data.clip_start = 0.02
    el.night_world(0.55, top=H(0x241B4A), horizon=H(0x4A3378))
    el.sun_light("Moon", (1.2, 2.2, -2.0), 1.4, (0.7, 0.74, 1.0), 6.0, 40.0)
    el.sun_light("Rim", (-2.2, -1.6, -1.2), 0.9, (0.6, 0.55, 1.0), 6.0, 40.0)
    sc = Scene(folder)
    colours = list(ln.COLOURS)
    # the sixteen: shapes across, colours down; and the neutral paper beside them
    for i, shape in enumerate(ln.SHAPES):  # neutral paper on top, then the four colours
        for k, cid in enumerate([None] + colours):
            sc.load(ln.MODELS[shape], Matrix.Translation((LINEUP_X + 0.62 * i, 0.0, 2.5 - 0.46 * k)), "Lineup", cid)
    # the table, Pip folding at it
    sc.load(ln.TABLE, TABLE_AT, "Table")
    pip(sc, PIP_AT_TABLE, 180.0, "Table")
    lamp("Table", (TABLE_AT[0] + 2.2, TABLE_AT[2] + 1.5, 3.2), 60, (1.0, 0.74, 0.42), 0.12, True)  # the Green's lamp
    # Pip's porch, the hook and her lantern
    for model, place in PORCH:
        parts = sc.load(model, place, "Porch")
        for name, o in parts.items():
            if name == "Lamp_Glow":
                c, _s = gr.centre(o)
                lamp("Porch", c + Vector((0, 0, -0.6)), 70, (1.0, 0.74, 0.42), 0.12, True)
            elif name == "Face_Glow":
                c, _s = gr.centre(o)
                lamp("Porch", c + Vector((0, 0, 0.05)), 6, (1.0, 0.65, 0.35), 0.1)
    hook = sc.load(ln.HOOK, HOOK_AT, "Porch")
    hang = hook[ln.HANG].matrix_world.translation.copy()
    shape, cid = PORCH_LANTERN
    sc.load(ln.MODELS[shape], Matrix.Translation(hang), "Porch", cid)
    lamp("Porch", hang + Vector((0, 0, -ln.drop_to_centre(shape))), 8, lin(ln.COLOURS[cid][1])[:3], 0.1)
    pip(sc, (0.0, 0.6, -13.9), 0.0, "Porch")
    lens = game_lens(PHONE[0] / PHONE[1])
    k = 1.0 if final else 0.5
    tmp = os.path.splitext(out)[0]
    lx = LINEUP_X + 0.93
    views = {
        # name: (camera, target, lens, w, h, groups)
        "lineup": (Vector((lx, -3.4, 1.6)), Vector((lx, 0.0, 1.42)), 40, 1000, 1000, ["Lineup"]),
        "lineup_side": (Vector((lx + 2.4, -2.6, 1.9)), Vector((lx, 0.3, 1.42)), 40, 1000, 1000, ["Lineup"]),
        "table_phone": (*gameplay((PIP_AT_TABLE[0], 0.95, PIP_AT_TABLE[2]), 180.0), lens, *PHONE, ["Table"]),
        "table_close": (Vector((TABLE_AT[0] - 1.3, TABLE_AT[2] + 1.7, 1.55)), Vector((TABLE_AT[0], TABLE_AT[2], 0.95)), 40, 844, 600, ["Table"]),
        "table_side": (Vector((TABLE_AT[0] + 2.6, TABLE_AT[2] + 0.4, 1.3)), Vector((TABLE_AT[0], TABLE_AT[2], 0.9)), 40, 600, 600, ["Table"]),
        "porch_arrival": (*gameplay((0.0, 1.55, -13.5), 0.0, 6.0), lens, *PHONE, ["Porch"]),
        "porch_plaza": (*gameplay((0.0, 1.0, -9.6), 180.0, 10.0), lens, *PHONE, ["Porch"]),
        "hook_corner": (Vector((-1.2, -12.2, 2.6)), Vector((-1.9, -13.2, 0.9)), 35, 600, 600, ["Porch"]),
        "hook_close": (hang + Vector((0.4, 1.7, -0.6)), hang + Vector((0.25, 0.0, -0.55)), 40, 600, 600, ["Porch"]),
    }
    tiles = {}
    world = bpy.context.scene.world.node_tree.nodes["Background"]
    for name, (loc, target, lns, w, h, groups) in views.items():
        if todo and name not in todo:
            continue
        sc.solo(groups)
        world.inputs[1].default_value = 0.55
        tiles[name] = el.render_view(cam, loc, target, lns, int(w * k), int(h * k), f"{tmp}_{name}.png")
        print("rendered", name, flush=True)
    if "porch_plaza" in tiles:
        tiles["porch_grey"] = grey(tiles["porch_plaza"])
    order = [["lineup", "lineup_side"], ["table_phone", "table_side"], ["table_close", "hook_corner", "hook_close"], ["porch_arrival", "porch_plaza"],
             ["porch_grey"]]
    rows = []
    for row in order:
        got = [tiles[n] for n in row if n in tiles]
        if got:
            hh = max(t.shape[0] for t in got)
            rows.append(np.concatenate([el.pad(t, t.shape[1], hh) for t in got], axis=1))
    width = max(r.shape[1] for r in rows)
    rows = [el.pad(r, width, r.shape[0]) for r in rows]
    el.co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
    print("lanterns game review sheet ->", out)


if __name__ == "__main__":
    main()
