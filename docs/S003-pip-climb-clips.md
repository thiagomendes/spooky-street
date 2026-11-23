# S003: Pip's wall kick, ledge catch, hang and climb clips

Branch `feature/pip-climb-clips`. The wall and ledge moves merged in `docs/S003-wall-ledge.md` borrowed other
clips: the kick replayed `JumpStart`, the climb played `JumpStart` then `Land`, and the hang was an overhead-arm
pose that the runtime leaned 10° and shifted 0.1 m to reach the edge. Pip.fbx now has a clip for each move, and
the runtime plays them. The moves themselves, their tuning and the level are unchanged.

## The clips

All four are in `Pip.fbx`, in place, 30 fps, authored in `tools/blender/export/pip_anims.py` against the game's
own numbers (`ClimbRules`, `PipMovementTuning`): the edge is 0.34 m ahead of the root and 1.27 m above it while she
hangs, and a kicked wall starts 0.31 m behind the root.

| Clip | Frames | Seconds | Plays | What happens |
|---|---|---|---|---|
| `WallKick` | 10 | 0.333 | once | Frame 0: coiled with her back to the wall, both soles and the left palm on it. Frame 1: the legs drive down and back, soles still on the wall. Frames 2–4: the body stretches along the launch, the left arm punches up and the cape whips out level. It ends on the `JumpAir` pose. |
| `LedgeGrab` | 6 | 0.2 | once | Frame 0: flying in, the left arm reaching for the edge, legs and cape trailing, the pail arm flung out to her right. Frames 2–3: the left hand is on the edge and the body swings in under it. Frames 4–6: it swings back and settles, the cape falls. It ends on `LedgeHang` frame 0. |
| `LedgeHang` (replaced) | 48 | 1.6 | loop | The left hand lies forward on the edge, knuckles 5 cm past it. The body swings gently about that hand (about 3 cm fore and aft), the legs make small alternating kicks, the head looks about, and the right arm holds the pail out in the open. |
| `LedgeClimb` | 12 | 0.4 | once | Frames 0–3: she pulls up until her chin is over the edge. Frames 4–5: the left arm presses down on the edge, the pail goes over first and the right knee comes up. Frame 7: she kneels on the edge. Frames 9–12: she springs up onto her feet. It ends on `Idle` frame 0. |

`LedgeClimb` is timed to `ClimbRules.ClimbProgress`. The game moves the root up 1.33 m over the first 60 % and
then 0.74 m forward, so in the clip the edge sinks from the hands to just under the feet and then passes behind
her. The left hand stays pinned to that moving edge until frame 5.

### How the hands are placed

`pip_anims.py` gained three small helpers. Clips call them, and `pip_export.py` hands them the rig
(`pip_anims.RIG`):

- `fk` returns where every bone is for a pose.
- `reach` turns one arm so its knuckles land on a point (damped least squares on three angles). Out of reach, the
  arm points at the target as far as it goes.
- `level_hand` bends the wrist so the hand lies flat.

The hang, the catch and the climb use them to keep the left hand on the edge while the body moves.

### One hand, and why

The brief asked for both hands on the edge and for the pail not to clip the wall. Both cannot hold on this rig.
The pail hangs from a pivot inside the right hand's fingers and is 15 cm across, so a right hand gripping an edge
puts half the pail inside any wall face under that edge. Hooking the pail further up the arm put it through the
upper arm instead.

So she hangs by her left hand and keeps the pail safe in her right, away from the wall. In the catch the right
arm swings out to the side instead of up.

## Measured on the re-imported FBX

`tools/blender/export/pip_climb_review.py` prints these for every frame (the contact is placed where the game
puts it, with the motor's root motion applied).

| Check | Result |
|---|---|
| Hang: left knuckles | 5.0 cm past the edge and 2.2 cm above the top on all 48 frames |
| Hang: pail and body against the face under the edge | Pail always clear. One toe touches the face (4 mm) on one frame. |
| Hang: feet | The lowest toe stays within 3 cm of the root's height, so the hands are about 1.27 m above the feet |
| Catch: left knuckles | On the edge from frame 2 (1 cm off) and exactly there from frame 3. Pail and body clear on every frame. |
| Kick: soles | Frame 0: left sole on the wall, right 2.5 cm into it. Frame 1: within 2 cm. Frame 2: 11 cm clear. |
| Kick: left palm | Knuckles 2 cm from the wall at frame 0 |
| Climb: left knuckles | Pinned to the edge on frames 0–5, lifting off from frame 6 |
| Climb: body against the ledge | Clear, except the right thigh and shin brush the corner by up to 2 cm on frames 4 and 5 as the knee comes over |
| Climb: pail | Clear on every frame |

`tools/blender/export/pip_fbx_diff.py` compares the new FBX with the previous one: the meshes, UVs, skin weights
and the skeleton are identical, and the eleven other clips are unchanged (every curve difference is exactly 0).
The textures and every `.meta` are untouched. `Pip.fbx.meta` still lists twelve clips;
`ModelImportRules.OnPreprocessAnimation` rebuilds the list from the takes on import, as it does for Bea's `Point`.

## Runtime wiring

- **Kick.** `PipMotor` turns Pip to face away from the wall in the step of the kick. Before, she turned at
  720°/s, so the wall was beside her for most of the clip. `PipAnimator` plays `WallKick` at full weight at once
  (nothing can blend across a half turn) and then restarts `JumpAir`, whose first frame is the clip's last.
- **Catch.** `LedgeGrabbed` plays `LedgeGrab`, fading in over 0.04 s, and then restarts `LedgeHang` at frame 0.
- **Climb.** `ClimbStarted` plays `LedgeClimb` over `LedgeClimbTime`. The pose tracker has a new `Climbing` input
  that counts the climb as ground, so no air pose shows under the clip and stepping onto the top plays no `Land`.
- **Play to the end.** These three clips play to their last frame, which is the first frame of what follows.
  Other one-shots still end one fade early. A kick that reaches the ground, another wall or a ledge ends its clip,
  and so does a catch that is let go.
- **Stand-ins.** `PipClips.Fallback` sends a missing `WallKick` or `LedgeClimb` to `JumpStart`, and the animator
  then behaves as before these clips existed.
- **The lean is gone.** `MoveFeedbackRules.HangLeanDegrees`, `HangShift` and `HangLeanRate`, and
  `PipAvatar.HangLean`, are removed. The hang clip meets the edge with no body offset.

## Tests

- **Core (dotnet, local): 520/520.** New or changed:
  - `PipClipsTests`: the three new names are one-shots, they sit at the end of `PipClips.All` (the mixer's input
    order), and their stand-ins resolve as described above.
  - `WallLedgeTests.TheClimbCountsAsGroundSoNoAirPoseOrLandingFollowsIt` (30 and 60 fps).
- **EditMode project:** `ModelImportRulesTests.PipHasTheWallAndLedgeClips` checks that the four takes import with
  the right loop flag and length and load from Resources.
- **PlayMode `WallLedgeTests`:**
  - the kick turns her away from the shed at once and plays `WallKick`;
  - the catch plays `LedgeGrab` and gives way to `LedgeHang`, with the body at no offset;
  - the climb plays `LedgeClimb` and ends in the locomotion pose with no landing.

## Evidence

Rendered from the re-imported `Pip.fbx` (Blender 4.0.2, Eevee) by `pip_climb_review.py`. Each sheet has six
frames per row: from the side, from the front three-quarter, from the side against the wall or ledge, the contact
up close, and from about where the game's camera looks.

- `docs/evidence/s003/pip_climb_clips/pip_WallKick.jpg`
- `docs/evidence/s003/pip_climb_clips/pip_LedgeGrab.jpg`
- `docs/evidence/s003/pip_climb_clips/pip_LedgeHang.jpg`
- `docs/evidence/s003/pip_climb_clips/pip_LedgeClimb.jpg`

CI: the run for this branch is recorded here once it has finished.

## Not verified

- The feel on a phone. That needs a TestFlight build after the merge.
- The kick and the climb in motion in the game. CI captures only still frames of the slide and the hang; the
  kick, the catch and the climb are checked by tests and by the Blender sheets.
