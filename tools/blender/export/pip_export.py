"""Pip, game-ready: low-poly meshes, one baked atlas (+ a face map), a rig, seventeen clips, one FBX.

    blender -b -t 2 --python tools/blender/export/pip_export.py -- [--out DIR] [--cache FILE.blend]
                                                               [--tex 2048] [--face-tex 1024] [--report FILE.json]

Builds the approved Pip with tools/blender/chars/pip_vampire.py, then:
  1. realises every part at game resolution (lower subdivision, decimation) and removes faces nothing can see;
  2. unwraps (Smart UV) into one atlas for body/hair/cape/pail and a second, small map for the face;
  3. bakes the procedural craft materials with Cycles (CPU): base colour x subtle AO, tangent-space normals;
  4. builds the armature and skin weights (bone heat for the body, transferred / analytic for everything else);
  5. keys the clips from pip_anims.py as one action each and exports the FBX (one take per action).

Outputs in --out (default Assets/SpookyStreet/Content/Resources/Models/Characters/Pip): Pip.fbx, Pip_BaseColor.png,
Pip_Normal.png, Pip_Face.png. `--cache` saves the scene after step 4 and, when the file already exists, starts
from it, so animation edits re-export in seconds. Conventions: metres, Z up, Pip faces -Y (Unity +Z after import).
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
for sub in ("style", "pip", "chars", "export"):
    sys.path.insert(0, os.path.join(TOOLS, sub))

import char_export as ce  # noqa: E402
import pip_anims as anims  # noqa: E402
from char_export import SkinSource, set_weights, smooth, tri_count  # noqa: E402

DEFAULT_OUT = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models", "Characters", "Pip")
RIG_NAME = "Pip_Rig"
GROUPS = ("Body", "Hair", "Cape", "Pail")
# small surface details: they never count as cover when deciding which faces are hidden
DETAIL = re.compile(r"Pip_(SleeveFold|VestButton|VestPiping|Brooch|Clasp|Jabot|ShortsHem|Shoe.Lace|HairGroove|"
                    r"HairStrand|BatClip|Lash|Brow|Smile|Glint|Cuff)")  # atlas meshes; the face is its own mesh with its own map

# Source part (regex on the builder's object name) -> group, subdivision level (None = as built), curve
# (bevel resolution, spline resolution), keep bevel modifiers, triangle target (None = no decimation;
# ("sheet", n) decimates the cloth as a single sheet of n triangles and only then gives it its thickness, so the
# outer and lining shells share one topology and can never cross).
PARTS = [
    (r"Pip_Head$", "Face", 0, None, True, None),
    (r"Pip_Sclera", "Face", 0, None, True, 400),
    (r"Pip_Iris", "Face", None, None, True, 180),
    (r"Pip_Pupil", "Face", None, None, True, 120),
    (r"Pip_Glint", "Face", None, None, True, None),
    (r"Pip_Lash", "Face", None, (1, 4), True, 160),
    (r"Pip_Brow", "Face", None, (1, 4), True, 110),
    (r"Pip_Smile", "Face", None, (1, 4), True, 260),
    (r"Pip_Nose", "Face", 0, None, True, 360),
    (r"Pip_Fang", "Face", 0, None, True, 96),
    (r"Pip_(Mouth|Tongue)", "Face", None, None, True, None),
    (r"Pip_Hair$", "Hair", 0, None, True, None),
    (r"Pip_HairGroove", "Hair", None, (0, 2), True, 56),
    (r"Pip_HairStrand", "Hair", None, (0, 1), True, 40),
    (r"Pip_BatClipFelt", "Hair", None, None, False, None),
    (r"Pip_Body$", "Body", None, None, True, 4100),
    (r"Pip_Shorts$", "Body", 1, None, True, ("sheet", 360)),
    (r"Pip_ShortsLeg", "Body", 1, None, True, ("sheet", 96)),
    (r"Pip_ShortsHem", "Body", 0, None, True, 72),
    (r"Pip_Vest$", "Body", 1, None, True, ("sheet", 560)),
    (r"Pip_VestButtonDisc", "Body", 0, None, True, 56),
    (r"Pip_VestPiping", "Body", None, (0, 3), True, 110),
    (r"Pip_Brooch", "Body", 0, None, True, 90),
    (r"Pip_Jabot", "Body", 0, None, True, ("sheet", 60)),
    (r"Pip_SleeveFold", "Body", 0, None, True, 72),
    (r"Pip_Cuff", "Body", 0, None, True, 150),
    (r"Pip_CapeCollar", "Body", 1, None, True, ("sheet", 160)),
    (r"Pip_ClaspButton", "Body", 0, None, True, 72),
    (r"Pip_ClaspCord", "Body", None, (0, 3), True, 72),
    (r"Pip_Hand[LR]Finger", "Body", None, None, True, 140),
    (r"Pip_Hand[LR]Thumb", "Body", None, None, True, 210),
    (r"Pip_Hand[LR]Palm", "Body", 0, None, True, 240),
    (r"Pip_Hand[LR]Wrist", "Body", None, None, True, 96),
    (r"Pip_Hand[LR]Nail", "Body", 0, None, True, 32),
    (r"Pip_Shoe[LR]Upper", "Body", 0, None, True, 400),
    (r"Pip_Shoe[LR]ToeCap", "Body", 0, None, True, 220),
    (r"Pip_Shoe[LR]HeelCounter", "Body", 0, None, True, 180),
    (r"Pip_Shoe[LR]HeelTab", "Body", 0, None, True, 36),
    (r"Pip_Shoe[LR]Lace", "Body", None, (0, 2), True, 24),
    (r"Pip_Shoe[LR]Sole", "Body", None, None, True, 340),
    (r"Pip_Shoe[LR]Tongue", "Body", 0, None, True, 90),
    (r"Pip_Shoe[LR]Collar", "Body", 0, None, True, 200),
    (r"Pip_Cape$", "Cape", 1, None, True, ("sheet", 1500)),
    (r"Pip_CapeStarFelt", "Cape", None, None, False, None),
    (r"Pip_CapeStitch", "Cape", None, None, True, 260),
    (r"Pip_Pail$", "Pail", 0, None, True, 700),
    (r"Pip_PailRim", "Pail", 0, None, True, 140),
    (r"Pip_Pail(Eye|Grin)", "Pail", None, None, False, None),
    (r"Pip_PailHandle", "Pail", None, (0, 3), True, 140),
]


PIP = ce.Char("Pip", GROUPS, PARTS, DETAIL, tag="pip_export")
log = PIP.log


# --- 1. game meshes ----------------------------------------------------------------------------------------

def flare_vest(src, me):
    if src.name.startswith("Pip_Vest"):
        # The vest hem and the shorts' waistband share a radius; at game resolution they would zig-zag through
        # each other, so the vest flares a few millimetres over the waistband (below the waist, z = 0.696).
        for v in me.vertices:
            f = 1 + 0.075 * smooth((0.7 - v.co.z) / 0.08)
            v.co.x *= f
            v.co.y *= f


def trim_hair(obj, P):
    """Drop the bob's inner shell where nothing can look at it: keep it beside the cheeks and along the hem."""
    r, hc = P.head_r, P.head_c
    centre = hc + Vector((0, r * 0.04, r * 0.1))
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    doomed = []
    for f in bm.faces:
        c = f.calc_center_median()
        inner = f.normal.dot(c - centre) < 0
        lon = abs(math.degrees(math.atan2(c.x, -c.y)))
        if inner and lon > 80 and c.z > hc.z - 0.36 * r:
            doomed.append(f)
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(obj.data)
    bm.free()


def build_game_parts():
    import craft as craft_lib
    import pip_vampire as pv
    P = pv.build_pip(craft_lib.Craft(1.2))
    bpy.context.view_layer.update()
    ce.patch_materials()
    info = {
        "handL": bpy.data.objects["Pip_HandLPivot"].matrix_world.copy(),
        "handR": bpy.data.objects["Pip_HandRPivot"].matrix_world.copy(),
    }
    rim = bpy.data.objects["Pip_PailRim"]
    centre = sum((v.co for v in rim.data.vertices), Vector()) / len(rim.data.vertices)
    info["grip"] = centre + Vector((0, 0, 0.103))
    sources = [o for o in bpy.data.objects if o.type in ("MESH", "CURVE")]
    parts = []
    for src in sources:
        spec = PIP.spec_for(src.name)
        if spec is None:
            raise RuntimeError("no game spec for " + src.name)
        parts.append(ce.realize(src, spec, flare_vest))
        if src.name == "Pip_Hair":
            trim_hair(parts[-1], P)
        if src.name == "Pip_Head":
            # the crown and back of the skull are always under the bob (and would wrap the face's UV island)
            ce.keep_skull_front(parts[-1], P.head_c, lambda d: d.y > 0.42 and d.z > -0.7)
    for o in [o for o in bpy.data.objects if not o.name.startswith("G_")]:
        bpy.data.objects.remove(o, do_unlink=True)
    for cu in list(bpy.data.curves):
        if cu.users == 0:
            bpy.data.curves.remove(cu)
    return P, parts, info


# --- 2. rig -------------------------------------------------------------------------------------------------

def cape_ring(z):
    """Half-width of the cape at height z (the builder's lathe profile)."""
    prof = [(0.93, 0.13), (0.88, 0.188), (0.774, 0.2), (0.6, 0.216), (0.304, 0.232)]
    for (z0, r0), (z1, r1) in zip(prof, prof[1:]):
        if z >= z1:
            return r0 + (r1 - r0) * (z0 - min(z, z0)) / (z0 - z1)
    return prof[-1][1]


CAPE_Y = 0.64  # the cape's depth / width
CAPE_JOINTS = (0.87, 0.68, 0.49, 0.30)
CAPE_AZ = 58.0  # degrees from straight back to the side chains


def bone_table(P, info):
    """[(name, parent, head, tail, deforms the body skin)] in parents-first order."""
    z = P.z
    hc, r = P.head_c, P.head_r
    B = [("Root", None, Vector((0, 0, 0)), Vector((0, 0, 0.12)), False),
         ("Hips", "Root", Vector((0, 0, z["pelvis"])), Vector((0, 0, 0.69)), True),
         ("Spine", "Hips", Vector((0, 0, 0.69)), Vector((0, 0, 0.79)), True),
         ("Chest", "Spine", Vector((0, 0, 0.79)), Vector((0, 0, 0.905)), True),
         ("Neck", "Chest", Vector((0, 0, 0.905)), Vector((0, 0.004, 0.952)), True),
         ("Head", "Neck", Vector((0, 0.004, 0.952)), Vector((0, 0.004, 1.2)), True),
         ("Hair_L", "Head", hc + Vector((r * 1.02, 0.02, 0.0)), hc + Vector((r * 1.12, 0.02, -0.095)), False),
         ("Hair_R", "Head", hc + Vector((-r * 1.02, 0.02, 0.0)), hc + Vector((-r * 1.12, 0.02, -0.095)), False),
         ("Hair_Back", "Head", hc + Vector((0, r * 1.08, 0.0)), hc + Vector((0, r * 1.2, -0.095)), False)]
    for S, s in (("L", 1), ("R", -1)):
        sh = Vector((s * P.shoulder_x, 0, z["shoulder"]))
        el = P.elbow(s) + Vector((0, 0.014, 0))
        wr = P.wrist(s)
        M = info["hand" + S]
        k = P.head_r / 0.13 * 1.15
        hand_tip = M @ Vector((0, 0, -0.05 * k))
        knuckle = M @ Vector((0.002 * k, 0, -0.053 * k))
        finger_tip = M @ Vector((0.002 * k, 0, -0.098 * k))
        t1 = M @ (Vector((-0.005, -0.029, -0.03)) * k)
        t3 = M @ (Vector((-0.013, -0.049, -0.057)) * k)
        B += [("Shoulder_" + S, "Chest", Vector((s * 0.03, 0, z["shoulder"] - 0.008)), sh, True),
              ("UpperArm_" + S, "Shoulder_" + S, sh, el, True),
              ("LowerArm_" + S, "UpperArm_" + S, el, wr, True),
              ("Hand_" + S, "LowerArm_" + S, wr, hand_tip, True),
              ("Fingers_" + S, "Hand_" + S, knuckle, finger_tip, False),
              ("Thumb_" + S, "Hand_" + S, t1, t3, False)]
        if S == "R":
            B.append(("Pail", "Hand_R", info["grip"].copy(), info["grip"] + Vector((0, 0, -0.15)), False))
        hip = Vector((s * P.hip_x, 0, z["hip"]))
        knee = Vector((s * P.hip_x * 1.02, -0.006, z["knee"]))
        ankle = Vector((s * P.hip_x, 0.004, z["ankle"]))
        ball = Vector((s * P.hip_x, -0.098, 0.022))
        B += [("UpperLeg_" + S, "Hips", hip, knee, True),
              ("LowerLeg_" + S, "UpperLeg_" + S, knee, ankle, True),
              ("Foot_" + S, "LowerLeg_" + S, ankle, ball, True),
              ("Toe_" + S, "Foot_" + S, ball, Vector((s * P.hip_x, -0.158, 0.022)), False)]
    for C, s in (("L", 1), ("M", 0), ("R", -1)):
        az = math.radians(CAPE_AZ) * s
        pts = [Vector((cape_ring(zz) * math.sin(az), cape_ring(zz) * CAPE_Y * math.cos(az), zz))
               for zz in CAPE_JOINTS]
        for i in range(3):
            B.append((f"Cape_{C}{i + 1}", "Chest" if i == 0 else f"Cape_{C}{i}", pts[i], pts[i + 1], False))
    return B


# --- 3. skin weights ----------------------------------------------------------------------------------------

def cape_weights(obj, rigid=False):
    """Analytic cape weights from position; `rigid` gives a sewn-on patch the weights of its centre."""
    n = len(obj.data.vertices)
    centre = sum((v.co for v in obj.data.vertices), Vector()) / n
    W = {"Chest": np.zeros(n)}
    for C in "LMR":
        for i in (1, 2, 3):
            W[f"Cape_{C}{i}"] = np.zeros(n)
    j = CAPE_JOINTS
    for v in obj.data.vertices:
        p = centre if rigid else v.co
        az = math.degrees(math.atan2(p.x, p.y / CAPE_Y))
        cl = smooth(az / CAPE_AZ)
        cr = smooth(-az / CAPE_AZ)
        chains = (("L", cl), ("M", 1 - cl - cr), ("R", cr))
        top = smooth((0.925 - p.z) / 0.09)
        b12 = smooth((j[1] + 0.06 - p.z) / 0.12)
        b23 = smooth((j[2] + 0.06 - p.z) / 0.12)
        levels = (top * (1 - b12), top * b12 * (1 - b23), top * b12 * b23)
        W["Chest"][v.index] = 1 - top
        for C, c in chains:
            for i, lv in enumerate(levels):
                W[f"Cape_{C}{i + 1}"][v.index] = c * lv
    set_weights(obj, W)


def hair_weights(obj, P):
    n = len(obj.data.vertices)
    W = {k: np.zeros(n) for k in ("Head", "Hair_L", "Hair_R", "Hair_Back")}
    hc = P.head_c
    for v in obj.data.vertices:
        p = v.co
        dyn = 0.85 * smooth((hc.z - 0.005 - p.z) / 0.1)
        lon = abs(math.degrees(math.atan2(p.x, -p.y)))  # 0 front … 180 back
        back = smooth((lon - 95) / 70)
        side = "Hair_L" if p.x > 0 else "Hair_R"
        W["Hair_Back"][v.index] = dyn * back
        W[side][v.index] = dyn * (1 - back)
        W["Head"][v.index] = 1 - dyn
    set_weights(obj, W)


def weight_parts(parts, P, rig, table):
    body = next(o for o in parts if o["src"] == "Pip_Body")
    names, W = ce.body_auto_weights(body, rig, table)
    torso = {"Hips", "Spine", "Chest", "Neck"}
    on_torso = SkinSource(body, names, W, torso)  # garments never pick up arm weights from a resting arm
    on_hips = SkinSource(body, names, W, {"Hips", "Spine", "UpperLeg_L", "UpperLeg_R"})
    on_arm = {S: SkinSource(body, names, W, {"UpperArm_" + S, "LowerArm_" + S}) for S in "LR"}
    for o in parts:
        src = o["src"]
        if o is body:
            continue
        side = "L" if sum(v.co.x for v in o.data.vertices) > 0 else "R"
        if o["grp"] == "Face":
            set_weights(o, {"Head": 1.0})
        elif o["grp"] == "Hair":
            hair_weights(o, P)
        elif o["grp"] == "Pail":
            set_weights(o, {"Pail": 1.0})
        elif o["grp"] == "Cape":
            cape_weights(o, rigid=src.startswith("Pip_CapeStar"))
        elif src.startswith("Pip_Hand"):
            ce.hand_weights(o, P.head_r / 0.13 * 1.15, src, len("Pip_Hand"))
        elif src.startswith("Pip_Shoe"):
            ce.shoe_weights(o, src, len("Pip_Shoe"))
        elif re.match(r"Pip_(CapeCollar|Clasp)", src):
            set_weights(o, {"Chest": 1.0})
        elif src.startswith("Pip_Cuff"):
            set_weights(o, {"LowerArm_" + side: 1.0})
        elif src.startswith("Pip_SleeveFold"):
            on_arm[side].transfer(o)
        elif re.match(r"Pip_(VestButton|Brooch)", src):
            on_torso.transfer(o, rigid=True)
        elif re.match(r"Pip_(Vest|Jabot)", src):
            on_torso.transfer(o)
        elif re.match(r"Pip_Shorts(Leg|Hem)", src):
            on_hips.transfer(o, lambda n, x: n == "Hips" or n == "UpperLeg_" + ("L" if x > 0 else "R"),
                             fallback="Hips")
        elif src == "Pip_Shorts":
            on_hips.transfer(o, fallback="Hips")
        else:
            raise RuntimeError("no weights for " + src)
    body.modifiers.clear()
    body.parent = None


# --- 6. animation + export -----------------------------------------------------------------------------------

def solver_rig(table):
    rig = {name: (parent, head.copy()) for name, parent, head, _tail, _d in table}
    sole = {}
    for S, s in (("L", 1), ("R", -1)):
        x = rig["Foot_" + S][1].x
        sole[S] = [("Foot_" + S, Vector((x, 0.05, 0.0))), ("Foot_" + S, Vector((x, -0.098, 0.0))),
                   ("Toe_" + S, Vector((x, -0.156, 0.0)))]
    rig["_sole"] = sole
    return rig


# --- main ----------------------------------------------------------------------------------------------------

def stage_model(args):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    P, parts, info = build_game_parts()
    log("parts", len(parts), "triangles before culling", sum(tri_count(o) for o in parts))
    counts = {}
    for o in parts:
        key = re.sub(r"[.\d]+$", "", o["src"])
        counts[key] = counts.get(key, 0) + tri_count(o)
    log("per part:", ", ".join(f"{k[4:]} {v}" for k, v in sorted(counts.items(), key=lambda kv: -kv[1])))
    table = bone_table(P, info)
    rig = ce.build_armature(table, RIG_NAME)
    weight_parts(parts, P, rig, table)
    lists = {g: [o for o in parts if o["grp"] == g] for g in GROUPS + ("Face",)}
    face = ce.join(PIP, lists["Face"], "Pip_Face")
    by_group = {g: ce.join(PIP, lists[g], "Tmp_" + g) for g in GROUPS}
    solid = [face, by_group["Hair"], by_group["Body"]]
    log("hidden faces removed", ce.remove_hidden(PIP, solid, {"Pip_Face": 1, "Tmp_Hair": 1, "Tmp_Body": 2}))
    atlas = ce.join(PIP, [by_group[g] for g in GROUPS], "Tmp_Atlas")
    ce.unwrap(atlas, 0.0035)
    ce.unwrap_face(face, P.head_c, P.head_r)
    meshes = ce.split_groups(PIP, atlas)
    solid = [meshes["Body"], meshes["Hair"], face]
    # AO occluders: the cape and pail move far from the body, so they shade only themselves
    visible = {meshes["Body"]: solid, meshes["Hair"]: solid, face: solid, meshes["Cape"]: [meshes["Cape"]],
               meshes["Pail"]: [meshes["Pail"]]}
    paths = ce.bake_all(PIP, meshes, face, visible, args["out"], args["tex"], args["face_tex"], args["samples"], args["ao_samples"])
    objs = [meshes[g] for g in GROUPS] + [face]
    for o in objs:
        ce.limit_weights(o)
        for name in ("orco", "grp", "skull", "part"):
            if name in o.data.attributes:
                o.data.attributes.remove(o.data.attributes[name])
        o.parent = rig
        o.modifiers.new("Armature", "ARMATURE").object = rig
    rig["pip_table"] = json.dumps([(n, p, tuple(h), tuple(t), d) for n, p, h, t, d in table])
    rig["pip_textures"] = json.dumps(paths)
    return rig, objs


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

    def opt(name, default, cast=str):
        return cast(argv[argv.index(name) + 1]) if name in argv else default
    args = {"out": opt("--out", DEFAULT_OUT), "cache": opt("--cache", ""), "tex": opt("--tex", 2048, int),
            "face_tex": opt("--face-tex", 1024, int), "samples": opt("--samples", 4, int),
            "ao_samples": opt("--ao-samples", 24, int), "report": opt("--report", "")}
    if args["cache"] and os.path.exists(args["cache"]):
        bpy.ops.wm.open_mainfile(filepath=args["cache"])
        rig = bpy.data.objects[RIG_NAME]
        objs = [o for o in bpy.data.objects if o.type == "MESH"]
        log("loaded cache", args["cache"])
    else:
        rig, objs = stage_model(args)
        if args["cache"]:
            bpy.ops.wm.save_as_mainfile(filepath=args["cache"], compress=True)
    table = [(n, p, Vector(h), Vector(t), d) for n, p, h, t, d in json.loads(rig["pip_table"])]
    srig = solver_rig(table)
    anims.RIG = srig  # the wall and ledge clips place their hands by measuring the pose

    def pose_at(p):
        D, offset = anims.solve(p, srig)
        return D, {"Hips": offset}
    clips = ce.key_clips(rig, anims.CLIPS, pose_at, anims.FPS)
    fbx = os.path.join(args["out"], "Pip.fbx")
    os.makedirs(args["out"], exist_ok=True)
    ce.export_fbx(rig, objs, fbx, anims.FPS)
    report = {
        "fbx": fbx, "textures": json.loads(rig["pip_textures"]),
        "texture_sizes": {"Pip_BaseColor": args["tex"], "Pip_Normal": args["tex"], "Pip_Face": args["face_tex"]},
        "triangles": {o.name: tri_count(o) for o in objs},
        "bones": [b.name for b in rig.data.bones], "clips": clips,
    }
    report["triangles"]["total"] = sum(report["triangles"].values())
    log(json.dumps(report, indent=1))
    if args["report"]:
        with open(args["report"], "w") as fh:
            json.dump(report, fh, indent=1)
    log("exported", fbx)


if __name__ == "__main__":
    main()
