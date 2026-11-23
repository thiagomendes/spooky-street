"""Pip's animation clips as pure pose maths (spec §5 moveset, v1.2): no Blender scene access, only mathutils.

A pose is a flat dict of named scalars in degrees / metres (see DEFAULTS). `solve` turns it into one rotation
per bone, expressed as a delta in the character's own axes (X = Pip's left, -Y = forward, Z = up) carried by the
parent bone, so a knee always bends with rx(+) and an arm always lifts sideways with ry(-side) however the rest
of the body is posed. pip_export.py converts those deltas into pose-bone quaternions.

All clips are in place (no root motion); CLIPS records the ground speed each was animated for.

The wall and ledge clips (S003) also place a hand on a point in space: `fk` measures a pose, `reach` turns an arm
until its knuckles are on the target and `level_hand` lays the hand flat. They need the solver rig, which
pip_export.py puts in RIG before keying; without it they leave the authored arm as it is.
"""
import math

from mathutils import Matrix, Vector

FPS = 30
TAU = 2 * math.pi


def rx(a):
    return Matrix.Rotation(math.radians(a), 3, "X")


def ry(a):
    return Matrix.Rotation(math.radians(a), 3, "Y")


def rz(a):
    return Matrix.Rotation(math.radians(a), 3, "Z")


def smooth(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def bump(theta, centre, width):
    """Periodic bell around `centre` (radians) with half-width `width`."""
    d = (theta - centre + math.pi) % TAU - math.pi
    return math.exp(-(d / width) ** 2)


SIDES = (("L", 1.0), ("R", -1.0))
CHAINS = (("L", 1.0), ("M", 0.0), ("R", -1.0))

DEFAULTS = {
    # hips: offset from rest (m) and rotation; `ground` 0..1 drops/lifts the hips so the lowest sole is on Z=0
    "hx": 0.0, "hy": 0.0, "hz": 0.0, "hip_pitch": 0.0, "hip_yaw": 0.0, "hip_roll": 0.0, "ground": 0.0,
    "spine_pitch": 0.0, "spine_yaw": 0.0, "spine_roll": 0.0,
    "chest_pitch": 0.0, "chest_yaw": 0.0, "chest_roll": 0.0,
    "neck_pitch": 0.0,
    # head: relative to the neck, or absolute (character space) when head_world = 1
    "head_pitch": 0.0, "head_yaw": 0.0, "head_roll": 0.0, "head_world": 0.0,
    # hair locks (relative to the head): swing back (+) and to Pip's left (+)
    "hair_back": 0.0, "hair_side": 0.0, "hair_flare": 0.0,
    # pail: hangs plumb in character space when pail_world = 1; pail_x swings it back (+), pail_y to the left (+)
    "pail_x": 0.0, "pail_y": 0.0, "pail_world": 1.0,
    # feet: absolute pitch (toes down +) when feet_world = 1, else relative to the shin
    "feet_world": 0.0,
}
for _s, _ in SIDES:
    DEFAULTS.update({
        f"shoulder{_s}": 0.0,  # shrug up
        f"arm{_s}_fwd": 0.0, f"arm{_s}_out": 0.0, f"arm{_s}_twist": 0.0, f"elbow{_s}": 0.0,
        f"hand{_s}_pitch": 0.0, f"hand{_s}_side": 0.0, f"fingers{_s}": 0.0, f"thumb{_s}": 0.0,
        f"leg{_s}_fwd": 0.0, f"leg{_s}_out": 0.0, f"leg{_s}_twist": 0.0, f"knee{_s}": 0.0,
        f"foot{_s}": 0.0, f"toe{_s}": 0.0,
    })
for _c, _ in CHAINS:
    for _i in (1, 2, 3):
        # cape bones are absolute (character space): pitched back (+), fanned around Z, flared sideways
        DEFAULTS.update({f"cape{_c}{_i}_back": 0.0, f"cape{_c}{_i}_fan": 0.0, f"cape{_c}{_i}_flare": 0.0})
DEFAULTS["cape_sway"] = 0.0  # whole cape swings to Pip's left (+)


def pose(**kw):
    p = dict(DEFAULTS)
    for k, v in kw.items():
        if k not in p:
            raise KeyError(k)
        p[k] = v
    return p


def cape(p, back, fan=(0, 0, 0), flare=(0, 0, 0), side_back=None, sway=0.0):
    """Set the three cape chains: `back` per bone level for the middle chain, `side_back` for the outer two."""
    side_back = side_back or back
    for c, s in CHAINS:
        for i in range(3):
            p[f"cape{c}{i + 1}_back"] = (side_back if s else back)[i]
            p[f"cape{c}{i + 1}_fan"] = fan[i] * s
            p[f"cape{c}{i + 1}_flare"] = flare[i] * abs(s)
    p["cape_sway"] = sway
    return p


def blend(a, b, t):
    return {k: a[k] + (b[k] - a[k]) * t for k in a}


def keyed(keys, u):
    """Catmull-Rom through [(u, pose), …] (clamped ends): smooth, with a little natural overshoot."""
    if u <= keys[0][0]:
        return dict(keys[0][1])
    if u >= keys[-1][0]:
        return dict(keys[-1][1])
    i = max(k for k in range(len(keys) - 1) if keys[k][0] <= u)
    u0, p1 = keys[i]
    u1, p2 = keys[i + 1]
    p0 = keys[max(0, i - 1)][1]
    p3 = keys[min(len(keys) - 1, i + 2)][1]
    t = (u - u0) / (u1 - u0)
    t2, t3 = t * t, t * t * t
    out = {}
    for k in p1:
        a, b, c, d = p0[k], p1[k], p2[k], p3[k]
        out[k] = 0.5 * (2 * b + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3)
    for k in ("ground", "head_world", "pail_world", "feet_world"):
        out[k] = min(1.0, max(0.0, out[k]))
    return out


# --- solver -----------------------------------------------------------------------------------------------

def _lerp_rot(m, t):
    """Identity → m by fraction t."""
    if t >= 1.0:
        return m
    return Matrix.Identity(3).to_quaternion().slerp(m.to_quaternion(), max(0.0, t)).to_matrix()


def solve(p, rig):
    """Pose dict -> ({bone: 3x3 delta in parent-carried character axes}, hips offset Vector).

    `rig` maps bone -> (parent or None, rest head Vector) in creation (parents-first) order, and has
    rig["_sole"] = {"L": [(bone, point), …], "R": …}: rest-space sole points used to keep the feet on the ground."""
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
    D["Hair_L"] = rx(-p["hair_back"]) @ ry(p["hair_side"] - p["hair_flare"])
    D["Hair_R"] = rx(-p["hair_back"]) @ ry(p["hair_side"] + p["hair_flare"])
    D["Hair_Back"] = rx(-p["hair_back"] - p["hair_flare"]) @ ry(p["hair_side"])
    for S, s in SIDES:
        D["Shoulder_" + S] = ry(-s * p[f"shoulder{S}"])
        D["UpperArm_" + S] = rx(-p[f"arm{S}_fwd"]) @ ry(-s * p[f"arm{S}_out"]) @ rz(-s * p[f"arm{S}_twist"])
        D["LowerArm_" + S] = rx(-p[f"elbow{S}"])
        D["Hand_" + S] = rx(-p[f"hand{S}_pitch"]) @ ry(-s * p[f"hand{S}_side"])
        D["Fingers_" + S] = ry(s * p[f"fingers{S}"])
        D["Thumb_" + S] = ry(s * p[f"thumb{S}"])
        D["UpperLeg_" + S] = rz(s * p[f"leg{S}_twist"]) @ ry(-s * p[f"leg{S}_out"]) @ rx(-p[f"leg{S}_fwd"])
        D["LowerLeg_" + S] = rx(p[f"knee{S}"])
        if p["feet_world"] > 0.5:
            W["Foot_" + S] = rx(p[f"foot{S}"])
        else:
            D["Foot_" + S] = rx(p[f"foot{S}"])
        D["Toe_" + S] = rx(-p[f"toe{S}"])
    for c, s in CHAINS:
        for i in (1, 2, 3):
            W[f"Cape_{c}{i}"] = (rz(-p[f"cape{c}{i}_fan"]) @ rx(p[f"cape{c}{i}_back"])
                                 @ ry(p["cape_sway"] - s * p[f"cape{c}{i}_flare"]))
    pail_w = ry(p["pail_y"]) @ rx(-p["pail_x"])

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
            elif name == "Pail":
                D[name] = _lerp_rot(A[parent].transposed() @ pail_w, p["pail_world"]) if p["pail_world"] > 0 \
                    else pail_w
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
    return D, offset


# --- forward kinematics and a small arm reach (used by the wall and ledge clips) ------------------------------

RIG = None  # the solver rig; pip_export.py sets it before keying, so clips can measure their own poses


def fk(p, rig=None):
    """Pose dict -> ({bone: world 3x3}, {bone: head position}) in character space, hips offset included."""
    rig = rig or RIG
    D, offset = solve(p, rig)
    A, pos = {}, {}
    for name, (parent, head_at) in ((k, v) for k, v in rig.items() if not k.startswith("_")):
        if parent is None:
            A[name] = D.get(name, Matrix.Identity(3))
            pos[name] = head_at.copy()
            continue
        A[name] = A[parent] @ D.get(name, Matrix.Identity(3))
        pos[name] = pos[parent] + A[parent] @ (head_at - rig[parent][1])
        if name == "Hips":
            pos[name] += offset
    return A, pos


def bone_point(p, bone, rest_point=None, rig=None):
    """Where a rest-space point carried by `bone` ends up (default: the bone's head)."""
    rig = rig or RIG
    A, pos = fk(p, rig)
    if rest_point is None:
        return pos[bone]
    return pos[bone] + A[bone] @ (Vector(rest_point) - rig[bone][1])


def knuckles(S, rig=None):
    """Rest position of the hand's knuckle line: the point a grip or a push is measured at."""
    return (rig or RIG)["Fingers_" + S][1]


def reach(p, S, target, weight=1.0, rig=None):
    """Turns arm{S}_fwd / _out and elbow{S} so the knuckles of hand S land on `target` (character space), starting
    from the pose's own arm (which picks the elbow's side); out of reach, the arm points at it as far as it goes.
    `weight` blends from the authored arm (0) to the solved one (1). Without a rig (pure pose maths) it is a no-op."""
    rig = rig or RIG
    if rig is None or weight <= 0.0:
        return p
    keys = (f"arm{S}_fwd", f"arm{S}_out", f"elbow{S}")
    lo, hi = (-80.0, -30.0, 4.0), (215.0, 120.0, 140.0)
    target = Vector(target)
    q = dict(p)
    grip = knuckles(S, rig)

    def miss(pose_):
        return bone_point(pose_, "Hand_" + S, grip, rig) - target

    lam = 1e-4
    e = miss(q)
    for _ in range(40):
        if e.length < 2e-4:
            break
        J = []
        for k in keys:
            d = dict(q)
            d[k] += 0.5
            J.append((miss(d) - e) / 0.5)
        Jm = Matrix((J[0], J[1], J[2])).transposed()  # columns = d(miss)/d(param)
        H = Jm.transposed() @ Jm
        g = Jm.transposed() @ e
        for i in range(3):
            H[i][i] += lam
        try:
            step = H.inverted() @ g
        except ValueError:
            lam *= 10
            continue
        trial = dict(q)
        for i, k in enumerate(keys):
            trial[k] = min(hi[i], max(lo[i], q[k] - max(-25.0, min(25.0, step[i]))))
        e2 = miss(trial)
        if e2.length < e.length:
            q, e, lam = trial, e2, max(1e-6, lam * 0.5)
        else:
            lam *= 6
            if lam > 1e4:
                break
    for k in keys:
        p[k] = p[k] + (q[k] - p[k]) * weight
    return p


# --- clips ------------------------------------------------------------------------------------------------

def _stand(**kw):
    """Relaxed standing base: soft knees, arms a touch forward, left fingers loose."""
    p = pose(ground=1.0, feet_world=1.0, kneeL=4, kneeR=4, legL_fwd=2, legR_fwd=2, elbowL=8, elbowR=6,
             fingersL=10)
    p.update(kw)
    return p


def idle(u):
    a = TAU * u
    breathe = math.sin(a)
    p = _stand()
    p.update(hx=0.008 * math.sin(a), hip_roll=-1.2 * math.sin(a),
             kneeL=5 + 3 * (0.5 - 0.5 * math.cos(a)), kneeR=5 + 3 * (0.5 - 0.5 * math.cos(a)),
             legL_fwd=2.5 + 1.5 * (0.5 - 0.5 * math.cos(a)), legR_fwd=2.5 + 1.5 * (0.5 - 0.5 * math.cos(a)),
             spine_pitch=1.0 + 1.2 * breathe, chest_pitch=-1.5 * breathe, spine_roll=1.2 * math.sin(a),
             shoulderL=2.5 * (0.5 + 0.5 * breathe), shoulderR=2.5 * (0.5 + 0.5 * breathe),
             armL_out=2 + 2 * breathe, armR_out=1 + 1.5 * breathe, armL_fwd=2 * math.sin(a - 0.6),
             elbowL=10 + 3 * math.sin(a - 0.9), handL_pitch=4 * math.sin(a - 1.2),
             fingersL=14 + 6 * math.sin(a - 1.5),
             head_yaw=5 * math.sin(a - 0.4), head_roll=2.5 * math.sin(a + 0.7), head_pitch=-1 + 1.5 * breathe,
             hair_side=1.5 * math.sin(a - 1.2), hair_back=1.0 * math.sin(a - 1.0),
             pail_x=3 * math.sin(a - 0.8), pail_y=2 * math.sin(2 * a))
    cape(p, [3 + 2.5 * math.sin(a - 0.5 * i - 0.6) for i in range(3)],
         flare=[1.5 + 1.5 * math.sin(a - 0.5 * i) for i in range(3)], sway=2.5 * math.sin(a - 1.0))
    return p


def _locomotion(u, run):
    """Walk (run=0) or run (run=1) cycle; the left foot strikes at u = 0."""
    a = TAU * u
    k = float(run)
    p = pose(ground=1.0, feet_world=1.0, head_world=1.0)
    swing = 27 + 15 * k
    for S, s, ph in (("L", 1.0, a), ("R", -1.0, a + math.pi)):
        stance = max(0.0, math.sin(ph))  # 0..pi: foot on the ground travelling back
        lift = max(0.0, -math.sin(ph))  # pi..2pi: recovering forward
        p[f"leg{S}_fwd"] = swing * math.cos(ph) + (6 + 16 * k) * lift + 4 * k
        p[f"knee{S}"] = 7 + 14 * k + (10 + 12 * k) * bump(ph, 0.5, 0.5) + (52 + 50 * k) * lift ** 1.3
        p[f"foot{S}"] = (-22 * bump(ph, -0.1, 0.45) + (42 + 14 * k) * bump(ph, math.pi * 1.08, 0.6)
                         + 14 * k * lift)
        p[f"toe{S}"] = (26 + 6 * k) * bump(ph, math.pi * 0.95, 0.45)
        p[f"leg{S}_out"] = 1.0
    bounce = math.cos(2 * a - 0.9)
    p.update(hz=(0.012 + 0.03 * k) * (0.5 + 0.5 * math.cos(2 * a - 2.2 - 0.5 * k)) + 0.045 * k * bump(2 * a, 4.6, 0.9),
             hip_pitch=5 + 9 * k, hip_yaw=-(7 + 3 * k) * math.cos(a), hip_roll=(3 + 1.5 * k) * math.sin(a),
             hx=0.008 * math.sin(a),
             spine_pitch=2 + 4 * k + 1.5 * bounce, spine_yaw=(6 + 4 * k) * math.cos(a), spine_roll=-2 * math.sin(a),
             chest_yaw=(7 + 6 * k) * math.cos(a), chest_pitch=1 + 3 * k,
             head_pitch=1 + 2 * k + 1.5 * bounce, head_yaw=-2 * math.cos(a), head_roll=1.5 * math.sin(a),
             # free arm pumps opposite its leg; the pail arm stays quieter so the candy survives
             armL_fwd=-(26 + 24 * k) * math.cos(a) + 14 * k, armL_out=7 + 9 * k, armL_twist=12 * k,
             elbowL=22 + 58 * k + (10 + 6 * k) * math.cos(a - 0.5), handL_pitch=6 * math.sin(a), fingersL=18 + 45 * k,
             shoulderL=3 * k + 3 * math.cos(a), shoulderR=3 * k - 3 * math.cos(a),
             armR_fwd=(14 + 10 * k) * math.cos(a) + 12 * k, armR_out=9 + 9 * k, elbowR=16 + 30 * k - 6 * math.cos(a - 0.5),
             pail_x=(8 + 10 * k) * math.cos(a - 1.1) + 6 * k, pail_y=-(3 + 4 * k) + 4 * math.sin(2 * a),
             hair_back=2 + 9 * k + (2.5 + 2 * k) * math.cos(2 * a - 2.0), hair_side=2 * math.sin(a - 0.8),
             hair_flare=1 + 2 * k)
    base = 15 + 37 * k
    wave = 5 + 4 * k
    cape(p, [base * (0.75 + 0.2 * i) + wave * math.sin(2 * a - 0.9 * i - 0.4) for i in range(3)],
         flare=[(5 + 7 * k) * (1 + 0.3 * i) + 2.5 * math.sin(2 * a - 0.9 * i) for i in range(3)],
         fan=[(4 + 8 * k) * (1 + 0.5 * i) for i in range(3)], sway=(3 + 2 * k) * math.sin(a - 0.7))
    return p


def walk(u):
    return _locomotion(u, 0.0)


def run(u):
    return _locomotion(u, 1.0)


def _air_rise(a=0.0):
    """Rising pose: one knee driving up, free arm punching up, cape trailing down and back."""
    p = pose(head_world=0.0, hip_pitch=6, spine_pitch=-4, chest_pitch=-5, head_pitch=-8,
             legL_fwd=62, kneeL=88, footL=20, legR_fwd=-14, kneeR=24, footR=48, toeR=-8,
             armL_fwd=150, armL_out=18, elbowL=22, fingersL=70, shoulderL=14,
             armR_fwd=-18, armR_out=26, elbowR=24, pail_x=-8, pail_y=-4,
             hair_back=-4, hair_flare=3)
    cape(p, [22, 30, 38], flare=[6, 10, 14], fan=[3, 6, 9])
    return p


def jump_start(u):
    crouch = _stand(ground=1.0, hip_pitch=20, spine_pitch=10, chest_pitch=6, head_pitch=-14,
                    legL_fwd=52, legR_fwd=48, kneeL=78, kneeR=72, footL=6, footR=6,
                    armL_fwd=-42, armL_out=16, elbowL=36, armR_fwd=-26, armR_out=18, elbowR=22,
                    fingersL=40, hair_back=5, pail_x=10)
    cape(crouch, [34, 22, 10], flare=[5, 8, 11])
    stretch = pose(ground=0.0, hz=0.02, hip_pitch=-3, spine_pitch=-6, chest_pitch=-8, head_pitch=-10,
                   legL_fwd=8, kneeL=10, footL=58, toeL=-10, legR_fwd=-8, kneeR=6, footR=62, toeR=-10,
                   armL_fwd=165, armL_out=10, elbowL=6, fingersL=20, shoulderL=18, shoulderR=6,
                   armR_fwd=10, armR_out=30, elbowR=10, pail_x=-14, hair_back=-8, hair_flare=2)
    cape(stretch, [2, -4, -10], flare=[3, 5, 8])
    return keyed([(0.0, crouch), (0.5, stretch), (1.0, _air_rise())], u)


def jump_air(u):
    a = TAU * u
    p = _air_rise()
    p.update(legL_fwd=62 + 4 * math.sin(a), kneeL=88 + 5 * math.sin(a + 0.6), legR_fwd=-14 - 4 * math.sin(a),
             kneeR=24 + 5 * math.sin(a + 1.0), armL_fwd=150 + 5 * math.sin(a + 0.4), elbowL=22 + 6 * math.sin(a),
             armR_out=26 + 4 * math.sin(a), spine_pitch=-4 + 1.5 * math.sin(a), head_pitch=-8 + 2 * math.sin(a + 0.5),
             pail_x=-8 + 5 * math.sin(a), hair_back=-4 + 2 * math.sin(2 * a))
    cape(p, [22 + 6 * math.sin(2 * a - 0.8 * i) + 8 * i for i in range(3)],
         flare=[6 + 4 * i + 3 * math.sin(2 * a - 0.8 * i + 1) for i in range(3)], fan=[3, 6, 9],
         sway=2 * math.sin(a))
    return p


def fall(u):
    a = TAU * u
    p = pose(hip_pitch=-4, spine_pitch=2, chest_pitch=4, head_pitch=14,
             legL_fwd=26 + 5 * math.sin(a), kneeL=42 + 8 * math.sin(a + 0.9), legL_out=12, footL=24,
             legR_fwd=4 - 5 * math.sin(a), kneeR=30 + 8 * math.sin(a + 2.2), legR_out=10, footR=30,
             armL_fwd=28 + 8 * math.sin(2 * a), armL_out=92 + 10 * math.sin(2 * a + 0.5), elbowL=34 + 10 * math.sin(2 * a + 1),
             fingersL=-6, shoulderL=14, shoulderR=10,
             armR_fwd=16 - 6 * math.sin(2 * a), armR_out=58 + 8 * math.sin(2 * a + 2.0), elbowR=22,
             pail_x=-6 + 6 * math.sin(a), pail_y=-8 + 4 * math.sin(2 * a),
             hair_back=-10 + 3 * math.sin(2 * a), hair_flare=14 + 4 * math.sin(2 * a + 1), hair_side=2 * math.sin(a))
    cape(p, [112 + 20 * i + 10 * math.sin(2 * a - 1.0 * i) for i in range(3)],
         side_back=[100 + 24 * i + 12 * math.sin(2 * a - 1.0 * i + 0.9) for i in range(3)],
         flare=[14 + 8 * i + 6 * math.sin(2 * a - 1.0 * i + 1.3) for i in range(3)], fan=[10, 18, 26],
         sway=4 * math.sin(a))
    return p


def land(u):
    hit = _stand(hip_pitch=30, spine_pitch=14, chest_pitch=8, head_pitch=-18,
                 legL_fwd=74, legR_fwd=70, kneeL=104, kneeR=98, legL_out=10, legR_out=10,
                 armL_fwd=6, armL_out=58, elbowL=28, armR_fwd=4, armR_out=40, elbowR=20, fingersL=-8,
                 hair_back=10, hair_flare=8, pail_x=12)
    cape(hit, [52, 30, 8], flare=[12, 20, 28])
    first = _stand(hip_pitch=14, spine_pitch=6, chest_pitch=2, head_pitch=-6,
                   legL_fwd=40, legR_fwd=38, kneeL=56, kneeR=52, legL_out=6, legR_out=6,
                   armL_fwd=10, armL_out=40, elbowL=20, armR_fwd=6, armR_out=30, elbowR=14, hair_back=-6)
    cape(first, [70, 84, 96], flare=[8, 12, 16])
    up = _stand(hip_pitch=-2, spine_pitch=-2, chest_pitch=-3, head_pitch=3, kneeL=2, kneeR=2, legL_fwd=1, legR_fwd=1,
                armL_out=10, armR_out=8, shoulderL=6, shoulderR=6, hz=0.0, hair_back=-4)
    cape(up, [8, 12, 14], flare=[4, 6, 8])
    rest = idle(0.0)
    return keyed([(0.0, first), (0.22, hit), (0.62, up), (1.0, rest)], u)


def glide(u):
    a = TAU * u
    flap = math.sin(a)
    p = pose(hz=0.02 * flap, hip_pitch=58 + 2 * flap, spine_pitch=-10, chest_pitch=-12, neck_pitch=-16,
             head_pitch=-26 + 2 * math.sin(a + 0.6), hip_roll=2.5 * math.sin(a + 1.2),
             legL_fwd=-14 + 3 * math.sin(a + 0.4), kneeL=16 + 5 * math.sin(a + 1.0), legL_out=9, footL=58,
             legR_fwd=-6 - 3 * math.sin(a + 0.4), kneeR=26 + 5 * math.sin(a + 2.4), legR_out=8, footR=60,
             shoulderL=10, shoulderR=10,
             armL_fwd=-8 + 4 * flap, armL_out=72 + 5 * flap, elbowL=12, armL_twist=-10, fingersL=-10, handL_side=-6,
             armR_fwd=-8 + 4 * flap, armR_out=68 + 5 * flap, elbowR=12, armR_twist=-10,
             pail_x=14 + 5 * math.sin(a - 1.0), pail_y=-4,
             hair_back=-16 + 3 * math.sin(2 * a), hair_flare=6)
    # the cape lies over the back like a bat wing: the side chains fan out to the wrists and the hem ripples
    cape(p, [70 + 3 * i + 5 * math.sin(2 * a - 1.1 * i) for i in range(3)],
         side_back=[74 + 2 * i + 6 * math.sin(2 * a - 1.1 * i + 0.7) for i in range(3)],
         fan=[50 + 3 * flap, 60 + 3 * flap, 66 + 4 * math.sin(a - 0.6)], flare=[4, 2, 0])
    return p


def wall_slide(u):
    """Braced against a wall straight ahead: left hand and both soles on it, looking down over the pail arm."""
    a = TAU * u
    p = pose(hip_pitch=-12, hy=0.04, hz=0.004 * math.sin(2 * a), spine_pitch=6, chest_pitch=6, chest_yaw=-14,
             head_pitch=18, head_yaw=-26, head_roll=-4,
             legL_fwd=78 + 2 * math.sin(a), kneeL=96, footL=-34, legL_out=8,
             legR_fwd=52 - 2 * math.sin(a), kneeR=92, footR=-26, legR_out=6,
             armL_fwd=118 + 2 * math.sin(2 * a), armL_out=14, elbowL=42, handL_pitch=-58, fingersL=-12,
             shoulderL=12,
             armR_fwd=-16, armR_out=34 + 3 * math.sin(a), elbowR=16, pail_x=-4 + 4 * math.sin(a), pail_y=-6,
             hair_back=-12 + 2 * math.sin(2 * a), hair_flare=10 + 3 * math.sin(2 * a + 1))
    cape(p, [112 + 22 * i + 9 * math.sin(2 * a - 1.0 * i) for i in range(3)],
         side_back=[102 + 24 * i + 10 * math.sin(2 * a - 1.0 * i + 0.9) for i in range(3)],
         flare=[10 + 7 * i + 5 * math.sin(2 * a - 1.0 * i + 1) for i in range(3)], fan=[8, 14, 20],
         sway=3 * math.sin(a))
    return p


# The wall and ledge clips are authored against the game's own numbers (ClimbRules, PipMovementTuning): while
# Pip hangs, the ledge's top edge is LEDGE_OUT ahead of her root and LEDGE_UP above it; a kicked wall starts
# WALL_BEHIND behind her root. The root itself is moved by the game, so every clip stays in place.
LEDGE_OUT, LEDGE_UP = 0.34, 1.27
CLIMB_RISE_FRACTION, CLIMB_LIFT, CLIMB_INSET = 0.6, 0.06, 0.4
WALL_BEHIND = 0.31
GRIP_X = 0.11  # the gripping (left) hand sits this far to Pip's left of her centre line


def level_hand(p, S, rig=None):
    """Sets hand{S}_side so the hand lies flat (knuckles ahead of the wrist), whatever the arm is doing."""
    rig = rig or RIG
    if rig is None:
        return p
    axis = rig["Fingers_" + S][1] - rig["Hand_" + S][1]
    key = f"hand{S}_side"
    best = None
    q = dict(p)
    for side in range(-130, 131, 5):
        q[key] = side
        A, _pos = fk(q, rig)
        d = (A["Hand_" + S] @ axis).normalized()
        if d.y < 0 and (best is None or abs(d.z) < best[0]):
            best = (abs(d.z), side, d.z)
    if best is None:
        return p
    side = best[1]
    for _ in range(4):  # refine by secant steps around the best sample
        q[key] = side
        A, _pos = fk(q, rig)
        z0 = (A["Hand_" + S] @ axis).normalized().z
        q[key] = side + 1.0
        A, _pos = fk(q, rig)
        z1 = (A["Hand_" + S] @ axis).normalized().z
        if abs(z1 - z0) < 1e-6:
            break
        side -= z0 / (z1 - z0)
    p[key] = side
    return p


def _grip(p, edge_y=-LEDGE_OUT, edge_z=LEDGE_UP, x=GRIP_X, weight=1.0):
    """Lays the left hand flat on a ledge top whose edge is at (edge_y, edge_z): the knuckles rest just past the
    edge. `weight` < 1 lets the authored arm take over (the hand leaving the ledge)."""
    if weight <= 0.0:
        return p
    target = Vector((x, edge_y - 0.05, edge_z + 0.022))
    q = dict(p)
    for _ in range(3):  # the wrist angle moves the knuckles a little, so settle the two together
        reach(q, "L", target)
        level_hand(q, "L")
    for k in ("armL_fwd", "armL_out", "elbowL", "handL_side"):
        p[k] += (q[k] - p[k]) * weight
    return p


def _hang(a):
    """Hanging by the left hand, which lies forward on the edge; the right arm keeps the pail out in the open,
    away from the face under the edge. `a` is the sway phase."""
    sw, sw2 = math.sin(a), math.sin(a - 0.9)
    kick = math.sin(a - 0.5)
    p = pose(hx=0.035 + 0.018 * math.cos(a), hy=-0.165 + 0.03 * sw, hz=0.075 + 0.008 * math.cos(2 * a),
             hip_pitch=-6 + 6 * sw2, hip_roll=-5 + 3.0 * math.cos(a - 0.5), hip_yaw=-10 + 5 * sw2,
             spine_pitch=3, spine_roll=-4, spine_yaw=-6, chest_pitch=4, chest_roll=-6, chest_yaw=-8,
             neck_pitch=-4, head_pitch=-8 + 4 * sw2, head_roll=10 + 3 * sw, head_yaw=14 + 8 * math.sin(a - 1.6),
             shoulderL=32, armL_fwd=150, armL_out=4, armL_twist=90, elbowL=18,
             handL_side=-62, handL_pitch=0, fingersL=30, thumbL=12,
             shoulderR=-2, armR_fwd=-10 + 7 * sw2, armR_out=38 + 5 * math.cos(a), elbowR=26 + 6 * sw,
             pail_x=7 * math.sin(a - 1.3), pail_y=-4 + 4 * math.cos(a - 0.8),
             # small alternating kicks, toes pointed
             legL_fwd=4 + 15 * kick, kneeL=16 + 26 * (0.5 - 0.5 * math.sin(a - 1.4)), footL=40, legL_out=3,
             legR_fwd=-6 - 15 * kick, kneeR=16 + 26 * (0.5 + 0.5 * math.sin(a - 1.4)), footR=44, legR_out=5,
             hair_back=3 * sw2, hair_side=-3 + 2 * sw)
    cape(p, [5 + 6 * math.sin(a - 0.6 * i - 1.2) for i in range(3)], flare=[2, 3, 4],
         sway=-2 + 4 * math.sin(a - 0.7))
    return p


def ledge_hang(u):
    """Looping hang: left hand forward on the edge, a gentle swing about it, small leg kicks, pail held clear."""
    return _grip(_hang(TAU * u))


def ledge_grab(u):
    """Catch: both arms reach up and forward, the left hand lands on the edge, the body swings in under it and
    settles into LedgeHang frame 0 while the pail arm drops out to the side."""
    hang = _hang(0.0)
    come = dict(hang)
    come.update(hx=0.0, hy=-0.02, hz=-0.02, hip_pitch=10, hip_yaw=0, hip_roll=0, spine_pitch=-4, spine_roll=0,
                spine_yaw=0, chest_pitch=-6, chest_roll=0, chest_yaw=0, head_pitch=-24, head_roll=0, head_yaw=0,
                shoulderL=26, shoulderR=14, armR_fwd=14, armR_out=80, elbowR=30, pail_x=20, pail_y=-12,
                fingersL=-6,
                legL_fwd=-6, kneeL=34, legR_fwd=-30, kneeR=52, footL=30, footR=40,
                hair_back=-8, hair_flare=6)
    cape(come, [58, 72, 86], flare=[10, 16, 22], fan=[5, 9, 13])
    swing = dict(hang)
    swing.update(hx=0.03, hy=-0.235, hz=0.04, hip_pitch=-13, hip_yaw=-6, spine_pitch=4, chest_pitch=6,
                 head_pitch=-4, shoulderL=38, shoulderR=6, armR_fwd=-12, armR_out=62, elbowR=38, pail_x=-14, pail_y=10,
                 legL_fwd=-14, kneeL=40, legR_fwd=-20, kneeR=30, hair_back=8)
    cape(swing, [30, 22, 14], flare=[6, 9, 12], fan=[3, 5, 7])
    back = dict(hang)
    back.update(hy=-0.135, hz=0.085, hip_pitch=3, armR_fwd=-26, armR_out=42, pail_x=12,
                legL_fwd=12, kneeL=12, legR_fwd=6, kneeR=16, hair_back=-4)
    cape(back, [-2, 2, 6], flare=[3, 4, 6])
    p = keyed([(0.0, come), (0.42, swing), (0.74, back), (1.0, hang)], u)
    return _grip(p)


def ledge_climb(u):
    """Pull up and over, timed to the game's climb: the root rises for the first 60 % (the edge sinks from the
    hands to just under the feet), then travels in over the edge. Left hand pinned to the edge while it pulls and
    presses, pail lifted over first, right knee onto the edge, then a spring up into Idle frame 0."""
    rise = smooth(u / CLIMB_RISE_FRACTION)
    over = smooth((u - CLIMB_RISE_FRACTION) / (1 - CLIMB_RISE_FRACTION))
    edge_y = -LEDGE_OUT + over * (LEDGE_OUT + CLIMB_INSET)
    edge_z = LEDGE_UP - rise * (LEDGE_UP + CLIMB_LIFT)
    hang = _hang(0.0)
    level = dict(hx=0.0, hip_yaw=0, hip_roll=0, spine_roll=0, spine_yaw=0, chest_yaw=0, chest_roll=0, head_yaw=0,
                 head_roll=0, neck_pitch=0)
    pull = dict(hang)  # chin over the edge: elbow bending, knees gathering, pail swinging up
    pull.update(level)
    pull.update(hy=-0.13, hz=0.04, hip_pitch=-2, spine_pitch=2, chest_pitch=2, head_pitch=-18, shoulderL=20,
                elbowL=80, armL_out=30, shoulderR=14, armR_fwd=-46, armR_out=36, elbowR=108, pail_x=-4,
                legL_fwd=10, kneeL=40, legR_fwd=12, kneeR=84, footL=44, footR=30)
    cape(pull, [14, 18, 22], flare=[4, 6, 8])
    press = dict(hang)  # mantle: the left arm presses down, the right knee comes up to the edge
    press.update(level)
    press.update(hy=-0.17, hz=-0.32, hip_pitch=24, spine_pitch=8, chest_pitch=4, head_pitch=-26,
                 shoulderL=14, armL_fwd=40, armL_out=18, elbowL=50, fingersL=4,
                 shoulderR=12, armR_fwd=102, armR_out=24, elbowR=62, pail_x=-18,
                 legL_fwd=26, kneeL=60, footL=46, legR_fwd=120, kneeR=138, footR=50, legR_out=8)
    cape(press, [30, 38, 46], flare=[8, 12, 16], fan=[3, 6, 9])
    kneel = dict(press)  # right knee on the edge, left leg tucked under, pail already over the top
    kneel.update(hy=-0.24, hz=-0.36, hip_pitch=30, spine_pitch=10, head_pitch=-30, armL_fwd=20, elbowL=24,
                 armL_out=14, armR_fwd=70, elbowR=62, armR_out=20, pail_x=-6,
                 legL_fwd=44, kneeL=84, footL=36, legR_fwd=70, kneeR=100, footR=62)
    cape(kneel, [38, 42, 44], flare=[8, 12, 16], fan=[3, 6, 9])
    spring = _stand(ground=1.0, hy=-0.06, hip_pitch=22, spine_pitch=8, chest_pitch=4, head_pitch=-14,
                    legR_fwd=70, kneeR=96, legL_fwd=34, kneeL=84, footL=30, footR=0,
                    armL_fwd=-34, armL_out=22, elbowL=36, fingersL=30, armR_fwd=36, armR_out=24, elbowR=40,
                    pail_x=14, hair_back=6)
    cape(spring, [44, 34, 22], flare=[8, 12, 16])
    up = _stand(hip_pitch=-2, spine_pitch=-3, chest_pitch=-3, head_pitch=3, kneeL=2, kneeR=2, legL_fwd=1, legR_fwd=1,
                armL_out=12, armR_out=10, shoulderL=6, shoulderR=6, hair_back=-4, pail_x=-8)
    cape(up, [6, 12, 16], flare=[4, 6, 8])
    p = keyed([(0.0, hang), (0.24, pull), (0.47, press), (0.62, kneel), (0.8, spring), (0.92, up), (1.0, idle(0.0))], u)
    pin = 1.0 - smooth((u - 0.47) / 0.13)
    return _grip(p, edge_y, edge_z, GRIP_X + 0.04 * smooth(u / 0.4), pin)


def wall_kick(u):
    """Kick off a wall behind her: coiled with both soles and the left palm on the wall, the body uncoils along
    the launch, the cape whips out, and it ends in the JumpAir pose."""
    coil = pose(hy=0.13, hz=-0.08, hip_pitch=16, spine_pitch=8, chest_pitch=4, chest_yaw=14, spine_yaw=6,
                head_pitch=-20, head_yaw=-14, feet_world=1.0,
                legL_fwd=50, kneeL=122, footL=80, legL_out=6, legR_fwd=40, kneeR=92, footR=80, legR_out=6,
                shoulderL=-4, armL_fwd=-35, armL_out=24, armL_twist=90, elbowL=35, handL_side=45, fingersL=-6,
                armR_fwd=52, armR_out=24, elbowR=52, pail_x=-10, pail_y=-4, hair_back=6, hair_flare=4)
    cape(coil, [8, 5, 2], flare=[4, 8, 12], fan=[4, 8, 12])
    reach(coil, "L", Vector((0.22, WALL_BEHIND - 0.02, 0.66)))
    # one frame later the root is 0.15 m further out and 0.25 m higher: the legs drive down and back
    push = pose(hy=0.05, hz=0.0, hip_pitch=24, spine_pitch=0, chest_pitch=-4, chest_yaw=6, head_pitch=-22,
                feet_world=1.0, legL_fwd=-14, kneeL=8, footL=62, legL_out=5, legR_fwd=2, kneeR=44, footR=70,
                legR_out=5, shoulderL=6, armL_fwd=20, armL_out=26, elbowL=50, fingersL=20,
                armR_fwd=20, armR_out=34, elbowR=40, pail_x=8, pail_y=-4, hair_back=10, hair_flare=6)
    cape(push, [40, 44, 46], flare=[8, 14, 20], fan=[6, 10, 14])
    burst = pose(hy=0.0, hz=0.03, hip_pitch=14, spine_pitch=-8, chest_pitch=-10, head_pitch=-12, feet_world=1.0,
                 legL_fwd=-34, kneeL=6, footL=70, toeL=-10, legR_fwd=-16, kneeR=22, footR=66, toeR=-10,
                 legL_out=5, legR_out=5,
                 shoulderL=16, armL_fwd=156, armL_out=14, elbowL=8, fingersL=40,
                 armR_fwd=-34, armR_out=40, elbowR=14, pail_x=26, pail_y=-6, hair_back=14, hair_flare=8)
    cape(burst, [84, 100, 114], flare=[14, 22, 30], fan=[8, 14, 20])
    fly = _air_rise()
    fly.update(hip_pitch=10, legL_fwd=44, kneeL=70, legR_fwd=-22, kneeR=14, armR_fwd=-26, pail_x=10, hair_back=6)
    cape(fly, [56, 70, 82], flare=[10, 16, 22], fan=[5, 9, 13])
    return keyed([(0.0, coil), (0.1, push), (0.34, burst), (0.68, fly), (1.0, _air_rise())], u)


def roll(u):
    """Forward somersault: dive from a crouch, tuck, spin once about the hips, pop back up into a crouch."""
    start = _stand(ground=1.0, hip_pitch=34, spine_pitch=14, chest_pitch=10, head_pitch=-10,
                   legL_fwd=58, kneeL=70, legR_fwd=30, kneeR=52, footR=20,
                   armL_fwd=70, armL_out=10, elbowL=30, armR_fwd=20, armR_out=16, elbowR=30, pail_world=1.0)
    cape(start, [52, 50, 48], flare=[5, 8, 10])
    tuck = pose(ground=0.0, hz=-0.3, spine_pitch=34, chest_pitch=30, neck_pitch=14, head_pitch=24,
                legL_fwd=118, kneeL=128, footL=-10, legR_fwd=112, kneeR=124, footR=-10, legL_out=6, legR_out=6,
                armL_fwd=62, armL_out=14, elbowL=98, armL_twist=30, fingersL=60,
                armR_fwd=40, armR_out=20, elbowR=70, armR_twist=10, pail_world=0.0, hair_flare=4)
    end = _stand(ground=1.0, hip_pitch=24, spine_pitch=10, chest_pitch=6, head_pitch=-12,
                 legL_fwd=50, kneeL=66, legR_fwd=46, kneeR=62,
                 armL_fwd=20, armL_out=30, elbowL=30, armR_fwd=10, armR_out=24, elbowR=24, pail_world=1.0)
    cape(end, [44, 36, 28], flare=[5, 8, 10])
    tuck_a, tuck_b = dict(tuck), dict(tuck)
    tuck_a["hz"], tuck_b["hz"] = -0.27, -0.4  # belly-down, then rolling over the back
    p = keyed([(0.0, start), (0.2, tuck_a), (0.5, tuck), (0.8, tuck_b), (1.0, end)], u)
    spin = 360.0 * smooth((u - 0.06) / 0.86)
    p["hip_pitch"] += spin
    air = smooth(u / 0.16) * (1 - smooth((u - 0.86) / 0.14))
    p["ground"] = 1.0 - air
    # the cape wraps around the ball: it follows the spin, lagging a little
    lag = 30 * math.sin(math.pi * u)
    for c, _ in CHAINS:
        for i in (1, 2, 3):
            p[f"cape{c}{i}_back"] += spin - lag * i * 0.6
    return p


def interact(u):
    """Reach forward with the free (left) hand at chest height, a little press, and back."""
    rest = idle(0.0)
    reach = _stand(hip_pitch=6, spine_pitch=6, chest_pitch=6, chest_yaw=-14, spine_yaw=-6, head_pitch=6,
                   head_yaw=4, legL_fwd=10, kneeL=14, legR_fwd=-4, kneeR=6, hy=-0.02,
                   shoulderL=6, armL_fwd=84, armL_out=4, elbowL=16, handL_pitch=-24, fingersL=-4,
                   armR_fwd=-12, armR_out=12, elbowR=14, pail_x=8, hair_back=4)
    cape(reach, [22, 20, 18], flare=[3, 5, 7])
    wind = _stand(hip_pitch=2, spine_pitch=1, chest_yaw=8, head_pitch=2, armL_fwd=30, armL_out=12, elbowL=86,
                  handL_pitch=10, fingersL=30, armR_fwd=-4, armR_out=8, elbowR=10)
    cape(wind, [4, 5, 6], flare=[2, 3, 4])
    press = dict(reach)
    press.update(armL_fwd=88, elbowL=6, hip_pitch=8, handL_pitch=-34)
    return keyed([(0.0, rest), (0.2, wind), (0.42, reach), (0.55, press), (0.68, reach), (1.0, rest)], u)


# name, pose function, frames at 30 fps, loops, intended ground speed (m/s), note
CLIPS = [
    ("Idle", idle, 75, True, 0.0, "breathing, weight shift, cape sway"),
    ("Walk", walk, 20, True, 1.6, "left foot strikes at frame 0"),
    ("Run", run, 12, True, 4.5, "left foot strikes at frame 0"),
    ("JumpStart", jump_start, 8, False, None, "launch: crouch -> stretch -> JumpAir pose"),
    ("JumpAir", jump_air, 20, True, None, "rising"),
    ("Fall", fall, 20, True, None, "descending, cape up"),
    ("Land", land, 10, False, 0.0, "squash on frame 2, ends on Idle frame 0"),
    ("Glide", glide, 30, True, 3.5, "bat-cape glide, body pitched forward"),
    ("WallSlide", wall_slide, 20, True, 0.0, "wall straight ahead; slides down at about 1.5 m/s"),
    ("LedgeHang", ledge_hang, 48, True, 0.0, "left hand forward on the edge 0.34 m ahead, 1.27 m up; pail arm free"),
    ("Roll", roll, 16, False, 5.0, "forward somersault, one turn"),
    ("Interact", interact, 30, False, 0.0, "left-hand reach, contact at frames 13-17"),
    ("WallKick", wall_kick, 10, False, None, "wall 0.31 m BEHIND her at frame 0; ends on the JumpAir pose"),
    ("LedgeGrab", ledge_grab, 6, False, 0.0, "catch; ends on LedgeHang frame 0"),
    ("LedgeClimb", ledge_climb, 12, False, 0.0, "pull up and over in 0.4 s (60 % rise, then in); ends on Idle frame 0"),
]
