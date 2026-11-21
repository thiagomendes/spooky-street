"""The Spooky Street cast in homemade Halloween costumes, at true relative scale (spec §2 v1.3, D17/D18).

    blender -b --python tools/blender/chars/cast.py -- <out.png>

Writes a review sheet: the full-body lineup on top, five face portraits below. Everyone is a human in a costume:
Bea the pumpkin (with her bicycle), Pip the little vampire, June the star witch, Aunt Kit the fortune teller and
Mr. Vale the friendly mummy.
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars"):
    sys.path.insert(0, os.path.join(TOOLS, sub))

import anatomy as an  # noqa: E402
import costume as co  # noqa: E402
import craft as craft_lib  # noqa: E402
import pip_model as pm  # noqa: E402
import pip_vampire  # noqa: E402
from costume import V, applique, band, cap, ell_lathe, flat, rods, star_outline, striped  # noqa: E402

H = craft_lib.hex_rgb
C = {  # cast-specific colours on top of the shared palette
    "leaf": H(0x2FA84F), "silver": H(0xDAE2EE), "cream": H(0xF3E3BE), "grey": H(0xB7B3C6), "amber": H(0xFFAA1E),
    "wicker": H(0xC98A45), "auburn": H(0xB8431F), "june_hair": H(0x2A1712), "kit_hair": H(0x1E1220),
    "glow": H(0xC9FF9A), "freckle": H(0xC9794F),
}
SKIN = {  # (skin, blush, nail)
    "bea": (H(0xFAD7BC), H(0xF58A8E), H(0xFBD3C6)), "june": (H(0xC98C5E), H(0xD9644F), H(0xE0A98A)),
    "vale": (H(0xEDBB9C), H(0xE8867C), H(0xF2CDBB)), "kit": (H(0x8F5A3A), H(0xB5473C), H(0xC08C70)),
}


# --- shared scaffolding ------------------------------------------------------------------------------------

def grouped(name, build):
    """Run `build()` at the world origin (surface probes need that) and parent everything it made to one root,
    which can then be moved. Returns (root, result of build)."""
    before = set(bpy.data.objects)
    result = build()
    root = bpy.data.objects.new(name, None)
    pm.link(root)
    for obj in set(bpy.data.objects) - before:
        if obj.parent is None and obj is not root:
            obj.parent = root
    return root, result


def solo(name):
    """Render only the group called `name` (plus the stage): software rendering time grows with everything
    in view, so each character is rendered on its own."""
    for obj in bpy.data.objects:
        top = obj
        while top.parent is not None:
            top = top.parent
        obj.hide_render = top.name in GROUPS and top.name != name


def face(craft, name, P, who, iris_rgb, brow_rgb, line_rgb=V["line"], **kw):
    """Head with blushed skin and graded irises; returns (skin material, face surface function)."""
    r, hc = P.head_r, P.head_c
    skin_rgb, blush_rgb, _nail = SKIN[who]
    skin = craft.smooth(skin_rgb, 0.5, subsurface=0.25)
    cheeks = [hc + Vector((s * r * 0.56, -r * 0.8, -r * 0.4)) for s in (1, -1)]
    iris = co.graded(craft.smooth(tuple(c * 0.4 for c in iris_rgb), 0.2), iris_rgb, r * 0.1, -r * 0.13)
    surface = an.head(name, P, co.blushed(skin, blush_rgb, cheeks, r * 0.3, 0.5), craft.smooth(V["white"], 0.15),
                      iris, craft.smooth(V["black"], 0.3), craft.smooth(line_rgb, 0.6),
                      craft.smooth(V["mouth"], 1.0), craft.smooth(V["tongue"], 0.8), brow=craft.felt(brow_rgb), **kw)
    return skin, surface


def figure(craft, name, P, who, skin, top, legs, cuff, curls=(0.4, 0.4)):
    """Body, hands and wrist cuffs. Returns (probe of the body, [left grip, right grip])."""
    z = P.z
    reach = P.hip_x * 2.5

    def region(c):
        if abs(c.x) < reach and c.z < z["hip"] + 0.02:
            return 1
        return 2 if c.z > z["neck"] + 0.012 else 0
    body = an.body(name + "_Body", P, [top, legs, skin], region)
    nail = craft.smooth(SKIN[who][2], 0.3)
    grips = []
    for side, curl in zip((1, -1), curls):
        grips.append(an.hand(f"{name}_Hand{'L' if side > 0 else 'R'}", P, side, skin, nail, curl=curl,
                             size=1.15 if P.child else 1.0)[1])
        pm.torus(name + "_Cuff", cuff, P.wrist(side) + Vector((0, 0, 0.002)), P.limb * 0.56, P.limb * 0.19,
                 rotation=(0, -side * P.arm_out, 0), segments=24, sides=10)
    return an.probe(body), grips


def shoes(craft, name, P, upper, accent, lace=V["white"]):
    for side in (1, -1):
        an.sneaker(f"{name}_Shoe{'L' if side > 0 else 'R'}", P, side, craft.felt(upper), craft.smooth(V["bone"], 0.6),
                   craft.felt(accent), craft.smooth(lace, 0.7))


def on_shell(cast, angle, zz):
    """Point and normal on the outside of a lathed garment at `angle` (0 = +X, counter-clockwise) and height."""
    return cast((2 * math.cos(angle), 2 * math.sin(angle), zz), (-math.cos(angle), -math.sin(angle), 0))


def hem_stitch(craft, name, cast, rgb, zz, start_deg=0.0, end_deg=360.0, lift=0.0):
    """Running stitch around a lathed garment at height `zz`, wherever the cloth is."""
    points = []
    for i in range(181):
        hit = on_shell(cast, math.radians(start_deg + (end_deg - start_deg) * i / 180), zz + lift)
        if hit:
            points.append(hit[0] + hit[1] * 0.0015)
    return craft.stitches(name, points, rgb, spacing=0.02, dash=0.011, radius=0.0022)


# --- Bea: pumpkin ------------------------------------------------------------------------------------------

def build_bea(craft):
    P = an.Proportions(h=1.34, child=True, heads=4.75)
    P.arm_out = 0.85  # the round costume holds her arms out
    z, T, r, hc = P.z, P.torso, P.head_r, P.head_c
    skin, _surface = face(craft, "Bea_", P, "bea", H(0x3F8F4A), C["auburn"], mouth="grin",
                         teeth=craft.smooth(V["white"], 0.3), freckle=craft.smooth(C["freckle"], 0.7), ears=False)
    green = craft.knit(C["leaf"])
    figure(craft, "Bea", P, "bea", skin, green, striped(craft.knit(V["lime"]), V["black"], 0.1), green)
    shoes(craft, "Bea", P, V["purple"], V["lime"])

    # The pumpkin: a round felt tunic with lobe seams, a stitched jack-o'-lantern face and a leaf collar.
    orange = craft.felt(V["orange"])
    tunic = ell_lathe("Bea_Pumpkin", orange,
                      [(T * 0.62, z["shoulder"] + 0.05, 0.9), (T * 1.25, z["shoulder"] - 0.005, 0.92),
                       (T * 1.6, z["chest"] - 0.03, 0.95), (T * 1.76, z["waist"] - 0.02, 0.96),
                       (T * 1.7, z["pelvis"] - 0.02, 0.96), (T * 1.4, z["hip"] - 0.07, 0.95),
                       (T * 1.16, z["hip"] - 0.1, 0.95)], thickness=0.008, folds=8, fold_depth=0.045, fold_power=0.3)
    on_tunic = an.probe(tunic)
    seam = craft.felt(H(0xD9560F))
    for k in range(8):  # a darker seam in every valley between lobes
        a = -math.pi / 2 + 2 * math.pi * (k + 0.5) / 8
        pts = [on_shell(on_tunic, a, zz) for zz in np.linspace(z["shoulder"] - 0.02, z["hip"] - 0.07, 9)]
        pm.curve_tube("Bea_PumpkinSeam", seam, [p + n * 0.001 for p, n in pts], 0.006, taper=(1, 1))
    black = craft.felt(V["black"])
    belly = z["waist"] - 0.01
    for s in (1, -1):
        applique("Bea_JackEye", black, on_tunic, [(-0.03, -0.022), (0.03, -0.022), (0.0, 0.03)],
                 (s * 0.062, belly + 0.05), craft=craft, stitch=V["candy"])
    applique("Bea_JackNose", black, on_tunic, [(-0.014, -0.012), (0.014, -0.012), (0.0, 0.014)], (0, belly + 0.005))
    grin = [(-0.095, 0.02), (-0.055, 0.0), (-0.03, 0.014), (0.0, -0.004), (0.03, 0.014), (0.055, 0.0), (0.095, 0.02),
            (0.06, -0.035), (0.03, -0.024), (0.0, -0.045), (-0.03, -0.024), (-0.06, -0.035)]
    applique("Bea_JackGrin", black, on_tunic, grin, (0, belly - 0.05), craft=craft, stitch=V["candy"])
    for k in range(9):
        a = math.radians(40 * k + 10)
        point, normal = on_tunic((0.105 * math.cos(a), 0.1 * math.sin(a), z["shoulder"] + 0.3), (0, 0, -1))
        leaf = flat("Bea_LeafFelt", craft.felt(C["leaf"] if k % 2 else V["lime"]), co.leaf_outline(0.085), 0.004)
        an.mount("Bea_Leaf", point + normal * 0.004, normal, [leaf])

    # Auburn hair with a side-swept ponytail, under a jack-o'-lantern bike helmet with a stem.
    hair = craft.felt(C["auburn"])
    cap("Bea_Hair", hair, P, fringe_z=0.34, length_z=-0.5, face_half=52, thickness=0.07, fall=0.96, lobes=10,
        lobe_depth=0.05)
    tail = [hc + Vector(p) * r for p in ((0.55, 0.95, 0.05), (1.25, 0.9, -0.1), (1.6, 0.7, -0.75), (1.5, 0.6, -1.35))]
    pm.curve_tube("Bea_Ponytail", hair, tail, r * 0.24, taper=(0.9, 0.25))
    pm.torus("Bea_HairTie", craft.felt(V["lime"]), tail[1] - Vector((0.03, 0, -0.012)), r * 0.2, r * 0.07,
             rotation=(0, math.radians(70), 0), segments=24, sides=8)
    helmet, on_helmet = cap("Bea_Helmet", craft.smooth(V["orange"], 0.35), P, radii=(1.24, 1.27, 1.2),
                            offset=(0, 0.04, 0.14), fringe_z=0.58, length_z=0.14, face_half=62, thickness=0.06,
                            lobes=10, lobe_depth=0.05)
    on_shell_helmet = an.probe(helmet)
    for s in (1, -1):
        applique("Bea_HelmetEye", black, on_shell_helmet, [(-0.02, -0.014), (0.02, -0.014), (0.0, 0.02)],
                 (s * r * 0.4, hc.z + r * 0.86), thickness=0.003)
    top = on_helmet(0, math.radians(89), 0.98)
    pm.curve_tube("Bea_HelmetStem", craft.felt(C["leaf"]), [top, top + Vector((0.004, 0, 0.035)),
                                                            top + Vector((0.03, 0, 0.065))], 0.016, taper=(1.3, 0.8))
    return P


def build_bike(craft):
    """Bea's bicycle: purple frame, wicker basket, foil moons and paper bats hanging from the handlebar."""
    R, tyre = 0.25, 0.022
    hub_z = R + tyre
    frame = craft.smooth(V["purple"], 0.3)
    black = craft.smooth(V["black"], 0.5)
    steel = craft.smooth(C["silver"], 0.25)
    for x in (0.43, -0.43):
        c = Vector((x, 0, hub_z))
        pm.torus("Bike_Tyre", black, c, R, tyre, rotation=(math.pi / 2, 0, 0), segments=48, sides=10)
        pm.torus("Bike_Rim", steel, c, R - 0.026, 0.009, rotation=(math.pi / 2, 0, 0), segments=48, sides=8)
        pm.ellipsoid("Bike_Hub", steel, c, (0.022, 0.03, 0.022), subsurf=1)
        rods("Bike_Spokes", steel, [(c, c + Vector((math.cos(a), 0, math.sin(a))) * (R - 0.026)) for a in
                                    [2 * math.pi * i / 12 for i in range(12)]], 0.0022)
    rear, front = Vector((0.43, 0, hub_z)), Vector((-0.43, 0, hub_z))
    crank, seat = Vector((0.08, 0, hub_z - 0.01)), Vector((0.2, 0, 0.6))
    head_top, head_low = Vector((-0.3, 0, 0.66)), Vector((-0.335, 0, 0.5))
    for a, b in ((seat, crank), (crank, rear), (seat, rear), (seat, head_top), (crank, head_low),
                 (head_top, head_low), (head_low, front)):
        pm.curve_tube("Bike_Frame", frame, [a, b], 0.014, taper=(1, 1))
    post = seat + Vector((0.012, 0, 0.07))
    pm.curve_tube("Bike_SeatPost", steel, [seat, post], 0.009, taper=(1, 1))
    pm.ellipsoid("Bike_Saddle", black, post + Vector((0.01, 0, 0.018)), (0.1, 0.05, 0.024), subsurf=1)
    bar = head_top + Vector((0.025, 0, 0.13))
    pm.curve_tube("Bike_Stem", steel, [head_top, bar], 0.009, taper=(1, 1))
    pm.curve_tube("Bike_Handlebar", steel, [bar + Vector((0.05, -0.22, 0.02)), bar + Vector((0, -0.1, 0)), bar,
                                            bar + Vector((0, 0.1, 0)), bar + Vector((0.05, 0.22, 0.02))], 0.009,
                  taper=(1, 1))
    for s in (1, -1):
        pm.curve_tube("Bike_Grip", black, [bar + Vector((0.03, s * 0.17, 0.012)),
                                           bar + Vector((0.055, s * 0.235, 0.022))], 0.014, taper=(1, 1))
    pm.ellipsoid("Bike_Chainring", craft.smooth(V["orange"], 0.35), crank, (0.065, 0.012, 0.065), subsurf=1)
    pm.curve_tube("Bike_Cranks", steel, [crank + Vector((0.05, -0.05, -0.09)), crank + Vector((0, -0.04, 0)),
                                         crank + Vector((0, 0.04, 0)), crank + Vector((-0.05, 0.05, 0.09))], 0.007,
                  taper=(1, 1))
    for p in (crank + Vector((0.05, -0.085, -0.09)), crank + Vector((-0.05, 0.085, 0.09))):
        pm.ellipsoid("Bike_Pedal", black, p, (0.04, 0.035, 0.012), subsurf=1)
    pm.curve_tube("Bike_Kickstand", steel, [crank, crank + Vector((0.06, 0.16, -hub_z + 0.016))], 0.006, taper=(1, 1))
    wicker = craft.knit(C["wicker"])
    basket = ell_lathe("Bike_Basket", wicker,
                       [(0.13, 0.2, 0.8), (0.118, 0.1, 0.8), (0.1, 0.012, 0.8), (0.02, 0.0, 0.8)], thickness=0.008,
                       segments=40)
    basket.location = bar + Vector((-0.17, 0, -0.17))
    rim = pm.torus("Bike_BasketRim", craft.felt(C["wicker"]), (0, 0, 0), 0.13, 0.009, segments=40, sides=8)
    rim.parent, rim.location, rim.scale = basket, (0, 0, 0.2), (1, 0.8, 1)
    # Decorations on strings along the handlebar, staggered so each one reads.
    foil = craft.smooth(V["candy"], 0.18)
    paper = craft.paper(V["purple"])
    moon = pm.smooth_outline([(0.0, 0.05), (0.036, 0.03), (0.046, -0.01), (0.03, -0.045), (0.0, -0.056),
                              (0.02, -0.024), (0.022, 0.006), (0.012, 0.032)], 5)
    for k, (y, drop) in enumerate(((-0.2, 0.2), (-0.09, 0.12), (0.09, 0.22), (0.2, 0.13))):
        top = bar + Vector((0.05 * (abs(y) / 0.22) ** 2, y, 0.02 * (abs(y) / 0.22)))
        low = top + Vector((0, 0, -drop))
        rods("Bike_String", black, [(top, low + Vector((0, 0, 0.02)))], 0.0015)
        if k % 2 == 0:
            flat("Bike_Moon", foil, moon, 0.003, low + Vector((-0.012, 0, -0.03)))
        else:
            flat("Bike_PaperBat", paper, co.bat_outline(0.1), 0.002, low + Vector((0, 0, -0.012)))


# --- June: star witch --------------------------------------------------------------------------------------

def build_june(craft):
    P = an.Proportions(h=1.5, child=True, heads=5.3, build=0.95)
    z, T, r, hc = P.z, P.torso, P.head_r, P.head_c
    skin, _surface = face(craft, "June_", P, "june", H(0x7A4A1E), C["june_hair"], ears=False)
    violet = craft.felt(V["violet"])
    _on_body, _grips = figure(craft, "June", P, "june", skin, violet,
                              striped(craft.knit(V["purple"]), V["black"], 0.1), craft.felt(V["candy"]))
    # Lace-up boots: the sneaker last in black with a tall shaft.
    shoes(craft, "June", P, V["black"], V["violet"], lace=V["candy"])
    for s in (1, -1):
        band("June_BootShaft", craft.felt(V["black"]), (s * P.hip_x, 0.004, z["ankle"] + 0.085), P.limb * 0.8, 0.1,
             thickness=0.008)
        pm.torus("June_BootCuff", craft.felt(V["violet"]), (s * P.hip_x, 0.004, z["ankle"] + 0.135), P.limb * 0.78,
                 0.009, segments=28, sides=8)

    # Deep-violet dress with a swinging skirt, an orange sash and sewn-on stars.
    dress = ell_lathe("June_Dress", violet,
                      [(T * 0.66, z["shoulder"] + 0.045, 0.8), (T * 1.08, z["shoulder"] - 0.01, 0.78),
                       (T * 1.12, z["chest"] - 0.01, 0.8), (T * 0.98, z["waist"], 0.8),
                       (T * 1.25, z["pelvis"], 0.82), (T * 1.8, z["knee"] + 0.12, 0.85),
                       (T * 2.0, z["knee"] + 0.04, 0.85)], thickness=0.007, folds=9, fold_depth=0.06)
    on_dress = an.probe(dress)
    band("June_Sash", craft.felt(V["orange"]), (0, 0, z["waist"]), T * 1.03, 0.04, ys=0.8)
    candy = craft.felt(V["candy"])
    flat("June_SashStar", candy, star_outline(0.032), 0.006, (0, -T * 1.03 * 0.8 - 0.005, z["waist"]))
    for x, zz, size in ((-0.12, z["knee"] + 0.2, 0.03), (0.1, z["knee"] + 0.26, 0.022), (0.02, z["knee"] + 0.13, 0.026),
                        (-0.05, z["pelvis"] - 0.05, 0.018), (0.17, z["knee"] + 0.12, 0.02)):
        point, normal = on_dress((x, -3, zz), (0, 1, 0))
        an.mount("June_DressStar", point + normal * 0.004, normal,
                 [flat("June_DressStarFelt", candy, star_outline(size), 0.004)], roll=x * 4)
    hem_stitch(craft, "June_DressStitch", on_dress, V["candy"], z["knee"] + 0.07)

    # The silver comet cape: satin, long, with the triangular tear at the hem (story clue).
    # It wraps forward over her arms like a cloak, so the torn front panel faces the viewer. The tear is cut
    # out of the cloth itself: a ragged triangle rising from the hem.
    tear_at, tear_half, tear_height = math.radians(-38), math.radians(16), 0.3
    satin = craft.waxed(C["silver"])
    k, hem_z = P.shoulder_x / 0.115, z["ankle"] + 0.05
    lower = [(0.345 - 0.0006 * i, hem_z + 0.035 * i, 0.62, 26 + 0.45 * i) for i in range(10, -1, -1)]
    cape = ell_lathe("June_Cape", satin,
                     [(0.068 * k, z["neck"] + 0.012 * k, 0.9, 50), (0.13 * k, z["shoulder"] + 0.066 * k, 0.74, 70),
                      (0.188 * k, z["shoulder"] + 0.016 * k, 0.66, 66), (0.21 * k, z["chest"] - 0.03 * k, 0.64, 56),
                      (0.3, z["pelvis"], 0.62, 40)] + lower, thickness=0.006, folds=7, fold_depth=0.04, segments=150)
    bm = bmesh.new()
    bm.from_mesh(cape.data)
    torn = []
    for f in bm.faces:
        c = f.calc_center_median()
        rise = (c.z - hem_z) / tear_height
        if rise < 1 and abs(math.atan2(c.y / 0.62, c.x) - tear_at) < tear_half * (1 - rise):
            torn.append(f)
    bmesh.ops.delete(bm, geom=torn, context="FACES")
    bm.to_mesh(cape.data)
    bm.free()
    on_cape = an.probe(cape)
    threads = []
    for i in range(8):  # frayed threads hanging from the torn edges
        side, rise = (1 if i % 2 else -1), 0.12 + 0.1 * (i // 2)
        a = tear_at + side * (tear_half * (1 - rise) + 0.03)
        hit = on_shell(on_cape, a, hem_z + rise * tear_height)
        if hit:
            threads.append((hit[0], hit[0] + Vector((-side * 0.012 * math.sin(a), 0.0, -0.03 - 0.008 * (i % 3)))))
    rods("June_CapeThreads", satin, threads, 0.0016)
    ends = []
    for s in (1, -1):
        a = -math.pi / 2 + s * math.radians(52)
        ends.append(Vector((0.07 * k * math.cos(a), 0.063 * k * math.sin(a) - 0.004, z["neck"] + 0.004)))
    pm.curve_tube("June_ClaspCord", craft.smooth(V["candy"], 0.4),
                  [ends[0], (ends[0] + ends[1]) * 0.5 + Vector((0, -0.014, -0.014)), ends[1]], 0.0035, taper=(1, 1))
    for p in ends:
        flat("June_ClaspStar", candy, star_outline(0.022), 0.005, p + Vector((0, -0.006, 0)))

    # Long dark hair with one thick braid over her left shoulder.
    hair = craft.felt(C["june_hair"])
    cap("June_Hair", hair, P, fringe_z=0.5, length_z=-1.36, face_half=47, thickness=0.05, fall=0.97, lobes=12,
        lobe_depth=0.06, stretch=1.7)
    start = hc + Vector((r * 0.8, -r * 0.72, -r * 1.05))
    beads = 9
    for k in range(beads):
        f = k / (beads - 1)
        zz = start.z - 0.02 - 0.3 * f
        x = start.x - 0.02 * f
        hit = on_dress((x, -3, zz), (0, 1, 0))
        y = min(start.y, hit[0].y - 0.03) if hit else start.y
        y = start.y + (y - start.y) * min(1.0, f * 2.5)
        pm.ellipsoid("June_Braid", hair, (x, y, zz), (0.03 - 0.006 * f, 0.027 - 0.006 * f, 0.03),
                     rotation=(0, 0.45 * (1 if k % 2 else -1), 0), subsurf=1)
        if k == beads - 1:
            pm.torus("June_BraidTie", craft.felt(V["orange"]), (x, y, zz - 0.026), 0.016, 0.007, segments=20, sides=8)
            pm.ellipsoid("June_BraidTuft", hair, (x, y, zz - 0.06), (0.016, 0.015, 0.035), subsurf=1)

    # Pointed witch hat with a star band and a crooked tip.
    felt_hat = craft.felt(V["purple"])
    hat = ell_lathe("June_Hat", felt_hat, [(0.3, -0.012), (0.22, 0.004), (0.166, 0.012), (0.158, 0.03), (0.125, 0.13),
                                           (0.08, 0.25), (0.036, 0.35), (0.005, 0.42)], yscale=1.0, thickness=0.006,
                    inner=craft.felt(V["violet"]))
    for v in hat.data.vertices:
        if v.co.z > 0.2:
            v.co.x += 0.85 * (v.co.z - 0.2) ** 1.5
            v.co.z -= 0.35 * (v.co.z - 0.2) ** 1.5
    hat_band = band("June_HatBand", craft.felt(V["candy"]), (0, 0, 0.05), 0.154, 0.04, thickness=0.004)
    star = flat("June_HatStar", craft.felt(V["orange"]), star_outline(0.04), 0.005, (0, -0.158, 0.055),
                (math.radians(-18), 0, 0))
    pivot = bpy.data.objects.new("June_HatPivot", None)
    pm.link(pivot)
    pivot.location = hc + Vector((0, r * 0.03, r * 0.45))
    pivot.rotation_euler = (math.radians(-9), math.radians(5), 0)
    for part in (hat, hat_band, star):
        part.parent = pivot
    return P


# --- Mr. Vale: friendly mummy ------------------------------------------------------------------------------

def build_vale(craft):
    P = an.Proportions(h=1.72, child=False, belly=1.12)
    z, T, L, r, hc = P.z, P.torso, P.limb, P.head_r, P.head_c
    grey = craft.felt(C["grey"])
    skin, surface = face(craft, "Vale_", P, "vale", H(0x4F7FA8), C["grey"], adult=True, mouth="smile")
    cream = craft.felt(C["cream"])
    on_body, grips = figure(craft, "Vale", P, "vale", skin, craft.knit(V["purple"]), craft.felt(H(0x2B2350)), cream,
                             curls=(0.4, 1.5))
    shoes(craft, "Vale", P, V["black"], C["cream"], lace=V["orange"])
    pm.ellipsoid("Vale_Moustache", grey, surface(0, -r * 0.43, r * 0.0), (r * 0.3, r * 0.07, r * 0.085), subsurf=1)
    for s in (1, -1):
        pm.ellipsoid("Vale_MoustacheTip", grey, surface(s * r * 0.26, -r * 0.47, -r * 0.005),
                     (r * 0.12, r * 0.05, r * 0.06), rotation=(0, s * 0.5, 0), subsurf=1)

    # Loose bandage turns over his clothes, fitted to the body, with the purple jumper showing in the gaps.
    def girth(centre, side):
        """Distance from a limb's centre line to its surface on the outer side."""
        hit = on_body(centre + Vector((side * 0.5, 0.001, 0.001)), (-side, 0, 0))
        return (hit[0] - centre).length

    turns = [(z["neck"] - 0.07, 0.12, -0.05), (z["chest"] + 0.03, -0.1, 0.07), (z["chest"] - 0.05, 0.06, -0.1),
             (z["waist"] + 0.07, -0.05, 0.12), (z["waist"], 0.1, -0.04), (z["waist"] - 0.075, -0.08, -0.1),
             (z["pelvis"] + 0.02, 0.05, 0.09), (z["pelvis"] - 0.055, -0.06, -0.05)]
    for zz, tilt_x, tilt_y in turns:
        depth = -on_body((P.hip_x * 0.6, -1, zz), (0, 1, 0))[0].y  # off-centre: below the hips there are two legs
        band("Vale_Wrap", cream, (0, 0, zz), (depth + 0.026) / 0.76, 0.07, ys=0.76, tilt=(tilt_x, tilt_y, 0))
    band("Vale_WrapSash", cream, (0, 0, z["chest"] + 0.0), T * 1.02, 0.06, ys=0.84, tilt=(0.0, 0.45, 0))
    for s in (1, -1):
        sh, el, wr = Vector((s * P.shoulder_x, 0, z["shoulder"])), P.elbow(s), P.wrist(s)
        hip = Vector((s * P.hip_x, 0, z["hip"]))
        knee, ankle = Vector((s * P.hip_x * 1.02, -0.006, z["knee"])), Vector((s * P.hip_x, 0.004, z["ankle"]))
        arm = [(sh, el, 0.3, 0.3), (sh, el, 0.62, -0.25), (sh, el, 0.92, 0.2), (el, wr, 0.3, -0.2), (el, wr, 0.6, 0.25),
               (el, wr, 0.86, -0.15)]
        leg = [(hip, knee, 0.3, 0.2), (hip, knee, 0.58, -0.22), (hip, knee, 0.86, 0.15), (knee, ankle, 0.2, -0.2),
               (knee, ankle, 0.5, 0.22), (knee, ankle, 0.8, -0.12)]
        for a, b, f, tilt in arm:
            c = a.lerp(b, f)
            band("Vale_ArmWrap", cream, c, girth(c, s) + 0.007, 0.06, tilt=(tilt, -s * P.arm_out, 0))
        for a, b, f, tilt in leg:
            c = a.lerp(b, f)
            band("Vale_LegWrap", cream, c, girth(c, s) + 0.007, 0.065, tilt=(tilt, s * 0.08, 0))
    frayed = [(-0.02, 0.0), (0.02, 0.0), (0.022, -0.09), (0.01, -0.11), (0.0, -0.088), (-0.012, -0.112),
              (-0.022, -0.09)]
    wl = P.wrist(1)
    for loc, rot in ((wl + Vector((0.035, -0.02, 0.05)), (0, 0.12, 0.5)),
                     (hc + Vector((-r * 0.98, -r * 0.25, -r * 0.02)), (0, -0.1, -1.2))):
        flat("Vale_LooseEnd", cream, frayed, 0.004, loc, rot)

    # Grey hair at the sides, a bandage cap, and one turn slipping down over his left eye.
    cap("Vale_Hair", grey, P, radii=(1.03, 1.05, 1.03), offset=(0, 0.03, 0.02), fringe_z=0.6, length_z=-0.45,
        face_half=78, thickness=0.04)
    cap("Vale_HeadWrap", cream, P, radii=(1.07, 1.09, 1.07), offset=(0, 0.02, 0.04), fringe_z=0.56, length_z=0.2,
        face_half=180, thickness=0.03)
    band("Vale_HeadTurn", cream, hc + Vector((0, 0, r * 0.52)), r * 0.93, r * 0.2, tilt=(0.08, -0.16, 0),
         thickness=0.004)
    band("Vale_EyeTurn", cream, hc + Vector((0, 0, r * 0.2)), r * 1.045, r * 0.2, tilt=(0.0, 0.5, 0), thickness=0.004)

    # A black cauldron of sweets hanging from his right hand.
    grip = grips[1]
    pot = grip + Vector((0, 0, -0.2))
    iron = craft.smooth(V["black"], 0.35)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=20, radius=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * 0.115, v.co.y * 0.115, max(-0.92, min(0.55, v.co.z)) * 0.1)) + pot
    pm.new_mesh_object("Vale_Cauldron", bm, iron).modifiers.new("S", "SUBSURF").levels = 1
    pm.torus("Vale_CauldronRim", iron, pot + Vector((0, 0, 0.055)), 0.098, 0.012, segments=40, sides=10)
    sweets = [V["orange"], V["lime"], V["berry"], V["candy"], V["purple"], V["white"]]
    for k in range(16):
        ring, a = (0.0, 0) if k == 0 else ((0.04, k * 1.05) if k < 7 else (0.074, k * 0.7))
        c = pot + Vector((ring * math.cos(a), ring * math.sin(a), 0.085 - ring * 0.32))
        pm.ellipsoid("Vale_Sweet", craft.smooth(sweets[k % 6], 0.25), c, (0.021, 0.021, 0.017 + 0.004 * (k % 2)),
                     rotation=(k * 0.7, k * 1.3, 0), segments=16, rings=10, subsurf=1)
    co.hang("Vale_CauldronHandle", iron, grip, (pot + Vector((0, -0.105, 0.055)), pot + Vector((0, 0.105, 0.055))))
    return P


# --- Aunt Kit: fortune teller ------------------------------------------------------------------------------

def build_kit(craft):
    P = an.Proportions(h=1.64, child=False, build=0.93)
    z, T, r, hc = P.z, P.torso, P.head_r, P.head_c
    skin, surface = face(craft, "Kit_", P, "kit", H(0xD98A1E), C["kit_hair"], line_rgb=H(0x4A1420), adult=True,
                         mouth="smile", ears=False)
    blouse = craft.felt(V["bone"])
    _on_body, grips = figure(craft, "Kit", P, "kit", skin, blouse, craft.felt(V["violet"]), craft.felt(V["candy"]),
                             curls=(0.4, 1.5))
    shoes(craft, "Kit", P, V["berry"], V["candy"])

    # Long gathered skirt with a lime hem stitch and stars, and an orange sash.
    skirt = ell_lathe("Kit_Skirt", craft.felt(V["purple"]),
                      [(T * 0.98, z["waist"] + 0.04, 0.8), (T * 1.12, z["pelvis"] + 0.02, 0.82),
                       (T * 1.3, z["hip"] - 0.1, 0.85), (T * 1.75, z["knee"], 0.88),
                       (T * 2.1, z["ankle"] + 0.07, 0.9)], thickness=0.008, folds=11, fold_depth=0.07)
    on_skirt = an.probe(skirt)
    hem_stitch(craft, "Kit_SkirtStitch", on_skirt, V["lime"], z["ankle"] + 0.12)
    candy = craft.felt(V["candy"])
    for x, zz, size in ((-0.16, z["knee"] - 0.12, 0.034), (0.12, z["knee"] - 0.02, 0.026), (0.0, z["knee"] - 0.2, 0.03),
                        (0.2, z["knee"] - 0.22, 0.022), (-0.08, z["knee"] + 0.1, 0.022)):
        point, normal = on_skirt((x, -3, zz), (0, 1, 0))
        an.mount("Kit_SkirtStar", point + normal * 0.004, normal,
                 [flat("Kit_SkirtStarFelt", candy, star_outline(size), 0.004)], roll=x * 3)
    band("Kit_Sash", craft.felt(V["orange"]), (0, 0, z["waist"] + 0.03), T * 1.02, 0.06, ys=0.8)

    # Fringed berry shawl over the shoulders, pinned with the painted owl.
    def shawl_hem(t):
        ends = max(0.0, 1 - min(t, 1 - t) / 0.14)  # the two front corners hang in points
        return -0.05 * abs(math.sin(math.pi * t)) ** 3 - 0.1 * ends * ends
    sx, sz = P.shoulder_x, z["shoulder"]
    hem_z = P.elbow(1).z - 0.02
    shawl = ell_lathe("Kit_Shawl", craft.felt(V["berry"]),
                      [(sx * 0.5, z["neck"] + 0.016, 0.9, 24), (sx + 0.016, sz + 0.09, 0.7, 12),
                       (sx + 0.092, sz + 0.02, 0.6, 6), (sx + 0.118, z["chest"] - 0.05, 0.56, 4),
                       (sx + 0.14, hem_z, 0.56, 4)], hem=shawl_hem, thickness=0.008, folds=7, fold_depth=0.04,
                      inner=craft.felt(V["purple"]))
    on_shawl = an.probe(shawl)
    hem_stitch(craft, "Kit_ShawlStitch", on_shawl, V["candy"], hem_z + 0.03, -55, 235)
    fringe = []
    for i in range(140):
        t = i / 139
        a = math.radians(-90 + 5 + (360 - 10) * t)
        hit = on_shell(on_shawl, a, hem_z + shawl_hem(t) + 0.008)
        if hit:
            fringe.append((hit[0] - hit[1] * 0.003, hit[0] - hit[1] * 0.003 + Vector((0, 0, -0.06 - 0.008 * (i % 3)))))
    rods("Kit_ShawlFringe", craft.felt(V["purple"]), fringe, 0.0022)
    pin_at, pin_normal = on_shawl((0.085, -3, z["chest"] + 0.02), (0, 1, 0))
    amber = craft.smooth(C["amber"], 0.3)
    owl = [flat("Kit_OwlFace", amber, pm.smooth_outline([(0.0, 0.056), (0.042, 0.044), (0.058, 0.0), (0.04, -0.052),
                                                        (0.0, -0.066), (-0.04, -0.052), (-0.058, 0.0),
                                                        (-0.042, 0.044)], 5), 0.007)]
    for s in (1, -1):
        owl.append(pm.ellipsoid("Kit_OwlEye", craft.smooth(V["white"], 0.2), (s * 0.023, -0.002, 0.012),
                                (0.02, 0.006, 0.02), subsurf=1))
        owl.append(pm.ellipsoid("Kit_OwlPupil", craft.smooth(V["black"], 0.2), (s * 0.023, -0.007, 0.012),
                                (0.01, 0.004, 0.01), subsurf=1))
    owl.append(flat("Kit_OwlBeak", craft.smooth(V["orange"], 0.3), [(-0.01, -0.01), (0.01, -0.01), (0.0, -0.032)],
                    0.004, (0, -0.004, 0)))
    an.mount("Kit_OwlPin", pin_at + pin_normal * 0.007, pin_normal, owl)

    # Dark curls, an orange headscarf with coin trim and a knot at the side, big hoop earrings.
    hair = craft.felt(C["kit_hair"])
    _hair, on_hair = cap("Kit_Hair", hair, P, radii=(1.1, 1.12, 1.1), fringe_z=0.4, length_z=-0.6, face_half=50,
                         thickness=0.06, fall=0.99)
    for row, el in enumerate((-8, -24, -40)):  # rows of round curls below the scarf
        for k in range(15):
            lon = math.radians(-180 + 24 * k + 12 * (row % 2))
            if abs(lon) < math.radians(54):
                continue
            pm.ellipsoid("Kit_Curl", hair, on_hair(lon, math.radians(el), 0.99), (r * 0.2, r * 0.2, r * 0.18),
                         segments=16, rings=10, subsurf=1)
    scarf = craft.felt(V["orange"])
    cap("Kit_Scarf", scarf, P, radii=(1.2, 1.22, 1.18), offset=(0, 0.04, 0.12), fringe_z=0.46, length_z=0.02,
        face_half=62, thickness=0.04)
    band("Kit_ScarfTrim", craft.felt(V["purple"]), hc + Vector((0, r * 0.04, r * 0.5)), r * 1.19, r * 0.12,
         ys=1.02, thickness=0.004)
    gold = craft.smooth(V["candy"], 0.2)
    for k in range(7):
        x = (k - 3) * 0.19
        pm.ellipsoid("Kit_Coin", gold, surface(r * x, r * (0.4 - 0.05 * x * x), r * 0.014),
                     (r * 0.075, r * 0.018, r * 0.075), subsurf=1)
    knot = hc + Vector((-r * 1.28, r * 0.05, r * 0.22))
    pm.ellipsoid("Kit_ScarfKnot", scarf, knot, (r * 0.14, r * 0.16, r * 0.14), subsurf=1)
    for dx, dz in ((-0.1, -0.55), (0.12, -0.62)):
        pm.curve_tube("Kit_ScarfTail", scarf, [knot, knot + Vector((r * dx * 0.5, 0, r * dz * 0.5)),
                                               knot + Vector((r * dx, 0, r * dz))], r * 0.08, taper=(0.8, 1.2))
    for s in (1, -1):
        pm.torus("Kit_Hoop", gold, hc + Vector((s * r * 1.26, -r * 0.3, -r * 0.62)), r * 0.2, r * 0.022,
                 rotation=(math.pi / 2, 0, 0), segments=32, sides=8)

    # A crystal-ball lantern hanging from her right hand: a glowing glass sphere on a little stand.
    grip = grips[1]
    ball = grip + Vector((0, 0, -0.18))
    pm.ellipsoid("Kit_CrystalBall", craft.smooth(C["glow"], 0.1, emission=2.2), ball, (0.07, 0.07, 0.07), subsurf=2)
    brass = craft.smooth(V["candy"], 0.25)
    pm.torus("Kit_BallCollar", brass, ball + Vector((0, 0, -0.05)), 0.05, 0.008, segments=32, sides=8)
    stand = ell_lathe("Kit_BallStand", craft.smooth(V["violet"], 0.35), [(0.046, 0.0), (0.03, -0.03), (0.06, -0.055)],
                      yscale=1.0, thickness=0.006, segments=32)
    stand.location = ball + Vector((0, 0, -0.05))
    pm.torus("Kit_BallCap", brass, ball + Vector((0, 0, 0.058)), 0.038, 0.007, segments=32, sides=8)
    co.hang("Kit_BallHandle", brass, grip, (ball + Vector((0, -0.04, 0.062)), ball + Vector((0, 0.04, 0.062))))
    glow = bpy.data.lights.new("Kit_BallGlow", "POINT")
    glow.energy, glow.color, glow.shadow_soft_size = 2.5, C["glow"], 0.07
    lamp = bpy.data.objects.new("Kit_BallGlow", glow)
    lamp.location = ball + Vector((0, -0.1, 0.02))
    pm.link(lamp)
    return P


# --- lineup and review sheet -------------------------------------------------------------------------------

def build_bea_with_bike(craft):
    P = build_bea(craft)
    bike, _ = grouped("Bike", lambda: build_bike(craft))
    bike.location = (-0.88, 0.38, 0)
    bike.rotation_euler.z = math.radians(-24)
    bike.scale = (0.9, 0.9, 0.9)
    return P


# name, builder, width of the lineup tile in metres, where the tile is centred relative to the character
LINEUP = (("Bea", build_bea_with_bike, 2.0, -0.47), ("Pip", pip_vampire.build_pip, 0.95, 0.0),
          ("June", build_june, 1.0, 0.0), ("Kit", build_kit, 1.0, 0.0), ("Vale", build_vale, 1.0, -0.03))
GROUPS = [entry[0] for entry in LINEUP]
SPACING = 4.0  # metres between characters on the stage, so no shadow reaches a neighbour's tile
LINEUP_HEIGHT = 2.3  # metres of stage in an 800-pixel-high lineup tile
PIXELS_PER_METRE = 800 / LINEUP_HEIGHT


def build_cast(craft, only=None):
    """Build the cast along X; returns {name: (Proportions, x)}."""
    placed = {}
    for i, (name, build, _width, _centre) in enumerate(LINEUP):
        if only and name not in only:
            continue
        root, P = grouped(name, lambda: build(craft))
        root.location.x = (i - 2) * SPACING
        placed[name] = (P, root.location.x)
    return placed


def lineup_view(x, centre, lens=85):
    """Front camera, slightly raised, at one fixed distance for everyone so the tiles share one scale."""
    distance = LINEUP_HEIGHT * lens / 36
    return (x + centre, -distance, 1.85), (x + centre, 0.0, 1.0), lens


def portrait_view(P, x):
    hc = P.head_c
    return (x - 0.34, -1.75, hc.z + 0.14), (x, 0.0, hc.z + 0.04), 85


def main():
    out = sys.argv[sys.argv.index("--") + 1:][0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    placed = build_cast(craft_lib.Craft(1.2))
    cam = co.stage(lineup=True)
    bpy.context.scene.eevee.taa_render_samples = 32  # software GL: keeps the ten tiles to a few minutes
    tmp = os.path.splitext(out)[0]
    full, faces = [], []
    face_width = sum(round(entry[2] * PIXELS_PER_METRE) for entry in LINEUP) // 5
    for name, _build, width, centre in LINEUP:
        P, x = placed[name]
        solo(name)
        full.append(co.render_view(cam, *lineup_view(x, centre), round(width * PIXELS_PER_METRE), 800,
                                   f"{tmp}_{name}.png"))
        faces.append(co.render_view(cam, *portrait_view(P, x), face_width, 540, f"{tmp}_{name}_face.png"))
    sheet = np.concatenate([np.concatenate(faces, axis=1), np.concatenate(full, axis=1)], axis=0)  # rows bottom-up
    co.save_sheet(sheet, out)
    print("cast sheet ->", out)


if __name__ == "__main__":
    main()
