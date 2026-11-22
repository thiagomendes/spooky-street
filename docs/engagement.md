# Engagement: why children come back (approved 2026-11-22, D27)

Owner (2026-11-22, translated): "We need to think about strategies to keep players engaged in the game." Spooky
Street is a **premium, paid-once children's game** aimed at the Apple Kids category. Engagement here means a child
*wants* to keep exploring and asks to play again, never that the game pulls them back. Part of
`docs/narrative-bible.md`; the arc it serves is in `docs/world-and-cases.md`.

## 1. Engagement from the experience itself

1. **The curiosity loop inside a case.** Something odd → a place to look → something found → a new question. Spec
   §4 sets a meaningful event every 15–25 s on first approach; the mystery-first clues and their diegetic
   attractions (glints, hums, flutters) are what fill that rhythm.
2. **The aha of deduction.** The child's own placement makes the answer appear (`mystery-first.md` §5). This is the
   strongest hook the game has; it is why the notebook never solves anything for them.
3. **The street remembers.** After each case the place changes visibly and stays changed: the owl booth bows on the
   hour, Bea's bike wears an owl, Vale saves a sweet, a new porch light. Returning to an old place shows the child
   their effect on the world.
4. **Relationships grow.** Each case befriends someone; greetings change; returning characters help in new ways
   (Bea's lookout, June's shortcuts). The notebook's character page collects a friendship sticker per person.
5. **The notebook as a scrapbook.** Solved questions keep their comics; each case closes with a case page (the
   porch shot, the child's stickers, the friends made). The notebook becomes the trophy shelf of the season.
6. **Seeds and gentle cliffhangers.** Each final card shows the next seed (a puppet in Kit's photo lights up, a sketch
   page hints at a place), often as a short seed cutscene (`narrative-bible.md` §5). Curiosity, not a countdown.
   The season finale resolves its spine fully and then leaves a few open threads (`world-and-cases.md` §4.1): the
   child ends happy and wanting more, and nothing in the game ever sells that "more".
7. **Revisits with new moves (metroidvania-lite).** Ledge grab and wall jump, taught in later places, open secrets in
   old ones: a rooftop paper bat, a Players' crest, a hidden sketch. Never a required clue of an old case.
8. **Collectibles with meaning.** The 30 paper bats per place (spec §4) teach routes and vantage points. Every 10
   unlock something visible: a **sketch page** with lore (who the Players were, a hint of the next place), a
   **costume piece** for Pip (spec §4 already allows cape colours), and a **bat mobile** that grows over Pip's bed
   on the title screen. Counted, never spent.
9. **Small surprises for repeat play.** Touch rewards (pumpkins wobble, Vale's bandage slips), seasonal jokes, the
   owl bows if you knock three times on the booth, a second costume line from a witness when Pip wears a found
   costume piece.
10. **Session design.** Cases split into 10–20 min chapters at natural stops (after the witnesses, after a solved
    question, after the reveal). Autosave at each (spec §7). **Continue** shows a picture recap: the last solved
    comic, the open question's chips and where Pip stands, so a 6-year-old resumes without reading.

## 2. Hard guardrails (rules, not goals)

1. **No ads**, of any kind, anywhere (spec §1).
2. **No in-app purchases inside the kids' experience.** Any future paid content (a new season) is bought outside
   play, behind the parent gate, never promoted in the child's flow.
3. **No streaks, timers, daily rewards, limited-time events or FOMO.** Nothing is lost by not playing today.
4. **No manipulative notifications.** None by default; if ever added, opt-in by a parent, informational only.
5. **No loot boxes or random rewards** tied to money or to time; every reward is placed, findable and fixed.
6. **No data beyond what the game needs.** No accounts, no analytics on children, no third-party SDKs that collect
   data; saves stay on device (Apple Kids category, COPPA, GDPR-K).
7. **Rewards are never gated behind time spent**, only behind curiosity and skill that a child can choose to apply.
8. **No social pressure**: no leaderboards, no sharing prompts, no comparisons.
9. **Parents can see progress**: behind the parent gate, a simple page with cases solved, places visited, time played
   this week (computed locally) and the notebook scrapbook.
10. **Breaks are welcome**: chapter ends say goodbye warmly; nothing punishes leaving.

## 3. Measures (observed in playtests, never tracked in the app)

| Signal | How to observe | Good sign |
|---|---|---|
| Wants to continue | at a chapter stop, does the child ask to keep playing? | 4 of 5 ask |
| Unprompted clue finding | clues found before any radio press, counted by the observer | ≥ 70 % of a question's clues |
| The aha | reaction on a right placement (words, gesture, "I knew it!") | most solved questions |
| Recall | the next day, can the child retell what happened and who helped? | the side entrance or knock source in own words (spec §8) |
| Return for bats | in a second session, does the child go hunting paper bats or old places unasked? | 3 of 5 |
| Comfort | wrong tries met with curiosity, not frustration; nobody quits in the notebook | no quits |
| Guardian view | does the parent find the progress page clear and the game trustworthy? | yes, unprompted |

Report sample sizes and limits; no telemetry substitutes for watching.

## 4. Ranked recommendations

| Rank | Build | Cost | Pillars served | Stage (see `roadmap.md`) |
|---|---|---|---|---|
| 1 | Mystery-first deduction in case 01 (stickers, slots, comic from the child's stickers) | M | detective, curiosity | next increment |
| 2 | Continue picture recap and chapter stops | S | detective, comfort | with 1 |
| 3 | The street remembers (owl booth bows on the hour, Bea's owl, Vale's sweet, case page) | S | living neighbourhood | with 1 |
| 4 | Diegetic attraction audio (knocks, hum, flutter, whirr) | M | curiosity | S003 audio |
| 5 | Paper bats with sketch pages, costume pieces, the bat mobile | M | curiosity, moving is exploring | after audio |
| 6 | Character actions for testimony and greetings that change | M | living neighbourhood | after audio |
| 7 | Parent progress page behind the gate | S | trust | S004 (with settings) |
| 8 | Ledge grab and wall jump with revisit secrets in Lantern Lane | M–L | moving is exploring | before case 02 |
| 9 | Seeds on the final card (troupe photo, crest) | S | arc | with 1 (art) and case 02 |
| 10 | Dialogue portraits that act | M | character | polish, S004 |

## 5. Owner answers (decided 2026-11-22, D27: the docs' recommendations; questions kept for the record)

1. **Costume pieces for Pip as bat rewards?** (a) Yes, cosmetic only, visible in play and on the title. (b) Sketch
   pages only. **Decided (a):** children love dressing Pip, and it costs one modular costume slot per reward.
2. **Parent progress page.** (a) Build it with settings (S004). (b) Skip for now. **Decided (a):** it is small and
   it is what makes a paid kids' game trusted.
3. **Future paid content.** (a) Each new season is a separate paid-once update or app, bought by a parent outside
   play. (b) One price includes all future cases. **Decided as recommended: defer** until after case 01's
   playtest; either way no purchase ever appears inside the child's flow.
4. **Chapter stops.** (a) Soft stops with a warm goodbye card and autosave. (b) No explicit stops. **Decided (a):**
   it supports bedtime and short sessions without any timer.
