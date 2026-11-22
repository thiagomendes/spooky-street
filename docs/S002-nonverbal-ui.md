# S002: the animated non-verbal UI on screen (D23)

Branch `feature/nonverbal-ui`. This pass puts the approved non-verbal UX v2 (`docs/nonverbal-ux.md`, decision D23) on
screen. It builds on the engine-free core from `feature/nonverbal-core` (`Core/Nonverbal`) and follows the
integration steps in §12. The visual target is the mocks in `docs/evidence/s002/nonverbal_v2/`.

## What shipped

| Piece | Where | Notes |
|---|---|---|
| PuppetAtlas art | `tools/ui_mock/puppet_atlas.py` → `Content/Resources/UI/puppet_atlas.png` (2048², 1.5 MB PNG) and `Core/Nonverbal/PuppetAtlasLayout.cs` (generated rect table) | Every ID in `PuppetParts.All` (79 parts) has one cell, drawn by the mock helpers (v1 glyphs plus the v2 June run, clock and hand, puff, key ring, stage panel, Pip body, Bea's arm). Six extras: June's 3 run flipbook frames, the two first-launch pictures and the bubble tail. Ink drop shadows are baked in. Silver `#DAE2EE` appears only on June's cape and the clue parts. The import rule imports the sheet as a 2D sprite at full 2048 (`UiSpriteImportRules.PuppetAtlasName`); the runtime cuts it with `Sprite.Create`. |
| `VignettePlayer` | `Runtime/UI/Nonverbal/VignettePlayer.cs` | Masked stage (`RectMask2D`). 12 pooled part `Image`s and 3 overlays (ring, puff, knock). Samples `VignetteSampler` every frame into a reused `PartState[]`. Runs on unscaled time gated by pause. Reduced motion uses `ReducedMotionTime`, cross-fades between beats and stops rings pulsing. June's flipbook runs while her track moves. World cues (`Gesture`) are raised once per loop. |
| Speech bubble | `DialogueView` | `C01Vignettes.For(line.Key)`: lines with a vignette get the bone-paper bubble, with the tail aimed at the speaker's head in the world, the face disc (puppet face) on the left edge, the night-felt stage, the next disc and the pips. Lines with `UsesV1Strip` keep the existing card, now with the face disc. The text line shows only when `ShowSpeechText` is on. `LineDismissed` (next, not Skip) and `GestureCue` are new events. |
| Bea's Point | `Witness.PlayGesture` | Plays the clip only if the model has it (no log otherwise). While it plays, the head turn toward Pip is off and the body yaws toward the side window, then turns back. No IK; the clip's 23° aim is accepted. |
| Memory replay | `Runtime/Case/MemoryReplayView.cs` | One Cinemachine camera per `ReplayPlace` takes over like `BenchView`. Custom brain blends give 0.35 s in and out (a cut with reduced motion). The letterbox bars, lilac tint and edge vignette follow `MemoryReplayPlayback.Weight`, and a candy ring on the skip disc fills over the hold. Tap anywhere, the skip disc, Use, Jump or a stick push skips. Time scale and gameplay input are never touched; the HUD hides while it runs. |
| June's ghost | same | The flat paper cut-out path of D23.7: June's run puppet from the atlas, pale and translucent, billboarded and flipbooked. It runs treehouse deck → bridge → side window, then the orange key ring pulses on the real window. At the front door, ⊘ thumps onto the real door. |
| Seen set | `NonverbalLayer` | `NonverbalProgressStore` sits beside `CaseSaveStore` (same folder): `Load(state)` at boot, `Observe` on every step, `MarkSeen` before playing, `Reset` on fresh start or replay. |
| Notebook | `NotebookView` (rewritten) | One question per page, in a swipeable carousel with edge arrows, dot pips and peeking neighbours. A page holds the order badge (dots), three question chips, the answer comic (tap → `AnswerStripFor`, plays twice then holds) or dashed "?" panels, and clue stickers that wiggle in the first time. The eye on Bea's and Vale's stickers replays the memory on demand (`OnDemandFor`). A last page holds the final card. Words show under the pictures when `ShowSpeechText` is on. The public surface the tests use is unchanged. |
| Hints | `HintGuide`, HUD radio disc | The radio is now on the HUD too (orange disc, three tier pips), and the notebook keeps its button. A tier-2 press runs `BatTrailSchedule`: up to 12 pooled billboard paper bats fly an arc from Pip to the objective's goal, and a goal sticker pulses there. The trail stops on a new tier or a new objective. Tier 3 shows only on a press, unless Helper is on and `MayAutoShowTier3(stall)` (once per objective). |
| First launch and settings | `Runtime/UI/Nonverbal/NonverbalMenus.cs` | At first launch, two picture cards (Pip + book / Pip + comic panels) appear over the title. A gear on the pause card opens a fresh `ParentGate` (two felt spots, hold 3 s, candy ring; one gate per guarded action). Once passed, the settings card shows two toggles (Words under pictures, Helper), saved at once with `NonverbalSettingsStore`. |
| Director hooks | `CaseDirector` (+22 lines) | Creates `NonverbalLayer`, exposes `Talking`, `RequestHint()`/`NextHintTier`, and calls `OnCaseReset()` from `ResetCase`. Everything else lives in the new components. `CameraRig` and `CaptionStrip` are untouched. |

## Tests

- **Core (`tools/core-tests`, also EditMode):** `PuppetAtlasLayoutTests`. The atlas holds exactly `PuppetParts.All`;
  every vignette part, flipbook frame and extra resolves; rects fit and never overlap; the stage is 9-sliced.
  `UiSpriteCatalogTests` lets the atlas sit in the UI folder.
- **EditMode project:** `UiSpriteImportTests.PuppetAtlasImportsAsAFullSize2DSprite`.
- **PlayMode `NonverbalUiTests`:**
  - a vignette plays for a scoped line, and the v1 card for others;
  - the text line follows the setting;
  - a memory replay plays once, and not again after a reload;
  - skip keeps the fact, and the notebook eye replays on demand;
  - the notebook carousel plays its comics;
  - the tier-2 bat trail starts and stops;
  - the parent gate guards the toggles;
  - the first-launch cards appear once and persist.
- **Visual `NonverbalCaptureTests`:** frames 24–28.

## Evidence

**CI.**
- Run 73 failed to compile: Cinemachine 3.1 has no `kBlendFromAnyCameraLabel`. Fixed by using the literal wildcard
  label.
- Run 74: EditMode 378/378 and PlayMode 60/60 passed. Visual 5/6: the first-launch capture waited on game time,
  which the title screen stops.
- Runs 75 and 78 (commit `091ca6d`, Unity 6000.3.28f1): EditMode 378/378, PlayMode 60/60, Visual 6/6, magenta 0 %
  in every frame. The existing `CaseWiringTests` and `CaseInteriorTests` pass with the replay running after Bea's
  line, because it never takes the controls.
- **Run 79** (commit `d320df9`: `main` merged with D24, D25 and June's Walk, plus the gamepad/keyboard gate):
  EditMode 378/378, PlayMode 61/61 (with the new two-button gate test), Visual 6/6, all green.

**Frames** (run 78, reviewed against the `nonverbal_v2` mock sheets):

| Frame | Mock | Review |
|---|---|---|
| `docs/evidence/s002/24_bea_vignette_run78.jpg` | `nv2_bea_vignette_sheet.jpg` frame 5 | Bubble with its tail on Bea, the pumpkin face disc, and the stage with the treehouse, bridge and wall. The window is lit with June inside, Bea's hand points at it and the orange ring pulses. Words line under the stage. Matches the mock's layout and clarity. |
| `docs/evidence/s002/25_memory_replay_run78.jpg` | `nv2_memory_replay_sheet.jpg` frames 2–3 | The real treehouse deck and the house from the plaza side, letterbox bars, a lilac pencil tint and the skip disc with its progress ring. June's pale paper ghost with a candy rim runs on the bridge toward the side window. |
| `docs/evidence/s002/26_notebook_page_run78.jpg` | `nv2_notebook_sheet.jpg` | One question per page: an order dot, the June ? house chips, and the comic panels (treehouse → bridge → window) with June running. The answer words sit under the strip; Bea's and Vale's stickers carry eye badges, and unfound clues show dashed slots. The next page peeks dimmed beside an arrow, with page pips below. |
| `docs/evidence/s002/27_bat_trail_run78.jpg` | `nv2_hint_sheet.jpg` frames 2–4 | From the arrival porch, the paper bats fly an arc toward Mr. Vale's bench, and the bench sticker in the orange ring pulses over it. |
| `docs/evidence/s002/28_first_launch_run78.jpg` | (§2.6) | Two picture cards over the dimmed title: Pip with an open book ("Words and pictures") and Pip with comic panels ("Pictures only"). |
| `docs/evidence/s002/14_dialogue_run78.jpg`, `15_notebook_run78.jpg` | — | The existing case frames with the new views: the raised caption now sits above the bubble, and the notebook is the carousel. |

Review iterations, from run 74 to run 78:
- **Atlas:** Bea's hand turned to point at the window; `house_side` redrawn as a wall section; pale paper ghost frames
  (the cut-out had kept June's colours); a bone-paper bat with a candy glow (the v1 bat vanished against the night).
- **Hint trail:** bats and sticker keep a minimum on-screen size, so they read at distance.
- **Notebook:** the active panel gets an orange stitch instead of a large ring; the arrows point the right way; the
  eye is a small badge (`PressScale` was undoing its scale); neighbouring pages are dimmed.
- **Dialogue:** raised captions sit above the bubble.

## Known gaps

- **June's ghost:** only the flat paper cut-out. The skinned-mesh ghost with a translucent URP material (D23.7's
  first choice) is not built; the cut-out is the documented fallback and low-end path.
- **Tier 3:** still the action caption. The ghost-hand demo over the real controls (§6) is a later pass, and so is the
  tier-1 sparkle and head turn.
- **Lines without a vignette** keep the v1 card, and its words show even in Pictures only: no v1 pictogram strip
  exists at runtime, and those lines get vignettes in S004 (D23.3).
- **Babble arcs and sync:** the arcs beside the face disc and the babble syllable sync are not drawn (no babble
  voice yet).
- **Replay staging:** the cameras, the ghost route and the marks come from `MemoryReplayView.DefaultStaging`
  (`ReplayStaging` data, overridable with `SetStaging`). When D24/D24.1 move the interior to its own space, only that
  data changes.
- **Parent gate, portability (D25):** besides two touches, the gate passes with two held buttons through the same
  `ParentGate` logic: both gamepad shoulder buttons, or Q + P on a keyboard (`NonverbalMenus.FeedButtons`; tested).
  The gate's hint line still describes the touch spots: a per-scheme hint string is a follow-up. The new UI bakes in
  no "tap" wording (every new control is icon-only), but the bubble's next disc and the skip disc do not yet swap
  glyphs per control scheme.
- **Accessibility names:** the `ui.memory.skip` and `ui.memory.watch_again` keys are not used yet (both controls
  are icon-only).
- **`CaptionStrip`:** gains one property, `RaisedHeight`, so raised captions sit above the taller bubble (a 2-line
  change kept apart from `fix/interior-polish`).
