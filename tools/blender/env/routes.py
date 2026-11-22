"""The treehouse routes of the first case (spec §4 "Routes"): the main accessible loop up the shed ramp, the
bridge to June's sill, the cabin on the treehouse platform and the cloth clue.

Frames
  ShedRamp        x = world X + 9.5, y = world Z - 12 (the maple's origin, yaw 0): ramp leg 1 north along the west
                  side (1:5), the low-shed landing, leg 2 east (1:5) onto the tall shed roof at 2.4 m, which meets
                  the east edge of the treehouse platform and carries the bridge head.
  SillBridge      span along X from the shed roof (2.40 m) to June's sill (2.50 m), origin under mid-span.
  TreehouseCabin  the maple's own frame (as `TreehousePlatform`, yaw 200 in game).
  ClothSnag       origin at the root of the splinter, the scrap hanging toward -Y.

The skilled ascent reuses the approved `Crate` (0.6 m) and `Awning` (1.35 -> 2.1 m) against the shed's south
face; their sizes fit a 1.0 m jump."""
import math

from mathutils import Vector

import env_lib as el
import interior as it
import porch_kit as pk
import props as pr
from env_lib import E, R, S, T, Builder, H, catenary, lc, shade
from interior import beam, face

MAPLE_YAW = 200.0  # Unity yaw of the maple / treehouse platform
ZT, HALF = 2.4, 1.3  # treehouse deck
PLAT = Vector((0.05 * ZT + 0.015 * ZT * ZT, -0.02 * ZT))  # platform centre in the maple frame (trunk lean)
W = 1.9  # ramp width (1.66 m clear between the guard rails)
L1 = (-3.9, -2.0, -4.6, 3.0)  # leg 1: x0, x1, y0 (ground), y1 (top)
LAND_Z = (L1[3] - L1[2]) / 5.0  # 1.52
L2_X1 = L1[1] + (ZT - LAND_Z) * 5.0  # 2.4
NORTH = L1[3] + W  # 4.7
ROOF_E, ROOF_S, ROOF_W = 3.75, -0.45, 1.55
BRIDGE_Y, BRIDGE_W, BRIDGE_L = 2.0, 0.8, 2.4
SILL_Z = 2.5


def maple_to_ramp(p):
    """Maple-frame plan point -> ShedRamp frame (the maple stands at this frame's origin, turned by its yaw)."""
    a = math.radians(MAPLE_YAW)
    return Vector((p[0] * math.cos(a) + p[1] * math.sin(a), -p[0] * math.sin(a) + p[1] * math.cos(a)))


EDGE_S = maple_to_ramp(PLAT + Vector((-HALF, HALF)))  # the platform's open east edge, south and north ends
EDGE_N = maple_to_ramp(PLAT + Vector((-HALF, -HALF)))


def edge_x(y, gap=0.03):
    t = (y - EDGE_S.y) / (EDGE_N.y - EDGE_S.y)
    return EDGE_S.x + (EDGE_N.x - EDGE_S.x) * t + gap


def woods(craft):
    P = it.paints(craft)
    P.update(
        plank_x=craft.timber(E["timber"], "X", wear=0.5, bare=E["timber_dark"]),
        plank_y=craft.timber(E["timber"], "Y", wear=0.5, bare=E["timber_dark"]),
        plank_z=craft.timber(E["timber"], "Z", wear=0.5, bare=E["timber_dark"]),
        dark_x=craft.timber(E["timber_dark"], "X", wear=0.3),
        dark_y=craft.timber(E["timber_dark"], "Y", wear=0.3),
        dark_z=craft.timber(E["timber_dark"], "Z", wear=0.3),
    )
    return P


def deck(b, mat, x0, x1, y0, y1, z0, z1, along="y", plank=0.2, thick=0.05):
    """Planks across the direction of travel (`along`), the top surface running from z0 to z1."""
    a0, a1 = (y0, y1) if along == "y" else (x0, x1)
    n = max(1, round((a1 - a0) / plank))
    ang = math.degrees(math.atan2(z1 - z0, a1 - a0))
    for i in range(n):
        t = (i + 0.5) / n
        c, z = a0 + (a1 - a0) * t, z0 + (z1 - z0) * t - thick / 2
        w = (a1 - a0) / n / math.cos(math.radians(ang)) - 0.012
        if along == "y":
            b.box(mat, (x1 - x0 + b.rng.uniform(0.0, 0.05), w, thick), ((x0 + x1) / 2, c, z), bevel=0.01, rot=R("X", ang), tint=b.vary(0.78, 1.12))
        else:
            b.box(mat, (w, y1 - y0 + b.rng.uniform(0.0, 0.05), thick), (c, (y0 + y1) / 2, z), bevel=0.01, rot=R("Y", -ang), tint=b.vary(0.78, 1.12))


def skirt(b, mat, p0, p1, zt0, zt1, out, step=0.2, zb=0.0, drop=0.05, var=(0.74, 1.05)):
    """Boards closing the side of a ramp or shed from the ground to just under the deck: plan p0 -> p1, top
    running zt0 -> zt1, seen from the `out` side."""
    p0, p1 = Vector(p0), Vector(p1)
    n = max(1, round((p1 - p0).length / step))
    for i in range(n):
        a, c = p0.lerp(p1, i / n), p0.lerp(p1, (i + 1) / n)
        za, zc = zt0 + (zt1 - zt0) * i / n - drop, zt0 + (zt1 - zt0) * (i + 1) / n - drop
        if max(za, zc) - zb < 0.03:
            continue
        m = (a + c) / 2
        face(b, mat, [(a.x, a.y, zb), (c.x, c.y, zb), (c.x, c.y, max(zb, zc)), (a.x, a.y, max(zb, za))], (m.x + out[0], m.y + out[1], 0.3), b.vary(*var))


def siding(b, mat, p0, p1, z1, out, course=0.2, var=(0.8, 1.1)):
    """Clapboard courses as flat faces (the solid shed)."""
    p0, p1 = Vector(p0), Vector(p1)
    n = round(z1 / course)
    for i in range(n):
        za, zb = z1 * i / n, z1 * (i + 1) / n
        cuts = [0.0, 1.0] if (p1 - p0).length < 2.2 else [0.0, b.rng.uniform(0.3, 0.7), 1.0]
        for ta, tb in zip(cuts, cuts[1:]):
            a, c = p0.lerp(p1, ta), p0.lerp(p1, tb)
            m = (a + c) / 2
            face(b, mat, [(a.x, a.y, za), (c.x, c.y, za), (c.x, c.y, zb), (a.x, a.y, zb)], (m.x + out[0], m.y + out[1], za), b.vary(*var))


def fence(b, P, pts, skip=()):
    """Post-and-rail guard along a polyline of deck-level points: a post at every point, handrail and mid rail
    between them. Segments whose index is in `skip` stay open (the bridge head)."""
    pts = [Vector(p) for p in pts]
    for i, p in enumerate(pts):
        b.box(P["trim_z"], (0.1, 0.1, 0.98), (p.x, p.y, p.z + 0.45), bevel=0.018)
        b.lathe(P["trim_z"], [(0.0, 0.0), (0.062, 0.0), (0.072, 0.03), (0.04, 0.075), (0.0, 0.09)], 8, T(p.x, p.y, p.z + 0.94))
        b.box(P["orange"], (0.115, 0.115, 0.03), (p.x, p.y, p.z + 0.8), bevel=0.008, tint=1.0)
    for i, (p, q) in enumerate(zip(pts, pts[1:])):
        if i in skip:
            continue
        up = Vector((0, 0, 1))
        beam(b, P["trim_x"], p + up * 0.86, q + up * 0.86, 0.075, 0.055, 0.014)
        beam(b, P["trim_x"], p + up * 0.45, q + up * 0.45, 0.05, 0.07, 0.012, 0.95)


def run(p0, p1, every=1.5):
    """Points from p0 to p1 (exclusive of p1) no further apart than `every`."""
    p0, p1 = Vector(p0), Vector(p1)
    n = max(1, math.ceil((p1 - p0).length / every))
    return [p0.lerp(p1, i / n) for i in range(n)]


def plank_door(b, P, w, h, M):
    with b.at(M):
        n = 4
        for i in range(n):
            b.box(P["door"], (w / n - 0.008, 0.035, h), (-w / 2 + (i + 0.5) * w / n, -0.02, h / 2), bevel=0.01, tint=b.vary(0.85, 1.1))
        for z in (0.2, h - 0.2):
            b.box(P["door_x"], (w - 0.06, 0.03, 0.11), (0, -0.048, z), bevel=0.012, tint=1.1)
        beam(b, P["door_x"], (-w / 2 + 0.06, -0.048, 0.26), (w / 2 - 0.06, -0.048, h - 0.26), 0.03, 0.1, 0.012, 1.1)
        for sx in (-1, 1):
            b.box(P["trim_z"], (0.09, 0.07, h + 0.09), (sx * (w / 2 + 0.045), -0.03, h / 2 + 0.045), bevel=0.016)
        b.box(P["trim_x"], (w + 0.3, 0.09, 0.09), (0, -0.035, h + 0.045), bevel=0.016)
        b.lathe(P["brass"], [(0.0, 0.0), (0.02, 0.0), (0.032, 0.03), (0.0, 0.05)], 8, T(w / 2 - 0.1, -0.06, h * 0.5) @ R("X", 90), tint=1.0)


def build_shed_ramp(craft):
    P = woods(craft)
    b = Builder(221)
    x0, x1, y0, y1 = L1
    # ----- decks: leg 1, landing on the low shed, leg 2, the tall shed roof -----
    deck(b, P["plank_x"], x0, x1, y0, y1, 0.0, LAND_Z, "y")
    deck(b, P["plank_y"], x0, x1, y1, NORTH, LAND_Z, LAND_Z, "x")
    deck(b, P["plank_y"], x1, L2_X1, y1, NORTH, LAND_Z, ZT, "x")
    n = round((NORTH - ROOF_S) / 0.2)
    for i in range(n):  # roof boards run east-west, each cut to the outline
        ya, yb = ROOF_S + (NORTH - ROOF_S) * i / n, ROOF_S + (NORTH - ROOF_S) * (i + 1) / n
        ym = (ya + yb) / 2
        xa = L2_X1 if ym > y1 else ROOF_W if ym > EDGE_N.y + 0.05 else edge_x(ym, 0.06)
        b.box(P["plank_x"], (ROOF_E - xa + b.rng.uniform(0.0, 0.04), yb - ya - 0.012, 0.05), ((ROOF_E + xa) / 2, ym, ZT - 0.025), bevel=0.01,
              tint=b.vary(0.78, 1.12))
    beam(b, P["trim_y"], (edge_x(ROOF_S, 0.05), ROOF_S, ZT - 0.03), (EDGE_N.x + 0.05, EDGE_N.y + 0.05, ZT - 0.03), 0.1, 0.07, 0.014)  # threshold to the platform
    b.box(P["trim_x"], (W + 0.1, 0.16, 0.05), ((x0 + x1) / 2, y0 - 0.04, 0.012), bevel=0.012, rot=R("X", 11.3))  # toe board at the foot
    # ----- sides: board skirts under the ramps, clapboard on the two sheds -----
    wall_v, clap = P["wall_v"], P["clap"]
    skirt(b, wall_v, (x0, y0), (x0, y1), 0.0, LAND_Z, (-1, 0))
    skirt(b, wall_v, (x1, y0), (x1, y1), 0.0, LAND_Z, (1, 0))
    skirt(b, wall_v, (x1, y1), (ROOF_W, y1), LAND_Z, LAND_Z + (ROOF_W - x1) / 5.0, (0, -1))
    skirt(b, wall_v, (x1, NORTH), (L2_X1, NORTH), LAND_Z, ZT, (0, 1))
    lz, tz = LAND_Z - 0.05, ZT - 0.05
    siding(b, clap, (x0, y1), (x0, NORTH), lz, (-1, 0))
    siding(b, clap, (x0, NORTH), (x1, NORTH), lz, (0, 1))
    siding(b, clap, (L2_X1, NORTH), (ROOF_E, NORTH), tz, (0, 1))
    siding(b, clap, (ROOF_E, ROOF_S), (ROOF_E, NORTH), tz, (1, 0))
    sw = edge_x(ROOF_S)
    siding(b, clap, (sw, ROOF_S), (ROOF_E, ROOF_S), tz, (0, -1))
    siding(b, clap, (ROOF_W, EDGE_N.y + 0.05), (ROOF_W, y1), tz, (-1, 0))
    siding(b, clap, (sw, ROOF_S), (EDGE_N.x + 0.03, EDGE_N.y + 0.05), tz, (-1, 0.3))
    face(b, clap, [(EDGE_N.x + 0.03, EDGE_N.y + 0.05, 0), (ROOF_W, EDGE_N.y + 0.05, 0), (ROOF_W, EDGE_N.y + 0.05, tz), (EDGE_N.x + 0.03, EDGE_N.y + 0.05, tz)],
         (1.5, 0.0, 1.0), 0.85)
    # fascias along every deck edge (the plank edges that guide the eye) and corner boards
    fz = -0.06
    for p, q in (((x0 - 0.02, y0, 0.0), (x0 - 0.02, y1, LAND_Z)), ((x1 + 0.02, y0, 0.0), (x1 + 0.02, y1, LAND_Z)),
                 ((x0 - 0.02, y1, LAND_Z), (x0 - 0.02, NORTH + 0.02, LAND_Z)), ((x0 - 0.02, NORTH + 0.02, LAND_Z), (x1, NORTH + 0.02, LAND_Z)),
                 ((x1, NORTH + 0.02, LAND_Z), (L2_X1, NORTH + 0.02, ZT)), ((L2_X1, NORTH + 0.02, ZT), (ROOF_E + 0.02, NORTH + 0.02, ZT)),
                 ((ROOF_E + 0.02, NORTH + 0.02, ZT), (ROOF_E + 0.02, ROOF_S - 0.02, ZT)), ((ROOF_E + 0.02, ROOF_S - 0.02, ZT), (sw, ROOF_S - 0.02, ZT)),
                 ((x1, y1 - 0.02, LAND_Z), (ROOF_W - 0.02, y1 - 0.02, LAND_Z + (ROOF_W - x1) / 5.0)),
                 ((ROOF_W - 0.02, y1, ZT), (ROOF_W - 0.02, EDGE_N.y + 0.05, ZT))):
        beam(b, P["trim_x"], Vector(p) + Vector((0, 0, fz)), Vector(q) + Vector((0, 0, fz)), 0.05, 0.16, 0.014)
    for x, y, z in ((x0, y1, lz), (x0, NORTH, lz), (x1, NORTH, lz), (L2_X1, NORTH, tz), (ROOF_E, NORTH, tz), (ROOF_E, ROOF_S, tz), (sw, ROOF_S, tz),
                    (ROOF_W, y1, tz)):
        b.box(P["trim_z"], (0.13, 0.13, z - 0.1), (x, y, (z - 0.1) / 2), bevel=0.016)
    # shed door and lit window on the east face, a hatch on the low shed
    plank_door(b, P, 0.8, 1.55, T(ROOF_E, 3.55, 0) @ R("Z", 90))
    b.glow_tag = "ShedWindow"
    with b.at(T(ROOF_E, 0.55, 0.25) @ R("Z", 90)):
        pk.window(b, P, lit=P["paper_dim"])
    b.glow_tag = None
    plank_door(b, P, 0.7, 1.05, T(x0, (y1 + NORTH) / 2, 0) @ R("Z", -90))
    # ----- guard rails; the east edge stays open at the bridge head -----
    g = 0.07
    west = run((x0 + g, y0 + 0.1, 0.02), (x0 + g, y1, LAND_Z)) + [Vector((x0 + g, y1, LAND_Z)), Vector((x0 + g, NORTH - g, LAND_Z))]
    north = run((x0 + g, NORTH - g, LAND_Z), (x1, NORTH - g, LAND_Z))[1:] + run((x1, NORTH - g, LAND_Z), (L2_X1, NORTH - g, ZT)) + \
        [Vector((L2_X1, NORTH - g, ZT)), Vector((ROOF_E - g, NORTH - g, ZT))]
    fence(b, P, west + north)
    by0, by1 = BRIDGE_Y - BRIDGE_W / 2 - 0.1, BRIDGE_Y + BRIDGE_W / 2 + 0.1
    fence(b, P, [(ROOF_E - g, NORTH - g, ZT), (ROOF_E - g, by1 + 1.1, ZT), (ROOF_E - g, by1, ZT), (ROOF_E - g, by0, ZT), (ROOF_E - g, by0 - 1.0, ZT),
                 (ROOF_E - g, ROOF_S + 0.12, ZT)], skip=(2,))
    fence(b, P, run((x1 - g, y0 + 0.1, 0.02), (x1 - g, y1 - 0.1, LAND_Z - 0.02)) + [Vector((x1 - g, y1 - 0.1, LAND_Z - 0.02))])
    south = run((x1 + 0.1, y1 + g, LAND_Z + 0.02), (ROOF_W + g, y1 + g, LAND_Z + (ROOF_W + g - x1) / 5.0))
    fence(b, P, south + [Vector((ROOF_W + g, y1 + g, ZT)), Vector((ROOF_W + g, 1.95, ZT)), Vector((ROOF_W + g, EDGE_N.y + 0.2, ZT))])
    # dressing: bunting along the long rail, pumpkins at the turns, a lantern post at the bridge head
    pr.bunting(b, craft, (x0 + g, y0 + 0.3, 0.9), (x0 + g, y1, LAND_Z + 0.84), 0.2, 20, 0.2, 0.26)
    pr.pumpkin(b, craft, 0.2, 0.8, T(x0 + 0.4, NORTH - 0.4, LAND_Z) @ R("Z", 200), style=2, per_lobe=2)
    pr.pumpkin(b, craft, 0.14, 0.85, T(ROOF_E - 0.4, NORTH - 0.45, ZT), style=None, rgb=H(0xF0E3C4), per_lobe=2)
    pr.pumpkin(b, craft, 0.17, 0.8, T(x1 + 0.5, y0 + 0.2, 0.0) @ R("Z", 160), style=0, per_lobe=2)
    px, py = ROOF_E - g, by1
    b.box(P["trim_z"], (0.08, 0.08, 1.1), (px, py, ZT + 1.45), bevel=0.014)
    beam(b, P["trim_y"], (px, py, ZT + 1.95), (px, py - 0.5, ZT + 1.95), 0.06, 0.06, 0.012)
    with b.at(T(px, py - 0.45, ZT + 1.92)):
        pr.hanging_lamp(b, craft, 0.12, 0.8, 2.4)
    b.finish("ShedRamp")


def build_sill_bridge(craft):
    """Short plank bridge from the shed roof to June's sill: 2.4 m span, 0.8 m wide, rising 0.1 m."""
    P = woods(craft)
    b = Builder(222)
    hl, hw = BRIDGE_L / 2, BRIDGE_W / 2
    z0, z1 = ZT, SILL_Z
    ang = math.degrees(math.atan2(z1 - z0, BRIDGE_L))
    for sy in (-1, 1):
        beam(b, P["dark_x"], (-hl, sy * (hw - 0.14), z0 - 0.115), (hl - 0.02, sy * (hw - 0.14), z1 - 0.115), 0.09, 0.13, 0.016)
        beam(b, P["dark_x"], (-hl - 0.02, sy * (hw - 0.14), z0 - 1.0), (-0.25, sy * (hw - 0.14), z0 - 0.17), 0.07, 0.08, 0.014)  # knee brace to the shed
    b.box(P["dark_y"], (0.07, BRIDGE_W - 0.1, 0.16), (-hl + 0.035, 0, z0 - 0.13), bevel=0.014)
    n = round(BRIDGE_L / 0.2)
    for i in range(n):
        t = (i + 0.5) / n
        b.box(P["plank_y"], (BRIDGE_L / n - 0.012, BRIDGE_W + b.rng.uniform(-0.03, 0.05), 0.04), (-hl + BRIDGE_L * t, b.rng.uniform(-0.012, 0.012),
              z0 + (z1 - z0) * t - 0.02), bevel=0.01, rot=R("Y", -ang) @ R("Z", b.rng.uniform(-1.4, 1.4)), tint=b.vary(0.78, 1.12))
    rope = lc(H(0xC9A66B))
    for sy in (-1, 1):
        posts = []
        for x in (-hl + 0.12, hl - 0.5):
            z = z0 + (z1 - z0) * (x + hl) / BRIDGE_L
            b.box(P["dark_z"], (0.08, 0.08, 0.86), (x, sy * (hw + 0.02), z + 0.3), bevel=0.014)
            b.lathe(P["dark_z"], [(0.05, 0.0), (0.058, 0.02), (0.03, 0.06), (0.0, 0.075)], 4, T(x, sy * (hw + 0.02), z + 0.73) @ R("Z", 45))
            posts.append(Vector((x, sy * (hw + 0.02), z + 0.62)))
        pts = catenary(posts[0], posts[1], 0.14, 8)
        for p, q in zip(pts, pts[1:]):
            b.rod(P["paint"], p, q, 0.016, 6, rope)
        for p in posts:
            b.lathe(P["paint"], [(0.047, -0.03), (0.06, 0.0), (0.047, 0.03)], 8, T(*p), tint=shade(rope, 0.8))
    pr.bat(b, P["paper"], 0.2, T(-hl + 0.12, -hw - 0.03, z0 + 0.5) @ R("Y", 12), fold=0.3, curl=0.3)
    b.finish("SillBridge")


def build_cabin(craft):
    """Small plank cabin on the treehouse platform: board walls with a window on the two railed sides, a
    lookout parapet toward the yard, a door toward the shed roof, and a shingled roof split at the ridge for
    the boughs."""
    P = woods(craft)
    b = Builder(223)
    h, eave, t = 1.12, 1.45, 0.03
    door = (-1.02, -0.12, 1.3)  # y0, y1, height of the opening in the east wall

    def board(axis, at, lo, hi, z0, z1, mat=None):
        c = (lo + hi) / 2
        size = (hi - lo - 0.01, t, z1 - z0) if axis == "x" else (t, hi - lo - 0.01, z1 - z0)
        pos = (c, at, (z0 + z1) / 2) if axis == "x" else (at, c, (z0 + z1) / 2)
        b.box(mat or P["plank_z"], size, pos, bevel=0.008, tint=b.vary(0.76, 1.12))

    def boards(axis, at, lo, hi, z0, z1, gap=None):
        n = round((hi - lo) / 0.19)
        for i in range(n):
            a, c = lo + (hi - lo) * i / n, lo + (hi - lo) * (i + 1) / n
            if gap and gap[0] < (a + c) / 2 < gap[1]:
                board(axis, at, a, c, z0, gap[2])
                if gap[3] < z1:
                    board(axis, at, a, c, gap[3], z1)
            else:
                board(axis, at, a, c, z0, z1 + b.rng.uniform(-0.0, 0.0))

    with b.at(T(PLAT.x, PLAT.y, ZT)):
        for sx in (-1, 1):
            for sy in (-1, 1):
                b.box(P["dark_z"], (0.1, 0.1, eave), (sx * h, sy * h, eave / 2), bevel=0.018)
            b.box(P["dark_y"], (0.1, 2 * h + 0.3, 0.1), (sx * h, 0, eave + 0.03), bevel=0.016)
            b.box(P["dark_x"], (2 * h + 0.3, 0.1, 0.1), (0, sx * h, eave + 0.03), bevel=0.016)
        win = (-0.3, 0.3, 0.62, 1.12)
        boards("x", -h, -h + 0.06, h - 0.06, 0.0, eave - 0.02, win)  # north wall (railed side)
        boards("y", h, -h + 0.06, h - 0.06, 0.0, eave - 0.02, win)  # west wall (railed side)
        boards("x", h, -h + 0.06, h - 0.06, 0.0, 0.72)  # lookout parapet toward the yard
        b.box(P["trim_x"], (2 * h - 0.1, 0.12, 0.045), (0, h, 0.745), bevel=0.014)
        boards("y", -h, door[1] + 0.02, h - 0.06, 0.0, eave - 0.02)  # east wall beside the door
        boards("y", -h, -h + 0.06, door[0] - 0.02, 0.0, eave - 0.02)
        b.box(P["plank_y"], (t, door[1] - door[0] + 0.04, eave - 0.02 - door[2]), (-h, (door[0] + door[1]) / 2, (eave - 0.02 + door[2]) / 2), bevel=0.008)
        for y in (door[0], door[1]):
            b.box(P["trim_z"], (0.06, 0.07, door[2]), (-h - 0.01, y, door[2] / 2), bevel=0.014)
        b.box(P["trim_y"], (0.06, door[1] - door[0] + 0.2, 0.08), (-h - 0.01, (door[0] + door[1]) / 2, door[2] + 0.04), bevel=0.014)
        for axis, at, sgn in (("x", -h, -1), ("y", h, 1)):  # window frames
            for z in (win[2] - 0.03, win[3] + 0.03):
                size = (0.72, 0.06, 0.06) if axis == "x" else (0.06, 0.72, 0.06)
                b.box(P["trim_x"] if axis == "x" else P["trim_y"], size, (0, at, z) if axis == "x" else (at, 0, z), bevel=0.014)
            for s in (-1, 1):
                size = (0.06, 0.06, 0.5) if axis == "x" else (0.06, 0.06, 0.5)
                b.box(P["trim_z"], size, (s * 0.33, at, 0.87) if axis == "x" else (at, s * 0.33, 0.87), bevel=0.014)
        # roof: two shingled panels, open at the ridge where the boughs leave
        pitch, ez = 33.0, eave - 0.1
        ln = (h + 0.3 - 0.3) / math.cos(math.radians(pitch))
        pk.roof(b, P, 2.9, ln, pitch, T(0, -h - 0.3, ez))
        pk.roof(b, P, 2.9, ln, pitch, T(0, h + 0.3, ez) @ R("Z", 180))
        rz = ez + (h + 0.0) * math.tan(math.radians(pitch))
        for sx in (-1, 1):  # gable frames: rafters pair and a star
            for sy in (-1, 1):
                beam(b, P["trim_x"], (sx * 1.46, sy * (h + 0.3), ez - 0.04), (sx * 1.46, sy * 0.3, rz + 0.02), 0.05, 0.14, 0.014)
            b.box(P["trim_y"], (0.05, 0.66, 0.1), (sx * 1.46, 0, rz + 0.02), bevel=0.014)
            b.prism(P["candy"], el.co.star_outline(0.13), 0.03, T(sx * 1.49, 0, rz + 0.03) @ R("Z", 90 * sx) @ T(0, -0.015, 0), tint=1.0, bevel=0.006)
        # inside and around: a box seat, paper bats on the wall, bunting on the parapet, a lantern by the door
        it.crate(b, P, (0.5, 0.36, 0.3), T(0.72, -0.82, 0) @ R("Z", 8))
        b.lathe(P["felt_v"], [(0.0, 0.3), (0.16, 0.3), (0.17, 0.33), (0.1, 0.36), (0.0, 0.365)], 10, T(0.72, -0.82, 0) @ S(1.2, 0.9, 1.0), tint=1.0)
        pr.bat(b, P["paper"], 0.26, T(-0.62, -h + 0.025, 1.0) @ R("Z", 180) @ R("Y", 10), fold=0.2, curl=0.25)
        pr.bat(b, P["paper"], 0.2, T(0.7, -h + 0.025, 1.12) @ R("Z", 180) @ R("Y", -14), fold=0.2, curl=0.25)
        pr.bunting(b, craft, (-h, h + 0.05, 0.7), (h, h + 0.05, 0.7), 0.12, 8, 0.18, 0.22, start=1)
        beam(b, P["dark_y"], (-h - 0.02, -h + 0.02, eave - 0.12), (-h - 0.42, -h + 0.02, eave - 0.12), 0.05, 0.05, 0.012)
        with b.at(T(-h - 0.36, -h + 0.02, eave - 0.15)):
            pr.hanging_lamp(b, craft, 0.1, 0.75, 2.4)
    b.finish("TreehouseCabin")


def build_cloth_snag(craft):
    b = Builder(224)
    it.cloth_snag(b, craft, T(0, 0, 0))
    b.finish("ClothSnag")
