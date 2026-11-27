"""Quick as-built look at the paper lanterns, the table and the hook (procedural materials, before the export), for
iterating on shapes. The real review is `lanterns_review.py`, from the exported FBX.

Run: blender -b -t 2 --python tools/blender/env/lanterns_build_review.py -- out.png [draft|final]"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np
from mathutils import Vector

import env_lib as el
import lanterns as ln
from env_lib import H, T


def main():
    out, quality, _rest = el.args()
    final = quality == "final"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    craft = el.EnvCraft(1.2)
    el.stage_ground(craft, 40.0, H(0x211D3C))
    cam = el.setup_render(samples=32 if final else 12)
    el.night_world(0.6, top=H(0x241B4A), horizon=H(0x4A3378))
    el.sun_light("Moon", (1.2, 2.2, -2.0), 1.6, (0.72, 0.76, 1.0), 6.0, 30.0)
    el.sun_light("Fill", (-2.0, 1.0, -0.6), 0.5, (1.0, 0.8, 0.7), 6.0, 30.0)
    b = el.Builder(1)
    colours = list(ln.COLOURS)
    for i, shape in enumerate(ln.SHAPES):  # neutral paper on top, each shape in one colour below
        ln.paper_lantern(b, craft, shape, T(-0.9 + 0.6 * i, 0, 1.45))
        ln.paper_lantern(b, craft, shape, T(-0.9 + 0.6 * i, 0, 0.95), rgb=ln.COLOURS[colours[i]][1])
    b.finish("Lanterns")
    table = el.grouped("Table", lambda: ln.build_table(craft))[0]
    table.location = (3.0, 0.0, 0.0)
    hook = el.grouped("Hook", lambda: ln.build_hook(craft))[0]
    hook.location = (5.0, 0.0, 0.0)
    k = 1.0 if final else 0.6
    tiles = []
    tmp = os.path.splitext(out)[0]
    views = [
        ("front", Vector((0.0, -2.9, 1.2)), Vector((0.0, 0.0, 1.05)), 38, 1200, 700),
        ("side", Vector((1.9, -2.0, 1.4)), Vector((0.1, 0.3, 1.0)), 38, 1200, 700),
        ("table", Vector((3.0, -2.4, 1.6)), Vector((3.0, 0.0, 0.9)), 45, 1200, 700),
        ("hook", Vector((5.6, -2.6, 1.6)), Vector((5.15, 0.0, 1.6)), 45, 700, 700),
    ]
    for name, loc, target, lens, w, h in views:
        tiles.append(el.render_view(cam, loc, target, lens, int(w * k), int(h * k), f"{tmp}_{name}.png"))
    width = max(t.shape[1] for t in tiles)
    rows = [el.pad(t, width, t.shape[0]) for t in tiles]
    el.co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
    print("lanterns build review ->", out)


if __name__ == "__main__":
    main()
