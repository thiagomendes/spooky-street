"""Phone-speaker mastering: band-limit, gentle compression, loudness normalisation per category, true-peak limit.

A phone speaker reproduces almost nothing below ~250 Hz and distorts when pushed with sub-bass, so every category
rolls off below its high-pass corner; the low "body" of knocks and the floor hum is carried by harmonics that the
speaker can play (the ear fills in the missing fundamental). Loudness targets keep categories balanced against each
other before the in-game mixer applies the player's volume settings.
"""
from dataclasses import dataclass

import numpy as np
from scipy import ndimage, signal

from . import SAMPLE_RATE as SR
from . import dsp, loudness


@dataclass(frozen=True)
class Category:
    name: str
    lufs: float           # integrated (or short-clip) loudness target
    highpass: float       # sub-bass band limit, Hz (4th-order Butterworth)
    lowpass: float        # top band limit, Hz
    ratio: float = 2.0    # gentle compression above the threshold
    threshold: float = -12.0  # dB below the envelope peak
    tolerance: float = 1.0    # LU allowed around the target after limiting


CEILING_DBTP = -1.0

CATEGORIES = {
    # Clue sounds (knocks, hum, cam): the loudest effects, so all three taps stay distinct on a phone speaker.
    "clue": Category("clue", -18.0, 110.0, 16000.0),
    # Movement one-shots (jump, land, glide) sit under the clues.
    "movement": Category("movement", -22.0, 120.0, 17000.0),
    # Footsteps repeat constantly: quieter still.
    "footstep": Category("footstep", -25.0, 120.0, 17000.0),
    # Notebook and transitions: tactile, close, never harsh.
    "notebook": Category("notebook", -21.0, 140.0, 16000.0),
    # UI taps play hundreds of times: soft.
    "ui": Category("ui", -24.0, 160.0, 15000.0),
    # Babble voices carry character next to captions.
    "voice": Category("voice", -19.0, 130.0, 14000.0, ratio=2.5, threshold=-10.0),
    # The night bed stays well under everything (spec §6: ambient life never competes with evidence).
    "ambience": Category("ambience", -28.0, 90.0, 15000.0, ratio=1.5, threshold=-8.0),
    # Music motif.
    "music": Category("music", -20.0, 90.0, 16000.0, ratio=1.8),
    # The living street's own sounds (doorbells): at the notebook's level, under the clues, so a bell by a door never
    # competes with the case's knocks.
    "street": Category("street", -21.0, 140.0, 15000.0),
}


def band_limit(x, category):
    y = dsp.highpass(x, category.highpass, 4)
    return dsp.lowpass(y, category.lowpass, 4)


def compress(x, category, attack=0.004, release=0.09):
    """Feed-forward compressor with a soft knee; threshold is relative to the signal's own envelope peak."""
    mono = np.max(np.abs(x), axis=1) if x.ndim == 2 else np.abs(x)
    a_att = np.exp(-1.0 / (attack * SR))
    env = signal.lfilter([1 - a_att], [1, -a_att], mono ** 2)
    env = np.sqrt(np.maximum(env, 0))
    level = dsp.db(env)
    threshold = level.max() + category.threshold
    knee = 6.0
    over = level - threshold
    gain_db = np.where(over <= -knee / 2, 0.0,
                       np.where(over >= knee / 2, -(over) * (1 - 1 / category.ratio),
                                -((over + knee / 2) ** 2) / (2 * knee) * (1 - 1 / category.ratio)))
    a_rel = np.exp(-1.0 / (release * SR))
    gain_db = signal.lfilter([1 - a_rel], [1, -a_rel], gain_db)
    gain = dsp.from_db(gain_db)
    return x * (gain[:, None] if x.ndim == 2 else gain)


def limit(x, ceiling_db, lookahead=0.0015):
    """Look-ahead brickwall limiter: the gain reaches its minimum before each over-ceiling peak."""
    mono = np.max(np.abs(x), axis=1) if x.ndim == 2 else np.abs(x)
    ceiling = dsp.from_db(ceiling_db)
    needed = np.minimum(1.0, ceiling / np.maximum(mono, 1e-12))
    window = max(3, dsp.samples(lookahead))
    held = ndimage.minimum_filter1d(needed, size=2 * window + 1, mode="nearest")
    kernel = np.hanning(window + 2)[1:-1]
    kernel /= kernel.sum()
    gain = np.minimum(signal.fftconvolve(held, kernel, mode="same"), needed)
    release = np.exp(-1.0 / (0.05 * SR))
    gain = 1.0 - signal.lfilter([1 - release], [1, -release], 1.0 - gain)
    gain = np.minimum(gain, needed)
    return x * (gain[:, None] if x.ndim == 2 else gain)


def _circular(process, x):
    """Run a causal process on [x x x] and keep the middle copy, so a loop has no filter transient at its seam."""
    tripled = np.concatenate([x, x, x], axis=0)
    return process(tripled)[len(x):2 * len(x)]


def master(x, category_name, loop=False, lufs_offset=0.0):
    """Master one take to its category target (+ an optional per-sound offset). Returns (audio, info dict);
    info['limiter_db'] is how far the true peak would have overshot the ceiling without the limiter."""
    category = CATEGORIES[category_name]
    target = category.lufs + lufs_offset
    x = np.asarray(x, dtype=float)
    x = x - np.mean(x, axis=0)

    def chain(sig):
        return compress(band_limit(sig, category), category)

    y = _circular(chain, x) if loop else chain(x)
    y = y - np.mean(y, axis=0)
    if not loop:
        y = dsp.fade(y, 0.001, 0.006)

    gain_db = target - loudness.integrated_lufs(y)
    limiter_db = max(0.0, loudness.true_peak_db(y * dsp.from_db(gain_db)) - CEILING_DBTP)
    out = y
    for _ in range(6):
        scaled = y * dsp.from_db(gain_db)
        out = _circular(lambda s: limit(s, CEILING_DBTP - 0.4), scaled) if loop else limit(scaled, CEILING_DBTP - 0.4)
        tp = loudness.true_peak_db(out)
        if tp > CEILING_DBTP:
            out = out * dsp.from_db(CEILING_DBTP - 0.05 - tp)
        measured = loudness.integrated_lufs(out)
        error = target - measured
        if abs(error) < 0.1:
            break
        gain_db += error
    info = {"category": category.name, "target_lufs": target, "gain_db": round(float(gain_db), 2),
            "limiter_db": round(limiter_db, 1)}
    return out, info
