# S003a final pass: the new gestures in play and in case 01's cutscenes

Branch `feature/s003a-pass` (from `main` 2c44716). Not merged. Contract: `docs/roadmap.md` §1 S003a and §1.1 (the
gesture, babble and music pass is data edits to the same cutscenes; cutscene timing is data), `docs/narrative-bible.md`
§5–§6 (item 16), `docs/mystery-first.md` (D26: testimonies are partial; gestures never reveal the answer; hints point to
an area only). Builds on `docs/S003a-gestures.md` (the clips), `docs/S003-voices-music-wiring.md` (babble and score)
and `docs/S002.5b-cutscenes.md` (the cutscene system).

## 1. Testimony gestures in play

**Data, in Core** (`Core/Characters/LineGestures.cs`): a table keyed by the line's localization key (so a translation
never moves a gesture) giving the clip, a delay after the line appears (so the gesture lands on its words) and, only for a
gesture aimed along the body, the place to turn toward.

| Line | Words | Gesture | Delay | Turns toward |
|---|---|---|---|---|
| `c01.line.bea.saw_cape` | "Something silver flashed up high, by the side of the house!" | `ShadeEyes` | 0.3 s | up high over the side yard (`LineGestures.BeaLookedUpHigh`: the side-yard hint area's centre, 3 m up) |
| `c01.line.bea.repeat` | "Up high, over the hedge. Then it was gone!" | `ShadeEyes` | 0.3 s | same |
| `c01.line.vale.repeat` | "Not one creak from that door all evening." | `CheckWatch` | 0.6 s | — (seated, never turned) |
| `c01.line.vale.was_shut` | "It was shut when I looked." | `CheckWatch` | 0.3 s | — |
| `c01.line.june.greet` | "… Did you hear the knocks too?" | `Listen` | 1.2 s (on the question) | — |
| `c01.line.june.greet_shortcut` | "… and nobody showed you! Did you hear the knocks too?" | `Listen` | 1.6 s | — |
| `c01.line.june.listen_floor` | "I came down to find the knocks. Listen…" | `Listen` | 0.9 s | — |
| `c01.line.kit.ask` | "So, detective, what happened here?" | `Ponder` | 0.2 s | — |

Not gestured, on purpose: Mr. Vale's first account (`vale.door_shut`, about the bench view; the bench itself is the
clue) and his invitation to sit; June's door and latch lines; every closed, thanks and "remembers" line (testimony, not
celebration); every Pip line and caption. At most one line per conversation gestures (tested over the canonical route).

**Wiring** (`Runtime/Case/NonverbalLayer.cs`, about 60 lines): `DialogueView.LineShown` → `LineGestures.TryGet` → the
speaking witness (`CaseDirector.WitnessOf(speaker)`, new, internal; it also finds Aunt Kit at the finale, where nobody is
"talked to") → after the delay on game time, `Witness.PlayGesture(clip, aim)`. A gesture whose line was already tapped
away is dropped; a missing clip does nothing (as `PlayGesture` already did). No MonoBehaviour knows a line or a clip.

**Why none of them gives an answer (D26).** Bea turns to the place her own words name (the side of the house, over the
hedge) and searches; she points at nothing and the aim is an area point clear of every Use target (tested ≥ 1.5 m).
Mr. Vale's watch is about *when*; June's ear leans to her own side and does not beat the rhythm; Kit asks.

## 2. Pip's hint glance

**Rule, in Core** (`HintPresentation.PipGlance`, `GlanceTarget`, `GlanceMinAngle`; `PipClips.GlanceMaxSpeed` moved to Core
and `PipAnimator.GlanceMaxSpeed` now refers to it): Pip glances only for the tier-1 nudge (radio press or the opt-in
Helper's nudge), never for the bats or a move demo; only while she stands (grounded, ≤ 0.6 m/s); not under reduced
motion (`AmbientMotion.ReducedMotion`, the project's setting; the glow and the witness's glance still show the area);
and only when the area lies at least 30° to one side (straight ahead it is already in front of her). The side is the one
the **area's centre** lies on (`GlanceTarget` = the authored `HintArea` centre, which is clear of every object), so the
glance says "that way", never "that thing".

**Wiring** (`Runtime/Case/HintGuide.cs`): `StartBreeze` asks the rule and calls `PipAnimator.PlayGlance(area centre)`;
`HintGuide.PipGlance` exposes the clip played (tests).

Choice to confirm: skipping the glance under reduced motion. The project's reduced motion keeps character clips (walks,
the witness head turn) and drops bursts and camera motion, so playing it would also be defensible; I followed the brief
("respect reduced motion"). It is one condition in `PipGlance`.

## 3. Cutscene pass (data only, `Core/Cutscenes/C01Cutscenes.cs`)

| Where | Before | Now |
|---|---|---|
| Reveal, Bea's flash sticker | `Point` toward the side window | `ShadeEyes` toward the same place (where she looked, not what was there) + `bea.surprised` babble (her testimony's emotion) |
| Reveal, Mr. Vale's door sticker | head turn to the door | `CheckWatch` (when he looked), then the head turn + `vale.neutral` babble |
| Reveal, reopening | `kit.happy`, `june.happy` | same ids, now derived from `LineVoices` (`kit.first_show`, `june.go_outside`) via `CutsceneAudio.BabbleAsIn` |
| Reveal, score | `music_reveal` at 0 s | at 1.44 s, computed (`RevealMusicAt`) so the 19.36 s score ends exactly as the reopening beat starts (20.8 s) |
| Opening, the glowing window | silent | `pip.thinking` babble at 1.6 s (Pip wonders at the glow; no words) |
| Opening, Bea's wave; street theme | `bea.happy`, `music_street_theme` | unchanged (already the real ids) |

- **Why retime the score:** after a cue the music plan brings the street theme back from its start. With the reveal at
  0 s the theme faded in at 19.4 s, over the owl's three knocks (the story beat at 17.6–20.8 s). Now the reveal's quiet
  ending sits under the bows and the theme returns from its first bar as Aunt Kit opens her booth. The lead is computed
  from the imported master's sample count, so a re-render retimes it without code. A skip in the first 1.44 s simply
  never starts the score.
- **Header**: the "placeholders" note is gone; the ids are the imported score's and the voice library's (they already
  were since S003; this pass ties the emotions to `LineVoices`).
- **No new cutscene code**: only beats, cues and one data helper that places a music cue at an absolute time.
- `CutsceneAudio` gained `BabbleAsIn`, `EmotionOf` (by name; the two `BabbleEmotion` enums are pinned together by test)
  and `MusicSeconds`.

## 4. Narrative bible checklist (§6)

| # | Check | Pass |
|---|---|---|
| 1 | True, kind story | unchanged |
| 2 | Questions raised by the world | unchanged |
| 3 | Answers only by found stickers | unchanged; no gesture or glance adds or names a clue |
| 4 | Diegetic attraction | unchanged |
| 5 | Testimony is perception, partial, from a position | yes: the gestures *are* the perception (Bea searching up high, Vale's time, June listening) and add nothing her words do not say |
| 6 | No conclusion stated | yes: no gesture points at June, the window, the floor or the mechanism; Kit asks |
| 7 | Hints are areas and moves | yes: Pip's glance turns to the side of the hint **area**; only on tier 1; tested against the area table |
| 8 | At most one decoy | unchanged |
| 9 | Reveal pays off the child's work | yes: the witnesses repeat their testimony gestures over the child's own stickers |
| 10 | No deadlocks | unchanged; no case fact changes |
| 11 | Street remembers | unchanged; Bea's greeting vignette now rendered (frame 68) |
| 12 | Seed and continuity | unchanged |
| 13 | New mechanics serve the mystery | yes: none added; the glance serves the hint |
| 14 | Strings in en.json | no new string |
| 15 | Engagement guardrails | yes: nothing timed or pushy; a gesture never delays the line or the tap |
| 16 | Cutscenes | yes: still short (opening 20.5 s, reveal 28.4 s), skippable, rewatchable, in-engine, the same two purposes; the reveal still only after the stamp and only from the child's stickers |

## 5. Tests

- **Core** (`tools/core-tests` and Unity EditMode):
  - `TestimonyGestureTests` (7): the table (Bea `ShadeEyes` on her flash line, Vale `CheckWatch` on his time line, …;
    everything else still); every gesture is a spoken case line, the speaker's own clip, a one-shot, delay ≤ 2 s, none
    after the case closes, an aim only for aimed gestures; all four gestures used; fewer than half of each speaker's
    lines and at most one per conversation; Bea's aim is up high over the side yard and clear of every Use target;
    the glance rule (tier 1 only, standing, not moving, not reduced motion, area at least 30° aside, the right side,
    behind-and-aside); the glance target is the area's centre for every area.
  - `CutsceneTests` (+2, 2 updated): `TheRevealScoreEndsAsAuntKitReopensHerBooth` (two sticker sets),
    `WitnessesActAndSoundInCutscenesAsInPlay` (ShadeEyes, CheckWatch, no `Point`, babble ids from `LineVoices`, the
    emotion enums agree, Pip's opening babble); the allowed clips include the testimony gestures.
- **PlayMode** (`TestimonyGesturePlayTests`, new):
  - `ATestimonyTalkPlaysTheSpeakersGestureOnItsLine`: Bea's first talk plays `ShadeEyes` after its delay, turned toward
    the side of the house, then she turns back; Vale's first account plays nothing; his second talk plays `CheckWatch`
    and he does not turn on the bench; a line skipped before its moment drops its gesture.
  - `ATierOneHintMakesPipGlanceTowardTheArea`: facing east on the west sidewalk, the radio's first press makes Pip play
    `GlanceLeft` (the side yard); facing the yard, no glance; reduced motion, no glance; the second press (bats), no glance.
  - Updated for the retimed score: `CutscenePlayTests.TheThirdDeductionPlaysTheRevealOnceAndAReloadNeverReplaysIt`
    (the hook's calls in the data's time order) and `VoicesMusicTests.TheStingPlaysWhenTheCaseClosedCardShows` (waits for
    the reveal cue).
- **Visual** (`TestimonyGestureCaptureTests`, new): frames 65–68 below, plus review frames.

## 6. Frames (CI, looked at)

Committed under `docs/evidence/s003a/` from the run in §7.

| File | What it shows | Seen |
|---|---|---|
| `65_bea_shade_eyes.jpg` | Bea's testimony bubble; Bea up on her toes, hand flat over her brow, head back, turned toward the side of the house | reads clearly |
| `r_bea_shade_eyes_close.jpg`, `r_bea_shade_eyes_side.jpg` | the same moment without UI, front and side (the bike beside her) | the visor hand and the tipped head read at play distance |
| `66_vale_check_watch.jpg` | Vale's "Not one creak from that door all evening." bubble; seated, left forearm up, the dark watch on his wrist | reads; see §8 |
| `r_vale_check_watch_close.jpg` | closer, no UI | the watch and the bowed head read |
| `67_pip_hint_glance.jpg` | the tier-1 line ("A breeze keeps tugging toward the side yard."); Pip's head turned to her left, free hand up, the first leaves leaving her that way | reads |
| `r_pip_hint_glance_close.jpg`, `r_pip_hint_glance_wide.jpg` | close; and from behind, the warm glow over the side yard | |
| `68_street_greeting_bea.jpg` | after a remembered load: Bea's "Pip's back! See my owl? I'm your lookout now!" with its vignette (Pip, the bike, the owl sticker, the eye opened over the bike) | first time this vignette was rendered; reads |
| `55_reveal_sticker_vale.jpg` (existing frame, new content) | the reveal's Vale sticker: over his shoulder, head bowed to his watch, the door shut | the watch itself is hidden from behind; the bow reads |
| `r_reveal_sticker_bea_flash.jpg` | the reveal's Bea sticker | Bea is mostly below the frame (as with `Point` before); see §8 |

## 7. CI

**Run 148** of `ci.yml` (id 45149533149) on `972012d`: **success**, read from the job log.

| Results | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| EditMode | 673 | 673 | 0 | 0 |
| PlayMode | 133 | 133 | 0 | 0 |
| Visual captures | 16 | 16 | 0 | 0 |

- `TestimonyGestureTests` 7/7, `CutsceneTests` 24/24, `TestimonyGesturePlayTests` 2/2 (none ignored, so the models and
  clips were found), `CutscenePlayTests` 5/5, `VoicesMusicTests` 5/5, `HintPicturesTests` 8/8, `GestureClipTests` 5/5,
  `TestimonyGestureCaptureTests` 2/2. Static checks job: success.
- Run **147** (`c808e6d`, the first push) failed two PlayMode tests that pinned the old reveal audio order
  (`CutscenePlayTests…ReloadNeverReplaysIt`, `VoicesMusicTests.TheStingPlaysWhenTheCaseClosedCardShows`): the score
  now starts 1.44 s in. Both were updated to read the order from the data; run 148 is green. Run 147's frames were
  looked at and led to two framing fixes (Pip no longer covers Mr. Vale in 66; the wide glance frame shows Pip whole).
- Frames were extracted from the raw job log (`gh api …/actions/jobs/<id>/logs` + `tools/ci/extract_frames.py`) and
  looked at; the files in §6 are run 148's.
- Seen in the run, not from this branch: the "Unity normalised files" step lists audio, FBX and UI `.meta` files that
  come with `main`.
- Local: `./tools/check-local.sh` passes (633 core tests).

## 8. Notes and gaps

- The reveal's two witness sticker shots are framed **over the shoulder** (from S002.5b): the gestures play but read
  only partly (Vale's bow; Bea's raised arm at the frame's bottom edge). Reframing those shots is a camera data change I
  left for the owner's call, since the shots are about what the witness saw.
- `vale.door_shut` (his first account) has no watch: the watch belongs to his lines about time. If the owner wants the
  watch on the first talk too, it is one row.
- The glance under reduced motion (§2).
- Not checked by ear: the babble added to the reveal stickers and the opening, and the retimed score.
