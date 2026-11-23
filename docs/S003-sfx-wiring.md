# S003: the approved sound effects in the game

Branch `feature/s003-sfx-wiring`. The owner approved every synthesised effect in the first listening review (D20.1).
This increment imports them into Unity, sets their import settings in code and wires them to gameplay. **Voices and
music are being replaced (D20.1), so no `voice_*` or `music_*` sound is imported or wired.** CI evidence is at the end.

## Import

- `python3 tools/audio/build.py` renders the masters. `python3 tools/audio/import_unity.py` then copies the 18
  approved effects (70 takes) to `Assets/SpookyStreet/Content/Resources/Audio/<id>/<id>_t<n>.<ext>`. Run
  `python3 tools/unity_meta.py` afterwards. It now has an `AudioImporter` template for `.wav` and `.ogg`.
- **Plain git, no LFS.** A `.gitattributes` exception for `Content/Resources/Audio/**` works like the one for Models
  and UI.
- **Formats and size:**
  - **Short effects:** kept as their 48 kHz, 16-bit mono WAV masters (5.8 MiB). They are lossless sources, and
    Unity encodes them to Vorbis at build time, so the repo never holds a second lossy generation.
  - **The night bed:** the 24 s stereo `amb_night` loop is stored as Ogg Vorbis, libvorbis q6 (about 190 kbps,
    1.7 MiB for 3 takes, instead of 14 MiB of WAV). The decoded length is exactly 1 152 000 samples. The seam jump
    (0.010) is below the bed's own 99.9th-percentile sample step (0.036), so the loop stays continuous.
  - **Total added to the repo:** 7.3 MiB (7 630 476 bytes) of audio, plus metas.
  - Encoding is bit-exact (`-fflags +bitexact`), so re-running the import gives identical files.
    `tools/audio/tests/test_import.py` re-renders three takes and checks that they are byte-equal to the project
    copies. It also checks that every take and its meta exist, that the WAV format is right, that no other folder is
    there, that the approved list matches the C# plan, and that the folder stays under 9 MiB.
- **Import settings are set in code:** `Editor/AudioImportRules.cs`, an `AssetPostprocessor` like `ModelImportRules`.
  The rules themselves live engine-free in `Core/Audio/AudioImportPlan.cs` and are unit tested.

| Category | Ids | Vorbis quality | Load type | Mono |
|---|---|---|---|---|
| clue | knocks, floor hum, cam tap, wind wheel | 0.80 | DecompressOnLoad | knocks, cam, wheel (3D one-shots) |
| notebook | page turn, sticker pick/drop/wobble, correct glow | 0.70 | DecompressOnLoad | no |
| movement | jump, land, glide open, glide loop | 0.60 | DecompressOnLoad | no |
| ui | UI tap | 0.60 | DecompressOnLoad | no |
| footstep | wood, grass, stone | 0.50 | DecompressOnLoad | no |
| ambience | night bed | 0.50 | Streaming, load in background, no preload | no (stereo) |

- **Sample rate:** every clip keeps its 48 kHz master rate (`PreserveSampleRate`). That is the iPhone's hardware rate,
  so there is no runtime resampling and no second resampling pass at import. The same settings apply on every
  platform (D25), and the rules clear any platform override.
- **Decompress on load:** the short sounds decode to PCM once, so they cost no CPU and start on the exact frame (the
  knock sync needs this). All the effects together take about 6 MiB of PCM.

## Wiring (`Runtime/Audio/GameAudio.cs`, created by `GameBootstrap` after the director)

Everything hooks in through events, so the player, movement and camera files are untouched. The few edits elsewhere
add events or settings only:

- `KnockBeat` gains `Started`, `SoundPosition` and the knock times from `AudioEvents.KnockOnsets`.
- `WorkshopView` gains `Knocked(int)` and `CycleStarted(bool wind)`.
- `NonverbalMenus` gains the volume sliders.
- `GameBootstrap` gets one line that creates the audio.
- `NotebookView` (after the deduction merge): seven `AudioCues` calls and the silent marker on the slots.

| Sound | When | Where |
|---|---|---|
| `step_wood` / `step_grass` / `step_stone` | One step per stride (`FootstepCadence`: 0.55 m walking to 0.95 m running) of distance Pip covers on the ground; never in the air, rolling, standing, or on the take-off and landing frames. The walk and run clips are phase-locked to ground speed, so stride distance follows the animation's cadence. The first step comes a quarter stride after Pip starts moving. | 2D. A ray finds the surface under the feet, and `FootstepSurfaces` classifies the collider's own name, then its parents' names. **Wood:** porch, deck, step, stair, floor, bridge, ramp, shed, cabin, sill… **Stone:** path, stone, sidewalk… **Grass:** lawn, ground, hedge… Inside the house every floor is boards. The lawn's ground collider under the sidewalk slabs counts as stone. Every walkable slab of the route and the house classifies as wood (tested). |
| `move_jump`, `move_land` | `MotorStep.Jumped`, `MotorStep.Landed`. The wall moves from `feature/wall-ledge` reuse them: the wall kick (`WallJumped`) and the pull-up (`ClimbStarted`) play the jump; the ledge catch (`LedgeGrabbed`) and the feet reaching the top (`ClimbEnded`) play the land. `WallLedgeTests` checks the kick, catch, pull-up and top. | 2D |
| `move_glide_open`, then `move_glide_loop` | The cape snaps open on `GlideStarted`. The flutter loop takes over 0.35 s later if Pip is still gliding, and fades out over 0.15 s on `GlideEnded`. | 2D |
| `ui_page_turn` | `ThresholdTravel.Started` (every crossing) | 2D |
| `ui_tap` | Every uGUI `Button` click. Buttons are found in the scene, and new ones are picked up within 0.5 s. A `UiTapSound` marker is added, with `Silent` for controls that play their own sound. | 2D, plays through the pause |
| `amb_night` | Loops from the first Playing frame | 2D. Outdoors it plays open. Inside the house it drops to −12 dB with a 1.1 kHz low-pass, fading at 24 dB/s, so the change happens under the page-turn. |
| `clue_knocks_distant` | `KnockBeat.Started`, the same frame the attic-light animation starts | 3D at the attic light (min 12 m, max 70 m, so it reaches the arrival porch about 25 m away) |
| `clue_floor_hum` | When the hall's hum caption (`c01.caption.foyer_hum`) plays, which happens on entering the hall until the cam has been seen | 2D, all around in the hall |
| `clue_cam_tap` | Each knock contact of the workshop mechanism (`WorkshopView.Knocked`, the frame the follower drops onto the cam). This covers the grille look, the crank, the gap tap and the cutaway, but only while the mechanism can be heard: Pip indoors, or in the crank cutaway. | 3D at the cam (min 2.5 m, max 14 m) |
| `clue_wind_wheel` | Each wind-driven mechanism cycle (`CycleStarted(true)`, every 9 s after the knocks were heard) while Pip is outdoors | 3D at the rooftop wind wheel |

**Knock sync:**
- `KnockBeat.KnockTimes` is now `AudioEvents.KnockOnsets` (0.02, 0.28, 1.00 s), the onsets inside every take. Before,
  it was 0, 0.32, 1.05; each pulse moves by 50 ms at most.
- The clip starts on `Started`, the beat's first frame, and the bat still shakes loose on the second knock.
- Light and sound both run on game time and both hold under the pause, so they stay together.
- The caption ("[Three knocks from the blue house]", with its direction cue) is unchanged.
- **Not changed:** the caption's small picture-vignette pulses in its own illustrative rhythm (0.3, 0.7, 1.6 s in
  `C01Vignettes.Lines.cs`). The spacing is close to the sound's, but it is not frame-locked. That file is being
  rebuilt on `feature/s002.5a-deduction`, so I left it alone (see Gaps).

**The mechanism's own rhythm:** the mechanism knocks at 0.45, 0.85 and 1.9 s (`MechanismMotion`). The cam taps
fire on those exact frames, so the taps, the follower and the owl's bow line up.

## Rig additions (`Runtime/Audio/AudioRig.cs`)

- **Event log** (`Log`, `CountPlays`): every play (id, take, bus, 3D or not, position, game time, frame), stop, pause
  and resume. Batchmode has no audio device, so tests read the log and the source state, never the output.
- **Pause** (`SetGamePaused`, `IsBusPaused`, `IsHeld`): pausing the game holds the SFX, Voice and Ambience buses in
  place (`AudioSource.Pause`), and they resume where they were. A knock never loses its sync, and a held source is
  never stolen. Music carries on (none is wired yet). The UI tap still plays, so the pause menu's own buttons answer.
  It uses `ignoreListenerPause`, because `GameBootstrap` still sets `AudioListener.pause` as a backstop. New
  pausing sounds are dropped while paused.
- **Bus shaping** (`SetBusShaping`): an extra trim and low-pass (`AudioLowPassFilter`) per bus. This is the bed
  indoors.
- **Other:** fade-out stops (`Stop(id, seconds)`), per-event 3D distances (`AudioEventDef.MinDistance` and
  `MaxDistance`), no Doppler, and 10 SFX sources (4 on each other bus).

## Notebook sounds (wired after the `feature/s002.5a-deduction` merge)

`Core/Audio/AudioCues.cs` is engine-free and static, so Core or Runtime code can call it, and `GameAudio` plays each
cue through the rig. After main brought in the deduction notebook (merge 3a1d535), the notebook calls the cues at
these moments (`docs/S002.5a-deduction.md` §"Notebook"). Each is a single line, plus one marker line on the slots:

| Call | Plays | Where in the notebook |
|---|---|---|
| `AudioCues.StickerPick()` | `note_sticker_pick` | a sticker lifts: drag start (`OnStickerDragBegan`), or a tap-tap or cursor pick (`CursorResult.Picked`) |
| `AudioCues.StickerDrop()` | `note_sticker_drop` | a sticker lands on a slot: a drag drop or `CursorResult.Placed` |
| `AudioCues.StickerWobble()` | `note_sticker_wobble` | a wrong set: as the stickers start wobbling home after the shake |
| `AudioCues.CorrectGlow()` | `note_correct_glow` | a right set: as the stitches glow, 0.6 s before the comic draws itself |
| `AudioCues.PageTurn()` | `ui_page_turn` | `ShowPage` turns to another page (arrows, swipe, keys); never for a snap on open |
| `AudioCues.UiTap()` | `ui_tap` | spare, for a tappable control that is not a uGUI `Button` |

The question slots are uGUI buttons, so they carry a `UiTapSound` with `Silent = true`: they play the sticker's
press, not the tap. Stickers are not buttons. The page arrows tap and also flip the page. With no listener
(EditMode, or a scene without the rig), the calls do nothing. The PlayMode test
`NotebookStickersPageAndVerdictsPlayTheirSounds` checks every cue through the rig's log:
- page turns;
- tap-tap picks and drops;
- the wobble after a wrong verdict and not before;
- a drag pick and drop;
- the glow on the right set before the comic;
- no taps from the slots.

## Settings and the iOS audio session

- **Sliders:** the parent-gated settings card (`NonverbalMenus`) has three new rows: Music, Sounds and Voices. They
  are new keys `ui.settings.music`, `ui.settings.sounds` and `ui.settings.voices` with translator context, using the
  pictures `pg.emo_tune`, `pg.knock_ring` and `pg.emo_wow`. They edit `PreferencesData.MusicVolume`,
  `EffectsVolume` and `DialogueVolume`, which drive the exposed `MusicVol`, `SFXVol` (the bed's `AmbienceVol`
  follows it) and `VoiceVol` through `AudioMix.SliderToDb`.
- **Persistence:** the sliders save through `PreferencesStore`, which has its own file, so a replay never resets
  them. The save happens 0.5 s after a slider rests. The saved levels are applied on boot.
- **Reduced motion has no audio effect.** It is a visual setting (tested).
- **iOS audio session: Ambient.** It respects the Ring/Silent switch and screen lock, and it mixes with other apps'
  audio, so a parent's music or podcast is never stopped by the game. That is the kid-friendly default. Unity picks it
  from the existing PlayerSettings (`muteOtherAudioSources: 0`, `Prepare IOS For Recording: 0`), with no platform
  audio code (D25). Both are now locked: in `tools/ci/validate_project.py`, and by an EditMode test for
  `muteOtherAudioSources`.

## Tests

- **EditMode, Core** (`Tests/EditMode/Core/AudioWiringTests.cs`, 11 tests, run by `tools/core-tests` and Unity):
  - catalog mapping: the approved set, voices and music excluded, buses, 3D distances, markers, the step throttle
    never drops a running step;
  - the import plan for every approved id;
  - path parsing;
  - the surface mapping (names, parents, indoors, sidewalk, every walkable slab is wood) and the sidewalk band;
  - the stride cadence (walk, run, none airborne or standing);
  - the `AudioCues` hooks;
  - the pause and indoor mix rules.

  The Nonverbal key count test now expects 7 keys.
- **EditMode, Unity** (`Tests/EditMode/Project/AudioImportTests.cs`): every one of the 70 takes has an
  `AudioImporter` with the plan's Vorbis quality, load type, mono and background loading, and loads from Resources at
  48 kHz with the right channels; the rules recognise only catalog takes; no voice or music is imported; the
  Ambient session setting.
- **PlayMode, rig** (`Tests/PlayMode/AudioRigTests.cs`, three new tests): pause holds game sound and the UI tap still
  plays; bus shaping; the log.
- **PlayMode, game** (`Tests/PlayMode/GameAudioTests.cs`, real scene, fresh save folder each):
  - **Surfaces:** walks on lawn, path-stone and porch-deck test floors play only their own footsteps, and none while
    standing. The real path, lawn, porch and sidewalk probe as stone, grass, wood and stone.
  - **Moves and pause:** the glide plays the cape, then the loop. Pausing holds the SFX and ambience buses and the
    loop, and the Resume button taps and resumes. Folding the cape stops the loop; the landing plays; a ground jump
    plays the jump, then the landing.
  - **Knocks:** the New game button taps and the bed starts. The knocks play as a 3D source at the attic light on
    the beat's first frame, the light pulses on each onset, and the caption stays. The wind wheel whirrs from the
    roof on the next wind cycle, and no cam taps reach the street.
  - **Threshold:** the window crossing plays one page turn, and the bed is −12 dB and low-passed inside. The floor is
    boards, the mechanism taps three times from the cam in its knock rhythm, and no wind wheel plays indoors.
  - **Sliders:** they drive the buses, reduced motion changes nothing, and after a reload the levels and the sliders
    are restored.
- Python: `tools/audio/tests/test_import.py` (4 tests) runs in `check-local.sh` and in the CI static job.

## Gaps

- Not checked by ear in the game. CI has no audio device, so these tests prove the wiring (events, timing,
  positions, bus state), not the mix. The owner should listen on the next TestFlight build, especially:
  - the knock level from the arrival porch;
  - the footstep level against the bed;
  - the indoor muffle.
- **Caption vignette rhythm:** see Knock sync above. To frame-lock it, set `KnockRhythm` in
  `C01Vignettes.Lines.cs` to `AudioEvents.KnockOnsets` (plus the caption's 0.3 s lead-in) once the deduction
  branch has merged.
- **Mechanism knocks outdoors:** wind cycles outdoors play only the wheel's whirr. The mechanism's knock rhythm
  (0.45, 0.85, 1.9 s) differs from the street-knock take (0.02, 0.28, 1.00 s), so the distant knocks are not
  replayed on wind cycles.
- **Notebook sounds** are wired. Two sounds are not used: a sticker sent home by a tap plays no sound, and the "Case
  closed" card has none.
- **Music and voices:** the `MusicVol` and `VoiceVol` sliders work but nothing plays on those buses yet.
- **No real AudioMixer asset:** the rig is the code-built mixer from S003. There is no reverb send for the indoor rooms.
- **Run 113 rig test:** the rig test once read the bed 6 dB below the expected trim. That is the size of the voice
  duck, as if `VoiceActive` were true with no voice playing; the cause was not confirmed. The test now checks the
  trim relative to the bed's open level and passes. Worth a look on device once voices are wired.

## CI evidence

- **Run 45097129045** (commit `a63909b`, main `6e78392` merged in): green.
  - Static checks: passed.
  - Unity EditMode: 556/556, including `AudioWiringTests` 11/11, `AudioCatalogTests` 11/11, `AudioImportTests` 4/4
    and `WallLedgeTests` 39/39.
  - Unity PlayMode: 103/103, including `GameAudioTests` 6/6, `AudioRigTests` 8/8, `DeductionNotebookTests` 4/4 and
    `WallLedgeTests` 8/8 (with the wall-move sound checks).
  - Visual: 11/11.
- **Earlier failures, fixed in later commits:**
  - **Run 113** (`d1b87e6`): three test-timing faults.
    - Pip slid one more stride after the stick was released.
    - The held Jump reads released after a resume, so the glide can end.
    - The bed's trim check is now relative.
  - **Run 45093136131** (`c287dce`): the notebook opened on the page the test meant to turn to.
- Local: `./tools/check-local.sh` passes, with 528 core tests and 24 audio pipeline tests.
