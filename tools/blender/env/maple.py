"""The autumn maple (art bible §5, spec §4): a leaning trunk with buttress roots and furrowed bark that forks into
four limbs, secondary branches and tapered twigs grown by space colonisation, and layered rosettes of cut-paper
maple leaves (five points, folded on the midrib, curled edges) at the twig tips. No canopy blobs: the crown's
depth comes from a few big dark leaf cards hung inside it, and the sky shows between the lobes.

The tree keeps the first maple's frame: origin at the trunk base, the same lean (the treehouse deck at 2.4 m is
centred on it) and the same footprint and height. The trunk stays a single column up to the fork at 4.2 m, the
limbs leave through the ridge slot of the `TreehouseCabin` roof, and nothing grows or hangs in the cabin, over
its roof or in front of its door (`blocked`).

Everything is deterministic (`grow` is seeded and cached). Leaves are alpha-free cut geometry. The review builder
gets real leaves; the game export bakes the 24 leaf variants of `build_leaf_kit` once and instances them
(`env_export_maple.py`), so the whole crown costs a few hundred texels squared."""
import math
import random

import bmesh
import numpy as np
from mathutils import Matrix, Vector, noise

import env_lib as el
from env_lib import E, H, S, T, blend, lc, lin, shade

Builder = el.Builder
UP = Vector((0.0, 0.0, 1.0))
ZT, FORK_Z, TOP_Z = 2.4, 4.2, 7.65


def lean(z):
    """Trunk centre line (x, y) at height z."""
    return 0.05 * z + 0.015 * z * z, -0.02 * z


PLAT = Vector((*lean(ZT), 0.0))  # treehouse platform and cabin centre
FORK = Vector((*lean(FORK_Z), FORK_Z))
CROWN_AXIS = Vector((0.35, 0.0, 5.3))

# Crown envelope: lobes (centre, radii) that the branches grow into. The gaps between them are the sky gaps.
LOBES = [((-1.95, 0.4, 5.35), (1.35, 1.3, 0.9)), ((2.15, -0.2, 6.0), (1.4, 1.35, 1.0)), ((0.35, 0.9, 6.75), (1.35, 1.3, 0.85)),
         ((-2.45, -0.7, 4.4), (0.9, 0.95, 0.55)), ((2.75, 0.6, 4.5), (0.95, 0.95, 0.55)), ((0.3, -1.15, 5.5), (1.1, 0.85, 0.75)),
         ((0.7, 1.6, 5.2), (0.9, 0.7, 0.55))]
# The four limbs: first node as an offset from the fork (it stays inside the cabin's ridge slot, |y| < 0.15 m
# off the trunk line), then the point the limb heads for above the roof.
LIMBS = [((-0.62, 0.05, 0.62), (-1.45, 0.35, 5.25)), ((0.6, -0.04, 0.62), (1.7, -0.25, 5.35)), ((-0.24, 0.08, 0.78), (0.1, 0.7, 5.95)),
         ((0.3, -0.08, 0.72), (0.6, -0.75, 5.55))]
STEP, REACH, KILL = 0.26, 1.5, 0.37
TIP_R, LIMB_R = 0.014, 0.16
DENSITY = 64
TWIG_R = 0.05


def blocked(p, margin=0.0):
    """Air the tree must leave free: under the crown, the cabin with its roof, and the approach to its door."""
    if p.z < 3.8 - margin or p.z > TOP_Z:
        return True
    dx, dy = p.x - PLAT.x, p.y - PLAT.y
    if abs(dx) < 1.8 - margin and abs(dy) < 1.8 - margin and p.z < 4.8 - margin:
        return True
    return -2.8 < dx < -1.7 and abs(dy) < 1.7 and p.z < 4.45 - margin  # the door is in the cabin's -x wall


def _open(p):
    """The heart of the crown stays bare so the limbs read as they fan out of the trunk."""
    return p.z < 5.45 and math.hypot(p.x - FORK.x, p.y - FORK.y) < 1.35 - 0.5 * max(0.0, p.z - 4.9)


class Tree:
    def __init__(self):
        self.p, self.parent, self.r, self.kids = [], [], [], []
        self.chains = []  # [(node index, ...)] from a fork (or the trunk fork) out to a tip
        self.leaves = []  # (shape, colour, Matrix)


def _attraction(rng):
    pts = []
    for k, (c, rad) in enumerate(LOBES):
        n = round(DENSITY * rad[0] * rad[1] * rad[2] / 0.6) + 8
        for _ in range(n):
            d = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1))).normalized()
            f = rng.uniform(0.6, 1.0) * (1 + 0.28 * noise.noise(d * 1.4 + Vector((k * 3.7, 0, 0))))
            p = Vector((c[0] + d.x * rad[0] * f, c[1] + d.y * rad[1] * f, c[2] + d.z * rad[2] * f))
            if not blocked(p, -0.15):
                pts.append(p)
    return pts


def _colonise(tree, rng):
    """Space colonisation (Runions et al.): every attraction point pulls its nearest branch node, nodes step
    toward the average pull, points are used up when a branch arrives."""
    P = np.array([p[:] for p in _attraction(rng)])
    fixed = len(tree.p)
    frozen = [0] + [i for i in range(fixed) if tree.parent[i] == 0]  # the fork and the limb stubs in the slot
    for _ in range(140):
        if not len(P):
            break
        N = np.array([p[:] for p in tree.p])
        dist = np.linalg.norm(P[:, None, :] - N[None, :, :], axis=2)
        dist[:, frozen] = 1e9
        near, dmin = dist.argmin(axis=1), dist.min(axis=1)
        added = []
        for n in sorted(set(near[dmin < REACH].tolist())):
            pull = P[(near == n) & (dmin < REACH)] - N[n]
            pull = (pull / np.linalg.norm(pull, axis=1)[:, None]).sum(axis=0)
            here = Vector(N[n])
            grown = here - tree.p[tree.parent[n]]
            d = Vector(pull)
            if d.length < 0.25:
                continue
            d = (d.normalized() + grown.normalized() * 0.35 + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * 0.12)
            cand = here + d.normalized() * STEP
            if blocked(cand):
                cand = here + (d.normalized() + UP * 0.9).normalized() * STEP
                if blocked(cand):
                    continue
            if np.linalg.norm(N - np.array(cand[:]), axis=1).min() < STEP * 0.55 or any((cand - a).length < STEP * 0.55 for a, _n in added):
                continue
            added.append((cand, n))
        if not added:
            break
        for cand, n in added:
            tree.p.append(cand)
            tree.parent.append(n)
        A = np.array([c[:] for c, _n in added])
        P = P[np.linalg.norm(P[:, None, :] - A[None, :, :], axis=2).min(axis=1) > KILL]
    return fixed


def _prune(tree, fixed):
    """Drop one-step spurs beside a branch that carries on: they only add burrs to the silhouette."""
    while True:
        kids = [[] for _ in tree.p]
        for i, q in enumerate(tree.parent):
            if q >= 0:
                kids[q].append(i)
        drop = {i for i in range(fixed, len(tree.p))
                if not kids[i] and len(kids[tree.parent[i]]) > 1 and any(kids[k] for k in kids[tree.parent[i]])}
        if not drop:
            tree.kids = kids
            return
        keep = [i for i in range(len(tree.p)) if i not in drop]
        index = {old: new for new, old in enumerate(keep)}
        tree.p = [tree.p[i] for i in keep]
        tree.parent = [index.get(tree.parent[i], -1) for i in keep]


def _shape(tree, fixed, rng):
    kids = tree.kids
    for _ in range(2):  # relax the zigzag of the growth steps
        new = list(tree.p)
        for i in range(fixed, len(tree.p)):
            if kids[i]:
                mid = sum((tree.p[k] for k in kids[i]), Vector()) / len(kids[i])
                new[i] = tree.p[i] * 0.5 + (tree.p[tree.parent[i]] + mid) * 0.25
        tree.p = new
    # pipe model: a branch carries the cross-section of what it feeds, and thickens a little along its length
    r = [TIP_R] * len(tree.p)
    for i in range(len(tree.p) - 1, 0, -1):
        if kids[i]:
            r[i] = sum(r[k] ** 2.7 for k in kids[i]) ** (1 / 2.7) * 1.03 + 0.0012
    top = max(r[k] for k in kids[0])
    tree.r = [TIP_R + (x - TIP_R) * (LIMB_R - TIP_R) / (top - TIP_R) for x in r]
    for k in kids[0]:  # the four limbs leave the fork at a similar girth
        tree.r[k] = max(tree.r[k], LIMB_R * 0.78)
        for g in kids[k]:
            tree.r[g] = max(tree.r[g], LIMB_R * 0.66)


def _simplify(pts, radii, tol):
    """Douglas-Peucker on a branch centre line; a kept node keeps its radius."""
    keep = {0, len(pts) - 1}

    def split(a, b):
        if b - a < 2:
            return
        ab = pts[b] - pts[a]
        worst, at = 0.0, None
        for i in range(a + 1, b):
            d = ((pts[i] - pts[a]).cross(ab)).length / max(ab.length, 1e-6)
            if d > worst:
                worst, at = d, i
        if worst > tol or ab.length > 0.9:
            at = at if worst > tol else (a + b) // 2
            keep.add(at)
            split(a, at)
            split(at, b)
    split(0, len(pts) - 1)
    order = sorted(keep)
    return [pts[i] for i in order], [radii[i] for i in order]


def _chains(tree):
    kids, r = tree.kids, tree.r
    todo = [(0, k) for k in kids[0]]
    while todo:
        start, node = todo.pop()
        chain = [start, node]
        while kids[node]:
            best = max(kids[node], key=lambda k: r[k])
            todo.extend((node, k) for k in kids[node] if k != best)
            node = best
            chain.append(node)
        tree.chains.append(chain)


def _frame(t, f):
    """Leaf frame: local Z (stem to tip) along t, the printed face toward f."""
    f = f - t * f.dot(t)
    if f.length < 0.05:
        f = t.orthogonal()
    f.normalize()
    y = -f
    x = y.cross(t)
    return Matrix((x, y, t)).transposed().to_4x4()


N_SHAPES = 3
COLOURS = [H(0x5C2030), H(0x862A22), H(0xA83A22), H(0xC9442E), H(0xDD6429), H(0xEC8430), H(0xF4A63C), H(0xFBC244)]
DARK, YELLOW = (0, 1), len(COLOURS) - 1


def _foliage(tree, rng):
    """Rosettes of leaves at the twig tips, smaller sprays along the twigs, big dark cards deeper in."""
    kids, r, out = tree.kids, tree.r, tree.leaves

    def tone(p):
        s = (p.z - 4.3) / 2.7 + 0.12 * ((p - CROWN_AXIS).length - 2.0) + rng.gauss(0, 0.17)
        if s > 0.72 and rng.random() < 0.3:
            return YELLOW
        return max(2, min(6, round(2 + s * 4.6)))

    def leaf(c, t, f, size, colour):
        t = t.normalized()
        if blocked(c + t * size, -0.15) or blocked(c + t * size * 0.5, -0.15) or _open(c + t * size * 0.6):
            return
        out.append((rng.randrange(N_SHAPES), colour, T(*(c + t * 0.015)) @ _frame(t, f) @ S(size)))

    def rosette(c, axis, n, size, spread, colour, centre=True):
        u = axis.orthogonal().normalized()
        v = axis.cross(u)
        ph0 = rng.uniform(0, 2 * math.pi)
        for j in range(n):
            ph = ph0 + 2 * math.pi * j / n + rng.uniform(-0.3, 0.3)
            th = math.radians(rng.uniform(*spread))
            t = axis * math.cos(th) + (u * math.cos(ph) + v * math.sin(ph)) * math.sin(th) - UP * rng.uniform(0.45, 0.95)
            k = colour if colour == YELLOW else max(2, min(6, colour + rng.choice((-1, 0, 0, 1))))
            leaf(c, t, axis + UP * 0.2, size * rng.uniform(0.82, 1.18), k)
        if centre:
            t = axis + Vector((rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), rng.uniform(-0.8, -0.3)))
            leaf(c + axis * 0.04, t, axis.cross(UP).cross(axis) + axis * 0.6, size * rng.uniform(0.9, 1.15), colour)

    for i in range(1, len(tree.p)):
        p, q = tree.p[i], tree.p[tree.parent[i]]
        along = (p - q).normalized()
        outward = (p - CROWN_AXIS)
        outward.z *= 0.6
        outward.normalize()
        if not kids[i]:
            axis = (along * 0.55 + outward * 0.6 + UP * 0.12).normalized()
            rosette(p, axis, 5, 0.52, (48, 86), tone(p))
        elif r[i] < 0.03 and rng.random() < 0.5:
            side = along.cross(Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.2, 1)))).normalized()
            axis = (side * 0.7 + outward * 0.55 + UP * 0.1).normalized()
            rosette(p, axis, 2, 0.46, (35, 75), tone(p), centre=False)
        elif 0.034 < r[i] < 0.085 and rng.random() < 0.2:  # depth: dark cards hanging off the secondary branches
            for _ in range(2):
                t = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1.3, -0.3)))
                leaf(p - UP * r[i], t, outward + Vector((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), 0.3)), rng.uniform(0.62, 0.82),
                     rng.choice(DARK))
    for k in range(7):  # fallen leaves round the roots, face up
        a = 0.9 * k + 0.3
        c = Vector((1.3 * math.cos(a) + 0.15 + rng.uniform(-0.1, 0.1), 1.2 * math.sin(a) + rng.uniform(-0.1, 0.1), 0.022 + 0.002 * k))
        t = Vector((math.cos(2.3 * k), math.sin(2.3 * k), 0.0))
        out.append((k % N_SHAPES, (3, 5, 6, 4, 6, 2, 5)[k], T(*c) @ _frame(t, UP + t * 0.06) @ S(0.3)))


_TREE = {}


def grow(seed=4):
    """The maple's skeleton and leaf placements (cached: the game export and the review builder share it)."""
    if seed in _TREE:
        return _TREE[seed]
    rng = random.Random(seed)
    tree = Tree()
    tree.p, tree.parent = [FORK.copy()], [-1]
    for off, aim in LIMBS:
        a, c = FORK + Vector(off), Vector(aim)
        tree.p.append(a)
        tree.parent.append(0)
        side = (c - a).cross(UP).normalized() * rng.uniform(-0.09, 0.09)
        for k in (1, 2, 3):  # the limb arches up and sways on its way out
            t = k / 3
            tree.p.append(a.lerp(c, t) + UP * 0.13 * math.sin(math.pi * t) + side * math.sin(2 * math.pi * t))
            tree.parent.append(len(tree.p) - 2)
    fixed = _colonise(tree, rng)
    _prune(tree, fixed)
    _shape(tree, fixed, rng)
    _chains(tree)
    _foliage(tree, random.Random(seed + 100))
    _TREE[seed] = tree
    return tree


# --- wood --------------------------------------------------------------------------------------------------

def bark(craft, kind="trunk"):
    """Furrowed bark: dark cracks between warm ridges, running up the trunk; rounder on the limbs, whose grain
    cannot follow them in object space. Lichen-pale patches, deep bump."""
    def build():
        mat, nodes, links, bsdf = craft._base(f"Bark_{kind}")
        vec = craft._coords(nodes, links, (7.5, 7.5, 0.8) if kind == "trunk" else (5.0, 5.0, 3.2))
        ridges = craft._noise(nodes, links, vec, 2.0, 5.0, 0.72)
        cut = nodes.new("ShaderNodeMapRange")
        cut.inputs["From Min"].default_value, cut.inputs["From Max"].default_value = 0.4, 0.6
        links.new(ridges, cut.inputs["Value"])
        base = E["bark"]
        colour = craft._mix_colors(nodes, links, cut.outputs["Result"], lin(shade(blend(base, E["timber_dark"], 0.3), 0.42)),
                                   lin(shade(blend(base, E["timber_dark"], 0.6), 0.95)))
        patch = craft._noise(nodes, links, craft._coords(nodes, links, (1.7, 1.7, 1.1)), 1.0, 3.0, 0.7)
        ramp = nodes.new("ShaderNodeMapRange")
        ramp.inputs["From Min"].default_value, ramp.inputs["From Max"].default_value = 0.62, 0.7
        links.new(patch, ramp.inputs["Value"])
        fade = nodes.new("ShaderNodeMath")
        fade.operation = "MULTIPLY"
        fade.inputs[1].default_value = 0.3
        links.new(ramp.outputs["Result"], fade.inputs[0])
        mix = nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        links.new(fade.outputs[0], mix.inputs[0])
        links.new(colour, mix.inputs[6])
        mix.inputs[7].default_value = lin(H(0x6E5A70))
        links.new(craft._tint(nodes, links, mix.outputs[2]), bsdf.inputs["Base Color"])
        links.new(craft._bump(nodes, links, ridges, 1.0, 0.035 if kind == "trunk" else 0.02), bsdf.inputs["Normal"])
        bsdf.inputs["Roughness"].default_value = 0.92
        mat.diffuse_color = lin(base)
        return mat
    return craft._cached(("bark", kind), build)


ROOTS = (0.35, 1.5, 2.85, 4.0, 5.3)  # where the buttress roots leave the trunk (radians)
TRUNK = [(0.62, -0.06), (0.54, 0.1), (0.47, 0.3), (0.395, 0.7), (0.355, 1.25), (0.325, 1.9), (0.31, 2.4), (0.292, 3.0), (0.272, 3.6),
         (0.258, 4.0), (0.262, 4.2), (0.24, 4.28), (0.15, 4.36)]


def _trunk(b, mat):
    segs = 24

    def radial(a, z):
        toe = max(max(0.0, math.cos(a - a0)) ** 8 for a0 in ROOTS)
        flare = max(0.0, 1 - z / 0.95) ** 2
        burl = 0.16 * math.exp(-((math.sin((a - 4.3) / 2) / 0.22) ** 2) - ((z - 1.6) / 0.22) ** 2)
        return 1 + burl + 0.72 * toe * flare - 0.2 * flare + 0.04 * math.cos(9 * a + 0.9 * z) + 0.03 * math.cos(4 * a - 1.3 * z)
    bm = bmesh.new()
    prev = None
    for r, z in TRUNK:
        ox, oy = lean(max(z, 0.0))
        ring = []
        for s in range(segs):
            a = 2 * math.pi * s / segs + 0.12 * z  # the bark twists a little on its way up
            k = r * radial(a, z)
            ring.append(bm.verts.new((ox + k * math.cos(a), oy + k * math.sin(a), z)))
        if prev:
            for s in range(segs):
                bm.faces.new((prev[s], prev[(s + 1) % segs], ring[(s + 1) % segs], ring[s]))
        prev = ring
    ox, oy = lean(FORK_Z)
    cap = bm.verts.new((ox, oy, 4.4))
    for s in range(segs):
        bm.faces.new((prev[s], prev[(s + 1) % segs], cap))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    b.add_bm(bm, mat, tint=lambda c: (0.8 + 0.2 * min(1.0, c.z / 1.2),) * 3)


def _sides(r):
    return 7 if r > 0.1 else 6 if r > 0.06 else 5 if r > 0.034 else 4 if r > 0.02 else 3


def _tube(b, mat, pts, radii, tint):
    """A tapered branch: rings along the centre line (parallel-transported frame), closed to a point."""
    n = _sides(radii[0])
    bm = bmesh.new()
    normal = None
    prev = None
    for i, (p, r) in enumerate(zip(pts, radii)):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        normal = t.orthogonal().normalized() if normal is None else (normal - t * normal.dot(t)).normalized()
        if i == len(pts) - 1:
            tip = bm.verts.new(p + t * 0.03)
            for s in range(n):
                bm.faces.new((prev[s], prev[(s + 1) % n], tip))
            break
        side = t.cross(normal)
        ring = [bm.verts.new(p + (normal * math.cos(2 * math.pi * s / n) + side * math.sin(2 * math.pi * s / n)) * r) for s in range(n)]
        if prev:
            for s in range(n):
                bm.faces.new((prev[s], prev[(s + 1) % n], ring[(s + 1) % n], ring[s]))
        prev = ring
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    b.add_bm(bm, mat, tint=tint)


def add_wood(b, craft, tree=None, part=None):
    """Trunk, limbs and twigs. `part` builds only the "thick" wood (trunk and branches from `TWIG_R` up) or only
    the "thin" twigs, which the game export bakes at a lower texel density."""
    tree = tree or grow()
    if part != "thin":
        _trunk(b, bark(craft, "trunk"))
    limb = bark(craft, "limb")
    rng = random.Random(9)
    for chain in tree.chains:
        pts = [tree.p[i] for i in chain]
        radii = [tree.r[i] for i in chain]
        radii[0] = min(radii[0], radii[1] * (1.4 if chain[0] == 0 else 1.08))  # a branch starts at its own girth, flared
        v = rng.uniform(0.86, 1.08)
        if part and (part == "thin") != (radii[1] < TWIG_R):
            continue
        if chain[0] == 0:
            pts[0] = pts[0] - (pts[1] - pts[0]).normalized() * 0.12  # rooted in the top of the trunk
        pts, radii = _simplify(pts, radii, 0.022 + radii[1] * 0.25)
        _tube(b, limb, pts, radii, (v, v, v))


# --- leaves ------------------------------------------------------------------------------------------------

# Half outline (x, z) of the unit leaf, stem end at the origin, tip at z = 1: lower lobe, notch, side lobe, notch, tip.
LEAF_HALF = [(0.0, 0.0), (0.45, -0.02), (0.27, 0.25), (0.62, 0.5), (0.21, 0.6), (0.0, 1.0)]
LEAF_TRIS = [(0, 1, 2), (2, 3, 4), (0, 2, 4), (0, 4, 5)]
SHAPES = [(0.2, 0.2, -0.1, 0.0), (0.1, 0.32, 0.1, 0.1), (0.28, 0.14, -0.16, -0.08)]  # fold, curl, tip bend, twist
KIT_SIZE, KIT_STEP = 0.5, 1.0


def leaf_bm(shape, size=1.0):
    """One cut-paper maple leaf (8 triangles): folded on the midrib, lobes curling toward the face (-Y)."""
    fold, curl, bend, twist = SHAPES[shape]
    half = 0.62
    bm = bmesh.new()

    def vert(x, z):
        t = abs(x) / half
        y = -fold * abs(x) - curl * half * t ** 3 + bend * z * z + twist * x * z
        return bm.verts.new((x * size, y * size, z * size))
    right = [vert(x, z) for x, z in LEAF_HALF]
    left = [right[0]] + [vert(-x, z) for x, z in LEAF_HALF[1:-1]] + [right[-1]]
    for a, c, d in LEAF_TRIS:
        bm.faces.new((right[a], right[c], right[d]))
        bm.faces.new((left[a], left[d], left[c]))
    return bm


def leaf_paper(craft):
    """Leaf paper: all its colour from the piece tint, blotchy like hand-painted card."""
    return craft.tinted((1.0, 1.0, 1.0), 0.95, 0.3, 11.0, sheen=0.2)


def _leaf_tint(colour, shape):
    """Two-tone cut paper: the lobe tips a shade deeper than the heart of the leaf."""
    rgb = lc(COLOURS[colour])
    k = (1.0, 0.93, 1.06)[shape]

    def tint(c):
        f = k * (0.8 if abs(c.x) > 0.2 * KIT_SIZE and c.z < 0.5 * KIT_SIZE else 0.9 if c.z > 0.62 * KIT_SIZE else 1.06)
        return tuple(min(1.0, v * f) for v in rgb)
    return tint


def add_leaves(b, craft, tree=None):
    """Review builder: every leaf as its own geometry."""
    tree = tree or grow()
    mat = leaf_paper(craft)
    for shape, colour, M in tree.leaves:
        b.add_bm(leaf_bm(shape, KIT_SIZE), mat, M @ S(1 / KIT_SIZE), tint=_leaf_tint(colour, shape), smooth=False)


def build_leaf_kit(craft):
    """Game export: the leaf variants (colour x shape) in a row, `KIT_STEP` apart, to be baked once and instanced."""
    b = Builder(83)
    if hasattr(b, "card"):  # the game builder: paper cards get real back faces and never hide other faces
        b.card = b.sheet = True
    mat = leaf_paper(craft)
    for colour in range(len(COLOURS)):
        for shape in range(N_SHAPES):
            i = colour * N_SHAPES + shape
            b.add_bm(leaf_bm(shape, KIT_SIZE), mat, T(i * KIT_STEP, 0, 1.0), tint=_leaf_tint(colour, shape), smooth=False)
    return b.finish("MapleLeafKit")


def stats(tree=None):
    tree = tree or grow()
    wood = 24 * 2 * (len(TRUNK) - 1) + 24
    for chain in tree.chains:
        pts = [tree.p[i] for i in chain]
        radii = [tree.r[i] for i in chain]
        pts, radii = _simplify(pts, radii, 0.022 + radii[1] * 0.25)
        n = _sides(min(radii[0], radii[1] * (1.4 if chain[0] == 0 else 1.08)))
        wood += n * 2 * (len(pts) - 2) + n
    tips = sum(1 for k in tree.kids if not k)
    return dict(nodes=len(tree.p), chains=len(tree.chains), tips=tips, wood_tris=wood, leaves=len(tree.leaves),
                tris=wood + 16 * len(tree.leaves))
