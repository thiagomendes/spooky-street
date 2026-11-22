# S002 fix: collision audit and boundary

Branch `fix/collisions`. Owner device feedback on TestFlight 13.1:

1. "She still passes through some objects, e.g. the treehouse: when I jump, I go through it."
2. "When you try to jump over a hedge at the end of the area, she falls into a limbo and then goes back by herself."

Cause of (1): the treehouse deck had no collider and every other prop had a single bounds box or a pole. Cause of (2):
the greybox hedge colliders were only 1.4 m tall and there was no ground behind them. A 2.1–2.8 m jump cleared the
hedge, Pip fell past the 44 × 36 m ground box, and `SafeGroundTracker` returned her.

## Collider recipes (`Core/World/CollisionRules.cs`)

The table is engine-free and covered by tests. Parts are given in the model's own upright frame: +Y up, the front
toward -Z, and Blender (x, y, z) mapping to (x, z, y). The bounds were measured from the FBX exports with headless
Blender, and the deck, trunk and limb numbers come from `tools/blender/env/props.py` and `maple.py`.
`Runtime/World/ModelColliders.cs` builds the parts under a `Colliders` child that is turned by the placement yaw only.
`EnvironmentDressing.Spawn` uses a model's recipe when it has one. Otherwise it falls back to the placement's own
`PlacementCollider`.

| Model | Recipe |
|---|---|
| TreehousePlatform | Deck floor 2.68 × 0.15 × 2.64 m with its top at 2.4 m, centred on the trunk lean. Rails to 3.35 m on the -Z and +X edges. The -X edge stays open, toward the shed roof. |
| Maple (both) | Trunk capsules: r 0.42 from 0.45 m to the deck height, then r 0.3 up to the fork at 4.3 m. Four limb-base capsules (r 0.14) from the fork. The crown and leaves have no collider. |
| OwlHouse | The existing house boxes, plus a porch roof slab pitched -10° (2.9 → 3.3 m), a ridge cap to 6.45 m, the front gable peak to 7.0 m and the chimney to 7.4 m. |
| OwlHouseDressing | Porch clutter, west to 1.45 m and east to 1.0 m, and the pumpkins beside the house steps. The steps and the door stay clear. |
| Bench | Seat (0.56 m), backrest (0.94 m), and Mr. Vale's seated body at local x +0.28 (0.56–1.35 m). |
| PartyTable | Fitted table top at 0.72 m, plus a box for the party things on it (1.0 m). |
| StreetLamp | A 0.3 m pole to 3.3 m, and the arm and lantern head (2.2–3.0 m). |
| FenceGate | The two posts only (0.14 × 1.28 m). The opening is 0.96 m wide and clears the route centre line by 0.38 m and 0.58 m. |
| FenceSection, Hedge, Mailbox, Crate, CandyBowl, Pumpkin, JackOLantern, Kit_Rail | Box fitted to the mesh bounds, with a standable top. |
| BoundaryHedgeLong | Box fitted to the mesh bounds (top 1.29 m). The greybox hedge lines stay behind it, lowered to 1.25 m. |
| NeighbourHouseA/B/C | Box fitted to the mesh bounds. All three stand outside the walls, so Pip cannot enter them. |
| Kit_Floor, Kit_Steps | Carried by the greybox ArrivalPorch and PorchStep footings. |
| Awning (route piece, not placed here) | A wall slice, plus the canvas slope pitched -26.6° (1.35 → 2.1 m, walkable). |
| PaperBat, Lawn, PathStones, Sidewalk, LeafScatterA/B/C | Decoration, no collider. |

## Boundary (`Runtime/World/BoundaryWalls.cs`)

- **Walls:** four invisible walls, 1 m thick, from -2 m to 14 m high. Their inner faces are at x = ±22 and z = ±18:
  the outer edge of the hedge lines and of the block ground, so there is no gap to stand in. The walls are on the
  Ignore Raycast layer. They stop Pip's CharacterController, but the camera boom, the ground probes and the
  Use-target sight checks ignore them.
- **Height:** the highest standable top is 7.4 m (the chimney). With the best jump (2.8 m) and Pip's height
  (1.1 m), she can reach 11.3 m, so the 14 m walls need no ceiling cap.
- **Ground ring:** SafeGround strips under the whole lawn ring (x ±30, z -18..34) outside the block ground, with no
  overlap. Every spot has ground, so the safe-ground return is only a last resort.
- **Spawn and plaza:** the arrival porch and the plaza lie well inside the block ground.
- **Tests moved:** the two fall-return tests now teleport to (40, 2, 40), beyond the lawn ring.

## Tests

- **EditMode `CollisionRulesTests`:**
  - Every placed model has a collision decision.
  - Models that look solid are solid, and decoration adds no collider.
  - Every part is well formed.
  - The deck is at 2.4 m with the trunk through it and four limb bases.
  - The gate posts keep the route open.
  - The walls are flush with the hedges and the ground edge, and outreach the best jump.
  - The neighbour houses stand outside the block.
  - Every lawn-ring cell lies on exactly one ground piece.
- **PlayMode `CollisionTests`:**
  - Every solid placement has its recipe's colliders on its meshes.
  - Pip is driven with the gamepad stick into 13 props: the porch rail, crate, mailbox, lamp, jack-o'-lantern,
    pumpkin, table, bench, both maples, a fence section, a gate post and the front hedge. Her capsule axis never
    enters a collider, and she reaches each prop.
  - The treehouse deck carries her at 2.4 m, its rail keeps her on it, and a held jump from below bumps the
    underside.
  - Boundary: Pip runs with held chain jumps and the glide at north, south, east, west, the NE corner and off the
    owl house ridge. She stays inside, always has ground below, lands, and triggers no safe-ground return.
  - The boundary hedge tops are standable.
- **Visual `CollisionCaptureTests`:** `22_treehouse_deck`, Pip standing on the deck.

## CI evidence

CI run 62 ([45037992083](https://github.com/thiagomendes/spooky-street/actions/runs/45037992083)), commit `d6e66b8`:
the Unity compile and the EditMode and PlayMode tests job passed.

| Results | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| EditMode | 295 | 295 | 0 | 0 |
| PlayMode | 46 | 46 | 0 | 0 |
| Visual | 3 | 3 | 0 | 0 |

The existing route walk, witness, moveset and fall-return tests stayed green. Log lines:

- **Solid placements:** `[CollisionTests] 66 solid placements checked`.
- **Boundary:** at north, south, east, west and the NE corner, Pip peaks at 4.12 m (a 2.8 m jump off the 1.32 m
  hedge top). She glides, ends on the hedge top against the wall (|z| or |x| = 17.69 / 21.69), and gets 0 returns.
  Off the owl house roof she peaks at 8.58 m, glides, ends at (-0.05, 1.32, 17.69) and gets 0 returns.
- **Probes:** the closest approach is 0.28–0.31 m, which is Pip's radius plus the skin. Round props deflect her
  around them; nothing lets her through.

Frames were extracted with `tools/ci/extract_frames.py` from the raw job log and reviewed:

- `22_treehouse_deck` (`docs/evidence/s002/22_treehouse_deck_run62.jpg`): Pip stands on the plank deck beside the
  trunk, with both rails behind her. Her feet are on the planks, not sunk into them and not floating.
- `03_after_walking` and the other S000 frames are unchanged.

## Known gaps

- **Approximations:**
  - The owl house roof slopes are stepped boxes: Pip can stand within about 0.5 m above the shingles.
  - The wind wheel and the owl puppet have no collider.
  - The deck braces under the treehouse have no collider (thin diagonals).
- **Bench:** the bench recipe includes a box for Mr. Vale's seated body. If he ever leaves the bench, that box has to
  follow him.
- **Merge with `feature/s002-interior-wiring`:**
  - Recipes are matched by model name and take precedence over a placement's `PlacementCollider`. The Awning and Crate
    recipes therefore also apply where the route wiring places them.
  - ShedRamp, TreehouseCabin, SillBridge and ClothSnag have no recipe here. They keep the placement kinds that branch
    gives them.
  - `EveryPlacedModelIsAudited` fails on merge if a route model is placed with `PlacementCollider.None` and no recipe.
  - The cabin walls stand inside the deck rails, so the deck test's standing point (deck-local 0.75, 0.6) stays inside
    the cabin.
