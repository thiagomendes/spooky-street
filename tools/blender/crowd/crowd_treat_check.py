"""Measures the trick-or-treat clips on the EXPORTED FBX files (the files the game loads), the same checks as the
Unity import tests (TreatClipImportTests), so a failure shows before CI:

    blender -b -t 2 --python tools/blender/crowd/crowd_treat_check.py -- [--crowd DIR] [--pip DIR] [--report FILE]

Per model and clip: the first and last frame against the rest pose the clip promises (Idle frame 0; Walk at the
start phase or frame 0 for the walk transitions), as the largest bone position difference; the hand (or the pail
grip) at the clip's marker, ahead of the root and above the ground; and for WalkStart / WalkStop how far the planted
ball of the foot slides forward or back with the root moved by the contract's travel, over the frames it is on the
ground, and how far it pivots sideways (Walk sets the feet narrower than Idle).
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars", "export", "crowd"):
    p = os.path.join(TOOLS, sub)
    if p not in sys.path:
        sys.path.insert(0, p)

import crowd_anims as anims  # noqa: E402
import witness_review as wr  # noqa: E402

FPS = 30
VERBOSE = "--verbose" in sys.argv
CLIPS = {
    "CrowdChild": ["HoldPail", "Cheer", "WalkStart", "WalkStop"],
    "CrowdAdult": ["GiveTreat", "OpenDoorPeek", "WalkStart", "WalkStop"],
    "CrowdNeighbours": ["GiveTreat", "OpenDoorPeek", "WalkStart", "WalkStop"],
    "Pip": ["HandTreat"],
}
# clip -> (marker u, bone, expected (ahead, height), extra drop under the bone)
MARKS = {
    "GiveTreat": (0.55, "Fingers_L", (0.45, 0.62), 0.0),
    "OpenDoorPeek": (0.25, "Fingers_L", (0.32, 1.0), 0.0),
    "HoldPail": (0.5, "Pail", (0.26, 0.56), 0.085),
    "HandTreat": (0.55, "Fingers_L", (0.38, 0.62), 0.0),
}
STEP = {"CrowdChild": (1.167, 20 / 24), "CrowdAdult": (1.173, 35 / 41), "CrowdNeighbours": (1.173, 35 / 41)}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(name, default):
        return argv[argv.index(name) + 1] if name in argv else default
    crowd = opt("--crowd", os.path.join(wr.MODELS, "Characters", "Crowd"))
    pip = opt("--pip", os.path.join(wr.MODELS, "Characters", "Pip"))
    report = {}
    for model, clips in CLIPS.items():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        m = wr.import_model(pip if model == "Pip" else crowd, model, leaf_bones=True)
        rig = m["rig"]
        rig.animation_data_create()
        scene = bpy.context.scene

        def sample(clip, frame):
            act = m["actions"][clip]
            rig.animation_data.action = act
            f0 = act.frame_range[0]
            whole = math.floor(frame)
            scene.frame_set(int(f0 + whole), subframe=frame - whole)
            return {b.name: rig.matrix_world @ b.head for b in rig.pose.bones}

        def length(clip):
            a = m["actions"][clip]
            return int(a.frame_range[1] - a.frame_range[0])
        rest = sample("Idle", 0)
        ahead = rest["Toe_L"] - rest["Foot_L"]
        ahead.z = 0
        ahead.normalize()
        for clip in clips:
            n = length(clip)
            start, end = rest, rest
            if clip == "WalkStart":
                end = sample("Walk", STEP[model][1] * length("Walk"))
            if clip == "WalkStop":
                start = sample("Walk", 0)
            first, last = sample(clip, 0), sample(clip, n)
            r = {"frames": n,
                 "first_vs_rest_mm": round(1000 * max((first[b] - start[b]).length for b in start), 2),
                 "last_vs_rest_mm": round(1000 * max((last[b] - end[b]).length for b in end), 2)}
            if clip in MARKS:
                u, bone, (want_ahead, want_h), drop = MARKS[clip]
                at = sample(clip, u * n)[bone]
                r["marker"] = {"bone": bone, "ahead": round(at.dot(ahead), 3), "height": round(at.z - drop, 3),
                               "want": [want_ahead, want_h]}
            if clip in ("WalkStart", "WalkStop"):
                speed = STEP[model][0]
                anims.WALK.update(speed=speed)
                anims.BODY = "CrowdChild" if model == "CrowdChild" else "CrowdAdult"
                d = json.load(open(os.environ["CROWD_TAKES_REPORT"]))[model][
                    "walk_start_distance" if clip == "WalkStart" else "walk_stop_distance"] \
                    if os.environ.get("CROWD_TAKES_REPORT") else None
                k = speed * (anims.STEP_FRAMES / FPS) / d
                ball = "Toe_R" if clip == "WalkStart" else "Toe_L"
                pts = []
                for f in range(n + 1):
                    u = f / n
                    travel = d * u ** k if clip == "WalkStart" else d * (1 - (1 - u) ** k)
                    p = sample(clip, f)[ball] + ahead * travel
                    pts.append(p)
                    if VERBOSE:
                        print("   ", f, round(travel, 4), tuple(round(c, 4) for c in sample(clip, f)[ball]), tuple(round(c, 4) for c in p))
                low = min(p.z for p in pts)
                down = [Vector((p.x, p.y, 0)) for p in pts if p.z <= low + 0.01]
                side = Vector((-ahead.y, ahead.x, 0))
                r["planted_frames"] = len(down)
                r["slide_forward_mm"] = round(1000 * max(abs((a - b).dot(ahead)) for a in down for b in down), 2)
                r["drift_sideways_mm"] = round(1000 * max(abs((a - b).dot(side)) for a in down for b in down), 2)
                r["travel_m"] = d
            report[f"{model}:{clip}"] = r
            print("[treat_check]", model, clip, json.dumps(r), flush=True)
    if opt("--report", ""):
        with open(opt("--report", ""), "w") as fh:
            json.dump(report, fh, indent=1)


if __name__ == "__main__":
    main()
