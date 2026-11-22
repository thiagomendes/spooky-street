# Mystery first: case 01 redesign (approved 2026-11-22, D26)

Owner (2026-11-22, translated): "Doesn't it take the fun out of the game's mysteries, which is the whole point, if
it becomes dull by explaining everything you have to do? … They have to gather the clues and solve the mysteries
themselves." The owner chose a **mystery-first** redesign. This document applies the rules of
`docs/narrative-bible.md` to case 01. It changes **what** the vignettes, captions, notebook, hints and replays show,
not **that** they exist: the hybrid reading mode (D23.1) and the Helper-off default (D23) stay. No game code changes
here. Decision **D26** (approved 2026-11-22) is at the end, with the owner's answers; the spec carries it from v1.4.

Mocks (Pillow GIFs at 1266×585, 15 fps, plus a 6-frame `_sheet.jpg` each) in `docs/evidence/s002/mystery_first/`,
drawn by `tools/ui_mock/mystery_first_mock.py` in the puppet style of the v2 mocks:

| Mock | Shows |
|---|---|
| `mf_bea_testimony` | Bea's new testimony: a silver flash streaks high over the hedge and vanishes by the house's roof; her eye, a "?". No June, no lit window, no pointing ring. |
| `mf_notebook_deduction` | "How did June get in?": three stickers dragged onto three slots; a wrong set shakes and wobbles back, Pip thinks; the right set glows, then the comic answer is drawn. |
| `mf_hint_tiers` | Tier 1: a breeze carries leaves toward the side yard and warms the *area*. Tier 2: the bat trail flies to the area and circles it. No object picked out. |
| `mf_deduction_help` | Stuck on "Where are the knocks from?": Pip muses (listen, then look), and the one clue still out in the world shows its *place* (the house), never its partner. |
| `mf_clue_glint` | Diegetic attraction: the silver cloth flutters on the ramp rail and glints twice; no arrow, no ring. |

## 1. Audit: what over-explains today (verified)

The orchestrator's five findings are all confirmed in code and frames. Four more were found:

1. **Bea's vignette is the answer** (`vg.bea.saw_cape`, frame 24): June runs treehouse → bridge → window, the window
   lights with her inside, a hand points, the key ring pulses. `vg.bea.door_open` repeats it.
2. **The memory replay re-enacts the route** in the 3D world (frame 25): June's ghost runs the bridge into the window,
   which gets the "this matters" ring. It auto-plays on first hearing.
3. **The notebook answers itself.** Answers are written by physical proof (`CaseNotebook.Questions`: entering the
   attic *answers* "How did June get inside?"), and each page shows dashed annotation slots for exactly the clues
   that belong to it (frame 26): the page tells you which clue goes where.
4. **Hints solve.** Tier 1 sparkles the exact object and a character looks at it; tier 3 is the ghost hand pressing
   the action; tier-1 *lines* carry the deduction ("The hedge hides the side of the house. Could someone climb in?").
5. **Captions narrate conclusions**: "[Click. The striped arm is back in place.]", "[Tap. Nothing else moves: the arm
   isn't connected.]".
6. *(new)* **The key ring is a spoiler.** The orange stitched ring ("this matters") rings the cloth, the sill, the
   sweet, the latch, the gap: every real clue is labelled as a clue, and the decoy streamer gets ⊘ stamped on it.
7. *(new)* **Examine lines debunk for the child**: "Smooth foil streamer. Shiny, but not woven." and the streamer
   vignette sets June's cape beside the foil and stamps ⊘. The comparison *is* the deduction.
8. *(new)* **Objective titles narrate the route**: the Continue button reads "Find June's way in", "Fix what's loose",
   "Test the machine".
9. *(new)* **Green check stamps in vignettes** (`arm_linked`, the three answer strips) declare "correct" before the
   child has decided anything.

## 2. Principles (case 01 reading of the bible's rules)

1. **Questions, not instructions.** The game asks three questions; it never tells the next step.
2. **Witnesses report perceptions.** Each sees a piece, through their own position and bias. Vignettes show what the
   witness perceived, at the size and blur they perceived it.
3. **Clues are things in the world** that call for curiosity diegetically: a glint, a flutter, a hum, a draught.
4. **The child connects.** Answers exist only after the child places clues on a question. The world can show
   *effects* (the owl bows when cranked) because the child caused them; the notebook holds the *explanation*.
5. **Help is about getting there, never about meaning.** Hints point to areas and demonstrate moves.
6. **Every reveal pays off what the child found.** The answer comic is drawn from the child's own stickers.
7. **No signal sorts clues from non-clues.** Every examinable that can become a sticker gets the same sticker
   feedback, the decoy included. The "this matters" ring is retired from case content.

## 3. Case 01 questions and clues

Three questions keep their stable IDs. Each appears in the notebook **when the world raises it** (page-flip sound,
the notebook disc wobbles once), not all at start:

| Question (`c01.note.*`) | Raised when | Slots | Accepted (ideas each covered by a different sticker) |
|---|---|---|---|
| `q_source` "Where are the knocks coming from?" | the arrival knocks | 2 | (a) **floor hum** and (b) **wooden piece lifting** |
| `q_entry` "How did June get in?" | Bea or Vale has spoken | 3 | (a) not the front door: **Vale's door** or **stuck door**; (b) silver up high: **cloth** or **sill snag**; (c) the hidden side: **Bea's flash**, **bench view**, or the other snag. No decoy. |
| `q_moved` "What should the knocks move?" | first workshop visit | 2 | (a) an owl: **owl panel** or **owl booth**; (b) **loose arm** |

Stickers (each one a fact, earned by examining or talking; the tray is shared by all questions):

| Sticker | Where and what finding it needs | Diegetic attraction | New? |
|---|---|---|---|
| Bea's flash | talk to Bea (plaza west) | her wheel spins, she waves | rewritten |
| Vale's door | talk to Vale (bench) | candy cauldron steam, he beckons | kept |
| Bench view | sit on the bench | the seat is Vale's invitation | kept |
| Stuck door | Use the front door from outside | warm light under the door, knocks | kept |
| Foil streamer *(decoy)* | examine Bea's bike | lavender foil twinkles in the lamp | kept, reworded |
| Silver cloth | examine the splinter on the ramp rail | it flutters and glints (`mf_clue_glint`) | kept |
| Sill snag | examine the attic sill (outside or in) | a thread lifts in the draught | kept |
| Floor hum | stand in the hall during knocks (auto) | floorboards tremble, dust hops | was a caption |
| Wooden piece | look through the low grille | three slats of light flicker in rhythm | kept |
| Wind wheel *(bonus)* | look at the rooftop wheel from the treehouse deck | it whirs before each knock cycle | **new** |
| Owl panel | examine the painted owl on the grille panel | its eyes catch the nook lamp | **new** |
| Owl booth | examine the shuttered booth outside | ribbons twitch; the owl stays still | **new** |
| Loose arm | examine the striped arm, or crank before linking | it swings when the floor hums | **new** (was a caption) |

The decoy breaks no art rule: the streamer stays **lavender foil** (art bible: cloth silver only for June's cape and
its clues); its trap is the word "silver" in Bea's line and its shine. Woven versus crinkly is the child's
comparison, shown by touch sounds and animation, never by a ⊘.

## 4. Beat map and pacing (first play, a 6–8-year-old, assisted reading)

| # | Beat | Question → explore → clue → connect → reveal | Time | Frustration risk | Soft safety net |
|---|---|---|---|---|---|
| 1 | Arrival | knocks raise **Q-source**; walk to the plaza | 1–2 min | low | lit attic, knock direction disc |
| 2 | Witnesses | Bea's flash vs Vale's shut door raise **Q-entry** | 2–3 min | low | both stand on the main sightline |
| 3 | Look around | bench view, stuck door, streamer (decoy) | 2–4 min | medium: may stop here | the breeze nudge (tier 1) toward the side yard |
| 4 | Climb | side yard → ramp → cloth → bridge → sill | 3–5 min | **high**: side yard hidden; ramp vs crate | tier 2 bat trail; tier 3 jump demo only on the crate route |
| 5 | Connect Q-entry | first notebook use; drag stickers | 1–3 min | **high**: first time with the mechanic | one ghost-hand drag demo on the first sticker; wobble + musing; scaffold (§5.4) |
| 6 | June and the door | meet June, lift the latch together | 1–2 min | low | June's line asks for two hands |
| 7 | The hall | floor hum, grille, owl panel | 2–3 min | medium: grille is low | dust hops toward the grille; tier 1 toward the nook |
| 8 | Connect Q-source | two stickers | 1–2 min | medium | musing (listen, then look) |
| 9 | The machine | loose arm, link it, crank; owl bows outside | 2–3 min | medium: order of arm and crank | crank-early tap shows the dangling arm (no words) |
| 10 | Connect Q-moved, finale | porch; Kit asks "So, detective?"; case stamp | 2–3 min | low | Q-moved is easy after the bow |

Total about 17–30 min for a young first-timer, 12–18 for an older reader (9–12; spec v1.4 §3 target). The density
target (a meaningful event every 15–25 s) holds: each beat above has 2–4 events.

## 5. Deduction mechanic (notebook)

1. **Page.** One question per page (carousel unchanged): order badge, question chips plus the words, 2 or 3 empty
   dashed slots joined by "+", and the shared **clue tray** (felt strip) with every found sticker. Unfound clues are
   *not* shown per page; the tray shows one generic "?" outline per clue still out in the world (count only).
2. **Place.** Drag a sticker onto a slot (it lifts, follows the thumb, presses in with a peel sound); tap a placed
   sticker to send it home. Tap-tap also works (tap a sticker, tap a slot) for motor ease and for gamepad and
   keyboard (D25: cursor moves over tray and slots, Use places). Tapping a sticker in the tray replays its vignette.
3. **Check.** When every slot is full, Pip pauses for 0.4 s, then:
   - **Wrong:** the slots give a small shake, the stickers wobble home in turn (soft wooden *tok*, never a buzzer),
     and Pip's thought cloud shows a thinking face. No count of right stickers, no red.
   - **Right:** the stitches glow orange, the stickers press in, a puff, the three-note motif; the slots give way to
     the **answer comic**, built from the child's stickers (a found bonus such as the wind wheel adds its own panel),
     then the *Found it!* stamp. The stickers return to the tray: they stay usable for other questions.
4. **Scaffold (progressive, per question).** Try 1–2: everything wobbles home. From try 3: stickers that fit stay in
   place with a small pencil tick; the others wobble home. After try 5 with all needed clues found: the decoy (if
   any) is set aside in the tray with a pencil circle ("not this time"). Never more than this.
5. **Rules.** No penalty, no timer, no counter shown. Attempts are in memory only. A solved question writes a fact
   (`c01.solved.entry` etc.); the comic plays twice and holds, as today.
6. **For pre-readers.** Everything above is pictures and motion; the question words sit under the chips (D23.1).
7. **Teaching.** The first time a sticker arrives, the notebook disc wobbles. On the first notebook open with two or
   more stickers, the ghost hand demonstrates one drag onto a slot (a *how-to-play* demo, the same rule as move tips)
   and leaves the sticker there, so the child finishes the set.

## 6. Hint policy (radio)

Hints answer "where can I look?" and "how do I get there?", never "what does it mean?".

- **Target.** Not the canonical next step but the **area** holding an unfound clue for the earliest open question,
  or the route blocker (side yard, treehouse, attic, hall, nook, porch, booth). Areas are authored volumes.
- **Tier 1, nudge.** A breeze carries leaves toward the area and the nearest witness glances that way; one sensory
  line ("A breeze keeps tugging toward the side yard."). No object sparkle.
- **Tier 2, trail.** The bat trail flies to the area's threshold and circles the area; no goal sticker.
- **Tier 3, move demo.** Only when the blocker is a move (the crate hop, the high jump to the awning, a glide gap):
  the ghost hand demonstrates the move on the real controls. Never for Use on a clue, never for a deduction. When
  the blocker is not a move, the radio shows two pips.
- **Stuck on a question** (radio pressed inside the notebook, `mf_deduction_help`): (a) Pip's musing reframes the
  question with two pictures (Q-source: listen, then look; Q-entry: door, then up high; Q-moved: the owl, then the
  arm); (b) if a needed clue is unfound, the tray's "?" outline lights and shows the **place** badge, and leaving the
  notebook starts that area's tier 1. It never shows which stickers belong together.
- **Helper** stays opt-in and off (D23); when on, a long stall offers tier 1 only, never tier 3 or the musing.

## 7. Memory replay decision

**Recommended: keep it, but only as the witness's hazy memory and only on demand.** It no longer auto-plays. The eye
badge on Bea's and Vale's stickers plays it from the notebook, as a clue to examine:

- Bea: the camera glances from Bea's spot at the house's side; a silver blur streaks high above the hedge and is
  gone. No June, no window entry, no ring.
- Vale: the camera holds on the shut door from the bench while the moon moves; no ⊘ in the world.

It keeps value for pre-readers (the real place behind the picture) and costs little (`MemoryReplayView` staging and
ghost data change; the ghost becomes a blur cut-out). Removing it entirely is the cheaper fallback (Q3).

## 8. Spoiler audit (every element, verdict and new content)

Keys are `c01.*`. K = keep, R = rewrite, C = cut. Vignette IDs follow the line unless stated.

| Element | Today | Verdict | New content |
|---|---|---|---|
| `caption.arrival_knocks` + vg | knocks from the house | K | — |
| `caption.foyer_hum` + vg | "The floor hums: knock, knock… knock" | R | "[Knock, knock… knock. The floorboards tremble.]"; becomes the floor-hum sticker |
| `caption.grille` + vg | wooden piece lifts behind owl panel | K | — (perception) |
| `caption.arm_linked` + vg | "Click. The striped arm is back in place." + green check | R | "[Click!]"; arm swings, puff; no check |
| `caption.crank_gap` + vg | "Nothing else moves: the arm isn't connected." ring on gap, ⊘ on owl | R | "[Tap. The striped arm swings in the air.]"; crank turns, arm dangles; no ring, no ⊘ |
| `caption.owl_bows` | "Three knocks. Outside, the owl bows three times." | K | the child caused it (effect, not explanation) |
| `examine.streamer` + vg | "Smooth foil… not woven." cape beside foil, ⊘ | R | "Crinkly, shiny foil." foil swishes and crackles; no cape, no ⊘ |
| `examine.cloth` + vg | "Silver cloth. One corner's torn." ring, cape slides in | R | "Soft silver cloth, caught on a splinter." cloth flutters; no ring, no cape |
| `examine.sill` + vg | "Another silver snag" ring, cape | R | "A silver thread on the sill." thread lifts in the draught |
| `examine.door_stuck` + vg | "It's stuck on the inside." padlock ringed | R | "It won't budge." door rattles; no padlock |
| `examine.bowl`, `bowl_sweet` | flavour, bonus | K | drop the ring on the sweet |
| *new* `examine.wind_wheel`, `owl_panel`, `owl_booth`, `loose_arm` | — | new | "Whirr! The little wheel spins." / "A painted owl, wings folded." / "A wooden owl behind a shutter. Very still." / "This striped arm hangs loose." |
| `line.bea.saw_cape` + vg | "I saw June's silver cape go in. Right there!" June runs into window, point, ring | R | "Something silver flashed up high, by the side of the house!" (`mf_bea_testimony`) |
| `line.bea.repeat` + vg | "Only the cape, above the hedge. I couldn't see her feet." | R | "Up high, over the hedge. Then it was gone!" flash only |
| `line.bea.door_open` + vg | memory cloud of June's route | R | "The door's open now? That silver flash was long before." cloud shows the flash |
| `line.bea.closed` | owl on her bike | K | — |
| `line.vale.door_shut` + vg | door, clock, ⊘ | K | his claim, true and partial |
| `line.vale.invite_sit` | Pip on bench, eye | K | — |
| `line.vale.repeat`, `was_shut`, `open_now`, `closed`, `saved_sweet` | — | K | — |
| `line.june.greet` + `greet_shortcut` | "You came my way! I heard the knocks before I climbed in." | R | "Pip! You found my secret way in! Did you hear the knocks too?" / "…and nobody showed you!" |
| `line.june.door_sticks`, `latch_reminder`, `latch_thanks`, `door_already_open` | latch help | K | latch vignette loses its ring |
| `line.june.listen_floor` + vg | "They're under the floor somewhere." planks jump, ear | R | "I came down to find the knocks. Listen…" ear + knock rings only |
| `line.june.go_outside`, `resolve`, `closed` | after the show | K | — |
| `line.kit.first_show`, `kit.thanks` | — | K | add `kit.ask` "So, detective, what happened here?" (opens the notebook if a question is open) |
| `objective.*` (10 titles) | "Find June's way in", "Fix what's loose"… | R | the Continue button shows the current open question's chips; titles become the questions |
| `hint.*.observe` (10) | deduction lines ("Could someone climb in?") | R | sensory area lines ("A breeze keeps tugging toward the side yard.") |
| `hint.*.locate` (10) | exact place ("The striped wooden arm hangs loose beside the cam.") | R | area trail lines ("Follow the bats to the hall.") |
| `hint.*.act` (10) | exact action ("Reconnect the wooden arm.") | C / R | only move-demo lines remain ("Hold Jump to jump higher."); others cut |
| `hint.free_roam.*` | post-solve pointers | K | — |
| Tier-1 sparkle on object + head turn at object | | R | breeze toward area, glance toward area |
| Tier-2 goal sticker over the object | | C | trail circles the area |
| Tier-3 ghost hand on Use/Talk | | C | move demos only |
| `notebook.q_*` | questions | K | `q_moved` → "What should the knocks move?"; raised progressively |
| `notebook.a_*` + answer strips (`vg.notebook.*`) | shown on physical proof | R | shown only after a right placement; comic from the child's stickers; no green check in the strip |
| `notebook.note_*` (9) | conclusions ("From the bench, the hedge hides the side") | R | perceptions: "From the bench: the door, and the hedge." "Bea: a silver flash, up high." "The foil streamer on Bea's bike." |
| `notebook.tag_june` "June's way in" | names the answer | R | "June, in the attic" |
| Per-question annotation slots | show which clue belongs where | C | shared tray + generic "?" outlines |
| Memory replays (4) | route ghost, auto first time | R | hazy partial memory, notebook only (§7) |
| Key ring cue on clues | "this matters" | C | retired from case content (kept for UI focus only) |

## 9. Playtest plan

Recruit as spec §8 (3 children 6–9 and 2 aged 9–12, a guardian each; spec v1.4 audience, D27). External observation only: no recording in
the game, no account. Hand the phone after the title; stay quiet; answer "I don't know, what do you think?".

Observe and note, per child: (1) unprompted theories after Bea and Vale ("What do you think happened?"), (2) first
route and time to find the side yard, (3) whether the cloth's glint draws them without a hint, (4) the first
notebook use: do they drag without help after the one demo, (5) wrong tries per question and their face on the
wobble (curious or upset), (6) radio presses and which tier, (7) whether the decoy fools them and whether they
self-correct, (8) the "aha": a verbal or physical reaction on a right placement, (9) recall afterwards: can they
explain the side entrance or the knock source in their own words, (10) do they ask to keep playing.

Gate: 4 of 5 finish; at least 3 of 5 solve Q-entry with at most one radio press; nobody gives up in the notebook;
any confusion repeated across two sessions gets a revision. Report sample size and limits; human acceptance stays
pending until observed.

## 10. Implementation sketch (after approval)

Core: `CaseNotebook` becomes questions with slots, idea groups and accepted sets (pure, tested by enumerating
placements); new facts for the four new stickers and the three solutions (additive IDs, so case-save schema 1 still
loads; old saves get solutions derived from their proofs once, so nobody loses progress). `CaseHints` targets areas
with an authored area table; `HintPresentation` loses the object sparkle and the Use demo. `C01Vignettes`: 14
vignettes rewritten, 4 new, the key ring cue removed from content. `MemoryReplays`: on-demand only, new staging.
`en.json`: about 60 strings changed, each with translator context. Views: notebook tray and slots, drag input per
control scheme. World: four new examine targets with their idle attractions. Tests: every accepted set is reachable;
no placement order deadlocks; the decoy never completes a question; no case line names a conclusion (a keyword lint
on `en.json` for the review).

## 11. Decision D26 (approved 2026-11-22; the final text is in `decisions.md`)

| ID | Decision | Why | Revisit when |
|---|---|---|---|
| D26 | **Owner (approved 2026-11-22; proposal text as reviewed):** mystery first. (1) Witness lines and vignettes show only what the witness perceived; captions describe what is seen or heard, never what it means; the "this matters" ring leaves case content. (2) Notebook answers exist only after the child places clue stickers on a question (2–3 slots, shared tray, gentle wobble on a wrong set, progressive scaffold, comic built from the child's stickers). (3) Hints target areas: tier 1 nudge, tier 2 trail, tier 3 only demonstrates moves; stuck deductions get a musing and the missing clue's place, never the pairing. Helper stays off (D23). (4) Memory replays become partial, hazy memories, on demand from the notebook. (5) Questions appear as the world raises them. Supersedes D23 items 2 and the tier pictures of `nonverbal-ux.md` §6; D23.1 is unchanged. | Owner (2026-11-22): explaining everything removes the fun; children must gather the clues and solve the mysteries themselves. | Playtests show children 5–8 cannot finish without frustration, or the deduction step is skipped by trial and error. |

## 12. Owner answers (decided 2026-11-22, D26: every recommendation taken; questions kept for the record)

1. **Does the finale need the deductions?** (a) The owl show and Kit's reopening need only the world actions, and
   the "Case closed" stamp needs the three solved questions (Kit asks "So, detective?"). (b) Nothing needs
   deductions; they are bonus stamps. (c) The show itself waits for the deductions. **Decided (a):** the joy of
   the show is never withheld, but the case is only closed by the child's own explanation.
2. **Wrong-try feedback.** (a) All-or-nothing always. (b) Show which stickers fit from the first try. (c)
   Progressive: all-or-nothing for two tries, then fitting stickers stay. **Decided (c):** it keeps the aha for
   most children and stops brute force from becoming a chore for the youngest.
3. **Memory replay.** (a) Remove. (b) Hazy partial memory, notebook only. (c) Hazy memory, auto-play the first time.
   **Decided (b).** Auto-play pulls the camera toward the side of the house, which is a hint nobody asked for.
4. **The decoy streamer.** (a) Keep one decoy per case. (b) No decoys for case 01, add them from case 02.
   **Decided (a):** one honest red herring that the child can disprove by touching both is the classic aha,
   and the scaffold sets it aside after repeated tries.
5. **Tier 3 for Use actions.** (a) Move demos only (as proposed). (b) Also demonstrate Use when the child stands next
   to an unexamined clue for a long time. **Decided (a):** the Use prompt already appears in range, and the
   glint draws the eye; a hand pressing "Look" on the clue picks the clue for the child.
