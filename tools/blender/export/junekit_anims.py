"""Clips for June (the missing witness, in the attic and later on the porch) and Aunt Kit (the owner, at the
reveal), spec §2, §3.

Same pose vocabulary and solver conventions as pip_anims.py / witness_anims.py (pure maths, mathutils only): a
pose is a dict of scalars in degrees / metres, `solve` turns it into one rotation per bone as a delta in the
character's own axes (X = the character's left, -Y = forward, Z = up) carried by the parent bone. On top of
Pip's vocabulary:

  June   cape<C><i>_back / _flare   her floor-length cape: chains C = L, M, R of three bones each, absolute
         cape<S><i>_open            (they hang plumb whatever the chest does). The side chains start over the
         cape_sway, cape_arm        shoulders and are swung by the upper arms (`cape_arm` = how much), so a
                                    raised arm lifts the cloak; `open` turns a side panel outward like a door,
                                    which lets a forearm through the front of the cape
         braid<i>_x / _y            the braid over her left shoulder hangs plumb and swings (back +, left +)
         hat_back / hat_side        the crooked tip of the witch hat
  Kit    lantern_x / _y             the crystal-ball lantern hangs plumb from her right hand (`Lantern`)
         shawl_follow               how much the shawl's wings (`Shawl_L/R`) follow the upper arms
         skirt_x / _y, tail_x / _y, ringL_x / _y, ringR_x / _y   skirt, scarf tails and hoop earrings: plumb
  both   hand<S>_twist              pronation of the hand at the cuff

The exporter calls `bind(rig)` first: seated and pointing poses are fitted to the skeleton with a small numeric
IK (`reach`). All clips are in place; June's Walk is planted for JUNE_WALK_SPEED (the game moves her root).
"""
import math

import numpy as np
from mathutils import Matrix, Vector

import pip_anims as pa
from pip_anims import SIDES, TAU, bump, keyed, rx, ry, rz, smooth

FPS = pa.FPS
JUNE_SEAT = 0.30  # height of the low stool / attic floor edge June's KneeTap is fitted to
# KneeTap: frames (of 72, at 30 fps) on which her right hand lands on her knee: short, short, pause, last
TAP_FRAMES = (6, 15, 36)
PLUMB = {"Braid_1": "braid1", "Braid_2": "braid2", "Skirt": "skirt", "ScarfTail": "tail", "Earring_L": "ringL",
         "Earring_R": "ringR"}
CAPE_FOLLOW = (1.0, 0.3, 0.05)  # how much of the upper arm's swing each level of a side chain takes

DEFAULTS = dict(pa.DEFAULTS)
DEFAULTS.update({"lantern_x": 0.0, "lantern_y": 0.0, "lantern_world": 1.0, "hat_back": 0.0, "hat_side": 0.0,
                 "cape_arm": 1.0, "shawl_follow": 1.0})
for _s in "LR":
    DEFAULTS[f"hand{_s}_twist"] = 0.0  # turns the palm down when the forearm points forward (+), at the cuff
    for _i in (1, 2, 3):
        DEFAULTS[f"cape{_s}{_i}_open"] = 0.0
for _k in PLUMB.values():
    DEFAULTS.update({_k + "_x": 0.0, _k + "_y": 0.0})

RIG = None


def bind(rig):
    """rig: bone -> (parent or None, rest head Vector) in parents-first order, plus rig["_sole"] (see solve)."""
    global RIG
    RIG = rig


def pose(**kw):
    p = dict(DEFAULTS)
    for k, v in kw.items():
        if k not in p:
            raise KeyError(k)
        p[k] = v
    return p


def blend(a, b, t):
    return {k: a[k] + (b[k] - a[k]) * t for k in a}


# --- solver -----------------------------------------------------------------------------------------------

def fk(p, rig):
    """Pose -> (deltas {bone: 3x3}, offsets {bone: Vector}, absolute rotations, joint positions)."""
    D, W = {}, {}
    D["Hips"] = rz(p["hip_yaw"]) @ ry(p["hip_roll"]) @ rx(p["hip_pitch"])
    D["Spine"] = rz(p["spine_yaw"]) @ ry(p["spine_roll"]) @ rx(p["spine_pitch"])
    D["Chest"] = rz(p["chest_yaw"]) @ ry(p["chest_roll"]) @ rx(p["chest_pitch"])
    D["Neck"] = rx(p["neck_pitch"])
    head = rz(p["head_yaw"]) @ ry(p["head_roll"]) @ rx(p["head_pitch"])
    if p["head_world"] > 0.5:
        W["Head"] = head
    else:
        D["Head"] = head
    D["HatTip"] = rx(-p["hat_back"]) @ ry(p["hat_side"])
    arm_rest = {}
    for S, s in SIDES:
        D["Shoulder_" + S] = ry(-s * p[f"shoulder{S}"])
        D["UpperArm_" + S] = rx(-p[f"arm{S}_fwd"]) @ ry(-s * p[f"arm{S}_out"]) @ rz(-s * p[f"arm{S}_twist"])
        D["LowerArm_" + S] = rx(-p[f"elbow{S}"])
        D["Hand_" + S] = rx(-p[f"hand{S}_pitch"]) @ ry(-s * p[f"hand{S}_side"]) @ rz(-s * p[f"hand{S}_twist"])
        D["Fingers_" + S] = ry(s * p[f"fingers{S}"])
        D["Thumb_" + S] = ry(s * p[f"thumb{S}"])
        D["UpperLeg_" + S] = rz(s * p[f"leg{S}_twist"]) @ ry(-s * p[f"leg{S}_out"]) @ rx(-p[f"leg{S}_fwd"])
        D["LowerLeg_" + S] = rx(p[f"knee{S}"])
        if p["feet_world"] > 0.5:
            W["Foot_" + S] = rx(p[f"foot{S}"])
        else:
            D["Foot_" + S] = rx(p[f"foot{S}"])
        D["Toe_" + S] = rx(-p[f"toe{S}"])
        arm_rest[S] = (rig["LowerArm_" + S][1] - rig["UpperArm_" + S][1]).normalized()
    for bone, key in PLUMB.items():
        W[bone] = ry(p[key + "_y"]) @ rx(-p[key + "_x"])
    lantern_w = ry(p["lantern_y"]) @ rx(-p["lantern_x"])

    def swing(S, A, frame=None):
        """Shortest rotation taking the rest upper arm to where it points now (in `frame`'s axes)."""
        now = A["UpperArm_" + S] @ arm_rest[S]
        if frame is not None:
            now = frame.transposed() @ now
        return arm_rest[S].rotation_difference(now).to_matrix()

    def forward(offset):
        A, pos = {}, {}
        for name, (parent, head_at) in rig.items():
            if name.startswith("_"):
                continue
            if parent is None:
                A[name] = D.get(name, Matrix.Identity(3))
                pos[name] = head_at.copy()
                continue
            if name in W:
                D[name] = A[parent].transposed() @ W[name]
            elif name == "Lantern":
                D[name] = pa._lerp_rot(A[parent].transposed() @ lantern_w, p["lantern_world"]) \
                    if p["lantern_world"] > 0 else lantern_w
            elif name.startswith("Cape_"):
                c, i = name[5], int(name[6])
                s = {"L": 1.0, "M": 0.0, "R": -1.0}[c]
                hang = rx(p[f"cape{c}{i}_back"]) @ ry(p["cape_sway"] - s * p[f"cape{c}{i}_flare"])
                if s:
                    hang = (pa._lerp_rot(swing(c, A), CAPE_FOLLOW[i - 1] * p["cape_arm"])
                            @ rz(s * p[f"cape{c}{i}_open"]) @ hang)
                D[name] = A[parent].transposed() @ hang
            elif name.startswith("Shawl_"):
                D[name] = pa._lerp_rot(swing(name[-1], A, A["Chest"]), p["shawl_follow"])
            A[name] = A[parent] @ D.get(name, Matrix.Identity(3))
            pos[name] = pos[parent] + A[parent] @ (head_at - rig[parent][1])
            if name == "Hips":
                pos[name] += offset
        return A, pos

    offset = Vector((p["hx"], p["hy"], p["hz"]))
    A, pos = forward(offset)
    if p["ground"] > 0:
        low = min((pos[b] + A[b] @ (pt - rig[b][1])).z for side in rig["_sole"].values() for b, pt in side)
        offset.z -= low * p["ground"]
        A, pos = forward(offset)
    return D, {"Hips": offset}, A, pos


def solve(p, rig=None):
    """Pose -> ({bone: 3x3 delta in parent-carried character axes}, {bone: offset from rest})."""
    D, offsets, _A, _pos = fk(p, rig or RIG)
    return D, offsets


def point(p, bone, local=None):
    """Where a point of `bone` (rest-space `local`, default its head) is in the pose."""
    _D, _o, A, pos = fk(p, RIG)
    if local is None:
        return pos[bone]
    return pos[bone] + A[bone] @ (Vector(local) - RIG[bone][1])


def reach(p, keys, bone, target, local=None, steps=14, axes=(0, 1, 2)):
    """Numeric IK: adjust the pose angles `keys` until `bone`'s point is at `target` (damped least squares)."""
    target = Vector(target)
    for _ in range(steps):
        at = point(p, bone, local)
        err = np.array([(target - at)[i] for i in axes])
        if np.linalg.norm(err) < 2e-4:
            break
        J = np.zeros((len(axes), len(keys)))
        for j, k in enumerate(keys):
            q = dict(p)
            q[k] += 1.0
            d = point(q, bone, local) - at
            J[:, j] = [d[i] for i in axes]
        step = J.T @ np.linalg.solve(J @ J.T + 1e-7 * np.eye(len(axes)), err)
        step = np.clip(step, -20.0, 20.0)
        for j, k in enumerate(keys):
            p[k] += float(step[j])
    return p


def plant(p, spread=0.0):
    """Keep both ankles where they are in the rest pose (feet flat) whatever the hips do."""
    for S, s in SIDES:
        rest = RIG["Foot_" + S][1]
        reach(p, [f"leg{S}_fwd", f"leg{S}_out", f"knee{S}"], "Foot_" + S, rest + Vector((s * spread, 0, 0)))
    return p


def aim_arm(p, S, direction, bend=12.0):
    """Point the arm of side S (shoulder to fingertips) along `direction` (character axes)."""
    p[f"elbow{S}"] = bend
    sh = point(p, "UpperArm_" + S)
    tip = RIG["Fingers_" + S][1]
    length = (RIG["LowerArm_" + S][1] - RIG["UpperArm_" + S][1]).length + (tip - RIG["LowerArm_" + S][1]).length
    d = Vector(direction).normalized()
    p[f"arm{S}_out"] = max(p[f"arm{S}_out"], 20.0)
    p[f"arm{S}_twist"] = 40.0 if d.y < -0.3 else p[f"arm{S}_twist"]
    reach(p, [f"arm{S}_out", f"arm{S}_twist", f"arm{S}_fwd"], "Fingers_" + S, sh + d * length * 0.985, steps=24)
    return p


# --- June -------------------------------------------------------------------------------------------------
# Her cape wraps forward over her arms; at rest her hands are inside it. The torn panel is on her left, so her
# right hand does the talking, waving, tapping and pointing, and the tear stays in view.

def _cape(p, a, back=(1.0, 2.0, 3.0), flare=(0.5, 1.5, 2.5), amp=1.0, sway=1.5):
    """Standing cape: a slow travelling ripple; `back` / `flare` are the mean lean of each level."""
    for c in "LMR":
        lag = 0.0 if c == "M" else (0.7 if c == "L" else 1.3)
        for i in (1, 2, 3):
            wave = math.sin(a - 0.7 * i - lag)
            if c == "M":
                p[f"cape{c}{i}_back"] = back[i - 1] + amp * 1.4 * i * 0.6 * wave
            else:
                p[f"cape{c}{i}_back"] = amp * 0.5 * wave
                p[f"cape{c}{i}_flare"] = flare[i - 1] + amp * 0.9 * i * 0.6 * wave
    p["cape_sway"] = sway * math.sin(a - 1.0)
    return p


def _june_stand(**kw):
    p = pose(ground=1.0, feet_world=1.0, kneeL=4, kneeR=4, legL_fwd=2, legR_fwd=2,
             armL_out=-3, armR_out=-3, armL_fwd=3, armR_fwd=3, elbowL=12, elbowR=12, armL_twist=6, armR_twist=6,
             fingersL=8, fingersR=8)
    p.update(kw)
    return p


def june_idle(u):
    """At the attic window: looking out and a little up, a slow sweep of the street, one peek on tiptoe."""
    a = TAU * u
    breathe = math.sin(2 * a)
    shift = math.sin(a)
    peek = bump(a, 3.9, 0.55)
    look = math.sin(a - 0.6)
    p = _june_stand()
    p.update(hx=0.012 * shift, hip_roll=-1.4 * shift, spine_roll=0.9 * shift, chest_roll=0.7 * shift,
             hy=-0.008 * peek, footL=13 * peek, footR=13 * peek, toeL=10 * peek, toeR=10 * peek,
             hip_pitch=1.5 * peek, spine_pitch=1.0 + 0.8 * breathe + 2.0 * peek, chest_pitch=-1.0 * breathe - 1.5 * peek,
             chest_yaw=4 * look, spine_yaw=2 * look,
             neck_pitch=-3 - 3 * peek, head_pitch=-7 + 1.2 * breathe - 5 * peek, head_yaw=13 * look,
             head_roll=2.5 * math.sin(a + 0.9) + 3 * peek,
             shoulderL=1.5 * (0.5 + 0.5 * breathe) + 3 * peek, shoulderR=1.5 * (0.5 + 0.5 * breathe) + 3 * peek,
             armL_fwd=3 + 2 * math.sin(a - 0.7), armR_fwd=3 - 2 * math.sin(a - 1.0),
             elbowL=12 + 3 * math.sin(a - 1.1) + 5 * peek, elbowR=12 + 3 * math.sin(a - 1.5) + 5 * peek,
             braid1_y=2.5 * math.sin(a - 1.2) - 5 * (bump(a, 0.9, 0.5) - bump(a, 2.6, 0.6)),
             braid2_y=4 * math.sin(a - 1.7) - 7 * (bump(a, 1.2, 0.5) - bump(a, 2.9, 0.6)),
             braid1_x=-1.5 * peek, braid2_x=1.5 * math.sin(2 * a - 1.0) - 3 * bump(a, 4.3, 0.4),
             hat_side=3 * math.sin(a - 1.4), hat_back=2 * math.sin(2 * a - 0.8) + 4 * bump(a, 4.3, 0.4))
    _cape(p, a, amp=1.0 + 0.8 * peek)
    return plant(p)


def _june_hand_out(p, S, fwd, bend, out=4.0, twist=28.0, door=34.0):
    """Bring the forearm of side S out through the front of the cape: the panel on that side opens like a door."""
    k = smooth(bend / 60.0)
    p.update({f"arm{S}_fwd": fwd, f"arm{S}_out": out, f"elbow{S}": bend, f"arm{S}_twist": twist,
              f"cape{S}1_open": door * 0.35 * k, f"cape{S}2_open": door * k, f"cape{S}3_open": door * 0.75 * k})
    for i, f in ((1, 2.0), (2, 9.0), (3, 7.0)):
        p[f"cape{S}{i}_flare"] += f * k
    return p


def june_talk(u):
    """"You came my way!": her right hand out of the cape, two beats per cycle, quick nods, bright and eager."""
    a = TAU * u
    beat = 0.5 + 0.5 * math.cos(2 * a)
    bob = 0.5 - 0.5 * math.cos(2 * a - 0.6)
    p = _june_stand()
    _cape(p, a, amp=1.3)
    p.update(hx=0.006 * math.sin(a), hip_roll=-0.8 * math.sin(a), hz=-0.003 - 0.006 * (1 - bob),
             spine_pitch=2 + 1.2 * beat, chest_pitch=1 - 1.5 * bob, chest_yaw=-4 + 2 * math.sin(a), spine_yaw=-2,
             head_yaw=3 + 7 * math.sin(a - 0.3), head_pitch=-4 - 3 * beat, head_roll=4 * math.sin(a + 0.6),
             shoulderR=4 + 4 * beat, shoulderL=2 * bob,
             handR_pitch=-12 + 16 * math.sin(2 * a + 1.0), handR_twist=-28, fingersR=-8, thumbR=-8,
             armL_fwd=5 + 2 * math.sin(a), elbowL=16 + 4 * math.sin(2 * a - 0.5),
             braid1_y=3 * math.sin(a - 0.9), braid2_y=5 * math.sin(a - 1.4), braid2_x=2 * math.sin(2 * a - 1.0),
             hat_side=3 * math.sin(a - 1.0), hat_back=3 * math.sin(2 * a - 1.2))
    _june_hand_out(p, "R", fwd=26 + 8 * beat, bend=78 - 16 * beat, out=6 + 5 * beat, twist=24)
    return plant(p)


JUNE_HIP_ABOVE_SEAT = 0.07  # hip joint above the seat when seated
JUNE_SIT_BACK = 0.03
JUNE_KNEE_R = 0.055  # knee radius under the stocking


def june_hips():
    """Offset of the Hips bone from its rest position when seated (also reported to the game)."""
    return Vector((0.0, JUNE_SIT_BACK, JUNE_SEAT + JUNE_HIP_ABOVE_SEAT - RIG["UpperLeg_L"][1].z))


def _june_sit(a):
    """On a low stool, knees up and together, leaning in a little; the cape pools behind and beside her."""
    breathe = math.sin(a - 0.3)
    off = june_hips()
    p = pose(feet_world=1.0, hx=off.x, hy=off.y, hz=off.z,
             hip_pitch=-4, spine_pitch=7 + 0.8 * breathe, chest_pitch=6 - 1.4 * breathe, neck_pitch=-4,
             shoulderL=2 + 1.5 * breathe, shoulderR=2 + 1.5 * breathe,
             legL_fwd=95, legR_fwd=95, kneeL=100, kneeR=100, legL_out=3, legR_out=3)
    ankle_z = RIG["Foot_L"][1].z
    for S, s, fwd in (("L", 1.0, 0.34), ("R", -1.0, 0.37)):
        hip = point(p, "UpperLeg_" + S)
        reach(p, [f"leg{S}_fwd", f"leg{S}_out", f"knee{S}"], "Foot_" + S,
              Vector((hip.x + s * 0.03, hip.y - fwd, ankle_z)))
    sway = math.sin(a - 0.8)
    for i, (back, flare, door, side_back) in enumerate(((6, 6, 18, 0), (46, 34, 40, 6), (82, 70, 30, 14))):
        p[f"capeM{i + 1}_back"] = back + 1.2 * (i + 1) * 0.5 * sway
        for S in "LR":
            p[f"cape{S}{i + 1}_flare"] = flare + 1.0 * sway
            p[f"cape{S}{i + 1}_open"] = door
            p[f"cape{S}{i + 1}_back"] = side_back
    p.update(braid1_x=-2, braid2_x=1.5 * math.sin(a - 1.0), braid2_y=2 * math.sin(a - 1.4),
             hat_side=2 * math.sin(a - 1.2))
    return p


def june_knee_tap(u):
    """Seated: her right hand taps the knock rhythm on her right knee (short, short, pause, last), her head
    nodding along; the left hand rests on the other knee. Contacts on TAP_FRAMES of 72."""
    frames = 72.0
    f = u * frames
    a = TAU * u
    lift = nod = 0.0
    for k, tap in enumerate(TAP_FRAMES):
        strength = 1.35 if k == len(TAP_FRAMES) - 1 else 1.0
        d = (f - (tap - 3.4) + frames / 2) % frames - frames / 2
        lift = max(lift, strength * math.exp(-(d / 2.3) ** 2))
        d = (f - (tap + 0.5) + frames / 2) % frames - frames / 2
        nod = max(nod, strength * math.exp(-(d / 2.6) ** 2))
    listen = bump(a, TAU * 54 / frames, 0.9)  # after the last knock she looks up: "Hear that?"
    p = _june_sit(a)
    p.update(head_pitch=2 + 7 * nod - 9 * listen, head_yaw=-6 + 10 * listen, head_roll=-3 + 5 * listen,
             chest_pitch=p["chest_pitch"] + 1.5 * nod, spine_yaw=-2, chest_yaw=-3,
             shoulderR=p["shoulderR"] + 5 * lift, hat_back=4 * nod - 3 * listen, braid2_x=3 * nod)
    # left hand resting on the left knee
    knee_l = point(p, "LowerLeg_L")
    p.update(armL_fwd=30, armL_out=4, elbowL=50, armL_twist=20, handL_pitch=-6, handL_twist=70, fingersL=16, thumbL=6)
    reach(p, ["armL_fwd", "armL_twist", "elbowL"], "Fingers_L",
          knee_l + Vector((0.004, 0.075, JUNE_KNEE_R + 0.03)), steps=24)
    # right hand: palm down over the right knee, lifted from the wrist and elbow before each tap
    knee_r = point(p, "LowerLeg_R")
    p.update(armR_fwd=30, armR_out=4, elbowR=50, armR_twist=20, handR_twist=70, fingersR=6 - 14 * lift, thumbR=4,
             handR_pitch=-4 - 24 * lift)
    reach(p, ["armR_fwd", "armR_twist", "elbowR"], "Fingers_R",
          knee_r + Vector((-0.004, 0.05, JUNE_KNEE_R + 0.026 + 0.05 * lift)), steps=24)
    return p


def june_wave(u):
    """A quick, glad wave with her right hand, forearm up beside her hat; the arm lifts the cape a little.
    Starts and ends on Idle frame 0."""
    p = june_idle(0.0)
    env = smooth(u / 0.24) * (1 - smooth((u - 0.78) / 0.22))
    w = math.sin(TAU * 3.0 * (u - 0.1)) * env
    up = dict(p)
    up.update(armR_out=34, armR_fwd=30, armR_twist=6, elbowR=104, handR_pitch=0, handR_side=0, handR_twist=-30,
              fingersR=-10, thumbR=-8, shoulderR=10, cape_arm=0.7,
              capeR1_open=14, capeR2_open=38, capeR3_open=30,
              capeR2_flare=p["capeR2_flare"] + 12, capeR3_flare=p["capeR3_flare"] + 12)
    p = blend(p, up, env)
    p.update(armR_out=p["armR_out"] + 8 * w, elbowR=p["elbowR"] + 5 * w, handR_side=16 * w,
             footL=8 * env, footR=8 * env, toeL=6 * env, toeR=6 * env,
             head_roll=p["head_roll"] - 5 * env - 2 * w, head_pitch=p["head_pitch"] + 4 * env,
             chest_roll=p["chest_roll"] + 3 * env - 1.5 * w, spine_roll=p["spine_roll"] + 2 * env,
             hx=p["hx"] + 0.01 * env,
             capeR2_flare=p["capeR2_flare"] + 4 * w, capeR3_flare=p["capeR3_flare"] + 7 * w,
             braid1_y=p["braid1_y"] + 4 * w, braid2_y=p["braid2_y"] + 7 * w,
             hat_side=p["hat_side"] - 5 * w, hat_back=p["hat_back"] + 3 * env)
    p["ground"] = 1.0
    return plant(p)


JUNE_DOWN = Vector((-0.18, -0.8, -0.56))  # "down there": the house below the attic, ahead of her and down


def june_point(u):
    """Points down into the house with her right hand, leaning over to look, then back to the listener.
    Starts and ends on Idle frame 0; the arm is on target from about u = 0.3 to 0.7."""
    rest = june_idle(0.0)
    env = smooth(u / 0.26) * (1 - smooth((u - 0.74) / 0.26))
    jab = math.exp(-((u - 0.36) / 0.07) ** 2)
    aimed = dict(rest)
    aimed.update(hip_pitch=2, spine_pitch=5, chest_pitch=6, chest_yaw=-8, spine_yaw=-4, hy=-0.006,
                 neck_pitch=3, head_pitch=9, head_yaw=-10, head_roll=-3, shoulderR=8,
                 handR_pitch=-4, handR_twist=20, fingersR=-10, thumbR=-12, armL_fwd=8, elbowL=20,
                 footL=0, footR=0, toeL=0, toeR=0)
    _june_hand_out(aimed, "R", fwd=40, bend=14, out=10, twist=30, door=44)
    # the cloak slides off the pointing arm: the panel only half follows it and swings wide open
    aimed.update(cape_arm=0.55, capeR1_open=22, capeR2_open=40, capeR3_open=34,
                 capeM1_back=4, capeM2_back=9, capeM3_back=12)
    for S in "LR":
        aimed.update({f"cape{S}1_back": 2, f"cape{S}2_back": 5, f"cape{S}3_back": 7})
    aimed.update(capeR1_flare=6, capeR2_flare=16, capeR3_flare=14, capeL2_flare=5, capeL3_flare=6)
    aim_arm(aimed, "R", JUNE_DOWN, bend=14)
    p = blend(rest, aimed, env)
    look_back = smooth((u - 0.48) / 0.14) * env  # glances up at the listener while still pointing
    p.update(head_pitch=p["head_pitch"] - 16 * look_back, head_yaw=p["head_yaw"] + 8 * look_back,
             elbowR=p["elbowR"] - 8 * jab, shoulderR=p["shoulderR"] + 3 * jab,
             braid1_x=3 * env, braid2_x=5 * env, hat_back=-5 * env + 4 * jab)
    p["ground"] = 1.0
    return plant(p)


# Walk: the game moves her root along the path at JUNE_WALK_SPEED; the clip is in place. One cycle is two
# steps (left heel strikes on frame 0, right on frame 15), so she covers JUNE_WALK_STRIDE per cycle.
JUNE_WALK_FRAMES = 30
JUNE_WALK_SPEED = 1.1  # m/s the feet are planted for
JUNE_WALK_STRIDE = JUNE_WALK_SPEED * JUNE_WALK_FRAMES / FPS  # metres per cycle (two steps of half that)
JUNE_WALK_DUTY = 0.56  # share of the cycle a foot is on the ground (0.06 of double support after each strike)
JUNE_WALK_TRACK = 0.052  # half the distance between the two lines of footprints
JUNE_WALK_LEAD = 0.062  # the contact patch runs from this far short of symmetric about the hips (she leans in)
JUNE_WALK_STRIKE, JUNE_WALK_PUSH = -17.0, 56.0  # foot pitch at heel strike and at toe-off (toes down +)


def _june_walk_foot(S, s, q):
    """Ankle position, foot pitch and toe bend of side S at phase q of its own cycle (0 = heel strike).
    On the ground the foot's contact (the heel while the toes are up, then the whole sole, then the toes with
    the heel peeling off) travels straight back at exactly the walk speed, so nothing slides."""
    ankle, toe_j = RIG["Foot_" + S][1], RIG["Toe_" + S][1]
    heel = next(pt for b, pt in RIG["_sole"][S] if b == "Foot_" + S)
    travel = JUNE_WALK_STRIDE * JUNE_WALK_DUTY
    c0 = -travel / 2 + JUNE_WALK_LEAD
    x = s * JUNE_WALK_TRACK

    def planted(c, pitch):
        pivot = heel if pitch < 0 else toe_j
        at = Vector((0.0, c + pivot.y - ankle.y, pivot.z)) + rx(pitch) @ (ankle - pivot)
        return Vector((x, at.y, at.z))

    if q < JUNE_WALK_DUTY:
        t = q / JUNE_WALK_DUTY
        pitch = JUNE_WALK_STRIKE * (1 - smooth(t / 0.17)) + JUNE_WALK_PUSH * max(0.0, (t - 0.5) / 0.5) ** 1.6
        return planted(c0 + travel * t, pitch), pitch, max(0.0, pitch)
    t = (q - JUNE_WALK_DUTY) / (1 - JUNE_WALK_DUTY)
    a, b = planted(c0 + travel, JUNE_WALK_PUSH), planted(c0, JUNE_WALK_STRIKE)
    m = JUNE_WALK_STRIDE * (1 - JUNE_WALK_DUTY)  # leaves and meets the ground at the ground's own speed
    t2, t3 = t * t, t * t * t
    y = (2 * t3 - 3 * t2 + 1) * a.y + (t3 - 2 * t2 + t) * 0.1 * m + (-2 * t3 + 3 * t2) * b.y + (t3 - t2) * 0.3 * m
    pitch = JUNE_WALK_PUSH + (JUNE_WALK_STRIKE - JUNE_WALK_PUSH) * smooth(t / 0.9)
    toe = JUNE_WALK_PUSH * (1 - smooth(t / 0.35))
    z = a.z + (b.z - a.z) * smooth(t) + 0.03 * math.sin(math.pi * min(1.0, t / 0.8)) ** 2
    # never through the floor: lift the ankle until the lowest point of the sole clears it
    low = min((rx(pitch) @ (heel - ankle)).z, (rx(pitch) @ (toe_j - ankle)).z - toe_j.z,
              (rx(pitch) @ (toe_j - ankle) + rx(pitch - toe) @ Vector((0.0, -0.06, -toe_j.z))).z)
    z = max(z, 0.014 * math.sin(math.pi * t) - low)
    return Vector((x, y, z)), pitch, toe


def june_walk(u):
    """A cheerful kid's walk, in place: 1.0 s per cycle of two steps, planted for JUNE_WALK_SPEED. Left heel
    strikes at u = 0. Arms swing opposite the legs under the cloak (the side panels swing with them), a small
    bounce, the cape trailing behind and rippling a beat late, braid and hat tip bobbing."""
    a = TAU * u
    step = 2 * a  # one turn per step
    up = -math.cos(step - TAU * 0.09)  # highest as each leg passes under her, lowest just after a heel strikes
    p = pose(feet_world=1.0, ground=0.0, head_world=0.0)
    p.update(hz=-0.018 + 0.012 * up, hx=0.011 * math.sin(a - 0.5), hy=-0.012,
             hip_pitch=3.0, hip_yaw=-5.5 * math.cos(a), hip_roll=2.2 * math.sin(a - 0.3),
             spine_pitch=2.0 + 1.0 * up, spine_yaw=4.5 * math.cos(a), spine_roll=-1.6 * math.sin(a - 0.3),
             chest_pitch=0.5, chest_yaw=5.5 * math.cos(a - 0.2), chest_roll=-1.0 * math.sin(a - 0.3),
             neck_pitch=-2.0, head_pitch=-5.0 - 1.6 * math.cos(step - TAU * 0.75), head_yaw=-4.0 * math.cos(a - 0.2),
             head_roll=3.2 * math.sin(a - 0.6),
             shoulderL=1.5 + 1.5 * math.cos(a), shoulderR=1.5 - 1.5 * math.cos(a),
             armL_fwd=3 - 19 * math.cos(a - 0.25), armR_fwd=3 + 19 * math.cos(a - 0.25),
             armL_out=-1, armR_out=-1, armL_twist=8, armR_twist=8,
             elbowL=20 - 10 * math.cos(a - 0.7), elbowR=20 + 10 * math.cos(a - 0.7),
             handL_pitch=5 * math.sin(a - 0.9), handR_pitch=-5 * math.sin(a - 0.9), fingersL=12, fingersR=12,
             cape_arm=0.75,
             braid1_x=3 + 2.5 * math.cos(step - TAU * 0.85), braid2_x=5 + 4.5 * math.cos(step - TAU * 1.0),
             braid1_y=2.0 * math.sin(a - 1.2), braid2_y=4.0 * math.sin(a - 1.7),
             hat_back=5 + 4.5 * math.cos(step - TAU * 0.9), hat_side=3.5 * math.sin(a - 1.4))
    # the cape trails: leaning back from the collar, more toward the hem, each level a beat later
    for i in (1, 2, 3):
        ripple = math.cos(step - TAU * 0.3 + 0.75 * (3 - i))  # the hem is furthest back as a heel kicks up
        p[f"capeM{i}_back"] = (6.0, 12.0, 17.0)[i - 1] + 2.0 * i * ripple
        for S, s in SIDES:
            mine = math.cos(a - 0.5 * i - (0.0 if S == "L" else math.pi))  # behind its own leg's back-swing
            p[f"cape{S}{i}_back"] = (3.0, 7.0, 11.0)[i - 1] + 1.2 * i * ripple + 1.5 * i * mine
            p[f"cape{S}{i}_flare"] = (1.5, 3.5, 5.5)[i - 1] + 0.8 * i * math.cos(step - TAU * 0.4 + 0.75 * (3 - i))
    p["cape_sway"] = 2.2 * math.sin(a - 1.1)
    for S, s, q in (("L", 1.0, u % 1.0), ("R", -1.0, (u + 0.5) % 1.0)):
        at, pitch, toe = _june_walk_foot(S, s, q)
        p[f"foot{S}"], p[f"toe{S}"] = pitch, toe
        hip = point(p, "UpperLeg_" + S)
        p[f"leg{S}_fwd"] = math.degrees(math.atan2(hip.y - at.y, hip.z - at.z)) + 12.0
        p[f"knee{S}"] = 24.0
        reach(p, [f"leg{S}_fwd", f"leg{S}_out", f"knee{S}"], "Foot_" + S, at, steps=30)
    return p


# --- Aunt Kit ---------------------------------------------------------------------------------------------
# She carries the crystal-ball lantern in her right hand, so her left does the talking and waving; in OpenBooth
# both arms open wide and the lantern goes up.

def _kit_stand(**kw):
    p = pose(ground=1.0, feet_world=1.0, kneeL=3, kneeR=3, legL_fwd=1.5, legR_fwd=1.5,
             armL_out=-4, armL_fwd=4, elbowL=14, armL_twist=8, fingersL=8,
             armR_out=2, armR_fwd=6, elbowR=16, armR_twist=6)
    p.update(kw)
    return p


def _kit_dangle(p, a, k=1.0, lean=0.0):
    """Skirt, scarf tails, earrings and lantern: slow, out of phase."""
    p.update(skirt_y=1.6 * k * math.sin(a - 1.2), skirt_x=1.2 * k * math.sin(2 * a - 0.6) + lean,
             tail_y=5 * k * math.sin(a - 1.0), tail_x=4 * k * math.sin(a + 0.6),
             ringL_y=6 * k * math.sin(a - 0.8), ringR_y=6 * k * math.sin(a - 1.1),
             ringL_x=4 * k * math.sin(2 * a - 0.4), ringR_x=4 * k * math.sin(2 * a - 0.9),
             lantern_y=3 * k * math.sin(a - 0.9), lantern_x=2.5 * k * math.sin(2 * a - 0.3))
    return p


def kit_idle(u):
    """Easy and warm: a slow weight shift, breathing, a small tilt of the head, the lantern swaying."""
    a = TAU * u
    breathe = math.sin(2 * a - 0.4)
    shift = math.sin(a)
    p = _kit_stand()
    p.update(hx=0.014 * shift, hip_roll=-1.6 * shift, spine_roll=1.0 * shift, chest_roll=0.8 * shift,
             hip_yaw=1.5 * math.sin(a - 0.8),
             spine_pitch=0.5 + 0.8 * breathe, chest_pitch=-1.2 * breathe, chest_yaw=2 * math.sin(a - 0.5),
             head_yaw=6 * math.sin(a - 0.7), head_roll=3 * math.sin(a + 0.5), head_pitch=-2 + 1.0 * breathe,
             shoulderL=1.5 * (0.5 + 0.5 * breathe), shoulderR=1.5 * (0.5 + 0.5 * breathe),
             armL_fwd=4 + 2 * math.sin(a - 0.6), elbowL=14 + 3 * math.sin(a - 1.0), handL_pitch=4 * math.sin(a - 1.3),
             fingersL=10 + 5 * math.sin(a - 1.6),
             armR_fwd=6 - 1.5 * math.sin(a - 0.9), elbowR=16 + 2 * math.sin(a - 0.4))
    _kit_dangle(p, a)
    return plant(p)


def kit_talk(u):
    """Storyteller: her left hand opens out, palm up, on two beats per cycle; nods and a tilt of the head."""
    a = TAU * u
    beat = 0.5 + 0.5 * math.cos(2 * a)
    p = _kit_stand()
    p.update(hx=0.008 * math.sin(a), hip_roll=-1.0 * math.sin(a),
             spine_pitch=1.5 + 1.0 * beat, chest_pitch=0.5 - 1.5 * beat, chest_yaw=5 + 2 * math.sin(a), spine_yaw=2,
             head_yaw=-3 + 6 * math.sin(a - 0.3), head_pitch=-2 + 4 * beat, head_roll=4 * math.sin(a + 0.7),
             shoulderL=4 + 3 * beat, shoulderR=1.5 * beat,
             armL_fwd=34 + 8 * beat, armL_out=8 + 8 * beat, armL_twist=-6, elbowL=78 - 22 * beat,
             handL_twist=-70, handL_pitch=-8 + 12 * math.sin(2 * a + 0.9), fingersL=-12, thumbL=-12,
             armR_fwd=8 + 2 * math.sin(a), elbowR=20 + 3 * math.sin(2 * a - 0.8))
    _kit_dangle(p, a, 1.3)
    return plant(p)


def kit_open_booth(u):
    """"First show of the night": gathers herself, then both arms sweep up and out, lantern high in her right
    hand, chest and chin up, and back down. Starts and ends on Idle frame 0; the ta-da holds from about
    u = 0.36 to 0.66 (frames 22-40 of 60)."""
    rest = kit_idle(0.0)
    gather = dict(rest)
    gather.update(hz=-0.03, hip_pitch=5, spine_pitch=6, chest_pitch=5, head_pitch=8, head_yaw=0, head_roll=0,
                  shoulderL=-2, shoulderR=-2,
                  armL_fwd=30, armL_out=-10, elbowL=86, armL_twist=30, handL_twist=20, fingersL=20,
                  armR_fwd=34, armR_out=-4, elbowR=70, armR_twist=24, lantern_x=6, skirt_x=-3)
    tada = dict(rest)
    tada.update(hz=0.0, hx=0.0, hip_roll=0, hip_pitch=-2, spine_pitch=-3, chest_pitch=-6, spine_roll=0, chest_roll=-2,
                chest_yaw=0, neck_pitch=-3, head_pitch=-9, head_yaw=0, head_roll=3,
                shoulderL=9, shoulderR=12, footL=7, footR=7, toeL=6, toeR=6,
                armL_fwd=22, armL_out=47, elbowL=44, armL_twist=-24, handL_twist=-70, handL_pitch=-16,
                fingersL=-14, thumbL=-14,
                armR_fwd=24, armR_out=63, elbowR=88, armR_twist=-8, handR_pitch=0,
                lantern_y=-9, lantern_x=-4, skirt_x=3, tail_x=8, ringL_y=10, ringR_y=-10)
    hold = dict(tada)
    hold.update(chest_pitch=-4.5, head_pitch=-6, head_roll=-2, armL_out=44, elbowL=40, armR_out=60, elbowR=84,
                shoulderL=7, shoulderR=10, footL=2, footR=2, toeL=2, toeR=2,
                lantern_y=5, lantern_x=3, skirt_x=-1, tail_x=-3, ringL_y=-5, ringR_y=5)
    p = keyed([(0.0, rest), (0.15, gather), (0.37, tada), (0.66, hold), (1.0, rest)], u)
    settle = smooth((u - 0.37) / 0.1) * (1 - smooth((u - 0.6) / 0.12))
    p["lantern_y"] += 5 * settle * math.sin(TAU * 2.2 * (u - 0.37))
    return plant(p)


def kit_wave(u):
    """A warm, unhurried wave with her left hand, forearm up beside her scarf; the lantern stays low.
    Starts and ends on Idle frame 0."""
    p = kit_idle(0.0)
    env = smooth(u / 0.24) * (1 - smooth((u - 0.78) / 0.22))
    w = math.sin(TAU * 2.5 * (u - 0.1)) * env
    up = dict(p)
    up.update(armL_out=36, armL_fwd=30, armL_twist=6, elbowL=102, handL_pitch=0, handL_side=0, handL_twist=-30,
              fingersL=-10, thumbL=-8, shoulderL=9)
    p = blend(p, up, env)
    p.update(armL_out=p["armL_out"] + 8 * w, elbowL=p["elbowL"] + 5 * w, handL_side=16 * w,
             head_roll=p["head_roll"] + 5 * env + 1.5 * w, head_pitch=p["head_pitch"] - 3 * env,
             chest_roll=p["chest_roll"] - 2.5 * env + 1.0 * w, chest_pitch=p["chest_pitch"] - 2 * env,
             hx=p["hx"] - 0.01 * env,
             tail_y=p["tail_y"] - 6 * w, ringL_y=p["ringL_y"] - 8 * w, ringR_y=p["ringR_y"] - 6 * w,
             lantern_y=p["lantern_y"] + 3 * w, skirt_y=p["skirt_y"] + 1.5 * w)
    p["ground"] = 1.0
    return plant(p)


# name, pose function, frames at 30 fps, loops, intended ground speed (m/s), note
JUNE_CLIPS = [
    ("Idle", june_idle, 90, True, 0.0, "standing: looking out of the attic window, slow sweep, one peek on tiptoe"),
    ("Talk", june_talk, 60, True, 0.0, "standing: right hand out of the cape, two beats per cycle"),
    ("KneeTap", june_knee_tap, 72, True, 0.0,
     "SEATED (seat 0.30 m): right hand taps her right knee on frames 6, 15, 36: short, short, pause, last"),
    ("Wave", june_wave, 45, False, 0.0, "standing: right-hand wave; starts and ends on Idle frame 0"),
    ("Point", june_point, 48, False, 0.0,
     "standing: right hand points ahead and down into the house; on target frames 14-34; starts/ends on Idle 0"),
    ("Walk", june_walk, JUNE_WALK_FRAMES, True, JUNE_WALK_SPEED,
     "in place, 1.1 m per cycle (two 0.55 m steps): left heel strikes on frame 0, right on frame 15"),
]
KIT_CLIPS = [
    ("Idle", kit_idle, 90, True, 0.0, "standing: weight shift, breathing, lantern swaying in her right hand"),
    ("Talk", kit_talk, 60, True, 0.0, "standing: left hand opens out palm up, two beats per cycle"),
    ("OpenBooth", kit_open_booth, 60, False, 0.0,
     "standing: two-arm ta-da, lantern raised; peak held frames 22-40; starts and ends on Idle frame 0"),
    ("Wave", kit_wave, 45, False, 0.0, "standing: left-hand wave; starts and ends on Idle frame 0"),
]
