"""Stylized-human anatomy kit for Spooky Street's cast (spec §2, §6 premium bar; decision D18).

Humans in Halloween costumes, not creatures: shaped limbs with elbows, knees and calves, five-fingered hands
with nails, real sneakers (sole, toe cap, tongue, laces, padded collar), and a child's head: big round cranium,
full cheeks, short chin, large low-set eyes, button nose, small open smile. Children ≈ 4.5 heads tall, adults ≈ 6.

Conventions: metres, Z up, characters face -Y, feet on Z = 0. Everything is procedural and original.
"""
import math

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector
from mathutils.bvhtree import BVHTree

import pip_model as pm  # shared mesh helpers (ellipsoid, torus, curve_tube, link, new_mesh_object)


class Proportions:
    """Joint landmarks for one body. `h` is standing height to the top of the skull (not hair)."""

    def __init__(self, h=1.2, child=True, build=1.0, belly=1.0, heads=None, neck=1.0):
        self.h = h
        self.child = child
        heads = heads or (4.5 if child else 6.0)
        self.head_h = h / heads
        self.head_r = self.head_h * 0.5
        u = h  # landmarks as fractions of height
        if child:
            f = dict(ankle=0.06, knee=0.27, hip=0.49, pelvis=0.5, waist=0.58, chest=0.67, shoulder=0.72,
                     neck=0.765, elbow=0.56, wrist=0.43)
        else:
            f = dict(ankle=0.055, knee=0.29, hip=0.52, pelvis=0.53, waist=0.62, chest=0.75, shoulder=0.825,
                     neck=0.83, elbow=0.61, wrist=0.47)
        self.z = {k: v * u for k, v in f.items()}
        self.head_c = Vector((0.0, 0.0, h - self.head_r * 1.02))
        w = build
        self.hip_x = (0.055 if child else 0.09) * h / (1.2 if child else 1.65) * w
        self.shoulder_x = (0.115 if child else 0.19) * h / (1.2 if child else 1.65) * w
        self.limb = (0.056 if child else 0.072) * h / (1.2 if child else 1.65)
        self.torso = (0.108 if child else 0.16) * h / (1.2 if child else 1.65) * w
        self.belly = belly
        self.neck = neck  # neck thickness factor
        self.arm_out = 0.24  # radians away from vertical (relaxed A-pose for rigging)

    def wrist(self, side):
        z = self.z
        drop = z["shoulder"] - z["wrist"]
        return Vector((side * (self.shoulder_x + math.sin(self.arm_out) * drop * 0.95), -0.012 * self.h, z["wrist"]))

    def elbow(self, side):
        z = self.z
        drop = z["shoulder"] - z["elbow"]
        return Vector((side * (self.shoulder_x + math.sin(self.arm_out) * drop * 0.9), 0.0, z["elbow"]))


def probe(obj):
    """Ray-cast onto `obj` with its modifiers applied: cast(origin, direction) -> (point, outward normal) or None.

    The object must sit at the world origin with no transform (true for everything this kit builds in place)."""
    bpy.context.view_layer.update()
    tree = BVHTree.FromObject(obj, bpy.context.evaluated_depsgraph_get())

    def cast(origin, direction):
        d = Vector(direction).normalized()
        loc, normal, _index, _dist = tree.ray_cast(Vector(origin), d)
        if loc is None:
            return None
        return loc, (-normal if normal.dot(d) > 0 else normal)
    return cast


def mount(name, point, normal, children, roll=0.0):
    """Parent `children` (built around the origin, facing -Y) to a pivot at `point` whose -Y follows `normal`."""
    pivot = bpy.data.objects.new(name, None)
    pm.link(pivot)
    pivot.location = point
    quat = Vector(normal).to_track_quat("-Y", "Z")
    pivot.rotation_euler = (quat.to_matrix() @ Euler((0, roll, 0)).to_matrix()).to_euler()
    for child in children:
        child.parent = pivot
    return pivot


def patch(name, mat, points, thickness, fan=True):
    """Thin patch through `points` (a closed loop facing -Y): filled from its centroid, or, for concave
    outlines, as one polygon (`fan=False`)."""
    area = sum(a.x * b.z - b.x * a.z for a, b in zip(points, points[1:] + points[:1]))
    if area < 0:
        points = points[::-1]
    bm = bmesh.new()
    ring = [bm.verts.new(p) for p in points]
    if fan:
        centre = bm.verts.new(sum(points, Vector()) / len(points))
        for i in range(len(ring)):
            bm.faces.new((centre, ring[i], ring[(i + 1) % len(ring)]))
    else:
        bmesh.ops.triangulate(bm, faces=[bm.faces.new(ring)])
    obj = pm.new_mesh_object(name, bm, mat)
    obj.modifiers.new("Thick", "SOLIDIFY").thickness = thickness
    return obj


def _dome_patch(name, mat, radii, y0, centre, size, lift, rings=5, segments=28):
    """Elliptical patch hugging the front (-Y) of an ellipsoid of `radii` centred at (0, y0, 0)."""
    rx, ry, rz = radii
    bm = bmesh.new()

    def vert(u, w):
        q = max(0.0, 1 - (u / rx) ** 2 - (w / rz) ** 2)
        return bm.verts.new((u, y0 - ry * lift * math.sqrt(q), w))
    mid = vert(*centre)
    prev = None
    for k in range(1, rings + 1):
        f = k / rings
        ring = [vert(centre[0] + size[0] * f * math.cos(2 * math.pi * i / segments),
                     centre[1] + size[1] * f * math.sin(2 * math.pi * i / segments)) for i in range(segments)]
        for i in range(segments):
            j = (i + 1) % segments
            if prev is None:
                bm.faces.new((mid, ring[i], ring[j]))
            else:
                bm.faces.new((prev[i], ring[i], ring[j], prev[j]))
        prev = ring
    return pm.new_mesh_object(name, bm, mat)


def _skin_mesh(name, nodes, edges, root, subsurf=2):
    names = list(nodes)
    me = bpy.data.meshes.new(name + "Skin")
    me.from_pydata([tuple(nodes[n][0]) for n in names], [(names.index(a), names.index(b)) for a, b in edges], [])
    obj = bpy.data.objects.new(name + "Skin", me)
    pm.link(obj)
    skin = obj.modifiers.new("Skin", "SKIN")
    skin.use_smooth_shade = True
    skin.branch_smoothing = 0.8
    if not me.skin_vertices:
        me.skin_vertices.new()
    for i, n in enumerate(names):
        sv = me.skin_vertices[0].data[i]
        r = nodes[n][1]
        sv.radius = (r, r) if isinstance(r, (int, float)) else r
        sv.use_root = n == root
    obj.modifiers.new("Smooth", "SUBSURF").levels = subsurf
    deps = bpy.context.evaluated_depsgraph_get()
    baked = bpy.data.meshes.new_from_object(obj.evaluated_get(deps))
    bpy.data.objects.remove(obj, do_unlink=True)
    out = bpy.data.objects.new(name, baked)
    pm.link(out)
    for p in baked.polygons:
        p.use_smooth = True
    return out


def body(name, P, materials, classify):
    """Torso, neck, arms to the wrist and legs to the ankle as one smooth mesh; regions get materials."""
    z, L, T = P.z, P.limb, P.torso
    n = {
        "pelvis": ((0, 0, z["pelvis"]), (T * 1.05, T * 0.8)),
        "waist": ((0, -0.004, z["waist"]), (T * 0.95 * P.belly, T * 0.75 * P.belly)),
        "chest": ((0, 0, z["chest"]), (T * 1.05, T * 0.78)),
        "neck": ((0, 0, z["neck"]), L * 0.68 * P.neck),
        "neck_top": ((0, 0.004, z["neck"] + P.head_r * 0.55), L * 0.62 * P.neck),
    }
    edges = [("pelvis", "waist"), ("waist", "chest"), ("chest", "neck"), ("neck", "neck_top")]
    for side, s in (("L", 1), ("R", -1)):
        sh = Vector((s * P.shoulder_x, 0, z["shoulder"]))
        el = P.elbow(s)
        wr = P.wrist(s)
        hip = Vector((s * P.hip_x, 0, z["hip"]))
        knee = Vector((s * P.hip_x * 1.02, -0.006, z["knee"]))
        ankle = Vector((s * P.hip_x * 1.0, 0.004, z["ankle"]))
        n.update({
            "shoulder" + side: (sh, L * getattr(P, "shoulder_r", 0.86)),
            "puff" + side: (sh.lerp(el, 0.28), L * 0.9),  # softly puffed sleeve head
            "bicep" + side: (sh.lerp(el, 0.66), L * 0.68),
            "elbow" + side: (el + Vector((0, 0.014, 0)), L * 0.54),
            "forearm" + side: (el.lerp(wr, 0.4) + Vector((0, 0.004, 0)), L * 0.7),
            "sleeve" + side: (el.lerp(wr, 0.78), L * 0.78),  # sleeve fullness gathered into the cuff
            "wrist" + side: (wr, L * 0.5),
            "hip" + side: (hip, L * 1.05),
            "thigh" + side: (hip.lerp(knee, 0.45), L * 0.98),
            "knee" + side: (knee, L * 0.78),
            "calf" + side: (knee.lerp(ankle, 0.33) + Vector((0, 0.012, 0)), L * 0.84),
            "shin" + side: (knee.lerp(ankle, 0.75), L * 0.6),
            "ankle" + side: (ankle, L * 0.52),
        })
        edges += [("chest", "shoulder" + side), ("shoulder" + side, "puff" + side), ("puff" + side, "bicep" + side),
                  ("bicep" + side, "elbow" + side), ("elbow" + side, "forearm" + side),
                  ("forearm" + side, "sleeve" + side), ("sleeve" + side, "wrist" + side),
                  ("pelvis", "hip" + side), ("hip" + side, "thigh" + side), ("thigh" + side, "knee" + side),
                  ("knee" + side, "calf" + side), ("calf" + side, "shin" + side), ("shin" + side, "ankle" + side)]
    obj = _skin_mesh(name, n, edges, "pelvis")
    for m in materials:
        obj.data.materials.append(m)
    for poly in obj.data.polygons:
        poly.material_index = classify(poly.center)
    return obj


def hand(name, P, side, skin, nail, curl=0.35, size=1.0):
    """Five-fingered child's hand hanging from the wrist, palm facing the thigh.

    The thumb and each finger are separate meshes rooted inside the palm, so the fingers stay apart down to
    the knuckles. `curl` is the total finger bend in radians (0.3 relaxed, 1.5 gripping).
    Returns (pivot, grip): the world point inside the curled fingers where a held handle passes."""
    s = P.head_r / 0.13 * (1.0 if P.child else 1.3) * size
    wrap = min(1.0, curl / 1.5)
    parts = [pm.ellipsoid(name + "Palm", skin, (0, 0, -0.034 * s), (0.0145 * s, 0.031 * s, 0.028 * s), subsurf=1),
             _skin_mesh(name + "Wrist", {"a": ((0, 0, 0.004 * s), (0.014 * s, 0.018 * s)),
                                         "b": ((0, 0, -0.022 * s), (0.013 * s, 0.022 * s))}, [("a", "b")], "a")]
    thumb = [Vector((-0.003, -0.016, -0.026)) * s, Vector((-0.005, -0.029, -0.03)) * s,
             Vector((-0.01 - 0.008 * wrap, -0.043, -0.04)) * s,
             Vector((-0.014 - 0.022 * wrap, -0.05 + 0.014 * wrap, -0.057)) * s]
    parts.append(_skin_mesh(name + "Thumb", {f"t{k}": (p, rad * s) for k, (p, rad) in
                                             enumerate(zip(thumb, (0.008, 0.0098, 0.0088, 0.008)))},
                            [(f"t{k}", f"t{k + 1}") for k in range(3)], "t0"))
    fingers = [(-0.0235, -0.052, 0.042), (-0.0078, -0.055, 0.048), (0.0078, -0.054, 0.044), (0.0235, -0.05, 0.035)]
    fan = 0.1 * (1 - wrap) + 0.02  # relaxed fingers spread a little, gripping fingers close up
    tips, holds = [], []
    for i, (fy, fz, fl) in enumerate(fingers):  # index finger first: -Y is the character's front
        root = Vector((0, fy * s * 0.96, (fz + 0.012) * s))  # buried in the palm
        pos = Vector((0.002 * s, fy * s, fz * s))  # the knuckle stands a little proud of the back of the hand
        chain = {"root": (root, 0.0062 * s), "k0": (pos.copy(), 0.0074 * s)}
        links = [("root", "k0")]
        bend = 0.0
        for j in range(1, 4):
            bend = curl * (j - 0.4) / 2.6
            seg = fl * s / 3
            pos = pos + Vector((-math.sin(bend) * seg, fy * s * fan, -math.cos(bend) * seg))
            chain[f"k{j}"] = (pos.copy(), (0.0068 - 0.0003 * j) * s)
            links.append((f"k{j - 1}", f"k{j}"))
        parts.append(_skin_mesh(f"{name}Finger{i}", chain, links, "root"))
        tips.append((pos.copy(), bend))
        holds.append((Vector(chain["k0"][0]) + pos) * 0.5)
    for part in parts[1:]:
        part.data.materials.append(skin)
    for tip, bend in tips + [(thumb[3], 0.35 * wrap)]:
        along = Vector((-math.sin(bend), 0, -math.cos(bend)))
        back = Vector((math.cos(bend), 0, -math.sin(bend)))
        c = tip - along * 0.0045 * s + back * 0.0048 * s
        parts.append(pm.ellipsoid(name + "Nail", nail, c, (0.0012 * s, 0.0038 * s, 0.0044 * s),
                                  rotation=(0, bend, 0), segments=12, rings=8, subsurf=1))
    pivot = bpy.data.objects.new(name + "Pivot", None)
    pm.link(pivot)
    for part in parts:
        part.parent = pivot
    # Local hand: hangs along -Z, palm faces -X. The left hand (x > 0) already faces the body; the right hand is
    # mirrored. Then tilt to continue the arm's outward angle.
    pivot.location = P.wrist(side)
    pivot.rotation_euler = (0, -side * P.arm_out * 0.85, 0)
    pivot.scale = (side, 1, 1)
    hold = (holds[1] + holds[2]) * 0.5
    hold.x -= 0.004 * s
    return pivot, pivot.matrix_basis @ hold


def sneaker(name, P, side, upper, rubber, accent, lace):
    """Chunky child's sneaker: rubber sole and toe cap, mid-top upper, tongue panel, crossed laces, padded collar."""
    L = (0.19 if P.child else 0.25) * P.head_r / 0.13
    W = L * 0.46
    x0 = side * P.hip_x
    y0 = -L * 0.27  # the foot extends forward of the ankle
    sole_h = L * 0.13
    ankle_y = 0.004

    def top(xn, yn, lift=0.0):
        """Point on the upper for unit-disc coordinates (yn = -1 toe … +1 heel), pushed out by `lift`."""
        t = (yn + 1) / 2
        rise = min(1.0, max(0.0, (t - 0.3) / 0.45))
        height = L * (0.2 + 0.24 * rise * rise * (3 - 2 * rise))  # low toe box, high heel counter
        width = W * 0.5 * (1 - 0.1 * max(0.0, -yn) ** 2)
        dome = math.sqrt(max(0.0, 1 - xn * xn - yn * yn))
        blunt = math.copysign(abs(yn) ** 0.75, yn)
        return Vector((x0 + xn * (width + lift), y0 + blunt * (L * 0.49 + lift), sole_h * 0.8 + dome * (height + lift)))

    outline = pm.smooth_outline([(0.0, -L * 0.52), (W * 0.44, -L * 0.4), (W * 0.53, -L * 0.05), (W * 0.44, L * 0.4),
                                 (0.0, L * 0.52), (-W * 0.44, L * 0.4), (-W * 0.5, -L * 0.05), (-W * 0.44, -L * 0.4)],
                                6)
    bm = bmesh.new()
    bm.faces.new([bm.verts.new((side * px + x0, py + y0, 0.0)) for px, py in outline])
    res = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
    bmesh.ops.translate(bm, verts=[e for e in res["geom"] if isinstance(e, bmesh.types.BMVert)], vec=(0, 0, sole_h))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bev = pm.new_mesh_object(name + "Sole", bm, rubber, smooth=False).modifiers.new("Bevel", "BEVEL")
    bev.width = L * 0.035
    bev.segments = 3

    def shell(part, mat, keep, lift):
        bm = bmesh.new()
        # Poles on the toe-heel axis, so the toe cap and heel counter end on clean rings.
        bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=24, radius=1.0,
                                  matrix=Matrix.Rotation(math.pi / 2, 4, "X"))
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if not keep(f.calc_center_median())], context="FACES")
        for v in bm.verts:
            v.co = top(v.co.x, v.co.y, lift) if v.co.z >= 0 else Vector((top(v.co.x, v.co.y, lift).x,
                                                                         top(v.co.x, v.co.y, lift).y, sole_h * 0.8))
        obj = pm.new_mesh_object(name + part, bm, mat)
        obj.modifiers.new("Smooth", "SUBSURF").levels = 1
        return obj
    shell("Upper", upper, lambda c: c.z > -0.2, 0.0)
    shell("ToeCap", rubber, lambda c: c.z > -0.2 and c.y < -0.58, L * 0.012)
    shell("HeelCounter", accent, lambda c: c.z > -0.2 and c.y > 0.72, L * 0.01)
    # Tongue panel under the laces, running up the instep to the ankle.
    bm = bmesh.new()
    rows = [[bm.verts.new(top(xn, yn, L * 0.012)) for xn in (-0.24, -0.12, 0.0, 0.12, 0.24)]
            for yn in (-0.5, -0.36, -0.22, -0.08, 0.06, 0.2)]
    for a, b in zip(rows, rows[1:]):
        for i in range(4):
            bm.faces.new((a[i], b[i], b[i + 1], a[i + 1]))
    tongue = pm.new_mesh_object(name + "Tongue", bm, accent)
    tongue.modifiers.new("Thick", "SOLIDIFY").thickness = L * 0.02
    tongue.modifiers.new("Smooth", "SUBSURF").levels = 1
    for i in range(4):
        yn = -0.44 + i * 0.15
        for d in (1, -1):
            pm.curve_tube(name + "Lace", lace, [top(-d * 0.23, yn, L * 0.016), top(0.0, yn + 0.065, L * 0.024),
                                                top(d * 0.23, yn + 0.13, L * 0.016)], L * 0.013, taper=(1, 1),
                          resolution=4)
    ankle = Vector((x0, ankle_y, P.z["ankle"] + L * 0.14))
    pm.torus(name + "Collar", accent, ankle - Vector((0, 0, L * 0.03)), P.limb * 0.7, L * 0.05, squash=0.8,
             segments=28, sides=10)
    pm.ellipsoid(name + "HeelTab", upper, (x0, y0 + L * 0.5, L * 0.5), (W * 0.1, L * 0.02, L * 0.07), subsurf=1)


def head(name, P, skin, sclera, iris, pupil, line, mouth_inside, tongue, fang=None, brow=None, freckle=None,
         ears=True, adult=False, mouth="open", teeth=None):
    """A child's head: round cranium, full cheeks, short soft chin, big low-set eyes, button nose, open smile.

    `adult` keeps the friendly eyes but lengthens the face, tapers the jaw, raises and shrinks the eyes a little
    and grows the nose. `mouth` is "open" (small open smile), "grin" (wide, with `teeth`) or "smile" (closed).
    Returns surface(x, z, out): the point on the face at offsets (x, z) from the head centre, lifted by `out`."""
    r = P.head_r
    hc = P.head_c
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=32, radius=1.0)
    for v in bm.verts:
        x, y, zz = v.co
        if zz < 0:
            d = -zz
            cheek = math.exp(-((d - 0.42) / 0.3) ** 2)
            if adult:
                x *= 1 + 0.03 * cheek - 0.24 * d ** 2.2  # a longer face with a defined jaw
                zz *= 1.02
            else:
                x *= 1 + 0.08 * cheek - 0.12 * d ** 3  # full cheeks, barely tapering to a soft chin
                zz *= 0.9  # short lower face
            if y < 0:
                y *= 1 + 0.05 * cheek
        v.co = Vector((x * r * (0.95 if adult else 1.0), y * r * 0.97, zz * r)) + hc
    skull = pm.new_mesh_object(name + "Head", bm, skin)
    skull.modifiers.new("Smooth", "SUBSURF").levels = 1
    cast = probe(skull)

    def frame(x, zz):
        return cast(hc + Vector((x, -3 * r, zz)), (0, 1, 0))

    def surface(x, zz, out=0.0):
        point, normal = frame(x, zz)
        return point + normal * out

    def loop(points, out):
        return [surface(r * px, r * pz, r * out) for px, pz in points]

    eye_x, eye_z = (r * 0.35, r * 0.02) if adult else (r * 0.37, -r * 0.1)
    e = r * (0.8 if adult else 1.0)  # eye size
    er = (e * 0.19, r * 0.075, e * 0.225)
    y0 = r * 0.045  # the eye sits in the head, only a shallow dome shows
    for s in (1, -1):
        point, normal = frame(s * eye_x, eye_z)
        parts = [pm.ellipsoid(name + "Sclera", sclera, (0, y0, 0), er, segments=32, rings=20)]
        gaze = (-s * e * 0.02, -e * 0.012)
        parts.append(_dome_patch(name + "Iris", iris, er, y0, gaze, (e * 0.118, e * 0.142), 1.03))
        parts.append(_dome_patch(name + "Pupil", pupil, er, y0, gaze, (e * 0.062, e * 0.078), 1.06))
        for du, dw, size in ((-e * 0.05, e * 0.062, e * 0.04), (e * 0.045, -e * 0.07, e * 0.018)):
            parts.append(_dome_patch(name + "Glint", sclera, er, y0, (gaze[0] + du, gaze[1] + dw), (size, size),
                                     1.09, rings=2, segments=16))
        # A thin, soft upper lash line: no liner, no lower lid.
        lash = []
        for k in range(9):
            a = math.radians(158 - 136 * k / 8)
            u, w = 0.8 * math.cos(a), 0.8 * math.sin(a)  # the rim of the dome where it meets the skin
            lash.append((s * er[0] * u, y0 - er[1] * 0.6, er[2] * w))
        parts.append(pm.curve_tube(name + "Lash", line, lash, r * 0.013, taper=(0.5, 1.0)))
        mount(name + "Eye", point, normal.lerp(Vector((0, -1, 0)), 0.4), parts)
        if brow is not None:
            arc = [surface(s * r * dx, eye_z + e * dz, r * 0.004) for dx, dz in ((0.22, 0.36), (0.36, 0.385),
                                                                                 (0.5, 0.35))]
            pm.curve_tube(name + "Brow", brow, arc, r * (0.024 if adult else 0.016), taper=(1.0, 0.6))
        if ears:
            ear_c = hc + Vector((s * r * 0.97, r * 0.08, -r * 0.14))
            pm.ellipsoid(name + "Ear", skin, ear_c, (r * 0.07, r * 0.11, r * 0.16), rotation=(0, 0, s * 0.35))
    if adult:
        pm.ellipsoid(name + "Nose", skin, surface(0, -r * 0.27, -r * 0.01), (r * 0.085, r * 0.08, r * 0.1))
    else:
        pm.ellipsoid(name + "Nose", skin, surface(0, -r * 0.33, -r * 0.012), (r * 0.062, r * 0.055, r * 0.05))
    wide = {"open": 1.0, "grin": 1.45, "smile": 1.2}[mouth]
    drop = -0.07 if adult else 0.0

    def lips(points, out):
        return loop([(px * wide, pz + drop) for px, pz in points], out)
    lip = [(-0.15, -0.465), (-0.08, -0.49), (0.0, -0.497), (0.08, -0.49), (0.15, -0.465)]
    if mouth != "smile":
        # Open smile: dark mouth, tongue, and either two tiny fangs or a row of teeth along the upper lip.
        opening = lip + [(0.125, -0.535), (0.065, -0.6), (0.0, -0.618), (-0.065, -0.6), (-0.125, -0.535)]
        patch(name + "Mouth", mouth_inside, lips(opening, 0.003), r * 0.006)
        patch(name + "Tongue", tongue, lips([(0.06 * math.cos(t), -0.578 + 0.028 * math.sin(t)) for t in
                                            [2 * math.pi * i / 14 for i in range(14)]], 0.006), r * 0.004)
        if teeth is not None:
            patch(name + "Teeth", teeth, lips(lip[1:-1] + [(0.105, -0.478), (0.09, -0.525), (0.0, -0.537),
                                                          (-0.09, -0.525), (-0.105, -0.478)], 0.007), r * 0.004)
        if fang is not None:
            for s in (1, -1):
                pm.ellipsoid(name + "Fang", fang, surface(s * r * 0.078, -r * 0.518, r * 0.004),
                             (r * 0.024, r * 0.014, r * 0.04), subsurf=1)
    pm.curve_tube(name + "Smile", line, lips([(-0.205, -0.432), (-0.185, -0.452)] + lip + [(0.185, -0.452),
                                                                                            (0.205, -0.432)], 0.008),
                  r * 0.012, taper=(0.6, 0.6))
    if freckle is not None:
        for s in (1, -1):
            for dx, dz in ((0.36, -0.3), (0.46, -0.26), (0.54, -0.34), (0.42, -0.38)):
                pm.ellipsoid(name + "Freckle", freckle, surface(s * r * dx, r * dz, -r * 0.004),
                             (r * 0.02, r * 0.008, r * 0.02), subsurf=0)
    return surface


def hair_clump(name, mat, points, radius, taper=(1.0, 0.1)):
    return pm.curve_tube(name, mat, points, radius, taper=taper, resolution=8)
