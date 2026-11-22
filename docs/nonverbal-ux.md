# Non-verbal UX v2: animated sequences (approved 2026-11-22, decision D23)

Owner request (2026-11-22): "besides the text dialogues we need a NON-VERBAL UX too, thinking of younger children."
Owner feedback on v1: "we could have ANIMATED SEQUENCES instead of static images that are too cluttered to understand."
v1 rows of 3–4 pictogram chips (eye → cape → house) are retired as the *carrier*. v2 shows **one idea at a time,
moving**, so pre-readers (about 4–7) can read each beat at a glance. Readers keep every word. This is the design; the
engine-free core is built (§12).

Mocks (animated GIFs at 1266×585, 15 fps, plus a 6-frame `_sheet.jpg` contact sheet each), in
`docs/evidence/s002/nonverbal_v2/`, drawn by `tools/ui_mock/nonverbal_v2_mock.py` over the CI frames:
`nv2_bea_vignette.gif` · `nv2_vale_vignette.gif` · `nv2_memory_replay.gif` · `nv2_notebook.gif` · `nv2_hint.gif`.
The v1 stills stay in `docs/evidence/s002/nonverbal/` for reference; `nv_icons.png` is now the parts library.

## 1. Owner decisions (recorded 2026-11-22)
| Topic | Decision |
|---|---|
| Stuck helper | Opt-in **Helper** setting, **OFF** by default. Tier-3 help plays only when the radio is pressed; nothing auto-plays after a stall. |
| Parent gate | **Press and hold two marked spots for 3 s.** No reading, no maths. |
| Gestures | Make **only Bea's Point** now. Vale bench-pat, June high five and other new gestures wait for the "more character actions" work; those lines use existing clips. |
| Reading mode | **Decided (D23.1):** animations always, small text line on by default, one-tap Pictures only at first launch, changeable in Settings; one setting `ShowSpeechText`. |
| Memory replay | **Decided (D23.2):** first time each key line is heard in a save, then on demand from the notebook. |
| Scope | **Decided (D23.3):** now the 4 key Bea/Vale lines, June's resolve, the animated notebook and the tier-2 bat trail; other lines keep the v1 strip. |
| Ghost, bat trail | **Decided (D23.7):** June mesh + translucent paper material (flat cut-out on low-end); trail repeats every 4 s for about 8 s per radio press. |

## 2. Principles
1. **One idea per beat.** A vignette has 1–3 beats; each beat has one subject, a clean background, big shapes and at
   most **two moving parts** at once. Nothing reads as a row of symbols.
2. **The world is the picture.** For the key testimony the camera *looks at the real place* and a memory ghost
   re-enacts it there (§4). Pre-readers link the clue to the place, not to an icon.
3. **Motion carries grammar.** Travelling = "goes to"; popping into a window = "goes in"; ticking clock + still door
   = "for a long time, nothing"; a ⊘ stamp that thumps and settles = "never"; an orange stitched ring pulsing =
   "this matters" (key clue). Same meaning everywhere (dialogue, notebook, hints).
4. **Never by colour alone** (spec §6): the key ring is a stitched shape that pulses; ⊘ is a shape; found = sticker
   + stamp; unfound = dashed outline.
5. **Never blocks play.** Every vignette loops until tapped, every replay is skippable, hints never stop movement.
6. **Reading mode (decided, D23.1).** Animations always play, in every mode. A small optional
   text line under the stage is **on by default** (Andika 14 pt, `nv2_bea_vignette`). At first launch one tap
   chooses **Pictures only** (two big cards: child + book / child + pictures; no reading needed); Pictures only
   hides the text line and the bubble shrinks to the stage (`nv2_vale_vignette`). Settings can change it later.
   `NonverbalSettings.ShowSpeechText` (bool, default true, in its own settings file) replaces v1's three-way `ReadingMode`.

## 3. Animated speech (`nv2_bea_vignette.gif`, `nv2_vale_vignette.gif`)
**Bubble.** When a character speaks (babble voice, D20) a bone-paper speech bubble opens with its tail on the speaker
in the world. Inside is a **stage**: a lifted night-felt panel (about 520×120 pt) where a 2–4 s paper-puppet
vignette loops. The speaker's face medallion sits on the bubble's left edge and the three candy babble arcs pulse
beside it while the voice plays. Chrome stays icon-only: next disc (bottom right), skip disc (top right). The
speaker in the world plays Talk and, on the beat that names a place, the gesture (Bea: Point).
**Loop.** The vignette plays once with the babble, then loops quietly (no voice) until the player taps next. Beats
start on babble syllables, so the voice and picture feel like one sentence. Reduced motion: each beat becomes a
cross-fade between 2–3 still poses, rings stop pulsing.

**Bea, `line.bea.saw_cape` → vignette `vg.bea.saw_cape` (3.6 s).**
1. 0.2–1.7 s: a tiny paper June (violet hat, silver cape with its tear notch) scurries from the treehouse across
   the plank bridge. One moving part.
2. 1.7–2.0 s: she hops and *pops* into the round-topped window (paper puff); the window lights with June inside.
3. 2.2–3.6 s: Bea's pointing hand slides in and taps toward the window; an orange stitched ring pulses on it.
   In the world Bea plays Point toward the side of the house at the same moment.

**Vale, `line.vale.door_shut` → `vg.vale.door_shut` (3.6 s).**
1. 0–2.5 s: one still subject, the front door (plum, no wiggle: shut ≠ stuck). A clock hand ticks once every 0.5 s;
   Vale's eye (looking at the door) blinks once. Two moving parts.
2. 2.6–3.4 s: a ⊘ stamp thumps onto the door and wiggles to a stop: "never opened".

**Other lines (one vignette ID per `CaseScript` key; same beat rules).**
| Line key (`c01.…`) | Vignette beats |
|---|---|
| `line.bea.repeat` | hedge; the cape tip glides along its top · feet below stay empty, ⊘ thumps on the feet slot |
| `line.bea.door_open` | memory frame (pencil-cloud border) replays `vg.bea.saw_cape` beats 1–2 · Bea nods |
| `line.vale.invite_sit` | the bench; Pip hops onto it · an eye opens above it |
| `line.vale.repeat` / `was_shut` | moon slides across · door stays still (was_shut inside the memory frame) |
| `line.june.greet` | three knocks ring out of the window · June waves from inside |
| `line.june.latch_reminder` | the latch bar; a hand lifts it · the door swings open |
| `line.june.listen_floor` | floor planks shiver three times · an ear pops up beside them |
| `line.june.resolve` | knock, knock, … knock · the owl bows in the same rhythm |
| examine lines (Pip) | one object + one reaction (sparkle, shrug, wiggle), never more |
Captions (`caption.*`) stay as the v1 direction strip but with one moving glyph (knock rings travel toward the
arrow), since they must not hide the world.

## 4. In-world memory replay (`nv2_memory_replay.gif`)
For the **key evidence lines only**: `line.bea.saw_cape`, `line.bea.door_open`, `line.vale.door_shut`,
`line.vale.was_shut`. It plays **once per line per save** (the first time), then only from the notebook (§5).
- **Trigger:** after the bubble's vignette has played once and the player taps next (never mid-line).
- **Camera:** a `MemoryReplay` Cinemachine camera (same pattern as `BenchView`) takes over: 0.35 s ease blend to a
  framing of the place (Bea: treehouse balcony → side window, from the plaza side; Vale: front door from the bench),
  **hold 1.5–2 s**, 0.35 s blend back. Total ≤ 2.7 s. FOV 45, no roll, no shake.
- **Memory look:** thin ink letterbox bars slide in, the world takes a lilac pencil-paper tint with a soft vignette.
- **Ghost:** a translucent paper cut-out re-enacts the moment in the real 3D place. Bea: June's silhouette runs off
  the treehouse balcony, across the bridge and into the side window, which then gets the pulsing orange ring. Vale:
  the front door stays shut while the moon moves a hand's width, ⊘ thumps onto the door.
- **Skip:** tap anywhere, or the skip disc (top right) with a candy progress ring. Skipping blends back at once and
  still records the clue. Input is buffered, not lost.
- The mock approximates the glance in 2D over a CI frame, so the ghost ends at the front-left window there.
- **Never blocks play:** time-scale stays 1, physics keeps running, Pip is in a dialogue state already (no input
  to lose). If the trigger arrives while Pip is airborne or in a hazard, the replay is dropped (the notebook can
  play it). Reduced motion: cut instead of blend, ghost appears at its end pose and fades.

## 5. Animated notebook (`nv2_notebook.gif`)
- **One question per page**, in a carousel (swipe or edge arrows; 3 dot pips under the page, neighbours peek at
  the sides). The page has only: the order badge (dots, no digits), the question as three big chips
  (June ? house), the answer strip (three panels) and the found clue stickers. Empty space is intentional.
- **Answer = tiny comic.** Unknown: dashed panels with a pencil "?". Known: the panels are drawn; **tap the strip
  and it animates**: June runs panel to panel (the active panel gets the orange stitch), the window lights with her
  inside, then the green check stamp thumps down. It loops twice and stops (no constant motion on the page).
- **Clue stickers wiggle into place.** When a clue is found, its sticker (Bea + cape) flies in from the notebook
  disc and wiggles to a stop in its dashed slot with a peel sound. Tapping a sticker replays its vignette; for the
  four key lines it offers the memory replay (an eye icon).
- Panels stay fact-driven (`CaseNotebookTags`), so save/restore rebuilds them without stored display text.

## 6. Animated hints (`nv2_hint.gif`)
The radio (orange disc, top bar) is the hint button; three candy pips show the tier. Each press advances a tier.
- **Tier 1, look:** the target object sparkles (2–3 felt stars, 0.6 Hz) and the nearest character turns its head
  to look at it. Nothing on the HUD but the pips.
- **Tier 2, where:** a **paper-bat trail** launches from Pip one bat at a time (every 0.22 s), flies the route and
  fades at the goal; a floating sticker of the goal (orange ring) pulses over it. The trail repeats every 4 s for
  ~8 s, then fades; never collides, never blocks the camera.
- **Tier 3, show me:** a translucent bone **ghost hand** appears over the real controls and demonstrates the exact
  action (drag the stick toward the goal, then tap Use with the right verb glyph). Only on radio press (Helper
  setting OFF by default; when ON, tier 3 may also appear after a 3 min stall).

## 7. Onboarding
Ghost-hand demos stay as in v1 (already animated): the hand loops twice over the real control, performing the move
dismisses it, `TipScheduler` rules unchanged. Simplified: the cause→effect paper card is replaced by **Pip doing the
move in a tiny looping stage** above the hand (hold Jump → a tall arc; tap in the air → glide). One stage, one hand.

## 8. Feedback, menus, parent gate (kept from v1, simplified)
- Success: green stamp + felt confetti + three-note motif + haptic; the nearest character cheers. Not yet: soft
  wooden *tok*, object wiggles, Pip shrugs; never a buzzer. Fact gained: sticker flies to the notebook disc.
- Menus: icon + picture toggles; settings rows gain **Helper** (off) and **Words under pictures** (on).
- Parent gate (decided): two marked felt spots; hold both 3 s, a candy ring fills; release resets. Guards language
  downloads, credits links and any external link. Destructive actions use a pictogram confirm, no gate.

## 9. Parts library (from v1, `nv_icons.png`)
Puppets are assembled from the v1 pictograms, now used as **parts**, not as sentences. IDs `pg.<id>`:
characters `pip bea vale june kit` · places `house house_side window june_window door_shut door_open stuck lock
latch hedge hedge_cape bench treehouse bridge porch bike lamp pin` · clues `cape cloth streamer feet sweet bowl
candy` · machine `knock3 knock_ring floor_hum grille cam arm connect gap crank wind owl owl_bow booth` · senses and
logic `eye ear hands pointer question check not heart moon star bat memory quiet` · moves `tip_*` · emotes `emo_*`.
New v2 parts: full-body puppets (June running, Pip, Bea arm), clock + hand, puff, stage panel, ghost material.

## 10. Implementation mapping
- **Vignettes as data (`Core`, engine-free):** `Core/Vignettes/Vignette { string Id; float Length; Track[] Tracks }`,
  `Track { string Part; Key[] Keys }`, `Key { float T; float X, Y, Rot, Scale, Alpha; string Pose; Ease Ease }`,
  plus `Cue` events (`Ring`, `Stamp`, `Puff`, `Gesture`, `BabbleSync`). One table `C01Vignettes` maps **the line's
  localization key → vignette ID** (`c01.line.bea.saw_cape` → `vg.bea.saw_cape`), so `ScriptLine` and `CaseScript`
  keep one source of truth. Authored as JSON in `Content/Resources/Vignettes/` and loaded by a pure parser.
- **Tests (`Tests/EditMode/Core`):** every `CaseScript` line key has a vignette; every part exists in the atlas;
  ≤ 3 beats, ≤ 2 tracks moving at any time, length 2–4 s; key lines have a `MemoryReplay` entry; parser round-trip.
- **Runtime:** `VignettePlayer` (UI) samples tracks and moves `Image` puppets inside a masked stage `RectTransform`;
  parts come from one `PuppetAtlas` sprite sheet (2048², to be baked from the same procedural parts, as
  `ui_mock.py --export` does for the UI). **No video files.**
- **`DialogueView`:** the text card becomes the bubble: stage + optional text line + medallion; `Show(index)` calls
  `VignettePlayer.Play(C01Vignettes.For(line.Key))`; `Advance()` is unchanged; raises `GestureCue` for `Witness`.
- **`MemoryReplay` (Runtime/Case):** a Cinemachine camera per key place (pose data in `CaseUseTargets`), blend and
  hold like `BenchView` (`HoldSeconds` 1.8, `BlendSeconds` 0.35), skip on any tap. Ghost: the **real character
  mesh** (June, posed with existing clips; no new clip) with a translucent unlit paper shader (bone tint, 55% alpha, candy rim, depth
  write off) moved along a spline; fallback a flat billboard cut-out for low-tier devices.
- **`NotebookView`:** page carousel; each page = question chips + answer `VignettePlayer` (plays on tap) + sticker
  slots; sticker arrival uses the same wiggle curve.
- **`HintSession`:** tiers unchanged; `CaseHints` gains a world target and route per objective; a `HintGuide`
  component plays sparkle/look (1), the bat trail and beacon (2), the ghost-hand demo (3).
- **Budget (iPhone, 60 fps target):** one vignette alive at a time; ≤ 12 puppet images, one atlas, one draw call
  batch; no per-frame allocations; memory replay ≤ 1 extra transparent character draw + 2 UI bars; bat trail ≤ 12
  billboard quads sharing one material; total ≤ 0.3 ms CPU, ≤ 4 MB textures.
- **Art pipeline:** parts stay procedural (Python now, baked sprites at @3x, reviewed by rendering), ghost shader in
  URP Shader Graph; Bea Point goes to the Blender character scripts now.

## 11. Open questions (all answered by the owner on 2026-11-22, D23; kept for the record)
1. **Reading mode:** confirm "animations always + small text line on by default + one-tap Pictures only at first
   launch" (§2.6). Recommended.
2. **Memory replay frequency:** once per key line per save, then on demand from the notebook. Recommended over every
   time (repeats would slow replays of the same conversation).
3. **Ghost style:** reuse the June mesh with the paper ghost shader (reads as "June, earlier") vs. a flat cut-out
   (cheaper, more "storybook"). Recommend the mesh for June, flat cut-out as the low-tier fallback.
4. **Vignettes for every line or key lines only first:** recommend the 4 key lines + June's resolve in S003, the rest
   in S004, keeping the v1 chip strip as an interim fallback for lines without a vignette.
5. **Bat-trail loop length:** 4 s repeat for ~8 s, then silence until the next press. Recommended; longer could feel
   like nagging.

## 12. Core implementation and integration notes (`feature/nonverbal-core`)
The engine-free half is built in `Assets/SpookyStreet/Core/Nonverbal/` and tested in
`Tests/EditMode/Core/{Vignette,MemoryReplay,NonverbalSettings,ParentGate,BatTrailSchedule}Tests.cs`. It changes no
view and none of `CaseDirector`, `DialogueView`, `NotebookView`, `CaseScript`, the `HintSession` wiring or `CaseSave`.
Vignettes are authored as C# data (`C01Vignettes`), not JSON yet: the format is the same as §10 and a JSON loader can
be added when an editor exists; until then the compiler and `VignetteValidator` check every field.

| Piece | Type | What it gives the views |
|---|---|---|
| Format + evaluator | `Vignette`, `VignetteTrack`, `VignetteKey`, `VignetteCue`, `VignetteSampler` | `Sample(v, t, PartState[])` fills one state per track (part ID, visible, x/y/scale/rotation/alpha in stage-normalized space, 0..1 with origin bottom-left); `ActiveCues` (ring, stamp, puff, knock overlays with progress), `CuesStarted` (world events: `Gesture "Point"`, babble sync); `LocalTime(elapsed)` (loops, `LoopCount` 0 = until next, 2 = notebook); `ReducedMotionTime(t)` (hold each beat's end pose). No allocations with reused buffers. |
| Rules | `VignetteValidator` | 2–4 s, 1–3 beats, ≤ 12 tracks, ≤ 2 moving parts at any instant (moving tracks, overlay cues and stamped tracks counted), every part in `PuppetParts`. |
| Parts | `PuppetParts` | The atlas contract: one sprite per ID, named as the ID (`pg.window`, … plus v2 `pg.june_run` flipbook, `pg.clock`, `pg.clock_hand`, `pg.eye_shut`, `pg.puff`, `pg.key_ring`, `pg.stage`, `pg.pip_body`, `pg.bea_arm`). |
| Data | `C01Vignettes` | `For(lineKey)` → `LineVisual` (vignette ID, or `UsesV1Strip` = true); `LineTable` covers every `CaseScript` key; `AnswerStripFor(questionId)` for the three notebook questions. |
| Replays | `MemoryReplays`, `MemoryReplayPolicy`, `MemoryReplayPlayback`, `MemoryReplayTiming` | Which lines replay and where (`ReplayPlace`), the first-hearing decision, notebook availability per sticker, and the blend weight / ghost progress over time (0.35 s in, 1.8 s hold, 0.35 s out; skip blends back from the current weight; reduced motion cuts). |
| Seen set | `NonverbalProgressStore` (`case-c01-nonverbal.json`) | Seen replay IDs for the current save, with atomic writes and recovery (`SaveSlotStore`). |
| Settings | `NonverbalSettings`, `NonverbalSettingsStore` (`nonverbal-settings.json`) | `ShowSpeechText` (on), `HelperAutoHints` (off), `FirstLaunchChoiceMade`; `ChooseReadingMode(picturesOnly)`, `MayAutoShowTier3(stall)`. |
| Parent gate | `ParentGate` | Feed `TouchDown/Move/Up(id, x, y)` in normalized panel coordinates, `ReleaseAll()` on OS touch cancel, `Update(dt)`; read `State`, `Progress` (candy ring), `IsZoneHeld(0/1)`. Hold 3 s, timeout 30 s. |
| Bat trail | `BatTrailSchedule` | `Press(now)` on a tier-2 radio press, `Sample(now, BatState[])` → route progress (0..1, eased) and alpha per bat, `BeaconVisible(now)`, `Stop()`. Two waves (0 s, 4 s), 9 bats each, ends at about 7.9 s, ≤ 12 bats alive. |
| Strings | `LocalizationKeys.NonverbalUi` | First-launch cards, the two settings rows, memory replay skip / watch-again names, the parent gate line (all in `en.json` with context). |

**Why a separate seen-replays file.** It cannot be derived from facts: `line.bea.door_open` and `line.vale.was_shut`
repeat in every conversation after the door opens without gaining a fact (`saw_cape` and `door_shut` could be, via
`BeaSpoke`/`ValeSpoke`, and those facts are still used so older saves get the notebook replays). Adding a field to
`CaseSave` would mean a schema migration in a file another branch is editing. The file instead records the case facts in
acquisition order at its last write; facts only grow within a save, so if they are not a prefix of the current save's
facts the file belongs to an earlier save and is cleared. A crash between "case save replaced" and "seen set cleared"
therefore heals itself on the next load. Losing the file only means a replay plays once more. No `CaseSave` migration is
needed.

**What the integration step must do (after `feature/s002-interior-wiring` merges).**
1. **Boot (`GameBootstrap` / `CaseDirector`).** Create `NonverbalSettingsStore` and `NonverbalProgressStore` with the
   same `ISaveFileSystem` and directory as `CaseSaveStore`. Load settings before the title; if
   `NeedsFirstLaunchChoice`, show the two picture cards (`ReadingChoiceWords` / `ReadingChoicePictures`), call
   `ChooseReadingMode` and `Save`. After `CaseSaveStore.Load()` call `progress.Load(resume.State)`.
2. **Case state changes.** After every fact change, fresh start (`StartFresh`) or confirmed replay (`Replay`) call
   `progress.Observe(newState)`; on fresh start/replay also call `progress.Reset()`.
3. **`DialogueView`.** In `Show(index)`: `var visual = C01Vignettes.For(line.Key)`; if `visual.UsesV1Strip` keep the v1
   pictogram strip, else play `C01Vignettes.Get(visual.VignetteId)` in a `VignettePlayer` (masked stage, `Image` per
   track from the `PuppetAtlas`, sampled each frame with a reused `PartState[Vignette.MaxTracks]`; use
   `ReducedMotionTime` when reduced motion is on). Forward `CuesStarted` gesture cues to the speaker (`Point` for Bea).
   Show the text line only when `settings.ShowSpeechText`.
4. **Memory replay trigger.** When the player taps next on a line (after its vignette played once):
   `var d = MemoryReplayPolicy.OnLineDismissed(line.Key, progress.Seen, pip.IsAirborne, pip.InHazard)`; if
   `d.MarkSeen` call `progress.MarkSeen(d.Replay.Id, state)` **before** playing; if `d.Play` start a
   `MemoryReplayPlayback(d.Replay, MemoryReplayTiming.Default, reducedMotion)`, drive the `MemoryReplay` Cinemachine
   camera blend, letterbox and tint from `Weight` and the ghost along its spline from `GhostProgress`, call `Skip()` on
   any tap or the skip disc (`MemorySkip`). Never gate a fact on it: the line already recorded its clue.
5. **`NotebookView`.** Page per question: answered → `AnswerStripFor(question.Id)` plays on tap (`LoopCount` 2, then
   holds the end). Sticker eye icon (`MemoryWatchAgain`) when `MemoryReplayPolicy.OnDemandFor(entryId, state,
   progress.Seen)` is non-empty; it plays the replay without marking anything.
6. **`HintSession` / `HintGuide`.** On a tier-2 radio press call `BatTrailSchedule.Press(Time.time)`; render
   `Sample(...)` bats on billboard quads along the objective route; `Stop()` when the tier changes or the objective is
   met. Tier 3 plays on a radio press; auto after a stall only if `settings.MayAutoShowTier3(stallSeconds)`.
7. **Settings screen.** Two toggles: `ShowSpeechTextLabelKey` and `HelperLabelKey`, saved with
   `NonverbalSettingsStore.Save`.
8. **Parent gate.** A `ParentGate` instance per gated action (language download, credits link, any external link),
   with the two felt spots drawn at `ZoneA`/`ZoneB` and the `ParentGateHint` line; proceed only on `Passed`.
9. **Art.** Bake the `PuppetAtlas` with exactly the `PuppetParts.All` names (a test can then compare the atlas to the
   list), render-review it, and add the June paper-ghost material (flat cut-out fallback on low tier).

**Evidence.** CI run 67 (commit `c7842e0`, `feature/nonverbal-core`): static checks and engine-free core tests passed;
Unity 6000.3.28f1 EditMode 359/359, PlayMode 46/46, Visual 3/3. New fixtures: `VignetteTests` 22, `MemoryReplayTests`
19, `ParentGateTests` 9, `NonverbalSettingsTests` 8, `BatTrailScheduleTests` 6 (64 tests, all passing).
