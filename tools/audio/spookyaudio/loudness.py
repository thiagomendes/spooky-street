"""Loudness and peak metering: ITU-R BS.1770-4 integrated loudness (LUFS) and 4x-oversampled true peak.

Short clips: BS.1770 gates over 400 ms blocks, which a 60 ms UI tap cannot fill. For clips shorter than one
block the meter uses the ungated K-weighted mean square of the whole clip ("short-clip loudness"), the usual
practice for game one-shots; `integrated_lufs` reports which method it used.
"""
import numpy as np
from scipy import signal

from . import SAMPLE_RATE as SR

# BS.1770-4 K-weighting coefficients at 48 kHz (pre-filter shelf, then RLB high-pass).
_SHELF_B = [1.53512485958697, -2.69169618940638, 1.19839281085285]
_SHELF_A = [1.0, -1.69065929318241, 0.73248077421585]
_RLB_B = [1.0, -2.0, 1.0]
_RLB_A = [1.0, -1.99004745483398, 0.99007225036621]

BLOCK = 0.4
OVERLAP = 0.75
ABSOLUTE_GATE = -70.0
RELATIVE_GATE = -10.0


def _channels(x):
    x = np.asarray(x, dtype=float)
    return x[:, None] if x.ndim == 1 else x


def k_weight(x):
    x = _channels(x)
    y = signal.lfilter(_SHELF_B, _SHELF_A, x, axis=0)
    return signal.lfilter(_RLB_B, _RLB_A, y, axis=0)


def _to_lufs(power):
    return -0.691 + 10 * np.log10(max(power, 1e-20))


def integrated_lufs(x, with_method=False):
    """Integrated loudness in LUFS (mono counts as one channel, stereo as L+R with weight 1)."""
    y = k_weight(x)
    n = len(y)
    block = int(BLOCK * SR)
    if n < block:
        value = _to_lufs(float(np.sum(np.mean(y ** 2, axis=0))))
        return (value, "short-clip") if with_method else value
    hop = int(block * (1 - OVERLAP))
    starts = range(0, n - block + 1, hop)
    powers = np.array([np.sum(np.mean(y[s:s + block] ** 2, axis=0)) for s in starts])
    loud = np.array([_to_lufs(p) for p in powers])
    gated = powers[loud > ABSOLUTE_GATE]
    if len(gated) == 0:
        return (-120.0, "gated") if with_method else -120.0
    relative = _to_lufs(float(np.mean(gated))) + RELATIVE_GATE
    final = powers[(loud > ABSOLUTE_GATE) & (loud > relative)]
    value = _to_lufs(float(np.mean(final)))
    return (value, "bs1770") if with_method else value


def momentary_max_lufs(x):
    """Highest 400 ms (momentary) loudness, or the short-clip loudness for shorter clips."""
    y = k_weight(x)
    block = int(BLOCK * SR)
    if len(y) < block:
        return _to_lufs(float(np.sum(np.mean(y ** 2, axis=0))))
    hop = block // 10
    return max(_to_lufs(float(np.sum(np.mean(y[s:s + block] ** 2, axis=0))))
               for s in range(0, len(y) - block + 1, hop))


def sample_peak_db(x):
    return float(20 * np.log10(max(np.max(np.abs(x)), 1e-12)))


def true_peak_db(x):
    """4x-oversampled peak (dBTP), per BS.1770-4 Annex 2."""
    up = signal.resample_poly(_channels(x), 4, 1, axis=0)
    return float(20 * np.log10(max(np.max(np.abs(up)), 1e-12)))
