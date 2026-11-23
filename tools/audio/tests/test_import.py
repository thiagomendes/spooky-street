"""The approved effects in the Unity project (S003, D20.1) match the pipeline and the C# import plan."""
import re
import unittest
import wave

from tests._path import REPO  # puts tools/audio on sys.path

import import_unity  # noqa: E402
from spookyaudio import SAMPLE_RATE, sounds  # noqa: E402

PLAN = REPO / "Assets" / "SpookyStreet" / "Core" / "Audio" / "AudioImportPlan.cs"
EVENTS = REPO / "Assets" / "SpookyStreet" / "Core" / "Audio" / "AudioEvents.cs"


class ImportedAudioTests(unittest.TestCase):
    def test_approved_lists_match_the_csharp_plan(self):
        consts = dict(re.findall(r'public const string (\w+) = "([a-z0-9_]+)";', EVENTS.read_text()))
        block = re.search(r"Approved =\s*\{(.*?)\};", PLAN.read_text(), re.S).group(1)
        cs = [consts[name] for name in re.findall(r"AudioEvents\.(\w+)", block)]
        self.assertEqual(cs, list(import_unity.APPROVED))
        for sound_id in import_unity.APPROVED:
            self.assertFalse(sound_id.startswith(("voice_", "music_")), sound_id)

    def test_every_take_is_in_the_project_and_nothing_else(self):
        by_id = {s.id: s for s in sounds.SOUNDS}
        dest = import_unity.DEST
        self.assertEqual(sorted(p.name for p in dest.iterdir() if p.is_dir()), sorted(import_unity.APPROVED))
        for sound_id in import_unity.APPROVED:
            ext = import_unity.extension(sound_id)
            expected = {f"{sound_id}_t{t}.{ext}" for t in range(1, by_id[sound_id].takes + 1)}
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

    def test_the_project_stays_lean(self):
        total = sum(p.stat().st_size for p in import_unity.DEST.rglob("*") if p.is_file())
        self.assertLess(total, 9 * 1024 * 1024, "the imported effects should stay under 9 MiB")


if __name__ == "__main__":
    unittest.main()
