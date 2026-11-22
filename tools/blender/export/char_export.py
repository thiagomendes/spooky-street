"""Shared game-export pipeline for the rigged cast (Pip, Bea, Mr. Vale): everything that does not depend on one
character's costume. `pip_export.py` and `witness_export.py` supply the part tables, bones, weights and clips.

Steps (see pip_export.py for the full story): realise every builder part at game resolution -> rig and skin
weights -> join, remove faces nothing can see -> Smart UV into one atlas plus a small face map (the skull is one
azimuthal island) -> Cycles bake (base colour x tinted AO, tangent normals) -> one action per clip -> FBX.

Conventions: metres, Z up, characters face -Y (Unity +Z after import), +X is the character's left.
"""
import math
import re

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


class Char:
    """What the shared steps need to know about one character."""

    def __init__(self, name, groups, parts, detail, tag=None):
        self.name = name  # object / file prefix: "Pip" -> Pip_Body, Pip_Face, Pip_BaseColor.png
        self.groups = tuple(groups)  # atlas mesh groups (the face is its own mesh with its own map)
        self.parts = parts  # [(regex, group, subsurf, curve, bevel, triangle target)]
        self.detail = detail  # compiled regex: small surface details never count as cover for hidden faces
        self.part_names = []  # source part of every face (face attribute "part" indexes this)
        self.tag = tag or name.lower() + "_export"

    def log(self, *a):
        print(f"[{self.tag}]", *a, flush=True)

    def spec_for(self, src_name):
        return next((s for s in self.parts if re.match(s[0], src_name)), None)


def smooth(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def activate(obj, others=()):
    bpy.ops.object.select_all(action="DESELECT")
    for o in others:
        o.select_set(True)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def tri_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


# --- 1. game meshes ----------------------------------------------------------------------------------------

def patch_materials():
    """Procedural textures read object space; point them at a stored `orco` attribute instead, so parts can be
    decimated, moved to world space and joined without the pattern sliding."""
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        attr = None
        for node in list(nodes):
            if node.type != "TEX_COORD":
                continue
            for link in list(node.outputs["Object"].links):
                if attr is None:
                    attr = nodes.new("ShaderNodeAttribute")
                    attr.attribute_type = "GEOMETRY"
                    attr.attribute_name = "orco"
                to = link.to_socket
                links.remove(link)
                links.new(attr.outputs["Vector"], to)


def rebake(obj, old):
    """Apply the object's modifiers into a fresh mesh."""
    deps = bpy.context.evaluated_depsgraph_get()
    deps.update()
    new = bpy.data.meshes.new_from_object(obj.evaluated_get(deps))
    obj.modifiers.clear()
    obj.data = new
    bpy.data.meshes.remove(old)
    return new


def realize(src, spec, post=None):
    """Game-resolution copy of one source part, in world space, remembering its object-space coordinates.

    spec = (pattern, group, subdivision level (None = as built), curve (bevel resolution, spline resolution),
    keep bevel modifiers, triangle target). The target is None (no decimation), a number, or ("sheet", n): the
    cloth is decimated as a single sheet of n triangles and only then given its thickness, so the outer and
    lining shells share one topology and can never cross. `post(src, mesh)` may adjust the world-space mesh."""
    _pattern, group, subsurf, curve, bevel, target = spec
    for m in src.modifiers:
        if m.type == "SUBSURF" and subsurf is not None:
            m.levels = subsurf
        if m.type == "BEVEL" and not bevel:
            m.show_viewport = False
    if src.type == "CURVE" and curve is not None:
        src.data.bevel_resolution, src.data.resolution_u = curve
    sheet = None
    if isinstance(target, tuple):
        sol = next(m for m in src.modifiers if m.type == "SOLIDIFY")
        sheet = (sol.thickness, sol.offset, sol.material_offset, sol.material_offset_rim)
        sol.show_viewport = False
        target = target[1]
    deps = bpy.context.evaluated_depsgraph_get()
    deps.update()
    me = bpy.data.meshes.new_from_object(src.evaluated_get(deps))
    obj = bpy.data.objects.new("G_" + src.name, me)
    bpy.context.scene.collection.objects.link(obj)
    me.calc_loop_triangles()
    count = len(me.loop_triangles)
    if target is not None and count > target * 1.1:
        dec = obj.modifiers.new("Dec", "DECIMATE")
        dec.ratio = target / count
        me = rebake(obj, me)
    if sheet is not None:
        sol = obj.modifiers.new("Thick", "SOLIDIFY")
        sol.thickness, sol.offset, sol.material_offset, sol.material_offset_rim = sheet
        me = rebake(obj, me)
    co = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get("co", co)
    me.attributes.new("orco", "FLOAT_VECTOR", "POINT").data.foreach_set("vector", co)
    world = src.matrix_world.copy()
    me.transform(world)
    if world.determinant() < 0:
        me.flip_normals()
    if post is not None:
        post(src, me)
    me.update()
    obj["src"] = src.name
    obj["grp"] = group
    return obj


def keep_skull_front(obj, head_c, hidden):
    """Delete the skull faces `hidden(unit direction from the head centre)` says are always under hair (they
    would also wrap the face's UV island) and mark the rest as the skull island."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    doomed = [f for f in bm.faces if hidden((f.calc_center_median() - head_c).normalized())]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(me)
    bm.free()
    me.attributes.new("skull", "INT", "FACE").data.foreach_set("value", [1] * len(me.polygons))


# --- 2. rig -------------------------------------------------------------------------------------------------

def build_armature(table, rig_name):
    """table: [(name, parent, head, tail, deforms the body skin)] in parents-first order."""
    arm = bpy.data.armatures.new(rig_name)
    rig = bpy.data.objects.new(rig_name, arm)
    bpy.context.scene.collection.objects.link(rig)
    activate(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, parent, head, tail, _ in table:
        eb = arm.edit_bones.new(name)
        eb.head, eb.tail = head, tail
        if parent:
            eb.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return rig


def hand_scale(P, size=1.0):
    """The factor anatomy.hand() scales its local coordinates by."""
    return P.head_r / 0.13 * (1.0 if P.child else 1.3) * size


def shoe_length(P):
    """Sneaker length, as in anatomy.sneaker()."""
    return (0.19 if P.child else 0.25) * P.head_r / 0.13


def human_bones(P, info, hand_size=1.0):
    """Pip's skeleton (same names, same conventions) fitted to any Proportions: Root, Hips, Spine, Chest, Neck,
    Head, and per side Shoulder, UpperArm, LowerArm, Hand, Fingers, Thumb, UpperLeg, LowerLeg, Foot, Toe.
    `info["handL"/"handR"]` are the hand pivots' world matrices. Costume bones are appended by the caller."""
    z, h = P.z, P.h
    waist = z["waist"]
    chest0 = z["chest"] - 0.012 * h
    neck0 = z["neck"] - 0.0108 * h
    head0 = P.head_c.z - P.head_r * (0.9 if P.child else 1.02) + 0.06 * P.head_r
    B = [("Root", None, Vector((0, 0, 0)), Vector((0, 0, 0.1 * h)), False),
         ("Hips", "Root", Vector((0, 0, z["pelvis"])), Vector((0, 0, waist)), True),
         ("Spine", "Hips", Vector((0, 0, waist)), Vector((0, 0, chest0)), True),
         ("Chest", "Spine", Vector((0, 0, chest0)), Vector((0, 0, neck0)), True),
         ("Neck", "Chest", Vector((0, 0, neck0)), Vector((0, 0.004, head0)), True),
         ("Head", "Neck", Vector((0, 0.004, head0)), Vector((0, 0.004, head0 + 0.2 * h)), True)]
    k = hand_scale(P, hand_size)
    f = shoe_length(P) / 0.195  # Pip's shoe is the reference for the foot joints
    for S, s in (("L", 1), ("R", -1)):
        sh = Vector((s * P.shoulder_x, 0, z["shoulder"]))
        el = P.elbow(s) + Vector((0, 0.014, 0))
        wr = P.wrist(s)
        M = info["hand" + S]
        hand_tip = M @ Vector((0, 0, -0.05 * k))
        knuckle = M @ Vector((0.002 * k, 0, -0.053 * k))
        finger_tip = M @ Vector((0.002 * k, 0, -0.098 * k))
        t1 = M @ (Vector((-0.005, -0.029, -0.03)) * k)
        t3 = M @ (Vector((-0.013, -0.049, -0.057)) * k)
        B += [("Shoulder_" + S, "Chest", Vector((s * 0.025 * h, 0, z["shoulder"] - 0.0067 * h)), sh, True),
              ("UpperArm_" + S, "Shoulder_" + S, sh, el, True),
              ("LowerArm_" + S, "UpperArm_" + S, el, wr, True),
              ("Hand_" + S, "LowerArm_" + S, wr, hand_tip, True),
              ("Fingers_" + S, "Hand_" + S, knuckle, finger_tip, False),
              ("Thumb_" + S, "Hand_" + S, t1, t3, False)]
        hip = Vector((s * P.hip_x, 0, z["hip"]))
        knee = Vector((s * P.hip_x * 1.02, -0.006, z["knee"]))
        ankle = Vector((s * P.hip_x, 0.004, z["ankle"]))
        ball = Vector((s * P.hip_x, -0.098 * f, 0.022 * f))
        B += [("UpperLeg_" + S, "Hips", hip, knee, True),
              ("LowerLeg_" + S, "UpperLeg_" + S, knee, ankle, True),
              ("Foot_" + S, "LowerLeg_" + S, ankle, ball, True),
              ("Toe_" + S, "Foot_" + S, ball, Vector((s * P.hip_x, -0.158 * f, 0.022 * f)), False)]
    return B


# --- 3. skin weights ----------------------------------------------------------------------------------------

def set_weights(obj, weights):
    """weights: {bone: per-vertex array or scalar}."""
    n = len(obj.data.vertices)
    for name, w in weights.items():
        vg = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
        w = np.broadcast_to(np.asarray(w, dtype=float), (n,))
        for i in np.nonzero(w > 1e-4)[0]:
            vg.add([int(i)], float(w[i]), "REPLACE")


def body_auto_weights(body, rig, table):
    """Bone-heat weights for the one clean body skin, from the body bones only."""
    deform = {name for name, *_rest, d in table if d}
    for b in rig.data.bones:
        b.use_deform = b.name in deform
    activate(rig, [body])
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    for b in rig.data.bones:
        b.use_deform = True
    names = [g.name for g in body.vertex_groups]
    W = np.zeros((len(body.data.vertices), len(names)))
    for v in body.data.vertices:
        for g in v.groups:
            W[v.index, g.group] = g.weight
    if (W.sum(axis=1) < 1e-6).any():
        raise RuntimeError("bone heat left unweighted body vertices")
    return names, W / W.sum(axis=1, keepdims=True)


class SkinSource:
    """Nearest-surface lookup of the body's weights."""

    def __init__(self, body, names, W, bones=None):
        """`bones`: only body triangles ruled by one of these bones are looked up, and only they are copied."""
        me = body.data
        me.calc_loop_triangles()
        self.co = [v.co.copy() for v in me.vertices]
        self.tris = [tuple(t.vertices) for t in me.loop_triangles]
        self.keep = None
        if bones is not None:
            self.keep = np.array([n in bones for n in names])
            self.tris = [t for t in self.tris if names[int(np.argmax(W[t[0]] + W[t[1]] + W[t[2]]))] in bones]
        self.tree = BVHTree.FromPolygons(self.co, self.tris)
        self.names, self.W = names, W

    def at(self, p):
        loc, _n, idx, _d = self.tree.find_nearest(p)
        a, b, c = self.tris[idx]
        pa, pb, pc = self.co[a], self.co[b], self.co[c]
        v0, v1, v2 = pb - pa, pc - pa, loc - pa
        d00, d01, d11, d20, d21 = v0.dot(v0), v0.dot(v1), v1.dot(v1), v2.dot(v0), v2.dot(v1)
        den = d00 * d11 - d01 * d01
        if abs(den) < 1e-14:
            return self.W[a]
        v = (d11 * d20 - d01 * d21) / den
        w = (d00 * d21 - d01 * d20) / den
        return self.W[a] * (1 - v - w) + self.W[b] * v + self.W[c] * w

    def transfer(self, obj, allow=None, fallback="Chest", rigid=False):
        """Copy weights to `obj`; `allow(bone, x)` filters bones; `rigid` gives the whole part its centre's weights."""
        pts = [v.co for v in obj.data.vertices]
        if rigid:
            centre = sum(pts, Vector()) / len(pts)
            pts = [centre] * len(pts)
        out = np.zeros((len(pts), len(self.names)))
        cache = None
        for i, p in enumerate(pts):
            if rigid and cache is not None:
                out[i] = cache
                continue
            w = self.at(p).copy()
            if self.keep is not None:
                w = w * self.keep
            if allow is not None:
                for j, n in enumerate(self.names):
                    if not allow(n, p.x):
                        w[j] = 0.0
            if w.sum() < 1e-4:
                w[:] = 0.0
                w[self.names.index(fallback)] = 1.0
            out[i] = w / w.sum()
            cache = out[i]
        set_weights(obj, {n: out[:, j] for j, n in enumerate(self.names)})


def hand_weights(obj, k, src, prefix_len):
    """Palm and wrist follow the hand; fingers and thumb blend onto their bones past the knuckle.
    `k` is the hand's scale (hand_scale), `prefix_len` the length of "<Name>_Hand"."""
    S = src[prefix_len]  # <Name>_Hand<L|R>…
    n = len(obj.data.vertices)
    local = np.empty(n * 3, dtype=np.float32)
    obj.data.attributes["orco"].data.foreach_get("vector", local)
    local = local.reshape(n, 3)
    w = np.zeros(n)
    kind = src[prefix_len + 1:]
    bone = "Fingers_" + S
    if kind.startswith("Finger"):
        fy, fz = [(-0.0235, -0.052), (-0.0078, -0.055), (0.0078, -0.054), (0.0235, -0.05)][int(kind[6])]
        k0 = np.array((0.002 * k, fy * k, fz * k))
        d = np.linalg.norm(local - k0, axis=1)
        w = np.array([smooth((di - 0.004 * k) / (0.012 * k)) if q[2] < k0[2] + 0.003 * k else 0.0
                      for di, q in zip(d, local)])
    elif kind.startswith("Thumb"):
        bone = "Thumb_" + S
        t1 = np.array((-0.005, -0.029, -0.03)) * k
        axis = np.array((-0.008, -0.02, -0.027))
        axis /= np.linalg.norm(axis)
        w = np.array([smooth(((q - t1) @ axis + 0.004 * k) / (0.012 * k)) for q in local])
    elif kind.startswith("Nail"):
        if kind.endswith(".004"):
            bone = "Thumb_" + S
        w = np.ones(n)
    set_weights(obj, {"Hand_" + S: 1 - w, bone: w})


def shoe_weights(obj, src, prefix_len, f=1.0):
    """`f`: shoe length relative to Pip's (0.195 m); `prefix_len` the length of "<Name>_Shoe"."""
    S = src[prefix_len]
    y = np.array([v.co.y for v in obj.data.vertices])
    w = np.array([smooth((-0.086 * f - yi) / (0.026 * f)) for yi in y])
    if "Collar" in src:
        w[:] = 0
    set_weights(obj, {"Foot_" + S: 1 - w, "Toe_" + S: w})


def limit_weights(obj, limit=4):
    for v in obj.data.vertices:
        gs = sorted(v.groups, key=lambda g: -g.weight)
        for g in gs[limit:]:
            obj.vertex_groups[g.group].remove([v.index])
        total = sum(g.weight for g in gs[:limit])
        if total <= 0:
            raise RuntimeError(f"{obj.name}: unweighted vertex {v.index}")
        for g in gs[:limit]:
            g.weight = g.weight / total


# --- 4. join, hidden faces, UVs ------------------------------------------------------------------------------

def join(char, parts, name):
    for o in parts:
        if "part" not in o.data.attributes:
            char.part_names.append(o["src"])
            o.data.attributes.new("part", "INT", "FACE").data.foreach_set(
                "value", [len(char.part_names) - 1] * len(o.data.polygons))
        if "grp" not in o.data.attributes:
            o.data.attributes.new("grp", "INT", "FACE").data.foreach_set(
                "value", [char.groups.index(o["grp"]) if o["grp"] in char.groups else 0] * len(o.data.polygons))
    activate(parts[0], parts[1:])
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.data.name = name
    return obj


def region_of(bone):
    """Body region a bone belongs to; only geometry of the same region may hide a face."""
    if bone == "Head" or bone.startswith("Hair"):
        return "head"
    m = re.match(r"(Shoulder|UpperArm|LowerArm|Hand|Fingers|Thumb)_([LR])$", bone)
    if m:
        return "arm" + m.group(2)
    m = re.match(r"(UpperLeg|LowerLeg|Foot|Toe)_([LR])$", bone)
    if m:
        return "leg" + m.group(2)
    return "core"


def remove_hidden(char, objs, rings, region=region_of):
    """Delete faces that no ray can reach from outside (sampled over the hemisphere).

    A face only counts as hidden when geometry of its own body region (head, core, each arm, each leg) covers
    it, so nothing that can move away (an arm resting against the vest, the cape) ever justifies a hole; small
    details never count as cover; and a margin of `rings[obj.name]` faces around everything visible is kept."""
    work = []
    soup = {}
    for o in objs:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.normal_update()
        deform = bm.verts.layers.deform.verify()
        part = bm.faces.layers.int["part"]
        names = [g.name for g in o.vertex_groups]
        regions = {}
        for f in bm.faces:
            total = {}
            for v in f.verts:
                for gi, w in v[deform].items():
                    total[gi] = total.get(gi, 0.0) + w
            regions[f] = region(names[max(total, key=total.get)])
            if not char.detail.match(char.part_names[f[part]]):
                verts, polys = soup.setdefault(regions[f], ([], []))
                polys.append(tuple(range(len(verts), len(verts) + len(f.verts))))
                verts.extend(v.co.copy() for v in f.verts)
        work.append((o, bm, regions))
    trees = {r: BVHTree.FromPolygons(v, p) for r, (v, p) in soup.items()}
    dirs = []
    for i in range(72):  # Fibonacci sphere
        zz = 1 - 2 * (i + 0.5) / 72
        rr = math.sqrt(1 - zz * zz)
        a = i * math.pi * (3 - math.sqrt(5))
        dirs.append(Vector((rr * math.cos(a), rr * math.sin(a), zz)))
    removed = 0
    for o, bm, regions in work:
        hidden = set()
        for f in bm.faces:
            tree = trees.get(regions[f])
            if tree is None:
                continue
            n = f.normal
            c = f.calc_center_median()
            seen = False
            for p in [c] + [c.lerp(v.co, 0.6) for v in f.verts]:
                start = p + n * 4e-4
                for d in dirs:
                    if d.dot(n) > 0.06 and tree.ray_cast(start, d)[0] is None:
                        seen = True
                        break
                if seen:
                    break
            if not seen:
                hidden.add(f)
        for _ in range(rings[o.name]):
            hidden = {f for f in hidden if all(g in hidden for v in f.verts for g in v.link_faces)}
        removed += len(hidden)
        bmesh.ops.delete(bm, geom=list(hidden), context="FACES")
        bm.to_mesh(o.data)
        bm.free()
        o.data.update()
    return removed


def unwrap(obj, margin):
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=margin, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def unwrap_face(face, head_c, head_r):
    """Smart UV for the small parts, but the skull becomes ONE island (azimuthal projection about the facing
    direction), so no seam crosses the cheeks, blush or mouth."""
    unwrap(face, 0.006)
    me = face.data
    uv = me.uv_layers.active.data
    skull = np.zeros(len(me.polygons), dtype=np.int32)
    me.attributes["skull"].data.foreach_get("value", skull)
    area3 = area_uv = 0.0
    for poly in me.polygons:
        if skull[poly.index]:
            continue
        pts = [uv[li].uv for li in poly.loop_indices]
        area_uv += abs(sum(a.x * b.y - b.x * a.y for a, b in zip(pts, pts[1:] + pts[:1]))) * 0.5
        area3 += poly.area
    k = math.sqrt(area_uv / area3)  # UV units per metre used by the other islands
    hc = head_c
    for poly in me.polygons:
        if not skull[poly.index]:
            continue
        for li in poly.loop_indices:
            d = (me.vertices[me.loops[li].vertex_index].co - hc).normalized()
            theta = math.acos(max(-1.0, min(1.0, -d.y)))
            phi = math.atan2(d.z, d.x)
            rad = k * head_r * theta
            uv[li].uv = (5.0 + rad * math.cos(phi), 5.0 + rad * math.sin(phi))
    activate(face)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=False, margin=0.006)
    bpy.ops.object.mode_set(mode="OBJECT")


def split_groups(char, atlas):
    out = {}
    for gi, g in enumerate(char.groups):
        o = atlas.copy()
        o.data = atlas.data.copy()
        bpy.context.scene.collection.objects.link(o)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        layer = bm.faces.layers.int["grp"]
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[layer] != gi], context="FACES")
        bm.to_mesh(o.data)
        bm.free()
        o.name = o.data.name = char.name + "_" + g
        used = {g_.group for v in o.data.vertices for g_ in v.groups}
        for vg in [vg for vg in o.vertex_groups if vg.index not in used]:
            o.vertex_groups.remove(vg)
        out[g] = o
    bpy.data.objects.remove(atlas, do_unlink=True)
    return out


# --- 5. bake -------------------------------------------------------------------------------------------------

def bake_setup(samples):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = False
    scene.render.bake.use_selected_to_active = False
    scene.render.bake.margin_type = "EXTEND"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Bake")
    scene.world.light_settings.distance = 0.07


def bake(objs, image, kind, visible, margin, clear=True):
    """Bake `kind` of each object into `image`; only `visible[obj]` objects occlude (AO)."""
    scene = bpy.context.scene
    everything = [o for o in scene.objects if o.type == "MESH"]
    for i, obj in enumerate(objs):
        for mat in obj.data.materials:
            nodes = mat.node_tree.nodes
            node = nodes.get("BakeTarget") or nodes.new("ShaderNodeTexImage")
            node.name = "BakeTarget"
            node.image = image
            nodes.active = node
        for o in everything:
            o.hide_render = o not in visible.get(obj, everything)
        activate(obj)
        args = dict(margin=margin, use_clear=clear and i == 0, margin_type="EXTEND")
        if kind == "COLOR":
            bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, **args)
        elif kind == "NORMAL":
            bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", **args)
        else:
            bpy.ops.object.bake(type="AO", **args)
    for o in everything:
        o.hide_render = False


def pixels(image):
    px = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
    image.pixels.foreach_get(px)
    return px.reshape(image.size[1], image.size[0], 4)


def save_png(image, px, path):
    image.pixels.foreach_set(px.ravel())
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    return path


def bake_all(char, meshes, face, visible, out, tex, face_tex, samples, ao_samples, ao_strength=0.4):
    """Bake <Name>_BaseColor/_Normal (atlas meshes) and <Name>_Face, then give the meshes their final materials.
    `visible`: {object: [objects that occlude it in the AO pass]}."""
    import os
    name = char.name
    bake_setup(samples)
    atlas_objs = [meshes[g] for g in char.groups]
    results = {}
    for label, objs, size in (("atlas", atlas_objs, tex), ("face", [face], face_tex)):
        margin = max(2, size // 512)
        colour = bpy.data.images.new(f"{name}_{label}_colour", size, size, alpha=False)
        char.log("bake colour", label, size)
        bake(objs, colour, "COLOR", {}, margin)
        rgb = pixels(colour).copy()
        ao = bpy.data.images.new(f"{name}_{label}_ao", size, size, alpha=False, is_data=True)
        bpy.context.scene.cycles.samples = ao_samples
        char.log("bake ao", label)
        bake(objs, ao, "AO", visible, margin)
        bpy.context.scene.cycles.samples = samples
        shade = pixels(ao)[:, :, :3]
        # sRGB-encoded bake: darken creases a little, tinted toward the night purple rather than grey
        tint = np.array((0.80, 0.74, 0.92), dtype=np.float32)
        k = (ao_strength if label == "atlas" else ao_strength * 0.5) * (1.0 - np.clip(shade, 0.0, 1.0))
        rgb[:, :, :3] *= 1.0 - k * (1.0 - tint * 0.55)
        rgb[:, :, 3] = 1.0
        bpy.data.images.remove(ao)
        results[label] = [colour, rgb]
    normal = bpy.data.images.new(f"{name}_atlas_normal", tex, tex, alpha=False, is_data=True)
    char.log("bake normal")
    bake(atlas_objs, normal, "NORMAL", {}, max(2, tex // 512))
    npx = pixels(normal).copy()
    npx[:, :, 3] = 1.0
    os.makedirs(out, exist_ok=True)
    paths = {
        "base": save_png(results["atlas"][0], results["atlas"][1], os.path.join(out, f"{name}_BaseColor.png")),
        "face": save_png(results["face"][0], results["face"][1], os.path.join(out, f"{name}_Face.png")),
        "normal": save_png(normal, npx, os.path.join(out, f"{name}_Normal.png")),
    }

    # final materials: one per map (Unity ignores them, but they name the sub-meshes and make the FBX readable)
    def final(mat_name, colour_img, normal_img):
        mat = bpy.data.materials.new(mat_name)
        mat.use_nodes = True
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        bsdf = nodes["Principled BSDF"]
        t = nodes.new("ShaderNodeTexImage")
        t.image = colour_img
        links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.85
        if normal_img is not None:
            tn = nodes.new("ShaderNodeTexImage")
            tn.image = normal_img
            nm = nodes.new("ShaderNodeNormalMap")
            links.new(tn.outputs["Color"], nm.inputs["Color"])
            links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
        return mat
    atlas_mat = final(name + "_Atlas", results["atlas"][0], normal)
    face_mat = final(name + "_Face", results["face"][0], None)
    for obj, mat in [(o, atlas_mat) for o in atlas_objs] + [(face, face_mat)]:
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        for p in obj.data.polygons:
            p.material_index = 0
    for mat in [m for m in bpy.data.materials if m.users == 0]:
        bpy.data.materials.remove(mat)
    return paths


# --- 6. animation + export -----------------------------------------------------------------------------------

def key_clips(rig_obj, clips, pose_at, fps):
    """One action per clip. clips: [(name, pose function, frames, loops, ground speed, note)].
    `pose_at(pose)` -> ({bone: 3x3 delta in parent-carried character axes}, {bone: offset Vector}); every bone
    gets rotation keys on every frame, and the bones with offsets (the same ones on every frame) location keys."""
    rest = {b.name: b.matrix_local.to_3x3() for b in rig_obj.data.bones}
    for act in list(bpy.data.actions):
        bpy.data.actions.remove(act)
    rig_obj.animation_data_create()
    report = []
    for name, fn, frames, loop, speed, note in clips:
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        quats = {b: [] for b in rest}
        locs = {}
        for f in range(frames + 1):
            u = (f % frames) / frames if loop else f / frames
            D, offsets = pose_at(fn(u))
            for b, B in rest.items():
                q = (B.inverted() @ D.get(b, Matrix.Identity(3)) @ B).to_quaternion()
                if quats[b] and quats[b][-1].dot(q) < 0:
                    q.negate()
                quats[b].append(q)
            for b, offset in offsets.items():
                locs.setdefault(b, []).append(rest[b].inverted() @ offset)

        def curve(path, index, group, values):
            fc = act.fcurves.new(path, index=index, action_group=group)
            fc.keyframe_points.add(len(values))
            fc.keyframe_points.foreach_set("co", [x for f, v in enumerate(values) for x in (float(f), v)])
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
            fc.update()
        for b in rest:
            for i in range(4):
                curve(f'pose.bones["{b}"].rotation_quaternion', i, b, [q[i] for q in quats[b]])
        for b, values in locs.items():
            for i in range(3):
                curve(f'pose.bones["{b}"].location', i, b, [v[i] for v in values])
        report.append({"name": name, "frames": frames, "fps": fps, "seconds": round(frames / fps, 3),
                       "loop": loop, "ground_speed_mps": speed, "note": note})
    return report


def export_fbx(rig_obj, meshes, path, fps):
    rig_obj.animation_data.action = None
    for pb in rig_obj.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.scene.render.fps = fps
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    activate(rig_obj, meshes)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
        axis_forward="-Z", axis_up="Y", bake_space_transform=False,
        use_mesh_modifiers=True, mesh_smooth_type="OFF", use_tspace=False,
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False,
        armature_nodetype="NULL",
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0, path_mode="STRIP", embed_textures=False)
