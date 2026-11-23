"""Hand placement for the testimony gestures (S003a): pure maths, shared by witness_anims.py and junekit_anims.py.

A gesture puts a hand somewhere on purpose: over the brow, at an ear, under the chin, in front of the chest
with the wrist turned up. `fit_hand` turns the arm and the wrist until the knuckles are at a point AND the hand
faces the way the gesture needs, with one damped least-squares solve over the pose angles (the same idea as the
modules' own `reach`, with the hand's orientation added to the error).

`fk` is the calling module's own forward kinematics (pose, rig) -> (deltas, offsets, absolute rotations, joint
positions); `rig` is its solver rig (bone -> (parent, rest head)).
"""
import numpy as np
from mathutils import Vector

ARM_KEYS = ("arm{S}_fwd", "arm{S}_out", "arm{S}_twist", "elbow{S}")
WRIST_KEYS = ("hand{S}_pitch", "hand{S}_side", "hand{S}_twist")


def hand_frame(rig, S):
    """The hand of side S as modelled (rest pose, character axes): (along the hand from the wrist to the
    knuckles, out of the back of the hand). The arms hang with the palms toward the thighs, so the back of a
    hand faces away from the body."""
    s = 1.0 if S == "L" else -1.0
    along = (rig["Fingers_" + S][1] - rig["Hand_" + S][1]).normalized()
    back = Vector((s, 0.0, 0.0))
    back = (back - along * back.dot(along)).normalized()
    return along, back


def hand_now(fk, rig, p, S):
    """(knuckle line, along the hand, out of the back of the hand) in the pose, character axes."""
    _D, _o, A, pos = fk(p, rig)
    along, back = hand_frame(rig, S)
    return pos["Fingers_" + S], A["Hand_" + S] @ along, A["Hand_" + S] @ back


def fit_hand(fk, rig, p, S, target, along=None, back=None, keys=None, turn=0.03, steps=40, limits=None,
             elbow=None, elbow_pull=0.15):
    """Adjust the arm and wrist angles of side S in pose `p` until the knuckles are at `target`, the hand runs
    along `along` and the back of the hand faces `back` (either may be None: free). `turn` is how much a full
    miss of a direction counts, in metres of knuckle error. `limits`: {key: (low, high)} keeps a joint in range.
    `elbow`: a point the elbow should lean toward (it picks the swivel of the arm; `elbow_pull` is how much a
    metre of its miss counts). Changes `p` in place and returns it."""
    names = [k.format(S=S) for k in (keys or ARM_KEYS + WRIST_KEYS)]
    target = Vector(target)
    along = Vector(along).normalized() if along is not None else None
    back = Vector(back).normalized() if back is not None else None
    limits = limits or {}
    elbow = Vector(elbow) if elbow is not None else None

    def error(q):
        at, a, b = hand_now(fk, rig, q, S)
        e = list(target - at)
        if elbow is not None:
            e += list((elbow - fk(q, rig)[3]["LowerArm_" + S]) * elbow_pull)
        if along is not None:
            e += list((along - a) * turn)
        if back is not None:
            e += list((back - b) * turn)
        return np.array(e)

    for _ in range(steps):
        err = error(p)
        if np.linalg.norm(err) < 2e-4:
            break
        J = np.zeros((len(err), len(names)))
        for j, k in enumerate(names):
            q = dict(p)
            q[k] += 1.0
            J[:, j] = err - error(q)
        step = np.linalg.solve(J.T @ J + 1e-6 * np.eye(len(names)), J.T @ err)
        step = np.clip(step, -15.0, 15.0)
        for j, k in enumerate(names):
            p[k] += float(step[j])
            if k in limits:
                p[k] = min(limits[k][1], max(limits[k][0], p[k]))
    return p


def miss(fk, rig, p, S, target, along=None, back=None):
    """(knuckle distance from `target` in metres, degrees off `along`, degrees off `back`) for a review print."""
    at, a, b = hand_now(fk, rig, p, S)
    out = [(Vector(target) - at).length]
    for want, have in ((along, a), (back, b)):
        out.append(None if want is None else float(np.degrees(have.angle(Vector(want).normalized()))))
    return tuple(out)
