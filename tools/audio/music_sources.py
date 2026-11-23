#!/usr/bin/env python3
"""Generate the music sources with Lyria (D20.1). Only missing sources are requested. Each source is stored as the
service delivered it (MP3, 44.1 kHz stereo) in tools/audio/generated/music/<cue>/s<seed>.mp3 and recorded in
tools/audio/generated/manifest.json with the model, prompt, seed, the model's structure text and response id.

  python3 tools/audio/music_sources.py               # every missing source
  python3 tools/audio/music_sources.py --only music_reveal
"""
import argparse
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from spookyaudio import genai, manifest  # noqa: E402
from spookyaudio.music_spec import CUES  # noqa: E402

MUSIC_DIR = genai.GENERATED / "music"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", nargs="*")
    args = ap.parse_args()
    data = manifest.load()
    failures = 0
    for cue in CUES:
        if args.only and cue.id not in args.only:
            continue
        for seed in cue.seeds:
            key = f"{cue.id}/s{seed}"
            path = MUSIC_DIR / cue.id / f"s{seed}.mp3"
            if path.exists():
                continue
            body = {"contents": [{"parts": [{"text": cue.prompt}]}], "generationConfig": {"seed": seed}}
            try:
                r = genai.generate(cue.model, body, "music", f"music source {key}", timeout=600)
            except genai.ApiError as e:
                print(key, "FAILED", str(e)[:300])
                failures += 1
                if failures >= 3:
                    break
                continue
            parts, text = genai.inline_parts(r)
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(parts[0][1])
            data["music_sources"][key] = {"file": str(path.relative_to(genai.GENERATED)), "model": cue.model,
                                          "model_version": r.get("modelVersion"), "prompt": cue.prompt,
                                          "generation_config": body["generationConfig"], "mime": parts[0][0],
                                          "structure_text": text, "response_id": r.get("responseId")}
            manifest.save(data)
            print(key, r.get("modelVersion"), len(parts[0][1]), text.replace("\n", " ")[:80])
    manifest.save(data)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
