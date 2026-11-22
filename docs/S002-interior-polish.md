# S002 (part 4): interior polish

Branch `fix/interior-polish`. It fixes the problems found in the CI frames of case 01 (run 65): the indoor camera
looking through the house, a cut-off caption, a wrong direction cue, a leftover caption in the finale, and June
gliding on Idle. It applies D24 ("the camera stays inside the room it is in").

## 1. The indoor camera stays inside the room

**Before (run 65).**
- `23_foyer_door`: the boom sat on the stair, about 8 cm under the hall joists. The loft underside and the stair rail
  filled the top third of the frame.
- `19_attic_june`: the camera was outside June's window, so the arch hid her.

**Approach.** An interior camera profile per room, plus a lens that never touches or looks through anything solid.

- **`Core/World/InteriorCamera` (engine-free).**
  - *Air:* the house's air is the hall under the loft, the entry bay, the stairwell and the attic under its roof slope.
    The workshop nook is not air, and the air stays 0.3 m clear of the rafters.
  - *Solids:*
    - every shell and interior collider slab;
    - what has no collider: the joists, fascia and bunting under the loft, the closed side of the stair and its
      balustrade and newel, the owl and star lanterns and paper stars, the drive shafts, June's stool and the hall
      dressing;
    - the inner door leaf, once it swings open.
  - *Profiles:*

    | Room | Boom | Shoulder | Pitch |
    |---|---|---|---|
    | Hall | 2.4 m | 0.1 m | −4…9° (under the 2.15 m joists) |
    | Stair | 2.2 m | — | 10…45° (over the balustrade) |
    | Attic and alcove | 2.2 m | — | 2…40° |
  - *`Solve`:* keeps the player's orbit when the full boom fits. Otherwise it searches pitch, then a lower shoulder,
    then a yaw swing (up to 180° in the alcove), for the longest boom whose lens is in clear air and sees Pip's head
    and chest. It also keeps June in view when she is the one Pip is talking to, or about to. Candidates are tried
    best-first and pruned. On desktop a solve averages 0.2 ms (worst 2.5 ms) and a probe takes 0.05 ms.
- **`Runtime/Case/InteriorFraming`.**
  - Runs the solve a few times a second, and whenever Pip, the orbit or the talk partner changes.
  - Runs the probe every frame against the framing actually on screen. It pulls the boom in at once when anything
    solid would come between the lens and Pip, and eases it out at 2.5 m/s.
  - `CameraRig` takes the room's pitch, shoulder, yaw swing and boom limit, all eased. Indoors it uses short follow
    damping.
  - Outside the house everything is handed back.

**Tests.**
- EditMode `InteriorCameraTests`: every standing place in the hall, attic and stair (0.3–0.4 m grid), at 8 yaws, with
  the door shut and open, gets a clear boom of at least 1.3 m. It also covers the 23 and 19 poses, the alcove, June in
  view, the probe and the solids.
- PlayMode `InteriorFramingTests`: at 14 places in the hall, attic, alcove and on the stair, nothing solid lies between
  the lens and Pip's head and chest. "Solid" means every collider plus every drawn interior and mechanism mesh, given
  temporary mesh colliders for the check. Pip's feet-to-head height fills 20–95 % of the frame. June stays visible
  when she is the offered target, and the camera is handed back outside.

## 2. Captions wrap to two lines

- **Cause.** The strip was a fixed 92 units tall, with best-fit text and vertical truncation. Andika's line box is
  1.61 em, so a second line never fit, and "[Behind the grille, a wooden piece lifts three" lost its end.
- **Fix.** `CaptionStrip` measures the text with the real font. It keeps the default size if two lines suffice,
  steps down toward 13 pt only when a third line would be needed, and grows the strip to the lines' height. The strip
  grows upward from its bottom edge, so a raised caption stays above the speech bubble (`RaisedHeight`, from the
  non-verbal UI).
- **Shortened texts.** Keys are kept; the translator context now gives the two-line budget.

  | Key | New text |
  |---|---|
  | `c01.caption.grille` | "[A wooden piece lifts three times behind the owl panel.]" |
  | `c01.caption.crank_gap` | "[Tap. Nothing else moves: the arm isn't connected.]" |
  | `c01.hint.find_way_in.observe` | "The hedge hides the side of the house. Could someone climb in?" |
  | `c01.hint.open_door.locate` | "The wooden latch is on the inside of the front door." |
- **Test.** PlayMode `CaptionStripTests` checks every caption, hint and note, plus each direction word, at strip widths
  for 1334 units (the canvas reference, the narrowest strip) and 1443 units (a notched iPhone's safe area): at most two
  lines at 13 pt or larger. It also checks that a shown caption is never cut.

## 3. The direction cue is only for directional sounds

- **Before.** The workshop captions carried their source, so the finale frame read
  "[Click. The striped arm is back in place.] (ahead)".
- **Now.**
  - `CaptionDirection.IsDirectionalSound` covers only the arrival knocks and the hall hum.
  - `ShouldCue` also needs the source at least 1.5 m from Pip.
  - `CaseDirector` passes a source only for those keys, and the strip checks the distance from Pip.
- **Tests.**
  - EditMode `CaptionCueTests`: the rules, and ahead/left/right/behind for several camera headings.
  - PlayMode cue cases.
  - The canonical case test asserts no cue on the arm caption.

## 4. The finale starts clean

`StartFinale` clears the caption queue, so no caption from the workshop carries over. The canonical case test asserts
that nothing shows when the finale starts. The reopening's own lines still play after the intro.

## 5. June's walk

- She plays `CharacterClips.Walk` while she moves, if her model has it, and Idle otherwise, with no error when it is
  missing. Walk cross-fades in and out over 0.2 s, because frame 0 is mid-stride.
- She first turns toward the path for 0.4 s, eases in over 0.7 m and out over 0.9 m, and faces a point 0.45 m ahead,
  so she turns into corners.
- The timing is in floor-plan metres at 1.1 m/s, the clip's pace. On the 37° stair that is about 1.3 m/s along the
  slope. Walk playback is scaled by speed / 1.1.
- **Tests.** EditMode `JuneWalkTests`; the canonical case test checks that Walk plays at its authored pace mid-walk.

## Evidence

- **Local checks.** `./tools/check-local.sh` passed. `dotnet test tools/core-tests`: 376 passed, after merging main,
  which adds the non-verbal UI and June's walk clip.
- **CI run 74** (`1743cb3`). The run was cancelled: the visual step's editor hung during startup before any test ran.
  - EditMode: 330/330.
  - PlayMode: 56/58. Two test-side failures:
    - Pip's screen share was measured from skinned bounds;
    - June's focus test assumed she was the offered target in the alcove, where the sill wins.
- **CI run 82** (`09698f1`, after merging main): green. EditMode 400/400, PlayMode 67/67, visual 6/6. The frames
  showed two problems:
  - `24_stair` looked back down at Pip from the stair top, with the attic cut away so the street showed through;
  - the attic hid whenever the lens was high.
- **Fixes (`585a284`):**
  - A swing past 90° (the lens facing Pip, with the stick's directions flipped) costs extra. On the stair the lens now
    looks down from high behind (pitch up to 66°).
  - With the lens inside the house, the attic is never cut away (D24).
  - The stair frame is renamed `29_stair`; main now has a `24_bea_vignette`.
- **CI run 83** (`585a284`, run id 45053552464): **green.** EditMode 400/400, PlayMode 67/67 (`InteriorFramingTests`
  3/3, `CaptionStripTests` 3/3, `CaseInteriorTests` 6/6), visual 6/6.

**Frames** (`docs/evidence/s002/*_run83.jpg`, compared with `*_run65.jpg`):

- `23_foyer_door`:
  - *Before:* the lens sat on the stair under the joists. Beams and the stair rail filled the top third and the left of
    the frame, and they cut the caption.
  - *After:* the lens is in the hall at 1.84 m. Pip is centred in front of the open door, with the porch, bunting,
    windows and broom in view. The joists stay at the top as the ceiling, never between the lens and Pip. The hum
    caption wraps to two whole lines and says "(to the left)", toward the workshop.
- `19_attic_june`:
  - *Before:* the lens was outside June's window, and the arch hid June.
  - *After:* the lens is in the attic. Pip stands in the dormer, framed by the arch behind him, and June sits on her
    stool on the right, in front of the rail, with nothing between.
- `29_stair` (new): Pip halfway up the stair, seen from high behind over the balustrade. The treads, the framed
  pictures and the attic floor are in view; there is no void and no cut-away.
- `20_workshop_cutaway`: unchanged shot. The caption on screen ("[Click. The striped arm is back in place.]") has no
  direction cue. The long grille caption is covered by `CaptionStripTests`.
- `21_finale`:
  - *Before:* a workshop caption with "(ahead)" sat over the porch.
  - *After:* the shot is clean, with no leftover caption.

**Not run:** device. No device pass is claimed.

## Remaining gaps

- **Bottom of the stair and the alcove.** The camera can only look back at Pip there, so the stick's directions flip.
  - The first metre of the stair runs under the low loft and behind the balustrade.
  - In the dormer alcove, the window is behind Pip.

  Both are short; walking straight through, the camera only wobbles. A widened stair or alcove (D24) would remove them.
- **19 composition.** June is half cut by the right edge, and her face washes out under the attic lamp. The solver only
  keeps her in clear view when she is the offered talk target; from the alcove the sill snag wins. Keeping her fully
  in frame would need a framing term, not just a clear line.
- **The hall ceiling.** The joists still show in the top quarter of hall frames: the hall is 1.74 m clear under the
  loft. D24's taller rooms would fix this.
- **Mesh colliders.** The PlayMode sightline check gives the interior meshes temporary mesh colliders in the editor.
  On a device, only the Core model of the solids protects the lens. It is checked by the EditMode sweep, and by the
  PlayMode check against the real meshes at 14 places.
- **June's walk.** Her model has the Walk clip: run 83 logs `June Idle,KneeTap,Point,Talk,Walk,Wave`. The canonical
  case test passed its check that Walk plays at about its authored speed mid-walk. No frame shows her walking yet.
