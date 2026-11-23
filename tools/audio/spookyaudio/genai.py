"""Minimal client for the Google Generative Language API (Gemini TTS and Lyria music), used only to make *source*
material that tools/audio then cuts and masters (D20.1).

No API key appears anywhere: in the cloud sessions that generated the committed sources, the network proxy adds the
owner's key to requests to generativelanguage.googleapis.com. Every call is appended to
tools/audio/generated/calls.jsonl (time, model, purpose, status; never headers or keys) so the free-tier budget can
be audited, and every successful response is cached on disk so a rebuild never calls the service again.
"""
import base64
import json
import time
from pathlib import Path

API = "https://generativelanguage.googleapis.com/v1beta"
GENERATED = Path(__file__).resolve().parent.parent / "generated"
CALL_LOG = GENERATED / "calls.jsonl"


class ApiError(RuntimeError):
    pass


def _log(entry):
    GENERATED.mkdir(parents=True, exist_ok=True)
    with CALL_LOG.open("a") as f:
        f.write(json.dumps(entry, sort_keys=True) + "\n")


def calls_made(kind=None):
    if not CALL_LOG.exists():
        return 0
    rows = [json.loads(line) for line in CALL_LOG.read_text().splitlines() if line.strip()]
    return sum(1 for r in rows if kind is None or r.get("kind") == kind)


def generate(model, body, kind, purpose, timeout=300, max_retries=2):
    """POST :generateContent once (plus at most `max_retries` back-offs on 429/503). Returns the JSON response."""
    import requests  # only needed when actually generating

    url = f"{API}/models/{model}:generateContent"
    for attempt in range(max_retries + 1):
        t0 = time.time()
        r = requests.post(url, json=body, timeout=timeout)
        _log({"t": round(t0), "kind": kind, "model": model, "purpose": purpose, "status": r.status_code,
              "secs": round(time.time() - t0, 1)})
        if r.status_code in (429, 503) and attempt < max_retries:
            time.sleep(30 * (attempt + 1))
            continue
        if r.status_code != 200:
            raise ApiError(f"{model} {r.status_code}: {r.text[:800]}")
        return r.json()
    raise ApiError("unreachable")


LISTENER = "gemini-3.8-flash"


def listen(audio_bytes, mime, question, purpose, model=LISTENER):
    """Ask an audio-understanding model about a clip: a machine listening proxy (Claude cannot hear). Its answer is
    advisory only; the owner's ears decide (D20.1)."""
    body = {"contents": [{"parts": [{"inlineData": {"mimeType": mime, "data": base64.b64encode(audio_bytes).decode()}},
                                    {"text": question}]}],
            "generationConfig": {"temperature": 0.2}}
    r = generate(model, body, "listen", purpose, timeout=180)
    return inline_parts(r)[1].strip()


def to_wav_bytes(mime, data):
    """Normalise a TTS part to WAV bytes: newer models send audio/wav, older ones raw audio/L16 PCM."""
    if mime and "wav" in mime:
        return data
    import io
    import re
    import wave

    rate = int(re.search(r"rate=(\d+)", mime or "").group(1)) if mime and "rate=" in mime else 24000
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(data)
    return buf.getvalue()


def inline_parts(response):
    """([(mime_type, bytes)], joined text) for the inline data and text parts of a response."""
    out, texts = [], []
    for cand in response.get("candidates", []):
        for part in cand.get("content", {}).get("parts", []):
            data = part.get("inlineData") or part.get("inline_data")
            if data:
                out.append((data.get("mimeType") or data.get("mime_type"), base64.b64decode(data["data"])))
            elif "text" in part:
                texts.append(part["text"])
    return out, "\n".join(texts)
