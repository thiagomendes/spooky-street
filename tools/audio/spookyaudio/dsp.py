"""DSP building blocks: envelopes, noise, filters, oscillators, modal banks and synthetic reverbs.

All functions work on float64 numpy arrays at `SAMPLE_RATE`. Mono signals are 1-D; stereo signals are (n, 2).
"""
import zlib

import numpy as np
from scipy import signal

from . import SAMPLE_RATE as SR


# ---------------------------------------------------------------------------------------------------------------
# Seeds and time

def seed_for(*parts):
    """A stable 32-bit seed from any parts (sound id, take...). crc32 is identical on every platform."""
    return zlib.crc32("|".join(str(p) for p in parts).encode("utf-8"))


def rng_for(*parts):
    return np.random.default_rng(seed_for(*parts))


def samples(seconds):
    return max(1, int(round(seconds * SR)))


def timeline(n):
    return np.arange(n) / SR


def silence(seconds):
    return np.zeros(samples(seconds))


def place(buffer, sound, at_seconds, gain=1.0):
    """Mix `sound` into `buffer` starting at `at_seconds` (clipped to the buffer)."""
    start = int(round(at_seconds * SR))
    if start >= len(buffer):
        return buffer
    end = min(len(buffer), start + len(sound))
    if start < 0:
        sound = sound[-start:]
        start = 0
        end = min(len(buffer), len(sound))
    buffer[start:end] += gain * sound[:end - start]
    return buffer


def mix(*parts):
    """Sum signals of different lengths (each a (gain, signal) pair or a bare signal), padding the shorter ones."""
    pairs = [p if isinstance(p, tuple) else (1.0, p) for p in parts]
    out = np.zeros(max(len(sig) for _, sig in pairs))
    for gain, sig in pairs:
        out[:len(sig)] += gain * sig
    return out


def place_wrapped(buffer, sound, at_seconds, gain=1.0):
    """Mix `sound` into a loop buffer, wrapping past the end (seamless loops)."""
    n = len(buffer)
    start = int(round(at_seconds * SR)) % n
    idx = (start + np.arange(len(sound))) % n
    np.add.at(buffer, idx, gain * sound)
    return buffer


def db(x):
    return 20.0 * np.log10(np.maximum(np.abs(x), 1e-12))


def from_db(d):
    return 10.0 ** (np.asarray(d) / 20.0)


# ---------------------------------------------------------------------------------------------------------------
# Envelopes

def fade(x, fade_in=0.002, fade_out=0.004):
    """Raised-cosine fades that remove start/end clicks."""
    y = np.array(x, dtype=float, copy=True)
    for length, sl in ((samples(fade_in), slice(None)), (samples(fade_out), slice(None, None, -1))):
        length = min(length, len(y))
        if length <= 1:
            continue
        ramp = 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, length))
        view = y[sl]
        if view.ndim == 1:
            view[:length] *= ramp
        else:
            view[:length] *= ramp[:, None]
    return y


def exp_decay(n, tau):
    return np.exp(-timeline(n) / max(tau, 1e-5))


def attack_decay(n, attack, tau, curve=2.0):
    """Smooth power-curve attack then exponential decay."""
    t = timeline(n)
    a = np.clip(t / max(attack, 1e-5), 0, 1) ** curve
    return a * np.exp(-np.maximum(t - attack, 0) / max(tau, 1e-5))


def hann_pulse(width):
    n = max(3, samples(width))
    w = np.hanning(n)
    return w / w.sum()


def segment_envelope(points, n):
    """Piecewise-linear envelope through (time, value) points, held at the ends."""
    times, values = zip(*points)
    return np.interp(timeline(n), times, values)


def smooth(x, seconds):
    """One-pole low-pass smoothing (time constant in seconds), zero-phase."""
    if seconds <= 0:
        return x
    a = np.exp(-1.0 / (seconds * SR))
    return signal.filtfilt([1 - a], [1, -a], x, padlen=min(len(x) - 1, 3 * samples(seconds)))


# ---------------------------------------------------------------------------------------------------------------
# Noise

def white(rng, n):
    return rng.standard_normal(n)


def coloured(rng, n, slope_db_per_octave):
    """Noise with a spectral slope (-3 = pink, -6 = brown), made in the frequency domain (periodic: loops cleanly)."""
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    f[0] = f[1]
    spec *= (f / 1000.0) ** (slope_db_per_octave / 6.0206)
    out = np.fft.irfft(spec, n)
    return out / (np.std(out) + 1e-12)


def spectral_shape(x, points):
    """Shape a signal's spectrum through (frequency Hz, gain dB) points, interpolated on a log axis. Periodic."""
    n = len(x)
    spec = np.fft.rfft(x)
    f = np.maximum(np.fft.rfftfreq(n, 1 / SR), 1.0)
    fs, gs = zip(*points)
    gain = np.interp(np.log(f), np.log(np.maximum(fs, 1.0)), gs)
    return np.fft.irfft(spec * from_db(gain), n)


def grains(rng, n, rate, grain, seconds_density=None):
    """Sparse random impulses (Poisson, `rate` per second) each convolved with `grain`; amplitude random."""
    impulses = np.zeros(n)
    density = np.full(n, rate / SR) if seconds_density is None else seconds_density * rate / SR
    hits = rng.random(n) < density
    impulses[hits] = rng.standard_normal(hits.sum()) * rng.uniform(0.3, 1.0, hits.sum())
    return signal.fftconvolve(impulses, grain)[:n]


# ---------------------------------------------------------------------------------------------------------------
# Filters

def _sos(kind, freq, order):
    nyq = SR / 2
    if isinstance(freq, (tuple, list)):
        freq = [min(max(f, 5.0), nyq * 0.98) for f in freq]
    else:
        freq = min(max(freq, 5.0), nyq * 0.98)
    return signal.butter(order, freq, btype=kind, fs=SR, output="sos")


def highpass(x, freq, order=2):
    return signal.sosfilt(_sos("highpass", freq, order), x, axis=0)


def lowpass(x, freq, order=2):
    return signal.sosfilt(_sos("lowpass", freq, order), x, axis=0)


def bandpass(x, lo, hi, order=2):
    return signal.sosfilt(_sos("bandpass", (lo, hi), order), x, axis=0)


def peaking(x, freq, gain_db, q=1.0):
    """RBJ peaking EQ."""
    a = 10 ** (gain_db / 40)
    w = 2 * np.pi * freq / SR
    alpha = np.sin(w) / (2 * q)
    b = [1 + alpha * a, -2 * np.cos(w), 1 - alpha * a]
    den = [1 + alpha / a, -2 * np.cos(w), 1 - alpha / a]
    return signal.lfilter(np.array(b) / den[0], np.array(den) / den[0], x, axis=0)


def resonator_coeffs(freq, bandwidth):
    """Klatt two-pole resonator with unity gain at DC: y = A x + B y1 + C y2."""
    r = np.exp(-np.pi * bandwidth / SR)
    b = 2 * r * np.cos(2 * np.pi * freq / SR)
    c = -r * r
    return 1 - b - c, b, c


def time_varying_resonators(x, tracks, block=32):
    """Cascade of resonators whose (freq, bandwidth) tracks change over time (one value per sample).

    `tracks` is a list of (freq_array, bw_array). Coefficients update every `block` samples; filter state carries
    across blocks, so the cascade glides smoothly between targets (formant transitions).
    """
    y = np.array(x, dtype=float)
    n = len(y)
    for freqs, bws in tracks:
        out = np.empty(n)
        zi = np.zeros(2)
        for start in range(0, n, block):
            end = min(n, start + block)
            mid = (start + end) // 2
            a_gain, b, c = resonator_coeffs(freqs[mid], bws[mid])
            out[start:end], zi = signal.lfilter([a_gain], [1.0, -b, -c], y[start:end], zi=zi)
        y = out
    return y


def tilt(x, db_per_octave, pivot=1000.0):
    """Gentle spectral tilt around a pivot (frequency domain, for colouring noise)."""
    return spectral_shape(x, [(pivot / 16, -4 * db_per_octave), (pivot, 0), (pivot * 16, 4 * db_per_octave)])


# ---------------------------------------------------------------------------------------------------------------
# Oscillators and modal synthesis

def phase_from_freq(freq):
    """Integrate an instantaneous-frequency array into phase (radians)."""
    return 2 * np.pi * np.cumsum(freq) / SR


def sine_glide(freq, amp=1.0):
    return amp * np.sin(phase_from_freq(freq))


def modal_bank(n, modes, rng=None, jitter=0.0):
    """Sum of exponentially decaying sinusoids: modes = [(freq, tau seconds, amplitude), ...].

    `jitter` detunes each mode by up to that fraction and randomises its phase (needs `rng`), so takes differ like
    two knocks on the same board.
    """
    t = timeline(n)
    out = np.zeros(n)
    for freq, tau, amp in modes:
        if freq >= SR * 0.45:
            continue
        phase = 0.0
        if rng is not None:
            freq *= 1 + rng.uniform(-jitter, jitter)
            if jitter > 0:
                amp *= 1 + rng.uniform(-0.25, 0.25)
            phase = rng.uniform(0, 2 * np.pi) * 0.25
        out += amp * np.exp(-t / tau) * np.sin(2 * np.pi * freq * t + phase)
    return out


def strike(modes_ir, hardness_width):
    """Excite a modal impulse response with a raised-cosine contact pulse: a wider pulse is a softer mallet."""
    return signal.fftconvolve(modes_ir, hann_pulse(hardness_width))[:len(modes_ir)]


# ---------------------------------------------------------------------------------------------------------------
# Space

def reverb_ir(rng, rt60, length, bands=((0, 1.0), (1500, 0.7), (5000, 0.4)), predelay=0.0, early=(),
              stereo=False, density_ramp=0.02):
    """A synthetic room impulse response: band-split decaying noise (highs die faster) plus early reflections.

    bands: (low edge Hz, decay-time factor). early: [(delay s, gain)]. Mono (n,) or decorrelated stereo (n, 2).
    """
    n = samples(length)
    t = timeline(n)
    channels = []
    for _ in range(2 if stereo else 1):
        tail = np.zeros(n)
        edges = [b[0] for b in bands] + [SR / 2 * 0.98]
        for i, (lo, factor) in enumerate(bands):
            hi = edges[i + 1]
            noise = rng.standard_normal(n)
            if lo <= 0:
                part = lowpass(noise, hi, 4)
            else:
                part = bandpass(noise, lo, hi, 3)
            decay = np.exp(-6.907755 * t / (rt60 * factor))
            tail += part * decay
        tail *= np.clip(t / max(density_ramp, 1e-4), 0, 1) ** 2
        ir = np.zeros(n)
        pre = samples(predelay) if predelay > 0 else 0
        ir[pre:] = tail[:n - pre]
        ir /= np.sqrt(np.sum(ir ** 2)) + 1e-12
        for delay, gain in early:
            k = samples(delay)
            if k < n:
                ir[k] += gain * (1 + 0.15 * rng.standard_normal())
        channels.append(ir)
    return np.stack(channels, axis=1) if stereo else channels[0]


def convolve(x, ir, wet=0.3, dry=1.0):
    """Mix a dry signal with its convolution by `ir`; output grows by the IR length (tails are kept)."""
    n = len(x) + (len(ir) - 1)
    if ir.ndim == 1:
        wet_sig = signal.fftconvolve(x, ir)
        out = np.zeros(n)
        out[:len(x)] += dry * x
        return out + wet * wet_sig
    out = np.zeros((n, 2))
    for ch in range(2):
        out[:len(x), ch] += dry * (x if x.ndim == 1 else x[:, ch])
        src = x if x.ndim == 1 else x[:, ch]
        out[:, ch] += wet * signal.fftconvolve(src, ir[:, ch])
    return out


def circular_convolve(x, ir):
    """Convolution that wraps around the loop length (so a loop's reverb tail feeds its own start)."""
    n = len(x)
    ir = ir[:n] if len(ir) > n else np.pad(ir, (0, n - len(ir)))
    return np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(ir), n)


def trim_tail(x, threshold_db=-70.0, keep=0.01):
    """Drop the near-silent end of a signal (relative to its peak) and keep a short margin."""
    mono = np.abs(x) if x.ndim == 1 else np.max(np.abs(x), axis=1)
    peak = mono.max() + 1e-12
    above = np.nonzero(mono > peak * from_db(threshold_db))[0]
    if len(above) == 0:
        return x
    end = min(len(x), above[-1] + samples(keep))
    return x[:end]


def stereo(mono, delay=0.0):
    """Mono to stereo with an optional small delay on the right channel."""
    right = np.concatenate([np.zeros(samples(delay)), mono])[:len(mono)] if delay > 0 else mono
    return np.stack([mono, right], axis=1)
