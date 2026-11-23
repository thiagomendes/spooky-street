"""Review of Pip's wall and ledge clips, rendered from the RE-IMPORTED Pip.fbx (the file the game loads).

    blender -b -t 2 --python tools/blender/export/pip_climb_review.py -- <out_dir> [--dir DIR] [--only WallKick,…]
                                                                         [--rows side,quarter,check,close,game]
                                                                         [--samples 32]

Writes one sheet per clip into <out_dir> (pip_<clip>.png), six frames per row:
  side     Pip alone from her right side
  quarter  Pip alone from the front three-quarter
  check    from the side against the wall or the ledge, placed where the GAME puts them: the motor's root motion is
           applied (the kick's launch, the climb's rise and step), so the hands and feet can be seen meeting them
  close    the contact up close: the hand on the edge, the soles and the palm on the wall
  game     the same from above, about where the follow camera looks from (behind her at a ledge, in front of her
           after a kick); for the climb the camera stays with the ledge, so the whole path shows
It also prints, for every frame of each clip, where the gripping hand and the feet are relative to the wall or the
edge, and how far the pail and the body stay clear of the face. Game numbers (ClimbRules / PipMovementTuning):
hang 0.34 m out and 1.27 m under the top, climb 0.4 s (60 % rise, then 0.4 m in), kick 4.5 m/s away and 1.2 m up.
The committed evidence (docs/evidence/s003/pip_climb_clips/*.jpg) is these sheets saved as JPEG.
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
for sub in ("style", "pip", "chars", "export"):
    sys.path.insert(0, os.path.join(TOOLS, sub))

import costume as co  # noqa: E402
import pip_model as pm  # noqa: E402
from pip_review import DEFAULT_DIR, import_pip, label  # noqa: E402

FPS = 30.0
HANG_OUT, HANG_HAND = 0.34, 1.27  # LedgeHangWallOffset, LedgeHangHandHeight
CLIMB_TIME, CLIMB_RISE, CLIMB_INSET, CLIMB_LIFT = 0.4, 0.6, 0.4, 0.06
KICK_AWAY, KICK_HEIGHT, GRAVITY, WALL_CONTACT = 4.5, 1.2, 25.0, 0.31


def smooth(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def still(_t):
    return 0.0, 0.0


def kick_motion(t):
    v0 = math.sqrt(2 * GRAVITY * KICK_HEIGHT)
    return KICK_AWAY * t, v0 * t - 0.5 * GRAVITY * t * t


def climb_motion(t):
    rise_end = CLIMB_TIME * CLIMB_RISE
    return (smooth((t - rise_end) / (CLIMB_TIME - rise_end)) * (HANG_OUT + CLIMB_INSET),
            smooth(t / rise_end) * (HANG_HAND + CLIMB_LIFT))


# clip -> prop ("wall" behind Pip or "ledge" ahead), root motion (seconds -> metres forward, up), review frames,
# check-row frames, (check camera: distance, look-at in the prop's frame or None to follow Pip)
CLIPS = {
    "WallKick": ("wall", kick_motion, (0, 1, 2, 4, 7, 10), (0, 1, 2, 3, 5, 8), None),
    "LedgeGrab": ("ledge", still, (0, 1, 2, 3, 4, 6), (0, 1, 2, 3, 4, 6), None),
    "LedgeHang": ("ledge", still, (0, 8, 16, 24, 32, 40), (0, 8, 16, 24, 32, 40), None),
    "LedgeClimb": ("ledge", climb_motion, (0, 3, 5, 7, 9, 12), (0, 3, 5, 7, 9, 12), (5.8, (0.0, 0.05, 1.42))),
}


def box(name, lo, hi, colour):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((lo[i] + (v.co[i] + 0.5) * (hi[i] - lo[i]) for i in range(3)))
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*colour, 1)
    bsdf.inputs["Roughness"].default_value = 0.9
    mat.shadow_method = "NONE"  # the stage's key light stands on the far side of the props
    obj = pm.new_mesh_object(name, bm, mat)
    return obj


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out = argv[0]

    def opt(name, default):
        return argv[argv.index(name) + 1] if name in argv else default
    folder = opt("--dir", DEFAULT_DIR)
    only = opt("--only", ",".join(CLIPS)).split(",")
    rows = opt("--rows", "side,quarter,check,close,game").split(",")
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rig, meshes, _sizes = import_pip(folder)
    actions = {a.name.split("|")[-1]: a for a in bpy.data.actions}
    print("[pip_climb_review] actions", {k: int(a.frame_range[1] - a.frame_range[0]) for k, a in sorted(actions.items())})
    cam = co.stage()
    scene = bpy.context.scene
    scene.eevee.taa_render_samples = int(opt("--samples", 32))
    ground = bpy.data.objects["Ground"]
    # the wall behind Pip (its face at y = 0) and the ledge ahead (its top edge at the origin), both cut off on the
    # camera's side so the contact is not hidden; they are moved into place per frame
    wall = bpy.data.objects.new("WallProp", None)
    ledge = bpy.data.objects.new("LedgeProp", None)
    for e in (wall, ledge):
        scene.collection.objects.link(e)
    box("Wall", (-0.5, 0.0, -3.0), (0.6, 0.3, 4.0), (0.2, 0.16, 0.3)).parent = wall
    box("Ledge", (-0.5, -1.4, -3.0), (0.6, 0.0, 0.0), (0.2, 0.16, 0.3)).parent = ledge
    box("LedgeTop", (-0.5, -1.4, -0.02), (0.6, 0.0, 0.004), (0.5, 0.36, 0.2)).parent = ledge
    tmp = os.path.join(out, "_tile")

    def set_pose(clip, frame):
        rig.animation_data_create()
        rig.animation_data.action = actions[clip]
        scene.frame_set(int(actions[clip].frame_range[0]) + frame)
        bpy.context.view_layer.update()

    def place(prop, motion, frame, shown):
        """Props in Pip's frame: Pip's root has moved (forward, up) since the clip began."""
        fwd, up = motion(frame / FPS)
        wall.location = (0, WALL_CONTACT + fwd, -up)
        ledge.location = (0, -HANG_OUT + fwd, HANG_HAND - up)
        for e, kind in ((wall, "wall"), (ledge, "ledge")):
            for c in e.children:
                c.hide_render = not (shown and prop == kind)
        return Vector((0, fwd, -up))  # where the clip's starting root is, in Pip's frame

    def shot(location, target, lens, text, w=300, h=360):
        scene.render.resolution_x, scene.render.resolution_y = w, h
        lab = label(cam, text, 0.065)
        px = co.render_view(cam, location, target, lens, w, h, tmp + ".png")
        bpy.data.objects.remove(lab, do_unlink=True)
        return px

    def orbit(target, yaw, pitch, dist):
        a, e = math.radians(yaw), math.radians(pitch)
        return target + dist * Vector((-math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))

    def world_points(group_names):
        """Evaluated world positions of the vertices mostly weighted to the given bones."""
        dg = bpy.context.evaluated_depsgraph_get()
        pts = []
        for o in meshes:
            idx = {g.index for g in o.vertex_groups if g.name in group_names}
            if not idx:
                continue
            ev = o.evaluated_get(dg)
            me = ev.to_mesh()
            mw = ev.matrix_world
            for v, src in zip(me.vertices, o.data.vertices):
                if src.groups and max(src.groups, key=lambda g: g.weight).group in idx:
                    pts.append(mw @ v.co)
            ev.to_mesh_clear()
        return pts

    def measure(clip, prop, motion):
        n = int(actions[clip].frame_range[1] - actions[clip].frame_range[0])
        print(f"[measure] {clip}: {n} frames, {n / FPS:.3f} s")
        body = ({b.name for b in rig.data.bones if not b.name.startswith("Cape")}
                - {"Pail", "Hand_L", "Fingers_L", "Thumb_L", "LowerArm_L"})
        for f in range(0, n + 1, 1 if n <= 12 else 4):
            set_pose(clip, f)
            place(prop, motion, f, False)
            mw = rig.matrix_world
            hand = {S: mw @ rig.pose.bones["Fingers_" + S].head for S in "LR"}
            toes = {S: world_points({"Toe_" + S, "Foot_" + S}) for S in "LR"}
            pail = world_points({"Pail"})
            if prop == "ledge":
                e = ledge.location
                inside = lambda p: e.y - p.y if p.z < e.z else -9.0  # noqa: E731 depth into the face under the top
                under = lambda p: e.z - p.z if p.y < e.y else -9.0  # noqa: E731 depth under the top beyond the edge
                clip_of = lambda pts: max((min(inside(p), under(p)) for p in pts), default=-9.0)  # noqa: E731
                worst = max(((clip_of(world_points({b})), b) for b in sorted(body)), default=(0, "-"))
                line = (f"  f{f:02d} edge y{e.y:+.2f} z{e.z:+.2f} | L knuckle fwd of edge {e.y - hand['L'].y:+.3f} above {hand['L'].z - e.z:+.3f}"
                        f" | R knuckle fwd {e.y - hand['R'].y:+.3f} above {hand['R'].z - e.z:+.3f}"
                        f" | into ledge: pail {clip_of(pail):+.3f} body {worst[0]:+.3f} {worst[1]}"
                        f" | feet low z {min(p.z for S in 'LR' for p in toes[S]):+.3f}")
            else:
                wy = wall.location.y
                line = (f"  f{f:02d} wall y{wy:+.2f} | gap to wall: L foot {wy - max(p.y for p in toes['L']):+.3f}"
                        f" R foot {wy - max(p.y for p in toes['R']):+.3f} L knuckle {wy - hand['L'].y:+.3f} (z {hand['L'].z:+.2f})"
                        f" pail {wy - max(p.y for p in pail):+.3f} body {wy - max(p.y for p in world_points(body)):+.3f}")
            print(line)

    for clip in only:
        prop, motion, frames, check_frames, check_cam = CLIPS[clip]
        measure(clip, prop, motion)
        ground.hide_render = True
        sheet_rows = []
        for row in rows:
            tiles = []
            for i in range(6):
                f = (check_frames if row in ("check", "close", "game") else frames)[i]
                set_pose(clip, f)
                start = place(prop, motion, f, row in ("check", "close", "game"))
                text = f"{clip} {f}" + (f"  {row}" if i == 0 else "")
                low = motion is climb_motion  # her legs hang under the root while she mantles
                if row == "side":
                    t = Vector((0, -0.08, 0.5 if low else 0.7))
                    tiles.append(shot(orbit(t, 90, 5, 4.5 if low else 3.7), t, 85, text))
                elif row == "quarter":
                    t = Vector((0, -0.08, 0.5 if low else 0.7))
                    tiles.append(shot(orbit(t, 38, 8, 4.5 if low else 3.7), t, 85, text))
                elif row == "check":
                    if low:
                        t = Vector((0, -0.12, 0.5))
                        tiles.append(shot(orbit(t, 90, 3, 4.5), t, 85, text))
                    else:
                        t = Vector((0, -0.08, 0.72 if prop == "ledge" else 0.62))
                        tiles.append(shot(orbit(t, 90, 3, 3.9), t, 85, text))
                elif row == "close":  # the contact itself: the hand on the edge, the soles on the wall
                    if prop == "ledge":
                        t = Vector((0.1, ledge.location.y + 0.02, max(ledge.location.z, 0.1) - 0.05))
                        tiles.append(shot(orbit(t, 244, 18, 1.9), t, 85, text))  # from her left: the gripping side
                    else:
                        t = Vector((0.0, min(wall.location.y, 0.75) - 0.2, 0.45))
                        tiles.append(shot(orbit(t, 72, 6, 2.4), t, 85, text))
                else:  # game: from above, a little to one side
                    if check_cam:
                        t = start + Vector((0, -HANG_OUT, 0)) + Vector(check_cam[1])
                        tiles.append(shot(orbit(t, 200, 18, check_cam[0]), t, 85, text))
                    else:
                        # the follow camera stays where it was: behind her at a ledge, in front of her after a kick
                        t = Vector((0, -0.08, 0.72))
                        tiles.append(shot(orbit(t, 205 if prop == "ledge" else 28, 18, 4.2), t, 85, text))
            sheet_rows.append(np.concatenate(tiles, axis=1))
        if not sheet_rows:
            continue
        sheet = np.concatenate(sheet_rows[::-1], axis=0)  # image rows run bottom-up
        path = os.path.join(out, f"pip_{clip}.png")
        co.save_sheet(sheet, path)
        print("[pip_climb_review] sheet ->", path, sheet.shape)


if __name__ == "__main__":
    main()
