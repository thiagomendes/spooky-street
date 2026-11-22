# Roadmap: fitting mystery-first into the stages (proposal, pending owner approval)

Owner (2026-11-22, translated): "See where it makes sense to fit these improvements into the specs, aiming at
implementation efficiency and avoiding rework later." This page places the items of `docs/mystery-first.md`,
`docs/narrative-bible.md`, `docs/world-and-cases.md` and `docs/engagement.md` into the spec's stages (§9), audits
what exists, says what to freeze, lists the spec edits and proposes the next three increments. Principle: **build
the foundations once (data model, deduction, street state, frameworks), then pour content into them.**

## 1. Stage map (ordered by dependencies)

| Stage | Status | What belongs there now |
|---|---|---|
| S000–S000.7 (skeleton, polish, art, UI language, dressing, witnesses, moveset, sky, maple) | done | nothing; the moveset gains wall jump and ledge grab later (they need climbable geometry, `MovementModel` notes) |
| S001 block and controls | done in S002 passes | nothing new |
| S002 greybox mystery (case core, wiring, interior, vignettes, hints) | done, under review | the audit in §2 decides what is adapted |
| **S002.5 Mystery first (new)** | proposed | **foundations:** case data model (true story doc, clues as stickers, testimonies, idea groups and slots), notebook deduction mechanic, area-based hints, partial testimonies and spoiler-free lines and captions, replay on demand, `street` state (what the street remembers), case page and Continue picture recap, the case template in `docs/cases/` |
| S003 representative finished corner + **audio** | next after S002.5 | audio synthesis pipeline (D20) with the clue sounds first: knocks, floor hum, cloth flutter, wind-wheel whirr, latch, sticker peel, wobble *tok*; babble voices; the diegetic attractions of the four new clue objects; character actions for testimonies and the hint glance |
| S004 entire slice + language delivery | later | paper bats with meaning (sketch pages, costume pieces, bat mobile), parent progress page behind the gate, language packs, accessibility pass (deduction by gamepad and keyboard), dialogue portraits, the troupe photo and crest seeds |
| S005 release candidate | later | formative playtest of the mystery-first case (`mystery-first.md` §9, `engagement.md` §3) before RC; device profiling |
| **S006 season framework (new)** | after case 01 ships | wall jump and ledge grab with revisit secrets in Lantern Lane; the place framework (second place, D24.1 threshold reuse); case 02 designed with the template |
| S007+ cases 02–08 | proposals | one case per stage: design doc (template) → owner approval → greybox → content |

Dependencies: the deduction model and sticker facts (S002.5) feed the notebook, hints, the case page and every
future case. Audio (S003) depends only on the clue list. Paper bat sketch pages (S004) depend on the arc being
approved. New places (S006) depend on the street state and the template.

## 2. Rework audit of what exists today

Costs in agent-days (one Opus session ≈ 1 day of work, CI runs included). The aim is minimum change, maximum reuse.

| System | Verdict | What changes | Cost |
|---|---|---|---|
| Vignette engine (`Vignette*`, sampler, validator, `VignettePlayer`, puppet atlas pipeline) | **keep** | nothing | — |
| Case 01 vignette content (`C01Vignettes`, `.Lines`) | adapt | 14 rewritten, 4 new, key-ring cue removed from content; validator rule "no ring cue in case lines" | 1.5 |
| Puppet atlas parts | adapt | about 6 new parts (silver flash, blur streak, wind wheel, owl panel, booth owl, loose arm); existing rects unchanged | 1 (graphics agent) |
| Memory replay (`MemoryReplay*`, `MemoryReplayView`) | adapt | policy: on demand only; Bea's staging shows a blur streak, Vale's loses the ⊘; seen set no longer gates | 0.5 |
| Notebook carousel (`NotebookView`) | adapt | carousel, pips, final card kept; page body rebuilt as slots + shared tray + drag/tap-tap; per-question annotation slots removed | 2 |
| Answer strips (`vg.notebook.*`) | adapt | played after a right placement, panels chosen from the child's stickers, no check stamp | 0.5 |
| `CaseNotebook` (Core) | **rebuild** | questions with slots, idea groups, accepted sets, progressive scaffold, tray; enumerating tests | 1.5 |
| Hints (`CaseHints`, `HintSession`, `HintPresentation`) | adapt | objective → area table; tiers: nudge, trail, move demo only; stuck-deduction musing and missing-clue place | 1.5 |
| `HintGuide` sparkle and head turn | adapt | sparkle on object → leaf breeze and warm halo over the area; head turn toward the area | 0.5 |
| Bat trail (`BatTrailSchedule`, view) | keep | ends by circling the area; goal sticker removed | 0.2 |
| Ghost hand (`HintGhostDemo`) | keep | restricted to move demos; one new notebook drag demo | 0.3 |
| Captions (`CaptionStrip`) | keep | strings rewritten only | — |
| `CaseScript` | keep | strings rewritten in `en.json`; new `kit.ask` line and four examine lines | 0.5 |
| Facts and reducer (`CaseDefinition`, `CaseReducer`) | keep | additive facts: 4 new sticker facts, 3 solved facts; ending rule per open question 1 | 0.5 |
| Save (`CaseSave`, `SaveSlotStore`) | keep | schema unchanged (facts are additive); old saves derive solved facts from their old proofs once; `street.json` added beside it | 0.5 |
| `NonverbalProgress` seen set | adapt | kept for "watched" badges only | 0.1 |
| House v3 thresholds (`Thresholds`, `ThresholdTravel`) and interior camera (`InteriorCamera`) | **keep** | four new examine targets placed in `OwlHouseStage`/street (booth, panel, arm, wheel) | 0.5 |
| Objective titles, Continue detail | adapt | open-question chips and a picture recap | 0.5 |
| Tests (PlayMode `NonverbalUiTests`, `HintPicturesTests`, wiring tests) | adapt | replay-once and object-sparkle expectations change; new deduction tests | 1 |
| Parent gate, settings, control glyphs, tips | keep | — | — |

**Total ≈ 13 agent-days**, mostly data and two Core rebuilds; no engine system is thrown away.

## 3. What to freeze now

**Do not do until the redesign is approved** (it would be thrown away):

- new or polished case 01 vignettes, answer strip art, the larger caption stage;
- any hint visual work (sparkle polish, new tier pictures, ghost-hand tuning beyond moves);
- the skinned-mesh June memory ghost (D23.7's first choice) and replay staging polish;
- notebook page visuals, sticker art for the current annotation slots;
- new case 01 lines or captions, objective titles, babble timing tied to current vignette beats;
- any case 02 content, new places or new characters.

**Safe in parallel** (no design change touches it):

- the audio synthesis infrastructure (D20: `tools/audio/`, mixing, loudness, the listening page) and generic sounds
  (footsteps, cloth, UI taps, leaves);
- movement: wall slide, wall jump and ledge grab in `MovementModel`, feel tuning, tests;
- performance and device fixes, the TestFlight pipeline, memory and thermal measurements;
- portability plumbing (D25): control scheme glyphs, safe areas, aspect ratios, quality tiers;
- environment and character art that is not a clue (dressing, ambient life, Kit's model).

## 4. Spec amendments (proposed diff summary; the spec is not edited yet)

| Section | Edit |
|---|---|
| Header, *First audience* | "core designed for about 6–9, with optional detective extras for 9–12; hybrid pictures and words" (if bible Q4 is approved) |
| §1 *Priority order* | first item becomes "a mystery the child solves (questions, found clues, own deduction)"; add the narrative bible as the north star |
| §2 Bea row | "Saw a silver flash up high by the side of the house; the hedge hid the rest." |
| §2 Aunt Kit row | add the troupe photo and crest as seeds (proposal) |
| §3 table, *Conflicting views* | Bea's line "Something silver flashed up high, by the side of the house!"; questions raised by the world |
| §3 table, *Physical evidence* | cloth line "Soft silver cloth, caught on a splinter."; add the decoy rule |
| §3 table, *Find the source* | captions as perceptions; new clue objects: wind wheel, owl panel, owl booth, loose arm |
| §3 table, *Resolve* | Kit asks "So, detective, what happened here?"; the case stamp follows the deductions (open question 1) |
| §3 *Notebook* | replace "Fill each answer after physical proof" with the deduction mechanic (`mystery-first.md` §5) |
| §3 *Solvability rules* | add: no placement order deadlocks; every accepted set is reachable; solved facts are additive |
| §4 *Collectibles* | sketch pages carry lore and seeds; costume pieces for Pip; never gate clues |
| §5 last bullet (hints) | replace "first observation, then precise location, then direct action" and its example with area nudge, area trail, move demo; stuck-deduction musing |
| §6 *FX and UI* | captions describe what is heard or seen, never what it means; no "this matters" ring on clues |
| §8 checks | add **CASE-04 deduction**: every accepted set reachable, decoy never completes, no line names a conclusion; playtest gate adds "solves Q-entry with at most one radio press" |
| §9 sequence | insert S002.5 Mystery first; S003 audio leads with clue sounds; S004 adds paper bat pages and the parent progress page; add S006 season framework |
| New appendix | links to `narrative-bible.md`, `world-and-cases.md`, `engagement.md` and the case template |

## 5. Next three increments

**1. S002.5a — Mystery-first core and notebook** (about 6 agent-days; TestFlight: yes)
- Scope: `CaseNotebook` rebuild, new facts, `en.json` rewrite (lines, captions, notes, hints), `C01Vignettes`
  rewrite and the new parts, notebook slots + tray + drag and tap-tap, answer comic from stickers, replay on demand,
  hint areas (nudge, trail, move demo), updated tests.
- Streams: (A) Core model and tests, Opus; (B) strings and vignette data, Opus; (C) puppet parts, `fable` graphics
  agent (Opus high if Fable does not improve them); (D) notebook view and input, Opus after (A) lands.
- The owner gets: case 01 where Bea only saw a flash, the notebook asks and waits, wrong tries wobble back, the comic
  appears only when the child solves it, hints point to areas.

**2. S002.5b — Diegetic clues and the street remembers** (about 4 agent-days; TestFlight: yes)
- Scope: four new examine targets with idle attractions (glint, flutter, whirr, twitching ribbons), the `street`
  state and visible after-case changes, the case page and Continue picture recap, first clue sounds from the audio
  pipeline (knocks, hum, flutter) if S003 infrastructure is ready.
- Streams: (A) world targets and attractions, Opus; (B) `street` state and recap UI, Opus; (C) audio pipeline,
  started in parallel now (safe), Opus.
- The owner gets: clues that call the eye and ear, a street that stays changed, a picture recap on Continue.

**3. S003a — Audio clues, character testimony actions, playtest kit** (about 5 agent-days; TestFlight: yes, used
for the family playtest)
- Scope: the clue sound set and babble voices (D20 listening page for the owner), testimony gestures (Bea shading
  her eyes up high, Vale tapping his watch), hint glances, the playtest script and observation sheet.
- Streams: (A) audio, Opus; (B) character actions in Blender scripts, `fable`; (C) wiring and the playtest kit, Opus.
- The owner gets: the build to play with children, plus the observation sheet from `mystery-first.md` §9.

Open questions that block increment 1: `mystery-first.md` §12 questions 1 and 2 (the finale gate, wrong-try
feedback). The others can be decided during it.
