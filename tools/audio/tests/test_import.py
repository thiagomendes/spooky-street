"""The approved sounds in the Unity project match the pipeline and the C# import plan: the effects (S003, D20.1) and
the v2 voices and music (D20.2), with the music loops' exact loop region and seam surviving the Ogg encode."""
import re
import shutil
import subprocess
import unittest
import wave

import numpy as np

from tests._path import REPO  # puts tools/audio on sys.path

import import_unity  # noqa: E402
from spookyaudio import SAMPLE_RATE, babble, sounds  # noqa: E402
from spookyaudio.babble_spec import CAST, EMOTIONS  # noqa: E402

CORE = REPO / "Assets" / "SpookyStreet" / "Core" / "Audio"
PLAN = CORE / "AudioImportPlan.cs"
EVENTS = CORE / "AudioEvents.cs"
VOICE_MUSIC = CORE / "VoiceMusicEvents.cs"
PLANNER_TESTS = REPO / "Assets" / "SpookyStreet" / "Tests" / "EditMode" / "Core" / "BabblePlannerTests.cs"


def vorbis_format(path):
    """(channels, sample rate) from the Vorbis identification header of an Ogg file."""
    data = path.read_bytes()[:256]
    i = data.index(b"\x01vorbis")
    return data[i + 11], int.from_bytes(data[i + 12:i + 16], "little")


def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-ac", "2", "-"],
                         check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2)


class ImportedAudioTests(unittest.TestCase):
    def test_approved_lists_match_the_csharp_plan(self):
        consts = dict(re.findall(r'public const string (\w+) = "([a-z0-9_]+)";', EVENTS.read_text()))
        block = re.search(r"Approved =\s*\{(.*?)\};", PLAN.read_text(), re.S).group(1)
        cs = [consts[name] for name in re.findall(r"AudioEvents\.(\w+)", block)]
        self.assertEqual(cs, list(import_unity.APPROVED))
        for sound_id in import_unity.APPROVED:
            self.assertFalse(sound_id.startswith(("voice_", "music_")), sound_id)
        # The v2 sets: the C# catalog builds voice_<char>_<emotion> from the same cast and emotions.
        vm = VOICE_MUSIC.read_text()
        cs_music = dict(re.findall(r'public const string (\w+) = "(music_[a-z_]+)";', vm))
        self.assertEqual(sorted(cs_music.values()), sorted(import_unity.MUSIC))
        cs_emotions = re.search(r"EmotionIds =\s*\{(.*?)\};", vm, re.S).group(1)
        self.assertEqual(re.findall(r'"(\w+)"', cs_emotions), list(EMOTIONS))
        self.assertIn(f"PhrasesPerEmotion = {import_unity.PHRASES};", vm)

    def test_every_take_is_in_the_project_and_nothing_else(self):
        dest = import_unity.DEST
        self.assertEqual(sorted(p.name for p in dest.iterdir() if p.is_dir()), sorted(import_unity.all_ids()))
        for sound_id in import_unity.all_ids():
            ext = import_unity.extension(sound_id)
            expected = {f"{sound_id}_t{t}.{ext}" for t in range(1, import_unity.takes_of(sound_id) + 1)}
            files = {p.name for p in (dest / sound_id).iterdir() if not p.name.endswith(".meta")}
            self.assertEqual(files, expected, sound_id)
            for name in files:
                self.assertTrue((dest / sound_id / (name + ".meta")).exists(), name + " needs a .meta")
                path = dest / sound_id / name
                if ext == "wav":
                    with wave.open(str(path)) as w:
                        self.assertEqual((w.getframerate(), w.getsampwidth(), w.getnchannels()), (SAMPLE_RATE, 2, 1), name)
                else:
                    self.assertEqual(path.read_bytes()[:4], b"OggS", name)
                    channels = 1 if sound_id in import_unity.VOICES else 2
                    self.assertEqual(vorbis_format(path), (channels, SAMPLE_RATE), name)

    def test_project_files_are_the_pipeline_masters(self):
        import tempfile
        from pathlib import Path

        import build
        from spookyaudio import checks, dsp

        by_id = {s.id: s for s in sounds.SOUNDS}
        for sound_id in ("ui_tap", "step_stone", "clue_cam_tap"):
            audio, _ = build.render_take(by_id[sound_id], 1)
            with tempfile.TemporaryDirectory() as tmp:
                out = Path(tmp) / "t1.wav"
                checks.write_wav(out, audio, dsp.seed_for(sound_id, 1, "dither"))
                self.assertEqual(out.read_bytes(), (import_unity.DEST / sound_id / f"{sound_id}_t1.wav").read_bytes(),
                                 sound_id + ": re-run build.py and import_unity.py")

    def test_generated_csharp_data_is_current(self):
        text = (CORE / "VoiceMusicData.cs").read_text()
        samples = {sid: int(re.search(name + r"Samples = (\d+);", text).group(1)) for sid, name in (
            ("music_street_theme", "StreetTheme"), ("music_exploration", "Exploration"),
            ("music_case_closed", "CaseClosed"), ("music_reveal", "Reveal"))}
        self.assertEqual(import_unity.data_cs(samples), text, "re-run tools/audio/import_unity.py")
        self.assertIn(", ".join(f'"{c.id}"' for c in CAST), text)

    @unittest.skipUnless(shutil.which("ffmpeg"), "needs ffmpeg")
    def test_music_keeps_its_exact_length_and_loop_seam(self):
        text = (CORE / "VoiceMusicData.cs").read_text()
        for sound_id, name in (("music_street_theme", "StreetTheme"), ("music_exploration", "Exploration"),
                               ("music_case_closed", "CaseClosed"), ("music_reveal", "Reveal")):
            expected = int(re.search(name + r"Samples = (\d+);", text).group(1))
            x = decode(import_unity.DEST / sound_id / f"{sound_id}_t1.ogg")
            self.assertEqual(len(x), expected, sound_id + ": the decoded Ogg must keep every sample of the master")
            master = import_unity.source_of(sound_id, 1)
            if master.exists():
                with wave.open(str(master)) as w:
                    self.assertEqual(w.getnframes(), expected, sound_id)
            if sound_id in ("music_street_theme", "music_exploration"):
                # The clip loops whole: the jump from its last sample back to its first stays inside the music's own
                # sample-to-sample motion, so the seam is not a click.
                seam = float(np.abs(x[0] - x[-1]).max())
                step = float(np.percentile(np.abs(np.diff(x, axis=0)), 99.9))
                self.assertLess(seam, step, f"{sound_id}: seam {seam:.4f} vs 99.9th percentile step {step:.4f}")
                self.assertLess(seam, 0.02, sound_id)

    def test_planner_golden_captions_match_the_python_reference(self):
        cases = re.findall(r'\[TestCase\("((?:[^"\\]|\\.)*)", (\d+)\)\]', PLANNER_TESTS.read_text(encoding="utf-8"))
        self.assertGreaterEqual(len(cases), 8)
        for text, count in cases:
            self.assertEqual(babble.caption_syllables(text.replace('\\"', '"')), int(count), text)

    def test_the_project_stays_lean(self):
        dest = import_unity.DEST
        effects = sum(p.stat().st_size for sid in import_unity.APPROVED for p in (dest / sid).iterdir())
        v2 = sum(p.stat().st_size for sid in import_unity.VOICES + tuple(import_unity.MUSIC) for p in (dest / sid).iterdir())
        self.assertLess(effects, 9 * 1024 * 1024, "the imported effects should stay under 9 MiB")
        self.assertLess(v2, 8 * 1024 * 1024, "the imported voices and music should stay under 8 MiB")


if __name__ == "__main__":
    unittest.main()
