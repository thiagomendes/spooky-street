"""Dialogue portraits (S004 stream G, plan §2.6): 5 speakers x 5 babble emotions, rendered from the GAME models.

    blender -b -t 1 --python tools/blender/export/portraits.py -- [--cache DIR] [--only Bea,Pip]
                                                                 [--emotions happy,whisper] [--pack-only]
                                                                 [--review DIR] [--no-atlas]

For every speaker the script imports the game's own FBX with its baked textures (the same mesh and maps Unity
loads; Mr. Vale wears his wristwatch because it is in his FBX), poses the first frame of the resting clip, and
renders a bust through one fixed portrait camera and one fixed light rig. The expression is built on the model's
own face: the `<Name>_Face` mesh keeps its eyes, brows and mouth as separate islands of geometry, so the script

  * finds those islands (sclera, iris, pupil, glints, lash, brow, mouth parts) by where they sit and by their colour
    in the face map, and fails loudly if a face does not have what it expects;
  * moves and reshapes them: the gaze, the eye size, the brows (redrawn a little bolder so they read at phone size);
  * adds what the model lacks: eyelids (skin sampled from the face map), a happy closed-eye arc, and a new mouth per
    emotion (colours sampled from the model's own mouth), projected on the skull like the originals;
  * repairs the face map in memory where a moved feature leaves its baked contact shadow behind (never saved).

Nothing is written back to the model. Each render is then cut into a badge: a night-sky paper disc, the bust
clipped to it, the head and hat allowed to pop out above, an ink outline around everything (the puppet look). The
25 badges are packed into `Assets/SpookyStreet/Content/Resources/UI/Portraits/portraits.png`, the rect table is
written to `Core/Nonverbal/PortraitAtlasLayout.cs`, and review sheets go to `docs/evidence/s004/portraits/`.

`--cache` keeps the raw renders (default: a temp folder) so `--pack-only` can redo the badges without rendering.
Run one Blender at a time: the renders use Eevee.
"""
import math
import os
import sys
import tempfile

import numpy as np

try:
    import bmesh
    import bpy
    from mathutils import Euler, Matrix, Vector
    from mathutils.bvhtree import BVHTree
except ImportError:  # --pack-only also runs under plain python3 (Pillow + numpy)
    bpy = None

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOLS))
MODELS = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models", "Characters")
OUT_PNG = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "UI", "Portraits", "portraits.png")
OUT_CS = os.path.join(REPO, "Assets", "SpookyStreet", "Core", "Nonverbal", "PortraitAtlasLayout.cs")
REVIEW = os.path.join(REPO, "docs", "evidence", "s004", "portraits")

# Speaker order and emotion order are the C# enums' (Core/Case/CaseAction.cs Speaker without Caption,
# Core/Audio/VoiceMusicEvents.cs BabbleEmotion): the atlas has one row per speaker and one column per emotion.
SPEAKERS = ("Pip", "Bea", "Vale", "June", "Kit")
EMOTIONS = ("neutral", "happy", "surprised", "thinking", "whisper")

RENDER = 800  # raw render edge; the badge is composed at this size and reduced to CELL
CELL = 400  # one atlas cell
GAP = 8
ATLAS = 2048
# The badge inside a cell, in cell fractions with the origin at the bottom left (Unity's): the sky disc including
# its ink rim. The head may rise above the disc up to the top of the cell.
DISC_CX, DISC_CY, DISC_R = 0.5, 0.43, 0.42

# --- the cast ------------------------------------------------------------------------------------------------------
# rest: the clip whose first frame is the bust's pose. calm: scales the head's nod, turn and lean. frame: the square frame's edge in skull heights; aim: where
# the frame's centre sits, in skull heights from the skull's centre (x to screen right, z up).
CAST = {
    "Pip": dict(rest="Idle", adult=False, frame=2.25, aim=(0.0, 0.04), fangs=True),
    "Bea": dict(rest="Idle", adult=False, frame=2.35, aim=(0.0, 0.10), teeth=True),
    "Vale": dict(rest="SitIdle", adult=True, frame=2.25, aim=(0.0, 0.02)),
    # June: the whole witch hat stays in the cell, and its wide brim swings far, so her head moves half as much
    "June": dict(rest="Idle", adult=False, frame=3.0, aim=(0.0, 0.41), calm=0.5),
    "Kit": dict(rest="Idle", adult=True, frame=2.25, aim=(0.0, 0.04)),
}

# --- the expressions -------------------------------------------------------------------------------------------------
# Lengths are in head radii (r), angles in degrees. Sides: "l" is the character's left, which is screen right.
#   head: (nod forward, turn to screen right, lean to screen right)
#   brow: {side: (raise, tilt)}; tilt > 0 lifts the inner end (worried), < 0 lowers it (firm)
#   eye:  scale, iris (scale), gaze (x, z in eye half-widths), lid: None | ("happy",) for eyes shut in a smile
#   mouth: None keeps the model's own, else (shape, params)
EXPRESSIONS = {
    "neutral": dict(head=(0, 0, 0), brow=dict(l=(0.0, 0), r=(0.0, 0)), eye=dict(), mouth=None),
    "happy": dict(head=(-2, 0, 5), brow=dict(l=(0.07, 4), r=(0.07, 4)), eye=dict(lid=("happy",)),
                  mouth=("open", dict(width=0.30, depth=0.24))),
    "surprised": dict(head=(-5, 0, 0), brow=dict(l=(0.11, 8), r=(0.11, 8)), eye=dict(scale=1.12, iris=0.8),
                      mouth=("oh", dict(rx=0.085, rz=0.115, drop=0.07))),
    "thinking": dict(head=(-3, 9, -6), brow=dict(l=(0.10, 6), r=(-0.035, -14)),
                     eye=dict(gaze=(0.30, 0.34)), mouth=("hm", dict(shift=0.05))),
    # whisper: leaning in, a glance aside, and the left hand cupped by the mouth (Mr. Vale's is the watch hand).
    # hand: where its middle sits from the mouth (r), where the fingers point, where the palm faces (head space).
    "whisper": dict(head=(6, -8, 4), brow=dict(l=(0.05, 12), r=(0.05, 12)),
                    eye=dict(gaze=(-0.40, -0.05)),
                    mouth=("oh", dict(rx=0.06, rz=0.05, drop=0.03, shift=0.03)),
                    hand=dict(at=(0.44, -0.25, -0.22), fingers=(-0.22, -0.05, 1.0), palm=(-0.95, 0.42, 0.0))),
}

INK = (30, 26, 38)


def srgb_to_lin(c):
    c = np.asarray(c, dtype=np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


# ====================================================================================================================
# Blender side
# ====================================================================================================================
def import_model(name):
    """The game FBX with the game's maps on plain Principled materials (as the witness/gesture reviews do)."""
    for sub in ("style", "pip", "chars", "export"):
        path = os.path.join(TOOLS, sub)
        if path not in sys.path:
            sys.path.insert(0, path)
    import witness_review
    return witness_review.import_model(os.path.join(MODELS, name), name)


def flat_material(name, rgb, roughness=0.6):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*srgb_to_lin(rgb), 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


class Face:
    """The face mesh of one imported character, measured in the rig's rest space (metres, -Y forward, Z up)."""

    def __init__(self, model, spec):
        self.name, self.spec = model["name"], spec
        self.rig = model["rig"]
        self.obj = next(o for o in model["meshes"] if o.name.endswith("_Face"))
        self.source = self.obj.data.copy()  # pristine mesh: every expression starts from a copy
        self.source.use_fake_user = True
        mat = self.obj.data.materials[0]
        self.tex = next(n for n in mat.node_tree.nodes if n.type == "TEX_IMAGE").image
        w, h = self.tex.size
        self.pixels = np.array(self.tex.pixels[:], dtype=np.float32).reshape(h, w, 4)
        self.world = self.obj.matrix_world.copy()
        self.extra = []
        hair = next(o for o in model["meshes"] if o.name.endswith("_Hair"))
        hw = hair.matrix_world
        self.hair_tree = BVHTree.FromPolygons([hw @ v.co for v in hair.data.vertices],
                                              [tuple(p.vertices) for p in hair.data.polygons])
        self.measure()

    # --- reading the face ------------------------------------------------------------------------------------------
    def fresh(self):
        """A new copy of the model's own face mesh on the object; removes the last expression's parts."""
        for obj in self.extra:
            bpy.data.objects.remove(obj, do_unlink=True)
        self.extra = []
        old = self.obj.data
        self.obj.data = self.source.copy()
        if old is not self.source:
            bpy.data.meshes.remove(old)
        bm = bmesh.new()
        bm.from_mesh(self.obj.data)
        bm.verts.ensure_lookup_table()
        return bm

    def islands(self, bm):
        seen, out = set(), []
        for v in bm.verts:
            if v.index in seen:
                continue
            stack, comp = [v], []
            seen.add(v.index)
            while stack:
                a = stack.pop()
                comp.append(a)
                for e in a.link_edges:
                    b = e.other_vert(a)
                    if b.index not in seen:
                        seen.add(b.index)
                        stack.append(b)
            out.append(comp)
        return out

    def colour_of(self, bm, comp):
        uv = bm.loops.layers.uv.active
        h, w = self.pixels.shape[:2]
        cols = [self.pixels[min(h - 1, int(l[uv].uv.y * h)), min(w - 1, int(l[uv].uv.x * w)), :3]
                for v in comp[:60] for l in v.link_loops[:1]]
        return np.mean(cols, axis=0)

    def classify(self, bm):
        """{role: [island, ...]} with roles skull, nose, sclera_l/r, iris_*, pupil_*, glint_*, lash_*, brow_*,
        mouth (every mouth part), other. An island is a list of BMVerts; positions are read through self.world."""
        W = self.world
        info = []
        for comp in self.islands(bm):
            pts = np.array([W @ v.co for v in comp])
            info.append(dict(verts=comp, lo=pts.min(0), hi=pts.max(0), c=(pts.min(0) + pts.max(0)) / 2,
                             col=self.colour_of(bm, comp)))
        info.sort(key=lambda i: -len(i["verts"]))
        roles = {"skull": [info[0]]}
        skin = info[0]["col"]
        rest = info[1:]
        scleras = sorted([i for i in rest if len(i["verts"]) >= 80 and i["col"].min() > 0.8], key=lambda i: i["c"][0])
        if len(scleras) != 2:
            raise RuntimeError(f"{self.name}: expected two scleras, found {len(scleras)}")
        eye = {"r": scleras[0], "l": scleras[1]}
        used = {id(s) for s in scleras}
        for side, s in eye.items():
            roles["sclera_" + side] = [s]
            pad = (s["hi"] - s["lo"]) * 0.12
            for i in rest:
                if id(i) in used or not (s["lo"][0] - pad[0] <= i["c"][0] <= s["hi"][0] + pad[0] and
                                         s["lo"][2] - pad[2] <= i["c"][2] <= s["hi"][2] + pad[2]):
                    continue
                size = i["hi"] - i["lo"]
                if i["col"].max() < 0.2:
                    role = "pupil"
                elif i["col"].min() > 0.8:
                    role = "glint"
                elif len(i["verts"]) <= 8 or size[0] > 1.6 * size[2]:
                    role = "lash"
                elif len(i["verts"]) >= 60:
                    role = "iris"
                else:
                    continue  # a freckle by the eye
                roles.setdefault(f"{role}_{side}", []).append(i)
                used.add(id(i))
        eye_top = max(s["hi"][2] for s in scleras)
        eye_out = max(abs(s["lo"][0]) for s in scleras) + 0.02
        for i in rest:
            if id(i) not in used and i["c"][2] > eye_top and abs(i["c"][0]) < eye_out and len(i["verts"]) <= 70:
                roles.setdefault("brow_l" if i["c"][0] > 0 else "brow_r", []).append(i)
                used.add(id(i))
        centre = [i for i in rest if id(i) not in used and abs(i["c"][0]) < 0.004]
        nose = [i for i in centre if np.abs(i["col"] - skin).max() < 0.06 and len(i["verts"]) > 100]
        if len(nose) != 1:
            raise RuntimeError(f"{self.name}: expected one nose, found {len(nose)}")
        roles["nose"] = nose
        used.add(id(nose[0]))
        smile = [i for i in centre if id(i) not in used and i["c"][2] < nose[0]["c"][2] and len(i["verts"]) > 100]
        if len(smile) != 1:
            raise RuntimeError(f"{self.name}: expected one smile line, found {len(smile)}")
        s = smile[0]
        roles["smile"] = smile
        pad = (s["hi"][0] - s["lo"][0]) * 0.03
        for i in rest:
            if id(i) not in used and i["c"][2] < nose[0]["lo"][2] + 0.002 and \
                    s["lo"][0] - pad <= i["lo"][0] and i["hi"][0] <= s["hi"][0] + pad:
                roles.setdefault("mouth", []).append(i)
                used.add(id(i))
        roles["other"] = [i for i in rest if id(i) not in used]
        for need in ("iris", "pupil", "glint", "lash", "brow"):
            for side in "lr":
                if not roles.get(f"{need}_{side}"):
                    raise RuntimeError(f"{self.name}: no {need} on side {side}")
        return roles

    def measure(self):
        bm = self.fresh()
        roles = self.classify(bm)
        skull = roles["skull"][0]
        self.r = (skull["hi"][0] - skull["lo"][0]) / 2 / (0.95 if self.spec["adult"] else 1.0)
        self.skull_h = skull["hi"][2] - skull["lo"][2]
        self.centre = Vector(skull["c"])
        self.front = skull["lo"][1] - 0.3
        self.eyes = {}
        for side in "lr":
            s = roles["sclera_" + side][0]
            self.eyes[side] = dict(c=Vector(s["c"]), ax=(s["hi"][0] - s["lo"][0]) / 2, az=(s["hi"][2] - s["lo"][2]) / 2)
        self.brows = {}
        for side in "lr":
            pts = [i for i in roles["brow_" + side]]
            lo = np.min([i["lo"] for i in pts], axis=0)
            hi = np.max([i["hi"] for i in pts], axis=0)
            self.brows[side] = dict(lo=lo, hi=hi, col=max(pts, key=lambda i: len(i["verts"]))["col"])
        smile = roles["smile"][0]
        self.lip_z = float(smile["c"][2])
        self.line_col = smile["col"]
        mouth = roles["mouth"]
        self.mouth_lo = np.min([i["lo"] for i in mouth], axis=0)
        self.mouth_hi = np.max([i["hi"] for i in mouth], axis=0)
        self.nose_lo = float(roles["nose"][0]["lo"][2])
        # the model's own mouth colours where it has them (Vale and Kit smile with their mouths closed)
        named = {"inside": (0.431, 0.122, 0.2), "tongue": (0.941, 0.447, 0.498)}  # costume.py V["mouth"], V["tongue"]
        for i in mouth:
            if i is smile or len(i["verts"]) <= 8:
                continue
            c = i["col"]
            if c.min() > 0.75:
                continue
            if c[0] > 0.7:
                named["tongue"] = tuple(c)
            elif c[0] < 0.5 and np.abs(c - self.line_col).max() > 0.04:
                named["inside"] = tuple(c)
        self.mouth_cols = named
        self.skull_tree, self.skull_uv = self.tree(bm, roles["skull"][0]["verts"], uvs=True)
        self.skin_cols = {side: self.sample(self.eyes[side]["c"].x, self.eyes[side]["c"].z + self.eyes[side]["az"] * 1.25)
                          for side in "lr"}
        self.maps = self.repaired_maps(roles)
        counts = {k: len(v) for k, v in sorted(roles.items())}
        print(f"[portraits] {self.name}: r {self.r:.4f} lip {self.lip_z:.3f} islands {counts}")
        bm.free()

    def tree(self, bm, verts, uvs=False):
        """A BVH of an island in rest space (triangles), and optionally each triangle's UVs."""
        vset = {v.index for v in verts}
        uv = bm.loops.layers.uv.active
        coords, tris, tri_uv = [], [], []
        index = {}
        for f in bm.faces:
            if f.verts[0].index not in vset:
                continue
            loops = list(f.loops)
            for k in range(1, len(loops) - 1):
                tri = (loops[0], loops[k], loops[k + 1])
                ids = []
                for l in tri:
                    if l.vert.index not in index:
                        index[l.vert.index] = len(coords)
                        coords.append(self.world @ l.vert.co)
                    ids.append(index[l.vert.index])
                tris.append(ids)
                tri_uv.append([l[uv].uv.copy() for l in tri])
        tree = BVHTree.FromPolygons(coords, tris)
        return (tree, (coords, tris, tri_uv)) if uvs else tree

    def hit(self, x, z, trees):
        """First surface a ray from the front meets at (x, z): (point, normal towards the viewer) or None."""
        best = None
        for tree in trees:
            loc, normal, _index, dist = tree.ray_cast(Vector((x, self.front, z)), Vector((0, 1, 0)))
            if loc is not None and (best is None or dist < best[2]):
                best = (loc, normal if normal.y < 0 else -normal, dist)
        return best

    def sample(self, x, z):
        """Face-map colour (sRGB) of the skull at (x, z)."""
        loc, _n, index, _d = self.skull_tree.ray_cast(Vector((x, self.front, z)), Vector((0, 1, 0)))
        if loc is None:
            raise RuntimeError(f"{self.name}: no skull at {x:.3f}, {z:.3f}")
        px = self.texel(loc, index)
        return tuple(self.pixels[px[1], px[0], :3])

    def texel(self, loc, index):
        coords, tris, tri_uv = self.skull_uv
        a, b, c = (coords[i] for i in tris[index])
        v0, v1, v2 = b - a, c - a, loc - a
        d00, d01, d11, d20, d21 = v0.dot(v0), v0.dot(v1), v1.dot(v1), v2.dot(v0), v2.dot(v1)
        den = d00 * d11 - d01 * d01 or 1e-12
        v = (d11 * d20 - d01 * d21) / den
        w = (d00 * d21 - d01 * d20) / den
        uv = tri_uv[index][0] * (1 - v - w) + tri_uv[index][1] * v + tri_uv[index][2] * w
        h, wd = self.pixels.shape[:2]
        return min(wd - 1, max(0, int(uv.x * wd))), min(h - 1, max(0, int(uv.y * h)))

    # --- the face map without the baked contact shadows of features that move ---------------------------------------
    def repaired_maps(self, roles):
        """{"brows": map with the brows' shadows gone, "all": the mouth's gone as well}: the bake left a soft dark
        print of each brow and of the mouth on the skin, which would stay behind when they move."""
        def region(lo, hi, pad, top=None):
            mask = np.zeros(self.pixels.shape[:2], dtype=bool)
            step = 0.0004
            z_hi = hi[2] + pad if top is None else min(hi[2] + pad, top)
            for x in np.arange(lo[0] - pad, hi[0] + pad, step):
                for z in np.arange(lo[2] - pad, z_hi, step):
                    loc, _n, index, _d = self.skull_tree.ray_cast(Vector((x, self.front, z)), Vector((0, 1, 0)))
                    if loc is not None:
                        px = self.texel(loc, index)
                        mask[px[1], px[0]] = True
            for _ in range(3):  # close the gaps between samples and take a small margin
                grown = mask.copy()
                grown[1:] |= mask[:-1]
                grown[:-1] |= mask[1:]
                grown[:, 1:] |= mask[:, :-1]
                grown[:, :-1] |= mask[:, 1:]
                mask = grown
            return mask

        def heal(img, mask):
            ys, xs = np.nonzero(mask)
            if len(ys) == 0:
                return img
            y0, y1, x0, x1 = max(ys.min() - 4, 0), ys.max() + 5, max(xs.min() - 4, 0), xs.max() + 5
            crop, m = img[y0:y1, x0:x1, :3].copy(), mask[y0:y1, x0:x1]
            known = ~m
            # start from the mean of the rim, then relax (membrane interpolation of the surrounding skin)
            crop[m] = crop[known].mean(axis=0)
            for _ in range(600):
                avg = (np.roll(crop, 1, 0) + np.roll(crop, -1, 0) + np.roll(crop, 1, 1) + np.roll(crop, -1, 1)) / 4
                crop[m] = avg[m]
            out = img.copy()
            out[y0:y1, x0:x1, :3] = crop
            return out

        brows = self.pixels
        for side in "lr":
            b = self.brows[side]
            brows = heal(brows, region(b["lo"], b["hi"], 0.006))
        both = heal(brows, region(self.mouth_lo, self.mouth_hi, 0.010, top=self.nose_lo - 0.002))
        return {"brows": brows, "all": both}

    def use_map(self, key):
        self.tex.pixels.foreach_set(self.maps[key].ravel())
        self.tex.update()

    # --- building parts --------------------------------------------------------------------------------------------
    def part(self, name, bm, rgb, roughness=0.6):
        """A new mesh on the head: rest-space geometry, skinned rigidly to the Head bone."""
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = True
        obj = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(obj)
        me.materials.append(flat_material(name, rgb, roughness))
        me.materials[0].use_backface_culling = False
        obj.parent = self.rig
        obj.matrix_world = Matrix.Identity(4)
        group = obj.vertex_groups.new(name="Head")
        group.add(range(len(me.vertices)), 1.0, "REPLACE")
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = self.rig
        self.extra.append(obj)
        return obj

    def strip(self, name, upper, lower, out, rgb, trees=None, rows=5, roughness=0.6):
        """A patch between two polylines of (x, z) with the same count, laid on the face and lifted by `out`."""
        trees = trees or [self.skull_tree]
        bm = bmesh.new()
        grid = []
        for (ux, uz), (lx, lz) in zip(upper, lower):
            col = []
            for k in range(rows + 1):
                t = k / rows
                x, z = ux + (lx - ux) * t, uz + (lz - uz) * t
                h = self.hit(x, z, trees)
                if h is None:
                    col.append(None)
                    continue
                col.append(bm.verts.new(h[0] + h[1] * out))
            grid.append(col)
        for a, b in zip(grid, grid[1:]):
            for k in range(rows):
                quad = (a[k], b[k], b[k + 1], a[k + 1])
                if all(v is not None for v in quad) and len({v.co.to_tuple(6) for v in quad}) >= 3:
                    try:
                        bm.faces.new(quad)
                    except ValueError:
                        pass
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        return self.part(f"{self.name}_X_{name}", bm, rgb, roughness)

    def tube(self, name, points, radius, out, rgb, trees=None, closed=False, taper=(1.0, 1.0), sides=8,
             over_hair=0.0):
        """A round line along (x, z) points on the face (lashes, brows, lip lines). `over_hair`: where a fringe
        hangs between the line and the camera (`self.eye`) closer than this, the line is drawn on the fringe (the
        cartoon convention for brows: an expression must not vanish under a haircut); a hat brim further out
        still hides it."""
        trees = trees or [self.skull_tree]
        centres = []
        for x, z in points:
            h = self.hit(x, z, trees)
            if h is None:
                continue
            c = h[0] + h[1] * out
            if over_hair:  # is there hair between this point and the camera?
                to_eye = (self.eye - c).normalized()
                loc, _n, _i, dist = self.hair_tree.ray_cast(c + to_eye * 0.0005, to_eye, over_hair)
                if loc is not None:
                    c = loc + to_eye * (out + radius * 0.6)
            centres.append(c)
        n = len(centres)
        bm = bmesh.new()
        rings = []
        for i, c in enumerate(centres):
            a = centres[(i - 1) % n] if closed else centres[max(i - 1, 0)]
            b = centres[(i + 1) % n] if closed else centres[min(i + 1, n - 1)]
            tangent = (b - a).normalized()
            side = tangent.cross(Vector((0, -1, 0))).normalized()
            up = side.cross(tangent).normalized()
            t = i / max(n - 1, 1)
            rad = radius * (taper[0] + (taper[1] - taper[0]) * t)
            if not closed:  # rounded ends
                edge = min(i, n - 1 - i)
                rad *= (0.55, 0.85)[edge] if edge < 2 else 1.0
            rings.append([bm.verts.new(c + (side * math.cos(2 * math.pi * k / sides) +
                                             up * math.sin(2 * math.pi * k / sides)) * rad) for k in range(sides)])
        pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
        for a, b in pairs:
            for k in range(sides):
                bm.faces.new((a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]))
        if not closed and rings:
            bm.faces.new(rings[0])
            bm.faces.new(list(reversed(rings[-1])))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return self.part(f"{self.name}_X_{name}", bm, rgb, 0.6)

    # --- one expression --------------------------------------------------------------------------------------------
    def build(self, emotion, eye):
        """`eye`: the camera's place in the head's rest space, for what a fringe would hide."""
        self.eye = eye
        e = EXPRESSIONS[emotion]
        bm = self.fresh()
        roles = self.classify(bm)
        Wi = self.world.inverted()
        r = self.r
        doomed = []

        def kill(*names):
            for n in names:
                for i in roles.get(n, []):
                    doomed.extend(i["verts"])

        def move(islands, fn, trees_before, trees_after):
            """Move island vertices in (x, z) and keep each one's lift over the surface it sat on."""
            for i in islands:
                for v in i["verts"]:
                    p = self.world @ v.co
                    before = self.hit(p.x, p.z, trees_before)
                    q = fn(p)
                    after = self.hit(q.x, q.z, trees_after)
                    if before is not None and after is not None:
                        q.y = after[0].y + (p.y - before[0].y)
                    v.co = Wi @ q

        # brows: redrawn as bolder felt lines at the model's own place and colour, then raised and tilted
        kill("brow_l", "brow_r")
        for side in "lr":
            b = self.brows[side]
            s = 1 if side == "l" else -1
            raise_, tilt = e["brow"][side]
            x_in, x_out = (b["lo"][0], b["hi"][0]) if s > 0 else (b["hi"][0], b["lo"][0])
            zc = (b["lo"][2] + b["hi"][2]) / 2
            cx = (x_in + x_out) / 2
            a = math.radians(tilt)
            pts = []
            for k in range(9):
                t = k / 8
                x = x_in + (x_out - x_in) * t
                z = zc + r * (0.035 * (1 - (2 * t - 1) ** 2) - 0.012 * t)  # a soft arch, the outer end a little lower
                u, v = (x - cx) * s, z - zc  # outward, up
                pts.append((cx + s * (u * math.cos(a) + v * math.sin(a)),
                            zc + v * math.cos(a) - u * math.sin(a) + r * raise_))
            self.tube("Brow_" + side, pts, r * (0.034 if self.spec["adult"] else 0.028), r * 0.006, b["col"],
                      taper=(1.0, 0.7), over_hair=r * 0.3)

        # eyes
        eye = e["eye"]
        lid = eye.get("lid")
        for side in "lr":
            g = self.eyes[side]
            s = 1 if side == "l" else -1
            c, ax, az = g["c"], g["ax"], g["az"]
            sclera = roles["sclera_" + side]
            inner = roles["iris_" + side] + roles["pupil_" + side] + roles["glint_" + side]
            tree0 = self.tree(bm, [v for i in sclera for v in i["verts"]])
            scale = eye.get("scale", 1.0)
            if scale != 1.0:
                for i in sclera + roles["lash_" + side]:
                    for v in i["verts"]:
                        p = self.world @ v.co
                        v.co = Wi @ (c + (p - c) * scale)
            tree1 = self.tree(bm, [v for i in sclera for v in i["verts"]]) if scale != 1.0 else tree0
            k = eye.get("iris", 1.0) * scale
            gx, gz = eye.get("gaze", (0.0, 0.0))
            ic = Vector(np.mean([i["c"] for i in roles["iris_" + side]], axis=0))

            def gaze(p, ic=ic, k=k, gx=gx, gz=gz, ax=ax):
                q = ic + (p - ic) * k
                return Vector((q.x + gx * ax, p.y, q.z + gz * ax))
            if k != 1.0 or gx or gz:
                move(inner, gaze, [tree0], [tree1])
            eye_trees = [self.skull_tree, tree1]
            skin = self.skin_cols[side]
            line = self.line_col
            xs = [c.x + ax * 1.12 * (2 * j / 24 - 1) for j in range(25)]

            def rim(x, sign, c=c, ax=ax, az=az):
                t = max(-1.0, min(1.0, (x - c.x) / (ax * 1.12)))
                return c.z + sign * az * 1.12 * math.sqrt(1 - t * t)
            if lid and lid[0] == "happy":
                kill("iris_" + side, "pupil_" + side, "glint_" + side, "lash_" + side)
                self.strip("Lid_" + side, [(x, rim(x, 1)) for x in xs], [(x, rim(x, -1)) for x in xs], r * 0.010, skin,
                           trees=eye_trees, rows=8)
                arc = [(c.x + ax * 0.92 * math.cos(t), c.z - az * 0.30 + az * 0.62 * math.sin(t))
                       for t in [math.pi * j / 16 for j in range(17)]]
                self.tube("Arc_" + side, arc, r * 0.024, r * 0.016, line, trees=eye_trees)

        # mouth
        if e["mouth"] is not None:
            kill("mouth")
            self.mouth(*e["mouth"])
        self.use_map("all" if e["mouth"] is not None else "brows")
        bmesh.ops.delete(bm, geom=list({v for v in doomed}), context="VERTS")
        bm.to_mesh(self.obj.data)
        bm.free()
        self.obj.data.update()

    def mouth(self, shape, p):
        r, z0 = self.r, self.lip_z
        inside, tongue, line = self.mouth_cols["inside"], self.mouth_cols["tongue"], self.line_col
        white = (0.93, 0.93, 0.94)
        n = 24
        if shape == "open":  # a wide open smile: corners up, deep bowl, tongue; teeth or fangs where the model has them
            W, D = p["width"] * r, p["depth"] * r
            ts = [2 * j / n - 1 for j in range(n + 1)]

            def up(t):
                return z0 + r * (0.045 * t * t - 0.012)

            def lo(t):
                return up(t) - D * (1 - t * t) ** 0.75
            self.strip("Mouth", [(W * t, up(t)) for t in ts], [(W * t, lo(t)) for t in ts], r * 0.004, inside, rows=6)
            tt = [0.7 * t for t in ts]
            self.strip("Tongue", [(W * t, min(up(t), max(lo(t), z0 - D * (0.62 + 0.5 * t * t)))) for t in tt],
                       [(W * t, lo(t)) for t in tt], r * 0.008, tongue, rows=3)
            if self.spec.get("teeth"):
                tt = [0.86 * t for t in ts]
                self.strip("Teeth", [(W * t, up(t)) for t in tt], [(W * t, max(lo(t), up(t) - r * 0.05)) for t in tt],
                           r * 0.010, white, rows=2)
            if self.spec.get("fangs"):
                for s in (1, -1):
                    xs = [s * W * 0.36 + r * 0.032 * (2 * j / 8 - 1) for j in range(9)]
                    self.strip("Fang" + "LR"[s < 0], [(x, up(x / W)) for x in xs],
                               [(x, up(x / W) - r * 0.075 * (1 - abs(2 * j / 8 - 1))) for j, x in enumerate(xs)],
                               r * 0.010, white, rows=2)
            lip = [(W * t, up(t)) for t in ts]
            lip = [(-W - r * 0.035, up(1) + r * 0.035)] + lip + [(W + r * 0.035, up(1) + r * 0.035)]
            self.tube("Lip", lip, r * 0.016, r * 0.012, line)
        elif shape == "oh":  # a round little mouth
            rx, rz = p["rx"] * r, p["rz"] * r
            cx, cz = p.get("shift", 0.0) * r, z0 - p["drop"] * r
            ts = [math.pi * j / n for j in range(n + 1)]
            top = [(cx - rx * math.cos(t), cz + rz * math.sin(t)) for t in ts]
            bottom = [(cx - rx * math.cos(t), cz - rz * math.sin(t)) for t in ts]
            self.strip("Mouth", top, bottom, r * 0.004, inside, rows=6)
            if rz > r * 0.08:
                self.strip("Tongue", [(x, max(z, cz - rz * 0.5)) for x, z in bottom], bottom,
                           r * 0.008, tongue, rows=2)
            ring = [(cx + rx * math.cos(t), cz + rz * math.sin(t)) for t in [2 * math.pi * j / 32 for j in range(32)]]
            self.tube("Lip", ring, r * 0.014, r * 0.010, line, closed=True)
        elif shape == "hm":  # a closed mouth pushed to one side, one corner tucked up
            sx = p["shift"] * r
            pts = [(sx + r * x, z0 + r * z) for x, z in ((-0.13, -0.005), (-0.08, -0.018), (-0.02, -0.022),
                                                         (0.05, -0.016), (0.11, 0.0), (0.15, 0.022), (0.165, 0.045))]
            dense = []
            for a, b in zip(pts, pts[1:]):
                dense += [(a[0] + (b[0] - a[0]) * k / 4, a[1] + (b[1] - a[1]) * k / 4) for k in range(4)]
            self.tube("Lip", dense + [pts[-1]], r * 0.017, r * 0.012, line)
        else:
            raise ValueError(shape)


def bake_pose(rig, action, frame=0):
    """Hold one frame of a clip as a plain pose, so single bones can be turned afterwards."""
    scene = bpy.context.scene
    rig.animation_data_create()
    rig.animation_data.action = action
    scene.frame_set(int(action.frame_range[0]) + frame)
    bpy.context.view_layer.update()
    basis = {pb.name: pb.matrix_basis.copy() for pb in rig.pose.bones}
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.matrix_basis = basis[pb.name]
    bpy.context.view_layer.update()


def set_head(rig, anchor, head):
    """Head upright and facing the viewer at its posed place (`anchor`: the pose bone's matrix), then the
    expression's nod, turn and lean about the neck joint."""
    pb = rig.pose.bones["Head"]
    nod, turn, lean = (math.radians(a) for a in head)
    rot = Euler((nod, lean, turn), "YXZ").to_matrix().to_4x4()
    pb.matrix = Matrix.Translation(anchor.translation) @ rot @ pb.bone.matrix_local.to_3x3().to_4x4()
    bpy.context.view_layer.update()


def hand_frame(model, side="L"):
    """Rest-space measure of a hand from its skinned vertices: wrist, finger direction, palm normal, length."""
    rig = model["rig"]
    names = [n + "_" + side for n in ("Hand", "Fingers", "Thumb", "Index")]
    pts, thumb = [], []
    for obj in model["meshes"]:
        groups = {obj.vertex_groups[n].index: n for n in names if n in obj.vertex_groups}
        if not groups:
            continue
        for v in obj.data.vertices:
            w = {groups[g.group]: g.weight for g in v.groups if g.group in groups}
            if sum(w.values()) > 0.6:
                p = obj.matrix_world @ v.co
                pts.append(p)
                if w.get("Thumb_" + side, 0) > 0.5:
                    thumb.append(p)
    if not pts:
        raise RuntimeError(f"{model['name']}: no hand vertices")
    arr = np.array(pts)
    centre = Vector(arr.mean(0))
    bone = rig.data.bones["Hand_" + side]
    wrist = rig.matrix_world @ bone.head_local
    fingers = (centre - wrist).normalized()
    along = (arr - np.array(wrist)) @ np.array(fingers)
    length = float(np.percentile(along, 98))
    if thumb:
        t = Vector(np.array(thumb).mean(0)) - centre
        t = (t - fingers * t.dot(fingers)).normalized()
        palm = fingers.cross(t) if side == "L" else t.cross(fingers)
    else:  # no thumb bone: the thinnest axis of the hand, facing the body
        _w, vecs = np.linalg.eigh(np.cov((arr - arr.mean(0)).T))
        palm = Vector(vecs[:, 0])
        palm = (palm - fingers * palm.dot(fingers)).normalized()
        if palm.dot(Vector((-1 if side == "L" else 1, 0, -0.3))) < 0:
            palm = -palm
    print(f"[portraits] {model['name']} hand {side}: length {length:.3f} thumb verts {len(thumb)} "
          f"fingers {tuple(round(c, 2) for c in fingers)} palm {tuple(round(c, 2) for c in palm)}")
    return dict(wrist=wrist, fingers=fingers, palm=palm.normalized(), length=length)


def raise_hand(model, hand, to_head, centre, fingers, palm, side="L"):
    """Bring a hand to the face: `centre` (middle of the hand), `fingers` and `palm` directions are given in the
    head's rest space and carried to the posed head by `to_head`. IK for the arm, then the hand turned."""
    rig = model["rig"]
    rot = to_head.to_3x3()
    f = (rot @ Vector(fingers)).normalized()
    n = rot @ Vector(palm)
    n = (n - f * n.dot(f)).normalized()
    target = to_head @ Vector(centre) - f * hand["length"] * 0.5
    empty = bpy.data.objects.new("HandTarget", None)
    bpy.context.scene.collection.objects.link(empty)
    empty.location = target
    lower = rig.pose.bones["LowerArm_" + side]
    chain = [rig.pose.bones[b + "_" + side] for b in ("UpperArm", "LowerArm")]
    ik = lower.constraints.new("IK")
    ik.target, ik.chain_count = empty, len(chain)
    bpy.context.view_layer.update()
    held = [pb.matrix.copy() for pb in chain]
    lower.constraints.remove(ik)
    bpy.data.objects.remove(empty, do_unlink=True)
    for pb, mat in zip(chain, held):
        pb.matrix = mat
        bpy.context.view_layer.update()
    pb = rig.pose.bones["Hand_" + side]

    def basis(a, b):
        return Matrix((a, b, a.cross(b))).transposed()
    f0, n0 = hand["fingers"], hand["palm"]
    n0 = (n0 - f0 * n0.dot(f0)).normalized()
    delta = basis(f, n) @ basis(f0, n0).inverted()  # carries the rest hand's (fingers, palm) frame to the wanted one
    world = rig.matrix_world
    new = (world.inverted().to_3x3() @ delta @ world.to_3x3() @ pb.bone.matrix_local.to_3x3()).to_4x4()
    new.translation = pb.matrix.translation
    pb.matrix = new
    bpy.context.view_layer.update()
    miss = (world @ pb.matrix.translation - target).length
    print(f"[portraits] {model['name']} hand to face: the wrist misses its target by {miss * 100:.1f} cm")


def setup_scene(samples):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = samples
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance = 0.12
    scene.eevee.gtao_factor = 0.8
    scene.eevee.use_soft_shadows = True
    scene.eevee.shadow_cube_size = "2048"
    scene.eevee.shadow_cascade_size = "2048"
    scene.view_settings.view_transform = "Standard"  # the palette stays as saturated as it is in the game
    scene.view_settings.look = "None"
    scene.render.film_transparent = True
    scene.render.resolution_x = scene.render.resolution_y = RENDER
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    world = bpy.data.worlds.new("PortraitSky")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.50, 0.46, 0.80, 1.0)  # violet ambient: shadows go indigo, never black
    bg.inputs[1].default_value = 0.38
    scene.world = world
    cam_data = bpy.data.cameras.new("PortraitCam")
    cam_data.lens = 90
    cam_data.sensor_width = cam_data.sensor_height = 36
    cam_data.sensor_fit = "HORIZONTAL"
    cam = bpy.data.objects.new("PortraitCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    lights = {}
    for name, energy, colour, size in (("Key", 30, (1.0, 0.90, 0.78), 1.0), ("Fill", 12, (0.80, 0.82, 1.0), 1.6),
                                       ("Rim", 70, (0.72, 0.66, 1.0), 0.8), ("Chin", 8, (1.0, 0.86, 0.70), 1.2)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.color, data.size = energy, colour, size
        data.use_contact_shadow = True
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        lights[name] = obj
    return cam, lights


# The one portrait rig, relative to the head's centre in skull heights (x screen right, y towards the viewer is
# negative, z up): the camera level with the face and a touch to the character's right, a warm key from the upper left, a cool fill
# from the right, a violet rim from behind and a low warm bounce under hat brims.
CAM_DIR = (-0.12, -1.0, 0.0)
LIGHTS = {"Key": (-3.2, -5.6, 3.4), "Fill": (4.6, -4.2, 0.6), "Rim": (3.4, 4.6, 3.6), "Chin": (-0.6, -4.6, -2.4)}


def aim_rig(cam, lights, target, skull_h, frame):
    size = skull_h * frame
    dist = size * cam.data.lens / cam.data.sensor_width
    cam.location = target + Vector(CAM_DIR).normalized() * dist
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    for name, obj in lights.items():
        obj.location = target + Vector(LIGHTS[name]) * 0.25
        obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_all(cache, only, emotions, samples):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cam, lights = setup_scene(samples)
    scene = bpy.context.scene
    models = {}
    for name in only:
        models[name] = import_model(name)
        for o in models[name]["all"]:
            o.hide_render = True
    for name in only:
        m, spec = models[name], CAST[name]
        for o in m["all"]:
            o.hide_render = False
        rig = m["rig"]
        face = Face(m, spec)
        bake_pose(rig, m["actions"][spec["rest"]])
        held = {b.name: b.matrix_basis.copy() for b in rig.pose.bones}
        hand = hand_frame(m)
        pb = rig.pose.bones["Head"]
        anchor = pb.matrix.copy()
        set_head(rig, anchor, (0, 0, 0))
        to_posed = rig.matrix_world @ pb.matrix @ pb.bone.matrix_local.inverted()
        centre = to_posed @ face.centre
        target = centre + Vector((spec["aim"][0], 0, spec["aim"][1])) * face.skull_h
        aim_rig(cam, lights, target, face.skull_h, spec["frame"])
        head_parts = [o for o in m["meshes"] if o.name.endswith(("_Face", "_Hair"))]
        for emotion in emotions:
            for b in rig.pose.bones:
                b.matrix_basis = held[b.name]
            bpy.context.view_layer.update()
            set_head(rig, anchor, [a * spec.get("calm", 1.0) for a in EXPRESSIONS[emotion]["head"]])
            to_head = rig.matrix_world @ pb.matrix @ pb.bone.matrix_local.inverted()
            face.build(emotion, to_head.inverted() @ cam.location)
            gesture = EXPRESSIONS[emotion].get("hand")
            if gesture:
                mouth = Vector((0, face.hit(0, face.lip_z, [face.skull_tree])[0].y, face.lip_z))
                raise_hand(m, hand, to_head, mouth + Vector(gesture["at"]) * face.r, gesture["fingers"],
                           gesture["palm"])
            # pass 1: the bust. pass 2: the same with everything but the head held out, so the composer knows
            # which pixels are head and hat (they may rise above the disc; shoulders may not).
            for tag, holdout in (("", False), ("_head", True)):
                for o in m["meshes"]:
                    o.is_holdout = holdout and o not in head_parts
                scene.eevee.taa_render_samples = 8 if holdout else samples
                scene.render.filepath = os.path.join(cache, f"{name.lower()}_{emotion}{tag}.png")
                bpy.ops.render.render(write_still=True)
            for o in m["meshes"]:
                o.is_holdout = False
            print(f"[portraits] rendered {name} {emotion}")
        face.fresh()
        for o in m["all"] + face.extra:
            o.hide_render = True


# ====================================================================================================================
# Pillow side: badges, atlas, C# table, review sheets
# ====================================================================================================================
def _grow(alpha, radius):
    from PIL import Image, ImageFilter
    img = Image.fromarray((np.clip(alpha, 0, 1) * 255).astype(np.uint8))
    return np.asarray(img.filter(ImageFilter.MaxFilter(2 * int(radius) + 1)).filter(ImageFilter.GaussianBlur(1.2)),
                      dtype=np.float32) / 255


def _blur(alpha, radius):
    from PIL import Image, ImageFilter
    img = Image.fromarray((np.clip(alpha, 0, 1) * 255).astype(np.uint8))
    return np.asarray(img.filter(ImageFilter.GaussianBlur(radius)), dtype=np.float32) / 255


def badge(cache, name, emotion):
    """One portrait badge (CELL x CELL RGBA): sky disc, bust, pop-out head, ink outline."""
    from PIL import Image
    S = RENDER
    bust = np.asarray(Image.open(os.path.join(cache, f"{name.lower()}_{emotion}.png")).convert("RGBA"),
                      dtype=np.float32) / 255
    head = np.asarray(Image.open(os.path.join(cache, f"{name.lower()}_{emotion}_head.png")).convert("RGBA"),
                      dtype=np.float32)[..., 3] / 255
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    cx, cy, rad = DISC_CX * S, (1 - DISC_CY) * S, DISC_R * S
    ink_w = 0.0125 * S  # the ink line
    d = np.hypot(xx - cx, yy - cy)
    inner = np.clip(rad - ink_w - d + 0.5, 0, 1)  # the sky disc inside its ink rim
    # the head may leave the disc only above its middle (faded out over a short band, in case hair hangs low)
    above = np.clip((cy - 0.04 * rad - yy) / (0.10 * rad), 0, 1)
    margin = np.clip(np.minimum.reduce([xx, S - 1 - xx, yy]) / ink_w - 1.2, 0, 1)  # keep the outline inside the cell
    keep = bust[..., 3] * np.maximum(inner, head * above * margin)

    # sky paper: violet rising to night, a soft moon glow behind the head, paper grain
    rng = np.random.default_rng(7)
    t = np.clip((yy - (cy - rad)) / (2 * rad), 0, 1)[..., None]
    top, bottom = np.array([42, 37, 80]) / 255, np.array([104, 72, 142]) / 255
    sky = top * (1 - t) + bottom * t
    glow = np.exp(-(((xx - cx) / (rad * 0.80)) ** 2 + ((yy - (cy - rad * 0.12)) / (rad * 0.80)) ** 2))[..., None]
    sky = sky + (np.array([150, 126, 196]) / 255 - sky) * glow * 0.55
    grain = _blur(rng.random((S, S)).astype(np.float32), 1.6)[..., None]
    sky = np.clip(sky * (0.94 + 0.12 * (grain - 0.5) * 4), 0, 1)
    shade = _blur(np.roll(np.roll(keep, int(0.012 * S), 0), int(0.008 * S), 1), 0.012 * S)[..., None]
    sky = sky * (1 - 0.45 * shade * (1 - keep[..., None]))

    ink = np.array(INK) / 255
    line = _grow(keep, ink_w * 0.75)  # ink around the figure
    rim = np.clip(rad - d + 0.5, 0, 1)  # the disc with its rim
    rgb = np.broadcast_to(ink, (S, S, 3)).copy()
    alpha = np.maximum(rim, line)
    disc_fill = inner[..., None] * (1 - np.clip(line - keep, 0, 1)[..., None])
    rgb = rgb * (1 - disc_fill) + sky * disc_fill
    k = keep[..., None]
    rgb = rgb * (1 - k) + bust[..., :3] * k
    out = np.dstack([rgb, alpha])
    img = Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA")
    return img.resize((CELL, CELL), Image.LANCZOS)


def cell_rect(row, col):
    """Pixel rect of a cell, origin bottom-left (Unity texture space); row 0 is the top row of the image."""
    x = GAP + col * (CELL + GAP)
    y_top = GAP + row * (CELL + GAP)
    return x, ATLAS - y_top - CELL, CELL, CELL


def pack(cache, only, emotions):
    from PIL import Image
    atlas = Image.new("RGBA", (ATLAS, ATLAS), (0, 0, 0, 0))
    cells = {}
    for row, name in enumerate(SPEAKERS):
        for col, emotion in enumerate(EMOTIONS):
            if name not in only or emotion not in emotions:
                continue
            img = badge(cache, name, emotion)
            cells[(name, emotion)] = img
            x, y, _w, _h = cell_rect(row, col)
            atlas.paste(img, (x, ATLAS - y - CELL))
    return atlas, cells


def write_cs():
    rows = []
    for row, name in enumerate(SPEAKERS):
        for col, emotion in enumerate(EMOTIONS):
            x, y, w, h = cell_rect(row, col)
            rows.append(f'            new PortraitRect("portrait.{name.lower()}.{emotion}", Speaker.{name}, '
                        f'BabbleEmotion.{emotion.capitalize()}, {x}, {y}, {w}, {h}),')
    text = f"""// <auto-generated>Written by tools/blender/export/portraits.py; re-run the script instead of editing.</auto-generated>
using System;
using System.Collections.Generic;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Core.Nonverbal
{{
    /// <summary>One portrait badge in the baked <c>Resources/UI/Portraits/portraits.png</c>.</summary>
    public readonly struct PortraitRect
    {{
        public PortraitRect(string id, Speaker speaker, BabbleEmotion emotion, int x, int y, int width, int height)
        {{
            Id = id;
            Speaker = speaker;
            Emotion = emotion;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }}

        /// <summary>The sprite name, <c>portrait.&lt;speaker&gt;.&lt;emotion&gt;</c> (e.g. <c>portrait.bea.surprised</c>).</summary>
        public string Id {{ get; }}

        public Speaker Speaker {{ get; }}
        public BabbleEmotion Emotion {{ get; }}

        /// <summary>Pixel rect in the atlas, origin bottom-left (Unity texture space).</summary>
        public int X {{ get; }}
        public int Y {{ get; }}
        public int Width {{ get; }}
        public int Height {{ get; }}
    }}

    /// <summary>
    /// The dialogue portrait atlas (S004 §2.6): every speaking character in every babble emotion, rendered from the
    /// game models by <c>tools/blender/export/portraits.py</c>. A line's portrait is
    /// <c>TryGet(line.Speaker, LineVoices.For(line.Key), out rect)</c>: the emotion is data keyed by the line, so a
    /// translation never changes the face. Each cell is a finished badge: a night-sky disc with an ink rim at
    /// (<see cref="DiscCenterX"/>, <see cref="DiscCenterY"/>) with radius <see cref="DiscRadius"/> (cell fractions,
    /// origin bottom-left), the bust clipped to it, and the head and hat free to rise above it to the top of the cell.
    /// To sit the badge on a round frame, scale the cell so the disc matches the frame and offset it by the centre.
    /// </summary>
    public static class PortraitAtlasLayout
    {{
        public const string ResourcePath = "UI/Portraits/portraits";
        public const string AssetPath = "Assets/SpookyStreet/Content/Resources/UI/Portraits/portraits.png";
        public const int AtlasSize = {ATLAS};
        public const int CellSize = {CELL};

        public const float DiscCenterX = {DISC_CX:.2f}f;
        public const float DiscCenterY = {DISC_CY:.2f}f;
        public const float DiscRadius = {DISC_R:.2f}f;

        /// <summary>The speakers with portraits (captions have no face).</summary>
        public static readonly IReadOnlyList<Speaker> Speakers = new[]
        {{
            {", ".join("Speaker." + s for s in SPEAKERS)},
        }};

        public static readonly IReadOnlyList<BabbleEmotion> Emotions = new[]
        {{
            {", ".join("BabbleEmotion." + e.capitalize() for e in EMOTIONS)},
        }};

        public static readonly IReadOnlyList<PortraitRect> All = new[]
        {{
{chr(10).join(rows)}
        }};

        private static readonly Dictionary<string, PortraitRect> ById = Index();

        /// <summary>The sprite name of a speaker's emotion, or null for a speaker without portraits (captions).</summary>
        public static string SpriteName(Speaker speaker, BabbleEmotion emotion)
        {{
            foreach (var rect in All)
            {{
                if (rect.Speaker == speaker && rect.Emotion == emotion)
                {{
                    return rect.Id;
                }}
            }}

            return null;
        }}

        public static bool TryGet(Speaker speaker, BabbleEmotion emotion, out PortraitRect rect)
        {{
            string id = SpriteName(speaker, emotion);
            if (id == null)
            {{
                rect = default;
                return false;
            }}

            return ById.TryGetValue(id, out rect);
        }}

        public static bool TryGet(string id, out PortraitRect rect)
        {{
            if (id == null)
            {{
                rect = default;
                return false;
            }}

            return ById.TryGetValue(id, out rect);
        }}

        private static Dictionary<string, PortraitRect> Index()
        {{
            var map = new Dictionary<string, PortraitRect>(StringComparer.Ordinal);
            foreach (var rect in All)
            {{
                map[rect.Id] = rect;
            }}

            return map;
        }}
    }}
}}
"""
    with open(OUT_CS, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def review_sheets(cells, out_dir, only, emotions):
    """Contact sheets of the badges as the dialogue shows them, at phone pixels and at twice that."""
    from PIL import Image, ImageDraw, ImageFont
    os.makedirs(out_dir, exist_ok=True)
    ui = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "UI")
    felt = {n: Image.open(os.path.join(ui, f"felt_disc_{c}.png")).convert("RGBA")
            for n, c in (("Pip", "night"), ("Bea", "orange"), ("Vale", "bone"), ("June", "night"), ("Kit", "bone"))}
    ring = Image.open(os.path.join(ui, "stitch_ring.png")).convert("RGBA")
    candy = Image.new("RGBA", ring.size, (246, 211, 101, 255))
    candy.putalpha(ring.getchannel("A"))
    try:
        font_path = next(os.path.join(dp, f) for dp, _d, fs in os.walk(os.path.join(REPO, "Assets")) for f in fs
                         if f.lower().startswith("andika") and f.lower().endswith(".ttf") and "bold" in f.lower())
    except StopIteration:
        font_path = None

    def framed(name, emotion, disc):
        """The badge on the speaker's felt disc with the stitch ring, the disc `disc` pixels across."""
        frame = int(disc * 0.80)  # the badge's disc sits inside the stitches, as the face disc does today
        scale = frame / (2 * DISC_R * CELL)
        size = max(1, round(CELL * scale))
        tile = Image.new("RGBA", (int(disc * 1.3), int(disc * 1.42)), (0, 0, 0, 0))
        ox, oy = (tile.width - disc) // 2, tile.height - disc - int(disc * 0.06)
        base = felt[name].resize((disc, disc), Image.LANCZOS)
        if name == "Kit":  # plum tint, as DialogueView.ApplyPortrait
            r, g, b, a = base.split()
            base = Image.merge("RGBA", (r.point(lambda v: int(v * 0.55)), g.point(lambda v: int(v * 0.32)),
                                        b.point(lambda v: int(v * 0.6)), a))
        tile.alpha_composite(base, (ox, oy))
        tile.alpha_composite(candy.resize((disc, disc), Image.LANCZOS), (ox, oy))
        b = cells[(name, emotion)].resize((size, size), Image.LANCZOS)
        bx = ox + disc / 2 - DISC_CX * size
        by = oy + disc / 2 - (1 - DISC_CY) * size
        tile.alpha_composite(b, (round(bx), round(by)))
        return tile

    def sheet(disc, path, title):
        names = [n for n in SPEAKERS if n in only]
        emos = [e for e in EMOTIONS if e in emotions]
        tile_w, tile_h = int(disc * 1.3), int(disc * 1.42)
        left, top = int(max(70, disc * 0.55)), int(max(52, disc * 0.42))
        img = Image.new("RGBA", (left + tile_w * len(emos) + 12, top + tile_h * len(names) + 12), (58, 50, 96, 255))
        draw = ImageDraw.Draw(img)
        size = max(11, int(disc * 0.12))
        font = ImageFont.truetype(font_path, size) if font_path else ImageFont.load_default()
        draw.text((8, 6), title, fill=(244, 230, 200, 255), font=font)
        for c, emotion in enumerate(emos):
            draw.text((left + c * tile_w + tile_w / 2, top - size * 0.2), emotion, fill=(246, 211, 101, 255), font=font,
                      anchor="mb")
        for r_, name in enumerate(names):
            draw.text((8, top + r_ * tile_h + tile_h / 2), name, fill=(244, 230, 200, 255), font=font, anchor="lm")
            for c, emotion in enumerate(emos):
                img.alpha_composite(framed(name, emotion, disc), (left + c * tile_w, top + r_ * tile_h))
        img.convert("RGB").save(path, quality=92)
        print(f"[portraits] wrote {os.path.relpath(path, REPO)} {img.size}")

    # iPhone landscape at 3x is 1170 px tall for the 750-unit reference: the bubble's face disc is 150 units and the
    # card's is 188 units.
    px = 1170 / 750
    sheet(round(150 * px), os.path.join(out_dir, "portraits_phone_1x.png"),
          "Dialogue portraits at phone pixels: the bubble's face disc (150 ref units = 234 px on an iPhone at 3x)")
    sheet(round(150 * px * 2), os.path.join(out_dir, "portraits_phone_2x.jpg"), "Dialogue portraits at 2x phone pixels")
    sheet(96, os.path.join(out_dir, "portraits_96px.png"), "Legibility check: 96 px discs")

    # the raw atlas cells on a checker, to judge edges and the pop-out
    names = [n for n in SPEAKERS if n in only]
    emos = [e for e in EMOTIONS if e in emotions]
    img = Image.new("RGBA", (CELL * len(emos), CELL * len(names)), (120, 120, 130, 255))
    chk = np.indices((img.height, img.width)).sum(0) // 20 % 2
    img = Image.fromarray(np.dstack([np.where(chk, 150, 110).astype(np.uint8)] * 3 + [np.full(chk.shape, 255, np.uint8)]),
                          "RGBA")
    for r_, name in enumerate(names):
        for c, emotion in enumerate(emos):
            img.alpha_composite(cells[(name, emotion)], (c * CELL, r_ * CELL))
    img = img.resize((img.width // 2, img.height // 2), Image.LANCZOS)
    img.convert("RGB").save(os.path.join(out_dir, "portraits_cells.png"))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]

    def opt(name, default):
        return argv[argv.index(name) + 1] if name in argv else default
    global RENDER
    RENDER = int(opt("--size", RENDER))
    cache = opt("--cache", os.path.join(tempfile.gettempdir(), "spooky_portraits"))
    os.makedirs(cache, exist_ok=True)
    only = [n for n in SPEAKERS if n in opt("--only", ",".join(SPEAKERS)).split(",")]
    emotions = [e for e in EMOTIONS if e in opt("--emotions", ",".join(EMOTIONS)).split(",")]
    if "--pack-only" not in argv:
        if bpy is None:
            raise SystemExit("rendering needs Blender: blender -b --python tools/blender/export/portraits.py")
        render_all(cache, only, emotions, int(opt("--samples", "64")))
    atlas, cells = pack(cache, only, emotions)
    review_sheets(cells, opt("--review", REVIEW), only, emotions)
    complete = len(cells) == len(SPEAKERS) * len(EMOTIONS)
    if complete and "--no-atlas" not in argv:
        os.makedirs(os.path.dirname(OUT_PNG), exist_ok=True)
        atlas.save(OUT_PNG, optimize=True)
        write_cs()
        print(f"[portraits] wrote {os.path.relpath(OUT_PNG, REPO)} and {os.path.relpath(OUT_CS, REPO)}")
    elif not complete:
        print("[portraits] partial run: review sheets only, the atlas and the C# table are untouched")


if __name__ == "__main__":
    main()
