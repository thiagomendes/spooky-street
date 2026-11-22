# S002 hints close: tier-1 sparkle, tier-3 ghost hand, glyphs per control scheme

This increment closes the hint and polish gaps left by `docs/S002-nonverbal-ui.md` (Known gaps) and
`docs/S002-house-v3-wiring.md` (Remaining gaps). The design is `docs/nonverbal-ux.md` §6. The rules come from D23.4
(Helper off by default, tier 3 only on a radio press) and D25 (prompts and glyphs per control scheme, input only
through Input System actions).

Branch `feature/s002-hints-close`. Evidence: CI run 97 (`9fb1772`, after merging `origin/main` with `feature/s002-vignettes`), all green: EditMode 435/435, PlayMode 73/73, Visual 7/7, plus the engine-free core suite.

## What shipped

### Rules (engine-free, `Core/Nonverbal`)

- **`HintPresentation`** decides what each hint shows:
  - Gating:
    - A radio press shows only its own tier's picture. Tier 1 is the sparkle and the head turn, tier 2 the bat
      trail, tier 3 the ghost demo.
    - Without a press nothing shows, unless the Helper is on and `MayAutoShowTier3(stall)`. The Helper then shows
      the sparkle and the demo once per objective. The bat trail stays press-only.
  - The tier-1 target is the objective's interaction point at its marker height, or its anchor raised 1.2 m. A
    target on the other side of the owl house walls moves to the threshold on Pip's side (D24.1), because a sparkle
    behind a wall cannot be seen.
  - The looker is the witness or cast member nearest Pip who is available and on screen, within 16 m.
  - The tier-3 action is chosen in this order:
    - Use, when the objective's own point is the current Use target (or Pip is close and level with it);
    - otherwise the high jump, when the goal is at least 1.2 m above Pip and within 4 m;
    - otherwise a push toward the goal.
- **`ControlGlyphs`** gives the glyph per scheme and prompt:
  - **Touch:** the on-screen control itself.
  - **Gamepad:** a lit face button in a felt diamond, a bumper or the stick. These are shapes, never one platform's
    letters.
  - **Keyboard:** a key cap whose name comes from the binding path, through `InputControlPath`, so it follows the
    keyboard.
  - It also picks the parent gate line and the speech card's "continue" line per scheme. The new keys are in
    `LocalizationKeys.ControlPrompts`, each with translator context in `en.json`.

### Runtime

- **Active control scheme.** `ControlSchemeTracker` starts from `GameInput.DetectPromptDevice()`. After that it
  follows the device of the last performed Input System action, through `InputSystem.onActionChange`, with no
  device polling. Mouse motion alone does not switch the scheme. Tests and captures pin a scheme with `Override`.
- **Tier 1, look (`HintGuide`).**
  - Six pooled paper star flecks, from the new `hint_star` sprite, twinkle at 0.6 Hz on a slowly turning ring around
    the target.
  - They are candy corn `#F6D365` × `GlowLevels.HintSparkle` (0.95), unlit, so under the bloom threshold of 1.
  - They keep at least 5.5 % of the view height, so they read at phone size.
  - The sparkle lasts 6 s and stops when the objective changes, on a reset and on the title screen.
  - The chosen looker turns their head to the target for 2 s through the new `Witness.LookAt(point, seconds)`. The
    head turn is limited to 70°, and the look is skipped while the character gestures.
- **Tier 3, show me (`HintGhostDemo`, on the HUD).**
  - **Touch:** the translucent bone-paper ghost hand (alpha 0.82, pivot on the fingertip) demonstrates the action on
    the real control. It taps the Use disc, or holds the Jump disc while a candy ring fills, or presses the stick,
    drags it toward the goal on screen and holds.
  - **Gamepad and keyboard:** there is no hand. The bound button or key glyph pulses above the same control. A held
    prompt fills a ring. A push tilts the stick knob, or picks W, A, S or D toward the goal.
  - It plays 3 loops of 2.2 s per press, never takes input, and stops when the objective changes.
- **Hint lines with a picture (`NonverbalLayer`).** A hint line in the caption strip carries its tier's picture on a
  small night-felt badge at the strip's corner: the star for tier 1, a paper bat for tier 2, the ghost hand for
  tier 3. Without it the line would be words only. The badge shows only while that hint is the strip's line. It
  also appears for a hint asked from the notebook and for the Helper's stall hint. It sits beside the caption
  vignette stage that `feature/s002-vignettes` added, and leaves `CaptionStrip` unchanged.
- **Parent gate (`NonverbalMenus`).** The line and the spots follow the scheme:
  - touch: "hold both spots";
  - gamepad: "hold both shoulder buttons", with a bumper glyph on each spot;
  - keyboard: "hold Q and P together", with Q and P key caps on the spots.

  The gate logic is unchanged.
- **Next and skip discs (`DialogueGlyphs`, one line in `DialogueView`).** The next discs show their arrow on touch.
  With a gamepad or keyboard they show the advance button or key instead: the south face button, or E.
  - A small glyph of the skip button or key sits on Skip. That is the east face button or Tab, bound in its own
    action map by this component.
  - The v1 card's line reads "Tap to continue" on touch and "Press to continue" otherwise.
- **Memory keys.**
  - `ui.memory.skip` is the small caption beside the replay's skip disc, in the letterbox. It is also the disc's
    accessibility name.
  - `ui.memory.watch_again` is the accessibility name of the notebook's eye.
  - Both go through the new `AccessibleName` component. It keeps the key, so the name follows the language.
  - Nothing depends on `ShowSpeechText` or the first-launch choice: the caption always shows.
- **Workshop nook lamp (`OwlHouseStage`).**
  - A small hanging lamp (ink cord, purple shade, amber bulb at `GlowLevels.NookLamp` 1.2) has its own warm point
    light, `NookTopLamp`.
  - It hangs over the nook's open side, in front of the mechanism, above the 1.6–2.6 m camera band (D24).
  - It lights the cam, the follower and the arm from the cutaway camera's side and stays out of that frame.
  - It hides with the interior.
  - The attic window (4.0) and the rooftop owl stay the brightest points.

### Art

`tools/ui_mock/hint_sprites.py` uses the paper, felt and ink helpers from `ui_mock.py`. It writes three small
sprites to `Content/Resources/UI/`, catalogued in `UiSpriteCatalog`:

- `ghost_hand` (256²): a pointing hand drawn opaque, with a lilac ghost rim and a wrist that fades out. The game
  shows it translucent.
- `hint_star` (64²): a white paper star, tinted in the game.
- `glyph_key` (128², 9-sliced with a 40 px border, so wide keys stretch).

The review sheet is `docs/evidence/s002/hint_sprites_review.png`. It shows each sprite on felt and on paper, at size
and at a third of the size, plus "E" and a stretched "Space" key. The first hand read as a lumpy mitten. It was
redrawn with three folded fingers and a thumb across them before it entered the game. `puppet_atlas.png` and
`PuppetParts` are untouched.

## Tests

- **EditMode Core.**
  - `HintPresentationTests` (9) covers:
    - each press shows its own tier;
    - with the Helper off nothing shows without a press, at any stall;
    - the Helper shows the sparkle and the demo only after the stall;
    - every objective except free roam has a sparkle target;
    - the target sits at the marker height;
    - a target behind the walls moves to the dormer, or to the door once it is open;
    - the looker is the nearest character in view, and nobody looks when no one qualifies;
    - the demo choice.
  - `ControlGlyphsTests` (6) covers touch, gamepad and keyboard glyphs, the move key's direction, the gate and
    continue lines per scheme, and the new keys in `en.json` with context.
- **PlayMode `HintPicturesTests` (7).**
  - A tier-1 press sparkles the bench and Vale looks at it for about 2 s. The hint line carries the star badge. The sparkle stops when the objective
    changes.
  - A tier-3 press shows the translucent hand on touch, ends after its loops, and shows no glyph.
  - On a keyboard the key cap shows; on a gamepad a lit button or the stick shows, never the hand.
  - With the Helper off, nothing shows after a long stall. With the Helper on, the stall shows the sparkle and the
    demo.
  - The parent gate line and spots follow the scheme: Q and P on a keyboard, the bumpers on a gamepad.
  - The dialogue's next and skip glyphs follow the scheme.
  - The memory skip disc carries its caption and its accessibility name.
- **Existing test touched.** `UiLanguageTests.HudChromeStaysDimmerThanBone` now counts `glyph_key` and `ghost_hand`
  as bone surfaces. Both are tinted `PlayPaper`, like the other in-play paper.
- **Every PlayMode and Visual fixture** calls `CaseDirector.UseTemporarySaveDirectory()` in Setup.

## Frames (`docs/evidence/s002/*_run97.jpg`, reviewed by eye)

Iterations:

- **Run 90:** the code compiled and every new test passed. One existing HUD brightness test failed: the key cap and
  the hand were tinted brighter than bone. In the frames:
  - the sparkle was too small and Pip hid the bench;
  - the hand left the screen at the right edge;
  - the nook lamp, behind the parts, lifted the top band only from 28.5 to 35 luma (0–255).
- **Run 93 (green):**
  - the lamp moved to the nook's open side, which lifted the top band to 45.9;
  - the flecks were bigger;
  - the hand came in from the left, but sank below the bottom edge.
- **Run 97 (green, final):**
  - the hand comes from above for the thumb controls;
  - frame 33 keeps the hint line, so its new picture badge shows.

| Frame | What to look for | Verdict |
|---|---|---|
| `33_hint_sparkle_run97` | Tier 1 from 4.6 m: six candy star flecks ring Mr. Vale's bench beside Vale, clear of Pip. The hint line below carries the star badge. No bloom halo. | 4/5. Reads at phone size. Vale's 2 s head turn is too small to see at this distance; PlayMode asserts it. |
| `34_hint_ghost_hand_run97` | Tier 3 on touch, next to the bench (Use in reach). The translucent paper hand comes from above with its fingertip on the Talk disc, mid-tap, with the candy ring. | 4/5. The action is obvious and the hand stays on screen. |
| `36_hint_key_glyph_run97` | The same hint with a keyboard: no hand. A bone key cap "E" pulses above the Talk disc. | 4/5. Clean. A desktop build would hide the touch discs; the glyph would then sit at the same spot. |
| `35_nook_relit_run97` | The cutaway on the third knock. The crossbar, the purple cam, the follower and the striped arm read against the back wall; the top band went from 28.5 to 45.9 luma with no new clipped highlights. The inset owl is still the brightest point. | 4/5. The lamp itself sits above the frame on purpose (D24 camera band). |
| `20_workshop_cutaway_run97` | The regular cutaway frame, same lighting. | Same as 35. |

## Gaps

- **Accessibility bridge.** `AccessibleName` holds the localized names of the memory skip disc and the notebook
  eye. No OS screen reader reads them yet: Unity's accessibility module is not in the manifest, so wiring
  `AccessibilityHierarchy` is a follow-up. The skip disc's caption is visible today; the eye has no visible caption,
  because there is no room on the sticker.
- **Control scheme on device.** `ControlSchemeTracker` switches on the device of the last performed action. It is
  tested only with `Override`. Switching by real devices (an iPad with a keyboard, a paired controller) needs a
  device run.
- **The first prompt.** `GameUi`'s first move prompt still picks its line once, from `DetectPromptDevice`. Captures
  hide it because CI detects a keyboard. Moving it onto `ControlSchemeTracker` is a small follow-up.
- **Gamepad glyph shapes.** The face diamond and the bumper are neutral shapes. Platform button art (letters or
  symbols) waits on the D25 platform order.
- **Skip on other screens.** The new Tab / east-button skip covers conversations only. The cutaway and the memory
  replay keep their own skip input, so their skip buttons show no scheme glyph yet.
- **Helper stall.** The stall shows the sparkle and the demo once per objective, with the tier-3 line. It never
  shows the bat trail (press-only by design).
- **Device.** Not run on a phone. The hand's size, translucency and timing need a device check, which costs a
  TestFlight upload (owner's call).
