"""D20.1 post-processing: PSOLA pitch/speed, syllable slicing, the runtime babble planner, the loop search and the
key transposition. All on synthetic signals (no network, no generated sources needed), plus a consistency check
of the committed babble bank and manifest when they exist."""
import json
import unittest

import numpy as np

from tests._path import AUDIO
from spookyaudio import SAMPLE_RATE as SR
from spookyaudio import babble, music
from spookyaudio.babble_spec import AVOID, CAST, EMOTIONS


def glottal(f0, seconds, formant=900.0, seed=0):
    """A pulse train through one resonance: a voice-like test signal with a known pitch."""
    n = int(seconds * SR)
    x = np.zeros(n)
    period = SR / f0
    for k in np.arange(0, n, period):
        x[int(k)] = 1.0
    t = np.arange(int(0.01 * SR)) / SR
    ir = np.exp(-t * 300) * np.sin(2 * np.pi * formant * t)
    return np.convolve(x, ir)[:n] * 0.3


def syllable_train(count, f0=300.0, syl=0.16, gap=0.0):
    parts = []
    for i in range(count):
        s = glottal(f0 * (1 + 0.05 * (i % 3)), syl) * np.hanning(int(syl * SR))
        parts += [s, np.zeros(int(gap * SR))]
    return np.concatenate(parts)


class PsolaTests(unittest.TestCase):
    def test_pitch_and_speed(self):
        x = glottal(200.0, 1.0)
        y = babble.transform(x, 7.0, 1.8, 1.0)
        self.assertAlmostEqual(len(y) / len(x), 1 / 1.8, delta=0.02)
        self.assertAlmostEqual(babble.median_f0(y) / 200.0, 2 ** (7 / 12), delta=0.05)

    def test_identity_is_transparent(self):
        x = glottal(220.0, 0.8)
        y = babble.psola(x, 1.0, 1.0, 1.0)
        # grains may land a constant fraction of a cycle off where voicing starts: align, then compare
        a, b = int(0.1 * SR), int(0.7 * SR)
        corr = np.correlate(y[a:b], x[a:b], "full")
        lag = int(np.argmax(corr)) - (b - a - 1)
        self.assertLess(abs(lag), int(SR / 220))
        ys = np.roll(y, -lag)
        err = np.sqrt(np.mean((ys[a:b] - x[a:b]) ** 2)) / np.sqrt(np.mean(x[a:b] ** 2))
        self.assertLess(err, 0.25)

    def test_formants_follow_resample_part(self):
        # with formant = pitch ratio the whole shift is a resample: the resonance moves up by the same ratio
        x = glottal(150.0, 1.0, formant=1000.0)
        y = babble.transform(x, 5.0, 1.0, 2 ** (5 / 12))
        f = np.fft.rfftfreq(len(y), 1 / SR)
        peak = f[np.argmax(np.abs(np.fft.rfft(y)) * (f > 500))]
        self.assertAlmostEqual(peak / 1000.0, 2 ** (5 / 12), delta=0.12)


class SlicingTests(unittest.TestCase):
    def test_counts_syllables_and_runs(self):
        x = np.concatenate([syllable_train(4), np.zeros(int(0.3 * SR)), syllable_train(3)])
        sylls = babble.syllables(x)
        self.assertEqual(len(sylls), 7)
        self.assertEqual(len({s["run"] for s in sylls}), 2)

    def test_phrase_lands_near_target(self):
        x = np.concatenate([syllable_train(6), np.zeros(int(0.3 * SR)), syllable_train(6)])
        runs = babble.runs_of(x, babble.syllables(x))
        rng = np.random.default_rng(1)
        for target in (0.6, 1.2, 1.8):
            p = babble.phrase_from_runs(runs, target, rng, (0.02, 0.04))
            self.assertLess(abs(len(p) / SR - target), 0.4, target)


class PlannerTests(unittest.TestCase):
    BANK = [{"duration_s": 0.11 + 0.01 * (i % 4), "f0_hz": 300 + 15 * i, "peak_db": -float(i % 5)}
            for i in range(16)]

    def test_caption_syllables(self):
        self.assertEqual(babble.caption_syllables("Sit down and see for yourself."), 7)
        self.assertGreaterEqual(babble.caption_syllables("Hmm"), 1)

    def test_length_follows_caption_within_bounds(self):
        short = babble.plan_sequence("Hi!", self.BANK, 9.0, 1)
        long = babble.plan_sequence("That front door? I could see it from my bench. It stayed shut. " * 3,
                                    self.BANK, 9.0, 1)
        dur = lambda plan: sum(self.BANK[i]["duration_s"] for i, _, _ in plan)  # noqa: E731
        self.assertGreaterEqual(dur(short), 0.45)
        self.assertLessEqual(dur(long), 3.3)
        self.assertGreater(len(long), len(short))

    def test_seeded_and_no_immediate_repeat(self):
        a = babble.plan_sequence("I saw June's silver cape go in. Right there!", self.BANK, 10.5, 7)
        b = babble.plan_sequence("I saw June's silver cape go in. Right there!", self.BANK, 10.5, 7)
        self.assertEqual(a, b)
        idx = [i for i, _, _ in a]
        self.assertTrue(all(p != q for p, q in zip(idx, idx[1:])))
        self.assertTrue(any(g > 0 for _, g, _ in a[:-1]), "the full stop should become a pause")
        self.assertTrue(all(abs(s) <= 2.0 for _, _, s in a))

    def test_question_rises(self):
        plan = babble.plan_sequence("Give me a hand?", self.BANK, 9.0, 3)
        self.assertEqual(plan[-1][2], 2.0)
        self.assertEqual(plan[-1][0], int(np.argmax([b["f0_hz"] for b in self.BANK])))


class MusicEditTests(unittest.TestCase):
    def _beat_music(self, bpm=96.0, bars=48):
        beat = 60 / bpm
        n = int(bars * 4 * beat * SR)
        x = np.zeros(n)
        notes = [146.83, 174.61, 220.0, 293.66]  # D F A D
        for k in range(bars * 4):
            t0 = int(k * beat * SR)
            m = min(n - t0, int(0.3 * SR))
            env = np.exp(-np.arange(m) / (0.08 * SR))
            f = notes[(k // 4) % 4] * (2 if k % 4 == 0 else 1)
            x[t0:t0 + m] += env * np.sin(2 * np.pi * f * np.arange(m) / SR) * (1.0 if k % 4 == 0 else 0.6)
        return np.stack([x, x], axis=1) * 0.3

    def test_tempo_loop_and_seam(self):
        x = self._beat_music()
        beats, bpm = music.beats(x)
        self.assertAlmostEqual(bpm, 96.0, delta=2.0)
        found = music.find_loop(x, beats, (40.0, 95.0))
        length = (found["end"] - found["start"]) / SR
        bar = 4 * 60 / 96.0
        self.assertAlmostEqual(length / bar, round(length / bar), delta=0.1)
        loop, _ = music.make_loop(x, found["start"], found["end"])
        seam = np.abs(loop[0] - loop[-1]).max()
        self.assertLess(seam, 0.05)

    def test_transpose_to_d_or_a(self):
        x = np.zeros((48000, 2))
        self.assertEqual(music.transpose_to(x, 3)[1], -1)   # Eb -> D
        self.assertEqual(music.transpose_to(x, 7)[1], 2)    # G -> A
        self.assertEqual(music.transpose_to(x, 2)[1], 0)    # D stays
        y, st = music.transpose_to(x, 0)                    # C -> D (+2): shorter by 2 semitones
        self.assertEqual(st, 2)
        self.assertAlmostEqual(len(y) / len(x), 2 ** (-2 / 12), delta=0.01)

    def test_key_estimate_of_a_d_major_chord(self):
        t = np.arange(SR * 2) / SR
        chord = sum(np.sin(2 * np.pi * f * t) for f in (146.83, 185.0, 220.0, 293.66))
        tonic, mode, _ = music.estimate_key(chord)
        self.assertEqual((music.PITCH_NAMES[tonic], mode), ("D", "major"))


class SpecAndDataTests(unittest.TestCase):
    def test_cast_is_distinct(self):
        self.assertEqual(len({c.voice for c in CAST}), len(CAST))
        self.assertEqual(len({c.id for c in CAST}), len(CAST))

    def test_avoid_list_blocks_known_words(self):
        for word in ("hobo", "mama", "kit", "boba"):
            self.assertIn(word, AVOID)

    def test_committed_bank_matches_spec(self):
        path = AUDIO / "generated" / "babble_bank.json"
        if not path.exists():
            self.skipTest("no generated bank")
        bank = json.loads(path.read_text())
        self.assertEqual(set(bank["characters"]), {c.id for c in CAST})
        for cid, c in bank["characters"].items():
            self.assertEqual(set(c["emotions"]), set(EMOTIONS), cid)
            for e, data in c["emotions"].items():
                self.assertGreaterEqual(len(data["syllables"]), 8, f"{cid}/{e}")
                self.assertEqual(len(data["phrases"]), 6, f"{cid}/{e}")
                offsets = [s["offset"] for s in data["syllables"]]
                self.assertEqual(offsets, sorted(offsets))

    def test_manifest_has_no_secrets(self):
        for name in ("manifest.json", "calls.jsonl"):
            path = AUDIO / "generated" / name
            if path.exists():
                text = path.read_text().lower()
                for marker in ("x-goog-api-key", "aiza", "key=", "authorization", "bearer"):
                    self.assertNotIn(marker, text, f"{name} contains {marker}")


if __name__ == "__main__":
    unittest.main()
