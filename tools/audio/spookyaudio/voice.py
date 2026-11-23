"""Babble voices: a small source-filter (formant) synthesiser for expressive, non-verbal character speech (D20).

Source: a Rosenberg glottal pulse train (rendered at 2x and decimated against aliasing) with jitter, shimmer,
vibrato and breath noise. Filter: a cascade of five Klatt resonators whose formant tracks glide between vowel
targets and consonant loci. Consonants are approximated (plosive bursts, nasal murmur, glides, soft fricatives) so
babble has the rhythm of speech without real words. Each character has its own pitch, range, rate, vocal-tract
size, breathiness and melody habits.

Script syntax: words separated by spaces, syllables by '-', UPPERCASE syllables are stressed, punctuation shapes
the melody (',' short pause, '.' fall, '?' rise, '!' lift then fall, '~' long pause). Example: "hm? BA-di, wi-YA-ko?"
"""
from dataclasses import dataclass, field

import numpy as np
from scipy import signal

from . import SAMPLE_RATE as SR
from . import dsp

# Adult reference formants (F1, F2, F3) per vowel; F4/F5 are fixed. Scaled per character (smaller tract = higher).
VOWELS = {
    "a": (800, 1250, 2700),
    "e": (560, 1950, 2650),
    "i": (330, 2450, 3050),
    "o": (520, 920, 2600),
    "u": (360, 900, 2400),
    "y": (330, 2300, 2900),   # 'ee' glide-like
    "m": (280, 1150, 2500),   # closed-mouth hum (nasal)
}
F45 = (3500, 4300)
BANDWIDTHS = (80, 100, 140, 220, 300)

# Consonant loci: where F1/F2/F3 start before gliding into the vowel.
LOCI = {
    "b": (250, 900, 2300), "p": (250, 900, 2300), "m": (280, 1100, 2400),
    "d": (280, 1700, 2700), "t": (280, 1700, 2700), "n": (280, 1600, 2700), "l": (360, 1150, 2800),
    "g": (280, 2000, 2300), "k": (280, 2000, 2300),
    "w": (300, 700, 2300), "r": (320, 1100, 1800), "y": (300, 2300, 3000),
    "h": None, "s": None, "sh": None, "f": None, "": None,
}
PLOSIVE_BURST = {"b": (300, 1500), "p": (300, 1600), "d": (2500, 6000), "t": (3000, 7000),
                 "g": (1200, 3000), "k": (1400, 3500)}
VOICELESS = {"p", "t", "k", "h", "s", "sh", "f"}
ONSETS = ("sh", "b", "d", "g", "p", "t", "k", "m", "n", "l", "w", "r", "y", "h", "s", "f")


@dataclass
class VoiceProfile:
    name: str
    f0: float                  # speaking pitch, Hz
    range_st: float            # melodic range, semitones
    rate: float                # syllables per second
    tract: float               # formant scale (child ≈ 1.15–1.25)
    breath: float = 0.08       # aspiration mix
    open_quotient: float = 0.55
    tilt_hz: float = 4500.0    # source low-pass (lower = softer, darker)
    vibrato_hz: float = 0.0
    vibrato_st: float = 0.0
    jitter: float = 0.006
    shimmer: float = 0.04
    creak_ends: bool = False   # gentle vocal fry at falling phrase ends
    staccato: float = 0.0      # extra gap between syllables (bouncy speech)
    voicing: float = 1.0       # 1 = full voice, lower = half whisper
    bandwidth_scale: float = 1.0
    extra: dict = field(default_factory=dict)


@dataclass
class Syllable:
    onset: str
    vowel: str
    stressed: bool
    long: bool
    end: str          # '', ',', '.', '?', '!', '~'
    word_end: bool


def parse(script):
    """Split a babble script into syllables."""
    out = []
    for word in script.split():
        end = ""
        while word and word[-1] in ",.?!~":
            end = word[-1] if not end else end
            word = word[:-1]
        parts = [p for p in word.split("-") if p]
        for i, part in enumerate(parts):
            stressed = part.isupper()
            low = part.lower()
            onset = next((o for o in ONSETS if low.startswith(o) and len(low) > len(o)), "")
            rest = low[len(onset):]
            if rest.startswith("ee"):
                vowel, long = "i", True
            elif rest.startswith("oo"):
                vowel, long = "u", True
            elif rest and rest[0] in VOWELS:
                vowel, long = rest[0], len(rest) > 1 and rest[1] == rest[0]
            else:
                # a voiceless whisper syllable such as "pss" or "shh": the consonant carries it
                vowel, long = "_", False
                if onset in PLOSIVE_BURST and rest.startswith("s"):
                    onset = "ps"
                elif not onset:
                    onset = "s"
            last = i == len(parts) - 1
            out.append(Syllable(onset, vowel, stressed, long, end if last else "", last))
    return out


def _formants(vowel, tract):
    f1, f2, f3 = VOWELS[vowel]
    return np.array([f1, f2, f3, F45[0], F45[1]]) * tract


def _locus(onset, tract, vowel_formants):
    loc = LOCI.get(onset)
    if loc is None:
        return vowel_formants.copy()
    out = vowel_formants.copy()
    out[:3] = np.array(loc) * tract * 0.6 + vowel_formants[:3] * 0.4
    return out


def _glottal(phase, open_quotient):
    """Rosenberg glottal flow for a phase array in [0, 1)."""
    tp = open_quotient * 0.66
    tn = open_quotient * 0.34
    g = np.zeros_like(phase)
    rising = phase < tp
    g[rising] = 0.5 * (1 - np.cos(np.pi * phase[rising] / tp))
    falling = (phase >= tp) & (phase < tp + tn)
    g[falling] = np.cos(0.5 * np.pi * (phase[falling] - tp) / tn)
    return g


def speak(rng, profile, script, tail=0.25):
    """Render one babble line. Returns mono float audio."""
    syllables = parse(script)
    base_dur = 1.0 / profile.rate
    # --- timing
    plan = []
    t = 0.05
    for i, syl in enumerate(syllables):
        dur = base_dur * rng.uniform(0.85, 1.15)
        if syl.stressed:
            dur *= 1.3
        if syl.long:
            dur *= 1.6
        if syl.end in ".?!":
            dur *= 1.45
        plan.append((t, dur, syl))
        t += dur + profile.staccato * base_dur
        t += {",": 0.14, ".": 0.24, "?": 0.24, "!": 0.2, "~": 0.45}.get(syl.end, 0.0)
        if syl.word_end and not syl.end:
            t += 0.03
    total = t + tail
    n = dsp.samples(total)

    # --- tracks
    f0_st = np.zeros(n)
    voice_amp = np.zeros(n)
    asp_amp = np.zeros(n)
    fric = np.zeros(n)
    formants = np.zeros((5, n))
    nasal = np.zeros(n)
    noise_events = []
    phrase_start = 0
    current = _formants("a", profile.tract)
    formants[:] = current[:, None]

    def phrase_positions():
        idx = []
        start = 0
        for j, (_, _, s) in enumerate(plan):
            if s.end in ",.?!~" or j == len(plan) - 1:
                idx.append((start, j))
                start = j + 1
        return idx

    pos_in_phrase = {}
    for start, end in phrase_positions():
        length = end - start + 1
        for j in range(start, end + 1):
            pos_in_phrase[j] = ((j - start) / max(1, length - 1), end)

    rng_melody = rng.uniform(-1, 1, len(plan))
    for j, (t0, dur, syl) in enumerate(plan):
        a = dsp.samples(t0)
        b = min(n, a + dsp.samples(dur))
        frac, phrase_end = pos_in_phrase[j]
        end_mark = plan[phrase_end][2].end
        r = profile.range_st
        # melody: gentle declination, stress lifts, a little randomness, a contour on the last syllable
        st_start = r * (0.3 - 0.6 * frac) + rng_melody[j] * r * 0.3 + (0.6 * r if syl.stressed else 0.0)
        st_end = st_start + r * rng.uniform(-0.3, 0.15)  # each syllable glides a little (sing-song babble)
        if j == phrase_end:
            if end_mark == "?":
                st_end = st_start + 0.75 * r
            elif end_mark == ".":
                st_end = st_start - 0.55 * r
            elif end_mark == "!":
                st_start += 0.35 * r
                st_end = st_start - 0.6 * r
        f0_st[a:b] = np.linspace(st_start, st_end, b - a)
        if j + 1 < len(plan):
            nxt = dsp.samples(plan[j + 1][0])
            f0_st[b:nxt] = st_end

        if syl.vowel == "_":
            # whispered consonant syllable: an optional burst, then a soft hiss for the whole syllable
            a_hiss = a
            if syl.onset == "ps":
                noise_events.append(("burst", a, PLOSIVE_BURST["p"], 0.4))
                a_hiss = a + dsp.samples(0.02)
            band = (2000, 6000) if syl.onset == "sh" else (3800, 9000)
            noise_events.append(("fric", a_hiss, band, 0.32, b - a_hiss))
            continue
        target = _formants(syl.vowel, profile.tract)
        locus = _locus(syl.onset, profile.tract, target)
        onset_len = {"": 0.0, "h": 0.05, "s": 0.09, "sh": 0.09, "f": 0.07, "m": 0.055, "n": 0.05, "l": 0.045,
                     "w": 0.06, "r": 0.05, "y": 0.05}.get(syl.onset, 0.045)
        onset_len = min(onset_len, dur * 0.45)
        oa = a
        ob = min(b, a + dsp.samples(onset_len))
        # formant tracks: hold locus during the onset, glide to the vowel over ~55 ms
        glide = dsp.samples(min(0.055, dur * 0.4))
        if syl.onset in LOCI and LOCI[syl.onset] is not None:
            formants[:, oa:ob] = locus[:, None]
            g_end = min(b, ob + glide)
            ramp = 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, max(1, g_end - ob)))
            formants[:, ob:g_end] = locus[:, None] + (target - locus)[:, None] * ramp[None, :]
            formants[:, g_end:b] = target[:, None]
        else:
            formants[:, oa:b] = target[:, None]
        if j + 1 < len(plan):
            nxt = min(n, dsp.samples(plan[j + 1][0]))
            formants[:, b:nxt] = target[:, None]
        else:
            formants[:, b:] = target[:, None]

        # amplitudes
        level = (1.25 if syl.stressed else 1.0) * rng.uniform(0.88, 1.08)
        vowel_a = ob
        v_len = b - vowel_a
        if v_len > 0:
            env = np.ones(v_len)
            att = min(v_len, dsp.samples(0.022))
            rel = min(v_len - att, dsp.samples(0.05 if not syl.end else 0.09))
            env *= 0.8 + 0.2 * np.sin(np.linspace(0, np.pi, v_len)) + rng.uniform(-0.05, 0.05)
            env[:att] *= np.linspace(0, 1, att) ** 1.5
            if rel > 0:
                env[-rel:] *= np.linspace(1, 0, rel) ** 1.3
            voice_amp[vowel_a:b] = np.maximum(voice_amp[vowel_a:b], env * level)
            if syl.vowel == "m":
                nasal[vowel_a:b] = 1.0
        onset = syl.onset
        if onset in ("m", "n"):
            voice_amp[oa:ob] = np.maximum(voice_amp[oa:ob], 0.45 * level)
            nasal[oa:ob] = 1.0
        elif onset in ("l", "w", "r", "y"):
            voice_amp[oa:ob] = np.maximum(voice_amp[oa:ob], np.linspace(0.3, 0.8, ob - oa) * level)
        elif onset in PLOSIVE_BURST:
            closure_end = oa + dsp.samples(onset_len * 0.6)
            if onset not in VOICELESS:
                voice_amp[oa:closure_end] = np.maximum(voice_amp[oa:closure_end], 0.08)
            noise_events.append(("burst", closure_end, PLOSIVE_BURST[onset], 0.5 * level))
            if onset in VOICELESS:
                asp_amp[closure_end:ob + dsp.samples(0.02)] += 0.55 * level
        elif onset == "h":
            asp_amp[oa:ob + dsp.samples(0.015)] += 0.7 * level
        elif onset in ("s", "sh", "f"):
            band = {"s": (4200, 9000), "sh": (2000, 6000), "f": (1500, 8000)}[onset]
            gain = {"s": 0.22, "sh": 0.3, "f": 0.12}[onset]
            noise_events.append(("fric", oa, band, gain * level, ob - oa))

    # --- pitch: smooth contour, vibrato, jitter, creaky phrase ends
    f0_st = dsp.smooth(f0_st, 0.035)
    t_axis = dsp.timeline(n)
    if profile.vibrato_st:
        depth = profile.vibrato_st * np.clip(dsp.smooth(voice_amp, 0.15) * 1.2, 0, 1)
        f0_st += depth * np.sin(2 * np.pi * profile.vibrato_hz * t_axis + rng.uniform(0, np.pi))
    drift = dsp.lowpass(dsp.white(rng, n), 4.0, 1)
    f0_st += drift / (np.std(drift) + 1e-9) * 0.25
    jitter = dsp.lowpass(dsp.white(rng, n), 60.0, 1)
    f0 = profile.f0 * 2 ** (f0_st / 12) * (1 + profile.jitter * jitter / (np.std(jitter) + 1e-9))
    if profile.creak_ends:
        for j, (t0, dur, syl) in enumerate(plan):
            if syl.end == ".":
                a = dsp.samples(t0 + dur * 0.55)
                b = min(n, dsp.samples(t0 + dur))
                ramp = np.linspace(0, 1, b - a)
                f0[a:b] *= 1 - 0.3 * ramp
                f0[a:b] *= 1 + 0.06 * ramp * rng.standard_normal(b - a)

    # --- glottal source at 2x, decimated
    over = 2
    f0_up = np.repeat(f0, over) / over
    phase = np.cumsum(f0_up / SR) % 1.0
    flow = _glottal(phase, profile.open_quotient)
    period_index = np.cumsum(np.diff(phase, prepend=phase[0]) < 0)
    shimmer = 1 + profile.shimmer * rng.standard_normal(period_index.max() + 2)[period_index]
    source = np.diff(flow * shimmer, prepend=0.0)
    source = signal.resample_poly(source, 1, over)[:n]
    flow_ds = signal.resample_poly(flow, 1, over)[:n]
    source = dsp.lowpass(source, profile.tilt_hz, 1)
    source /= np.std(source) + 1e-9

    amp = dsp.smooth(voice_amp, 0.006)
    breath_noise = dsp.bandpass(dsp.white(rng, n), 400, 7000, 2)
    breath_noise /= np.std(breath_noise) + 1e-9
    asp = dsp.smooth(asp_amp, 0.005) + profile.breath * amp * (0.5 + 0.5 * flow_ds)
    excitation = source * amp * profile.voicing + breath_noise * asp * 0.9

    # --- formant cascade (nasal sections widen F2/F3 and drop them)
    tracks = []
    nasal_s = dsp.smooth(nasal, 0.01)
    formants = np.stack([dsp.smooth(f, 0.012) for f in formants])
    for k in range(5):  # a living vocal tract: slow, small formant wander
        wander = dsp.lowpass(dsp.white(rng, n), 3.0, 1)
        formants[k] *= 1 + 0.025 * wander / (np.std(wander) + 1e-9)
    for k in range(5):
        bw = BANDWIDTHS[k] * profile.bandwidth_scale * profile.tract ** 0.5
        bws = np.full(n, bw) * (1 + nasal_s * (2.2 if k in (1, 2) else 0.6))
        freqs = formants[k] * (1 - 0.15 * nasal_s * (k in (1, 2)))
        tracks.append((freqs, bws))
    out = dsp.time_varying_resonators(excitation, tracks, block=48)

    # --- consonant noises (parallel path), scaled to the voiced level
    ref = np.std(out[amp > 0.3]) if np.any(amp > 0.3) else np.std(out) + 1e-9
    for event in noise_events:
        if event[0] == "burst":
            _, at, band, gain = event
            length = dsp.samples(0.012)
            burst = dsp.bandpass(dsp.white(rng, length), *band, 2) * dsp.exp_decay(length, 0.003)
            burst /= np.max(np.abs(burst)) + 1e-12
            dsp.place(out, burst * gain * ref * 2.5, at / SR)
        else:
            _, at, band, gain, length = event
            length = max(length, dsp.samples(0.03))
            env = np.sin(np.linspace(0, np.pi, length)) ** 0.8
            hiss = dsp.bandpass(dsp.white(rng, length), *band, 2) * env
            hiss /= np.std(hiss) + 1e-9
            dsp.place(out, hiss * gain * ref * 1.5, at / SR)
    out = dsp.peaking(out, 2800, 2.0, 1.0)
    return dsp.fade(out / (np.max(np.abs(out)) + 1e-12), 0.004, 0.06)


CAST = {
    # The curious new kid: a bright child voice, medium pace, questions rise.
    "pip": VoiceProfile("pip", f0=275, range_st=8.0, rate=5.6, tract=1.2, breath=0.12, open_quotient=0.6,
                        tilt_hz=5000, jitter=0.005, bandwidth_scale=1.25),
    # Fast and sure: higher, quicker, bouncier, brighter.
    "bea": VoiceProfile("bea", f0=305, range_st=8.5, rate=8.2, tract=1.24, breath=0.07, open_quotient=0.5,
                        tilt_hz=6500, jitter=0.004, staccato=0.12, bandwidth_scale=1.2),
    # Slow and dry: a kind old man, narrow melody, long syllables, gentle creak at the end of a thought.
    "vale": VoiceProfile("vale", f0=118, range_st=3.0, rate=3.4, tract=0.88, breath=0.1, open_quotient=0.5,
                         tilt_hz=3200, jitter=0.009, shimmer=0.06, creak_ends=True, bandwidth_scale=1.0),
    # Conspiratorial: half-whispered, close, quick little bursts.
    "june": VoiceProfile("june", f0=258, range_st=6.0, rate=6.6, tract=1.16, breath=0.55, open_quotient=0.7,
                         tilt_hz=3800, voicing=0.55, jitter=0.006, bandwidth_scale=1.3),
    # Theatrical: wide swoops, warm vibrato, generous vowels.
    "kit": VoiceProfile("kit", f0=200, range_st=12.0, rate=4.6, tract=1.03, breath=0.09, open_quotient=0.55,
                        tilt_hz=4200, vibrato_hz=5.6, vibrato_st=0.45, jitter=0.005, bandwidth_scale=1.1),
}
