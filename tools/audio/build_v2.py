#!/usr/bin/env python3
"""Build the owner page "Vozes e música v2" (D20.1): process the generated babble and music sources (no network),
master them, run the checks and write docs/evidence/s003/listening_v2/ (index.html, report.json, audio/*.mp3,
spectrograms/*.png). WAV masters stay in tools/audio/out/ (not committed).

  python3 tools/audio/build_v2.py [--copy-to DIR]
"""
import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import babble_build  # noqa: E402
import music_build  # noqa: E402
from spookyaudio import checks, manifest, page_v2  # noqa: E402
from spookyaudio.babble_spec import CAST, EMOTIONS, TTS_MODEL  # noqa: E402

REPO = HERE.parent.parent
OUT = REPO / "docs" / "evidence" / "s003" / "listening_v2"
WAV = HERE / "out"

# Which phrase takes the page shows (all are built): short, middle, long.
PAGE_PHRASES = ("neutral_p2", "neutral_p4", "neutral_p6")

# Options shown per cue, best first, with a one-line note (from listening proxies: an audio-understanding model's
# review, the measured key/tempo and the seam checks; see docs/S003.1-voices-music.md).
PAGE_MUSIC = {
    "music_street_theme": [
        (101, "Valsa de livro de histórias: pizzicato, celesta e madeiras, a mais 'feltro e papel'. Combina com a "
              "Exploração opção 1."),
        (303, "Mais 'detetive na ponta dos pés', em fá/ré menor; um pouco mais desenho animado clássico."),
        (202, "A mais doce e nostálgica; tem um acordeão (não pedido) que puxa para feira de vila."),
    ],
    "music_exploration": [
        (111, "Variação leve da opção 1 do tema (mesma família, tonalidade vizinha); deixa espaço para os efeitos."),
        (333, "Investigação discreta, combina com a opção 2 do tema; um pouco mais seca."),
        (222, "Calma e espaçada, mas com menos identidade própria."),
    ],
    "music_case_closed": [
        (121, "Charmosa e cintilante, termina num acorde que soa até o fim."),
        (242, "Alegre, de flauta e clarinete, bem 'mistério resolvido' leve."),
        (484, "A mais grandiosa (orquestra cheia): talvez para o fim da temporada."),
    ],
    "music_reveal": [
        (131, "Celesta e harpa abrindo em cordas e sopros: maravilhamento de livro de histórias."),
        (524, "Começa curiosa e abre num ré maior caloroso."),
        (262, "Mais curta e doce (cerca de 12 s), de caixinha de música."),
    ],
}


def encode(wav, mp3, quality):
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(wav), "-map_metadata", "-1", "-fflags", "+bitexact",
                    "-flags:a", "+bitexact", "-c:a", "libmp3lame", "-q:a", str(quality), str(mp3)], check=True)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--copy-to", help="also copy the finished page folder here")
    args = ap.parse_args()
    if OUT.exists():
        shutil.rmtree(OUT)
    (OUT / "audio").mkdir(parents=True)
    (OUT / "spectrograms").mkdir(parents=True)
    failed = []

    voice_rep = babble_build.build(WAV / "babble", verbose=False)
    voices = {}
    for ch in CAST:
        v = voice_rep[ch.id]
        d = WAV / "babble" / ch.id
        files = {"phrases": [], "emotions": [], "sequenced": []}
        lufs, tps, fails = [], [], []
        by_name = {p["name"]: p for e in EMOTIONS for p in v["phrases"][e]}
        for name in PAGE_PHRASES:
            p = by_name[name]
            encode(d / f"{name}.wav", OUT / "audio" / f"voice_{ch.id}_{name}.mp3", 4)
            files["phrases"].append({"file": f"audio/voice_{ch.id}_{name}.mp3", "duration_s": p["duration_s"]})
        for e in EMOTIONS:
            name = f"{e}_p4"
            encode(d / f"{name}.wav", OUT / "audio" / f"voice_{ch.id}_{name}.mp3", 4)
            files["emotions"].append({"emotion": e, "file": f"audio/voice_{ch.id}_{name}.mp3"})
        for s in v["examples"]:
            encode(d / f"{s['name']}.wav", OUT / "audio" / f"voice_{ch.id}_{s['name']}.mp3", 4)
            files["sequenced"].append({"file": f"audio/voice_{ch.id}_{s['name']}.mp3", "caption": s["caption"]})
        for p in list(by_name.values()) + v["examples"]:
            lufs.append(p["lufs"])
            tps.append(p["true_peak_dbtp"])
            fails += p["failures"]
        _, x = checks.read_wav(d / "neutral_p4.wav")
        checks.spectrogram_png(OUT / "spectrograms" / f"voice_{ch.id}.png", x)
        voices[ch.id] = {"name": ch.name, "voice": ch.voice, "files": files, "spectrogram":
                         f"spectrograms/voice_{ch.id}.png",
                         "meta": {"f0_median_hz": v["f0_median_hz"], "speed": ch.speed, "lufs_min": min(lufs),
                                  "lufs_max": max(lufs), "tp_max": max(tps), "failures": sorted(set(fails))},
                         "build": {k: v[k] for k in ("target_f0_hz", "formant_follow", "shifts_st", "bank",
                                                     "f0_range_st")},
                         "takes_built": {e: [{kk: p[kk] for kk in ("name", "duration_s", "lufs", "true_peak_dbtp",
                                                                   "f0_hz", "failures")} for p in v["phrases"][e]]
                                         for e in EMOTIONS},
                         "sequenced": v["examples"]}
        failed += [f"{ch.id}: {f}" for f in fails]

    music_rep = music_build.build(WAV / "music", verbose=False)
    music = []
    for cue_id, picks in PAGE_MUSIC.items():
        cue = music_rep[cue_id]
        by_seed = {o["seed"]: o for o in cue["options"]}
        opts = []
        for i, (seed, note) in enumerate(picks):
            o = dict(by_seed[seed])
            name = o["name"]
            encode(WAV / "music" / f"{name}.wav", OUT / "audio" / f"{name}.mp3", 5)
            o["file"] = f"audio/{name}.mp3"
            o["note_pt"] = note
            if cue["kind"] == "loop":
                encode(WAV / "music" / f"{name}_seam.wav", OUT / "audio" / f"{name}_seam.mp3", 5)
                o["seam_file"] = f"audio/{name}_seam.mp3"
            if i == 0:
                _, x = checks.read_wav(WAV / "music" / f"{name}.wav")
                checks.spectrogram_png(OUT / "spectrograms" / f"{cue_id}.png", x[: 30 * 48000])
            o["spectrogram"] = f"spectrograms/{cue_id}.png"
            failed += [f"{name}: {f}" for f in o["failures"]]
            opts.append(o)
        music.append({"id": cue_id, **{k: cue[k] for k in ("title", "title_pt", "kind", "model", "listen_pt",
                                                           "purpose_pt")},
                      "options": opts, "all_options": cue["options"]})

    data = manifest.load()
    manifest.save(data)
    data = manifest.load()
    report = {"tts_model": TTS_MODEL, "music_models": sorted({c["model"] for c in music}),
              "api_calls": data["api_calls"], "voices": voices, "music": music, "failures": failed}
    (OUT / "report.json").write_text(json.dumps(report, indent=1, ensure_ascii=False) + "\n")
    (OUT / "index.html").write_text(page_v2.render(report))
    size = sum(p.stat().st_size for p in OUT.rglob("*") if p.is_file())
    print(f"page: {OUT / 'index.html'} ({size / 1e6:.1f} MB), failures: {failed or 'none'}")
    if args.copy_to:
        dest = Path(args.copy_to)
        if dest.exists():
            shutil.rmtree(dest)
        shutil.copytree(OUT, dest)
        print("copied to", dest)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
