"""Inside Aunt Kit's owl house (spec §2-§4): the interior shell that fits inside the approved exterior
(`owl_house.py`, same builder space: body x -3..3, y 0..6, front wall at y = 0, porch deck at z = 0.6), and the
workshop mechanism behind the grille.

OwlHouseInterior
  Foyer (z = 0.6): entry bay open to the roof under the round window, the inside of the ticket-booth door,
  the stair along the back wall (10 risers of 0.19 m, 1.0 m wide), and the workshop partition on the east side
  with the low grille and the opening for the service panel.
  Loft / attic (z = 2.5, the level of June's sill): from y = 1.5 back, railed toward the entry bay and round
  the stairwell, with the dormer alcove on the west side.
  Every wall is a single inward face, so a camera outside the shell sees in through the culled near walls.

WorkshopMechanism (own frame: front toward -Y, floor of the nook at z = 0, foyer floor at z = 0.5)
  Cam + shaft, Follower, LinkArm_Slipped / LinkArm_Seated, ServicePanel, Crank, KitPhoto.

Game meshes only (exported by `env_export_interior.py`, reviewed from the FBX by `env_interior_review.py`)."""
import math

import bmesh
from mathutils import Matrix, Vector

import env_lib as el
import porch_kit as pk
import props as pr
from env_lib import E, R, S, T, Builder, H, arc, blend, lc, shade

FL = pk.FLOOR
X0, X1, Y0, Y1 = -2.86, 2.86, 0.14, 5.86
LOFT, LOFT_T, VOID_Y = 2.5, 0.16, 1.5
UNDER = LOFT - LOFT_T  # foyer ceiling
KNEE, APEX = 2.86, 6.27
SLOPE = (APEX - KNEE) / X1
STAIR_Y, STAIR_X, RISERS, RISE, TREAD = 4.86, -1.72, 10, 0.19, 0.28
STAIR_TOP = STAIR_X + (RISERS - 1) * TREAD  # 0.8
HOLE = (STAIR_X + TREAD, STAIR_TOP, STAIR_Y, Y1)  # stairwell in the loft floor
NOOK_X, NOOK_Y, NOOK_Z, PART_T = 1.45, 1.75, 0.1, 0.1
MECH_Y = 3.1
MECH = T(NOOK_X, MECH_Y, NOOK_Z) @ R("Z", -90)  # mechanism frame in house space
MECH_FLOOR = FL - NOOK_Z  # foyer floor in the mechanism frame (0.5)
GRILLE = (-0.85, 0.85, 0.58, 1.0)  # x0, x1, z0, z1 in the mechanism frame
PANEL = (-0.25, 0.65, 1.08, 1.95)
ALC_Y0, ALC_Y1, ALC_EAVE, ALC_RIDGE = 2.3, 3.7, 3.9, 4.5
SILL_Z = 2.5
DOOR_HINGE = T(-0.55, -0.015, FL)  # the exterior FrontDoor pivot
SILVER = H(0xDAE2EE)


def ceil_z(x):
    return KNEE + (X1 - abs(x)) * SLOPE


def ceil_x(z):
    return X1 - (z - KNEE) / SLOPE


# --- small geometry helpers --------------------------------------------------------------------------------

def face(b, mat, pts, toward, tint=None):
    """One flat single-sided polygon whose front looks at `toward`."""
    bm = bmesh.new()
    f = bm.faces.new([bm.verts.new(p) for p in pts])
    bm.normal_update()
    if f.normal.dot(Vector(toward) - f.calc_center_median()) < 0:
        bmesh.ops.reverse_faces(bm, faces=[f])
    b.add_bm(bm, mat, None, tint, smooth=False)


def grid_wall(b, bands, x0, x1, holes=(), y=0.0, facing=-1):
    """Vertical wall in the local XZ plane at `y`, seen from the `facing` side of Y. `bands` are
    (z0, z1, material, plank width or None, (lo, hi) tint range); `holes` are (x0, x1, z0, z1)."""
    for z0, z1, mat, step, var in bands:
        zs = sorted({z0, z1} | {h[k] for h in holes for k in (2, 3) if z0 < h[k] < z1})
        xs = sorted({x0, x1} | {h[k] for h in holes for k in (0, 1) if x0 < h[k] < x1})
        for za, zb in zip(zs, zs[1:]):
            for xa, xb in zip(xs, xs[1:]):
                cx, cz = (xa + xb) / 2, (za + zb) / 2
                if any(h[0] < cx < h[1] and h[2] < cz < h[3] for h in holes):
                    continue
                n = max(1, round((xb - xa) / step)) if step else 1
                for i in range(n):
                    a, c = xa + (xb - xa) * i / n, xa + (xb - xa) * (i + 1) / n
                    face(b, mat, [(a, y, za), (c, y, za), (c, y, zb), (a, y, zb)], ((a + c) / 2, y + facing, cz), b.vary(*var))


def reveal(b, mat, hole, y0, y1, tint=0.8):
    """The four inside faces of a wall opening between y0 and y1."""
    xa, xb, za, zb = hole
    cx, cz, cy = (xa + xb) / 2, (za + zb) / 2, (y0 + y1) / 2
    for pts in ([(xa, y0, za), (xa, y1, za), (xa, y1, zb), (xa, y0, zb)], [(xb, y0, za), (xb, y1, za), (xb, y1, zb), (xb, y0, zb)],
                [(xa, y0, za), (xb, y0, za), (xb, y1, za), (xa, y1, za)], [(xa, y0, zb), (xb, y0, zb), (xb, y1, zb), (xa, y1, zb)]):
        face(b, mat, pts, (cx, cy, cz), tint)


def planks(b, mat, x0, x1, y0, y1, z, width=0.2, up=True, joints=True, var=(0.82, 1.1)):
    """Floor boards running along Y as single faces (top or underside), with staggered butt joints."""
    n = max(1, round((x1 - x0) / width))
    for i in range(n):
        a, c = x0 + (x1 - x0) * i / n, x0 + (x1 - x0) * (i + 1) / n
        cuts = [y0, y1]
        if joints and y1 - y0 > 2.4:
            cuts = [y0, b.rng.uniform(y0 + 0.8, y1 - 0.8), y1]
        for ya, yb in zip(cuts, cuts[1:]):
            face(b, mat, [(a, ya, z), (c, ya, z), (c, yb, z), (a, yb, z)], ((a + c) / 2, (ya + yb) / 2, z + (1 if up else -1)), b.vary(*var))


def beam(b, mat, p0, p1, w, h, bevel=0.014, tint=None):
    """Timber from p0 to p1 with a w (sideways) x h (up) section."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    b.box(mat, (d.length, w, h), tuple((p0 + p1) / 2), bevel=bevel, rot=d.to_track_quat("X", "Z").to_matrix().to_4x4(), tint=tint)


def paints(craft):
    P = pk.paints(craft)
    P.update(
        floor=craft.timber(E["timber"], "Y", wear=0.45, bare=E["timber_dark"]),
        floor_x=craft.timber(E["timber"], "X", wear=0.45, bare=E["timber_dark"]),
        wains=craft.timber(E["plum"], "Z", wear=0.25, bare=E["timber_dark"]),
        upper=craft.tinted(blend(E["purple"], E["bone"], 0.2), 0.9, 0.16, 2.5),
        board_y=craft.timber(H(0x8E5C3C), "Y", wear=0.2, bare=E["timber_dark"]),
        board_x=craft.timber(H(0x8E5C3C), "X", wear=0.2, bare=E["timber_dark"]),
        rafter=craft.timber(E["timber_dark"], "X", wear=0.25),
        rafter_y=craft.timber(E["timber_dark"], "Y", wear=0.25),
        post=craft.timber(E["timber_dark"], "Z", wear=0.25),
        dark=craft.tinted(H(0x2B2038), 0.9, 0.14, 4.0),
        night=craft.tinted(H(0x2B3766), 0.3, 0.2, 3.0),
        cloth=craft.tinted(E["plum"], 0.9, 0.2, 14.0, sheen=0.4),
        silver=craft.tinted(SILVER, 0.6, 0.1, 60.0, sheen=0.5),
        felt=craft.felt(el.V["orange"]),
        felt_v=craft.felt(el.V["violet"]),
        paint=craft.paint(),
        paper=craft.paper_white(),
    )
    return P


def glow(b, craft, tag, rgb, strength=2.0):
    b.glow_tag = tag
    return craft.glow(rgb, strength)


# --- furniture and dressing --------------------------------------------------------------------------------

def inner_window(b, P, w=0.8, h=1.2, z0=0.75, curtains=True):
    """Window seen from the room: local wall plane y = 0, room toward -Y, z = 0 at the floor."""
    zc = z0 + h / 2
    b.box(P["night"], (w, 0.02, h), (0, -0.005, zc), bevel=0, tint=1.0, drop=("+y",))
    for sx in (-1, 1):
        b.box(P["trim_z"], (0.09, 0.06, h + 0.09), (sx * (w / 2 + 0.045), -0.03, zc + 0.045), bevel=0.014)
    b.box(P["trim_x"], (w + 0.26, 0.07, 0.09), (0, -0.035, z0 + h + 0.045), bevel=0.014)
    b.box(P["trim_x"], (w + 0.3, 0.15, 0.05), (0, -0.07, z0 - 0.025), bevel=0.016)
    b.box(P["trim_x"], (w + 0.14, 0.05, 0.08), (0, -0.03, z0 - 0.09), bevel=0.012, tint=0.92)
    b.box(P["trim_z"], (0.04, 0.03, h), (0, -0.02, zc), bevel=0.008)
    b.box(P["trim_x"], (w, 0.03, 0.045), (0, -0.02, zc), bevel=0.008)
    if curtains:
        b.rod(P["brass"], (-w / 2 - 0.2, -0.09, z0 + h + 0.13), (w / 2 + 0.2, -0.09, z0 + h + 0.13), 0.014, 6, 1.0)
        for sx in (-1, 1):
            drape = [(0.0, 0.0), (0.3, 0.0), (0.27, -0.5), (0.13, -h * 0.62), (0.1, -h - 0.12), (0.0, -h - 0.16)]
            m = T(sx * (w / 2 + 0.18), -0.075, z0 + h + 0.13) @ (S(-1, 1, 1) if sx > 0 else T())
            b.prism(P["cloth"], drape, 0.03, m, tint=b.vary(0.9, 1.1), bevel=0.01)
            b.box(P["orange"], (0.07, 0.04, 0.035), (sx * (w / 2 + 0.07), -0.085, z0 + h * 0.38), bevel=0.01, tint=1.0)


def frame_art(b, P, w, h, motif, M, back=None):
    """Small framed paper cut-out: wall plane y = 0, room toward -Y, origin at the frame centre."""
    with b.at(M):
        b.box(P["paint"], (w, 0.012, h), (0, -0.012, 0), bevel=0, tint=lc(back or E["night"]), drop=("+y",))
        for sx in (-1, 1):
            b.box(P["trim_z"], (0.035, 0.035, h + 0.07), (sx * (w / 2 + 0.0175), -0.02, 0), bevel=0.01)
            b.box(P["trim_x"], (w, 0.035, 0.035), (0, -0.02, sx * (h / 2 + 0.0175)), bevel=0.01)
        motif(b)


def stool(b, P, M, seat=0.3, r=0.17, cushion=None):
    with b.at(M):
        b.lathe(P["amber"], [(0.0, seat - 0.045), (r, seat - 0.045), (r + 0.012, seat - 0.02), (r, seat), (0.0, seat)], 12)
        for k in range(3):
            a = math.radians(120 * k + 30)
            b.rod(P["amber"], (r * 0.95 * math.cos(a), r * 0.95 * math.sin(a), 0), (r * 0.55 * math.cos(a), r * 0.55 * math.sin(a), seat - 0.04), 0.022, 6)
        if cushion:
            b.lathe(cushion, [(0.0, seat), (r * 0.9, seat), (r * 0.95, seat + 0.03), (r * 0.7, seat + 0.055), (0.0, seat + 0.06)], 12, tint=1.0)


def crate(b, P, size, M, tint=None):
    sx, sy, sz = size
    with b.at(M):
        b.box(P["floor_x"], (sx, sy, sz), (0, 0, sz / 2), bevel=0.014, tint=tint)
        for z in (0.07, sz - 0.07):
            b.box(P["amber"], (sx + 0.03, sy + 0.03, 0.06), (0, 0, z), bevel=0.012)


def owl_lantern(b, craft, P, M):
    """The owl lantern June switches on: a round paper lantern with ring eyes that repeat the attic window."""
    lit = glow(b, craft, "OwlLantern", E["glow"], 3.0)
    with b.at(M):
        n = 7
        body = [(0.0, -0.2)] + [(0.2 * math.sin(math.pi * i / n) ** 0.8, -0.2 * math.cos(math.pi * i / n)) for i in range(1, n)] + [(0.0, 0.2)]
        b.lathe(lit, body, 14, tint=1.0)
        b.lathe(P["ink"], [(0.0, 0.2), (0.09, 0.18), (0.1, 0.2), (0.05, 0.25), (0.0, 0.26)], 10, tint=1.0)
        b.lathe(P["ink"], [(0.0, -0.215), (0.08, -0.2), (0.09, -0.18), (0.1, -0.185), (0.06, -0.23), (0.0, -0.24)], 10, tint=1.0)
        for k in range(6):  # paper ribs
            a = math.radians(60 * k + 30)
            pts = [(0.203 * math.sin(math.pi * i / n) ** 0.8, -0.2 * math.cos(math.pi * i / n)) for i in range(1, n)]
            for (r0, z0), (r1, z1) in zip(pts, pts[1:]):
                b.rod(P["ink"], (r0 * math.cos(a), r0 * math.sin(a), z0), (r1 * math.cos(a), r1 * math.sin(a), z1), 0.006, 4, 1.0)
        for sx in (-1, 1):  # eyes look into the room (+Y) and ear tufts
            with b.at(T(sx * 0.075, 0.185, 0.03) @ R("Z", 180) @ R("Z", -sx * 20)):
                b.ring(P["trim_x"], 0.036, 0.062, 0.02, T(0, -0.02, 0), segs=12, tint=1.0)
                b.disc(P["ink"], 0.024, 0.012, T(0, -0.012, 0), segs=8, tint=1.0)
            b.prism(P["ink"], [(-0.035, 0.0), (0.035, 0.0), (0.0, 0.1)], 0.02, T(sx * 0.07, -0.01, 0.2) @ R("Y", sx * 22), tint=1.0)
        b.lathe(craft.paint(), [(0.0, 0.0), (0.02, 0.0), (0.0, -0.045)], 4, T(0, 0.2, -0.035) @ R("X", -90), tint=lc(E["orange"]))
    b.glow_tag = None


def cloth_snag(b, craft, M, s=1.0):
    """Evidence: a scrap of June's silver cape caught on a splinter. Origin at the root of the splinter, which
    sticks out toward -Y; the scrap hangs below it."""
    silver = craft.tinted(SILVER, 0.6, 0.1, 60.0, sheen=0.5)
    wood = craft.timber(E["bare"], "Y", wear=0.0)
    with b.at(M @ S(s)):
        b.rod(wood, (0, 0.01, 0), (0.008, -0.1, 0.018), 0.014, 4, 1.0, r2=0.002)
        b.rod(wood, (0.02, 0.01, 0.012), (0.035, -0.055, 0.03), 0.009, 4, 0.9, r2=0.001)
        scrap = [(-0.01, 0.01), (0.03, 0.0), (0.085, -0.05), (0.07, -0.12), (0.1, -0.2), (0.035, -0.165), (-0.02, -0.235), (-0.055, -0.13),
                 (-0.085, -0.1), (-0.05, -0.03)]
        b.paper(silver, scrap, T(0.0, -0.075, 0.0) @ R("Z", 14) @ R("X", 8), tint=1.0, fold=0.22, curl=0.5, cuts=1,
                crease=lambda x, z: 0.03 * math.sin(z * 30.0))
        for k in range(3):  # frayed threads
            b.rod(silver, (-0.02 + 0.03 * k, -0.075, -0.19 - 0.02 * (k % 2)), (-0.03 + 0.035 * k, -0.08 - 0.01 * k, -0.27 - 0.015 * k), 0.003, 3, 1.0)


def photo_motif(P, craft):
    """Aunt Kit at her booth as layered paper cut-outs (frame-centred, 0.36 x 0.46)."""
    def draw(b):
        paper, y = P["paper"], [-0.014]

        def cut(outline, rgb, at=(0, 0), k=1.0):
            y[0] -= 0.0035
            b.prism(paper, [(x * k + at[0], z * k + at[1]) for x, z in outline], 0.003, T(0, y[0], 0), tint=lc(rgb))
        circ = lambda r, n=14: [(r * math.cos(2 * math.pi * i / n), r * math.sin(2 * math.pi * i / n)) for i in range(n)]  # noqa: E731
        cut(circ(0.07), E["moon"], (0.1, 0.15))  # moon
        cut([(-0.18, -0.23), (0.18, -0.23), (0.18, -0.06), (-0.18, -0.06)], E["purple"])  # booth front
        for i in range(4):  # booth stripes
            cut([(-0.16 + 0.09 * i, -0.22), (-0.115 + 0.09 * i, -0.22), (-0.115 + 0.09 * i, -0.08), (-0.16 + 0.09 * i, -0.08)], E["orange"])
        cut([(-0.03, -0.04), (0.03, -0.04), (0.11, -0.2), (0.0, -0.17), (-0.11, -0.2)], E["plum"], (-0.04, 0.02))  # shawl
        for i in range(5):  # shawl fringe
            cut([(-0.012, 0.0), (0.012, 0.0), (0.0, -0.035)], E["candy"], (-0.13 + 0.045 * i, -0.165 + (0.012 if i in (1, 3) else 0.0)))
        cut(circ(0.05), H(0xE9B98A), (-0.04, 0.045))  # face
        cut([(-0.058, 0.0), (0.058, 0.0), (0.05, 0.04), (0.0, 0.065), (-0.05, 0.04)], E["orange"], (-0.04, 0.055))  # headscarf
        cut([(0.04, 0.0), (0.1, -0.05), (0.07, 0.02)], E["orange"], (-0.04, 0.06))  # scarf tail
        cut(circ(0.016, 8), E["candy"], (-0.04, -0.04))  # owl pin
        cut([(-0.2, -0.085), (0.2, -0.085), (0.2, -0.055), (-0.2, -0.055)], E["bone"])  # counter
        cut(circ(0.036), E["glow"], (0.1, -0.02))  # crystal-ball lantern
        cut([(-0.03, -0.035), (0.03, -0.035), (0.02, 0.0), (-0.02, 0.0)], E["ink"], (0.1, -0.02 - 0.02))
        cut(el.scallop_strip(0.4, 0.04, 5, 0.03, 3), E["orange"], (-0.2, 0.25))  # scalloped pelmet
        cut(el.co.star_outline(0.02), E["candy"], (-0.14, 0.14))
        cut(el.co.star_outline(0.013), E["candy"], (-0.02, 0.16))
    return draw


# --- the interior shell ------------------------------------------------------------------------------------

def build_interior(craft):
    P = paints(craft)
    b = Builder(201)
    WAINS = FL + 0.85
    LOW, HIGH = (0.82, 1.08), (0.94, 1.04)

    def bands(top):
        return [(FL, WAINS, P["wains"], 0.19, LOW), (WAINS, top, P["upper"], 0.95, HIGH)]

    def cap(length, at, rot=0.0):
        with b.at(T(*at) @ R("Z", rot)):
            b.box(P["trim_x"], (length, 0.05, 0.07), (0, -0.02, WAINS), bevel=0.014, drop=())
            b.box(P["trim_x"], (length, 0.035, 0.12), (0, -0.012, FL + 0.06), bevel=0.012, tint=0.9)

    # ----- ground floor: floor boards, walls with wainscot, door and window units -----
    planks(b, P["floor"], X0, NOOK_X, Y0, Y1, FL)
    planks(b, P["floor"], NOOK_X, X1, Y0, NOOK_Y - PART_T, FL)
    door = (-0.57, 0.57, FL, FL + 2.02)
    with b.at(T(0, Y0, 0) @ R("Z", 180)):  # front wall (the entry bay is open to the roof)
        grid_wall(b, bands(KNEE), X0, X1, holes=(door,))
        n = 12
        for i in range(n):  # gable boards
            za, zb = KNEE + (APEX - KNEE) * i / n, KNEE + (APEX - KNEE) * (i + 1) / n
            xa, xb = ceil_x(za), ceil_x(zb)
            face(b, P["board_x"], [(-xa, 0, za), (xa, 0, za), (xb, 0, zb), (-xb, 0, zb)], (0, -1, za), b.vary(0.8, 1.08))
        b.box(P["trim_x"], (2 * X1, 0.06, 0.1), (0, -0.03, KNEE), bevel=0.016)
        for x0, x1 in ((X0, -0.68), (0.68, X1)):
            cap(x1 - x0, ((x0 + x1) / 2, 0, 0))
        for x in (-2, 2):
            with b.at(T(x, 0, FL)):
                inner_window(b, P)
        for sx in (-1, 1):  # door casing
            b.box(P["trim_z"], (0.11, 0.06, 2.1), (sx * 0.625, -0.03, FL + 1.05), bevel=0.016)
        b.box(P["trim_x"], (1.5, 0.08, 0.12), (0, -0.04, FL + 2.1), bevel=0.018)
        b.box(P["trim_x"], (1.6, 0.11, 0.04), (0, -0.05, FL + 2.18), bevel=0.012, tint=1.05)
        # the round window from inside: lit paper in a fat ring, under a bracket that carries the owl lantern
        wz, wr = 4.3, 0.56
        b.disc(glow(b, craft, "AtticWindowIn", E["glow"], 1.6), wr + 0.02, 0.012, T(0, -0.014, wz), segs=28, tint=1.0)
        b.glow_tag = None
        b.ring(P["trim_x"], wr, wr + 0.16, 0.09, T(0, -0.09, wz), segs=28, tint=1.0)
        b.ring(P["orange"], wr + 0.16, wr + 0.195, 0.05, T(0, -0.05, wz), segs=28, tint=0.95)
        b.box(P["trim_z"], (0.05, 0.035, wr * 2), (0, -0.04, wz), bevel=0.01)
        b.box(P["trim_x"], (wr * 2, 0.035, 0.05), (0, -0.04, wz), bevel=0.01)
        for k in range(4):
            a = math.radians(90 * k)
            b.box(P["trim_x"], (0.12, 0.11, 0.18), (math.sin(a) * (wr + 0.085), -0.055, math.cos(a) * (wr + 0.085)), bevel=0.02,
                  rot=R("Y", 90 * k), tint=1.06)
    reveal(b, P["trim_z"], door, 0.04, Y0, 0.85)
    b.box(P["deck_x"], (1.14, 0.2, 0.03), (0, 0.06, FL - 0.012), bevel=0.01, tint=0.9)
    with b.at(T(X0, 0, 0) @ R("Z", 90)):  # west wall (local x = house y)
        grid_wall(b, bands(UNDER), VOID_Y, Y1)
        grid_wall(b, bands(KNEE), Y0, VOID_Y)
        cap(Y1 - Y0, ((Y0 + Y1) / 2, 0, 0))
        with b.at(T(5.0, 0, FL)):
            inner_window(b, P, h=0.95)
    with b.at(T(X1, 0, 0) @ R("Z", -90)):  # east wall: foyer part (local x = -house y) and inside the nook
        grid_wall(b, bands(KNEE), -(NOOK_Y - PART_T), -Y0)
        cap(NOOK_Y - PART_T - Y0, (-(NOOK_Y - PART_T + Y0) / 2, 0, 0))
        grid_wall(b, [(NOOK_Z, UNDER, P["dark"], 0.3, LOW)], -Y1, -NOOK_Y)
        b.box(P["night"], (0.8, 0.02, 0.95), (-3.0, -0.006, FL + 0.75 + 0.475), bevel=0, tint=1.25, drop=("+y",))
        for sx in (-1, 1):
            b.box(P["post"], (0.07, 0.04, 1.05), (-3.0 + sx * 0.43, -0.02, FL + 1.225), bevel=0.012)
        b.box(P["post"], (0.93, 0.04, 0.07), (-3.0, -0.02, FL + 0.72), bevel=0.012)
        b.box(P["post"], (0.8, 0.03, 0.04), (-3.0, -0.018, FL + 1.225), bevel=0.008)
        b.box(P["post"], (0.04, 0.03, 0.95), (-3.0, -0.018, FL + 1.225), bevel=0.008)
    grid_wall(b, bands(UNDER), X0, NOOK_X, y=Y1)  # back wall
    cap(STAIR_X - 0.1 - X0, ((X0 + STAIR_X - 0.1) / 2, Y1, 0))
    grid_wall(b, [(NOOK_Z, UNDER, P["dark"], 0.3, LOW)], NOOK_X + PART_T, X1, y=Y1)
    # nook: sunk floor, south wall (both faces)
    face(b, P["dark"], [(NOOK_X + PART_T, NOOK_Y, NOOK_Z), (X1, NOOK_Y, NOOK_Z), (X1, Y1, NOOK_Z), (NOOK_X + PART_T, Y1, NOOK_Z)],
         (2.2, 3.0, 1.0), 0.8)
    grid_wall(b, [(NOOK_Z, UNDER, P["dark"], 0.3, LOW)], NOOK_X + PART_T, X1, y=NOOK_Y, facing=1)
    grid_wall(b, bands(UNDER), NOOK_X, X1, y=NOOK_Y - PART_T)
    cap(X1 - NOOK_X, ((X1 + NOOK_X) / 2, NOOK_Y - PART_T, 0))
    grid_wall(b, [(UNDER, LOFT, P["upper"], 0.95, HIGH)], HOLE[0], HOLE[1], y=Y1)  # back wall inside the stairwell
    # ----- stair along the back wall -----
    for k in range(RISERS):
        x, z = STAIR_X + TREAD * k, FL + RISE * k
        face(b, P["wains"], [(x, STAIR_Y, z), (x, Y1, z), (x, Y1, z + RISE - 0.04), (x, STAIR_Y, z + RISE - 0.04)], (x - 1, 5.3, z), b.vary(0.8, 1.0))
        if k:
            b.box(P["floor_x"], (TREAD + 0.035, Y1 - STAIR_Y + 0.02, 0.045), (x - TREAD / 2 - 0.0175, (STAIR_Y + Y1) / 2 - 0.01, z - 0.0225), bevel=0.012,
                  tint=b.vary(0.85, 1.12))
    side = [(STAIR_X, FL)]
    for k in range(RISERS - 1):
        side += [(STAIR_X + TREAD * k, FL + RISE * (k + 1) - 0.045), (STAIR_X + TREAD * (k + 1), FL + RISE * (k + 1) - 0.045)]
    side += [(STAIR_TOP, UNDER), (STAIR_TOP, FL)]
    b.prism(P["wains"], side, 0.04, T(0, STAIR_Y - 0.04, 0), tint=0.92, smooth=False)
    run = Vector((TREAD, 0, RISE)).normalized()
    beam(b, P["trim_x"], Vector((STAIR_X - 0.06, STAIR_Y - 0.055, FL + 0.02)) , Vector((STAIR_X - 0.06, STAIR_Y - 0.055, FL + 0.02)) + run * 2.56, 0.04, 0.2, 0.014)
    face(b, P["wains"], [(STAIR_TOP, STAIR_Y, FL), (STAIR_TOP, Y1, FL), (STAIR_TOP, Y1, UNDER), (STAIR_TOP, STAIR_Y, UNDER)], (STAIR_TOP + 1, 5.3, 1.5), 0.9)
    with b.at(T(-0.25, STAIR_Y - 0.04, FL)):  # cupboard under the stair
        b.box(P["door"], (0.62, 0.03, 0.8), (0, -0.012, 0.42), bevel=0.012, tint=1.05)
        for sx in (-1, 1):
            b.box(P["trim_z"], (0.06, 0.04, 0.9), (sx * 0.34, -0.018, 0.45), bevel=0.012)
        b.box(P["trim_x"], (0.74, 0.04, 0.06), (0, -0.018, 0.87), bevel=0.012)
        b.lathe(P["brass"], [(0.0, 0.0), (0.02, 0.0), (0.03, 0.03), (0.0, 0.045)], 8, T(0.22, -0.03, 0.45) @ R("X", 90), tint=1.0)
    # balustrade: newel, three square balusters, handrail up to a drop post under the loft edge
    ny = STAIR_Y + 0.07
    b.box(P["trim_z"], (0.13, 0.13, 1.0), (STAIR_X - 0.1, ny, FL + 0.5), bevel=0.02)
    b.lathe(P["trim_z"], [(0.0, 0.0), (0.075, 0.0), (0.09, 0.05), (0.05, 0.11), (0.0, 0.13)], 10, T(STAIR_X - 0.1, ny, FL + 1.0))
    drop_k = 5
    dx, dz = STAIR_X + TREAD * (drop_k - 0.5), FL + RISE * drop_k
    b.box(P["trim_z"], (0.1, 0.1, UNDER - dz), (dx, ny, (UNDER + dz) / 2), bevel=0.016)
    beam(b, P["trim_x"], (STAIR_X - 0.1, ny, FL + 0.9), (dx, ny, dz + 0.86), 0.07, 0.06, 0.016)
    for k in range(1, drop_k):
        x, z = STAIR_X + TREAD * (k - 0.5), FL + RISE * k
        top = FL + 0.9 + (dz + 0.86 - FL - 0.9) * (x - STAIR_X + 0.1) / (dx - STAIR_X + 0.1)
        b.box(P["trim_z"], (0.045, 0.045, top - z), (x, ny, (top + z) / 2), bevel=0.008)
    # ----- the workshop partition: wainscot, grille, panel opening (hideable for the cutaway) -----
    b.part = "NookWall"
    with b.at(MECH):
        f0, top = MECH_FLOOR, UNDER - NOOK_Z
        wx0, wx1 = -(Y1 - MECH_Y), MECH_Y - (NOOK_Y - PART_T)
        holes = (GRILLE, PANEL)
        grid_wall(b, [(f0, f0 + 0.85, P["wains"], 0.19, LOW), (f0 + 0.85, top, P["upper"], 0.95, HIGH)], wx0, wx1, holes)
        grid_wall(b, [(0.0, top, P["dark"], 0.3, LOW)], wx0, wx1 - PART_T, holes, y=PART_T, facing=1)
        for hole in holes:
            reveal(b, P["trim_z"], hole, 0.0, PART_T, 0.8)
        for x0, x1 in ((wx0, GRILLE[0] - 0.08), (GRILLE[1] + 0.08, wx1)):
            b.box(P["trim_x"], (x1 - x0, 0.05, 0.07), ((x0 + x1) / 2, -0.02, f0 + 0.85), bevel=0.014)
        for x0, x1 in ((wx0, GRILLE[0] - 0.08), (GRILLE[1] + 0.08, wx1)):
            b.box(P["trim_x"], (x1 - x0, 0.035, 0.12), ((x0 + x1) / 2, -0.012, f0 + 0.06), bevel=0.012, tint=0.9)
        # grille: fat frame and turned bars
        gx0, gx1, gz0, gz1 = GRILLE
        for sx in (gx0 - 0.04, gx1 + 0.04):
            b.box(P["trim_z"], (0.08, 0.14, gz1 - gz0 + 0.16), (sx, 0.04, (gz0 + gz1) / 2), bevel=0.018)
        for z in (gz0 - 0.04, gz1 + 0.04):
            b.box(P["trim_x"], (gx1 - gx0 + 0.16, 0.15, 0.08), ((gx0 + gx1) / 2, 0.04, z), bevel=0.018, tint=1.04)
        nb = 12
        bar = [(0.016, 0.0), (0.016, 0.05), (0.026, 0.1), (0.016, 0.16), (0.016, 0.26), (0.026, 0.32), (0.016, 0.37), (0.016, 0.42)]
        for i in range(nb):
            x = gx0 + (i + 0.5) * (gx1 - gx0) / nb
            b.lathe(P["orange"] if i % 3 == 1 else P["trim_z"], bar, 6, T(x, 0.05, gz0), tint=1.0 if i % 3 == 1 else None)
        # panel casing
        px0, px1, pz0, pz1 = PANEL
        for sx in (px0 - 0.035, px1 + 0.035):
            b.box(P["trim_z"], (0.07, 0.05, pz1 - pz0 + 0.14), (sx, -0.025, (pz0 + pz1) / 2), bevel=0.014)
        for z in (pz0 - 0.035, pz1 + 0.035):
            b.box(P["trim_x"], (px1 - px0 + 0.14, 0.05, 0.07), ((px0 + px1) / 2, -0.025, z), bevel=0.014)
        pk.owl_motif(b, P, 0.6, T(1.03, -0.004, f0 + 0.96))  # the painted owl, as on the shutter outside
    b.part = None
    # ----- the inside of the front door (swings with the exterior FrontDoor) and its wooden latch -----
    b.part = "InnerDoor"
    with b.at(T(0, 0.036, FL)):
        hw, hh = 0.55, 2.02
        b.box(P["door"], (2 * hw, 0.03, hh), (0, 0.015, hh / 2), bevel=0, tint=0.85, drop=("-y",))
        for sx in (-1, 1):
            b.box(P["door"], (0.13, 0.025, hh), (sx * (hw - 0.065), 0.04, hh / 2), bevel=0.01)
        for z, h in ((0.09, 0.18), (0.8, 0.13), (hh - 0.07, 0.14)):
            b.box(P["door_x"], (2 * hw - 0.26, 0.025, h), (0, 0.04, z), bevel=0.01)
        for sx in (-1, 1):
            b.box(P["door"], (0.27, 0.018, 0.5), (sx * 0.21, 0.035, 0.45), bevel=0.014, tint=1.12)
        b.box(P["door"], (0.1, 0.025, 0.56), (0, 0.04, 0.46), bevel=0.01)
        # ticket window from behind: slat backs on two ledges, a pull cord, the ticket shelf with a roll
        sw, z0, z1 = 0.66, 1.06, 1.72
        for i in range(8):
            z = z0 + (i + 0.5) * (z1 - z0) / 8
            b.box(P["slat"], (sw, 0.02, (z1 - z0) / 8 - 0.006), (0, 0.034, z), bevel=0.006, tint=b.vary(0.9, 1.1))
        for sx in (-1, 1):
            b.box(P["trim_z"], (0.05, 0.035, z1 - z0 + 0.1), (sx * (sw / 2 + 0.025), 0.045, (z0 + z1) / 2), bevel=0.01)
            b.box(P["amber"], (0.045, 0.02, z1 - z0 - 0.06), (sx * 0.2, 0.052, (z0 + z1) / 2), bevel=0.008)
        b.box(P["trim_x"], (sw + 0.1, 0.035, 0.05), (0, 0.045, z1 + 0.025), bevel=0.01)
        b.box(P["trim_x"], (0.86, 0.17, 0.04), (0, 0.11, z0 - 0.02), bevel=0.014)
        for sx in (-1, 1):
            b.box(P["trim_x"], (0.035, 0.1, 0.1), (sx * 0.34, 0.085, z0 - 0.09), bevel=0.012)
        b.cyl(P["paint"], 0.05, 0.05, T(0.26, 0.12, z0 + 0.05) @ R("Y", 90), segments=10, tint=lc(E["candy"]))  # roll of tickets
        b.box(P["paint"], (0.045, 0.004, 0.11), (0.26, 0.17, z0 - 0.02), bevel=0, tint=lc(E["candy"]))
        b.box(P["paint"], (0.06, 0.06, 0.05), (-0.25, 0.12, z0 + 0.025), bevel=0.012, tint=lc(E["plum"]))  # stamp
        b.lathe(P["amber"], [(0.012, 0.0), (0.012, 0.04), (0.024, 0.06), (0.0, 0.075)], 8, T(-0.25, 0.12, z0 + 0.05))
        b.rod(P["paint"], (0.0, 0.07, z1 + 0.02), (0.0, 0.07, z0 + 0.2), 0.006, 4, lc(E["bone_dim"]))
        b.lathe(P["orange"], [(0.0, 0.0), (0.022, 0.012), (0.022, 0.04), (0.0, 0.055)], 8, T(0, 0.07, z0 + 0.15), tint=1.0)
        b.lathe(P["brass"], [(0.0, 0.0), (0.022, 0.0), (0.016, 0.03), (0.034, 0.05), (0.03, 0.075), (0.0, 0.085)], 10,
                T(hw - 0.07, 0.05, 0.95) @ R("X", -90), tint=1.0)
        for z in (0.25, 1.0, 1.78):
            b.box(P["ink"], (0.035, 0.03, 0.11), (-hw + 0.01, 0.045, z), bevel=0.005, tint=1.0)
        b.box(P["amber"], (0.1, 0.03, 0.14), (0.12, 0.062, 0.95), bevel=0.012)  # latch pivot plate
    b.part = "DoorLatch"  # pivot at its bolt; lifts by turning about the door normal
    with b.at(T(0.12, 0.036, FL + 0.95)):
        b.box(P["candy"], (0.62, 0.035, 0.075), (0.25, 0.095, 0.0), bevel=0.016, tint=1.0)
        b.disc(P["brass"], 0.022, 0.012, T(0, 0.125, 0) @ R("Z", 180), segs=8, tint=1.0, bevel=0.004)
        b.lathe(P["orange"], [(0.0, 0.0), (0.02, 0.0), (0.03, 0.03), (0.0, 0.05)], 8, T(0.4, 0.11, 0) @ R("X", -90), tint=1.0)
    b.part = None
    b.box(P["amber"], (0.06, 0.07, 0.16), (0.69, Y0 + 0.035, FL + 0.95), bevel=0.012)  # keeper on the jamb
    b.box(P["amber"], (0.06, 0.03, 0.05), (0.66, Y0 + 0.09, FL + 0.895), bevel=0.01)
    # ----- foyer dressing -----
    b.box(P["felt"], (0.9, 0.55, 0.025), (0, 0.85, FL + 0.012), bevel=0.008, tint=1.0)
    b.prism(P["felt_v"], el.co.star_outline(0.16), 0.004, T(0, 0.85, FL + 0.027) @ R("X", -90) @ T(0, -0.004, 0), tint=1.0)
    rug = [(0.0, 0.0), (1.0, 0.0), (1.02, 0.012), (0.98, 0.022), (0.0, 0.022)]
    b.lathe(P["cloth"], rug, 20, T(-0.7, 2.9, FL) @ S(1.15, 0.8, 1.0), tint=1.0)
    b.lathe(P["felt"], [(0.0, 0.022), (0.74, 0.022), (0.74, 0.026), (0.0, 0.026)], 20, T(-0.7, 2.9, FL) @ S(1.15, 0.8, 1.0), tint=1.0)
    b.lathe(P["cloth"], [(0.0, 0.026), (0.56, 0.026), (0.56, 0.03), (0.0, 0.03)], 20, T(-0.7, 2.9, FL) @ S(1.15, 0.8, 1.0), tint=1.15)
    with b.at(T(X0, 2.4, FL) @ R("Z", 90)):  # bench with coat pegs and a witch hat on the west wall
        b.box(P["amber"], (1.1, 0.34, 0.05), (0, -0.2, 0.36), bevel=0.016)
        for sx in (-1, 1):
            b.box(P["amber"], (0.06, 0.3, 0.34), (sx * 0.45, -0.2, 0.17), bevel=0.014)
        b.box(P["trim_x"], (1.2, 0.04, 0.1), (0, -0.02, 1.1), bevel=0.014)
        for i in range(4):
            b.rod(P["orange"], (-0.45 + 0.3 * i, -0.04, 1.1), (-0.45 + 0.3 * i, -0.13, 1.13), 0.016, 6, 1.0)
        hat = craft.felt(E["purple"])
        b.lathe(hat, [(0.0, 0.0), (0.2, 0.0), (0.21, 0.012), (0.1, 0.03), (0.07, 0.16), (0.02, 0.3), (0.0, 0.33)], 12,
                T(0.15, -0.14, 0.95) @ R("X", -72), tint=1.0)
        b.lathe(P["orange"], [(0.1, 0.03), (0.105, 0.05), (0.094, 0.07)], 12, T(0.15, -0.14, 0.95) @ R("X", -72), tint=1.0)
        b.lathe(glow(b, craft, "Lamp", E["glow"], 2.4), [(0.0, 0.0), (0.06, 0.0), (0.075, 0.06), (0.06, 0.13), (0.0, 0.13)], 8, T(-0.35, -0.2, 0.39), tint=1.0)
        b.glow_tag = None
        b.lathe(P["ink"], [(0.065, 0.13), (0.07, 0.145), (0.03, 0.17), (0.0, 0.18)], 8, T(-0.35, -0.2, 0.39), tint=1.0)
        b.lathe(P["ink"], [(0.07, -0.0), (0.075, 0.012), (0.06, 0.014)], 8, T(-0.35, -0.2, 0.385), tint=1.0)
    for i, (y, z, s, rz) in enumerate(((1.1, 2.05, 0.3, 14), (1.75, 2.25, 0.22, -10), (0.6, 2.5, 0.2, 24))):  # paper bats up the west wall
        pr.bat(b, P["paper"], s, T(X0 + 0.02, y, z) @ R("Z", 90) @ R("Y", rz), fold=0.25, curl=0.3)
    foot, tip = Vector((0.95, Y0 + 0.5, FL)), Vector((0.98, Y0 + 0.06, FL + 1.5))  # broom by the door
    aim = (tip - foot).to_track_quat("Z", "Y").to_matrix().to_4x4()
    b.rod(P["amber"], foot.lerp(tip, 0.2), tip, 0.018, 6)
    b.lathe(craft.tinted(H(0xC9A24B), 0.9, 0.3, 30.0), [(0.0, 0.0), (0.13, 0.0), (0.1, 0.2), (0.035, 0.36), (0.03, 0.4)], 9, T(*foot) @ aim,
            radial=lambda q, i: 1 + 0.12 * math.cos(9 * q) * (i < 3))
    pr.pumpkin(b, craft, 0.2, 0.8, T(-2.35, 4.25, FL) @ R("Z", -60), style=0, per_lobe=2)
    pr.pumpkin(b, craft, 0.13, 0.85, T(-2.5, 3.8, FL), style=None, rgb=H(0xF0E3C4), per_lobe=2)
    pr.pumpkin(b, craft, 0.15, 0.8, T(-1.05, 0.5, FL), style=None, rgb=H(0xF59A23), per_lobe=2)
    crate(b, P, (0.5, 0.42, 0.36), T(1.12, 5.45, FL) @ R("Z", 8))
    with b.at(T(0, Y1, 0)):  # three framed cut-outs stepping up the stair
        mots = (lambda q: pr.bat(q, P["paper"], 0.22, T(0, -0.02, 0), fold=0.03, curl=0.05),
                lambda q: q.prism(P["paper"], [(0.09 * math.cos(math.radians(a)), 0.09 * math.sin(math.radians(a))) for a in range(60, 301, 30)]
                                  + [(0.04 + 0.07 * math.cos(math.radians(a)), 0.07 * math.sin(math.radians(a))) for a in range(270, 89, -30)], 0.004,
                                  T(0, -0.02, 0), tint=lc(E["candy"])),
                lambda q: pk.owl_motif(q, P, 0.2, T(0, -0.014, -0.1), depth=0.005))
        for i, mot in enumerate(mots):
            x = -1.0 + 0.7 * i
            frame_art(b, P, 0.3, 0.3, mot, T(x, 0, FL + (x - STAIR_X) / TREAD * RISE + 1.15))
    pr.bunting(b, craft, (-2.6, VOID_Y - 0.07, LOFT - 0.06), (2.6, VOID_Y - 0.07, LOFT - 0.06), 0.14, 13, 0.22, 0.27)
    # the owl lantern on its bracket in front of the round window
    lx, ly, lz = 0.0, 0.95, 4.42
    b.box(P["ink"], (0.045, 0.045, 0.5), (lx, Y0 + 0.03, 5.38), bevel=0.01, tint=1.0)
    beam(b, P["ink"], (lx, Y0 + 0.02, 5.55), (lx, ly + 0.06, 5.55), 0.035, 0.035, 0.008, 1.0)
    beam(b, P["ink"], (lx, Y0 + 0.04, 5.18), (lx, ly - 0.25, 5.53), 0.03, 0.03, 0.008, 1.0)
    for i in range(9):
        z = 5.53 - (i + 0.5) * 0.095
        b.box(P["ink"], (0.024 if i % 2 else 0.008, 0.008 if i % 2 else 0.024, 0.1), (lx, ly, z), bevel=0.003, tint=1.0)
    owl_lantern(b, craft, P, T(lx, ly, lz))
    b.rod(P["paint"], (lx + 0.02, ly, lz - 0.24), (lx + 0.12, ly + 0.15, LOFT + 1.3), 0.005, 4, lc(E["bone_dim"]))  # pull cord toward the loft
    b.lathe(P["orange"], [(0.0, 0.0), (0.02, 0.012), (0.02, 0.04), (0.0, 0.05)], 6, T(lx + 0.12, ly + 0.15, LOFT + 1.25), tint=1.0)
    for i, (x, y, z, s) in enumerate(((-1.5, 0.6, 4.2, 0.1), (1.35, 0.8, 4.6, 0.08), (-0.75, 1.1, 5.3, 0.07), (1.9, 0.5, 3.5, 0.09), (-2.1, 0.9, 3.4, 0.08))):
        top = min(ceil_z(x) - 0.05, 5.9)
        b.rod(P["paint"], (x, y, z + s), (x, y, top), 0.003, 3, lc(E["bone_dim"]))
        b.paper(P["paper"], el.co.star_outline(s), T(x, y, z) @ R("Z", 30 * i), lc(E["candy"]), fold=0.25, curl=0.2, cuts=0)
    b.finish("InteriorShell")

    # =================== the loft ===================
    a = Builder(202)
    a.part = "Attic"
    hx0, hx1, hy0, hy1 = HOLE
    # floor: boards on top, ceiling boards and joists below, fascias at the edges
    planks(a, P["floor"], X0, hx0, VOID_Y, Y1, LOFT)
    planks(a, P["floor"], hx0, hx1, VOID_Y, hy0, LOFT)
    planks(a, P["floor"], hx1, X1, VOID_Y, Y1, LOFT)
    planks(a, P["board_y"], X0, hx0, VOID_Y, Y1, UNDER, 0.3, up=False, joints=False)
    planks(a, P["board_y"], hx0, hx1, VOID_Y, hy0, UNDER, 0.3, up=False, joints=False)
    planks(a, P["board_y"], hx1, X1, VOID_Y, Y1, UNDER, 0.3, up=False, joints=False)
    for y in (2.0, 2.75, 3.5, 4.25):
        a.box(P["rafter"], (NOOK_X - X0, 0.09, 0.11), ((NOOK_X + X0) / 2, y, UNDER - 0.055), bevel=0.014, drop=())
    a.box(P["trim_x"], (2 * X1, 0.06, LOFT_T + 0.08), (0, VOID_Y - 0.03, LOFT - LOFT_T / 2 - 0.04), bevel=0.016)
    a.box(P["trim_x"], (hx1 - hx0, 0.05, LOFT_T + 0.05), ((hx0 + hx1) / 2, hy0 + 0.025, LOFT - LOFT_T / 2 - 0.025), bevel=0.014)
    a.box(P["trim_y"], (0.05, hy1 - hy0, LOFT_T + 0.05), (hx0 + 0.025, (hy0 + hy1) / 2, LOFT - LOFT_T / 2 - 0.025), bevel=0.014)
    # rails toward the entry bay and round the stairwell
    reach = ceil_x(LOFT + 0.98)
    with a.at(T(0, VOID_Y + 0.08, LOFT)):
        pk.rail(a, P, 2 * reach - 0.2, rhythm=1)
        for x in (-reach, 0.0, reach):
            with a.at(T(x, 0, 0)):
                pk.post(a, P, 0.95, ())
    for sx in (-1, 1):  # boarded-in corners under the roof slope
        tri = [(sx * reach, LOFT), (sx * X1, LOFT), (sx * X1, KNEE), (sx * reach, LOFT + 0.9)]
        a.prism(P["wains"], tri if sx > 0 else tri[::-1], 0.04, T(0, VOID_Y + 0.06, 0), tint=0.95, smooth=False)
    with a.at(T((hx0 + hx1) / 2 + 0.03, hy0 - 0.07, LOFT)):
        pk.rail(a, P, hx1 - hx0 - 0.1)
    with a.at(T(hx0 - 0.07, (hy0 + hy1) / 2 + 0.02, LOFT) @ R("Z", -90)):
        pk.rail(a, P, hy1 - hy0 - 0.14)
    for x, y in ((hx0 - 0.07, hy0 - 0.07), (hx1 + 0.06, hy0 - 0.07)):
        with a.at(T(x, y, LOFT)):
            pk.post(a, P, 0.95, ())
    # knee walls, roof boards, rafters, collar ties, ridge
    for sx in (-1, 1):
        with a.at(T(sx * X1, 0, 0) @ R("Z", -sx * 90)):
            spans = ((VOID_Y, ALC_Y0), (ALC_Y1, Y1)) if sx < 0 else ((VOID_Y, Y1),)
            for y0, y1 in spans:
                lo, hi = (y0, y1) if sx < 0 else (-y1, -y0)
                grid_wall(a, [(LOFT, KNEE, P["wains"], 0.19, LOW)], lo, hi)
        n = 10
        for i in range(n):
            za, zb = KNEE + (APEX - KNEE) * i / n, KNEE + (APEX - KNEE) * (i + 1) / n
            xa, xb = sx * ceil_x(za), sx * ceil_x(zb)
            parts = [(Y0, Y1)]
            if sx < 0:
                parts = [(Y0, ALC_Y0), (ALC_Y1, Y1)] + ([(ALC_Y0, ALC_Y1)] if za >= ALC_RIDGE - 1e-6 else [])
            for y0, y1 in parts:
                cuts = [y0, y1] if y1 - y0 < 2.5 else [y0, a.rng.uniform(y0 + 0.8, y1 - 0.8), y1]
                for ya, yb in zip(cuts, cuts[1:]):
                    face(a, P["board_y"], [(xa, ya, za), (xa, yb, za), (xb, yb, zb), (xb, ya, zb)], (0, 3, 3.5), a.vary(0.78, 1.08))
        ang = math.degrees(math.atan(SLOPE))
        for y in (0.55, 1.45, 2.1, 3.9, 4.55, 5.45):
            if sx > 0 or not ALC_Y0 - 0.1 < y < ALC_Y1 + 0.1:
                c = Vector((sx * X1 / 2, y, (KNEE + APEX) / 2 - 0.075))
                a.box(P["rafter"], (math.hypot(X1, APEX - KNEE), 0.08, 0.11), tuple(c), bevel=0.014, rot=R("Y", sx * ang), drop=())
    # mouth of the dormer in the west slope: fill between the alcove gable and the board courses above it
    zr = KNEE + (APEX - KNEE) * math.ceil((ALC_RIDGE - KNEE) / (APEX - KNEE) * 10 - 1e-6) / 10
    mouth = [(-ceil_x(ALC_EAVE), ALC_Y0, ALC_EAVE), (-ceil_x(ALC_RIDGE), 3.0, ALC_RIDGE), (-ceil_x(ALC_EAVE), ALC_Y1, ALC_EAVE),
             (-ceil_x(zr), ALC_Y1, zr), (-ceil_x(zr), ALC_Y0, zr)]
    face(a, P["board_y"], mouth, (0, 3, 3.5), 0.92)
    for y in (0.55, 2.1, 3.9, 5.45):
        hw = ceil_x(5.15)
        a.box(P["rafter"], (2 * hw, 0.07, 0.1), (0, y - 0.075, 5.1), bevel=0.014)
    a.box(P["rafter_y"], (0.1, Y1 - Y0, 0.16), (0, (Y0 + Y1) / 2, APEX - 0.1), bevel=0.016, drop=("+z",))
    # back gable boards and the loft's back wall strip
    for i in range(14):
        za, zb = LOFT + (APEX - LOFT) * i / 14, LOFT + (APEX - LOFT) * (i + 1) / 14
        xa, xb = min(X1, ceil_x(za)), min(X1, ceil_x(zb))
        pts = [(-xa, Y1, za), (xa, Y1, za), (xb, Y1, zb), (-xb, Y1, zb)]
        if za < KNEE < zb:
            pts = [(-X1, Y1, za), (X1, Y1, za), (X1, Y1, KNEE), (xb, Y1, zb), (-xb, Y1, zb), (-X1, Y1, KNEE)]
        face(a, P["board_x"], pts, (0, 3, za), a.vary(0.8, 1.08))
    # ----- June's dormer alcove: the round-topped window is open, its sill is the floor level -----
    wx = X0
    arch = [(ALC_Y0, 3.3), (2.6, 3.3)] + [(y, z) for y, z in arc(3.0, 3.3, 0.4, 180, 0, 10)][1:] + [(ALC_Y1, 3.3), (ALC_Y1, ALC_EAVE),
                                                                                                 (3.0, ALC_RIDGE), (ALC_Y0, ALC_EAVE)]
    face(a, P["wains"], [(wx, y, z) for y, z in arch], (0, 3, 3.3), 0.95)
    for y0, y1 in ((ALC_Y0, 2.6), (3.4, ALC_Y1)):
        face(a, P["wains"], [(wx, y0, LOFT), (wx, y1, LOFT), (wx, y1, 3.3), (wx, y0, 3.3)], (0, 3, 3), a.vary(0.9, 1.05))
    for y in (ALC_Y0, ALC_Y1):
        face(a, P["wains"], [(wx, y, KNEE), (wx, y, ALC_EAVE), (-ceil_x(ALC_EAVE), y, ALC_EAVE)], (0, 3.0, 3.5), 0.88)
    for sy in (-1, 1):
        y = 3.0 + sy * 0.7
        face(a, P["board_y"], [(wx, y, ALC_EAVE), (wx, 3.0, ALC_RIDGE), (-ceil_x(ALC_RIDGE), 3.0, ALC_RIDGE), (-ceil_x(ALC_EAVE), y, ALC_EAVE)],
             (-2.4, 3.0, 3.0), 0.85 + 0.1 * sy)
    with a.at(T(wx, 3.0, SILL_Z) @ R("Z", 90)):  # window surround, room toward local -Y
        for sx in (-1, 1):
            a.box(P["trim_z"], (0.09, 0.07, 0.8), (sx * 0.445, -0.03, 0.4), bevel=0.016)
        a.ring(P["trim_x"], 0.4, 0.49, 0.07, T(0, -0.065, 0.8), a0=0, a1=180, segs=12, tint=1.0)
        a.box(P["trim_x"], (0.13, 0.1, 0.15), (0, -0.045, 1.25), bevel=0.02, tint=1.06)
        a.box(P["trim_x"], (1.1, 0.34, 0.05), (0, 0.06, -0.022), bevel=0.014, tint=1.02)  # the sill board, flush with the floor
        for sx, x in ((-1, -0.4), (1, 0.4)):  # reveal through the wall thickness
            face(a, P["trim_z"], [(x, 0, 0), (x, 0.16, 0), (x, 0.16, 0.8), (x, 0, 0.8)], (0, 0.08, 0.4), 0.85)
        pts = [(x, z) for x, z in arc(0.0, 0.8, 0.4, 0, 180, 10)]
        for (xa, za), (xb, zb) in zip(pts, pts[1:]):
            face(a, P["trim_x"], [(xa, 0, za), (xb, 0, zb), (xb, 0.16, zb), (xa, 0.16, za)], (0, 0.08, 0.8), 0.85)
        with a.at(T(0.42, 0.0, 0.0) @ R("Z", 62)):  # the inner shutter leaf, swung into the room
            for z in (0.04, 0.4, 0.76):
                a.box(P["shutter"], (0.4, 0.03, 0.07), (-0.2, 0, z), bevel=0.008)
            for x in (-0.03, -0.37):
                a.box(P["shutter_z"], (0.06, 0.03, 0.8), (x, 0, 0.4), bevel=0.008)
            for k in range(5):
                a.box(P["shutter"], (0.3, 0.012, 0.09), (-0.2, 0, 0.1 + 0.13 * k + (0.08 if k > 2 else 0)), rot=R("X", 28), bevel=0)
    a.part = "SillSnag"  # the matching snag on the sill (evidence)
    cloth_snag(a, craft, T(X0 + 0.03, 3.37, SILL_Z + 0.42) @ R("Z", 90), 0.8)
    a.part = "Attic"
    # ----- June's corner: stool, lamp glow, crafts -----
    stool(a, P, T(-1.75, 4.05, LOFT), cushion=P["felt_v"])
    a.lathe(P["cloth"], rug, 20, T(-0.55, 3.0, LOFT) @ S(1.0, 0.75, 1.0), tint=1.0)
    a.lathe(P["felt"], [(0.0, 0.022), (0.72, 0.022), (0.72, 0.026), (0.0, 0.026)], 20, T(-0.55, 3.0, LOFT) @ S(1.0, 0.75, 1.0), tint=1.0)
    a.prism(P["felt_v"], el.co.star_outline(0.3), 0.004, T(-0.55, 3.0, LOFT + 0.03) @ R("X", -90) @ T(0, -0.004, 0), tint=1.0)
    with a.at(T(1.2, 2.35, LOFT) @ R("Z", -8)):  # craft table: bats in the making, paint pots, paper, a pumpkin
        a.box(P["amber"], (1.1, 0.62, 0.05), (0, 0, 0.44), bevel=0.016)
        for sx in (-1, 1):
            for sy in (-1, 1):
                a.box(P["amber"], (0.07, 0.07, 0.42), (sx * 0.47, sy * 0.24, 0.21), bevel=0.014)
        a.box(P["amber"], (0.94, 0.04, 0.07), (0, 0.24, 0.36), bevel=0.01)
        for i, rgb in enumerate((E["orange"], E["purple"], E["green"])):
            a.lathe(P["paint"], [(0.0, 0.0), (0.045, 0.0), (0.05, 0.08), (0.042, 0.085), (0.042, 0.07), (0.0, 0.07)], 8, T(-0.4 + 0.12 * i, 0.18, 0.465),
                    tint=lc(rgb))
        a.rod(P["amber"], (-0.3, 0.2, 0.55), (-0.18, 0.05, 0.475), 0.008, 4)
        for i, rgb in enumerate((E["candy"], E["orange"], E["purple"], E["bone"])):
            a.box(P["paint"], (0.3, 0.22, 0.006), (0.25 + 0.012 * i, -0.08 + 0.01 * i, 0.468 + 0.006 * i), bevel=0, rot=R("Z", 9 * i - 12), tint=lc(rgb))
        pr.bat(a, P["paper"], 0.26, T(0.22, -0.07, 0.5) @ R("X", -80) @ R("Z", 20), fold=0.25, curl=0.2)
        pr.bat(a, P["paper"], 0.2, T(-0.18, -0.14, 0.474) @ R("X", -90) @ R("Z", -30), fold=0.05, curl=0.1)
        a.paper(P["paper"], el.co.star_outline(0.07), T(-0.02, 0.06, 0.474) @ R("X", -90), lc(E["candy"]), fold=0.05, curl=0.1, cuts=0)
        pr.pumpkin(a, craft, 0.11, 0.85, T(0.42, 0.16, 0.465), style=None, rgb=H(0xF59A23), per_lobe=2)
        stool(a, P, T(-0.1, -0.62, 0), seat=0.26, r=0.15)
    pr.pumpkin(a, craft, 0.2, 0.8, T(-2.1, 1.95, LOFT) @ R("Z", 70), style=1, rgb=H(0xF59A23), per_lobe=2)
    crate(a, P, (0.6, 0.5, 0.42), T(2.2, 5.3, LOFT) @ R("Z", 6))
    crate(a, P, (0.45, 0.4, 0.3), T(2.15, 5.28, LOFT + 0.42) @ R("Z", -14), tint=0.9)
    crate(a, P, (0.5, 0.45, 0.36), T(-2.2, 5.35, LOFT) @ R("Z", -8))
    moon = [(0.3 * math.cos(math.radians(d)), 0.3 * math.sin(math.radians(d))) for d in range(60, 301, 24)] + \
           [(0.13 + 0.23 * math.cos(math.radians(d)), 0.23 * math.sin(math.radians(d))) for d in range(276, 83, -24)]
    a.prism(P["candy"], moon, 0.025, T(-1.25, Y1 - 0.1, LOFT + 0.34) @ R("X", 12) @ R("Y", -12), tint=1.0, bevel=0.006)  # cardboard moon
    pr.bat_garland(a, craft, (-1.0, 2.1, 5.0), (1.0, 3.9, 5.0), 0.5, 6, 0.24)
    pr.bunting(a, craft, (-0.98, 3.83, 5.02), (0.98, 5.38, 5.02), 0.4, 7, 0.2, 0.26, start=2)
    # star lantern under the collar ties
    star = glow(a, craft, "AtticStar", E["candy"], 2.0)
    with a.at(T(-0.2, 3.0, 4.35)):
        a.rod(P["paint"], (0, 0, 0.1), (0, 0.9, 0.7), 0.004, 3, lc(E["bone_dim"]))
        for rz in (0, 90):
            a.prism(star, el.co.star_outline(0.2), 0.012, R("Z", rz) @ T(0, -0.006, 0), tint=1.0)
    a.glow_tag = None
    # the drive from the wind wheel: shaft up from the workshop, along a rafter, along the ridge to the mast
    sx, sy = NOOK_X + 0.68, MECH_Y
    top = ceil_z(sx) - 0.16
    a.cyl(P["amber"], 0.035, top - LOFT, T(sx, sy, (top + LOFT) / 2), segments=8)
    a.lathe(P["brass"], [(0.06, 0.0), (0.075, 0.015), (0.06, 0.04), (0.04, 0.04)], 8, T(sx, sy, LOFT), tint=1.0)
    rx = 0.2
    rtop = ceil_z(rx) - 0.22
    for p in ((sx, sy, top), (rx, sy, rtop), (rx, 0.62, rtop)):
        a.box(P["shutter"], (0.2, 0.2, 0.2), p, bevel=0.03, tint=1.0)
        a.disc(P["brass"], 0.045, 0.014, T(p[0], p[1] - 0.107, p[2]), segs=8, tint=1.0, bevel=0.004)
    a.rod(P["amber"], (sx, sy, top), (rx, sy, rtop), 0.03, 6)
    a.rod(P["amber"], (rx, sy, rtop), (rx, 0.62, rtop), 0.03, 6)
    a.rod(P["amber"], (rx, 0.62, rtop), (rx - 0.15, 0.62, APEX - 0.05), 0.03, 6)
    # the push rod from the linkage to the puppet, striped like the link arm
    lx2, ly2 = NOOK_X + 0.46, MECH_Y - 0.5
    ltop = ceil_z(lx2) - 0.05
    n = 8
    for i in range(n):
        z0, z1 = LOFT + (ltop - LOFT) * i / n, LOFT + (ltop - LOFT) * (i + 1) / n
        a.cyl(P["paint"], 0.02, z1 - z0, T(lx2, ly2, (z0 + z1) / 2), segments=6, tint=lc(E["orange"] if i % 2 else E["bone"]), caps=False)
    a.lathe(P["brass"], [(0.04, 0.0), (0.05, 0.012), (0.04, 0.03), (0.025, 0.03)], 8, T(lx2, ly2, LOFT), tint=1.0)
    a.finish("InteriorAttic")


# --- the workshop mechanism --------------------------------------------------------------------------------

SHAFT = Vector((0.0, 0.68))
CAM_TOP, RAMP = 0.52, 0.09
ROLLER_R = 0.3  # distance of the follower roller from the shaft
ARM_Y = SHAFT.y - ROLLER_R
HINGE = Vector((-0.72, ARM_Y, 0.66))
TIP_X, CRADLE_Z, HAMMER_X = 0.5, 1.4, 0.68
GUIDE_Z = 2.08
CRANK = Vector((0.0, 0.2, 1.38))
PANEL_HINGE = Vector((PANEL[1], 0.02, PANEL[2]))
LOBES = (90.0, 210.0, 330.0)
SLIP_DEG = 11.0
PHOTO = Vector((-0.62, 0.0, 1.52))


def cam_radius(deg):
    r = 0.22
    for c in LOBES:
        d = (deg - c + 180) % 360 - 180
        if abs(d) < 60:
            r = max(r, 0.22 + 0.2 * math.cos(math.radians(1.5 * d)) ** 1.2)
    return r


def build_mechanism(craft):
    P = paints(craft)
    paint = P["paint"]
    b = Builder(211)
    frame, frame_x, frame_y = P["post"], P["rafter"], P["rafter_y"]
    # ----- frame: sills, four posts, plates, the crank beam, the top beam with shaft bearing and rod guide -----
    for sx in (-1, 1):
        b.box(frame_y, (0.14, 1.0, 0.1), (sx * 0.95, 0.68, 0.05), bevel=0.02)
        for y in (0.3, 1.06):
            b.box(frame, (0.11, 0.11, 2.1), (sx * 0.95, y, 1.05), bevel=0.02)
        b.box(frame_y, (0.1, 0.9, 0.1), (sx * 0.95, 0.68, 2.1), bevel=0.018)
    b.box(frame_x, (2.0, 0.1, 0.12), (0, SHAFT.y, 2.05), bevel=0.02)
    b.box(frame_x, (2.0, 0.08, 0.09), (0, 0.3, CRANK.z - 0.16), bevel=0.016)
    b.box(frame_x, (1.9, 0.07, 0.08), (0, 1.06, 1.2), bevel=0.016)
    b.box(P["shutter"], (0.24, 0.24, 0.1), (SHAFT.x, SHAFT.y, 0.05), bevel=0.03, tint=1.0)  # foot bearing
    b.box(P["shutter"], (0.2, 0.16, 0.14), (SHAFT.x, SHAFT.y, 2.05), bevel=0.03, tint=1.0)
    b.box(P["shutter"], (0.16, 0.12, 0.16), (CRANK.x, 0.3, CRANK.z - 0.04), bevel=0.03, tint=1.0)  # crank bearing
    b.box(frame_y, (0.08, SHAFT.y - ARM_Y + 0.1, 0.07), (TIP_X, (SHAFT.y + ARM_Y) / 2 - 0.02, GUIDE_Z - 0.035), bevel=0.014)  # guide bracket
    b.ring(P["brass"], 0.036, 0.062, 0.03, T(TIP_X, ARM_Y, GUIDE_Z) @ R("X", -90), segs=10, tint=1.0)
    # follower hinge post with fork cheeks, and the knock block with its brass plate
    b.box(frame, (0.1, 0.22, 0.56), (HINGE.x, HINGE.y, 0.28), bevel=0.018)
    for sy in (-1, 1):
        b.box(frame, (0.12, 0.035, 0.26), (HINGE.x, HINGE.y + sy * 0.065, 0.66), bevel=0.012)
    b.box(P["door"], (0.14, 0.18, 0.5), (HAMMER_X, ARM_Y, 0.25), bevel=0.025, tint=1.0)
    b.box(P["brass"], (0.11, 0.15, 0.03), (HAMMER_X, ARM_Y, 0.512), bevel=0.008, tint=1.0)
    # ----- Cam: the shaft from the roof wheel with the three-lobed cam and the crown wheel -----
    b.part = "Cam"
    with b.at(T(SHAFT.x, SHAFT.y, 0)):
        b.cyl(P["amber"], 0.04, 2.3, T(0, 0, 1.2), segments=10)
        for z in (0.12, 1.92):
            b.lathe(P["brass"], [(0.04, 0.0), (0.06, 0.012), (0.06, 0.04), (0.04, 0.052)], 10, T(0, 0, z), tint=1.0)
        outline = [(cam_radius(d) * math.cos(math.radians(d)), cam_radius(d) * math.sin(math.radians(d))) for d in range(0, 360, 6)]
        b.prism(P["amber"], outline, 0.09, T(0, 0, CAM_TOP) @ R("X", -90), tint=1.0, bevel=0.016)
        b.lathe(P["orange"], [(0.04, 0.0), (0.1, 0.0), (0.1, 0.03), (0.07, 0.05), (0.04, 0.05)], 12, T(0, 0, CAM_TOP), tint=1.0)
        for i, c in enumerate(LOBES):  # wedge ramps: rise as the cam turns counter-clockwise, then drop
            bm = bmesh.new()
            steps, cols = 6, []
            for s in range(steps + 1):
                d = c + 30 - 55 * s / steps
                h = 0.012 + (RAMP - 0.012) * s / steps
                ro, ri = cam_radius(d) - 0.03, 0.16
                cs, sn = math.cos(math.radians(d)), math.sin(math.radians(d))
                cols.append([bm.verts.new((ri * cs, ri * sn, 0)), bm.verts.new((ro * cs, ro * sn, 0)), bm.verts.new((ro * cs, ro * sn, h)),
                             bm.verts.new((ri * cs, ri * sn, h))])
            for p, q in zip(cols, cols[1:]):
                for k in range(1, 4):
                    bm.faces.new((p[k], p[(k + 1) % 4], q[(k + 1) % 4], q[k]))
            bm.faces.new(cols[0])
            bm.faces.new(cols[-1][::-1])
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
            b.add_bm(bm, P["orange"], T(0, 0, CAM_TOP), 1.0, smooth=False)
            for k in range(i + 1):  # one, two, three brass studs
                d = math.radians(c + 36 + 8 * k - 4 * i)
                b.cyl(P["brass"], 0.02, 0.024, T(0.36 * math.cos(d), 0.36 * math.sin(d), CAM_TOP + 0.006), segments=6, tint=1.0)
        wz = CRANK.z - 0.13 - 0.085
        b.cyl(P["shutter"], 0.26, 0.05, T(0, 0, wz), segments=20, tint=1.0)
        b.lathe(P["shutter"], [(0.04, 0.0), (0.09, 0.0), (0.07, 0.05), (0.04, 0.05)], 10, T(0, 0, wz + 0.025), tint=1.1)
        for k in range(12):
            d = math.radians(30 * k)
            b.cyl(P["trim_z"], 0.02, 0.07, T(0.22 * math.cos(d), 0.22 * math.sin(d), wz + 0.055), segments=6, tint=1.0)
    # ----- Follower: lever on the hinge post, roller on the cam, hammer over the knock block, riser and cradle -----
    b.part = "Follower"
    b.box(P["trim_x"], (HAMMER_X - HINGE.x + 0.14, 0.07, 0.065), ((HAMMER_X + HINGE.x) / 2 - 0.01, ARM_Y, HINGE.z), bevel=0.016, tint=1.0)
    b.cyl(P["brass"], 0.022, 0.2, T(HINGE.x, HINGE.y, HINGE.z) @ R("X", 90), segments=8, tint=1.0)
    for sy in (-1, 1):
        b.box(P["trim_x"], (0.05, 0.016, 0.13), (SHAFT.x, ARM_Y + sy * 0.045, HINGE.z - 0.06), bevel=0.006, tint=0.95)
    b.cyl(P["orange"], 0.05, 0.06, T(SHAFT.x, ARM_Y, CAM_TOP + 0.05) @ R("X", 90), segments=12, tint=1.0)
    b.box(P["door"], (0.11, 0.11, 0.09), (HAMMER_X, ARM_Y, HINGE.z - 0.075), bevel=0.02, tint=1.1)  # hammer
    b.box(P["trim_z"], (0.06, 0.06, CRADLE_Z - 0.09 - HINGE.z), (TIP_X, ARM_Y, (CRADLE_Z - 0.09 + HINGE.z) / 2), bevel=0.012, tint=1.0)
    b.box(P["candy"], (0.16, 0.1, 0.035), (TIP_X, ARM_Y, CRADLE_Z - 0.075), bevel=0.012, tint=1.0)
    for sx in (-1, 1):
        b.box(P["candy"], (0.035, 0.1, 0.1), (TIP_X + sx * 0.0625, ARM_Y, CRADLE_Z - 0.02), bevel=0.012, tint=1.0)

    def link_arm(M):
        """Striped push rod, foot ball at the local origin, running up local Z through the guide to a knob."""
        with b.at(M):
            b.lathe(P["candy"], [(0.0, -0.045), (0.035, -0.03), (0.045, 0.0), (0.035, 0.03), (0.022, 0.045)], 10, tint=1.0)
            n, z0, z1 = 9, 0.04, 0.9
            for i in range(n):
                za, zb = z0 + (z1 - z0) * i / n, z0 + (z1 - z0) * (i + 1) / n
                b.cyl(paint, 0.026, zb - za, T(0, 0, (za + zb) / 2), segments=8, tint=lc(E["orange"] if i % 2 == 0 else E["bone"]), caps=False)
            b.lathe(P["candy"], [(0.026, 0.9), (0.07, 0.91), (0.08, 0.94), (0.05, 0.99), (0.0, 1.0)], 10, tint=1.0)
    b.part = "LinkArm_Seated"
    link_arm(T(TIP_X, ARM_Y, CRADLE_Z))
    b.part = "LinkArm_Slipped"  # dropped until its knob rests on the guide, foot swung clear of the cradle
    link_arm(T(TIP_X, ARM_Y, GUIDE_Z + 0.03) @ R("Y", -SLIP_DEG) @ T(0, 0, -0.9))
    # ----- Crank: handle, axle and the pin wheel that meshes with the crown wheel -----
    b.part = "Crank"
    with b.at(T(CRANK.x, 0, CRANK.z)):
        b.cyl(P["amber"], 0.028, 0.34, T(0, 0.34, 0) @ R("X", 90), segments=8)
        b.disc(P["shutter"], 0.13, 0.05, T(0, 0.435, 0), segs=16, tint=1.0, bevel=0.01)
        for k in range(8):
            d = math.radians(45 * k)
            b.cyl(P["trim_z"], 0.018, 0.07, T(0.1 * math.cos(d), 0.5, 0.1 * math.sin(d)) @ R("X", 90), segments=6, tint=1.0)
        arm = [(0.045 * math.cos(math.radians(d)), 0.045 * math.sin(math.radians(d))) for d in range(90, 271, 30)] + \
              [(0.17 + 0.035 * math.cos(math.radians(d)), 0.035 * math.sin(math.radians(d))) for d in range(-90, 91, 30)]
        b.prism(P["door"], arm, 0.04, T(0, CRANK.y - 0.02, 0) @ R("Y", 35), tint=1.1, bevel=0.01)
        hx, hz = 0.17 * math.cos(math.radians(35)), -0.17 * math.sin(math.radians(35))
        b.lathe(P["orange"], [(0.0, 0.0), (0.03, 0.0), (0.038, 0.03), (0.03, 0.075), (0.0, 0.085)], 10, T(hx, CRANK.y - 0.015, hz) @ R("X", 90), tint=1.0)
        b.disc(P["brass"], 0.02, 0.012, T(0, CRANK.y - 0.03, 0), segs=8, tint=1.0, bevel=0.004)
    # ----- ServicePanel: slatted like the ticket shutter, with the painted owl; hinged on its +X edge -----
    b.part = "ServicePanel"
    px0, px1, pz0, pz1 = PANEL
    w, h = px1 - px0 - 0.012, pz1 - pz0 - 0.012
    with b.at(T((px0 + px1) / 2, 0.0, pz0 + 0.006)):
        n = 9
        for i in range(n):
            z = (i + 0.5) * h / n
            b.box(P["slat"], (w, 0.03, h / n - 0.006), (0, 0.02, z), bevel=0.008, tint=b.vary(0.9, 1.12))
        for sx in (-1, 1):
            b.box(P["trim_z"], (0.07, 0.022, h), (sx * (w / 2 - 0.035), -0.004, h / 2), bevel=0.01)
        for z in (0.035, h - 0.035):
            b.box(P["trim_x"], (w - 0.14, 0.022, 0.07), (0, -0.004, z), bevel=0.01)
        pk.owl_motif(b, P, 0.52, T(0, -0.004, 0.16))
        b.lathe(P["orange"], [(0.0, 0.0), (0.02, 0.0), (0.032, 0.03), (0.02, 0.05), (0.0, 0.055)], 8, T(-w / 2 + 0.035, -0.015, h * 0.5) @ R("X", 90), tint=1.0)
        for z in (0.12, h - 0.12):
            b.box(P["ink"], (0.03, 0.03, 0.1), (w / 2 + 0.004, -0.012, z), bevel=0.005, tint=1.0)
    # ----- KitPhoto: Aunt Kit at her booth, framed on the foyer side of the partition -----
    b.part = "KitPhoto"
    frame_art(b, P, 0.4, 0.5, photo_motif(P, craft), T(*PHOTO), back=E["night"])
    b.rod(paint, (PHOTO.x - 0.1, -0.02, PHOTO.z + 0.27), (PHOTO.x, -0.012, PHOTO.z + 0.36), 0.004, 3, lc(E["bone_dim"]))
    b.rod(paint, (PHOTO.x + 0.1, -0.02, PHOTO.z + 0.27), (PHOTO.x, -0.012, PHOTO.z + 0.36), 0.004, 3, lc(E["bone_dim"]))
    b.disc(P["brass"], 0.012, 0.01, T(PHOTO.x, -0.012, PHOTO.z + 0.36), segs=6, tint=1.0)
    b.part = None
    b.finish("Mechanism")


def mechanism_pivots():
    return {
        "Cam": T(SHAFT.x, SHAFT.y, CAM_TOP),
        "Follower": T(*HINGE),
        "LinkArm_Seated": T(TIP_X, ARM_Y, CRADLE_Z),
        "LinkArm_Slipped": T(TIP_X, ARM_Y, GUIDE_Z + 0.03),
        "Crank": T(*CRANK),
        "ServicePanel": T(*PANEL_HINGE),
        "KitPhoto": T(*PHOTO),
    }


def interior_pivots():
    return {
        "InnerDoor": DOOR_HINGE,
        "DoorLatch": T(0.12, 0.13, FL + 0.95),
        "SillSnag": T(X0 + 0.03, 3.37, SILL_Z + 0.42),
    }
