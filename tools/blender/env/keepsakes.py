"""The S004 keepsakes (plan section 2.1): the collectible paper bat, the bat mobile and Pip's dress-up trunk.

PaperBatKeepsake  A bat folded from old drawing paper: ink black outside, and a cream inner fold with faint pencil
                  lines that shows when it flutters open. Parts: the body (root), `Wing_L` and `Wing_R`, hinged on
                  the body's folds (the pivots' up axis). It is built open; the game folds the wings shut over the
                  front (so only the black backs show) and flutters them open when Pip is near. Origin at the body's
                  lowest point, front toward -Y.
BatMobile         Hangs from an eave by the hook at its origin. Three painted hoops, one under the other, each
                  around a little paper lantern on the middle cord. Parts: `Ring1..3` (the hoop with a gap),
                  `Ring1..3_Close` (the piece that closes it at 10, 20 and 30 bats), `KeepsakeLantern1..3_Glow` (the
                  lantern's paper, lit) and `Hook_01..30`, each a thread whose pivot is where its bat hangs.
DressUpTrunk      One of the arrival porch's moving boxes (the crate, 0.6 m) as Pip's costume trunk: `TrunkLid` is
                  hinged on the back edge (pivot), and the cloth, hat and lantern inside show when it opens.

Review builders (`REVIEW`, rendered by `keepsakes_review.py`) and game builders (`env_export_keepsakes.py`)."""
import math

import bmesh
from mathutils import Vector

import env_lib as el
import porch_kit as pk
import props as pr
from env_lib import E, R, T, Builder, H, lc

BAT_SPAN = 0.34
BAT_K = BAT_SPAN / 1.24
BAT_HINGE = 0.13 * BAT_K  # the two folds beside the body
BAT_FOOT = -0.16 * BAT_K  # lowest point of the outline
BAT_REST = 12.0  # degrees the open wings still lean toward the viewer
PENCIL = H(0x8A7F72)
CREAM = H(0xF3E6C6)
INK = H(0x231D30)

RINGS = ((0.15, -0.30), (0.23, -0.55), (0.31, -0.80))  # hoop radius, height under the hook
RING_GAP = (245.0, 295.0)  # the closing piece, toward the viewer
HOOKS_PER_RING = 10
TRUNK = 0.6
TRUNK_FLOOR = 0.4  # the raised floor inside: the dress-up things lie just under the rim
MOBILE_BAT = 0.4  # scale of a keepsake bat on the mobile


def _clip(poly, x0, keep_above):
    """Sutherland-Hodgman against the line x = x0."""
    out = []
    for i, p in enumerate(poly):
        q = poly[(i + 1) % len(poly)]
        pin, qin = (p[0] >= x0) == keep_above, (q[0] >= x0) == keep_above
        if pin:
            out.append(p)
        if pin != qin:
            t = (x0 - p[0]) / (q[0] - p[0])
            out.append((x0, p[1] + (q[1] - p[1]) * t))
    return out


def _ribbon(pts, w):
    left, right = [], []
    for i, p in enumerate(pts):
        a, c = pts[max(i - 1, 0)], pts[min(i + 1, len(pts) - 1)]
        dx, dz = c[0] - a[0], c[1] - a[1]
        n = math.hypot(dx, dz) or 1.0
        left.append((p[0] - dz / n * w / 2, p[1] + dx / n * w / 2))
        right.append((p[0] + dz / n * w / 2, p[1] - dx / n * w / 2))
    return left + right[::-1]


def _circ(r, n=12, at=(0.0, 0.0)):
    return [(at[0] + r * math.cos(2 * math.pi * i / n), at[1] + r * math.sin(2 * math.pi * i / n)) for i in range(n)]


def bat_pivots():
    lift = -BAT_FOOT
    return {"Wing_L": T(-BAT_HINGE, 0, lift), "Wing_R": T(BAT_HINGE, 0, lift)}


def keepsake_bat(b, craft, M=None, fold=0.0):
    """`fold`: 0 = open (as exported), 1 = shut over the front."""
    paper = craft.paper_white()
    k, h = BAT_K, BAT_HINGE
    out = [(x * k, z * k) for x, z in pr.BAT]
    part = getattr(b, "part", None)
    with b.at((M if M is not None else T()) @ T(0, 0, -BAT_FOOT)):
        body = _clip(_clip(out, -h, True), h, False)
        b.prism(paper, body, 0.004, T(0, -0.002, 0), tint=lc(INK))
        b.prism(paper, [(0.0, 0.085 * k), (0.075 * k, -0.02 * k), (0.0, -0.1 * k), (-0.075 * k, -0.02 * k)], 0.0012, T(0, -0.0032, 0), tint=lc(CREAM))
        for s in (-1, 1):
            b.prism(craft.paint(), _circ(0.0042, 6, (s * 0.014, 0.115 * k)), 0.001, T(0, -0.003, 0), tint=lc(E["candy"]))
        wing = [(x - h, z) for x, z in _clip(out, h, True)]
        cx = sum(p[0] for p in wing) / len(wing)
        cz = sum(p[1] for p in wing) / len(wing)
        inner = [(cx + (x - cx) * 0.88 if x > 1e-6 else 0.004, cz + (z - cz) * 0.88) for x, z in wing]
        root = (0.012, 0.03 * k)
        tips = [((0.5 * k - h) * 0.86, 0.13 * k), ((0.6 * k - h) * 0.82, 0.02 * k), ((0.44 * k - h) * 0.8, -0.07 * k), ((0.24 * k - h) * 0.8, -0.105 * k)]
        for s, name in ((1, "Wing_R"), (-1, "Wing_L")):
            b.part = name
            flip = (lambda pts: pts) if s > 0 else (lambda pts: [(-x, z) for x, z in pts][::-1])
            with b.at(T(s * h, 0, 0) @ R("Z", -s * (BAT_REST + fold * (168.0 + 4.0 * s - BAT_REST)))):
                b.prism(paper, flip(wing), 0.0026, T(0, -0.0006, 0), tint=lc(INK))  # the black outside (back and edge)
                b.prism(paper, flip(inner), 0.0012, T(0, -0.0018, 0), tint=lc(CREAM))  # the cream inner fold
                for tip in tips:  # faint pencil: old fold lines
                    b.prism(paper, flip(_ribbon([root, tip], 0.0034)), 0.0006, T(0, -0.0024, 0), tint=lc(PENCIL))
                if s > 0:  # a scrap of the old drawing: a star, and on the other wing a little lantern
                    b.prism(paper, [(x + 0.07, z + 0.012) for x, z in el.co.star_outline(0.013)], 0.0006, T(0, -0.0024, 0), tint=lc(PENCIL))
                else:
                    b.ring(paper, 0.008, 0.0115, 0.0006, T(-0.07, -0.0024, 0.012), segs=10, tint=lc(PENCIL), chamfer=0.0)
                    b.prism(paper, flip(_ribbon([(0.07, 0.024), (0.07, 0.034)], 0.003)), 0.0006, T(0, -0.0024, 0), tint=lc(PENCIL))
        b.part = part


def build_keepsake_bat(craft, fold=0.0):
    b = Builder(301)
    b.part = None
    keepsake_bat(b, craft, fold=fold)
    return b.finish("PaperBatKeepsake")


# --- the bat mobile -------------------------------------------------------------------------------------------

def hoop(b, mat, radius, t, a0, a1, segs, M=None, tint=None):
    """A painted hoop (diamond section) in the XY plane, from angle a0 to a1 (degrees)."""
    bm = bmesh.new()
    prof = [(-t, 0.0), (0.0, -t), (t, 0.0), (0.0, t)]
    cols = []
    for i in range(segs + 1):
        a = math.radians(a0 + (a1 - a0) * i / segs)
        cols.append([bm.verts.new(((radius + dr) * math.cos(a), (radius + dr) * math.sin(a), dz)) for dr, dz in prof])
    for p, q in zip(cols, cols[1:]):
        for j in range(4):
            bm.faces.new((p[j], p[(j + 1) % 4], q[(j + 1) % 4], q[j]))
    if abs(a1 - a0) < 359.9:
        bm.faces.new(cols[0])
        bm.faces.new(cols[-1][::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    b.add_bm(bm, mat, M, tint)


def hook_points():
    """The thirty hooks, in the order bats are hung (ring 1 first): (ring index, top on the hoop, bottom)."""
    pts = []
    for k, (radius, z) in enumerate(RINGS):
        for i in range(HOOKS_PER_RING):
            a = math.radians(90.0 + 18.0 * k + 360.0 * i / HOOKS_PER_RING)
            drop = 0.07 if i % 2 == 0 else 0.115
            x, y = radius * math.cos(a), radius * math.sin(a)
            pts.append((k, Vector((x, y, z)), Vector((x, y, z - drop))))
    return pts


def mobile_pivots():
    return {f"Hook_{n + 1:02d}": T(*bottom) for n, (_k, _top, bottom) in enumerate(hook_points())}


def lantern_body(r=0.04, hh=0.045, n=7):
    return [(0.012, hh)] + [(r * math.sin(math.pi * (i + 1) / (n + 1)) ** 0.6, hh * math.cos(math.pi * (i + 1) / (n + 1))) for i in range(n)] + [(0.012, -hh)]


def bat_mobile(b, craft, P, M=None, bats=0):
    """`bats`: review only, hang that many little keepsake bats and close and light the rings they complete."""
    paint = craft.paint()
    thread = lc(E["bone_dim"])
    hoops = (P["orange"], P["trim_x"], P["candy"])
    part = getattr(b, "part", None)
    with b.at(M if M is not None else T()):
        b.ring(P["brass"], 0.012, 0.02, 0.006, T(0, 0.003, -0.02), segs=10, tint=1.0, chamfer=0.0)
        b.rod(paint, (0, 0, -0.035), (0, 0, RINGS[-1][1] - 0.1), 0.003, 4, thread)
        b.lathe(P["orange"], [(0.0, 0.0), (0.012, -0.008), (0.012, -0.02), (0.0, -0.03)], 6, T(0, 0, RINGS[-1][1] - 0.095), tint=1.0)  # the end bead
        game = hasattr(b, "parts")  # the game model carries every part; the review shows the state for `bats`
        for k, (radius, z) in enumerate(RINGS):
            closed = game or bats >= (k + 1) * HOOKS_PER_RING
            for a in (90, 210, 330):  # three threads carry each hoop
                a = math.radians(a + 40 * k)
                b.rod(paint, (0, 0, z + 0.16), (radius * math.cos(a), radius * math.sin(a), z), 0.002, 3, thread)
            # the little paper lantern in the middle of the hoop: caps and ribs here, its paper is the glow part
            with b.at(T(0, 0, z + 0.005)):
                for sz in (-1, 1):
                    b.cyl(P["shutter"], 0.016, 0.012, T(0, 0, sz * 0.05), segments=8, tint=1.0)
                b.lathe(paint, lantern_body(), 10, tint=lc(blend_paper(0.25)))
                for zz in (-0.022, 0.0, 0.022):
                    rr = 0.04 * math.sin(math.acos(zz / 0.045)) ** 0.6 + 0.0015
                    b.lathe(paint, [(rr, zz - 0.003), (rr + 0.002, zz), (rr, zz + 0.003)], 10, tint=lc(E["orange"]))
                if closed:
                    b.glow_tag = f"KeepsakeLantern{k + 1}"
                    b.lathe(craft.glow(E["glow"], 1.0), [(r + 0.0012, zz * 0.97) for r, zz in lantern_body()], 10, tint=1.0)
                    b.glow_tag = None
            b.part = f"Ring{k + 1}"
            hoop(b, hoops[k], radius, 0.012, RING_GAP[1], RING_GAP[0] + 360.0, 26, T(0, 0, z), tint=1.0)
            if closed:
                b.part = f"Ring{k + 1}_Close"
                hoop(b, hoops[k], radius, 0.012, RING_GAP[0], RING_GAP[1], 5, T(0, 0, z), tint=1.0)
            b.part = part
        for n, (k, top, bottom) in enumerate(hook_points()):
            if not game and n >= bats:
                continue
            b.part = f"Hook_{n + 1:02d}"
            b.rod(paint, top, bottom + Vector((0, 0, 0.008)), 0.0016, 3, thread)
            b.lathe(P["brass"], [(0.0, 0.008), (0.004, 0.004), (0.0, 0.0)], 4, T(*bottom), tint=1.0)
            b.part = part
            if n < bats:  # review: a little bat on the hook, turned outward
                yaw = math.degrees(math.atan2(top.y, top.x)) + 90.0
                s = MOBILE_BAT
                keepsake_bat(b, craft, T(*bottom) @ R("Z", yaw) @ el.S(s) @ T(0, 0, -0.36 * BAT_K - 0.012), fold=0.12)


def blend_paper(t):
    return el.blend(E["bone"], E["orange"], t)


def build_bat_mobile(craft, bats=0):
    b = Builder(302)
    b.part = None
    b.glow_tag = None
    bat_mobile(b, craft, pk.paints(craft), bats=bats)
    return b.finish("BatMobile")


# --- the dress-up trunk ---------------------------------------------------------------------------------------

def trunk_pivots():
    return {"TrunkLid": T(0, TRUNK / 2, TRUNK)}


def dress_up_trunk(b, craft, P, M=None, lid_open=0.0):
    """The porch crate with a hinged lid (`lid_open` degrees: review only), lined, with Pip's dress-up things."""
    size, hs = TRUNK, TRUNK / 2
    wood_x = craft.timber(E["timber"], "X", wear=0.5, bare=E["timber_dark"])
    wood_z = craft.timber(E["timber"], "Z", wear=0.5, bare=E["timber_dark"])
    paint, felt = craft.paint(), craft.felt(el.V["violet"])
    part = getattr(b, "part", None)
    with b.at(M if M is not None else T()):
        for side in range(4):
            with b.at(R("Z", 90 * side)):
                n = 4
                for i in range(n):
                    z = (i + 0.5) * size / n
                    b.box(wood_x, (size - 0.02, 0.025, size / n - 0.016), (0, -hs + 0.012, z), bevel=0.008, tint=b.vary(0.8, 1.12))
                b.box(paint, (size - 0.05, 0.006, size - 0.03), (0, -hs + 0.028, hs + 0.012), bevel=0, tint=lc(el.blend(E["plum"], E["bone"], 0.3)), drop=("-y",))  # lining
                for sx in (-1, 1):
                    b.box(wood_z, (0.085, 0.03, size), (sx * (hs - 0.0425), -hs - 0.012, hs), bevel=0.01)
                    for z in (0.08, size - 0.08, hs):
                        b.disc(paint, 0.009, 0.006, T(sx * (hs - 0.0425), -hs - 0.03, z), segs=6, tint=lc(E["ink"]))
                if side == 0:  # the front: a paper label with a bat and a star, as on a costume box
                    b.prism(paint, [(-0.15, -0.1), (0.15, -0.1), (0.15, 0.1), (-0.15, 0.1)], 0.004, T(0, -hs - 0.004, hs), tint=lc(E["bone"]))
                    pr.bat(b, craft.paper_white(), 0.2, T(-0.035, -hs - 0.009, hs - 0.005), fold=0.04, curl=0.06)
                    b.prism(paint, el.co.star_outline(0.036), 0.003, T(0.1, -hs - 0.007, hs + 0.045), tint=lc(E["orange"]))
        b.box(felt, (size - 0.06, size - 0.06, 0.02), (0, 0, TRUNK_FLOOR - 0.01), bevel=0, tint=1.0, drop=("-z",))  # the floor of the trunk, raised
        # inside: the star-lined cloth folded, the showman's hat, the paper lantern
        b.box(paint, (0.3, 0.36, 0.09), (-0.1, 0.0, TRUNK_FLOOR + 0.045), bevel=0.02, tint=lc(E["night"]))
        for x, y, r in ((-0.16, -0.08, 0.03), (-0.05, 0.04, 0.036), (-0.15, 0.1, 0.024), (-0.03, -0.11, 0.022)):
            b.prism(paint, el.co.star_outline(r), 0.004, T(x, y, TRUNK_FLOOR + 0.0905) @ R("X", -90) @ T(0, -0.004, 0), tint=lc(E["candy"]))
        with b.at(T(0.15, 0.1, TRUNK_FLOOR) @ R("Y", -14)):
            b.lathe(felt, [(0.0, 0.0), (0.105, 0.0), (0.105, 0.012), (0.062, 0.016), (0.066, 0.12), (0.0, 0.124)], 14, tint=1.0)
            b.lathe(paint, [(0.064, 0.018), (0.07, 0.02), (0.07, 0.045), (0.066, 0.047)], 14, tint=lc(E["orange"]))
        with b.at(T(0.15, -0.13, TRUNK_FLOOR + 0.08)):
            b.lathe(paint, [(0.02, 0.075)] + [(0.075 * math.sin(math.pi * (i + 1) / 8) ** 0.6, 0.075 * math.cos(math.pi * (i + 1) / 8)) for i in range(7)]
                    + [(0.02, -0.075)], 12, tint=lc(E["bone"]))
            for zz in (-0.04, 0.0, 0.04):
                rr = 0.075 * math.sin(math.acos(zz / 0.075)) ** 0.6 + 0.002
                b.lathe(paint, [(rr, zz - 0.004), (rr + 0.003, zz), (rr, zz + 0.004)], 12, tint=lc(E["orange"]))
        b.part = "TrunkLid"
        with b.at(T(0, hs, size) @ R("X", lid_open) @ T(0, -hs, 0)):
            n = 4
            for i in range(n):
                y = -hs + (i + 0.5) * size / n
                b.box(wood_x, (size + 0.02, size / n - 0.014, 0.028), (0, y, 0.012), bevel=0.008, tint=b.vary(0.85, 1.12))
            for sx in (-1, 1):
                b.box(wood_z, (0.07, size + 0.03, 0.024), (sx * (hs - 0.09), 0, 0.036), bevel=0.008)
                b.box(P["brass"], (0.05, 0.016, 0.06), (sx * (hs - 0.09), hs + 0.012, 0.0), bevel=0.005, tint=1.0)  # hinges
            b.box(paint, (size - 0.07, size - 0.07, 0.006), (0, 0, -0.004), bevel=0, tint=lc(E["plum"]), drop=("+z",))  # lining under the lid
            b.prism(paint, el.co.star_outline(0.12), 0.003, T(0, 0, -0.0085) @ R("X", 90), tint=lc(E["candy"]))
            b.box(P["brass"], (0.09, 0.02, 0.07), (0, -hs - 0.022, 0.0), bevel=0.008, tint=1.0)  # clasp
        b.part = part


def build_trunk(craft, lid_open=0.0):
    b = Builder(303)
    b.part = None
    dress_up_trunk(b, craft, pk.paints(craft), lid_open=lid_open)
    return b.finish("DressUpTrunk")


# --- review ---------------------------------------------------------------------------------------------------

def _review_bats(craft):
    b = Builder(311)
    b.part = None
    for i, fold in enumerate((1.0, 0.55, 0.0)):
        keepsake_bat(b, craft, T(-0.42 + 0.42 * i, 0, 0.1), fold=fold)
    pr.bat(b, craft.paper_white(), BAT_SPAN, T(0.9, 0, 0.16), fold=0.45, curl=0.35)  # the decorative ink-black bat, to tell apart
    b.box(craft.tinted(H(0x3A3058), 0.9, 0.1), (2.2, 0.3, 0.1), (0.2, 0.05, 0.05), bevel=0.01, tint=1.0)
    return b.finish("Bats")


def _review_mobile(craft, bats):
    def build(c):
        b = Builder(312)
        b.part = None
        b.glow_tag = None
        P = pk.paints(c)
        bat_mobile(b, c, P, T(0, 0, 1.3), bats=bats)
        b.box(P["trim_x"], (1.0, 0.15, 0.14), (0, 0, 1.37), bevel=0.02)
        return b.finish("Mobile")
    return build


def _review_trunk(craft):
    b = Builder(313)
    b.part = None
    P = pk.paints(craft)
    dress_up_trunk(b, craft, P, T(-0.5, 0, 0) @ R("Z", 18))
    dress_up_trunk(b, craft, P, T(0.5, 0, 0) @ R("Z", -22), lid_open=-105.0)
    return b.finish("Trunks")


REVIEW = [("Bats", _review_bats), ("Mobile12", _review_mobile(None, 12)), ("Mobile30", _review_mobile(None, 30)), ("Trunks", _review_trunk)]
# name -> (group, eye offset, target offset, lens, width, height, ortho scale)
VIEWS = {
    "bats": ("Bats", (0.2, -3.0, 0.2), (0.2, 0.0, 0.2), 50, 1600, 400, 1.75),
    "bat_open": ("Bats", (0.42, -3.0, 0.16), (0.42, 0.0, 0.16), 50, 800, 400, 0.5),
    "bats_side": ("Bats", (-0.5, -0.9, 0.5), (0.1, 0.0, 0.15), 50, 800, 400, None),
    "mobile12": ("Mobile12", (0.35, -1.9, 1.0), (0.0, 0.0, 0.8), 50, 800, 1000, None),
    "mobile30": ("Mobile30", (0.35, -1.9, 1.0), (0.0, 0.0, 0.8), 50, 800, 1000, None),
    "trunks": ("Trunks", (0.0, -2.4, 1.6), (0.0, 0.0, 0.42), 50, 1600, 1000, None),
}
