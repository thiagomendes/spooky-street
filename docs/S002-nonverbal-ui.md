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

RESULTS

## Known gaps

GAPS
