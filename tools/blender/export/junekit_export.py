"""June and Aunt Kit, game-ready (spec §2, §3): the missing witness and the owner, through the cast's pipeline.

    blender -b -t 2 --python tools/blender/export/junekit_export.py -- <June|Kit> [--out DIR] [--cache FILE.blend]
                                    [--tex 2048] [--face-tex 1024] [--report FILE.json] [--count]

Same steps, options and outputs as witness_export.py (which runs them): the approved character from
tools/blender/chars/cast.py, game-resolution parts, hidden-face removal, one baked atlas plus a face map on
`<Name>_Face`, Pip's skeleton plus costume bones, the clips of junekit_anims.py, one FBX.

Costume bones (none deform the body skin):
  June  Cape_L1-3, Cape_M1-3, Cape_R1-3   the silver comet cape: a back chain and two side chains. The side
                                          chains start over the shoulders, are swung by the upper arms (the arm
                                          lifts the cloak) and open like doors when a forearm comes forward.
        Braid_1, Braid_2                  the braid over her left shoulder (hangs plumb from the head)
        HatTip                            the crooked tip of the witch hat (the hat itself rides on Head)
  Kit   Lantern (child of Hand_R, at the grip, like Pip's Pail), Brooch (child of Chest: the owl pin, rigid),
        Shawl_L, Shawl_R (the shawl's wings over the arms; they follow the upper arms), Skirt (the long skirt
        below the hips, hangs plumb), ScarfTail (the knot's two tails), Earring_L, Earring_R (the hoops)
June's dress skirt is skinned to the thighs, so she can sit; her long hair is shared between head, neck and chest.
"""
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
for sub in ("style", "pip", "chars", "export"):
    sys.path.insert(0, os.path.join(TOOLS, sub))

import char_export as ce  # noqa: E402
import junekit_anims as anims  # noqa: E402
import witness_export as we  # noqa: E402
from char_export import set_weights, smooth  # noqa: E402
from witness_export import centre_of, common_parts  # noqa: E402


def trunk_row(ctx, p):
    """The body's trunk weights (hips, spine, chest, shoulders) nearest to `p`, as {bone: weight}."""
    src = ctx["on_trunk"]
    w = src.at(p) * src.keep
    if w.sum() < 1e-4:
        return {"Spine": 1.0}
    w = w / w.sum()
    return {n: float(w[j]) for j, n in enumerate(src.names) if w[j] > 1e-4}


def weigh_rows(o, row_at, rigid=False):
    """Give every vertex of `o` the weights row_at(position) (`rigid`: the whole part gets its centre's)."""
    pts = [v.co for v in o.data.vertices]
    if rigid:
        row = row_at(sum(pts, Vector()) / len(pts))
        rows = [row] * len(pts)
    else:
        rows = [row_at(p) for p in pts]
    bones = sorted({b for row in rows for b in row})
    set_weights(o, {b: np.array([row.get(b, 0.0) for row in rows]) for b in bones})


# --- June --------------------------------------------------------------------------------------------------

CAPE_Y = 0.63  # the cape's depth / width
CAPE_BLEND = (25.0, 75.0)  # degrees from straight back: the back chain's share ends, the side chain's begins


class June:
    name = "June"
    anims = anims
    groups = ("Body", "Hair", "Cape")
    hand_size = 1.15
    solid = ("Hair", "Body")
    rings = {"June_Face": 1, "Tmp_Hair": 1, "Tmp_Body": 2}
    loose = ("Cape",)
    detail = re.compile(r"June_(DressStar|SashStar|DressStitch|Collar|Clasp|CapeThreads|HatBand|HatStar|BraidTie|"
                        r"BootCuff|Shoe.Lace|Lash|Brow|Smile|Glint|Cuff)")
    parts = common_parts("June") + [
        (r"June_Body$", "Body", None, None, True, 4300),
        (r"June_BootShaft", "Body", 0, None, True, ("sheet", 56)),
        (r"June_BootCuff", "Body", 0, None, True, 110),
        (r"June_DressStitch", "Body", None, None, True, 420),
        (r"June_(DressStarFelt|SashStar|ClaspStar)", "Body", None, None, False, None),
        (r"June_Dress$", "Body", 1, None, True, ("sheet", 1000)),
        (r"June_CollarTrim", "Body", 0, None, True, 96),
        (r"June_Collar", "Body", 0, None, True, ("sheet", 80)),
        (r"June_Sash$", "Body", 0, None, True, ("sheet", 96)),
        (r"June_ClaspCord", "Body", None, (0, 3), True, 72),
        (r"June_CapeThreads", "Cape", None, None, False, 96),
        (r"June_Cape$", "Cape", 1, None, True, ("sheet", 2100)),
        (r"June_Hair$", "Hair", 0, None, True, 3000),
        (r"June_BraidTie", "Hair", 0, None, True, 80),
        (r"June_BraidTuft", "Hair", 0, None, True, 70),
        (r"June_Braid", "Hair", 0, None, True, 110),
        (r"June_HatBand", "Hair", 0, None, True, ("sheet", 80)),
        (r"June_HatStar", "Hair", None, None, False, None),
        (r"June_Hat$", "Hair", 1, None, True, ("sheet", 760)),
    ]
    clips = anims.JUNE_CLIPS
    lift = ((r"June_Mouth", 0.0006), (r"June_Tongue", 0.0008), (r"June_Smile", 0.0004))

    @staticmethod
    def build(craft):
        import cast
        return cast.build_june(craft)

    @staticmethod
    def skull(d):
        return d.y > 0.42 and d.z > -0.45, False

    @staticmethod
    def prepare(P, sources):
        """The skirt's back is as deep as the cape that hangs over it, so its folds showed through the silver
        from behind: the game skirt is a little flatter at the back."""
        top = P.z["waist"]
        for o in sources:
            if not re.match(r"June_Dress(Stitch)?$", o.name):
                continue
            for v in o.data.vertices:
                if v.co.y > 0 and v.co.z < top:
                    v.co.y *= 1 - 0.2 * smooth((top - v.co.z) / 0.12)
            o.data.update()

    @staticmethod
    def probe(P, info, sources):
        import anatomy as an
        import cast
        braid = sorted((centre_of(o) for o in sources if re.match(r"June_Braid(\.\d+)?$", o.name)),
                       key=lambda c: -c.z)
        info["braid"] = braid
        info["hat"] = bpy.data.objects["June_HatPivot"].matrix_world.copy()
        on_cape = an.probe(bpy.data.objects["June_Cape"])
        z = P.z
        hem = z["ankle"] + 0.05
        levels = (z["shoulder"] + 0.03, z["elbow"], (z["elbow"] + hem) * 0.5 + 0.04, hem)
        info["cape_z"] = levels
        info["cape"] = {}
        for c, angle in (("L", 0.0), ("M", math.pi / 2), ("R", math.pi)):
            pts = []
            for zz in levels:
                hit = cast.on_shell(on_cape, angle, max(zz, hem + 0.01))
                if hit is None:
                    raise RuntimeError(f"no cape at chain {c}, height {zz:.2f}")
                pts.append(Vector((hit[0].x, hit[0].y, zz)))
            info["cape"][c] = pts

    @staticmethod
    def bones(P, info):
        out = []
        for c in "LMR":
            pts = info["cape"][c]
            for i in range(3):
                out.append((f"Cape_{c}{i + 1}", "Chest" if i == 0 else f"Cape_{c}{i}", pts[i].copy(),
                            pts[i + 1].copy(), False))
        b = info["braid"]
        M = info["hat"]
        out += [("Braid_1", "Head", b[1].copy(), b[5].copy(), False),
                ("Braid_2", "Braid_1", b[5].copy(), b[8] + (b[8] - b[7]) * 1.6, False),
                ("HatTip", "Head", M @ Vector((0.02, 0.0, 0.22)), M @ Vector((0.093, 0.0, 0.384)), False)]
        return out

    @staticmethod
    def region(bone):
        return "head" if bone.startswith(("Braid", "HatTip")) else ce.region_of(bone)

    @staticmethod
    def cape_row(info, p):
        j = info["cape_z"]
        az = math.degrees(math.atan2(p.x, p.y / CAPE_Y))  # 0 = straight back, + = her left
        side = smooth((abs(az) - CAPE_BLEND[0]) / (CAPE_BLEND[1] - CAPE_BLEND[0]))
        chains = (("L" if az > 0 else "R", side), ("M", 1 - side))
        top = smooth((j[0] + 0.05 - p.z) / 0.1)
        b12 = smooth((j[1] + 0.07 - p.z) / 0.14)
        b23 = smooth((j[2] + 0.07 - p.z) / 0.14)
        row = {"Chest": 1 - top}
        for c, share in chains:
            for i, level in enumerate((top * (1 - b12), top * b12 * (1 - b23), top * b12 * b23)):
                row[f"Cape_{c}{i + 1}"] = share * level
        return row

    @staticmethod
    def dress_row(ctx, p):
        """The dress follows the trunk; below the hips the skirt is shared between the thighs (less so behind),
        so it tents over her lap when she sits."""
        P = ctx["P"]
        row = trunk_row(ctx, p)
        legs = smooth((P.z["hip"] + 0.02 - p.z) / 0.12) * (1 - 0.45 * smooth(p.y / 0.08))
        left = smooth((p.x + 0.04) / 0.08)
        row = {b: w * (1 - legs) for b, w in row.items()}
        row["UpperLeg_L"] = legs * left
        row["UpperLeg_R"] = legs * (1 - left)
        return row

    @staticmethod
    def weigh(o, src, side, ctx):
        info, P = ctx["info"], ctx["P"]
        hc, r = P.head_c, P.head_r
        if o["grp"] == "Cape":
            weigh_rows(o, lambda p: June.cape_row(info, p))
        elif src.startswith("June_Hat"):
            Mi = info["hat"].inverted()
            tip = np.array([smooth(((Mi @ v.co).z - 0.2) / 0.12) for v in o.data.vertices])
            set_weights(o, {"Head": 1 - tip, "HatTip": tip})
        elif src.startswith("June_Braid"):
            b = info["braid"]
            zs = np.array([v.co.z for v in o.data.vertices])
            w1 = np.array([smooth((b[1].z + 0.02 - zi) / 0.06) for zi in zs])
            w2 = np.array([smooth((b[5].z + 0.03 - zi) / 0.06) for zi in zs])
            set_weights(o, {"Head": 1 - w1, "Braid_1": w1 * (1 - w2), "Braid_2": w1 * w2})
        elif src == "June_Hair":
            # long hair: the lengths below the jaw stay partly with the neck and shoulders when she turns her head
            t = np.array([smooth((hc.z - 0.75 * r - v.co.z) / (0.55 * r)) for v in o.data.vertices])
            set_weights(o, {"Head": 1 - 0.62 * t, "Neck": 0.3 * t, "Chest": 0.32 * t})
        elif re.match(r"June_(DressStarFelt|SashStar)", src):
            weigh_rows(o, lambda p: June.dress_row(ctx, p), rigid=True)
        elif re.match(r"June_(Dress|Sash)", src):
            weigh_rows(o, lambda p: June.dress_row(ctx, p))
        elif src.startswith("June_Collar"):
            ctx["on_neck"].transfer(o)
        elif src.startswith("June_Clasp"):
            set_weights(o, {"Chest": 1.0})
        elif src.startswith("June_Boot"):
            set_weights(o, {"LowerLeg_" + side: 1.0})
        else:
            return False
        return True

    @staticmethod
    def meta(P, info):
        return {}

    @staticmethod
    def report(report, heads, meta):
        off = anims.june_hips()
        report["seat"] = {"seat_height": anims.JUNE_SEAT, "hips_offset_from_rest": [round(c, 4) for c in off],
                          "hips_rest": heads["Hips"],
                          "pelvis_seated": [round(a + b, 4) for a, b in zip(heads["Hips"], off)],
                          "knee_tap_contact_frames": list(anims.TAP_FRAMES)}


# --- Aunt Kit ----------------------------------------------------------------------------------------------

class Kit:
    name = "Kit"
    anims = anims
    groups = ("Body", "Hair", "Shawl", "Lantern")
    hand_size = 1.0
    solid = ("Hair", "Body")
    rings = {"Kit_Face": 1, "Tmp_Hair": 1, "Tmp_Body": 2}
    loose = ("Shawl", "Lantern")
    detail = re.compile(r"Kit_(SkirtStar|SkirtStitch|Sash|Coin|ScarfTrim|ScarfTail|ScarfEar|Hoop|Shoe.Lace|Lash|Brow|"
                        r"Smile|Glint|Cuff)")
    parts = common_parts("Kit") + [
        (r"Kit_Body$", "Body", None, None, True, 4300),
        (r"Kit_SkirtStitch", "Body", None, None, True, 420),
        (r"Kit_SkirtStarFelt", "Body", None, None, False, None),
        (r"Kit_Skirt$", "Body", 1, None, True, ("sheet", 1100)),
        (r"Kit_Sash$", "Body", 0, None, True, ("sheet", 96)),
        (r"Kit_ShawlStitch", "Shawl", None, None, True, 360),
        (r"Kit_ShawlFringe", "Shawl", None, None, False, None),
        (r"Kit_Shawl$", "Shawl", 1, None, True, ("sheet", 1300)),
        (r"Kit_Owl(Rim|Body|Beak)", "Shawl", None, None, True, 60),
        (r"Kit_OwlChest", "Shawl", 0, None, True, 56),
        (r"Kit_Owl(Brow|Perch)", "Shawl", None, (0, 2), True, 40),
        (r"Kit_OwlWing", "Shawl", 0, None, True, 40),
        (r"Kit_OwlEyeRing", "Shawl", None, None, True, 64),
        (r"Kit_OwlEye", "Shawl", 0, None, True, 40),
        (r"Kit_OwlPupil", "Shawl", 0, None, True, 28),
        (r"Kit_OwlTalon", "Shawl", None, None, True, 16),
        (r"Kit_Hair$", "Hair", 0, None, True, 2400),
        (r"Kit_Curl", "Hair", 0, None, True, 64),
        (r"Kit_ScarfTrim", "Hair", None, (0, 1), True, None),
        (r"Kit_ScarfKnot", "Hair", 0, None, True, 110),
        (r"Kit_ScarfEar", "Hair", 0, None, True, 70),
        (r"Kit_ScarfTail", "Hair", 0, None, True, None),
        (r"Kit_Scarf$", "Hair", 0, None, True, None),
        (r"Kit_CoinLink", "Hair", None, None, True, 12),
        (r"Kit_Coin", "Hair", 0, None, True, 40),
        (r"Kit_Hoop", "Hair", None, None, True, 140),
        (r"Kit_CrystalBall", "Lantern", 0, None, True, 380),
        (r"Kit_BallGlint", "Lantern", 0, None, True, 24),
        (r"Kit_BallStar", "Lantern", None, None, False, None),
        (r"Kit_Ball(Collar|Cap)", "Lantern", None, None, True, 150),
        (r"Kit_BallStand", "Lantern", 0, None, True, ("sheet", 60)),
        (r"Kit_BallHandle", "Lantern", None, (0, 3), True, 140),
    ]
    clips = anims.KIT_CLIPS
    lift = ((r"Kit_Smile", 0.0004),)

    @staticmethod
    def build(craft):
        import cast
        return cast.build_kit(craft)

    @staticmethod
    def skull(d):
        return d.y > 0.42 and d.z > -0.45, False

    @staticmethod
    def prepare(P, sources):
        """The shawl's fringe is 140 six-sided rods; the game keeps every thread as a three-sided one."""
        fringe = next(o for o in sources if o.name == "Kit_ShawlFringe")
        co = [v.co.copy() for v in fringe.data.vertices]
        if len(co) % 12:
            raise RuntimeError("unexpected fringe mesh")
        bm = bmesh.new()
        for k in range(0, len(co), 12):
            rod = sorted(co[k:k + 12], key=lambda c: -c.z)  # the threads hang straight down
            ends = (sum(rod[:6], Vector()) / 6, sum(rod[6:], Vector()) / 6)
            rings = []
            for end, rad in zip(ends, (0.0034, 0.0026)):
                rings.append([bm.verts.new(end + Vector((rad * math.cos(a), rad * math.sin(a), 0.0)))
                              for a in (k + 0.5, k + 0.5 + math.tau / 3, k + 0.5 + 2 * math.tau / 3)])
            for i in range(3):
                bm.faces.new((rings[0][i], rings[1][i], rings[1][(i + 1) % 3], rings[0][(i + 1) % 3]))
            bm.faces.new(rings[1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.to_mesh(fringe.data)
        bm.free()
        fringe.data.update()

    @staticmethod
    def probe(P, info, sources):
        r = P.head_r
        named = {o.name: o for o in sources}
        info["grip"] = centre_of(named["Kit_CrystalBall"]) + Vector((0, 0, 0.18))
        info["ball"] = centre_of(named["Kit_CrystalBall"])
        info["pin"] = bpy.data.objects["Kit_OwlBrooch"].matrix_world.translation.copy()
        info["knot"] = centre_of(named["Kit_ScarfKnot"])
        hoops = sorted((centre_of(o) for o in sources if o.name.startswith("Kit_Hoop")), key=lambda c: -c.x)
        info["hoops"] = [c + Vector((0, 0, r * 0.2)) for c in hoops]  # where each hoop hangs from

    @staticmethod
    def bones(P, info):
        z = P.z
        g, pin, knot = info["grip"], info["pin"], info["knot"]
        out = [("Lantern", "Hand_R", g.copy(), g + Vector((0, 0, -0.15)), False),
               ("Brooch", "Chest", pin.copy(), pin + Vector((0, -0.04, 0)), False),
               ("Skirt", "Hips", Vector((0, 0, z["hip"])), Vector((0, 0, z["hip"] - 0.3)), False),
               ("ScarfTail", "Head", knot.copy(), knot + Vector((0, 0, -0.1)), False)]
        for S, s, hoop in (("L", 1, info["hoops"][0]), ("R", -1, info["hoops"][1])):
            sh = Vector((s * P.shoulder_x, 0, z["shoulder"]))
            out += [("Shawl_" + S, "Chest", sh, P.elbow(s) + Vector((0, 0.014, 0)), False),
                    ("Earring_" + S, "Head", hoop.copy(), hoop + Vector((0, 0, -0.05)), False)]
        return out

    @staticmethod
    def region(bone):
        if bone.startswith(("ScarfTail", "Earring")):
            return "head"
        return ce.region_of(bone)

    @staticmethod
    def shawl_row(P, p):
        sx = P.shoulder_x
        arm = smooth((abs(p.x) - 0.55 * sx) / (0.5 * sx))
        return {"Chest": 1 - arm, "Shawl_" + ("L" if p.x > 0 else "R"): arm}

    @staticmethod
    def skirt_row(ctx, p):
        P = ctx["P"]
        row = trunk_row(ctx, p)
        hang = smooth((P.z["pelvis"] + 0.02 - p.z) / 0.3)
        row = {b: w * (1 - hang) for b, w in row.items()}
        row["Skirt"] = hang
        return row

    @staticmethod
    def weigh(o, src, side, ctx):
        info, P = ctx["info"], ctx["P"]
        if o["grp"] == "Lantern":
            set_weights(o, {"Lantern": 1.0})
        elif src.startswith("Kit_Owl"):
            set_weights(o, {"Brooch": 1.0})
        elif o["grp"] == "Shawl":
            weigh_rows(o, lambda p: Kit.shawl_row(P, p))
        elif src.startswith("Kit_ScarfTail"):
            w = np.array([smooth((info["knot"].z - 0.015 - v.co.z) / 0.05) for v in o.data.vertices])
            set_weights(o, {"Head": 1 - w, "ScarfTail": w})
        elif src.startswith("Kit_Hoop"):
            set_weights(o, {"Earring_" + side: 1.0})
        elif o["grp"] == "Hair":
            set_weights(o, {"Head": 1.0})
        elif src.startswith("Kit_SkirtStarFelt"):
            weigh_rows(o, lambda p: Kit.skirt_row(ctx, p), rigid=True)
        elif re.match(r"Kit_(Skirt|Sash)", src):
            weigh_rows(o, lambda p: Kit.skirt_row(ctx, p))
        else:
            return False
        return True

    @staticmethod
    def meta(P, info):
        return {"lantern_ball": tuple(info["ball"]), "lantern_grip": tuple(info["grip"]), "brooch": tuple(info["pin"])}

    @staticmethod
    def report(report, heads, meta):
        report["attach"] = {
            "Lantern": {"bone_head": heads["Lantern"], "light_centre_rest": [round(c, 4) for c in meta["lantern_ball"]],
                        "light_below_bone_m": round(meta["lantern_grip"][2] - meta["lantern_ball"][2], 4)},
            "Brooch": {"bone_head": heads["Brooch"]},
        }


CAST = {"June": June, "Kit": Kit}

if __name__ == "__main__":
    we.main(CAST)
