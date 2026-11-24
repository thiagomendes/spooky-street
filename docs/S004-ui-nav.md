# S004 stream F1: UI navigation base

Branch `feature/s004-ui-nav`. Plan: `docs/S004-plan.md` §3 "Stream F1"; rules: `docs/portability.md` (D25). This
adds one way for every screen to work by gamepad and keyboard, and migrates the notebook onto it with no change in
behaviour. Touch is untouched: every screen still answers taps on its own buttons. No new strings.

## What other streams use (wave 2: keepsakes trunk, language screen, parent page, settings)

1. **A focus graph per screen** (`Core/Flow/UiFocusGraph.cs`, engine-free). Place each focusable element on a row
   and column grid; the order you add them is the focus order.

   ```csharp
   var graph = new UiFocusGraph("settings")
       .Add("close", 0, 3)
       .AddRow(1, "music", "voices", "effects")
       .Add("done", 2, 1);
   graph.Exit = "close";          // required: Back always does what this element does
   graph.DefaultFocus = "music";  // optional: else the first enabled element
   ```

   Left and right move along a row and wrap (`WrapRows`, on by default). Up and down go to the nearest row with an
   enabled element and pick the closest column, the left one on a tie (`WrapColumns`, off by default). `Link(from,
   direction, to)` overrides the grid for odd layouts. Disabled elements are skipped and never hold focus.
   `Validate()` lists every rule break: no exit, a broken link or default, an element that cannot be reached, a trap
   with no way back. Call it in a Core test for **every state** of your screen (see
   `EveryStateOfTheNotebookShapedScreenIsSound`).
2. **The screen helper** (`Runtime/UI/UiFocusScreen.cs`):

   ```csharp
   var nav = UiFocusScreen.Attach(screenRoot, graph, overlay); // overlay draws above the elements
   nav.Bind("close", closeButton);                // Selectable: Submit clicks it like a tap
   nav.Bind("music", musicRow, () => ToggleMusic()); // or any RectTransform + action
   nav.PageLeft = …; nav.PageRight = …;           // optional, the bumpers / Q and R
   nav.BackOverride = () => LetGoOfHeldThing();   // optional, true keeps the screen open
   nav.AddPrompt(ControlPrompt.Back, parent, anchor, position, 56f); // glyph that follows the scheme
   ```

   It owns its own `UiNavigateActions`, on while the root is active, off when it is hidden, disposed with it. Submit
   presses the focused element exactly as a tap would; Back runs the exit element's action, so the close disc (touch)
   and East/Escape (pad, keys) are one code path. Hidden or non-interactable elements drop out of the graph each
   frame. The outline shows on gamepad and keyboard and hides on touch (`ControlSchemeTracker`). The helper clears
   the uGUI selection so the default UI module never moves focus a second time. `Navigate`, `Submit`, `Back` and
   `FocusOn` are public for tests.
3. **The action map** (`GameInput.CreateUiNavigate()` → `UiNavigateActions`, map `UiNavigate`): `Left`, `Right`,
   `Up`, `Down` (arrows, WASD, d-pad, left stick), `Submit` (Enter, Space, E, South), `Back` (Escape, Backspace,
   East), `PageLeft` (Q, Page Up, left bumper), `PageRight` (R, Page Down, right bumper), `Rewatch` (V, North).
   `DirectionPressed()` and `Pressed(action)` are edge-triggered and false while the map is off. Use it directly only
   for a screen that is not a grid (the notebook's cursor).
4. **The outline** (`Runtime/UI/UiFocus.cs`): `UiFocus.Create(overlay)`, `Show(target)`, `Hide()`. A candy-corn
   running stitch round the target (`docs/ui-language.md` focus ring; the key ring is retired from case content,
   `mystery-first.md` §8), never a raycast target, following its target every frame.
5. **Glyphs** (`ControlGlyphs`): new prompts `Submit`, `Back`, `PageLeft`, `PageRight`, `Rewatch`. The keyboard
   glyph's path is the first binding of the matching action (Enter, Escape, Q, R, V), and the gamepad glyph is
   always bound (South, East, the bumpers, North); `TheNavigationMapBindsWhatItsGlyphsShow` checks both.

## The notebook migration

- `NotebookView.Deduction.cs`: the inline `Notebook` map (nine actions) is replaced by one `UiNavigateActions`;
  `TickInput` reads it with the same priorities (direction, then Submit, else Back, else page left, else page
  right). Bindings are identical, so behaviour is unchanged. The candy ring becomes a `UiFocus` (same stitch, same
  margins), now rotating with a tilted sticker and never catching touches.
- `NotebookRewatch.cs`: its own `V`/North map is gone; it reads the notebook's `Rewatch` action while the notebook
  is open.
- The notebook keeps its own `DeductionCursor` model (slots and tray rows); it does not use `UiFocusGraph`, since
  the plan asks for a bindings-only change there. New test hooks: `UiActionsEnabled`, `FocusOutline`.
- No notebook prompts were added: the gamepad glyphs on the deduction tray belong to stream F2 (frame 77).

## Tests

- `Tests/EditMode/Core/UiFocusGraphTests.cs` (15): start and default focus, row wrap, nearest column across rows,
  left-on-tie, disabled elements and empty rows skipped, links (and through disabled targets), tab order, every
  element reachable, a one-way link reported as a trap, an unreachable element reported, Back needs an enabled exit,
  broken links and defaults, an empty screen, unique ids, every state of a wave-2-shaped paged screen, and the
  navigation glyphs on all three schemes. Also run locally by `tools/core-tests` (639 passed).
- `Tests/PlayMode/UiNavigationTests.cs` (5): a sample screen (close disc, three cards, a done button) navigates by
  keyboard (arrows and WASD, Enter and Space, Page Down and Q, Escape exits) and by gamepad (d-pad, South, bumpers,
  a greyed card skipped, Back override then East exits), its prompts switch between a lit pad button, a key cap and
  nothing on touch, and the outline hides on touch; taps still press the buttons; the map binds what the glyphs
  show; the notebook by gamepad: bumpers turn pages, the d-pad moves the ringed cursor, South picks and places,
  East lets go of a held sticker and then closes, and the map is off when closed.
- Existing notebook and rewatch tests unchanged: `DeductionNotebookTests` 4/4 (keyboard cursor included),
  `CutscenePlayTests` 5/5 (rewatch by V and gamepad North), `DeductionCursorTests` 3/3, `ControlGlyphsTests` 6/6.

## CI evidence

Run **#150** (`45159280958`) on `093d215`: **success**. Static checks passed; Unity: EditMode 679/679, PlayMode
136/136, Visual 15/15 (`UiFocusGraphTests` 15/15, `UiNavigationTests` 5/5, `UiFocusCaptureTests` 1/1).

Frames (reserved 80–81 for F1 in `docs/S004-plan.md`; extracted from the job log and reviewed):

- `docs/evidence/s004/ui_nav/80_ui_focus_tray_run150.jpg`: keyboard; "What should the knocks move?" with the
  cursor on the second tray sticker (the loose arm), ringed by the candy-corn stitch outside its own dashed edge.
  Magenta 0.00 %.
- `docs/evidence/s004/ui_nav/81_ui_focus_slot_run150.jpg`: gamepad; the arm is held (lifted, orange stitch) and the
  outline has followed to the first slot. Magenta 0.00 %.

Looking at them: the outline reads clearly on the dark felt tray, but on the cream page round a slot the yellow
stitch is faint (frame 81). That is the S002.5a ring unchanged; stream F2 (accessibility, frame 77) should add the
"2 pt candy outline" from `docs/ui-language.md`, or an ink edge, so focus stands out on paper too. `UiFocus` is the
one place to change it.
