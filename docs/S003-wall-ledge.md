# S003: wall slide, wall jump and ledge grab

Branch `feature/wall-ledge`. This branch adds the last two moves of the spec v1.2 moveset to Pip: the wall slide with
the wall jump, and the ledge grab with climb-up. Both are tuned for the 6–9 audience (D27) and are readable on touch.

- The rules are engine-free in `Core/Movement/ClimbRules.cs`, and `MovementModel` applies them.
- The tuning is data, as in D21: `MovementTuningData`, `PipMovementTuning` and `Config/PipMovementTuning.asset`.
- The runtime only measures probes, in `Runtime/Player/ClimbProbes.cs`.
- Only marked surfaces count. The marks are listed in `CollisionRules.TraversalMarks`, and the runtime adds a
  `ClimbSurface` component to each marked collider.

Level content and clues are unchanged (frozen). This branch only marks existing colliders.

## Moves

| Move | Input | Behaviour |
|---|---|---|
| Wall slide | In the air, past coyote time and falling, against a marked wall. The stick or the motion must point into the wall. | Pip faces the wall and grips: the fall slows to 0.6 m/s for 0.2 s, then slides at 1.5 m/s at most. A faster fall is braked at 30 m/s². A resting thumb keeps the slide. The stick held away for 0.25 s lets go, and the stick cannot pull her off before then. The slide also ends when she lands or the wall ends. The bat cape cannot open while she slides. |
| Wall jump | Jump while sliding, while touching a marked wall (even while rising), up to 0.2 s after leaving it (coyote), or up to 0.15 s before touching it (buffer) | A kick away from the wall at 4.5 m/s that rises 1.2 m above the kick under plain gravity. There is no high-jump boost and no chain stage. For 0.25 s air control drops to 0.15, so a stick still pointing at the wall does not cancel the kick. |
| Chain | Kick, steer at the opposite marked wall, kick again | Between two walls each kick gains about 1 m (Core: about 6 m after 6 kicks at 30 and 60 fps). Pip cannot climb a single wall: she can only slide on or kick off the wall she last kicked off once her feet are below that kick. Landing clears this rule. |
| Ledge grab | In the air, past coyote time, rising slower than 1.5 m/s or falling, near a marked ledge | Pip catches the ledge and hangs. The ledge top must sit 0.92–1.57 m above her feet, around the hanging hand height of 1.27 m. The edge must be within 0.35 m of her body. She must face it, steer at it or move toward it. There must be room to hang, room to stand on top, and at least 0.15 m of air under the hang spot. A 0.1 s snap places her 0.34 m out from the face with her hands on the top. There is no gravity while she hangs. Gliding, sliding and diving all end in the catch. |
| Climb up | Jump while hanging | 0.4 s. For the first 60 % she rises until her feet are 0.06 m above the top, then she steps over until her centre is 0.4 m in from the edge. |
| Drop | Stick down, or stick away from the wall (dot ≥ 0.5), held 0.1 s | She lets go with a 1 m/s push away from the wall. No ledge can be caught again for 0.4 s. |

Use does nothing while Pip slides, hangs or climbs: there is no dive from a wall. Interactions still take
priority as before.

## Tuning values (`MovementTuningData`, defaults also in the asset)

| Field | Value | Field | Value |
|---|---|---|---|
| `BodyRadius` (from `radius`) | 0.28 m | `LedgeHangHandHeight` | 1.27 m |
| `WallContactSlack` | 0.12 m | `LedgeHangWallOffset` | 0.34 m |
| `WallSlideMinFeetHeight` | 0.35 m | `LedgeGrabReachBelow` / `Above` | 0.35 / 0.3 m |
| `WallSlideIntoMin` | 0.3 | `LedgeGrabReachForward` | 0.35 m |
| `WallSlideGripTime` / `GripSpeed` | 0.2 s / 0.6 m/s | `LedgeGrabMaxRiseSpeed` | 1.5 m/s |
| `WallSlideSpeed` | 1.5 m/s | `LedgeMinHangClearance` | 0.15 m |
| `WallSlideBrakeDeceleration` | 30 m/s² | `LedgeSnapTime` | 0.1 s |
| `WallSlideReleaseTime` | 0.25 s | `LedgeClimbTime` / `RiseFraction` | 0.4 s / 0.6 |
| `WallJumpHeight` | 1.2 m | `LedgeClimbInset` / `Lift` | 0.4 / 0.06 m |
| `WallJumpAwaySpeed` | 4.5 m/s | `LedgeDropHoldTime` | 0.1 s |
| `WallJumpCoyoteTime` | 0.2 s | `LedgeDropPushSpeed` | 1 m/s |
| `WallJumpControlLockTime` / `LockedAirControl` | 0.25 s / 0.15 | `LedgeRegrabDelay` | 0.4 s |

The hang values were measured on Pip.fbx in Blender (headless 4.0.2, side renders against a box):

- **LedgeHang.** The left hand grips 1.27 m above the root, almost straight above it. The avatar therefore leans
  10° toward the wall about the feet and slides 0.1 m in while hanging (`MoveFeedbackRules.HangLeanDegrees` and
  `HangShift`). The hand meets the edge, and the chest stays clear of the face. The pail dips about 4 cm into the face.
- **WallSlide.** The toe and the hand touch a wall 0.31 m in front of the root, which is the controller's contact
  distance, so the slide needs no offset.

## Marked surfaces (`CollisionRules.TraversalMarks`)

| Surface | Mark | Why |
|---|---|---|
| `Shed_RoofSouth` slab, west face only (x −7.95, 0–2.4 m) | wall | The tall shed's clapboard side under the treehouse, facing the open yard pocket between the shed, the deck and ramp leg 2. Kicks go west, away from the route. |
| `FenceGate` `PostWest` and `PostEast` (1.28 m) | wall | The front-yard gate posts. Slides are short (the feet stay below 0.73 m), and a kick never reaches higher than a held jump. The paper bat at (−0.9, 2.0, 4.7) sits right above them. |
| `StreetLamp` `Head` (top 3.0 m, 0.97 × 0.7 m) | ledge | The lamp's arm by the arrival path. A held jump (or the third tapped chain jump) catches it, but a tap does not. Climbing up reaches the two paper bats at 3.1–3.35 m beside it. |

**The treehouse deck edge is not marked.** The plan asked for it, but no free deck edge has room to climb onto:

- The cabin (D24) closes the deck on all four sides, with its walls 0.18 m in from the deck edge. Pip needs 0.56 m.
- Rails close the north and west edges.
- The threshold boards and the shed roof sit against the east edge.
- The lookout bay covers the south edge.

The rule "room to stand on top or no catch" (`RoomToClimb`) would refuse every catch there. Marking the edge anyway
would also break the rule that a catch always has a climb. The shed roof is railed too, and the awning wall is a
0.13 m lip. So the first in-game ledge is the lamp arm, which is open, safe and visible from the start.

To get a deck ledge, a later content change could open a rail stretch or widen the deck strip outside the cabin.
That change is frozen here and is the owner's call.

## Case 01: route and reach

- **The mandatory route is unchanged.** No route slab changed shape. The only marked slab is the shed's west *side*,
  and no route leg walks there. The roof stays the route's walk top. `TraversalMarkTests` asserts this and checks
  every `MandatoryLegs` point.
- **The shed kick reaches nothing new.** The highest slide has the chest at the 2.4 m top, so the feet are at 1.85 m.
  The kick apex is 3.05 m. That is below the shed's west rail (3.4 m) and the deck rails (3.35 m), and the kick goes
  west. A chain needs two facing marked walls, and none exist in the street. Pip cannot climb a single wall.
  June's window (the dormer threshold), the cloth clue and the front door all lie east of the marked face, behind
  the solid shed.
- **The lamp gives a 3 m lookout over the arrival path.** From there the cape glides about 7 m, and the dormer, the
  cloth and the door are all more than 9 m away. No clue, witness line or deduction is placed high up there, and
  paper bats never gate the mystery.
- **Deductions cannot be skipped.** The moves only help exploration, and case facts are untouched.

## Feedback, animation and tips

- **Kick.** A paper puff of 9 confetti and star pieces bursts off the wall where the feet pushed. It sprays along
  the normal and fans across the face. Pip gets a stretch kick of −0.09.
- **Catch.** 4 small felt dust puffs appear at the hands on the edge, with a squash of 0.06. The slide start
  squashes by 0.04.
- **Reduced motion and cost.** With reduced motion every burst is skipped, but the squash stays. The bursts share
  the existing 40-piece pool.
- **Animation.** `WallSlide` and `LedgeHang` from Pip.fbx now follow the motor (`PipMotor.WallSliding`,
  `PipMotor.Hanging`). While sliding or hanging, Pip turns to face the surface.
- **Missing clips.** Pip.fbx has no **`WallJump`** (kick-off) clip and no **`LedgeClimb`** (pull-up) clip:
  - the kick restarts `JumpStart` and the normal air arc follows;
  - the climb plays `JumpStart` as a one-shot timed to the climb, then `Land` on top.

  The Blender clip list is in `tools/blender/export/pip_anims.py` `CLIPS`. A `LedgeGrab` catch clip would also help.
- **Tips** (`MoveTips`, shown once ever):

  | Tip | Text | Shown when |
  |---|---|---|
  | `tip.move.wall_jump` | "Jump to bounce off the wall!" | a slide lasts 0.3 s without a kick |
  | `tip.move.ledge_climb` | "Jump to climb up!" | a hang lasts 0.35 s |

  Either tip ends when its move is performed. A child who performs the move first never sees the tip.

  The tip tag now leads with the control's glyph in the current scheme (D25), for every move tip:
  - touch: the Jump or Use disc's ink icon;
  - gamepad: the lit face button;
  - keyboard: the key cap.

  It follows `ControlSchemeTracker` live.

## Tests

- **Core (dotnet, local): 461/461.** The new tests are:
  - `WallLedgeTests` (39 cases at 30 and 60 fps) on the engine-free `ClimbTestWorld`. They cover:
    - wall contact rules;
    - the grip and the 1.5 m/s cap;
    - the release delay;
    - the kick vector and its 1.2 m apex;
    - the control lock;
    - the press buffer;
    - wall coyote time;
    - a kick while rising;
    - a 6-kick chain between two walls;
    - no single-wall climbing, and the same-wall rule clearing on landing;
    - no dive while held;
    - the catch window, reach and room checks;
    - the hang spot, snap and climb placement;
    - the climb ending on top;
    - the drop down or away, with no re-catch;
    - a high jump catching a 3 m ledge where a tap does not;
    - that unmarked walls and ledges never trigger anything, including probes reporting them with Marks None;
    - the feedback bursts and the poses.
  - `TraversalMarkTests` (11). They check:
    - which surfaces are marked;
    - that each mark names an existing collider;
    - that the house, deck, rails and route tops stay unmarked;
    - the shed face;
    - the lamp's reach and room;
    - the gate posts;
    - the shed's reach;
    - clearance from the thresholds and clues;
    - the tip ids, glyphs, lines and triggers.
- **EditMode project.** `ProjectSetupTests` asserts the new asset values and that `ToData` carries them.
- **PlayMode (`WallLedgeTests`, gamepad stick and South button, a temporary save directory in Setup):**
  - slide and kick on the shed's west side;
  - the owl house siding never slides or kicks;
  - a 5-kick chain between two marked walls in a test-only rig south of the block;
  - an unmarked twin of a marked ledge is never caught, while the marked one is;
  - a held jump catches the lamp arm (a tap does not), then hang, lean, dust, the climb tip, and Jump climbs on top;
  - pulling away drops, with no re-catch;
  - the porch roof's eave is never caught;
  - the wall tip shows the gamepad's lit South button and switches to the touch icon.

  The existing moveset tests run unchanged.
- **Frames:** `50_wall_slide` and `51_ledge_hang`.

## Evidence (CI run 116, commit 5f717e3, software GL; not a device)

- **Tests:** EditMode 532/532 (`WallLedgeTests` 39/39, `TraversalMarkTests` 11/11, `ProjectSetupTests` 5/5),
  PlayMode 92/92 (`WallLedgeTests` 8/8, and the existing `MovesetTests`, `MovesetFeelTests` and `CollisionTests` all
  pass), Visual 11/11. Core (dotnet, local, after merging main): 508/508.
- **Chain** (PlayMode log): 5 kicks between the rig's walls, feet 5.80 m above the floor.
- **Frames** (S002.5a took 45–49, so these are 50 and 51):
  - `docs/evidence/s003/50_wall_slide_run116.jpg`: Pip in the WallSlide pose against the shed's west side, cape up,
    with the wall tip fading in (its glyph is the keyboard's Space key cap). Readable, but the pocket under the
    treehouse is dark: there is no light on that side of the shed.
  - `docs/evidence/s003/51_ledge_hang_run116.jpg`: Pip hanging from the lamp arm, hand on the edge and body leaning in,
    with the "Jump to climb up!" tip and its Space glyph.
- **Earlier runs:** 105 failed one PlayMode test (the stick stayed pushed after the tap); 109 failed the climb-tip
  check (the hold tip's cooldown) and the slide frame (captured too late). Both are fixed.

Not verified: the feel on a phone. That needs a TestFlight build after the merge.
