# S003: audio synthesis pipeline and the first sound set

Branch `feature/s003-audio-pipeline`. This is the D20 foundation that the roadmap marks as safe to build in parallel:
a synthesis pipeline in `tools/audio/`, a first set of 25 sounds (91 takes) led by the case 01 clue sounds, an owner
listening page, and a minimal Unity side (catalog and code-built mixer). **Nothing is wired to gameplay yet.** The
owner approves the sounds by ear first.

## Pipeline (`tools/audio/`)

- Run `python3 tools/audio/build.py` (or `--only <ids>`). It needs numpy and scipy (pinned in
  `tools/requirements-ci.txt`) and uses ffmpeg for the MP3 previews if it is installed. It renders every take, masters
  it, checks it and writes `docs/evidence/s003/listening/`: `audio/*.mp3`, `spectrograms/*.png`, `report.json` and
  `index.html`. The 48 kHz, 16-bit WAV masters go to `tools/audio/out/wav/`. That folder is ignored by git: WAVs
  would go through LFS, which cloud sessions cannot reach.
- **Everything is synthesised (D20).** Nothing is downloaded, sampled or taken from a pack.

| Module | What it holds |
|---|---|
| `dsp.py` | envelopes, noise (white and coloured), Butterworth/RBJ filters, time-varying Klatt resonators, modal banks, synthetic room IRs, circular convolution for loops |
| `models.py` | wood (modal panel and plank knocks, hard cam tap, creak), crackle grains (paper, adhesive, leaves, grit), cloth flutter and snap, footsteps on wood, grass and stone, mallet bars (marimba, celesta), plucked string |
| `voice.py` | babble voices: a Rosenberg glottal source (2x oversampled), with jitter, shimmer, vibrato and breath, through a 5-formant cascade, plus consonant approximations; per-character `VoiceProfile` |
| `sounds.py` | the sound set: ids, descriptions, game purpose, takes, duration budgets, sync markers, what to listen for |
| `master.py` | phone mastering: 4th-order high-pass (90–160 Hz per category), low-pass, soft-knee compression (ratio 1.5–2.5), loudness normalisation to the category target, look-ahead true-peak limiter (ceiling −1 dBTP) |
| `loudness.py` | ITU-R BS.1770-4 K-weighted, gated integrated loudness; 4x-oversampled true peak; clips shorter than 400 ms are measured over the whole clip ("short-clip") |
| `checks.py` | per-take checks (loudness ±1 LU, true peak, clipping, DC, sub-bass, edge clicks or loop seam, duration budget), seeded TPDF-dithered WAV, spectrogram PNG (waveform over a log-frequency spectrogram, guide lines at 100 Hz, 250 Hz (the phone floor), 1, 4 and 10 kHz) |
| `page.py` | the listening page |

- **Determinism:** every random choice comes from `numpy.random.default_rng(crc32(id|take))`. Rebuilding gives the
  same samples, the same WAV bytes and the same MP3 bytes (bit-exact ffmpeg flags).
- **Variation:** each sound has 3–5 takes (mode detuning, contact hardness, timing, grain placement, syllables). The
  runtime plays them from a shuffle bag with no immediate repeat and adds a little pitch jitter.
- **Tests:** `python3 -m unittest discover -s tools/audio/tests -t tools/audio` covers the meter against BS.1770
  reference values (a 0 dBFS 997 Hz sine reads −3.01 LUFS, stereo +3 dB, gating, inter-sample true peak), mastering
  to every category target under the ceiling, the sub-bass cut, loop seams, the checks catching faults, seeded
  determinism (a pinned seed value, identical audio and WAV bytes), takes differing, every sound passing its checks,
  and **catalog sync** (ids, take counts, loops and knock markers against `AudioEvents.cs`). This runs in
  `tools/check-local.sh` and in the CI static job.

## Loudness targets (per category, before the player's volume sliders)

| Category | Target | High-pass | Used by |
|---|---|---|---|
| clue | −18 LUFS (knocks −20, floor hum −19, cam tap −21 per-sound trims) | 110 Hz | knocks, hum, cam, wind wheel |
| movement | −22 | 120 Hz | jump, land, glide |
| footstep | −25 | 120 Hz | steps |
| notebook | −21 | 140 Hz | page turn, stickers, glow |
| ui | −24 | 160 Hz | UI tap |
| voice | −19 | 130 Hz | babble |
| ambience | −28 | 90 Hz | night bed |
| music | −20 | 90 Hz | motif |

The trims on the knocks, hum and cam keep the limiter from flattening their attacks. Even so, the peaks still go
through 1–3.6 dB of peak limiting (`limiter_db` in the report).

## The set (evidence: `docs/evidence/s003/listening/report.json`)

All 91 takes passed every check. True peak ≤ −1.2 dBTP everywhere, no clipped samples, |DC| < 0.001.

| Id | Takes | Length (s) | Loudness (LUFS) | Notes |
|---|---|---|---|---|
| `clue_knocks_distant` | 4 | 1.8–1.9 | −20.0 | knock onsets fixed at 0.02, 0.28, 1.00 s (short, short, pause, last) for cam and owl sync; mono, so it is placed at the house in 3D |
| `clue_floor_hum` | 3 | 4.2 | −19.0 | the hum swells over 1 s; knocks at 1.10, 1.36, 2.08 s with floor boom and a dust rattle; the 98 Hz hum is carried by overtones |
| `clue_cam_tap` | 5 | 0.3 | −21.0 (short-clip) | hard wood on wood plus a follower settle |
| `clue_wind_wheel` | 3 | 3.2 | −18.0 | blade-pass whirr that spins up and slows, axle creaks |
| `step_wood` / `step_grass` / `step_stone` | 5 each | 0.14–0.25 | −25.0 (short-clip) | heel and toe; one wood take creaks; walk previews on the page |
| `move_jump` / `move_land` | 4 each | 0.32 / 0.38 | −22.0 | push and cape whoosh / thud, soles, one cape flutter, dust |
| `move_glide_open` / `move_glide_loop` | 3 each | 1.4 / 2.0 | −22.0 | cape snap and flutter / a seamless crossfaded loop |
| `ui_page_turn` | 4 | 0.78 | −21.0 | the threshold page turn (D24.1) |
| `note_sticker_pick` / `_drop` / `_wobble` / `note_correct_glow` | 4/4/3/3 | 0.3 / 0.14 / 0.75 / 2.2 | −21.0 | peel, press, friendly boing and *tok*, celesta glow in D major |
| `ui_tap` | 5 | 0.12 | −24.0 (short-clip) | felt-covered wooden button |
| `voice_pip`, `voice_bea`, `voice_vale`, `voice_june`, `voice_kit` | 3 each (different lines) | 2.0–4.2 | −19.0 | measured median pitch about 340, 400, 127, 280 and 290 Hz; melodic ranges about 6–14, 6–9, 4–6, 6–9 and 9–12 semitones |
| `amb_night` | 3 | 24.0 (stereo loop) | −28.0 | gusts with whole cycles per loop (about 10 dB of swell), leaves, two crickets; generated circularly, so the seam is continuous |
| `music_motif_pluck` / `music_motif_wood` | 3 each | 3.8 / 5.0 | −20.0 | D–F–A in the knock rhythm; closure on marimba with a rolled D major chord (the Picardy lift) |

The spectrograms were reviewed by eye. Two problems were found and fixed before this set: the plucks and celesta
were cut off abruptly at their buffer ends, and the grass and page turn had too much energy above 8 kHz. Wood ringing
was shortened, the hum was made less organ-like (faster harmonic roll-off plus a woody rumble), and the voices got
wider melodies, formant wander and syllable arches.

## Unity side (minimal, not wired)

- `Core/Audio/AudioCatalog.cs` (engine-free): `AudioBus`, `AudioEventDef` (takes, trim, pitch jitter, loop,
  spatial, minimum interval, markers, resource path `Audio/{id}/{id}_t{n}`) and `AudioCatalog` with validation.
- `Core/Audio/AudioEvents.cs`: the 25 ids, the knock markers and `CreateCatalog()`.
- `Core/Audio/AudioPlayback.cs`: `VariantPicker` (a seeded xorshift shuffle bag with no repeats), `AudioThrottle`, and
  `AudioMix`, which holds the parameter names `MusicVol`, `SFXVol`, `VoiceVol` and `AmbienceVol`, the slider-to-dB
  taper, the preferences mapping (ambience follows effects), −6 dB voice ducking of music and ambience, and the pitch
  jitter.
- `Runtime/Audio/AudioRig.cs` is a **code-built mixer**. Unity's AudioMixer group API is editor-internal, so a mixer
  asset cannot be authored from code reliably. The rig instead uses four buses of pooled AudioSources with the same
  exposed parameter names, `SetVolume`/`ApplyPreferences`, `Play`/`PlayAt`/`Stop`, and loads clips from Resources
  through `IAudioClipSource`. If an AudioMixer and its groups are assigned later, sources route into those groups
  and the mixer receives the same parameter names. It uses standard AudioSource calls only (D25).
- Tests: `Tests/EditMode/Core/AudioCatalogTests.cs` (11, run by `tools/core-tests` and Unity) and
  `Tests/PlayMode/AudioRigTests.cs`. The PlayMode tests use generated stand-in clips to cover take rotation, bus
  volumes, preferences, 3D placement, loops, throttling and missing clips.
- No clips are imported into `Assets/` yet. After approval, the next step brings the WAV masters in under
  `Content/Resources/Audio/<id>/` with a plain-git `.gitattributes` exception (like Models), using Vorbis
  compression, mono for 3D sounds and load-in-background for the ambience, and wires the events.

## Owner review

Open `docs/evidence/s003/listening/index.html`. Each card has the sound, its takes, what it does in the game, what to
listen for, its loudness and its spectrogram. Approve or reject per sound. Nothing here counts as a pass until the
owner has listened.
