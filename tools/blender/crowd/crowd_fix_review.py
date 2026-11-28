"""Before/after review of a change to the crowd kit, rendered from two sets of EXPORTED files (the files the game
loads): the committed ones and the new ones, side by side, in the review stage's light and in a night light like
the game's (moonlight and a warm porch light from the house side, a dark street, the walkers' backs in shadow).

    blender -b -t 2 --python tools/blender/crowd/crowd_fix_review.py -- <out_dir> --before DIR [--after DIR]
                                                                       [--only A,B] [--samples 24] [--no-lineup]

Writes:
  fix_lineup.png        the nine walkers with Pip at the gameplay camera (3.8 m, 50 degrees, phone frame), from
                        behind in the review light, from behind and from the front in the game's night light, and
                        the close lineup from behind in the night light; before on the left, after on the right
  fix_<Walker>.png      per walker: front, back and profile in the review light, back, front and the gameplay camera
                        crossing in the night light; before in the top row, after below; for the ghost also eight
                        Walk frames in profile and from the front three-quarter, Wave and Talk, before and after
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
for sub in ("style", "pip", "chars", "export", "crowd"):
    p = os.path.join(TOOLS, sub)
    if p not in sys.path:
        sys.path.insert(0, p)

import costume as co  # noqa: E402
import craft as craft_lib  # noqa: E402
import crowd_kit as kit  # noqa: E402
import crowd_review as cr  # noqa: E402
import witness_review as wr  # noqa: E402
from crowd_kit import H  # noqa: E402
from pip_review import label  # noqa: E402

NIGHT_GROUND = H(0x2E2D4C)  # the sidewalk's slate in the capture frames
NIGHT_SKY = (0.075, 0.056, 0.21, 1.0)  # linear
STUDIO_GROUND = H(0x1E1830)


class Lights:
    """Switches between the review stage's lights and a night light like the game's."""

    def __init__(self):
        scene = bpy.context.scene
        self.studio = [o for o in scene.objects if o.type == "LIGHT"]
        self.bg = scene.world.node_tree.nodes["Background"]
        self.studio_bg = (tuple(self.bg.inputs[0].default_value), self.bg.inputs[1].default_value)
        self.ground = bpy.data.materials["Ground"].node_tree.nodes["Principled BSDF"].inputs["Base Color"]
        self.night = []
        moon = bpy.data.lights.new("Moon", "SUN")
        moon.energy, moon.color, moon.angle = 0.25, (0.62, 0.66, 1.0), math.radians(8)
        self._add(moon, (0, 0, 0), Vector((0.5, 0.6, -1.0)))
        porch = bpy.data.lights.new("Porch", "AREA")
        porch.energy, porch.color, porch.size = 250.0, (1.0, 0.78, 0.5), 2.0
        self._add(porch, (0.5, 7.0, 3.2), Vector((0, 0, 0.7)) - Vector((0.5, 7.0, 3.2)))
        self.set(False)

    def _add(self, data, loc, direction):
        obj = bpy.data.objects.new(data.name, data)
        obj.location = loc
        obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        bpy.context.scene.collection.objects.link(obj)
        self.night.append(obj)

    def set(self, night):
        for o in self.studio:
            o.hide_render = night
        for o in self.night:
            o.hide_render = not night
        if night:
            # mostly a dim violet sky light: the capture frames show the walkers' fronts and backs about equally
            # lit (white felt at about sRGB 51, 44, 87 in 93b and 93c), as in the game's ambient-led night
            self.bg.inputs[0].default_value = NIGHT_SKY
            self.bg.inputs[1].default_value = 1.0
            self.ground.default_value = craft_lib.lin(NIGHT_GROUND)
        else:
            self.bg.inputs[0].default_value, self.bg.inputs[1].default_value = self.studio_bg
            self.ground.default_value = craft_lib.lin(STUDIO_GROUND)


def side_by_side(a, b, gap=8):
    pad = np.full((a.shape[0], gap, 4), 0.07, dtype=np.float32)
    pad[:, :, 3] = 1.0
    return np.concatenate([a, pad, b], axis=1)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out_dir = argv[0]

    def opt(name, default):
        return argv[argv.index(name) + 1] if name in argv else default
    before = opt("--before", "")
    after = opt("--after", os.path.join(wr.MODELS, "Characters", "Crowd"))
    cr.RECIPES.update(kit.WALKERS)
    only = [w for w in opt("--only", ",".join(kit.WALKERS)).split(",") if w]
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cam = co.stage(lineup=True)
    scene = bpy.context.scene
    scene.eevee.taa_render_samples = int(opt("--samples", "24"))
    lights = Lights()
    tmp = os.path.join(out_dir, "_tile")
    count = [0]

    def shot(location, target, lens, w, h, text="", size=0.03):
        count[0] += 1
        lab = None
        if text:
            cam.location = Vector(location)
            cam.data.lens = lens
            scene.render.resolution_x, scene.render.resolution_y = w, h
            cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
            lab = label(cam, text, size)
        px = co.render_view(cam, location, target, lens, w, h, f"{tmp}_{count[0]}.png")
        if lab:
            bpy.data.objects.remove(lab, do_unlink=True)
        return px

    sets = {}
    for tag, folder in (("before", before), ("after", after)):
        mat = cr.crowd_material(folder)
        sets[tag] = {w: cr.walker(folder, w, mat) for w in (only if "--no-lineup" in argv else kit.WALKERS)}
    pip = wr.import_model(os.path.join(wr.MODELS, "Characters", "Pip"), "Pip", leaf_bones=True)
    phone_lens = cr.game_lens(cr.PHONE[0] / cr.PHONE[1])

    def show(visible):
        for ms in sets.values():
            for m in ms.values():
                for o in m["all"]:
                    o.hide_render = m not in visible
        for o in pip["all"]:
            o.hide_render = pip not in visible

    # --- the lineup -------------------------------------------------------------------------------------------
    look = Vector((0, 0, 0.66))
    halves = {}
    for tag, ms in ([] if "--no-lineup" in argv else sets.items()):
        order = [ms[w] for w in kit.WALKERS]
        n = len(order) + 1
        xs = [(i - (n - 1) / 2) * cr.SPACING for i in range(n)]
        lineup = order[:n // 2] + [pip] + order[n // 2:]
        for m, x in zip(lineup, xs):
            cr.place(m, x)
            cr.set_pose(m, "Idle", 0)
            cr.turn(m, 180)
        show(lineup)
        tiles = []
        lights.set(False)
        tiles.append(shot(cr.orbit(look, cr.GAME_DISTANCE, 0, cr.GAME_PITCH + 8), look, phone_lens, *cr.PHONE,
                          f"{tag}: gameplay camera, from behind, review light", 0.03))
        lights.set(True)
        tiles.append(shot(cr.orbit(look, cr.GAME_DISTANCE, 0, cr.GAME_PITCH + 8), look, phone_lens, *cr.PHONE,
                          f"{tag}: gameplay camera, from behind, night light (porch beyond them)", 0.03))
        mid = Vector((0, 0, 0.8))
        tiles.append(shot(cr.orbit(mid, 9.5, 0, 6), mid, 36, *cr.PHONE,
                          f"{tag}: close, from behind, night light", 0.03))
        for m in lineup:
            cr.turn(m, 0)
        tiles.append(shot(cr.orbit(look, cr.GAME_DISTANCE, 0, cr.GAME_PITCH + 8), look, phone_lens, *cr.PHONE,
                          f"{tag}: gameplay camera, front, night light", 0.03))
        lights.set(False)
        halves[tag] = np.concatenate(tiles[::-1], axis=0)
        for m in lineup:
            cr.place(m, 0)
    if halves:
        co.save_sheet(side_by_side(halves["before"], halves["after"]), os.path.join(out_dir, "fix_lineup.png"))
        print("[fix_review] sheet -> fix_lineup.png", flush=True)

    # --- per walker ---------------------------------------------------------------------------------------------
    for wid in only:
        rows = []
        adult = kit.WALKERS[wid][0] != "CrowdChild"
        look = Vector((0, 0, 0.95 if adult else 0.58))
        dist = 5.0 if adult else 3.4
        for tag, ms in sets.items():
            m = ms[wid]
            show([m])
            cr.place(m, 0)
            tiles = []
            cr.set_pose(m, "Walk", 4)
            lights.set(False)
            for yaw, note in ((28, "front"), (180, "back"), (90, "profile")):
                tiles.append(shot(cr.orbit(look, dist, yaw, 6), look, 85, 211, 330, f"{tag} {note}", 0.07))
            lights.set(True)
            for yaw, note in ((180, "back, night"), (20, "front, night")):
                cr.turn(m, 180 if yaw == 180 else 0)
                tiles.append(shot(cr.orbit(look, dist, 0, 6), look, 85, 211, 330, f"{tag} {note}", 0.07))
            cr.turn(m, 0)
            pip_look = Vector((0, -1.6, 0.66))
            for yaw in (90,):
                cr.turn(m, yaw)
                tiles.append(shot(cr.orbit(pip_look, cr.GAME_DISTANCE, 180, cr.GAME_PITCH + 8), pip_look,
                                  phone_lens, 422, 195, f"{tag} game camera, night", 0.05))
            cr.turn(m, 0)
            lights.set(False)
            rows.append(cr.row(tiles, 1688))
        if wid == "GhostKid":
            n = cr.clip_len(sets["after"][wid], "Walk")
            frames = [round(i * n / 8) for i in range(8)]
            for yaw, note in ((28, "Walk"), (90, "Walk profile"), (160, "Walk back")):
                for tag, ms in sets.items():
                    m = ms[wid]
                    show([m])
                    tiles = []
                    for i, f in enumerate(frames):
                        cr.set_pose(m, "Walk", f)
                        tiles.append(shot(cr.orbit(look, dist, yaw, 6), look, 85, 211, 330,
                                          f"{f}" + (f"  {tag} {note}" if i == 0 else ""), 0.07))
                    rows.append(cr.row(tiles, 1688))
            for tag, ms in sets.items():
                m = ms[wid]
                show([m])
                tiles = []
                for clip, fr in (("Wave", (8, 16, 24, 34)), ("Talk", (0, 18, 36, 54))):
                    for f in fr:
                        cr.set_pose(m, clip, f)
                        tiles.append(shot(cr.orbit(look, dist, 20, 6), look, 85, 211, 330, f"{tag} {clip} {f}", 0.07))
                rows.append(cr.row(tiles, 1688))
        out = os.path.join(out_dir, f"fix_{wid}.png")
        co.save_sheet(np.concatenate(rows[::-1], axis=0), out)
        print("[fix_review] sheet ->", out, flush=True)


if __name__ == "__main__":
    main()
