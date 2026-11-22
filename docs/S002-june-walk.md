# S002: June's walk clip

Branch `feature/june-walk`. June has an in-place, looping `Walk` clip for her path from the attic stool down the stair
to the foyer (`docs/S002-interior-wiring.md`, Known gaps). This pass adds the clip and its review only. Playing it on
the path is a small change in `CaseCast` and `CaseStage` that is not made here (see "What the game should do").

## The clip

| | |
|---|---|
| Name | `Walk` (take `June_Rig\|Walk`), looping, 30 frames at 30 fps = 1.0 s per cycle |
| Steps | two per cycle: left heel strikes on frame 0, right heel on frame 15 |
| Designed speed | **1.1 m/s** |
| Stride | **1.1 m per cycle**, so each step is 0.55 m |
| On the ground | each foot for 56 % of the cycle (frames 0-16 left, 15-1 right); both feet for about 2 frames |
| Bounce | the hips rise and fall 24 mm, twice per cycle: lowest just after a heel strike, highest as a leg passes under her |
| Root | in place: no translation of `Root`; the game moves her |

The clip is `june_walk` in `tools/blender/export/junekit_anims.py`. The feet are not keyed by hand. Each foot has a
contact point that travels straight back at exactly the walk speed while the foot is down: the heel while the toes are
still up, then the whole sole, then the toes while the heel peels off. The legs are then fitted to those ankles with the
file's `reach` IK. Change `JUNE_WALK_SPEED` and the feet stay planted for the new speed.

- **Body:** the pelvis turns with the leading leg and the chest turns against it. The arms swing opposite the legs
  (19 degrees each way, a little late), the head stays up with a small tilt from side to side.
- **Cape:** it has bones (three chains of three: back, left and right), so it has its own secondary motion. It leans
  back from the collar, more toward the hem (6, 12 and 17 degrees), and a ripple runs down it once per step, so the hem
  is furthest back as each heel kicks up behind her. The side panels follow the upper arms, so they swing with the
  arm swing and the left panel's tear opens and closes. The sway from side to side follows the hips a beat late.
- **Braid and hat tip:** they bob once per step and swing once per cycle.

## Evidence (from the re-imported FBX)

`tools/blender/export/june_walk_review.py` imports `June.fbx` again, renders the sheets and measures the bones.

- `docs/evidence/s002/june_walk/june_walk_sheet.jpg`: eight frames across the cycle. Rows: her right side, front
  three-quarter, the legs with the cape hidden, from behind.
- `docs/evidence/s002/june_walk/june_walk_ground.jpg`: the clip with the root moved at 1.1 m/s past a fixed camera,
  cape hidden. The bars on the floor are fixed footprints (amber left, teal right). Each shoe stays on its bar from heel
  strike to toe-off.
- `docs/evidence/s002/june_walk/june_walk_contact.json`: the numbers below.

| Check | Result |
|---|---|
| Heel drift along the floor while the heel is down | 0.3 mm |
| Ball-of-foot drift while it is down | 0.05 mm |
| Sideways drift | 0.01 mm |
| Lowest sole point over the cycle | 0.01 mm above the floor (never below it) |
| Knee | 16 degrees at heel strike, 28 as the leg takes her weight, 12 at mid-stance, 35 at toe-off, 58 in the swing; the largest change is 9.6 degrees in one frame (the leg straightening before the strike) |
| Loop | frame 30 equals frame 0 exactly, for every bone |
| Mesh and older clips | identical to the previous `June.fbx`: vertices 0.0, every key of Idle, Talk, KneeTap, Wave and Point 0.0 |

The export used the cached scene of the original export (`junekit_export.py -- June --cache`), written to a scratch
folder. Only `June.fbx` was copied into the project. The three textures and all four `.meta` files are unchanged.

## What the game should do

- **Speed.** Play `CharacterClips.Walk` (it loops) while she walks, and move her root at **1.1 m/s** on the level
  parts of `CaseStage.JuneWalk`. `JuneWalkSpeed` is 1.3 today, which would make her feet slide by 0.2 m every second.
  To walk at another speed, scale the clip's playback speed by `speed / 1.1`.
- **Stair.** The stair leg falls 1.9 m over 3.0 m, and `CaseStage.Along` measures distance along the slope. Move her
  at **1.3 m/s along the slope** there. That is 1.1 m/s over the floor plan, so the steps still land where the clip
  puts them. She then comes down the stair in 2.7 s, which reads as a kid trotting down.
- **What a flat walk cannot do on the stair.** The clip plants both feet at the root's height. On the 32 degree slope
  the leading foot is about 0.15 m above the stair line and the trailing foot about 0.15 m below it. Her floor-length
  cape hides most of this from behind and from the side; from the front the shoes will float and sink. If that reads
  wrong in the Unity capture, the fix is a `WalkDownStair` clip: the foot planner would need a sloped contact line and
  a shorter step. It was not made here.
- **Turns.** The path has square corners. The clip has no turn; the game's rotation slerp turns her while she walks.
- **Start and stop.** `Walk` frame 0 is mid-stride, not the Idle pose. Cross-fade from KneeTap or Idle into `Walk`,
  and from `Walk` into Idle at the foyer mark (about 0.2 s each).

## Not verified here

- Nothing was run in Unity in this container. The import (clip present, loop flag) is checked by CI's
  `ModelImportRulesTests`; see the commit's CI run.
- The clip was judged from still frames and bone measurements, not from a played-back video.
- The walk has not been seen on the path, on the stair or from the game camera.
