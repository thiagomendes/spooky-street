# S003: the approved voices and music in the game (D20.2)

Branch `feature/s003-voices-music-wiring`. The owner approved the v2 voices and music with the recommended options
(D20.2): cartoon babble with phrase takes as the main path; street theme seed 101, exploration seed 111, case-closed
sting seed 121 and reveal seed 131. This increment imports them, ports the babble planner to Core, and wires voices
and music to the dialogue, the street, the house, the "Case closed" card and the cutscene system. CI evidence is at
the end.

## Import

- **Pipeline:** `babble_build.py` and `music_build.py` render the WAV masters into `tools/audio/out/` (ignored)
  from the committed sources, with no network. `import_unity.py` (extended) then copies them into
  `Content/Resources/Audio/<id>/<id>_t<n>.ogg` and writes `Core/Audio/VoiceMusicData.cs`. Run
  `tools/unity_meta.py` afterwards. The rebuild is reproducible: re-rendering gave a byte-identical
  `babble_bank.json` and `music_edits.json`, and the 70 effect takes were unchanged by the re-import.
- **Plain git**, like the effects (the `.gitattributes` exception for `Content/Resources/Audio/**`).
- **Voices:** one event per character and emotion, `voice_<char>_<emotion>` (25 events). Its six takes are the
  phrase takes p1–p6 (0.5–3.1 s, shortest first): 150 clips.
  - Stored as mono Ogg Vorbis, libvorbis q6, bit-exact (about 23 KiB each, 3.35 MiB in all, instead of 26 MiB of WAV).
  - Imported at Vorbis 0.7, **Compressed in memory**, a new `AudioLoad` value. Decompressing all 150 phrases would
    hold about 26 MiB of PCM; compressed they hold about 3.5 MiB. A babble does not need a frame-exact start.
  - **The syllable banks are not imported.** The runtime plays phrase takes only. They are the main path in D20.2,
    and the listening proxy rated the sequenced babble choppier (`docs/S003.1-voices-music.md`). Variety comes from
    the take rotation instead (see Planner).
- **Music:** `music_street_theme`, `music_exploration`, `music_case_closed` and `music_reveal`, one take each.
  - Stored as stereo Ogg Vorbis q6 (3.56 MiB for the four).
  - Imported at Vorbis 0.7, Streaming, load in background.
  - Each event has a −3 dB trim, so the clue sounds and the babble stay on top of the score.
- **Loop points:** each loop master is the loop body itself (start to end, with its 60 ms seam already crossfaded
  by `music.make_loop`). So the clip loops whole on `AudioSource.loop`, and there are no loop points to lose.
  What must survive is the exact length and the seam:
  - **The decoded Ogg keeps every sample.** Theme 3 637 510 samples (75.781 s), exploration 3 686 401 (76.800 s),
    sting 277 359, reveal 929 477. These equal the WAV masters. `VoiceMusicData` records them, and the music plan
    times its passes by them.
  - **Checked three ways:**
    1. Python `test_music_keeps_its_exact_length_and_loop_seam` decodes the project Ogg files. It checks the length
       against the WAV master and the C# constant.
    2. The same test checks the seam: the jump from the last sample back to the first is below both 0.02 and the
       music's own 99.9th-percentile sample step.
    3. The Unity EditMode test `EveryVoiceAndMusicTakeIsImportedWithItsRules` checks that `AudioClip.samples` of
       each imported music clip equals the constant, so Unity's re-encode keeps the length too. It also checks that
       every phrase clip's length equals the planner's table within 2 ms.
- **Size added:** 7 238 870 bytes (6.9 MiB) of audio (3 510 357 voices, 3 728 513 music), plus 154 metas and the
  generated data file. `test_the_project_stays_lean` caps the v2 set at 8 MiB (the effects keep their 9 MiB cap).
- **Import rules** (`AudioImportPlan`, `Editor/AudioImportRules.cs`, version 2):
  - Voice → `CompressedInMemory`;
  - Music → `Streaming`;
  - `IsApproved` covers the effects plus the v2 voices and music;
  - the rejected S003 formant voices (`voice_pip`…) and plucked motif stay in the catalog for the pipeline sync,
    and are never imported (tested).

## Babble planner (Core, engine-free)

`Core/Audio/BabblePlanner.cs` ports the Python reference (`babble.caption_syllables`, `plan_sequence`'s length rule):

1. **Syllables of the caption as shown.** A syllable is a vowel group per word, `[A-Za-zÀ-ÿ']+` words, `y` counted
   as a vowel. A silent final *e* is dropped unless the word ends in *le* or *ee*. Each word counts at least one.
2. **Length:** `0.6 × syllables ÷ rate`, clamped to 0.6–3 s. The rate is the character's syllables per second (Pip
   8.5, Bea 10.5, Vale 5.5, June 9, Kit 7).
3. **The phrase take of the line's emotion closest to that length** (the main path).
4. **Variety** (`BabblePicker`): if the closest take is the one this voice played last, and another take is within
   0.35 s as close, the other one plays. The same line twice never sounds identical.

`VoiceMusicData.cs` is generated from `babble_bank.json` (rates and phrase lengths) and the music masters. Python
`test_generated_csharp_data_is_current` fails if it drifts. The C# test `SyllablesMatchThePythonReference` holds 14
golden captions, and Python `test_planner_golden_captions_match_the_python_reference` checks the same captions
against `babble.caption_syllables`, so both implementations are pinned together.

**Emotion per line:** `Core/Audio/LineVoices.cs` is a data table keyed by the line's localization key, so a
translation never changes a voice. Captions have no voice. A test checks that every spoken line of `CaseScript`
has a row and that every row is a spoken line. The emotion follows how the speaker sounds, never what the line
means for the case (D26):

| Speaker | Lines |
|---|---|
| Pip (examine) | thinking (streamer, cloth, sill, owl panel), neutral ("It won't budge."), happy (the bowl, the saved sweet) |
| Bea | surprised (saw the cape, door open now?), neutral (repeat), happy (closed) |
| Mr. Vale | neutral (door shut, invite to sit, repeat, was shut), thinking ("It's open now, though. Well, well."), happy (closed, saved sweet) |
| June | whisper (both greetings, latch reminder, listen to the floor), neutral (door sticks), surprised (door already open, resolve), happy (latch thanks, go outside, closed) |
| Aunt Kit | happy (first show, thanks), thinking ("So, detective, what happened here?") |

## Voices wired

- `DialogueView` gains two events (8 lines, nothing else changed): `LineShown(ScriptLine, string text)` at the end of
  `Show`, and `Closed` from `Cancel`. `Cancel` covers finishing, Skip and cancelling.
- `GameAudio` listens:
  - **A spoken line plays its speaker's babble** in the line's emotion on the Voice bus (2D). This includes Pip's
    examine lines in Pip's voice, and the finale's lines. Every non-caption line goes through the dialogue (the
    caption strip shows only environmental captions, which stay silent).
  - **The next line, Skip and closing stop it** with a 60 ms fade, so there is no click.
- **Ducking:**
  - Music and the night bed dip 6 dB under a voice. The duck now ramps: in at 40 dB/s (0.15 s), out at 10 dB/s
    (0.6 s), so it no longer steps. Effects never duck.
  - The rig logs `Duck` and `Unduck` entries.
  - The voice is held by the pause (it pauses with the game), and so is its duck.
- **Run 113's unexplained −6 dB on the bed** (`docs/S003-sfx-wiring.md` Gaps) was the size of this duck. The old
  `AudioRig.VoiceActive` read `AudioSource.isPlaying` of every voice source. That property depends on the audio
  device, and batchmode has none. The cause was never confirmed, but the duck no longer reads `isPlaying`:
  - each voice play lasts its clip's length on unscaled time;
  - that time holds under the pause;
  - a stop ends it.

  A stale or device-dependent play state can no longer duck the bed. The new rig test asserts that there is no
  duck without a voice, and the old relative check in `BusShapingTrimsAndFiltersOneBus` stays.
- **Slider:** the Voices slider (`VoiceVol`) now controls real audio.

## Music wired

`Core/Audio/MusicPlan.cs` holds the rules: pure, tested, on unscaled time. `GameAudio` applies its start, stop and
fade commands to the rig. `AudioRig.PlayTake(id, take, fadeIn)` adds fade-ins, and a stop fades out from wherever
a fade-in left the gain.

- **Title and street:** the street theme fades in (1.5 s) on the title and carries on into the game.
  1. One theme pass, then exploration for two passes, then the theme again, and so on.
  2. Each change is a 3 s crossfade that starts 3 s before the pass ends. The new loop starts from its beginning as
     the old one reaches its seam.
  3. Between passes the clip loops on its own seam.
- **Indoors (my choice):** the exploration loop only, crossfaded in over 1.5 s under the page-turn, with the music
  bus 5 dB lower.
  - *Why:* exploration 111 is the theme's sparse twin. The interior is where the child listens for clues: the hall's
    hum and the mechanism's three knocks. Busier music or a muffle would fight them. A low-pass "through the walls"
    would make the score sound like it comes from outside, and the score is not a sound in the world.
  - *Back outside:* the current exploration pass finishes, then the theme returns.
- **Pause:** music keeps playing (the existing design), 6 dB lower under the pause card. The music bus is never held.
- **"Case closed":**
  - The step that stamps the case arms the sting, and the sting plays when the notebook's final "Case closed" card
    shows. That happens after the last comic (the card slides in 1.4 s later), or when the notebook opens on it
    after the reopening.
  - If the child closes the notebook first, the sting waits 4 s of nothing on screen, then is dropped rather than
    played out of context.
  - The loop fades out under the sting (0.4 s) and the theme comes back from its start after it (2 s fade-in).
- **Slider:** the Music slider (`MusicVol`) now controls real audio (tested).

## Hooks for cutscenes (`feature/s002.5b-cutscenes`)

The other branch's cutscenes raise `SignalCue`s with placeholder ids: `music.c01.opening`, `music.c01.reveal`,
`babble.c01.bea.hello`, `babble.c01.kit.cheer`, `babble.c01.june.cheer`. Its `CutscenePlayer.CueFired` handler
needs one line for both channels:

```csharp
player.CueFired += cue => SpookyStreet.Core.Audio.AudioCues.PlayCue(cue.Id); // music and babble ids
```

All hooks are static, engine-free and in `Core/Audio/AudioCues.cs`. With no listener they do nothing.

| Call | Effect |
|---|---|
| `AudioCues.PlayCue("music_reveal")` or `PlayCue("music.c01.reveal")` | the reveal (19.4 s) plays; the loop fades out under it (0.4 s) and the loop for where Pip is comes back after it (2 s) |
| `PlayCue("music_case_closed")` / `"music.c01.case_closed"` | the sting (5.8 s), same rules; a newer cue replaces an older one |
| `PlayCue("music.c01.opening")` / `"music_street_theme"` | restarts the street theme from its start (`music_exploration` / `music.c01.explore` jumps to exploration) |
| `PlayCue("babble.c01.<char>.<word>")` | that character's babble, about 1.2 s; hello, hi, cheer, yay, laugh → happy; gasp, wow, oh → surprised; hmm → thinking; psst, shh → whisper; anything else neutral |
| `AudioCues.Babble(Speaker.Kit, BabbleEmotion.Happy, 1.5f)` | a babble of a chosen emotion and length (the closest phrase take, 0.6–3 s) |
| `AudioCues.StopCue(fade = 0.5f)` | ends the reveal or sting early; the loop comes back |
| `AudioCues.StopMusic(fade = 1f)` / `AudioCues.ResumeMusic()` | silence until resumed (or until the next `PlayCue`) |
| `AudioCues.DuckMusic(true / false)` | holds the music bus 10 dB lower (voice lines duck it 6 dB anyway) |

`PlayCue` returns false for an unknown id. `AudioCues.ResolveMusic` and `TryResolveBabble` expose the mapping.
Babble placed with `.Babble(...)` in a cutscene goes through the Voice bus, so it ducks the music and holds with
the pause like a line. I did not edit the cutscene branch's files.

**Interplay:** that branch plays the reveal on the step that stamps the case. That step also arms the case-closed
sting, which waits for the notebook's card. Whichever cue comes last wins (a newer cue replaces the older with a
0.5 s fade). If the reveal should always win, that branch can call `AudioCues.StopCue()` after its cutscene, or skip
the card's sting with `PlayCue("music_reveal")` once the card is up.

## Tests

- **EditMode, Core** (run by `tools/core-tests` and Unity):
  - `BabblePlannerTests` (20 cases):
    - the 14 golden syllable counts;
    - the length rule and both clamps;
    - the closest take for every character, emotion and length;
    - variety;
    - every spoken line's emotion, and captions silent;
    - the v2 catalog (buses, takes, import rules, pause behaviour);
    - the cutscene ids and the request events.
  - `MusicPlanTests` (7):
    - the exact loop lengths;
    - theme → exploration ×2 → theme, with crossfades on the seam;
    - indoors exploration only and lower, and back out;
    - starting indoors;
    - the sting over a faded loop that comes back;
    - the reveal replacing the sting, and an early stop;
    - loop cues, silence and resume.
  - `AudioCatalogTests` and `AudioWiringTests` updated for the 54-event catalog.
- **EditMode, Unity** (`AudioImportTests`):
  - `EveryVoiceAndMusicTakeIsImportedWithItsRules`: all 154 clips with their importer settings, channels and 48 kHz;
    each phrase's length equals the planner's table; each music clip's sample count is exact.
  - `OnlyApprovedSoundsAreImported` (renamed).
- **PlayMode, rig** (`AudioRigTests.VoiceDucksMusicAndTheBedSmoothlyAndAStopOrTheEndReleasesIt`):
  - the chosen take plays;
  - the duck ramps in and dips music and the bed, but not effects;
  - a held voice still ducks;
  - a stop releases the duck, and so does a voice playing to its end;
  - music fade-in and fade-out.
- **PlayMode, game** (`VoicesMusicTests`, real scene, `CaseDirector.UseTemporarySaveDirectory()` in Setup):
  - **Lines:**
    - Bea's first line plays `voice_bea_surprised` with the planner's take, and music and the bed duck 6 dB.
    - Skip stops the babble and the duck lets go.
    - Vale's two lines each play and stop.
    - Pip's cloth examine plays `voice_pip_thinking`.
    - One babble per spoken line.
  - **Music:**
    - the theme plays on the title and carries into the game;
    - the pause keeps it playing 6 dB lower;
    - the music slider scales it;
    - through June's window it crosses to exploration 5 dB lower.
  - **Sting:** it waits for the card after the stamping deduction, plays once when the "Case closed" card shows, and
    the theme comes back after it.
  - **Reveal API:**
    - `PlayCue("music.c01.reveal")` fades the theme and plays the reveal;
    - `DuckMusic` lowers the bus and lets go;
    - `StopCue` brings the loop back;
    - `StopMusic` and `ResumeMusic` work;
    - `PlayCue("babble.c01.bea.hello")` plays Bea's happy babble.
- **Python** (`tools/audio/tests/test_import.py`, 7 tests):
  - the folder set and formats (mono and stereo Vorbis, 48 kHz);
  - the generated C# data is current;
  - the exact music lengths and the loop seam (needs ffmpeg, skipped without it);
  - the golden captions;
  - the size caps.

## What to listen for on the phone

- **Babble level against the music and the bed.** The voices are mastered 1 dB above the music, the music has a
  −3 dB trim, and the duck takes it 6 dB further down. Does Bea cut through on the street, and does Vale's low
  voice?
- **The duck itself.** Is a 0.15 s dip and a 0.6 s recovery smooth, or does it pump on Vale's two lines?
- **The theme to exploration crossfade** at about 1:13 on the street, and the seam of each loop (theme at 75.8 s,
  exploration at 76.8 s). There should be no click or skip.
- **Indoors:** exploration 5 dB lower. Are the floor hum and the mechanism's knocks clearly on top?
- **The sting at "Case closed".** Does it land with the card, and does the theme come back gently?
- **Phrase choice:** short lines get the 0.6 s takes. Do long lines feel cut short at 3 s (by design the text line
  carries the words)?
- **Children's pitch** (the open point in S003.1): it is still one number per character in `babble_spec.py`.

## Gaps

- **Not checked by ear in the game.** CI has no audio device, so the tests prove the wiring (events, takes, timing,
  levels as numbers), not the mix.
- **The syllable banks are unused at runtime.** If the owner wants more variety, the bank sheets can be imported
  and sliced at load.
- **Music timing runs on the game's unscaled clock, not the audio DSP clock.** On a device the crossfade point
  may drift by a few milliseconds from the clip's own seam. That is harmless, because each clip also loops on its
  own seam.
- **The finale lines** (Kit and June at the reopening) babble like any line. The cutscene branch's babble
  placeholders map to emotions by their last word only.

## CI evidence

Pending.
