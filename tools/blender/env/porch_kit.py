"""The modular porch kit (spec §6 Environment row): floor, steps, post, rail, wall, window, door, roof and
corner trim. Every module is a function that adds chunky bevelled painted-timber pieces to a Builder in local
space: x across, front face toward -Y, z = 0 at the module's base. `build_kit_strip` lays the nine out for
review; `owl_house.py` assembles Aunt Kit's house from the same functions."""
import math

import bmesh as _bm

from env_lib import E, R, S, T, Builder, arc, arch_outline, grouped, owl_outline, scallop_strip, shade

U = 2.0  # module width
FLOOR = 0.6  # porch deck height: three 0.2 m risers
WALL = 2.2  # storey height
POST = 2.0


def paints(craft):
    """The house's paint scheme, one material per role and grain direction."""
    return {
        "clap": craft.timber(E["indigo"], "X", wear=0.25),
        "wall_v": craft.timber(E["indigo_dark"], "Z", wear=0.2),
        "back": craft.timber(E["indigo_dark"], "X", wear=0.0),
        "trim_x": craft.timber(E["bone_dim"], "X", wear=0.6, bare=E["timber_dark"]),
        "trim_y": craft.timber(E["bone_dim"], "Y", wear=0.6, bare=E["timber_dark"]),
        "trim_z": craft.timber(E["bone_dim"], "Z", wear=0.6, bare=E["timber_dark"]),
        "deck": craft.timber(E["indigo_light"], "Y", wear=0.7, bare=E["bare"]),
        "deck_x": craft.timber(E["indigo_light"], "X", wear=0.7, bare=E["bare"]),
        "door": craft.timber(E["plum"], "Z", wear=0.35, bare=E["timber_dark"]),
        "door_x": craft.timber(E["plum"], "X", wear=0.35, bare=E["timber_dark"]),
        "shutter": craft.timber(E["purple"], "X", wear=0.4),
        "shutter_z": craft.timber(E["purple"], "Z", wear=0.4),
        "slat": craft.timber(E["twilight"], "X", wear=0.3),
        "orange": craft.timber(E["orange"], "X", wear=0.4, bare=E["timber_dark"]),
        "candy": craft.timber(E["candy"], "Z", wear=0.2, bare=E["timber"]),
        "amber": craft.timber(E["timber"], "Z", wear=0.2, bare=E["timber_dark"]),
        "ink": craft.tinted(E["ink"], 0.5, 0.1),
        "brass": craft.tinted(E["brass"], 0.3, 0.15),
        "glass": craft.tinted(H_GLASS, 0.15, 0.1),
        "shingle": craft.tinted(E["purple"], 0.85, 0.25, 5.0),
        "scale": craft.tinted(E["indigo_light"], 0.85, 0.2, 5.0),
        "roofdeck": craft.timber(E["indigo_dark"], "X", wear=0.0),
        "paper_glow": craft.glow(E["glow"], 3.2),
        "paper_dim": craft.glow(E["amber"], 1.3),
        "eye": craft.glow(E["candy"], 0.9),
    }


H_GLASS = (0.07, 0.08, 0.16)


# --- modules -----------------------------------------------------------------------------------------------

def floor(b, P, width=U, depth=U, skirt=("front",)):
    """Deck: boards running front to back on a rim joist, with a slatted skirt. Spans y = 0 (front) to depth."""
    n = round(width / 0.2)
    w = width / n
    for i in range(n):
        x = -width / 2 + (i + 0.5) * w
        b.box(P["deck"], (w - 0.008, depth + 0.06, 0.05), (x, depth / 2 - 0.03, FLOOR - 0.025), drop=("-z", "+y"))
    b.box(P["trim_x"], (width, 0.06, 0.14), (0, 0.03, FLOOR - 0.12), bevel=0.012)
    b.box(P["back"], (width - 0.02, depth - 0.1, 0.1), (0, depth / 2, FLOOR - 0.1), bevel=0)
    sides = {"front": T(0, 0.05, 0), "left": T(-width / 2 + 0.05, depth / 2, 0) @ R("Z", -90) @ T(0, 0, 0),
             "right": T(width / 2 - 0.05, depth / 2, 0) @ R("Z", 90)}
    for side in skirt:
        span = width if side == "front" else depth
        with b.at(sides[side]):
            if side != "front":
                b.box(P["trim_x"], (span, 0.06, 0.14), (0, -0.02, FLOOR - 0.12), bevel=0.012)
            m = round(span / 0.16)
            for i in range(m):
                x = -span / 2 + (i + 0.5) * span / m
                b.box(P["wall_v"], (0.1, 0.025, FLOOR - 0.2), (x, 0.0, (FLOOR - 0.2) / 2 + 0.01), drop=("+y", "-z", "+z"))
            b.box(P["back"], (span, 0.02, 0.07), (0, 0.02, 0.06), bevel=0)
    for sx in (-1, 1):  # corner piers
        b.box(P["trim_z"], (0.14, 0.14, FLOOR - 0.06), (sx * (width / 2 - 0.07), 0.07, (FLOOR - 0.06) / 2), bevel=0.015)


def steps(b, P, width=1.6, tread=0.3):
    """Two treads up to the deck between chunky cheek boards; top riser meets a deck front at y = 0."""
    rises = round(FLOOR / 0.2)
    for i in range(1, rises):
        z = FLOOR - 0.2 * i
        y = -tread * i
        for k in range(2):  # two boards per tread
            b.box(P["deck_x"], (width - 0.02, tread / 2 + 0.014, 0.045), (0, y + tread * (0.25 + 0.5 * k) - 0.012, z - 0.0225),
                  bevel=0.01)
        b.box(P["wall_v"], (width - 0.1, 0.025, 0.2), (0, y + 0.02, z - 0.1), bevel=0.006, tint=0.85)
    b.box(P["wall_v"], (width - 0.1, 0.025, 0.2), (0, -0.005, FLOOR - 0.1), bevel=0.006, tint=0.85)
    run = tread * (rises - 1)
    cheek = [(0.0, 0.0), (0.0, FLOOR + 0.06), (-0.12, FLOOR + 0.06), (-run - 0.1, 0.3), (-run - 0.1, 0.0)]
    for sx in (-1, 1):  # outline is (y, z); stand it up along Y, bevelled face outward
        b.prism(P["trim_y"], cheek, 0.09, (S(-1, 1, 1) if sx < 0 else T()) @ T(width / 2 + 0.09, 0, 0) @ R("Z", 90),
                bevel=0.015)
        b.box(P["trim_z"], (0.13, 0.13, 0.36), (sx * (width / 2 + 0.045), -run - 0.04, 0.18), bevel=0.02)
        b.lathe(P["trim_z"], [(0.0, 0.0), (0.075, 0.0), (0.085, 0.05), (0.05, 0.11), (0.0, 0.13)], 10,
                T(sx * (width / 2 + 0.045), -run - 0.04, 0.36))


def post(b, P, height=POST, brackets=(-1, 1)):
    """Square porch post with plinth, necking and fretwork brackets."""
    b.box(P["trim_z"], (0.2, 0.2, 0.26), (0, 0, 0.13), bevel=0.022)
    b.box(P["trim_z"], (0.13, 0.13, height - 0.26), (0, 0, 0.26 + (height - 0.26) / 2), bevel=0.02)
    b.box(P["trim_z"], (0.18, 0.18, 0.05), (0, 0, 0.6), bevel=0.015)
    b.box(P["trim_z"], (0.2, 0.2, 0.09), (0, 0, height - 0.045), bevel=0.02)
    b.box(P["orange"], (0.15, 0.15, 0.035), (0, 0, height - 0.13), bevel=0.01, tint=1.0)
    r = 0.42
    bracket = [(0.0, 0.0), (r, 0.0), (r, -0.07)] + arc(r, -r, r - 0.07, 90, 180, 6)[1:] + [(0.0, -r)]
    for sx in brackets:
        with b.at(T(sx * 0.065, 0.025, height - 0.09) @ (S(-1, 1, 1) if sx < 0 else S(1))):
            b.prism(P["trim_x"], bracket, 0.05, T(0, -0.05, 0), bevel=0.012)
            b.disc(P["orange"], 0.045, 0.02, T(0.13, -0.066, -0.13), segs=10, tint=1.0, bevel=0.006)


BALUSTER = [(-0.05, 0.0), (0.05, 0.0), (0.05, 0.08), (0.022, 0.16), (0.055, 0.3), (0.055, 0.4), (0.02, 0.53), (0.05, 0.6),
            (-0.05, 0.6), (-0.02, 0.53), (-0.055, 0.4), (-0.055, 0.3), (-0.022, 0.16), (-0.05, 0.08)]


def rail(b, P, length=U, rhythm=0):
    """Flat-sawn baluster rail; `rhythm` 1 swaps every third baluster for a plain slat with a moon hole look."""
    b.box(P["trim_x"], (length, 0.07, 0.06), (0, 0, 0.13), bevel=0.014)
    b.box(P["trim_x"], (length, 0.07, 0.05), (0, 0, 0.765), bevel=0.014)
    b.box(P["trim_x"], (length + 0.02, 0.12, 0.04), (0, 0, 0.81), bevel=0.016, tint=1.05)
    n = max(2, round(length / 0.2))
    for i in range(n):
        x = -length / 2 + (i + 0.5) * length / n
        if rhythm and i % 3 == 1:
            b.box(P["shutter_z"], (0.11, 0.03, 0.6), (x, 0, 0.45), bevel=0.01)
            b.disc(P["candy"], 0.03, 0.006, T(x, -0.021, 0.5), segs=10, tint=1.0)
        else:
            b.prism(P["trim_z"], BALUSTER, 0.03, T(x, -0.015, 0.15))


def wall(b, P, width=U, height=WALL, openings=(), base=True, frieze=True):
    """Clapboard wall on a backing slab; `openings` are (x0, x1, z0, z1) holes for windows and doors."""
    def spans(z0, z1):
        cuts = sorted((o[0], o[1]) for o in openings if o[2] < z1 - 0.02 and o[3] > z0 + 0.02)
        out, x = [], -width / 2
        for c0, c1 in cuts:
            if c0 - x > 0.03:
                out.append((x, c0))
            x = max(x, c1)
        if width / 2 - x > 0.03:
            out.append((x, width / 2))
        return out

    for x0, x1 in spans(0.0, height):
        b.box(P["back"], (x1 - x0, 0.1, height), ((x0 + x1) / 2, 0.07, height / 2), tint=0.8, drop=("+z", "-z"))
    for o in openings:  # backing above and below each hole
        if o[2] > 0.05:
            b.box(P["back"], (o[1] - o[0], 0.1, o[2]), ((o[0] + o[1]) / 2, 0.07, o[2] / 2), bevel=0, tint=0.8)
        if height - o[3] > 0.02:
            b.box(P["back"], (o[1] - o[0], 0.1, height - o[3]), ((o[0] + o[1]) / 2, 0.07, (height + o[3]) / 2), bevel=0,
                  tint=0.8)
    n = round(height / 0.2)
    h = height / n
    tilt = R("X", -6)
    for i in range(n):
        for x0, x1 in spans(i * h, (i + 1) * h):
            pieces = [(x0, x1)]
            if x1 - x0 > 1.5 and b.rng.random() < 0.3:  # a butt joint now and then
                cut = b.rng.uniform(x0 + 0.5, x1 - 0.5)
                pieces = [(x0, cut), (cut, x1)]
            for p0, p1 in pieces:
                b.box(P["clap"], (p1 - p0 - 0.004, 0.035, h * 1.07), ((p0 + p1) / 2, 0.005, (i + 0.5) * h), drop=("+y", "+z"),
                      rot=tilt, tint=b.vary(0.8, 1.12))
    if base:
        for x0, x1 in spans(0.0, 0.16):
            b.box(P["trim_x"], (x1 - x0, 0.05, 0.16), ((x0 + x1) / 2, -0.03, 0.08), bevel=0.012)
    if frieze:
        b.box(P["trim_x"], (width, 0.05, 0.09), (0, -0.03, height - 0.045), bevel=0.012)


def window(b, P, cx=0.0, z0=0.75, w=0.8, h=1.2, shutters=True, lit=None):
    """Sash window unit standing proud of the wall (pair with a wall opening of the same rectangle)."""
    zc = z0 + h / 2
    b.box(lit or P["glass"], (w, 0.02, h), (cx, 0.0, zc), bevel=0, tint=1.0)
    for sx in (-1, 1):
        b.box(P["trim_z"], (0.09, 0.08, h + 0.09), (cx + sx * (w / 2 + 0.045), -0.035, zc + 0.045), bevel=0.014)
    b.box(P["trim_x"], (w + 0.3, 0.1, 0.09), (cx, -0.04, z0 + h + 0.045), bevel=0.014)
    b.box(P["trim_x"], (w + 0.38, 0.13, 0.04), (cx, -0.045, z0 + h + 0.11), bevel=0.012, tint=1.05)
    b.box(P["trim_x"], (w + 0.34, 0.16, 0.06), (cx, -0.06, z0 - 0.03), bevel=0.016)
    b.box(P["trim_x"], (w + 0.14, 0.06, 0.1), (cx, -0.035, z0 - 0.11), bevel=0.012, tint=0.92)
    b.box(P["trim_z"], (0.04, 0.035, h), (cx, -0.012, zc), bevel=0.008)
    b.box(P["trim_x"], (w, 0.045, 0.055), (cx, -0.016, zc), bevel=0.008)
    if shutters:
        sw = 0.3
        for sx in (-1, 1):
            x = cx + sx * (w / 2 + 0.1 + sw / 2)
            for ex in (-1, 1):
                b.box(P["shutter_z"], (0.05, 0.035, h), (x + ex * (sw / 2 - 0.025), -0.04, zc), bevel=0.008)
            for z in (z0 + 0.03, zc, z0 + h - 0.03):
                b.box(P["shutter"], (sw - 0.1, 0.035, 0.06), (x, -0.04, z), drop=("+y", "+x", "-x"))
            for half in (0, 1):
                for k in range(4):
                    z = z0 + 0.06 + half * (h / 2 - 0.03) + (k + 0.5) * (h / 2 - 0.09) / 4
                    b.box(P["shutter"], (sw - 0.1, 0.012, 0.1), (x, -0.04, z), rot=R("X", 28), drop=("+x", "-x", "+y"))
            b.box(P["ink"], (0.03, 0.02, 0.07), (x - sx * (sw / 2 + 0.005), -0.05, z0 + h * 0.25), bevel=0.004, tint=1.0)
            b.box(P["ink"], (0.03, 0.02, 0.07), (x - sx * (sw / 2 + 0.005), -0.05, z0 + h * 0.75), bevel=0.004, tint=1.0)


def owl_motif(b, P, height, M, depth=0.012):
    """The painted owl (Kit's brooch design) as layered cut-outs, origin at the feet, facing -Y."""
    u = height / 81.0
    with b.at(M):
        b.prism(P["candy"], owl_outline(height), depth, T(0, -depth, 0), tint=1.0, bevel=depth * 0.3)
        for s in (1, -1):
            wing = [(s * 17 * u, 54 * u), (s * 28 * u, 40 * u), (s * 26 * u, 16 * u), (s * 15 * u, 8 * u)]
            b.prism(P["orange"], wing if s > 0 else wing[::-1], depth * 0.6, T(0, -depth * 1.6, 0), tint=1.0)
            b.ring(P["trim_x"], 6 * u, 10.5 * u, depth * 0.8, T(s * 11 * u, -depth * 1.8, 47 * u), segs=14, tint=1.0)
            b.disc(P["ink"], 6.2 * u, depth * 0.5, T(s * 11 * u, -depth * 1.4, 47 * u), segs=12, tint=1.0)
            b.disc(P["eye"], 2.6 * u, depth * 0.4, T(s * 11 * u, -depth * 1.9, 47 * u), segs=8, tint=1.0)
        b.prism(P["orange"], [(-4.5 * u, 40 * u), (4.5 * u, 40 * u), (0, 29 * u)], depth, T(0, -depth * 2, 0), tint=1.0)
        chest = [(-9 * u, 26 * u), (9 * u, 26 * u), (12 * u, 14 * u), (0, 5 * u), (-12 * u, 14 * u)]
        b.prism(P["trim_x"], chest, depth * 0.5, T(0, -depth * 1.5, 0), tint=0.95)


def door(b, P, width=1.1, height=2.02):
    """Closed front door as a ticket booth: stile-and-rail leaf, a shuttered ticket window with the painted owl,
    a counter shelf and scalloped pelmet. Pair with a wall opening (-width/2 - 0.02 .. width/2 + 0.02, 0, height)."""
    hw = width / 2
    b.box(P["door"], (width, 0.05, height), (0, 0.01, height / 2), bevel=0.006, tint=0.9)
    for sx in (-1, 1):  # leaf stiles, then the proud frame
        b.box(P["door"], (0.13, 0.03, height), (sx * (hw - 0.065), -0.025, height / 2), bevel=0.01)
        b.box(P["trim_z"], (0.11, 0.1, height + 0.1), (sx * (hw + 0.055), -0.04, height / 2 + 0.05), bevel=0.016)
    for z, hh in ((0.09, 0.18), (0.92, 0.13), (height - 0.07, 0.14)):
        b.box(P["door_x"], (width - 0.26, 0.03, hh), (0, -0.025, z), bevel=0.01)
    b.box(P["door"], (0.1, 0.03, 0.68), (0, -0.025, 0.52), bevel=0.01)
    for sx in (-1, 1):  # raised lower panels
        b.box(P["door"], (0.27, 0.02, 0.56), (sx * 0.21, -0.02, 0.52), bevel=0.016, tint=1.12)
    b.box(P["trim_x"], (width + 0.36, 0.11, 0.09), (0, -0.045, height + 0.065), bevel=0.016)
    b.box(P["trim_x"], (width + 0.44, 0.14, 0.04), (0, -0.05, height + 0.13), bevel=0.012, tint=1.05)
    b.box(P["trim_x"], (width + 0.1, 0.16, 0.04), (0, -0.06, 0.02), bevel=0.012, tint=0.9)
    # ticket window: frame, eight shutter slats, the painted owl, counter, pelmet
    sw, sz0, sz1 = 0.66, 1.06, 1.72
    for sx in (-1, 1):
        b.box(P["trim_z"], (0.05, 0.035, sz1 - sz0 + 0.05), (sx * (sw / 2 + 0.025), -0.04, (sz0 + sz1) / 2), bevel=0.01)
    b.box(P["trim_x"], (sw + 0.1, 0.035, 0.05), (0, -0.04, sz1 + 0.025), bevel=0.01)
    n = 8
    for i in range(n):
        z = sz0 + (i + 0.5) * (sz1 - sz0) / n
        b.box(P["slat"], (sw, 0.024, (sz1 - sz0) / n - 0.006), (0, -0.03, z), bevel=0.007, tint=b.vary(0.9, 1.1))
    owl_motif(b, P, 0.52, T(0, -0.04, sz0 + 0.07))
    b.box(P["trim_x"], (0.84, 0.15, 0.045), (0, -0.1, sz0 - 0.025), bevel=0.014)
    for sx in (-1, 1):
        bracket = [(0.0, 0.0), (0.11, 0.0), (0.0, -0.11)]
        b.prism(P["trim_x"], bracket, 0.035, T(sx * 0.33 - 0.0175, -0.035, sz0 - 0.045) @ R("Z", -90), bevel=0.006)
    pelmet = scallop_strip(0.8, 0.05, 5, 0.06, 4)
    b.prism(P["orange"], pelmet, 0.03, T(-0.4, -0.085, sz1 + 0.14), tint=1.0, bevel=0.006)
    b.box(P["orange"], (0.84, 0.07, 0.03), (0, -0.07, sz1 + 0.15), bevel=0.01, tint=0.92)
    # hardware
    b.lathe(P["brass"], [(0.0, 0.0), (0.022, 0.0), (0.016, 0.03), (0.034, 0.05), (0.03, 0.075), (0.0, 0.085)], 10,
            T(hw - 0.07, -0.04, 0.95) @ R("X", 90), tint=1.0)
    b.box(P["brass"], (0.05, 0.012, 0.12), (hw - 0.07, -0.043, 0.95), bevel=0.005, tint=0.9)
    b.box(P["brass"], (0.2, 0.012, 0.045), (0, -0.043, 0.92), bevel=0.005, tint=1.0)
    b.box(P["ink"], (0.14, 0.008, 0.012), (0, -0.05, 0.92), bevel=0, tint=1.0)
    for z in (0.25, 1.0, 1.78):
        b.box(P["ink"], (0.035, 0.03, 0.11), (-hw - 0.002, -0.05, z), bevel=0.005, tint=1.0)


def _mouth_top(mouth, x):
    """Up-slope edge of a dormer mouth (half width, y at the eave, y at the ridge) at `x`; None beside it."""
    hm, ye, yr = mouth
    return yr - (yr - ye) * abs(x) / hm if abs(x) < hm else None


def _open_mouth(bm, mouth):
    """Cut a shingle tab along the edge of a dormer mouth; returns the pieces that lie outside it."""
    hm, ye, yr = mouth

    def cut(src, co, no):
        part = src.copy()
        _bm.ops.bisect_plane(part, geom=part.verts[:] + part.edges[:] + part.faces[:], dist=1e-5, plane_co=co, plane_no=no,
                             clear_inner=True)
        return part
    out = []
    for sx in (1, -1):
        half = cut(bm, (0, 0, 0), (sx, 0, 0))
        beside = cut(half, (sx * hm, 0, 0), (sx, 0, 0))
        within = cut(half, (sx * hm, 0, 0), (-sx, 0, 0))
        above = cut(within, (0, yr, 0), (sx * (yr - ye), hm, 0))
        out += [beside, above]
        half.free()
        within.free()
    bm.free()
    return [piece for piece in out if piece.faces]


def shingles(b, mat, width, length, M, tab=0.5, exposure=0.42, thick=0.045, keep=None, palette=None, skirt=0.0, mouth=None):
    """Fish-scale shingle courses on a plane: local x across, y up the slope, z out. `keep(cx, y0, y1, hw)`
    drops tabs (hips, dormers, the round window); `palette` is a list of tint triples picked per tab.
    `mouth` (half width, y at the eave, y at the ridge) opens the courses where a room runs into a dormer, without
    disturbing the random tints of the other tabs."""
    rows = int(math.ceil((length + skirt) / exposure))
    with b.at(M):
        for j in range(rows):
            y0 = j * exposure - skirt
            y1 = min(length, y0 + exposure * 1.28)
            count = int(math.ceil(width / tab)) + 1
            for i in range(count):
                cx = -width / 2 + i * tab + (tab / 2 if j % 2 == 0 else 0.0)
                hw = tab / 2 - 0.012
                x0, x1 = max(-width / 2, cx - hw), min(width / 2, cx + hw)
                if x1 - x0 < 0.06 or (keep and not keep(cx, y0, y1, hw)):
                    continue
                crosses = False
                if mouth and y0 < mouth[2] - 0.05:
                    near = 0.0 if x0 < 0.0 < x1 else min(abs(x0), abs(x1))
                    tops = [_mouth_top(mouth, x) for x in (near, x0, x1)]
                    crosses = tops[0] is not None and y0 - 0.03 < tops[0]
                    if all(t is not None and y1 < t for t in tops[1:]):  # wholly inside
                        b.rng.uniform(-0.02, 0.02)  # the draws this tab would have made
                        if palette:
                            b.rng.choice(palette)
                        b.rng.uniform(0.82, 1.12)
                        continue
                jit = b.rng.uniform(-0.02, 0.02)
                pts = [(max(x0, min(x1, cx + hw * math.cos(a))), y0 + jit + hw * 0.62 * (1 + math.sin(a)))
                       for a in (math.radians(180 + 180 * s / ARC) for s in range(ARC + 1))]
                pts += [(x1, y1), (x0, y1)]
                bm = _bm.new()
                span = y1 - y0
                top = [bm.verts.new((x, y, thick * (0.35 + 1.0 * (y1 - y) / span))) for x, y in pts]
                bot = [bm.verts.new((x, y, 0.0)) for x, y in pts]
                bm.faces.new(top)
                for k in range(len(pts)):
                    q = (k + 1) % len(pts)
                    if k >= ARC:
                        continue  # only the rounded butt shows: the sides and top sit against other tabs
                    bm.faces.new((top[q], top[k], bot[k], bot[q]))
                tint = b.rng.choice(palette) if palette else (1, 1, 1)
                v = b.rng.uniform(0.82, 1.12)
                for piece in (_open_mouth(bm, mouth) if crosses else [bm]):
                    b.add_bm(piece, mat, None, tuple(c * v for c in tint))


ARC = 4  # segments in a shingle's rounded butt
ROOF_TINTS = [(1, 1, 1)] * 5 + [(1.25, 0.9, 1.05), (0.75, 0.85, 1.25), (0.8, 0.8, 0.9)]


def roof(b, P, width=U, length=2.4, pitch=35.0, M=None, notch=None, rafters=True, mat=None, hole=None, mouth=None):
    """Roof panel: shingles on a deck board with a fascia and exposed rafter tails. Origin at the eave centre,
    x along the eave, rising toward +Y. `notch` = (x0, x1, up) leaves a gap at the eave for a wall dormer.
    `mouth` = (half width, y at the dormer eave, y at its ridge) opens the roof where a room runs into the
    dormer (needs a symmetric `notch`), so that nothing of the roof crosses that room."""
    M = (M if M is not None else T()) @ R("X", pitch)
    with b.at(M):
        if notch:
            x0, x1, up = notch
            for a0, a1, y0 in ((-width / 2, x0, 0.0), (x1, width / 2, 0.0), (x0, x1, up)):
                if mouth and y0 > 0:
                    hm, ye, yr = mouth
                    side = [(hm, y0), (a1, y0), (a1, length), (0.0, length), (0.0, yr), (hm, ye)]
                    for sx in (1, -1):
                        b.prism(P["roofdeck"], side, 0.07, (S(-1, 1, 1) if sx < 0 else T()) @ R("X", -90), tint=0.8)
                        b.box(P["trim_x"], (a1 - hm, 0.05, 0.16), (sx * (a1 + hm) / 2, y0 - 0.02, -0.06), bevel=0.014,
                              tint=None if sx > 0 else 1.0)
                    continue
                b.box(P["roofdeck"], (a1 - a0, length - y0, 0.07), ((a0 + a1) / 2, (length + y0) / 2, -0.035), bevel=0, tint=0.8)
                b.box(P["trim_x"], (a1 - a0, 0.05, 0.16), ((a0 + a1) / 2, y0 - 0.02, -0.06), bevel=0.014)
        else:
            b.box(P["roofdeck"], (width, length, 0.07), (0, length / 2, -0.035), bevel=0, tint=0.8)
            b.box(P["trim_x"], (width + 0.02, 0.05, 0.16), (0, -0.02, -0.06), bevel=0.014)
        if rafters:
            n = max(2, round(width / 0.7))
            for i in range(n):
                x = -width / 2 + (i + 0.5) * width / n
                if notch and notch[0] - 0.1 < x < notch[1] + 0.1:
                    continue
                b.box(P["trim_y"], (0.08, 0.55, 0.12), (x, 0.3, -0.125), bevel=0.016)

        def keep(cx, y0, y1, hw):
            if hole and hole[0] < cx < hole[1] and hole[2] < y0 and y1 < hole[3]:
                return False  # buried under a dormer
            return not (notch and notch[0] - 0.1 < cx < notch[1] + 0.1 and y0 < notch[2] - 0.05)
    shingles(b, mat or P["shingle"], width, length, M, keep=keep, palette=ROOF_TINTS, skirt=0.06, mouth=mouth)


def corner_trim(b, P, height=WALL):
    """Corner board pair with a little cap and plinth; origin on the outside corner, arms toward +X and +Y."""
    b.box(P["trim_z"], (0.16, 0.05, height), (0.055, -0.03, height / 2), bevel=0.014)
    b.box(P["trim_z"], (0.05, 0.16, height), (-0.03, 0.055, height / 2), bevel=0.014)
    b.box(P["trim_z"], (0.2, 0.2, 0.2), (0.045, 0.045, 0.1), bevel=0.02, tint=0.95)
    b.box(P["trim_z"], (0.2, 0.2, 0.08), (0.045, 0.045, height - 0.04), bevel=0.016)
    b.box(P["orange"], (0.17, 0.17, 0.03), (0.045, 0.045, height - 0.11), bevel=0.008, tint=1.0)


def round_window(b, P, radius, M, lit, depth=0.14):
    """Round attic window: fat bevelled ring, keystones at the quarters, cross muntins over lit paper."""
    with b.at(M):
        b.disc(lit, radius + 0.02, 0.02, T(0, -0.03, 0), segs=28, tint=1.0)
        b.ring(P["trim_x"], radius, radius + 0.17, depth, T(0, -depth, 0), segs=28, tint=1.0)
        b.ring(P["orange"], radius + 0.17, radius + 0.205, depth * 0.55, T(0, -depth * 0.55, 0), segs=28, tint=0.95)
        b.box(P["trim_z"], (0.05, 0.04, radius * 2), (0, -0.06, 0), bevel=0.01)
        b.box(P["trim_x"], (radius * 2, 0.04, 0.05), (0, -0.06, 0), bevel=0.01)
        for k in range(4):
            a = math.radians(90 * k)
            b.box(P["trim_x"], (0.13, depth + 0.03, 0.2), (math.sin(a) * (radius + 0.09), -depth / 2 - 0.015,
                                                          math.cos(a) * (radius + 0.09)), bevel=0.02,
                  rot=R("Y", 90 * k), tint=1.06)


def arch_window(b, P, w, h, M, lit, sill=0.3):
    """Round-topped attic window with a deep sill (June's way in) and one casement swung open."""
    with b.at(M):
        b.prism(lit, arch_outline(w, h, 10), 0.02, T(0, -0.02, 0), tint=1.0)
        for sx in (-1, 1):
            b.box(P["trim_z"], (0.1, 0.1, h), (sx * (w / 2 + 0.05), -0.04, h / 2), bevel=0.016)
        b.ring(P["trim_x"], w / 2, w / 2 + 0.1, 0.1, T(0, -0.09, h), a0=0, a1=180, segs=12, tint=1.0)
        b.box(P["trim_x"], (0.14, 0.13, 0.16), (0, -0.05, h + w / 2 + 0.05), bevel=0.02, tint=1.06)
        b.box(P["trim_x"], (w + 0.5, sill + 0.1, 0.08), (0, -sill / 2, -0.04), bevel=0.02)
        for sx in (-1, 1):
            b.prism(P["trim_x"], [(0.0, 0.0), (sill - 0.04, 0.0), (0.0, -0.26)], 0.06,
                    T(sx * (w / 2 + 0.1) - 0.03, -0.01, -0.07) @ R("Z", -90), bevel=0.01)
        b.box(P["trim_z"], (0.035, 0.03, h + w / 2 - 0.02), (0, -0.035, (h + w / 2) / 2), bevel=0.008)
        b.box(P["trim_x"], (w, 0.03, 0.035), (0, -0.035, h * 0.55), bevel=0.008)
        with b.at(T(-w / 2 - 0.02, -0.09, 0.0) @ R("Z", -108)):  # the open casement leaf
            for z in (0.03, h * 0.5, h - 0.03):
                b.box(P["shutter"], (w * 0.5, 0.03, 0.06), (w * 0.25, 0, z), bevel=0.008)
            for x in (0.03, w * 0.5 - 0.03):
                b.box(P["shutter_z"], (0.06, 0.03, h), (x, 0, h / 2), bevel=0.008)


# --- review strip ------------------------------------------------------------------------------------------

def build_kit_strip(craft, gap=2.5):
    """The nine modules in a row, each its own mesh. Returns [(name, root, x)]."""
    P = paints(craft)
    opening_w = (-0.4, 0.4, 0.75, 1.95)
    opening_d = (-0.57, 0.57, 0.0, 2.02)

    def win(b):
        wall(b, P, openings=(opening_w,))
        window(b, P)

    def dr(b):
        wall(b, P, openings=(opening_d,))
        door(b, P)

    def rail_demo(b):
        rail(b, P, 1.8)
        for sx in (-1, 1):
            with b.at(T(sx * 0.96, 0, 0)):
                post(b, P, 1.0, ())

    def roof_demo(b):  # the panel on a little lean-to frame so it stands for review
        roof(b, P, M=T(0, 0, 0.0))
        run, rise = 2.4 * math.cos(math.radians(35)), 2.4 * math.sin(math.radians(35))
        for sx in (-1, 1):
            b.prism(P["wall_v"], [(0.1, 0.0), (run, 0.0), (run, rise - 0.06)], 0.08, T(sx * 0.85 + 0.04, 0, 0) @ R("Z", 90),
                    bevel=0.012)
        b.box(P["trim_x"], (1.9, 0.08, 0.1), (0, run - 0.04, 0.05), bevel=0.014)

    mods = [("Floor", lambda b: floor(b, P, skirt=("front", "left", "right"))),
            ("Steps", lambda b: steps(b, P), ), ("Post", lambda b: post(b, P)),
            ("Rail", rail_demo),
            ("Wall", lambda b: wall(b, P)), ("Window", win), ("Door", dr),
            ("Roof", roof_demo), ("Corner", lambda b: corner_trim(b, P))]
    out = []
    for i, (name, fn) in enumerate(mods):
        def build(fn=fn, name=name, i=i):
            b = Builder(seed=10 + i)
            fn(b)
            b.finish("Kit_" + name)
        root, _ = grouped("Kit_" + name, build)
        x = (i - (len(mods) - 1) / 2) * gap
        root.location = (x, 0.3 if name == "Steps" else 0.0, 0)
        root.rotation_euler.z = math.radians({"Corner": 35, "Roof": -28, "Post": -20}.get(name, 0))
        out.append((name, root, x))
    return out
