"""Adds the trick-or-treat clips to the committed crowd FBX files, leaving their meshes, skin, skeleton and old clips
alone (docs/living-street-crowd-art.md "Trick-or-treat clips"; graphics standards §1).

    blender -b -t 2 --python tools/blender/crowd/crowd_takes.py -- --fresh DIR [--into DIR]
                                                                [--bodies CrowdChild,CrowdAdult,CrowdNeighbours]
                                                                [--base DIR] [--report FILE]

1. For each body, rebuilds only what the skeleton needs (the hands give the hand pivots and the grip; the tail
   points are the builder's), the same bone table as crowd_export.py, and keys EVERY clip of crowd_anims.clips(body)
   on it: the four shipped ones and the new ones. Writes <fresh>/<body>.fbx (the rig and its takes, no meshes).
   Compare it with the committed file with pip_fbx_diff.py: the shipped clips' curves must be identical (that proves
   the rebuilt skeleton is the committed one bone for bone).
2. With --into, copies only the new takes (fbx_add_takes.py) from the fresh file into the committed
   Assets/.../Crowd/<body>.fbx and writes the result to <into>/<body>.fbx (pass the Crowd folder itself to replace
   the committed files). --base reads the committed files from another folder (e.g. the shipped ones out of git,
   once the folder already holds the new takes).

The door neighbours (CrowdNeighbours) are the grown-up body with its own file: they get the grown-up's clips.
"""
import json
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars", "export", "crowd"):
    p = os.path.join(TOOLS, sub)
    if p not in sys.path:
        sys.path.insert(0, p)

import char_export as ce  # noqa: E402
import craft as craft_lib  # noqa: E402
import crowd_anims as anims  # noqa: E402
import crowd_build as cb  # noqa: E402
import crowd_export as ex  # noqa: E402
import crowd_kit as kit  # noqa: E402

SHIPPED = set(kit.CLIPS)
BODIES = ("CrowdChild", "CrowdAdult", kit.NEIGHBOUR_BODY)


def log(*a):
    print("[crowd_takes]", *a, flush=True)


def rig_for(body, craft):
    """The body's skeleton as crowd_export.py builds it, from the hands alone (nothing else moves a bone)."""
    B = cb.Builder(craft, body)
    B.hands(f"{body}_Tmp", tone="t1")
    table = ex.bone_table(B)
    rig = ce.build_armature(table, body + "_Rig")
    for o in [o for o in bpy.data.objects if o.type != "ARMATURE"]:
        bpy.data.objects.remove(o, do_unlink=True)
    return B, rig, table


def export_takes(body, out):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    craft = craft_lib.Craft(1.0)
    B, rig, table = rig_for(body, craft)
    P = B.P
    srig = ex.solver_rig(table, ce.shoe_length(P) / 0.195)
    anims.bind(srig, cb.LID_DEPTH * P.head_r * 1.0)
    clips = anims.clips(body)
    report = ce.key_clips(rig, clips, anims.solve, anims.FPS)
    path = os.path.join(out, body + ".fbx")
    ce.export_fbx(rig, [], path, anims.FPS)
    extra = {"walk_start_distance": round(anims.step_distance("WalkStart"), 4),
             "walk_stop_distance": round(anims.step_distance("WalkStop"), 4),
             "walk_start_phase": anims.START_PHASE[anims._kind(body)],
             "walk_speed": anims.WALK["speed"]}
    log("exported", path, json.dumps(extra))
    return path, [c["name"] for c in report if c["name"] not in SHIPPED], report, extra


def add_takes(committed, fresh, out, takes):
    import fbx_add_takes
    argv = sys.argv
    sys.argv = ["fbx_add_takes", "--", committed, fresh, out] + list(takes)
    try:
        fbx_add_takes.main()
    finally:
        sys.argv = argv


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(name, default=None):
        return argv[argv.index(name) + 1] if name in argv else default
    fresh = opt("--fresh")
    into = opt("--into")
    bodies = opt("--bodies", ",".join(BODIES)).split(",")
    os.makedirs(fresh, exist_ok=True)
    report = {}
    for body in bodies:
        path, new, clips, extra = export_takes(body, fresh)
        report[body] = {"fresh": path, "new_takes": new, "clips": clips, **extra}
        if into:
            os.makedirs(into, exist_ok=True)
            committed = os.path.join(opt("--base", ex.OUT), body + ".fbx")
            target = os.path.join(into, body + ".fbx")
            add_takes(committed, path, target, new)
            report[body]["out"] = target
    log(json.dumps({b: {k: v for k, v in r.items() if k != "clips"} for b, r in report.items()}, indent=1))
    if opt("--report"):
        with open(opt("--report"), "w") as fh:
            json.dump(report, fh, indent=1)


if __name__ == "__main__":
    main()
