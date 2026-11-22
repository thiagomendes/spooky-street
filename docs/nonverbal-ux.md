# Non-verbal UX (design sketch, for owner review)

Owner request (2026-11-22): "besides the text dialogues we need a NON-VERBAL UX too, thinking of younger children."
Goal: pre-readers (about 4–7) can understand and finish *Three Knocks on Lantern Lane*; readers keep every word.
This is a sketch, not an implementation. Mocks: `docs/evidence/s002/nonverbal/` (`nv_dialogue_bea.jpg`,
`nv_dialogue_vale.jpg`, `nv_notebook.jpg`, `nv_hint.jpg`, `nv_onboarding.jpg`, `nv_icons.png`), drawn by
`tools/ui_mock/nonverbal_mock.py` over the S002 CI frames. Yellow sticky notes in the mocks are design notes, not UI.

## 1. Principles
1. **Show, don't tell.** Every essential beat (spec §3) reads with text off: knocks → two witnesses disagree →
   hedge hides the side → treehouse route → June → latch → cam → crank → owl bows. Spec §2 already promises "the
   explanation is visible even with all text skipped"; this pass extends that to dialogue, notebook, hints and tips.
2. **Text is a layer, not the carrier.** A `Reading` preference: **Pictures** (no text in play, notebook or hints;
   menus keep icon + word) and **Pictures + Words** (pictogram strip first, one small Andika line below). Readers
   can choose a third, **Words**, which is today's UI with the strip shrunk to a 64 pt row. Default: Pictures + Words.
3. **Babble + emote + gesture + pictograms = one line.** Characters speak in synthesised babble (D20). Each line
   pairs a 2–4 icon *pictogram sentence* with an emote balloon and a body gesture that points at the world.
4. **The world answers, not the HUD.** Glow, sparkle, look-lines, paper-bat trails and sound rings guide; the HUD
   only confirms.
5. **Same grammar everywhere.** Arrow = "goes to / causes", `=` = "same as", orange ring = key clue, ⊘ = "not",
   pencil thought-cloud = "earlier" (past tense), dots on an orange badge = order (1, 2, 3 without digits).
6. **Never by colour alone** (spec §6): every state also changes shape, icon, motion or position.

## 2. Pictogram speech
**Card.** Same stitched paper card as today. The portrait becomes the character's pictogram face in a felt ring
(speaker colour: Bea orange, Vale purple, June violet, Kit plum, Pip night). Chips are 64–72 pt die-cut paper
stickers; connectors are purple felt strokes. The name tab, "Tap to continue" and "Skip" text disappear in
Pictures mode (next disc, skip disc and line pips stay). A clue word underline becomes an orange stitched ring on its chip.
**Speaking animation.** Chips flutter in one by one on the babble syllables (120 ms apart, ease-out-back; reduced
motion: cross-fade). While the babble plays the portrait mouth swaps between 3 shapes, three candy arcs pulse
beside it, and the speaker in the world plays Talk with a head nod on each chip. The gesture fires on the chip that
names a place and points the arm at the real object. The emote balloon pops above the head for 1.2 s at line start.
Lines play until tapped. Gestures marked (new) need clips; existing clips: Bea Talk/Wave, Vale SitTalk/Wave,
June Talk/KneeTap/Wave/Point, Kit Talk/OpenBooth/Wave.

| Key (`c01.…`) | Pictogram sentence | Emote · expression · gesture |
|---|---|---|
| `caption.arrival_knocks` | [knock3] [house] + direction arrow | sound ring travels from the attic window |
| `caption.foyer_hum` | [ear] [floor_hum] | floor planks shiver three times |
| `caption.grille` | [grille] → [cam×3] [owl] | follower lifts three times behind the bars |
| `caption.arm_linked` | [arm] [connect] | click sparkle on the joint |
| `caption.crank_gap` | [crank] → [gap] | ? · one tap, the empty socket blinks |
| `caption.owl_bows` | [knock3] = [owl_bow×3] | sound ring outside, owl bows in sync |
| `examine.streamer` (Pip) | [streamer] ≠ [cloth] | huh · head tilt, rubs foil |
| `examine.cloth` (Pip) | [cloth!] + sparkle | wow · lean in, crouch (sketch) |
| `examine.sill` (Pip) | [window] [cloth] = [cloth] | wow · points from snag to cloth sticker |
| `examine.door_stuck` (Pip) | [stuck] (door wiggle + lock) | huh · tug twice, shrug |
| `examine.bowl` (Pip) | [bowl] | love · peek |
| `examine.bowl_sweet` (Pip) | [sweet] → [pip] | love · hug the helmet |
| `line.bea.saw_cape` | [eye] [cape!] → [house_side] | wow · wide eyes · **Point (new)** at the side window |
| `line.bea.repeat` | [hedge_cape] · [feet] ⊘ | think · hand flat at hedge height |
| `line.bea.door_open` | memory( [cape] → [house_side] ) · [door_open] | proud · Point (new), then nod |
| `line.bea.closed` | [owl_bow×3] → [bike] [heart] | love · Wave, spins wheel |
| `line.vale.door_shut` | [bench] → [eye] [door_shut!] | think · SitTalk, arm toward the door; dotted look-line bench → door |
| `line.vale.invite_sit` | [pip] → [bench] [eye] | love · pats the bench (new, or SitTalk) |
| `line.vale.repeat` | [moon] [door_shut] [quiet] | think · arms folded |
| `line.vale.was_shut` | memory( [bench] [eye] [door_shut] ) | huh · scratches bandage |
| `line.vale.open_now` | [door_open] | wow · bandage slips over the eye |
| `line.vale.closed` | [bench] [eye] [owl_bow×3] | wow · Wave |
| `line.vale.saved_sweet` | [sweet] → [pip] | love · holds out the helmet |
| `line.june.greet` | [pip] → [window] · memory( [knock3] → [june_window] ) | wow · Wave, then KneeTap rhythm |
| `line.june.greet_shortcut` | [pip] → [window] [star] · memory( [knock3] ) | proud · claps, Wave |
| `line.june.door_sticks` | [stuck] [hands] ? | huh · Point down the stairs |
| `line.june.door_already_open` | [door_open] [pip] [star] | wow · hands on cheeks |
| `line.june.latch_reminder` | [latch] → [door_shut] [hands] | huh · Point at the latch |
| `line.june.latch_thanks` | [hands] → [door_open] [check] | proud · high five (new) |
| `line.june.listen_floor` | [ear] [knock3] → [floor_hum] | think · kneels, KneeTap |
| `line.june.go_outside` | [june] [pip] → [porch] [eye] | love · Point to the door, walks |
| `line.june.resolve` | [knock3] = [owl_bow×3] | tune · KneeTap ×3 in sync |
| `line.june.closed` | [knock3] [moon] | tune · KneeTap loop |
| `line.kit.first_show` | [booth] [star] → [porch] | proud · OpenBooth |
| `line.kit.thanks` | [owl_bow] [heart] → [pip] [june] | love · Wave |

`memory( … )` = chips inside a pencil thought-cloud on darker paper: the past-tense variants (`vale.was_shut`,
`bea.door_open`) stay truthful without grammar. Lines with two groups show them on one row with a 24 pt gap.

## 3. World cues instead of instructions
- **Next useful thing:** the active point of interest keeps the brightest value (art bible §3) and gains a slow
  candy sparkle (2–3 felt stars, 0.6 Hz). Only one thing sparkles at a time: the current objective's target.
- **Characters point:** a witness who mentions a place turns head and arm to it; a dotted candy *look-line* draws
  from their eyes to the object for 2 s (Vale's bench → front door; Bea → side window). Bea's line sits behind the
  camera in the plaza, so an edge arrow at the screen rim shows where her arm points.
- **Soft path:** paper bats (or Pip-sized felt footprints indoors) appear one by one from Pip toward the goal, fade
  after ~8 s, and never block movement. Used by hint tier 2 and the first-visit arrival (plaza → blue house).
- **The knock:** three amber pulses at the attic window (already wired) + a candy *sound ring* travelling along the
  street at audio speed, rhythm short-short-pause-last, plus haptic taps in the same rhythm (toggle). Indoors the
  floor planks shiver and dust hops on each knock.
- **Stuck door:** Use on the porch → the door wiggles twice (anticipation, two failed tugs), a felt padlock
  pictogram pops above the handle and Pip shrugs. Shut-but-free doors never wiggle: Vale's "shut" ≠ "stuck".
- **Gap before the arm:** the crank taps once, the empty socket blinks orange and a ghost arm outline shows where
  the piece goes.
- **Clue pickup:** the found thing lifts, spins once and flies as a sticker into the notebook disc, which bumps.

## 4. Picture notebook (`nv_notebook.jpg`)
- **Questions as drawings:** Q1 [june] [?] [house]; Q2 [knock3] [?] [house_cut]; Q3 [knock3] → [?]. Order shows
  as 1/2/3 dots on an orange badge (no digits). The heading and case name are drawn, not written.
- **Answers as comics:** Q1 [treehouse] → [bridge] → [june_window]; Q2 [wind] → [cam] under [floor_hum];
  Q3 [cam] → [owl_bow×3]. Panels fill one by one when the proof lands (fact-driven, so save/restore rebuilds them),
  then the green check stamp thumps down with the closure motif. Unknown answers show the pencil "?" silhouette
  in a dashed frame with empty panel slots.
- **Clue stickers:** each annotation is a sticker of 1–2 glyphs: knocks [knock3 house], Bea [bea cape], Vale
  [vale door_shut], bench [bench hedge], streamer [streamer ⊘], cloth [cloth], sill [window cloth], June
  [june treehouse], sweet [sweet]. Unfound = dashed outline with "?"; found = it flies in and sticks with a peel
  sound. Tapping a sticker replays its moment: the speaker's pictogram line with babble, or a 2 s camera vignette
  of the clue spot. Tapping is never required.
- **Paper bats:** 10 bat outlines per page fill in black paper; a full row unlocks a sketch page or cape colour.
- **Final card:** a comic page of the player's actual discoveries in order, the owl bowing, two icon buttons:
  home-street (back) and a circular arrow for replay with a pictogram confirm (notebook pages flying away + ✓/✕).

## 5. Hints for pre-readers (`nv_hint.jpg`)
The radio stays the hint button (icon only). Each press advances a tier; three candy pips show the tier.
Tier 1 **look**: radio bubble with [eye]/[ear] + the subject + ?; the subject sparkles. Tier 2 **where**: route
pictograms + the ghost path + a floating beacon sticker over the goal (seen through walls, fades on arrival).
Tier 3 **show me**: the ghost hand appears over the HUD and demonstrates the exact action (walk there, then press
Use; on the target the hand taps the Use disc showing the right verb glyph).

| Objective | Tier 1 look | Tier 2 where (+ ghost path to) | Tier 3 show me |
|---|---|---|---|
| arrival | [ear] [knock3] ? | [pip] → [house] (plaza) | hand drags the stick forward |
| check_view | [bench] [eye] [hedge] ? | [pip] → [bench] | hand walks to bench, taps Use (Sit) |
| find_way_in | [hedge_cape] ? | [pip] → [treehouse] → [window] | hand walks the ramp, taps Jump at gaps |
| meet_june | [lamp] [window] ? | [pip] → [june_window] | hand taps Use (Talk) on June |
| open_door | [stuck] ? | [pip] → [latch] (foyer) | hand taps Use (Lift) |
| find_source | [ear] [floor_hum] ? | [pip] → [grille] | hand taps Use (Look) |
| link_arm | [gap] ? | [pip] → [arm] | hand taps Use (Connect) |
| turn_crank | [connect] → ? | [pip] → [crank] | hand taps Use (Turn) |
| return_to_porch | [ear] [owl_bow] ? | [pip] → [porch] | hand walks out the front door |
| free_roam | [star] [bike] [bench] [treehouse] | ghost path to the nearest missing clue | crank → [owl_bow] |

## 6. Onboarding by demonstration (`nv_onboarding.jpg`)
Text tips (`prompt.first.*`, `tip.move.*`) become **ghost-hand demos**: a translucent bone felt hand over the
real control, looping twice, plus a small paper card that shows cause → effect. Shown once per move; repeated only if
the move goes unused for the scheduler's cooldown (`TipScheduler` rules unchanged). Performing the move dismisses it.

| Tip | Ghost hand | Card picture |
|---|---|---|
| `prompt.first.move_*` | drags the stick knob in a slow circle | [tip_walk]: stick with a drag arrow |
| `tip.move.hold_jump` | presses and **holds** Jump; candy ring fills | [tip_jump] small arc vs [tip_hold] tall arc |
| `tip.move.chain_jump` | taps Jump on each landing (three beats) | [tip_chain] three rising arcs |
| `tip.move.glide` | taps Jump, then holds while Pip falls | [tip_glide] cape open, slow dotted fall |
| `tip.move.dive` | in the air, taps Use (roll glyph) | [tip_dive] arc then forward streak |
| first Use target | taps Use when the marker bobs | verb glyph on the Use disc |

## 7. Feedback without text
- **Success:** green check stamp (shape + colour), a shower of felt confetti and paper bats (reduced motion: a
  single star burst), the rising three-note wood motif, a short haptic, and the nearest character cheers (Wave / emote).
- **Fact gained:** sticker flies to the notebook disc, disc bumps, a candy "new" dot appears on it (no text badge).
- **Not yet / wrong:** never a buzzer or red cross. A soft wooden *tok*, the object wiggles, Pip shrugs and a "?"
  emote shows; if it is a known blocker (door, gap) the cue from §3 plays. Repeats stay identical, never louder.
- **Case closed:** the arrival composition, owl bows ×3, Kit's OpenBooth, the notebook stamp on the comic page.

## 8. Menus
- **Title:** baked logo (product name stays, it is art), case strip as a drawing of the blue house; big orange
  play disc (Continue shows Pip's face + the bat count as bat silhouettes), a smaller "new" disc (blank page +
  sparkle), gear disc. Targets ≥ 65 pt for primary, ≥ 50 pt for others; spacing ≥ 12 pt.
- **Pause:** resume (play), notebook, settings (gear), home (back to title, behind a hold-to-confirm ring).
- **Settings:** each row = icon + toggle with a picture of its effect: Reading mode (three cards: picture / picture +
  line / lines), captions size (small/large "Aa" also drawn as two bubble sizes), caption backing, reduced motion,
  vibration, left-handed (mirrored hands), volumes as three sliders with speaker/music/voice icons.
- **Parent gate:** language downloads, credits links and any future external link sit behind a gate that a
  pre-reader cannot pass by chance and a parent passes in seconds: *press and hold two marked spots for 3 s*
  (no reading, no maths in English). Destructive actions (New game, Replay) use a pictogram confirm, no gate.

## 9. Accessibility interplay
- **Colour-blind safe:** every pictogram has a distinct silhouette; key clue = orange **stitched ring** (shape),
  found = stamp + check (shape), unfound = dashed outline (pattern). The silver cape is told by its tear notch.
- **Reduced motion:** chips cross-fade, no flutter; ghost path shows all bats at once and fades; ghost hand
  becomes a static hand with a pulsing ring; sound ring becomes a single expanding outline; confetti → one burst.
- **Deaf / muted (A11Y-01):** captions stay. In Pictures mode the caption strip shows pictograms ([knock3] [house])
  with the direction arrow; in Pictures + Words the bracketed text follows. Babble always has its strip. Haptic
  knock rhythm is optional extra.
- **Screen readers / VoiceOver:** each pictogram ID has an accessibility label key (`pg.<id>.label`), so a strip
  reads as a sentence; the existing text key is read for the whole line.
- **Motor:** larger targets in Pictures mode (Use 72 pt), hints can play tier 3 automatically if the player stalls
  for 3 min (optional "helper" toggle, default off).

## 10. Icon inventory (see `nv_icons.png`)
All are die-cut bone-paper stickers, felt-cut fills, 1 ink outline weight; IDs are `pg.<id>`.
- **Characters:** `pip` vampire-bat child, felt ears with berry lining, tiny fangs · `bea` pumpkin bike helmet,
  rust hair, leaf collar · `vale` mummy wraps, bandage over one eye, grey moustache · `june` violet witch hat with
  candy band and orange star, silver collar · `kit` orange headscarf, gold hoops, magenta shawl.
- **Places:** `house` indigo gable, glowing round attic window · `house_side` same, side window ringed orange ·
  `house_open` door lit warm · `house_cut` floor line with "?" · `window` round-topped lit window on indigo ·
  `june_window` June inside it · `door_shut` plum panelled door · `door_open` swung door, warm inside ·
  `stuck` door + wiggle marks + padlock · `lock` felt padlock · `latch` wooden bar in brackets, lift arrow ·
  `hedge` bumpy green felt · `hedge_cape` cape tip above hedge · `bench` purple slatted bench · `treehouse`
  maple canopy, purple hut, ladder · `bridge` plank bridge · `porch` steps, rails, star · `bike` violet bicycle ·
  `lamp` amber attic lantern · `pin` orange place pin.
- **Clues:** `cape` silver cape with triangular tear and stars · `cloth` silver scrap on a splinter · `streamer`
  lavender foil ribbon (never cape silver) · `feet` striped stockings and shoes · `sweet` candy in tiny knight
  helmet · `bowl` candy cauldron · `candy` wrapped sweet.
- **Machine:** `knock3` short-short-pause-last dots with impact ticks · `knock_ring` travelling sound rings ·
  `floor_hum` planks with three hum squiggles · `grille` bars with the cam behind · `cam` three-lobed wooden cam ·
  `arm` orange/purple striped arm · `connect` arm + peg + check · `gap` arm, peg, dashed gap, "?" · `crank` wheel,
  handle, turn arrow · `wind` blue gusts + rooftop pinwheel · `owl` indigo owl with window eyes · `owl_bow`
  tilted owl with bow arrow · `booth` striped awning, curtains, owl.
- **Senses, logic, feelings:** `eye` · `ear` with sound arcs · `hands` two hands helping · `pointer` bone glove
  hand (also the ghost hand) · `question` · `check` witch-green tick · `not` orange ⊘ · `heart` · `moon` · `star` ·
  `bat` paper bat · `memory` pencil thought-cloud (past) · `quiet` sound off. Connectors (not stickers): →, =, ≠, +.
- **Moves:** `tip_walk` stick + hand · `tip_jump` small arc · `tip_hold` tall arc with the small one ghosted ·
  `tip_chain` three rising arcs · `tip_glide` cape spread, slow dots · `tip_dive` arc + forward streak.
- **Emotes (balloons):** `emo_wow` ! · `emo_huh` ? · `emo_love` heart · `emo_tune` note · `emo_think` … ·
  `emo_proud` star.
Existing UI line icons (jump, examine, talk, radio, gear, close, skip, next, home…) stay for chrome.

## 11. Implementation notes (maps onto the current code)
- **Data, engine-free (`Core`):** new `Core/Pictograms/PictogramSequence` = `{ string Id; PicToken[] Tokens;
  Emote Emote; Gesture Gesture; int GestureToken }` with `PicToken` = glyph ID + flags (`KeyClue`, `Times3`,
  `Memory`) or a connector. One table `C01Pictures` keyed by the **same localization key** as the line
  (`c01.line.bea.saw_cape` → sequence), so `ScriptLine` needs no new field: `CaseScript` stays the single source and
  the picture lookup is `PictureFor(line.Key)`. Glyph IDs (`pg.cape`) are language-neutral and never translated;
  accessibility labels are new keys `pg.<id>.label` with translator context in `en.json`.
- **Tests (`Tests/EditMode/Core`):** every `CaseScript` line key, caption key, hint key, notebook question/note and
  move tip has a sequence; every token is a known glyph; 2–4 chips per group; memory only on past-tense lines.
- **`DialogueView`:** add an icon strip row (chips from a `PictogramAtlas` sprite sheet baked by
  `nonverbal_mock.py --export`, like `ui_mock.py --export`); the text label becomes optional by mode; portrait
  uses the face glyph; emote balloon and gesture are raised as events for `Witness` to play.
- **`CaptionStrip`:** pictogram variant keeps the direction cue; `LargeText` hook unchanged.
- **`NotebookView`:** picture mode swaps question labels for strips, answer text for comic panels, slip labels for
  stickers; facts drive panels exactly as today (`CaseNotebookTags` gains glyph pairs).
- **`HintSession`:** unchanged tiers; `CaseHints` gains a picture row per objective plus a world target ID, and a
  new `HintGuide` runtime component draws the ghost path/beacon and the tier-3 ghost hand.
- **`MoveTips` / `MoveTipView`:** each `MoveTip` maps to a demo (control + gesture + card glyph); `TipScheduler`
  timing stays.
- **Settings:** `PreferencesData.ReadingMode { Pictures, PicturesAndWords, Words }` (schema 1 → 2 migration with
  default PicturesAndWords). Saved progress never stores display text or glyph IDs, only facts (unchanged).
- **Art pipeline:** glyphs stay procedural (Python now; final sprites baked at 256 px @3x, reviewed by rendering, as
  for the UI). Gestures marked (new) go to the Blender character scripts.

## 12. Open questions (recommendation first)
1. **Default reading mode?** Recommend *Pictures + Words* for everyone, with a first-launch picture choice
   (child face with book / child face with pictures) so a parent can set *Pictures* in one tap.
2. **New gesture clips** (Bea Point, Vale bench-pat, June high five): recommend Bea Point now (her line depends on
   it); the other two can fall back to existing clips.
3. **Tier-3 auto-help after a stall:** recommend an opt-in "helper" toggle, default off, so play stays unhurried.
4. **Parent gate style:** recommend hold-two-spots for 3 s over a written question, since a written gate excludes
   non-English parents too.
5. **Notebook sticker replay** (camera vignette vs. line replay only): recommend line replay first (cheap), vignette in S004.
