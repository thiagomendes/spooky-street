#!/usr/bin/env python3
"""Generate the babble source speech (D20.1) with Gemini TTS. Only missing sources are requested, so re-running is
free; pass --force-take to replace a specific source. Sources go to tools/audio/generated/voice/<char>/ as FLAC
(24 kHz mono, as delivered) and are recorded in tools/audio/generated/manifest.json.

  python3 tools/audio/babble_sources.py                 # every missing source
  python3 tools/audio/babble_sources.py --only pip      # one character
  python3 tools/audio/babble_sources.py --force-take kit/happy_t2
"""
import argparse
import subprocess
import sys
import tempfile
import zlib
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from spookyaudio import genai, manifest  # noqa: E402
from spookyaudio.babble_spec import AVOID, CAST, EMOTION_DIRECTION, EMOTIONS, TAKES_PER_EMOTION, TTS_MODEL  # noqa: E402

VOICE_DIR = genai.GENERATED / "voice"


def nonsense_line(ch, emotion, take, revision=0):
    """A made-up syllable string: 5-7 'words' of 2-3 syllables, punctuated for the emotion. Seeded."""
    rng = np.random.default_rng(zlib.crc32(f"{ch.id}|{emotion}|{take}|{revision}".encode()))
    words = []
    while len(words) < int(rng.integers(5, 8)):
        n = int(rng.choice([2, 2, 3]))
        sylls = []
        for _ in range(n):
            syl = str(rng.choice(ch.onsets)) + str(rng.choice(ch.vowels))
            if rng.random() < 0.2:
                syl += str(rng.choice(ch.codas))
            sylls.append(syl)
        word = "-".join(sylls)
        if any(a == b for a, b in zip(sylls, sylls[1:])):
            continue
        if word.replace("-", "") in AVOID or any(s in AVOID and len(s) > 2 for s in sylls) or word in words:
            continue
        words.append(word)
    marks = {"neutral": [",", ",", "."], "happy": ["!", ",", "!"], "surprised": ["!", "?", "!"],
             "thinking": ["...", "?", ","], "whisper": [",", "...", "."]}[emotion]
    out = []
    for i, w in enumerate(words):
        if i and rng.random() < 0.45:
            out[-1] += str(rng.choice(marks))
        out.append(w)
    end = {"neutral": ".", "happy": "!", "surprised": "?!", "thinking": "?", "whisper": "..."}[emotion]
    text = " ".join(out) + end
    return text[0].upper() + text[1:]


def prompt_for(ch, emotion, line):
    return (f"Speaker direction (do not read aloud): {ch.direction}. {EMOTION_DIRECTION[emotion]} "
            "The transcript is made-up cartoon gibberish: perform it with full emotion and natural, lively rhythm, "
            "never as real words.\nTranscript: " + line)


def to_flac(wav_bytes, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(suffix=".wav") as tmp:
        tmp.write(wav_bytes)
        tmp.flush()
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", tmp.name, "-map_metadata", "-1", "-fflags", "+bitexact",
                        "-c:a", "flac", "-compression_level", "8", str(path)], check=True)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--force-take", nargs="*", default=[], help="<char>/<emotion>_t<n> to regenerate")
    ap.add_argument("--revision", type=int, default=None, help="new syllable string revision for forced takes")
    args = ap.parse_args()
    data = manifest.load()
    failures = 0
    for ch in CAST:
        if args.only and ch.id not in args.only:
            continue
        for emotion in EMOTIONS:
            for take in range(1, TAKES_PER_EMOTION + 1):
                key = f"{ch.id}/{emotion}_t{take}"
                path = VOICE_DIR / ch.id / f"{emotion}_t{take}.flac"
                if path.exists() and key not in args.force_take:
                    continue
                revision = data["voice_sources"].get(key, {}).get("revision", -1) + 1 if key in args.force_take \
                    else 0
                if args.revision is not None and key in args.force_take:
                    revision = args.revision
                line = nonsense_line(ch, emotion, take, revision)
                prompt = prompt_for(ch, emotion, line)
                body = {"contents": [{"parts": [{"text": prompt}]}],
                        "generationConfig": {"responseModalities": ["AUDIO"],
                                             "seed": zlib.crc32(key.encode()) & 0x7FFFFFFF, "speechConfig": {
                            "voiceConfig": {"prebuiltVoiceConfig": {"voiceName": ch.voice}}}}}
                try:
                    r = genai.generate(TTS_MODEL, body, "tts", f"babble source {key}")
                except genai.ApiError as e:
                    print(key, "FAILED", e)
                    failures += 1
                    if failures >= 3:
                        print("Stopping after 3 failures.")
                        manifest.save(data)
                        return 1
                    continue
                parts, _ = genai.inline_parts(r)
                to_flac(genai.to_wav_bytes(*parts[0]), path)
                data["voice_sources"][key] = {
                    "file": str(path.relative_to(genai.GENERATED)), "model": r.get("modelVersion", TTS_MODEL),
                    "voice": ch.voice, "emotion": emotion, "transcript": line, "prompt": prompt,
                    "revision": revision, "generation_config": body["generationConfig"],
                    "response_id": r.get("responseId"),
                    "seed_note": "generationConfig.seed is sent; the service does not promise identical audio"}
                print(key, ch.voice, line)
                manifest.save(data)
    manifest.save(data)
    return 0


if __name__ == "__main__":
    sys.exit(main())
