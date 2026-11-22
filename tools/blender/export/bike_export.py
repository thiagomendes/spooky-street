"""Bea's bicycle as a static prop (spec §2: "festooned with foil moons and paper bats").

    blender -b -t 2 --python tools/blender/export/bike_export.py -- [out_dir] [size=1024] [samples=4] [count]

Builds the approved bicycle (tools/blender/chars/cast.py `build_bike`, at the size it has next to Bea in the
approved lineup: 0.9), realises its parts at game resolution with char_export, and then follows the environment
conventions by reusing tools/blender/env/env_export.py for the rest: faces no viewpoint can see are dropped
(`visible`), Smart UV + pack, Cycles bake of the craft materials, plain one-material mesh, the same FBX settings.

Outputs (default Assets/…/Models/Environment): BeaBicycle.fbx, BeaBicycle_BaseColor.png, BeaBicycle_Normal.png
(ModelKit's per-model texture convention). Origin: on the ground midway between the wheels; the front wheel and
basket point to -Y (Unity +Z after import); it stands upright on its wheels with the kickstand out to its left.
"""
import math
import os
import re
import sys

import bmesh
import bpy
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars", "export", "env"):
    sys.path.insert(0, os.path.join(TOOLS, sub))

import char_export as ce  # noqa: E402
import env_export as ex  # noqa: E402

NAME = "BeaBicycle"
SCALE = 0.9  # the approved lineup shows the bicycle at 0.9 of the builder's size
# builder part -> subdivision level, curve (bevel resolution, spline resolution), keep bevels, triangle target
PARTS = [
    (r"Bike_Tyre", NAME, 0, None, True, 520),
    (r"Bike_Rim", NAME, 0, None, True, 300),
    (r"Bike_Hub", NAME, 0, None, True, 64),
    (r"Bike_Spokes", NAME, None, None, True, 150),
    (r"Bike_Frame", NAME, None, (1, 1), True, None),
    (r"Bike_(SeatPost|Stem|Kickstand)", NAME, None, (0, 1), True, None),
    (r"Bike_Handlebar", NAME, None, (1, 3), True, 150),
    (r"Bike_Grip", NAME, None, (1, 1), True, None),
    (r"Bike_Saddle", NAME, 0, None, True, 110),
    (r"Bike_Chainring", NAME, 0, None, True, 90),
    (r"Bike_Cranks", NAME, None, (0, 2), True, 70),
    (r"Bike_Pedal", NAME, 0, None, True, 44),
    (r"Bike_BasketRim", NAME, 0, None, True, 170),
    (r"Bike_Basket", NAME, 0, None, True, ("sheet", 280)),
    (r"Bike_String", NAME, None, None, True, None),
    (r"Bike_Moon", NAME, None, None, False, 60),
    (r"Bike_PaperBat", NAME, None, None, False, None),
]


def log(*a):
    print("[bike_export]", *a, flush=True)


def build_working_mesh():
    import cast
    craft = ex.GameCraft(1.2)
    cast.build_bike(craft)
    bpy.context.view_layer.update()
    ce.patch_materials()
    char = ce.Char(NAME, (NAME,), PARTS, re.compile(r"$^"), tag="bike_export")
    parts = []
    for src in [o for o in bpy.data.objects if o.type in ("MESH", "CURVE")]:
        spec = char.spec_for(src.name)
        if spec is None:
            raise RuntimeError("no game spec for " + src.name)
        parts.append(ce.realize(src, spec))
    for o in [o for o in bpy.data.objects if not o.name.startswith("G_")]:
        bpy.data.objects.remove(o, do_unlink=True)
    counts = {}
    for o in parts:
        key = re.sub(r"[.\d]+$", "", o["src"])
        counts[key] = counts.get(key, 0) + ce.tri_count(o)
    log("per part:", ", ".join(f"{k[5:]} {v}" for k, v in sorted(counts.items(), key=lambda kv: -kv[1])))
    ce.activate(parts[0], parts[1:])
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = obj.data.name = NAME + "__main"
    # the builder's front wheel is at -X: turn it to -Y, at lineup size
    obj.data.transform(Matrix.Scale(SCALE, 4) @ Matrix.Rotation(math.radians(90), 4, "Z"))
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    verts = [v.co.copy() for v in bm.verts]
    faces = [[v.index for v in f.verts] for f in bm.faces]
    keep = ex.visible(verts, faces, [False] * len(faces), None, True, None)
    doomed = [f for f, k in zip(bm.faces[:], keep) if not k]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    log(f"{len(doomed)} of {len(faces)} faces never visible, dropped; {len(obj.data.polygons)} triangles")
    return obj


def main():
    rest = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = dict(a.split("=", 1) for a in rest if "=" in a)
    flags = [a for a in rest if "=" not in a]
    out_dir = next((a for a in flags if a != "count"), ex.OUT_DIR)
    size, samples = int(opts.get("size", 1024)), int(opts.get("samples", 4))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    obj = build_working_mesh()
    if "count" in flags:
        return
    model = ex.Model(NAME, NAME, None)
    model.parts[None] = obj
    os.makedirs(out_dir, exist_ok=True)
    ex.unwrap_and_pack([model], NAME, size)
    ex.bake_atlas([model], NAME, size, samples, out_dir)
    os.remove(os.path.join(out_dir, NAME + "_Emission.png"))  # nothing on the bicycle glows
    report = ex.export_model(model, {NAME: ex.plain_material(NAME)}, out_dir)
    lo = [min(v.co[i] for v in obj.data.vertices) for i in range(3)]
    hi = [max(v.co[i] for v in obj.data.vertices) for i in range(3)]
    log(f"exported {NAME}.fbx: {report['triangles']} tris, {model.density:.0f} px/m, bounds "
        f"{[round(c, 3) for c in lo]} .. {[round(c, 3) for c in hi]}")


if __name__ == "__main__":
    main()
