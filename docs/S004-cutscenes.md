# S004 stream C: the seed and "the street remembers" cutscenes, and the show clock

Branch `feature/s004-cutscenes` (from `main` 3b97daa). Not merged. Contract: `docs/S004-plan.md` §2.2, §2.3 and
§3 stream C; `docs/narrative-bible.md` §5 slots 4 and 5 and checklist item 16; D28 (cutscenes), D29 (street memory).
Built as data on the S002.5b cutscene system (`docs/S002.5b-cutscenes.md`). There is no new cutscene engine. The
system got two small extensions: a chained start that keeps the bars down, and photo-part and booth-crest anchors.

## 1. What shipped

### The reveal, trimmed and re-anchored

The reveal loses its last beat (the push on the old photo, which S002.5b framed for the old "Kit at her booth"
motif). It now ends on Kit's reopening and runs **25 s**, always 3 + 2 + 2 sticker shots. Its score still ends as the
reopening starts (`RevealMusicAt`, unchanged). The photo shot moved into the seed cutscene and is now framed from the
new troupe photo's anchor (`KeepsakeArt.Photo*`, facing the hall from the partition, hung above the push rod).

### Seed `cs.c01.seed` (13.0 s, bible slot 4)

It plays once, straight after the reveal's first play, whether the reveal ran through or was skipped (the night
always ends on the seed). The player ends the reveal without the blend back to play, and the seed starts with the bars
already down (`CutscenePlayback.StartLetterboxed`), so the hand-over has no flicker. It can be rewatched from the case
page.

| # | Beat | Shot (anchor) | Caption | s |
|---|---|---|---|---|
| 1 | `photo` | the troupe photo, wide, slow push (`anchor.kit_photo`) | `c01.cutscene.reveal.photo` "In the hall, an old photo: Aunt Kit and friends." | 3.0 |
| 2 | `owl` | push to the owl in young Kit's hands (`anchor.photo.Puppet_Owl`); the lamp's light runs along its gilt edge (`world.glint.Puppet_Owl`, 0.9 s) | `c01.cutscene.seed.owl` "[The little owl catches the light.]" | 3.0 |
| 3 | `moon` | slow pan along the five puppets; the moon glints, smaller and dimmer (`world.glint.soft.Puppet_Moon`, 1.7 s) | `c01.cutscene.seed.moon` "[Then the moon, too.]" | 3.0 |
| 4a | `mount` | the crest on the photo's mount (`anchor.photo.PhotoCrest`) | `c01.cutscene.seed.crest` "[An owl crest, on the booth too.]" | 1.0 |
| 4b | `booth` | page-turn match: the booth's crest, the same lens and size in frame (`anchor.booth_crest`) | same | 1.0 |
| 4c | `pip` | Pip on the arrival porch, facing the lane (`pip.thinking`) | same | 1.0 |
| 5 | `back` | the drift into the play camera behind Pip (`PorchPlayView`) | — | 1.0 |

The glowing puppet is data (`C01Cutscenes.SeedPuppet = Puppet_Moon`, case 02's seed, world-and-cases §4). The glints
are the lamp on gilt paper, not magic (§5). No beat shows a rooftop light or anything else of case 02's question.
Played through or skipped, play starts on the arrival porch. A rewatch never moves Pip.

### "The street remembers" `cs.c01.street_remembers` (18.0 s, bible slot 5)

It plays once, on the **first load after the stamp** (Continue), before control. The policy needs four things: the
case is stamped, the street lays its memories over the world (`StreetMemoryStage.WorldView.Any`, false while a replay
rests them), the stamp did not land in this run of the game, and this save has not seen it. Re-stamping a replay and
reloading plays it again, because the seen set clears with the replay. It can be rewatched from the case page.

| # | Beat | Shot | Cues | Caption | s |
|---|---|---|---|---|---|
| 1 | `lane` | the opening's crane down over Lantern Lane (same first pose) | `music_street_theme`; Pip placed on the porch | "Back on Lantern Lane." | 3.0 |
| 2 | `hour` | over Mr. Vale's shoulder to the owl house | `world.booth_bell` 0.1 s, Vale `CheckWatch` 0.25 s, `vale.happy`, `world.owl_show` 0.9 s, Vale looks up at the owl 1.9 s | "[Ding! Mr. Vale checks his watch.]" | 3.0 |
| 3 | `show` | the rooftop owl (the reveal's owl framing, `anchor.owl`) | `world.owl_show` 0.5 s (the wheel whirrs, three bows on three knocks) | "[Knock, knock… knock. The owl bows.]" | 3.5 |
| 4 | `bea` | Bea by her bike, the owl sticker on the basket | `Wave` toward the rooftop owl, `bea.happy` | "[Bea waves at the owl.]" | 2.5 |
| 5 | `june` | June at the foot of her treehouse way | `Wave`, `june.happy` | — | 2.5 |
| 6a | `kit` | Kit at her open booth | `Wave`, `kit.happy` | — | 2.3 |
| 6b | `porch` | drift into the play camera behind Pip on the arrival porch | Pip placed | — | 1.2 |

The owl starts bowing inside beat 2, so frame 72 can show Vale's watch and the bows together. The beat-2 show ends
(2.6 s) before beat 3's show starts. Existing clips only: `CheckWatch`, `Wave`, the open booth idle, head turns.

### The show clock and the knock (Core `BoothShowClock`, `StreetMemoryStage`)

- Once the street remembers case 01 (live view, not resting), the owl's 9 s wind cycle gives way to a show
  **"on the hour"**: every **90 s of play on the street**. The bell dings (a paper-light ding at the booth), the
  wind wheel whirrs and the owl bows three times (`WorkshopView.PlayShow`). Mr. Vale checks his watch if he is on
  screen within 24 m. The clock stands still while Pip is indoors, paused, in a cutscene, a conversation, the
  notebook, the finale or a crossing. It counts in double precision and gives at most one show per step.
- **Three knocks** on the booth (Use on the owl-booth spot, verb "Knock", within 2.5 s of each other) make the owl
  bow, and the hour starts over. The spot is offered as the knock only while the show clock runs. Before the stamp,
  and while a replay rests the memories, it stays the owl-booth clue, so no clue changes (engagement §1.9). A knock
  changes no case fact.
- There is no countdown or timer on screen (guardrail).

### Notebook

The case page gets two more discs left of the reveal's: the old photo (young Kit's picture, `ui.cutscene.watch_seed`)
and the street the next day (the house with its booth open, `ui.cutscene.watch_street`). Keyboard V and gamepad North
on the case page step through the reveal, the photo and the street, one per press. A rewatch never chains into the
seed and never moves Pip.

### Strings

There are 10 new keys, all with translator context, in `en.json` through `LocalizationKeys`: `ui.verb.knock`,
`ui.cutscene.watch_seed`, `ui.cutscene.watch_street`, `c01.cutscene.seed.{owl,moon,crest}` and
`c01.cutscene.street.{lane,hour,show,bea}`. The context of `c01.cutscene.reveal.photo` now describes its new place.
Saved progress keys stay stable IDs only (`cs.c01.seed`, `cs.c01.street_remembers` in the seen set).

## 2. Tests

- **Core** (`tools/core-tests`, 733/733 locally):
  - `CutsceneTests`, extended. Every cue fires once and in order at 30, 60 and a jittery 24 fps, and the camera is
    the same at 30 and 60 fps, for the seed and the street remembers. Other cases: skip at any point, reduced motion,
    data rules, 10–40 s, existing clips only, and the caption lint (no conclusion words, nothing ahead of a later
    case).
  - New `CutsceneTests` cases: the reveal ends on the reopening; the seed and the street remembers follow the plan
    beat for beat (glint IDs, the page-turn match, anchors, the end on `PorchPlayView`, settle on the porch); **never
    ahead** (for every reachable state both exist and are rewatchable only when stamped); the trigger rules (after
    the reveal's first play only, never on a rewatch, once; on Continue only with memories shown, not in the stamp's
    session, once); a replay clears the seen set, and a re-stamp plays both again; the chained start keeps the bars
    down.
  - `BoothShowClockTests` (5): every 90 s at 30 and 60 fps, within a frame and the same at both; only street play
    counts; a long frame never stacks shows; three knocks in a row; inert while off.
- **PlayMode**:
  - `CutscenePlayTests`: the solve, the reveal, then the seed (bars down, glints on the owl then the moon, the
    crest page-turn, control on the porch); not in the stamp's session; the reload plays the street remembers once
    before control (captions, the bell), then control on the porch; the next reload plays nothing. Skip works on
    the seed after a skipped reveal (disc, then two taps), on the street remembers (keyboard Space ×2), and on a seed
    rewatch (gamepad South ×2 under reduced motion). Rewatch works by touch (both new discs, Pip unmoved) and by
    V/North cycling reveal, photo, street, reveal.
  - `StreetMemoryPlayTests`: the show on the hour (the bell, wheel and three bows; the notebook stops the clock),
    three knocks through `CaseDirector.Use`, knocks inert during a replay.
  - `CaseInteriorTests` and the street memory test now skip the seed too.
- **Visual** (`CutsceneCaptureTests`): frames 71 and 72, plus small review frames of every seed and street-remembers
  beat and of control after each. The reveal frames 55–57 are recaptured.

## 3. CI evidence

(filled in below)

## 4. Frames

(filled in below)

## 5. Narrative bible checklist (§6)

| # | Check | Result |
|---|---|---|
| 1 | Written true story | Unchanged (spec §2). |
| 2 | Questions raised by the world | No new question. The seed raises only curiosity (an old photo, two glints, a shared crest). |
| 3 | Answers only from stickers | Both cutscenes exist only once stamped (tested over every reachable state). |
| 4 | Diegetic attractions | Unchanged. The show and the knock are after-case touch rewards, not clues. |
| 5 | Testimony as perception | Not touched. |
| 6 | No conclusion stated | Captions describe what is seen or heard. The lint forbids conclusion words and later-case words ("why", "who", "missing", "lost", "rooftop light", ...). |
| 7 | Hints to areas only | Unchanged. |
| 8 | One decoy | Unchanged. |
| 9 | The reveal pays off the child's clues and action | Unchanged reveal, now ending on the reopening. |
| 10 | No deadlock | Cutscenes change no fact. The knock changes no fact (tested). The seed and the street remembers settle Pip on the arrival porch. |
| 11 | The street remembers | Slot 5 cutscene on the first load after the stamp, the show on the hour, Bea's stickered bike, the knock reward. |
| 12 | Seed for a later case | The moon glints, as data (`SeedPuppet`). The crest is shared by photo and booth. Nothing of case 02's question is shown. |
| 13 | New mechanics serve mystery or exploration | The knock is a small touch reward after the case (engagement §1.9). The show makes the solved world feel alive. |
| 14 | Strings with context, stable IDs | 10 keys. The seen set stores IDs. |
| 15 | Guardrails | No timer UI: the hour is ambient. No FOMO: the show repeats forever, and both cutscenes are rewatchable. No purchases. |
| 16 | Cutscenes short, skippable, rewatchable, in-engine, six purposes, never ahead | 13 s and 18 s; skip on touch, keyboard and gamepad (tested); rewatch from the case page; real models; slots 4 (seed) and 5 (street remembers); both exist only after the stamp. |

## 6. Gaps

- The booth's bell has no sound asset. The ding is visual (a paper-light sparkle by the shutter) plus the caption.
  There is also no knock sound for the child's knocks. Both want SFX later.
- The seed's first shot cuts into the hall instead of a page-turn, because a page-turn exists only between beats.
- The knock spot is the S002.5b owl-booth spot at the porch's east side. It is not on the shutter itself, which Kit
  stands in front of after the case.
