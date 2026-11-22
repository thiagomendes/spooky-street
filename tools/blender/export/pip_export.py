"""Pip, game-ready: low-poly meshes, one baked atlas (+ a face map), a rig, twelve clips, one FBX.

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
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOLS))
for sub in ("style", "pip", "chars", "export"):
    sys.path.insert(0, os.path.join(TOOLS, sub))

import pip_anims as anims  # noqa: E402

DEFAULT_OUT = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models", "Characters", "Pip")
RIG_NAME = "Pip_Rig"
GROUPS = ("Body", "Hair", "Cape", "Pail")  # atlas meshes; the face is its own mesh with its own map

# Source part (regex on the builder's object name) -> group, subdivision level (None = as built), curve
# (bevel resolution, spline resolution), keep bevel modifiers, triangle target (None = no decimation).
PARTS = [
    (r"Pip_Head$", "Face", 0, None, True, None),
    (r"Pip_Sclera", "Face", 0, None, True, None),
    (r"Pip_Iris", "Face", None, None, True, None),
    (r"Pip_Pupil", "Face", None, None, True, 120),
    (r"Pip_Glint", "Face", None, None, True, None),
    (r"Pip_Lash", "Face", None, (1, 4), True, 220),
    (r"Pip_Brow", "Face", None, (1, 4), True, 110),
    (r"Pip_Smile", "Face", None, (1, 4), True, 260),
    (r"Pip_Nose", "Face", 0, None, True, 360),
    (r"Pip_Fang", "Face", 0, None, True, 96),
    (r"Pip_(Mouth|Tongue)", "Face", None, None, True, None),
    (r"Pip_Hair$", "Hair", 0, None, True, None),
    (r"Pip_HairGroove", "Hair", None, (0, 2), True, 96),
    (r"Pip_HairStrand", "Hair", None, (0, 1), True, 56),
    (r"Pip_BatClipFelt", "Hair", None, None, False, None),
    (r"Pip_Body$", "Body", None, None, True, None),
    (r"Pip_Shorts$", "Body", 1, None, True, 1000),
    (r"Pip_ShortsLeg", "Body", 1, None, True, 340),
    (r"Pip_ShortsHem", "Body", 0, None, True, 150),
    (r"Pip_Vest$", "Body", 1, None, True, 1700),
    (r"Pip_VestButtonDisc", "Body", 0, None, True, 56),
    (r"Pip_VestPiping", "Body", None, (0, 3), True, 110),
    (r"Pip_Brooch", "Body", 0, None, True, 90),
    (r"Pip_Jabot", "Body", 0, None, True, 330),
    (r"Pip_SleeveFold", "Body", 0, None, True, 100),
    (r"Pip_Cuff", "Body", 0, None, True, 190),
    (r"Pip_CapeCollar", "Body", 1, None, True, 640),
    (r"Pip_ClaspButton", "Body", 0, None, True, 72),
    (r"Pip_ClaspCord", "Body", None, (0, 3), True, 72),
    (r"Pip_Hand[LR]Finger", "Body", None, None, True, 190),
    (r"Pip_Hand[LR]Thumb", "Body", None, None, True, 210),
    (r"Pip_Hand[LR]Palm", "Body", 0, None, True, 240),
    (r"Pip_Hand[LR]Wrist", "Body", None, None, True, 96),
    (r"Pip_Hand[LR]Nail", "Body", 0, None, True, 32),
    (r"Pip_Shoe[LR]Upper", "Body", 0, None, True, 480),
    (r"Pip_Shoe[LR]ToeCap", "Body", 0, None, True, 220),
    (r"Pip_Shoe[LR]HeelCounter", "Body", 0, None, True, 180),
    (r"Pip_Shoe[LR]HeelTab", "Body", 0, None, True, 36),
    (r"Pip_Shoe[LR]Lace", "Body", None, (0, 2), True, 32),
    (r"Pip_Shoe[LR]Sole", "Body", None, None, True, 340),
    (r"Pip_Shoe[LR]Tongue", "Body", 0, None, True, 90),
    (r"Pip_Shoe[LR]Collar", "Body", 0, None, True, 200),
    (r"Pip_Cape$", "Cape", 1, None, True, 3200),
    (r"Pip_CapeStarFelt", "Cape", None, None, False, None),
    (r"Pip_CapeStitch", "Cape", None, None, True, 420),
    (r"Pip_Pail$", "Pail", 0, None, True, 1000),
    (r"Pip_PailRim", "Pail", 0, None, True, 180),
    (r"Pip_Pail(Eye|Grin)", "Pail", None, None, False, None),
    (r"Pip_PailHandle", "Pail", None, (0, 3), True, 140),
]


def log(*a):
    print("[pip_export]", *a, flush=True)


def smooth(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def activate(obj, others=()):
    bpy.ops.object.select_all(action="DESELECT")
    for o in others:
        o.select_set(True)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def tri_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


# --- 1. game meshes ----------------------------------------------------------------------------------------

def patch_materials():
    """Procedural textures read object space; point them at a stored `orco` attribute instead, so parts can be
    decimated, moved to world space and joined without the pattern sliding."""
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        attr = None
        for node in list(nodes):
            if node.type != "TEX_COORD":
                continue
            for link in list(node.outputs["Object"].links):
                if attr is None:
                    attr = nodes.new("ShaderNodeAttribute")
                    attr.attribute_type = "GEOMETRY"
                    attr.attribute_name = "orco"
                to = link.to_socket
                links.remove(link)
                links.new(attr.outputs["Vector"], to)


def realize(src, spec):
    """Game-resolution copy of one source part, in world space, remembering its object-space coordinates."""
    _pattern, group, subsurf, curve, bevel, target = spec
    for m in src.modifiers:
        if m.type == "SUBSURF" and subsurf is not None:
            m.levels = subsurf
        if m.type == "BEVEL" and not bevel:
            m.show_viewport = False
    if src.type == "CURVE" and curve is not None:
        src.data.bevel_resolution, src.data.resolution_u = curve
    deps = bpy.context.evaluated_depsgraph_get()
    deps.update()
    me = bpy.data.meshes.new_from_object(src.evaluated_get(deps))
    obj = bpy.data.objects.new("G_" + src.name, me)
    bpy.context.scene.collection.objects.link(obj)
    me.calc_loop_triangles()
    count = len(me.loop_triangles)
    if target is not None and count > target * 1.1:
        dec = obj.modifiers.new("Dec", "DECIMATE")
        dec.ratio = target / count
        deps = bpy.context.evaluated_depsgraph_get()
        deps.update()
        low = bpy.data.meshes.new_from_object(obj.evaluated_get(deps))
        obj.modifiers.clear()
        obj.data = low
        bpy.data.meshes.remove(me)
        me = low
    co = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get("co", co)
    me.attributes.new("orco", "FLOAT_VECTOR", "POINT").data.foreach_set("vector", co)
    world = src.matrix_world.copy()
    me.transform(world)
    if world.determinant() < 0:
        me.flip_normals()
    me.update()
    obj["src"] = src.name
    obj["grp"] = group
    return obj


def build_game_parts():
    import craft as craft_lib
    import pip_vampire as pv
    P = pv.build_pip(craft_lib.Craft(1.2))
    bpy.context.view_layer.update()
    patch_materials()
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
        spec = next((s for s in PARTS if re.match(s[0], src.name)), None)
        if spec is None:
            raise RuntimeError("no game spec for " + src.name)
        parts.append(realize(src, spec))
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


def build_armature(table):
    arm = bpy.data.armatures.new(RIG_NAME)
    rig = bpy.data.objects.new(RIG_NAME, arm)
    bpy.context.scene.collection.objects.link(rig)
    activate(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, parent, head, tail, _ in table:
        eb = arm.edit_bones.new(name)
        eb.head, eb.tail = head, tail
        if parent:
            eb.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return rig


# --- 3. skin weights ----------------------------------------------------------------------------------------

def set_weights(obj, weights):
    """weights: {bone: per-vertex array or scalar}."""
    n = len(obj.data.vertices)
    for name, w in weights.items():
        vg = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
        w = np.broadcast_to(np.asarray(w, dtype=float), (n,))
        for i in np.nonzero(w > 1e-4)[0]:
            vg.add([int(i)], float(w[i]), "REPLACE")


def body_auto_weights(body, rig, table):
    """Bone-heat weights for the one clean body skin, from the body bones only."""
    deform = {name for name, *_rest, d in table if d}
    for b in rig.data.bones:
        b.use_deform = b.name in deform
    activate(rig, [body])
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    for b in rig.data.bones:
        b.use_deform = True
    names = [g.name for g in body.vertex_groups]
    W = np.zeros((len(body.data.vertices), len(names)))
    for v in body.data.vertices:
        for g in v.groups:
            W[v.index, g.group] = g.weight
    if (W.sum(axis=1) < 1e-6).any():
        raise RuntimeError("bone heat left unweighted body vertices")
    return names, W / W.sum(axis=1, keepdims=True)


class SkinSource:
    """Nearest-surface lookup of the body's weights."""

    def __init__(self, body, names, W):
        me = body.data
        me.calc_loop_triangles()
        self.co = [v.co.copy() for v in me.vertices]
        self.tris = [tuple(t.vertices) for t in me.loop_triangles]
        self.tree = BVHTree.FromPolygons(self.co, self.tris)
        self.names, self.W = names, W

    def at(self, p):
        loc, _n, idx, _d = self.tree.find_nearest(p)
        a, b, c = self.tris[idx]
        pa, pb, pc = self.co[a], self.co[b], self.co[c]
        v0, v1, v2 = pb - pa, pc - pa, loc - pa
        d00, d01, d11, d20, d21 = v0.dot(v0), v0.dot(v1), v1.dot(v1), v2.dot(v0), v2.dot(v1)
        den = d00 * d11 - d01 * d01
        if abs(den) < 1e-14:
            return self.W[a]
        v = (d11 * d20 - d01 * d21) / den
        w = (d00 * d21 - d01 * d20) / den
        return self.W[a] * (1 - v - w) + self.W[b] * v + self.W[c] * w

    def transfer(self, obj, allow=None, fallback="Chest", rigid=False):
        """Copy weights to `obj`; `allow(bone, x)` filters bones; `rigid` gives the whole part its centre's weights."""
        pts = [v.co for v in obj.data.vertices]
        if rigid:
            centre = sum(pts, Vector()) / len(pts)
            pts = [centre] * len(pts)
        out = np.zeros((len(pts), len(self.names)))
        cache = None
        for i, p in enumerate(pts):
            if rigid and cache is not None:
                out[i] = cache
                continue
            w = self.at(p).copy()
            if allow is not None:
                for j, n in enumerate(self.names):
                    if not allow(n, p.x):
                        w[j] = 0.0
            if w.sum() < 1e-4:
                w[:] = 0.0
                w[self.names.index(fallback)] = 1.0
            out[i] = w / w.sum()
            cache = out[i]
        set_weights(obj, {n: out[:, j] for j, n in enumerate(self.names)})


def cape_weights(obj):
    n = len(obj.data.vertices)
    W = {"Chest": np.zeros(n)}
    for C in "LMR":
        for i in (1, 2, 3):
            W[f"Cape_{C}{i}"] = np.zeros(n)
    j = CAPE_JOINTS
    for v in obj.data.vertices:
        p = v.co
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


def hand_weights(obj, P, src):
    """Palm and wrist follow the hand; fingers and thumb blend onto their bones past the knuckle."""
    S = src[8]  # Pip_Hand<L|R>…
    k = P.head_r / 0.13 * 1.15
    n = len(obj.data.vertices)
    local = np.empty(n * 3, dtype=np.float32)
    obj.data.attributes["orco"].data.foreach_get("vector", local)
    local = local.reshape(n, 3)
    w = np.zeros(n)
    kind = src[9:]
    bone = "Fingers_" + S
    if kind.startswith("Finger"):
        fy, fz = [(-0.0235, -0.052), (-0.0078, -0.055), (0.0078, -0.054), (0.0235, -0.05)][int(kind[6])]
        k0 = np.array((0.002 * k, fy * k, fz * k))
        d = np.linalg.norm(local - k0, axis=1)
        w = np.array([smooth((di - 0.004 * k) / (0.012 * k)) if q[2] < k0[2] + 0.003 * k else 0.0
                      for di, q in zip(d, local)])
    elif kind.startswith("Thumb"):
        bone = "Thumb_" + S
        t1 = np.array((-0.005, -0.029, -0.03)) * k
        axis = np.array((-0.008, -0.02, -0.027))
        axis /= np.linalg.norm(axis)
        w = np.array([smooth(((q - t1) @ axis + 0.004 * k) / (0.012 * k)) for q in local])
    elif kind.startswith("Nail"):
        if kind.endswith(".004"):
            bone = "Thumb_" + S
        w = np.ones(n)
    set_weights(obj, {"Hand_" + S: 1 - w, bone: w})


def shoe_weights(obj, src):
    S = src[8]
    y = np.array([v.co.y for v in obj.data.vertices])
    w = np.array([smooth((-0.086 - yi) / 0.026) for yi in y])
    if "Collar" in src:
        w[:] = 0
    set_weights(obj, {"Foot_" + S: 1 - w, "Toe_" + S: w})


def weight_parts(parts, P, rig, table):
    body = next(o for o in parts if o["src"] == "Pip_Body")
    names, W = body_auto_weights(body, rig, table)
    skin = SkinSource(body, names, W)
    torso = {"Hips", "Spine", "Chest"}

    def side_only(prefixes):
        return lambda n, x: any(n.startswith(p) for p in prefixes) and (n[-1] == ("L" if x > 0 else "R"))
    for o in parts:
        src = o["src"]
        if o is body:
            continue
        if o["grp"] == "Face":
            set_weights(o, {"Head": 1.0})
        elif o["grp"] == "Hair":
            hair_weights(o, P)
        elif o["grp"] == "Pail":
            set_weights(o, {"Pail": 1.0})
        elif o["grp"] == "Cape":
            cape_weights(o)
        elif src.startswith("Pip_Hand"):
            hand_weights(o, P, src)
        elif src.startswith("Pip_Shoe"):
            shoe_weights(o, src)
        elif re.match(r"Pip_(CapeCollar|Clasp)", src):
            set_weights(o, {"Chest": 1.0})
        elif src.startswith("Pip_Cuff"):
            set_weights(o, {"LowerArm_" + ("L" if o.data.vertices[0].co.x > 0 else "R"): 1.0})
        elif src.startswith("Pip_SleeveFold"):
            skin.transfer(o, side_only(("UpperArm", "LowerArm")), fallback="Chest")
        elif re.match(r"Pip_(VestButton|Brooch)", src):
            skin.transfer(o, lambda n, x: n in torso, rigid=True)
        elif re.match(r"Pip_(Vest|Jabot)", src):
            skin.transfer(o, lambda n, x: n in torso or n.startswith("Shoulder"))
        elif re.match(r"Pip_Shorts(Leg|Hem)", src):
            skin.transfer(o, lambda n, x: n == "Hips" or n == "UpperLeg_" + ("L" if x > 0 else "R"),
                          fallback="Hips")
        elif src == "Pip_Shorts":
            skin.transfer(o, lambda n, x: n in ("Hips", "Spine") or n.startswith("UpperLeg"), fallback="Hips")
        else:
            raise RuntimeError("no weights for " + src)
    body.modifiers.clear()
    body.parent = None


def limit_weights(obj, limit=4):
    for v in obj.data.vertices:
        gs = sorted(v.groups, key=lambda g: -g.weight)
        for g in gs[limit:]:
            obj.vertex_groups[g.group].remove([v.index])
        total = sum(g.weight for g in gs[:limit])
        if total <= 0:
            raise RuntimeError(f"{obj.name}: unweighted vertex {v.index}")
        for g in gs[:limit]:
            g.weight = g.weight / total


# --- 4. join, hidden faces, UVs ------------------------------------------------------------------------------

def join(parts, name):
    for o in parts:
        if "grp" not in o.data.attributes:
            o.data.attributes.new("grp", "INT", "FACE").data.foreach_set(
                "value", [GROUPS.index(o["grp"]) if o["grp"] in GROUPS else 0] * len(o.data.polygons))
    activate(parts[0], parts[1:])
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.data.name = name
    return obj


def remove_hidden(objs, occluders, rings=2):
    """Delete faces that no ray can reach from outside (sampled over the hemisphere), keeping a safety margin of
    `rings` faces around everything visible so joints can flex without opening holes."""
    verts, tris = [], []
    for o in occluders:
        me = o.data
        me.calc_loop_triangles()
        off = len(verts)
        verts += [v.co.copy() for v in me.vertices]
        tris += [tuple(i + off for i in t.vertices) for t in me.loop_triangles]
    tree = BVHTree.FromPolygons(verts, tris)
    dirs = []
    for i in range(72):  # Fibonacci sphere
        zz = 1 - 2 * (i + 0.5) / 72
        rr = math.sqrt(1 - zz * zz)
        a = i * math.pi * (3 - math.sqrt(5))
        dirs.append(Vector((rr * math.cos(a), rr * math.sin(a), zz)))
    removed = 0
    for o in objs:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.normal_update()
        hidden = set()
        for f in bm.faces:
            n = f.normal
            c = f.calc_center_median()
            seen = False
            for p in [c] + [c.lerp(v.co, 0.6) for v in f.verts]:
                start = p + n * 4e-4
                for d in dirs:
                    if d.dot(n) > 0.06 and tree.ray_cast(start, d)[0] is None:
                        seen = True
                        break
                if seen:
                    break
            if not seen:
                hidden.add(f)
        for _ in range(rings):
            hidden = {f for f in hidden if all(g in hidden for v in f.verts for g in v.link_faces)}
        removed += len(hidden)
        bmesh.ops.delete(bm, geom=list(hidden), context="FACES")
        bm.to_mesh(o.data)
        bm.free()
        o.data.update()
    return removed


def unwrap(obj, margin):
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=margin, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def split_groups(atlas):
    out = {}
    for gi, g in enumerate(GROUPS):
        o = atlas.copy()
        o.data = atlas.data.copy()
        bpy.context.scene.collection.objects.link(o)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        layer = bm.faces.layers.int["grp"]
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[layer] != gi], context="FACES")
        bm.to_mesh(o.data)
        bm.free()
        o.name = o.data.name = "Pip_" + g
        used = {g_.group for v in o.data.vertices for g_ in v.groups}
        for vg in [vg for vg in o.vertex_groups if vg.index not in used]:
            o.vertex_groups.remove(vg)
        out[g] = o
    bpy.data.objects.remove(atlas, do_unlink=True)
    return out


# --- 5. bake -------------------------------------------------------------------------------------------------

def bake_setup(samples):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = False
    scene.render.bake.use_selected_to_active = False
    scene.render.bake.margin_type = "EXTEND"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Bake")
    scene.world.light_settings.distance = 0.07


def bake(objs, image, kind, visible, margin, clear=True):
    """Bake `kind` of each object into `image`; only `visible[obj]` objects occlude (AO)."""
    scene = bpy.context.scene
    everything = [o for o in scene.objects if o.type == "MESH"]
    for i, obj in enumerate(objs):
        for mat in obj.data.materials:
            nodes = mat.node_tree.nodes
            node = nodes.get("BakeTarget") or nodes.new("ShaderNodeTexImage")
            node.name = "BakeTarget"
            node.image = image
            nodes.active = node
        for o in everything:
            o.hide_render = o not in visible.get(obj, everything)
        activate(obj)
        args = dict(margin=margin, use_clear=clear and i == 0, margin_type="EXTEND")
        if kind == "COLOR":
            bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, **args)
        elif kind == "NORMAL":
            bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", **args)
        else:
            bpy.ops.object.bake(type="AO", **args)
    for o in everything:
        o.hide_render = False


def pixels(image):
    px = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
    image.pixels.foreach_get(px)
    return px.reshape(image.size[1], image.size[0], 4)


def save_png(image, px, path):
    image.pixels.foreach_set(px.ravel())
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    return path


def bake_all(meshes, face, out, tex, face_tex, samples, ao_samples, ao_strength=0.4):
    bake_setup(samples)
    atlas_objs = [meshes[g] for g in GROUPS]
    solid = [meshes["Body"], meshes["Hair"], face]
    # AO occluders: the cape and pail move far from the body, so they shade only themselves
    visible = {meshes["Body"]: solid, meshes["Hair"]: solid, face: solid, meshes["Cape"]: [meshes["Cape"]],
               meshes["Pail"]: [meshes["Pail"]]}
    results = {}
    for label, objs, size in (("atlas", atlas_objs, tex), ("face", [face], face_tex)):
        margin = max(2, size // 512)
        colour = bpy.data.images.new(f"Pip_{label}_colour", size, size, alpha=False)
        log("bake colour", label, size)
        bake(objs, colour, "COLOR", {}, margin)
        rgb = pixels(colour).copy()
        ao = bpy.data.images.new(f"Pip_{label}_ao", size, size, alpha=False, is_data=True)
        bpy.context.scene.cycles.samples = ao_samples
        log("bake ao", label)
        bake(objs, ao, "AO", visible, margin)
        bpy.context.scene.cycles.samples = samples
        shade = pixels(ao)[:, :, :3]
        # sRGB-encoded bake: darken creases a little, tinted toward the night purple rather than grey
        tint = np.array((0.80, 0.74, 0.92), dtype=np.float32)
        k = ao_strength * (1.0 - np.clip(shade, 0.0, 1.0))
        rgb[:, :, :3] *= 1.0 - k * (1.0 - tint * 0.55)
        rgb[:, :, 3] = 1.0
        bpy.data.images.remove(ao)
        results[label] = [colour, rgb]
    normal = bpy.data.images.new("Pip_atlas_normal", tex, tex, alpha=False, is_data=True)
    log("bake normal")
    bake(atlas_objs, normal, "NORMAL", {}, max(2, tex // 512))
    npx = pixels(normal).copy()
    npx[:, :, 3] = 1.0
    os.makedirs(out, exist_ok=True)
    paths = {
        "base": save_png(results["atlas"][0], results["atlas"][1], os.path.join(out, "Pip_BaseColor.png")),
        "face": save_png(results["face"][0], results["face"][1], os.path.join(out, "Pip_Face.png")),
        "normal": save_png(normal, npx, os.path.join(out, "Pip_Normal.png")),
    }
    # final materials: one per map (Unity ignores them, but they name the sub-meshes and make the FBX readable)
    def final(name, colour_img, normal_img):
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        bsdf = nodes["Principled BSDF"]
        t = nodes.new("ShaderNodeTexImage")
        t.image = colour_img
        links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.85
        if normal_img is not None:
            tn = nodes.new("ShaderNodeTexImage")
            tn.image = normal_img
            nm = nodes.new("ShaderNodeNormalMap")
            links.new(tn.outputs["Color"], nm.inputs["Color"])
            links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
        return mat
    atlas_mat = final("Pip_Atlas", results["atlas"][0], normal)
    face_mat = final("Pip_Face", results["face"][0], None)
    for obj, mat in [(o, atlas_mat) for o in atlas_objs] + [(face, face_mat)]:
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        for p in obj.data.polygons:
            p.material_index = 0
    for mat in [m for m in bpy.data.materials if m.users == 0]:
        bpy.data.materials.remove(mat)
    return paths


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


def key_clips(rig_obj, table):
    srig = solver_rig(table)
    rest = {b.name: b.matrix_local.to_3x3() for b in rig_obj.data.bones}
    for act in list(bpy.data.actions):
        bpy.data.actions.remove(act)
    rig_obj.animation_data_create()
    report = []
    for name, fn, frames, loop, speed, note in anims.CLIPS:
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        quats = {b: [] for b in rest}
        locs = []
        for f in range(frames + 1):
            u = (f % frames) / frames if loop else f / frames
            D, offset = anims.solve(fn(u), srig)
            for b, B in rest.items():
                q = (B.inverted() @ D.get(b, Matrix.Identity(3)) @ B).to_quaternion()
                if quats[b] and quats[b][-1].dot(q) < 0:
                    q.negate()
                quats[b].append(q)
            locs.append(rest["Hips"].inverted() @ offset)

        def curve(path, index, group, values):
            fc = act.fcurves.new(path, index=index, action_group=group)
            fc.keyframe_points.add(len(values))
            fc.keyframe_points.foreach_set("co", [x for f, v in enumerate(values) for x in (float(f), v)])
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
            fc.update()
        for b in rest:
            for i in range(4):
                curve(f'pose.bones["{b}"].rotation_quaternion', i, b, [q[i] for q in quats[b]])
        for i in range(3):
            curve('pose.bones["Hips"].location', i, "Hips", [v[i] for v in locs])
        report.append({"name": name, "frames": frames, "fps": anims.FPS, "seconds": round(frames / anims.FPS, 3),
                       "loop": loop, "ground_speed_mps": speed, "note": note})
    return report


def export_fbx(rig_obj, meshes, path):
    rig_obj.animation_data.action = None
    for pb in rig_obj.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.scene.render.fps = anims.FPS
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    activate(rig_obj, meshes)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
        axis_forward="-Z", axis_up="Y", bake_space_transform=False,
        use_mesh_modifiers=True, mesh_smooth_type="OFF", use_tspace=False,
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False,
        armature_nodetype="NULL",
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0, path_mode="STRIP", embed_textures=False)


# --- main ----------------------------------------------------------------------------------------------------

def stage_model(args):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    P, parts, info = build_game_parts()
    log("parts", len(parts), "triangles before culling", sum(tri_count(o) for o in parts))
    table = bone_table(P, info)
    rig = build_armature(table)
    weight_parts(parts, P, rig, table)
    lists = {g: [o for o in parts if o["grp"] == g] for g in GROUPS + ("Face",)}
    face = join(lists["Face"], "Pip_Face")
    by_group = {g: join(lists[g], "Tmp_" + g) for g in GROUPS}
    solid = [face, by_group["Hair"], by_group["Body"]]
    log("hidden faces removed", remove_hidden(solid, solid))
    atlas = join([by_group[g] for g in GROUPS], "Tmp_Atlas")
    unwrap(atlas, 0.0035)
    unwrap(face, 0.006)
    meshes = split_groups(atlas)
    paths = bake_all(meshes, face, args["out"], args["tex"], args["face_tex"], args["samples"], args["ao_samples"])
    objs = [meshes[g] for g in GROUPS] + [face]
    for o in objs:
        limit_weights(o)
        for name in ("orco", "grp"):
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
    clips = key_clips(rig, table)
    fbx = os.path.join(args["out"], "Pip.fbx")
    os.makedirs(args["out"], exist_ok=True)
    export_fbx(rig, objs, fbx)
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
