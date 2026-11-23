#!/usr/bin/env python3
"""Edit the generated music sources into game cues (D20.1). No network: it reads tools/audio/generated/music/.

  loops (street theme, exploration): beat-tracked, bar-to-bar loop of 55-95 s, phase-aligned 60 ms seam
  stings and reveals: the natural ending of a clip, from a strong beat (sting) or out of a hush (reveal)
Every cue is mastered to the music category (-20 LUFS, -1 dBTP) as 48 kHz stereo WAV in tools/audio/out/music/
(not committed). `build_v2.py` calls `build()` and puts MP3s on the owner page; the edit points are written to
tools/audio/generated/music_edits.json so they can be reproduced exactly.
"""
import json
import sys
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from spookyaudio import SAMPLE_RATE as SR  # noqa: E402
from spookyaudio import babble, checks, master, music  # noqa: E402
from spookyaudio.genai import GENERATED  # noqa: E402
from spookyaudio.music_spec import CUES  # noqa: E402


def seam_preview(loop, seconds=5.0):
    """The end of the loop straight into its start, as the game will play it."""
    n = int(seconds * SR)
    return np.concatenate([loop[-n:], loop[:n]], axis=0)


def build(wav_dir, verbose=True, only=None):
    wav_dir = Path(wav_dir)
    wav_dir.mkdir(parents=True, exist_ok=True)
    report, edits = {}, {}
    for cue in CUES:
        if only and cue.id not in only:
            continue
        options = []
        for seed in cue.seeds:
            src_path = GENERATED / "music" / cue.id / f"s{seed}.mp3"
            if not src_path.exists():
                continue
            x = babble.load_stereo(src_path)
            beat_s, bpm = music.beats(x)
            name = f"{cue.id}_s{seed}"
            info = {"name": name, "seed": seed, "source_s": round(len(x) / SR, 2), "bpm": round(float(bpm), 1)}
            if cue.kind == "loop":
                found = music.find_loop(x, beat_s, cue.length_s)
                body, end = music.make_loop(x, found["start"], found["end"])
                y, minfo = master.master(body, "music", loop=True)
                tonic, mode, conf = music.estimate_key(body)
                info["key"] = f"{music.PITCH_NAMES[tonic]} {mode}"
                info.update({"loop_start_s": round(found["start"] / SR, 3), "loop_end_s": round(end / SR, 3),
                             "bars": found["bars"], "beats_per_bar": found["beats_per_bar"],
                             "seam_similarity": found["score"]})
                metrics, failures = checks.analyse(y, "music", cue.length_s, loop=True)
                checks.write_wav(wav_dir / f"{name}_seam.wav", seam_preview(y), babble.seed_for(name, "seam"))
            else:
                prefer = "onset" if cue.kind == "sting" else "hush"
                body, start, end = music.cut_ending(x, beat_s, cue.length_s, prefer=prefer)
                # one tonal home for the score: move the ending's key to D (or A) when it is close
                tonic, mode, conf = music.estimate_key(body[-int(2.5 * SR):])
                body, shift = music.transpose_to(body, tonic)
                y, minfo = master.master(body, "music")
                info.update({"cut_start_s": round(start / SR, 3), "cut_end_s": round(end / SR, 3),
                             "ending_key": f"{music.PITCH_NAMES[tonic]} {mode}", "transposed_st": shift,
                             "final_key": f"{music.PITCH_NAMES[(tonic + shift) % 12]} {mode}"})
                metrics, failures = checks.analyse(y, "music", cue.length_s)
            metrics["limiter_db"] = minfo["limiter_db"]
            checks.write_wav(wav_dir / f"{name}.wav", y, babble.seed_for(name, "dither"))
            info.update(metrics)
            info["failures"] = failures
            options.append(info)
            edits[name] = {k: info[k] for k in info if k.endswith("_s") or k in ("bpm", "bars", "beats_per_bar", "seed", "transposed_st")}
            if verbose:
                print(f"{name:28s} {metrics['duration_s']:6.2f} s {metrics['lufs']:6.1f} LUFS "
                      f"{metrics['true_peak_dbtp']:5.1f} dBTP bpm {bpm:5.1f} "
                      f"{'seam ' + str(info.get('seam_similarity')) if cue.kind == 'loop' else ''} "
                      f"{'ok' if not failures else failures}")
        report[cue.id] = {"title": cue.title, "title_pt": cue.title_pt, "kind": cue.kind, "model": cue.model,
                          "listen_pt": cue.listen_pt, "purpose_pt": cue.purpose_pt, "options": options}
    path = GENERATED / "music_edits.json"
    old = json.loads(path.read_text()) if path.exists() and only else {}
    old.update(edits)
    path.write_text(json.dumps(old, indent=1) + "\n")
    return report


if __name__ == "__main__":
    build(HERE / "out" / "music", only=set(sys.argv[1:]) or None)
