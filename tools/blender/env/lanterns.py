"""The paper lantern table of the living street (`docs/living-street.md` §4.2, D34, D35): Pip picks a shape and a
colour from pictures, folds a paper lantern at a table on the Green, and it hangs lit on her porch.

PaperLanternBat / Cat / Ghost / Leaf
    A figure lantern folded from tissue paper on a thin frame: two domed paper faces joined by a pleated paper band,
    an ink card rim along both edges (the art bible's outline), the face cut from ink card on both sides, a wire bail
    to a ring at the top and a little tassel (the leaf hangs a paper stem instead). The bat spreads two
    accordion-folded wings on ink ribs. The paper is one child, `PaperLantern_Glow`, baked as NEUTRAL warm-white
    paper with a soft inner glow (bright low in the middle, where the candle is, darker toward the rim, each pleat a
    little darker than the next): the game tints that one renderer to the chosen colour (`COLOURS`, the same table
    as `Core/World/LanternArt.cs`). Everything else (ink, wire, tassel) is the root mesh and is never tinted.
    Origin: the top of the hang ring, so a lantern placed at a hook's `LanternHang` hangs from it.
LanternTable
    A folding craft table (painted top on two pairs of crossed legs with hinge bolts), sized for children: sheets of
    paper in the four colours, a paste pot with its brush, a pleated strip half folded, two cut-out shapes, and
    behind it two poles with a string of the four finished lanterns lit, one in each colour (`PaperLanternSamples_Glow`,
    colours baked in). Origin: the floor under the table's centre; front (the side Pip folds at) toward -Y.
LanternHook
    Pip's porch hook: a painted post clamped to the outside of the porch rail's corner, a timber bracket and a curled
    ink iron arm ending in a hook eye, `LanternHang`, whose pivot is where the lantern's ring hangs. A round
    doorbell plate with its push button (`BellButton`, pressed along the model's +Y) at a child's hand height on the
    post's front. Origin: the deck under the post; the arm reaches along +X, the front is -Y (place it with yaw 180
    on Pip's porch, like the kit pieces, so the bell faces the plaza and the lantern hangs out over the lawn).

Never a moon, star or pumpkin (D35: the Players' moon, June's star witch, Bea's pumpkin), no silver or lavender foil
(§3.4 rule 6): the metal is ink-painted iron, the colours are the art bible's families.

Builders are plain `env_lib.Builder` code, exported by `env_export_lanterns.py` (with `GameBuilder`) and reviewed
from the exported FBX by `lanterns_review.py`."""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

import env_lib as el
import porch_kit as pk
from env_lib import E, H, R, S, T, Builder, lc, lin

SHAPES = ("bat", "cat", "ghost", "leaf")
MODELS = {s: "PaperLantern" + s.capitalize() for s in SHAPES}
GLOW_TAG = "PaperLantern"  # the lantern paper: `PaperLantern_Glow`
SAMPLES_TAG = "PaperLanternSamples"  # the table's finished lanterns: `PaperLanternSamples_Glow`
TABLE, HOOK = "LanternTable", "LanternHook"
HANG, BELL = "LanternHang", "BellButton"

# The picker's colours: (paper, light through it). Same values as `LanternArt.cs`; the paper is what the game
# multiplies the neutral paper's base colour by, the light what it multiplies the emission by (a candle warms the
# colour, so the light is a little lighter and warmer than the paper).
COLOURS = {
    "orange": (H(0xEE7F2D), H(0xDC7E34)),
    "plum": (H(0x7A3E6E), H(0x92457C)),
    "green": (H(0x5E9A3A), H(0x8FB145)),
    "yellow": (H(0xF6D365), H(0xDEBB59)),
}
PAPER = H(0xFFF7E6)  # the neutral tissue the game tints
INK = E["ink"]
BONE = E["bone"]

DEPTH = 0.10  # paper face to paper face
BULGE = 0.016  # how far each face domes out at its centre
PUFF = 0.0035  # how far the pleated band bows out between the rims
PLEAT = 0.0028  # the band's fold depth
RIM_IN, RIM_OUT = 0.0055, 0.0055  # ink rim: over the paper / past the edge
BAIL = 0.075  # hang ring top -> top of the body

# --- outlines ----------------------------------------------------------------------------------------------


def _mirror(right):
    """Closed outline (counter-clockwise seen from the front, -Y) from its right half, bottom centre to top centre.
    Points are (x, z, sharp)."""
    left = [(-x, z, s) for x, z, s in reversed(right[1:-1])]
    return right + left


def _smooth(ctrl, rounds=3):
    """Subdivision that rounds the outline but keeps the sharp points (ear tips, lobe tips, hem points)."""
    pts = list(ctrl)
    for _ in range(rounds):
        out = []
        n = len(pts)
        for i in range(n):
            prev, p, q = pts[i - 1], pts[i], pts[(i + 1) % n]
            if p[2]:
                out.append(p)
            else:
                out.append((0.5 * p[0] + 0.25 * (prev[0] + q[0]), 0.5 * p[1] + 0.25 * (prev[1] + q[1]), False))
            out.append((0.5 * (p[0] + q[0]), 0.5 * (p[1] + q[1]), False))
        pts = out
    return pts


def _resample(pts, n):
    """`n` points along the closed outline, evenly by length, always keeping the sharp points."""
    m = len(pts)
    seg = [math.dist(pts[i][:2], pts[(i + 1) % m][:2]) for i in range(m)]
    total = sum(seg)
    sharp = [i for i in range(m) if pts[i][2]] or [0]
    out = []
    for k, i0 in enumerate(sharp):
        i1 = sharp[(k + 1) % len(sharp)]
        idx = list(range(i0, i1 if i1 > i0 else i1 + m))
        length = sum(seg[i % m] for i in idx)
        count = max(1, round(n * length / total))
        walk = [pts[i0 % m][:2]]
        acc = [0.0]
        for i in idx:
            walk.append(pts[(i + 1) % m][:2])
            acc.append(acc[-1] + seg[i % m])
        for s in range(count):
            d = length * s / count
            j = max(0, min(len(acc) - 2, next((t for t in range(len(acc) - 1) if acc[t + 1] >= d), len(acc) - 2)))
            span = acc[j + 1] - acc[j] or 1.0
            u = (d - acc[j]) / span
            a, b = walk[j], walk[j + 1]
            out.append((a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u))
    return out


def _ghost():
    r, cz = 0.132, 0.045
    right = [(0.0, -0.118, True), (0.024, -0.112, False), (0.046, -0.152, True), (0.07, -0.118, False), (0.094, -0.114, False),
             (0.118, -0.156, True), (0.136, -0.1, False), (0.138, -0.05, False), (0.162, -0.03, False), (0.168, -0.004, False),
             (0.142, 0.012, False)]
    right += [(cz * 0 + r * math.cos(math.radians(a)), cz + r * math.sin(math.radians(a)), False) for a in (10, 40, 66)]
    right += [(0.0, cz + r, False)]
    return _mirror(right)


def _cat():
    right = [(0.0, -0.118, False), (0.065, -0.108, False), (0.114, -0.074, False), (0.142, -0.022, False), (0.142, 0.03, False),
             (0.128, 0.066, False), (0.122, 0.19, True), (0.08, 0.124, False), (0.046, 0.106, True), (0.0, 0.112, False)]
    return _mirror(right)


def _bat_body():
    right = [(0.0, -0.11, False), (0.05, -0.098, False), (0.075, -0.05, False), (0.08, 0.012, False), (0.068, 0.06, False),
             (0.068, 0.142, True), (0.03, 0.088, True), (0.0, 0.086, False)]
    return _mirror(right)


LEAF_K = 0.85
LEAF_TIPS = ((0.0, 0.255), (0.19, 0.07), (-0.19, 0.07), (0.105, -0.105), (-0.105, -0.105))  # before LEAF_K


def _leaf():
    """A maple leaf: a big top lobe, two side lobes reaching up and out, two small ones by the stem, rounded sinuses
    and a few teeth."""
    right = [(0.0, -0.075, True), (0.03, -0.08, False), (0.105, -0.105, True), (0.09, -0.06, False), (0.07, -0.03, False),
             (0.12, -0.02, False), (0.15, -0.03, True), (0.16, 0.01, False), (0.19, 0.07, True), (0.15, 0.075, False),
             (0.16, 0.105, True), (0.11, 0.09, False), (0.07, 0.07, False), (0.085, 0.13, False), (0.12, 0.165, True),
             (0.07, 0.16, False), (0.055, 0.205, False), (0.045, 0.215, True), (0.02, 0.215, False), (0.0, 0.255, True)]
    return [(x * LEAF_K, z * LEAF_K, s) for x, z, s in _mirror(right)]


OUTLINES = {"ghost": _ghost, "cat": _cat, "bat": _bat_body, "leaf": _leaf}
# where the dome (and the candle's hot spot) is centred, per shape: inside every outline, star-shaped from it
CENTRES = {"ghost": (0.0, 0.0), "cat": (0.0, 0.0), "bat": (0.0, -0.01), "leaf": (0.0, 0.035)}
POINTS = {"ghost": 64, "cat": 64, "bat": 44, "leaf": 84}


def outline(shape, n=None):
    return _resample(_smooth(OUTLINES[shape]()), n or POINTS[shape])


# --- geometry helpers --------------------------------------------------------------------------------------

def _normals(pts):
    """Outward 2D normals (the outline runs counter-clockwise seen from -Y with x right and z up)."""
    out = []
    m = len(pts)
    for i in range(m):
        a, b = pts[i - 1], pts[(i + 1) % m]
        dx, dz = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(dx, dz) or 1.0
        out.append((dz / ln, -dx / ln))
    return out


def _area(pts):
    return 0.5 * sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))


def _ray_scale(pts, c, q):
    """Fraction s of the way from the centre `c` to the outline along the ray through `q` (0 centre, 1 edge)."""
    dx, dz = q[0] - c[0], q[1] - c[1]
    d = math.hypot(dx, dz)
    if d < 1e-9:
        return 0.0
    ux, uz = dx / d, dz / d
    best = None
    m = len(pts)
    for i in range(m):
        a, b = pts[i], pts[(i + 1) % m]
        ex, ez = b[0] - a[0], b[1] - a[1]
        den = ux * ez - uz * ex
        if abs(den) < 1e-12:
            continue
        t = ((a[0] - c[0]) * ez - (a[1] - c[1]) * ex) / den
        u = ((a[0] - c[0]) * uz - (a[1] - c[1]) * ux) / den
        if t > 0 and -1e-9 <= u <= 1 + 1e-9:
            best = t if best is None else min(best, t)
    return d / best if best else 1.0


def _dome(s, bulge=BULGE):
    return bulge * (1.0 - min(1.0, s) ** 2)


def _tint_map(bm, values):
    """A per-face tint callable for `Builder.add_bm`: `values` maps a face index to a grey level."""
    table = {}
    bm.faces.index_update()
    for f in bm.faces:
        table[tuple(round(v, 5) for v in f.calc_center_median())] = values.get(f.index, 1.0)

    def tint(centre):
        g = table.get(tuple(round(v, 5) for v in centre), 1.0)
        return (g, g, g)
    return tint


def _as_card(b, fn):
    """Thin single paper (wings, tassel strips): the game export gives it real back faces."""
    if hasattr(b, "card"):
        prev = (b.card, b.sheet)
        b.card, b.sheet = True, True
        try:
            fn()
        finally:
            b.card, b.sheet = prev
    else:
        fn()


# --- materials ---------------------------------------------------------------------------------------------

def paper_glow(craft, centre, radius, rgb=PAPER):
    """Lit tissue: emission only (its value is the same from every angle), bright low in the middle where the candle
    stands and darker toward the rim, a faint fibre, times the piece tint (each pleat a little darker than the next).
    `centre` and `radius` are in the model's builder space (the bake works there)."""
    key = ("lantern_paper", tuple(round(c, 4) for c in centre), round(radius, 4), tuple(round(c, 4) for c in rgb))

    def build():
        mat, nodes, links, bsdf = craft._base(f"LanternPaper_{len(craft._cache)}")
        coord = nodes.new("ShaderNodeTexCoord")
        sub = nodes.new("ShaderNodeVectorMath")
        sub.operation = "SUBTRACT"
        sub.inputs[1].default_value = centre
        links.new(coord.outputs["Object"], sub.inputs[0])
        dist = nodes.new("ShaderNodeVectorMath")
        dist.operation = "LENGTH"
        links.new(sub.outputs[0], dist.inputs[0])
        fall = nodes.new("ShaderNodeMapRange")
        fall.interpolation_type = "SMOOTHSTEP"
        fall.inputs["From Min"].default_value = 0.0
        fall.inputs["From Max"].default_value = radius
        fall.inputs["To Min"].default_value = 1.0
        fall.inputs["To Max"].default_value = 0.6
        links.new(dist.outputs["Value"], fall.inputs["Value"])
        fibre = craft._noise(nodes, links, craft._coords(nodes, links), 70.0, 4.0, 0.7)
        grain = nodes.new("ShaderNodeMapRange")
        grain.inputs["To Min"].default_value = 0.93
        grain.inputs["To Max"].default_value = 1.04
        links.new(fibre, grain.inputs["Value"])
        k = nodes.new("ShaderNodeMath")
        k.operation = "MULTIPLY"
        links.new(fall.outputs["Result"], k.inputs[0])
        links.new(grain.outputs["Result"], k.inputs[1])
        col = nodes.new("ShaderNodeMix")
        col.data_type = "RGBA"
        col.blend_type = "MULTIPLY"
        col.inputs[0].default_value = 1.0
        col.inputs[6].default_value = lin(rgb)
        links.new(k.outputs[0], col.inputs[7])
        links.new(craft._tint(nodes, links, col.outputs[2]), bsdf.inputs["Emission Color"])
        bsdf.inputs["Base Color"].default_value = (0.0, 0.0, 0.0, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.0
        bsdf.inputs["Roughness"].default_value = 0.9
        mat["glow"] = 1.0
        mat.diffuse_color = lin(rgb)
        return mat
    return craft._cached(key, build)


# --- the figure lantern ------------------------------------------------------------------------------------

def _panel(b, mat, pts, c, side, y0, tints, rings=(1.0, 0.66, 0.3)):
    """One domed paper face: the outline, inner rings and the centre."""
    bm = bmesh.new()
    rows = []
    for s in rings:
        rows.append([bm.verts.new((c[0] + (p[0] - c[0]) * s, side * (y0 + _dome(s)), c[1] + (p[1] - c[1]) * s)) for p in pts])
    mid = bm.verts.new((c[0], side * (y0 + _dome(0.0)), c[1]))
    m = len(pts)
    for lo, hi in zip(rows, rows[1:]):
        for i in range(m):
            j = (i + 1) % m
            bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
    for i in range(m):
        bm.faces.new((rows[-1][i], rows[-1][(i + 1) % m], mid))
    bm.normal_update()
    bmesh.ops.reverse_faces(bm, faces=[f for f in bm.faces if f.normal.y * side < 0])  # each face looks out
    b.add_bm(bm, mat, tint=tints)


def _band(b, mat, pts, normals, half):
    """The pleated paper band between the two faces: an accordion fold every two outline points."""
    bm = bmesh.new()
    m = len(pts)
    front, mid, back = [], [], []
    for i, (p, n) in enumerate(zip(pts, normals)):
        k = PUFF + (PLEAT if i % 2 == 0 else -PLEAT * 0.4)
        front.append(bm.verts.new((p[0], -half, p[1])))
        mid.append(bm.verts.new((p[0] + n[0] * k, 0.0, p[1] + n[1] * k)))
        back.append(bm.verts.new((p[0], half, p[1])))
    shade = {}
    for i in range(m):
        j = (i + 1) % m
        for row in ((front, mid), (mid, back)):
            f = bm.faces.new((row[0][i], row[1][i], row[1][j], row[0][j]))
            shade[f] = 0.94 if i % 2 == 0 else 0.78
    bm.normal_update()
    out = sum(Vector((f.normal.x, 0.0, f.normal.z)).dot(Vector((n[0], 0.0, n[1]))) for f, n in zip(bm.faces, [normals[i // 2] for i in range(2 * m)]))
    if out < 0:  # the band looks outward
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    bm.faces.index_update()
    values = {f.index: g for f, g in shade.items()}
    b.add_bm(bm, mat, tint=_tint_map(bm, values))


def _rim(b, mat, pts, normals, c, side, half, tint):
    """The ink card rim along one face's edge: over the paper by RIM_IN, past the edge by RIM_OUT."""
    bm = bmesh.new()
    inner, outer = [], []
    for p, n in zip(pts, normals):
        r = math.hypot(p[0] - c[0], p[1] - c[1]) or 1.0
        s_in = max(0.0, 1.0 - RIM_IN / r)
        q = (c[0] + (p[0] - c[0]) * s_in, c[1] + (p[1] - c[1]) * s_in)
        inner.append(bm.verts.new((q[0], side * (half + _dome(s_in) + 0.0012), q[1])))
        outer.append(bm.verts.new((p[0] + n[0] * RIM_OUT, side * (half + 0.0012), p[1] + n[1] * RIM_OUT)))
    m = len(pts)
    for i in range(m):
        j = (i + 1) % m
        f = bm.faces.new((outer[i], outer[j], inner[j], inner[i]))
    bm.normal_update()
    bmesh.ops.reverse_faces(bm, faces=[f for f in bm.faces if f.normal.y * side < 0])
    b.add_bm(bm, mat, tint=tint)


class Face:
    """Places flat ink cut-outs on a lantern's domed paper, on the front (side -1) and the back (side +1)."""

    def __init__(self, b, craft, pts, c, half):
        self.b, self.pts, self.c, self.half = b, pts, c, half
        self.ink = craft.paint()
        self.coarse = False

    def y(self, q, side):
        return side * (self.half + _dome(_ray_scale(self.pts, self.c, q)) + 0.0016)

    def cut(self, shape, rgb, sides=(-1, 1), lift=0.0):
        """`shape`: a closed (x, z) outline, in place on the face. Glints (`lift`) only at full detail."""
        if lift and self.coarse:
            return
        cx = sum(p[0] for p in shape) / len(shape)
        cz = sum(p[1] for p in shape) / len(shape)
        for side in sides:  # a prism runs from y = 0 into +y: the back one is mirrored so it looks toward +Y
            y = self.y((cx, cz), side) + side * lift
            self.b.prism(self.ink, shape, 0.002, T(0, y, 0) @ S(1, -side, 1), tint=lc(rgb))

    def strip(self, line, width, rgb, sides=(-1, 1)):
        """An ink ribbon along a polyline (veins, whiskers on the paper, the cat's mouth), following the dome."""
        dense = [line[0]]
        for a, c2 in zip(line, line[1:]):  # every ~1.5 cm, so the ribbon follows the dome between the points
            k = max(1, int(math.dist(a, c2) / 0.015))
            dense += [(a[0] + (c2[0] - a[0]) * t / k, a[1] + (c2[1] - a[1]) * t / k) for t in range(1, k + 1)]
        line = dense
        for side in sides:
            bm = bmesh.new()
            left, right = [], []
            for i, p in enumerate(line):
                a, c2 = line[max(0, i - 1)], line[min(len(line) - 1, i + 1)]
                dx, dz = c2[0] - a[0], c2[1] - a[1]
                ln = math.hypot(dx, dz) or 1.0
                nx, nz = -dz / ln * width / 2, dx / ln * width / 2
                y = self.y(p, side)
                left.append(bm.verts.new((p[0] + nx, y, p[1] + nz)))
                right.append(bm.verts.new((p[0] - nx, y, p[1] - nz)))
            for i in range(len(line) - 1):
                bm.faces.new((left[i], left[i + 1], right[i + 1], right[i]))
            bm.normal_update()
            bmesh.ops.reverse_faces(bm, faces=[f for f in bm.faces if f.normal.y * side < 0])
            self.b.add_bm(bm, self.ink, tint=lc(rgb))


def _oval(cx, cz, rx, rz, n=10, tilt=0.0):
    t = math.radians(tilt)
    out = []
    for i in range(n):
        a = 2 * math.pi * i / n
        x, z = rx * math.cos(a), rz * math.sin(a)
        out.append((cx + x * math.cos(t) - z * math.sin(t), cz + x * math.sin(t) + z * math.cos(t)))
    return out


def _almond(cx, cz, w, h, tilt=0.0, n=10):
    t = math.radians(tilt)
    out = []
    for i in range(n):
        a = 2 * math.pi * i / n
        x = w / 2 * math.cos(a)
        z = h / 2 * math.sin(a) * (abs(math.cos(a)) ** 0.0) * (1.0 - 0.35 * abs(math.cos(a)))
        out.append((cx + x * math.cos(t) - z * math.sin(t), cz + x * math.sin(t) + z * math.cos(t)))
    return out


def _features(face, shape, detail):
    n = 10 if detail >= 1 else 8
    if shape == "ghost":
        for s in (-1, 1):
            face.cut(_oval(s * 0.044, 0.062, 0.017, 0.026, n), INK)
            face.cut(_oval(s * 0.044 + 0.006, 0.072, 0.0055, 0.0055, 6), BONE, lift=0.0012)
        face.cut(_oval(0.0, 0.006, 0.016, 0.021, n), INK)
    elif shape == "cat":
        for s in (-1, 1):
            face.cut(_almond(s * 0.05, 0.02, 0.046, 0.032, tilt=s * 12, n=n), INK)
            face.cut(_oval(s * 0.05 + 0.007, 0.027, 0.005, 0.005, 6), BONE, lift=0.0012)
            face.cut([(s * 0.06, 0.108), (s * 0.103, 0.16), (s * 0.104, 0.084)][::s], INK)  # inner ear
            for k in (-1, 1):  # whiskers drawn on the paper (the wire ones reach past the edge)
                face.strip([(s * 0.062, -0.036 + 0.008 * k), (s * 0.1, -0.034 + 0.018 * k)], 0.0035, INK)
        face.cut([(-0.012, -0.022), (0.012, -0.022), (0.0, -0.036)], INK)  # nose
        face.strip([(-0.026, -0.05), (-0.013, -0.058), (0.0, -0.048), (0.013, -0.058), (0.026, -0.05)], 0.0045, INK)
    elif shape == "bat":
        for s in (-1, 1):
            face.cut(_almond(s * 0.032, 0.03, 0.038, 0.029, tilt=-s * 14, n=n), INK)
            face.cut(_oval(s * 0.032 + 0.005, 0.036, 0.0048, 0.0048, 6), BONE, lift=0.0012)
            face.cut([(s * 0.036, 0.074), (s * 0.058, 0.124), (s * 0.063, 0.068)][::s], INK)  # inner ear
        face.strip([(-0.022, -0.02), (-0.01, -0.03), (0.0, -0.027), (0.01, -0.03), (0.022, -0.02)], 0.0045, INK)
        face.cut([(0.007, -0.028), (0.017, -0.027), (0.012, -0.042)], BONE, lift=0.0012)  # one little fang (a friendly bat)
    elif shape == "leaf":
        base = (0.0, -0.04)
        for tip in LEAF_TIPS:
            end = (base[0] + (tip[0] * LEAF_K - base[0]) * 0.8, base[1] + (tip[1] * LEAF_K - base[1]) * 0.8)
            face.strip([base, end], 0.0062 if tip[1] > 0 else 0.0048, INK)


def _bail(b, craft, top_pt, depth, origin_z):
    """Wire bail from the body's top to the ring whose top is the lantern's origin (`origin_z` in the body's frame)."""
    wire = craft.paint()
    ring_r = 0.011
    for side in (-1, 1):
        b.rod(wire, (top_pt[0], side * depth * 0.32, top_pt[1] - 0.004), (0.0, 0.0, origin_z - 2 * ring_r + 0.002), 0.0022, 5, lc(INK))
    b.ring(wire, ring_r - 0.0028, ring_r, 0.0045, T(0, -0.00225, origin_z - ring_r), segs=12, tint=lc(INK), chamfer=0.0)


def _tassel(b, craft, at, depth, scale=1.0, detail=1.0):
    paint = craft.paint()
    x, z = at
    b.lathe(paint, [(0.0, 0.004), (0.013 * scale, 0.004), (0.016 * scale, -0.006), (0.009 * scale, -0.02 * scale), (0.0, -0.022 * scale)], 8,
            T(x, 0, z), tint=lc(INK))
    paper = craft.paper_white()

    def strips():
        for k in range(5 if detail >= 1 else 3):
            a = math.radians(-36 + 18 * k)
            L = (0.05 + 0.008 * (k % 2)) * scale
            w = 0.006 * scale
            p0 = (x + math.sin(a) * 0.006, z - 0.018 * scale)
            p1 = (x + math.sin(a) * 0.016 * scale, z - 0.018 * scale - L)
            b.poly(paper, [(p0[0] - w / 2, (k - 2) * 0.0015, p0[1]), (p0[0] + w / 2, (k - 2) * 0.0015, p0[1]),
                           (p1[0] + w / 2, (k - 2) * 0.0015, p1[1]), (p1[0] - w / 2, (k - 2) * 0.0015, p1[1])],
                   tint=lc(E["candy"] if k % 2 else BONE))
    strips()


def _bat_wing(b, craft, glow, side, detail):
    """An accordion-folded tissue wing on four ink ribs, from the shoulder inside the body: the ribs run to the finger
    tips (ridges toward the viewer) and the trailing edge bites in between them (valleys)."""
    sh = (0.055, 0.03)
    tips = [(0.27, 0.105), (0.285, 0.0), (0.21, -0.078), (0.118, -0.1)]
    root = (0.062, -0.075)
    top_edge = [(sh[0] + (tips[0][0] - sh[0]) * t, sh[1] + (tips[0][1] - sh[1]) * t + 0.03 * math.sin(math.pi * t)) for t in (0.33, 0.66)]
    ring = [(p, 0.0) for p in top_edge] + [(tips[0], -0.012)]
    steps = ((0.33, 0.7), (0.5, 1.0), (0.67, 0.7)) if detail >= 1 else ((0.5, 1.0),)
    for a, c2 in zip(tips, tips[1:] + [root]):
        mx, mz = (a[0] + c2[0]) / 2, (a[1] + c2[1]) / 2
        vx, vz = sh[0] - mx, sh[1] - mz
        ln = math.hypot(vx, vz)
        bite = 0.02 if c2 is not root else 0.012
        for t, k in steps:
            px, pz = a[0] + (c2[0] - a[0]) * t, a[1] + (c2[1] - a[1]) * t
            ring.append(((px + vx / ln * bite * k, pz + vz / ln * bite * k), 0.01 * k))
        ring.append((c2, -0.012 if c2 is not root else 0.0))
    bm = bmesh.new()
    s_v = bm.verts.new((side * sh[0], 0.0, sh[1]))
    vs = [bm.verts.new((side * p[0], fold, p[1])) for p, fold in ring]
    shade = {}
    for i in range(len(vs) - 1):
        f = bm.faces.new((s_v, vs[i], vs[i + 1]))
        shade[f] = 0.96 if ring[i][1] <= ring[i + 1][1] else 0.8
    bm.normal_update()
    bmesh.ops.reverse_faces(bm, faces=[f for f in bm.faces if f.normal.y > 0])
    bm.faces.index_update()
    values = {f.index: g for f, g in shade.items()}
    _as_card(b, lambda: b.add_bm(bm, glow, tint=_tint_map(bm, values)))
    wire = craft.paint()
    for tip in tips:
        b.rod(wire, (side * sh[0], -0.004, sh[1]), (side * tip[0], -0.015, tip[1]), 0.0022, 4, lc(INK))


def paper_lantern(b, craft, shape, M=None, rgb=None, detail=1.0):
    """Build one lantern into `b`, hanging from the origin (top of its ring) under the matrix `M`. `rgb`: bake the
    paper in that light colour (the table's samples); None: the neutral paper the game tints."""
    pts = outline(shape, max(24, int(POINTS[shape] * detail) // 2 * 2))
    if _area(pts) < 0:
        pts = pts[::-1]
    c = CENTRES[shape]
    top_z = max(p[1] for p in pts if abs(p[0]) < 0.02) if shape != "leaf" else max(p[1] for p in pts)
    drop = BAIL + top_z
    half = DEPTH / 2 * (0.8 if shape == "bat" else 1.0)
    radius = max(math.hypot(p[0] - c[0], p[1] - c[1]) for p in pts)
    with b.at((M if M is not None else Matrix.Identity(4)) @ T(0, 0, -drop)):
        world_c = b.stack[-1] @ Vector((c[0], 0.0, c[1] - 0.035))
        scale = (b.stack[-1].to_3x3() @ Vector((1, 0, 0))).length
        glow = paper_glow(craft, tuple(world_c), radius * 1.05 * scale, PAPER if rgb is None else el.blend(PAPER, rgb, 0.92))
        normals = _normals(pts)
        rings = (1.0, 0.66, 0.3) if detail >= 1 else (1.0, 0.5)
        _panel(b, glow, pts, c, -1, half, 1.0, rings)
        _panel(b, glow, pts, c, 1, half, 1.0, rings)
        _band(b, glow, pts, normals, half)
        ink = craft.paint()
        for side in (-1, 1):
            _rim(b, ink, pts, normals, c, side, half, lc(INK))
        face = Face(b, craft, pts, c, half)
        face.coarse = detail < 1
        _features(face, shape, detail)
        _bail(b, craft, (0.0, top_z), DEPTH * (0.8 if shape == "bat" else 1.0), drop)
        bottom = min(p[1] for p in pts)
        if shape == "bat":
            for side in (-1, 1):
                _bat_wing(b, craft, glow, side, detail)
            _tassel(b, craft, (0.0, bottom + 0.004), half, 0.8, detail)
            low = bottom - 0.07
        elif shape == "cat":
            for side in (-1, 1):  # wire whiskers past the cheeks
                for k in (-1, 1):
                    b.rod(craft.paint(), (side * 0.13, -half * 0.2, -0.03 + 0.006 * k), (side * 0.19, -half * 0.2, -0.028 + 0.024 * k), 0.0018, 4,
                          lc(INK))
            _tassel(b, craft, (0.0, bottom + 0.004), half, 1.0, detail)
            low = bottom - 0.08
        elif shape == "leaf":  # the stem, a rolled brown paper twist
            b.rod(craft.paint(), (0.0, 0.0, bottom + 0.012), (0.012, 0.0, bottom - 0.06), 0.0055, 6, lc(E["timber_dark"]))
            low = bottom - 0.06
        else:
            low = bottom
    return {"drop": drop, "low": low - drop, "centre": c[1] - drop}


def build_lantern(craft, shape):
    b = Builder(500 + SHAPES.index(shape))
    b.part = None
    info = paper_lantern(b, craft, shape)
    b.finish(MODELS[shape])
    return info


def drop_to_centre(shape):
    """From the hang ring's top down to the middle of the lantern's paper (where a light would go)."""
    pts = outline(shape)
    top_z = max(p[1] for p in pts if abs(p[0]) < 0.02) if shape != "leaf" else max(p[1] for p in pts)
    return BAIL + top_z - CENTRES[shape][1]


# --- the folding craft table -------------------------------------------------------------------------------

TOP_W, TOP_D, TOP_H = 1.3, 0.6, 0.62  # a child's craft table (Pip is 1.05 m)
POLE_H = 1.78
SAMPLE_SCALE = 0.8
SAMPLES = (("bat", "plum"), ("cat", "orange"), ("ghost", "yellow"), ("leaf", "green"))


def _x_legs(b, P, x):
    """One pair of crossed folding legs at the table end x, seen from the front as an X, with its hinge bolt."""
    h = TOP_H - 0.035
    for s in (-1, 1):
        a = (x, s * (TOP_D / 2 - 0.06), 0.0)
        c2 = (x, -s * (TOP_D / 2 - 0.08), h - 0.02)
        off = 0.022 if s > 0 else -0.022
        d = Vector(c2) - Vector(a)
        L = d.length
        rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
        b.box(P["legs"], (0.04, 0.03, L), ((a[0] + c2[0]) / 2 + off, (a[1] + c2[1]) / 2, (a[2] + c2[2]) / 2), rot=rot, bevel=0.008)
        b.box(P["feet"], (0.05, 0.06, 0.025), (x + off, a[1], 0.0125), bevel=0.006)
    b.rod(P["iron"], (x - 0.04, 0.0, h * 0.5 + 0.01), (x + 0.04, 0.0, h * 0.5 + 0.01), 0.012, 8, lc(INK))


def build_table(craft):
    """The lantern table (see the module doc). Returns the samples' (shape, colour, hang point) for the reviews."""
    b = Builder(610)
    b.part = None
    P = {
        "top": craft.timber(E["bone_dim"], "X", wear=0.45, bare=E["timber"]),
        "edge": craft.timber(E["plum"], "X", wear=0.35, bare=E["timber_dark"]),
        "legs": craft.timber(E["orange"], "Z", wear=0.4, bare=E["timber_dark"]),
        "feet": craft.tinted(E["ink"], 0.6, 0.1),
        "iron": craft.paint(),
        "pole": craft.timber(E["candy"], "Z", wear=0.25, bare=E["timber"]),
    }
    paint, paper = craft.paint(), craft.paper_white()
    # the top: a painted board with a plum edge band
    b.box(P["top"], (TOP_W, TOP_D, 0.028), (0, 0, TOP_H - 0.014), bevel=0.008)
    for sy in (-1, 1):
        b.box(P["edge"], (TOP_W + 0.012, 0.016, 0.05), (0, sy * (TOP_D / 2 + 0.004), TOP_H - 0.03), bevel=0.005)
    for sx in (-1, 1):
        b.box(P["edge"], (0.016, TOP_D, 0.05), (sx * (TOP_W / 2 + 0.004), 0, TOP_H - 0.03), bevel=0.005)
    for sx in (-1, 1):
        _x_legs(b, P, sx * (TOP_W / 2 - 0.1))
    z = TOP_H + 0.001
    # a sheet of kraft paper taped down where the folding happens
    b.box(craft.tinted(H(0xC9A27A), 0.95, 0.12, 30.0), (0.5, 0.34, 0.002), (0.02, -0.04, z + 0.001), bevel=0.0, tint=1.0)
    for x, y in ((-0.22, -0.2), (0.26, -0.2), (-0.22, 0.12), (0.26, 0.12)):
        b.box(paint, (0.05, 0.02, 0.0015), (x, y, z + 0.0028), rot=R("Z", 35 if x * y > 0 else -35), bevel=0.0, tint=lc(H(0xE9DDBF)))
    # the paper in the four colours, fanned at the left
    for k, (cid, ang) in enumerate((("plum", -18), ("green", -6), ("orange", 6), ("yellow", 18))):
        with b.at(T(-0.42, -0.02, z + 0.003 + 0.0035 * k) @ R("Z", ang)):
            for n in range(3):
                b.box(paper, (0.2, 0.2, 0.0012), (0.004 * n, -0.003 * n, 0.0012 * n), bevel=0.0, tint=lc(el.shade(COLOURS[cid][0], 1.0 - 0.06 * n)))
    # paste pot with its brush, at the right
    with b.at(T(0.44, 0.08, z)):
        b.lathe(paint, [(0.0, 0.0), (0.052, 0.0), (0.056, 0.01), (0.054, 0.07), (0.06, 0.078), (0.06, 0.088), (0.048, 0.09), (0.046, 0.07),
                        (0.0, 0.07)], 12, tint=lc(E["indigo_light"]))
        b.lathe(paint, [(0.0566, 0.032), (0.0566, 0.052), (0.0, 0.052)], 12, tint=lc(BONE))  # a bone band (the label, no words)
        b.lathe(paint, [(0.0466, 0.078), (0.0, 0.081)], 12, tint=lc(H(0xF3EEDF)))  # the paste
        b.rod(P["pole"], (0.0, 0.0, 0.07), (0.07, 0.05, 0.21), 0.006, 6, 1.0)
        b.rod(paint, (-0.006, -0.004, 0.078), (0.004, 0.003, 0.098), 0.009, 6, lc(INK), r2=0.006)
    # a pleated strip half folded into a lantern band, in front of the middle
    with b.at(T(0.05, -0.08, z + 0.002) @ R("Z", -8)):
        bm = bmesh.new()
        n, w = 14, 0.024
        top_row, bot_row = [], []
        for i in range(n + 1):
            x = (i - n / 2) * w * (0.55 if i > n * 0.55 else 1.0)
            hgt = 0.026 if i % 2 == 0 else 0.0
            if i > n * 0.55:
                hgt *= 1.6
            top_row.append(bm.verts.new((x, 0.04, hgt)))
            bot_row.append(bm.verts.new((x, -0.04, hgt)))
        for i in range(n):
            bm.faces.new((bot_row[i], bot_row[i + 1], top_row[i + 1], top_row[i]))
        _as_card(b, lambda: b.add_bm(bm, paper, tint=lc(COLOURS["green"][0]), smooth=False))
    # two cut-out shapes waiting to be glued: a bat in plum card and a leaf in yellow
    bat_pts = outline("bat", 28)
    with b.at(T(-0.13, 0.15, z + 0.0035) @ R("Z", 14) @ R("X", 90)):
        b.prism(paint, [(x * 0.9, zz * 0.9) for x, zz in bat_pts], 0.0015, tint=lc(COLOURS["plum"][0]))
    leaf_pts = outline("leaf", 40)
    with b.at(T(0.24, -0.16, z + 0.0035) @ R("Z", -22) @ R("X", 90)):
        b.prism(paint, [(x * 0.55, zz * 0.55) for x, zz in leaf_pts], 0.0015, tint=lc(COLOURS["yellow"][0]))
    # the poles at the back corners and the string of finished lanterns
    px, py = TOP_W / 2 - 0.03, TOP_D / 2 - 0.02
    for sx in (-1, 1):
        b.cyl(P["pole"], 0.016, POLE_H - TOP_H + 0.08, T(sx * px, py, (POLE_H + TOP_H - 0.08) / 2), segments=8)
        b.box(P["feet"], (0.05, 0.05, 0.06), (sx * px, py, TOP_H - 0.055), bevel=0.006)  # the clamp
        b.lathe(paint, [(0.0, 0.0), (0.026, 0.006), (0.03, 0.03), (0.02, 0.05), (0.0, 0.056)], 10, T(sx * px, py, POLE_H), tint=lc(E["orange"]))
    rope = el.catenary((-px, py, POLE_H - 0.01), (px, py, POLE_H - 0.01), 0.1, 12)
    for a, c2 in zip(rope, rope[1:]):
        b.rod(paint, a, c2, 0.0035, 4, lc(INK))
    samples = []
    for k, (shape, cid) in enumerate(SAMPLES):
        t = (0.2, 0.405, 0.605, 0.805)[k]  # the bat's wings stay clear of the left pole
        at = rope[0].lerp(rope[-1], t) - Vector((0, 0, 0.1 * 4 * t * (1 - t)))
        hang = at - Vector((0, 0, 0.05 + 0.035 * (k % 2)))
        b.rod(paint, at, hang, 0.0025, 4, lc(INK))
        M = T(*hang) @ R("Z", (-10, 6, -4, 10)[k]) @ S(SAMPLE_SCALE)
        paper_lantern(b, craft, shape, M, rgb=COLOURS[cid][1], detail=0.55)
        samples.append((shape, cid, hang))
    b.finish(TABLE)
    return samples


# --- Pip's porch hook ---------------------------------------------------------------------------------------

POST_OFF = (-0.09, -0.02)  # the post stands just outside the rail's corner (model x, y from the origin)
POST_H = 2.02  # above the deck
ARM_REACH = 0.44  # from the post's centre to the hang point, along +X
HANG_Z = 1.99  # hang point above the deck
BELL_Z = 0.98  # the bell plate's centre above the deck (a child's reach from the porch steps)


def hook_pivots():
    return {HANG: T(POST_OFF[0] + ARM_REACH, POST_OFF[1], HANG_Z), BELL: T(POST_OFF[0], POST_OFF[1] - 0.0535, BELL_Z)}


def build_hook(craft):
    b = Builder(620)
    b.part = None
    post = craft.timber(E["bone_dim"], "Z", wear=0.5, bare=E["timber_dark"])
    bracket = craft.timber(E["bone_dim"], "X", wear=0.5, bare=E["timber_dark"])
    orange = craft.timber(E["orange"], "X", wear=0.35, bare=E["timber_dark"])
    paint = craft.paint()
    x0, y0 = POST_OFF
    with b.at(T(x0, y0, 0)):
        b.box(post, (0.12, 0.12, 0.16), (0, 0, 0.08), bevel=0.014)
        b.box(post, (0.075, 0.075, POST_H - 0.16), (0, 0, 0.16 + (POST_H - 0.16) / 2), bevel=0.012)
        b.box(orange, (0.095, 0.095, 0.03), (0, 0, POST_H - 0.22), bevel=0.008, tint=1.0)
        b.box(post, (0.1, 0.1, 0.04), (0, 0, POST_H - 0.02), bevel=0.01)
        b.lathe(paint, [(0.0, 0.0), (0.03, 0.0), (0.04, 0.03), (0.026, 0.06), (0.012, 0.075), (0.0, 0.082)], 10, T(0, 0, POST_H),
                tint=lc(E["orange"]))
        for z in (0.32, 0.72):  # iron straps that clamp it to the rail
            b.box(paint, (0.085, 0.085, 0.022), (0, 0, z), bevel=0.004, tint=lc(INK))
            b.cyl(paint, 0.007, 0.006, T(0, -0.044, z) @ R("X", 90), segments=6, tint=lc(H(0x4A3F5A)))
        # the bracket under the arm (the porch's fretwork, small)
        r = 0.26
        outline_ = [(0.0, 0.0), (r, 0.0), (r, -0.05)] + el.arc(r, -r, r - 0.05, 90, 180, 6)[1:] + [(0.0, -r)]
        b.prism(bracket, outline_, 0.03, T(0.035, -0.015, HANG_Z + 0.07), bevel=0.008)
        b.disc(paint, 0.026, 0.012, T(0.035 + 0.085, -0.02, HANG_Z + 0.07 - 0.085), segs=10, tint=lc(E["orange"]), bevel=0.003)
        # the ink iron arm with a curl at its end, the hook eye hangs under the curl
        arm_z = HANG_Z + 0.085
        pts = [Vector((0.03, 0, arm_z)), Vector((ARM_REACH + 0.02, 0, arm_z))]
        cx, cz, rr = ARM_REACH + 0.02, arm_z + 0.035, 0.035
        curl = [Vector((cx + rr * (1 - 0.18 * k / 10) * math.sin(math.radians(36 * k)), 0, cz - rr * (1 - 0.18 * k / 10) * math.cos(math.radians(36 * k))))
                for k in range(11)]
        pts += curl[1:]
        for a, c2 in zip(pts, pts[1:]):
            b.rod(paint, a, c2, 0.009, 6, lc(INK))
        b.box(paint, (0.06, 0.03, 0.05), (0.04, 0, arm_z), bevel=0.006, tint=lc(INK))  # the arm's socket on the post
        # the bell plate: a round orange board with a bone rim; the button is its own part
        plate = -0.0375 - 0.016  # the plate's front face (it sits on the post's front)
        b.disc(orange, 0.06, 0.016, T(0, plate, BELL_Z), segs=16, tint=1.0, bevel=0.004)
        b.ring(paint, 0.05, 0.062, 0.004, T(0, plate - 0.002, BELL_Z), segs=16, tint=lc(BONE))
        b.part = BELL
        with b.at(T(0, plate, BELL_Z) @ R("X", 90)):  # lathes run along +Z: turned to stand out toward -Y
            b.lathe(paint, [(0.0, -0.002), (0.028, -0.002), (0.028, 0.006), (0.022, 0.012), (0.0, 0.013)], 12, tint=lc(BONE))
            b.lathe(paint, [(0.016, 0.011), (0.017, 0.017), (0.011, 0.022), (0.0, 0.023)], 10, tint=lc(E["candy"]))
        b.part = HANG
        b.ring(paint, 0.008, 0.013, 0.005, T(ARM_REACH, -0.0025, HANG_Z + 0.013) @ R("Y", 0), segs=10, tint=lc(INK), chamfer=0.0)
        b.rod(paint, (ARM_REACH, 0, HANG_Z + 0.026), (ARM_REACH + 0.002, 0, arm_z - 0.004), 0.004, 5, lc(INK))
        b.part = None
    b.finish(HOOK)
