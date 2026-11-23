"""tools/audio/generated/manifest.json: what was generated, with which model, prompt, voice, seed and parameters, plus
the post-processing that turned it into game audio. Reproducibility record for D20.1 (the services are not
guaranteed to be deterministic, so the source outputs themselves are kept next to this file)."""
import json

from .genai import CALL_LOG, GENERATED

PATH = GENERATED / "manifest.json"


def load():
    if PATH.exists():
        return json.loads(PATH.read_text())
    return {"about": __doc__.strip().replace("\n", " "), "voice_sources": {}, "music_sources": {}}


def save(data):
    rows = [json.loads(line) for line in CALL_LOG.read_text().splitlines() if line.strip()] if CALL_LOG.exists() else []
    counts = {}
    for r in rows:
        key = f'{r["kind"]}:{r["model"]}'
        counts[key] = counts.get(key, 0) + 1
    data["api_calls"] = {"total": len(rows), "by_kind_and_model": dict(sorted(counts.items())),
                         "log": "calls.jsonl (one line per HTTP request, including probes, auditions and errors)"}
    PATH.write_text(json.dumps(data, indent=1, ensure_ascii=False, sort_keys=False) + "\n")
