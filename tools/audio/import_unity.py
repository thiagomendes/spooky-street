#!/usr/bin/env python3
"""Copy the owner-approved sound effects (D20.1) from the WAV masters into the Unity project.

Usage:
  python3 tools/audio/build.py          # renders the masters into tools/audio/out/wav
  python3 tools/audio/import_unity.py   # -> Assets/SpookyStreet/Content/Resources/Audio/<id>/<id>_t<n>.<ext>

Formats (docs/S003-sfx-wiring.md): short effects keep their 48 kHz, 16-bit mono WAV masters (Unity encodes them to
Vorbis on import, so the repo holds the lossless source); the 24 s stereo night bed is stored as Ogg Vorbis q6
(about 190 kbps) to keep the repo lean. Encoding is bit-exact, so re-running gives identical files.
Voices and music are being replaced (D20.1) and are never imported here.
Run tools/unity_meta.py afterwards for the .meta files of new files.
"""
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from spookyaudio import sounds  # noqa: E402

REPO = HERE.parent.parent
WAV_DIR = HERE / "out" / "wav"
DEST = REPO / "Assets" / "SpookyStreet" / "Content" / "Resources" / "Audio"

# The effects the owner approved in the first listening review (D20.1). Never voice_* or music_*.
APPROVED = (
    "clue_knocks_distant", "clue_floor_hum", "clue_cam_tap", "clue_wind_wheel",
    "step_wood", "step_grass", "step_stone",
    "move_jump", "move_land", "move_glide_open", "move_glide_loop",
    "ui_page_turn", "note_sticker_pick", "note_sticker_drop", "note_sticker_wobble", "note_correct_glow",
    "ui_tap", "amb_night",
)

# Stored as Ogg Vorbis (long and stereo), with the libvorbis quality; everything else keeps its WAV master.
OGG = {"amb_night": "6"}


def extension(sound_id):
    return "ogg" if sound_id in OGG else "wav"


def encode_ogg(src, dst, quality):
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(src), "-map_metadata", "-1", "-fflags", "+bitexact",
                    "-flags:a", "+bitexact", "-c:a", "libvorbis", "-q:a", quality, str(dst)], check=True)


def main():
    by_id = {s.id: s for s in sounds.SOUNDS}
    missing = []
    for sound_id in APPROVED:
        sound = by_id[sound_id]
        folder = DEST / sound_id
        folder.mkdir(parents=True, exist_ok=True)
        for take in range(1, sound.takes + 1):
            src = WAV_DIR / f"{sound_id}_t{take}.wav"
            if not src.exists():
                missing.append(src.name)
                continue
            dst = folder / f"{sound_id}_t{take}.{extension(sound_id)}"
            if sound_id in OGG:
                encode_ogg(src, dst, OGG[sound_id])
            else:
                shutil.copyfile(src, dst)
            print(f"{dst.relative_to(REPO)}  {dst.stat().st_size / 1024:.0f} KiB")
    if missing:
        print("Missing masters (run tools/audio/build.py first): " + ", ".join(missing), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
