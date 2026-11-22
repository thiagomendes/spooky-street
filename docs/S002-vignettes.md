# S002: a vignette for every case 01 line, one hybrid reading mode

Branch `feature/s002-vignettes` (from `main` afcb774). Until now only the five key lines of D23.3 had a vignette;
every other line fell back to the v1 card. This pass gives **all 35 `CaseScript` lines** an animated vignette and, by
the owner's change to **D23.1** (2026-11-22), makes the reading mode one hybrid mode for everyone: every line plays
its animation **and** shows its words. "Pictures only" and the first-launch choice are gone.

## What changed

| Piece | Where | Notes |
|---|---|---|
| 28 new vignettes | `Core/Nonverbal/C01Vignettes.Lines.cs` (partial of `C01Vignettes`) | Captions (5), Pip's examines (6), Bea (2), Mr. Vale (5), June (8), Aunt Kit (2). Shared: June's two greetings → `vg.june.greet`; the owl-bow caption → `vg.june.resolve`. 36 vignettes in all (with the 5 key lines and 3 notebook strips). |
| Line table | `C01Vignettes` | `LineTable` maps every line; no row has `UsesV1Strip` (the property stays as the fallback for future lines). `ScopedLineKeys` is now every line; `KeyLineKeys` keeps the original five. |
| New puppet parts (4) | `PuppetParts` V3, `tools/ui_mock/puppet_atlas.py` | `pg.wave_hand` (open bone-paper palm, orange felt cuff: hello), `pg.sill_snag` (the round window's base, wooden sill, silver tuft on a nail: the sill clue), `pg.latch_bar` (the door's wooden bar with iron brackets: lifts as one piece), `pg.sparkle` (four-point candy twinkle). Silver only on the snag (a clue). |
| Atlas | `Content/Resources/UI/puppet_atlas.png`, `PuppetAtlasLayout.cs` (regenerated) | The first-launch art (`x.book`, `x.pictures`) is retired; `pg.wave_hand` and `pg.sill_snag` take those two cells and the other two parts take free cells, so **every existing rect is unchanged** (diffed). 83 parts + 9 extras. |
| Captions | `CaptionStrip` | A case 01 caption also plays its vignette on a small night-felt stage (108 × 497 units, the bubble's stage shape) standing on the strip's top edge; the words, the direction disc and the timing are unchanged. Hints and notebook notes are not case lines and keep the strip alone. Pause freezes the stage (game time). |
| Hybrid mode (D23.1) | `NonverbalSettings`, `NonverbalMenus`, `NonverbalLayer`, `DialogueView`, `NotebookView`, `en.json` | `ShowSpeechText`, `FirstLaunchChoiceMade`, `ChooseReadingMode` and the first-launch cards are removed; the settings card has only Helper behind the parent gate. Settings schema 2; schema 1 files still load (Helper kept, old fields dropped on the next save). The keys `ui.reading.choice_words`, `ui.reading.choice_pictures` and `ui.settings.show_speech_text` are removed from `en.json` and `LocalizationKeys`. The bubble always shows its text line; the notebook always shows its words, and its final card keeps its found-clue stickers. |
| Offline renderer | `tools/vignette-dump` (C#, samples the real data with `VignetteSampler`), `tools/ui_mock/vignette_sheet.py` | Draws every vignette the way `VignettePlayer` does (parts, flipbook, ring/puff/knock overlays) with the baked atlas: contact sheet and optional GIFs. |

The mapping table (line key → vignette → what it shows) is in `docs/nonverbal-ux.md` §3.

## Design rules kept

- 2–4 s, 1–3 beats, at most two moving parts at any instant (`VignetteValidator`; the dump tool fails on any error).
- One idea per beat; motion grammar as in §2 (⊘ thumps = never/not, the stitched key ring = this matters, puff = goes
  in/opens, knock rings in the knock, knock … knock rhythm).
- Clue lines show the clue itself: the silver cloth on its splinter, the snag on the sill, the foil streamer next to
  June's cape with ⊘ on the foil, the knight-helmet sweet, the latch bar, the gap in the arm.
- Subjects are drawn at 1.1–1.6× the old scale so each reads at phone size on the 215-unit stage.

## Tests

- **Core (`tools/core-tests`, also EditMode):** `VignetteTests` — every line has a vignette and none keeps the v1 card
  (35 lines); every vignette validates (36); every vignette is used; shared vignettes; clue lines show their clue
  parts and ring them; knock lines keep the rhythm. `PuppetAtlasLayoutTests` — the atlas holds exactly
  `PuppetParts.All` (v3 included), every part resolves, no overlaps, the retired art is gone.
  `NonverbalSettingsTests` — defaults, schema 2 round trip, **a schema 1 file from the reading-mode era still loads**
  (Helper kept, old fields dropped on save), crash safety, newer files kept aside, the retired strings are gone.
  Local run: 396/396.
- **PlayMode `NonverbalUiTests`:** `EveryCaseLineShowsItsVignetteAndItsTextLine` plays every line of every speaker
  (Bea, Vale, June, Kit, Pip) and asserts the bubble plays the mapped vignette with puppets on stage **and** the text
  line is visible; every caption plays on the strip's mini stage with its words; a hint has no stage.
  `NoReadingChoiceAtFirstLaunchAndOldSettingsStillLoad` writes an old Pictures-only file, loads, and checks there are
  no first-launch cards and the words still show. The parent-gate test now checks there is no words row.
- **Visual `VignetteCaptureTests`:** frames 40–44 (below). `NonverbalCaptureTests` drops frame 28 (first launch).

## Evidence

RESULTS_PLACEHOLDER

## Known gaps

- **Hints** show in the caption strip as words only; their pictures (tier 1 sparkle, tier 3 ghost hand) belong to
  `feature/s002-hints-close`.
- **Babble sync:** beats are timed for a 2–4 s line; there is still no babble voice to sync to.
- **Reduced motion** uses the existing beat-hold rule for the new vignettes; not reviewed frame by frame.
- **Mini caption stage** sits above the strip; with a raised strip over the bubble it covers part of the world for
  the caption's 3.5 s.
