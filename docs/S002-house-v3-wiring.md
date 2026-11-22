# S002 house v3 wiring: the owl house is bigger on the inside

Branch `feature/house-v3-wiring` (from `main` 7de967e, with the art branch `feature/house-v3` 96e3c42 merged in).
It wires the house v3 models into the game, following D24 and D24.1:

- The exterior keeps street scale and stays whole and solid.
- The rooms are a separate, closed interior in the same scene, away from the street.
- A short paper page-turn plays at the two thresholds: June's dormer window and the front door.
- The treehouse cabin stays seamless on the maple, now closed.

## 1. Where things stand

| Piece | Placement | Notes |
|---|---|---|
| `OwlHouse` (exterior) | unchanged (0, 0, 13) | Solid again: EnvironmentDressing always builds the body and roof boxes. `OwlHouseBody` is never hidden; `SideWindowPane` stays hidden (June's window is open onto its painted attic). |
| `OwlHouseInterior` | origin (0, 0, 60), yaw 0 | 42 m beyond the north boundary wall (z 18). World = (x, z_b, 58 + y_b) for builder (x, y_b, z_b): `CaseRouteLayout.InteriorFrontZ` = 58. |
| `WorkshopMechanism` | (2.20, 0.10, 62.60), yaw 90 | House v3 §3.4. |

**Interior visibility.**
- The interior and the mechanism (and their six lamps) are drawn only while Pip or the camera is in the interior.
- June's renderers hide with it while she is indoors, so nothing ever floats beyond the street.
- The interior is never cut away: the old `InteriorVisibility` rule (hide the body, hide the attic from a high lens)
  is gone. The `Attic` child is always shown.
- A ShadowsOnly box round the interior (`InteriorMoonShroud`) keeps the moonlight out of the closed rooms. Their walls
  are single inward faces, which cast no shadow.

## 2. Thresholds (`Core/World/Thresholds.cs`, `Runtime/World/ThresholdTravel.cs`, `ThresholdWipeView.cs`)

| End | Exit zone (Pip's feet) | Arrival (feet, yaw) |
|---|---|---|
| Dormer, outside | the sill against the window: x −3.42…−2.9, z 13.55…14.45 (the body stops Pip at x −3.33) | on the bridge (−3.95, 2.49, 14.0), facing −X |
| Dormer, inside | the window's reveal: x −3.95…−3.2, depth 1.0…2.0 (the view-box stop holds Pip at x −3.53) | attic (−1.70, 4.00, depth 1.50), facing +X |
| Front door, outside | the doorway from the porch: \|x\| < 0.5, z 10.58…11.3 (the body stops Pip at z 10.72) | porch (0, 0.60, 9.45), facing −Z |
| Front door, inside | the doorway from the hall: \|x\| < 0.5, depth −0.45…0.30 (the view-box stop holds Pip at depth −0.06) | hall (0, 0.60, depth 2.00), facing +Z |

- The front door works only once the latch is lifted (`doorOpen`), both ways. Before that, the outside door is still
  stuck (Use `TryFrontDoor`) and the inner leaf blocks the doorway.
- **Arrival points.** They sit a little further in than the house v3 table: 0.8 m in the attic, 0.7 m in the hall,
  0.3 m on the porch. That way the camera behind Pip has its boom from the first frame. Every arrival point is at
  least 0.5 m from every exit zone.
- **No double trigger.** `ThresholdTracker` fires only when Pip walks into a usable zone while armed. After a crossing
  (or any reset: new game, resume, replay) it re-arms only once Pip has been at least 0.25 m clear of every zone for
  0.5 s.
- **Facts.** They are unchanged. The dormer arrival stands in `InAtticEntry`, so `EnterAttic` (`enteredByWindow`, and
  `atticFirst` when no witness spoke) fires on the first frame after the page. The hall arrival stands in `InFoyer`.

**The page-turn** (`ThresholdWipe`, about 0.5 s):

- A deckled bone-paper page with its purple running stitch and a hard ink shadow turns over the screen from the right
  (0.2 s). Its lifting edge catches the light, a crease and a soft fold shade run behind it, and it casts a shadow on
  the world ahead of it. The place's puppet stamp sits in a stitched ring in the middle: June's window or the open door.
- While the page covers the screen (at least 0.1 s and two frames), Pip moves to the arrival point and the camera
  cuts: the orbit snaps behind Pip, the interior solver runs at once, and Cinemachine forgets the previous frame.
- Then the page turns on to the left and uncovers the new place (0.22 s).
- Gameplay input is off and the HUD is hidden for the whole crossing, and the page swallows taps. The canvas sorts
  under the HUD and pause menu, so a pause still shows.
- Reduced motion shows the same page as a 0.12 s fade in and out.
- Any teleport over 6 m (a resume, a replay, a test) also cuts the camera, so the lens never travels through the
  void between the street and the interior.

## 3. Constants moved to the new anchors

- **`CaseRouteLayout`.** The interior colliders follow house v3 §3:
  - the hall, the loft, both stair flights and the landing;
  - the stair side, wall, end wall and post, and the loft rails;
  - the workshop partition;
  - the shell, with the door and window openings and their view-box stops;
  - the roof plates and the dormer;
  - all 14 furniture boxes of §3.7.

  The exterior body is listed as reference slabs for the route checks. The mandatory route is now three legs
  (`MandatoryLegs`): street to June's window, attic to the front door, porch.
- **Cabin.** The walls are closed on all four sides, with the door gap and the lookout bay's side and outer walls.
  The bay boards are floor (`Cabin_BayFloor`), and roof plates stop jumps. The old `Cabin_Parapet` is gone.
- **`CaseStage`.**
  - June's stool is at (−1.10, 4.00, depth 2.75), yaw 35. Her walk is the 11-point path of §3.5. The hall mark is
    unchanged in builder space.
  - The sill snag target is on the inner jamb (Inside only).
  - The latch, grille, panel, link arm and crank are at their v3 places.
  - The anchors (Attic, Foyer, Workshop) are in the interior, all clear of the exit zones.
  - `NearestAnchor` keeps interior saves inside.
- **Cutaway.** The camera runs from builder (0.30, 4.90, 2.10) to (2.90, 4.50, 1.50) with `NookWall` hidden. The owl
  inset still renders the rooftop owl on the street.
- **Hints.** The bat trail and goal sticker lead to the threshold on Pip's side when the goal is on the other side:
  the open front door, or June's window while the door is latched (`Thresholds.WayTo`).
- **Glow.** `GlowLevels.RoomCard` = 1.2 (the lit window cards outside) and `StreetView` = 0.85 (the painted night
  views inside). The attic window (4.0) stays the brightest.
- **Memory replay.** It only stages exterior places (the bridge and June's window from outside), so only names
  changed.
- **Doors.** The exterior leaf opens at most 90° (art note). The inner leaf opens 100°.

## 4. The camera (`Core/World/InteriorCamera.cs`)

| Room | Boom | Pitch | Shortest |
|---|---|---|---|
| Hall | 2.4 m | −2…30° | 1.3 m |
| Stair | 2.2 m | 8…55° | 1.3 m |
| Attic, alcove | 2.2 m | −4…40° | 1.3 m |
| Cabin | 1.2 m | −6…45° | 1.0 m (a swing past 90° costs less there) |

- **Air.** Air is the hall under its beams, the stair bay and the upper flight's well above the treads, the attic
  under its roof (0.55 m clear of the slopes, up to 6.4 m), the alcove, and the cabin inside its walls and roof. The
  doorway and the window's reveal count as air too, so a shoulder stepping into a threshold still has a boom back into
  the room.
- **Solids.** The solids are every interior and cabin slab, the inner leaf (latched or swung open), and the dressing
  without colliders: the hall bunting, bat garland, star lamp and door lamps, the drive shaft and push rod, the
  lanterns, the cabin's trunk, seat and bay lamp.
- **The void.** A lens in a room's air sees only that room's inner faces, so it never shows the void.

## 5. Tests

- **EditMode Core** (`dotnet test tools/core-tests`: 392 passed).
  - `ThresholdTests` (11):
    - pairing;
    - arrival points outside every zone, on floors and clear;
    - every zone reachable on foot;
    - the door only once open;
    - no double trigger, and hysteresis;
    - a reset inside a zone;
    - hint routing;
    - the wipe's single move under full cover, its length, the reduced-motion fade and slow frames.
  - `CaseRouteTests`:
    - every leg walkable and ending in its zone, with the next leg at the paired arrival;
    - the latched door;
    - the window on both sides;
    - the interior apart from the street;
    - the v3 colliders;
    - the closed cabin with its bay;
    - interior saves restore inside.
  - `InteriorCameraTests`: a sweep of 7,288 shots over the hall, attic, stair and cabin at 8 yaws, with the door shut
    and open.
    - On the open floor of the hall and attic (1 m from any wall, rail or furniture), every shot gets at least 1.8 m.
    - Against walls, on the stair and in the alcove, every shot gets at least 1.3 m.
    - In the cabin, every shot gets at least 1.0 m.

    It also covers:
    - the door shot;
    - a threshold step;
    - the alcove;
    - June in view;
    - the probe;
    - the ceilings and beams;
    - the cabin profile.
- **PlayMode `CaseInteriorTests`.**
  - The mandatory route is walked leg by leg with the real colliders, through both thresholds.
  - CASE-01 runs through both thresholds.
  - CASE-02 runs attic first.
  - SAVE-01 reloads inside the interior with the camera in the room.
  - A threshold round trip covers: window in and out, the stuck door, the door both ways, and reduced motion.
- **PlayMode `InteriorFramingTests`.** The sightline, framing and room-air checks run at 20 places in the new rooms
  and the cabin, against the real meshes.
- **Visual `InteriorCaptureTests`.** The frames are 18, 19, 23, 29, 20, 31 (mid page-turn), 30 (exterior cards),
  32 (cabin) and 21.

## Evidence

- **Local checks.** `./tools/check-local.sh` passed. `dotnet test tools/core-tests`: 392 passed.
- **CI run 85** (`908ea17`). It compiled. EditMode 415/415, PlayMode 66/67, visual 6/6.
  - The route walk stopped 0.1 m short of the dormer zone (the test's 0.12 m arrival tolerance).
  - `31_threshold_page` showed only Pip's cape: with Pip in the doorway the shoulder was outside the room's air, so
    the boom collapsed. Fix: the doorway and the window's reveal count as camera air, and the latched leaf is now a
    camera solid.
- **CI runs 86 and 87.**
  - The route walk still missed a zone. Pip's capsule skin stops it about 0.1 m before the body. The outside dormer
    zone now also covers the bridge end (arrival moved out to x −4.1), and the route walks deeper into the doorway.
  - `32_cabin_inside` showed a black band where the roof is. A warm cabin lamp now lights the closed roof, so it reads
    as boards.
  - The page got an inked cut edge and a warmer bone tint.
- **CI run 88** (`16f3b15`, run id 45066297319): **green.** EditMode 416/416, PlayMode 67/67 (`CaseInteriorTests` 7/7,
  `InteriorFramingTests` 3/3, `CollisionTests` 5/5), visual 6/6.

**Frames** (`docs/evidence/s002/*_run88.jpg`, reviewed by eye):

- `18_treehouse_route`: unchanged route. The closed cabin with its bay sits on the maple.
- `19_attic_june`:
  - June sits on her stool beside Pip on the star rug.
  - The round window and the owl lantern are behind them, and the alcove arch and stair rail on the right.
  - The roof slopes and garlands close the top.
  - Nothing is cut away and no void shows.
- `23_hall_door`: the lens is in the hall, behind Pip. Both street windows and the open door show their painted night
  views. The inner leaf is swung open, and the lamps and the frieze are in frame. The hum caption is on two lines.
- `29_stair`: Pip on the lower flight, seen from behind and above. The bat cut-outs are on the wall, and the balustrade
  and the framed pictures are on the stair wall. No void.
- `20_workshop_cutaway`:
  - The nook from the hall, with `NookWall` hidden.
  - The cam, the striped push rod, the crank and Kit's photo are in frame.
  - The inset shows the rooftop owl on the street.
  - The nook is dim at the top.
- `31_threshold_page`:
  - Mid page-turn on the way out.
  - Pip stands in the doorway against the painted porch.
  - The bone page comes in from the right, tilted, with a stitched edge, a highlight on the curl and a cast shadow.
  - The open-door stamp is in a stitched ring.

  It reads as paper (about 4/5).
- `30_exterior_cards`:
  - The exterior at street scale. Warm cards glow in the ground-floor windows, and the door is open on its painted
    hall.
  - The round attic window stays clearly the brightest point.
  - Pip is on the porch.
- `32_cabin_inside`: inside the closed cabin with the short boom. The lit roof boards, the bay's lookout window and its
  lamp are in frame, with the trunk on the right.
- `21_finale`: the arrival sightline. June and Kit are on the porch, and there is no leftover caption.

**Not run:** device. No device or TestFlight pass is claimed.

## Remaining gaps

- **Arrival points.** They sit 0.3–0.8 m further in than the house v3 table, so the camera has its boom at once.
  Pip appears a step inside the room, not on the threshold.
- **Cabin framing.** The cabin's 1.0–1.2 m boom frames Pip almost full height. Corners near the trunk may swing the
  camera past 90° (cheaper there by design).
- **Drawing cost.** The street keeps drawing while Pip is in the interior; nothing culls it but distance and the
  walls. If the frame budget needs it, hide the street while inside.
- **Workshop lighting.** The nook is dim at the top of the cutaway frame. A brighter nook lamp would help.
- **The page in captures.** It looks a little grey in captures: they render the UI through the camera's colour
  grading. On a device the page is an overlay.
- **Device.** Not run. The page-turn's timing and feel need a phone.
