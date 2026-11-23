"""Bea and Mr. Vale, game-ready (spec §2, §3): the two witnesses of the first case, through Pip's pipeline.

    blender -b -t 2 --python tools/blender/export/witness_export.py -- <Bea|Vale> [--out DIR] [--cache FILE.blend]
                                    [--tex 2048] [--face-tex 1024] [--report FILE.json] [--count]

Builds the approved character with tools/blender/chars/cast.py and runs the shared steps of char_export.py:
game-resolution parts, hidden-face removal, one baked atlas plus a face map on `<Name>_Face`, Pip's skeleton
(same bone names and axes) plus a few costume bones, the clips of witness_anims.py, one FBX.

Outputs in --out (default Assets/SpookyStreet/Content/Resources/Models/Characters/<Name>): <Name>.fbx,
<Name>_BaseColor.png, <Name>_Normal.png, <Name>_Face.png. `--cache` saves the scene before the clips are keyed
and starts from it when it exists, so animation edits re-export in seconds. `--count` only prints triangle counts.

Costume bones (none deform the body skin):
  Bea   Pumpkin (child of Chest: the whole pumpkin, leaf collar and stitched face), Ponytail_1, Ponytail_2;
        Index_L / _R (child of Hand: the index finger alone) and FingerTips_L / _R (child of Fingers: the
        second joint of the other three), so she can point with one finger; see split_hand
  Vale  Cauldron (child of Hand_R, at the grip, like Pip's Pail), Bandage_Head / _ArmL / _ArmR / _Belly / _Knee;
        Watch (child of LowerArm_L: his wristwatch, a small mesh of its own; see add_watch)
Bea's bicycle is a separate static prop: see bike_export.py.
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
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOLS))
for sub in ("style", "pip", "chars", "export"):
    sys.path.insert(0, os.path.join(TOOLS, sub))

import char_export as ce  # noqa: E402
import witness_anims as anims  # noqa: E402
from char_export import SkinSource, set_weights, smooth, tri_count  # noqa: E402

CHARACTERS = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models", "Characters")


def common_parts(n):
    """Face, hands, cuffs and sneakers: the same budgets as Pip's."""
    return [
        (rf"{n}_Head$", "Face", 0, None, True, None),
        (rf"{n}_Sclera", "Face", 0, None, True, 400),
        (rf"{n}_Iris", "Face", None, None, True, 180),
        (rf"{n}_Pupil", "Face", None, None, True, 120),
        (rf"{n}_Glint", "Face", None, None, True, None),
        (rf"{n}_Lash", "Face", None, (1, 4), True, 160),
        (rf"{n}_Brow", "Face", None, (1, 4), True, 110),
        (rf"{n}_Smile", "Face", None, (1, 4), True, 260),
        (rf"{n}_Nose", "Face", 0, None, True, 360),
        (rf"{n}_(Mouth|Tongue|Teeth)", "Face", None, None, True, None),
        (rf"{n}_Freckle", "Face", None, None, True, 28),
        (rf"{n}_Ear", "Face", 0, None, True, 150),
        (rf"{n}_Cuff", "Body", 0, None, True, 150),
        (rf"{n}_Hand[LR]Finger", "Body", None, None, True, 140),
        (rf"{n}_Hand[LR]Thumb", "Body", None, None, True, 210),
        (rf"{n}_Hand[LR]Palm", "Body", 0, None, True, 240),
        (rf"{n}_Hand[LR]Wrist", "Body", None, None, True, 96),
        (rf"{n}_Hand[LR]Nail", "Body", 0, None, True, 32),
        (rf"{n}_Shoe[LR]Upper", "Body", 0, None, True, 400),
        (rf"{n}_Shoe[LR]ToeCap", "Body", 0, None, True, 220),
        (rf"{n}_Shoe[LR]HeelCounter", "Body", 0, None, True, 180),
        (rf"{n}_Shoe[LR]HeelTab", "Body", 0, None, True, 36),
        (rf"{n}_Shoe[LR]Lace", "Body", None, (0, 2), True, 24),
        (rf"{n}_Shoe[LR]Sole", "Body", None, None, True, 340),
        (rf"{n}_Shoe[LR]Tongue", "Body", 0, None, True, 90),
        (rf"{n}_Shoe[LR]Collar", "Body", 0, None, True, 200),
    ]


def centre_of(obj):
    pts = [obj.matrix_world @ v.co for v in obj.data.vertices] if obj.type == "MESH" else \
        [obj.matrix_world @ p.co for s in obj.data.splines for p in s.bezier_points]
    return sum(pts, Vector()) / len(pts)


# --- Bea ---------------------------------------------------------------------------------------------------

class Bea:
    name = "Bea"
    groups = ("Body", "Hair", "Pumpkin")
    hand_size = 1.15
    solid = ("Hair", "Body", "Pumpkin")
    rings = {"Bea_Face": 1, "Tmp_Hair": 1, "Tmp_Body": 2, "Tmp_Pumpkin": 1}
    loose = ()  # groups that only shade themselves in the AO bake
    detail = re.compile(r"Bea_(PumpkinSeam|Jack|LeafFelt|HelmetSeam|HelmetEye|HelmetLeaf|HelmetStem|HairTie|"
                        r"Shoe.Lace|Lash|Brow|Smile|Glint|Freckle|Cuff)")
    parts = common_parts("Bea") + [
        (r"Bea_Body$", "Body", None, None, True, 4300),
        (r"Bea_PumpkinSeam", "Pumpkin", None, (0, 1), True, 64),
        (r"Bea_Pumpkin$", "Pumpkin", 1, None, True, ("sheet", 1800)),
        (r"Bea_JackEyeStitch", "Pumpkin", None, None, True, 110),
        (r"Bea_JackGrinStitch", "Pumpkin", None, None, True, 300),
        (r"Bea_Jack(Eye|Nose|Grin)", "Pumpkin", None, None, True, None),
        (r"Bea_LeafFelt", "Pumpkin", None, None, False, 44),
        (r"Bea_Hair$", "Hair", 0, None, True, None),
        (r"Bea_Ponytail", "Hair", 0, None, True, 220),
        (r"Bea_HairTie", "Hair", 0, None, True, 96),
        (r"Bea_HelmetSeam", "Hair", None, (0, 1), True, 56),
        (r"Bea_HelmetEye", "Hair", None, None, True, None),
        (r"Bea_HelmetStem", "Hair", None, (1, 3), True, 150),
        (r"Bea_HelmetLeafFelt", "Hair", None, None, False, 44),
        (r"Bea_Helmet$", "Hair", 0, None, True, None),
    ]
    clips = anims.BEA_CLIPS

    @staticmethod
    def build(craft):
        import cast
        return cast.build_bea(craft)

    @staticmethod
    def skull(d):
        """(always under the hair: delete, behind the ears: not part of the face island)"""
        return d.y > 0.42 and d.z > -0.45, False

    lift = ((r"Bea_Mouth", 0.0006), (r"Bea_Tongue", 0.0008), (r"Bea_Teeth", 0.001), (r"Bea_Freckle", 0.0006),
            (r"Bea_Smile", 0.0004))

    @staticmethod
    def prepare(P, sources):
        pass

    @staticmethod
    def probe(P, info, sources):
        tail = sorted((centre_of(o) for o in sources if o.name.startswith("Bea_Ponytail")), key=lambda c: -c.z)
        info["tail"] = tail

    @staticmethod
    def bones(P, info):
        t = info["tail"]
        top = Vector((0, 0, P.z["shoulder"]))
        return [("Pumpkin", "Chest", top, top + Vector((0, 0, 0.12)), False),
                ("Ponytail_1", "Head", t[1].copy(), t[3].copy(), False),
                ("Ponytail_2", "Ponytail_1", t[3].copy(), t[4] + (t[4] - t[3]) * 0.6, False)]

    @staticmethod
    def region(bone):
        return "head" if bone.startswith("Ponytail") else ce.region_of(bone)

    @staticmethod
    def weigh(o, src, side, ctx):
        if o["grp"] == "Pumpkin":
            set_weights(o, {"Pumpkin": 1.0})
        elif src.startswith("Bea_Ponytail"):
            t = ctx["info"]["tail"]
            z = np.array([v.co.z for v in o.data.vertices])
            w1 = np.array([smooth((t[1].z + 0.02 - zi) / 0.06) for zi in z])
            w2 = np.array([smooth((t[3].z + 0.03 - zi) / 0.06) for zi in z])
            set_weights(o, {"Head": 1 - w1, "Ponytail_1": w1 * (1 - w2), "Ponytail_2": w1 * w2})
        elif o["grp"] == "Hair":
            set_weights(o, {"Head": 1.0})
        else:
            return False
        return True

    @staticmethod
    def meta(P, info):
        return {}

    @staticmethod
    def refit(rig, objs, table):
        return split_hand(rig, objs, table)


def split_hand(rig, objs, table):
    """Bea's pointing hand. The shared skeleton moves all four fingers with one bone (`Fingers_<S>`), which can
    only make a paddle or a flat fold. Two more bones per hand, weights only (no vertex, UV or texture changes):
      Index_<S>       child of Hand, hinged at the index finger's own knuckle: takes that finger's share of
                      Fingers, so she can point with one finger while Fingers curls the other three
      FingerTips_<S>  child of Fingers, at the middle joint of those three: with Fingers it closes a fist
    Runs on the staged rig (fresh or from --cache) and does nothing if the bones are already there. Existing
    clips look the same: Index follows Fingers and FingerTips stays straight unless a pose says otherwise
    (witness_anims `index<S>`, `tips<S>`). Returns the extended bone table."""
    if "Index_L" in rig.data.bones:
        return table
    at = {b: (Vector(h), Vector(t)) for b, _p, h, t, _d in table}
    added = []
    for S, s in (("L", 1.0), ("R", -1.0)):
        head, tail = at["Fingers_" + S]
        along = (tail - head).normalized()
        k = (tail - head).length / 0.045  # the hand's scale: the bone is 0.045 long in hand units (human_bones)
        _back, across, _along = anims.hand_axes(head, along, at["Thumb_" + S][0], s)
        knuckle = head - across * (0.0235 * k)  # on the Fingers hinge line, so old clips keep their exact shape
        joint = head + along * (0.022 * k)
        added += [("Index_" + S, "Hand_" + S, knuckle, knuckle + along * (0.045 * k), False),
                  ("FingerTips_" + S, "Fingers_" + S, joint, tail.copy(), False)]
        moved = 0
        for o in objs:
            src = o.vertex_groups.get("Fingers_" + S)
            if src is None:
                continue
            index = o.vertex_groups.new(name="Index_" + S)
            tips = o.vertex_groups.new(name="FingerTips_" + S)
            M = o.matrix_world
            fingers = {1: [], 2: [], 3: []}  # middle, ring, little: (distance past the knuckle line, vertex, weight)
            for v in o.data.vertices:
                g = next((g for g in v.groups if g.group == src.index), None)
                if g is None:
                    continue
                r = (M @ v.co) - head
                y = r.dot(across) / k
                if y < -0.0157:  # the index finger: halfway to the middle finger's axis
                    index.add([v.index], g.weight, "REPLACE")
                    src.remove([v.index])
                    moved += 1
                else:
                    fingers[1 if y < 0.0 else 2 if y < 0.0157 else 3].append((r.dot(along) / k, v.index, g.weight))
            for verts in fingers.values():
                # The game fingers are a few uneven rings of triangles. A blended joint would shear them into
                # shards, so each finger hinges in the widest gap between its rings around the middle joint
                # and everything past the gap follows FingerTips rigidly.
                reach = sorted(t for t, _i, _w in verts if 0.012 < t < 0.034)
                gap, cut = max((b - a, (a + b) / 2) for a, b in zip(reach, reach[1:]))
                for t, i, weight in verts:
                    if t > cut:
                        tips.add([i], weight, "REPLACE")
                        src.remove([i])
        if not moved:
            raise RuntimeError("no index finger found for side " + S)
    ce.activate(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, parent, head, tail, _d in added:
        eb = rig.data.edit_bones.new(name)
        eb.head, eb.tail = head, tail
        eb.parent = rig.data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    for name, *_rest in added:
        rig.pose.bones[name].rotation_mode = "QUATERNION"
    return table + added


# --- Mr. Vale ----------------------------------------------------------------------------------------------

BAND_BONES = {"Head": "Head", "ArmL": "LowerArm_L", "ArmR": "UpperArm_R", "Belly": "Hips", "Knee": "UpperLeg_L"}


class Vale:
    name = "Vale"
    groups = ("Body", "Wraps", "Hair", "Cauldron")
    hand_size = 1.0
    solid = ("Hair", "Body", "Wraps")
    rings = {"Vale_Face": 1, "Tmp_Hair": 1, "Tmp_Body": 2, "Tmp_Wraps": 1}
    loose = ("Cauldron",)
    detail = re.compile(r"Vale_(LooseEnd|Moustache|HeadTurn|EyeTurn|Shoe.Lace|Lash|Brow|Smile|Glint|Cuff|Ear)")
    parts = common_parts("Vale") + [
        (r"Vale_Body$", "Body", None, None, True, 4300),
        (r"Vale_Collar", "Body", 0, None, True, ("sheet", 96)),
        (r"Vale_MoustacheTip", "Hair", 0, None, True, 90),
        (r"Vale_Moustache", "Hair", 0, None, True, 180),
        (r"Vale_(WrapSash|Wrap)", "Wraps", 0, None, True, ("sheet", 112)),
        (r"Vale_ArmWrap", "Wraps", 0, None, True, ("sheet", 48)),
        (r"Vale_LegWrap", "Wraps", 0, None, True, ("sheet", 56)),
        (r"Vale_LooseEnd", "Wraps", None, None, False, None),
        (r"Vale_(HeadTurn|EyeTurn)", "Hair", 0, None, True, ("sheet", 96)),
        (r"Vale_Hair$", "Hair", 0, None, True, None),
        (r"Vale_HeadWrap", "Hair", 0, None, True, None),
        (r"Vale_CauldronRim", "Cauldron", 0, None, True, 220),
        (r"Vale_CauldronHandle", "Cauldron", None, (0, 3), True, 140),
        (r"Vale_Cauldron$", "Cauldron", 0, None, True, 700),
        (r"Vale_Sweet", "Cauldron", 0, None, True, 64),
    ]
    clips = anims.VALE_CLIPS

    @staticmethod
    def build(craft):
        import cast
        return cast.build_vale(craft)

    @staticmethod
    def skull(d):
        return d.y > 0.42 and d.z > -0.2, d.y > 0.42

    lift = ((r"Vale_Smile", 0.0004),)

    @staticmethod
    def prepare(P, sources):
        """Fit the bandage turns to the body: the builder lathes them as flat tilted ellipses, which stand off
        the back and shoulders by centimetres (and would hang there like hoops when he sits) and cut through
        each other wherever two cross. Every turn is laid at a fixed height over the surface it wraps (its 5 mm
        thickness grows inward), and turns that cross lie in different layers, 6 mm apart."""
        body = next(o for o in sources if o.name == "Vale_Body")
        me = body.data
        verts = [v.co.copy() for v in me.vertices]
        z = P.z

        def arm(c, s):
            return c.x * s > P.shoulder_x - 0.04 and (c.z > z["hip"] + 0.04 or c.x * s > P.hip_x + P.limb * 1.1 + 0.05)

        def tree(keep):
            return BVHTree.FromPolygons(verts, [tuple(p.vertices) for p in me.polygons if keep(p.center)])
        trees = {"trunk": tree(lambda c: not arm(c, 1) and not arm(c, -1))}
        for S, s in (("L", 1), ("R", -1)):
            trees["arm" + S] = tree(lambda c, s=s: arm(c, s))
            trees["leg" + S] = tree(lambda c, s=s: c.x * s > 0 and c.z < z["hip"] + 0.03 and not arm(c, s))
        for o in sources:
            m = re.match(r"Vale_(ArmWrap|LegWrap|WrapSash|Wrap)(?:\.(\d+))?$", o.name)
            if not m:
                continue
            index = int(m.group(2) or 0)
            layer = {"WrapSash": 3, "Wrap": index % 3}.get(m.group(1), (index % 7) % 2)
            near = 0.007 + 0.006 * layer
            far = near + 0.002
            M = o.matrix_world.copy()
            Mi = M.inverted()
            side = "L" if (M @ Vector((0, 0, 0))).x > 0 else "R"
            t = trees[{"ArmWrap": "arm" + side, "LegWrap": "leg" + side}.get(m.group(1), "trunk")]
            for v in o.data.vertices:
                p = M @ v.co
                q, n, _i, d = t.find_nearest(p)
                out = p - q
                if out.dot(n) < 0 or d < 1e-6:
                    p = q + n * near
                elif d > far:
                    p = q + out * (far / d)
                elif d < near:
                    p = q + out * (near / d)
                v.co = Mi @ p
            o.data.update()

    @staticmethod
    def probe(P, info, sources):
        rim = next(o for o in sources if o.name == "Vale_CauldronRim")
        info["grip"] = centre_of(rim) + Vector((0, 0, 0.145))
        ends = [o.matrix_world.translation.copy() for o in sources if o.name.startswith("Vale_LooseEnd")]
        hc = P.head_c
        # the builder's order: left wrist, right elbow, belly, left knee, head
        named = {}
        for p in ends:
            if p.z > hc.z - P.head_r:
                named["Head"] = p
            elif p.z < P.z["knee"] + 0.1:
                named["Knee"] = p
            elif abs(p.x) < P.torso * 0.8:
                named["Belly"] = p
            elif p.x > 0:
                named["ArmL"] = p
            else:
                named["ArmR"] = p
        if len(named) != 5:
            raise RuntimeError(f"expected five loose bandage ends, found {sorted(named)}")
        info["ends"] = named

    @staticmethod
    def bones(P, info):
        g = info["grip"]
        out = [("Cauldron", "Hand_R", g.copy(), g + Vector((0, 0, -0.15)), False)]
        for b, parent in BAND_BONES.items():
            p = info["ends"][b]
            out.append(("Bandage_" + b, parent, p.copy(), p + Vector((0, 0, -0.1)), False))
        return out

    @staticmethod
    def region(bone):
        m = re.match(r"Bandage_(\w+)$", bone)
        if m:
            return {"Head": "head", "ArmL": "armL", "ArmR": "armR", "Belly": "core", "Knee": "legL"}[m.group(1)]
        return ce.region_of(bone)

    @staticmethod
    def weigh(o, src, side, ctx):
        if o["grp"] == "Cauldron":
            set_weights(o, {"Cauldron": 1.0})
        elif o["grp"] == "Hair":
            set_weights(o, {"Head": 1.0})
        elif src.startswith("Vale_LooseEnd"):
            at = sum((v.co for v in o.data.vertices), Vector()) / len(o.data.vertices)
            b = min(ctx["info"]["ends"], key=lambda k: (ctx["info"]["ends"][k] - at).length)
            set_weights(o, {"Bandage_" + b: 1.0})
        elif src.startswith("Vale_Collar"):
            ctx["on_neck"].transfer(o)
        elif src.startswith("Vale_ArmWrap"):
            ctx["on_arm"][side].transfer(o, fallback="UpperArm_" + side)
        elif src.startswith("Vale_LegWrap"):
            ctx["on_leg"][side].transfer(o, fallback="UpperLeg_" + side)
        elif src.startswith("Vale_Wrap"):
            ctx["on_trunk"].transfer(o, fallback="Spine")
        else:
            return False
        return True

    @staticmethod
    def meta(P, info):
        return {"thigh_r": P.limb * 0.98 + 0.012}

    @staticmethod
    def refit(rig, objs, table):
        return add_watch(rig, objs, table)


WATCH, WATCH_BONE = "Vale_Watch", "Watch"
WATCH_AT = 0.80  # along the left forearm, elbow to wrist: just above the cuff
WATCH_BAND, WATCH_CASE, WATCH_DIAL = (0.13, 0.10, 0.24), (0.13, 0.10, 0.24), (0.95, 0.91, 0.81)


def flat_texel(colour, normal, want, block=16):
    """UV of the middle of the `block`-pixel square of the baked atlas that is one flat colour nearest to `want`
    and whose normal map is flat too, so a small new part can be coloured without touching the textures.
    `colour`, `normal`: (h, w, 3) arrays, rows bottom-up as Blender stores them."""
    h, w = colour.shape[:2]
    hb, wb = h // block, w // block

    def blocks(px):
        b = px[:hb * block, :wb * block].reshape(hb, block, wb, block, 3)
        return b.mean(axis=(1, 3)), b.std(axis=(1, 3)).max(axis=2)
    mean, std = blocks(colour)
    n_mean, n_std = blocks(normal)
    off = np.abs(n_mean - np.array([0.5, 0.5, 1.0])).max(axis=2)
    ok = (std < 0.006) & (n_std < 0.006) & (off < 0.02)
    if not ok.any():
        raise RuntimeError("the atlas has no flat block to colour the watch from")
    miss = np.where(ok, np.abs(mean - np.array(want)).sum(axis=2), 1e9)
    # the first of the best blocks in reading order: the choice must not depend on bake noise
    best = np.argwhere(miss <= miss.min() + 0.01)[0]
    y, x = int(best[0]), int(best[1])
    return ((x + 0.5) * block / w, (y + 0.5) * block / h), tuple(float(c) for c in mean[y, x]), block / w


def add_watch(rig, objs, table):
    """Mr. Vale's wristwatch (S003a): his testimony is about time and his gesture is checking it, so there has
    to be something on that wrist. A dark strap and case with a pale dial and two hands on the outside of his
    left forearm, over the wraps, rigid on its own bone `Watch` (child of LowerArm_L, turned about the forearm
    with the hand). It is its own small mesh (`Vale_Watch`): the other
    meshes, their UVs and the textures are not touched. Its UVs sit inside flat squares of the atlas that
    already have the right colours (the dark of his trousers, the pale of the bandages), found by
    `flat_texel` in the committed textures. Runs on the staged rig (fresh or from --cache) and does nothing if
    the watch is already there. Returns the bone table with the new bone."""
    if WATCH in bpy.data.objects:
        return table
    at = {b: (Vector(h), Vector(t)) for b, _p, h, t, _d in table}
    elbow, wrist = at["LowerArm_L"][0], at["Hand_L"][0]
    a = (wrist - elbow).normalized()  # along the forearm
    b = Vector((1.0, 0.0, 0.0))
    b = (b - a * b.dot(a)).normalized()  # out of the back of the wrist (the arm hangs palm to the thigh)
    c = a.cross(b)
    centre = elbow + (wrist - elbow) * WATCH_AT
    # how thick the wrapped arm is there: the strap goes around everything
    radii = []
    for o in objs:
        arm = o.vertex_groups.get("LowerArm_L")  # the skin and the wraps of that forearm, not the loose end
        if arm is None:
            continue
        M = o.matrix_world
        for v in o.data.vertices:
            if not any(g.group == arm.index and g.weight > 0.5 for g in v.groups):
                continue
            r = (M @ v.co) - centre
            if abs(r.dot(a)) < 0.014:
                radii.append((r - a * r.dot(a)).length)
    if len(radii) < 8:
        raise RuntimeError("no arm found under the watch")
    r_in = float(np.percentile(radii, 90)) + 0.002
    r_out = r_in + 0.0035
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    folder = os.path.join(CHARACTERS, "Vale")
    maps = []
    for suffix in ("BaseColor", "Normal"):
        img = bpy.data.images.load(os.path.join(folder, f"Vale_{suffix}.png"))
        maps.append(np.array(img.pixels[:], dtype=np.float32).reshape(img.size[1], img.size[0], 4)[:, :, :3])
        bpy.data.images.remove(img)
    texels = {k: flat_texel(maps[0], maps[1], want)
              for k, want in (("band", WATCH_BAND), ("case", WATCH_CASE), ("dial", WATCH_DIAL))}

    parts = []  # per face, in creation order

    def at_(u, v, w):
        return centre + a * u + b * v + c * w

    def face(points, part):
        (cu, cv), _colour, size = texels[part]
        f = bm.faces.new([bm.verts.new(p) for p in points])
        parts.append(part)
        f.smooth = True
        for loop in f.loops:
            # a small real footprint inside the flat square: zero-area UVs would give no tangents
            r = loop.vert.co - centre
            loop[uv].uv = (cu + size * 0.2 * max(-1.0, min(1.0, r.dot(a) / 0.03)),
                           cv + size * 0.2 * max(-1.0, min(1.0, r.dot(c) / 0.05)))
        return f

    n, half = 16, 0.011
    ring = [(math.cos(2 * math.pi * i / n), math.sin(2 * math.pi * i / n)) for i in range(n + 1)]
    for (c0, s0), (c1, s1) in zip(ring, ring[1:]):
        # strap: outside, and the two edges down to the wraps
        face([at_(-half, r_out * c0, r_out * s0), at_(half, r_out * c0, r_out * s0),
              at_(half, r_out * c1, r_out * s1), at_(-half, r_out * c1, r_out * s1)], "band")
        for side in (-1, 1):
            pts = [at_(side * half, r_out * c0, r_out * s0), at_(side * half, r_in * 0.9 * c0, r_in * 0.9 * s0),
                   at_(side * half, r_in * 0.9 * c1, r_in * 0.9 * s1), at_(side * half, r_out * c1, r_out * s1)]
            face(pts if side < 0 else pts[::-1], "band")
    # the case: a short drum on the back of the wrist, its dial facing out
    case_r, dial_r, low, high = 0.027, 0.0215, r_out - 0.002, r_out + 0.009
    for (c0, s0), (c1, s1) in zip(ring, ring[1:]):
        face([at_(case_r * c0, low, case_r * s0), at_(case_r * c1, low, case_r * s1),
              at_(case_r * c1, high, case_r * s1), at_(case_r * c0, high, case_r * s0)], "case")
        face([at_(case_r * c0, high, case_r * s0), at_(case_r * c1, high, case_r * s1),
              at_(dial_r * c1, high + 0.0008, dial_r * s1), at_(dial_r * c0, high + 0.0008, dial_r * s0)], "case")
        face([at_(dial_r * c0, high + 0.0008, dial_r * s0), at_(dial_r * c1, high + 0.0008, dial_r * s1),
              at_(0.0, high + 0.0012, 0.0)], "dial")
    # the hands: ten past ten, the way watches smile
    for angle, length in ((math.radians(-58), 0.012), (math.radians(60), 0.0165)):
        d = Vector((math.sin(angle), math.cos(angle)))  # in the dial's plane: (across c, toward the hand a)
        side = Vector((d.y, -d.x)) * 0.0021
        top = high + 0.0022

        def on_dial(q):
            return at_(q.y, top, q.x)
        face([on_dial(-side - d * 0.002), on_dial(side - d * 0.002), on_dial(side + d * length),
              on_dial(-side + d * length)], "case")
    # every face looks away from the middle of its own part (the strap from the arm, the rest from the case)
    middle = {"band": centre, "case": at_(0.0, (low + high) / 2, 0.0), "dial": at_(0.0, (low + high) / 2, 0.0)}
    bm.faces.index_update()
    bm.normal_update()
    for f in bm.faces:
        if f.normal.dot(f.calc_center_median() - middle[parts[f.index]]) < 0:
            f.normal_flip()
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    me = bpy.data.meshes.new(WATCH)
    bm.to_mesh(me)
    bm.free()
    me.use_auto_smooth = True  # the strap's edges and the case's rim stay crisp
    me.auto_smooth_angle = math.radians(50)
    obj = bpy.data.objects.new(WATCH, me)
    bpy.context.scene.collection.objects.link(obj)
    model = next(o for o in objs if o.name == "Vale_Body")
    for mat in model.data.materials:
        me.materials.append(mat)
    # its own bone, so it can turn about the forearm with the hand (witness_anims.fk): the rig has no forearm
    # twist, and a watch that stayed put while the wrist turned over would face the wrong way
    bone = (WATCH_BONE, "LowerArm_L", centre.copy(), centre + a * 0.03, False)
    ce.activate(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones.new(WATCH_BONE)
    eb.head, eb.tail = bone[2], bone[3]
    eb.parent = rig.data.edit_bones["LowerArm_L"]
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.pose.bones[WATCH_BONE].rotation_mode = "QUATERNION"
    set_weights(obj, {WATCH_BONE: 1.0})
    obj.parent = rig
    obj.modifiers.new("Armature", "ARMATURE").object = rig
    objs.append(obj)
    print(f"[witness_export] watch: {tri_count(obj)} tris, strap radius {r_out:.4f} m, colours "
          + ", ".join(f"{k} {tuple(round(x, 3) for x in t[1])} at uv {tuple(round(x, 4) for x in t[0])}"
                      for k, t in texels.items()))
    return table + [bone]


WITNESSES = {"Bea": Bea, "Vale": Vale}


# --- pipeline ----------------------------------------------------------------------------------------------

def build_game_parts(W, char):
    import craft as craft_lib
    n = W.name
    P = W.build(craft_lib.Craft(1.2))
    bpy.context.view_layer.update()
    ce.patch_materials()
    info = {"handL": bpy.data.objects[n + "_HandLPivot"].matrix_world.copy(),
            "handR": bpy.data.objects[n + "_HandRPivot"].matrix_world.copy()}
    sources = [o for o in bpy.data.objects if o.type in ("MESH", "CURVE")]
    W.probe(P, info, sources)
    W.prepare(P, sources)
    bpy.context.view_layer.update()

    def post(src, me):
        """Mouth, teeth and freckles sit a fraction of a millimetre above the smooth skull; the game skull is
        its coarser cage, so they are lifted clear of it."""
        for pattern, lift in W.lift:
            if re.match(pattern, src.name):
                for v in me.vertices:
                    v.co += (v.co - P.head_c).normalized() * lift
    parts = []
    for src in sources:
        spec = char.spec_for(src.name)
        if spec is None:
            raise RuntimeError("no game spec for " + src.name)
        parts.append(ce.realize(src, spec, post))
        if hasattr(W, "after"):
            W.after(P, src.name, parts[-1])
        if src.name == n + "_Head":
            ce.keep_skull_front(parts[-1], P.head_c, lambda d: W.skull(d)[0])
            me = parts[-1].data
            flags = [0 if W.skull((p.center - P.head_c).normalized())[1] else 1 for p in me.polygons]
            me.attributes["skull"].data.foreach_set("value", flags)
    for o in [o for o in bpy.data.objects if not o.name.startswith("G_")]:
        bpy.data.objects.remove(o, do_unlink=True)
    for cu in list(bpy.data.curves):
        if cu.users == 0:
            bpy.data.curves.remove(cu)
    return P, parts, info


def weight_parts(W, parts, P, rig, table, info):
    n = W.name
    body = next(o for o in parts if o["src"] == n + "_Body")
    names, Wt = ce.body_auto_weights(body, rig, table)
    ctx = {
        "info": info, "P": P,
        "on_trunk": SkinSource(body, names, Wt, {"Hips", "Spine", "Chest", "Shoulder_L", "Shoulder_R"}),
        "on_neck": SkinSource(body, names, Wt, {"Chest", "Neck"}),
        "on_arm": {S: SkinSource(body, names, Wt, {"UpperArm_" + S, "LowerArm_" + S}) for S in "LR"},
        "on_leg": {S: SkinSource(body, names, Wt, {"UpperLeg_" + S, "LowerLeg_" + S}) for S in "LR"},
    }
    k = ce.hand_scale(P, W.hand_size)
    f = ce.shoe_length(P) / 0.195
    for o in parts:
        src = o["src"]
        if o is body:
            continue
        side = "L" if sum(v.co.x for v in o.data.vertices) > 0 else "R"
        if o["grp"] == "Face":
            set_weights(o, {"Head": 1.0})
        elif src.startswith(n + "_Hand"):
            ce.hand_weights(o, k, src, len(n + "_Hand"))
        elif src.startswith(n + "_Shoe"):
            ce.shoe_weights(o, src, len(n + "_Shoe"), f)
        elif src.startswith(n + "_Cuff"):
            set_weights(o, {"LowerArm_" + side: 1.0})
        elif not W.weigh(o, src, side, ctx):
            raise RuntimeError("no weights for " + src)
    body.modifiers.clear()
    body.parent = None


def stage_model(W, args):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    n = W.name
    char = ce.Char(n, W.groups, W.parts, W.detail, tag="witness_export")
    P, parts, info = build_game_parts(W, char)
    char.log(n, "parts", len(parts), "triangles before culling", sum(tri_count(o) for o in parts))
    counts = {}
    for o in parts:
        key = re.sub(r"[.\d]+$", "", o["src"])
        counts[key] = counts.get(key, 0) + tri_count(o)
    char.log("per part:", ", ".join(f"{k[len(n) + 1:]} {v}" for k, v in sorted(counts.items(), key=lambda kv: -kv[1])))
    table = ce.human_bones(P, info, W.hand_size) + W.bones(P, info)
    rig = ce.build_armature(table, n + "_Rig")
    weight_parts(W, parts, P, rig, table, info)
    lists = {g: [o for o in parts if o["grp"] == g] for g in W.groups + ("Face",)}
    face = ce.join(char, lists["Face"], n + "_Face")
    by_group = {g: ce.join(char, lists[g], "Tmp_" + g) for g in W.groups}
    solid = [face] + [by_group[g] for g in W.solid]
    char.log("hidden faces removed", ce.remove_hidden(char, solid, W.rings, W.region))
    char.log("after culling:", {o.name: tri_count(o) for o in [face] + list(by_group.values())})
    if args["count"]:
        return None, None
    atlas = ce.join(char, [by_group[g] for g in W.groups], "Tmp_Atlas")
    ce.unwrap(atlas, 0.0035)
    ce.unwrap_face(face, P.head_c, P.head_r)
    meshes = ce.split_groups(char, atlas)
    together = [meshes[g] for g in W.groups if g not in W.loose] + [face]
    visible = {o: together for o in together}
    visible.update({meshes[g]: [meshes[g]] for g in W.loose})
    paths = ce.bake_all(char, meshes, face, visible, args["out"], args["tex"], args["face_tex"], args["samples"],
                        args["ao_samples"])
    objs = [meshes[g] for g in W.groups] + [face]
    for o in objs:
        ce.limit_weights(o)
        for name in ("orco", "grp", "skull", "part"):
            if name in o.data.attributes:
                o.data.attributes.remove(o.data.attributes[name])
        o.parent = rig
        o.modifiers.new("Armature", "ARMATURE").object = rig
    f = ce.shoe_length(P) / 0.195
    meta = {"shoe": f, "height": P.h, "head_c": tuple(P.head_c), "head_r": P.head_r}
    meta.update(W.meta(P, info))
    rig["char_table"] = json.dumps([(b, p, tuple(h), tuple(t), d) for b, p, h, t, d in table])
    rig["char_textures"] = json.dumps(paths)
    rig["char_meta"] = json.dumps(meta)
    return rig, objs


def solver_rig(table, f):
    rig = {name: (parent, head.copy()) for name, parent, head, _tail, _d in table}
    sole = {}
    for S in "LR":
        x = rig["Foot_" + S][1].x
        sole[S] = [("Foot_" + S, Vector((x, 0.05 * f, 0.0))), ("Foot_" + S, Vector((x, -0.098 * f, 0.0))),
                   ("Toe_" + S, Vector((x, -0.156 * f, 0.0)))]
    rig["_sole"] = sole
    return rig


def main(cast=None):
    """`cast`: {name: character class}; junekit_export.py passes its own."""
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    W = (cast or WITNESSES)[argv[0]]
    n = W.name

    def opt(name, default, cast=str):
        return cast(argv[argv.index(name) + 1]) if name in argv else default
    args = {"out": opt("--out", os.path.join(CHARACTERS, n)), "cache": opt("--cache", ""),
            "tex": opt("--tex", 2048, int), "face_tex": opt("--face-tex", 1024, int),
            "samples": opt("--samples", 4, int), "ao_samples": opt("--ao-samples", 24, int),
            "report": opt("--report", ""), "count": "--count" in argv}
    log = ce.Char(n, (), (), None, tag="witness_export").log
    if args["cache"] and os.path.exists(args["cache"]):
        bpy.ops.wm.open_mainfile(filepath=args["cache"])
        rig = bpy.data.objects[n + "_Rig"]
        objs = [o for o in bpy.data.objects if o.type == "MESH"]
        log("loaded cache", args["cache"])
    else:
        rig, objs = stage_model(W, args)
        if rig is None:
            return
        if args["cache"]:
            bpy.ops.wm.save_as_mainfile(filepath=args["cache"], compress=True)
    table = [(b, p, Vector(h), Vector(t), d) for b, p, h, t, d in json.loads(rig["char_table"])]
    if hasattr(W, "refit"):
        table = W.refit(rig, objs, table)
    meta = json.loads(rig["char_meta"])
    srig = solver_rig(table, meta["shoe"])
    A = getattr(W, "anims", anims)  # the module with this character's solver and clips
    A.bind(srig)
    if "thigh_r" in meta:
        anims.VALE_THIGH_R = meta["thigh_r"]
    clips = ce.key_clips(rig, W.clips, A.solve, A.FPS)
    fbx = os.path.join(args["out"], n + ".fbx")
    os.makedirs(args["out"], exist_ok=True)
    ce.export_fbx(rig, objs, fbx, A.FPS)
    heads = {b: [round(c, 4) for c in h] for b, _p, h, _t, _d in table}
    report = {
        "fbx": fbx, "textures": json.loads(rig["char_textures"]),
        "texture_sizes": {n + "_BaseColor": args["tex"], n + "_Normal": args["tex"], n + "_Face": args["face_tex"]},
        "triangles": {o.name: tri_count(o) for o in objs},
        "bones": [b.name for b in rig.data.bones], "bone_heads": heads, "clips": clips, "meta": meta,
    }
    if n == "Vale":
        off = anims.vale_hips()
        report["seat"] = {"seat_height": anims.SEAT, "hips_offset_from_rest": [round(c, 4) for c in off],
                          "hips_rest": heads["Hips"],
                          "pelvis_seated": [round(a + b, 4) for a, b in zip(heads["Hips"], off)]}
    if hasattr(W, "report"):
        W.report(report, heads, meta)
    report["triangles"]["total"] = sum(report["triangles"].values())
    log(json.dumps(report, indent=1))
    if args["report"]:
        with open(args["report"], "w") as fh:
            json.dump(report, fh, indent=1)
    log("exported", fbx)


if __name__ == "__main__":
    main()
