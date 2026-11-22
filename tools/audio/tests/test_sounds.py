"""Seeded determinism of the synthesis, take variation, every sound passing its checks, and the sync with the
game's audio catalog (Assets/SpookyStreet/Core/Audio/AudioEvents.cs)."""
import io
import re
import unittest

import numpy as np

from tests._path import REPO
from spookyaudio import checks, dsp, master, sounds

import build  # tools/audio/build.py (on sys.path via tests._path)

FAST = ("ui_tap", "step_stone", "clue_cam_tap", "voice_pip", "note_sticker_drop")


class DeterminismTests(unittest.TestCase):
    def test_seeds_are_stable_across_platforms(self):
        # crc32-based: this value must never change, or every sound would re-render differently.
        self.assertEqual(dsp.seed_for("ui_tap", 1), 1795783536)

    def test_same_seed_same_audio(self):
        for sound_id in FAST:
            sound = sounds.by_id()[sound_id]
            a, _ = build.render_take(sound, 1)
            b, _ = build.render_take(sound, 1)
            np.testing.assert_array_equal(a, b, err_msg=sound_id)

    def test_wav_bytes_are_reproducible(self):
        sound = sounds.by_id()["ui_tap"]
        audio, _ = build.render_take(sound, 2)
        blobs = []
        for _ in range(2):
            buf = io.BytesIO()
            from scipy.io import wavfile
            wavfile.write(buf, 48000, checks.to_pcm16(audio, dsp.seed_for("ui_tap", 2, "dither")))
            blobs.append(buf.getvalue())
        self.assertEqual(blobs[0], blobs[1])

    def test_takes_differ(self):
        for sound_id in FAST:
            sound = sounds.by_id()[sound_id]
            a, _ = build.render_take(sound, 1)
            b, _ = build.render_take(sound, 2)
            n = min(len(a), len(b))
            self.assertFalse(np.allclose(a[:n], b[:n], atol=1e-3), sound_id)


class SoundSetTests(unittest.TestCase):
    def test_every_sound_passes_its_checks(self):
        for sound in sounds.SOUNDS:
            with self.subTest(sound=sound.id):
                audio, info = build.render_take(sound, 1)
                _, failures = checks.analyse(audio, sound.category, sound.budget, loop=sound.loop,
                                             target_lufs=info["target_lufs"])
                self.assertEqual(failures, [])

    def test_set_shape(self):
        ids = [s.id for s in sounds.SOUNDS]
        self.assertEqual(len(ids), len(set(ids)))
        for sound in sounds.SOUNDS:
            self.assertIn(sound.category, master.CATEGORIES)
            self.assertTrue(3 <= sound.takes <= 5, sound.id)
            self.assertTrue(re.fullmatch(r"[a-z0-9_]+", sound.id), sound.id)
        self.assertGreater(sounds.KNOCK_ONSETS[2] - sounds.KNOCK_ONSETS[1],
                           2 * (sounds.KNOCK_ONSETS[1] - sounds.KNOCK_ONSETS[0]), "short, short, pause, last")


class CatalogSyncTests(unittest.TestCase):
    """The C# catalog plays exactly the takes the pipeline renders."""

    def setUp(self):
        self.cs = (REPO / "Assets" / "SpookyStreet" / "Core" / "Audio" / "AudioEvents.cs").read_text()

    def test_ids_and_take_counts_match(self):
        consts = dict(re.findall(r'public const string (\w+) = "([a-z0-9_]+)";', self.cs))
        adds = re.findall(r"new AudioEventDef\((\w+), AudioBus\.\w+, (\d+)", self.cs)
        cs_takes = {consts[name]: int(count) for name, count in adds}
        py_takes = {s.id: s.takes for s in sounds.SOUNDS}
        self.assertEqual(cs_takes, py_takes)
        cs_loops = {consts[n] for n in re.findall(r"new AudioEventDef\((\w+),[^;]*loop: true", self.cs)}
        self.assertEqual(cs_loops, {s.id for s in sounds.SOUNDS if s.loop})

    def test_knock_markers_match(self):
        for name, onsets in (("KnockOnsets", sounds.KNOCK_ONSETS), ("HumKnockOnsets", sounds.HUM_KNOCK_ONSETS)):
            m = re.search(r"%s = \{ ([^}]*) \}" % name, self.cs)
            values = tuple(float(v.strip().rstrip("f")) for v in m.group(1).split(","))
            self.assertEqual(values, tuple(onsets), name)


if __name__ == "__main__":
    unittest.main()
