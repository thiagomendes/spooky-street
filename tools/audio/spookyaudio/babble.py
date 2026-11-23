"""Cartoon babble from natural source speech (D20.1): speed-up and pitch-up with formant control, syllable slicing,
a per-character syllable bank, phrase assembly and the runtime sequencing rules.

Signal path per source take (48 kHz):
  1. resample by the pitch factor p (pitch and speed both rise by p),
  2. move the spectral envelope back so the formants end at `formant` x the source (cepstral envelope warp per STFT
     frame; this is what stops it sounding like a chipmunk),
  3. WSOLA time-scale the result to the character's final speed (p and the speed are close, so this step is small
     and leaves few artefacts),
  4. slice into syllables at envelope minima between energy peaks; runs of syllables between pauses are kept, too.
Phrase takes are natural runs (with their original co-articulation) joined with short gaps; the sequenced examples
use the bank exactly as the runtime would (`plan_sequence`).
"""
import subprocess
import zlib

import numpy as np
from scipy import ndimage, signal

from . import SAMPLE_RATE as SR
from . import dsp


# ---------------------------------------------------------------- I/O

def load_audio(path, sr=SR):
    """Decode any file ffmpeg reads to float mono at `sr`."""
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-ac", "1", "-ar", str(sr), "-"],
                         check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).astype(float)


def load_stereo(path, sr=SR):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-ac", "2", "-ar", str(sr), "-"],
                         check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).astype(float).reshape(-1, 2)


# ---------------------------------------------------------------- analysis

def envelope_db(x, hop=0.005, smooth=0.02, band=(200.0, 4000.0)):
    """Smoothed band-limited RMS envelope in dB, one value per hop."""
    y = dsp.bandpass(x, band[0], band[1], 2)
    h = max(1, int(hop * SR))
    n = len(y) // h
    rms = np.sqrt(np.mean(y[: n * h].reshape(n, h) ** 2, axis=1) + 1e-12)
    k = max(1, int(smooth / hop))
    rms = np.convolve(rms, np.hanning(k + 2)[1:-1] / np.hanning(k + 2)[1:-1].sum(), mode="same")
    return 20 * np.log10(rms + 1e-9), h


def f0_track(x, frame=0.04, hop=0.01, fmin=70.0, fmax=900.0, threshold=0.55):
    """Normalised-autocorrelation pitch per frame (Hz, 0 where unvoiced)."""
    n, h = int(frame * SR), int(hop * SR)
    lo, hi = int(SR / fmax), int(SR / fmin)
    out = []
    for start in range(0, max(1, len(x) - n), h):
        seg = x[start:start + n] * np.hanning(n)
        if np.sqrt(np.mean(seg ** 2)) < 1e-3:
            out.append(0.0)
            continue
        spec = np.fft.rfft(seg, 2 * n)
        ac = np.fft.irfft(np.abs(spec) ** 2)[: n]
        ac = ac / (ac[0] + 1e-12)
        lag = lo + int(np.argmax(ac[lo:hi]))
        if ac[lag] < threshold:
            out.append(0.0)
            continue
        # parabolic refinement
        if 1 <= lag < len(ac) - 1:
            a, b, c = ac[lag - 1], ac[lag], ac[lag + 1]
            denom = a - 2 * b + c
            lag = lag + (0.5 * (a - c) / denom if denom != 0 else 0.0)
        out.append(SR / lag)
    return np.array(out)


def median_f0(x):
    f = f0_track(x)
    f = f[f > 0]
    return float(np.median(f)) if len(f) else 0.0


# ---------------------------------------------------------------- transform

def _cepstral_envelope(logmag, cutoff):
    cep = np.fft.irfft(logmag, axis=-1)
    lifter = np.zeros(cep.shape[-1])
    lifter[:cutoff] = 1.0
    lifter[-cutoff + 1:] = 1.0
    lifter[cutoff - 1] = 0.5
    return np.fft.rfft(cep * lifter, axis=-1).real


def formant_warp(x, ratio, n_fft=1024, hop=128, cutoff=30, max_gain_db=14.0, smooth_frames=3):
    """Scale the spectral envelope's frequency axis by 1/ratio: ratio > 1 moves formants down (after a pitch-up by
    resampling, ratio = p / desired formant scale). The fine (harmonic) structure is untouched. A short frame
    (21 ms) keeps attacks crisp; the gain is smoothed over a few frames so it cannot flutter (a 'phasey' sound)."""
    if abs(ratio - 1.0) < 1e-3:
        return x
    f, t, z = signal.stft(x, SR, window="hann", nperseg=n_fft, noverlap=n_fft - hop, boundary="even")
    mag = np.abs(z).T + 1e-9
    logmag = np.log(mag)
    env = _cepstral_envelope(logmag, cutoff)
    bins = np.arange(env.shape[1])
    src = np.clip(bins * ratio, 0, env.shape[1] - 1)
    target = np.stack([np.interp(src, bins, row) for row in env])
    gain = target - env
    if smooth_frames > 1:
        k = np.hanning(smooth_frames + 2)[1:-1]
        gain = ndimage.convolve1d(gain, k / k.sum(), axis=0, mode="nearest")
    gain = np.clip(gain, -max_gain_db / 8.686, max_gain_db / 8.686)
    z = z * np.exp(gain.T)
    _, y = signal.istft(z, SR, window="hann", nperseg=n_fft, noverlap=n_fft - hop, boundary=True)
    y = y[: len(x)]
    if len(y) < len(x):
        y = np.pad(y, (0, len(x) - len(y)))
    return y


def wsola(x, stretch, win=0.025, tolerance=0.008):
    """Waveform-similarity overlap-add time scaling. stretch < 1 shortens. Pitch and formants are kept."""
    if abs(stretch - 1.0) < 1e-3:
        return x.copy()
    n = int(win * SR) // 2 * 2
    hs = n // 2
    ha = hs / stretch
    tol = int(tolerance * SR)
    w = np.hanning(n + 2)[1:-1]
    out_len = int(len(x) * stretch) + n
    y = np.zeros(out_len + n)
    norm = np.zeros(out_len + n)
    xp = np.pad(x, (tol + n, tol + 2 * n))
    off = tol + n
    prev = 0
    k = 0
    while True:
        out_pos = k * hs
        nominal = int(round(k * ha))
        if nominal >= len(x) or out_pos >= out_len:
            break
        if k == 0:
            best = nominal
        else:
            natural = xp[off + prev + hs: off + prev + hs + n]
            lo = nominal - tol
            region = xp[off + lo: off + lo + n + 2 * tol]
            corr = signal.fftconvolve(region, natural[::-1], mode="valid")
            energy = np.sqrt(signal.fftconvolve(region ** 2, np.ones(n), mode="valid") + 1e-12)
            best = lo + int(np.argmax(corr / energy))
        y[out_pos: out_pos + n] += xp[off + best: off + best + n] * w
        norm[out_pos: out_pos + n] += w
        prev = best
        k += 1
    norm[norm < 1e-3] = 1.0
    y = y / norm
    return y[: int(len(x) * stretch)]


def pitch_marks(x, fmin=70.0, fmax=900.0):
    """Pitch-synchronous analysis marks for PSOLA. Returns (marks, periods, voiced) as sample arrays.
    Voiced regions: one mark per glottal cycle at the waveform peak nearest the predicted position. Unvoiced:
    marks every 5 ms."""
    hop = 0.005
    f0 = f0_track(x, frame=0.03, hop=hop, fmin=fmin, fmax=fmax, threshold=0.5)
    f0 = ndimage.median_filter(f0, size=5, mode="nearest")
    # bridge one-frame dropouts, kill isolated voiced frames
    voiced_frames = f0 > 0
    voiced_frames = ndimage.binary_closing(voiced_frames, structure=np.ones(3))
    voiced_frames = ndimage.binary_opening(voiced_frames, structure=np.ones(3))
    centre = int(0.015 * SR)
    h = int(hop * SR)

    def period_at(i):
        k = min(len(f0) - 1, max(0, (i - centre) // h))
        f = f0[k]
        if f <= 0:
            near = np.nonzero(f0 > 0)[0]
            f = f0[near[np.argmin(np.abs(near - k))]] if len(near) else 200.0
        return int(round(SR / f))

    def is_voiced(i):
        k = min(len(f0) - 1, max(0, (i - centre) // h))
        return bool(voiced_frames[k])

    lp = dsp.lowpass(x, 1200.0, 2)
    marks, periods, voiced = [], [], []
    i = 0
    unv = int(0.005 * SR)
    while i < len(x):
        if is_voiced(i):
            t = period_at(i)
            if marks and voiced[-1]:
                # waveform similarity: the cycle that best matches the previous one (consistent epochs)
                lo, hi = max(0, i - int(0.2 * t)), min(len(x) - t, i + int(0.2 * t) + 1)
                prev = marks[-1]
                half = t // 2
                if hi <= lo or prev - half < 0 or hi + half >= len(x):
                    break
                ref = lp[prev - half: prev + half]
                region = lp[lo - half: hi + half]
                if len(region) < len(ref):
                    break
                corr = signal.fftconvolve(region, ref[::-1], mode="valid")
                energy = np.sqrt(signal.fftconvolve(region ** 2, np.ones(len(ref)), mode="valid") + 1e-12)
                score = corr / energy
                m = lo + int(np.argmax(score[: hi - lo]))
            else:
                lo, hi = i, min(len(x), i + t)
                if hi <= lo:
                    break
                m = lo + int(np.argmax(lp[lo:hi]))
            if marks and m <= marks[-1]:
                m = marks[-1] + max(1, t // 2)
            marks.append(m)
            periods.append(t)
            voiced.append(True)
            i = m + t
        else:
            marks.append(i)
            periods.append(unv)
            voiced.append(False)
            i += unv
    return np.array(marks), np.array(periods), np.array(voiced)


def psola(x, pitch_ratio, speed, formant=1.0):
    """TD-PSOLA: raise pitch by `pitch_ratio`, play `speed` x faster and scale the formants by `formant` (each
    two-period grain is the vocal tract's response to one glottal pulse, so re-spacing the grains moves the pitch
    and keeps the formants; resampling a grain moves its formants). Unvoiced grains keep their spacing."""
    marks, periods, voiced = pitch_marks(x)
    out_len = int(len(x) / speed) + 1
    pad = int(0.03 * SR)
    y = np.zeros(out_len + 2 * pad)
    wsum = np.zeros_like(y)
    # Use the measured spacing to the next mark as the local period where both marks are voiced (it follows the
    # real cycle length better than the 5 ms pitch track), and start on the first mark so an identity transform
    # puts every grain back where it came from.
    spacing = np.diff(marks, append=marks[-1] + periods[-1])
    both = voiced & np.append(voiced[1:], False)
    ok = both & (spacing > 0.6 * periods) & (spacing < 1.4 * periods)
    periods = np.where(ok, spacing, periods)
    t_out = marks[0] / speed
    while t_out < out_len:
        t_a = t_out * speed
        k = int(np.searchsorted(marks, t_a))
        if k >= len(marks):
            k = len(marks) - 1
        if k > 0 and abs(marks[k - 1] - t_a) < abs(marks[k] - t_a):
            k -= 1
        m, t, v = int(marks[k]), int(periods[k]), bool(voiced[k])
        a, b = max(0, m - t), min(len(x), m + t + 1)
        grain = x[a:b]
        win = np.hanning(2 * t + 3)[1:-1][(a - (m - t)):(a - (m - t)) + len(grain)]
        grain = grain * win
        centre = m - a
        if v and abs(formant - 1.0) > 1e-3 and len(grain) > 8:
            n_new = max(4, int(round(len(grain) / formant)))
            grain = signal.resample(grain, n_new)
            win = signal.resample(win, n_new)
            centre = int(round(centre / formant))
        pos = int(round(t_out)) + pad - centre
        lo = max(0, pos)
        hi = min(len(y), pos + len(grain))
        if hi > lo:
            y[lo:hi] += grain[lo - pos: hi - pos]
            wsum[lo:hi] += win[lo - pos: hi - pos]
        step = t / pitch_ratio if v else t
        t_out += max(1.0, step)
    wsum = np.maximum(wsum, 0.5 * np.median(wsum[wsum > 1e-3])) if np.any(wsum > 1e-3) else np.ones_like(wsum)
    y = y / wsum
    return y[pad: pad + out_len]


def transform(x, pitch_st, speed, formant=1.0, method="psola"):
    """Pitch-shift by `pitch_st` semitones and play `speed` x faster, with formants scaled by `formant`."""
    p = 2.0 ** (pitch_st / 12.0)
    if method == "psola":
        # The formant part of the shift is a plain resample (vari-speed: no artefacts at all); PSOLA only adds the
        # remaining pitch rise with the formants kept, and the remaining speed change.
        q = max(1e-3, formant)
        up, down = _ratio(1.0 / q)
        y = signal.resample_poly(x, up, down) if abs(q - 1.0) > 1e-3 else x
        return psola(y, p / q, speed / q, 1.0)
    if method == "psola_grain":
        return psola(x, p, speed, formant)
    # Resample: rational approximation of 1/p (both pitch and speed rise by p).
    up, down = _ratio(1.0 / p)
    y = signal.resample_poly(x, up, down)
    y = formant_warp(y, p / formant)
    return wsola(y, p / speed)


def _ratio(r, limit=480):
    from fractions import Fraction
    fr = Fraction(r).limit_denominator(limit)
    return fr.numerator, fr.denominator


# ---------------------------------------------------------------- slicing

def trim(x, threshold_db=-42.0, pad=0.01):
    env, h = envelope_db(x, smooth=0.01)
    on = np.where(env > env.max() + threshold_db)[0]
    if not len(on):
        return x
    a = max(0, on[0] * h - int(pad * SR))
    b = min(len(x), (on[-1] + 1) * h + int(pad * SR))
    return x[a:b]


def syllables(x, min_gap=0.055, prominence_db=4.5, floor_db=-30.0, pause_s=0.07):
    """Cut a processed clip into syllables. Returns a list of dicts: start, end (samples), peak_db, run (index of the
    pause-delimited run the syllable belongs to). Boundaries sit at envelope minima between peaks."""
    env, h = envelope_db(x)
    top = env.max()
    peaks, _ = signal.find_peaks(env, prominence=prominence_db, distance=max(1, int(min_gap / 0.005)),
                                 height=top + floor_db)
    if not len(peaks):
        return []
    bounds = [None] * (len(peaks) + 1)
    silent = env < top + floor_db - 6
    # Outer bounds: walk out to where the envelope falls into the floor.
    def walk(i, step):
        while 0 < i < len(env) - 1 and env[i] > top + floor_db - 8:
            i += step
        return i
    bounds[0] = walk(peaks[0], -1)
    bounds[-1] = walk(peaks[-1], +1)
    for i in range(len(peaks) - 1):
        a, b = peaks[i], peaks[i + 1]
        bounds[i + 1] = a + int(np.argmin(env[a:b]))
    out = []
    run = 0
    for i, pk in enumerate(peaks):
        a, b = bounds[i], bounds[i + 1]
        if i > 0:
            # a pause (envelope below the floor for pause_s) between this syllable and the previous starts a new run
            gap = silent[peaks[i - 1]:pk]
            longest = _longest_true(gap) * 0.005
            if longest >= pause_s:
                run += 1
                # end the previous syllable and start this one at the edges of the silence
                a = _silence_edge(env, pk, top + floor_db - 6, -1)
                out[-1]["end"] = min(out[-1]["end"], _silence_edge(env, peaks[i - 1], top + floor_db - 6, +1) * h)
        out.append({"start": a * h, "end": b * h, "peak_db": float(env[pk]), "run": run})
    for s in out:
        s["end"] = max(s["end"], s["start"] + int(0.03 * SR))
    return out


def _longest_true(mask):
    best = cur = 0
    for v in mask:
        cur = cur + 1 if v else 0
        best = max(best, cur)
    return best


def _silence_edge(env, i, level, step):
    while 0 < i < len(env) - 1 and env[i] > level:
        i += step
    return i


def cut(x, s, fade_in=0.004, fade_out=0.012):
    seg = x[s["start"]: s["end"]].copy()
    return dsp.fade(seg, fade_in, fade_out)


# ---------------------------------------------------------------- assembly

def join(pieces, gaps, xfade=0.006):
    """Concatenate pieces; a positive gap inserts silence, a negative one overlaps (crossfade)."""
    out = np.zeros(0)
    for i, p in enumerate(pieces):
        if i == 0:
            out = p.copy()
            continue
        g = gaps[i - 1]
        if g >= 0:
            out = np.concatenate([out, np.zeros(int(g * SR)), p])
        else:
            ov = min(int(-g * SR), len(p) // 2, len(out) // 2)
            ramp = np.linspace(0, np.pi / 2, ov)
            out[-ov:] = out[-ov:] * np.cos(ramp) + p[:ov] * np.sin(ramp)  # equal power: no dip at the join
            out = np.concatenate([out, p[ov:]])
    return out


def runs_of(audio, sylls):
    """Group syllables into pause-delimited runs: [{'audio': processed clip, 'cuts': [syllable end offsets]}]."""
    runs = {}
    for s in sylls:
        runs.setdefault(s["run"], []).append(s)
    out = []
    for group in runs.values():
        a, b = group[0]["start"], group[-1]["end"]
        out.append({"audio": audio[a:b], "cuts": [s["end"] - a for s in group], "starts": [s["start"] - a for s in group]})
    return out


def phrase_from_runs(runs, target_s, rng, gap_s, max_runs=8):
    """Join natural runs (contiguous source speech, so the co-articulation inside each run is kept) to land near
    target_s. A run longer than what is left is cut at a syllable boundary; the phrase always ends on a whole
    syllable with a soft release."""
    target = int(target_s * SR)
    order = list(rng.permutation(len(runs)))
    pieces, total = [], 0
    mean_gap = int(np.mean(gap_s) * SR)
    for idx in order:
        run = runs[idx]
        remaining = target - total
        if remaining < 0.12 * SR or len(pieces) >= max_runs:
            break
        if len(run["audio"]) <= remaining * 1.12:
            pieces.append(dsp.fade(run["audio"], 0.004, 0.02))
            total += len(run["audio"]) + mean_gap
            continue
        # cut at the syllable end closest to the remaining length (at least one syllable)
        ends = np.array(run["cuts"])
        k = int(np.argmin(np.abs(ends - remaining)))
        if ends[k] > remaining * 1.25 and pieces:
            continue
        pieces.append(dsp.fade(run["audio"][: ends[k]], 0.004, 0.03))
        total += int(ends[k]) + mean_gap
    gaps = [float(rng.uniform(*gap_s)) for _ in range(len(pieces) - 1)]
    return join(pieces, gaps)


# ---------------------------------------------------------------- runtime planner (reference for Core)

def caption_syllables(text):
    """Rough spoken-syllable count of a caption (vowel groups per word), at least 1 per word."""
    import re
    words = re.findall(r"[A-Za-zÀ-ÿ']+", text)
    n = 0
    for w in words:
        groups = re.findall(r"[aeiouyàáâãéêíóôõúü]+", w.lower())
        k = len(groups)
        if w.lower().endswith("e") and k > 1 and not w.lower().endswith(("le", "ee")):
            k -= 1
        n += max(1, k)
    return max(1, n)


def plan_sequence(text, bank, syllable_rate, seed, max_s=3.0, min_s=0.6, ratio=0.6):
    """The runtime rule: babble length follows the caption, but shorter than reading it (babble is a flavour, the
    text line carries the words). Returns [(syllable index, gap_after_s, pitch_offset_st)].

    - target = clamp(ratio * syllables / syllable_rate * 1.0 s, min_s, max_s)
    - syllables are drawn from the bank with a seeded shuffle bag (no immediate repeat),
    - commas and full stops in the caption become 90 / 160 ms gaps at the matching point of the babble,
    - a declining melody per breath group (see below); a question ends on the highest syllable raised 2 st,
      an exclamation on the loudest syllable.
    """
    import re
    n_cap = caption_syllables(text)
    target = float(np.clip(ratio * n_cap / syllable_rate, min_s, max_s))
    rng = np.random.default_rng(seed)
    durs = np.array([b["duration_s"] for b in bank])
    mean = float(durs.mean())
    count = max(2, int(round(target / mean)))
    # pause points proportional to the caption's punctuation positions
    words = re.findall(r"[^\s]+", text)
    pause_at = {}
    pos = 0
    for w in words[:-1]:
        pos += max(1, caption_syllables(w))
        if w.endswith((",", ";", ":")):
            pause_at[int(round(pos / n_cap * count))] = 0.09
        elif w.endswith((".", "!", "?", "…")):
            pause_at[int(round(pos / n_cap * count))] = 0.16
    # Melody: each breath group (between pauses) starts a little high and falls (declination); a question rises
    # on its last syllable. Each pick is one of the 3 unused syllables closest to the contour, nudged by at most
    # 1.5 semitones (AudioSource.pitch at runtime).
    f0s = np.array([b["f0_hz"] for b in bank], dtype=float)
    voiced = f0s[f0s > 0]
    med = float(np.median(voiced)) if len(voiced) else 1.0
    f0s[f0s <= 0] = med
    hi, lo = float(np.percentile(f0s, 70)), float(np.percentile(f0s, 35))
    group_start = 0
    bounds = sorted(k for k in pause_at if 0 < k < count) + [count]
    plan, bag, last = [], [], None
    for i in range(count):
        group_end = next(b for b in bounds if b > i)
        if i in pause_at:
            group_start = i
        frac = (i - group_start) / max(1, group_end - group_start - 1)
        target_f0 = hi * (lo / hi) ** frac
        if len(bag) < 3:
            bag = list(rng.permutation(len(bank)))
        options = sorted((k for k in bag if k != last), key=lambda k: abs(np.log(f0s[k] / target_f0)))[:3]
        pick = int(options[int(rng.integers(len(options)))])
        bag.remove(pick)
        last = pick
        nudge = float(np.clip(12 * np.log2(target_f0 / f0s[pick]), -1.5, 1.5))
        plan.append([pick, pause_at.get(i + 1, 0.0), round(nudge, 2)])
    stripped = text.strip()
    if stripped.endswith("?"):
        plan[-1][0] = int(np.argmax(f0s))
        plan[-1][2] = 2.0
    elif stripped.endswith("!"):
        plan[-1][0] = int(np.argmax([b["peak_db"] for b in bank]))
        plan[-1][2] = 0.0
    plan[-1][1] = 0.0
    return [tuple(p) for p in plan]


def seed_for(*parts):
    return zlib.crc32("|".join(str(p) for p in parts).encode())
