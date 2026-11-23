"""Editing generated music into game cues: beat tracking, bar-to-bar loop search with a phase-aligned crossfade,
and stings/reveals cut from a piece's natural ending. numpy/scipy only.

Loop search: per beat, a feature vector (12-bin chroma plus 6 band energies). A loop [i, j) must be a whole number
of bars (4 beats), last 55-95 s, start after the intro and end before the outro. Its score is the cosine similarity
of the 8 beats after i with the 8 beats after j (what the ear hears across the seam) and of the 4 beats before
each (so the music leading into the seam matches too). The seam is then aligned to the sample by waveform
cross-correlation and crossfaded with equal power over 60 ms, using the audio just before the loop start as the
fade-in, so the end flows into the start exactly as the original flowed into bar i.
"""
import numpy as np
from scipy import ndimage, signal

from . import SAMPLE_RATE as SR

HOP = 512


def mono(x):
    return x.mean(axis=1) if x.ndim == 2 else x


def stft_mag(x, n_fft=2048, hop=HOP):
    f, t, z = signal.stft(mono(x), SR, nperseg=n_fft, noverlap=n_fft - hop, boundary=None, padded=False)
    return f, np.abs(z)


def onset_envelope(x):
    f, mag = stft_mag(x)
    logm = np.log1p(100 * mag)
    flux = np.maximum(0, np.diff(logm, axis=1)).sum(axis=0)
    flux = np.concatenate([[0], flux])
    flux = flux - ndimage.uniform_filter1d(flux, 32)
    return np.maximum(flux, 0)


def tempo(env, lo=70.0, hi=140.0, prior=96.0):
    """Tempo from the onset autocorrelation, weighted gently toward the prompted tempo."""
    fps = SR / HOP
    ac = np.correlate(env - env.mean(), env - env.mean(), mode="full")[len(env) - 1:]
    lags = np.arange(len(ac))
    bpm = 60 * fps / np.maximum(lags, 1)
    ok = (bpm >= lo) & (bpm <= hi)
    weight = np.exp(-0.5 * (np.log2(bpm / prior) / 0.4) ** 2)
    score = np.where(ok, ac * weight, -np.inf)
    lag = int(np.argmax(score))
    # parabolic refinement
    if 1 <= lag < len(ac) - 1:
        a, b, c = ac[lag - 1], ac[lag], ac[lag + 1]
        d = a - 2 * b + c
        lag = lag + (0.5 * (a - c) / d if d != 0 else 0)
    return 60 * fps / lag


def beats(x, bpm=None, tightness=100.0):
    """Dynamic-programming beat tracker (Ellis 2007). Returns beat times in samples and the tempo."""
    env = onset_envelope(x)
    env = env / (env.std() + 1e-9)
    bpm = bpm or tempo(env)
    fps = SR / HOP
    period = 60 * fps / bpm
    n = len(env)
    score = env.copy()
    back = -np.ones(n, dtype=int)
    lo, hi = int(round(period / 2)), int(round(period * 2))
    for t in range(hi, n):
        prev = np.arange(t - hi, t - lo + 1)
        cost = -tightness * (np.log((t - prev) / period)) ** 2
        k = int(np.argmax(score[prev] + cost))
        score[t] = env[t] + score[prev[k]] + cost[k]
        back[t] = prev[k]
    # start from the best score in the last period
    t = n - hi + int(np.argmax(score[n - hi:]))
    out = []
    while t >= 0:
        out.append(t)
        t = back[t]
    frames = np.array(out[::-1])
    return frames * HOP + HOP // 2, bpm


def beat_features(x, beat_samples):
    f, mag = stft_mag(x)
    band = (f >= 80) & (f <= 5000)
    midi = 69 + 12 * np.log2(np.maximum(f[band], 1) / 440.0)
    pc = np.mod(np.round(midi), 12).astype(int)
    chroma = np.zeros((12, mag.shape[1]))
    for k in range(12):
        chroma[k] = mag[band][pc == k].sum(axis=0)
    edges = [60, 150, 400, 1000, 2500, 6000, 16000]
    bands = np.stack([mag[(f >= a) & (f < b)].sum(axis=0) for a, b in zip(edges[:-1], edges[1:])])
    frames = beat_samples // HOP
    feats = []
    for a, b in zip(frames[:-1], frames[1:]):
        c = chroma[:, a:b].mean(axis=1)
        c = c / (np.linalg.norm(c) + 1e-9)
        e = np.log1p(bands[:, a:b].mean(axis=1))
        e = e / (np.linalg.norm(e) + 1e-9)
        feats.append(np.concatenate([c, 0.7 * e]))
    return np.array(feats)


def meter(feats):
    """3 or 4 beats per bar: the lag (and its double) at which beat features repeat best."""
    def sim(lag):
        return float(np.mean(np.sum(feats[:-lag] * feats[lag:], axis=1)))
    s3, s4 = sim(3) + sim(6), sim(4) + sim(8)
    return (3 if s3 > s4 else 4), round(s3, 3), round(s4, 3)


def find_loop(x, beat_samples, length_s=(55.0, 95.0), skip_start_s=6.0, skip_end_s=12.0, beats_per_bar=None,
              window=8, context=4, prefer_s=75.0):
    feats = beat_features(x, beat_samples)
    if beats_per_bar is None:
        beats_per_bar = meter(feats)[0]
    nb = len(feats)
    t = beat_samples / SR
    best = None
    step = 2 * beats_per_bar  # whole pairs of bars, so phrases stay intact
    for i in range(context, nb - window):
        if t[i] < skip_start_s:
            continue
        for j in range(i + step, nb - window, step):
            d = t[j] - t[i]
            if d < length_s[0]:
                continue
            if d > length_s[1] or t[j] > len(x) / SR - skip_end_s:
                break
            after = np.mean(np.sum(feats[i:i + window] * feats[j:j + window], axis=1)) / 1.49
            before = np.mean(np.sum(feats[i - context:i] * feats[j - context:j], axis=1)) / 1.49
            bars = (j - i) // beats_per_bar
            phrase_bonus = 0.01 if bars % 8 == 0 else (0.005 if bars % 4 == 0 else 0.0)
            score = 0.65 * after + 0.35 * before - 0.002 * abs(d - prefer_s) + phrase_bonus
            if best is None or score > best[0]:
                best = (score, i, j)
    if best is None:
        raise ValueError("no loop candidate")
    score, i, j = best
    return {"start": int(beat_samples[i]), "end": int(beat_samples[j]), "score": round(float(score), 4),
            "bars": int((j - i) // beats_per_bar), "beats_per_bar": int(beats_per_bar), "start_beat": int(i),
            "end_beat": int(j)}


def align(x, a, b, search=0.02, span=0.08):
    """Shift b by up to `search` s so the waveform after b best matches the waveform after a."""
    m = mono(x)
    n, s = int(span * SR), int(search * SR)
    ref = m[a:a + n]
    region = m[b - s:b + s + n]
    if len(region) < n + 2 * s or len(ref) < n:
        return b
    corr = signal.fftconvolve(region, ref[::-1], mode="valid")
    return b - s + int(np.argmax(corr))


def make_loop(x, start, end, xfade=0.06):
    """The loop is x[start:end] whose last `xfade` seconds blend into the audio that preceded `start`."""
    end = align(x, start, end)
    n = int(xfade * SR)
    body = x[start:end].copy()
    pre = x[start - n:start]
    ramp = np.linspace(0, np.pi / 2, n)
    if body.ndim == 2:
        ramp = ramp[:, None]
    body[-n:] = body[-n:] * np.cos(ramp) + pre * np.sin(ramp)
    return body, end


def music_end(x, floor_db=-50.0):
    """Sample index where the piece has decayed into silence (relative to its loudest 100 ms)."""
    m = mono(x)
    h = int(0.05 * SR)
    k = len(m) // h
    rms = np.sqrt(np.mean(m[: k * h].reshape(k, h) ** 2, axis=1) + 1e-12)
    db = 20 * np.log10(rms)
    loud = np.nonzero(db > db.max() + floor_db)[0]
    return int((loud[-1] + 1) * h) if len(loud) else len(m)


def cut_ending(x, beat_samples, length_s, prefer="onset", fade_in=0.012, fade_out=0.8):
    """Take the natural ending of a piece: start on a beat between end-length_s[1] and end-length_s[0]
    ('onset': the strongest attack; 'hush': the beat after the quietest second, for a reveal that builds)."""
    end = music_end(x)
    m = mono(x)
    env = onset_envelope(x)
    lo, hi = end - int(length_s[1] * SR), end - int(length_s[0] * SR)
    cands = [b for b in beat_samples if lo <= b <= hi]
    if not cands:
        cands = [max(0, lo)]
    if prefer == "onset":
        scores = [env[min(len(env) - 1, b // HOP)] for b in cands]
        start = int(cands[int(np.argmax(scores))])
    else:
        rms = [np.sqrt(np.mean(m[max(0, b - SR):b] ** 2) + 1e-12) for b in cands]
        start = int(cands[int(np.argmin(rms))])
    out = x[start:end].copy()
    n_in, n_out = int(fade_in * SR), int(min(fade_out, (end - start) / SR / 4) * SR)
    w = np.ones(len(out))
    w[:n_in] = np.sin(np.linspace(0, np.pi / 2, n_in)) ** 2
    w[-n_out:] *= np.cos(np.linspace(0, np.pi / 2, n_out)) ** 2
    return out * (w[:, None] if out.ndim == 2 else w), start, end


# ---------------------------------------------------------------- key and transposition

PITCH_NAMES = "C C# D Eb E F F# G Ab A Bb B".split()
_MAJOR = np.array([6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88])  # Krumhansl-Kessler
_MINOR = np.array([6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17])


def chroma(x, lo=60.0, hi=2000.0):
    f, mag = stft_mag(x, n_fft=8192)
    band = (f >= lo) & (f <= hi)
    pc = np.mod(np.round(69 + 12 * np.log2(f[band] / 440.0)), 12).astype(int)
    c = np.array([mag[band][pc == k].sum() for k in range(12)])
    return c / (c.sum() + 1e-12)


def estimate_key(x):
    """(tonic pitch class, 'major'|'minor', correlation) by Krumhansl-Kessler profile matching."""
    c = chroma(x)
    best = max(((np.corrcoef(np.roll(prof, r), c)[0, 1], r, mode)
                for r in range(12) for prof, mode in ((_MAJOR, "major"), (_MINOR, "minor"))))
    return best[1], best[2], round(float(best[0]), 3)


def transpose_to(x, tonic, targets=(2, 9), max_st=3):
    """Vari-speed (resample) the clip so its tonic lands on the nearest target pitch class (D, else A), when that
    is at most max_st semitones away. Returns (audio, semitones). Vari-speed is the cleanest shift there is; it
    also changes the tempo by the same ratio, which is fine for a sting or reveal."""
    best = None
    for t in targets:
        d = ((t - tonic + 6) % 12) - 6
        if abs(d) <= max_st and (best is None or abs(d) < abs(best)):
            best = d
    if not best:
        return x, 0
    from fractions import Fraction
    fr = Fraction(2 ** (-best / 12)).limit_denominator(480)
    return signal.resample_poly(x, fr.numerator, fr.denominator, axis=0), int(best)
