# S004 stream E: the grown-ups' corner (settings card, "For grown-ups" page, credits)

Branch `feature/s004-parent-settings` (from `main` e98715c, then merged `main` up to ddc0279: D30 and the translator contexts). Not merged.
Contract: `docs/S004-plan.md` §2.5 and §3 "Stream E"; engagement §2.6, §2.8 and §2.9; D25 portability; F1
navigation (`docs/S004-ui-nav.md`); the parent gate and language screen (`docs/S004-lang-packs.md`).

## What a grown-up sees

1. **The way in.** There is a gear on the title (top right, new) and a gear on the pause card. Each one asks for
   a fresh parent gate: hold two spots for 3 s, or both bumpers, or Q and P. Only then does the settings card
   open. A cancelled or failed gate opens nothing.
2. **The settings card.** The card is a paper card in three columns, built from `Core/Parent/SettingsCard.cs`:
   - **Sound and help:** Helper, Music, Sounds, Voices.
   - **Camera and touch:** Camera speed, Invert up and down, Left-handed controls, Vibration.
   - **Reading and motion:** Reduce motion, Large text, Solid text boxes.
   - **Entries along the bottom:** Language, For grown-ups, Credits.

   Every row except Helper is a `PreferencesData` field that was stored before but had no control. All of them
   are saved in `preferences.json`, so they survive Replay and New game. The card is laid out for the
   1334 × 750 reference size. `UiFitInside` scales it down, never up, to fit the safe area at any aspect ratio,
   4:3 included. Each switch row is one 96-unit touch target, the whole row. A slider takes a 60-unit thumb.
3. **Changes take effect at once.** `GrownUpCorner.Apply` runs on every change, and at boot:
   - **Reduce motion** sets `AmbientMotion.ReducedMotion`, and `NonverbalLayer.ApplyMotion` passes it to the
     dialogue, notebook and captions. The next cutscene cuts instead of blending.
   - **Large text** turns on `CaptionStrip.LargeText` and `DialogueView.LargeText`.
   - **Solid text boxes** makes the caption backing 100 % opaque instead of 94 % (`CaptionStrip.Opaque`).
   - **Invert up and down** and **Camera speed** set `CameraRig.InvertY` and `CameraRig.Sensitivity`
     (0.25–3×, with 1× in the middle of the slider). They apply to touch, mouse and stick alike.
   - **Left-handed controls** runs `GameUi.SetLeftHanded`. It mirrors every HUD element live: the stick zone,
     the jump and use discs, the pause disc, and anything the case added. The stick's ghost and the move-tip slots
     follow.
   - **Vibration** sets `GameHaptics.Enabled`. The game plays no haptics yet, so for now the switch only records
     the choice, and every future haptic must check it.
   - At boot, reduced motion and left-handed can only switch on. This way a default file never overrides the
     scene's own left-handed flag.
4. **The "For grown-ups" page** (`Runtime/UI/ParentPage.cs`). This is a calm, scrolling paper sheet. It is
   rebuilt from the saves each time it opens, by `ParentProgress.Build(case state, the child's answers, street,
   keepsakes, playtime, now)`. It shows these sections:
   - **Cases:** the case's status (not started, in progress or solved) and one picture chip per solved question.
   - **The child's own answers:** each solved question's comic in the child's own order, drawn with the
     notebook's sticker art. The answer words come after the comic.
   - **Places explored:** Lantern Lane, the treehouse, and inside the owl house, each worked out from the facts.
   - **Friends met:** portrait discs of Bea, Mr. Vale, June and Aunt Kit.
   - **Keepsakes:** "12 of 30 paper bats", sketch pages and costume pieces. This is the only place in the game
     with keepsake totals.
   - **Time played this week:** a rounded total ("About 1 h 20 min") and seven day dots. Dot size shows the
     time played that day. There are no goals, no comparisons and no "play more".
   - **What the game practises:** three lines.
   - **Privacy:** three lines.

   There is no share, export or link button. A case the street remembers reads "Solved" even after Replay.
5. **Credits** (`Runtime/UI/CreditsPage.cs`) show:
   - the original-art note;
   - where the voices (Gemini TTS syllables) and the music (Lyria) come from, as D20.2 and
     `docs/asset-provenance.md` describe;
   - Unity and Blender;
   - the two font credits, with **both SIL OFL 1.1 licence texts in full**.

   The licence files moved from `Content/FontLicenses` to `Content/Resources/Licenses`, with their metas kept, so
   they ship in the app. They are shown verbatim and untranslated. Addresses in them are plain text, not links.

## Gamepad and keyboard (F1)

- **The settings card** uses `SettingsCard.BuildFocus()`, a `UiFocusGraph`:
  - the close disc on top, which is the exit;
  - a 3-column × 4-row grid of settings;
  - the three entries along the bottom.

  Submit flips a switch, the same as a tap. Left and right on a focused slider move it one step (10 %). This uses
  a new `UiFocusScreen.DirectionOverride` hook, so the switch rows carry focus across the columns. Back
  (Escape or East) closes the card.
- **The page and the credits** use `ReadingSheetFocus`: the close disc, then the sheet.
  - Down scrolls one step (180 units).
  - Up scrolls back up, and at the top it moves focus to the close disc.
  - The bumpers, or Q and R, scroll a whole page.
  - Back closes the page and returns to the card.
- **Opening a page from the card** hides the card. The page's own Back brings the card back, with no new gate.
- **Language** opens the language screen. Closing it returns to the card.
- **The pause key.** While any grown-up screen is up, and on the frame it closes, `GameBootstrap` ignores the
  pause key (Escape or Start). So one Escape closes the card without also resuming the game.

## Playtime (`Core/Parent/PlaytimeLedger.cs`, `playtime.json`)

- It counts only frames in `GameState.Playing`. Title, pause, cutscene pause and background don't count.
- Each frame adds at most 0.5 s, so the first frame after a suspend adds almost nothing.
- Time goes to the local calendar day, and the last 14 days are kept.
- The week is the seven local days ending today.
- If the clock was set backwards, the "future" days never show, and the next tick drops them.
- The file is written every 30 s of play, and whenever play stops (pause, title, background, quit). It is
  written with the same atomic save slot as the other files, and a newer schema is kept untouched.
- Only the page reads the ledger.

## Strings

- 59 keys in one block at the end of `en.json`, each with translator context, through
  `LocalizationKeys.Parent`. The prefixes are `ui.settings.*`, `ui.parent.*` and `ui.credits.*`.
- Natural pt-BR for all of them, written for an adult reader and fitting the +35 % budget:
  - The child is "a criança", so the page never genders the child. No line addresses Pip (D30).
  - Weekdays are in lower case ("seg", "ter"…).
- `tools/l10n/review_page.py` has a new "For grown-ups and credits" group with notes. `docs/S004-ptBR-review.md`
  was regenerated.

## Tests

- **Core** (Unity EditMode and `tools/core-tests`):
  - **`PlaytimeLedgerTests` (14):**
    - only unpaused play counts;
    - a suspend adds one capped tick;
    - midnight roll-over;
    - 14 days kept, and a 7-day week ending today;
    - a clock set backwards;
    - the file round trip, with bad entries skipped and a newer schema refused;
    - the store writes `playtime.json`;
    - rounding of the total (6 cases);
    - dot sizes.
  - **`ParentProgressTests` (8):**
    - a new game;
    - mid-case places, friends and no comic;
    - the child's own order is the comic;
    - every reachable case state: a question is listed if and only if it is solved, and every comic is a right set;
    - a remembered case stays solved after Replay;
    - keepsake totals come from the found bats;
    - the week comes from the ledger;
    - **no network type in `Core/Parent`**, checked in the source and in the member signatures.
  - **`SettingsCardTests` (5):**
    - every switch flips only its own field and survives a save and reload;
    - sliders step, clamp and persist, and the camera speed mapping round-trips;
    - every stored accessibility field has a control;
    - the card is a sound focus graph, with any one row hidden too;
    - the reading sheets are sound focus graphs.
- **PlayMode `ParentSettingsTests` (4):**
  - the gate guards the card, the page, the credits and the language entry (cancel opens nothing, and a new visit
    needs a new gate);
  - every switch takes effect at once:
    - reduced motion reaches `AmbientMotion` and the next cutscene;
    - large text reaches the captions and the speech card;
    - solid captions;
    - invert and camera speed;
    - left-handed moves the jump disc and the stick zone;
    - vibration;

    and after a fresh boot everything comes back;
  - the card and the page by keyboard and gamepad (Q+P gate, arrows, slider steps, Enter, scroll, bumper page,
    East and Escape close), and Escape on the card never resumes the paused game;
  - playtime counts only unpaused play and is written when play stops.
- **Visual `ParentSettingsCaptureTests`:** frames 75 and 76, the numbers reserved for stream E. See below.

## CI evidence

Run **#188** (`45419804580`) on `1475d1d`: **success**.

- Static checks passed.
- Unity: EditMode 863/863, PlayMode 155/155 and Visual 22/22. Of these:
  - `ParentProgressTests` 8/8, `PlaytimeLedgerTests` 14/14 and `SettingsCardTests` 5/5;
  - `ParentSettingsTests` 4/4;
  - `ParentSettingsCaptureTests` 1/1.
- Locally: `./tools/check-local.sh` passed, with 799 Core tests before the merge from `main`.

Run #185 (`c346c99`) failed on one test. In `TheCardAndThePageWorkByKeyboardAndGamepad`, Q and P were pressed
on the same frame and released before the assert. The test now holds them like the gate's own test does, Q first
and then P, and asserts while they are still held. Run #185's frames also showed two problems:
- the weekday labels under the dots were clipped to nothing, because Andika's line is taller than the 36-unit box
  (now 48 units);
- the "Solid text boxes" picture was a blank paper part (now the lantern).

Both are fixed in #188.

Frames (stream E's reserved numbers 75–76, extracted from the job log with `tools/ci/extract_frames.py`; all
1278 × 590 with 0.00 % magenta). I looked at each one:

- `docs/evidence/s004/parent_settings/75_parent_page_run188.jpg`: the page mid-case.
  - The heading "For grown-ups", the device-only line, and "Three Knocks on Lantern Lane" with an "In progress"
    tag.
  - The ear-and-eye chip with a green check for the solved question.
  - The start of "The child's own answers".
- `75d_parent_page_answers_run188.jpg`: the end of the knocks comic, then its answer words.
  - Places explored: the lantern, June's treehouse and inside the owl house.
  - The portraits of Bea and Mr. Vale under "Friends met".
- `75b_parent_page_lower_run188.jpg`:
  - "12 of 30 paper bats", "1 of 3 sketch pages" and "1 of 3 costume pieces".
  - "About 1 h 20 min".
  - Seven dots from Wed to today, Tue, in bold. Dot size shows the time played that day.
- `75c_parent_page_ptbr_run188.jpg`: the same page in pt-BR. "Para adultos", "Em andamento" and "As respostas da
  criança" all fit.
- `76_settings_card_run188.jpg`: the card in three columns.
  - The sound column: Helper, then Music, Sounds and Voices sliders.
  - The camera column: Camera speed at 1×, mid-slider, then Invert up and down, Left-handed controls, and
    Vibration (on).
  - The reading column: Reduce motion (on, with the keyboard focus stitch), Large text, Solid text boxes.
  - Language, For grown-ups and Credits along the bottom.
  - Nothing is clipped.
- `76b_settings_card_ptbr_run188.jpg`: pt-BR. The longest labels ("Inverter cima e baixo", "Caixas de texto
  sólidas") wrap to two lines inside their rows. "Controles canhotos" fits on one.
- `76c_credits_run188.jpg`: the original-art note, the voices and music, the tools, and the font credits, then the
  start of the Andika licence.
- `76d_credits_ptbr_run188.jpg`: the same in pt-BR. The licence stays in English, as published.

Notes for later (not blocking):

- The page's dashed focus stitch rings the whole sheet on keyboard and gamepad. It reads as "this scrolls".
- Stream F2 may want a scroll hint, such as a fade at the sheet's bottom edge.
- Camera speed and Large text share the eye picture.
- The optional iOS system Reduce Motion plugin (plan §3 E) is not built. A plugin can never be tested in CI here
  without Xcode. The in-game switch covers the need, and the plugin can go behind an interface later.
