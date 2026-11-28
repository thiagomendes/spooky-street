#!/usr/bin/env python3
"""Copy the owner-approved sounds into the Unity project: the effects (D20.1) and the v2 voices and music (D20.2).

Usage:
  python3 tools/audio/build.py          # renders the effect masters into tools/audio/out/wav
  python3 tools/audio/babble_build.py   # renders the babble phrase takes into tools/audio/out/babble/<char>/
  python3 tools/audio/music_build.py    # renders the music cues into tools/audio/out/music/
  python3 tools/audio/import_unity.py   # -> Assets/SpookyStreet/Content/Resources/Audio/<id>/<id>_t<n>.<ext>
                                        #    and Core/Audio/VoiceMusicData.cs (phrase lengths, music lengths)

Formats (docs/S003-sfx-wiring.md): short effects keep their 48 kHz, 16-bit mono WAV masters (Unity encodes them to
Vorbis on import, so the repo holds the lossless source); the 24 s stereo night bed is stored as Ogg Vorbis q6
(about 190 kbps) to keep the repo lean. Encoding is bit-exact, so re-running gives identical files.

Voices and music (docs/S003-voices-music-wiring.md): every phrase take (5 characters x 5 emotions x 6 lengths) as
mono Ogg Vorbis q6, one event per character and emotion (`voice_<char>_<emotion>`, take n = phrase p<n>); the four
approved cues (street theme seed 101, exploration 111, case-closed sting 121, reveal 131) as stereo Ogg Vorbis q6.
The syllable banks are not imported (the runtime plays phrase takes only). The rejected formant voices
(`voice_<char>`) and the plucked motif (`music_motif_*`) are never imported.
The decoded length of every Ogg equals its WAV master sample for sample, so the music loops keep their exact loop
region (each loop master is the loop body, start to end); tests/test_import.py checks the length and the seam.
Run tools/unity_meta.py afterwards for the .meta files of new files.
"""
import json
import shutil
import subprocess
import sys
import wave
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from spookyaudio import sounds  # noqa: E402
from spookyaudio.babble_spec import CAST, EMOTIONS  # noqa: E402

REPO = HERE.parent.parent
WAV_DIR = HERE / "out" / "wav"
BABBLE_DIR = HERE / "out" / "babble"
MUSIC_DIR = HERE / "out" / "music"
BANK = HERE / "generated" / "babble_bank.json"
DEST = REPO / "Assets" / "SpookyStreet" / "Content" / "Resources" / "Audio"
DATA_CS = REPO / "Assets" / "SpookyStreet" / "Core" / "Audio" / "VoiceMusicData.cs"

# The effects the owner approved in the first listening review (D20.1), plus the doorbell below. Never voice_* or
# music_*.
APPROVED = (
    "clue_knocks_distant", "clue_floor_hum", "clue_cam_tap", "clue_wind_wheel",
    "step_wood", "step_grass", "step_stone",
    "move_jump", "move_land", "move_glide_open", "move_glide_loop",
    "ui_page_turn", "note_sticker_pick", "note_sticker_drop", "note_sticker_wobble", "note_correct_glow",
    "ui_tap", "amb_night",
    # The living street's doorbell ships before the owner's listening review (brief of 2026-11-28); replaceable.
    "street_doorbell",
)

# Stored as Ogg Vorbis (long and stereo), with the libvorbis quality; everything else keeps its WAV master.
OGG = {"amb_night": "6"}

# D20.2: the phrase takes per character and emotion (take n is phrase p<n>, shortest first).
PHRASES = 6
VOICES = tuple(f"voice_{c.id}_{e}" for c in CAST for e in EMOTIONS)

# D20.2: the approved music options, one take each.
MUSIC = {
    "music_street_theme": "music_street_theme_s101",
    "music_exploration": "music_exploration_s111",
    "music_case_closed": "music_case_closed_s121",
    "music_reveal": "music_reveal_s131",
}
VORBIS_V2 = "6"


def extension(sound_id):
    return "ogg" if sound_id in OGG or sound_id in VOICES or sound_id in MUSIC else "wav"


def all_ids():
    return APPROVED + VOICES + tuple(MUSIC)


def takes_of(sound_id):
    if sound_id in VOICES:
        return PHRASES
    if sound_id in MUSIC:
        return 1
    return {s.id: s for s in sounds.SOUNDS}[sound_id].takes


def source_of(sound_id, take):
    """The WAV master a project take comes from."""
    if sound_id in VOICES:
        _, char, emotion = sound_id.split("_", 2)
        return BABBLE_DIR / char / f"{emotion}_p{take}.wav"
    if sound_id in MUSIC:
        return MUSIC_DIR / f"{MUSIC[sound_id]}.wav"
    return WAV_DIR / f"{sound_id}_t{take}.wav"


def encode_ogg(src, dst, quality):
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(src), "-map_metadata", "-1", "-fflags", "+bitexact",
                    "-flags:a", "+bitexact", "-c:a", "libvorbis", "-q:a", quality, str(dst)], check=True)


def wav_frames(path):
    with wave.open(str(path)) as w:
        return w.getnframes()


def cs_float(x):
    return f"{x:.3f}f"


def data_cs(music_samples):
    """Core/Audio/VoiceMusicData.cs: each character's syllable rate and phrase lengths (babble_bank.json), and the
    exact length in samples of each music cue (its WAV master, equal to the decoded Ogg)."""
    bank = json.loads(BANK.read_text())["characters"]
    lines = [
        "// <auto-generated> by tools/audio/import_unity.py from tools/audio/generated/babble_bank.json and the music",
        "// masters. Do not edit: re-run the import. tools/audio/tests/test_import.py checks it is current.",
        "// </auto-generated>",
        "namespace SpookyStreet.Core.Audio",
        "{",
        "    public static partial class VoiceMusicData",
        "    {",
        "        /// <summary>Character ids in cast order.</summary>",
        "        public static readonly string[] Characters = { " + ", ".join(f'"{c.id}"' for c in CAST) + " };",
        "",
        "        /// <summary>Babble syllables per second after processing (babble_spec.py), per character.</summary>",
        "        public static readonly float[] SyllableRates = { " +
        ", ".join(cs_float(bank[c.id]["syllable_rate"]) for c in CAST) + " };",
        "",
        "        /// <summary>Phrase take lengths in seconds: [character][emotion][take - 1], emotions in BabbleEmotion",
        "        /// order (neutral, happy, surprised, thinking, whisper).</summary>",
        "        public static readonly float[][][] PhraseSeconds =",
        "        {",
    ]
    for c in CAST:
        emos = bank[c.id]["emotions"]
        lines.append(f"            new[] // {c.id}")
        lines.append("            {")
        for e in EMOTIONS:
            lines.append("                new[] { " + ", ".join(cs_float(p["duration_s"]) for p in emos[e]["phrases"]) +
                         " },")
        lines.append("            },")
    lines += [
        "        };",
        "",
        "        /// <summary>Exact length of each music cue in samples at 48 kHz (a loop's whole clip is its loop",
        "        /// region).</summary>",
        "        public const int StreetThemeSamples = %d;" % music_samples["music_street_theme"],
        "        public const int ExplorationSamples = %d;" % music_samples["music_exploration"],
        "        public const int CaseClosedSamples = %d;" % music_samples["music_case_closed"],
        "        public const int RevealSamples = %d;" % music_samples["music_reveal"],
        "    }",
        "}",
        "",
    ]
    return "\n".join(lines)


def main():
    missing = []
    for sound_id in all_ids():
        folder = DEST / sound_id
        folder.mkdir(parents=True, exist_ok=True)
        for take in range(1, takes_of(sound_id) + 1):
            src = source_of(sound_id, take)
            if not src.exists():
                missing.append(str(src.relative_to(HERE)))
                continue
            dst = folder / f"{sound_id}_t{take}.{extension(sound_id)}"
            if sound_id in OGG:
                encode_ogg(src, dst, OGG[sound_id])
            elif extension(sound_id) == "ogg":
                encode_ogg(src, dst, VORBIS_V2)
            else:
                shutil.copyfile(src, dst)
            print(f"{dst.relative_to(REPO)}  {dst.stat().st_size / 1024:.0f} KiB")
    if missing:
        print("Missing masters (run build.py, babble_build.py and music_build.py first): " + ", ".join(missing),
              file=sys.stderr)
        return 1
    DATA_CS.write_text(data_cs({sound_id: wav_frames(source_of(sound_id, 1)) for sound_id in MUSIC}))
    print(DATA_CS.relative_to(REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main())
