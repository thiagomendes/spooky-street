"""Game export of the crowd costume kit (docs/living-street.md §7 and §8, slice of Phase 1; D34).

    blender -b -t 2 --python tools/blender/crowd/crowd_export.py -- [--out DIR] [--tex 2048] [--samples 4]
                                                                  [--ao-samples 16] [--report FILE] [--count]

Builds both crowd bodies and every part of crowd_kit.py (crowd_build.py), on Pip's skeleton fitted to each body
(char_export.human_bones, same bone names and axes) plus four costume bones: `Pail` (the right hand's prop, hangs
plumb as Pip's pail), `Blink` (brings the lids out of the skull), `Tail_1`, `Tail_2`. Then:

  realise at game resolution -> weights (bone heat on the body, nearest body weights for cloth, rigid for hats
  and props) -> one shared atlas for both bodies (smart UV, faces at 2.4x density) -> Cycles bake of the craft
  materials (colour, tangent normal, emission) with ambient occlusion per part -> painted details from a baked
  position map (faces, eyes, blink lids, the skeleton print, the ghost's eye holes, stitches, robot panel,
  crown gems, sweets) -> LOD1 copies (`<Part>_Lo`) -> clips (crowd_anims.py) -> one FBX per body.

Outputs in --out (default Assets/SpookyStreet/Content/Resources/Models/Characters/Crowd):
  CrowdChild.fbx, CrowdAdult.fbx      skeleton, clips Idle/Walk/Wave/Talk, every part and its _Lo as skinned meshes
  Crowd_BaseColor.png (2048)          the shared atlas
  Crowd_Normal.png (1024)             felt and paper relief (LOD0 only, per the budget)
  Crowd_Emission.png (512)            the paper lanterns' glow
"""
import json
import math
import os
import re
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOLS))
for sub in ("style", "pip", "chars", "export", "crowd"):
    p = os.path.join(TOOLS, sub)
    if p not in sys.path:
        sys.path.insert(0, p)

import char_export as ce  # noqa: E402
import craft as craft_lib  # noqa: E402
import crowd_anims as anims  # noqa: E402
import crowd_build as cb  # noqa: E402
import crowd_kit as kit  # noqa: E402
from char_export import SkinSource, set_weights, smooth, tri_count  # noqa: E402

OUT = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models", "Characters", "Crowd")
HEAD_DENSITY = 2.4  # faces get this many times the texels per metre of everything else
PROP_DENSITY = 0.8
GHOST_DENSITY = 1.5  # the ghost's painted face is on its sheet
LOD1_RATIO = {"Hands": 0.22, "Shoes": 0.3, "Head": 0.32, "Body": 0.34}
LOD1_DEFAULT = 0.36


def log(*a):
    print("[crowd_export]", *a, flush=True)


# --- 1. build and realise -------------------------------------------------------------------------------------

class Part:
    def __init__(self, body, name, slot, kind, opts):
        self.body, self.name, self.slot, self.kind, self.opts = body, name, slot, kind, opts
        self.pieces = []  # realised objects before the join: (obj, rule)
        self.obj = None
        self.lo = None

    @property
    def key(self):
        return f"{self.body}:{self.name}"


def build_all(craft, bodies=kit.BODIES):
    builders, parts = {}, []
    for body in bodies:
        B = cb.Builder(craft, body)
        builders[body] = B
        for name, (slot, kind, opts) in kit.PARTS[body].items():
            before = set(bpy.data.objects)
            made = B.build(f"{body[5:]}_{name}", kind, opts)
            part = Part(body, name, slot, kind, opts)
            part.sources = made
            parts.append(part)
            _ = before
    ce.patch_materials()
    bpy.context.view_layer.update()
    for part in parts:
        for src, rule, target in part.sources:
            g = ce.realize(src, (src.name, "A", None, None, True, target))
            part.pieces.append((g, rule))
        del part.sources
    for o in [o for o in bpy.data.objects if not o.name.startswith("G_")]:
        bpy.data.objects.remove(o, do_unlink=True)
    for cu in list(bpy.data.curves):
        if cu.users == 0:
            bpy.data.curves.remove(cu)
    return builders, parts


# --- 2. skeleton and weights -------------------------------------------------------------------------------

def bone_table(B):
    P, info = B.P, B.info
    r, hc = P.head_r, P.head_c
    table = ce.human_bones(P, info, cb.hand_size(P))
    g = info["grip"]
    tail = info.get("tail") or B.tail_points()
    table += [("Pail", "Hand_R", g.copy(), g + Vector((0, 0, -0.15 * P.h / 1.2)), False),
              ("Blink", "Head", hc + Vector((0, -0.5 * r, 0)), hc + Vector((0, -0.5 * r, 0.3 * r)), False),
              ("Tail_1", "Hips", tail[0].copy(), tail[2].copy(), False),
              ("Tail_2", "Tail_1", tail[2].copy(), tail[4].copy(), False)]
    return table


def weigh(parts, builders):
    rigs, ctx = {}, {}
    for body, B in builders.items():
        table = bone_table(B)
        rig = ce.build_armature(table, body + "_Rig")
        rigs[body] = (rig, table)
        ref = None
        for part in [p for p in parts if p.body == body and p.slot == "Body"]:
            for obj, rule in part.pieces:
                if rule != "body":
                    continue
                if ref is None:
                    names, W = ce.body_auto_weights(obj, rig, table)
                    obj.modifiers.clear()
                    obj.parent = None
                    ref = (obj, names, W)
                else:
                    if len(obj.data.vertices) != len(ref[0].data.vertices):
                        raise RuntimeError("body variants differ: " + part.name)
                    set_weights(obj, {n: ref[2][:, j] for j, n in enumerate(ref[1])})
        body_obj, names, W = ref
        ctx[body] = {
            "trunk": SkinSource(body_obj, names, W, {"Hips", "Spine", "Chest", "Neck"}),
            "names": names,
        }
    for part in parts:
        B = builders[part.body]
        for obj, rule in part.pieces:
            if rule == "body":
                continue
            apply_rule(obj, rule, part, B, ctx[part.body])
    return rigs


def apply_rule(obj, rule, part, B, ctx):
    P = B.P
    src = obj["src"]
    if isinstance(rule, tuple):
        set_weights(obj, {rule[1]: 1.0})
    elif rule == "head":
        set_weights(obj, {"Head": 1.0})
    elif rule == "lid":
        set_weights(obj, {"Blink": 1.0})
    elif rule == "cuff":
        side = "L" if sum(v.co.x for v in obj.data.vertices) > 0 else "R"
        set_weights(obj, {"LowerArm_" + side: 1.0})
    elif rule == "hand":
        prefix = src[:src.rindex("_Hand") + 5]  # the part name holds "_Hands" too
        ce.hand_weights(obj, ce.hand_scale(P, cb.hand_size(P)), src, len(prefix))
    elif rule == "shoe":
        prefix = src[:src.rindex("_Shoe") + 5]
        ce.shoe_weights(obj, src, len(prefix), ce.shoe_length(P) / 0.195)
    elif rule == "hood":
        z0 = P.head_c.z - 0.5 * P.head_r
        w = np.array([0.55 * smooth((z0 - v.co.z) / (0.6 * P.head_r)) for v in obj.data.vertices])
        set_weights(obj, {"Head": 1 - w, "Neck": w})
    elif rule == "skirt":
        cloth_weights(obj, P, ctx)
    elif rule == "sheet":
        sheet_weights(obj, P, ctx)
    elif rule == "tail":
        tail_weights(obj, B.info["tail"])
    else:
        raise RuntimeError(f"no weight rule {rule} for {src}")


def cloth_weights(obj, P, ctx):
    rows = cloth_rows(obj, P, ctx)
    bones = sorted({n for r in rows for n in r})
    set_weights(obj, {b: np.array([r.get(b, 0.0) for r in rows]) for b in bones})


# The ghost's sheet follows the arms part of the way, as a sheet does over a child's swinging arms: the lower part
# of the bell on each side takes some of that side's arm, nothing at the chest and the middle of the front and back,
# up to `pull` at the hem's sides (`x0`/`x1` in shoulder widths: where the pull starts and where it is full), so the
# walk's arm swing stays under the cloth. `lower` of it goes to the forearm at the hem.
SHEET_ARM = dict(x0=0.3, x1=1.7, pull=0.5, lower=0.3)


def sheet_weights(obj, P, ctx):
    rows = cloth_rows(obj, P, ctx)
    cfg = SHEET_ARM
    z_top, z_low = P.z["chest"], P.z["wrist"]
    for v, row in zip(obj.data.vertices, rows):
        p = v.co
        side = smooth((abs(p.x) / P.shoulder_x - cfg["x0"]) / (cfg["x1"] - cfg["x0"]))
        down = smooth((z_top - p.z) / (z_top - z_low))
        w = cfg["pull"] * side * down
        if w <= 1e-4:
            continue
        S = "L" if p.x > 0 else "R"
        lower = cfg["lower"] * down
        for n in list(row):
            row[n] *= 1 - w
        row["UpperArm_" + S] = row.get("UpperArm_" + S, 0.0) + w * (1 - lower)
        row["LowerArm_" + S] = row.get("LowerArm_" + S, 0.0) + w * lower
    bones = sorted({n for r in rows for n in r})
    set_weights(obj, {b: np.array([r.get(b, 0.0) for r in rows]) for b in bones})


def cloth_rows(obj, P, ctx):
    """Head above the neck (the ghost's hood), the trunk's nearest weights down to the hips, then the hips
    easing a little onto the leg on the same side, so the hem follows the stride without tearing."""
    src = ctx["trunk"]
    names = ctx["names"]
    z = P.z
    neck_top = P.head_c.z - 0.75 * P.head_r
    span = z["hip"] - z["knee"]
    rows = []
    for v in obj.data.vertices:
        p = v.co
        w = src.at(p).copy() * src.keep
        w = w / w.sum() if w.sum() > 1e-6 else np.eye(len(names))[names.index("Chest")]
        out = {n: w[j] for j, n in enumerate(names) if w[j] > 1e-4}
        low = smooth((z["hip"] + 0.04 * P.h - p.z) / (0.08 * P.h))
        if low > 0:
            side = "L" if p.x > 0 else "R"
            leg = 0.32 * smooth((z["hip"] - p.z) / (0.6 * span)) * min(1.0, abs(p.x) / (2.0 * P.hip_x))
            lower = {"Hips": 1 - leg, "UpperLeg_" + side: leg}
            out = {n: out.get(n, 0) * (1 - low) + lower.get(n, 0) * low for n in set(out) | set(lower)}
        up = smooth((p.z - neck_top) / (0.35 * P.head_r))
        if up > 0:
            out = {n: x * (1 - up) for n, x in out.items()}
            out["Head"] = out.get("Head", 0) + up
        rows.append(out)
    return rows


def tail_weights(obj, pts):
    mid = pts[2]
    base = pts[0]
    w2, wh = [], []
    for v in obj.data.vertices:
        d_mid = (v.co - mid).length
        d_base = (v.co - base).length
        along = d_base / (d_base + d_mid + 1e-6)
        w2.append(smooth((along - 0.42) / 0.3))
        wh.append(0.6 * (1 - smooth(d_base / 0.03)))
    w2, wh = np.array(w2), np.array(wh)
    w1 = 1 - w2
    set_weights(obj, {"Hips": wh * w1, "Tail_1": (1 - wh) * w1, "Tail_2": w2})


# --- 3. join, unwrap, bake, paint ------------------------------------------------------------------------------

def join_parts(parts):
    pid = 0
    pieces = {}
    for part in parts:
        objs = []
        for obj, rule in part.pieces:
            pid += 1
            pieces[pid] = (part.key, "lid" if rule == "lid" else re.sub(r"[.\d]+$", "", obj["src"]))
            me = obj.data
            me.attributes.new("pid", "FLOAT", "FACE").data.foreach_set("value", [float(pid)] * len(me.polygons))
            co = np.empty(len(me.vertices) * 3, dtype=np.float32)
            me.vertices.foreach_get("co", co)
            me.attributes.new("rest", "FLOAT_VECTOR", "POINT").data.foreach_set("vector", co)
            objs.append(obj)
        ce.activate(objs[0], objs[1:])
        if len(objs) > 1:
            bpy.ops.object.join()
        o = bpy.context.view_layer.objects.active
        o.name = o.data.name = part.key
        part.obj = o
        del part.pieces
    return pieces


def unwrap_all(parts):
    objs = [p.obj for p in parts]
    ce.activate(objs[0], objs[1:])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(62), island_margin=0.002, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.average_islands_scale()
    bpy.ops.object.mode_set(mode="OBJECT")
    for part in parts:
        k = HEAD_DENSITY if part.slot == "Head" else PROP_DENSITY if part.slot == "Prop" else \
            GHOST_DENSITY if part.kind == "ghost_sheet" else 1.0
        if k != 1.0:
            uv = part.obj.data.uv_layers.active.data
            arr = np.empty(len(uv) * 2, dtype=np.float32)
            uv.foreach_get("uv", arr)
            uv.foreach_set("uv", arr * k)
    ce.activate(objs[0], objs[1:])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=0.0025)
    bpy.ops.object.mode_set(mode="OBJECT")


def _target(objs, image):
    for o in objs:
        for mat in o.data.materials:
            nodes = mat.node_tree.nodes
            node = nodes.get("BakeTarget") or nodes.new("ShaderNodeTexImage")
            node.name = "BakeTarget"
            node.image = image
            nodes.active = node


def _bake(objs, image, kind, margin):
    _target(objs, image)
    ce.activate(objs[0], objs[1:])
    args = dict(margin=margin, use_clear=True, margin_type="EXTEND")
    if kind == "COLOR":
        bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, **args)
    elif kind == "NORMAL":
        bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", **args)
    else:
        bpy.ops.object.bake(type=kind, **args)


def _attr_material(name, attr, scale=1.0, bias=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emit = nodes.new("ShaderNodeEmission")
    a = nodes.new("ShaderNodeAttribute")
    a.attribute_type = "GEOMETRY"
    a.attribute_name = attr
    m = nodes.new("ShaderNodeVectorMath")
    m.operation = "MULTIPLY_ADD"
    m.inputs[1].default_value = (scale, scale, scale)
    m.inputs[2].default_value = (bias, bias, bias)
    links.new(a.outputs["Vector"] if attr == "rest" else a.outputs["Fac"], m.inputs[0])
    links.new(m.outputs[0], emit.inputs["Color"])
    links.new(emit.outputs[0], out.inputs[0])
    return mat


def bake_maps(parts, tex, samples, ao_samples):
    ce.bake_setup(samples)
    objs = [p.obj for p in parts]
    margin = max(2, tex // 512)
    images = {}

    def img(name, size, data=False, float_=False):
        im = bpy.data.images.new(name, size, size, alpha=False, float_buffer=float_, is_data=data)
        images[name] = im
        return im
    log("bake colour")
    _bake(objs, img("colour", tex), "COLOR", margin)
    log("bake normal")
    _bake(objs, img("normal", tex, data=True), "NORMAL", margin)
    log("bake emission")
    _bake(objs, img("emit", tex), "EMIT", margin)
    # ambient occlusion of each part on its own: spread the parts out so none shades another
    bpy.context.scene.cycles.samples = ao_samples
    for i, o in enumerate(objs):
        o.location = (6.0 * (i % 10), 6.0 * (i // 10), 0)
    bpy.context.view_layer.update()
    log("bake ao")
    _bake(objs, img("ao", tex, data=True), "AO", margin)
    for o in objs:
        o.location = (0, 0, 0)
    bpy.context.scene.cycles.samples = 1
    # rest position and piece id of every texel, for painting
    saved = {o: list(o.data.materials) for o in objs}
    for attr, scale, bias in (("rest", 0.25, 0.5), ("pid", 1.0, 0.0)):
        mat = _attr_material("Bake_" + attr, attr, scale, bias)
        for o in objs:
            o.data.materials.clear()
            o.data.materials.append(mat)
        log("bake", attr)
        _bake(objs, img(attr, tex, data=True, float_=True), "EMIT", 0)
    for o, mats in saved.items():
        o.data.materials.clear()
        for m in mats:
            o.data.materials.append(m)
    return images


# --- 4. LOD1, materials, clips, FBX -------------------------------------------------------------------------

def make_lod1(parts):
    for part in parts:
        o = part.obj
        lo = o.copy()
        lo.data = o.data.copy()
        bpy.context.scene.collection.objects.link(lo)
        lo.name = lo.data.name = part.key + kit.LOD1_SUFFIX
        dec = lo.modifiers.new("Dec", "DECIMATE")
        dec.ratio = LOD1_RATIO.get(part.slot, LOD1_DEFAULT)
        ce.rebake(lo, lo.data)
        part.lo = lo


def final_material(objs, base, normal, emission):
    mat = bpy.data.materials.new("Crowd_Atlas")
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    t = nodes.new("ShaderNodeTexImage")
    t.image = base
    links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    tn = nodes.new("ShaderNodeTexImage")
    tn.image = normal
    nm = nodes.new("ShaderNodeNormalMap")
    links.new(tn.outputs["Color"], nm.inputs["Color"])
    links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    te = nodes.new("ShaderNodeTexImage")
    te.image = emission
    links.new(te.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(mat)
        for p in o.data.polygons:
            p.material_index = 0
        for name in ("orco", "pid", "rest", "skull", "part", "grp"):
            if name in o.data.attributes:
                o.data.attributes.remove(o.data.attributes[name])


def solver_rig(table, f):
    rig = {name: (parent, head.copy()) for name, parent, head, _tail, _d in table}
    rig["_sole"] = {S: [("Foot_" + S, Vector((rig["Foot_" + S][1].x, 0.05 * f, 0.0))),
                        ("Foot_" + S, Vector((rig["Foot_" + S][1].x, -0.098 * f, 0.0))),
                        ("Toe_" + S, Vector((rig["Toe_" + S][1].x, -0.156 * f, 0.0)))] for S in "LR"}
    return rig


def export_body(body, B, rig, table, parts, out):
    mine = [p for p in parts if p.body == body]
    others = [p for p in parts if p.body != body]
    for p in others:
        p.obj.name = "~" + p.key
        p.lo.name = "~" + p.key + kit.LOD1_SUFFIX
    meshes = []
    for p in mine:
        p.obj.name = p.name
        p.lo.name = p.name + kit.LOD1_SUFFIX
        for o in (p.obj, p.lo):
            ce.limit_weights(o)
            o.parent = rig
            o.matrix_parent_inverse.identity()
            o.modifiers.clear()
            o.modifiers.new("Armature", "ARMATURE").object = rig
            meshes.append(o)
    P = B.P
    srig = solver_rig(table, ce.shoe_length(P) / 0.195)
    anims.bind(srig, cb.LID_DEPTH * P.head_r * 1.0)
    clips = ce.key_clips(rig, anims.clips(body), anims.solve, anims.FPS)
    path = os.path.join(out, body + ".fbx")
    ce.export_fbx(rig, meshes, path, anims.FPS)
    for p in mine:
        for o in (p.obj, p.lo):
            o.parent = None
            o.modifiers.clear()
    for p in parts:
        p.obj.name = p.key
        p.lo.name = p.key + kit.LOD1_SUFFIX
    log("exported", path)
    heads = {b: [round(c, 4) for c in h] for b, _p, h, _t, _d in table}
    return {"fbx": path, "clips": clips, "bones": [b[0] for b in table], "bone_heads": heads,
            "height": P.h, "head_radius": P.head_r}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(name, default, cast=str):
        return cast(argv[argv.index(name) + 1]) if name in argv else default
    out = opt("--out", OUT)
    tex = opt("--tex", 2048, int)
    report_path = opt("--report", "")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    craft = craft_lib.Craft(1.0)
    bodies = opt("--bodies", ",".join(kit.BODIES)).split(",")
    atlas = kit.ATLAS[bodies[0]]
    assert all(kit.ATLAS[b] == atlas for b in bodies), "one atlas per export"
    recipes = {w: r for w, r in {**kit.WALKERS, **kit.NEIGHBOURS}.items() if r[0] in bodies}
    builders, parts = build_all(craft, bodies)
    counts = {p.key: sum(tri_count(o) for o, _r in p.pieces) for p in parts}
    log("triangles per part", json.dumps(counts, indent=0))
    for wid, (body, names) in recipes.items():
        log(wid, sum(counts[f"{body}:{n}"] for n in names))
    if "--count" in argv:
        return
    rigs = weigh(parts, builders)
    pieces = join_parts(parts)
    unwrap_all(parts)
    images = bake_maps(parts, tex, opt("--samples", 4, int), opt("--ao-samples", 16, int))
    import crowd_paint
    os.makedirs(out, exist_ok=True)
    paths = crowd_paint.finish(images, pieces, parts, builders, out, tex, atlas)
    make_lod1(parts)
    final_material([p.obj for p in parts] + [p.lo for p in parts], *crowd_paint.final_images(paths))
    report = {"textures": paths, "bodies": {}, "parts": {}, "walkers": {}}
    for body, (rig, table) in rigs.items():
        report["bodies"][body] = export_body(body, builders[body], rig, table, parts, out)
    for p in parts:
        report["parts"][p.key] = {"slot": p.slot, "lod0": tri_count(p.obj), "lod1": tri_count(p.lo)}
    for wid, (body, names) in recipes.items():
        report["walkers"][wid] = {
            "body": body, "parts": names,
            "lod0": sum(report["parts"][f"{body}:{n}"]["lod0"] for n in names),
            "lod1": sum(report["parts"][f"{body}:{n}"]["lod1"] for n in names)}
    log(json.dumps(report["walkers"], indent=1))
    if report_path:
        with open(report_path, "w") as fh:
            json.dump(report, fh, indent=1)


if __name__ == "__main__":
    main()
