#!/usr/bin/env python3
"""Machine listening review of the babble sources (a proxy only: Claude cannot hear, the owner decides).

Sends each character's source takes to an audio-understanding Gemini model in one request and asks, per clip,
whether a direction was read aloud, whether real words were formed, which emotion is heard and how natural it is.
Results go to tools/audio/generated/review/voice_sources.json. Re-run with --only to refresh a character.
"""
import argparse
import base64
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from spookyaudio import genai  # noqa: E402
from spookyaudio.babble_spec import CAST, EMOTIONS, TAKES_PER_EMOTION  # noqa: E402

REVIEW = genai.GENERATED / "review" / "voice_sources.json"

QUESTION = """These clips are source recordings for a children's game character ({who}). Each should be made-up
gibberish syllables (never real words) performed with the emotion in its file name. For EACH clip, return a JSON object:
{{"clip": name, "direction_read_aloud": bool, "real_words": [list of real English or Portuguese words heard, if any],
"emotion_heard": short, "matches_emotion": bool, "naturalness": 1-10, "problems": short or ""}}.
Answer with a JSON array only."""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", nargs="*")
    args = ap.parse_args()
    data = json.loads(REVIEW.read_text()) if REVIEW.exists() else {}
    for ch in CAST:
        if args.only and ch.id not in args.only:
            continue
        parts = []
        for emotion in EMOTIONS:
            for take in range(1, TAKES_PER_EMOTION + 1):
                name = f"{emotion}_t{take}"
                path = genai.GENERATED / "voice" / ch.id / f"{name}.flac"
                parts.append({"text": f"Clip '{name}':"})
                parts.append({"inlineData": {"mimeType": "audio/flac",
                                             "data": base64.b64encode(path.read_bytes()).decode()}})
        parts.append({"text": QUESTION.format(who=ch.direction)})
        body = {"contents": [{"parts": parts}],
                "generationConfig": {"temperature": 0.1, "responseMimeType": "application/json"}}
        r = genai.generate(genai.LISTENER, body, "listen", f"review sources {ch.id}")
        text = genai.inline_parts(r)[1]
        try:
            data[ch.id] = json.loads(text)
        except json.JSONDecodeError:
            data[ch.id] = {"raw": text}
        REVIEW.parent.mkdir(parents=True, exist_ok=True)
        REVIEW.write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n")
        for row in data[ch.id] if isinstance(data[ch.id], list) else []:
            flag = "!!" if row.get("direction_read_aloud") or row.get("real_words") or not row.get("matches_emotion") \
                else "  "
            print(flag, ch.id, row.get("clip"), row.get("naturalness"), row.get("emotion_heard"), row.get("real_words"),
                  row.get("problems"))


if __name__ == "__main__":
    main()
