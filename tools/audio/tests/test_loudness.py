"""Loudness meter, mastering chain and automatic checks."""
import unittest

import numpy as np

from tests._path import AUDIO  # noqa: F401  (sets sys.path)
from spookyaudio import SAMPLE_RATE as SR
from spookyaudio import checks, dsp, loudness, master


def sine(freq, amp, seconds, phase=0.0):
    t = np.arange(int(seconds * SR)) / SR
    return amp * np.sin(2 * np.pi * freq * t + phase)


class LoudnessMeterTests(unittest.TestCase):
    def test_full_scale_997_hz_sine_reads_minus_3_lufs(self):
        # BS.1770-4: a 0 dBFS 997 Hz sine in one channel reads -3.01 LUFS.
        self.assertAlmostEqual(loudness.integrated_lufs(sine(997, 1.0, 3)), -3.01, delta=0.05)

    def test_level_changes_track_in_db(self):
        a = loudness.integrated_lufs(sine(997, 0.5, 3))
        b = loudness.integrated_lufs(sine(997, 0.05, 3))
        self.assertAlmostEqual(a - b, 20.0, delta=0.05)

    def test_two_identical_channels_add_3_db(self):
        mono = sine(997, 0.25, 3)
        stereo = np.stack([mono, mono], axis=1)
        self.assertAlmostEqual(loudness.integrated_lufs(stereo) - loudness.integrated_lufs(mono), 3.01, delta=0.05)

    def test_silence_is_gated_out(self):
        tone = sine(997, 0.5, 2)
        padded = np.concatenate([tone, np.zeros(4 * SR)])
        # Ungated, 4 s of silence would pull it down 4.8 LU; gated, only blocks straddling the edge count a little.
        self.assertAlmostEqual(loudness.integrated_lufs(padded), loudness.integrated_lufs(tone), delta=0.5)

    def test_short_clips_use_the_whole_clip(self):
        value, method = loudness.integrated_lufs(sine(997, 1.0, 0.1), with_method=True)
        self.assertEqual(method, "short-clip")
        self.assertAlmostEqual(value, -3.01, delta=0.15)

    def test_true_peak_sees_intersample_peaks(self):
        # fs/4 sine with a 45° phase: samples sit at ±0.707 of the true peak.
        x = sine(SR / 4, 1.0, 0.05, np.pi / 4)
        self.assertAlmostEqual(loudness.sample_peak_db(x), -3.01, delta=0.05)
        self.assertGreater(loudness.true_peak_db(x), -0.5)


class MasteringTests(unittest.TestCase):
    def test_every_category_hits_its_target_under_the_ceiling(self):
        rng = np.random.default_rng(1)
        burst = dsp.bandpass(rng.standard_normal(SR), 200, 5000) * dsp.attack_decay(SR, 0.01, 0.2)
        for name, category in master.CATEGORIES.items():
            out, _ = master.master(burst + 0.01, name)
            self.assertAlmostEqual(loudness.integrated_lufs(out), category.lufs, delta=category.tolerance, msg=name)
            self.assertLessEqual(loudness.true_peak_db(out), master.CEILING_DBTP + 0.05, name)
            self.assertLess(abs(float(np.mean(out))), 1e-3, name)

    def test_sub_bass_is_removed(self):
        low = sine(40, 0.5, 1.0) + sine(1000, 0.1, 1.0)
        out, _ = master.master(low, "movement")
        spectrum = np.abs(np.fft.rfft(out)) ** 2
        freqs = np.fft.rfftfreq(len(out), 1 / SR)
        sub = spectrum[freqs < 60].sum() / spectrum.sum()
        self.assertLess(sub, 0.01)

    def test_loops_stay_seamless(self):
        rng = np.random.default_rng(3)
        n = 2 * SR
        loop = np.fft.irfft(np.fft.rfft(rng.standard_normal(n)) * (np.arange(n // 2 + 1) < 4000), n)
        out, _ = master.master(np.stack([loop, loop], axis=1), "ambience", loop=True)
        step = np.abs(np.diff(out[:, 0])).max()
        self.assertLessEqual(abs(out[0, 0] - out[-1, 0]), step * 1.5)


class CheckTests(unittest.TestCase):
    def test_a_clean_take_passes(self):
        out, _ = master.master(dsp.fade(sine(800, 0.3, 0.5) * dsp.exp_decay(SR // 2, 0.1)), "ui")
        _, failures = checks.analyse(out, "ui", (0.1, 1.0))
        self.assertEqual(failures, [])

    def test_problems_are_reported(self):
        x = np.full(SR, 0.2)
        x[100] = 1.0
        _, failures = checks.analyse(x, "ui", (2.0, 3.0))
        text = " ".join(failures)
        for word in ("loudness", "clipped", "DC offset", "duration", "click", "true peak"):
            self.assertIn(word, text)

    def test_spectrogram_png_is_written(self):
        import tempfile
        from pathlib import Path
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "s.png"
            checks.spectrogram_png(path, sine(500, 0.5, 0.3))
            data = path.read_bytes()
        self.assertTrue(data.startswith(b"\x89PNG\r\n\x1a\n"))


if __name__ == "__main__":
    unittest.main()
