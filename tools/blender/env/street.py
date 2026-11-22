"""Street dressing for Lantern Lane (spec §4, §6): the ground set (lawn tile, flagstone path strip, sidewalk
module, leaf scatter cards), the long boundary hedge and the three skyline neighbour houses.

Same craft as the corner: chunky painted timber, painted stone and cut paper, built from `env_lib.Builder`
pieces and the porch kit's builders. Each `build_*` adds to a Builder at the origin, on the ground, facing -Y.

Ground rules (so colliders stay simple boxes): nothing rises more than 3 cm above z = 0, and every continuous
surface is painted with noise that repeats exactly over the module (`ground_paint`), so tiles laid on a 2 m or
4 m grid show no seam. Flagstones and slabs are whole pieces: the path layout wraps around the module end, so a
stone may poke into the next module, where the same layout leaves room for it.

The neighbour houses are seen at 15 to 30 m: big boards, big shingles, no bevels, a plain back wall (+Y).
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh
from mathutils import Matrix, Vector, noise

import env_lib as el
import porch_kit as pk
import props as pr
from env_lib import E, R, S, T, Builder, H, lc, shade

TOP = 0.03  # the highest any ground piece reaches
LAWN, LAWN_GRID, LAWN_MAX = 4.0, 8, 0.018
PATH_W, PATH_L = 1.6, 2.0
WALK = 2.0
HEDGE_LEN = 6.0


# --- repeating ground paint --------------------------------------------------------------------------------

def _periodic(nodes, links, period, nx, ny, detail=2.0, rough=0.55, offset=0.0):
    """Noise that repeats exactly every `period` metres in x and y: 4D noise read on a flat torus, with about
    nx by ny features per period (unequal counts give brush strokes)."""
    coord = nodes.new("ShaderNodeTexCoord")
    split = nodes.new("ShaderNodeSeparateXYZ")
    links.new(coord.outputs["Object"], split.inputs[0])

    def turn(axis, op, radius, add=0.0):
        ang = nodes.new("ShaderNodeMath")
        ang.operation = "MULTIPLY"
        ang.inputs[1].default_value = 2 * math.pi / period
        links.new(split.outputs[axis], ang.inputs[0])
        fn = nodes.new("ShaderNodeMath")
        fn.operation = op
        links.new(ang.outputs[0], fn.inputs[0])
        out = nodes.new("ShaderNodeMath")
        out.operation = "MULTIPLY_ADD"
        out.inputs[1].default_value = radius
        out.inputs[2].default_value = add
        links.new(fn.outputs[0], out.inputs[0])
        return out.outputs[0]
    rx, ry = nx / (2 * math.pi), ny / (2 * math.pi)
    vec = nodes.new("ShaderNodeCombineXYZ")
    links.new(turn("X", "COSINE", rx, offset), vec.inputs[0])
    links.new(turn("X", "SINE", rx, offset * 0.37), vec.inputs[1])
    links.new(turn("Y", "COSINE", ry, offset * 1.71), vec.inputs[2])
    tex = nodes.new("ShaderNodeTexNoise")
    tex.noise_dimensions = "4D"
    tex.inputs["Scale"].default_value = 1.0
    tex.inputs["Detail"].default_value = detail
    tex.inputs["Roughness"].default_value = rough
    links.new(vec.outputs[0], tex.inputs["Vector"])
    links.new(turn("Y", "SINE", ry), tex.inputs["W"])
    return tex.outputs["Fac"]


def _ramp(nodes, links, value, lo, hi):
    node = nodes.new("ShaderNodeMapRange")
    node.inputs["From Min"].default_value, node.inputs["From Max"].default_value = lo, hi
    links.new(value, node.inputs["Value"])
    return node.outputs["Result"]


def _over(nodes, links, factor, base, rgb, amount):
    k = nodes.new("ShaderNodeMath")
    k.operation = "MULTIPLY"
    k.inputs[1].default_value = amount
    links.new(factor, k.inputs[0])
    mix = nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    links.new(k.outputs[0], mix.inputs[0])
    links.new(base, mix.inputs[6])
    mix.inputs[7].default_value = el.lin(rgb)
    return mix.outputs[2]


def ground_paint(craft, name, period, deep, light, stroke, strokes=(110, 36), patches=13, relief=0.6):
    """Painted felt ground: soft patches from `deep` to `light`, short brushed blades of `stroke` and their
    shadows, all repeating every `period` metres, times the piece tint."""
    def build():
        mat, nodes, links, bsdf = craft._base(name)
        big = _periodic(nodes, links, period, 3, 3, 1.0, 0.5, 3.1)
        mid = _periodic(nodes, links, period, patches, patches, 2.0, 0.55, 7.7)
        dab = _periodic(nodes, links, period, strokes[0], strokes[1], 1.5, 0.6, 1.3)
        fuzz = _periodic(nodes, links, period, period * 90, period * 90, 1.0, 0.5, 5.9)
        both = nodes.new("ShaderNodeMix")
        both.inputs[0].default_value = 0.25
        links.new(mid, both.inputs[2])
        links.new(big, both.inputs[3])
        colour = craft._mix_colors(nodes, links, _ramp(nodes, links, both.outputs[0], 0.38, 0.62), el.lin(deep), el.lin(light))
        colour = _over(nodes, links, _ramp(nodes, links, dab, 0.55, 0.63), colour, stroke, 0.6)
        colour = _over(nodes, links, _ramp(nodes, links, dab, 0.45, 0.37), colour, shade(deep, 0.6), 0.55)
        links.new(craft._tint(nodes, links, colour), bsdf.inputs["Base Color"])
        height = nodes.new("ShaderNodeMix")
        height.inputs[0].default_value = 0.5
        links.new(mid, height.inputs[2])
        links.new(dab, height.inputs[3])
        tall = nodes.new("ShaderNodeMath")
        tall.operation = "MULTIPLY_ADD"
        tall.inputs[1].default_value = 0.25
        links.new(fuzz, tall.inputs[0])
        links.new(height.outputs[0], tall.inputs[2])
        links.new(craft._bump(nodes, links, tall.outputs[0], relief, 0.03), bsdf.inputs["Normal"])
        bsdf.inputs["Roughness"].default_value = 1.0
        bsdf.inputs["Sheen Weight"].default_value = 0.3
        mat.diffuse_color = el.lin(light)
        return mat
    return craft._cached(("ground", name), build)


GRASS_DEEP, GRASS_LIGHT, GRASS_BLADE = H(0x1C3445), H(0x2B524E), H(0x467859)
STONE = H(0x6A5C72)  # warm grey-plum
STONE_TINTS = [(1.0, 1.0, 1.0), (1.1, 0.98, 0.95), (0.92, 0.94, 1.08), (1.08, 0.94, 1.04), (0.86, 0.86, 0.92), (1.16, 1.06, 1.02)]


def lawn_paint(craft):
    return ground_paint(craft, "LawnPaint", LAWN, GRASS_DEEP, GRASS_LIGHT, GRASS_BLADE)


def lawn_height(x, y):
    """Gentle swells, repeating every 4 m, between 0 and LAWN_MAX."""
    u, v = 2 * math.pi * x / LAWN, 2 * math.pi * y / LAWN
    h = 0.5 + 0.2 * math.sin(u + 1.3) * math.cos(2 * v + 0.4) + 0.16 * math.sin(2 * u + v + 2.1) + 0.12 * math.cos(3 * v - 2 * u)
    return max(0.0, min(1.0, h)) * LAWN_MAX


def lawn_normal(x, y, e=0.01):
    n = Vector((lawn_height(x - e, y) - lawn_height(x + e, y), lawn_height(x, y - e) - lawn_height(x, y + e), 2 * e))
    return n.normalized()


def build_lawn(craft):
    """4 x 4 m lawn tile. Heights and paint repeat over the tile, so it tiles on a 4 m grid (same rotation)."""
    b = Builder(41)
    bm = bmesh.new()
    n, step = LAWN_GRID, LAWN / LAWN_GRID
    grid = [[bm.verts.new((-LAWN / 2 + i * step, -LAWN / 2 + j * step, lawn_height(-LAWN / 2 + i * step, -LAWN / 2 + j * step)))
             for i in range(n + 1)] for j in range(n + 1)]
    for j in range(n):
        for i in range(n):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    b.add_bm(bm, lawn_paint(craft), tint=1.0)
    return b.finish("Lawn")


# --- flagstones and slabs ----------------------------------------------------------------------------------

def _clip(poly, point, normal):
    """Keep the part of a convex polygon where (p - point) . normal >= 0."""
    out = []
    for i, p in enumerate(poly):
        q = poly[(i + 1) % len(poly)]
        dp, dq = (p - point).dot(normal), (q - point).dot(normal)
        if dp >= 0:
            out.append(p)
        if (dp >= 0) != (dq >= 0):
            out.append(p + (q - p) * (dp / (dp - dq)))
    return out


def _inset(poly, d):
    out = list(poly)
    for i, p in enumerate(poly):
        q = poly[(i + 1) % len(poly)]
        e = q - p
        if e.length < 1e-6:
            continue
        nrm = Vector((-e.y, e.x)).normalized()  # inward for a counter-clockwise polygon
        out = _clip(out, p + nrm * d, nrm)
        if len(out) < 3:
            return []
    return out


def _round(poly, cut=0.22, tiny=0.02):
    """Knock the corners off (one corner-cutting pass) and drop slivers of edge."""
    pts = []
    for i, p in enumerate(poly):
        q = poly[(i + 1) % len(poly)]
        for t in (cut, 1 - cut):
            c = p.lerp(q, t)
            if not pts or (c - pts[-1]).length > tiny:
                pts.append(c)
    if len(pts) > 3 and (pts[0] - pts[-1]).length <= tiny:
        pts.pop()
    return pts


def slab(b, mat, poly, top, bevel=0.012, tint=(1, 1, 1), edge=1.2, side=0.62, base=0.0):
    """A chunky stone: flat top inset by `bevel`, a chamfer that catches the light (painted lighter), sides
    going down to `base` (painted darker, as if in its own shadow)."""
    inner = _inset(poly, bevel)
    if len(inner) != len(poly):  # a tiny edge vanished: inset corner by corner toward the middle instead
        c = sum(poly, Vector((0, 0))) / len(poly)
        inner = [p + (c - p).normalized() * bevel * 1.3 for p in poly]
    bm = bmesh.new()
    zc = top - bevel * 0.75
    ring_t = [bm.verts.new((p.x, p.y, top)) for p in inner]
    ring_c = [bm.verts.new((p.x, p.y, zc)) for p in poly]
    ring_b = [bm.verts.new((p.x, p.y, base)) for p in poly]
    bm.faces.new(ring_t)
    n = len(poly)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((ring_c[i], ring_c[j], ring_t[j], ring_t[i]))
        bm.faces.new((ring_b[i], ring_b[j], ring_c[j], ring_c[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

    def paint(c):
        k = 1.0 if c.z > top - 1e-4 else edge if c.z > zc + 1e-4 else side
        return tuple(t * k for t in tint)
    b.add_bm(bm, mat, None, paint, smooth=False)


def _path_layout(seed=7):
    """Seed points of the flagstones (rows of two and three across the strip, jittered) and of the phantom
    stones that shape the ragged outer edge."""
    rng = random.Random(seed)
    stones, phantoms = [], []
    for row in range(4):
        y = -PATH_L / 2 + (row + 0.5) * PATH_L / 4
        xs = (-0.36, 0.38) if row % 2 else (-0.5, 0.02, 0.52)
        for x in xs:
            stones.append(Vector((x + rng.uniform(-0.07, 0.07), y + rng.uniform(-0.09, 0.09))))
    for k in range(5):
        y = -PATH_L / 2 + (k + 0.5) * PATH_L / 5
        for sx in (-1, 1):
            phantoms.append(Vector((sx * rng.uniform(0.98, 1.16), y + rng.uniform(-0.1, 0.1))))
    return stones, phantoms


def build_path(craft):
    """Flagstone strip, about 1.6 m wide by 2 m long (along Y), grass showing between the stones. The layout
    repeats every 2 m along Y, so strips laid end to end interlock."""
    b = Builder(42)
    rng = b.rng
    mat = craft.tinted(STONE, 0.9, 0.3, 4.5)
    stones, phantoms = _path_layout()
    others = [p + Vector((0, k * PATH_L)) for p in stones + phantoms for k in (-1, 0, 1)]
    for s in stones:
        cell = [s + Vector(c) for c in ((-2, -2), (2, -2), (2, 2), (-2, 2))]
        for o in others:
            if (o - s).length < 1e-6:
                continue
            cell = _clip(cell, (s + o) / 2, (s - o).normalized())
        cell = _round(_inset(cell, 0.032))
        tint = rng.choice(STONE_TINTS)
        v = rng.uniform(0.86, 1.1)
        slab(b, mat, cell, TOP - rng.choice((0.0, 0.0, 0.003, 0.005)), 0.017, tuple(c * v for c in tint), edge=1.28, side=0.55)
    return b.finish("PathStones")


def _quad(x0, x1, y0, y1, rng, jit=0.008):
    j = lambda: rng.uniform(-jit, jit)  # noqa: E731
    return [Vector((x0 + j(), y0 + j())), Vector((x1 + j(), y0 + j())), Vector((x1 + j(), y1 + j())), Vector((x0 + j(), y1 + j()))]


def build_sidewalk(craft):
    """2 x 2 m sidewalk module running along X: a kerb of chunky blocks on the street side (-Y), two courses of
    paving slabs with open seams, on a dark bed. Tiles on a 2 m grid along X."""
    b = Builder(43)
    rng = b.rng
    slab_mat = craft.tinted(H(0x5A5370), 0.9, 0.26, 3.5)
    kerb_mat = craft.tinted(H(0x74667A), 0.9, 0.3, 5.0)
    bed = ground_paint(craft, "WalkBed", WALK, H(0x1C1930), H(0x262040), H(0x2E2A4A), (40, 40), 9, 0.3)
    bm = bmesh.new()
    bm.faces.new([bm.verts.new(p) for p in ((-1, -0.9, 0.006), (1, -0.9, 0.006), (1, 1, 0.006), (-1, 1, 0.006))])
    b.add_bm(bm, bed, tint=1.0, smooth=False)
    g = 0.011  # half a seam
    for x0, x1 in ((-1.0, -0.34), (-0.34, 0.36), (0.36, 1.0)):
        v = rng.uniform(0.9, 1.1)
        slab(b, kerb_mat, _round(_quad(x0 + g, x1 - g, -1.0, -0.74 - g, rng, 0.004), 0.06), TOP, 0.016,
             tuple(c * v for c in rng.choice(STONE_TINTS[:4])), edge=1.22, side=0.7)
    rows = ((-0.74, 0.12, (-1.0, 0.04, 1.0)), (0.12, 1.0, (-1.0, -0.42, 0.5, 1.0)))
    for y0, y1, xs in rows:
        for x0, x1 in zip(xs, xs[1:]):
            v = rng.uniform(0.88, 1.08)
            poly = _quad(x0 + g, x1 - g, y0 + g, y1 - g, rng)
            if x1 - x0 > 0.95 and y0 < 0:  # one slab has lost a corner
                poly = poly[:2] + [poly[2] + Vector((0, -0.1)), poly[2] + Vector((-0.13, 0))] + poly[3:]
            slab(b, slab_mat, _round(poly, 0.035, 0.004), TOP - rng.choice((0.003, 0.004, 0.006)), 0.01,
                 tuple(c * v for c in rng.choice(STONE_TINTS)), edge=1.16, side=0.6)
    return b.finish("Sidewalk")


# --- leaves ------------------------------------------------------------------------------------------------

LEAF_RGB = (E["leaf_a"], E["leaf_b"], E["leaf_c"], H(0xD9632B), H(0xB5372A))


def _ground_leaf(b, craft, x, y, size, rz, rgb):
    """A paper maple leaf lying face up, cupped a little, its lowest point just above the lawn's highest."""
    m = T(x, y, LAWN_MAX + 0.002) @ R("Z", rz) @ R("X", -90)
    half = size * 0.62
    fold = 0.05
    curl = min(0.14, (TOP - LAWN_MAX - 0.003 - fold * half) / half)
    pr.leaf(b, craft, m, size, rgb, curl=curl, fold=fold, low=True)


def build_leaf_scatter(craft, kind="A"):
    """Leaf decal cards: A a loose drift of seven, B a little heap of five, C a windblown trail of ten."""
    b = Builder({"A": 51, "B": 52, "C": 53}[kind])
    rng = b.rng
    spots = []
    if kind == "A":
        spots = [(rng.uniform(-0.55, 0.55), rng.uniform(-0.45, 0.45)) for _ in range(40)]
        count, lo, hi, gap = 7, 0.13, 0.2, 0.26
    elif kind == "B":
        spots = [(rng.gauss(0, 0.16), rng.gauss(0, 0.14)) for _ in range(40)]
        count, lo, hi, gap = 5, 0.15, 0.22, 0.17
    else:
        spots = [(-1.0 + 2.0 * t + rng.uniform(-0.08, 0.08), 0.22 * math.sin(t * 5.0) + rng.uniform(-0.12, 0.12))
                 for t in (k / 39 for k in range(40))]
        rng.shuffle(spots)
        count, lo, hi, gap = 10, 0.11, 0.19, 0.2
    placed = []
    for x, y in spots:
        if len(placed) >= count or any(math.hypot(x - px, y - py) < gap for px, py in placed):
            continue
        placed.append((x, y))
        _ground_leaf(b, craft, x, y, rng.uniform(lo, hi), rng.uniform(0, 360), rng.choice(LEAF_RGB))
    return b.finish("LeafScatter" + kind)


# --- boundary hedge ----------------------------------------------------------------------------------------

def build_hedge_long(craft, w=HEDGE_LEN, d=0.8, h=1.05, seed=27, name="BoundaryHedgeLong"):
    """The corner's hedge (props.build_hedge) drawn out to close a block edge: clipped faces, a softly rolling
    top, bigger and fewer leaves, darker toward the ground, a few fallen maple leaves on top."""
    b = Builder(seed)
    mat = craft.tinted(E["hedge"], 0.95, 0.4, 13.0)
    rng = b.rng
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for axis, cuts in ((0, 18), (1, 3), (2, 3)):
        for k in range(1, cuts):
            co = Vector((0, 0, 0))
            co[axis] = -0.5 + k / cuts
            no = Vector((0, 0, 0))
            no[axis] = 1.0
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no)
    hh = h - 0.1

    def roll(x):
        return 0.07 * math.sin(x * 1.9 + 0.6) + 0.05 * math.sin(x * 4.3 + 2.0)
    for v in bm.verts:
        n = v.co * 2
        soft = 0.03 + 0.075 * max(0.0, n.z) ** 2
        k = 1 - soft * (abs(n.x) ** 10 + abs(n.y) ** 6 + abs(n.z) ** 6 - 1)
        taper = 1 - 0.08 * (v.co.z + 0.5)
        p = Vector((v.co.x * w * k, v.co.y * d * taper * k, v.co.z * hh * (1 - (1 - k) * 0.5)))
        p.z += roll(p.x) * (v.co.z + 0.5) ** 2
        p.y += 0.05 * math.sin(p.x * 2.7 + 1.0) * (0.4 + v.co.z + 0.5)
        bump = noise.noise(p * 2.2 + Vector((seed, 0, 0))) * 0.05
        v.co = p + Vector((0, n.y, n.z)).normalized() * bump + Vector((0, 0, hh / 2 + 0.1))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()

    def grade(z):
        return 0.5 + 0.8 * max(0.0, min(1.0, z / h)) ** 1.3
    cards = []
    for f in bm.faces:
        c, nrm = f.calc_center_median(), f.normal.copy()
        if nrm.z < -0.5:
            continue
        for _ in range(3 if nrm.z > 0.5 else 2):
            p = c + Vector((rng.uniform(-0.15, 0.15), rng.uniform(-0.1, 0.1), rng.uniform(-0.13, 0.13)))
            tan = nrm.cross(Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))))
            if tan.length < 0.1:
                continue
            tan.normalize()
            out = (nrm * rng.uniform(0.25, 0.7) + tan * 0.9).normalized()
            side = out.cross(nrm).normalized()
            ln, wd = rng.uniform(0.1, 0.17), rng.uniform(0.035, 0.055)
            lightness = rng.uniform(0.8, 1.4) * grade(p.z) * (1.15 if nrm.z > 0.5 else 1.0)
            cards.append(([p - out * 0.03, p + out * ln * 0.45 + side * wd, p + out * ln, p + out * ln * 0.45 - side * wd],
                          (lightness * 0.95, lightness, lightness * 0.74)))
    b.add_bm(bm, mat, tint=lambda c: (grade(c.z) * 0.85,) * 3)
    for pts, tint in cards:
        b.poly(mat, pts, tint=tint)
    for i in range(9):
        x = -w / 2 + 0.3 + i * (w - 0.6) / 8 + rng.uniform(-0.1, 0.1)
        b.rod(craft.paint(), (x, rng.uniform(-0.1, 0.1), 0.0), (x + rng.uniform(-0.05, 0.05), 0, 0.24), 0.03, 5, lc(E["bark"]), r2=0.022)
    for k in range(6):
        x = rng.uniform(-w / 2.3, w / 2.3)
        pr.leaf(b, craft, T(x, rng.uniform(-d / 4, d / 4), h + roll(x) + 0.03) @ R("Z", rng.uniform(0, 360)) @ R("X", 80), 0.17,
                LEAF_RGB[k % 3], low=True)
    return b.finish(name)


# --- skyline houses: shared pieces -------------------------------------------------------------------------

WINDOW_GLOW = (0.68, 0.42, 0.18)  # dim amber paper: well under the attic window, and under the bloom threshold
PLINTH = 0.4
BOARD = 0.32
TAB, EXPOSURE = 0.92, 0.72
ROOF_TINTS = pk.ROOF_TINTS


def far_paints(craft, body, roof, trim=None, accent=None, door=None):
    """A neighbour's paint scheme: dark body and roof so the house sits back, dulled trim, one accent."""
    trim = trim or shade(E["bone_dim"], 0.62)
    return {
        "clap": craft.timber(body, "X", wear=0.2), "wall_v": craft.timber(shade(body, 0.8), "Z", wear=0.15),
        "back": craft.timber(shade(body, 0.7), "X", wear=0.0),
        "trim_x": craft.timber(trim, "X", wear=0.5, bare=E["timber_dark"]),
        "trim_y": craft.timber(trim, "Y", wear=0.5, bare=E["timber_dark"]),
        "trim_z": craft.timber(trim, "Z", wear=0.5, bare=E["timber_dark"]),
        "deck": craft.timber(shade(body, 1.25), "Y", wear=0.6, bare=E["bare"]),
        "door": craft.timber(door or accent or E["plum"], "Z", wear=0.35, bare=E["timber_dark"]),
        "shutter": craft.timber(accent or E["purple"], "Z", wear=0.4),
        "orange": craft.timber(shade(E["orange"], 0.85), "X", wear=0.4, bare=E["timber_dark"]),
        "ink": craft.tinted(E["ink"], 0.5, 0.1), "glass": craft.tinted(pk.H_GLASS, 0.15, 0.1),
        "shingle": craft.tinted(roof, 0.85, 0.25, 5.0), "roofdeck": craft.timber(shade(roof, 0.5), "X", wear=0.0),
        "stone": craft.tinted(E["stone"], 0.9, 0.3, 4.0), "brick": craft.tinted(shade(E["plum"], 0.8), 0.9, 0.3, 9.0),
        "lit": craft.glow(WINDOW_GLOW, 1.0), "paper": craft.paper_white(), "paint": craft.paint(),
    }


def far_wall(b, P, width, height, openings=(), base=True, frieze=True, boards=True):
    """The kit's clapboard wall with broad boards (porch_kit.wall has them 20 cm): x across, front toward -Y,
    z = 0 at the foot. `openings` are (x0, x1, z0, z1)."""
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
    for o in openings:
        if o[2] > 0.05:
            b.box(P["back"], (o[1] - o[0], 0.1, o[2]), ((o[0] + o[1]) / 2, 0.07, o[2] / 2), bevel=0, tint=0.8)
        if height - o[3] > 0.02:
            b.box(P["back"], (o[1] - o[0], 0.1, height - o[3]), ((o[0] + o[1]) / 2, 0.07, (height + o[3]) / 2), bevel=0, tint=0.8)
    if boards:
        n = max(1, round(height / BOARD))
        h = height / n
        for i in range(n):
            for x0, x1 in spans(i * h, (i + 1) * h):
                b.box(P["clap"], (x1 - x0 - 0.004, 0.045, h * 1.07), ((x0 + x1) / 2, 0.003, (i + 0.5) * h), drop=("+y", "+z"),
                      rot=R("X", -6), tint=b.vary(0.78, 1.12))
    if base:
        for x0, x1 in spans(0.0, 0.2):
            b.box(P["trim_x"], (x1 - x0, 0.06, 0.2), ((x0 + x1) / 2, -0.035, 0.1), bevel=0, drop=("+y", "-z"))
    if frieze:
        b.box(P["trim_x"], (width, 0.06, 0.14), (0, -0.035, height - 0.07), bevel=0, drop=("+y",))


def far_gable(b, P, outline, depth=0.1):
    """Gable end: a board cut to `outline` (x, z from the eave line) with clapboards stepped to fit it."""
    b.prism(P["back"], outline, depth, T(0, 0.02, 0), tint=0.8)
    top = max(z for _x, z in outline)
    n = max(1, round(top / BOARD))
    h = top / n

    def reach(z, sign):
        best = None
        for i, (x0, z0) in enumerate(outline):
            x1, z1 = outline[(i + 1) % len(outline)]
            if (z0 - z) * (z1 - z) <= 0 and abs(z1 - z0) > 1e-6:
                x = x0 + (x1 - x0) * (z - z0) / (z1 - z0)
                if best is None or x * sign > best * sign:
                    best = x
        return best
    for i in range(n):
        z = (i + 0.5) * h
        x0, x1 = reach(z, -1), reach(z, 1)
        if x0 is None or x1 - x0 < 0.3:
            continue
        b.box(P["clap"], (x1 - x0, 0.045, h * 1.07), ((x0 + x1) / 2, 0.003, z), drop=("+y", "+z"), rot=R("X", -6),
              tint=b.vary(0.78, 1.12))


def far_window(b, P, cx, z0, w=0.9, h=1.4, lit=False, shutters=True, cutout=None):
    """Sash window standing proud of the wall, in plain boards; `lit` panes are dim amber paper."""
    zc = z0 + h / 2
    b.box(P["lit"] if lit else P["glass"], (w, 0.02, h), (cx, -0.004, zc), bevel=0, tint=1.0, drop=("+y", "+x", "-x", "+z", "-z"))
    for sx in (-1, 1):
        b.box(P["trim_z"], (0.11, 0.09, h + 0.1), (cx + sx * (w / 2 + 0.055), -0.04, zc + 0.05), bevel=0, drop=("+y",))
    b.box(P["trim_x"], (w + 0.4, 0.13, 0.13), (cx, -0.05, z0 + h + 0.115), bevel=0, drop=("+y",))
    b.box(P["trim_x"], (w + 0.36, 0.17, 0.09), (cx, -0.065, z0 - 0.045), bevel=0, drop=("+y",))
    b.box(P["trim_z"], (0.055, 0.04, h), (cx, -0.03, zc), bevel=0, drop=("+y", "+z", "-z"))
    b.box(P["trim_x"], (w, 0.04, 0.06), (cx, -0.032, zc + h * 0.08), bevel=0, drop=("+y", "+x", "-x"))
    if shutters:
        sw = 0.36
        for sx in (-1, 1):
            x = cx + sx * (w / 2 + 0.13 + sw / 2)
            b.box(P["shutter"], (sw, 0.04, h), (x, -0.035, zc), bevel=0, drop=("+y",), tint=b.vary(0.85, 1.1),
                  rot=R("Y", b.rng.uniform(-2.5, 2.5)))
            for z in (z0 + h * 0.2, z0 + h * 0.8):
                b.box(P["shutter"], (sw - 0.04, 0.03, 0.09), (x, -0.065, z), bevel=0, drop=("+y",), tint=0.8)
    if cutout:
        b.paper(P["paper"], cutout, T(cx, -0.05, z0 + 0.03), lc(E["ink"]), fold=0.01, curl=0.02, cuts=0)


def far_door(b, P, cx=0.0, w=1.1, h=2.1):
    b.box(P["door"], (w, 0.06, h), (cx, -0.01, h / 2), bevel=0, tint=0.95, drop=("+y", "-z"))
    for sx in (-1, 1):
        b.box(P["trim_z"], (0.13, 0.1, h + 0.12), (cx + sx * (w / 2 + 0.065), -0.04, h / 2 + 0.06), bevel=0, drop=("+y", "-z"))
        b.box(P["door"], (w * 0.3, 0.03, h * 0.34), (cx + sx * w * 0.21, -0.05, h * 0.27), bevel=0, tint=1.15, drop=("+y",))
    b.box(P["trim_x"], (w + 0.46, 0.14, 0.14), (cx, -0.05, h + 0.13), bevel=0, drop=("+y",))
    b.box(P["orange"], (w * 0.5, 0.03, h * 0.2), (cx, -0.05, h * 0.72), bevel=0, tint=1.0, drop=("+y",))
    b.box(P["paint"], (0.07, 0.07, 0.07), (cx + w * 0.36, -0.07, h * 0.47), bevel=0, tint=lc(E["brass"]))


def far_roof(b, P, width, length, pitch, M, keep=None, fascia=True, tab=TAB, exposure=EXPOSURE):
    """The kit's roof panel with broad shingle tabs: origin at the eave centre, x along the eave, rising
    toward +Y."""
    M = M @ R("X", pitch)
    with b.at(M):
        b.box(P["roofdeck"], (width, length, 0.08), (0, length / 2, -0.04), bevel=0, tint=0.8)
        if fascia:
            b.box(P["trim_x"], (width + 0.02, 0.06, 0.2), (0, -0.02, -0.08), bevel=0)
    arc = pk.ARC
    pk.ARC = 2
    try:
        pk.shingles(b, P["shingle"], width, length, M, tab=tab, exposure=exposure, thick=0.075, keep=keep, palette=ROOF_TINTS,
                    skirt=0.08)
    finally:
        pk.ARC = arc


def gable_roof_y(b, P, half, depth, eave_z, pitch, over=0.45, y0=0.0, barge=True, keep_l=None, keep_r=None):
    """Roof with its ridge along Y over a block x = -half..half, y = y0..y0+depth: two panels, a ridge roll and
    scalloped bargeboards on the front gable. Returns the ridge height."""
    tan = math.tan(math.radians(pitch))
    length = (half + over) / math.cos(math.radians(pitch))
    eave = eave_z - over * tan
    width = depth + 2 * over
    ridge = eave_z + half * tan
    far_roof(b, P, width, length, pitch, T(-half - over, y0 + depth / 2, eave) @ R("Z", -90), keep=keep_l)
    far_roof(b, P, width, length, pitch, T(half + over, y0 + depth / 2, eave) @ R("Z", 90), keep=keep_r)
    b.cyl(P["trim_y"], 0.11, width + 0.08, T(0, y0 + depth / 2, ridge + 0.03) @ R("X", 90), segments=6)
    if barge:
        strip = el.scallop_strip(length + 0.14, 0.2, max(4, round(length / 0.62)), 0.13, 3)
        for sx in (1, -1):
            m = (S(-1, 1, 1) if sx < 0 else T()) @ R("Y", pitch)
            b.prism(P["trim_x"], strip, 0.06, T(0, y0 - over - 0.04, ridge + 0.09) @ m)
        b.box(P["trim_z"], (0.22, 0.1, 0.9), (0, y0 - over - 0.045, ridge - 0.1), bevel=0, tint=1.05)
    return ridge


def chimney(b, P, x, y, z0, z1, size=0.7, lean=2.5, pots=2, course=0.34):
    """Brick stack in chunky courses, a little out of true, with a cap board and orange pots."""
    n = max(1, round((z1 - z0) / course))
    with b.at(T(x, y, z0) @ R("Y", lean)):
        for i in range(n):
            b.box(P["brick"], (size, size, course - 0.008), (b.rng.uniform(-0.02, 0.02), b.rng.uniform(-0.02, 0.02), (i + 0.5) * course),
                  bevel=0, tint=b.vary(0.72, 1.18))
        top = n * course
        b.box(P["trim_x"], (size + 0.18, size + 0.18, 0.13), (0, 0, top + 0.05), bevel=0)
        for k in range(pots):
            dx = (k - (pots - 1) / 2) * size * 0.5
            b.lathe(P["paint"], [(0.1, 0.0), (0.115, 0.2), (0.085, 0.32), (0.07, 0.32)], 6, T(dx, 0, top + 0.11), tint=lc(shade(E["orange"], 0.8)))


def plinth(b, P, x0, x1, y0, y1, h=PLINTH):
    b.box(P["stone"], (x1 - x0 + 0.1, y1 - y0 + 0.1, h), ((x0 + x1) / 2, (y0 + y1) / 2, h / 2), bevel=0, tint=0.75, drop=("-z",))


def corner_boards(b, P, corners, z0, height, w=0.2):
    """Corner boards at (x, y, sx, sy): the outward directions of the corner."""
    for x, y, sx, sy in corners:
        b.box(P["trim_z"], (w, 0.06, height), (x - sx * (w / 2 - 0.035), y + sy * 0.035, z0 + height / 2), bevel=0)
        b.box(P["trim_z"], (0.06, w, height), (x + sx * 0.035, y - sy * (w / 2 - 0.035), z0 + height / 2), bevel=0)


def block(b, P, x0, x1, y0, y1, z0, height, front=(), left=(), right=(), door=None, corners="flfr", back_boards=False):
    """Four clapboard walls around a rectangle. `front`, `left`, `right` list windows as
    (centre along the wall, sill height, w, h, lit, shutters[, cutout]); `door` is the door's x on the front."""
    def face(M, width, wins, has_door=None):
        ops = [(c - w / 2, c + w / 2, z, z + h) for c, z, w, h, *_ in wins]
        if has_door is not None:
            ops.append((has_door - 0.57, has_door + 0.57, 0.0, 2.1))
        with b.at(M):
            far_wall(b, P, width, height, ops)
            for c, z, w, h, lit, shut, *rest in wins:
                far_window(b, P, c, z, w, h, lit, shut, rest[0] if rest else None)
            if has_door is not None:
                far_door(b, P, has_door)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    face(T(cx, y0, z0), x1 - x0, front, None if door is None else door - cx)
    face(T(x0, cy, z0) @ R("Z", -90), y1 - y0, left)
    face(T(x1, cy, z0) @ R("Z", 90), y1 - y0, right)
    with b.at(T(cx, y1, z0) @ R("Z", 180)):
        far_wall(b, P, x1 - x0, height, boards=back_boards, base=False, frieze=False)
    spots = {"fl": (x0, y0, -1, -1), "fr": (x1, y0, 1, -1), "bl": (x0, y1, -1, 1), "br": (x1, y1, 1, 1)}
    corner_boards(b, P, [spots[corners[i:i + 2]] for i in range(0, len(corners), 2)], z0, height)


def jack(b, craft, x, y, z, r, style=0, rz=0.0, rgb=None):
    """A lit jack-o'-lantern big enough to read from across the street."""
    pr.pumpkin(b, craft, r, 0.8, T(x, y, z) @ R("Z", rz), style=style, rgb=rgb, lobes=8, per_lobe=2, glow=1.6)


def far_garland(b, P, p0, p1, sag=0.45, count=5, span=0.62):
    """Oversized paper bats on a sagging cord (the porch garland, cut big for a far wall)."""
    pts = el.catenary(p0, p1, sag, 6)
    for a, c in zip(pts, pts[1:]):
        b.rod(P["paint"], a, c, 0.014, 3, lc(E["bone_dim"]))
    d = Vector(p1) - Vector(p0)
    yaw = math.degrees(math.atan2(d.y, d.x))
    k0 = 1 / 1.24
    for i in range(count):
        t = (i + 0.5) / count
        pos = Vector(p0).lerp(Vector(p1), t) - Vector((0, 0, sag * 4 * t * (1 - t)))
        s = span * b.rng.uniform(0.85, 1.15) * k0
        m = T(*pos) @ R("Z", yaw) @ R("Y", b.rng.uniform(-14, 14)) @ R("X", b.rng.uniform(-12, 12)) @ T(0, -0.03, -span * 0.16)
        b.paper(P["paper"], [(x * s, z * s) for x, z in pr.BAT], m, lc(E["ink"]), fold=0.3, curl=0.25, cuts=0)


def witch_hat(b, P, half, z0, height, tiers=5, flare=0.5, segments=4, turn=45.0):
    """Steep tower roof in overlapping shingled tiers, each a flared skirt, with a ball finial."""
    r0 = (half + flare) * (math.sqrt(2) if segments == 4 else 1.0)
    with b.at(T(0, 0, z0) @ R("Z", turn)):
        for i in range(tiers):
            t0, t1 = i / tiers, (i + 1) / tiers
            ra, rb = r0 * (1 - t0) ** 1.25, r0 * (1 - t1) ** 1.25
            za, zb = height * t0, height * t1
            tint = b.rng.choice(ROOF_TINTS)
            v = b.rng.uniform(0.85, 1.1)
            b.lathe(P["shingle"], [(ra * 0.9, za - 0.02), (ra + 0.07, za - 0.04), (max(rb, 0.05) + 0.02, zb + 0.06 * height / tiers)],
                    segments, tint=tuple(c * v for c in tint))
        b.lathe(P["paint"], [(0.0, height - 0.1), (0.1, height - 0.05), (0.06, height + 0.25), (0.17, height + 0.42), (0.11, height + 0.62),
                             (0.0, height + 0.95)], 6, R("Z", -turn), tint=lc(shade(E["bone_dim"], 0.7)))


def stoop(b, P, cx, y, width=1.7, steps=2, rise=0.2, tread=0.34):
    """Plain plank steps down from a door sill at height steps * rise, front edge toward -Y."""
    for i in range(steps):
        z = rise * (steps - i)
        b.box(P["deck"], (width, tread + 0.04, 0.07), (cx, y - tread * (i + 0.5), z - 0.035), bevel=0, tint=b.vary(0.9, 1.1))
        b.box(P["wall_v"], (width - 0.1, 0.04, z - 0.06), (cx, y - tread * (i + 1) + 0.04, (z - 0.06) / 2), bevel=0, tint=0.8, drop=("-z", "+z", "+y"))


def cat_cutout(h=0.8):
    """A sitting paper cat for a lit window (x, z), feet at z = 0."""
    u = h / 10.0
    pts = [(-2.6, 0), (2.4, 0), (2.9, 0.5), (4.4, 0.9), (4.6, 1.6), (3.0, 1.3), (2.5, 3.6), (1.6, 5.6), (1.9, 7.0), (2.2, 9.9),
           (1.0, 8.6), (-0.4, 8.6), (-1.6, 10.0), (-1.5, 7.2), (-1.9, 5.4), (-2.9, 3.0)]
    return [(x * u, z * u) for x, z in pts]


def ghost_cutout(h=0.8):
    u = h / 10.0
    pts = [(-3.2, 0), (-2.0, 1.0), (-0.9, 0), (0.2, 1.0), (1.3, 0), (2.3, 1.0), (3.2, 0), (3.0, 5.5), (2.2, 8.4), (0.0, 10.0), (-2.2, 8.4),
           (-3.0, 5.5)]
    return [(x * u, z * u) for x, z in pts]


def _tag_windows(b):
    if hasattr(b, "glow_tag"):
        b.glow_tag = "NeighbourWindow"


# --- skyline houses ----------------------------------------------------------------------------------------

def build_neighbour_a(craft):
    """The tower house: two storeys under a steep front gable, a square corner tower with a witch-hat roof, a
    door hood and a tall chimney. 6.5 m wide, 6 m deep, 13 m to the finial."""
    b = Builder(61)
    _tag_windows(b)
    P = far_paints(craft, shade(E["indigo_dark"], 0.92), shade(E["purple"], 0.85), accent=E["plum"])
    half, depth, wall, pitch = 3.0, 6.0, 5.0, 52.0
    eave = PLINTH + wall
    with b.at(T(0.25, -depth / 2, 0)):
        plinth(b, P, -half, half, 0, depth)
        ty0, tx0, tx1 = -0.5, -3.5, -0.9  # the tower stands half a metre proud of the corner
        block(b, P, tx1, half, 0, depth, PLINTH, wall, door=0.25,
              front=((2.0, 0.85, 0.9, 1.4, True, True, ghost_cutout(0.85)), (0.25, 3.0, 0.9, 1.4, False, True), (2.0, 3.0, 0.9, 1.4, False, True)),
              right=((-1.2, 0.85, 0.9, 1.4, False, True), (1.4, 3.0, 0.9, 1.4, False, True)), corners="frbr")
        block(b, P, -half, tx1, 2.1, depth, PLINTH, wall, left=((0.4, 0.85, 0.9, 1.4, False, True), (0.4, 3.0, 0.9, 1.4, False, True)),
              corners="bl")
        b.box(P["back"], (2.1, 0.1, wall), (-1.95, 2.15, PLINTH + wall / 2), bevel=0, tint=0.6)
        with b.at(T(0, 0, eave)):
            rise = half * math.tan(math.radians(pitch))
            far_gable(b, P, [(-half, 0.0), (half, 0.0), (0.0, rise)])
            b.prism(P["back"], [(-half, 0.0), (half, 0.0), (0.0, rise)], 0.1, T(0, depth - 0.12, 0), tint=0.7)
            b.box(P["trim_x"], (2 * half + 0.1, 0.08, 0.18), (0, -0.04, 0.02), bevel=0)
            far_window(b, P, 0.6, 0.75, 0.8, 1.2, False, False)
        ridge = gable_roof_y(b, P, half, depth, eave, pitch)
        # the tower
        th = 7.4
        plinth(b, P, tx0, tx1, ty0, 2.1)
        block(b, P, tx0, tx1, ty0, 2.1, PLINTH, th, corners="flfrblbr",
              front=((0.0, 0.9, 0.8, 1.5, False, False), (0.0, 3.3, 0.8, 1.5, False, True), (0.0, 5.55, 0.9, 1.5, True, False, cat_cutout(0.8))),
              left=((0.0, 3.3, 0.8, 1.5, False, False), (0.0, 5.55, 0.9, 1.5, True, False)),
              right=((0.0, 5.55, 0.9, 1.5, False, False),))
        for z in (PLINTH + 2.7, PLINTH + 5.0):
            b.box(P["trim_x"], (tx1 - tx0 + 0.16, 2.6 + 0.16, 0.14), ((tx0 + tx1) / 2, (ty0 + 2.1) / 2, z), bevel=0, tint=0.95)
        with b.at(T((tx0 + tx1) / 2, (ty0 + 2.1) / 2, 0)):
            b.box(P["trim_x"], (3.0, 3.0, 0.16), (0, 0, PLINTH + th + 0.02), bevel=0)
            witch_hat(b, P, 1.3, PLINTH + th + 0.1, 4.4)
        # door hood, stoop, chimney
        hood = [(-0.95, 0.0), (0.95, 0.0), (0.0, 0.75)]
        b.prism(P["shingle"], hood, 0.95, T(0.25, -0.95, PLINTH + 2.45), tint=(1.1, 0.95, 1.2))
        b.prism(P["trim_x"], [(-1.05, -0.12), (1.05, -0.12), (0.0, 0.72)], 0.07, T(0.25, -1.0, PLINTH + 2.5), tint=0.95)
        for sx in (-1, 1):
            b.box(P["trim_z"], (0.1, 0.1, 1.0), (0.25 + sx * 0.85, -0.5, PLINTH + 2.15), bevel=0, rot=R("X", -42))
        stoop(b, P, 0.25, 0.0)
        chimney(b, P, 2.0, 4.3, eave + 0.8, ridge + 1.5, lean=-2.0)
        # Halloween dressing
        jack(b, craft, 1.45, -0.75, 0, 0.42, 0, 8)
        jack(b, craft, -0.55, -0.85, 0, 0.3, 2, -14, H(0xF59A23))
        far_garland(b, P, (tx1 + 0.1, -0.09, PLINTH + 2.75), (half - 0.05, -0.09, PLINTH + 2.75), 0.5, 5, 0.6)
    return b.finish("NeighbourHouseA")


def build_neighbour_b(craft):
    """The gambrel house: wide and low under a barn roof with two dormers, a long front porch and a big end
    chimney. 9.5 m wide, 7.5 m deep with the porch, 8.5 m to the chimney pots."""
    b = Builder(62)
    _tag_windows(b)
    P = far_paints(craft, shade(E["plum"], 0.62), shade(E["indigo"], 0.9), accent=E["purple"])
    half, depth, wall, over = 4.5, 5.6, 2.8, 0.35
    eave = PLINTH + wall
    p1, l1, p2 = 66.0, 2.5, 27.0
    run1, rise1 = l1 * math.cos(math.radians(p1)), l1 * math.sin(math.radians(p1))
    run2 = depth / 2 + over - run1
    l2, rise2 = run2 / math.cos(math.radians(p2)), run2 * math.tan(math.radians(p2))
    with b.at(T(0, -depth / 2 + 0.9, 0)):
        plinth(b, P, -half, half, 0, depth)
        block(b, P, -half, half, 0, depth, PLINTH, wall, door=0.0, corners="flfrblbr",
              front=((-2.3, 0.8, 1.0, 1.4, True, True, cat_cutout(0.8)), (2.3, 0.8, 1.0, 1.4, False, True)),
              left=((0.0, 0.8, 0.9, 1.4, False, True),), right=((1.5, 0.8, 0.9, 1.4, False, True),))
        gam = [(-depth / 2, 0.0), (depth / 2, 0.0), (depth / 2 + over - run1, rise1 - over * 0.5), (0.0, rise1 + rise2 - over * 0.4),
               (-(depth / 2 + over - run1), rise1 - over * 0.5)]
        for sx, rz in ((-1, -90), (1, 90)):
            with b.at(T(sx * half, depth / 2, eave) @ R("Z", rz)):
                far_gable(b, P, gam)
                b.box(P["trim_x"], (depth + 0.1, 0.08, 0.18), (0, -0.04, 0.02), bevel=0)
                far_window(b, P, 0.0, 0.5, 0.9, 1.3, sx > 0, False)
                for s2 in (-1, 1):  # bargeboards along both slopes
                    with b.at(S(s2, 1, 1)):
                        b.box(P["trim_x"], (l1 + 0.2, 0.07, 0.22), (depth / 2 + over - run1 / 2 + 0.02, -over - 0.02, rise1 / 2 - 0.1), bevel=0,
                              rot=R("Y", p1))
                        b.box(P["trim_x"], (l2 + 0.2, 0.07, 0.22), (run2 / 2, -over - 0.02, rise1 + rise2 / 2 - 0.02), bevel=0, rot=R("Y", p2))
        ez = eave - 0.08
        dormers = (-2.3, 2.3)

        def keep(cx, y0, y1, hw):
            return not any(abs(cx - d) < 0.7 + hw and y0 < 2.0 for d in dormers)
        for sy, rz, kp in ((-1, 0, keep), (1, 180, None)):
            m = T(0, depth / 2 + sy * (depth / 2 + over), ez) @ R("Z", rz)
            far_roof(b, P, 2 * half + 2 * over, l1 + 0.05, p1, m, keep=kp)
            far_roof(b, P, 2 * half + 2 * over, l2, p2, m @ T(0, run1, rise1), fascia=False)
        b.cyl(P["trim_x"], 0.11, 2 * half + 2 * over + 0.08, T(0, depth / 2, ez + rise1 + rise2 + 0.02) @ R("Y", 90), segments=6)
        # dormers on the front slope
        for i, dx in enumerate(dormers):
            dz, dw, dh = eave + 0.25, 1.4, 1.55
            with b.at(T(dx, -0.12, dz)):
                far_wall(b, P, dw, dh, ((-0.4, 0.4, 0.2, 1.3),), base=False, frieze=False)
                far_window(b, P, 0.0, 0.2, 0.8, 1.1, i == 0, False)
                for sx in (-1, 1):
                    b.box(P["clap"], (0.1, 1.1, dh), (sx * (dw / 2 - 0.05), 0.55, dh / 2), bevel=0, tint=0.85)
                    b.box(P["trim_z"], (0.14, 0.07, dh), (sx * (dw / 2 - 0.05), -0.04, dh / 2), bevel=0)
                tri = [(-dw / 2 - 0.15, 0.0), (dw / 2 + 0.15, 0.0), (0.0, 0.8)]
                b.prism(P["clap"], tri, 0.08, T(0, -0.02, dh), tint=0.95)
                b.prism(P["shingle"], [(-dw / 2 - 0.25, -0.06), (dw / 2 + 0.25, -0.06), (0.0, 0.92)], 1.5, T(0, -0.2, dh + 0.04),
                        tint=b.rng.choice(ROOF_TINTS))
                b.prism(P["trim_x"], [(-dw / 2 - 0.3, -0.1), (dw / 2 + 0.3, -0.1), (0.0, 0.98)], 0.06, T(0, -0.27, dh + 0.02), tint=1.0)
        # the porch
        pd, pz = 1.9, PLINTH
        b.box(P["deck"], (6.4, pd, 0.1), (0, -pd / 2, pz - 0.05), bevel=0, tint=0.95)
        b.box(P["wall_v"], (6.3, 0.05, pz - 0.1), (0, -pd + 0.06, (pz - 0.1) / 2), bevel=0, tint=0.75, drop=("-z", "+z", "+y"))
        for x in (-3.05, -1.05, 1.05, 3.05):
            b.box(P["trim_z"], (0.16, 0.16, 2.25), (x, -pd + 0.12, pz + 1.125), bevel=0)
            b.box(P["trim_z"], (0.24, 0.24, 0.3), (x, -pd + 0.12, pz + 0.15), bevel=0)
            for sx in (-1, 1):
                if abs(x + sx * 0.5) < 3.3:
                    b.prism(P["trim_x"], [(0.0, 0.0), (sx * 0.5, 0.0), (0.0, -0.5)][::sx], 0.06, T(x, -pd + 0.09, pz + 2.25))
        b.box(P["trim_x"], (6.5, 0.18, 0.2), (0, -pd + 0.12, pz + 2.35), bevel=0)
        pp = 20.0
        far_roof(b, P, 6.9, (pd + 0.35) / math.cos(math.radians(pp)), pp, T(0, -pd - 0.25, pz + 2.47))
        for sx in (-1, 1):
            b.prism(P["wall_v"], [(0.0, 0.0), (pd + 0.2, 0.0), (pd + 0.2, (pd + 0.2) * math.tan(math.radians(pp)))], 0.08,
                    T(sx * 3.2 + 0.04, -pd - 0.1, pz + 2.45) @ R("Z", 90), tint=0.85)
        stoop(b, P, 0.0, -pd, 1.8)
        for sx in (-1, 1):  # plank rail between the outer posts
            b.box(P["trim_x"], (1.84, 0.07, 0.09), (sx * 2.05, -pd + 0.12, pz + 0.85), bevel=0)
            for k in range(5):
                b.box(P["trim_z"], (0.12, 0.04, 0.72), (sx * (1.37 + 0.34 * k), -pd + 0.12, pz + 0.44), bevel=0, tint=b.vary(0.85, 1.05))
        chimney(b, P, half + 0.42, depth / 2 + 0.6, 0.0, eave + rise1 + rise2 + 1.0, size=0.95, lean=1.2, pots=2)
        chimney(b, P, -2.6, depth / 2 + 0.5, eave + rise1 + 0.6, eave + rise1 + rise2 + 0.9, size=0.6, lean=-3.0, pots=1)
        # Halloween dressing
        jack(b, craft, -1.6, -pd - 0.5, 0, 0.44, 1, 10)
        jack(b, craft, 1.5, -pd + 0.4, pz, 0.3, 0, -12, H(0xF59A23))
        jack(b, craft, 2.25, -pd - 0.45, 0, 0.26, 2, 20, H(0xF0E3C4))
        far_garland(b, P, (-3.0, -pd + 0.0, pz + 2.2), (-1.1, -pd + 0.0, pz + 2.2), 0.4, 3, 0.55)
        far_garland(b, P, (1.1, -pd + 0.0, pz + 2.2), (3.0, -pd + 0.0, pz + 2.2), 0.4, 3, 0.55)
    return b.finish("NeighbourHouseB")


def build_neighbour_c(craft):
    """The crooked house: narrow and tall under a very steep gable, leaning a little, with a jettied bay, a
    lean-to wing, a roof dormer and a kinked chimney. 7 m wide with the wing, 6 m deep, 11.5 m to the pots."""
    b = Builder(63)
    _tag_windows(b)
    P = far_paints(craft, shade(E["twilight"], 0.72), shade(E["plum"], 0.7), accent=shade(E["purple"], 0.9), door=shade(E["orange"], 0.7))
    half, depth, wall, pitch = 2.3, 6.0, 5.7, 63.0
    eave = PLINTH + wall
    lean = Matrix.Identity(4)
    lean[0][2] = 0.035  # the whole house has settled to one side
    with b.at(T(-1.0, -depth / 2, 0) @ lean):
        plinth(b, P, -half, half, 0, depth)
        block(b, P, -half, half, 0, depth, PLINTH, wall, door=-0.9, corners="flfrblbr",
              front=((1.0, 0.85, 0.9, 1.4, False, True), (-0.9, 3.35, 0.8, 1.3, False, True)),
              left=((-1.0, 0.85, 0.9, 1.4, False, True), (1.2, 3.3, 0.9, 1.4, True, True)),
              right=((-1.2, 3.3, 0.9, 1.4, False, True),))
        rise = half * math.tan(math.radians(pitch))
        with b.at(T(0, 0, eave)):
            far_gable(b, P, [(-half, 0.0), (half, 0.0), (0.0, rise)])
            b.prism(P["back"], [(-half, 0.0), (half, 0.0), (0.0, rise)], 0.1, T(0, depth - 0.12, 0), tint=0.7)
            b.box(P["trim_x"], (2 * half + 0.1, 0.08, 0.18), (0, -0.04, 0.02), bevel=0)
            far_window(b, P, 0.0, 0.9, 0.8, 1.3, True, False, ghost_cutout(0.75))

        def keep(cx, y0, y1, hw):  # the dormer sits on the east slope
            return not (abs(cx - 0.6) < 0.75 + hw and 0.6 < y1 and y0 < 2.5)
        gable_roof_y(b, P, half, depth, eave, pitch, over=0.4, keep_r=keep)
        # jettied bay on the first floor
        bw, bd, bz, bh = 2.1, 0.55, PLINTH + 2.9, 2.0
        with b.at(T(1.0, -bd, bz)):
            far_wall(b, P, bw, bh, ((-0.5, 0.5, 0.45, 1.75),), base=False)
            far_window(b, P, 0.0, 0.45, 1.0, 1.3, True, False, cat_cutout(0.7))
            for sx in (-1, 1):
                b.box(P["clap"], (0.1, bd, bh), (sx * (bw / 2 - 0.05), bd / 2, bh / 2), bevel=0, tint=0.85)
                b.box(P["trim_z"], (0.14, 0.07, bh), (sx * (bw / 2 - 0.07), -0.04, bh / 2), bevel=0)
                b.prism(P["trim_x"], [(0.0, 0.0), (bd, 0.0), (0.0, -0.55)], 0.1, T(sx * (bw / 2 - 0.3) + 0.05, bd, 0) @ R("Z", -90))
            b.box(P["trim_x"], (bw + 0.1, bd + 0.1, 0.14), (0, bd / 2 - 0.03, -0.06), bevel=0)
            b.prism(P["shingle"], [(0.0, 0.0), (bd + 0.25, 0.0), (0.0, 0.6)][::-1], bw + 0.3, T(-bw / 2 - 0.15, bd, bh) @ R("Z", -90), tint=(1.1, 0.95, 1.2))
        # lean-to wing on the east side
        wx0, wx1, wy0, wy1, wh = half, half + 2.3, 1.4, 5.2, 2.2
        plinth(b, P, wx0, wx1, wy0, wy1)
        block(b, P, wx0, wx1, wy0, wy1, PLINTH, wh, front=((0.0, 0.7, 0.8, 1.2, False, False),), right=((0.0, 0.7, 0.9, 1.2, False, True),),
              corners="frbr")
        sp = 28.0
        sl = (wx1 - wx0 + 0.35) / math.cos(math.radians(sp))
        far_roof(b, P, wy1 - wy0 + 0.6, sl, sp, T(wx1 + 0.35, (wy0 + wy1) / 2, PLINTH + wh - 0.12) @ R("Z", 90))
        for y in (wy0, wy1 - 0.1):
            b.prism(P["clap"], [(0.0, 0.0), (wx1 - wx0, 0.0), (0.0, (wx1 - wx0) * math.tan(math.radians(sp)))], 0.1, T(wx0, y, PLINTH + wh), tint=0.9)
        # roof dormer on the east slope
        tan = math.tan(math.radians(pitch))
        dy, dzb = 3.0, eave + 1.5
        dxw = half - 1.5 / tan  # where the slope is at the dormer's foot
        with b.at(T(dxw + 0.75, dy, dzb) @ R("Z", 90)):
            far_wall(b, P, 1.3, 1.4, ((-0.38, 0.38, 0.15, 1.15),), base=False, frieze=False)
            far_window(b, P, 0.0, 0.15, 0.76, 1.0, False, False)
            for sx in (-1, 1):
                b.box(P["clap"], (0.1, 0.9, 1.4), (sx * 0.6, 0.45, 0.7), bevel=0, tint=0.85)
            b.prism(P["shingle"], [(-0.9, -0.05), (0.9, -0.05), (0.0, 0.85)], 1.5, T(0, -0.2, 1.4), tint=(0.8, 0.85, 1.2))
            b.prism(P["trim_x"], [(-0.95, -0.1), (0.95, -0.1), (0.0, 0.9)], 0.06, T(0, -0.26, 1.4), tint=1.0)
        # door, stoop, kinked chimney
        stoop(b, P, -0.9, 0.0, 1.5)
        cz = eave + (half - 1.3) * tan
        chimney(b, P, -1.3, 4.2, cz - 0.6, cz + 2.0, size=0.62, lean=-5.0, pots=0)
        top = Vector((-1.3, 4.2, cz - 0.6)) + Vector((math.sin(math.radians(-5.0)), 0, math.cos(math.radians(-5.0)))) * 2.5
        chimney(b, P, top.x, top.y, top.z - 0.1, top.z + 2.1, size=0.56, lean=6.0, pots=2)
        # Halloween dressing
        jack(b, craft, 0.1, -0.8, 0, 0.4, 2, -8)
        jack(b, craft, -2.0, -0.7, 0, 0.28, 0, 15, H(0xF59A23))
        far_garland(b, P, (-half + 0.1, -0.1, PLINTH + 2.75), (-0.1, -0.62, PLINTH + 2.85), 0.45, 3, 0.55)
        far_garland(b, P, (2.1, -0.62, PLINTH + 2.8), (wx1, wy0 - 0.1, PLINTH + 2.3), 0.5, 3, 0.55)
    return b.finish("NeighbourHouseC")
