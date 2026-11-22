"""Shared kit for the Spooky Street environment (spec §4, §6): the night palette, env craft materials with
per-piece paint variation, a mesh `Builder` that batches chunky bevelled timber pieces into one game-ready
mesh per asset, paper cut-outs with curled edges, and the review-sheet stage.

All art is original and procedural. Conventions: metres, Z up, assets face -Y (like the cast)."""
import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars", "env"):
    if os.path.join(TOOLS, sub) not in sys.path:
        sys.path.insert(0, os.path.join(TOOLS, sub))

import craft as craft_lib  # noqa: E402
import costume as co  # noqa: E402
import pip_model as pm  # noqa: E402

H = craft_lib.hex_rgb
lin = craft_lib.lin
V = co.V  # the cast's vivid costume palette (candies, bunting)
E = {  # environment palette (spec §6 light-and-materials row, v1.3 Halloween night families)
    "indigo": H(0x394461), "indigo_dark": H(0x2A3350), "indigo_light": H(0x4C5A85), "twilight": H(0x344767),
    "amber": H(0xF6AD58), "paper": H(0xF4E6C8), "night": H(0x2A2550), "purple": H(0x5B3A7A), "plum": H(0x7A3E6E),
    "orange": H(0xEE7F2D), "green": H(0x8DBF3F), "candy": H(0xF6D365), "bone": H(0xF4E6C8), "ink": H(0x1E1A26),
    "bone_dim": H(0xD9CBB0), "hedge": H(0x2F6B4A), "hedge_light": H(0x5FA052), "bark": H(0x4B3350),
    "timber": H(0xB8773E), "timber_dark": H(0x7A4A2C), "bare": H(0x9A8468), "brass": H(0xD9A53A),
    "stone": H(0x4A4468), "grass": H(0x2B4A45), "street": H(0x2C2748), "glow": H(0xFFD98A), "leaf_a": H(0xE8792B),
    "leaf_b": H(0xC9442E), "leaf_c": H(0xF2B544), "moon": H(0xF3E9C6),
}


def T(x=0.0, y=0.0, z=0.0):
    return Matrix.Translation((x, y, z))


def R(axis, deg):
    return Matrix.Rotation(math.radians(deg), 4, axis)


def S(x, y=None, z=None):
    return Matrix.Diagonal((x, x if y is None else y, x if z is None else z, 1.0))


def shade(rgb, f):
    return tuple(min(1.0, c * f) for c in rgb)


def blend(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


# --- materials ---------------------------------------------------------------------------------------------

class EnvCraft(craft_lib.Craft):
    """Craft materials for set pieces. `timber` and `tinted` multiply by the mesh's per-piece "tint" colour
    attribute (written by Builder), so every plank, shingle and leaf is painted a little differently."""

    def _tint(self, nodes, links, colour):
        attr = nodes.new("ShaderNodeAttribute")
        attr.attribute_name = "tint"
        mul = nodes.new("ShaderNodeMix")
        mul.data_type = "RGBA"
        mul.blend_type = "MULTIPLY"
        mul.inputs[0].default_value = 1.0
        links.new(colour, mul.inputs[6])
        links.new(attr.outputs["Color"], mul.inputs[7])
        return mul.outputs[2]

    def timber(self, rgb, grain="X", wear=0.4, bare=None, rough=0.7):
        """Hand-painted timber: brush streaks along `grain`, chipped paint showing bare wood, grain bump."""
        bare = bare or E["bare"]

        def build():
            mat, nodes, links, bsdf = self._base(f"Timber_{rgb}_{grain}")
            sc = {"X": (1.5, 30.0, 30.0), "Y": (30.0, 1.5, 30.0), "Z": (30.0, 30.0, 1.5)}[grain]
            vec = self._coords(nodes, links, sc)
            strokes = self._noise(nodes, links, vec, 3.0, 3.0, 0.65)
            k = self.k
            colour = self._mix_colors(nodes, links, strokes, lin(shade(rgb, 1 - 0.3 * k)), lin(shade(rgb, 1 + 0.16 * k)))
            height = strokes
            if wear > 0:
                wvec = self._coords(nodes, links, tuple(3.0 if s < 2 else 11.0 for s in sc))
                chips = self._noise(nodes, links, wvec, 1.0, 3.0, 0.75)
                ramp = nodes.new("ShaderNodeMapRange")
                ramp.inputs["From Min"].default_value = 0.71 - 0.08 * wear
                ramp.inputs["From Max"].default_value = 0.725 - 0.08 * wear
                links.new(chips, ramp.inputs["Value"])
                mix = nodes.new("ShaderNodeMix")
                mix.data_type = "RGBA"
                links.new(ramp.outputs["Result"], mix.inputs[0])
                links.new(colour, mix.inputs[6])
                mix.inputs[7].default_value = lin(bare)
                colour = mix.outputs[2]
                sub = nodes.new("ShaderNodeMath")
                sub.operation = "SUBTRACT"
                links.new(strokes, sub.inputs[0])
                links.new(ramp.outputs["Result"], sub.inputs[1])
                height = sub.outputs[0]
            links.new(self._tint(nodes, links, colour), bsdf.inputs["Base Color"])
            links.new(self._bump(nodes, links, height, min(1.0, 0.45 * k), 0.008), bsdf.inputs["Normal"])
            bsdf.inputs["Roughness"].default_value = rough
            mat.diffuse_color = lin(rgb)
            return mat
        return self._cached(("timber", rgb, grain, wear, bare, rough), build)

    def tinted(self, rgb, rough=0.85, mottle=0.2, scale=7.0, emission=0.0, sheen=0.0):
        """Matte painted surface with soft mottling (shingles, leaves, stone, cloth)."""
        def build():
            mat, nodes, links, bsdf = self._base(f"Tinted_{rgb}")
            vec = self._coords(nodes, links)
            blotch = self._noise(nodes, links, vec, scale, 3.0)
            colour = self._mix_colors(nodes, links, blotch, lin(shade(rgb, 1 - mottle)), lin(shade(rgb, 1 + mottle * 0.5)))
            colour = self._tint(nodes, links, colour)
            links.new(colour, bsdf.inputs["Base Color"])
            links.new(self._bump(nodes, links, blotch, 0.25, 0.01), bsdf.inputs["Normal"])
            bsdf.inputs["Roughness"].default_value = rough
            bsdf.inputs["Sheen Weight"].default_value = sheen
            if emission > 0:
                links.new(colour, bsdf.inputs["Emission Color"])
                bsdf.inputs["Emission Strength"].default_value = emission
            mat.diffuse_color = lin(rgb)
            return mat
        return self._cached(("tinted", rgb, rough, mottle, scale, emission, sheen), build)

    def paint(self):
        """White gloss paint that takes its whole colour from the piece tint (candies, toys, small parts)."""
        return self.tinted((1.0, 1.0, 1.0), 0.35, 0.06, 9.0)

    def paper_white(self):
        """White craft paper coloured by the piece tint (bunting, bats, moths)."""
        return self.tinted((1.0, 1.0, 1.0), 0.95, 0.16, 40.0, sheen=0.2)

    def foil(self):
        """Crinkled sweet-wrapper foil coloured by the piece tint."""
        def build():
            mat, nodes, links, bsdf = self._base("Foil")
            vec = self._coords(nodes, links)
            crinkle = self._noise(nodes, links, vec, 260.0, 2.0)
            attr = nodes.new("ShaderNodeAttribute")
            attr.attribute_name = "tint"
            links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
            links.new(self._bump(nodes, links, crinkle, 0.5, 0.002), bsdf.inputs["Normal"])
            bsdf.inputs["Metallic"].default_value = 0.75
            bsdf.inputs["Roughness"].default_value = 0.32
            return mat
        return self._cached(("foil",), build)

    def foliage(self, low, high, z0, z1):
        """Leaf paint that runs from `low` (rust, underneath) to `high` (warm, on top) over object height
        z0..z1, mottled, times the piece tint."""
        def build():
            mat, nodes, links, bsdf = self._base("Foliage")
            coord = nodes.new("ShaderNodeTexCoord")
            split = nodes.new("ShaderNodeSeparateXYZ")
            links.new(coord.outputs["Object"], split.inputs[0])
            fade = nodes.new("ShaderNodeMapRange")
            fade.inputs["From Min"].default_value, fade.inputs["From Max"].default_value = z0, z1
            links.new(split.outputs["Z"], fade.inputs["Value"])
            grad = self._mix_colors(nodes, links, fade.outputs["Result"], lin(low), lin(high))
            blotch = self._noise(nodes, links, self._coords(nodes, links), 5.0, 3.0)
            dull = nodes.new("ShaderNodeMix")
            dull.data_type = "RGBA"
            dull.blend_type = "MULTIPLY"
            dull.inputs[0].default_value = 0.45
            links.new(grad, dull.inputs[6])
            links.new(blotch, dull.inputs[7])
            links.new(self._tint(nodes, links, dull.outputs[2]), bsdf.inputs["Base Color"])
            bsdf.inputs["Roughness"].default_value = 0.9
            bsdf.inputs["Sheen Weight"].default_value = 0.2
            mat.diffuse_color = lin(high)
            return mat
        return self._cached(("foliage", low, high, z0, z1), build)

    def glow(self, rgb, strength=2.0):
        """Lit paper or glass: emission only, so its value is the same from every angle."""
        def build():
            mat, nodes, links, bsdf = self._base(f"Glow_{rgb}_{strength}")
            bsdf.inputs["Base Color"].default_value = (0.0, 0.0, 0.0, 1.0)  # lamps inside must not add to it
            bsdf.inputs["Emission Color"].default_value = lin(rgb)
            bsdf.inputs["Emission Strength"].default_value = strength
            bsdf.inputs["Roughness"].default_value = 0.6
            mat.diffuse_color = lin(rgb)
            return mat
        return self._cached(("glow", rgb, strength), build)

    def striped_cloth(self, a, b, period, axis="X", rough=0.8):
        """Two-colour awning or tablecloth stripes across `axis` (object space, metres), tinted."""
        def build():
            mat, nodes, links, bsdf = self._base(f"Stripe_{a}_{b}")
            coord = nodes.new("ShaderNodeTexCoord")
            split = nodes.new("ShaderNodeSeparateXYZ")
            links.new(coord.outputs["Object"], split.inputs[0])
            wave = nodes.new("ShaderNodeMath")
            wave.operation = "PINGPONG"
            wave.inputs[1].default_value = period / 2
            links.new(split.outputs[axis], wave.inputs[0])
            band = nodes.new("ShaderNodeMath")
            band.operation = "GREATER_THAN"
            band.inputs[1].default_value = period / 4
            links.new(wave.outputs[0], band.inputs[0])
            colour = self._mix_colors(nodes, links, band.outputs[0], lin(a), lin(b))
            weave = self._noise(nodes, links, self._coords(nodes, links), 9.0, 3.0)
            dull = nodes.new("ShaderNodeMix")
            dull.data_type = "RGBA"
            dull.blend_type = "MULTIPLY"
            dull.inputs[0].default_value = 0.35
            links.new(colour, dull.inputs[6])
            links.new(weave, dull.inputs[7])
            links.new(self._tint(nodes, links, dull.outputs[2]), bsdf.inputs["Base Color"])
            bsdf.inputs["Roughness"].default_value = rough
            bsdf.inputs["Sheen Weight"].default_value = 0.3
            mat.diffuse_color = lin(a)
            return mat
        return self._cached(("stripe", a, b, period, axis), build)


# --- mesh builder ------------------------------------------------------------------------------------------

class Builder:
    """Accumulates pieces into one mesh: one draw call per material, real triangle counts, per-piece tint."""

    def __init__(self, seed=0):
        self.verts, self.faces, self.cols, self.mat_ids, self.smooth = [], [], [], [], []
        self.mats = []
        self.rng = random.Random(seed)
        self.stack = [Matrix.Identity(4)]

    class _At:
        def __init__(self, b, m):
            self.b, self.m = b, m

        def __enter__(self):
            self.b.stack.append(self.b.stack[-1] @ self.m)

        def __exit__(self, *exc):
            self.b.stack.pop()

    def at(self, m):
        return Builder._At(self, m)

    def vary(self, lo=0.84, hi=1.1):
        v = self.rng.uniform(lo, hi)
        return (v, v, v)

    def _mat(self, mat):
        for i, m in enumerate(self.mats):
            if m is mat:
                return i
        self.mats.append(mat)
        return len(self.mats) - 1

    def add_bm(self, bm, mat, M=None, tint=None, smooth=True):
        tint = self.vary() if tint is None else ((tint,) * 3 if isinstance(tint, (int, float)) else tint)
        per_face = callable(tint)  # tint(face centre in the piece's local space) -> triple
        M = self.stack[-1] @ (M if M is not None else Matrix.Identity(4))
        flip = M.determinant() < 0
        base = len(self.verts)
        bm.verts.index_update()
        self.verts.extend((M @ v.co)[:] for v in bm.verts)
        mi = self._mat(mat)
        for f in bm.faces:
            idx = [base + v.index for v in f.verts]
            self.faces.append(idx[::-1] if flip else idx)
            self.cols.append(tint(f.calc_center_median()) if per_face else tint)
            self.mat_ids.append(mi)
            self.smooth.append(smooth)
        bm.free()

    def box(self, mat, size, centre=(0, 0, 0), bevel=0.012, rot=None, tint=None, M=None, drop=()):
        """Chamfered timber block. `rot` is a rotation matrix applied about the box centre. `drop` culls
        whole faces that can never be seen, by outward axis: e.g. ("+y", "-z") (unbevelled boxes only)."""
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts:
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        bevel = min(bevel, min(size) * 0.3)
        if drop:
            bevel = 0
            axes = {"x": Vector((1, 0, 0)), "y": Vector((0, 1, 0)), "z": Vector((0, 0, 1))}
            dirs = [axes[d[1]] * (1 if d[0] == "+" else -1) for d in drop]
            bm.normal_update()
            bmesh.ops.delete(bm, geom=[f for f in bm.faces if any(f.normal.dot(d) > 0.9 for d in dirs)], context="FACES")
        if bevel > 0:
            bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bevel, offset_type="OFFSET", segments=1, profile=0.5,
                            affect="EDGES")
        m = T(*centre) @ (rot if rot is not None else Matrix.Identity(4))
        self.add_bm(bm, mat, (M @ m) if M is not None else m, tint)

    def prism(self, mat, outline, depth, M=None, tint=None, bevel=0.0, smooth=True):
        """Board jig-sawn to `outline` (x, z), from y = 0 (front) to y = depth; `bevel` chamfers the front."""
        bm = bmesh.new()
        front = [bm.verts.new((x, 0.0, z)) for x, z in outline]
        back = [bm.verts.new((x, depth, z)) for x, z in outline]
        face = bm.faces.new(front)
        bm.faces.new(back[::-1])
        n = len(outline)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((front[i], back[i], back[j], front[j]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        if bevel > 0:
            bmesh.ops.bevel(bm, geom=list(face.edges), offset=bevel, offset_type="OFFSET", segments=1, profile=0.5,
                            affect="EDGES")
        self.add_bm(bm, mat, M, tint, smooth)

    def cyl(self, mat, r1, depth, M=None, r2=None, segments=12, tint=None, caps=True):
        """Cylinder or cone along local Z, centred."""
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=segments, radius1=r1,
                              radius2=r1 if r2 is None else r2, depth=depth)
        self.add_bm(bm, mat, M, tint)

    def rod(self, mat, a, b, radius, segments=6, tint=None, r2=None):
        a, b = Vector(a), Vector(b)
        d = b - a
        m = T(*((a + b) / 2)) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        self.cyl(mat, radius, d.length, m, r2=r2, segments=segments, tint=tint)

    def lathe(self, mat, profile, segments=16, M=None, tint=None, radial=None):
        """Rings (r, z) around local Z; r = 0 at either end closes with a pole. `radial(a, i)` scales ring i's
        radius at angle a (pumpkin lobes, dents)."""
        bm = bmesh.new()
        rings = []
        for i, (r, z) in enumerate(profile):
            if r <= 1e-6:
                rings.append([bm.verts.new((0, 0, z))])
                continue
            ring = []
            for s in range(segments):
                a = 2 * math.pi * s / segments
                k = radial(a, i) if radial else 1.0
                ring.append(bm.verts.new((r * k * math.cos(a), r * k * math.sin(a), z)))
            rings.append(ring)
        for lo, hi in zip(rings, rings[1:]):
            for s in range(segments):
                t = (s + 1) % segments
                if len(lo) == 1 and len(hi) == 1:
                    continue
                if len(lo) == 1:
                    bm.faces.new((lo[0], hi[s], hi[t]))
                elif len(hi) == 1:
                    bm.faces.new((lo[s], lo[t], hi[0]))
                else:
                    bm.faces.new((lo[s], lo[t], hi[t], hi[s]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        self.add_bm(bm, mat, M, tint)

    def ring(self, mat, r_in, r_out, depth, M=None, a0=0.0, a1=360.0, segs=24, tint=None, chamfer=None):
        """Annular frame in the XZ plane, front at y = 0, back at y = depth, with a chamfered front."""
        c = min(depth, r_out - r_in) * 0.3 if chamfer is None else chamfer
        bm = bmesh.new()
        full = abs(a1 - a0) >= 360.0 - 1e-6
        count = segs if full else segs + 1
        prof = [(r_in, depth), (r_in, c), (r_in + c, 0.0), (r_out - c, 0.0), (r_out, c), (r_out, depth)]
        cols = []
        for i in range(count):
            a = math.radians(a0 + (a1 - a0) * i / segs)
            cols.append([bm.verts.new((r * math.cos(a), y, r * math.sin(a))) for r, y in prof])
        for i in range(segs):
            j = (i + 1) % count
            for k in range(len(prof) - 1):
                bm.faces.new((cols[i][k], cols[i][k + 1], cols[j][k + 1], cols[j][k]))
        if not full:
            bm.faces.new(cols[0])
            bm.faces.new(cols[-1][::-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        self.add_bm(bm, mat, M, tint)

    def disc(self, mat, radius, depth, M=None, segs=24, tint=None, bevel=0.0):
        self.prism(mat, [(radius * math.cos(2 * math.pi * i / segs), radius * math.sin(2 * math.pi * i / segs))
                         for i in range(segs)], depth, M, tint, bevel)

    def paper(self, mat, outline, M=None, tint=None, fold=0.35, curl=0.5, cuts=1, crease=None):
        self.add_bm(paper_bm(outline, fold, curl, cuts, crease), mat, M, tint, smooth=False)

    def poly(self, mat, points, tint=None, M=None, smooth=False):
        """One flat card (leaf, paper scrap); rendered double-sided."""
        bm = bmesh.new()
        bm.faces.new([bm.verts.new(p) for p in points])
        self.add_bm(bm, mat, M, tint, smooth)

    def finish(self, name, sharp=75.0):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.verts, [], self.faces)
        for m in self.mats:
            me.materials.append(m)
        me.polygons.foreach_set("material_index", self.mat_ids)
        me.polygons.foreach_set("use_smooth", self.smooth)
        attr = me.color_attributes.new("tint", "FLOAT_COLOR", "CORNER")
        data = np.ones((len(me.loops), 4), dtype=np.float32)
        totals = np.zeros(len(me.polygons), dtype=np.int32)
        me.polygons.foreach_get("loop_total", totals)
        data[:, :3] = np.repeat(np.array(self.cols, dtype=np.float32).reshape(-1, 3), totals, axis=0)
        attr.data.foreach_set("color", data.ravel())
        me.use_auto_smooth = True
        me.auto_smooth_angle = math.radians(sharp)
        me.update()
        obj = bpy.data.objects.new(name, me)
        pm.link(obj)
        wn = obj.modifiers.new("Normals", "WEIGHTED_NORMAL")
        wn.keep_sharp = True
        wn.mode = "FACE_AREA_WITH_ANGLE"
        wn.weight = 50
        return obj


# --- outlines and cut paper --------------------------------------------------------------------------------

def arc(cx, cz, r, a0, a1, n):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
             cz + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


def arch_outline(w, h, segs=8):
    """Round-topped opening: w wide, h to the spring line, semicircle above; origin at bottom centre."""
    return [(-w / 2, 0.0), (w / 2, 0.0)] + arc(0.0, h, w / 2, 0, 180, segs)


def scallop_strip(length, height, n, drop, segs=5):
    """Board with a straight top edge (z = 0) and `n` scallops along the bottom, from x = 0 to length."""
    pts = [(length, 0.0), (0.0, 0.0), (0.0, -height)]
    w = length / n
    for i in range(n):
        for s in range(1, segs + 1):
            a = math.pi * s / segs
            pts.append((i * w + w / 2 - w / 2 * math.cos(a), -height - drop * math.sin(a)))
    return pts


OWL_HALF = [(0, 27), (9, 28), (21, 44), (25, 24), (29, 2), (24, -20), (13, -33), (0, -37)]  # as Kit's brooch


def owl_outline(height, smooth=0):
    u = height / 81.0
    pts = [(x * u, (z + 37) * u) for x, z in OWL_HALF] + [(-x * u, (z + 37) * u) for x, z in reversed(OWL_HALF[1:-1])]
    return pm.smooth_outline(pts, smooth) if smooth else pts


def paper_bm(outline, fold=0.35, curl=0.5, cuts=1, crease=None):
    """Cut paper as a bmesh: a centre fold and edges that curl toward the viewer (-Y). Outline is (x, z).
    Single-sided cards (rendered double-sided), faceted so the folds read."""
    half = max(abs(x) for x, _ in outline) or 1.0
    bm = bmesh.new()
    bm.faces.new([bm.verts.new((x, 0.0, z)) for x, z in outline])
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    if cuts:
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
    for v in bm.verts:
        t = abs(v.co.x) / half
        v.co.y = -fold * abs(v.co.x) - curl * half * t ** 3 + (crease(v.co.x, v.co.z) if crease else 0.0)
    return bm


def lc(rgb):
    """sRGB triple as a linear tint for the white `paint`/`paper` materials."""
    return lin(rgb)[:3]


def catenary(p0, p1, sag, n):
    p0, p1 = Vector(p0), Vector(p1)
    return [p0.lerp(p1, i / n) - Vector((0, 0, sag * 4 * (i / n) * (1 - i / n))) for i in range(n + 1)]


# --- scene scaffolding -------------------------------------------------------------------------------------

def grouped(name, build):
    """Run build() at the origin and parent everything it made to one movable root. Returns (root, result)."""
    root = bpy.data.objects.new(name, None)  # made first so it, not a mesh inside, gets the plain name
    pm.link(root)
    before = set(bpy.data.objects)
    result = build()
    for obj in set(bpy.data.objects) - before:
        if obj.parent is None and obj is not root:
            obj.parent = root
    return root, result


def top_of(obj):
    while obj.parent is not None:
        obj = obj.parent
    return obj


def solo(names, keep=("Stage",)):
    """Render only the root groups in `names` (plus roots in `keep` and anything ungrouped lights/cameras)."""
    for obj in bpy.data.objects:
        top = top_of(obj)
        if top.type == "EMPTY":
            obj.hide_render = not (top.name in names or top.name in keep)


def triangles(root):
    """Triangle count of everything under `root` as it will be exported (modifiers applied)."""
    dg = bpy.context.evaluated_depsgraph_get()
    total = 0
    for obj in bpy.data.objects:
        if top_of(obj) is not root or obj.type not in ("MESH", "CURVE"):
            continue
        ev = obj.evaluated_get(dg)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        total += len(me.loop_triangles)
        ev.to_mesh_clear()
    return total


def point_light(name, loc, energy, colour, radius=0.05, shadow=True, parent=None):
    data = bpy.data.lights.new(name, "POINT")
    data.energy, data.color, data.shadow_soft_size, data.use_shadow = energy, colour, radius, shadow
    obj = bpy.data.objects.new(name, data)
    obj.location = loc
    pm.link(obj, parent)
    return obj


def sun_light(name, direction, strength, colour, angle=6.0, distance=60.0):
    data = bpy.data.lights.new(name, "SUN")
    data.energy, data.color, data.angle = strength, colour, math.radians(angle)
    data.shadow_cascade_max_distance = distance
    data.shadow_cascade_count = 4
    obj = bpy.data.objects.new(name, data)
    obj.rotation_euler = Vector(direction).to_track_quat("-Z", "Y").to_euler()
    pm.link(obj)
    return obj


def area_light(name, loc, target, energy, colour, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy, data.color, data.size = energy, colour, size
    obj = bpy.data.objects.new(name, data)
    obj.location = loc
    obj.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    pm.link(obj)
    return obj


def setup_render(samples=32, exposure=0.0, bloom=0.06):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    ee = scene.eevee
    ee.taa_render_samples = samples
    ee.use_gtao, ee.gtao_distance, ee.gtao_factor = True, 0.5, 1.2
    ee.use_soft_shadows = True
    ee.shadow_cube_size, ee.shadow_cascade_size = "1024", "2048"
    ee.use_bloom, ee.bloom_intensity, ee.bloom_threshold, ee.bloom_radius = True, bloom, 1.0, 6.0
    scene.view_settings.view_transform = "Standard"  # as the cast: the palette as saturated as in-engine
    scene.view_settings.exposure = exposure
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.clip_end = 400.0
    pm.link(cam)
    scene.camera = cam
    return cam


def night_world(strength=0.6, top=None, horizon=None):
    """Violet night sky: deep at the zenith, haunted purple toward the horizon."""
    world = bpy.data.worlds.new("Night")
    world.use_nodes = True
    nodes, links = world.node_tree.nodes, world.node_tree.links
    bg = nodes["Background"]
    coord = nodes.new("ShaderNodeTexCoord")
    split = nodes.new("ShaderNodeSeparateXYZ")
    links.new(coord.outputs["Generated"], split.inputs[0])
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = lin(horizon or H(0x6A4690))
    ramp.color_ramp.elements[1].position = 0.45
    ramp.color_ramp.elements[1].color = lin(top or H(0x1D1A45))
    links.new(split.outputs["Z"], ramp.inputs[0])
    links.new(ramp.outputs[0], bg.inputs[0])
    bg.inputs[1].default_value = strength
    bpy.context.scene.world = world
    return world


def stage_ground(craft, radius=60.0, rgb=None):
    """Review-sheet floor: a dark painted board, grouped as "Stage" so `solo` keeps it."""
    def build():
        b = Builder()
        b.disc(craft.tinted(rgb or H(0x2A2444), 0.95, 0.12, 0.6), radius, 0.2, R("X", -90) @ T(0, 0, 0), segs=48, tint=1.0)
        b.finish("StageGround")
    return grouped("Stage", build)[0]


def render_view(cam, location, target, lens, width, height, path, ortho=None):
    cam.data.type = "ORTHO" if ortho else "PERSP"
    if ortho:
        cam.data.ortho_scale = ortho
    return co.render_view(cam, location, target, lens, width, height, path)


def pad(tile, width, height, rgb=(0.02, 0.016, 0.05)):
    """Centre a tile on a (height, width) canvas."""
    canvas = np.empty((height, width, 4), dtype=np.float32)
    canvas[:, :, :3] = rgb
    canvas[:, :, 3] = 1.0
    y0, x0 = (height - tile.shape[0]) // 2, (width - tile.shape[1]) // 2
    canvas[y0:y0 + tile.shape[0], x0:x0 + tile.shape[1]] = tile
    return canvas


def args():
    """(output path, quality) from `-- out.png [draft|final]`."""
    rest = sys.argv[sys.argv.index("--") + 1:]
    return rest[0], (rest[1] if len(rest) > 1 else "draft"), rest[2:]
