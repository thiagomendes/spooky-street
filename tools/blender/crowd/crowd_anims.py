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


def clips(body):
    """[(name, pose fn, frames, loops, ground speed m/s, note)] for a body. The walk's ground speed scales with
    leg length from Pip's (1.6 m/s at 20 frames, hips 0.588 m up); the grown-up walks a slower cadence so a
    parent and a child stroll side by side at the same speed (about 1.17 m/s)."""
    hip = RIG["UpperLeg_L"][1].z
    pip_speed = 1.6 * hip / 0.588
    frames = 24 if body == "CrowdChild" else 41
    speed = round(pip_speed * 20 / frames, 3)
    return [
        ("Idle", idle, IDLE_FRAMES, True, 0.0, "Pip's idle on this body; one blink at frame 52"),
        ("Walk", walk, frames, True, speed, "Pip's walk on this body; left foot strikes at frame 0"),
        ("Wave", wave, WAVE_FRAMES, False, 0.0, "left hand high, three swings; starts and ends on Idle frame 0"),
        ("Talk", talk, TALK_FRAMES, True, 0.0, "chat loop: left hand turns in front of the chest, two nods; "
                                               "points at nothing; one blink at frame 30"),
    ]


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
