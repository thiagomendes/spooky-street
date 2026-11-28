"""Adds new clips of pip_anims.CLIPS to the committed Pip.fbx without touching its meshes, skin, skeleton or old clips
(graphics standards §1; the same route as the S003a glances, docs/S003a-gestures.md).

    blender -b -t 2 --python tools/blender/export/pip_takes.py -- --fresh DIR [--into DIR] [--takes HandTreat,…]

1. Builds the approved Pip (pip_vampire.build_pip) only as far as her skeleton needs (the hand pivots), makes the
   bone table of pip_export.py and keys EVERY clip of pip_anims.CLIPS on it; writes <fresh>/Pip.fbx (the rig and its
   takes, no meshes). pip_fbx_diff.py against the committed Pip.fbx must show every shipped clip's curves identical.
2. With --into, copies only the named takes (default: the clips the committed file lacks) into the committed Pip.fbx
   (fbx_add_takes.py) and writes <into>/Pip.fbx.
"""
import json
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars", "export"):
    p = os.path.join(TOOLS, sub)
    if p not in sys.path:
        sys.path.insert(0, p)

import char_export as ce  # noqa: E402
import pip_anims as anims  # noqa: E402
import pip_export as pe  # noqa: E402


def log(*a):
    print("[pip_takes]", *a, flush=True)


def committed_takes(path):
    from io_scene_fbx import parse_fbx
    import fbx_add_takes as fat
    root, _version = parse_fbx.parse(path)
    objects = fat.top(root, b"Objects").elems
    return {fat.take_name(o) for o in objects if o.id == b"AnimationStack"}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(name, default=None):
        return argv[argv.index(name) + 1] if name in argv else default
    fresh, into = opt("--fresh"), opt("--into")
    os.makedirs(fresh, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import craft as craft_lib
    import pip_vampire as pv
    P = pv.build_pip(craft_lib.Craft(1.2))
    bpy.context.view_layer.update()
    info = {"handL": bpy.data.objects["Pip_HandLPivot"].matrix_world.copy(),
            "handR": bpy.data.objects["Pip_HandRPivot"].matrix_world.copy()}
    rim = bpy.data.objects["Pip_PailRim"]
    centre = sum((v.co for v in rim.data.vertices), Vector()) / len(rim.data.vertices)
    info["grip"] = centre + Vector((0, 0, 0.103))  # as pip_export.build_game_parts
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    table = pe.bone_table(P, info)
    rig = ce.build_armature(table, pe.RIG_NAME)
    srig = pe.solver_rig(table)
    anims.RIG = srig

    def pose_at(p):
        D, offset = anims.solve(p, srig)
        return D, {"Hips": offset}
    report = ce.key_clips(rig, anims.CLIPS, pose_at, anims.FPS)
    path = os.path.join(fresh, "Pip.fbx")
    ce.export_fbx(rig, [], path, anims.FPS)
    log("exported", path)
    committed = os.path.join(opt("--base", pe.DEFAULT_OUT), "Pip.fbx")  # --base: the shipped file from elsewhere
    takes = [t for t in opt("--takes", "").split(",") if t] or \
        [c["name"] for c in report if c["name"] not in committed_takes(committed)]
    log("new takes", takes)
    if into:
        import fbx_add_takes
        os.makedirs(into, exist_ok=True)
        saved = sys.argv
        sys.argv = ["fbx_add_takes", "--", committed, path, os.path.join(into, "Pip.fbx")] + takes
        try:
            fbx_add_takes.main()
        finally:
            sys.argv = saved
    log(json.dumps([c for c in report if c["name"] in takes], indent=1))


if __name__ == "__main__":
    main()
