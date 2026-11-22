"""Clips for the two witnesses of the first case (spec §2, §3): Bea by her bicycle and Mr. Vale on his bench.

Same pose vocabulary and solver conventions as pip_anims.py (pure maths, mathutils only): a pose is a dict of
scalars in degrees / metres, `solve` turns it into one rotation per bone as a delta in the character's own axes
(X = the character's left, -Y = forward, Z = up) carried by the parent bone. On top of Pip's vocabulary:

  Bea    pumpkin_*   the costume is one rigid shell on the `Pumpkin` bone; it follows the chest only partly
                     (`pumpkin_follow`), wobbles and bounces (`pumpkin_z`, metres)
         tail_*      the ponytail (`Ponytail_1`, `Ponytail_2`)
         index<S>    her index finger (`Index_<S>`) relative to the other three: it curls with `fingers<S>`
                     unless this undoes it, which is how she points with one finger
         tips<S>     the second joint of those three fingers (`FingerTips_<S>`): with `fingers<S>` it makes a fist
         thumb<S>_tuck  0..1 lays the thumb under the index finger, against the curled middle finger
  both   hand<S>_twist  pronation of the hand at the cuff
  Vale   pot_*       the cauldron hangs plumb from the right hand (`Cauldron`, like Pip's pail)
         b<End>_*    five loose bandage ends that hang plumb and sway (`Bandage_<End>`)
         belly_fwd / belly_lift  slide the belly end out of the lap when he sits

The exporter calls `bind(rig)` first: seated and pointing poses are fitted to the skeleton with a small numeric
IK (`reach`), so hands and feet land where they should whatever the proportions. All clips are in place.
"""
import math

import numpy as np
from mathutils import Matrix, Vector

import pip_anims as pa
from pip_anims import SIDES, TAU, bump, rx, ry, rz, smooth

FPS = pa.FPS
SEAT = 0.47  # height of the bench seat Mr. Vale's sitting clips are fitted to (the game's Bench.fbx)
BANDS = ("Head", "ArmL", "ArmR", "Belly", "Knee")

DEFAULTS = dict(pa.DEFAULTS)
DEFAULTS.update({
    "pumpkin_z": 0.0, "pumpkin_pitch": 0.0, "pumpkin_roll": 0.0, "pumpkin_yaw": 0.0, "pumpkin_follow": 0.7,
    "tail_back": 0.0, "tail_side": 0.0,
    "pot_x": 0.0, "pot_y": 0.0, "pot_world": 1.0,
    "belly_fwd": 0.0, "belly_lift": 0.0,
})
for _s in "LR":
    DEFAULTS[f"hand{_s}_twist"] = 0.0  # turns the palm down when the forearm points forward (+), at the cuff
    # Bea's pointing hand (witness_export.split_hand adds the bones): the index finger's own curl on top of
    # fingers<S>, the second joint of the other three, and the thumb tucked under the index finger (0..1)
    DEFAULTS.update({f"index{_s}": 0.0, f"tips{_s}": 0.0, f"thumb{_s}_tuck": 0.0})
for _b in BANDS:
    DEFAULTS.update({f"b{_b}_x": 0.0, f"b{_b}_y": 0.0})  # swings back (+) / to the character's left (+)

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

def hand_axes(knuckle, along, thumb_root, s):
    """The hand's own axes in character space, from three things every rig has: the `Fingers` bone's head
    (the knuckle line) and direction, and the thumb's root. Returns (out of the back of the hand, across the
    knuckles from the index finger to the little one, along the fingers); s = 1 for the left hand, -1 right."""
    to_thumb = thumb_root - knuckle
    u = (to_thumb - along * to_thumb.dot(along)).normalized()
    w = u.cross(-along) * s
    # in hand units the thumb's root is (-0.007, -0.029) from the knuckle line (char_export.human_bones)
    return (-0.007 * u - 0.029 * w).normalized(), (-0.029 * u + 0.007 * w).normalized(), along


THUMB_REST = Vector((-0.008, -0.020, -0.027)).normalized()  # (back, across, -along) as modelled: beside the palm
THUMB_TUCKED = Vector((-0.018, 0.016, -0.020)).normalized()  # under the index finger, tip on the middle finger


_TUCKS = {}


def _thumb_tuck(rig, S, s):
    """The rotation (character axes) that takes the thumb of side S from where it was modelled to tucked."""
    key = (id(rig), S)
    if key not in _TUCKS:
        knuckle = rig["Fingers_" + S][1]
        back, across, along = hand_axes(knuckle, (rig["FingerTips_" + S][1] - knuckle).normalized(),
                                        rig["Thumb_" + S][1], s)
        a, b = (back * v.x + across * v.y - along * v.z for v in (THUMB_REST, THUMB_TUCKED))
        _TUCKS[key] = a.rotation_difference(b).to_matrix()
    return _TUCKS[key]


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
    D["Ponytail_1"] = rx(-p["tail_back"]) @ ry(p["tail_side"])
    D["Ponytail_2"] = rx(-p["tail_back"] * 0.8) @ ry(p["tail_side"] * 0.8)
    for S, s in SIDES:
        D["Shoulder_" + S] = ry(-s * p[f"shoulder{S}"])
        D["UpperArm_" + S] = rx(-p[f"arm{S}_fwd"]) @ ry(-s * p[f"arm{S}_out"]) @ rz(-s * p[f"arm{S}_twist"])
        D["LowerArm_" + S] = rx(-p[f"elbow{S}"])
        D["Hand_" + S] = rx(-p[f"hand{S}_pitch"]) @ ry(-s * p[f"hand{S}_side"]) @ rz(-s * p[f"hand{S}_twist"])
        D["Fingers_" + S] = ry(s * p[f"fingers{S}"])
        D["Index_" + S] = ry(s * (p[f"fingers{S}"] + p[f"index{S}"]))
        D["FingerTips_" + S] = ry(s * p[f"tips{S}"])
        D["Thumb_" + S] = ry(s * p[f"thumb{S}"])
        if p[f"thumb{S}_tuck"] > 0:
            D["Thumb_" + S] = pa._lerp_rot(_thumb_tuck(rig, S, s), p[f"thumb{S}_tuck"]) @ D["Thumb_" + S]
        D["UpperLeg_" + S] = rz(s * p[f"leg{S}_twist"]) @ ry(-s * p[f"leg{S}_out"]) @ rx(-p[f"leg{S}_fwd"])
        D["LowerLeg_" + S] = rx(p[f"knee{S}"])
        if p["feet_world"] > 0.5:
            W["Foot_" + S] = rx(p[f"foot{S}"])
        else:
            D["Foot_" + S] = rx(p[f"foot{S}"])
        D["Toe_" + S] = rx(-p[f"toe{S}"])
    for b in BANDS:
        W["Bandage_" + b] = ry(p[f"b{b}_y"]) @ rx(-p[f"b{b}_x"])
    pot_w = ry(p["pot_y"]) @ rx(-p["pot_x"])
    wobble = rz(p["pumpkin_yaw"]) @ ry(p["pumpkin_roll"]) @ rx(p["pumpkin_pitch"])
    shifts = {"Pumpkin": Vector((0, 0, p["pumpkin_z"])),
              "Bandage_Belly": Vector((0, -p["belly_fwd"], p["belly_lift"]))}

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
            elif name == "Cauldron":
                D[name] = pa._lerp_rot(A[parent].transposed() @ pot_w, p["pot_world"]) if p["pot_world"] > 0 \
                    else pot_w
            elif name == "Pumpkin":
                D[name] = pa._lerp_rot(A[parent].transposed(), 1.0 - p["pumpkin_follow"]) @ wobble
            A[name] = A[parent] @ D.get(name, Matrix.Identity(3))
            pos[name] = pos[parent] + A[parent] @ (head_at - rig[parent][1])
            if name == "Hips":
                pos[name] += offset
            elif name in shifts:
                pos[name] += A[parent] @ shifts[name]
        return A, pos

    offset = Vector((p["hx"], p["hy"], p["hz"]))
    A, pos = forward(offset)
    if p["ground"] > 0:
        low = min((pos[b] + A[b] @ (pt - rig[b][1])).z for side in rig["_sole"].values() for b, pt in side)
        offset.z -= low * p["ground"]
        A, pos = forward(offset)
    offsets = {"Hips": offset}
    offsets.update({b: v for b, v in shifts.items() if b in rig})
    return D, offsets, A, pos


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


def aim_arm(p, S, direction, bend=12.0, keys=None, twist=40.0):
    """Point the arm of side S (shoulder to fingertips) along `direction` (character axes). `twist` is where
    the upper arm's twist starts for a forward aim (and stays, if `keys` leaves it out)."""
    s = 1.0 if S == "L" else -1.0
    p[f"elbow{S}"] = bend
    sh = point(p, "UpperArm_" + S)
    tip = RIG["Fingers_" + S][1]
    length = (RIG["LowerArm_" + S][1] - RIG["UpperArm_" + S][1]).length + (tip - RIG["LowerArm_" + S][1]).length
    d = Vector(direction).normalized()
    # start from roughly the right quadrant so the damped solver does not wander through the body
    p[f"arm{S}_out"] = max(p[f"arm{S}_out"], 20.0)
    p[f"arm{S}_twist"] = twist if d.y < -0.3 else p[f"arm{S}_twist"]
    reach(p, keys or [f"arm{S}_out", f"arm{S}_twist", f"arm{S}_fwd"], "Fingers_" + S, sh + d * length * 0.985, steps=24)
    _ = s
    return p


# --- Bea --------------------------------------------------------------------------------------------------
# Her pumpkin holds her arms out (they rest 49 degrees from her sides); the bicycle stands on her right, the
# house she points at is ahead and to her left.

def _bea_stand(**kw):
    p = pose(ground=1.0, feet_world=1.0, kneeL=5, kneeR=5, legL_fwd=2, legR_fwd=2,
             armL_out=-9, armR_out=-7, armL_fwd=6, armR_fwd=8, elbowL=30, elbowR=34, fingersL=6, fingersR=10,
             armL_twist=10, armR_twist=12, handR_pitch=6)
    p.update(kw)
    return p


def bea_idle(u):
    """Weight shifts left and right, one glance toward the house, one little bounce on her toes."""
    a = TAU * u
    shift = math.sin(a)
    breathe = math.sin(2 * a)
    glance = bump(a, 1.9, 0.55)
    dip = bump(a, 4.05, 0.3)
    hop = bump(a, 4.55, 0.3)
    p = _bea_stand()
    p.update(hx=0.022 * shift, hip_roll=-2.6 * shift, spine_roll=1.6 * shift, chest_roll=1.4 * shift,
             hz=-0.012 * dip, hip_yaw=3 * glance,
             footL=11 * hop, footR=11 * hop, toeL=9 * hop, toeR=9 * hop,
             spine_pitch=1.0 + 1.0 * breathe + 3 * dip, chest_pitch=-1.2 * breathe - 2 * hop,
             chest_yaw=7 * glance, spine_yaw=3 * glance,
             head_yaw=3 * math.sin(a - 0.5) + 24 * glance, head_roll=2.5 * math.sin(a + 0.8) - 3 * glance,
             head_pitch=-1 + 1.5 * breathe - 4 * hop + 3 * dip,
             shoulderL=2 * (0.5 + 0.5 * breathe) + 4 * hop, shoulderR=2 * (0.5 + 0.5 * breathe) + 4 * hop,
             armL_out=-9 + 2 * breathe + 7 * hop - 4 * dip, armR_out=-7 + 2 * breathe + 6 * hop - 4 * dip,
             armL_fwd=6 + 3 * math.sin(a - 0.6), armR_fwd=8 - 3 * math.sin(a - 0.9),
             elbowL=30 + 5 * math.sin(a - 1.0) + 8 * dip, elbowR=34 + 4 * math.sin(a - 1.4) + 8 * dip,
             handL_pitch=5 * math.sin(a - 1.3), fingersL=8 + 6 * math.sin(a - 1.6),
             # the costume lags the body: it sinks as she pops up, lifts as she lands, then settles
             pumpkin_z=0.012 * bump(a, 4.95, 0.28) - 0.01 * bump(a, 4.5, 0.26) - 0.004 * bump(a, 5.5, 0.3),
             pumpkin_roll=-1.4 * math.sin(a - 0.7), pumpkin_pitch=1.5 * bump(a, 4.9, 0.4),
             pumpkin_yaw=-3 * bump(a, 2.3, 0.6),
             tail_side=4 * math.sin(a - 1.1) - 9 * (bump(a, 1.6, 0.4) - bump(a, 2.6, 0.5)),
             tail_back=2 * math.sin(a - 1.0) - 7 * bump(a, 4.8, 0.3))
    return plant(p)


BEA_HOUSE = Vector((0.8, -0.56, 0.2))  # where "Right there!" is: to her left and ahead, a little up


def bea_talk(u):
    """Quick talker: left arm out toward the house with two jabs per cycle, right hand chattering along,
    bobbing on her toes, eyes going between the listener and the house."""
    a = TAU * u
    jab = 0.5 + 0.5 * math.cos(2 * a)  # 1 at the stretch of each "there!"
    bob = 0.5 - 0.5 * math.cos(2 * a - 0.5)
    look = 0.5 + 0.5 * math.sin(a - 0.4)  # 1: at the house
    p = _bea_stand()
    p.update(hx=0.008 * math.sin(a), hip_roll=-1.0 * math.sin(a), hip_yaw=5, hz=-0.004 - 0.008 * (1 - bob),
             footL=7 * bob, footR=7 * bob, toeL=6 * bob, toeR=6 * bob,
             spine_yaw=5, chest_yaw=8 + 4 * jab, spine_pitch=2 + 1.5 * jab, chest_pitch=1 - 2 * bob,
             chest_roll=-2 * jab,
             head_yaw=4 + 24 * look, head_pitch=-3 - 3 * jab, head_roll=4 * math.sin(a + 0.6),
             shoulderL=5 + 5 * jab, shoulderR=3 * bob,
             handL_pitch=-6 - 6 * jab, handL_side=-4, fingersL=-10, thumbL=-8,
             armR_out=-6 + 6 * bob, armR_fwd=22 + 8 * math.sin(2 * a + 0.8), armR_twist=20,
             elbowR=58 + 16 * math.sin(2 * a - 0.3), handR_pitch=-10 + 14 * math.sin(2 * a + 1.2), fingersR=-4,
             pumpkin_z=0.008 * math.sin(2 * a - 2.2), pumpkin_roll=1.2 * math.sin(a - 0.8),
             pumpkin_pitch=1.2 * math.sin(2 * a - 1.5),
             tail_side=-6 * math.cos(a - 0.9), tail_back=3 * math.sin(2 * a - 1.2))
    wobble = Vector((0.03 * math.sin(a), 0.0, 0.05 * jab - 0.02))
    aim_arm(p, "L", BEA_HOUSE + wobble, bend=30 - 22 * jab)
    return plant(p)


def _wave(p, S, u, swings=3.0):
    """Raise the arm of side S and wave the forearm; the envelope is 0 at both ends of the clip."""
    env = smooth(u / 0.24) * (1 - smooth((u - 0.78) / 0.22))
    w = math.sin(TAU * swings * (u - 0.1)) * env
    p[f"arm{S}_out"] += env * 62
    p[f"arm{S}_fwd"] += env * 12
    p[f"arm{S}_twist"] += env * 8
    p[f"elbow{S}"] += env * 46 + 0 * w
    p[f"arm{S}_out"] += 9 * w
    p[f"hand{S}_side"] = p[f"hand{S}_side"] * (1 - env) + 16 * w
    p[f"hand{S}_pitch"] *= 1 - env
    p[f"fingers{S}"] = p[f"fingers{S}"] * (1 - env) - 10 * env
    p[f"shoulder{S}"] += 10 * env
    return env, w


def bea_wave(u):
    """A big, happy wave with her left hand, up on her toes; starts and ends on Idle frame 0."""
    p = bea_idle(0.0)
    env, w = _wave(p, "L", u)
    p.update(footL=10 * env, footR=10 * env, toeL=8 * env, toeR=8 * env,
             head_roll=p["head_roll"] + 6 * env + 2 * w, head_pitch=p["head_pitch"] - 5 * env,
             chest_roll=p["chest_roll"] - 3 * env + 1.5 * w, spine_roll=p["spine_roll"] - 2 * env,
             chest_pitch=p["chest_pitch"] - 3 * env, hx=p["hx"] - 0.012 * env,
             armR_out=p["armR_out"] + 5 * env, elbowR=p["elbowR"] + 6 * env,
             pumpkin_roll=-2.5 * w, pumpkin_z=0.006 * math.sin(TAU * 3 * u) * env,
             tail_side=-7 * w, tail_back=-4 * env)
    return plant(p)


POINT_FRAMES = 54
POINT_TWIST = 88.0
POINT_UP = 23.0  # degrees above level; the game turns her to face the target, so the aim is her own forward
_POINT_ARM = ("armR_fwd", "armR_out", "armR_twist", "elbowR")


def _ramp(f, a, b):
    return smooth((f - a) / (b - a))


def _bell(f, centre, width):
    return math.exp(-((f - centre) / width) ** 2)


def _on_ball(S, heel):
    """Where the ankle of side S is when the heel is raised `heel` degrees with the ball of the foot in place."""
    ankle, ball = RIG["Foot_" + S][1], RIG["Toe_" + S][1]
    return ball - rx(heel) @ (ball - ankle)


def bea_point(u):
    """Look, THERE! She rocks back and winds her right hand up to her chin, stamps her left foot forward and
    throws the arm out straight ahead and POINT_UP degrees up, index finger out and the rest curled, up on her
    toes with her weight on the front foot; holds it with two eager bounces, then steps back.
    Starts and ends on Idle frame 0. Frames: wind-up 0-8, strike 8-13, on target 13-41, return 41-54."""
    f = u * POINT_FRAMES
    rest = bea_idle(0.0)
    # the same pose with the hips' height written out: here one heel peels up while the other foot steps, and
    # `ground` would answer that by lifting her whole body instead of bending the knee
    rest.update(hz=fk(rest, RIG)[1]["Hips"].z, ground=0.0)
    wind = _ramp(f, 0, 7) * (1 - _ramp(f, 8, 12.5))
    out = _ramp(f, 8, 13) * (1 - _ramp(f, 41, 52))
    hit = _bell(f, 14.5, 2.4) * out  # the overshoot as the arm lands
    dip = (_bell(f, 21.5, 2.6) + _bell(f, 30.5, 2.6)) * out  # the little squat before each bounce
    hop = (_bell(f, 25.0, 2.8) + _bell(f, 34.0, 2.8)) * out
    rise = hit + 0.8 * hop
    step = _ramp(f, 6.5, 12) * (1 - _ramp(f, 43.5, 50))  # the left foot is forward
    lift = _bell(f, 8.6, 2.6) * (1 - _ramp(f, 11.5, 13)) + 0.8 * _bell(f, 46.8, 2.4)

    wound = dict(rest)
    wound.update(hy=0.04, hz=-0.024, hx=-0.012, hip_pitch=-3, spine_pitch=-7, chest_pitch=-6, chest_yaw=-10,
                 spine_yaw=-4, head_yaw=12, head_pitch=-3, head_roll=3,
                 shoulderR=8, armR_fwd=8, armR_out=4, armR_twist=34, elbowR=112, handR_pitch=4,
                 fingersR=45, indexR=-25, tipsR=50, thumbR_tuck=0.5,
                 armL_fwd=16, armL_out=-4, elbowL=44, pumpkin_pitch=-3, pumpkin_z=0.008, tail_back=-8)
    aimed = dict(rest)
    aimed.update(hy=-0.085, hx=0.02, hz=0.012, hip_yaw=4, hip_pitch=2, spine_pitch=4, chest_pitch=3,
                 spine_yaw=5, chest_yaw=10, chest_roll=-2,
                 head_yaw=-19, head_pitch=-22, head_roll=-2, neck_pitch=-3,
                 shoulderR=12, handR_pitch=0, handR_side=0, handR_twist=0,
                 fingersR=80, indexR=-75, tipsR=86, thumbR_tuck=1.0,
                 shoulderL=3, armL_fwd=-14, armL_out=-10, elbowL=44, armL_twist=14, fingersL=34,
                 pumpkin_pitch=2, tail_back=6)
    p = {k: rest[k] + wind * (wound[k] - rest[k]) + out * (aimed[k] - rest[k]) for k in rest}
    p.update(hz=p["hz"] - 0.016 * dip + 0.012 * rise,
             spine_pitch=p["spine_pitch"] + 3 * hit + 2 * dip - 1.5 * hop,
             chest_pitch=p["chest_pitch"] + 2 * hit - 2 * hop,
             head_pitch=p["head_pitch"] - 3 * hit + 3 * dip - 3 * hop,
             shoulderR=p["shoulderR"] + 5 * hit + 3 * hop, shoulderL=p["shoulderL"] + 3 * hop,
             armL_out=p["armL_out"] + 4 * hit + 3 * hop - 2 * dip, elbowL=p["elbowL"] + 8 * dip,
             # the costume lags her: it sinks as she pops up and rocks forward after the arm lands
             pumpkin_z=p["pumpkin_z"] - 0.01 * _bell(f, 13.5, 2.2) * out + 0.008 * _bell(f, 17.5, 2.5) * out
             - 0.007 * hop + 0.006 * dip,
             pumpkin_pitch=p["pumpkin_pitch"] + 3 * _bell(f, 16.5, 2.6) * out,
             pumpkin_yaw=-3 * out,
             tail_back=p["tail_back"] + 12 * _bell(f, 12.0, 2.4) - 8 * _bell(f, 17.0, 2.8) - 5 * hop,
             tail_side=p["tail_side"] + 7 * _bell(f, 11.0, 2.5) - 5 * _bell(f, 16.0, 2.8))
    # feet: the left one lifts, steps 13 cm forward and lands with the strike; the right heel peels up behind
    heel = {"L": 6 * out + 12 * rise, "R": 24 * out + 8 * rise}
    for S in "LR":
        p["foot" + S] = heel[S]
        p["toe" + S] = heel[S]
    p["footL"] += -10 * lift  # toes up while it is in the air
    targets = {"L": _on_ball("L", heel["L"]) + Vector((0.01 * step, -0.13 * step, 0.045 * lift)),
               "R": _on_ball("R", heel["R"])}
    for S in "LR":
        reach(p, [f"leg{S}_fwd", f"leg{S}_out", f"knee{S}"], "Foot_" + S, targets[S], steps=24)
    # the arm: solved onto the line from the shoulder, straightest at the strike and at the top of each bounce
    if out > 0:
        q = dict(p)
        # the twist is fixed (elbow down and out, palm down): solved freely it flips as the elbow straightens
        q.update(armR_fwd=80, armR_out=34, armR_twist=POINT_TWIST)
        up = math.radians(POINT_UP + 4 * hit + 2.5 * hop - 2 * dip)
        aim_arm(q, "R", Vector((0.0, -math.cos(up), math.sin(up))), bend=7 - 5 * hit + 12 * dip - 3 * hop,
                keys=["armR_fwd", "armR_out"], twist=POINT_TWIST)
        for k in _POINT_ARM:
            p[k] += (q[k] - p[k]) * out
    return p


# --- Mr. Vale ---------------------------------------------------------------------------------------------
# He sits upright but easy, feet flat, the cauldron of sweets standing on his right thigh with his right hand
# through its handle; his left hand rests on his left thigh, points at the door, or waves.

VALE_HIP_ABOVE_SEAT = 0.09  # hip joint above the seat surface when seated
VALE_SIT_BACK = 0.05  # the pelvis sits this far behind the root


def vale_hips():
    """Offset of the Hips bone from its rest position when seated (also reported to the game)."""
    return Vector((0.0, VALE_SIT_BACK, SEAT + VALE_HIP_ABOVE_SEAT - RIG["UpperLeg_L"][1].z))


def _vale_sit(breathe=0.0, lean=0.0):
    off = vale_hips()
    p = pose(feet_world=1.0, hx=off.x, hy=off.y, hz=off.z,
             hip_pitch=-9 + lean, spine_pitch=5 + 0.8 * breathe + 0.5 * lean, chest_pitch=5 - 1.6 * breathe,
             neck_pitch=-2, head_pitch=-1 + 0.8 * breathe,
             shoulderL=2 + 2 * breathe, shoulderR=2 + 2 * breathe,
             legL_fwd=84, legR_fwd=84, kneeL=92, kneeR=92, legL_out=7, legR_out=9,
             fingersR=0, fingersL=14, thumbL=6,
             bBelly_x=-74, belly_lift=0.035, belly_fwd=0.012, bKnee_x=-4, bKnee_y=3)
    hip_l, hip_r = point(p, "UpperLeg_L"), point(p, "UpperLeg_R")
    thigh = (RIG["LowerLeg_L"][1] - RIG["UpperLeg_L"][1]).length
    ankle_z = RIG["Foot_L"][1].z
    for S, s, fwd, out in (("L", 1.0, 0.07, 0.035), ("R", -1.0, 0.0, 0.05)):
        hip = hip_l if S == "L" else hip_r
        reach(p, [f"leg{S}_fwd", f"leg{S}_out", f"knee{S}"], "Foot_" + S,
              Vector((hip.x + s * out, hip.y - thigh - 0.01 - fwd, ankle_z)))
    return p


def _vale_lap(p):
    """Seat the cauldron on the right thigh (hanging plumb from the fist) and rest the left hand on the left."""
    hip_r, knee_r = point(p, "UpperLeg_R"), point(p, "LowerLeg_R")
    on_thigh = hip_r.lerp(knee_r, 0.56)
    grip = Vector((on_thigh.x + 0.012, on_thigh.y, on_thigh.z + VALE_THIGH_R + 0.092 + 0.2 - 0.004))
    p.update(armR_fwd=30, armR_out=10, elbowR=85, armR_twist=35, handR_pitch=-10, handR_twist=20)
    reach(p, ["armR_fwd", "armR_twist", "elbowR"], "Cauldron", grip, steps=24)
    return p


def _vale_rest_hand(p):
    hip_l, knee_l = point(p, "UpperLeg_L"), point(p, "LowerLeg_L")
    on_thigh = hip_l.lerp(knee_l, 0.62)
    p.update(armL_fwd=20, armL_out=8, elbowL=70, armL_twist=15, handL_pitch=-8, handL_twist=75)
    reach(p, ["armL_fwd", "armL_twist", "elbowL"], "Fingers_L",
          Vector((on_thigh.x + 0.005, on_thigh.y, on_thigh.z + VALE_THIGH_R + 0.03)), steps=24)
    return p


VALE_THIGH_R = 0.085  # thigh radius under the trousers and wraps (set by the exporter from the body mesh)


def _vale_sway(p, a, k=1.0):
    """The loose bandage ends: slow, out of phase, the one by his ear and the one at his wrist the liveliest."""
    p.update(bHead_y=7 * k * math.sin(a - 0.4), bHead_x=5 * k * math.sin(a + 1.1),
             bArmL_y=6 * k * math.sin(a - 1.3), bArmL_x=4 * k * math.sin(a + 0.2),
             bArmR_y=5 * k * math.sin(a - 2.1), bArmR_x=4 * k * math.sin(a - 0.6),
             bKnee_y=3 + 6 * k * math.sin(a - 2.8), bKnee_x=-4 + 5 * k * math.sin(a - 1.6),
             bBelly_y=2.5 * k * math.sin(a - 0.9),
             pot_x=0.0, pot_y=0.0)
    return p


def vale_sit_idle(u):
    """Seated: gentle breathing, a slow look around, bandage ends swaying."""
    a = TAU * u
    breathe = math.sin(a - 0.3)
    p = _vale_sit(breathe)
    p.update(head_yaw=5 * math.sin(a - 0.9), head_roll=1.5 * math.sin(a + 0.6), chest_yaw=1.5 * math.sin(a - 0.5),
             spine_roll=0.6 * math.sin(a))
    _vale_sway(p, a)
    _vale_rest_hand(p)
    return _vale_lap(p)


VALE_DOOR = Vector((0.1, -0.96, 0.16))  # the front door from the bench: straight ahead, a little up


def vale_sit_talk(u):
    """Leaning in a little and pointing at the door with his left hand: two calm beats per cycle, a slow shake
    of the head ("It stayed shut")."""
    a = TAU * u
    beat = 0.5 + 0.5 * math.cos(2 * a)
    p = _vale_sit(0.6 * math.sin(a), lean=7 + 1.5 * beat)
    p.update(head_yaw=6 * math.sin(a), head_pitch=-4 + 2.5 * beat, head_roll=-1.5 * math.sin(a),
             chest_yaw=-4 + 1.5 * math.sin(a), spine_yaw=-2, shoulderL=6 + 3 * beat,
             handL_pitch=-4 - 5 * beat, fingersL=-12, thumbL=-10)
    _vale_sway(p, a, 1.3)
    aim_arm(p, "L", VALE_DOOR + Vector((0.02 * math.sin(a), 0, 0.045 * beat)), bend=26 - 16 * beat)
    return _vale_lap(p)


def vale_wave(u):
    """Seated wave with the left hand; starts and ends on SitIdle frame 0."""
    base = vale_sit_idle(0.0)
    env = smooth(u / 0.24) * (1 - smooth((u - 0.78) / 0.22))
    w = math.sin(TAU * 2.5 * (u - 0.1)) * env
    up = dict(base)
    up.update(armL_fwd=58, armL_out=40, armL_twist=18, elbowL=96, handL_pitch=-8, handL_side=0, handL_twist=0,
              fingersL=-10, thumbL=-8, shoulderL=12)
    p = blend(base, up, env)
    p.update(armL_out=p["armL_out"] + 9 * w, handL_side=15 * w, elbowL=p["elbowL"] + 4 * w,
             head_roll=base["head_roll"] + 4 * env + 1.5 * w, head_pitch=base["head_pitch"] - 3 * env,
             head_yaw=base["head_yaw"] * (1 - env), chest_roll=-2 * env + 1.0 * w, chest_pitch=base["chest_pitch"] - 2 * env)
    _vale_sway(p, TAU * u * 1.5, 1.0 + 0.8 * env)
    for k in ("bHead_y", "bHead_x", "bArmL_y", "bArmL_x", "bArmR_y", "bArmR_x", "bKnee_y", "bKnee_x", "bBelly_y"):
        p[k] = base[k] + (p[k] - base[k]) * env  # back to the idle's first frame at both ends
    p["bArmL_y"] += -14 * w
    p["bArmL_x"] += 6 * env
    return _vale_lap(p)


# name, pose function, frames at 30 fps, loops, intended ground speed (m/s), note
BEA_CLIPS = [
    ("Idle", bea_idle, 90, True, 0.0, "weight shift, a glance to her left (toward the house), a bounce on her toes"),
    ("Talk", bea_talk, 60, True, 0.0, "left arm points ahead-left at the house, two jabs per cycle"),
    ("Wave", bea_wave, 45, False, 0.0, "left-hand wave; starts and ends on Idle frame 0"),
    ("Point", bea_point, POINT_FRAMES, False, 0.0,
     "right index finger points straight ahead and 23 degrees up, left foot stamped forward, up on her toes; "
     "on target frames 13-41; starts and ends on Idle frame 0"),
]
VALE_CLIPS = [
    ("SitIdle", vale_sit_idle, 90, True, 0.0, "seated, breathing, bandage ends swaying, cauldron on his right thigh"),
    ("SitTalk", vale_sit_talk, 60, True, 0.0, "seated, left hand points straight ahead at the door"),
    ("Wave", vale_wave, 45, False, 0.0, "seated left-hand wave; starts and ends on SitIdle frame 0"),
]
