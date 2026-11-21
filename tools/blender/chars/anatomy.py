"""Stylized-human anatomy kit for Spooky Street's cast (spec §2, §6 premium bar; decision D18).

Humans in Halloween costumes, not creatures: shaped limbs with elbows, knees and calves, five-fingered hands
with nails, real shoes (sole, toe cap, laces, tongue), a head with jaw, ears, eyes with iris and lid line,
nose and mouth, and hair built from clumps. Children ≈ 4.5 heads tall, adults ≈ 6.

Conventions: metres, Z up, characters face -Y, feet on Z = 0. Everything is procedural and original.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

import pip_model as pm  # shared mesh helpers (ellipsoid, torus, curve_tube, link, new_mesh_object)


class Proportions:
    """Joint landmarks for one body. `h` is standing height to the top of the skull (not hair)."""

    def __init__(self, h=1.2, child=True, build=1.0, belly=1.0):
        self.h = h
        self.child = child
        heads = 4.5 if child else 6.0
        self.head_h = h / heads
        self.head_r = self.head_h * 0.5
        u = h  # landmarks as fractions of height
        if child:
            f = dict(ankle=0.06, knee=0.27, hip=0.49, pelvis=0.5, waist=0.58, chest=0.67, shoulder=0.72,
                     neck=0.765, elbow=0.56, wrist=0.43)
        else:
            f = dict(ankle=0.055, knee=0.29, hip=0.52, pelvis=0.53, waist=0.62, chest=0.72, shoulder=0.79,
                     neck=0.83, elbow=0.61, wrist=0.47)
        self.z = {k: v * u for k, v in f.items()}
        self.head_c = Vector((0.0, 0.0, h - self.head_r * 1.02))
        w = build
        self.hip_x = (0.055 if child else 0.09) * h / (1.2 if child else 1.65) * w
        self.shoulder_x = (0.115 if child else 0.19) * h / (1.2 if child else 1.65) * w
        self.limb = (0.056 if child else 0.072) * h / (1.2 if child else 1.65)
        self.torso = (0.108 if child else 0.16) * h / (1.2 if child else 1.65) * w
        self.belly = belly
        self.arm_out = 0.32  # radians away from vertical (relaxed A-pose for rigging)

    def wrist(self, side):
        z = self.z
        drop = z["shoulder"] - z["wrist"]
        return Vector((side * (self.shoulder_x + math.sin(self.arm_out) * drop * 0.95), -0.012 * self.h, z["wrist"]))

    def elbow(self, side):
        z = self.z
        drop = z["shoulder"] - z["elbow"]
        return Vector((side * (self.shoulder_x + math.sin(self.arm_out) * drop * 0.9), 0.0, z["elbow"]))


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
        "neck": ((0, 0, z["neck"]), L * 0.62),
        "neck_top": ((0, 0.004, z["neck"] + P.head_r * 0.55), L * 0.58),
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
            "shoulder" + side: (sh, L * 0.82),
            "bicep" + side: (sh.lerp(el, 0.45), L * 0.74),
            "elbow" + side: (el, L * 0.6),
            "forearm" + side: (el.lerp(wr, 0.35), L * 0.62),
            "wrist" + side: (wr, L * 0.46),
            "hip" + side: (hip, L * 1.05),
            "thigh" + side: (hip.lerp(knee, 0.45), L * 0.92),
            "knee" + side: (knee, L * 0.72),
            "calf" + side: (knee.lerp(ankle, 0.35) + Vector((0, 0.008, 0)), L * 0.76),
            "shin" + side: (knee.lerp(ankle, 0.75), L * 0.56),
            "ankle" + side: (ankle, L * 0.5),
        })
        edges += [("chest", "shoulder" + side), ("shoulder" + side, "bicep" + side), ("bicep" + side, "elbow" + side),
                  ("elbow" + side, "forearm" + side), ("forearm" + side, "wrist" + side),
                  ("pelvis", "hip" + side), ("hip" + side, "thigh" + side), ("thigh" + side, "knee" + side),
                  ("knee" + side, "calf" + side), ("calf" + side, "shin" + side), ("shin" + side, "ankle" + side)]
    obj = _skin_mesh(name, n, edges, "pelvis")
    for m in materials:
        obj.data.materials.append(m)
    for poly in obj.data.polygons:
        poly.material_index = classify(poly.center)
    return obj


def hand(name, P, side, skin, nail, curl=0.35):
    """Five-fingered stylized hand hanging from the wrist, palm facing the thigh, relaxed curl."""
    k = P.head_r / 0.13  # scale relative to a child's head
    length = 0.11 * k if P.child else 0.15 * k * 0.95
    s = length / 0.11
    nodes = {"wrist": ((0, 0, 0), 0.02 * s), "palm": ((0, 0, -0.032 * s), (0.024 * s, 0.024 * s))}
    edges = [("wrist", "palm")]
    fingers = [(-0.019, 0.042), (-0.0065, 0.047), (0.0065, 0.044), (0.018, 0.035)]  # (y offset, length)
    tips = []
    for i, (fy, fl) in enumerate(fingers):
        base = Vector((0, fy * s, -0.052 * s))
        nodes[f"f{i}0"] = (base, 0.0085 * s)
        edges.append(("palm", f"f{i}0"))
        prev = f"f{i}0"
        pos = base
        for j in range(1, 4):
            seg = fl * s / 3
            bend = curl * j / 3
            pos = pos + Vector((-math.sin(bend) * seg * 0.9, fy * s * 0.12, -math.cos(bend) * seg))
            key = f"f{i}{j}"
            nodes[key] = (pos.copy(), (0.0078 - 0.0008 * j) * s)
            edges.append((prev, key))
            prev = key
        tips.append(pos)
    thumb = [Vector((-0.006 * s, -0.022 * s, -0.016 * s)), Vector((-0.014 * s, -0.034 * s, -0.03 * s)),
             Vector((-0.02 * s, -0.04 * s, -0.045 * s))]
    nodes["t0"] = (thumb[0], 0.0105 * s)
    nodes["t1"] = (thumb[1], 0.0092 * s)
    nodes["t2"] = (thumb[2], 0.008 * s)
    edges += [("palm", "t0"), ("t0", "t1"), ("t1", "t2")]
    obj = _skin_mesh(name, nodes, edges, "wrist")
    obj.data.materials.append(skin)
    # Flatten the palm (hands are thinner than wide), then hang it from the wrist following the arm angle.
    for v in obj.data.vertices:
        v.co.x *= 0.72
    nails = []
    for i, tip in enumerate(tips + [thumb[2]]):
        nail_obj = pm.ellipsoid(name + "Nail", nail, (tip.x * 0.72 + 0.0055 * s, tip.y, tip.z + 0.004 * s),
                                (0.0035 * s, 0.0055 * s, 0.006 * s), subsurf=0)
        nails.append(nail_obj)
    pivot = bpy.data.objects.new(name + "Pivot", None)
    pm.link(pivot)
    obj.parent = pivot
    for nobj in nails:
        nobj.parent = pivot
    pivot.location = P.wrist(side)
    # Local hand: hangs along -Z, palm faces -X. The left hand (x > 0) already faces the body; the right hand is
    # mirrored. Then tilt to continue the arm's outward angle.
    angle = P.arm_out * 0.85
    if side > 0:
        pivot.rotation_euler = (0, -angle, 0)
    else:
        pivot.scale = (-1, 1, 1)
        pivot.rotation_euler = (0, angle, 0)
    return pivot


def sneaker(name, P, side, upper, sole, accent, lace):
    """Chunky stylized sneaker: rubber sole, rounded upper, toe cap, tongue, laces and heel tab."""
    k = P.head_r / 0.13
    L = (0.17 if P.child else 0.25) * k * (1.0 if P.child else 0.95)
    W = L * 0.44
    x = side * P.hip_x
    y0 = -L * 0.28  # foot extends forward of the ankle
    outline = pm.smooth_outline([(0.0, -L * 0.5), (W * 0.42, -L * 0.38), (W * 0.5, -L * 0.05), (W * 0.4, L * 0.38),
                                 (0.0, L * 0.5), (-W * 0.4, L * 0.38), (-W * 0.48, -L * 0.05), (-W * 0.42, -L * 0.38)],
                                6)
    bm = bmesh.new()
    verts = [bm.verts.new((px + x, py + y0, 0.0)) for px, py in outline]
    bm.faces.new(verts)
    res = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
    bmesh.ops.translate(bm, verts=[e for e in res["geom"] if isinstance(e, bmesh.types.BMVert)],
                        vec=(0, 0, L * 0.12))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    sole_obj = pm.new_mesh_object(name + "Sole", bm, sole, smooth=False)
    bev = sole_obj.modifiers.new("Bevel", "BEVEL")
    bev.width = L * 0.03
    bev.segments = 3
    # Upper: a sphere flattened at the bottom and pulled forward into a rounded toe.
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=18, radius=1.0)
    for v in bm.verts:
        cz = max(v.co.z, -0.15)
        front = max(0.0, -v.co.y)
        v.co = Vector((v.co.x * W * 0.47 * (1 - 0.15 * front), v.co.y * L * 0.47, cz * L * 0.2 * (1 - 0.35 * front)))
    up = pm.new_mesh_object(name + "Upper", bm, upper)
    up.location = (x, y0 + L * 0.02, L * 0.14)
    up.modifiers.new("Smooth", "SUBSURF").levels = 1
    pm.ellipsoid(name + "ToeCap", accent, (x, y0 - L * 0.33, L * 0.13), (W * 0.4, L * 0.16, L * 0.1), subsurf=1)
    pm.ellipsoid(name + "Collar", accent, (x, y0 + L * 0.28, L * 0.3), (W * 0.42, L * 0.16, L * 0.06), subsurf=1)
    pm.ellipsoid(name + "Tongue", upper, (x, y0 + L * 0.02, L * 0.31), (W * 0.22, L * 0.13, L * 0.035),
                 rotation=(math.radians(-25), 0, 0), subsurf=1)
    for i in range(3):
        yy = y0 - L * 0.12 + i * L * 0.075
        zz = L * 0.27 + i * L * 0.025
        pm.curve_tube(name + "Lace", lace, [(x - W * 0.2, yy, zz), (x, yy - L * 0.008, zz + L * 0.012),
                                            (x + W * 0.2, yy, zz)], L * 0.012, taper=(1, 1), resolution=4)
    pm.ellipsoid(name + "HeelTab", accent, (x, y0 + L * 0.47, L * 0.3), (W * 0.12, L * 0.03, L * 0.08), subsurf=1)


def head(name, P, skin, sclera, iris, ink, lid=None, mouth="smile", fangs=None, lips=None, brow=None,
         cheek=None, freckle=None):
    """Stylized-human head: skull with tapered jaw and chin, ears, eyes with iris and lash line, nose, mouth."""
    r = P.head_r
    hc = P.head_c
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=28, radius=1.0)
    for v in bm.verts:
        x, y, zz = v.co
        if zz < 0:
            taper = 1 - 0.28 * (-zz) ** 1.4  # jaw narrows toward the chin
            x *= taper
            y = y * (1 - 0.12 * (-zz)) - 0.1 * (-zz) ** 2 * (1 if y < 0 else 0)  # chin forward
        v.co = Vector((x * r * 0.93, y * r * 0.98, zz * r * 1.08)) + hc
    skull = pm.new_mesh_object(name + "Head", bm, skin)
    skull.modifiers.new("Smooth", "SUBSURF").levels = 1

    def surface(x, zz, out=0.0):
        """Point on the front of the skull at offsets (x, z) from the head centre, pushed out by `out`."""
        ex, ez = x / (r * 0.93), zz / (r * 1.08)
        q = max(0.0, 1 - ex * ex - ez * ez)
        return Vector((x, -math.sqrt(q) * r * 0.98 - out, zz)) + hc

    eye_x, eye_z = r * 0.36, r * 0.02
    for s in (1, -1):
        c = surface(s * eye_x, eye_z, -r * 0.02)
        pm.ellipsoid(name + "Sclera", sclera, c, (r * 0.2, r * 0.1, r * 0.22), rotation=(0, 0, -s * 0.1))
        pm.ellipsoid(name + "Iris", iris, c + Vector((0, -r * 0.075, -r * 0.01)), (r * 0.13, r * 0.04, r * 0.145))
        pm.ellipsoid(name + "Pupil", ink, c + Vector((0, -r * 0.1, -r * 0.01)), (r * 0.065, r * 0.02, r * 0.075),
                     subsurf=0)
        for dx, dz, rr in ((s * r * 0.05, r * 0.06, r * 0.038), (-s * r * 0.04, -r * 0.06, r * 0.018)):
            pm.ellipsoid(name + "Glint", sclera, c + Vector((dx, -r * 0.118, dz)), (rr, rr * 0.4, rr), subsurf=0)
        # Upper lash line hugging the eye: gives the face definition at phone size.
        lash = [c + Vector((s * r * dx, -r * 0.07, r * dz)) for dx, dz in
                ((-0.21, 0.02), (-0.1, 0.17), (0.05, 0.2), (0.18, 0.12), (0.25, 0.02))]
        pm.curve_tube(name + "Lash", ink, lash, r * 0.022, taper=(0.5, 1.2))
        if brow is not None:
            b = [surface(s * r * dx, r * dz, r * 0.015) for dx, dz in ((0.17, 0.34), (0.36, 0.4), (0.55, 0.33))]
            pm.curve_tube(name + "Brow", brow, b, r * 0.035, taper=(0.8, 0.5))
        if cheek is not None:
            pm.ellipsoid(name + "Cheek", cheek, surface(s * r * 0.48, -r * 0.27, -r * 0.012),
                         (r * 0.1, r * 0.02, r * 0.06), subsurf=1)
        # Ears with an inner fold.
        ear_c = hc + Vector((s * r * 0.92, r * 0.05, -r * 0.05))
        pm.ellipsoid(name + "Ear", skin, ear_c, (r * 0.09, r * 0.13, r * 0.2), rotation=(0, 0, s * 0.35))
        pm.ellipsoid(name + "EarInner", lid or skin, ear_c + Vector((s * r * 0.05, -r * 0.02, 0)),
                     (r * 0.04, r * 0.07, r * 0.12), rotation=(0, 0, s * 0.35))
    nose = surface(0, -r * 0.17, r * 0.06)
    pm.ellipsoid(name + "Nose", skin, nose, (r * 0.1, r * 0.08, r * 0.09))
    m = [surface(r * dx, -r * dz, -r * 0.0) for dx, dz in ((-0.24, 0.4), (-0.1, 0.47), (0.1, 0.47), (0.24, 0.4))]
    if mouth == "grin":
        m = [surface(r * dx, -r * dz) for dx, dz in ((-0.3, 0.38), (-0.12, 0.5), (0.12, 0.5), (0.3, 0.38))]
    pm.curve_tube(name + "Mouth", lips or ink, m, r * 0.028, taper=(0.7, 0.7))
    if fangs is not None:
        for s in (1, -1):
            f = surface(s * r * 0.1, -r * 0.5, r * 0.012)
            bm = bmesh.new()
            bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=r * 0.045, radius2=0.0, depth=r * 0.1,
                                  matrix=Matrix.Translation(f + Vector((0, 0, -r * 0.05))) @
                                  Matrix.Rotation(math.pi, 4, "X"))
            fang = pm.new_mesh_object(name + "Fang", bm, fangs)
            fang.modifiers.new("Smooth", "SUBSURF").levels = 1
    if freckle is not None:
        for s in (1, -1):
            for dx, dz in ((0.32, -0.2), (0.42, -0.14), (0.5, -0.24), (0.38, -0.3)):
                pm.ellipsoid(name + "Freckle", freckle, surface(s * r * dx, r * dz, -r * 0.01),
                             (r * 0.02, r * 0.008, r * 0.02), subsurf=0)
    return surface


def hair_clump(name, mat, points, radius, taper=(1.0, 0.1)):
    return pm.curve_tube(name, mat, points, radius, taper=taper, resolution=8)
