# S002 house v3: closed, camera-sized spaces (assets only)

Branch `feature/house-v3`. Owner feedback on TestFlight 17: "Inside the house, the camera keeps seeing through the
walls and through the ceiling. It shows outside, as if there were no wall." Decisions D24 and D24.1 followed:

- **Owl house exterior:** same size and shape. Its windows now show warm painted cards, and its two ways in open
  onto shallow closed view boxes.
- **Owl house interior:** a new free-standing model, "bigger on the inside", closed on every side and sized for
  the third-person camera. The game places it away from the street and moves Pip across at the two thresholds.
- **Treehouse cabin:** stays on the maple and stays seamless, but is closed and a little taller, with a lookout bay.

No C# changed. Nothing here is wired into the game yet; the anchor tables below are for that pass.

## 1. What was measured (before)

Measured from the committed FBX files with headless Blender, and from `interior.py` / `owl_house.py` at `27b4c76`.
Model space: metres, x across, y depth (front toward -y), z up, origin 2 m behind the porch front
(builder y = model y + 2).

### Exterior `OwlHouse` (unchanged by this branch)

| Child | Triangles | Bounds min (x, y, z) | Bounds max (x, y, z) | Pivot |
|---|---|---|---|---|
| `OwlHouseBody` | 14,497 | -3.59, -2.45, 0.00 | 3.46, 4.38, 9.03 | origin |
| `Porch` | 6,444 | -3.26, -4.72, 0.00 | 3.26, -1.96, 3.45 | origin |
| `FrontDoor` | 1,862 | -0.55, -2.17, 0.60 | 0.55, -1.96, 2.62 | -0.55, -2.015, 0.60 |
| `DoorRecessCard` | 2 | -0.57, -1.88, 0.60 | 0.57, -1.87, 2.64 | origin |
| `SideWindowPane` | 88 | -3.08, 0.50, 2.51 | -3.05, 1.50, 3.94 | -3.045, 0.50, 2.50 |
| `OwlPuppet` | 1,960 | -0.44, -2.26, 6.71 | 0.44, -1.91, 7.67 | 0.00, -2.02, 6.715 |
| `WindWheel` | 594 | -0.71, -1.64, 8.12 | 0.71, -1.37, 9.53 | 0.00, -1.50, 8.825 |

The body is 6.0 m wide and 6.0 m deep, with the eave at 2.86 m and the ridge at 6.44 m.

### Old interior, and where a 2.4 m boom behind a 1.1 m Pip failed

| Space | Size | Clear height | Camera result |
|---|---|---|---|
| Foyer | 4.31 m (x) by 5.72 m (y) | 1.74 m under the loft (floor 0.6, underside 2.34); open to the roof only for the first 1.36 m behind the door | A camera 1.6–2.6 m above the floor is inside the loft slab or the attic over 76 % of the floor. |
| Stair | 1.0 m wide, 10 risers of 0.19 m, along the back wall | under the loft until the fifth riser | The camera behind Pip is under the 1.74 m ceiling for the lower half. |
| Attic | 5.72 m by 4.36 m of floor, knee wall 0.36 m | 3.77 m at the ridge; 2.6 m only within 0.98 m of the centre line | June's stool stood 1.1 m from the west eave, where the roof is 1.68 m high: a camera on that side is outside the roof. Collar ties at 2.55 m, the star lantern at 1.85 m and the garlands at 2.0–2.5 m hung in the camera band. |
| Dormer alcove | 1.4 m wide, 0.9 m deep | 1.4–2.0 m | Only a camera outside on the bridge fits. |
| Workshop nook | 1.31 m by 4.11 m, floor 0.1 | 2.24 m | Not entered. |

Every wall was a single inward face, and the game put the camera outside the shell. That is the view the owner saw.

### Old `TreehouseCabin`

2.24 m square on the 2.6 m deck, eave 1.45 m, ridge 2.08 m. It was open on the yard side above a 0.72 m parapet,
open at both gables, and open along the whole ridge (a 0.6 m slot for the boughs). The trunk (0.62 m thick) stands
in the middle of the floor.

## 2. What was built

| Model | Triangles | Texels | Notes |
|---|---|---|---|
| `OwlHouseInterior` | 34,436 (was 24,998) | 2048, 66 px/m (was 87) | New rooms. Children as before, plus `StreetView_Glow`. |
| `OwlHouse` | 25,897 (was 25,603) | 2048, 69 px/m | Same geometry, plus `RoomCard_Glow` and the painted attic window. |
| `TreehouseCabin` | 6,822 (was 5,433) | shared `Routes` atlas, 113 px/m | Closed, eave +0.25 m, lookout bay. |
| `ShedRamp`, `SillBridge`, `ClothSnag` | unchanged | re-baked | Same meshes; only their UVs moved, because they share the `Routes` atlas with the cabin. |

`WorkshopMechanism`, `OwlHouseDressing` and every `Env` atlas model are untouched. No new file names, so no metas
changed.

The interior's biggest parts are the root mesh (14.7k: hall, stair, loft slab and hall dressing), `Attic` (13.5k),
`NookWall` (2.3k) and `InnerDoor` (1.9k). Structure and dressing together are above the art bible's "about 30k" for
a house structure. Moving the dressing into its own mesh is the first thing to do if the frame budget needs it.

### Interior rooms (`tools/blender/env/interior.py`)

Frame: the same builder convention as before, so the old formulas keep working with new constants. The inner face
of the front wall is at y = 0.14, the door is centred on x = 0, the hall floor is at z = 0.6, and the FBX origin is
at builder (0, 2, 0).

| Space | Size | Clear height |
|---|---|---|
| Whole shell (inner faces) | 7.20 m (x) by 6.86 m (y): 26 % wider and 20 % deeper than the old interior (5.72 m square) | floor 0.6 to ridge 8.22 |
| Hall | Clear rectangle 4.30 m by 5.36 m, plus the entry corner beside the stair (1.5 by 3.1 m) and the coat alcove (2.06 by 1.5 m) | 3.26 m (two ceiling beams at 3.21 m) |
| Stair | 1.5 m wide lower flight, 1.5 m square landing, 1.4 m wide upper flight; 18 risers of 0.189 m, 0.28 m treads | Lower flight and landing: open to the roof (2.9 m or more). Upper flight: open to the attic roof. |
| Attic | 7.20 m by 6.86 m of floor less the stairwell; knee walls 1.2 m, roof 40° | 4.22 m at the ridge; 2.6 m or more in a band 3.86 m wide; 1.2 m at the knee walls |
| Dormer alcove | 1.5 m wide, 0.7–1.4 m deep | 1.8 m at its eave, 2.4 m at its ridge |
| Workshop nook | 1.30 m by 4.50 m, floor 0.1 | 3.76 m |

Camera band: nothing hangs between 1.6 and 2.6 m above a walkable floor on a walking path. Lowest points:

- **Attic:** collar ties 3.15 m, bat garland and bunting 2.75 m, star lantern 2.65 m, owl lantern 2.71 m.
- **Hall:** ceiling beams 3.21 m, bunting 2.85 m, bat garland (on the stair wall) 2.74 m, star lamp 2.76 m.
- **On walls only:** the wall lamps (2.05 m, 0.2 m proud), curtains and frames. Two lamps flank the front door at
  x = ±1.15.
- **Under the low roof:** two star strings hang 0.15–0.35 m under the attic slopes at x = ±2.75, where the roof is
  only 1.9 m high. A camera cannot be there in any case.

Closed shell:

- Walls, floors, ceilings and roof boards have inward faces on every side.
- The stair wall, the nook partition, the loft slab and the front door have both faces.
- The space under the stair is boxed in.
- The front door and June's window open onto closed view boxes (porch boards and bridge boards running out to a
  painted night view). The five windows have a reveal and a painted night view 0.22 m behind the frame.
- All the painted views are one self-lit child, `StreetView_Glow`.

A camera that stays inside the rooms can never see past a face. A camera outside the shell still sees in through
the culled backs of the outer walls, exactly as before, so `InteriorCamera` has to hold.

### Exterior (`owl_house.py`)

- **Window cards:** the four ground-floor windows carry a warm painted room (lamp, picture, curtains) in front of the
  dark glass. The round attic window keeps its lit paper and gains the silhouette of the owl lantern and a string of
  flags. The cards are `RoomCard_Glow` (default glow level 1.5, colours at most 0.95), far below
  `AtticWindow_Glow` at 4.
- **Closed thresholds:** behind the front door there is a 0.88 m deep view box with a painted hall. Behind June's
  window there is a 0.73 m deep one with a painted attic. Both are part of `RoomCard_Glow`.
- **`DoorRecessCard`:** kept as a child for its name only (two triangles behind the hall view box). Hiding or
  showing it no longer changes anything.

### Treehouse cabin (`routes.py`)

- Walls on all four sides. The yard side is now a lookout bay on its own boards and two brackets, 0.45 m beyond the
  platform edge, with a wide window instead of the open parapet.
- Boarded gables. The ridge is boarded over, except for a 1.42 m by 0.6 m collar round the fork where the four boughs
  leave. Through it you see the tree, not the sky slot.
- Eave 1.45 → 1.70 m, door 1.30 → 1.45 m. The maple keeps the air above 2.4 m over the deck for its boughs, so the
  ridge cannot go higher than 2.33 m.
- The cabin is not needed for case 01 (both ascents meet on the shed roof) but Pip can walk in. It is a child-sized
  room with a trunk in the middle: a 2 m boom cannot fit, and the camera solver has to shorten the boom to about
  1.0–1.2 m there.

## 3. Anchors for the gameplay pass

Interior positions are in the interior model's builder frame (x, y depth, z up). In Unity, with the model root at
`O` and yaw 0, the world position is `(O.x + x, O.y + z, O.z + y − 2)`; that is, the old `FrontZ` becomes
`O.z − 2`. Old values are the constants in `CaseRouteLayout` and `CaseStage.Interior`, in the old builder frame.

### 3.1 Shell and levels

| Anchor | Old | New | Units |
|---|---|---|---|
| Inner faces, x (`InnerMinX` / `InnerMaxX`) | -2.86 / 2.86 | -3.60 / 3.60 | m |
| Inner faces, y (`InnerMinZ` / `InnerMaxZ` less `FrontZ`) | 0.14 / 5.86 | 0.14 / 7.00 | m |
| Hall floor (`FloorY`) | 0.60 | 0.60 | m |
| Attic floor (`LoftY`) | 2.50 | 4.00 | m |
| Loft underside (`LoftUnderside`) | 2.34 | 3.86 | m |
| Roof knee / apex (`KneeY` / `ApexY`) | 2.86 / 6.27 | 5.20 / 8.224 | m |
| Roof slope (rise per metre toward the centre) | 1.192 | 0.84 | – |
| Loft front edge (`LoftFrontZ` less `FrontZ`) | 1.50 (entry bay open to the roof) | none: the loft runs to the front wall | m |
| Model placement | (0, 0, 13): inside the exterior | any offset away from the street, yaw 0 | m |

### 3.2 Thresholds

| Anchor | Old | New | Units |
|---|---|---|---|
| Inner front door: opening x, head | -0.57…0.57, 2.62 | same | m |
| `InnerDoor` pivot (hinge) | (-0.55, -0.015, 0.60) | same | m |
| `DoorLatch` pivot | (0.12, 0.13, 1.55) | same | m |
| Latch use point | (0.35, 0.30, 0.60) | same | m |
| Door arrival point (Pip appears, facing +y) | – | (0.00, 1.30, 0.60) | m |
| Door exit zone (door open; feet) | – | abs(x) < 0.50, y < 0.30, z 0.5…1.5 | m |
| Door view box (keep Pip out: blocker at y = 0.0) | – | x -0.90…0.90, y -1.25…0.03, z 0.58…3.30 | m |
| June's window, wall plane x (`WindowX`) | -3.045 (outer) | -3.60 (inner face), reveal to -3.76 | m |
| June's window centre y (`WindowZ` less `FrontZ`) | 3.00 | 1.50 | m |
| Sill (`SillY`), half width, spring, apex | 2.50, 0.50, 0.95, 1.45 | 4.00, 0.50, 0.95, 1.45 | m |
| Alcove y range (`AlcoveMinZ` / `AlcoveMaxZ`) | 2.30 / 3.70 | 0.75 / 2.25 | m |
| Alcove eave / ridge height | 3.90 / 4.50 | 5.80 / 6.40 | m |
| Alcove depth into the room (at its eave / ridge) | to x -1.99 / -1.49 | to x -2.89 / -2.17 | m |
| Window arrival point (Pip appears, facing +x) | – | (-2.50, 1.50, 4.00) | m |
| Window exit zone (feet) | – | x < -3.15, abs(y − 1.5) < 0.50, z 3.7…5.0 | m |
| Attic entry zone (`InAtticEntry`) | x -3.10…-1.96, abs(y − 3.0) < 0.70 | x -3.65…-2.20, abs(y − 1.5) < 0.75 | m |
| Window view box (blocker at x = -3.76) | – | x -5.20…-3.76, y 0.65…2.35, z 3.97…5.90 | m |
| `SillSnag` pivot | (-2.83, 3.47, 2.92) | (-3.57, 1.97, 4.42) | m |
| Sill use point | (-2.83, 3.47, 2.50) | (-3.40, 1.97, 4.00) | m |

On the exterior nothing moved: the door plane, the `FrontDoor` hinge, `SideWindowPane` at (-3.045, 0.50, 2.50) in
model space and the 2.50 m sill are as they were. The outer view boxes are 0.88 m (door) and 0.73 m (window) deep.
Trigger the move to the interior at the threshold, and keep the existing door blocker and a blocker 0.3 m inside the
window, so Pip never walks into a view box.

### 3.3 Stair

| Anchor | Old | New | Units |
|---|---|---|---|
| Stair bottom (approach point on the hall floor) | (-1.86, 5.36, 0.60) | (-2.85, 3.05, 0.60) | m |
| Lower flight | along the back wall, rising east | x -3.60…-2.10, y 3.26 → 5.50, z 0.60 → 2.30 (rising north) | m |
| Landing | – | x -3.60…-2.10, y 5.50…7.00, z 2.30 | m |
| Upper flight | – | y 5.60…7.00, x -2.10 → 0.14, z 2.30 → 4.00 (rising east) | m |
| Stair top (first point on the attic floor) | (0.85, 5.36, 2.50) | (0.40, 6.30, 4.00) | m |
| Width | 1.00 | 1.50 lower, 1.40 upper | m |
| Rise / tread | 0.19 / 0.28 | 0.1889 / 0.28 (34°) | m |
| Stairwell in the loft | x -1.44…0.80, y 4.86…5.86 | x -3.60…-2.10, y 3.10…7.00 and x -2.10…0.14, y 5.60…7.00 | m |
| Loft rails (0.95 m posts) | front edge y 1.58; hole south and west | x = -2.04 for y 3.10…5.50; y = 3.04 for x -3.60…-2.10; y = 5.55 for x -2.10…0.14 | m |
| Stair wall (floor to loft, rail above) | stringer only | y 5.50…5.60, x -2.10…0.14 | m |
| Stair end wall (hall side) | – | x = 0.14, y 5.50…7.00, z 0.60…3.86 | m |
| Lower flight balustrade | – | x = -2.12, y 3.19…5.50, 0.95 m above the nosings | m |
| Post at the turn | – | (-2.08, 5.55), z 0.60…5.00 | m |

### 3.4 Workshop

| Anchor | Old | New | Units |
|---|---|---|---|
| Partition x (`NookX`) | 1.45 | 2.20 | m |
| Nook y range (`NookMinZ` less `FrontZ` to the back wall) | 1.65…5.86 | 2.40…7.00 | m |
| Nook floor (`NookFloorY`) | 0.10 | 0.10 | m |
| `WorkshopMechanism` placement (`MechanismX`, y up, `MechanismZ` less `FrontZ`), yaw | (1.45, 0.10, 3.10), 90° | (2.20, 0.10, 4.60), 90° | m |
| Grille opening | y 2.25…3.95, z 0.68…1.10 | y 3.75…5.45, z 0.68…1.10 | m |
| Grille use point | (1.40, 3.75, 0.60) | (2.15, 5.25, 0.60) | m |
| Service panel opening | y 2.45…3.35, z 1.18…2.05 | y 3.95…4.85, z 1.18…2.05 | m |
| Service panel use point | (1.40, 2.90, 0.60) | (2.15, 4.40, 0.60) | m |
| Link arm use point | (1.40, 2.55, 0.60) | (2.15, 4.05, 0.60) | m |
| Crank use point | (1.40, 3.15, 0.60) | (2.15, 4.65, 0.60) | m |
| Crank pivot (house frame) | (1.65, 3.10, 1.48) | (2.40, 4.60, 1.48) | m |
| Cam pivot (house frame) | (2.13, 3.10, 0.62) | (2.88, 4.60, 0.62) | m |
| Cutaway camera (with `NookWall` hidden), eye → look-at | – | (0.30, 4.90, 2.10) → (2.90, 4.50, 1.50) | m |

The use points keep their old offsets from `MechanismZ` and `NookX − 0.05`, so only those two constants change. The
mechanism model itself is the same file. Its shaft and push rod now carry on up through the nook's ceiling as part
of the shell.

### 3.5 June

| Anchor | Old | New | Units |
|---|---|---|---|
| Stool (`JuneAttic`), seat 0.30 m | (-1.75, 4.05, 2.50), yaw -133° | (-1.10, 2.75, 4.00), yaw 35° (toward the stair top) | m |
| Talk approach side | – | from the east or north; the rug centre is (-0.10, 2.85) | m |
| Foyer mark (`JuneFoyer`) | (-1.35, 1.20, 0.60), yaw 150° | same | m |
| Walk, stool to foyer mark | 8 points along the back-wall stair | (-1.10, 2.75, 4.00) → (-0.30, 4.60, 4.00) → (0.55, 5.30, 4.00) → (0.60, 6.30, 4.00) → (0.14, 6.30, 4.00) → (-2.10, 6.30, 2.30) → (-2.85, 6.30, 2.30) → (-2.85, 5.50, 2.30) → (-2.85, 3.26, 0.60) → (-2.85, 2.60, 0.60) → (-1.35, 1.20, 0.60) | m |

### 3.6 Children and visibility

| Child | Old rule | New rule |
|---|---|---|
| `Attic` (interior) | hidden while Pip is in the foyer with a high camera | always shown: it is the attic's walls, roof and dressing. The loft slab itself is in the root mesh. |
| `NookWall` (interior) | hidden for the crank cutaway | same |
| `InnerDoor`, `DoorLatch` (interior) | swing with the exterior `FrontDoor` | swing alone; `InnerDoor` now has an outer face |
| `StreetView_Glow` (interior) | – | always shown; default glow level |
| `OwlHouseBody`, `Porch` (exterior) | body hidden while Pip is inside | never hidden |
| `DoorRecessCard` (exterior) | hidden whenever the interior shows | no effect either way |
| `SideWindowPane`, `SideWindow_Glow` (exterior) | hidden to open June's window | same; the painted attic is behind it |
| `RoomCard_Glow` (exterior) | – | always shown; default glow level (1.5) |

### 3.7 Furniture that needs a collider

| Piece | Box (x, y ranges; top z) | Units |
|---|---|---|
| Hall bench and coat rack | x 0.45…1.95, y 6.59…7.00; seat 1.05, rack to 2.35 | m |
| Hall console (east wall) | x 3.24…3.60, y 0.80…1.80; top 1.35 | m |
| Blanket chest (west wall) | x -3.50…-3.02, y 1.17…2.03; top 1.10 | m |
| Long-case clock | x 2.67…3.13, y 2.12…2.40; top 2.70 | m |
| Hall crate | x -1.87…-1.33, y 4.97…5.43; top 0.96 | m |
| Umbrella stand | centre (-0.95, 0.42), radius 0.14; top 1.12 | m |
| Attic craft table and stool | x 2.10…3.16, y 2.35…3.45; top 4.47 | m |
| Attic trunks (east) | x 2.74…3.26, y 4.27…5.23, top 4.80; x 2.84…3.26, y 5.50…6.20, top 4.50 | m |
| Attic trunk and hat boxes (front) | x -2.30…-0.35, y 0.25…0.78; top 4.55 | m |
| Attic crates (front east) | x 2.05…3.25, y 0.30…1.02; top 4.72 | m |
| Attic pumpkin (front) | centre (1.60, 0.55), radius 0.20; top 4.32 | m |
| June's stool | centre (-1.10, 2.75), radius 0.17; seat 4.30 | m |
| Attic bookshelf | x 1.27…2.53, y 6.69…7.00; top 5.34 | m |

### 3.8 Treehouse cabin

Cabin frame: the platform centre in the maple frame (`routes.PLAT`), turned by the maple's yaw (200°); +y is the
yard side, -x is the door side toward the shed roof; z is height above the 2.4 m deck.

| Anchor | Old | New | Units |
|---|---|---|---|
| Wall lines (`CabinHalf`) | ±1.12 on all four sides | x ±1.12, y -1.12 (north), y +1.75 (bay outer wall) | m |
| Yard side | 0.72 m parapet at y +1.12, open above | no wall at y +1.12; bay side walls x ±1.12 for y 1.12…1.75; outer wall at y 1.75 with a window x -0.78…0.78, z 0.72…1.24 | m |
| Floor | platform deck only (2.68 by 2.64) | deck plus bay boards y 1.28…1.81, x ±1.12, top at the deck (walkable to y 1.72) | m |
| Eave (wall top) | 1.45 | 1.70 | m |
| Roof | ridge slot at 2.08, open | closed at 2.33; collar x -0.45…0.97, y ±0.30 | m |
| Bay roof | – | 1.36 at the outer wall, rising to 1.74 at y 1.12 | m |
| Clear height inside | 1.45 at the walls, open above | 1.30 at the bay's outer wall, 1.70 at the main walls, 2.28 under the ridge boards | m |
| Door (east wall, x -1.12) | y -1.02…-0.12, 1.30 high | y -1.02…-0.12, 1.45 high | m |
| Windows (north and west walls) | 0.60 wide, z 0.62…1.12 | same | m |
| Overall bounds, model space | (-1.39, -1.57, 2.40) … (1.71, 1.47, 4.64) | (-1.39, -1.57, 1.78) … (1.71, 1.99, 4.89) | m |
| Placement | maple frame, yaw 200° | same | – |

## 4. Review

Rendered by `tools/blender/env/env_house_v3_review.py` from the re-imported FBX files on their baked textures, with
back-face culling on and glow levels as in `GlowLevels.cs`. Frames are in `docs/evidence/s002/house_v3/`. The
figure in the interior frames is a 1.1 m stand-in for Pip; the camera is 2.4 m behind her at 18°, as a gameplay
boom would be, unless noted.

| Frame | What it shows |
|---|---|
| `int_hall_from_door.jpg` | The hall from the door: stair and balustrade, the stair wall with three frames, the coat alcove, the partition with the grille and service panel. |
| `int_hall_to_door.jpg` | Back toward the door: both street windows with their night views, the wall lamps, the latch. |
| `int_hall_to_nook.jpg`, `int_hall_to_stair.jpg` | The east and west sides of the hall (boom 2.0 m toward the stair). |
| `int_stair_lower.jpg`, `int_stair_upper.jpg` | On each flight (boom 2.0 m on the upper one). |
| `int_attic_june.jpg` | The attic toward June's stool and the dormer, with the painted yard beyond the window. |
| `int_attic_to_front.jpg`, `int_attic_to_back.jpg`, `int_attic_dormer.jpg` | The round window and owl lantern; the stair rail, bookshelf, trunks and craft table; the alcove. |
| `int_door_open.jpg` | The inner door swung 100°: porch boards and the painted street, no void. |
| `int_nook_cutaway.jpg` | The cutaway with `NookWall` hidden. |
| `int_plan_hall.jpg`, `int_plan_attic.jpg` | Plans, cut 0.25 m under the ceiling and 2.3 m above the attic floor. |
| `int_section_south.jpg`, `int_section_west.jpg` | Sections cut at y = 3.0 and x = -2.9. |
| `int_outside_shell.jpg` | The shell from outside: only the view boxes stand out from it. |
| `ext_three_quarter.jpg`, `ext_front.jpg` | The exterior, same silhouette, with the lit window cards; the attic window stays the brightest. |
| `ext_door_open.jpg`, `ext_dormer_open.jpg` | The front door open and June's window open: the painted hall and attic. |
| `cab_outside.jpg`, `cab_west.jpg` | The cabin from the shed roof and from the yard side (the bay). |
| `cab_inside_bay.jpg`, `cab_inside_door.jpg`, `cab_inside_window.jpg` | Inside the cabin with a 0.75–1.2 m boom. |
| `before_*.jpg` | The old cabin, for comparison. |

My reading of the frames: hall 4/5, attic 4/5, stair 3.5/5 (plain walls on the upper flight), nook unchanged,
cabin 3.5/5 inside (it is a small room round a trunk). No frame shows the void or a missing face.

## 5. What is not proven here

- No Unity import, placement, collider, camera or device check has run. These are Blender re-import renders only.
- Texel density of the interior fell from 87 to 66 px/m, because one 2048 atlas now covers 685 m². It matches the
  exterior (69 px/m), but the camera is now closer to the walls. If it reads soft on the phone, split the attic
  into its own model and atlas.
- The painted views are flat cards 0.22 m behind the windows and 1.3–1.4 m beyond the two thresholds. They have no
  parallax; that is the look D24.1 asked for, and it is untested in motion.
- The glow children take the default level (1.5). `RoomCard` and `StreetView` may want their own entries in
  `GlowLevels` after the first in-game frame.
- The cabin cannot hold a 2 m boom. It was closed and raised as far as the maple allows.
- The cabin's bay adds 0.45 m of floor beyond the platform's collider, and the old parapet collider must go.
- The exterior's front door swings into the hall view box; at 100° the leaf passes through the box's west side and
  is hidden behind it. A swing of 90° or less keeps it fully in view.
- The cabin's ridge trim and gable stars reach 2.49 m over the deck, 0.09 m into the air the maple keeps free. No bough
  crosses them in the review frames, but check `cab_outside.jpg` against the in-game tree.
- This branch must not be merged before the gameplay pass: the game still places `OwlHouseInterior` inside the
  exterior and builds its colliders from the old constants.

## 6. Checks

`./tools/check-local.sh`: all local checks passed (`dotnet test tools/core-tests`: 351 passed). No Unity, device or
CI run is claimed.
