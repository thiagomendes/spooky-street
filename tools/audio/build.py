#!/usr/bin/env python3
"""Render, master and check the Spooky Street sound set, and build the owner listening page.

Usage:
  python3 tools/audio/build.py                      # everything -> docs/evidence/s003/listening
  python3 tools/audio/build.py --only ui_tap step_wood
  python3 tools/audio/build.py --wav-dir /tmp/wav   # also keep 48 kHz WAV masters there

Outputs (in --out): audio/<id>_t<take>.mp3 (or .wav without ffmpeg), audio/<id>_demo.mp3 for in-context previews,
spectrograms/<id>.png (take 1), report.json (every check and loudness number) and index.html.
WAV masters (48 kHz, 16-bit, seeded TPDF dither) go to --wav-dir (default tools/audio/out/wav, not committed).
Exit status 1 when any check fails.
"""
import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from spookyaudio import SAMPLE_RATE, checks, dsp, master, page, sounds  # noqa: E402

REPO = HERE.parent.parent
DEFAULT_OUT = REPO / "docs" / "evidence" / "s003" / "listening"
DEFAULT_WAV = HERE / "out" / "wav"


def render_take(sound, take):
    """Synthesise and master one take. Returns (audio, mastering info)."""
    rng = dsp.rng_for(sound.id, take)
    raw = sound.render(rng, take)
    return master.master(raw, sound.category, loop=sound.loop, lufs_offset=sound.lufs_offset)


def encode(wav_path, out_path):
    """WAV -> MP3 (VBR ~190 kbps, bit-exact headers so rebuilds give identical files)."""
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(wav_path), "-map_metadata", "-1", "-fflags", "+bitexact",
                    "-flags:a", "+bitexact", "-c:a", "libmp3lame", "-q:a", "2", str(out_path)], check=True)


def build(ids=None, out_dir=DEFAULT_OUT, wav_dir=DEFAULT_WAV, encode_mp3=True):
    out_dir = Path(out_dir)
    wav_dir = Path(wav_dir)
    (out_dir / "audio").mkdir(parents=True, exist_ok=True)
    (out_dir / "spectrograms").mkdir(parents=True, exist_ok=True)
    wav_dir.mkdir(parents=True, exist_ok=True)
    use_mp3 = encode_mp3 and shutil.which("ffmpeg") is not None
    ext = "mp3" if use_mp3 else "wav"

    report_path = out_dir / "report.json"
    report = {"sample_rate": SAMPLE_RATE, "format": ext, "sounds": {}}
    if ids and report_path.exists():
        report = json.loads(report_path.read_text())
        report["format"] = ext
    failed = False
    for sound in sounds.SOUNDS:
        if ids and sound.id not in ids:
            continue
        entry = {"category": sound.category, "title": sound.title, "description": sound.description,
                 "purpose": sound.purpose, "listen_for": sound.listen_for, "group": sound.group, "loop": sound.loop,
                 "markers_s": list(sound.markers), "budget_s": list(sound.budget),
                 "target_lufs": master.CATEGORIES[sound.category].lufs + sound.lufs_offset, "takes": []}
        takes_audio = []
        for take in range(1, sound.takes + 1):
            audio, info = render_take(sound, take)
            metrics, failures = checks.analyse(audio, sound.category, sound.budget, loop=sound.loop,
                                               target_lufs=info["target_lufs"])
            metrics["limiter_db"] = info["limiter_db"]
            stem = f"{sound.id}_t{take}"
            wav_path = wav_dir / f"{stem}.wav"
            checks.write_wav(wav_path, audio, dsp.seed_for(sound.id, take, "dither"))
            if use_mp3:
                encode(wav_path, out_dir / "audio" / f"{stem}.mp3")
            else:
                shutil.copyfile(wav_path, out_dir / "audio" / f"{stem}.wav")
            if take == 1:
                checks.spectrogram_png(out_dir / "spectrograms" / f"{sound.id}.png", audio)
            entry["takes"].append({"file": f"audio/{stem}.{ext}", "failures": failures, **metrics})
            takes_audio.append(audio)
            failed |= bool(failures)
            status = "ok" if not failures else "FAIL: " + "; ".join(failures)
            print(f"{stem:28s} {metrics.get('duration_s', 0):6.2f} s {metrics.get('lufs', 0):7.1f} LUFS "
                  f"{metrics.get('true_peak_dbtp', 0):6.1f} dBTP  {status}")
        if sound.demo:
            demo = sound.demo(takes_audio)
            demo_wav = wav_dir / f"{sound.id}_demo.wav"
            checks.write_wav(demo_wav, demo, dsp.seed_for(sound.id, "demo"))
            if use_mp3:
                encode(demo_wav, out_dir / "audio" / f"{sound.id}_demo.mp3")
            else:
                shutil.copyfile(demo_wav, out_dir / "audio" / f"{sound.id}_demo.wav")
            entry["demo"] = f"audio/{sound.id}_demo.{ext}"
        entry["spectrogram"] = f"spectrograms/{sound.id}.png"
        report["sounds"][sound.id] = entry

    order = [s.id for s in sounds.SOUNDS]
    report["sounds"] = {k: report["sounds"][k] for k in order if k in report["sounds"]}
    report_path.write_text(json.dumps(report, indent=1) + "\n")
    (out_dir / "index.html").write_text(page.render(report))
    return report, failed


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--only", nargs="*", help="sound ids to rebuild (others stay as in report.json)")
    parser.add_argument("--out", default=str(DEFAULT_OUT))
    parser.add_argument("--wav-dir", default=str(DEFAULT_WAV))
    parser.add_argument("--no-mp3", action="store_true", help="copy WAVs to the page instead of MP3s")
    args = parser.parse_args()
    _, failed = build(set(args.only) if args.only else None, args.out, args.wav_dir, not args.no_mp3)
    print("All checks passed." if not failed else "Some checks FAILED (see above and report.json).")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
