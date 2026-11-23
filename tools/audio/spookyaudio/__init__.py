"""Spooky Street audio synthesis pipeline (decision D20).

Every sound in the game is synthesised here from code: no recordings, samples or sound packs. The package holds
the DSP building blocks (`dsp`), the physical and spectral models (`models`, `voice`), the sound set (`sounds`),
the phone-speaker mastering chain (`master`), loudness metering (`loudness`), the automatic checks (`checks`) and
the owner listening page (`page`). `tools/audio/build.py` renders everything.

Determinism: every random choice flows from a seed derived from the sound id and take number
(`dsp.seed_for`), so the same scripts always render the same audio.
"""

SAMPLE_RATE = 48000
