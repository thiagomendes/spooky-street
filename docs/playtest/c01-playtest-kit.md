# Case 01 family playtest kit (S003a stream C)

This kit is for the first family playtest of "Three Knocks on Lantern Lane". It covers who plays, how to set up, what
the facilitator says (and never says), timing, the end-of-session questions and the observation sheet. It applies
`docs/mystery-first.md` §9, `docs/engagement.md` §3 and the spec's §8 playtest gate to the case as it exists on
`main` today. It changes no game code.

Companions:

- `docs/playtest/c01-playtest-kit.pt-BR.md`: the facilitator script and the sheet in Brazilian Portuguese, with a
  table of literal readings of the English lines for children who do not read English.
- `docs/playtest/c01-observation-sheet.pdf`: the printable sheet (pt-BR, A4 landscape, one page per child), made by
  `tools/playtest/make_observation_sheet.py` (`pip install -r tools/playtest/requirements.txt`, then run it; it
  uses only reportlab's built-in Helvetica). `docs/evidence/playtest/c01-observation-sheet.png` is a preview render (110 dpi) of
  that page, reviewed by eye: one page, every row and the checklist legible.

**Nothing in this kit is shown to the child.** The facilitator keeps the sheet out of sight, because it names the
clues and the answers.

## 1. What we want to learn

The case was rebuilt "mystery first" (D26): the child gathers the clues and makes the deductions, and hints never
give the answer. The playtest checks whether that works for real children aged 6–9 (and 9–12 for the extras):

1. Do they form their own theories after Bea and Mr. Vale?
2. Do the clues draw them (glint, sound, motion) without the radio?
3. Can they use the notebook's sticker slots after at most one try-and-wobble, and does a wrong set feel curious
   rather than upsetting?
4. Do they feel the "aha" when a right set draws the comic?
5. Afterwards, can they explain the side entrance or the knock source in their own words?
6. Do they want to keep playing?

Human acceptance of the case stays **pending** until these sessions are observed and written up (spec §8).

## 2. What is in the build (check before the first session)

The observation sheet follows the case as it is on `main` (merge 61e5936, S003 voices and music). Before the first
session, play the TestFlight build once yourself, from New game to the "Case closed" stamp, and tick what you see.
If a later S003a build changes any of this, note it on the sheet's header.

| In the build | What the child meets |
|---|---|
| Opening cutscene (about 20 s, skippable) | Candlebrook at dusk, the moving boxes, the glowing round window, Bea waving; play starts on the porch |
| Three knocks | heard on arrival; they raise the first question in the notebook (the notebook disc wobbles) |
| Witnesses | Bea (plaza, by her bike) and Mr. Vale (bench). Each line plays a small animated picture with one English text line (hybrid reading, D23.1) |
| Clue stickers | 12 tray stickers (Bea's flash, Vale's door, bench view, stuck door, foil streamer = the honest decoy, silver cloth, sill thread, floor hum, wooden piece, owl panel, wooden owl, loose arm) and the saved sweet as a keepsake |
| Three questions | raised by the world, one page each, 2 or 3 empty slots, one shared tray, a "?" outline per clue still out there |
| Notebook checking | full slots, a short pause; wrong: shake, stickers wobble home, Pip thinks; right: orange glow, the comic of the child's own stickers, *Found it!*. From the 3rd wrong try fitting stickers stay; after the 5th the decoy is set aside |
| Radio (Hint button) | one press per tier for the current step: tier 1 a sparkle and a character's glance, tier 2 the paper-bat trail, tier 3 a see-through hand showing what to do. Inside the notebook it gives Pip's musing (two pictures) and lights a "?" with the clue's place |
| Moves | walk, jump, hold to jump higher, glide; wall jump and ledge climb on marked surfaces (one-time tips over the Jump button) |
| House | the attic window (page-turn into a roomier inside), June in the attic, the latch, the hall, the workshop grille, the arm and the crank, the owl bowing on the roof |
| Finale | the porch: Aunt Kit's owl show; Kit asks "So, detective, what happened here?" and opens the notebook if a question is still open; the stamp needs all three questions solved; then the reveal cutscene (about 28 s, built from the child's stickers) |
| Sound | knocks, floor hum, cloth flutter, notebook sounds, babble voices, music (Settings: Music, Sounds, Voices) |
| Helper | in Settings behind the parent gate, **off** by default. Keep it off |

Known differences from the design (so you do not misread what you see):

- Tier 1 still sparkles the target itself and tier 3 can demonstrate "Use" (the design's area-only hints are not
  built yet). A tier-1 or tier-3 press can therefore make a clue easier than the design intends: note the tier on
  every press and count "deduced alone" only for notebook placements.
- The wooden-owl sticker arrives with the owl's bow; the wind wheel bonus clue, the one-time ghost-hand drag demo,
  chapter stops and the Continue picture recap are not in the build.
- The "street remembers" changes are only the closing lines (Bea's owl, Vale's saved sweet) and Kit's open booth.

## 3. Who plays

- **Children:** about five, as spec §8 suggests: three aged 6–9 and two aged 9–12. Fewer is fine; report the number.
- **One child at a time.** Siblings wait in another room or play later; a sibling watching learns the answers. If two
  children insist on playing together, run it, but mark the sheet "pair" and do not count it towards the gate.
- **A guardian for each child**, present for the whole session. The guardian sits behind the child and stays quiet
  too (give them the same rules as the facilitator, §6).
- **Readers and non-readers of English.** Note on the header whether the child reads English (yes / a little / no).
  The game is English-only for now; see §6.5.
- Leave out a child who is unwell, tired or upset that day. Short, happy sessions beat complete ones.

## 4. Consent and privacy (Kids-category rules apply to the playtest too)

- **Ask the guardian first, then the child.** Explain in plain words: "We are testing a game for children. We are
  watching the game, not your child. They can stop at any time, and nothing they do is wrong." The guardian agrees
  out loud or in writing; the child is asked "Would you like to try a new game?" and may say no.
- **The child can stop at any moment**, without a reason. Stopping ends the session warmly ("Thank you, detective!").
- **No recording of faces or voices** unless the guardian explicitly agrees, for that session, after hearing what is
  recorded and who will see it. The default is written notes only.
  - If the guardian agrees to a screen recording, use iOS screen recording with the **microphone off**. It captures
    the game only. Keep it on the owner's device, watch it for the write-up, delete it within two weeks.
  - Never photograph or film the child.
- **No names in the notes.** Use a code (C1–C5), age and the reading level. Keep the code-to-name list on paper,
  apart from the sheets, and destroy it after the write-up.
- **The repository is public.** Only an anonymised summary goes in the repo (codes, ages, counts, quotes without
  names). Never commit the sheets, recordings, photos, names, schools or anything that identifies a family.
- **The game collects nothing.** There are no accounts and no analytics (engagement §2.6); everything we learn comes
  from watching. Do not install any tracking or recording app to "help".

## 5. Setup

- **Device:** the owner's iPhone with the TestFlight build. Write the version and build from the title screen's
  corner label on the sheet. Charge it to at least 80 %, close other apps.
- **Focus / Do Not Disturb on**, so no notification interrupts. Optional: Guided Access (triple-click) to keep the
  child in the app.
- **Sound on:** ring/silent switch off silent, volume about 60 %, played through the speaker so the facilitator hears
  the knocks and voices too. Settings: Music, Sounds and Voices at their defaults. No headphones.
- **Fresh start for every child:** on the title, tap **New game** (confirm if asked), so each child sees the opening
  cutscene and an empty notebook. Check Settings (behind the parent gate): **Helper off**.
- **A quiet room**, good light without glare on the screen, a comfortable chair or sofa, the phone held in landscape
  by the child. No TV, no other children in the room.
- **Facilitator position:** beside and slightly behind the child, where you see the screen but the child does not
  see your sheet. Guardian behind the child.
- **On the table:** the printed sheet (one per child) on a clipboard, a pen, a watch or phone stopwatch (silent),
  water. A spare printed sheet for notes.
- **Before the child arrives:** start a stopwatch at 0 when the child takes the phone and write times as mm:ss from
  that moment.

## 6. Facilitator script

The golden rule (spec §8, mystery-first §9): **hand over the phone after the title, stay quiet, and never give a
hint or an answer.** You are a curious listener, not a teacher. Your face stays friendly and neutral, whatever the
child does. Do not nod at the right clue or frown at the wrong one.

### 6.1 Welcome (say this, about 2 minutes)

> "Hi! This is a new game called Spooky Street. Nobody here made it hard on purpose, and there are no wrong moves.
> I want to see how kids play it, so I'll mostly be quiet and write notes about the **game**, not about you.
> You can talk out loud if you want: tell me what you're thinking, what you like, what's weird. You can stop
> whenever you want. Ready? Tap New game."

Then hand over the phone and start the stopwatch.

### 6.2 During play: what you may say

Only neutral prompts, and only now and then (at most one every few minutes), never in the middle of a cutscene:

- "What are you thinking?"
- "What do you notice?"
- "Tell me about that."
- "What do you think happened?" (**say this once, right after the child has heard both Bea and Mr. Vale**: it is
  the spec's one scripted question; write the answer word for word.)
- "Hmm." / "Okay." / "I see." (as a listener)
- When the child asks for help or what to do: **"I don't know, what do you think?"** or "What could you try?"
- When the child asks if something is right: "What do you think?"
- When the child solves something: smile, "Mm-hm!" is fine. No "Well done, that's right!" before the game says so.

### 6.3 During play: what you must never say or do

- **Never touch the phone** or point at the screen, and do not let the guardian do it.
- Never name a place or object to go to ("try the hedge", "look at the treehouse", "what about the bike?").
- Never explain a clue or a testimony ("Bea saw June's cape", "the silver is from June", "the foil is a trick").
- Never pair stickers or hint which sticker goes on which question, never count ("you need one more").
- Never mention the radio, the notebook or a button by name unless the child asks what it is; then say "What do you
  think it does?"
- Never explain controls beyond what the game's own tips show. (If the child cannot move at all after a minute, you
  may say "Try moving your thumb on the left side." once, and note it.)
- Never say "hurry", "almost", "you're close", "no, not that", or sigh. No praise or disappointment that tells the
  child whether a choice is right.
- Never translate *more* than the words on the screen (see §6.5).

### 6.4 When the child is stuck or unhappy

Write the time the stall begins. Then:

1. **Up to 3 minutes stuck but calm:** stay quiet. Exploring is play.
2. **After about 3 minutes, or visible frustration:** one neutral prompt: "What are you thinking?" or "What could you
   try?" Note it on the sheet ("P" for prompt).
3. **After about 5 minutes, or the child says they want to give up:** "Is there anything in the game that could
   help?" (still no button or place named). Note it ("P2").
4. **Upset, close to tears, or wants to stop:** stop. "Thank you, you were a great detective! Let's stop here." Note
   the moment and the reason. Never push a child to continue.

If the child is scared (they should not be: the game is spooky, never scary), pause, reassure, and note what
caused it: that is a design bug.

### 6.5 Children who do not read English yet (hybrid reading, D23.1)

The game is in English for now. It uses one hybrid reading mode for everyone: every line plays a small animated
picture that carries the meaning, with a short English text line under it. Pre-readers are meant to follow from
the pictures. So:

- **Do not read or translate by default.** First see whether the pictures are enough; that is one of the things we
  test. Note "pictures only" on the header when the child did not ask.
- **If the child asks "what does it say?"**, read the line once, **literally**, in Portuguese, in a flat voice, with
  no extra words, no emphasis on clue words and no explanation. Use the literal readings table in the pt-BR kit
  (appendix A) so every family hears the same words. Mark the row with "R" (read aloud).
- Never read the notebook answer words before the child has solved that question (they only appear after a right
  set anyway).
- If the child reads English a little, let them try; help only with a word they ask about.
- Note every reading request: many requests on one line mean the picture does not carry it, which is useful.

### 6.6 Time limit and ending play

- Let the child play until the "Case closed" stamp and the reveal cutscene end, or **40 minutes**, whichever comes
  first. At 35 minutes say "A few more minutes, then we'll stop." At 40 stop kindly, even mid-question.
- After the stamp, the child may keep exploring (free roam). Write down whether they **ask** to keep playing before
  you say anything; that is the "wants to continue" signal.

## 7. Timing (per child, about 45 minutes)

| Part | Minutes |
|---|---|
| Welcome, consent, setup check | 5 |
| Play (expected 17–30 for a young first-timer, 12–18 for an older reader; mystery-first §4) | up to 40, stop at 40 |
| End-of-session questions | 5–10 |
| Next day: recall question, asked by the guardian at home | 2 |

Leave 10–15 minutes between children to reset the phone (New game, Helper off) and to finish your notes while they
are fresh.

## 8. End-of-session questions

Ask the child in a relaxed voice, one question at a time, and write the answers word for word. Do not correct
anything, do not add clues, and accept "I don't know".

1. "Tell me what happened in the story." (recall; listen for the side entrance and the knock source)
2. "How did June get into the house?" (only if not covered by question 1)
3. "What was making the knocks?" (only if not covered)
4. "Who helped you?"
5. "What was your favourite part?"
6. "Was anything confusing? Was anything a bit too scary?"
7. "What did you think of the notebook and the stickers?"
8. "Did you use the radio? What did it do?"
9. "Would you like to play more of Spooky Street another day?"

Ask the guardian (child out of earshot if possible):

1. "Did anything worry you, or feel not right for your child?"
2. "Was the reading too much, about right, or not needed?"
3. "Would you trust this game on your child's phone? Why?"
4. "Anything you would change?"

**Next day (the guardian asks at home, writes the answer down):** "Can you tell me about the game you played
yesterday? What happened?" This is the engagement §3 recall signal.

## 9. Observation sheet

One sheet per child. The child may play the case in any order: fill the rows as the moments happen and write the
order number in the "#" column if it differs. The printable pt-BR PDF condenses this into one page (moments that
usually happen together share a row).

**Header:** child code · age · reads English (yes / a little / no) · pictures only? · build (version · build) ·
date · start time · pair? · guardian present · notes on setup changes.

**Columns:**

- **Time**: mm:ss from the start.
- **What the child did**: the action, in a few words (route, what they touched, what they tried).
- **Stuck? (min)**: how long without progress before this moment; P / P2 if you prompted.
- **Hint tier**: radio presses for this moment: 1, 2, 3, or M (notebook musing). Blank if none.
- **Said aloud**: the child's words, verbatim where possible; R if you read a line aloud.
- **Emotion**: D delight · C confusion · F frustration · B boredom (circle one or two; blank if neutral).
- **Deduced alone?**: for the theory prompt and the three notebook questions only: Y (the child's own placement,
  no radio on that question), H (after a radio musing), T (by trial and error, many quick sets), N (not solved).

| # | Moment (what the game shows) | Time | What the child did | Stuck? (min) | Hint tier | Said aloud | Emotion D/C/F/B | Deduced alone? |
|---|---|---|---|---|---|---|---|---|
| 1 | Opening cutscene: watched or skipped? | | | | | | | — |
| 2 | Arrival on the porch, first move (which way?) | | | | | | | — |
| 3 | First knock: the three knocks; does the child react or look at the house? | | | | | | | — |
| 4 | First notebook disc wobble (question raised): noticed? opened? | | | | | | | — |
| 5 | Talks to Bea (sticker: Bea's flash) | | | | | | | — |
| 6 | Talks to Mr. Vale (sticker: Vale's door) | | | | | | | — |
| 7 | Facilitator asks once: "What do you think happened?" (write the theory) | | | | | | | Y/N theory |
| 8 | Sits on the bench (sticker: bench view) | | | | | | | — |
| 9 | Tries the front door (sticker: stuck door) | | | | | | | — |
| 10 | Bea's bike: foil streamer (sticker, the decoy) | | | | | | | — |
| 11 | Finds the side yard (time from start) | | | | | | | — |
| 12 | Silver cloth on the ramp rail: drawn by the glint without the radio? (sticker) | | | | | | | — |
| 13 | Treehouse and bridge; tips for jump or glide shown? | | | | | | | — |
| 14 | Attic sill: silver thread (sticker) | | | | | | | — |
| 15 | Enters the attic window (page-turn), meets June | | | | | | | — |
| 16 | First notebook use with two or more stickers: drags or taps without help? | | | | | | | — |
| 17 | "How did June get in?": tries (count), decoy used?, face on the wobble | | | | | | | Y/H/T/N |
| 18 | Lifts the latch with June, front door opens | | | | | | | — |
| 19 | The hall: floor hum (sticker) | | | | | | | — |
| 20 | Workshop grille: wooden piece (sticker) | | | | | | | — |
| 21 | Owl panel (sticker) | | | | | | | — |
| 22 | "Where are the knocks coming from?": tries, face | | | | | | | Y/H/T/N |
| 23 | Loose arm: crank before linking? links the arm? (sticker) | | | | | | | — |
| 24 | Turns the crank: the owl bows on the roof (sticker: wooden owl) | | | | | | | — |
| 25 | "What should the knocks move?": tries, face | | | | | | | Y/H/T/N |
| 26 | Porch: Kit's owl show; Kit asks "So, detective?" | | | | | | | — |
| 27 | "Case closed" stamp, reveal cutscene: watched or skipped? reaction to own stickers? | | | | | | | — |
| 28 | Case page: "x of y clues" (write x and y) | | | | | | | — |
| 29 | Saved sweet from Mr. Vale (bonus) | | | | | | | — |
| 30 | Free roam: asks to keep playing before being asked? | | | | | | | — |
| H1 | Radio, tier 1 (sparkle and glance): each press, time and step | | | | 1 | | | — |
| H2 | Radio, tier 2 (bat trail): each press, time and step | | | | 2 | | | — |
| H3 | Radio, tier 3 (see-through hand): each press, time and step | | | | 3 | | | — |
| H4 | Radio in the notebook (Pip's musing and a "?" place): each press, which question | | | | M | | | — |
| X | Lost the camera or control, fell, got scared, wanted to stop | | | | | | | — |

**Counts at the end:** clues found before the first radio press ___ of 12 · total radio presses ___ · wrong sets per
question (entry ___ source ___ moved ___) · did the decoy fool them? yes/no · did they self-correct? yes/no ·
reading requests ___ · play time ___ min.

## 10. Success criteria (checklist)

Per child (tick on the sheet):

- [ ] Finished the case (the "Case closed" stamp) with **at most one requested hint** in total (spec §8).
- [ ] Solved "How did June get in?" with **at most one radio press** (mystery-first §9).
- [ ] Did not give up in the notebook.
- [ ] Found **at least 70 %** of a question's clues before any radio press (engagement §3).
- [ ] Showed an "aha" (words, gesture, "I knew it!") on a right placement.
- [ ] Wrong sets met with curiosity, not frustration.
- [ ] Explained the side entrance **or** the knock source in their own words (end of session).
- [ ] Asked to keep playing at a stop.
- [ ] Next day: retold what happened and who helped.
- [ ] Unprompted theory after Bea and Mr. Vale (any theory counts).
- [ ] The decoy: fooled them and they self-corrected, or did not fool them (either is fine; note which).

Group gate (all children together):

- [ ] **4 of 5** finish with at most one requested hint and can explain the side entrance or the knock source.
- [ ] **At least 3 of 5** solve "How did June get in?" with at most one radio press.
- [ ] **Nobody** gives up in the notebook.
- [ ] **4 of 5** ask to keep playing (engagement §3).
- [ ] Most solved questions show an aha.
- [ ] Guardians find the game trustworthy, unprompted.
- [ ] Any confusion seen in **two** sessions is listed for a design revision.
- [ ] (Second session, if any) **3 of 5** go hunting paper bats or old places unasked.

## 11. After the sessions

1. The same day, finish each sheet: counts, the checklist, and three lines of "what surprised me".
2. Write an anonymised summary in `docs/playtest/c01-results-<date>.md`: number of children and their ages, reading
   levels, the checklist counts, repeated confusions with the moments they happened at, verbatim quotes without
   names, and the limits (small sample, family setting, facilitator was the developer, English-only build).
3. Every repeated confusion becomes a revision item for the roadmap; a design change still passes the narrative
   bible's checklist first.
4. Do not report the case as accepted by children unless the gate was met in observed sessions.
5. Destroy the code-to-name list and any screen recordings once the summary is written.
