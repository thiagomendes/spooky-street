#!/usr/bin/env python3
"""Machine listening review of the processed babble (proxy only; the owner's ears decide).

For each character it sends the 1.8 s phrase of every emotion plus the two sequenced examples (from
tools/audio/out/babble/<char>/) to an audio-understanding model and asks for artefacts, words, stock-voice
recognisability, emotion and charm. Results: tools/audio/generated/review/babble.json.
"""
import argparse
import base64
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from spookyaudio import genai  # noqa: E402
from spookyaudio.babble_spec import CAST, EMOTIONS  # noqa: E402

REVIEW = genai.GENERATED / "review" / "babble.json"

QUESTION = """You are the audio director of a premium children's game (ages 6-9, cosy, spooky-not-scary). These clips are
the character '{name}' ({who}) speaking 'cartoon babble': fast, high chatter that forms no words, carrying only
rhythm and emotion (the words appear as text on screen). Clip names give the intended emotion; 'seq' clips are
assembled syllable by syllable at runtime. Be strict and specific. For EACH clip return
{{"clip": name, "emotion_heard": short, "matches": bool, "artefacts": "clicks / choppy joins / phasey / metallic /
warble / robotic, with times, or none", "chipmunk": 1-10 (10 = pure chipmunk), "sounds_like_stock_ai_voice": bool,
"real_words": [..], "charm": 1-10}}.
Then one object {{"clip": "overall", "fits_character": 1-10, "distinct_voice": short, "top_fix": short}}.
Return a JSON array only."""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--dir", default=str(HERE / "out" / "babble"))
    ap.add_argument("--tag", default="")
    args = ap.parse_args()
    data = json.loads(REVIEW.read_text()) if REVIEW.exists() else {}
    for ch in CAST:
        if args.only and ch.id not in args.only:
            continue
        d = Path(args.dir) / ch.id
        names = [f"{e}_p4" for e in EMOTIONS] + sorted(p.stem for p in d.glob("seq*.wav"))
        parts = []
        for n in names:
            parts.append({"text": f"Clip '{n}':"})
            parts.append({"inlineData": {"mimeType": "audio/wav",
                                         "data": base64.b64encode((d / f"{n}.wav").read_bytes()).decode()}})
        parts.append({"text": QUESTION.format(name=ch.name, who=ch.direction)})
        body = {"contents": [{"parts": parts}],
                "generationConfig": {"temperature": 0.1, "responseMimeType": "application/json"}}
        r = genai.generate(genai.LISTENER, body, "listen", f"review babble {ch.id}")
        text = genai.inline_parts(r)[1]
        try:
            rows = json.loads(text)
        except json.JSONDecodeError:
            rows = [{"raw": text}]
        data[ch.id + args.tag] = rows
        REVIEW.parent.mkdir(parents=True, exist_ok=True)
        REVIEW.write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n")
        for row in rows:
            print(ch.id, json.dumps(row, ensure_ascii=False))


if __name__ == "__main__":
    main()
