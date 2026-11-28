"""Clips of the crowd bodies, in the pose vocabulary of pip_anims.py (pure maths, mathutils only).

The crowd reuses the cast's motion: Idle and Walk are Pip's own `pip_anims.idle` and `pip_anims.walk` (the same
pose maths that made her clips, fitted to each body's skeleton by the solver), Wave is Bea's wave
(`witness_anims._wave`) raised higher, because on these narrower shoulders hers stood level and read as a point. Only Talk is new, because the cast's talk loops
point at the case (Bea points at the house): the crowd's chat points at nothing (living-street §3.4 rules 1-2).
The right hand always holds the walker's prop on the `Pail` bone, which hangs plumb as Pip's pail does.

On top of Pip's vocabulary: `blink` 0..1 brings the lids out of the skull (the `Blink` bone moves forward), and
`tail_side` / `tail_lift` swing the tail (`Tail_1`, `Tail_2`) when a walker wears one.
"""
import math

from mathutils import Matrix, Vector

import pip_anims as pa
from pip_anims import TAU, bump, rx, rz, smooth

FPS = pa.FPS
RIG = None
LID_TRAVEL = 0.0  # set by the exporter: how far (m) the lids travel out of the skull


def bind(rig, lid_travel):
    global RIG, LID_TRAVEL
    RIG = rig
    LID_TRAVEL = lid_travel
    pa.RIG = rig


def _extra(p, blink=0.0, tail_side=0.0, tail_lift=0.0):
    p["blink"] = blink
    p["tail_side"] = tail_side
    p["tail_lift"] = tail_lift
    return p


def _blinks(u, frames, at):
    """1 on the blink frames: two frames closed (about 70 ms) with one frame either side half closed."""
    f = u * frames
    out = 0.0
    for c in at:
        d = abs(f - c)
        out = max(out, 1.0 if d < 1.01 else 0.5 if d < 2.01 else 0.0)
    return out


IDLE_FRAMES = 90


def idle(u):
    a = TAU * u
    p = pa.idle(u)
    return _extra(p, _blinks(u, IDLE_FRAMES, (52,)), 9 * math.sin(a - 0.6), 4 * math.sin(2 * a))


def walk(u):
    a = TAU * u
    p = pa.walk(u)
    return _extra(p, 0.0, 14 * math.sin(a - 1.0), 6 + 5 * math.cos(2 * a - 1.4))


WAVE_FRAMES = 45


def wave(u):
    """A big wave with the free (left) hand high over the shoulder, the hand swinging side to side; the arm never
    stands level, so it cannot read as pointing (D26). Starts and ends on Idle frame 0."""
    p = pa.idle(0.0)
    env = smooth(u / 0.24) * (1 - smooth((u - 0.78) / 0.22))
    w = math.sin(TAU * 3.0 * (u - 0.1)) * env
    p["armL_out"] += env * (128 + 10 * w)
    p["armL_fwd"] += env * 14
    p["armL_twist"] += env * 30
    p["elbowL"] = p["elbowL"] * (1 - env) + env * (28 + 14 * w)
    p["handL_side"] = 22 * w
    p["handL_pitch"] *= 1 - env
    p["fingersL"] = p["fingersL"] * (1 - env) - 12 * env
    p["shoulderL"] += 12 * env
    p.update(head_roll=p["head_roll"] + 6 * env + 2 * w, head_pitch=p["head_pitch"] - 5 * env,
             chest_roll=p["chest_roll"] - 4 * env + 1.5 * w, spine_roll=p["spine_roll"] - 3 * env,
             chest_pitch=p["chest_pitch"] - 3 * env, hx=p["hx"] - 0.01 * env, footL=6 * env, footR=6 * env,
             toeL=5 * env, toeR=5 * env)
    i0 = idle(0.0)
    return _extra(p, 0.0, i0["tail_side"] - 10 * w, i0["tail_lift"] + 8 * env)


TALK_FRAMES = 72


def talk(u):
    """Chatting with someone in front: the free (left) hand opens and turns in front of the chest, two nods,
    weight on one hip. It points at nothing and looks only along the walker's own forward."""
    a = TAU * u
    p = pa.idle(u)
    beat = math.sin(2 * a)
    p.update(armL_fwd=28 + 10 * math.sin(2 * a + 0.4), armL_out=6 + 6 * math.sin(a), armL_twist=22,
             elbowL=72 + 14 * math.sin(2 * a - 0.3), handL_pitch=-12 + 10 * math.sin(2 * a + 1.0),
             handL_side=6 * math.sin(a + 0.5), fingersL=-6 + 8 * math.sin(2 * a), thumbL=-6,
             head_pitch=-2 + 4 * bump(a, 1.2, 0.35) + 4 * bump(a, 4.3, 0.35), head_yaw=4 * math.sin(a - 0.3),
             head_roll=3 * math.sin(a + 0.9), chest_yaw=4 * math.sin(a), spine_pitch=1.5 + 1.0 * beat,
             shoulderL=3 + 2 * beat, hip_roll=-1.8 * math.sin(a), hx=0.01 * math.sin(a))
    return _extra(p, _blinks(u, TALK_FRAMES, (30,)), 7 * math.sin(a), 4 * math.sin(2 * a + 0.4))


# --- trick-or-treat clips (docs/living-street-crowd-art.md "Trick-or-treat clips") --------------------------------
# The game sequences these by the numbers in Assets/SpookyStreet/Core/Crowd/CrowdClipTimings.cs; keep the two equal
# (the import tests read the clips' lengths and these points back from the FBX). Character space: metres, +X the
# walker's left, -Y ahead, Z up, from the root on the ground.

GIVE_FRAMES, PEEK_FRAMES, HOLD_FRAMES, CHEER_FRAMES, STEP_FRAMES = 48, 36, 48, 27, 12
GIVE_PICK, GIVE_RELEASE = 0.25, 0.55
PEEK_DOOR, PEEK_LET_GO = 0.25, 0.42
HOLD_DROP, CHEER_HOP = 0.6, 0.45
# the grown-up's left knuckles when the sweet is let go: 0.45 m ahead at a child's pail height, a little to the left
RELEASE = Vector((0.04, -0.45, 0.62))
# the middle of the child's pail mouth while it is held out (the pail hangs plumb PAIL_RIM below the right grip)
MOUTH = Vector((0.0, -0.26, 0.56))
PAIL_RIM = 0.085
# the edge of the door leaf where the left hand meets it, and where it lets go after a little push
DOOR = Vector((0.30, -0.32, 1.0))
DOOR_PUSHED = Vector((0.27, -0.43, 1.0))
# where the free hand's knuckles dip for a sweet, from the prop's grip: in front of the bail, at the sweets of a tin
# (rim 0.1 m under the grip) and just over those of a basket (rim 0.13 m under it); the fingers reach into either
PICK = Vector((0.025, -0.05, -0.06))
# the left fist beside the right one on the pail's bail
BAIL = Vector((0.052, 0.004, -0.004))
# WalkStart ends in the Walk cycle at this phase (the right foot planted and behind, the left in its swing); WalkStop
# starts at Walk phase 0 (the left heel just struck). Root travel over each is crowd_anims.step_distance().
# (on a whole Walk frame, so the game can start Walk there exactly: child frame 20 of 24, grown-up 35 of 41)
START_PHASE = {"CrowdChild": 20 / 24, "CrowdAdult": 35 / 41}
BODY = "CrowdChild"
WALK = {"speed": 1.167, "frames": 24}


def _at(p, bone, point=None):
    return pa.bone_point(p, bone, point)


def _sole(p, S, i):
    """Sole point i of foot S (0 heel, 1 ball, 2 toe tip) for pose p."""
    bone, point = RIG["_sole"][S][i]
    return pa.bone_point(p, bone, point)


def _lowest(p):
    return min(_sole(p, S, i).z for S in "LR" for i in range(3))


def _hips(p):
    """The hips offset a pose really gets (its `ground` applied)."""
    return pa.solve(p, RIG)[1]


def _floor(p):
    """Takes `ground` off a pose and sets hz so that it gives the same hips, so the hips can then be moved freely."""
    off = _hips(p)
    p.update(ground=0.0, hx=off.x, hy=off.y, hz=off.z)
    return p


def _no_sink(p):
    low = _lowest(p)
    if low < 0.0:
        p["hz"] -= low
    return p


def _reach_bone(p, S, bone, target, weight=1.0):
    """pa.reach for any point carried by hand S (the Pail bone is the right hand's grip)."""
    q = dict(p)
    for _ in range(4):
        off = _at(q, "Fingers_" + S) - _at(q, bone)
        pa.reach(q, S, Vector(target) + off)
    for k in (f"arm{S}_fwd", f"arm{S}_out", f"elbow{S}"):
        p[k] += (q[k] - p[k]) * weight
    return p


def _reach(p, S, target, bone=None, twists=(-40, -20, 0, 20, 40, 60, 80)):
    """Puts the knuckles (or a point carried by the hand, e.g. the Pail grip) of hand S on target, trying a few arm
    twists (which side the elbow bends to) when the authored one cannot get there: a hand crossing the body needs the
    twist. Keeps the authored twist when it already reaches within 5 mm."""
    def solved(twist):
        q = dict(p)
        if twist is not None:
            q[f"arm{S}_twist"] = twist
        if bone:
            _reach_bone(q, S, bone, target)
            got = _at(q, bone)
        else:
            pa.reach(q, S, target)
            got = _at(q, "Fingers_" + S)
        return (got - Vector(target)).length, q
    best = solved(None)
    if best[0] > 0.005:
        for t in twists:
            cand = solved(t)
            if cand[0] < best[0] - 0.002:
                best = cand
    p.update(best[1])
    return best[0]


def _spline(keys, u):
    """Catmull-Rom through the key poses with a still start and end (pip_anims.keyed_still): a clip eases out of
    Idle frame 0 and back into it."""
    return pa.keyed_still(keys, u)


def _leg(p, S, target, bone=None, point=None, iters=40):
    """Turns leg{S}_fwd, leg{S}_out and knee{S} so a point of the leg (default: the ankle) lands on target (all three
    axes: a planted foot must not drift sideways either). Returns the miss (m)."""
    bone = bone or "Foot_" + S
    keys = (f"leg{S}_fwd", f"leg{S}_out", f"knee{S}")
    lo, hi = (-70.0, -25.0, 0.0), (110.0, 35.0, 150.0)
    q = dict(p)
    target = Vector(target)

    def miss(pose_):
        return _at(pose_, bone, point) - target
    e = miss(q)
    lam = 1e-4
    for _ in range(iters):
        if e.length < 1e-4:
            break
        J = []
        for k in keys:
            d = dict(q)
            d[k] += 0.25
            J.append((miss(d) - e) / 0.25)
        Jm = Matrix((J[0], J[1], J[2])).transposed()
        H = Jm.transposed() @ Jm
        g = Jm.transposed() @ e
        for n in range(3):
            H[n][n] += lam
        try:
            step = H.inverted() @ g
        except ValueError:
            lam *= 10
            continue
        trial = dict(q)
        for n, k in enumerate(keys):
            trial[k] = min(hi[n], max(lo[n], q[k] - max(-20.0, min(20.0, step[n]))))
        e2 = miss(trial)
        if e2.length < e.length:
            q, e, lam = trial, e2, max(1e-7, lam * 0.5)
        else:
            lam *= 6
            if lam > 1e4:
                break
    p.update({k: q[k] for k in keys})
    return e.length


_CACHE = {}


def _cached(name, make):
    """Key poses are solved (reaches, twists) once per body and clip."""
    key = (name, BODY, id(RIG))
    if key not in _CACHE:
        _CACHE[key] = make()
    return _CACHE[key]


def _keys(keys, u, floor=True):
    """pa.keyed through key poses whose `ground` was taken off (so the hips move as keyed), then kept out of the
    floor. The ends are returned exactly (Idle frame 0)."""
    if u <= 0.0:
        return dict(keys[0][1])
    if u >= 1.0:
        return dict(keys[-1][1])
    p = _spline([(k, _floor(dict(q))) for k, q in keys], u)
    p["ground"] = 0.0
    return _no_sink(p) if floor else p


def give_treat(u):
    """A grown-up gives a child a sweet (GIVE_FRAMES). The bowl, tin or basket hangs from the right hand, which
    lifts it to the waist; the free left hand dips in for a sweet (GIVE_PICK), the body leans forward and down over
    bending knees and the hand puts the sweet into the pail at RELEASE (GIVE_RELEASE), opens, and they straighten.
    Starts and ends on Idle frame 0. Looks only at the bowl and the child in front."""
    return _keys(_cached("give_treat", _give_treat_keys), u)


def _give_treat_keys():
    rest = idle(0.0)
    pick = dict(rest)
    pick.update(chest_yaw=-12, spine_yaw=-5, spine_pitch=4, chest_pitch=4, head_pitch=24, head_yaw=-10, head_roll=-3,
                hip_roll=-2, armL_twist=-10, handL_pitch=-38, fingersL=24, thumbL=10)
    _reach(pick, "R", bone="Pail", target=Vector((-0.17, -0.28, 1.0)))
    _reach(pick, "L", _at(pick, "Pail") + PICK)
    # on the way: the bowl comes up first, the free hand follows halfway (the same arm as at the pick, so the hand
    # travels straight to the bowl instead of swinging out)
    raise_ = dict(rest)
    raise_.update(chest_yaw=-6, spine_yaw=-3, head_pitch=12, head_yaw=-6, spine_pitch=2, fingersL=14, handL_pitch=-18)
    _reach(raise_, "R", bone="Pail", target=Vector((-0.19, -0.25, 0.97)))
    for k in ("armL_fwd", "armL_out", "armL_twist", "elbowL"):
        raise_[k] = rest[k] + (pick[k] - rest[k]) * 0.55
    lift = dict(rest)
    lift.update(chest_yaw=-3, spine_pitch=7, hip_pitch=5, chest_pitch=4, head_pitch=22, head_roll=3, hy=0.01,
                kneeL=10, kneeR=10, legL_fwd=7, legR_fwd=7, handL_pitch=-30, fingersL=48, thumbL=16)
    _reach(lift, "R", bone="Pail", target=Vector((-0.21, -0.22, 0.96)))
    _reach(lift, "L", Vector((0.02, -0.33, 1.02)))
    put = dict(rest)
    put.update(hip_pitch=22, spine_pitch=14, chest_pitch=9, neck_pitch=-8, head_pitch=-6, head_roll=6, hy=0.05,
               kneeL=30, kneeR=30, legL_fwd=19, legR_fwd=19, handL_pitch=-34, fingersL=48, thumbL=16,
               shoulderL=4, footL=0, footR=0)
    put.update(armR_fwd=2, armR_out=12, elbowR=98, armR_twist=-10)
    _reach(put, "L", RELEASE)
    let_go = dict(put)
    let_go.update(head_pitch=2, head_roll=9, fingersL=-6, thumbL=-4, handL_pitch=-20)
    _reach(let_go, "L", RELEASE + Vector((0.0, 0.035, 0.06)))
    up = dict(rest)
    up.update(hip_pitch=5, spine_pitch=3, kneeL=8, kneeR=8, legL_fwd=4, legR_fwd=4, head_pitch=4, head_roll=6,
              armL_fwd=16, armL_out=4, elbowL=34, fingersL=6, armR_fwd=12, armR_out=4, elbowR=36)
    return [(0.0, rest), (0.12, raise_), (GIVE_PICK, pick), (0.38, lift), (GIVE_RELEASE, put),
                  (0.66, let_go), (0.84, up), (1.0, rest)]


def open_door_peek(u):
    """A grown-up comes into the doorway (PEEK_FRAMES): a small lean forward, the left hand reaches for the edge of
    the door leaf (DOOR at PEEK_DOOR, which the game swings), pushes it a little and lets go (PEEK_LET_GO), then a
    friendly look down at the children on the step. Bells only: the hand never knocks. Starts and ends on Idle 0."""
    return _keys(_cached("open_door_peek", _open_door_peek_keys), u)


def _open_door_peek_keys():
    rest = idle(0.0)
    touch = dict(rest)
    touch.update(hip_pitch=3, spine_pitch=6, chest_yaw=8, spine_yaw=3, head_yaw=10, head_pitch=4, hy=-0.025,
                 kneeL=7, kneeR=4, legL_fwd=5, armL_twist=-30, handL_pitch=-48, handL_side=10, fingersL=-6,
                 thumbL=-8, shoulderL=3)
    _reach(touch, "L", DOOR)
    come = dict(rest)
    come.update(spine_pitch=4, chest_yaw=5, head_yaw=8, head_pitch=2, hy=-0.01, handL_pitch=-24, fingersL=4)
    for k in ("armL_fwd", "armL_out", "armL_twist", "elbowL"):
        come[k] = rest[k] + (touch[k] - rest[k]) * 0.6
    come["elbowL"] += 18  # the hand comes up close, then the arm opens to the edge
    push = dict(touch)
    push.update(hip_pitch=5, spine_pitch=8, hy=-0.035, chest_yaw=6, head_yaw=4, head_pitch=8)
    _reach(push, "L", DOOR_PUSHED)
    look = dict(rest)
    look.update(spine_pitch=5, chest_pitch=6, neck_pitch=4, head_pitch=24, head_roll=9, head_yaw=-2, hy=-0.015,
                kneeL=6, kneeR=6, armL_fwd=14, armL_out=6, elbowL=34, fingersL=8, shoulderL=2, shoulderR=2)
    nod = dict(look)
    nod.update(head_pitch=16, head_roll=5, chest_pitch=4)
    return [(0.0, rest), (0.13, come), (PEEK_DOOR, touch), (PEEK_LET_GO, push), (0.6, look), (0.8, nod),
                  (1.0, rest)]


def hold_pail(u):
    """A child holds the pail out for a sweet (HOLD_FRAMES): both hands lift it forward by the bail until its mouth
    is at MOUTH, she looks up at the giver, bobs when the sweet drops in (HOLD_DROP) and peeks in, then brings it
    back. Starts and ends on Idle frame 0."""
    return _keys(_cached("hold_pail", _hold_pail_keys), u)


def _hold_pail_keys():
    rest = idle(0.0)
    grip = MOUTH + Vector((0.0, 0.0, PAIL_RIM))

    def held(at=None, **kw):
        q = dict(rest)
        q.update(spine_pitch=-1, chest_pitch=-2, shoulderL=4, shoulderR=4, armL_twist=24, armR_twist=-16,
                 handL_pitch=-8, fingersL=62, thumbL=20, kneeL=4, kneeR=4, legL_fwd=2, legR_fwd=2)
        q.update(kw)
        _reach(q, "R", bone="Pail", target=at if at is not None else grip)
        _reach(q, "L", _at(q, "Pail") + BAIL)
        return q
    lift = held(Vector((-0.06, -0.17, 0.57)), head_pitch=6, pail_x=8, spine_pitch=2)
    out = held(head_pitch=-9, head_roll=3, pail_x=-3)
    wait = held(head_pitch=-11, head_roll=5, head_yaw=2, pail_x=1, footL=7, footR=7, toeL=-7, toeR=-7)
    bob = held(grip + Vector((0.0, 0.0, -0.025)), head_pitch=12, head_roll=0, kneeL=9, kneeR=9, legL_fwd=5,
               legR_fwd=5, pail_x=3)
    happy = held(grip + Vector((0.0, 0.0, 0.006)), head_pitch=-6, head_roll=-6, pail_x=-2)
    back = held(Vector((-0.07, -0.2, 0.55)), head_pitch=-4, head_roll=-3, pail_x=-4)
    return [(0.0, rest), (0.17, lift), (0.32, out), (0.5, wait), (HOLD_DROP, bob), (0.69, happy),
                  (0.84, back), (1.0, rest)]


def cheer(u):
    """A small happy hop with both arms up (CHEER_FRAMES), the top of the hop at CHEER_HOP: a crouch, a spring onto
    the toes and up, fists high, a soft landing. Starts and ends on Idle frame 0."""
    keys = _cached("cheer", _cheer_keys)
    if u <= 0.0 or u >= 1.0:
        return idle(0.0)
    p = _spline(keys, u)
    p["ground"] = 0.0
    return _no_sink(p)


def _cheer_keys():
    rest = idle(0.0)
    crouch = dict(rest)
    crouch.update(kneeL=40, kneeR=40, legL_fwd=26, legR_fwd=26, hip_pitch=14, spine_pitch=6, chest_pitch=2,
                  head_pitch=-2, armL_fwd=-20, armL_out=10, elbowL=34, armR_fwd=-16, armR_out=10, elbowR=26,
                  fingersL=56, thumbL=20, pail_x=12, tail_lift=-6)
    spring = dict(rest)
    spring.update(kneeL=6, kneeR=6, legL_fwd=3, legR_fwd=3, footL=34, footR=34, toeL=-12, toeR=-12, hip_pitch=0,
                  spine_pitch=-2, chest_pitch=-2, head_pitch=-10, armL_out=112, armL_fwd=34, elbowL=48,
                  armR_out=62, armR_fwd=22, elbowR=58, fingersL=70, thumbL=30, shoulderL=10, shoulderR=6,
                  pail_x=-6, tail_lift=14)
    # the top: the free fist high, the prop hand out to the side at shoulder height (the pail, bag or lantern
    # swinging there, clear of the head)
    top = dict(spring)
    top.update(kneeL=26, kneeR=22, legL_fwd=16, legR_fwd=14, footL=30, footR=30, toeL=-6, toeR=-6, head_pitch=-12,
               head_roll=6, armL_out=148, armL_fwd=22, elbowL=30, armR_out=84, armR_fwd=24, elbowR=64, pail_x=10,
               tail_lift=22, tail_side=-8)
    # landing: the arms come down in front with bent elbows (never level out to the side, which reads as a point)
    land = dict(rest)
    land.update(kneeL=38, kneeR=36, legL_fwd=24, legR_fwd=23, hip_pitch=12, spine_pitch=5, head_pitch=-4,
                head_roll=3, armL_out=34, armL_fwd=30, elbowL=74, armR_out=26, armR_fwd=16, elbowR=44,
                fingersL=56, pail_x=-10, tail_lift=-4)
    settle = dict(rest)
    settle.update(kneeL=8, kneeR=8, legL_fwd=4, legR_fwd=4, head_pitch=-6, head_roll=-4, armL_out=12, armL_fwd=8,
                  elbowL=30, armR_out=10, elbowR=18, pail_x=5, tail_side=6)
    keys = [(0.0, rest), (0.2, crouch), (0.33, spring), (CHEER_HOP, top), (0.61, land), (0.79, settle), (1.0, rest)]
    keys = [(k, _floor(dict(q))) for k, q in keys]
    t = dict(keys[3][1])
    t["hz"] = 0.0
    t["hz"] = 0.065 - _lowest(t)  # the top of the hop: the lowest sole 6.5 cm off the ground
    keys[3] = (CHEER_HOP, t)
    return keys


def step_distance(clip, body=None):
    """Metres the root travels over WalkStart or WalkStop, chosen so the planted foot never slides: WalkStart keeps
    the right ball of the foot where it stood in Idle until it is where Walk at START_PHASE has it; WalkStop keeps the
    left heel where Walk frame 0 struck it until it is under the hips in Idle."""
    body = body or BODY
    if clip == "WalkStart":
        return _sole(walk(START_PHASE[_kind(body)]), "R", 1).y - _sole(idle(0.0), "R", 1).y
    return _sole(idle(0.0), "L", 0).y - _sole(walk(0.0), "L", 0).y


def P_H():
    """The body's height (m), from the skeleton: the top of the head bone's rest head is at ~0.8 of it."""
    return RIG["Head"][1].z / 0.79


def _kind(body):
    return "CrowdChild" if body == "CrowdChild" else "CrowdAdult"


def root_travel(clip, u, body=None):
    """The root's forward travel at u, the same curve as CrowdClipTimings.RootTravel: WalkStart d * u^k from
    standing to Walk speed, WalkStop d * (1 - (1 - u)^k) from Walk speed to standing, k = speed * length / d."""
    d = step_distance(clip, body)
    k = WALK["speed"] * (STEP_FRAMES / FPS) / d
    u = min(1.0, max(0.0, u))
    return d * u ** k if clip == "WalkStart" else d * (1.0 - (1.0 - u) ** k)


def _head_world(p):
    """The same pose with the head's angles in character space (Walk keeps its head level that way; Idle carries
    it on the neck), so the two can be blended."""
    if p["head_world"] > 0.5:
        return p
    A, _pos = pa.fk(p, RIG)
    e = A["Head"].to_euler("XYZ")
    p.update(head_world=1.0, head_pitch=math.degrees(e.x), head_roll=math.degrees(e.y), head_yaw=math.degrees(e.z))
    return p


def _hermite(p0, m0, p1, m1, t):
    t2, t3 = t * t, t * t * t
    return (2 * t3 - 3 * t2 + 1) * p0 + (t3 - 2 * t2 + t) * m0 + (-2 * t3 + 3 * t2) * p1 + (t3 - t2) * m1


def _step(u, start_clip):
    """WalkStart (start_clip True) or WalkStop. The upper body eases between Idle 0 and the Walk pose; the planted
    foot is held on the ground where it stands in the world while the root travels by root_travel(); the swinging
    foot's ankle follows a smooth arc in the world that leaves (or meets) the Walk cycle at its own speed."""
    clip = "WalkStart" if start_clip else "WalkStop"
    phase = START_PHASE[_kind(BODY)]
    a = idle(0.0) if start_clip else walk(0.0)
    b = walk(phase) if start_clip else idle(0.0)
    if u <= 0.0:
        return dict(a)
    if u >= 1.0:
        return dict(b)
    a, b = _head_world(dict(a)), _head_world(dict(b))
    plant, swing = ("R", "L") if start_clip else ("L", "R")
    sole_i = 1 if start_clip else 0  # the ball of the foot pushes off; the heel has just struck
    x = root_travel(clip, u)
    ease = smooth(u)
    p = pa.blend(_floor(dict(a)), _floor(dict(b)), ease)
    p["ground"] = 0.0
    # a small weight shift over the planted foot, and the walk's bob coming in or going out
    shift = math.sin(math.pi * u)
    p["hx"] += (-0.012 if plant == "R" else 0.012) * shift
    p["hip_roll"] += (2.5 if plant == "R" else -2.5) * shift
    # the planted foot: its pitch eases between the two poses (the heel peels up in WalkStart, the struck foot rolls
    # flat in WalkStop), and the leg is turned so that its sole point stays where it was
    f0, f1 = a["foot" + plant], b["foot" + plant]
    roll = smooth(u / 0.55) if not start_clip else smooth((u - 0.35) / 0.65)
    p["foot" + plant] = f0 + (f1 - f0) * roll
    p["toe" + plant] = a["toe" + plant] + (b["toe" + plant] - a["toe" + plant]) * roll
    planted = _sole(a, plant, sole_i) + Vector((0.0, x, 0.0))
    planted.z = 0.0
    # where it ends (the other pose's sole point, in the world): the same spot, so x eases only if the two differ
    planted.x = _sole(a, plant, sole_i).x + (_sole(b, plant, sole_i).x - _sole(a, plant, sole_i).x) * ease
    bone, point = RIG["_sole"][plant][sole_i]
    # the swinging ankle in the world: Hermite between the two poses, leaving or meeting Walk at its own speed
    walk_t = WALK["frames"] / FPS
    clip_t = STEP_FRAMES / FPS
    d = step_distance(clip)
    k = WALK["speed"] * clip_t / d

    def ankle(q):
        return _at(q, "Foot_" + swing)
    w0 = ankle(a)
    w1 = ankle(b) - Vector((0.0, d, 0.0))
    eps = 0.01
    if start_clip:
        dw = (ankle(walk(phase + eps)) - ankle(walk(phase - eps))) / (2 * eps) * (clip_t / walk_t)
        m0, m1 = Vector((0, 0, 0)), dw - Vector((0.0, d * k, 0.0))
    else:
        dw = (ankle(walk(eps)) - ankle(walk(-eps))) / (2 * eps) * (clip_t / walk_t)
        m0, m1 = dw - Vector((0.0, d * k, 0.0)), Vector((0, 0, 0))
    m0.x = m1.x = 0.0
    target = _hermite(w0, m0, w1, m1, u)
    if start_clip:
        # the first step: the foot lifts, then swings forward into the cycle at the cycle's speed. Its forward travel
        # is a power curve (never back, as a Hermite with that end speed would overshoot)
        dy = w1.y - w0.y
        n = max(1.0, m1.y / dy) if abs(dy) > 1e-4 else 1.0
        target.y = w0.y + dy * u ** n
        # and it rises less than a Hermite into the cycle's falling foot would lift it (a step, not a march)
        target.z = _hermite(w0.z, 0.0, w1.z, m1.z * 0.3, u)
    target.z += (0.045 if start_clip else 0.035) * (P_H() / 1.05) * 16 * (u * (1 - u)) ** 2
    target = target + Vector((0.0, x, 0.0))  # world -> root space (the root is x ahead)
    pf0, pf1 = a["foot" + swing], b["foot" + swing]
    p["foot" + swing] = pf0 + (pf1 - pf0) * ease
    # hips low enough for the planted leg to reach the ground
    for _ in range(3):
        miss = _leg(p, plant, planted, bone, point)
        if miss < 0.002:
            break
        p["hz"] -= miss
    _leg(p, swing, target)
    return p


def walk_start(u):
    """From Idle frame 0 into the Walk cycle at START_PHASE (STEP_FRAMES): the weight goes over the right foot, its
    heel peels up while the ball stays put, the left foot lifts and swings into the cycle. In place: move the root by
    root_travel("WalkStart", u)."""
    return _step(u, True)


def walk_stop(u):
    """From Walk frame 0 (the left heel just struck) to Idle frame 0 (STEP_FRAMES): the left foot rolls flat and
    stays put, the right swings in beside it, the body settles. In place: move the root by root_travel("WalkStop", u)."""
    return _step(u, False)


def clips(body):
    """[(name, pose fn, frames, loops, ground speed m/s, note)] for a body. The walk's ground speed scales with
    leg length from Pip's (1.6 m/s at 20 frames, hips 0.588 m up); the grown-up walks a slower cadence so a
    parent and a child stroll side by side at the same speed (about 1.17 m/s). After the four shipped clips come the
    trick-or-treat clips: the child's HoldPail and Cheer, the grown-up's (and the door neighbours') GiveTreat and
    OpenDoorPeek, and WalkStart / WalkStop for both."""
    global BODY
    BODY = body
    hip = RIG["UpperLeg_L"][1].z
    pip_speed = 1.6 * hip / 0.588
    frames = 24 if body == "CrowdChild" else 41
    speed = round(pip_speed * 20 / frames, 3)
    WALK.update(speed=speed, frames=frames)
    out = [
        ("Idle", idle, IDLE_FRAMES, True, 0.0, "Pip's idle on this body; one blink at frame 52"),
        ("Walk", walk, frames, True, speed, "Pip's walk on this body; left foot strikes at frame 0"),
        ("Wave", wave, WAVE_FRAMES, False, 0.0, "left hand high, three swings; starts and ends on Idle frame 0"),
        ("Talk", talk, TALK_FRAMES, True, 0.0, "chat loop: left hand turns in front of the chest, two nods; "
                                               "points at nothing; one blink at frame 30"),
    ]
    if body == "CrowdChild":
        out += [("HoldPail", hold_pail, HOLD_FRAMES, False, 0.0,
                 f"pail held out by the bail in both hands, mouth at {tuple(MOUTH)}; bob at {HOLD_DROP}"),
                ("Cheer", cheer, CHEER_FRAMES, False, 0.0, f"a hop with both arms up; top at {CHEER_HOP}")]
    else:
        out += [("GiveTreat", give_treat, GIVE_FRAMES, False, 0.0,
                 f"left hand into the bowl at {GIVE_PICK}, sweet let go at {tuple(RELEASE)} at {GIVE_RELEASE}"),
                ("OpenDoorPeek", open_door_peek, PEEK_FRAMES, False, 0.0,
                 f"left hand on the door edge {tuple(DOOR)} at {PEEK_DOOR}, lets go at {PEEK_LET_GO}")]
    out += [("WalkStart", walk_start, STEP_FRAMES, False, speed,
             f"Idle 0 -> Walk phase {START_PHASE[_kind(body)]}; root travels {step_distance('WalkStart'):.3f} m"),
            ("WalkStop", walk_stop, STEP_FRAMES, False, speed,
             f"Walk phase 0 -> Idle 0; root travels {step_distance('WalkStop'):.3f} m")]
    return out


def solve(p):
    """Pose -> ({bone: 3x3 delta in parent-carried axes}, {bone: offset})."""
    D, offset = pa.solve(p, RIG)
    if "Tail_1" in RIG:
        D["Tail_1"] = rz(p["tail_side"]) @ rx(p["tail_lift"])
        D["Tail_2"] = rz(p["tail_side"] * 0.9) @ rx(p["tail_lift"] * 0.8)
    offsets = {"Hips": offset}
    if "Blink" in RIG:
        offsets["Blink"] = Vector((0.0, -LID_TRAVEL * p["blink"], 0.0))
    return D, offsets


_ = Matrix
