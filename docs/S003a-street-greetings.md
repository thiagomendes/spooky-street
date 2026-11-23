# S003a: the street remembers in words (changed greetings after case 01's stamp)

Branch `feature/s003a-street-greetings` (from `main` 65854fc). Not merged. Contract: D29 (`docs/decisions.md`),
`docs/S002.5b-street-memory.md` §6 (the greeting left for S003a), `docs/narrative-bible.md` pillar 5 and checklist
11 ("a new greeting"), `docs/world-and-cases.md` §2 ("returning characters change … a greeting") and §6
("greetings change"), `docs/engagement.md` §1.6 and §2, D23.1 (hybrid reading).

## 1. The four lines

Once the street remembers case 01, each friend greets Pip with a changed line in place of the line they said the
night it was solved. Every line is short (9–11 words; bible §5: 8 on average, 14 at most), in the speaker's voice,
and reads from its vignette alone.

| Speaker | Key | Line | Replaces | Voice | Vignette (`vg.*.remembers`) |
|---|---|---|---|---|---|
| Bea | `c01.line.bea.remembers` | "Pip's back! See my owl? I'm your lookout now!" | `bea.closed` | Happy | Pip hops up to Bea's bike; the owl sticker on it bows once; an eye opens over the bike (lookout). 3.0 s, 3 beats |
| Mr. Vale | `c01.line.vale.remembers` | "Evening, detective. I watch the owl now, not the door." | `vale.closed` | Happy | An eye opens over his bench; the owl bows twice for him. 3.0 s, 2 beats |
| June | `c01.line.june.remembers` | "Psst, Pip! The door opens for me now. Come play outside!" | `june.closed` | Happy | June hops out of the open door; a hand waves (come play). 3.0 s, 2 beats |
| Aunt Kit | `c01.line.kit.remembers` | "Welcome back, detective! My old owl had friends once, long ago." | `kit.thanks` | Thinking | Inside the memory cloud the owl bows once; two "?" friends pop beside it. 3.2 s, 2 beats |

- **Each line points at a change the child can see.** Bea's owl sticker on her bike, the rooftop owl that bows
  now (Vale), the front door that opens from outside and June outside by her treehouse, Kit at her open booth.
- **Kit's line is the season's seed.** "My old owl had friends once" points at the Lantern Lane Players and the
  other puppets in her photo without naming them, the troupe, a place or a case. The two "?" in the vignette say the
  same thing: there were others, and who they were is left to wonder. The words do not explain it, and the
  translator context says not to add who or where.
- **Growth arcs** (bible §3): Bea starts being Pip's lookout; Vale finds something new to watch (his worry is being
  "only a watcher"); June goes from slipping in to inviting Pip out; Kit begins to tell the Players' story.
- **Voices and clips.** Each line has a `LineVoices` row (babble emotion; `BabblePlanner` picks the take by the
  caption's length). The speaker's existing talk clip plays through the usual `Witness.Conversing` path. No
  Character clip constant or Blender file was touched.

## 2. The rule (same as the other memories, D29)

`Core/Street/StreetGreetings.cs` (engine-free): `For(speaker, view)` gives the greeting key when
`StreetView.Remembers(case01)` (new: any of the case's memories shows), and `Retell(speaker, lines, view)` swaps the
speaker's closed line for the greeting and leaves every other line alone. `CaseScript` still writes the case's own
lines (its conditions for the four new keys require the stamp), so the reducer, the save and every order-of-play
test are unchanged.

- **The greetings arrive on the next load after the stamp**, like June outside and the open door. They follow
  `StreetMemoryStage.WorldView`, which is fixed on load or rebuild. The night the case is solved, friends still
  say the closed lines ("I'm putting an owl on my bike").
- **During a replay they rest.** While case 01 is open again (its save not stamped) `StreetView` holds back its
  memories, so the replay hears the first evening's lines. They come back with the replay's stamp and the next
  load.
- **A confirmed New game clears them** with the street.
- **Wiring.** `CaseDirector.Perform` passes a Talk's lines through `StreetMemoryStage.Retell` before
  `PlayLines`. The dialogue bubble, captions, vignette and babble then work as for any line. A greeting changes no
  case fact.

## 3. Narrative bible checklist (§6)

| # | Check | Pass |
|---|---|---|
| 1 | Kind, villain-free true story | yes: no story changes; the lines are friends saying hello |
| 2 | Questions raised by the world | n/a: no new question. Kit's seed is a wonder, not a task (no "?" to answer in the notebook) |
| 3 | Answers only by found stickers | unchanged |
| 4 | Diegetic attraction for clues | n/a: no clue |
| 5 | Testimony is perception | yes: each friend speaks about what they now have or see (a sticker, the owl bowing, the door, a memory) |
| 6 | No line states a conclusion | yes: none says how June got in, why the knocks came or what moved the owl. They also play only after the stamp and rest in a replay, so a replay keeps the whole mystery |
| 7 | Hints are areas and moves | unchanged; the greetings are not hints |
| 8 | At most one decoy | unchanged |
| 9 | Reveal pays off the child's work | yes: the greetings are the street thanking the child on the way back |
| 10 | No deadlocks, tested | yes: the reducer is unchanged and a greeting gains no fact (PlayMode asserts the same state instance) |
| 11 | The street visibly remembers | yes: this stage adds the "new greeting" half of it |
| 12 | Seed and continuity | yes: Kit's seed fits `world-and-cases.md` §4 and §5 (the Players built the machines; nothing magic is claimed; `c01.line.<speaker>.*` IDs) |
| 13 | New mechanics serve the mystery | n/a: no mechanic |
| 14 | Strings in `en.json` with context, stable IDs | yes: four keys with speaker, setting, picture, audience and intent; referenced through `LocalizationKeys.CaseLines` |
| 15 | Engagement guardrails | yes: a warm hello, no timer, no FOMO, no "come back tomorrow", no purchase. The "quero mais" pull is curiosity (Kit's seed), never pressure |
| 16 | Cutscenes | n/a: none added (the D28 slot-4 "street remembers" cutscene is still S004) |

## 4. Tests

- **EditMode** (`StreetGreetingsTests.cs`, also in `tools/core-tests`):
  - each friend's greeting once the street remembers case 01 (the script still says the closed line);
  - the night of the stamp keeps the closed lines;
  - a replay rests the greetings for every step until its stamp, and they return after it;
  - a New game clears them;
  - only the closed line changes;
  - every greeting has en.json words, a `LocalizationKeys` entry, a stamp-only condition, a voice, a valid vignette
    and 14 words at most.

  `VignetteTests` counts are now 40 vignettes and 42 lines, and `BabblePlannerTests` covers the new voice rows.
- **PlayMode** (`StreetMemoryPlayTests.cs`):
  - new `ATalkAfterARememberedLoadPlaysTheChangedGreeting`: all four talks after a remembered load play the new
    line with its vignette and words, and change no fact;
  - the reload test now expects June's and Bea's greetings;
  - the replay test checks that Bea's replay line is the old one, and that Vale greets again after the replay's
    stamp and a reload;
  - the New game test checks that Bea's first line is back.

  `NonverbalUiTests` plays the four new lines with their vignettes.

## 5. CI evidence

- `tools/check-local.sh`: all local checks pass (621 Core tests in `tools/core-tests`, 6 new).
- CI run: filled in below once `ci.yml` has finished.

## 6. Gaps

- The vignettes use existing puppet parts only and were not rendered in this stage (`VignetteCaptureTests` frames
  can be added if the owner wants to review them).
- There is no notebook character-page sticker for the greeting yet (`world-and-cases.md` §2 "the notebook's
  character page records it"); that page does not exist yet.
- Each greeting replaces the closed line for good; it does not alternate with an "after the greeting" line.
