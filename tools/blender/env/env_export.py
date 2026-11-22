"""Game export of the approved environment corner (spec §4, §6): the owl house, its Halloween dressing, the
hero props and the porch kit modules as FBX files plus baked texture atlases that `ModelKit` can load.

The approved builders (`owl_house.py`, `props.py`, `porch_kit.py`) are reused unchanged. This script swaps their
`Builder` for `GameBuilder`, which drops trim that cannot be seen at phone distance (tiny pieces, hairline
chamfers), tags faces for the separately animated children (`OwlPuppet`, `WindWheel`, `FrontDoor`, `Gate`) and
the emissive `*_Glow` objects, and marks paper cards so they get real back faces (URP Lit culls back faces).

Pipeline: build -> triangulate + weighted normals -> smart UV project -> pack every model of an atlas into one
UV square -> Cycles bake of the procedural craft materials (base colour, emission, tangent normal) -> rebuild
each part as a plain one-material mesh with its pivot -> one FBX per model.

Textures (next to the FBX files):
  OwlHouse_BaseColor/_Normal/_Emission.png  the house alone (ModelKit's per-model convention, no loader change)
  Env_BaseColor/_Normal/_Emission.png       shared atlas for the dressing, every prop and every kit module

Run (one Blender at a time): blender -b -t 2 --python tools/blender/env/env_export.py -- [out_dir] [options]
  count        only build and print triangle counts
  size=2048    atlas size (use 512 for a quick draft)
  samples=4    Cycles bake samples
  only=A,B     build a subset (the atlas then holds only those models: drafts only)
"""
import json
import math
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import tessellate_polygon

import env_lib as el
import owl_house as oh
import porch_kit as pk
import props as pr
from env_lib import E, R, T, H

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
OUT_DIR = os.path.join(REPO, "Assets", "SpookyStreet", "Content", "Resources", "Models", "Environment")
GLOW_SUFFIX = "_Glow"
SKIP = object()
HOUSE_ORIGIN = Vector((0.0, 2.0, 0.0))  # base centre of body + porch (builder layout: body y 0..6, porch y -2..0)


def log(*a):
    print("[env_export]", *a, flush=True)


# --- game builder ------------------------------------------------------------------------------------------

class Ctx:
    """Settings and collected geometry for the model(s) being built."""

    def __init__(self, model, min_size=0.0, min_bevel=0.0, classify=None, glow=None, per_finish=False, skip_finish=(),
                 ground=True, behind=None, sharp=75.0):
        self.model, self.min_size, self.min_bevel, self.classify = model, min_size, min_bevel, classify
        self.sharp = sharp  # auto-smooth angle (the review builders use 75)
        self.ground, self.behind = ground, behind  # hidden-face culling: stands on the ground / extra occluder model
        self.glow = glow or model
        self.per_finish, self.skip_finish = per_finish, set(skip_finish)
        self.builders = {}  # model name -> [GameBuilder]
        self.seen = set()


CTX = None


class GameCraft(el.EnvCraft):
    def glow(self, rgb, strength=2.0):
        mat = super().glow(rgb, strength)
        mat["glow"] = float(strength)
        return mat


def glow_strength(mat):
    """Emission strength of a craft material (0 for ordinary paint)."""
    if "glow" in mat.keys():
        return float(mat["glow"])
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    return float(bsdf.inputs["Emission Strength"].default_value) if bsdf and bsdf.inputs["Emission Color"].is_linked else 0.0


class GameBuilder(el.Builder):
    """`env_lib.Builder` that records, per face, which exported object it belongs to and whether it is a paper
    card. It consumes random numbers exactly like the review builder, so layouts match the approved sheets."""

    def __init__(self, seed=0):
        super().__init__(seed)
        self.parts, self.cards, self.sheets = [], [], []
        self.part = None  # child object the current pieces belong to (None = the model's main mesh)
        self.part_fn = None  # optional: world points -> part name / None / SKIP
        self.glow_tag = None
        self.card, self.solid = False, False
        self.sheet = False  # the current pieces are thin paper (cards and `solid` paper alike)

    def box(self, mat, size, centre=(0, 0, 0), bevel=0.012, rot=None, tint=None, M=None, drop=()):
        if bevel < CTX.min_bevel:
            bevel = 0
        super().box(mat, size, centre, bevel, rot, tint, M, drop)

    def prism(self, mat, outline, depth, M=None, tint=None, bevel=0.0, smooth=True):
        if bevel < CTX.min_bevel:
            bevel = 0.0
        super().prism(mat, outline, depth, M, tint, bevel, smooth)

    def paper(self, *a, **k):
        prev, self.card, self.sheet = (self.card, self.sheet), not self.solid, True
        super().paper(*a, **k)
        self.card, self.sheet = prev

    def poly(self, *a, **k):
        prev, self.card, self.sheet = (self.card, self.sheet), not self.solid, True
        super().poly(*a, **k)
        self.card, self.sheet = prev

    def add_bm(self, bm, mat, M=None, tint=None, smooth=True):
        tint = self.vary() if tint is None else tint  # same rng draw as the review builder, even if skipped
        Mw = self.stack[-1] @ (M if M is not None else Matrix.Identity(4))
        pts = [Mw @ v.co for v in bm.verts]
        lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        part = self.part
        if self.part_fn:
            got = self.part_fn(pts)
            part = got if got is not None else part
        if CTX.classify and part is not SKIP:
            got = CTX.classify(self, mat, lo, hi, pts)
            part = got if got is not None else part
        strength = glow_strength(mat)
        key = (mat.name, len(pts), tuple(round(c, 3) for c in (*lo, *hi)))
        if part is SKIP or (max(hi - lo) < CTX.min_size and strength < 1.0) or key in CTX.seen:
            bm.free()
            return
        CTX.seen.add(key)
        if strength >= 1.0:
            part = (self.glow_tag or CTX.glow) + GLOW_SUFFIX
        n0 = len(self.faces)
        super().add_bm(bm, mat, M, tint, smooth)
        n = len(self.faces) - n0
        self.parts.extend([part] * n)
        self.cards.extend([self.card] * n)
        self.sheets.extend([self.sheet] * n)

    def finish(self, name, sharp=75.0):
        if name not in CTX.skip_finish:
            CTX.builders.setdefault(name if CTX.per_finish else CTX.model, []).append(self)
        holder = bpy.data.objects.new("_built_" + name, None)  # the review builders expect an object back
        el.pm.link(holder)
        return holder


def _tagging(module, fname, **attrs):
    """Wrap `module.fname(b, ...)` so the builder carries `attrs` while it runs."""
    inner = getattr(module, fname)

    def wrapped(b, *a, **k):
        if not isinstance(b, GameBuilder):
            return inner(b, *a, **k)
        prev = {key: getattr(b, key) for key in attrs}
        for key, val in attrs.items():
            setattr(b, key, val)
        try:
            return inner(b, *a, **k)
        finally:
            for key, val in prev.items():
                setattr(b, key, val)
    setattr(module, fname, wrapped)


def _door_game(inner):
    """The door leaf (with everything mounted on it) becomes `FrontDoor`; the frame stays on the wall, and a
    dark recess behind the opening keeps the house from being see-through when the door swings."""
    def door(b, P, width=1.1, height=2.02):
        inv, hw = b.stack[-1].inverted(), width / 2

        def part(pts):
            loc = [inv @ p for p in pts]
            frame = (max(abs(p.x) for p in loc) > hw + 0.005 or min(p.z for p in loc) >= height - 1e-3
                     or max(p.z for p in loc) < 0.05)
            return None if frame else "FrontDoor"
        prev, b.part_fn = b.part_fn, part
        inner(b, P, width, height)
        b.part_fn = prev
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts:
            v.co = Vector((v.co.x * (width + 0.04), v.co.y * 1.2 + 0.66, (v.co.z + 0.5) * (height + 0.02)))
        bm.normal_update()
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.y < -0.9], context="FACES")
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        b.add_bm(bm, P["ink"], None, 0.6, smooth=False)
    return door


def _wheel_game(inner):
    """Hub, paddles, spokes and rim become `WindWheel` (spins about its local Y axle); mast and vane stay."""
    def wind_wheel(b, P, craft, radius=0.72):
        inv = b.stack[-1].inverted()

        def part(pts):
            loc = [inv @ p for p in pts]
            cy = (min(p.y for p in loc) + max(p.y for p in loc)) / 2
            reach = max(math.hypot(p.x, p.z) for p in loc)
            return "WindWheel" if cy < 0.06 and reach < radius + 0.02 else None
        prev, b.part_fn = b.part_fn, part
        inner(b, P, craft, radius)
        b.part_fn = prev
    return wind_wheel


GAME_LEAF = [(0.0, -0.55), (0.05, -0.2), (0.42, -0.3), (0.6, 0.1), (0.37, 0.19), (0.44, 0.47), (0.0, 0.8)]  # crown leaf
CROWN_CARDS, CROWN_LEAF_SCALE = 68, 1.25  # the review crown has 96 smaller leaves per clump
HEAP_SCALE = 1.0


def install_hooks():
    """Point the approved builders at the game builder. Nothing in the env scripts is edited."""
    for mod in (oh, pr, pk):
        mod.Builder = GameBuilder
    pk.door = _door_game(pk.door)
    oh.wind_wheel = _wheel_game(oh.wind_wheel)
    _tagging(oh, "owl_puppet", part="OwlPuppet")
    _tagging(pk, "round_window", glow_tag="AtticWindow")
    _tagging(pk, "arch_window", glow_tag="SideWindow")
    _tagging(pr, "hanging_lamp", glow_tag="Lamp")
    _tagging(pr, "_carve", glow_tag="Face", solid=True)  # carved faces lie on the shell: no back faces needed
    pr.MAPLE_MID = GAME_LEAF
    leaf, foliage, heap = pr.leaf, pr.foliage, pr.candy_heap

    def game_leaf(b, craft, M, size, rgb, curl=0.25, low=False, mat=None, tint=None, fold=0.25):
        leaf(b, craft, M, size * (CROWN_LEAF_SCALE if mat is not None else 1.0), rgb, curl, low, mat, tint, fold)

    def game_foliage(b, craft, centre, radii, seed, cards=96):
        foliage(b, craft, centre, radii, seed, CROWN_CARDS)

    def game_heap(b, craft, radius, height, count, s=1.0, lollies=2):
        heap(b, craft, radius, height, count, s * HEAP_SCALE, lollies)
    pr.leaf, pr.foliage, pr.candy_heap = game_leaf, game_foliage, game_heap
    _tagging(pr, "foliage", solid=True)  # crown leaves lie on the clumps facing out: single-sided


# --- models ------------------------------------------------------------------------------------------------

class Model:
    def __init__(self, name, atlas, build, weight=1.0, origin=(0, 0, 0), pivots=None, **ctx):
        self.name, self.atlas, self.build, self.weight = name, atlas, build, weight
        self.origin = Vector(origin)
        self.pivots = pivots or {}  # part name -> Matrix (builder space) of the child's pivot frame
        self.ctx = ctx
        self.parts = {}  # part name -> working object (builder space, identity transform)
        self.tris = {}


def _house(craft):
    oh.build_house(craft, dress=False, lights=False)


def _dressing(craft):
    pr.DETAIL = 0.75
    oh.dress_house(craft, pk.paints(craft), lights=False)
    pr.DETAIL = 1


def _candy_bowl(craft):
    global HEAP_SCALE
    b = GameBuilder(3)
    pr.DETAIL, HEAP_SCALE = 0.75, 1.15
    pr.candy_bowl(b, craft, 0.3, None, spill=False, heap=40)
    pr.DETAIL, HEAP_SCALE = 1, 1.0
    b.finish("CandyBowl")


def _pumpkin(craft):
    b = GameBuilder(5)
    pr.pumpkin(b, craft, 0.2, 0.9, None, style=None, rgb=H(0xF59A23), lobes=7)
    b.finish("Pumpkin")


def _jack(craft):
    b = GameBuilder(5)
    pr.pumpkin(b, craft, 0.34, 0.82, None, style=0, lobes=9)
    b.finish("JackOLantern")


GATE_W, GATE_LEN = 0.9, 0.9 + 0.16
GATE_HINGE = T(-GATE_LEN / 2 + 0.08 - 0.01, 0.0, 0.0)
GATE_AJAR = GATE_HINGE @ R("Z", -24)  # as the review builder poses it; the game model is exported closed


def _fence_gate_parts(P):
    fixed = {P["trim_z"].name, P["trim_x"].name}

    def classify(b, mat, lo, hi, pts):
        if b.card:
            return SKIP  # the pegged paper bat belongs to the long review run
        return None if mat.name in fixed else "Gate"
    return classify


def _no_cards(b, mat, lo, hi, pts):
    return SKIP if b.card else None


def _bat_only(b, mat, lo, hi, pts):
    """The collectible flutters free in game: no stand, wire or display sparkles."""
    if not b.card or (glow_strength(mat) >= 1.0 and len(pts) > 3 and max(hi - lo) < 0.05):
        return SKIP
    return None


def _kit_roof(P):
    def classify(b, mat, lo, hi, pts):
        if CTX_NAME[0] != "Kit_Roof":
            return None
        lean_to = mat.name == P["wall_v"].name or (mat.name == P["trim_x"].name and lo.y > 1.0)
        return SKIP if lean_to else None
    return classify


CTX_NAME = [""]
BAT_PIVOT = T(0, 0.085, 0.4)


def model_list(craft):
    P = pk.paints(craft)
    owl = T(0, -0.02, oh.OWL_Z)
    door_hinge = T(-0.55, -0.015, pk.FLOOR)
    props = [
        Model("Bench", "Env", pr.build_bench, 1.2, min_size=0.03),
        Model("CandyBowl", "Env", _candy_bowl, 2.6, glow="Face", sharp=95.0),
        Model("FenceSection", "Env", lambda c: pr.build_fence(c, length=2.0, gate=False), 0.9, classify=_no_cards),
        Model("FenceGate", "Env", lambda c: pr.build_fence(c, length=GATE_LEN, gate_at=0.08), 1.0,
              classify=_fence_gate_parts(P), pivots={"Gate": GATE_AJAR}),
        Model("Hedge", "Env", pr.build_hedge, 0.45),
        Model("StreetLamp", "Env", pr.build_street_lamp, 1.2),
        Model("PartyTable", "Env", pr.build_party_table, 1.5),
        Model("PaperBat", "Env", pr.build_paper_bat, 3.0, origin=BAT_PIVOT.translation, classify=_bat_only),
        Model("Crate", "Env", pr.build_crate, 1.3, min_size=0.03),
        Model("Awning", "Env", pr.build_awning, 0.7),
        Model("PlankBridge", "Env", pr.build_bridge, 0.9, min_size=0.03),
        Model("Maple", "Env", lambda c: pr.build_maple(c, treehouse=False), 0.3),
        Model("TreehousePlatform", "Env", lambda c: pr.build_maple(c, treehouse=True, crown=False), 0.8,
              skip_finish=("Maple",)),
        Model("Pumpkin", "Env", _pumpkin, 2.0),
        Model("JackOLantern", "Env", _jack, 2.0),
        Model("Mailbox", "Env", pr.build_mailbox, 1.5),
    ]
    kit = Model("Kit", "Env", lambda c: pk.build_kit_strip(c), 0.75, per_finish=True, classify=_kit_roof(P), ground=False,
                pivots={"FrontDoor": T(-0.55, -0.015, 0.0)})
    return [
        Model("OwlHouse", "OwlHouse", _house, 1.0, origin=HOUSE_ORIGIN, min_size=0.03, min_bevel=0.0,
              pivots={"OwlPuppet": owl, "WindWheel": T(*oh.WHEEL), "FrontDoor": door_hinge}),
        Model("OwlHouseDressing", "Env", _dressing, 1.2, origin=HOUSE_ORIGIN, min_size=0.012, behind="OwlHouse"),
    ] + props + [kit]


def clear_scene_objects(keep):
    for obj in list(bpy.data.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)


def _sphere_dirs(n):
    out = []
    for i in range(n):
        z = 1 - 2 * (i + 0.5) / n
        r, a = math.sqrt(1 - z * z), i * math.pi * (3 - math.sqrt(5))
        out.append(Vector((r * math.cos(a), r * math.sin(a), z)))
    return out


DIRS = _sphere_dirs(128)
EYE_MIN = 0.3  # the camera never goes lower than this above the ground


SAMPLE_STEP, SAMPLE_MIN, SAMPLE_MAX = 0.1, 3, 6  # visibility samples: about one per 10 cm, 3 to 6 rows per triangle


def _tri_grid(a, b2, c2):
    """Evenly spread points on a triangle (barycentric grid). A big face that is half buried in its neighbour
    (the overlapping clumps of the maple crown) is still seen through the part that sticks out, and a handful
    of fixed sample points can all lie in the buried part."""
    k = min(SAMPLE_MAX, max(SAMPLE_MIN, math.ceil(max((b2 - a).length, (c2 - b2).length, (a - c2).length) / SAMPLE_STEP)))
    mid = (a + b2 + c2) / 3
    out = [mid] + [mid.lerp(p, 0.85) for p in (a, b2, c2)]  # a face in a crack often shows only near its corners
    for i in range(k):
        for j in range(k - i):
            for third in (1, 2)[:2 if i + j < k - 1 else 1]:  # upright and inverted cells
                out.append(a + (b2 - a) * ((i + third / 3) / k) + (c2 - a) * ((j + third / 3) / k))
    return out


def _triangles(verts, face):
    if len(face) == 3:
        return [tuple(face)]
    return [tuple(face[i] for i in t) for t in tessellate_polygon([[Vector(verts[i]) for i in face]])]


def visible(verts, faces, cards, occluder=None, ground=True, sheets=None):
    """Per face: can any outside viewpoint see it? A face is hidden when every ray from it (a grid of sample
    points, the whole hemisphere) is blocked by the static geometry or would only reach an eye below
    `EYE_MIN`. Hidden faces (backing slabs behind boards, undersides on the ground, buried ends) are not
    exported, which saves triangles and, more importantly, atlas texels.

    Dropping a face that can be seen leaves a hole right through the model (the far side is back-face culled
    in game), so the test must not err that way:
    - Faces are split into triangles here, and the same triangles are the blockers and carry the sample
      points. A bent quad (the noisy lathe of a maple clump) is otherwise sampled on its flat average, which
      lies under the ridge of the blocker's own triangulation: the face then hides itself.
    - Paper `sheets` never count as blockers: they are loose scatter with gaps between them (crown leaves)."""
    blockers = [t for f, sheet in zip(faces, sheets or [False] * len(faces)) if not sheet for t in _triangles(verts, f)]
    all_v = list(verts)
    if occluder:
        base = len(all_v)
        all_v += occluder[0]
        blockers += [tuple(base + i for i in t) for f in occluder[1] for t in _triangles(occluder[0], f)]
    bvh = BVHTree.FromPolygons(all_v, blockers, all_triangles=True, epsilon=0.0)
    keep = []
    for f, card in zip(faces, cards):
        if card:
            keep.append(True)
            continue
        seen = False
        for tri in _triangles(verts, f):
            a, b2, c2 = (Vector(verts[i]) for i in tri)
            n = (b2 - a).cross(c2 - a)
            if n.length < 1e-10:
                continue  # sliver: says nothing either way
            n.normalize()
            samples = [p + n * 0.003 for p in _tri_grid(a, b2, c2)]
            for d in DIRS:
                if d.dot(n) < 0.08:
                    continue
                for o in samples:
                    reach = 1e4
                    if ground:
                        if o.z < 0.0:
                            continue
                        if d.z < 0.0:
                            reach = (o.z - EYE_MIN) / -d.z
                            if reach < 0.5:
                                continue
                    if bvh.ray_cast(o, d, reach)[0] is None:
                        seen = True
                        break
                if seen:
                    break
            if seen:
                break
        keep.append(seen)
    return keep


OCCLUDERS = {}  # model name -> (verts, faces) of its static mesh, for models dressed onto it


def mesh_from_builders(name, builders, part, model=None):
    """One working mesh for `part` out of every builder of a model: the approved materials on their own slots,
    the "tint" corner colour, and a "card" face flag."""
    verts, faces, cols, mat_ids, smooth, cards, sheets, mats = [], [], [], [], [], [], [], []
    for b in builders:
        remap, local = {}, {}
        for i, m in enumerate(b.mats):
            if m not in mats:
                mats.append(m)
            remap[i] = mats.index(m)
        for f, c, mi, sm, p, cd, sh in zip(b.faces, b.cols, b.mat_ids, b.smooth, b.parts, b.cards, b.sheets):
            if p != part:
                continue
            idx = []
            for v in f:
                if v not in local:
                    local[v] = len(verts)
                    verts.append(b.verts[v])
                idx.append(local[v])
            faces.append(idx)
            cols.append(c)
            mat_ids.append(remap[mi])
            smooth.append(sm)
            cards.append(cd)
            sheets.append(sh)
    if not faces:
        return None
    if part is None and model is not None:
        OCCLUDERS[model] = (verts, [f for f, sh in zip(faces, sheets) if not sh])
        keep = visible(verts, faces, cards, OCCLUDERS.get(CTX.behind), CTX.ground, sheets)
        faces, cols, mat_ids, smooth, cards = ([x for x, k in zip(seq, keep) if k] for seq in (faces, cols, mat_ids, smooth, cards))
        used = sorted({i for f in faces for i in f})
        index = {v: i for i, v in enumerate(used)}
        verts = [verts[v] for v in used]
        faces = [[index[i] for i in f] for f in faces]
        if len(keep) - sum(keep):
            log(f"  {name}: {len(keep) - sum(keep)} of {len(keep)} faces never visible, dropped")
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    for m in mats:
        me.materials.append(m)
    me.polygons.foreach_set("material_index", mat_ids)
    me.polygons.foreach_set("use_smooth", smooth)
    attr = me.color_attributes.new("tint", "FLOAT_COLOR", "CORNER")
    totals = np.zeros(len(me.polygons), dtype=np.int32)
    me.polygons.foreach_get("loop_total", totals)
    data = np.ones((len(me.loops), 4), dtype=np.float32)
    data[:, :3] = np.repeat(np.array(cols, dtype=np.float32).reshape(-1, 3), totals, axis=0)
    attr.data.foreach_set("color", data.ravel())
    flag = me.attributes.new("card", "INT", "FACE")
    flag.data.foreach_set("value", np.array(cards, dtype=np.int32))
    me.use_auto_smooth = True
    me.auto_smooth_angle = math.radians(CTX.sharp)
    me.update()
    obj = bpy.data.objects.new(name, me)
    el.pm.link(obj)
    obj.modifiers.new("Tri", "TRIANGULATE")
    wn = obj.modifiers.new("Normals", "WEIGHTED_NORMAL")
    wn.keep_sharp, wn.mode, wn.weight = True, "FACE_AREA_WITH_ANGLE", 50
    dg = bpy.context.evaluated_depsgraph_get()
    baked = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    obj.modifiers.clear()
    obj.data = baked
    bpy.data.meshes.remove(me)
    baked.name = name
    return obj


def build_models(craft, only=None):
    global CTX
    models = []
    for spec in model_list(craft):
        if only and spec.name not in only and not (spec.name == "Kit" and any(o.startswith("Kit") for o in only)):
            continue
        keep = set(bpy.data.objects)
        CTX = Ctx(spec.name, **spec.ctx)
        if spec.ctx.get("per_finish"):
            _per_finish_names(spec, craft)
        else:
            spec.build(craft)
        built = CTX.builders
        clear_scene_objects(keep)  # holders and the review lights
        for name, builders in built.items():
            m = spec if not CTX.per_finish else Model(name, spec.atlas, None, spec.weight, pivots=spec.pivots)
            if CTX.per_finish and only and name not in only and "Kit" not in only:
                continue
            for part in sorted({p for b in builders for p in b.parts}, key=lambda p: p or ""):
                obj = mesh_from_builders(f"{name}__{part or 'main'}", builders, part, name)
                if obj is not None:
                    m.parts[part] = obj
                    m.tris[part] = len(obj.data.polygons)
            models.append(m)
            cards = sum(int(np.sum(_face_attr(o.data, "card"))) for o in m.parts.values())
            total = sum(m.tris.values()) + cards
            detail = ", ".join(f"{p or 'main'} {n}" for p, n in m.tris.items())
            log(f"TRIS {name}: {total} (with card backs; {detail})")
    return models


def _per_finish_names(spec, craft):
    """Kit strip: every `finish` is its own model, and the roof classifier needs to know which one is open."""
    finish = GameBuilder.finish

    def named(self, name, sharp=75.0):
        return finish(self, name, sharp)
    orig_init = GameBuilder.__init__
    order = ["Kit_" + n for n in ("Floor", "Steps", "Post", "Rail", "Wall", "Window", "Door", "Roof", "Corner")]

    def init(self, seed=0):
        orig_init(self, seed)
        CTX_NAME[0] = order[seed - 10] if 10 <= seed < 10 + len(order) else ""
        CTX.seen = set()
    GameBuilder.__init__ = init
    try:
        spec.build(craft)
    finally:
        GameBuilder.__init__ = orig_init
        CTX_NAME[0] = ""


def _face_attr(me, name):
    out = np.zeros(len(me.polygons), dtype=np.int32)
    if name in me.attributes:
        me.attributes[name].data.foreach_get("value", out)
    return out


# --- UVs ---------------------------------------------------------------------------------------------------

def _select(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def _uv_and_area(me):
    n = len(me.loops)
    uv = np.zeros(n * 2, dtype=np.float32)
    me.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 3, 2)
    co = np.zeros(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get("co", co)
    vi = np.zeros(n, dtype=np.int32)
    me.loops.foreach_get("vertex_index", vi)
    tri = co.reshape(-1, 3)[vi].reshape(-1, 3, 3)
    a3 = 0.5 * np.linalg.norm(np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]), axis=1)
    e1, e2 = uv[:, 1] - uv[:, 0], uv[:, 2] - uv[:, 0]
    a2 = 0.5 * np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0])
    return uv, a3, a2


def unwrap_and_pack(models, atlas, size):
    """Smart-project every part, give each model `weight` x the common texel density, then pack all islands of
    the atlas into one square."""
    objs = [(m, o) for m in models if m.atlas == atlas for o in m.parts.values()]
    if not objs:
        return 0.0
    t0 = time.time()
    for m, o in objs:
        _select([o])
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.0, area_weight=0.0, correct_aspect=True,
                                 scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        uv, a3, a2 = _uv_and_area(o.data)
        k = m.weight * math.sqrt(max(a3.sum(), 1e-9) / max(a2.sum(), 1e-12)) * 0.01
        o.data.uv_layers.active.data.foreach_set("uv", (uv * k).ravel())
    log(f"{atlas}: unwrapped {len(objs)} meshes in {time.time() - t0:.0f}s")
    t0 = time.time()
    _select([o for _m, o in objs])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(udim_source="CLOSEST_UDIM", rotate=True, rotate_method="CARDINAL", scale=True, merge_overlap=False,
                            margin_method="FRACTION", margin=1.0 / size, shape_method="AABB")
    bpy.ops.object.mode_set(mode="OBJECT")
    used = area = 0.0
    for m, o in objs:
        _uv, a3, a2 = _uv_and_area(o.data)
        used += a2.sum()
        area += a3.sum()
        m.density = math.sqrt(a2.sum() / max(a3.sum(), 1e-9)) * size
    log(f"{atlas}: packed in {time.time() - t0:.0f}s, fill {used * 100:.0f}%, surface {area:.0f} m2")
    for m in models:
        if m.atlas == atlas:
            log(f"  {m.name}: {m.density:.0f} px/m")
    return used


# --- bake --------------------------------------------------------------------------------------------------

def _pixels(img):
    px = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    return px.reshape(img.size[1], img.size[0], 4)


def _dilate(rgb, mask, rounds):
    """Grow covered texels into empty ones (keeps mip-mapping from pulling in background colour)."""
    rgb, mask = rgb.copy(), mask.copy()
    for _ in range(rounds):
        if mask.all():
            break
        acc = np.zeros_like(rgb)
        cnt = np.zeros(mask.shape, dtype=np.float32)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            sm = np.roll(mask, (dy, dx), axis=(0, 1))
            acc += np.roll(rgb, (dy, dx), axis=(0, 1)) * sm[..., None]
            cnt += sm
        grow = (~mask) & (cnt > 0)
        rgb[grow] = acc[grow] / cnt[grow][:, None]
        mask |= grow
    return rgb, mask


def _save(name, rgb, out_dir, colour=True):
    h, w = rgb.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=False)
    if not colour:
        img.colorspace_settings.name = "Non-Color"
    px = np.ones((h, w, 4), dtype=np.float32)
    px[:, :, :3] = np.clip(rgb, 0.0, 1.0)
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = os.path.join(out_dir, name + ".png")
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def bake_atlas(models, atlas, size, samples, out_dir):
    objs = [o for m in models if m.atlas == atlas for o in m.parts.values()]
    if not objs:
        return
    t0 = time.time()
    copies = []
    for o in objs:
        c = o.copy()
        c.data = o.data.copy()
        el.pm.link(c)
        copies.append(c)
    _select(copies)
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = False
    scene.render.bake.margin = 0
    scene.render.bake.use_clear = True
    scene.render.bake.target = "IMAGE_TEXTURES"
    images = {}
    for kind in ("BaseColor", "Emission", "Normal"):
        img = bpy.data.images.new(f"bake_{atlas}_{kind}", size, size, alpha=True)
        img.generated_color = (0, 0, 0, 0)
        if kind == "Normal":
            img.colorspace_settings.name = "Non-Color"
        images[kind] = img
    saved = []
    nodes_added = []
    for slot in joined.material_slots:
        mat = slot.material
        nodes = mat.node_tree.nodes
        node = nodes.new("ShaderNodeTexImage")
        node.name = "BakeTarget"
        nodes.active = node
        nodes_added.append((mat, node))
        bsdf = nodes["Principled BSDF"]
        saved.append((bsdf, bsdf.inputs["Emission Strength"].default_value, bsdf.inputs["Metallic"].default_value))
        bsdf.inputs["Emission Strength"].default_value = min(1.0, bsdf.inputs["Emission Strength"].default_value)
        bsdf.inputs["Metallic"].default_value = 0.0  # foil: bake its colour, not a black metal albedo
    _select([joined])
    passes = (("BaseColor", dict(type="DIFFUSE", pass_filter={"COLOR"})), ("Emission", dict(type="EMIT")),
              ("Normal", dict(type="NORMAL", normal_space="TANGENT")))
    for kind, kw in passes:
        for _mat, node in nodes_added:
            node.image = images[kind]
        bpy.ops.object.bake(margin=0, use_clear=True, **kw)
        log(f"{atlas}: baked {kind} ({time.time() - t0:.0f}s)")
    for bsdf, strength, metallic in saved:
        bsdf.inputs["Emission Strength"].default_value = strength
        bsdf.inputs["Metallic"].default_value = metallic
    for mat, node in nodes_added:
        mat.node_tree.nodes.remove(node)
    base, emit, normal = (_pixels(images[k]) for k in ("BaseColor", "Emission", "Normal"))
    mask = base[:, :, 3] > 0.5
    colour = np.maximum(base[:, :, :3], emit[:, :, :3])  # glow parts carry their light colour as base colour
    colour, grown = _dilate(colour, mask, 24)
    colour[~grown] = colour[mask].mean(axis=0) if mask.any() else 0.5
    _save(f"{atlas}_BaseColor", colour, out_dir)
    glow, _ = _dilate(emit[:, :, :3] * mask[..., None], mask, 4)
    _save(f"{atlas}_Emission", glow, out_dir)
    nrm = normal[:, :, :3].copy()
    nrm[~(normal[:, :, 3] > 0.5)] = (0.5, 0.5, 1.0)
    xy = np.clip(nrm[:, :, :2] * 2 - 1, -0.3, 0.3)  # slivers bake wild tangents: keep it to brush-stroke relief
    nrm[:, :, :2] = xy * 0.5 + 0.5
    nrm[:, :, 2] = np.sqrt(1.0 - (xy ** 2).sum(axis=2)) * 0.5 + 0.5
    nrm, grown = _dilate(nrm, normal[:, :, 3] > 0.5, 8)
    _save(f"{atlas}_Normal", nrm, out_dir, colour=False)
    for img in images.values():
        bpy.data.images.remove(img)
    bpy.data.objects.remove(joined, do_unlink=True)
    log(f"{atlas}: textures written, coverage {mask.mean() * 100:.0f}% ({time.time() - t0:.0f}s)")


# --- final meshes and FBX ----------------------------------------------------------------------------------

def final_object(name, src, to_local, material):
    """Plain game mesh: one material, baked UVs, the approved shading normals, real back faces on cards,
    geometry moved into its pivot frame by `to_local`."""
    me = src.data
    me.calc_normals_split()
    nl = len(me.loops)
    co = np.zeros(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    vi = np.zeros(nl, dtype=np.int32)
    me.loops.foreach_get("vertex_index", vi)
    nrm = np.zeros(nl * 3, dtype=np.float32)
    me.loops.foreach_get("normal", nrm)
    nrm = nrm.reshape(-1, 3)
    uv = np.zeros(nl * 2, dtype=np.float32)
    me.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    M = np.array(to_local, dtype=np.float32)
    co = co @ M[:3, :3].T + M[:3, 3]
    nrm = nrm @ M[:3, :3].T
    tris = vi.reshape(-1, 3)
    card = _face_attr(me, "card").astype(bool)
    if card.any():
        used = np.unique(tris[card])
        back = np.full(len(co), -1, dtype=np.int32)
        back[used] = len(co) + np.arange(len(used))
        co = np.concatenate([co, co[used]])
        tris = np.concatenate([tris, back[tris[card]][:, ::-1]])
        loops = np.arange(nl).reshape(-1, 3)[card][:, ::-1].ravel()
        nrm = np.concatenate([nrm, -nrm[loops]])
        uv = np.concatenate([uv, uv[loops]])
    out = bpy.data.meshes.new(name)
    out.from_pydata(co.tolist(), [], tris.tolist())
    out.uv_layers.new(name="UVMap").data.foreach_set("uv", uv.ravel())
    out.polygons.foreach_set("use_smooth", np.ones(len(out.polygons), dtype=bool))
    out.use_auto_smooth = True
    out.normals_split_custom_set(nrm.tolist())
    out.materials.append(material)
    out.update()
    obj = bpy.data.objects.new(name, out)
    el.pm.link(obj)
    return obj


def export_model(m, materials, out_dir):
    shift = T(*(-m.origin))
    root_name = m.name
    children = []
    if None in m.parts:
        root = final_object(root_name, m.parts[None], shift, materials[m.atlas])
    else:
        root = bpy.data.objects.new(root_name, None)
        el.pm.link(root)
    report = {"triangles": 0, "children": {}}
    if root.type == "MESH":
        report["triangles"] += len(root.data.polygons)
    for part, src in m.parts.items():
        if part is None:
            continue
        # the child's pivot frame in model space; glow parts without one share the model origin
        frame = shift @ m.pivots[part] if part in m.pivots else Matrix.Identity(4)
        rest = Matrix.Translation(frame.translation)  # children are exported unrotated (gate closed)
        mat = materials["Glow"] if part.endswith(GLOW_SUFFIX) else materials[m.atlas]
        child = final_object(part, src, frame.inverted() @ shift, mat)
        child.parent = root
        child.matrix_local = rest
        children.append(child)
        report["children"][part] = {"triangles": len(child.data.polygons), "pivot": [round(c, 4) for c in rest.translation]}
        report["triangles"] += len(child.data.polygons)
    bpy.context.view_layer.update()
    _select([root] + children)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out_dir, m.name + ".fbx"), use_selection=True, object_types={"MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0, axis_forward="-Z", axis_up="Y",
        bake_space_transform=False, use_mesh_modifiers=False, mesh_smooth_type="OFF", use_tspace=False, bake_anim=False,
        use_custom_props=False, path_mode="STRIP", embed_textures=False)
    for o in [root] + children:
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data is not None:
            bpy.data.meshes.remove(data)
    report["atlas"] = m.atlas
    return report


def plain_material(name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    return mat


def main():
    rest = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = dict(a.split("=", 1) for a in rest if "=" in a)
    flags = [a for a in rest if "=" not in a]
    out_dir = next((a for a in flags if a != "count"), OUT_DIR)
    size, samples = int(opts.get("size", 2048)), int(opts.get("samples", 4))
    only = set(opts["only"].split(",")) if "only" in opts else None
    t0 = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    install_hooks()
    craft = GameCraft(1.2)
    models = build_models(craft, only)
    if "count" in flags:
        return
    os.makedirs(out_dir, exist_ok=True)
    for atlas in ("OwlHouse", "Env"):
        unwrap_and_pack(models, atlas, size)
        bake_atlas(models, atlas, size, samples, out_dir)
    materials = {name: plain_material(name) for name in ("OwlHouse", "Env", "Glow")}
    manifest = {}
    for m in models:
        manifest[m.name] = export_model(m, materials, out_dir)
        manifest[m.name]["px_per_m"] = round(getattr(m, "density", 0.0))
        log(f"exported {m.name}.fbx: {manifest[m.name]['triangles']} tris, children {list(manifest[m.name]['children'])}")
    side = opts.get("manifest")
    if side:
        with open(side, "w") as fh:
            json.dump(manifest, fh, indent=1)
    log(f"done in {time.time() - t0:.0f}s -> {out_dir}")


if __name__ == "__main__":
    main()
