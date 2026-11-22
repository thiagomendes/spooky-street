"""The opening key frame (spec §1): a child-height view past the candy bowl toward the dark blue owl house as
its round attic window lights up. Warm practicals against a cool violet night; the attic window holds the
brightest value, and the door, attic light and hedge separate in greyscale.

Run: blender -b --python tools/blender/env/keyframe.py -- out.png [draft|final]"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np
from mathutils import Vector

import env_lib as el
import owl_house as oh
import porch_kit as pk
import props as pr
from env_lib import E, R, S, T, Builder, H, grouped, lc, shade

CAM = Vector((1.4, -13.4, 1.05))  # a child's eye height above the street
YAW, PITCH_UP, LENS = -13.0, 10.5, 26.0


def bearing(deg, dist, z=0.0):
    """A point `dist` metres from the camera at `deg` right of straight north."""
    a = math.radians(deg)
    return Vector((CAM.x + dist * math.sin(a), CAM.y + dist * math.cos(a), z))


def build_ground(craft):
    b = Builder(201)
    street = craft.tinted(E["street"], 0.9, 0.25, 1.3)
    b.disc(street, 150.0, 0.2, R("X", -90), segs=40, tint=1.0)
    paver = craft.tinted(H(0x5A5380), 0.9, 0.25, 3.0)
    for i in range(-16, 17):
        b.box(paver, (1.17, 1.6, 0.14), (i * 1.2, -6.8, 0.05), bevel=0.03, tint=b.vary(0.8, 1.15), rot=R("Z", b.rng.uniform(-0.5, 0.5)))
    b.box(craft.tinted(H(0x3D3760), 0.9, 0.2, 3.0), (40.0, 0.16, 0.16), (0, -7.68, 0.04), bevel=0.03, tint=1.0)
    grass = craft.tinted(E["grass"], 0.95, 0.4, 2.2)
    b.box(grass, (80.0, 60.0, 0.1), (0, 24.0, -0.015), bevel=0, tint=1.0)
    rng = b.rng
    paper = craft.paper_white()
    for _ in range(70):  # fallen leaves across the street and pavement
        x, y = rng.uniform(-9, 8), rng.uniform(-13.5, -6.2)
        z = 0.125 if y > -7.6 else 0.006
        pr.leaf(b, craft, T(x, y, z + rng.uniform(0, 0.01)) @ R("Z", rng.uniform(0, 360)) @ R("X", rng.uniform(78, 88)), rng.uniform(0.1, 0.17),
                shade(rng.choice((E["leaf_a"], E["leaf_b"], E["leaf_c"])), rng.uniform(0.7, 1.0)), low=True)
    for _ in range(260):  # grass tufts along the lawn edge and path
        x, y = rng.uniform(-11, 11), rng.choice((rng.uniform(-5.95, -5.7), rng.uniform(-5.0, -2.8)))
        if abs(x) < 0.7 and y > -5.6:
            continue
        b.paper(paper, [(-0.025, 0), (0.025, 0), (0.0, rng.uniform(0.12, 0.24))], T(x, y, 0.03) @ R("Z", rng.uniform(0, 360)) @ R("X", rng.uniform(-10, 25)),
                tuple(c * rng.uniform(0.5, 1.0) for c in lc(E["hedge_light"])), fold=0.2, curl=0.5, cuts=0)
    b.finish("Ground")


def build_skyline(craft):
    """Neighbouring roofs and far trees: skyline only, a few dim windows."""
    b = Builder(202)
    dark = craft.tinted(H(0x231E44), 1.0, 0.15, 0.4)
    roof = craft.tinted(H(0x33255A), 1.0, 0.2, 0.5)
    for i, (x, y, w, h, rise, rz) in enumerate(((-13.5, 2.5, 7.0, 4.2, 3.2, 8), (-10.0, 19.0, 8.0, 5.0, 3.6, -5), (12.5, 1.5, 7.0, 3.8, 3.8, -10),
                                                 (15.0, 16.0, 8.0, 5.2, 3.0, 4), (5.0, 30.0, 9.0, 4.6, 3.4, 0), (-24.0, 12.0, 8.0, 4.4, 3.4, 12),
                                                 (26.0, 9.0, 8.0, 4.4, 3.4, -14))):
        with b.at(T(x, y, 0) @ R("Z", rz)):
            b.box(dark, (w, 6.0, h), (0, 0, h / 2), bevel=0, tint=1.0)
            b.prism(roof, [(-w / 2 - 0.4, 0.0), (w / 2 + 0.4, 0.0), (0.0, rise)], 6.6, T(0, -3.3, h), tint=1.0)
            b.box(dark, (0.6, 0.6, 1.6), (w * 0.25, 1.0, h + rise * 0.6), bevel=0, tint=0.9)
            for k in range(2):
                if (i + k) % 3 == 0:
                    continue
                b.box(craft.glow(E["amber"], 0.5), (0.7, 0.05, 0.9), (-w * 0.22 + k * w * 0.44, -3.02, h * 0.55), bevel=0, tint=1.0)
                b.box(dark, (0.06, 0.07, 0.9), (-w * 0.22 + k * w * 0.44, -3.03, h * 0.55), bevel=0, tint=1.0)
    tree = craft.tinted(H(0x2A2350), 1.0, 0.2, 0.6)
    for x, y, s in ((-19.0, 24.0, 1.0), (10.5, 24.0, 1.2), (20.0, 26.0, 0.9), (-5.0, 34.0, 1.3)):
        b.rod(dark, (x, y, 0), (x, y, 4.0 * s), 0.3 * s, 6, 1.0)
        for k in range(3):
            b.lathe(tree, [(0.0, -1.8), (2.2, -1.0), (2.6, 0.2), (1.6, 1.4), (0.0, 1.9)], 9,
                    T(x + (k - 1) * 1.6 * s, y, (5.2 + (k % 2) * 1.3) * s) @ S(s), radial=lambda a, i: 1 + 0.15 * math.cos(3 * a + i), tint=b.vary(0.85, 1.1))
    b.finish("Skyline")


def build_sky(craft):
    """Moon, stars and three paper bats crossing the moon, far behind everything."""
    b = Builder(203)
    rng = random.Random(7)
    moon_dir = (bearing(-21.5, 1.0) - CAM)
    moon = CAM + Vector((moon_dir.x, moon_dir.y, math.tan(math.radians(19.5)))) * 170.0
    face = (CAM - moon).to_track_quat("-Y", "Z").to_matrix().to_4x4()
    with b.at(T(*moon) @ face):
        b.disc(craft.glow(E["moon"], 0.92), 11.5, 0.2, segs=40, tint=1.0)
        for x, z, r in ((-3.5, 3.0, 2.4), (3.6, -1.0, 1.7), (-1.0, -5.0, 2.0), (4.5, 5.0, 1.1)):
            b.disc(craft.glow(H(0xD9CFAE), 0.8), r, 0.1, T(x, -0.2, z), segs=14, tint=1.0)
    star = craft.glow(H(0xFFF2C8), 1.6)
    for _ in range(150):
        az, elv = math.radians(rng.uniform(-75, 50)), math.radians(rng.uniform(7, 50))
        d = Vector((math.sin(az) * math.cos(elv), math.cos(az) * math.cos(elv), math.sin(elv)))
        p = CAM + d * 220.0
        s = rng.choice((0.2, 0.2, 0.28, 0.28, 0.4))
        q = (CAM - p).to_track_quat("-Y", "Z").to_matrix().to_4x4()
        b.poly(star, [tuple(p + q.to_3x3() @ Vector((s * math.cos(a), 0, s * math.sin(a)))) for a in (0, math.pi / 2, math.pi, 3 * math.pi / 2)], tint=1.0)
    ink = craft.tinted(H(0x1A1530), 1.0, 0.0)
    for k, (dx, dz, sc, tilt) in enumerate(((-9.0, 3.0, 5.0, 14), (5.0, 7.5, 3.6, -18), (10.0, -2.0, 2.8, 8))):
        with b.at(T(*(moon * 0.92 + CAM * 0.08)) @ face @ T(dx, 0, dz) @ R("Y", tilt)):
            pr.bat(b, ink, sc, T(), tint=(1, 1, 1), fold=0.3, curl=0.2)
    b.finish("Sky")
    return moon


def build_scene(craft, quality):
    grouped("House", lambda: oh.build_house(craft, attic=4.2))
    grouped("Ground", lambda: build_ground(craft))
    grouped("Skyline", lambda: build_skyline(craft))
    moon = build_sky(craft)

    # hedge on the west side of the path, picket fence and gate on the east
    for i, (x, w, h, seed) in enumerate(((-2.75, 3.1, 1.2, 21), (-5.95, 3.3, 1.28, 22), (-9.3, 3.4, 1.18, 23), (-12.7, 3.4, 1.25, 24))):
        hedge = pr.build_hedge(craft, w, 0.85, h, seed, f"Hedge{i}")
        hedge.location = (x, -5.35, 0.03)
    gate = pr.build_fence(craft, 3.4, 0.55, True, "FenceGate")
    gate.location = (0.72, -5.45, 0.03)
    run = pr.build_fence(craft, 7.0, 0.0, False, "FenceRun")
    run.location = (5.95, -5.45, 0.03)
    run2 = pr.build_fence(craft, 7.0, 0.0, False, "FenceRun2")
    run2.location = (12.95, -5.45, 0.03)

    lamp_root, lamp = grouped("StreetLamp", lambda: pr.build_street_lamp(craft, 3.3))
    lamp_root.location = (3.0, -6.7, 0.12)
    box_root, _ = grouped("Mailbox", lambda: pr.build_mailbox(craft))
    box_root.location = (-1.75, -6.3, 0.12)
    box_root.rotation_euler.z = math.radians(12)
    maple_root, _ = grouped("Maple", lambda: pr.build_maple(craft, treehouse=False))
    maple_root.location = (-4.6, -7.1, 0.1)
    maple_root.rotation_euler.z = math.radians(160)
    maple_root.scale = (1.1, 1.1, 1.1)
    pk_root, _ = grouped("YardPumpkins", lambda: pr.build_pumpkins(craft))
    pk_root.location = (2.0, -6.15, 0.12)
    pk_root.rotation_euler.z = math.radians(-12)

    # the candy bowl on an arrival-porch newel in the near foreground
    spot = bearing(YAW - 25.0, 1.75)

    def foreground():
        pr.DETAIL = 2
        b = Builder(204)
        P = pk.paints(craft)
        with b.at(T(spot.x, spot.y, 0)):
            pk.post(b, P, 0.8, ())
            b.box(P["trim_x"], (0.62, 0.62, 0.05), (0, 0, 0.82), bevel=0.02)
            b.box(P["orange"], (0.5, 0.5, 0.03), (0, 0, 0.785), bevel=0.01, tint=1.0)
            with b.at(T(-1.05, 0.25, 0) @ R("Z", -14)):
                pk.rail(b, P, 1.9, rhythm=1)
            pr.candy_bowl(b, craft, 0.3, T(0, 0, 0.845) @ R("Z", 122), spill=False, heap=54, glow=1.3)
            for x, y, kind, a, rz in ((0.22, -0.2, 0, "purple", 30), (0.26, 0.02, 3, "lime", 0)):
                pr.candy(b, craft.paint(), kind, T(x, y, 0.845 + 0.03) @ R("Z", rz) @ (R("X", 90) if kind == 0 else T()), el.V[a], el.V["bone"], 1.3)
        b.finish("Foreground")
        pr.DETAIL = 1
    grouped("Foreground", foreground)

    import pip_vampire
    pip_root, _ = grouped("Pip", lambda: pip_vampire.build_pip(craft))
    at = bearing(YAW - 5.5, 4.9)
    pip_root.location = (at.x, at.y, 0)
    pip_root.rotation_euler.z = math.radians(180 - 24)

    # a paper bat slipping sideways off the porch
    def falling():
        b = Builder(205)
        pr.bat(b, craft.paper_white(), 0.34, T(2.55, -2.3, 2.0) @ R("Y", 62) @ R("X", 20), fold=0.3)
        b.finish("FallingBat")
    grouped("FallingBat", falling)

    # --- light: cool night against warm practicals ---
    el.night_world(0.42, top=H(0x17143C), horizon=H(0x6B4496))
    to_scene = (Vector((0, 0, 3.0)) - moon).normalized()
    el.sun_light("Moon", to_scene, 1.5, (0.56, 0.66, 1.0), 3.0, 70.0)
    el.sun_light("SkyFill", (0.5, 1.0, -0.55), 0.3, (0.6, 0.55, 1.0), 20.0, 70.0)
    el.point_light("StreetWarm", (CAM.x + 2.4, CAM.y + 0.5, 3.0), 420, (1.0, 0.72, 0.42), 0.4, shadow=True)
    for name, k in (("PorchLamp", 2.2), ("StreetLampGlow", 2.5), ("AtticGlow", 1.5)):
        bpy.data.objects[name].data.energy *= k
    for mat in bpy.data.materials:  # the street lantern must stay under the attic window in value
        if mat.name.startswith("Glow_") and mat.name.endswith("_2.6"):
            mat.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 1.15
    return moon


def luminance(px):
    return 0.2126 * px[:, :, 0] + 0.7152 * px[:, :, 1] + 0.0722 * px[:, :, 2]


def main():
    out, quality, _rest = el.args()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    craft = el.EnvCraft(1.2)
    build_scene(craft, quality)
    final = quality == "final"
    cam = el.setup_render(samples=48 if final else 12, exposure=0.0, bloom=0.09)
    cam.data.sensor_width = 36.0
    cam.data.dof.use_dof = True
    cam.data.dof.focus_distance = 12.0
    cam.data.dof.aperture_fstop = 5.6
    bpy.context.scene.eevee.bokeh_max_size = 20.0
    width = 2000 if final else 1000
    height = round(width * 9 / 16)
    a, p = math.radians(YAW), math.radians(PITCH_UP)
    target = CAM + Vector((math.sin(a) * math.cos(p), math.cos(a) * math.cos(p), math.sin(p))) * 10.0
    for name in ("House", "Maple", "Foreground", "Pip", "StreetLamp"):
        root = bpy.data.objects.get(name)
        if root:
            print(f"TRIS {name}: {el.triangles(root)}", flush=True)
    tmp = os.path.splitext(out)[0]
    px = el.render_view(cam, CAM, target, LENS, width, height, tmp + "_tmp.png")
    el.co.save_sheet(px, out)
    grey = px.copy()
    lum = np.clip(luminance(px), 0, 1) ** (1 / 2.2)  # display-referred value check (pixels load as linear)
    grey[:, :, 0] = grey[:, :, 1] = grey[:, :, 2] = lum ** 2.2
    el.co.save_sheet(grey, tmp + "_grey.png")
    print("key frame ->", out)


if __name__ == "__main__":
    main()
