"""Geometry of the crowd costume kit: the two base bodies and every swappable part (crowd_kit.py names them).

Each builder makes the part's source objects at the world origin around a body's Proportions (anatomy.py), with
the shared craft materials, and returns [(object, weight rule, triangle target)]. crowd_export.py realises them at
game resolution, weights them to the body's skeleton, unwraps them into the shared atlas and bakes.

Weight rules: "body" (bone heat on the body skin), ("bone", name), "hand", "shoe", "head", "lid" (the Blink
bone), "hood" (head, easing onto the neck at its lower edge), "trunk" (nearest body weights of the trunk),
"skirt" (trunk above the hips, hips easing a little onto each leg below), "tail" (Tail_1 / Tail_2 by length).

Conventions: metres, Z up, characters face -Y, +X is the character's left (as the cast).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars", "export", "crowd"):
    p = os.path.join(TOOLS, sub)
    if p not in sys.path:
        sys.path.insert(0, p)

import anatomy as an  # noqa: E402
import costume as co  # noqa: E402
import pip_model as pm  # noqa: E402
from crowd_kit import PAL, SKIN, HAIR  # noqa: E402

# per body: the face layout in head radii (eye centre x / z, eye half-size x / z)
FACE = {True: dict(eye_x=0.37, eye_z=-0.1, eye_rx=0.165, eye_rz=0.2),
        False: dict(eye_x=0.34, eye_z=0.0, eye_rx=0.135, eye_rz=0.165)}
LID_DEPTH = 0.1


def GHOST_HEM(P):
    """The ghost sheet ends just above the wrists, so the hands (and the pail) peek out under it."""
    return P.z["wrist"] + 0.012 * P.h  # lids hide this far (head radii) inside the skull; the Blink bone brings them out


def proportions(body):
    if body == "CrowdChild":
        return an.Proportions(h=1.05, child=True, heads=4.5)
    return an.Proportions(h=1.70, child=False, heads=6.0, build=0.98, neck=1.3)


def hand_size(P):
    return 1.15 if P.child else 1.0


class Builder:
    """Builds the parts of one body. `info` collects the hand pivots and the grip for the skeleton."""

    def __init__(self, craft, body):
        self.craft = craft
        self.body = body
        self.P = proportions(body)
        self.info = {}

    # --- materials ------------------------------------------------------------------------------------
    def felt(self, key):
        c = self.craft
        if key == "bone_stripe":
            return co.striped(c.felt(PAL["night"]), PAL["bone"], 0.07 if self.P.child else 0.09)
        if key == "purple_knit":
            return c.knit(PAL["purple"])
        if key in ("bone", "bone_cable"):
            return c.knit(PAL["bone"])
        if key == "rose_knit":
            return c.knit(PAL["rose"])
        if key == "orange_stripe":  # the Abbotts' matching jumpers: orange with plum bands, plum with orange
            return co.striped(c.knit(PAL["orange"]), PAL["plum"], 0.16)
        if key == "plum_stripe":
            return co.striped(c.knit(PAL["plum"]), PAL["orange"], 0.16)
        return c.felt(PAL[key])

    def skin(self, tone):
        return self.craft.smooth(SKIN[tone][0], 0.55, subsurface=0.2)

    # --- body -------------------------------------------------------------------------------------------
    def body_part(self, name, top, legs, print=None):
        P = self.P
        z = P.z
        reach = P.hip_x * 2.5

        def region(c):
            return 1 if abs(c.x) < reach and c.z < z["hip"] + 0.02 * P.h else 0
        obj = an.body(name, P, [self.felt(top), self.felt(legs)], region)
        cuffs = []
        for side in (1, -1):
            cuffs.append(pm.torus(name + "Cuff", self.felt(top), P.wrist(side) + Vector((0, 0, 0.002)),
                                  P.limb * 0.56, P.limb * 0.19, rotation=(0, -side * P.arm_out, 0), segments=16,
                                  sides=8))
        return [(obj, "body", 1700 if P.child else 2000)] + [(c, "cuff", 64) for c in cuffs]

    # --- hands, shoes -----------------------------------------------------------------------------------
    def hands(self, name, tone=None, glove=None):
        P = self.P
        mat = self.felt(glove) if glove else self.skin(tone)
        out = []
        for side, curl in ((1, 0.4), (-1, 1.5)):
            S = "L" if side > 0 else "R"
            pivot, grip = an.hand(f"{name}_Hand{S}", P, side, mat, mat, curl=curl, size=hand_size(P))
            self.info["hand" + S] = pivot.matrix_world.copy()
            if side < 0:
                self.info["grip"] = grip.copy()
            bpy.context.view_layer.update()
            self.info["hand" + S] = pivot.matrix_world.copy()
            for ch in list(pivot.children):
                if "Nail" in ch.name:
                    bpy.data.objects.remove(ch, do_unlink=True)
                    continue
                kind = ch.name[len(name) + 6:]
                target = 110 if kind.startswith("Palm") else 36 if kind.startswith("Wrist") else \
                    48 if kind.startswith("Thumb") else 40
                out.append((ch, "hand", target))
        return out

    def shoes(self, name, upper, accent):
        P = self.P
        c = self.craft
        out = []
        targets = {"Sole": 70, "Upper": 100, "ToeCap": 40, "HeelCounter": 30, "Tongue": 20, "Collar": 40}
        for side in (1, -1):
            S = "L" if side > 0 else "R"
            before = set(bpy.data.objects)
            an.sneaker(f"{name}_Shoe{S}", P, side, self.felt(upper), c.smooth(PAL["sole"], 0.6), self.felt(accent),
                       c.smooth(PAL["white"], 0.7))
            for o in set(bpy.data.objects) - before:
                kind = o.name[len(name) + 6:].split(".")[0]
                if kind in targets:
                    out.append((o, "shoe", targets[kind]))
                else:
                    bpy.data.objects.remove(o, do_unlink=True)
        return out

    # --- head ---------------------------------------------------------------------------------------------
    def head(self, name, tone, **_kw):
        """A soft round head (no modelled eyes: the face is painted on the atlas), ears, a button nose and two
        lids hidden inside the skull for the blink."""
        P = self.P
        r, hc = P.head_r, P.head_c
        adult = not P.child
        skin = self.skin(tone)
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=26, v_segments=16, radius=1.0)
        for v in bm.verts:
            x, y, zz = v.co
            if zz < 0:
                d = -zz
                cheek = math.exp(-((d - 0.42) / 0.3) ** 2)
                if adult:
                    x *= 1 + 0.02 * cheek - 0.22 * d ** 2.2
                    zz *= 1.06
                else:
                    x *= 1 + 0.05 * cheek - 0.1 * d ** 3
                    if y < 0:
                        y *= 1 + 0.06 * cheek
                    zz *= 0.96
            else:
                y *= 1.03
            v.co = Vector((x * r, y * r, zz * r)) + hc
        head = pm.new_mesh_object(name + "_Skull", bm, skin)
        out = [(head, "head", 520)]
        cast = an.probe(head)
        F = FACE[P.child]
        for s in (1, -1):
            ear = pm.ellipsoid(name + "_Ear", skin, hc + Vector((s * r * 0.97, 0.03 * r, -0.12 * r)),
                               (0.1 * r, 0.07 * r, 0.19 * r), segments=10, rings=8, subsurf=0)
            out.append((ear, "head", 80))
        hit = cast(hc + Vector((0, -2 * r, (-0.32 if P.child else -0.28) * r)), (0, 1, 0))
        nose = pm.ellipsoid(name + "_Nose", skin, hit[0] + Vector((0, 0.03 * r, 0)),
                            (0.1 * r, 0.08 * r, 0.075 * r) if P.child else (0.1 * r, 0.11 * r, 0.12 * r),
                            segments=10, rings=8, subsurf=0)
        out.append((nose, "head", 72))
        # lids: a skin cap over each painted eye, modelled where it closes, then pushed back into the skull
        for s in (1, -1):
            ex, ez = s * F["eye_x"] * r, F["eye_z"] * r
            bm = bmesh.new()

            def on_face(fu, fw):
                px = hc.x + ex + fu * F["eye_rx"] * r * 1.2
                pz = hc.z + ez + fw * F["eye_rz"] * r * 1.16
                h_ = cast(Vector((px, hc.y - 2 * r, pz)), (0, 1, 0))
                p = h_[0] + h_[1] * (0.012 * r) if h_ else Vector((px, hc.y - 0.9 * r, pz))
                return bm.verts.new(p + Vector((0, LID_DEPTH * r, 0)))
            centre = on_face(0.0, 0.0)
            rings = [[on_face(k / 2 * math.cos(2 * math.pi * i / 12), k / 2 * math.sin(2 * math.pi * i / 12))
                      for i in range(12)] for k in (1, 2)]
            for i in range(12):
                j = (i + 1) % 12
                bm.faces.new((centre, rings[0][i], rings[0][j]))
                bm.faces.new((rings[0][i], rings[1][i], rings[1][j], rings[0][j]))
            lid = pm.new_mesh_object(name + "_Lid", bm, skin)
            lid.modifiers.new("Thick", "SOLIDIFY").thickness = 0.01 * r
            out.append((lid, "lid", None))
        return out

    def hair(self, name, style, colour):
        P = self.P
        r = P.head_r
        mat = self.craft.felt(HAIR[colour])
        if style == "long":
            obj, on = co.cap(name, mat, P, radii=(1.08, 1.12, 1.08), offset=(0, 0.05, 0.06), fringe_z=0.42,
                             length_z=-1.15, face_half=62, thickness=0.12, fall=0.98, stretch=1.25, lobes=7,
                             lobe_depth=0.05, segments=32)
            return [(obj, "hood", 520)]
        if style == "puffs":
            obj, on = co.cap(name, mat, P, radii=(1.06, 1.08, 1.06), offset=(0, 0.04, 0.06), fringe_z=0.5,
                             length_z=-0.3, face_half=64, thickness=0.1, segments=32)
            out = [(obj, "head", 320)]
            for s in (1, -1):
                out.append((pm.ellipsoid(name + "Puff", mat, on(s * 1.25, 0.75, 1.12), (0.42 * r, 0.4 * r, 0.42 * r),
                                         segments=14, rings=10, subsurf=0), "head", 160))
            return out
        if style == "bun":
            obj, on = co.cap(name, mat, P, radii=(1.06, 1.1, 1.06), offset=(0, 0.04, 0.07), fringe_z=0.5,
                             length_z=-0.45, face_half=60, thickness=0.1, segments=32)
            bun = pm.ellipsoid(name + "Bun", mat, on(math.pi, 0.7, 1.15), (0.36 * r, 0.32 * r, 0.33 * r),
                               segments=14, rings=10, subsurf=0)
            return [(obj, "head", 340), (bun, "head", 160)]
        fringe, length = (0.55, -0.32) if style == "short" else (0.62, -0.22)
        obj, on = co.cap(name, mat, P, radii=(1.05, 1.08, 1.05), offset=(0, 0.04, 0.08), fringe_z=fringe,
                         length_z=length, face_half=62, thickness=0.09, lobes=5 if style == "short" else 0,
                         lobe_depth=0.06, segments=32)
        return [(obj, "head", 340)]

    # --- head pieces ------------------------------------------------------------------------------------
    def hood(self, name, colour, ears=False):
        P = self.P
        r = P.head_r
        mat = self.felt(colour)
        obj, on = co.cap(name, mat, P, radii=(1.16, 1.18, 1.14), offset=(0, 0.05, 0.05), fringe_z=0.52,
                         length_z=-1.05, face_half=62, thickness=0.08, fall=0.92, segments=32)
        out = [(obj, "hood", 560)]
        if ears:
            inner = self.felt("plum")
            for s in (1, -1):
                base = on(s * 0.66, 0.72, 0.96)
                tilt = Matrix.Rotation(s * -0.5, 3, "Y") @ Matrix.Rotation(0.12, 3, "X")
                for mat_, scale, push in ((mat, 1.0, 0.0), (inner, 0.62, -0.018 * r)):
                    bm = bmesh.new()
                    bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=0.62 * r * scale, radius2=0.0,
                                          depth=0.95 * r * scale, matrix=Matrix.Translation((0, 0, 0.47 * r * scale)))
                    for v in bm.verts:
                        v.co = tilt @ Vector((v.co.x, v.co.y * 0.34, v.co.z)) + base + Vector((0, push - 0.02 * r, 0))
                    ear = pm.new_mesh_object(name + "Ear", bm, mat_, smooth=False)
                    ear.modifiers.new("Bevel", "BEVEL").width = 0.025 * r
                    out.append((ear, "head", 60))
        return out

    def ears_band(self, name, colour):
        """A headband with felt cat ears (grown-up): over the hair, ear to ear."""
        P = self.P
        r, hc = P.head_r, P.head_c
        mat = self.felt(colour)
        pts = []
        for i in range(9):
            a = math.radians(-100 + 200 * i / 8)
            pts.append(hc + Vector((math.sin(a) * r * 1.13, 0.1 * r, math.cos(a) * r * 1.16 + 0.06 * r)))
        band = pm.curve_tube(name + "Band", mat, pts, 0.07 * r, taper=(1, 1), resolution=4)
        band.data.bevel_resolution = 1
        out = [(band, "head", 160)]
        inner = self.felt("plum")
        for s in (1, -1):
            a = math.radians(s * 38)
            base = hc + Vector((math.sin(a) * r * 1.1, 0.1 * r, math.cos(a) * r * 1.12 + 0.06 * r))
            tilt = Matrix.Rotation(-s * 0.55, 3, "Y")
            for mat_, scale, push in ((mat, 1.0, 0.0), (inner, 0.62, -0.02 * r)):
                bm = bmesh.new()
                bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=0.62 * r * scale, radius2=0.0,
                                      depth=0.95 * r * scale, matrix=Matrix.Translation((0, 0, 0.47 * r * scale)))
                for v in bm.verts:
                    v.co = tilt @ Vector((v.co.x, v.co.y * 0.32, v.co.z)) + base + Vector((0, push, 0))
                ear = pm.new_mesh_object(name + "Ear", bm, mat_, smooth=False)
                ear.modifiers.new("Bevel", "BEVEL").width = 0.02 * r
                out.append((ear, "head", 60))
        return out

    def crown(self, name):
        """A cut-paper crown with five points; the gems are painted paper circles (no foil, no stars)."""
        P = self.P
        r, hc = P.head_r, P.head_c
        mat = self.craft.paper(PAL["candy"])
        R = r * (1.0 if P.child else 0.98)
        z0 = hc.z + r * (0.5 if P.child else 0.58)
        band_h = r * 0.34
        bm = bmesh.new()
        n = 60
        low, high = [], []
        for i in range(n):
            a = 2 * math.pi * i / n
            k = abs(math.sin(5 * a / 2))  # five points
            peak = band_h + r * 0.36 * (1 - k) ** 1.6
            p = Vector((math.cos(a) * R, math.sin(a) * R * 0.96, 0))
            low.append(bm.verts.new(p + Vector((0, 0.06 * r, z0))))
            high.append(bm.verts.new(p * 1.06 + Vector((0, 0.06 * r, z0 + peak))))
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((low[i], low[j], high[j], high[i]))
        obj = pm.new_mesh_object(name, bm, mat)
        obj.modifiers.new("Thick", "SOLIDIFY").thickness = 0.004
        obj.rotation_euler = (math.radians(-8), 0, 0)
        obj.location = Vector((0, 0, 0))
        bpy.context.view_layer.update()
        me = obj.data
        pivot = Vector((0, 0.06 * r, z0))
        rot = Matrix.Rotation(math.radians(-8), 4, "X")
        me.transform(Matrix.Translation(pivot) @ rot @ Matrix.Translation(-pivot))
        obj.rotation_euler = (0, 0, 0)
        return [(obj, "head", 260)]

    # --- costumes -------------------------------------------------------------------------------------------
    def ghost_sheet(self, name):
        """A bone-white sheet over the whole child down to the wrists, eye holes painted on, a scalloped hem
        with a running stitch; the arms stay under it and the hands peek out below."""
        P = self.P
        r, hc, z = P.head_r, P.head_c, P.z
        top = hc.z + r * 1.1
        hem = GHOST_HEM(P)
        prof = [(0.03 * r, top, 1.0), (0.6 * r, top - 0.18 * r, 1.0), (1.0 * r, hc.z + 0.42 * r, 0.98),
                (1.14 * r, hc.z - 0.1 * r, 0.96), (1.12 * r, hc.z - 0.72 * r, 0.92),
                (P.shoulder_x * 1.45, z["shoulder"] + 0.004 * P.h, 0.84), (P.torso * 2.05, z["chest"], 0.84),
                (P.torso * 2.45, z["waist"], 0.86), (P.torso * 2.75, z["hip"] + 0.02 * P.h, 0.88),
                (P.torso * 2.95, hem, 0.9)]
        obj = co.ell_lathe(name, self.craft.felt(PAL["white"]), prof, segments=40, thickness=0.006,
                           hem=lambda t: 0.018 * math.cos(2 * math.pi * 8 * t) - 0.008, folds=8, fold_depth=0.06)
        obj.modifiers["Smooth"].levels = 1
        return [(obj, "skirt", 1500)]

    def robot_box(self, name):
        P = self.P
        z = P.z
        w = P.shoulder_x * 2.25
        d = P.torso * 1.75
        z0, z1 = z["hip"] - 0.035 * P.h, z["shoulder"] + 0.012 * P.h
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((0, 0.004, (z0 + z1) / 2))
                              @ Matrix.Diagonal((w, d, z1 - z0, 1)))
        bottom = [f for f in bm.faces if f.normal.z < -0.9]
        bmesh.ops.delete(bm, geom=bottom, context="FACES")
        obj = pm.new_mesh_object(name, bm, self.craft.cardboard(PAL["kraft"]), smooth=False)
        obj.modifiers.new("Thick", "SOLIDIFY").thickness = 0.006
        bev = obj.modifiers.new("Bevel", "BEVEL")
        bev.width, bev.segments = 0.012, 2
        out = [(obj, ("bone", "Chest"), 400)]
        # two paper dials and a cardboard knob on the front, a shoulder light (unlit paper)
        front = 0.004 - d / 2
        for x, zz, rad, col in ((-0.33, 0.62, 0.14, "candy"), (0.3, 0.62, 0.1, "orange")):
            knob = pm.ellipsoid(name + "Knob", self.craft.felt(PAL[col]),
                                (x * w, front - 0.012, z0 + zz * (z1 - z0)), (rad * w, 0.02, rad * w),
                                segments=12, rings=6, subsurf=0)
            out.append((knob, ("bone", "Chest"), 70))
        return out

    def robot_helmet(self, name):
        P = self.P
        r, hc = P.head_r, P.head_c
        w, h, d = 2.36 * r, 2.3 * r, 2.3 * r
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation(hc + Vector((0, 0.06 * r, 0.12 * r)))
                              @ Matrix.Diagonal((w, d, h, 1)))
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.z < -0.9], context="FACES")
        front = next(f for f in bm.faces if f.normal.y < -0.9)
        res = bmesh.ops.inset_individual(bm, faces=[front], thickness=0.2 * r, depth=0.0)
        bmesh.ops.delete(bm, geom=[front], context="FACES")
        _ = res
        obj = pm.new_mesh_object(name, bm, self.craft.cardboard(PAL["kraft"]), smooth=False)
        obj.modifiers.new("Thick", "SOLIDIFY").thickness = 0.006
        bev = obj.modifiers.new("Bevel", "BEVEL")
        bev.width, bev.segments = 0.01, 2
        top = hc.z + 0.12 * r + h / 2
        stalk = pm.curve_tube(name + "Antenna", self.craft.smooth(PAL["ink"], 0.5),
                              [Vector((0.25 * r, 0.06 * r, top)), Vector((0.32 * r, 0.06 * r, top + 0.35 * r)),
                               Vector((0.4 * r, 0.04 * r, top + 0.62 * r))], 0.03 * r, taper=(1, 1), resolution=3)
        stalk.data.bevel_resolution = 1
        ball = pm.ellipsoid(name + "Ball", self.craft.felt(PAL["candy"]),
                            Vector((0.42 * r, 0.04 * r, top + 0.7 * r)), (0.12 * r,) * 3, segments=10, rings=8,
                            subsurf=0)
        return [(obj, "head", 300), (stalk, "head", 48), (ball, "head", 80)]

    def candy_corn(self, name):
        """A felt cone from the neck to above the knee in the three bands of a candy corn: bone at the top,
        orange, then candy yellow at the wide hem."""
        P = self.P
        z = P.z
        hem = z["knee"] + 0.42 * (z["hip"] - z["knee"])
        top = z["neck"] + 0.01 * P.h
        prof = [(P.limb * 0.85, top, 0.9), (P.shoulder_x * 0.95, z["shoulder"] - 0.005 * P.h, 0.78),
                (P.torso * 1.62, z["chest"] - 0.04 * P.h, 0.76), (P.torso * 2.0, z["waist"] - 0.02 * P.h, 0.8),
                (P.torso * 2.42, z["hip"] - 0.01 * P.h, 0.84), (P.torso * 2.62, hem, 0.86)]
        mats = [self.craft.felt(PAL["white"]), self.craft.felt(PAL["orange"]), self.craft.felt(PAL["candy"])]
        obj = co.ell_lathe(name, mats[0], prof, segments=40, thickness=0.006, hem=lambda t: -0.004)
        obj.modifiers["Smooth"].levels = 1
        for m in mats[1:]:
            obj.data.materials.append(m)
        obj.modifiers["Thick"].material_offset = 0
        b1 = z["chest"] + 0.02 * P.h
        b2 = z["waist"] - 0.05 * P.h
        for poly in obj.data.polygons:
            c = poly.center.z
            poly.material_index = 0 if c > b1 else 1 if c > b2 else 2
        return [(obj, "skirt", 1100)]

    def tunic(self, name, colour, trim, length, flare, belt=None, ermine=False):
        """A felt tunic from the shoulders: `length` of the hip-to-knee span below the hips, `flare` x hips."""
        P = self.P
        z = P.z
        hem = z["hip"] - length * (z["hip"] - z["knee"])
        prof = [(P.limb * 0.9, z["neck"] + 0.004 * P.h, 0.9), (P.shoulder_x * 1.02, z["shoulder"] - 0.003 * P.h, 0.78),
                (P.torso * 1.2, z["chest"], 0.76), (P.torso * 1.12, z["waist"], 0.8),
                (P.torso * 1.35 * flare, z["hip"], 0.82), (P.torso * 1.55 * flare, hem, 0.86)]
        base, edge = self.felt(colour), self.felt(trim)
        obj = co.ell_lathe(name, base, prof, segments=40, thickness=0.006, folds=7, fold_depth=0.05)
        obj.modifiers["Smooth"].levels = 1
        obj.data.materials.append(edge)
        mats = [base, edge]
        if belt:
            obj.data.materials.append(self.felt(belt))
            mats.append(self.felt(belt))
        band = 0.035 * P.h
        for poly in obj.data.polygons:
            c = poly.center.z
            idx = 1 if c < hem + band * 0.8 else 0
            if belt and abs(c - z["waist"] + 0.01 * P.h) < 0.022 * P.h:
                idx = 2
            poly.material_index = idx
        return [(obj, "skirt", 1100 if P.child else 1300)]

    def tail_points(self):
        """The tail's curve (also where the Tail_1 / Tail_2 bones go on a body that has no tail part)."""
        P = self.P
        z = P.z
        k = P.h / 1.05 if P.child else 1.2  # a grown-up's tail is a little longer, not scaled up
        y0 = P.torso * 0.78
        z0 = z["pelvis"] - 0.035 * P.h
        return [Vector((0, y0 - 0.01, z0)), Vector((0.01 * k, y0 + 0.09 * k, z0 - 0.1 * k)),
                Vector((0.04 * k, y0 + 0.2 * k, z0 - 0.1 * k)), Vector((0.07 * k, y0 + 0.27 * k, z0 + 0.01 * k)),
                Vector((0.08 * k, y0 + 0.28 * k, z0 + 0.1 * k))]

    def tail(self, name, colour, tip):
        P = self.P
        k = P.h / 1.05 if P.child else 1.2
        pts = self.tail_points()
        tube = pm.curve_tube(name, self.felt(colour), pts, 0.021 * k, taper=(1.0, 0.85), resolution=8)
        tube.data.bevel_resolution = 2
        tipo = pm.ellipsoid(name + "Tip", self.felt(tip), pts[-1] + Vector((0, 0, 0.012 * k)),
                            (0.024 * k, 0.024 * k, 0.034 * k), segments=10, rings=8, subsurf=0)
        self.info["tail"] = [p.copy() for p in pts]
        return [(tube, "tail", 300), (tipo, "tail", 80)]

    # --- props (hang from the right hand's grip, on the Pail bone) -----------------------------------------
    def _grip(self):
        if "grip" not in self.info:
            raise RuntimeError("build the hands before the props")
        return self.info["grip"]

    def pail(self, name, colour):
        P = self.P
        g = self._grip()
        k = 1.0 if P.child else 1.15
        rim = g + Vector((0, 0, -0.085 * k))
        bot = rim.z - 0.1 * k
        prof = [(0.058 * k, bot, 1.0), (0.072 * k, rim.z - 0.004, 1.0), (0.075 * k, rim.z, 1.0)]
        body = co.ell_lathe(name, self.felt(colour), [(0.001, bot, 1.0)] + prof, segments=24, thickness=0.004)
        body.modifiers["Smooth"].levels = 1
        body.location = (rim.x, rim.y, 0)
        band = pm.torus(name + "Band", self.felt("bone"), Vector((rim.x, rim.y, rim.z - 0.008 * k)), 0.075 * k,
                        0.007 * k, segments=24, sides=6)
        sweets = pm.ellipsoid(name + "Sweets", self.craft.smooth(PAL["candy"], 0.35),
                              Vector((rim.x, rim.y, rim.z - 0.004)), (0.064 * k, 0.064 * k, 0.026 * k),
                              segments=14, rings=8, subsurf=0)
        handle = pm.curve_tube(name + "Handle", self.craft.smooth(PAL["ink"], 0.4),
                               [rim + Vector((-0.072 * k, 0, 0)), rim + Vector((-0.06 * k, 0, 0.06 * k)), g,
                                rim + Vector((0.06 * k, 0, 0.06 * k)), rim + Vector((0.072 * k, 0, 0))],
                               0.0035 * k, taper=(1, 1), resolution=6)
        handle.data.bevel_resolution = 1
        return [(body, ("bone", "Pail"), 220), (band, ("bone", "Pail"), 96), (sweets, ("bone", "Pail"), 120),
                (handle, ("bone", "Pail"), 80)]

    def bag(self, name):
        P = self.P
        g = self._grip()
        k = 1.0 if P.child else 1.2
        w, d, h = 0.13 * k, 0.07 * k, 0.15 * k
        top = g.z - 0.06 * k
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((g.x, g.y, top - h / 2))
                              @ Matrix.Rotation(math.pi / 2, 4, "Z") @ Matrix.Diagonal((w, d, h, 1)))
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.z > 0.9], context="FACES")
        obj = pm.new_mesh_object(name, bm, self.craft.paper(PAL["bone"]), smooth=False)
        obj.modifiers.new("Thick", "SOLIDIFY").thickness = 0.003
        out = [(obj, ("bone", "Pail"), 120)]
        for s in (1, -1):
            hd = pm.curve_tube(name + "Handle", self.craft.paper(PAL["orange"]),
                               [Vector((g.x + s * d * 0.5, g.y - w * 0.25, top)), Vector((g.x + s * d * 0.5, g.y, g.z + 0.012)),
                                Vector((g.x + s * d * 0.5, g.y + w * 0.25, top))], 0.004 * k, taper=(1, 1), resolution=5)
            hd.data.bevel_resolution = 1
            out.append((hd, ("bone", "Pail"), 60))
        return out

    def lantern(self, name, colour):
        """A round paper lantern on a short wire bail; the paper glows (Crowd_Emission), below the bloom."""
        P = self.P
        g = self._grip()
        k = 1.0 if P.child else 1.2
        top = g.z - 0.05 * k
        R = 0.05 * k
        c = Vector((g.x, g.y, top - R * 1.45))
        prof = [(R * (0.62 + 0.38 * math.sin(math.pi * t)), c.z + R * 1.4 * math.cos(math.pi * t), 1.0)
                for t in [i / 10 for i in range(11)]]
        paper = co.ell_lathe(name, self.craft.paper(PAL[colour], glow=1.0), prof, segments=24, thickness=0.002,
                             folds=8, fold_depth=0.05)
        paper.modifiers["Smooth"].levels = 1
        paper.location = (c.x, c.y, 0)
        caps = []
        for zz in (c.z + R * 1.4, c.z - R * 1.4):
            caps.append(pm.torus(name + "Cap", self.felt("ink"), Vector((c.x, c.y, zz)), R * 0.56, R * 0.1,
                                 segments=16, sides=6))
        bail = pm.curve_tube(name + "Bail", self.craft.smooth(PAL["ink"], 0.4),
                             [Vector((c.x, c.y - R * 0.5, c.z + R * 1.4)), g, Vector((c.x, c.y + R * 0.5, c.z + R * 1.4))],
                             0.003 * k, taper=(1, 1), resolution=6)
        bail.data.bevel_resolution = 1
        return [(paper, ("bone", "Pail"), 360)] + [(cp, ("bone", "Pail"), 60) for cp in caps] + \
               [(bail, ("bone", "Pail"), 40)]

    # --- the door neighbours' house clothes (crowd_kit.NEIGHBOUR_PARTS) -----------------------------------------
    def slippers(self, name, colour, cuff):
        """Felt bootee slippers: the sneaker's last without laces, tongue, toe cap or heel counter, a soft dark sole
        and a fluffy cuff round the ankle."""
        P = self.P
        out = []
        targets = {"Sole": 70, "Upper": 100, "Collar": 48}
        for side in (1, -1):
            S = "L" if side > 0 else "R"
            before = set(bpy.data.objects)
            an.sneaker(f"{name}_Shoe{S}", P, side, self.felt(colour), self.craft.felt(PAL["ink"]), self.felt(cuff),
                       self.felt(cuff))
            for o in set(bpy.data.objects) - before:
                kind = o.name[len(name) + 6:].split(".")[0]
                if kind in targets:
                    out.append((o, "shoe", targets[kind]))
                else:
                    bpy.data.objects.remove(o, do_unlink=True)
        return out

    def beanie(self, name, colour, band):
        """A knitted bobble beanie with a turned-up band, over the hair."""
        P = self.P
        r, hc = P.head_r, P.head_c
        knit = self.craft.knit(PAL[colour])
        obj, _on = co.cap(name, knit, P, radii=(1.13, 1.15, 1.22), offset=(0, 0.04, 0.08), fringe_z=0.34, length_z=0.34,
                          face_half=0.0, thickness=0.07, segments=32)
        edge = 0.26 / 1.22  # unit height of the cap's lower edge
        rim = math.sqrt(1 - edge * edge)
        brim = pm.torus(name + "Band", self.craft.knit(PAL[band]), hc + Vector((0, 0.04 * r, 0.36 * r)), 1.13 * rim * r,
                        0.14 * r, segments=32, sides=10, squash=1.25)
        bobble = pm.ellipsoid(name + "Bobble", self.craft.felt(PAL[band]), hc + Vector((0, 0.04 * r, 1.48 * r)),
                              (0.3 * r, 0.3 * r, 0.27 * r), segments=14, rings=10, subsurf=0)
        return [(obj, "head", 420), (brim, "head", 200), (bobble, "head", 120)]

    def apron(self, name, colour, trim, pocket):
        """A cloth apron: a bib on the chest, a wide skirt to the knee, a neck strap; the pocket is painted."""
        P = self.P
        z = P.z
        T_ = P.torso
        hem = z["knee"] + 0.1 * (z["hip"] - z["knee"])
        rows = [(z["chest"] + 0.045 * P.h, T_ * 1.24, 34), (z["chest"], T_ * 1.24, 36), (z["waist"] + 0.02 * P.h, T_ * 1.16, 40),
                (z["waist"], T_ * 1.2, 74), (z["hip"], T_ * 1.48, 76), ((z["hip"] + hem) / 2, T_ * 1.6, 78), (hem, T_ * 1.7, 80)]
        cols = 14
        bm = bmesh.new()
        grid = []
        for zz, rad, half in rows:
            ring = []
            for i in range(cols + 1):
                a = math.radians(-half + 2 * half * i / cols)
                ring.append(bm.verts.new((rad * 1.03 * math.sin(a), -rad * 0.8 * 1.03 * math.cos(a) - 0.004, zz)))
            grid.append(ring)
        for ra, rb in zip(grid, grid[1:]):
            for i in range(cols):
                bm.faces.new((ra[i], ra[i + 1], rb[i + 1], rb[i]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.normal_update()
        front = min(bm.faces, key=lambda f: f.calc_center_median().y)
        if front.normal.y > 0:  # outward is toward the street (-Y)
            bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        obj = pm.new_mesh_object(name, bm, self.felt(colour))
        obj.data.materials.append(self.felt(trim))
        band = 0.03 * P.h
        for poly in obj.data.polygons:
            poly.material_index = 1 if poly.center.z < hem + band else 0
        obj.modifiers.new("Thick", "SOLIDIFY").thickness = 0.005
        top = rows[0]
        strap = []
        for s in (1, -1):
            a = math.radians(top[2] * 0.85)
            corner = Vector((s * top[1] * 1.03 * math.sin(a), -top[1] * 0.8 * 1.03 * math.cos(a), top[0]))
            neck = Vector((s * P.limb * 0.75, P.torso * 0.15, z["neck"] + 0.006 * P.h))
            strap.append(pm.curve_tube(name + "Strap", self.felt(trim), [corner, corner.lerp(neck, 0.5) + Vector((0, -0.01, 0.01)), neck],
                                       0.006, taper=(1, 1), resolution=4))
        for t in strap:
            t.data.bevel_resolution = 1
        return [(obj, "skirt", 700)] + [(t, "skirt", 40) for t in strap]

    def tin(self, name, colour, band):
        """A round sweet tin with its lid off, on a wire bail through the hand: sweets heaped on top."""
        P = self.P
        g = self._grip()
        rim = g + Vector((0, 0, -0.1))
        bot = rim.z - 0.085
        R = 0.1
        prof = [(0.001, bot, 1.0), (R * 0.96, bot, 1.0), (R, bot + 0.008, 1.0), (R, rim.z - 0.004, 1.0), (R * 1.02, rim.z, 1.0)]
        body = co.ell_lathe(name, self.felt(colour), prof, segments=24, thickness=0.004)
        body.modifiers["Smooth"].levels = 1
        body.location = (rim.x, rim.y, 0)
        stripe = pm.torus(name + "Band", self.felt(band), Vector((rim.x, rim.y, rim.z - 0.045)), R * 1.01, 0.012,
                          segments=24, sides=6, squash=2.0)
        sweets = pm.ellipsoid(name + "Sweets", self.craft.smooth(PAL["candy"], 0.35), Vector((rim.x, rim.y, rim.z + 0.006)),
                              (R * 0.92, R * 0.92, 0.035), segments=16, rings=8, subsurf=0)
        handle = pm.curve_tube(name + "Handle", self.craft.smooth(PAL["ink"], 0.4),
                               [rim + Vector((-R, 0, 0)), rim + Vector((-R * 0.8, 0, 0.07)), g, rim + Vector((R * 0.8, 0, 0.07)),
                                rim + Vector((R, 0, 0))], 0.0035, taper=(1, 1), resolution=6)
        handle.data.bevel_resolution = 1
        return [(body, ("bone", "Pail"), 220), (stripe, ("bone", "Pail"), 96), (sweets, ("bone", "Pail"), 120),
                (handle, ("bone", "Pail"), 80)]

    def basket(self, name):
        """A woven basket of sweets with a hoop handle through the hand."""
        P = self.P
        g = self._grip()
        rim = g + Vector((0, 0, -0.13))
        bot = rim.z - 0.1
        R = 0.12
        wicker = self.craft.cardboard(PAL["kraft"])
        prof = [(0.001, bot, 1.0), (R * 0.72, bot, 1.0), (R * 0.8, bot + 0.01, 1.0), (R, rim.z, 1.0)]
        body = co.ell_lathe(name, wicker, prof, segments=24, thickness=0.005, folds=12, fold_depth=0.04)
        body.modifiers["Smooth"].levels = 1
        body.location = (rim.x, rim.y, 0)
        lip = pm.torus(name + "Rim", self.craft.cardboard(PAL["kraft_dark"]), Vector((rim.x, rim.y, rim.z)), R, 0.012,
                       segments=24, sides=6, squash=0.8)
        sweets = pm.ellipsoid(name + "Sweets", self.craft.smooth(PAL["candy"], 0.35), Vector((rim.x, rim.y, rim.z + 0.004)),
                              (R * 0.9, R * 0.9, 0.04), segments=16, rings=8, subsurf=0)
        hoop = pm.curve_tube(name + "Hoop", self.craft.cardboard(PAL["kraft_dark"]),
                             [rim + Vector((-R, 0, 0)), rim + Vector((-R * 0.85, 0, 0.09)), g + Vector((0, 0, 0.01)),
                              rim + Vector((R * 0.85, 0, 0.09)), rim + Vector((R, 0, 0))], 0.008, taper=(1, 1), resolution=6)
        hoop.data.bevel_resolution = 1
        return [(body, ("bone", "Pail"), 300), (lip, ("bone", "Pail"), 96), (sweets, ("bone", "Pail"), 120),
                (hoop, ("bone", "Pail"), 90)]

    # --- dispatch --------------------------------------------------------------------------------------------
    def build(self, name, kind, opts):
        if kind == "body":
            return self.body_part(name, **opts)
        return getattr(self, kind)(name, **opts)
