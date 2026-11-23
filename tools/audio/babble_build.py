#!/usr/bin/env python3
"""Turn the generated babble sources into game-ready cartoon babble (D20.1). No network: it only reads
tools/audio/generated/voice/.

Per character it writes (WAV masters in tools/audio/out/babble/, not committed):
  - phrase takes per emotion at 0.6–3.0 s (natural syllable runs, joined with the character's gaps),
  - a syllable bank per emotion: one 'sheet' WAV with the syllables back to back plus their offsets,
  - two sequenced examples made exactly like the runtime would (`babble.plan_sequence`) from real captions,
and tools/audio/generated/babble_bank.json (the runtime data: offsets, lengths, pitch and level per syllable).
`build_v2.py` calls `build()` and puts MP3s of a subset on the owner page.
"""
import json
import sys
from pathlib import Path

import numpy as np
from scipy import signal

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from spookyaudio import SAMPLE_RATE as SR  # noqa: E402
from spookyaudio import babble, checks, dsp, master  # noqa: E402
from spookyaudio.babble_spec import CAST, EMOTION_PITCH, EMOTIONS, TAKES_PER_EMOTION  # noqa: E402
from spookyaudio.genai import GENERATED  # noqa: E402

PHRASE_LENGTHS = (0.6, 0.9, 1.3, 1.8, 2.4, 3.0)
BANK_PER_EMOTION = 16

# Captions for the sequenced examples (real case 01 lines; Pip has none, so a typical reaction).
EXAMPLES = {
    "pip": [("neutral", "Hmm… a knock? Where did that come from?"), ("surprised", "Three knocks! The owl bowed!")],
    "bea": [("neutral", "I saw June's silver cape go in. Right there!"),
            ("happy", "Three knocks, three bows! I'm putting an owl on my bike.")],
    "vale": [("neutral", "That front door? I could see it from my bench. It stayed shut."),
             ("thinking", "It's open now, though. Well, well.")],
    "june": [("whisper", "I came down here to find the knocks. They're under the floor somewhere."),
             ("happy", "There! It just needed two hands.")],
    "kit": [("neutral", "First show of the night. Best seats are on the porch."),
            ("happy", "My old owl is bowing again. Thank you, both of you.")],
}


SHIFTS = {}


def pitch_shift_for(ch, emotion, src):
    """Semitones that move this take's median pitch to the character's target for the emotion. Whispered takes
    have little measurable pitch; they reuse the neutral shift."""
    f0 = babble.f0_track(src)
    voiced = f0[f0 > 0]
    if emotion == "whisper" or len(voiced) < 0.15 * len(f0):
        neutral = babble.load_audio(GENERATED / "voice" / ch.id / "neutral_t1.flac")
        return pitch_shift_for(ch, "neutral", neutral)
    target = ch.f0_hz * EMOTION_PITCH[emotion]
    return float(np.clip(12 * np.log2(target / np.median(voiced)), -ch.max_shift_st, ch.max_shift_st))


def process_character(ch):
    """Process every source take of one character. Returns {emotion: [(audio, syllables), ...]}."""
    out = {}
    for emotion in EMOTIONS:
        takes = []
        for take in range(1, TAKES_PER_EMOTION + 1):
            src = babble.load_audio(GENERATED / "voice" / ch.id / f"{emotion}_t{take}.flac")
            over = ch.emotion_overrides.get(emotion, {})
            shift = pitch_shift_for(ch, emotion, src)
            shift = over.get("pitch_st", shift)
            formant = (2 ** (shift / 12)) ** ch.formant_follow
            y = babble.transform(src, shift, over.get("speed", ch.speed), over.get("formant", formant))
            y = babble.trim(y)
            y = y / (np.max(np.abs(y)) + 1e-9) * 0.5
            takes.append((y, babble.syllables(y)))
            SHIFTS[f"{ch.id}/{emotion}_t{take}"] = round(float(shift), 2)
        out[emotion] = takes
    return out


def make_bank(takes, limit=BANK_PER_EMOTION):
    """Choose clean syllables (not too short or long, not weak) across takes, spread over the pitch range."""
    cands = []
    for ti, (y, sylls) in enumerate(takes):
        top = max(s["peak_db"] for s in sylls)
        for s in sylls:
            dur = (s["end"] - s["start"]) / SR
            if not 0.06 <= dur <= 0.45 or s["peak_db"] < top - 14:
                continue
            seg = babble.cut(y, s, 0.003, 0.015)
            cands.append({"audio": seg, "duration_s": round(dur, 3), "peak_db": round(s["peak_db"] - top, 1),
                          "f0_hz": round(babble.median_f0(seg), 1), "take": ti + 1})
    # spread over pitch: sort by f0 and take evenly spaced picks
    cands.sort(key=lambda c: c["f0_hz"])
    if len(cands) > limit:
        idx = np.round(np.linspace(0, len(cands) - 1, limit)).astype(int)
        cands = [cands[i] for i in idx]
    # level-match the syllables (within 4 dB of the bank median) so sequenced babble does not jump in loudness
    rms = np.array([np.sqrt(np.mean(c["audio"] ** 2)) for c in cands])
    ref = np.median(rms)
    for c, r in zip(cands, rms):
        c["audio"] = c["audio"] * float(np.clip(ref / r, 10 ** (-4 / 20), 10 ** (4 / 20)))
    return cands


def sheet(bank, gap=0.05):
    """Syllables back to back with short silences; offsets let the runtime slice them into clips at load."""
    pieces, offsets, pos = [], [], 0
    for b in bank:
        offsets.append((pos, len(b["audio"])))
        pieces += [b["audio"], np.zeros(int(gap * SR))]
        pos += len(b["audio"]) + int(gap * SR)
    return np.concatenate(pieces), offsets


def sequence(bank, plan, overlap=0.028):
    """Render a runtime plan: syllables joined with a short crossfade, pitch offsets like AudioSource.pitch."""
    pieces, gaps = [], []
    for idx, gap_after, pitch_st in plan:
        a = bank[idx]["audio"]
        if pitch_st:
            r = 2 ** (pitch_st / 12)
            up, down = babble._ratio(1 / r)
            a = signal.resample_poly(a, up, down)
        pieces.append(a)
        gaps.append(gap_after if gap_after > 0 else -overlap)
    return babble.join(pieces, gaps[:-1])


def finish(x):
    """Soften the top a little (sped-up, raised speech gets bright and sibilant) and master to the voice category."""
    x = dsp.peaking(x, 7500.0, -3.0, 0.7)
    return master.master(x, "voice")


def build(wav_dir, verbose=True):
    """Returns {char: {...report...}} and writes the WAV masters + babble_bank.json."""
    wav_dir = Path(wav_dir)
    report, bank_json = {}, {"sample_rate": SR, "format": "one sheet WAV per character and emotion "
                             "(Audio/voice_<char>/<emotion>_bank); offsets in samples", "characters": {}}
    for ch in CAST:
        d = wav_dir / ch.id
        d.mkdir(parents=True, exist_ok=True)
        processed = process_character(ch)
        entry = {"name": ch.name, "voice": ch.voice, "speed": ch.speed, "target_f0_hz": ch.f0_hz,
                 "formant_follow": ch.formant_follow, "phrases": {}, "examples": [], "bank": {}}
        cj = {"syllable_rate": ch.syllable_rate, "gap_s": list(ch.gap_s), "emotions": {}}
        all_f0 = []
        banks = {}
        for emotion in EMOTIONS:
            takes = processed[emotion]
            runs = [r for y, s in takes for r in babble.runs_of(y, s)]
            rng = np.random.default_rng(babble.seed_for(ch.id, emotion, "phrases"))
            phrases = []
            for i, length in enumerate(PHRASE_LENGTHS):
                p = babble.phrase_from_runs(runs, length, rng, ch.gap_s)
                m, info = finish(p)
                metrics, failures = checks.analyse(m, "voice", (0.3, 3.6))
                metrics.update(info)
                name = f"{emotion}_p{i + 1}"
                checks.write_wav(d / f"{name}.wav", m, babble.seed_for(ch.id, name, "dither"))
                f0 = babble.median_f0(p)
                all_f0.append(f0)
                phrases.append({"name": name, "target_s": length, "failures": failures, "f0_hz": round(f0, 1),
                                **metrics})
                if verbose:
                    print(f"{ch.id:5s} {name:14s} {metrics['duration_s']:5.2f} s (target {length:.1f}) "
                          f"{metrics['lufs']:6.1f} LUFS {metrics['true_peak_dbtp']:5.1f} dBTP f0 {f0:4.0f} "
                          f"{'ok' if not failures else failures}")
            entry["phrases"][emotion] = phrases
            bank = make_bank(takes)
            banks[emotion] = bank
            sh, offsets = sheet(bank)
            shm, _ = finish(sh)
            checks.write_wav(d / f"{emotion}_bank.wav", shm, babble.seed_for(ch.id, emotion, "bank"))
            cj["emotions"][emotion] = {"phrases": [{"clip": p["name"], "duration_s": p["duration_s"]} for p in phrases],
                                       "syllables": [{"offset": o, "length": n, "duration_s": b["duration_s"],
                                                      "f0_hz": b["f0_hz"], "peak_db": b["peak_db"]}
                                                     for (o, n), b in zip(offsets, bank)]}
            entry["bank"][emotion] = len(bank)
        for k, (emotion, caption) in enumerate(EXAMPLES[ch.id]):
            plan = babble.plan_sequence(caption, banks[emotion], ch.syllable_rate,
                                        babble.seed_for(ch.id, "example", k))
            x = sequence(banks[emotion], plan)
            m, info = finish(x)
            metrics, failures = checks.analyse(m, "voice", (0.3, 3.6))
            name = f"seq{k + 1}_{emotion}"
            checks.write_wav(d / f"{name}.wav", m, babble.seed_for(ch.id, name))
            entry["examples"].append({"name": name, "emotion": emotion, "caption": caption, "syllables": len(plan),
                                      "failures": failures, **metrics})
        voiced = [f for f in all_f0 if f > 0]
        entry["f0_median_hz"] = round(float(np.median(voiced)), 1)
        entry["f0_range_st"] = round(float(12 * np.log2(max(voiced) / min(voiced))), 1)
        entry["shifts_st"] = {k: v for k, v in SHIFTS.items() if k.startswith(ch.id + "/")}
        report[ch.id] = entry
        bank_json["characters"][ch.id] = cj
    (GENERATED / "babble_bank.json").write_text(json.dumps(bank_json, indent=1) + "\n")
    return report


if __name__ == "__main__":
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else HERE / "out" / "babble"
    rep = build(out)
    print(json.dumps({k: {kk: v[kk] for kk in ("f0_median_hz", "f0_range_st", "bank")} for k, v in rep.items()}))
