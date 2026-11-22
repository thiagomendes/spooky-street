# S002 (part 3): interior wiring and the finale

Branch `feature/s002-interior-wiring`. This pass makes case 01 playable end to end. It stages the treehouse route, the
owl house interior and the workshop mechanism (`docs/S002-interior-assets.md`), June and Aunt Kit, the crank cutaway
and the finale. It also rebuilds all of them from facts on resume. It completes the checklist in
`docs/S002-wiring.md` ("What the interior pass needs").

## Engine-free additions

- **`Core/World/CaseRouteLayout.cs`:**
  - The model placements (the suggested placements in `tools/blender/env/env_interior_review.py`).
  - Every authored collider as a `StageSlab`: the centre line of a box's top face, a width and a depth. Slabs are
    `Walk` (safe ground) or `Wall`.
  - The mandatory route and the skilled ascent as waypoints.
  - Frames: owl house builder space → world is `x = bx, z = 11 + by, y = bz`. The ramp frame → world is
    `x = rx − 9.5, z = ry + 12`.
- **`Core/Case/CaseStage.Interior.cs`:**
  - The interior Use targets.
  - `PointSide`: the latch is offered only from inside, the front door only from outside.
  - `CaseStage.IsOffered`: which targets exist in the current `CaseWorldView`.
  - The trigger zones: `InAtticEntry`, `InFoyer`.
  - June's and Kit's marks, June's walk, and the finale's eye and look-at points.
  - `InteriorVisibility`: the cut-away rule.
  - `MechanismMotion`: cam, follower and owl as one function of cycle time.
- **`CaseStage`:**
  - All 11 anchors are staged.
  - `NearestAnchor(x, y, z)` weights height ×3, so the attic and the foyer never mix. It picks an inside anchor only
    for a point inside the shell.
- **`InteractionPoint`:** has an optional verb override. The service panel reads "Open" and reports `ExamineGrille`.

## Placement and colliders

| Model | World placement | Notes |
|---|---|---|
| `ShedRamp` | (−9.5, 0, 12), yaw 0 | Leg 1 runs north at x −13.4…−11.5, z 7.4 → 15.0, rising 0 → 1.52 (1:5, 1.9 m deck). Then the landing. Leg 2 runs east to x −7.1, rising 1.52 → 2.4 (1:5, 1.7 m deck). |
| tall shed / roof | x −7.95…−5.75, z 11.55…16.7, top 2.4 | Solid to the roof. A rail runs on the east edge, open at the bridge head. |
| `SillBridge` | (−4.55, 0, 14.0) | 2.4 m span, 2.40 → 2.50 m. Low guards (0.8 m) on both sides. |
| `ClothSnag` | (−5.87, 3.38, 14.5), yaw 90 | On the bridge-head post, 0.98 m above the roof. |
| `TreehouseCabin` | the maple's frame, yaw 200 | Wall colliders only. The platform deck is owned by `fix/collisions` (see below). |
| `Crate` | (−7.1, 0, 9.47), yaw 12 | 0.6 m. Its collider comes from the `CollisionRules` recipe (merged from `fix/collisions`). |
| `Awning` | (−7.11, 0, 11.42) | Its colliders come from the `CollisionRules` recipe: the canvas slope (1.35 → 2.1 m, 26.6°) and the wall slice. `CaseRouteLayout` keeps matching reference slabs for the route checks. |
| `OwlHouseInterior` | (0, 0, 13) | The same origin as `OwlHouse`. |
| `WorkshopMechanism` | (1.45, 0.1, 14.1), yaw 90 | The front faces the foyer (−X). |

The house shell is hollow. It has:

- outer walls (with the door opening and June's window cut out);
- roof plates along the ceiling slope, open over the dormer;
- a door-leaf blocker, switched off once the door opens;
- the foyer floor, a 37° stair ramp, the loft around the stairwell, the loft rails, the workshop partition, and the
  bench, craft table and crates.

`EnvironmentDressing` no longer adds the solid body and roof boxes once `OwlHouseInterior` is committed. That is the
only edit there.

While Pip is inside (or the interior shows), the shell's walls move to Ignore Raycast. The camera boom also ignores
SafeGround. So the camera sits outside the shell and looks in through the culled near walls, as the assets intend.
Outside, everything blocks the camera as before.

## How each interior beat is triggered

| Beat | Trigger | World reaction |
|---|---|---|
| ExamineCloth | Use (Look) at the bridge-head post on the shed roof | Pip's line. |
| EnterAttic | Pip's feet enter the dormer opening (`InAtticEntry`), on entry | The window is open from the start (`SideWindowPane` hidden). The interior shows, the body and the recess card hide, and the camera eases to the 2.1 m shoulder view. `atticFirst` applies if no witness has spoken. |
| ExamineSill | Use (Look) at the `SillSnag` on the north jamb, from the bridge or the attic | Pip's line. |
| Talk June | Use (Talk) at her stool | June sits and taps the knock rhythm (`KneeTap`) and turns her head to Pip. Her lines come from `CaseScript`; with `atticFirst` she gives the shortcut greeting. When the lines finish, she walks her authored path down the stair to the foyer mark. |
| EnterFoyer | Pip enters the ground floor, on entry | The hum caption (before `sawCam`), with a direction cue toward the workshop. |
| LiftLatch | Use (Lift) on the inside latch (inside only) | The latch lifts. Then `InnerDoor` and `FrontDoor` swing 100° inward together about the hinge, and the latch rides with the leaf. The door blocker switches off, so the door is a two-way shortcut. |
| ExamineGrille | Use (Look) at the low grille, or Use (Open) on the service panel | `sawCam`. The panel swings 112° toward the foyer. One knock cycle plays with `NookWall` hidden, so the follower is seen lifting three times. |
| LinkArm | Use (Connect), offered once the panel is open | `LinkArm_Slipped` hides and `LinkArm_Seated` shows. |
| TurnCrank, early | Use (Turn) before the arm | `CrankGap`: one tap, the cam a third of a turn, no bow, the gap caption, no fact. |
| TurnCrank | Use (Turn) after the arm | `testedOwl`. When `CaseStep.PlayCutaway` is set: the 4 s cutaway (below). Later turns replay the cycle. |
| ArriveAtPorch | Pip steps onto the arrival porch | Needs `frontOpen` and `testedOwl`. It gives `caseClosed`; see the finale steps below. |

The finale, in order:

1. June moves to the porch and waves.
2. Kit appears at her booth and plays `OpenBooth`.
3. The owl bows through one knock cycle.
4. The finale camera frames the porch from the arrival sightline while June's and Kit's lines play.
5. The shot holds for 1.6 s, then the notebook opens on its final card.
6. The player keeps control afterwards.

**The cutaway:**

- A fixed camera looks into the nook with `NookWall` hidden.
- A felt-framed inset renders the rooftop owl. Its camera renders on request into a 512 × 320 texture.
- The exterior body stays drawn for the inset; from inside, its outward faces cull.
- A tap, E, Space, Enter, the gamepad face buttons or Skip end it.

**Owl motion:**

- The cam turns 120° per knock, and the crank turns with it.
- The follower lifts and drops on each knock (short, short, pause, last).
- With the arm linked, the owl bows on each knock about its feet, toward the street.
- After the arrival knocks, wind cycles repeat every 9 s. The owl bows in them once it has been tested.

Hinge directions are chosen at runtime from each part's geometry, so they do not depend on the FBX axis conversion.

## Save and restore

`ApplyWorld` rebuilds from `CaseWorldView`:

- the door (both leaves, the latch and the blocker);
- June's mark (attic, foyer or porch), with Kit hidden until the reopening;
- the service panel, the link arm and the owl state (bowing, and the wind cycles);
- any cutaway or finale shot, which is cancelled.

Autosave records the staged anchor nearest Pip's last safe ground in 3D. The staged anchors are side yard, treehouse,
attic sill, attic, foyer and workshop.

## Tests

- **EditMode `CaseRouteTests`:**
  - The mandatory route walked in 10 cm samples, with no step over 0.2 m and no wall, rail, headroom or exterior prop
    in the way.
  - The latched door blocks the way out.
  - The ramp is 1:5 and at least 1.6 m wide.
  - The window fits Pip.
  - The skilled-ascent hops fit the 1.0 m tap jump, and both ascents meet on the shed roof.
  - Interior target coverage and the offered-target rules.
  - The June and latch markers stand apart.
  - Marks, anchors and June's walk stand on floors.
  - Attic and foyer saves never mix.
  - The zones, the cut-away rule, and mechanism sync (120° per knock, the follower lifted before each knock, a bow
    with each knock).
- **PlayMode `CaseInteriorTests`:**
  - The mandatory route walked with the real colliders (CharacterController, gravity, no jump).
  - The skilled-ascent surfaces hold Pip.
  - **CASE-01:** canonical, with Use on every interior target and closure with the correct evidence.
  - **CASE-02:** attic first, early crank, repeated talk, bench after the solve.
  - **SAVE-01:** reload after the window entry, after the latch, and during the crank cutaway.
  - The cut-away and attic-hiding rules.
- **Visual `InteriorCaptureTests`:** `18_treehouse_route`, `19_attic_june`, `20_workshop_cutaway`, `21_finale`,
  plus `23_foyer_door`.

## Evidence

- `./tools/check-local.sh`: all checks passed. `dotnet test tools/core-tests`: **287 passed**, including the 14
  `CaseRouteTests`.
- **CI run 63** (`b58d920`, run id 45038439105): compiled. EditMode 301/301, PlayMode 44/47, visual 3/3. The run
  confirmed the coordinate mapping: the logged latch, sill snag, window pane and mechanism stand where
  `CaseRouteLayout` puts them. Three failures:
  - the cutaway inset tried to create a render texture without a graphics device (PlayMode runs `-nographics`);
  - a caption assertion read the queue, not the step;
  - a resume anchor was legitimately the attic, not the sill.

  Frame review found three problems: Pip in the cutaway shot, the open panel blocking it, and the finale's speech card
  covering the porch.
- **Fixes (`08c46c7`):**
  - merged `main` (`fix/collisions`): Crate and Awning use their `CollisionRules` recipes, and the route models are
    audited;
  - the reopening plays for 3 s before its card;
  - the cutaway hides Pip and looks from the north;
  - the inset renders only with a graphics device.

  CI run 64: EditMode 309/309, PlayMode 51/52 (the awning-slope tolerance against the recipe's canvas), visual 4/4.
- **CI run 65** (`88737dc`, run id 45040964240): **all green**. EditMode 309/309, PlayMode 52/52 (`CaseInteriorTests`
  6/6, `CollisionTests` 5/5), visual 4/4. Frames (`docs/evidence/s002/*_run65.jpg`):
  - `18_treehouse_route`: the side yard, ramp with bunting, the cabin on the maple, the awning and crate against the
    shed, and the owl house. Pip is on the landing.
  - `19_attic_june`: the closer shoulder view through June's window. June sits on her stool tapping her knee, Pip
    stands beside her, and the rug, rails, star lantern and craft table are in frame.
  - `20_workshop_cutaway`: the nook with its near wall hidden. The cam's lobes, the follower bar, the striped push rod,
    the crank and Kit's photo are visible. The felt-framed inset shows the rooftop owl and wind wheel. Skip is at top
    right.
  - `21_finale`: the arrival sightline. The front door is open, June is on the porch to the left, Kit is at her booth
    to the right, and the owl and wind wheel are on the gable. A leftover caption from the test's quick sequence shows.
  - `23_foyer_door`: from the foyer, both leaves are swung inward on the west hinge and the porch is visible through
    the doorway. The loft underside and stair rail crowd the top-left of this gameplay frame.
- **Not run:** device. No device pass is claimed.

## Known gaps

- There is no walk clip for June (her clips are Idle, Talk, KneeTap, Wave and Point). She glides down her authored
  path on Idle at 1.3 m/s. No art was invented.
  The clip now exists (`docs/S002-june-walk.md`: `Walk`, planted for 1.1 m/s); playing it on the path is still to
  be wired.
- There are no ribbons in the committed art, so nothing twitches. There is no audio for the knocks yet; captions carry
  them.
- The treehouse platform deck collider belongs to the parallel `fix/collisions` branch. Until that merges, the cabin
  (walls only here) cannot be stood in. Both ascents meet on the shed roof, which is mine, so the case does not depend
  on it.
- Ramp leg 2's deck is 1.7 m wide. Its modelled rails stand 7 cm inside the deck edge; the rail colliders sit at the
  deck edge, so the usable width is the full 1.7 m.
- The finale's booth is the owl house porch and ticket door, with Kit at it. No separate booth shutter exists in the
  art.
- Device: not run. No device pass is claimed.
