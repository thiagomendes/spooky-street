# S002 (part 2): exterior wiring

Branch `feature/s002-wiring`. This pass connects the engine-free case core (`docs/S002-case-core.md`) to the world for
the **exterior half** of case 01. It adds the dialogue card, the caption strip, the notebook, the interaction marker
and save/resume. Interior beats (attic, stair, foyer, workshop, crank) are not staged yet; their assets are still being
made.

## Engine-free additions (`Core/Case/CaseStage.cs`)

- **`CaseStage`:** the exterior Use targets (`InteractionPoint`), the staged safe anchors and the trigger zones. Every
  position comes from `EnvironmentLayout`.
- **`UseTargetPicker`:** picks a target within reach and at a similar height, weighted toward the camera centre
  (spec §5).
- **`CaptionDirection`:** the signed angle from the camera to a sound, its bearing and the localized direction word.
- **`DialogueAdvanceGate`:** a line shows for at least 0.25 s. A button press advances only if the button was seen
  released after the line appeared, so holding Use never skips a line.
- **`CaseSpeakers`, `CaseNotebookTags`:** speaker name keys and the short labels on the notebook slips.
- **Keys:** `LocalizationKeys.CaseUi`, in a new partial file. Every key is in en.json with translator context.
  `validate_project.py` now reads every `LocalizationKeys*.cs` file.

## How each exterior beat is triggered

| Beat | Trigger | World reaction |
|---|---|---|
| HearKnocks | After 0.8 m of walking, or when Pip is north of z = -11.2 (walking straight past, or resuming at the plaza). Plays once. | Three amber pulses at the attic window (short, short, pause, last) that settle warmer. A paper bat falls from the owl-house porch eave to the deck. Caption `[Three knocks from the blue house] (ahead)` with a purple disc whose arrow turns toward the attic window as the camera moves. |
| Talk Bea / Talk Vale | Use within 1.8 m (Talk verb and glyph). | Speech card. The witness plays Talk or SitTalk and turns their head toward Pip (`Witness.Conversing`). Pip turns to face the witness. |
| SitOnBench | Use at the free end of the bench (Sit). | Fixed Cinemachine camera at a seated child's eye height (1.07 m), framing the front door. It holds for 3 s or until Pip moves, with a 0.6 s blend. |
| ExamineStreamer | Use at the bicycle (Look). | Pip's line on the card. |
| TryFrontDoor | Use on the porch (Open). LiftLatch is never offered from outside. | `DoorStuck`, no fact: "It's stuck on the inside." |
| ExamineCandyBowl | Use at the bowl on the arrival-porch crate (Look). The reach is 1.15 m, so the bowl is not offered at the spawn. | Before closure: "A bowl piled high with candy." After closure: the saved sweet and Vale's line. |
| ArriveAtPorch | Pip steps onto the arrival porch zone (on entry only). | `NotYet` until the interior pass makes closure reachable. |

## UI built (`Runtime/UI`, new classes; GameUi.cs untouched)

- **`DialogueView`:**
  - A stitched paper card with a night-violet bottom scrim.
  - A felt portrait frame with the speaker's initial. There is no portrait art yet.
  - An orange name tab, line pips, "Tap to continue" with an orange next disc, and Skip at top right.
  - A tap, E, Space, Enter or the gamepad face buttons advance a line. The advance input has its own action map.
- **`CaptionStrip`:**
  - Night felt with a candy stitch, placed bottom centre and sized to fit between the thumb clusters.
  - Captions queue on game time and show a direction cue when they have a source.
  - `LargeText` is a hook only.
- **`NotebookView`:**
  - A plum cover with two bone pages and a "Notebook" heading with the case name.
  - The three questions. A proven answer shows a green "Found it!" stamp. An unknown answer shows a "?" in a pencil
    silhouette.
  - Annotation slips sit beside their question. Tapping a found slip shows its note in the caption strip. Unfound
    slips are "?" silhouettes.
  - The hint radio button calls `HintSession.Request` and shows the hint in the caption strip.
  - An X closes the notebook.
  - Once the case is closed, a final card shows the discoveries in order, the clue count, "Back to the street" and a
    confirmed "Replay case".
  - There is no paper-bat counter, because no collectible exists yet.
- **`NoticeCard`:** the save notices (recovered, newer, unreadable) and the New game and Replay confirmations.
- **`InteractionPrompt`:** a bone felt marker with the target's glyph, bobbing at 1.2 Hz over the selected target.
- **HUD:** a notebook disc to the left of Pause. It is added by `CaseDirector` and mirrors with left-handed layouts.
  While the notebook or a conversation is open, the HUD is hidden and gameplay input is suspended.
- **New icons:** talk, radio, skip, next, arrow and check, exported by `ui_mock.py --export` and added to
  `UiSpriteCatalog`.

## Save and resume

- **Boot:** `CaseSaveStore(new DiskSaveFileSystem(), Application.persistentDataPath)` loads. The world is rebuilt from
  `CaseWorldView` (knock beat settled) while the title is up, before any control is enabled. Continue is shown when
  the save has facts.
- **Notices:**
  - Recovered: an information card.
  - Newer or unreadable: Start fresh or Back. A newer file is preserved by the store.
- **Autosave:** on every gained fact, including closure. The anchor saved is the staged anchor nearest Pip's last
  safe ground (arrival porch, plaza, bicycle corner, bench, blue porch). Write errors are logged; play continues.
- **Continue:** after the normal spawn, Pip is moved to the restored anchor. Unknown anchors, and interior anchors
  that are not staged yet, fall back to the plaza.
- **New game with facts:** the player confirms, then the store's `StartFresh` runs.
- **Replay:** after confirmation, `Replay()` resets only the case and puts Pip back on the arrival porch.
- **Tests:** `CaseDirector.UseTemporarySaveDirectory()` gives every PlayMode and visual test a fresh folder, so saves
  never leak between tests.

## Tests

- **EditMode** (`CaseWiringTests`, 9 tests):
  - keys and context;
  - exterior coverage, and no latch from outside;
  - nothing in reach at the spawn;
  - picker reach, height and camera weighting;
  - anchors and the plaza fallback;
  - the arrival cue plays once;
  - caption bearings;
  - the hold gate and the minimum show time.
- **PlayMode** (`CaseWiringTests`):
  - Bea talk: `beaSpoke` is recorded, her line plays, a save is written, she plays the Talk loop, and a repeat gains
    nothing;
  - holding Use doesn't skip lines;
  - the bench view records `checkedView`, then ends on movement or after the 3 s hold;
  - the door gives `DoorStuck` with no fact;
  - the knocks play once, with the caption and arrow, and also play when Pip goes straight past;
  - the notebook shows the three questions, the hint reaches the caption strip, and a slip tap shows its note;
  - save, then reload the scene, restores the facts and the world, and Continue goes to the anchor. New game asks
    first;
  - a closed case (reducer driven) shows the final card, and Replay resets.
- **Visual** (`CaseCaptureTests`): `14_dialogue` (Bea talking) and `15_notebook` (notebook open with four notes).
- **Existing PlayMode and visual tests:** each Setup now calls `UseTemporarySaveDirectory()`.

## Evidence

- `./tools/check-local.sh`: all checks passed; `dotnet test tools/core-tests`: **206 passed**.
- **CI run 44** (`6007753`): compiled. EditMode 223/223 passed. PlayMode 32/33: the reload test never heard the
  knocks, so the restored world had nothing to rebuild (a test bug). Visual 2/2. Frames `14_dialogue` and
  `15_notebook` showed clipped notebook labels and the caption strip covering the speech card.
- **CI run 45** (`0685946`): **all green**. EditMode 223/223, PlayMode 33/33, visual 2/2. Frame review:
  - The captions now sit above the card.
  - The notebook spread, the questions and the hint radio render.
  - The slip labels and "Tap to continue" were still blank. Andika's line box is about 1.5 × the font size, and uGUI
    draws nothing when a line does not fit.
- **Next run:** fixed by giving those labels their full height. Its frames are the evidence for that fix.
- **Not run:** device. No device pass is claimed.

## What the interior pass needs

- **Interaction points:** cloth (treehouse), sill, June, latch (inside), grille, arm and crank, in
  `CaseStage.InteriorPoints`. Add them to the `CaseUseTargets` list.
- **Trigger volumes:** EnterAttic (window entry) and EnterFoyer.
- **Staged anchors:** side yard, treehouse, attic sill, attic, foyer and workshop. The plaza fallback covers them until
  then.
- **`ApplyWorld`:** the door (Open), June's placement (attic, foyer, porch), the service panel, the owl state and
  Kit's booth.
- **Cutaway:** at most 4 s and skippable, when `CaseStep.PlayCutaway` is set.
- **Camera:** a closer shoulder view in the attic.
- **Finale:** ArriveAtPorch already fires on entering the arrival porch zone. The finale sightline framing still has
  to be added.
- **Paper bats:** the notebook bat counter waits for collectible paper bats.
- **Art:** portraits replace the initial discs. Clue-word underline in dialogue is not done yet.
