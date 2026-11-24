"""Inside Aunt Kit's owl house (spec §2-§4, decisions D24 / D24.1): the rooms, as a free-standing model that is
"bigger on the inside" than the approved exterior (`owl_house.py`), and the workshop mechanism behind the grille.

OwlHouseInterior (local frame: the inner face of the front wall at y = 0.14, the door centred on x = 0, hall
floor at z = 0.6; exported with the origin at (0, 2, 0) like the exterior)
  Hall (z = 0.6, 3.26 m clear): the inside of the ticket-booth door, the coat rack and bench in the alcove under
  the loft, the workshop partition on the east side with the low grille and the service panel opening.
  Stair (1.5 m wide): north along the west wall in a bay that is open to the roof, a landing in the corner, then
  east behind its own wall up to the middle of the attic.
  Attic (z = 4.0, 1.2 m knee walls, 4.2 m under the ridge): June's dormer window in the west wall with its
  alcove, her stool and rug, the round window in the front gable.
  The shell is closed on every side. The two thresholds (the front door and June's window) and the windows look
  onto closed view boxes with a painted night street (`StreetView_Glow`), never onto the void.

WorkshopMechanism (own frame: front toward -Y, floor of the nook at z = 0, hall floor at z = 0.5)
  Cam + shaft, Follower, LinkArm_Slipped / LinkArm_Seated, ServicePanel, Crank. The troupe photo (`TroupePhoto`, its own
  game model) hangs on the foyer side of the partition at `PHOTO`.

Game meshes only (exported by `env_export_interior.py`, reviewed from the FBX by `env_house_v3_review.py`)."""
import math
import random

import bmesh
from mathutils import Matrix, Vector

import env_lib as el
import pip_model as pm
import porch_kit as pk
import props as pr
from env_lib import E, R, S, T, Builder, H, arc, blend, catenary, lc, shade

FL = pk.FLOOR
X0, X1, Y0, Y1 = -3.6, 3.6, 0.14, 7.0
LOFT, LOFT_T = 4.0, 0.14
UNDER = LOFT - LOFT_T  # hall ceiling: 3.26 m clear
KNEE, SLOPE = LOFT + 1.2, 0.84  # attic knee walls and a 40 degree roof
APEX = KNEE + X1 * SLOPE
RISERS, TREAD, N1 = 18, 0.28, 9
RISE = (LOFT - FL) / RISERS
BAY_X = X0 + 1.5  # the stair is 1.5 m wide
LAND_Z, LAND_Y = FL + N1 * RISE, 5.5  # the corner landing
FOOT_Y = LAND_Y - (N1 - 1) * TREAD  # foot of the lower flight
TOP_X = BAY_X + (N1 - 1) * TREAD  # top of the upper flight
HA_Y0, HB_Y0 = FOOT_Y - 0.16, LAND_Y + 0.1  # the stairwell in the loft: over the west bay, and over the upper flight
LOFT_RECTS = ((X0, BAY_X, Y0, HA_Y0), (BAY_X, TOP_X, Y0, HB_Y0), (TOP_X, X1, Y0, Y1))
NOOK_X, NOOK_Y, NOOK_Z, PART_T = 2.2, 2.5, 0.1, 0.1
MECH_Y = 4.6
MECH = T(NOOK_X, MECH_Y, NOOK_Z) @ R("Z", -90)  # mechanism frame in house space
MECH_FLOOR = FL - NOOK_Z  # hall floor in the mechanism frame (0.5)
GRILLE = (-0.85, 0.85, 0.58, 1.0)  # x0, x1, z0, z1 in the mechanism frame
PANEL = (-0.25, 0.65, 1.08, 1.95)
SHAFT_X, ROD_X, ROD_Y = NOOK_X + 0.68, NOOK_X + 0.38, MECH_Y - 0.5  # the drive shaft and the push rod, in house space
DORM_Y, ALC_HALF = 1.5, 0.75  # June's dormer in the west wall
ALC_Y0, ALC_Y1, ALC_EAVE, ALC_RIDGE = DORM_Y - ALC_HALF, DORM_Y + ALC_HALF, LOFT + 1.8, LOFT + 2.4
SILL_Z = LOFT
WIN_W, WIN_SPRING = 1.0, 0.95  # June's window, as outside (owl_house.SIDE_W / SIDE_SPRING: 1.45 m apex)
VIEW_D = 1.6  # depth of the view box beyond June's window
SNAG = (X0 + 0.03, DORM_Y + WIN_W / 2 - 0.03, SILL_Z + 0.42)  # the cloth on the north jamb
DOOR_HINGE = T(-0.55, -0.015, FL)  # as the exterior FrontDoor pivot
ROUND_Z = LOFT + 2.75  # the round window in the front gable
STOOL = (-1.1, 2.75)  # June's stool
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
    (z0, z1, material, plank width or None, (lo, hi) tint range or a function of the plank index); `holes` are (x0, x1, z0, z1)."""
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
                    face(b, mat, [(a, y, za), (c, y, za), (c, y, zb), (a, y, zb)], ((a + c) / 2, y + facing, cz),
                         var(i) if callable(var) else b.vary(*var))


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
        frieze=craft.tinted(blend(E["night"], E["purple"], 0.45), 0.9, 0.16, 2.5),
        paint=craft.paint(),
        paper=craft.paper_white(),
    )
    return P


def glow(b, craft, tag, rgb, strength=2.0):
    b.glow_tag = tag
    return craft.glow(rgb, strength)


# --- painted night views (backdrop cards behind the windows and the two thresholds) -----------------------------

SKY = (H(0x1B1640), H(0x2E2360), H(0x4A3378), H(0x6A4690))
DARK, DARKER = H(0x15122B), H(0x0D0B1C)
WARM, MOON = (0.95, 0.66, 0.28), (0.86, 0.82, 0.62)


def night_view(b, craft, w, h, M, kind="street", seed=0):
    """A painted view of the street at night as layered paper cut-outs that light themselves (`StreetView_Glow`):
    local XZ plane, seen from -Y, origin at the bottom centre. `kind`: "street" (houses across the road),
    "porch" (the same past the porch rail) or "yard" (the maple, the treehouse and the shed roof)."""
    rng = random.Random(seed)
    prev, b.glow_tag = getattr(b, "glow_tag", None), "StreetView"
    hw, y = w / 2, [0.0]

    def cut(outline, rgb):
        y[0] -= 0.004
        with b.at(M @ T(0, y[0], 0)):
            face(b, craft.glow(rgb, 1.0), [(px, 0.0, pz) for px, pz in outline], (0.0, -1e6, 0.0), 1.0)

    def rect(x0, x1, z0, z1):
        return [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]

    def blob(cx, cz, rx, rz, n=9, wob=0.18):
        return [(cx + rx * (1 + wob * math.sin(3 * a + cx)) * math.cos(a), cz + rz * (1 + wob * math.cos(2 * a)) * math.sin(a))
                for a in (2 * math.pi * i / n for i in range(n))]

    cut(rect(-hw, hw, 0, h), SKY[0])
    cut(rect(-hw, hw, 0, h * 0.72), SKY[1])
    n = 8
    cut([(-hw, 0)] + [(-hw + w * i / n, h * (0.5 + 0.04 * math.sin(1.7 * i + seed))) for i in range(n + 1)] + [(hw, 0)], SKY[2])
    cut([(-hw, 0)] + [(-hw + w * i / n, h * (0.33 + 0.03 * math.cos(2.3 * i + seed))) for i in range(n + 1)] + [(hw, 0)], SKY[3])
    mx, mz, mr = (-0.24 if kind != "yard" else 0.2) * w, h * 0.8, min(w, h) * 0.085
    cut([(mx + mr * 1.5 * math.cos(a), mz + mr * 1.5 * math.sin(a)) for a in (2 * math.pi * i / 12 for i in range(12))], SKY[2])
    cut([(mx + mr * math.cos(a), mz + mr * math.sin(a)) for a in (2 * math.pi * i / 12 for i in range(12))], MOON)
    for _ in range(7):
        sx, sz, s = rng.uniform(-hw * 0.9, hw * 0.9), rng.uniform(h * 0.6, h * 0.95), rng.uniform(0.008, 0.014) * max(w, h)
        if math.hypot(sx - mx, sz - mz) > mr * 2:
            cut([(sx - s, sz), (sx, sz - s), (sx + s, sz), (sx, sz + s)], MOON)
    g = h * 0.2  # ground line
    if kind in ("street", "porch"):
        x = -hw
        while x < hw - 0.05 * w:  # the houses across the road
            hw2, wh, rh = rng.uniform(0.13, 0.2) * w, rng.uniform(0.16, 0.24) * h, rng.uniform(0.1, 0.17) * h
            cx = x + hw2
            x1 = min(hw, cx + hw2)
            cut([(x, g * 0.8), (x1, g * 0.8), (x1, g + wh), (min(cx, x1), g + wh + rh), (x, g + wh)], DARK)
            for k in range(2):
                wx = x + hw2 * (0.5 + 0.9 * k)
                if wx + 0.06 * w < x1 and rng.random() < 0.8:
                    cut(rect(wx - 0.035 * w, wx + 0.035 * w, g + wh * 0.35, g + wh * 0.75), WARM)
            x = x1 + rng.uniform(0.01, 0.05) * w
        cut(rect(-hw, hw, 0, g), DARKER)
        cut([(-0.1 * w, 0), (0.1 * w, 0), (0.035 * w, g), (-0.035 * w, g)], SKY[1])  # the garden path
        for i in range(-4, 5):  # the fence
            if i:
                px = i * w * 0.105
                cut([(px - 0.012 * w, g * 0.55), (px + 0.012 * w, g * 0.55), (px + 0.012 * w, g * 1.25), (px, g * 1.4), (px - 0.012 * w, g * 1.25)], SKY[3])
        for px, s in ((-0.3 * w, 0.03), (0.22 * w, 0.024), (0.36 * w, 0.034)):
            cut([(px + s * w * math.cos(a), g * 0.5 + s * w * 0.8 * (1 + math.sin(a))) for a in (2 * math.pi * i / 8 for i in range(8))], (0.85, 0.42, 0.12))
    else:
        cut(rect(-hw, hw, 0, g * 0.9), DARKER)
        tx = -0.12 * w
        cut([(tx - 0.035 * w, 0), (tx + 0.035 * w, 0), (tx + 0.02 * w, h * 0.62), (tx - 0.02 * w, h * 0.62)], DARK)  # the maple
        for cx, cz, rx, rz, rgb in ((tx - 0.13 * w, 0.66 * h, 0.17 * w, 0.1 * h, H(0x5C2030)), (tx + 0.14 * w, 0.7 * h, 0.18 * w, 0.11 * h, H(0x862A22)),
                                    (tx, 0.79 * h, 0.16 * w, 0.09 * h, H(0xA83A22))):
            cut(blob(cx, cz, rx, rz), shade(rgb, 0.75))
        cz = 0.36 * h  # the treehouse cabin with its lamp-lit window
        cut([(tx - 0.13 * w, cz), (tx + 0.13 * w, cz), (tx + 0.13 * w, cz + 0.14 * h), (tx, cz + 0.22 * h), (tx - 0.13 * w, cz + 0.14 * h)], DARKER)
        cut(rect(tx - 0.16 * w, tx + 0.16 * w, cz - 0.015 * h, cz + 0.005 * h), DARKER)
        cut(rect(tx + 0.03 * w, tx + 0.09 * w, cz + 0.05 * h, cz + 0.11 * h), WARM)
        cut(rect(0.08 * w, hw, 0, 0.3 * h), DARK)  # the shed roof the bridge comes from
        cut(rect(0.06 * w, hw, 0.3 * h, 0.32 * h), SKY[3])
    if kind == "porch":
        for px in (-0.36 * w, 0.36 * w):
            cut(rect(px - 0.03 * w, px + 0.03 * w, 0, h), E["bone_dim"] if False else shade(E["bone_dim"], 0.5))
        for z in (0.06 * h, 0.33 * h):
            cut(rect(-hw, -0.14 * w, z, z + 0.022 * h), shade(E["bone_dim"], 0.5))
            cut(rect(0.14 * w, hw, z, z + 0.022 * h), shade(E["bone_dim"], 0.5))
        for i in range(12):
            px = -hw + (i + 0.5) * w / 12
            if abs(px) > 0.15 * w:
                cut(rect(px - 0.012 * w, px + 0.012 * w, 0.06 * h, 0.33 * h), shade(E["bone_dim"], 0.42))
    b.glow_tag = prev


def shadow_box(b, mat, x0, x1, y0, y1, z0, z1, skip=(), tint=0.7):
    """The inward faces of a closed box (a view box behind an opening); `skip` names the sides left out."""
    c = ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
    sides = {"-x": [(x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1)], "+x": [(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)],
             "-y": [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], "+y": [(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)],
             "-z": [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], "+z": [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]}
    for name, pts in sides.items():
        if name not in skip:
            face(b, mat, pts, c, tint)


# --- furniture and dressing --------------------------------------------------------------------------------

def inner_window(b, P, craft, w=0.9, h=1.4, z0=0.8, curtains=True, depth=0.22, seed=0, kind="street", frame="trim"):
    """Window seen from the room: local wall plane y = 0, room toward -Y, z = 0 at the floor. The wall needs a
    hole of the same rectangle; behind it a reveal runs back to a painted night view."""
    zc = z0 + h / 2
    tz, tx = (P["trim_z"], P["trim_x"]) if frame == "trim" else (P["post"], P["rafter"])
    reveal(b, tz, (-w / 2, w / 2, z0, z0 + h), 0.0, depth, 0.8)
    night_view(b, craft, w, h, T(0, depth, z0), kind, seed)
    for sx in (-1, 1):
        b.box(tz, (0.09, 0.06, h + 0.09), (sx * (w / 2 + 0.045), -0.03, zc + 0.045), bevel=0.014)
    b.box(tx, (w + 0.26, 0.07, 0.09), (0, -0.035, z0 + h + 0.045), bevel=0.014)
    b.box(tx, (w + 0.3, 0.15, 0.05), (0, -0.07, z0 - 0.025), bevel=0.016)
    b.box(tx, (w + 0.14, 0.05, 0.08), (0, -0.03, z0 - 0.09), bevel=0.012, tint=0.92)
    b.box(tz, (0.04, 0.03, h), (0, 0.02, zc), bevel=0.008)
    for k in (1, 2):
        b.box(tx, (w, 0.03, 0.04), (0, 0.02, z0 + h * k / 3), bevel=0.008)
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


def trunk(b, P, size, M, body="door", lid=8.0):
    """Travel trunk with a domed lid, straps, brass corners and a clasp; the front faces -Y."""
    sx, sy, sz = size
    with b.at(M):
        b.box(P[body], (sx, sy, sz * 0.62), (0, 0, sz * 0.31), bevel=0.018, tint=b.vary(0.9, 1.05))
        with b.at(T(0, sy / 2, sz * 0.62) @ R("X", lid) @ T(0, -sy / 2, 0)):
            dome = [(-sy / 2, 0.0)] + [(-sy / 2 * math.cos(math.radians(a)), sz * 0.38 * math.sin(math.radians(a)) ** 0.7) for a in range(20, 161, 35)] + [(sy / 2, 0.0)]
            b.prism(P[body], dome, sx, T(sx / 2, 0, 0.004) @ R("Z", 90), tint=b.vary(0.95, 1.1), bevel=0.012)
            for x in (-sx * 0.28, sx * 0.28):
                b.prism(P["amber"], [(p[0] * 1.02, p[1] * 1.03 + 0.004) for p in dome], 0.07, T(x + 0.035, 0, 0.004) @ R("Z", 90), tint=0.9)
        for x in (-sx * 0.28, sx * 0.28):
            b.box(P["amber"], (0.07, sy + 0.02, sz * 0.62), (x, 0, sz * 0.31), bevel=0.008, tint=0.9)
        for ex in (-1, 1):
            for ey in (-1, 1):
                b.box(P["brass"], (0.07, 0.07, 0.07), (ex * (sx / 2 - 0.028), ey * (sy / 2 - 0.028), 0.03), bevel=0.012, tint=1.0)
        b.box(P["brass"], (0.09, 0.02, 0.11), (0, -sy / 2 - 0.008, sz * 0.58), bevel=0.008, tint=1.0)


def sconce(b, craft, P, M, tag="Lamp"):
    """Wall lamp: a paper globe on a small bracket (wall plane y = 0, room toward -Y)."""
    with b.at(M):
        b.box(P["ink"], (0.09, 0.02, 0.2), (0, -0.01, -0.04), bevel=0.006, tint=1.0)
        b.rod(P["ink"], (0, -0.01, -0.1), (0, -0.14, -0.06), 0.012, 5, 1.0)
        b.lathe(P["ink"], [(0.0, -0.075), (0.05, -0.075), (0.06, -0.06), (0.03, -0.05)], 8, T(0, -0.14, 0), tint=1.0)
        lit = glow(b, craft, tag, E["glow"], 2.4)
        b.lathe(lit, [(0.0, -0.06), (0.06, -0.05), (0.085, 0.02), (0.07, 0.09), (0.035, 0.12), (0.0, 0.125)], 10, T(0, -0.14, 0), tint=1.0)
        b.glow_tag = None
        b.lathe(P["ink"], [(0.04, 0.115), (0.045, 0.13), (0.0, 0.145)], 8, T(0, -0.14, 0), tint=1.0)


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


# --- the troupe photo (S004 plan section 2.2) -------------------------------------------------------------------

PHOTO_W, PHOTO_H = 0.7, 0.45  # the frame, outside
PHOTO_MOULD, PHOTO_MOUNT = 0.035, 0.028
PHOTO_WIN = (PHOTO_W / 2 - PHOTO_MOULD - PHOTO_MOUNT, PHOTO_H / 2 - PHOTO_MOULD - PHOTO_MOUNT)  # half size of the picture
PHOTO_STEP = 0.0011  # one paper layer
PHOTO_GROUND = -0.118
SEPIA = {  # warm paper and bone with a faded orange cast: it reads as old
    "sky": H(0xEAD7AE), "far": H(0xDCC597), "cream": H(0xF4E6C8), "mount": H(0xEFE0BF), "tan": H(0xCDA873), "ground": H(0xB8935F),
    "sienna": H(0xB07443), "umber": H(0x7A5134), "dark": H(0x4A3222), "orange": H(0xD98C4A), "rose": H(0xB5705C), "olive": H(0x9C8B56),
    "skin": H(0xE8C79C), "gilt": H(0xE0AC2E), "gilt_hi": H(0xF8E08C), "moon": H(0xF7EBCB),
}
# The five puppets, left to right (the Players' order in the picture): name of the exported child -> its centre in
# the frame (x, z). The seed cutscene's glints land on these.
PUPPETS = {"Puppet_Owl": (-0.257, 0.074), "Puppet_Fox": (-0.092, 0.092), "Puppet_Moth": (0.057, 0.066), "Puppet_Moon": (0.176, 0.104),
           "Puppet_Bell": (0.243, 0.05)}
PHOTO_CREST = (PHOTO_W / 2 - PHOTO_MOULD - 0.047, -PHOTO_H / 2 + PHOTO_MOULD + 0.047)  # on the mount, lower corner
PHOTO_CREST_D = 0.074


def _circ(r, n=14, rz=1.0):
    return [(r * math.cos(2 * math.pi * i / n), rz * r * math.sin(2 * math.pi * i / n)) for i in range(n)]


def _rect(x0, x1, z0, z1):
    return [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]


def _ribbon(pts, w):
    """A strip of width `w` along a polyline of (x, z) points."""
    left, right = [], []
    for i, p in enumerate(pts):
        a, c = pts[max(i - 1, 0)], pts[min(i + 1, len(pts) - 1)]
        dx, dz = c[0] - a[0], c[1] - a[1]
        n = math.hypot(dx, dz) or 1.0
        left.append((p[0] - dz / n * w / 2, p[1] + dx / n * w / 2))
        right.append((p[0] + dz / n * w / 2, p[1] - dx / n * w / 2))
    return left + right[::-1]


def photo_motif(P, craft):
    """The Lantern Lane Players, young, in front of the freshly painted owl booth, as layered sepia paper
    cut-outs (frame-centred; the picture is 2 x PHOTO_WIN). Kit (headscarf, crystal-ball lantern) holds the owl;
    then the fox, the moth, the moon and the bell, whose Player stands half out of the frame. Faces are plain
    paper discs; only Kit is recognisable. The puppets are gilt paper, each its own part (`PUPPETS`)."""
    C, G, step = SEPIA, PHOTO_GROUND, PHOTO_STEP
    wx, wz = PHOTO_WIN

    def draw(b):
        paper = P["paper"]

        def cut(outline, rgb, layer, at=(0.0, 0.0), k=1.0, mat=None):
            pts = [(x * k + at[0], z * k + at[1]) for x, z in outline]
            b.prism(mat or paper, pts, step * 0.9, T(0, -0.012 - step * (layer + 1), 0), tint=lc(rgb))

        # ----- the evening: paper sky, moon, the far roofs, the ground -----
        cut(_circ(0.04, 20), C["moon"], 0, (-0.2, 0.105))
        roofs = [(-wx, G), (-wx, 0.0), (-0.27, 0.03), (-0.245, 0.0), (-0.245, -0.03), (-0.21, -0.03), (-0.21, 0.01), (-0.185, 0.04), (-0.16, 0.01),
                 (-0.16, G)]
        cut(roofs, C["far"], 0)
        cut([(-x, z) for x, z in roofs][::-1], C["far"], 0)
        cut(_rect(-wx - 0.01, wx + 0.01, -wz - 0.01, G + 0.004), C["ground"], 1)
        for x, z, r in ((-0.23, -0.14, 0.02), (-0.06, -0.15, 0.025), (0.1, -0.138, 0.018), (0.21, -0.152, 0.022), (-0.15, -0.128, 0.012)):
            cut(_circ(r, 10, 0.3), blend(C["ground"], C["tan"], 0.6), 2, (x, z))
        # festival lanterns on two strings from the booth's roof
        for sx in (-1, 1):
            line = [(sx * p.x, p.z) for p in catenary((0.16, 0, 0.118), (wx + 0.01, 0, 0.158), 0.018, 10)]
            cut(_ribbon(line, 0.0022), C["umber"], 1)
            for i in (2, 5, 8):
                x, z = line[i]
                cut(_circ(0.0085, 10, 1.25), C["orange"] if i != 5 else C["cream"], 2, (x, z - 0.012))
                cut(_rect(-0.004, 0.004, -0.002, 0.002), C["umber"], 3, (x, z - 0.0015))
        # ----- the owl booth, freshly painted -----
        bw, top = 0.15, 0.1
        cut(_rect(-bw, bw, G, top), C["tan"], 3)
        n = 10
        for i in range(n):  # striped skirt under the counter
            x0 = -bw + 2 * bw * i / n
            if i % 2 == 0:
                cut(_rect(x0, x0 + 2 * bw / n, G, -0.032), C["orange"], 4)
        cut(_rect(-0.088, 0.088, -0.02, 0.062), C["dark"], 4)  # the ticket window, shutter up
        for i in range(3):
            cut(_rect(-0.088, 0.088, 0.036 + 0.009 * i, 0.042 + 0.009 * i), C["umber"], 5)
        for sx in (-1, 1):
            cut(_rect(sx * bw - 0.008, sx * bw + 0.008, G, top), C["umber"], 5)
            cut(_rect(sx * 0.094 - 0.006, sx * 0.094 + 0.006, -0.02, 0.068), C["cream"], 5)
        cut(_rect(-0.1, 0.1, 0.062, 0.071), C["cream"], 5)
        cut(_rect(-bw - 0.012, bw + 0.012, -0.034, -0.02), C["cream"], 6)  # counter
        cut(el.scallop_strip(2 * bw + 0.03, 0.012, 9, 0.012, 4), C["orange"], 6, (-bw - 0.015, 0.1))
        arch = [(0.2 * math.sin(math.radians(a)), top - 0.004 + 0.062 * math.cos(math.radians(a)) ** 0.8) for a in range(-60, 61, 12)]
        board = [(bw + 0.02, top - 0.004)] + arch[::-1] + [(-bw - 0.02, top - 0.004)]
        cut(board, C["sienna"], 6)
        cut([(x * 0.9, top + (z - top) * 0.84) for x, z in board], C["tan"], 7)
        el.crest(b, craft, 0.05, T(0.0, -0.012 - step * 8, top + 0.028), step=step * 0.5)  # the crest over its shutter

        # ----- the five Players -----
        def player(x, body, trim, hand=1, reach=(0.036, 0.1), low=None, head=True):
            """Feet at (x, G). `hand`: the side of the arm that holds the puppet's rod, `reach` where that hand is."""
            for sx in (-1, 1):
                cut(_rect(sx * 0.011 - 0.0055, sx * 0.011 + 0.0055, 0.004, 0.044), C["umber"], 9, (x, G))
                cut(_circ(0.0085, 8, 0.6), C["dark"], 10, (x + sx * 0.012, G + 0.005))
            cut([(-0.03, 0.034), (0.03, 0.034), (0.027, 0.06), (0.019, 0.099), (-0.019, 0.099), (-0.027, 0.06)], body, 10, (x, G))
            cut(_rect(-0.027, 0.027, 0.034, 0.041), trim, 11, (x, G))
            hx, hz = x + hand * reach[0], G + reach[1]
            sh = (x + hand * 0.017, G + 0.093)
            cut(_ribbon([sh, (hx, hz)], 0.011), body, 11)
            lx, lz = low if low else (x - hand * 0.034, G + 0.056)
            cut(_ribbon([(x - hand * 0.017, G + 0.093), (lx, lz)], 0.011), body, 9)
            for px, pz in ((hx, hz), (lx, lz)):
                cut(_circ(0.0068, 8), C["skin"], 12, (px, pz))
            cut(_rect(-0.008, 0.008, 0.096, 0.104), trim, 11, (x, G))
            if head:
                cut(_circ(0.0205, 16), C["skin"], 11, (x, G + 0.119))
                for sx in (-1, 1):
                    cut(_circ(0.0026, 6), C["dark"], 12, (x + sx * 0.0075, G + 0.12))
                cut(_ribbon([(x - 0.006, G + 0.1105), (x, G + 0.1085), (x + 0.006, G + 0.1105)], 0.0018), C["umber"], 12)
            return (hx, hz), (x, G + 0.119)

        def rod(hand, tip, part):
            b.part = part
            cut(_ribbon([(hand[0], hand[1] - 0.012), tip], 0.0042), C["umber"], 12)

        def gilt(outline, at, hi=None, k=1.0):
            """A gilt puppet shape with its dark paper backing (so it reads on sky and booth alike)."""
            cx = sum(q[0] for q in outline) / len(outline)
            cz = sum(q[1] for q in outline) / len(outline)
            cut([(cx + (px - cx) * 1.16, cz + (pz - cz) * 1.16) for px, pz in outline], C["dark"], 13, at, k)
            cut(outline, C["gilt"], 14, at, k)
            for shape in hi or ():
                cut(shape, C["gilt_hi"], 15, at, k)

        def dots(points, at, rgb=None, r=0.003):
            for px, pz in points:
                cut(_circ(r, 8), rgb or C["dark"], 16, (at[0] + px, at[1] + pz))

        # Kit: the orange headscarf with its tail, the fringed shawl, the crystal-ball lantern; she holds the owl
        hand, head = player(-0.215, C["rose"], C["orange"], hand=-1, reach=(0.04, 0.094), low=(-0.178, G + 0.062))
        cut([(-0.028, 0.0), (0.028, 0.0), (0.04, -0.03), (0.0, -0.022), (-0.04, -0.03)], C["umber"], 11, (-0.215, G + 0.098))  # shawl
        for i in range(5):
            cut([(-0.004, 0.0), (0.004, 0.0), (0.0, -0.011)], C["orange"], 12, (-0.247 + 0.016 * i, G + 0.07 + (0.004 if i in (1, 3) else 0.0)))
        cut([(-0.0235, -0.002), (0.0235, -0.002), (0.021, 0.014), (0.0, 0.027), (-0.021, 0.014)], C["orange"], 12, (head[0], head[1] + 0.003))
        cut([(0.016, 0.004), (0.043, -0.02), (0.03, 0.01)], C["orange"], 12, head)
        cut(_circ(0.0055, 8), C["gilt"], 13, (-0.215, G + 0.088))  # the owl brooch
        cut(_circ(0.015, 14), C["cream"], 13, (-0.176, G + 0.047))  # crystal-ball lantern
        cut(_circ(0.008, 10), C["moon"], 14, (-0.179, G + 0.05))
        cut([(-0.011, -0.012), (0.011, -0.012), (0.008, 0.0), (-0.008, 0.0)], C["dark"], 14, (-0.176, G + 0.047 - 0.011))
        at = PUPPETS["Puppet_Owl"]
        rod(hand, (at[0], at[1] - 0.02), "Puppet_Owl")
        owl = [(x, z - 0.028) for x, z in el.owl_outline(0.056)]
        u = 0.056 / 81.0
        gilt(owl, at, hi=[[(-9 * u, -11 * u), (9 * u, -11 * u), (12 * u, -23 * u), (0, -32 * u), (-12 * u, -23 * u)]])
        for sx in (-1, 1):
            cut(_circ(10 * u, 12), C["gilt_hi"], 15, (at[0] + sx * 11 * u, at[1] + 10 * u))
        dots([(-11 * u, 10 * u), (11 * u, 10 * u)], at, r=5.6 * u)
        cut([(-4 * u, 3 * u), (4 * u, 3 * u), (0, -8 * u)], C["umber"], 16, at)
        # the fox: a hood with pointed ears, a brush of a tail
        b.part = None
        hand, head = player(-0.105, C["sienna"], C["cream"], hand=1, reach=(0.03, 0.098))
        for sx in (-1, 1):
            cut([(sx * 0.006, 0.014), (sx * 0.023, 0.006), (sx * 0.021, 0.036)], C["sienna"], 12, head)
            cut([(sx * 0.011, 0.016), (sx * 0.02, 0.012), (sx * 0.019, 0.028)], C["cream"], 13, head)
        cut(_ribbon([(-0.02, 0.013), (0.0, 0.021), (0.02, 0.013)], 0.008), C["sienna"], 12, head)
        cut(pm.smooth_outline([(-0.022, 0.05), (-0.044, 0.046), (-0.056, 0.07), (-0.046, 0.092), (-0.034, 0.07)], 4), C["sienna"], 9, (-0.105, G))
        cut(_circ(0.008, 8), C["cream"], 10, (-0.105 - 0.047, G + 0.086))
        at = PUPPETS["Puppet_Fox"]
        rod(hand, (at[0], at[1] - 0.02), "Puppet_Fox")
        fox = [(0.0, -0.03), (0.021, -0.008), (0.033, 0.031), (0.013, 0.016), (0.0, 0.02), (-0.013, 0.016), (-0.033, 0.031), (-0.021, -0.008)]
        gilt(fox, at, hi=[[(0.016, 0.012), (0.027, 0.023), (0.022, 0.004)], [(-0.016, 0.012), (-0.022, 0.004), (-0.027, 0.023)],
                          [(0.0, -0.027), (0.012, -0.012), (0.0, -0.008), (-0.012, -0.012)]])
        dots([(-0.0095, 0.004), (0.0095, 0.004)], at)
        dots([(0.0, -0.026)], at, r=0.0036)
        # the moth: feathery antennae on a headband, a little cape
        b.part = None
        hand, head = player(0.012, C["olive"], C["cream"], hand=1, reach=(0.032, 0.082))
        cut(_ribbon([(-0.02, 0.011), (0.0, 0.02), (0.02, 0.011)], 0.006), C["cream"], 12, head)
        for sx in (-1, 1):
            cut(_ribbon([(sx * 0.008, 0.02), (sx * 0.013, 0.034), (sx * 0.022, 0.04)], 0.0035), C["umber"], 12, head)
            cut(_circ(0.0045, 8), C["cream"], 13, (head[0] + sx * 0.023, head[1] + 0.04))
            cut([(sx * 0.018, 0.096), (sx * 0.043, 0.06), (sx * 0.026, 0.05)], C["cream"], 9, (0.012, G))
        at = PUPPETS["Puppet_Moth"]
        rod(hand, (at[0], at[1] - 0.026), "Puppet_Moth")
        for sx in (-1, 1):  # kite wings
            upper = [(sx * 0.003, 0.008), (sx * 0.03, 0.036), (sx * 0.042, 0.012), (sx * 0.022, -0.004)]
            lower = [(sx * 0.003, -0.002), (sx * 0.024, -0.008), (sx * 0.028, -0.026), (sx * 0.01, -0.03)]
            for wing, spot in ((upper, (sx * 0.026, 0.015)), (lower, (sx * 0.018, -0.016))):
                gilt(wing, at)
                cut(_circ(0.0058, 8), C["gilt_hi"], 15, (at[0] + spot[0], at[1] + spot[1]))
        cut(_circ(0.0052, 10, 3.6), C["dark"], 16, at)
        for sx in (-1, 1):
            cut(_ribbon([(0.0, 0.016), (sx * 0.006, 0.027), (sx * 0.012, 0.03)], 0.002), C["dark"], 16, at)
        # the moon: a tall nightcap with a bobble; the crescent rides a long pole
        b.part = None
        hand, head = player(0.128, C["umber"], C["cream"], hand=1, reach=(0.034, 0.09))
        cap = pm.smooth_outline([(-0.021, 0.008), (0.0, 0.013), (0.021, 0.008), (0.019, 0.03), (0.03, 0.05), (0.01, 0.04), (-0.014, 0.026)], 3)
        cut(cap, C["cream"], 12, head)
        cut(_circ(0.006, 8), C["orange"], 13, (head[0] + 0.031, head[1] + 0.05))
        for i in range(3):
            cut(el.co.star_outline(0.0048), C["cream"], 11, (0.128 - 0.012 + 0.012 * i, G + 0.074 - 0.012 * (i % 2)))
        at = PUPPETS["Puppet_Moon"]
        rod(hand, (at[0] - 0.004, at[1] - 0.024), "Puppet_Moon")
        moon = [(0.03 * math.cos(math.radians(a)), 0.03 * math.sin(math.radians(a))) for a in range(50, 311, 20)] + \
               [(0.014 + 0.0225 * math.cos(math.radians(a)), 0.0225 * math.sin(math.radians(a))) for a in range(270, 89, -20)]
        gilt(moon, at, hi=[[(-0.02 + 0.006 * math.cos(math.radians(a)), 0.002 + 0.015 * math.sin(math.radians(a))) for a in range(0, 360, 40)]])
        cut(el.co.star_outline(0.008), C["gilt_hi"], 15, (at[0] + 0.017, at[1] + 0.004))
        # the bell: its Player stands half out of the frame (the one face Pip never met)
        b.part = None
        hand, _head = player(wx + 0.006, C["sienna"], C["orange"], hand=-1, reach=(0.036, 0.074), low=(wx + 0.04, G + 0.06))
        cut([(-0.03, 0.012), (0.024, 0.012), (0.02, 0.018), (0.016, 0.034), (-0.018, 0.034), (-0.02, 0.018)], C["dark"], 12, (wx + 0.006, G + 0.119))
        cut(_rect(-0.02, 0.02, 0.018, 0.023), C["orange"], 13, (wx + 0.006, G + 0.119))
        at = PUPPETS["Puppet_Bell"]
        rod(hand, (at[0], at[1] + 0.03), "Puppet_Bell")
        bell = pm.smooth_outline([(0.0, 0.028), (0.011, 0.021), (0.015, -0.004), (0.027, -0.019), (-0.027, -0.019), (-0.015, -0.004), (-0.011, 0.021)], 3)
        gilt(bell, at, hi=[_rect(-0.02, 0.02, -0.014, -0.009), _circ(0.004, 8)])
        cut(_circ(0.0062, 10), C["dark"], 13, (at[0], at[1] - 0.024))
        cut(_circ(0.0046, 10), C["gilt"], 14, (at[0], at[1] - 0.024))
        cut(_circ(0.0055, 8), C["gilt"], 14, (at[0], at[1] + 0.031))
        b.part = None
        # ----- the mount, with the Players' crest in its lower corner -----
        ox, oz = PHOTO_W / 2 - PHOTO_MOULD, PHOTO_H / 2 - PHOTO_MOULD
        for strip in (_rect(-ox, ox, wz, oz), _rect(-ox, ox, -oz, -wz), _rect(-ox, -wx, -wz, wz), _rect(wx, ox, -wz, wz)):
            cut(strip, C["mount"], 18)
        for strip in (_rect(-wx, wx, wz - 0.0025, wz), _rect(-wx, wx, -wz, -wz + 0.0025), _rect(-wx, -wx + 0.0025, -wz, wz), _rect(wx - 0.0025, wx, -wz, wz)):
            cut(strip, blend(C["tan"], C["umber"], 0.4), 17)
        b.part = "PhotoCrest"
        el.crest(b, craft, PHOTO_CREST_D, T(PHOTO_CREST[0], -0.012 - step * 19, PHOTO_CREST[1]), step=step * 0.7, fine=True)
        b.part = None
    return draw


def troupe_photo(b, P, craft, M):
    """The framed troupe photo: wall plane y = 0, room toward -Y, origin at the frame centre. A dark timber
    moulding with a gilt fillet, the cream mount, the hanging cord and its brass nail."""
    w, h, m = PHOTO_W, PHOTO_H, PHOTO_MOULD
    part = getattr(b, "part", None)
    with b.at(M):
        b.box(P["paint"], (w - m, 0.012, h - m), (0, -0.006, 0), bevel=0, tint=lc(SEPIA["sky"]), drop=("+y",))
        for sx in (-1, 1):
            b.box(P["post"], (m, 0.05, h), (sx * (w - m) / 2, -0.025, 0), bevel=0.007, tint=1.0)
            b.box(P["rafter"], (w - 2 * m, 0.046, m), (0, -0.023, sx * (h - m) / 2), bevel=0.007, tint=1.05)
            b.box(P["brass"], (0.006, 0.006, h - 2 * m), (sx * (w / 2 - m - 0.003), -0.043, 0), bevel=0, tint=1.0)
            b.box(P["brass"], (w - 2 * m, 0.006, 0.006), (0, -0.043, sx * (h / 2 - m - 0.003)), bevel=0, tint=1.0)
        photo_motif(P, craft)(b)
        b.part = part
        for sx in (-1, 1):
            b.rod(P["paint"], (sx * 0.2, -0.02, h / 2 - 0.005), (0, -0.012, h / 2 + 0.09), 0.004, 4, lc(E["bone_dim"]))
        b.disc(P["brass"], 0.013, 0.012, T(0, -0.014, h / 2 + 0.09), segs=8, tint=1.0)


def build_troupe_photo(craft):
    """The troupe photo as its own model (`TroupePhoto`): the game hangs it at `PHOTO` in the mechanism's frame."""
    b = Builder(213)
    b.part = None
    troupe_photo(b, paints(craft), craft, T())
    return b.finish("TroupePhoto")


def photo_pivots():
    y = -0.012 - PHOTO_STEP * 15
    piv = {name: T(x, y, z) for name, (x, z) in PUPPETS.items()}
    piv["PhotoCrest"] = T(PHOTO_CREST[0], -0.012 - PHOTO_STEP * 19, PHOTO_CREST[1])
    return piv


# --- the interior shell ------------------------------------------------------------------------------------

def solid_quads():
    """What lies behind the single-sided shell, for the export's visibility test: the ground under the hall, the
    nook pit and the middle of the loft slab."""
    x0, x1, y0, y1 = X0 - 0.5, X1 + 0.5, Y0 - 0.5, Y1 + 0.5
    z, zn, zl = FL - 0.004, NOOK_Z - 0.004, LOFT - LOFT_T / 2
    quads = [[(x0, y0, z), (NOOK_X, y0, z), (NOOK_X, y1, z), (x0, y1, z)],
             [(NOOK_X, y0, z), (x1, y0, z), (x1, NOOK_Y - PART_T, z), (NOOK_X, NOOK_Y - PART_T, z)],
             [(NOOK_X, NOOK_Y, zn), (x1, NOOK_Y, zn), (x1, y1, zn), (NOOK_X, y1, zn)]]
    for xa, xb, ya, yb in LOFT_RECTS:
        xa, xb, ya, yb = (x0 if xa == X0 else xa), (x1 if xb == X1 else xb), (y0 if ya == Y0 else ya), (y1 if yb == Y1 else yb)
        quads.append([(xa, ya, zl), (xb, ya, zl), (xb, yb, zl), (xa, yb, zl)])
    return quads


def build_interior(craft):
    P = paints(craft)
    b = Builder(201)
    WAINS, PIC = FL + 1.0, FL + 2.5
    LOW, HIGH = (0.82, 1.08), (0.94, 1.04)
    W, EA, F = T(X0, 0, 0) @ R("Z", 90), T(X1, 0, 0) @ R("Z", -90), T(0, Y0, 0) @ R("Z", 180)

    def stripe(i):
        return 1.06 if i % 2 else 0.93

    def bands(top):
        out = [(FL, WAINS, P["wains"], 0.19, LOW), (WAINS, min(top, PIC), P["upper"], 0.4, stripe)]
        if top > PIC:
            out.append((PIC, top, P["frieze"], 0.9, HIGH))
        return out

    def cap(length, at, rot=0.0, rail=True):
        with b.at(T(*at) @ R("Z", rot)):
            b.box(P["trim_x"], (length, 0.05, 0.07), (0, -0.02, WAINS), bevel=0.014, drop=())
            b.box(P["trim_x"], (length, 0.035, 0.12), (0, -0.012, FL + 0.06), bevel=0.012, tint=0.9)
            if rail:
                b.box(P["trim_x"], (length, 0.04, 0.06), (0, -0.016, PIC), bevel=0.012, tint=1.04)

    def stars(n, x0, x1, seed):  # stencilled stars on the frieze (wall-local)
        rng = random.Random(seed)
        for i in range(n):
            x = x0 + (i + 0.5) * (x1 - x0) / n + rng.uniform(-0.08, 0.08)
            b.prism(P["paint"], el.co.star_outline(rng.uniform(0.05, 0.075)), 0.003, T(x, -0.003, PIC + 0.42 + rng.uniform(-0.12, 0.12)) @ R("Y", rng.uniform(0, 72)),
                    tint=lc(E["candy"]))

    # ----- floors: the hall boards, the loft slab (attic boards above, ceiling boards below) -----
    planks(b, P["floor"], X0, NOOK_X, Y0, Y1, FL)
    planks(b, P["floor"], NOOK_X, X1, Y0, NOOK_Y - PART_T, FL)
    for xa, xb, ya, yb in LOFT_RECTS:
        planks(b, P["floor"], xa, xb, ya, yb, LOFT)
        planks(b, P["board_y"], xa, xb, ya, yb, UNDER, 0.3, up=False, joints=False)
    for y in (1.6, 3.5):  # shallow ceiling beams, well above the camera
        b.box(P["rafter"], (NOOK_X - BAY_X, 0.14, 0.05), ((NOOK_X + BAY_X) / 2, y, UNDER - 0.025), bevel=0.012, drop=())
    b.box(P["trim_x"], (BAY_X - X0, 0.05, LOFT_T + 0.06), ((X0 + BAY_X) / 2, HA_Y0 + 0.022, LOFT - LOFT_T / 2 - 0.03), bevel=0.014)
    b.box(P["trim_y"], (0.05, LAND_Y - HA_Y0, LOFT_T + 0.06), (BAY_X - 0.022, (HA_Y0 + LAND_Y) / 2, LOFT - LOFT_T / 2 - 0.03), bevel=0.014)
    # ----- front wall: the door, two windows on the street -----
    door = (-0.57, 0.57, FL, FL + 2.02)
    fw = [(-2.35 - 0.45, -2.35 + 0.45, FL + 0.8, FL + 2.2), (2.35 - 0.45, 2.35 + 0.45, FL + 0.8, FL + 2.2)]
    with b.at(F):
        grid_wall(b, bands(UNDER), X0, X1, holes=(door, *fw))
        for x0, x1 in ((X0, -0.68), (0.68, X1)):
            cap(x1 - x0, ((x0 + x1) / 2, 0, 0))
        stars(9, X0 + 0.3, X1 - 0.3, 1)
        for i, x in enumerate((-2.35, 2.35)):
            with b.at(T(x, 0, FL)):
                inner_window(b, P, craft, seed=11 + i)
        for sx in (-1, 1):  # door casing
            b.box(P["trim_z"], (0.11, 0.06, 2.1), (sx * 0.625, -0.03, FL + 1.05), bevel=0.016)
            sconce(b, craft, P, T(sx * 1.15, 0, FL + 2.05))
        b.box(P["trim_x"], (1.5, 0.08, 0.12), (0, -0.04, FL + 2.1), bevel=0.018)
        b.box(P["trim_x"], (1.6, 0.11, 0.04), (0, -0.05, FL + 2.18), bevel=0.012, tint=1.05)
        frame_art(b, P, 0.5, 0.36, lambda q: pr.bat(q, P["paper"], 0.36, T(0, -0.02, 0), fold=0.03, curl=0.05), T(0, 0, FL + 2.72), back=E["purple"])
    reveal(b, P["trim_z"], door, 0.04, Y0, 0.85)
    b.box(P["deck_x"], (1.14, 0.2, 0.03), (0, 0.06, FL - 0.012), bevel=0.01, tint=0.9)
    # beyond the door: a closed view box with the porch boards and the painted street
    shadow_box(b, P["dark"], -0.9, 0.9, -1.25, 0.03, FL - 0.02, FL + 2.7, skip=("+y", "-y", "-z"))
    planks(b, P["deck"], -0.9, 0.9, -1.25, 0.03, FL - 0.02, joints=False)
    night_view(b, craft, 1.8, 2.72, T(0, -1.25, FL - 0.02) @ R("Z", 180), "porch", 3)
    # ----- west wall: the hall window, then the stair bay open to the roof -----
    with b.at(W):
        ww = (1.6 - 0.45, 1.6 + 0.45, FL + 0.8, FL + 2.2)
        grid_wall(b, bands(UNDER), Y0, HA_Y0, holes=(ww,))
        grid_wall(b, bands(LOFT), HA_Y0, Y1)
        cap(FOOT_Y - Y0, ((Y0 + FOOT_Y) / 2, 0, 0))
        stars(4, Y0 + 0.2, HA_Y0, 2)
        with b.at(T(1.6, 0, FL)):
            inner_window(b, P, craft, seed=21)
        sconce(b, craft, P, T(0.62, 0, FL + 2.05))
    # ----- east wall: the hall part with its window, and the inside of the nook -----
    with b.at(EA):
        ew = (-1.3 - 0.45, -1.3 + 0.45, FL + 0.8, FL + 2.2)
        grid_wall(b, bands(UNDER), -(NOOK_Y - PART_T), -Y0, holes=(ew,))
        cap(NOOK_Y - PART_T - Y0, (-(NOOK_Y - PART_T + Y0) / 2, 0, 0))
        stars(3, -(NOOK_Y - PART_T) + 0.2, -Y0 - 0.2, 3)
        with b.at(T(-1.3, 0, FL)):
            inner_window(b, P, craft, seed=31)
        nw = (-6.25 - 0.4, -6.25 + 0.4, FL + 0.75, FL + 1.7)
        grid_wall(b, [(NOOK_Z, UNDER, P["dark"], 0.3, LOW)], -Y1, -NOOK_Y, holes=(nw,))
        with b.at(T(-6.25, 0, FL)):
            inner_window(b, P, craft, 0.8, 0.95, 0.75, curtains=False, seed=41, frame="dark")
    # ----- back wall, the nook's floor and walls -----
    grid_wall(b, bands(LOFT), X0, TOP_X, y=Y1)
    grid_wall(b, bands(UNDER), TOP_X, NOOK_X, y=Y1)
    cap(NOOK_X - TOP_X, ((NOOK_X + TOP_X) / 2, Y1, 0))
    with b.at(T(0, Y1, 0)):
        stars(3, TOP_X + 0.2, NOOK_X - 0.2, 4)
    grid_wall(b, [(NOOK_Z, UNDER, P["dark"], 0.3, LOW)], NOOK_X + PART_T, X1, y=Y1)
    face(b, P["dark"], [(NOOK_X + PART_T, NOOK_Y, NOOK_Z), (X1, NOOK_Y, NOOK_Z), (X1, Y1, NOOK_Z), (NOOK_X + PART_T, Y1, NOOK_Z)],
         (2.9, 4.0, 1.0), 0.8)
    grid_wall(b, [(NOOK_Z, UNDER, P["dark"], 0.3, LOW)], NOOK_X + PART_T, X1, y=NOOK_Y, facing=1)
    grid_wall(b, bands(UNDER), NOOK_X, X1, y=NOOK_Y - PART_T)
    cap(X1 - NOOK_X, ((X1 + NOOK_X) / 2, NOOK_Y - PART_T, 0))
    # the drive shaft and the push rod go on up from the mechanism through the nook's ceiling
    b.cyl(P["amber"], 0.035, UNDER - NOOK_Z - 2.3, T(SHAFT_X, MECH_Y, (UNDER + NOOK_Z + 2.3) / 2), segments=8)
    n = 4
    for i in range(n):
        z0, z1 = NOOK_Z + 2.45 + (UNDER - NOOK_Z - 2.45) * i / n, NOOK_Z + 2.45 + (UNDER - NOOK_Z - 2.45) * (i + 1) / n
        b.cyl(P["paint"], 0.02, z1 - z0, T(ROD_X, ROD_Y, (z0 + z1) / 2), segments=6, tint=lc(E["orange"] if i % 2 else E["bone"]), caps=False)
    # ----- the stair: lower flight north along the west wall, landing, upper flight east behind its own wall -----
    for k in range(N1):
        y, z = FOOT_Y + TREAD * k, FL + RISE * k
        face(b, P["wains"], [(X0, y, z), (BAY_X, y, z), (BAY_X, y, z + RISE - 0.04), (X0, y, z + RISE - 0.04)], (-2.8, y - 1, z), b.vary(0.8, 1.0))
        if k:
            b.box(P["floor"], (BAY_X - X0, TREAD + 0.035, 0.045), ((X0 + BAY_X) / 2, y - TREAD / 2 - 0.0175, z - 0.0225), bevel=0, drop=("-z", "+y", "-x"), tint=b.vary(0.85, 1.12))
    planks(b, P["floor"], X0, BAY_X, LAND_Y, Y1, LAND_Z, joints=False)
    side = [(FOOT_Y, FL)]
    for k in range(N1 - 1):
        side += [(FOOT_Y + TREAD * k, FL + RISE * (k + 1) - 0.045), (FOOT_Y + TREAD * (k + 1), FL + RISE * (k + 1) - 0.045)]
    side += [(LAND_Y, LAND_Z - 0.045), (LAND_Y, FL)]
    b.prism(P["wains"], side, 0.04, T(BAY_X, 0, 0) @ R("Z", 90), tint=0.92, smooth=False)
    beam(b, P["trim_x"], (BAY_X + 0.012, FOOT_Y + 0.1, FL + RISE + 0.06), (BAY_X + 0.012, LAND_Y - 0.05, LAND_Z + 0.02), 0.03, 0.14, 0.012)
    b.box(P["trim_y"], (0.03, LAND_Y - FOOT_Y - 0.1, 0.12), (BAY_X + 0.012, (FOOT_Y + LAND_Y) / 2, FL + 0.06), bevel=0.012, tint=0.9)
    for k in range(N1):
        x, z = BAY_X + TREAD * k, LAND_Z + RISE * k
        face(b, P["wains"], [(x, HB_Y0, z), (x, Y1, z), (x, Y1, z + RISE - 0.04), (x, HB_Y0, z + RISE - 0.04)], (x - 1, 6.3, z), b.vary(0.8, 1.0))
        if k:
            b.box(P["floor_x"], (TREAD + 0.035, Y1 - HB_Y0, 0.045), (x - TREAD / 2 - 0.0175, (HB_Y0 + Y1) / 2, z - 0.0225), bevel=0, drop=("-z", "+x", "+y", "-y"), tint=b.vary(0.85, 1.12))
    grid_wall(b, bands(UNDER), BAY_X, TOP_X, y=LAND_Y)  # the stair wall from the hall
    cap(TOP_X - BAY_X, ((TOP_X + BAY_X) / 2, LAND_Y, 0))
    grid_wall(b, [(LAND_Z, LOFT, P["upper"], 0.4, stripe)], BAY_X, TOP_X, y=HB_Y0, facing=1)  # and from the flight
    with b.at(T(TOP_X, 0, 0) @ R("Z", 90)):  # its end, with the cupboard under the stair
        grid_wall(b, bands(UNDER), LAND_Y, Y1)
        cap(Y1 - LAND_Y - 0.9, (LAND_Y + (Y1 - LAND_Y - 0.9) / 2, 0, 0), rail=False)
        b.box(P["trim_x"], (Y1 - LAND_Y, 0.04, 0.06), ((Y1 + LAND_Y) / 2, -0.016, PIC), bevel=0.012, tint=1.04)
        with b.at(T(Y1 - 0.47, 0, FL)):
            b.box(P["door"], (0.66, 0.03, 1.5), (0, -0.012, 0.77), bevel=0.012, tint=1.05)
            for z in (0.4, 1.15):
                b.box(P["door"], (0.44, 0.02, 0.52), (0, -0.03, z), bevel=0.014, tint=1.15)
            for sx in (-1, 1):
                b.box(P["trim_z"], (0.07, 0.04, 1.6), (sx * 0.365, -0.018, 0.8), bevel=0.012)
            b.box(P["trim_x"], (0.8, 0.04, 0.07), (0, -0.018, 1.62), bevel=0.012)
            b.lathe(P["brass"], [(0.0, 0.0), (0.02, 0.0), (0.03, 0.03), (0.0, 0.045)], 8, T(-0.24, -0.03, 0.8) @ R("X", 90), tint=1.0)
    b.box(P["trim_z"], (0.14, 0.14, LOFT + 1.0 - FL), (BAY_X + 0.02, LAND_Y + 0.05, (LOFT + 1.0 + FL) / 2), bevel=0.02)  # the post at the turn
    b.lathe(P["trim_z"], [(0.0, 0.0), (0.08, 0.0), (0.095, 0.05), (0.05, 0.11), (0.0, 0.13)], 10, T(BAY_X + 0.02, LAND_Y + 0.05, LOFT + 1.0))
    # balustrade of the lower flight: newel, square balusters, handrail up to the post at the turn
    rx, ny = BAY_X - 0.02, FOOT_Y - 0.07
    b.box(P["trim_z"], (0.13, 0.13, 1.05), (rx, ny, FL + 0.525), bevel=0.02)
    b.lathe(P["trim_z"], [(0.0, 0.0), (0.075, 0.0), (0.09, 0.05), (0.05, 0.11), (0.0, 0.13)], 10, T(rx, ny, FL + 1.05))
    r0, r1 = Vector((rx, ny, FL + 0.95)), Vector((rx, LAND_Y, LAND_Z + 0.95))
    beam(b, P["trim_x"], r0, r1, 0.07, 0.06, 0.016)
    for k in range(N1 - 1):
        for f in (0.25, 0.75):
            y, z = FOOT_Y + TREAD * (k + f), FL + RISE * (k + 1)
            top = r0.z + (r1.z - r0.z) * (y - r0.y) / (r1.y - r0.y)
            b.box(P["trim_z"], (0.04, 0.04, top - z), (rx, y, (top + z) / 2), bevel=0, drop=("+z", "-z"))
    # ----- the workshop partition: wainscot, grille, panel opening (hideable for the cutaway) -----
    b.part = "NookWall"
    with b.at(MECH):
        f0, top = MECH_FLOOR, UNDER - NOOK_Z
        wx0, wx1 = -(Y1 - MECH_Y), MECH_Y - (NOOK_Y - PART_T)
        holes = (GRILLE, PANEL)
        grid_wall(b, [(f0, f0 + 1.0, P["wains"], 0.19, LOW), (f0 + 1.0, f0 + 2.5, P["upper"], 0.4, stripe), (f0 + 2.5, top, P["frieze"], 0.9, HIGH)],
                  wx0, wx1, holes)
        grid_wall(b, [(0.0, top, P["dark"], 0.3, LOW)], wx0, wx1 - PART_T, holes, y=PART_T, facing=1)
        for hole in holes:
            reveal(b, P["trim_z"], hole, 0.0, PART_T, 0.8)
        for x0, x1 in ((wx0, PANEL[0] - 0.07), (PANEL[1] + 0.07, wx1)):
            b.box(P["trim_x"], (x1 - x0, 0.05, 0.07), ((x0 + x1) / 2, -0.02, f0 + 1.0), bevel=0.014)
        for x0, x1 in ((wx0, GRILLE[0] - 0.08), (GRILLE[1] + 0.08, wx1)):
            b.box(P["trim_x"], (x1 - x0, 0.035, 0.12), ((x0 + x1) / 2, -0.012, f0 + 0.06), bevel=0.012, tint=0.9)
        b.box(P["trim_x"], (wx1 - wx0, 0.04, 0.06), ((wx0 + wx1) / 2, -0.016, f0 + 2.5), bevel=0.012, tint=1.04)
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
        pk.owl_motif(b, P, 0.6, T(1.25, -0.004, f0 + 1.12))  # the painted owl, as on the shutter outside
    b.part = None
    # ----- the front door from inside (both faces: it swings into the hall) and its wooden latch -----
    b.part = "InnerDoor"
    with b.at(T(0, 0.036, FL)):
        hw, hh = 0.55, 2.02
        b.box(P["door"], (2 * hw, 0.03, hh), (0, 0.015, hh / 2), bevel=0, tint=0.85)
        for sx in (-1, 1):
            b.box(P["door"], (0.13, 0.025, hh), (sx * (hw - 0.065), 0.04, hh / 2), bevel=0.01)
            b.box(P["door"], (0.13, 0.02, hh), (sx * (hw - 0.065), -0.008, hh / 2), bevel=0.008)
        for z, h in ((0.09, 0.18), (0.8, 0.13), (hh - 0.07, 0.14)):
            b.box(P["door_x"], (2 * hw - 0.26, 0.025, h), (0, 0.04, z), bevel=0.01)
            b.box(P["door_x"], (2 * hw - 0.26, 0.02, h), (0, -0.008, z), bevel=0.008)
        for i in range(8):  # the ticket shutter from the street side, with the painted owl below
            z = 1.06 + (i + 0.5) * 0.66 / 8
            b.box(P["slat"], (0.66, 0.016, 0.66 / 8 - 0.006), (0, -0.006, z), bevel=0, drop=("+y",), tint=b.vary(0.9, 1.1))
        b.prism(P["orange"], [(0.0, -0.16), (0.12, 0.0), (0.0, 0.16), (-0.12, 0.0)], 0.01, T(0, -0.02, 0.45), tint=1.0)
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
        b.lathe(P["brass"], [(0.0, 0.0), (0.022, 0.0), (0.016, 0.03), (0.034, 0.05), (0.03, 0.075), (0.0, 0.085)], 10,
                T(hw - 0.07, -0.018, 0.95) @ R("X", 90), tint=1.0)
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
    # ----- hall dressing -----
    b.box(P["felt"], (0.9, 0.55, 0.025), (0, 0.85, FL + 0.012), bevel=0.008, tint=1.0)
    b.prism(P["felt_v"], el.co.star_outline(0.16), 0.004, T(0, 0.85, FL + 0.027) @ R("X", -90) @ T(0, -0.004, 0), tint=1.0)
    rug = [(0.0, 0.0), (1.0, 0.0), (1.02, 0.012), (0.98, 0.022), (0.0, 0.022)]
    rm = T(0.0, 3.0, FL) @ S(1.45, 1.15, 1.0)
    b.lathe(P["cloth"], rug, 24, rm, tint=1.0)
    b.lathe(P["felt"], [(0.0, 0.022), (0.8, 0.022), (0.8, 0.026), (0.0, 0.026)], 24, rm, tint=1.0)
    b.lathe(P["cloth"], [(0.0, 0.026), (0.62, 0.026), (0.62, 0.03), (0.0, 0.03)], 24, rm, tint=1.15)
    b.prism(P["felt"], el.co.star_outline(0.5), 0.004, T(0.0, 3.0, FL + 0.034) @ R("X", -90) @ T(0, -0.004, 0), tint=1.0)
    with b.at(T(1.2, Y1, FL)):  # the coat rack and bench in the alcove under the loft
        b.box(P["amber"], (1.5, 0.38, 0.05), (0, -0.22, 0.42), bevel=0.016)
        for sx in (-1, 1):
            b.box(P["amber"], (0.06, 0.34, 0.4), (sx * 0.66, -0.22, 0.2), bevel=0.014)
        b.box(P["amber"], (1.3, 0.04, 0.1), (0, -0.07, 0.3), bevel=0.012)
        b.lathe(P["felt_v"], [(0.0, 0.0), (0.2, 0.0), (0.22, 0.03), (0.16, 0.06), (0.0, 0.065)], 12, T(-0.42, -0.22, 0.445) @ S(1.2, 0.8, 1.0), tint=1.0)
        b.box(P["trim_x"], (1.7, 0.04, 0.12), (0, -0.02, 1.55), bevel=0.014)
        b.box(P["trim_x"], (1.78, 0.2, 0.04), (0, -0.1, 1.72), bevel=0.014, tint=1.04)
        for i in range(6):
            b.rod(P["orange"], (-0.7 + 0.28 * i, -0.04, 1.55), (-0.7 + 0.28 * i, -0.14, 1.59), 0.016, 6, 1.0)
        hat = craft.felt(E["purple"])
        b.lathe(hat, [(0.0, 0.0), (0.2, 0.0), (0.21, 0.012), (0.1, 0.03), (0.07, 0.16), (0.02, 0.3), (0.0, 0.33)], 12,
                T(-0.7, -0.15, 1.4) @ R("X", -72), tint=1.0)
        b.lathe(P["orange"], [(0.1, 0.03), (0.105, 0.05), (0.094, 0.07)], 12, T(-0.7, -0.15, 1.4) @ R("X", -72), tint=1.0)
        coat = [(-0.06, 0.0), (0.06, 0.0), (0.2, -0.1), (0.24, -0.5), (0.27, -0.95), (0.02, -0.92), (-0.25, -0.97), (-0.23, -0.5), (-0.2, -0.1)]
        b.prism(P["cloth"], coat, 0.09, T(-0.14, -0.15, 1.56), tint=1.0, bevel=0.03)  # Kit's shawl coat
        b.prism(craft.felt(el.V["orange"]), [(-0.05, 0.0), (0.05, 0.0), (0.09, -0.6), (0.03, -0.72), (-0.07, -0.62)], 0.05, T(0.42, -0.14, 1.56), tint=1.0, bevel=0.02)
        b.prism(craft.felt(E["purple"]), [(-0.05, 0.0), (0.05, 0.0), (0.16, -0.12), (0.2, -0.62), (-0.04, -0.7), (-0.19, -0.6), (-0.15, -0.12)], 0.06, T(0.7, -0.14, 1.56),
                tint=1.0, bevel=0.02)
        b.cyl(P["paint"], 0.13, 0.14, T(0.1, -0.1, 1.81), segments=12, tint=lc(E["plum"]))  # hat box
        b.cyl(P["paint"], 0.137, 0.04, T(0.1, -0.1, 1.89), segments=12, tint=lc(E["orange"]))
        for dx, rz in ((0.2, 12), (0.42, -8)):  # boots under the bench
            with b.at(T(dx, -0.2, 0) @ R("Z", rz)):
                b.box(P["ink"], (0.11, 0.26, 0.09), (0, -0.03, 0.045), bevel=0.03, tint=1.0)
                b.cyl(P["ink"], 0.055, 0.2, T(0, 0.05, 0.17), segments=8, tint=1.0)
        sconce(b, craft, P, T(-0.95, 0, 2.05))
        frame_art(b, P, 0.34, 0.42, lambda q: pk.owl_motif(q, P, 0.28, T(0, -0.014, -0.14), depth=0.005), T(0.95, 0, 2.2))
    with b.at(T(0, LAND_Y, FL)):  # framed cut-outs on the stair wall
        mots = (lambda q: pr.bat(q, P["paper"], 0.3, T(0, -0.02, 0), fold=0.03, curl=0.05),
                lambda q: q.prism(P["paper"], [(0.13 * math.cos(math.radians(a)), 0.13 * math.sin(math.radians(a))) for a in range(60, 301, 30)]
                                  + [(0.06 + 0.1 * math.cos(math.radians(a)), 0.1 * math.sin(math.radians(a))) for a in range(270, 89, -30)], 0.004,
                                  T(0, -0.02, 0), tint=lc(E["candy"])),
                lambda q: q.prism(P["paper"], el.co.star_outline(0.14), 0.004, T(0, -0.02, 0), tint=lc(E["candy"])))
        for i, (mot, (x, z, w, h)) in enumerate(zip(mots, ((-1.7, 1.85, 0.46, 0.42), (-1.0, 1.6, 0.42, 0.42), (-0.35, 1.9, 0.4, 0.46)))):
            frame_art(b, P, w, h, mot, T(x, 0, z), back=(E["night"], E["purple"], E["plum"])[i])
    with b.at(T(2.9, NOOK_Y - PART_T, FL)):  # the long-case clock on the nook's end wall
        b.box(P["door"], (0.46, 0.26, 0.3), (0, -0.14, 0.15), bevel=0.02)
        b.box(P["door"], (0.36, 0.2, 1.1), (0, -0.12, 0.85), bevel=0.016, tint=0.95)
        b.box(P["door"], (0.48, 0.27, 0.5), (0, -0.14, 1.65), bevel=0.02)
        b.prism(P["door_x"], [(-0.27, 0.0), (0.27, 0.0), (0.0, 0.2)], 0.28, T(0, -0.28, 1.9), bevel=0.012)
        b.disc(P["paint"], 0.17, 0.01, T(0, -0.285, 1.66), segs=16, tint=lc(E["bone"]))
        b.ring(P["brass"], 0.17, 0.2, 0.02, T(0, -0.295, 1.66), segs=16, tint=1.0)
        b.box(P["ink"], (0.014, 0.008, 0.12), (0, -0.292, 1.71), bevel=0, tint=1.0)
        b.box(P["ink"], (0.014, 0.008, 0.09), (0.03, -0.292, 1.68), bevel=0, rot=R("Y", 55), tint=1.0)
        b.box(P["night"], (0.2, 0.01, 0.7), (0, -0.222, 0.86), bevel=0, tint=0.9)
        b.rod(P["brass"], (0, -0.232, 1.2), (0.05, -0.232, 0.72), 0.008, 4, 1.0)
        b.disc(P["brass"], 0.06, 0.012, T(0.055, -0.24, 0.68), segs=10, tint=1.0)
    with b.at(EA @ T(-1.3, 0, FL)):  # console under the east window: a lamp, the candy bowl
        b.box(P["amber"], (1.0, 0.34, 0.045), (0, -0.19, 0.72), bevel=0.014)
        for sx in (-1, 1):
            b.box(P["amber"], (0.05, 0.05, 0.7), (sx * 0.44, -0.3, 0.35), bevel=0.012)
            b.box(P["amber"], (0.05, 0.05, 0.7), (sx * 0.44, -0.07, 0.35), bevel=0.012)
        b.box(P["amber"], (0.9, 0.03, 0.1), (0, -0.33, 0.64), bevel=0.01)
        b.lathe(glow(b, craft, "Lamp", E["glow"], 2.4), [(0.0, 0.0), (0.06, 0.0), (0.075, 0.06), (0.06, 0.13), (0.0, 0.13)], 8, T(-0.33, -0.19, 0.75), tint=1.0)
        b.glow_tag = None
        b.lathe(P["ink"], [(0.065, 0.13), (0.07, 0.145), (0.03, 0.17), (0.0, 0.18)], 8, T(-0.33, -0.19, 0.75), tint=1.0)
        b.lathe(P["ink"], [(0.07, -0.0), (0.075, 0.012), (0.06, 0.014)], 8, T(-0.33, -0.19, 0.745), tint=1.0)
        pr.pumpkin(b, craft, 0.14, 0.8, T(0.25, -0.19, 0.745), style=1, rgb=H(0xF59A23), per_lobe=2)
        for i, rgb in enumerate((E["candy"], E["bone"], E["orange"])):
            b.box(P["paint"], (0.22, 0.15, 0.006), (-0.05 + 0.01 * i, -0.2, 0.746 + 0.006 * i), bevel=0, rot=R("Z", 14 * i - 10), tint=lc(rgb))
    trunk(b, P, (0.85, 0.48, 0.5), T(X0 + 0.34, 1.6, FL) @ R("Z", 90), body="shutter_z")  # blanket chest under the west window
    b.lathe(P["shutter_z"], [(0.0, 0.0), (0.12, 0.0), (0.13, 0.5), (0.14, 0.52), (0.11, 0.52), (0.1, 0.03), (0.0, 0.03)], 10, T(-0.95, 0.42, FL))  # umbrella stand
    b.rod(P["ink"], (-0.95, 0.42, FL + 0.05), (-0.9, 0.36, FL + 0.95), 0.014, 5, 1.0)
    b.rod(P["orange"], (-0.97, 0.44, FL + 0.05), (-1.03, 0.47, FL + 0.85), 0.02, 6, 1.0, r2=0.008)
    for i, (y, z, s, rz) in enumerate(((3.7, 2.6, 0.3, 14), (4.5, 3.25, 0.24, -10), (5.2, 3.9, 0.22, 24), (6.1, 4.3, 0.26, -16))):  # paper bats up the stair
        pr.bat(b, P["paper"], s, T(X0 + 0.02, y, z) @ R("Z", 90) @ R("Y", rz), fold=0.25, curl=0.3)
    foot, tip = Vector((0.95, Y0 + 0.5, FL)), Vector((0.98, Y0 + 0.06, FL + 1.5))  # broom by the door
    aim = (tip - foot).to_track_quat("Z", "Y").to_matrix().to_4x4()
    b.rod(P["amber"], foot.lerp(tip, 0.2), tip, 0.018, 6)
    b.lathe(craft.tinted(H(0xC9A24B), 0.9, 0.3, 30.0), [(0.0, 0.0), (0.13, 0.0), (0.1, 0.2), (0.035, 0.36), (0.03, 0.4)], 9, T(*foot) @ aim,
            radial=lambda q, i: 1 + 0.12 * math.cos(9 * q) * (i < 3))
    pr.pumpkin(b, craft, 0.2, 0.8, T(BAY_X + 0.35, FOOT_Y - 0.45, FL) @ R("Z", -60), style=0, per_lobe=2)
    pr.pumpkin(b, craft, 0.15, 0.8, T(-1.25, 0.5, FL), style=None, rgb=H(0xF59A23), per_lobe=2)
    crate(b, P, (0.5, 0.42, 0.36), T(-1.6, LAND_Y - 0.3, FL) @ R("Z", 8))
    pr.bunting(b, craft, (BAY_X + 0.03, HA_Y0 + 0.1, UNDER - 0.02), (BAY_X + 0.03, LAND_Y - 0.1, UNDER - 0.02), 0.12, 9, 0.2, 0.25)
    pr.bat_garland(b, craft, (BAY_X + 0.2, LAND_Y - 0.03, FL + 3.0), (TOP_X - 0.1, LAND_Y - 0.03, FL + 3.0), 0.16, 4, 0.24)
    lamp = glow(b, craft, "Lamp", E["candy"], 2.0)  # star lamp flush under the ceiling
    with b.at(T(0.0, 3.0, UNDER - 0.3)):
        b.rod(P["ink"], (0, 0, 0.14), (0, 0, 0.3), 0.008, 4, 1.0)
        for rz in (0, 90):
            b.prism(lamp, el.co.star_outline(0.2), 0.012, R("Z", rz) @ T(0, -0.006, 0), tint=1.0)
    b.glow_tag = None
    b.finish("InteriorShell")

    # =================== the attic ===================
    a = Builder(202)
    a.part = "Attic"
    # rails round the stairwell
    with a.at(T(BAY_X + 0.06, (HA_Y0 + LAND_Y) / 2, LOFT) @ R("Z", 90)):
        pk.rail(a, P, LAND_Y - HA_Y0 - 0.2, rhythm=1)
    with a.at(T((X0 + BAY_X) / 2 + 0.15, HA_Y0 - 0.06, LOFT)):
        pk.rail(a, P, BAY_X - X0 - 0.3)
    with a.at(T((BAY_X + TOP_X) / 2 + 0.04, HB_Y0 - 0.05, LOFT)):
        pk.rail(a, P, TOP_X - BAY_X - 0.2, rhythm=1)
    for x, y in ((BAY_X + 0.06, HA_Y0 - 0.06), (TOP_X, HB_Y0 - 0.05)):
        with a.at(T(x, y, LOFT)):
            pk.post(a, P, 0.95, ())
    # knee walls, roof boards, rafters, collar ties, ridge
    ang = math.degrees(math.atan(SLOPE))
    n = 12
    for sx in (-1, 1):
        with a.at(T(sx * X1, 0, 0) @ R("Z", -sx * 90)):
            spans = ((Y0, ALC_Y0), (ALC_Y1, Y1)) if sx < 0 else ((Y0, Y1),)
            for y0, y1 in spans:
                lo, hi = (y0, y1) if sx < 0 else (-y1, -y0)
                grid_wall(a, [(LOFT, KNEE, P["wains"], 0.19, LOW)], lo, hi)
                a.box(P["trim_x"], (hi - lo, 0.05, 0.07), ((lo + hi) / 2, -0.02, KNEE - 0.02), bevel=0.014)
                a.box(P["trim_x"], (hi - lo, 0.03, 0.1), ((lo + hi) / 2, -0.01, LOFT + 0.05), bevel=0.01, tint=0.9)
        for i in range(n):
            za, zb = KNEE + (APEX - KNEE) * i / n, KNEE + (APEX - KNEE) * (i + 1) / n
            xa, xb = sx * ceil_x(za), sx * ceil_x(zb)
            parts = [(Y0, Y1)]
            if sx < 0:
                parts = [(Y0, ALC_Y0), (ALC_Y1, Y1)] + ([(ALC_Y0, ALC_Y1)] if za >= ALC_RIDGE - 1e-6 else [])
            for y0, y1 in parts:
                cuts = [y0, y1] if y1 - y0 < 2.5 else [y0, a.rng.uniform(y0 + 0.8, y1 - 0.8), y1]
                for ya, yb in zip(cuts, cuts[1:]):
                    face(a, P["board_y"], [(xa, ya, za), (xa, yb, za), (xb, yb, zb), (xb, ya, zb)], (0, 3.5, 5.0), a.vary(0.78, 1.08))
        for y in (0.45, 2.5, 3.5, 4.5, 5.5, 6.6):
            c = Vector((sx * X1 / 2, y, (KNEE + APEX) / 2 - 0.075))
            a.box(P["rafter"], (math.hypot(X1, APEX - KNEE), 0.08, 0.11), tuple(c), bevel=0.014, rot=R("Y", sx * ang), drop=())
    # mouth of the dormer in the west slope: fill between the alcove gable and the board courses above it
    zr = KNEE + (APEX - KNEE) * math.ceil((ALC_RIDGE - KNEE) / (APEX - KNEE) * n - 1e-6) / n
    mouth = [(-ceil_x(ALC_EAVE), ALC_Y0, ALC_EAVE), (-ceil_x(ALC_RIDGE), DORM_Y, ALC_RIDGE), (-ceil_x(ALC_EAVE), ALC_Y1, ALC_EAVE),
             (-ceil_x(zr), ALC_Y1, zr), (-ceil_x(zr), ALC_Y0, zr)]
    face(a, P["board_y"], mouth, (0, 3.5, 5.0), 0.92)
    COLLAR = LOFT + 3.2
    for y in (2.5, 4.5, 6.6):
        hw = ceil_x(COLLAR + 0.05)
        a.box(P["rafter"], (2 * hw, 0.07, 0.1), (0, y - 0.075, COLLAR), bevel=0.014)
    a.box(P["rafter_y"], (0.1, Y1 - Y0, 0.16), (0, (Y0 + Y1) / 2, APEX - 0.1), bevel=0.016, drop=("+z",))
    # gables
    m = 16
    for yy in (Y0, Y1):
        for i in range(m):
            za, zb = LOFT + (APEX - LOFT) * i / m, LOFT + (APEX - LOFT) * (i + 1) / m
            xa, xb = min(X1, ceil_x(za)), min(X1, ceil_x(zb))
            pts = [(-xa, yy, za), (xa, yy, za), (xb, yy, zb), (-xb, yy, zb)]
            if za < KNEE < zb:
                pts = [(-X1, yy, za), (X1, yy, za), (X1, yy, KNEE), (xb, yy, zb), (-xb, yy, zb), (-X1, yy, KNEE)]
            face(a, P["board_x"], pts, (0, 3.5, za), a.vary(0.8, 1.08))
    with a.at(F):
        a.box(P["trim_x"], (2 * X1, 0.03, 0.1), (0, -0.01, LOFT + 0.05), bevel=0.01, tint=0.9)
        # the round window from inside: lit paper in a fat ring, under a bracket that carries the owl lantern
        wz, wr = ROUND_Z, 0.6
        a.disc(glow(a, craft, "AtticWindowIn", E["glow"], 1.6), wr + 0.02, 0.012, T(0, -0.014, wz), segs=28, tint=1.0)
        a.glow_tag = None
        a.ring(P["trim_x"], wr, wr + 0.16, 0.09, T(0, -0.09, wz), segs=28, tint=1.0)
        a.ring(P["orange"], wr + 0.16, wr + 0.195, 0.05, T(0, -0.05, wz), segs=28, tint=0.95)
        a.box(P["trim_z"], (0.05, 0.035, wr * 2), (0, -0.04, wz), bevel=0.01)
        a.box(P["trim_x"], (wr * 2, 0.035, 0.05), (0, -0.04, wz), bevel=0.01)
        for k in range(4):
            q = math.radians(90 * k)
            a.box(P["trim_x"], (0.12, 0.11, 0.18), (math.sin(q) * (wr + 0.085), -0.055, math.cos(q) * (wr + 0.085)), bevel=0.02,
                  rot=R("Y", 90 * k), tint=1.06)
    a.box(P["trim_x"], (X1 - TOP_X, 0.03, 0.1), ((X1 + TOP_X) / 2, Y1 - 0.01, LOFT + 0.05), bevel=0.01, tint=0.9)
    # ----- June's dormer alcove: the round-topped window is open, its sill is the floor level -----
    wx, hw, sp = X0, WIN_W / 2, WIN_SPRING
    zs = SILL_Z + sp
    arch = [(ALC_Y0, zs), (DORM_Y - hw, zs)] + [(y, z) for y, z in arc(DORM_Y, zs, hw, 180, 0, 12)][1:] + [(ALC_Y1, zs), (ALC_Y1, ALC_EAVE),
                                                                                                        (DORM_Y, ALC_RIDGE), (ALC_Y0, ALC_EAVE)]
    face(a, P["wains"], [(wx, y, z) for y, z in arch], (0, DORM_Y, zs), 0.95)
    for y0, y1 in ((ALC_Y0, DORM_Y - hw), (DORM_Y + hw, ALC_Y1)):
        face(a, P["wains"], [(wx, y0, LOFT), (wx, y1, LOFT), (wx, y1, zs), (wx, y0, zs)], (0, DORM_Y, 5), a.vary(0.9, 1.05))
    for y in (ALC_Y0, ALC_Y1):
        face(a, P["wains"], [(wx, y, KNEE), (wx, y, ALC_EAVE), (-ceil_x(ALC_EAVE), y, ALC_EAVE)], (0, DORM_Y, 5.5), 0.88)
    for sy in (-1, 1):
        y = DORM_Y + sy * ALC_HALF
        face(a, P["board_y"], [(wx, y, ALC_EAVE), (wx, DORM_Y, ALC_RIDGE), (-ceil_x(ALC_RIDGE), DORM_Y, ALC_RIDGE), (-ceil_x(ALC_EAVE), y, ALC_EAVE)],
             (-2.4, DORM_Y, 5.0), 0.85 + 0.1 * sy)
    with a.at(T(wx, DORM_Y, SILL_Z) @ R("Z", 90)):  # window surround, room toward local -Y
        for sx in (-1, 1):
            a.box(P["trim_z"], (0.09, 0.07, sp), (sx * (hw + 0.045), -0.03, sp / 2), bevel=0.016)
        a.ring(P["trim_x"], hw, hw + 0.09, 0.07, T(0, -0.065, sp), a0=0, a1=180, segs=12, tint=1.0)
        a.box(P["trim_x"], (0.13, 0.1, 0.15), (0, -0.045, sp + hw + 0.05), bevel=0.02, tint=1.06)
        a.box(P["trim_x"], (WIN_W + 0.3, 0.34, 0.05), (0, 0.06, -0.022), bevel=0.014, tint=1.02)  # the sill board, flush with the floor
        rv = hw - 0.004
        for sx, x in ((-1, -rv), (1, rv)):  # reveal through the wall thickness
            face(a, P["trim_z"], [(x, 0, 0), (x, 0.16, 0), (x, 0.16, sp), (x, 0, sp)], (0, 0.08, sp / 2), 0.85)
        pts = [(x, z) for x, z in arc(0.0, sp, rv, 0, 180, 12)]
        for (xa, za), (xb, zb) in zip(pts, pts[1:]):
            face(a, P["trim_x"], [(xa, 0, za), (xb, 0, zb), (xb, 0.16, zb), (xa, 0.16, za)], (0, 0.08, sp), 0.85)
        lw = hw - 0.02
        with a.at(T(hw + 0.02, 0.0, 0.0) @ R("Z", 98)):  # the inner shutter leaf, swung back clear of the way in
            for z in (0.04, sp / 2, sp - 0.04):
                a.box(P["shutter"], (lw, 0.03, 0.07), (-lw / 2, 0, z), bevel=0.008)
            for x in (-0.03, -lw + 0.03):
                a.box(P["shutter_z"], (0.06, 0.03, sp), (x, 0, sp / 2), bevel=0.008)
            for k in range(6):
                a.box(P["shutter"], (lw - 0.1, 0.012, 0.09), (-lw / 2, 0, 0.1 + 0.13 * k + (0.08 if k > 2 else 0)), rot=R("X", 28), bevel=0)
    # beyond the window: a closed view box with the bridge boards running out to the painted yard
    bx0, bx1 = X0 - VIEW_D, X0 - 0.16
    shadow_box(a, P["dark"], bx0, bx1, DORM_Y - 0.85, DORM_Y + 0.85, SILL_Z - 0.03, SILL_Z + 1.9, skip=("+x", "-x", "-z"))
    for i in range(7):
        x0, x1 = bx0 + (bx1 - bx0) * i / 7, bx0 + (bx1 - bx0) * (i + 1) / 7
        face(a, P["floor"], [(x0, DORM_Y - 0.85, SILL_Z - 0.03), (x1, DORM_Y - 0.85, SILL_Z - 0.03), (x1, DORM_Y + 0.85, SILL_Z - 0.03), (x0, DORM_Y + 0.85, SILL_Z - 0.03)],
             (x0, DORM_Y, SILL_Z + 1), a.vary(0.7, 1.0))
    for sy in (-1, 1):
        for z in (0.35, 0.75):
            a.rod(P["amber"], (bx0, DORM_Y + sy * 0.46, SILL_Z + z), (bx1, DORM_Y + sy * 0.46, SILL_Z + z), 0.03, 6)
        a.box(P["amber"], (0.08, 0.08, 0.85), (bx0 + 0.5, DORM_Y + sy * 0.46, SILL_Z + 0.4), bevel=0.014)
    night_view(a, craft, 1.7, 1.93, T(bx0, DORM_Y, SILL_Z - 0.03) @ R("Z", 90), "yard", 5)
    a.part = "SillSnag"  # the matching snag on the jamb (evidence)
    cloth_snag(a, craft, T(*SNAG) @ R("Z", 90), 0.8)
    a.part = "Attic"
    # ----- June's corner: stool, rug, star lantern -----
    stool(a, P, T(*STOOL, LOFT), cushion=P["felt_v"])
    rm = T(STOOL[0] + 1.0, STOOL[1] + 0.1, LOFT) @ S(1.2, 0.95, 1.0)
    a.lathe(P["cloth"], rug, 22, rm, tint=1.0)
    a.lathe(P["felt"], [(0.0, 0.022), (0.76, 0.022), (0.76, 0.026), (0.0, 0.026)], 22, rm, tint=1.0)
    a.prism(P["felt_v"], el.co.star_outline(0.42), 0.004, T(STOOL[0] + 1.0, STOOL[1] + 0.1, LOFT + 0.03) @ R("X", -90) @ T(0, -0.004, 0), tint=1.0)
    for i, (dx, dy, rgb) in enumerate(((0.55, -0.5, el.V["orange"]), (0.75, 0.55, el.V["violet"]))):  # floor cushions
        a.lathe(craft.felt(rgb), [(0.0, 0.0), (0.2, 0.0), (0.24, 0.05), (0.2, 0.1), (0.0, 0.11)], 10, T(STOOL[0] + dx, STOOL[1] + dy, LOFT + 0.03) @ S(1.0, 1.0, 1.0), tint=1.0)
    star = glow(a, craft, "AtticStar", E["candy"], 2.0)
    with a.at(T(STOOL[0] + 0.9, 2.42, LOFT + 2.85)):
        a.rod(P["paint"], (0, 0, 0.1), (0, 0, COLLAR - LOFT - 2.85 - 0.05), 0.004, 3, lc(E["bone_dim"]))
        for rz in (0, 90):
            a.prism(star, el.co.star_outline(0.2), 0.012, R("Z", rz) @ T(0, -0.006, 0), tint=1.0)
    a.glow_tag = None
    with a.at(T(2.85, 2.9, LOFT) @ R("Z", -96)):  # craft table: bats in the making, paint pots, paper, a pumpkin
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
    # trunks, crates, books and a cardboard moon along the low sides
    trunk(a, P, (0.95, 0.52, 0.56), T(3.0, 4.75, LOFT) @ R("Z", -90), lid=24.0)
    trunk(a, P, (0.7, 0.42, 0.44), T(3.05, 5.85, LOFT) @ R("Z", -82), body="shutter_z")
    trunk(a, P, (0.8, 0.46, 0.5), T(-1.9, 0.5, LOFT) @ R("Z", 172), body="door_x")
    crate(a, P, (0.6, 0.5, 0.42), T(2.95, 0.75, LOFT) @ R("Z", 6))
    crate(a, P, (0.45, 0.4, 0.3), T(2.92, 0.72, LOFT + 0.42) @ R("Z", -14), tint=0.9)
    crate(a, P, (0.5, 0.45, 0.36), T(2.3, 0.55, LOFT) @ R("Z", -8))
    pr.pumpkin(a, craft, 0.2, 0.8, T(1.6, 0.55, LOFT) @ R("Z", 100), style=1, rgb=H(0xF59A23), per_lobe=2)
    moon = [(0.3 * math.cos(math.radians(d)), 0.3 * math.sin(math.radians(d))) for d in range(60, 301, 24)] + \
           [(0.13 + 0.23 * math.cos(math.radians(d)), 0.23 * math.sin(math.radians(d))) for d in range(276, 83, -24)]
    a.prism(P["candy"], moon, 0.025, T(-0.9, Y0 + 0.12, LOFT + 0.34) @ R("Z", 180) @ R("X", 12) @ R("Y", -12), tint=1.0, bevel=0.006)  # cardboard moon
    with a.at(T(1.9, Y1, LOFT)):  # bookshelf on the back gable
        for sx in (-1, 1):
            a.box(P["amber"], (0.05, 0.3, 1.3), (sx * 0.6, -0.16, 0.65), bevel=0.012)
        rng = random.Random(7)
        for k, z in enumerate((0.04, 0.46, 0.88, 1.3)):
            a.box(P["amber"], (1.25, 0.3, 0.04), (0, -0.16, z), bevel=0.01)
            if k == 3:
                break
            x = -0.56
            while x < 0.5:
                w, h = rng.uniform(0.05, 0.11), rng.uniform(0.24, 0.36)
                if rng.random() < 0.85:
                    a.box(P["paint"], (w, 0.2, h), (x + w / 2, -0.17, z + 0.02 + h / 2), bevel=0, drop=("+y", "-z"),
                          tint=lc(rng.choice((E["plum"], E["orange"], E["purple"], E["candy"], E["indigo_light"], E["bone_dim"]))))
                x += w + 0.008
        a.lathe(glow(a, craft, "Lamp", E["glow"], 2.4), [(0.0, 0.0), (0.06, 0.0), (0.075, 0.06), (0.06, 0.13), (0.0, 0.13)], 8, T(0.3, -0.16, 1.33), tint=1.0)
        a.glow_tag = None
        a.lathe(P["ink"], [(0.065, 0.13), (0.07, 0.145), (0.03, 0.17), (0.0, 0.18)], 8, T(0.3, -0.16, 1.33), tint=1.0)
    for i, (x, y, r, rgb) in enumerate(((-1.0, 0.55, 0.2, E["plum"]), (-1.0, 0.55, 0.16, E["orange"]), (-0.55, 0.45, 0.17, E["purple"]))):  # hat boxes
        z = LOFT + (0.2 if i == 1 else 0.0)
        a.cyl(P["paint"], r, 0.2, T(x, y, z + 0.1), segments=12, tint=lc(rgb))
        a.cyl(P["paint"], r + 0.008, 0.045, T(x, y, z + 0.2), segments=12, tint=lc(shade(rgb, 1.25)))
    # garlands: bats and bunting high between the collar ties, star strings along the low slopes
    pr.bat_garland(a, craft, (-0.9, 2.45, COLLAR - 0.04), (0.9, 4.42, COLLAR - 0.04), 0.3, 4, 0.26)
    pr.bunting(a, craft, (-0.9, 4.45, COLLAR - 0.04), (0.9, 6.5, COLLAR - 0.04), 0.3, 7, 0.2, 0.26, start=2)
    for sx in (-1, 1):
        x, z = sx * 2.75, ceil_z(2.75) - 0.12
        ys = [0.45, 2.5, 3.5, 4.5, 5.5, 6.6]
        for y0, y1 in zip(ys, ys[1:]):
            if sx < 0 and y0 < ALC_Y1:
                continue
            pts = catenary((x, y0, z), (x, y1, z), 0.14, 4)
            for p, q in zip(pts, pts[1:]):
                a.box(P["paint"], ((Vector(q) - Vector(p)).length, 0.006, 0.006), tuple((Vector(p) + Vector(q)) / 2), bevel=0,
                      rot=(Vector(q) - Vector(p)).to_track_quat("X", "Z").to_matrix().to_4x4(), tint=lc(E["bone_dim"]), drop=("+x", "-x"))
            for k in (1, 2, 3):
                p = Vector(pts[k])
                a.paper(P["paper"], el.co.star_outline(0.075), T(p.x, p.y, p.z - 0.09) @ R("Z", 90) @ R("Y", 18 * k), lc(E["candy"] if (k + int(y0)) % 2 else E["bone"]), fold=0.2,
                        curl=0.2, cuts=0)
    # the owl lantern on its bracket in front of the round window
    lx, ly, lz = 0.0, 0.95, ROUND_Z + 0.2
    a.box(P["ink"], (0.045, 0.045, 0.4), (lx, Y0 + 0.03, lz + 0.75), bevel=0.01, tint=1.0)
    beam(a, P["ink"], (lx, Y0 + 0.02, lz + 0.9), (lx, ly + 0.06, lz + 0.9), 0.035, 0.035, 0.008, 1.0)
    beam(a, P["ink"], (lx, Y0 + 0.04, lz + 0.6), (lx, ly - 0.25, lz + 0.88), 0.03, 0.03, 0.008, 1.0)
    for i in range(6):
        z = lz + 0.88 - (i + 0.5) * 0.1
        a.box(P["ink"], (0.024 if i % 2 else 0.008, 0.008 if i % 2 else 0.024, 0.105), (lx, ly, z), bevel=0.003, tint=1.0)
    owl_lantern(a, craft, P, T(lx, ly, lz))
    # the drive from the wind wheel: shaft up from the workshop, along a rafter, along the ridge to the mast
    sx, sy = SHAFT_X, MECH_Y
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
    ltop = ceil_z(ROD_X) - 0.05
    n = 6
    for i in range(n):
        z0, z1 = LOFT + (ltop - LOFT) * i / n, LOFT + (ltop - LOFT) * (i + 1) / n
        a.cyl(P["paint"], 0.02, z1 - z0, T(ROD_X, ROD_Y, (z0 + z1) / 2), segments=6, tint=lc(E["orange"] if i % 2 else E["bone"]), caps=False)
    a.lathe(P["brass"], [(0.04, 0.0), (0.05, 0.012), (0.04, 0.03), (0.025, 0.03)], 8, T(ROD_X, ROD_Y, LOFT), tint=1.0)
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
PHOTO = Vector((-0.8, 0.0, 2.26))  # frame centre: between the dado rail (1.5) and the frieze rail (3.0), clear of the panel


def cam_radius(deg):
    r = 0.22
    for c in LOBES:
        d = (deg - c + 180) % 360 - 180
        if abs(d) < 60:
            r = max(r, 0.22 + 0.2 * math.cos(math.radians(1.5 * d)) ** 1.2)
    return r


def build_mechanism(craft, photo=True):
    """`photo=False`: the game model. The troupe photo is its own model there (`build_troupe_photo`), at `PHOTO`."""
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
    # ----- the troupe photo, framed on the foyer side of the partition, above the dado rail -----
    if photo:
        b.part = "KitPhoto"
        troupe_photo(b, P, craft, T(*PHOTO))
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
    }


def interior_pivots():
    return {
        "InnerDoor": DOOR_HINGE,
        "DoorLatch": T(0.12, 0.13, FL + 0.95),
        "SillSnag": T(*SNAG),
    }
