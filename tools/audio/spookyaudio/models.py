"""Physical and spectral sound models: wood (modal), cloth and air (shaped noise), paper (crackle grains),
footsteps, mallet bars and plucked strings. Each model takes a numpy Generator so takes vary but stay seeded.
"""
import numpy as np

from . import SAMPLE_RATE as SR
from . import dsp


# ---------------------------------------------------------------------------------------------------------------
# Wood

# Mode ratios of a smallish wooden panel (irregular plate modes), measured-style, not harmonic.
PANEL_RATIOS = (1.0, 1.59, 2.13, 2.87, 3.62, 4.51, 5.78, 7.27, 9.04, 11.6, 14.3)


def wood_modes(base, ratios=PANEL_RATIOS, tau_low=0.09, tau_high=0.008, brightness=0.7, emphasis=(500, 1400)):
    """Modes for a wooden body: decay shortens and amplitude falls with frequency; `emphasis` band is lifted
    (the 'tok' region a phone speaker plays well)."""
    modes = []
    count = len(ratios)
    for i, ratio in enumerate(ratios):
        freq = base * ratio
        frac = i / max(1, count - 1)
        tau = tau_low * (tau_high / tau_low) ** frac
        amp = (1.0 / (1 + i)) ** (1.0 - brightness * 0.6)
        if emphasis[0] <= freq <= emphasis[1]:
            amp *= 1.8
        modes.append((freq, tau, amp))
    return modes


def wood_strike(rng, base, length=0.4, hardness=0.0012, brightness=0.7, tau_low=0.09, tau_high=0.008,
                jitter=0.03, contact_noise=0.15, cavity=None, emphasis=(500, 1400)):
    """A knock or tap on wood: modal response struck by a contact pulse plus a short contact noise.

    hardness: contact pulse width in seconds (knuckle ≈ 1–1.5 ms, hard cam lobe ≈ 0.3 ms).
    cavity: optional (freq, tau, amp) for a hollow box resonance (the 'knock on a hollow door' body).
    """
    n = dsp.samples(length)
    modes = wood_modes(base, tau_low=tau_low, tau_high=tau_high, brightness=brightness, emphasis=emphasis)
    if cavity:
        modes.append(cavity)
    ir = dsp.modal_bank(n, modes, rng=rng, jitter=jitter)
    body = dsp.strike(ir, hardness)
    body /= np.max(np.abs(body)) + 1e-12
    click_len = dsp.samples(0.006)
    click = dsp.bandpass(dsp.white(rng, click_len), 1800, 7000, 2) * dsp.exp_decay(click_len, 0.0012)
    click /= np.max(np.abs(click)) + 1e-12
    out = body.copy()
    out[:click_len] += contact_noise * click
    return out


def knuckle_knock(rng, weight=1.0):
    """A knock on a hollow wooden door/board: panel modes, a hollow cavity body, knuckle-soft contact."""
    base = 168 * (1 + rng.uniform(-0.02, 0.02))
    k = wood_strike(rng, base, length=0.45, hardness=0.0011 + 0.0003 * (1 - weight), brightness=0.65,
                    tau_low=0.055 * (0.8 + 0.3 * weight), tau_high=0.008, cavity=(265, 0.035, 1.2),
                    contact_noise=0.12, emphasis=(450, 1300))
    return k * weight


def hard_tap(rng, base=430.0, weight=1.0):
    """Hard wood on hard wood (the cam lobe on its follower plate): short, bright, tight."""
    tap = wood_strike(rng, base * (1 + rng.uniform(-0.015, 0.015)), length=0.3, hardness=0.00032,
                      brightness=0.9, tau_low=0.05, tau_high=0.004, contact_noise=0.25, emphasis=(800, 2600),
                      cavity=(310, 0.035, 0.6))
    return tap * weight


def creak(rng, length=0.12, f_start=520.0, f_end=640.0, roughness=60.0):
    """A small, friendly wood creak: stick-slip pulses at a gliding rate through wood resonances."""
    n = dsp.samples(length)
    rate = np.linspace(f_start, f_end, n) * (1 + 0.04 * dsp.lowpass(dsp.white(rng, n), roughness, 1) * 5)
    phase = np.cumsum(rate) / SR
    pulses = np.diff(np.floor(phase), prepend=0.0)
    pulses *= rng.uniform(0.6, 1.0, n)
    res = dsp.modal_bank(dsp.samples(0.02), [(1200, 0.004, 1.0), (2100, 0.003, 0.6), (3300, 0.002, 0.3)])
    out = np.convolve(pulses, res)[:n]
    env = np.sin(np.linspace(0, np.pi, n)) ** 1.5
    return out * env / (np.max(np.abs(out)) + 1e-12)


# ---------------------------------------------------------------------------------------------------------------
# Paper, adhesive and leaves: crackle grains

def crackle(rng, n, density, freq_range=(1800, 7000), tau_range=(0.0005, 0.002), amp_spread=(0.2, 1.0)):
    """Sum of tiny damped-sine grains. `density` is grains per second, as a scalar or a per-sample array."""
    dens = np.broadcast_to(np.asarray(density, dtype=float), (n,))
    expected = dens / SR
    hits = np.nonzero(rng.random(n) < expected)[0]
    out = np.zeros(n + dsp.samples(0.02))
    for at in hits:
        freq = np.exp(rng.uniform(np.log(freq_range[0]), np.log(freq_range[1])))
        tau = rng.uniform(*tau_range)
        length = dsp.samples(tau * 6)
        t = np.arange(length) / SR
        grain = np.exp(-t / tau) * np.sin(2 * np.pi * freq * t + rng.uniform(0, np.pi))
        out[at:at + length] += rng.uniform(*amp_spread) * grain
    return out[:n]


def airflow(rng, n, lo, hi, order=2):
    """Band-limited noise for air and cloth movement."""
    return dsp.bandpass(dsp.coloured(rng, n, -2.0), lo, hi, order)


def swept_noise(rng, n, f_start, f_end, q=1.2):
    """Noise through a resonant band whose centre glides (a whoosh)."""
    centre = np.geomspace(f_start, f_end, n)
    src = dsp.white(rng, n)
    bw = centre / q
    out = dsp.time_varying_resonators(src, [(centre, bw), (centre * 1.02, bw * 1.5)], block=64)
    return out / (np.max(np.abs(out)) + 1e-12)


# ---------------------------------------------------------------------------------------------------------------
# Cloth

def flutter_envelope(rng, n, rate, rate_spread=0.18, attack=0.003, decay=0.028, amp_spread=(0.45, 1.0)):
    """Irregular flap pulses: cloth in airflow slaps at a rough rate with random strength."""
    env = np.zeros(n)
    rates = np.broadcast_to(np.asarray(rate, dtype=float), (n,))
    t = rng.uniform(0, 1 / rates[0])
    pulse_len = dsp.samples(attack + decay * 5)
    tt = np.arange(pulse_len) / SR
    pulse = np.clip(tt / attack, 0, 1) * np.exp(-np.maximum(tt - attack, 0) / decay)
    while True:
        k = int(t * SR)
        if k >= n:
            break
        end = min(n, k + pulse_len)
        env[k:end] += rng.uniform(*amp_spread) * pulse[:end - k]
        t += (1 / rates[k]) * (1 + rng.uniform(-rate_spread, rate_spread))
    return env


def cloth_flutter(rng, length, rate=13.0, lo=350.0, hi=3200.0, air=0.35):
    """A cape fluttering in the air: flap-modulated noise plus a soft airflow bed."""
    n = dsp.samples(length)
    flaps = flutter_envelope(rng, n, rate)
    flap_noise = dsp.bandpass(dsp.white(rng, n), lo, hi, 2) * flaps
    flap_noise = dsp.peaking(flap_noise, 900, 3.0, 0.8)
    bed = airflow(rng, n, 200, 1400) * air
    out = flap_noise + bed * (0.6 + 0.4 * flaps / (flaps.max() + 1e-9))
    out = dsp.lowpass(out, 5500, 2)
    return out / (np.max(np.abs(out)) + 1e-12)


def cloth_snap(rng, length=0.09):
    """The cape opening: a quick 'fwup' (descending whoosh with a soft slap)."""
    n = dsp.samples(length)
    sweep = swept_noise(rng, n, 3200, 700, q=1.5)
    env = dsp.attack_decay(n, 0.008, 0.03)
    slap = dsp.lowpass(dsp.white(rng, n), 2200, 2) * dsp.attack_decay(n, 0.002, 0.012)
    out = sweep * env + 0.6 * slap / (np.max(np.abs(slap)) + 1e-12)
    return out / (np.max(np.abs(out)) + 1e-12)


# ---------------------------------------------------------------------------------------------------------------
# Footsteps (Pip: a light child in soft costume shoes)

def _scuff(rng, length, lo, hi, attack=0.004, tau=0.02):
    n = dsp.samples(length)
    s = dsp.bandpass(dsp.white(rng, n), lo, hi, 2) * dsp.attack_decay(n, attack, tau)
    return s / (np.max(np.abs(s)) + 1e-12)


def footstep_wood(rng, creaky=False):
    """Heel then toe on a porch plank: hollow plank modes, soft sole contact, a whisper of scuff."""
    length = 0.32
    n = dsp.samples(length)
    out = np.zeros(n)
    base = 205 * (1 + rng.uniform(-0.05, 0.05))
    plank = (1.0, 2.31, 3.86, 5.42, 7.1, 9.3)

    def contact(weight, hardness):
        ir = dsp.modal_bank(dsp.samples(0.2), [(base * r, 0.03 / (1 + 0.7 * i), 1 / (1 + 0.5 * i))
                                                for i, r in enumerate(plank)] + [(310, 0.02, 0.9)],
                            rng=rng, jitter=0.04)
        hit = dsp.strike(ir, hardness)
        hit /= np.max(np.abs(hit)) + 1e-12
        # the plank's broadband knock: a short noise burst through a woody band
        m = dsp.samples(0.05)
        knock = dsp.bandpass(dsp.white(rng, m), 300, 2500, 2) * dsp.attack_decay(m, 0.002, 0.009)
        knock /= np.max(np.abs(knock)) + 1e-12
        return weight * dsp.mix(hit, (0.55, knock))

    heel = contact(1.0, 0.0035)
    dsp.place(out, heel, 0.0)
    toe_at = rng.uniform(0.055, 0.075)
    dsp.place(out, contact(rng.uniform(0.35, 0.5), 0.0025), toe_at)
    dsp.place(out, _scuff(rng, 0.06, 1200, 4500, 0.006, 0.015), 0.004, 0.3)
    dsp.place(out, _scuff(rng, 0.05, 1500, 5000, 0.004, 0.012), toe_at + 0.01, 0.22)
    if creaky:
        dsp.place(out, creak(rng, 0.11, rng.uniform(430, 520), rng.uniform(560, 650)), toe_at + 0.02, 0.16)
    return dsp.fade(out, 0.0005, 0.03)


def footstep_grass(rng):
    """A foot pressing into lawn: dense brushy blade crackle in two waves (heel, roll) and a soft earth thud."""
    length = 0.3
    n = dsp.samples(length)
    t = dsp.timeline(n)
    roll = rng.uniform(0.06, 0.09)
    density = 9000 * (np.exp(-((t - 0.025) / 0.03) ** 2) + 0.7 * np.exp(-((t - roll - 0.03) / 0.04) ** 2))
    blades = crackle(rng, n, density * 0.7, freq_range=(1200, 5000), tau_range=(0.0005, 0.0018))
    blades = dsp.lowpass(dsp.highpass(blades, 900, 2), 6500, 2)
    brush = dsp.bandpass(dsp.white(rng, n), 1200, 5000, 2) * (density / density.max()) * 0.5
    thud = dsp.lowpass(dsp.white(rng, n), 600, 2) * dsp.attack_decay(n, 0.006, 0.025)
    out = blades / (np.max(np.abs(blades)) + 1e-12) + 0.6 * brush / (np.max(np.abs(brush)) + 1e-12)
    out += 0.45 * thud / (np.max(np.abs(thud)) + 1e-12)
    return dsp.fade(dsp.lowpass(out, 7000, 2), 0.0005, 0.04)


def footstep_stone(rng):
    """A soft sole on paving stone: a crisp heel tick with a tight resonance, a little grit, a quiet thud."""
    length = 0.26
    n = dsp.samples(length)
    out = np.zeros(n)
    f = rng.uniform(1700, 2300)

    def tick(weight):
        ir = dsp.modal_bank(dsp.samples(0.05), [(f, 0.006, 1.0), (f * 1.73, 0.004, 0.6), (f * 2.6, 0.003, 0.35),
                                                (480, 0.012, 0.5)], rng=rng, jitter=0.05)
        hit = dsp.strike(ir, 0.0009)
        return weight * hit / (np.max(np.abs(hit)) + 1e-12)

    dsp.place(out, tick(1.0), 0.0)
    toe_at = rng.uniform(0.05, 0.07)
    dsp.place(out, tick(rng.uniform(0.3, 0.45)), toe_at)
    t = dsp.timeline(n)
    grit_density = 1800 * (np.exp(-t / 0.03) + 0.6 * np.exp(-np.maximum(t - toe_at, 0) / 0.03) * (t > toe_at))
    grit = crackle(rng, n, grit_density, freq_range=(3500, 10000), tau_range=(0.0002, 0.0007))
    dsp.place(out, 0.35 * grit / (np.max(np.abs(grit)) + 1e-12), 0.0)
    thud = dsp.lowpass(dsp.white(rng, n), 500, 2) * dsp.attack_decay(n, 0.003, 0.015)
    out += 0.3 * thud / (np.max(np.abs(thud)) + 1e-12)
    dsp.place(out, _scuff(rng, 0.07, 2000, 7000, 0.01, 0.02), toe_at, 0.22)
    return dsp.fade(out, 0.0003, 0.03)


# ---------------------------------------------------------------------------------------------------------------
# Tuned instruments: mallet bars and plucked strings

def note_hz(name):
    """'A4' style note names (sharps with #) to Hz, A4 = 440."""
    names = {"C": -9, "C#": -8, "D": -7, "D#": -6, "E": -5, "F": -4, "F#": -3, "G": -2, "G#": -1, "A": 0,
             "A#": 1, "B": 2}
    pitch, octave = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((names[pitch] + 12 * (octave - 4)) / 12)


def mallet_bar(rng, freq, length=1.6, softness=0.0025, kind="marimba"):
    """A struck tuned bar. marimba: wood bar (1 : 3.93 : 9.0) with its resonator tube; celesta: a soft metal
    bar (1 : 2.76 : 5.40) heard as a gentle chime."""
    n = dsp.samples(length)
    if kind == "marimba":
        modes = [(freq, 0.55 * (440 / freq) ** 0.3, 1.0), (freq * 3.93, 0.09, 0.32), (freq * 9.0, 0.025, 0.1),
                 (freq * 2.0, 0.3, 0.05)]
    else:
        modes = [(freq, 0.9, 1.0), (freq * 2.76, 0.25, 0.18), (freq * 5.40, 0.08, 0.06),
                 (freq * 2.0, 0.6, 0.12), (freq * 1.003, 0.85, 0.4)]
    ir = dsp.modal_bank(n, modes, rng=rng, jitter=0.002)
    out = dsp.strike(ir, softness)
    return dsp.fade(out / (np.max(np.abs(out)) + 1e-12), 0.0, length * 0.3)


def pluck(rng, freq, length=2.2, position=0.18, brightness=0.6, decay=1.1):
    """A plucked nylon-like string: inharmonic partials with pluck-position amplitudes, faster decay with
    frequency, a soft finger noise, and a small wooden body (resonances near 220 and 520 Hz)."""
    n = dsp.samples(length)
    t = dsp.timeline(n)
    inharm = 0.00012
    out = np.zeros(n)
    for k in range(1, 28):
        fk = freq * k * np.sqrt(1 + inharm * k * k)
        if fk > 15000:
            break
        amp = abs(np.sin(np.pi * k * position)) / k ** (1.6 - brightness)
        tau = decay / (1 + 0.035 * k * k * (freq / 300))
        out += amp * np.exp(-t / tau) * np.sin(2 * np.pi * fk * t + rng.uniform(0, 0.3))
    out *= np.clip(t / 0.0015, 0, 1)
    finger = dsp.bandpass(dsp.white(rng, dsp.samples(0.02)), 800, 5000) * dsp.exp_decay(dsp.samples(0.02), 0.003)
    out[:len(finger)] += 0.06 * finger / (np.max(np.abs(finger)) + 1e-12) * np.max(np.abs(out))
    out = dsp.peaking(out, 220, 4.0, 1.2)
    out = dsp.peaking(out, 520, 2.5, 1.5)
    return dsp.fade(out / (np.max(np.abs(out)) + 1e-12), 0.0, length * 0.3)
