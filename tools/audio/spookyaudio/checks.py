"""Automatic checks per sound, WAV output and spectrogram images.

Claude cannot listen, so every take is measured: loudness against its category target, sample and true peak,
clipping, DC offset, sub-bass left after band-limiting, start/end clicks (or the loop seam), duration budget.
A spectrogram PNG (with the waveform above it) is written for visual review.
"""
import struct
import zlib

import numpy as np
from scipy import signal
from scipy.io import wavfile

from . import SAMPLE_RATE as SR
from . import dsp, loudness
from .master import CATEGORIES, CEILING_DBTP


def to_pcm16(x, seed):
    """Float to 16-bit PCM with seeded TPDF dither (deterministic)."""
    rng = np.random.default_rng(seed)
    lsb = 1.0 / 32768.0
    dither = (rng.random(x.shape) - rng.random(x.shape)) * lsb
    return np.clip(np.round((x + dither) * 32767.0), -32768, 32767).astype(np.int16)


def write_wav(path, x, seed):
    wavfile.write(str(path), SR, to_pcm16(x, seed))


def read_wav(path):
    rate, data = wavfile.read(str(path))
    return rate, data.astype(float) / 32768.0


def analyse(x, category_name, budget, loop=False, target_lufs=None):
    """Measure one take. Returns (metrics dict, list of failure strings)."""
    category = CATEGORIES[category_name]
    metrics = {}
    failures = []
    x = np.asarray(x, dtype=float)
    if not np.all(np.isfinite(x)):
        return {"finite": False}, ["non-finite samples"]
    duration = len(x) / SR
    lufs, method = loudness.integrated_lufs(x, with_method=True)
    mono = x if x.ndim == 1 else x.mean(axis=1)
    metrics.update({
        "duration_s": round(duration, 3),
        "channels": 1 if x.ndim == 1 else x.shape[1],
        "lufs": round(lufs, 2),
        "lufs_method": method,
        "momentary_max_lufs": round(loudness.momentary_max_lufs(x), 2),
        "sample_peak_db": round(loudness.sample_peak_db(x), 2),
        "true_peak_dbtp": round(loudness.true_peak_db(x), 2),
        "dc_offset": float(f"{float(np.mean(mono)):.6f}"),
        "clipped_samples": int(np.sum(np.abs(x) >= 0.999)),
    })
    spectrum = np.abs(np.fft.rfft(mono * np.hanning(len(mono)))) ** 2
    freqs = np.fft.rfftfreq(len(mono), 1 / SR)
    sub = spectrum[freqs < category.highpass * 0.5].sum()
    metrics["sub_bass_db"] = round(float(10 * np.log10((sub + 1e-20) / (spectrum.sum() + 1e-20))), 1)
    centroid = float((spectrum * freqs).sum() / (spectrum.sum() + 1e-20))
    metrics["spectral_centroid_hz"] = int(round(centroid))

    target = category.lufs if target_lufs is None else target_lufs
    if abs(lufs - target) > category.tolerance:
        failures.append(f"loudness {lufs:.1f} LUFS, target {target} ±{category.tolerance}")
    if metrics["true_peak_dbtp"] > CEILING_DBTP + 0.05:
        failures.append(f"true peak {metrics['true_peak_dbtp']} dBTP above {CEILING_DBTP}")
    if metrics["clipped_samples"]:
        failures.append(f"{metrics['clipped_samples']} clipped samples")
    if abs(metrics["dc_offset"]) > 1e-3:
        failures.append(f"DC offset {metrics['dc_offset']}")
    if metrics["sub_bass_db"] > -25:
        failures.append(f"sub-bass {metrics['sub_bass_db']} dB of total energy")
    low, high = budget
    if not (low <= duration <= high):
        failures.append(f"duration {duration:.2f} s outside budget {low}–{high} s")
    if loop:
        seam = float(np.max(np.abs(x[0] - x[-1])))
        metrics["loop_seam_jump"] = round(seam, 4)
        if seam > 0.02:
            failures.append(f"loop seam jump {seam:.3f}")
    else:
        edge = float(max(np.max(np.abs(x[:1])), np.max(np.abs(x[-1:]))))
        metrics["edge_level"] = round(edge, 5)
        if edge > 0.005:
            failures.append(f"click risk: edge sample {edge:.3f}")
    return metrics, failures


# ---------------------------------------------------------------------------------------------------------------
# Spectrogram PNG (no plotting library: deterministic pixels, felt-and-paper colours)

_PALETTE = [
    (0.00, (0x1E, 0x1A, 0x26)),   # ink
    (0.30, (0x2A, 0x25, 0x50)),   # night violet
    (0.55, (0x5B, 0x3A, 0x7A)),   # haunted purple
    (0.75, (0xEE, 0x7F, 0x2D)),   # jack-o'-lantern
    (0.90, (0xF6, 0xD3, 0x65)),   # candy corn
    (1.00, (0xF4, 0xE6, 0xC8)),   # bone
]


def _colour(values):
    stops = np.array([p[0] for p in _PALETTE])
    colours = np.array([p[1] for p in _PALETTE], dtype=float)
    out = np.empty(values.shape + (3,))
    for ch in range(3):
        out[..., ch] = np.interp(values, stops, colours[:, ch])
    return out.astype(np.uint8)


def _png(path, rgb):
    height, width, _ = rgb.shape
    raw = b"".join(b"\x00" + rgb[row].tobytes() for row in range(height))

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    with open(path, "wb") as fh:
        fh.write(b"\x89PNG\r\n\x1a\n")
        fh.write(chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)))
        fh.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        fh.write(chunk(b"IEND", b""))


def spectrogram_png(path, x, max_freq=16000.0, height=200, wave_height=48, max_width=640):
    """Waveform strip over a log-frequency spectrogram (60 Hz – max_freq), 80 dB range.

    Faint guide lines mark 100 Hz, 250 Hz (phone-speaker floor), 1 kHz, 4 kHz and 10 kHz.
    """
    mono = x if x.ndim == 1 else x.mean(axis=1)
    nper = 2048
    hop = max(64, int(np.ceil(len(mono) / max_width)))
    padded = np.pad(mono, (nper // 2, nper // 2))
    f, _, z = signal.stft(padded, SR, nperseg=nper, noverlap=nper - min(hop, nper - 1), boundary=None,
                          padded=False)
    mag = 20 * np.log10(np.abs(z) + 1e-9)
    mag -= mag.max()
    frames = mag.shape[1]
    log_f = np.geomspace(60, max_freq, height)[::-1]
    image = np.empty((height, frames))
    for col in range(frames):
        image[:, col] = np.interp(log_f, f, mag[:, col])
    norm = np.clip((image + 80) / 80, 0, 1)
    rgb = _colour(norm)
    for guide in (100, 250, 1000, 4000, 10000):
        row = int(np.argmin(np.abs(log_f - guide)))
        rgb[row, ::3] = (0xF4, 0xE6, 0xC8) if guide == 250 else (0x8D, 0xBF, 0x3F)

    wave = np.full((wave_height, frames, 3), (0xF4, 0xE6, 0xC8), dtype=np.uint8)
    centre = wave_height // 2
    for col in range(frames):
        seg = mono[col * hop:(col + 1) * hop]
        if len(seg) == 0:
            continue
        top = int(round(centre - seg.max() * (centre - 2)))
        bottom = int(round(centre - seg.min() * (centre - 2)))
        wave[max(0, top):min(wave_height, bottom + 1), col] = (0x5B, 0x3A, 0x7A)
    wave[centre, :] = (0x1E, 0x1A, 0x26)
    _png(path, np.concatenate([wave, np.full((2, frames, 3), 0x1E, dtype=np.uint8), rgb], axis=0))
