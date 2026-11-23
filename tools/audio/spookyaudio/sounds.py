"""The S003 first sound set: what each sound is, what it is for, and how it is synthesised.

Each `Sound` renders `takes` randomised variants (seeded by its id and take number). Ids are the event ids the
game uses (`Assets/SpookyStreet/Core/Audio/AudioEvents.cs`); a test keeps the two lists in sync.
"""
from dataclasses import dataclass, field
from typing import Callable, Optional

import numpy as np

from . import SAMPLE_RATE as SR
from . import dsp, models, voice


@dataclass
class Sound:
    id: str
    category: str              # mastering category (master.CATEGORIES)
    title: str
    description: str           # what you hear
    purpose: str               # what it does in the game
    render: Callable           # (rng, take) -> mono (n,) or stereo (n, 2) float audio
    takes: int = 4
    budget: tuple = (0.05, 3.0)  # allowed duration, seconds
    loop: bool = False
    markers: tuple = ()        # sync points (seconds), e.g. knock onsets for the cam and owl animation
    group: str = ""
    listen_for: str = ""       # what the owner should check by ear
    lufs_offset: float = 0.0   # per-sound trim of the category loudness target (e.g. a very peaky tap)
    demo: Optional[Callable] = None  # (takes) -> audio, an in-context preview for the listening page
    extra: dict = field(default_factory=dict)


# ---------------------------------------------------------------------------------------------------------------
# The knocks: short, short, pause, last (spec §6). Fixed onsets so the cam and owl animation can sync to them.

KNOCK_ONSETS = (0.02, 0.28, 1.00)
KNOCK_WEIGHTS = (0.85, 0.78, 1.0)
HUM_KNOCK_ONSETS = (1.10, 1.36, 2.08)


def _knock_sequence(rng, onsets, make):
    out = np.zeros(dsp.samples(onsets[-1] + 0.6))
    for at, weight in zip(onsets, KNOCK_WEIGHTS):
        dsp.place(out, make(rng, weight), at)
    return out


def knocks_distant(rng, take):
    """Three knocks inside the blue house, heard from the street: through a wall, across the yard."""
    def make(r, weight):
        body = models.knuckle_knock(r, weight)
        edge = models.hard_tap(r, base=380, weight=weight)
        return dsp.mix(body, (0.35, edge))

    dry = _knock_sequence(rng, KNOCK_ONSETS, make)
    # through the wall: highs absorbed, the 'tok' band kept for the phone speaker
    wall = dsp.lowpass(dry, 2400, 2)
    wall = dsp.peaking(wall, 850, 3.5, 0.9)
    wall = dsp.peaking(wall, 180, -3.0, 0.8)
    # across the yard: early reflections from the neighbouring houses and a light outdoor tail
    ir = dsp.reverb_ir(rng, rt60=1.0, length=1.1, predelay=0.012,
                       early=((0.041, 0.5), (0.077, 0.32), (0.123, 0.2), (0.17, 0.1)),
                       bands=((0, 1.0), (1200, 0.6), (4000, 0.35)))
    out = dsp.convolve(wall, ir, wet=0.3, dry=1.0)
    out = dsp.lowpass(out, 5000, 2)
    return dsp.fade(dsp.trim_tail(out, -62), 0.001, 0.15)


def floor_hum(rng, take):
    """Inside the hall: the floor starts to hum (the turning shaft), three knocks come up through the boards with
    a rattle of dust, then the hum dies away."""
    length = 3.9
    n = dsp.samples(length)
    t = dsp.timeline(n)
    f0 = 98.0 * (1 + rng.uniform(-0.01, 0.01))
    hum = np.zeros(n)
    for k in range(1, 11):
        amp = 1.0 / k ** 1.5 * (1.7 if 2 <= k <= 4 else 1.0)
        detune = 1 + rng.uniform(-0.002, 0.002)
        hum += amp * np.sin(2 * np.pi * f0 * k * detune * t + rng.uniform(0, 2 * np.pi))
        hum += 0.35 * amp * np.sin(2 * np.pi * (f0 * k + 0.55) * t + rng.uniform(0, 2 * np.pi))
    shaft = 5.2 * (1 + rng.uniform(-0.05, 0.05))
    tremble = 1 + 0.3 * np.sin(2 * np.pi * shaft * t) + 0.12 * np.sin(2 * np.pi * shaft * 2 * t + 1.0)
    swell = dsp.segment_envelope([(0, 0), (0.25, 0.1), (0.95, 1.0), (2.4, 1.0), (3.6, 0.0)], n)
    swell = swell ** 1.4
    hum = dsp.bandpass(hum, 85, 1600, 2)
    hum /= np.max(np.abs(hum)) + 1e-12
    # the boards themselves: a woody rumble band, wobbling with the shaft
    rumble = dsp.bandpass(dsp.coloured(rng, n, -3), 150, 900, 2)
    rumble /= np.max(np.abs(rumble)) + 1e-12
    hum = (hum + 0.5 * rumble) * tremble * swell
    hum /= np.max(np.abs(hum)) + 1e-12
    rattle = models.crackle(rng, n, 500 * swell * (tremble - 0.6), freq_range=(1500, 5000))
    out = 0.32 * hum + 0.06 * rattle / (np.max(np.abs(rattle)) + 1e-12)

    def make(r, weight):
        knock = models.knuckle_knock(r, weight)
        boom = dsp.modal_bank(len(knock), [(118, 0.12, 1.0), (176, 0.1, 0.8), (236, 0.08, 0.7),
                                                  (354, 0.05, 0.5)], rng=r, jitter=0.02)
        boom = dsp.strike(boom, 0.004)
        boom /= np.max(np.abs(boom)) + 1e-12
        dust_n = dsp.samples(0.12)
        dust = models.crackle(r, dust_n, 2500 * dsp.exp_decay(dust_n, 0.03), freq_range=(2000, 7000))
        dust /= np.max(np.abs(dust)) + 1e-12
        sound = knock + 0.6 * boom * weight
        return dsp.place(sound, 0.12 * weight * dust, 0.015)

    for at, weight in zip(HUM_KNOCK_ONSETS, KNOCK_WEIGHTS):
        dsp.place(out, make(rng, weight), at)
    ir = dsp.reverb_ir(rng, rt60=0.55, length=0.7, early=((0.009, 0.4), (0.017, 0.3), (0.026, 0.2)),
                       bands=((0, 1.0), (1500, 0.7), (4500, 0.4)))
    out = dsp.convolve(out, ir, wet=0.22)
    return dsp.fade(out[:dsp.samples(length + 0.3)], 0.002, 0.2)


def cam_tap(rng, take):
    """The wooden cam lobe tapping its follower, close up in the workshop."""
    out = np.zeros(dsp.samples(0.45))
    dsp.place(out, models.hard_tap(rng, base=430 * (1 + rng.uniform(-0.03, 0.03))), 0.002)
    # the follower settles with a tiny second contact
    dsp.place(out, models.hard_tap(rng, base=520, weight=0.18), rng.uniform(0.028, 0.04))
    ir = dsp.reverb_ir(rng, rt60=0.4, length=0.45, early=((0.006, 0.4), (0.013, 0.25)))
    out = dsp.convolve(out, ir, wet=0.14)
    return dsp.fade(dsp.trim_tail(out, -60), 0.0005, 0.05)


def wind_wheel(rng, take):
    """The little rooftop wind wheel spinning up, whirring, and slowing: heard from the treehouse deck."""
    length = 2.8
    n = dsp.samples(length)
    t = dsp.timeline(n)
    speed = dsp.segment_envelope([(0, 0.25), (0.9, 1.0), (1.9, 1.0), (2.8, 0.45)], n)
    speed *= 1 + 0.04 * np.sin(2 * np.pi * 0.7 * t + rng.uniform(0, 6))
    blades = 6
    rev_per_s = 5.2 * (1 + rng.uniform(-0.06, 0.06))
    env = models.flutter_envelope(rng, n, blades * rev_per_s * speed, rate_spread=0.03, attack=0.005,
                                  decay=0.012, amp_spread=(0.75, 1.0))
    whirr = dsp.bandpass(dsp.white(rng, n), 600, 2400, 2) * env
    whirr = dsp.peaking(whirr, 950, 5.0, 1.5)  # the wooden blades' 'whup'
    out = whirr / (np.max(np.abs(whirr)) + 1e-12)
    out *= dsp.smooth(speed, 0.05) ** 1.5
    for _ in range(2):
        dsp.place(out, models.creak(rng, 0.09, rng.uniform(380, 450), rng.uniform(470, 560)),
                  rng.uniform(0.3, 2.3), 0.22)
    out = dsp.lowpass(out, 6000, 2)
    ir = dsp.reverb_ir(rng, rt60=0.9, length=0.9, early=((0.03, 0.3), (0.07, 0.2)))
    out = dsp.convolve(out, ir, wet=0.2)
    return dsp.fade(out[:dsp.samples(length + 0.4)], 0.05, 0.3)


# ---------------------------------------------------------------------------------------------------------------
# Pip moving

def step_wood(rng, take):
    return dsp.trim_tail(models.footstep_wood(rng, creaky=(take == 4)), -60)


def step_grass(rng, take):
    return dsp.trim_tail(models.footstep_grass(rng), -60)


def step_stone(rng, take):
    return dsp.trim_tail(models.footstep_stone(rng), -60)


def walk_demo(takes, interval=0.37, steps=10):
    """A walk: alternate takes at a child's pace with slight timing and level variation."""
    rng = dsp.rng_for("walk-demo", len(takes))
    out = np.zeros(dsp.samples(interval * steps + 0.5))
    order = list(range(len(takes)))
    for i in range(steps):
        take = takes[order[i % len(order)]]
        at = i * interval + rng.uniform(-0.012, 0.012) + 0.02
        dsp.place(out, take, at, gain=rng.uniform(0.8, 1.0) * (0.9 if i % 2 else 1.0))
    return out


def jump(rng, take):
    """Take-off: a soft shoe push, the cape whooshing up with a couple of flaps."""
    n = dsp.samples(0.42)
    out = np.zeros(n)
    push = models._scuff(rng, 0.06, 900, 3500, 0.003, 0.014)
    dsp.place(out, push, 0.0, 0.45)
    w_len = dsp.samples(0.3)
    whoosh = models.swept_noise(rng, w_len, rng.uniform(500, 650), rng.uniform(1900, 2400), q=1.4)
    whoosh *= dsp.attack_decay(w_len, 0.06, 0.07)
    dsp.place(out, whoosh / (np.max(np.abs(whoosh)) + 1e-12), 0.01)
    f_len = dsp.samples(0.2)
    flaps = models.flutter_envelope(rng, f_len, 21.0, attack=0.002, decay=0.012)
    flap_noise = dsp.bandpass(dsp.white(rng, f_len), 500, 3000, 2) * flaps * dsp.exp_decay(f_len, 0.08)
    dsp.place(out, 0.35 * flap_noise / (np.max(np.abs(flap_noise)) + 1e-12), 0.04)
    return dsp.fade(dsp.trim_tail(out, -60), 0.0005, 0.05)


def land(rng, take):
    """Landing: a light body thud, two quick soles, the cape settling with one flutter, a puff of dust."""
    n = dsp.samples(0.55)
    out = np.zeros(n)
    thud_ir = dsp.modal_bank(dsp.samples(0.3), [(125, 0.05, 1.0), (196, 0.04, 0.8), (265, 0.03, 0.7),
                                                 (410, 0.02, 0.45), (620, 0.012, 0.25)], rng=rng, jitter=0.05)
    thud = dsp.strike(thud_ir, 0.006)
    thud /= np.max(np.abs(thud)) + 1e-12
    dsp.place(out, thud, 0.0)
    dsp.place(out, models._scuff(rng, 0.06, 300, 1600, 0.002, 0.014), 0.0, 0.7)
    dsp.place(out, models._scuff(rng, 0.05, 1000, 4000, 0.002, 0.01), 0.0, 0.45)
    dsp.place(out, models._scuff(rng, 0.05, 1200, 4500, 0.002, 0.01), rng.uniform(0.025, 0.04), 0.3)
    c_len = dsp.samples(0.32)
    flaps = models.flutter_envelope(rng, c_len, 17.0, attack=0.003, decay=0.016)
    cloth = dsp.bandpass(dsp.white(rng, c_len), 450, 2600, 2) * flaps * dsp.exp_decay(c_len, 0.1)
    dsp.place(out, 0.4 * cloth / (np.max(np.abs(cloth)) + 1e-12), 0.05)
    d_len = dsp.samples(0.15)
    dust = models.crackle(rng, d_len, 3500 * dsp.exp_decay(d_len, 0.04), freq_range=(2500, 8000))
    dsp.place(out, 0.18 * dust / (np.max(np.abs(dust)) + 1e-12), 0.006)
    return dsp.fade(dsp.trim_tail(out, -60), 0.0005, 0.06)


def glide_open(rng, take):
    """The bat-cape spreads ('fwup') and flutters in the air as Pip glides, then folds."""
    length = 1.4
    n = dsp.samples(length)
    out = np.zeros(n)
    dsp.place(out, models.cloth_snap(rng, 0.1), 0.0)
    flutter = models.cloth_flutter(rng, length - 0.05, rate=rng.uniform(12.5, 14.5))
    env = dsp.segment_envelope([(0, 0.0), (0.06, 0.75), (0.4, 0.6), (1.0, 0.55), (1.35, 0.0)], len(flutter))
    dsp.place(out, flutter * env, 0.04)
    return dsp.fade(out, 0.0005, 0.08)


def glide_loop(rng, take):
    """Seamless 2 s flutter for as long as the glide lasts (crossfaded onto itself)."""
    loop_len = 2.0
    fade_len = 0.4
    x = models.cloth_flutter(rng, loop_len + fade_len, rate=rng.uniform(12.5, 14.5)) * 0.6
    n = dsp.samples(loop_len)
    f = dsp.samples(fade_len)
    ramp = np.sin(np.linspace(0, np.pi / 2, f)) ** 2
    out = x[:n].copy()
    out[:f] = x[:f] * np.sqrt(ramp) + x[n:n + f] * np.sqrt(1 - ramp)
    return out


# ---------------------------------------------------------------------------------------------------------------
# Paper, notebook and UI

def page_turn(rng, take):
    """A big paper page turning over: lift crackle, airy sweep, flex, and a soft landing flap."""
    length = 0.78
    n = dsp.samples(length)
    t = dsp.timeline(n)
    out = np.zeros(n)
    lift = models.crackle(rng, n, 2200 * np.exp(-((t - 0.05) / 0.04) ** 2), freq_range=(1500, 6500))
    flex = models.crackle(rng, n, 700 * np.exp(-((t - 0.3) / 0.15) ** 2), freq_range=(2000, 8000),
                          tau_range=(0.0004, 0.0015))
    centre = np.interp(t, [0, 0.12, 0.3, 0.55, length], [700, 1100, 2700, 1200, 900])
    sweep = dsp.time_varying_resonators(dsp.white(rng, n), [(centre, centre / 1.3), (centre * 1.4, centre / 1.1)],
                                        block=64)
    sweep *= np.exp(-((t - 0.3) / 0.13) ** 2)
    sweep /= np.max(np.abs(sweep)) + 1e-12
    air = dsp.bandpass(dsp.coloured(rng, n, -3), 150, 700, 2) * np.exp(-((t - 0.32) / 0.16) ** 2)
    air /= np.max(np.abs(air)) + 1e-12
    out += 0.4 * lift / (np.max(np.abs(lift)) + 1e-12)
    out += 0.2 * flex / (np.max(np.abs(flex)) + 1e-12)
    out += 0.9 * sweep + 0.45 * air
    land_at = rng.uniform(0.5, 0.55)
    s_len = dsp.samples(0.12)
    slap = dsp.lowpass(dsp.white(rng, s_len), 1800, 2) * dsp.attack_decay(s_len, 0.002, 0.016)
    dsp.place(out, 0.7 * slap / (np.max(np.abs(slap)) + 1e-12), land_at)
    puff = dsp.lowpass(dsp.white(rng, s_len), 500, 2) * dsp.attack_decay(s_len, 0.004, 0.035)
    dsp.place(out, 0.4 * puff / (np.max(np.abs(puff)) + 1e-12), land_at)
    return dsp.fade(dsp.lowpass(out, 7000, 2), 0.002, 0.08)


def sticker_pick(rng, take):
    """Peeling a sticker off its backing: a rising adhesive crackle with a little tearing tone and a 'tk'."""
    n = dsp.samples(0.34)
    t = dsp.timeline(n)
    peel_end = rng.uniform(0.19, 0.23)
    ramp = np.clip((t - 0.02) / (peel_end - 0.02), 0, 1) * (t < peel_end)
    crack = models.crackle(rng, n, 700 + 4000 * ramp ** 1.5, freq_range=(2000, 7000), tau_range=(0.0002, 0.0006))
    crack *= (t < peel_end + 0.004)
    centre = 1400 + 1300 * ramp
    tear = dsp.time_varying_resonators(dsp.white(rng, n), [(centre, centre / 9)], block=64) * ramp
    out = 0.8 * crack / (np.max(np.abs(crack)) + 1e-12) + 0.3 * tear / (np.max(np.abs(tear)) + 1e-12)
    pop = dsp.strike(dsp.modal_bank(dsp.samples(0.05), [(2400, 0.004, 1.0), (3900, 0.002, 0.5)], rng=rng,
                                    jitter=0.05), 0.0003)
    dsp.place(out, 0.6 * pop / (np.max(np.abs(pop)) + 1e-12), peel_end)
    swish_n = dsp.samples(0.09)
    swish = dsp.bandpass(dsp.white(rng, swish_n), 1000, 4000) * np.sin(np.linspace(0, np.pi, swish_n))
    dsp.place(out, 0.12 * swish / (np.max(np.abs(swish)) + 1e-12), peel_end + 0.01)
    return dsp.fade(dsp.trim_tail(dsp.lowpass(out, 8000, 2), -60), 0.002, 0.03)


def sticker_drop(rng, take):
    """Pressing a sticker onto the page: a soft felt pat, a small wooden 'tuk', the adhesive gripping."""
    n = dsp.samples(0.3)
    out = np.zeros(n)
    p_len = dsp.samples(0.08)
    pat = dsp.lowpass(dsp.white(rng, p_len), 1500, 2) * dsp.attack_decay(p_len, 0.0012, 0.01)
    dsp.place(out, pat / (np.max(np.abs(pat)) + 1e-12), 0.0)
    tuk = dsp.strike(dsp.modal_bank(dsp.samples(0.15), [(660, 0.02, 1.0), (1310, 0.009, 0.5), (2150, 0.005, 0.25)],
                                    rng=rng, jitter=0.04), 0.0018)
    dsp.place(out, 0.75 * tuk / (np.max(np.abs(tuk)) + 1e-12), 0.002)
    g_len = dsp.samples(0.12)
    grip = models.crackle(rng, g_len, 3500 * dsp.exp_decay(g_len, 0.025), freq_range=(2000, 7000),
                          tau_range=(0.0002, 0.0007))
    dsp.place(out, 0.25 * grip / (np.max(np.abs(grip)) + 1e-12), 0.006)
    return dsp.fade(dsp.trim_tail(out, -60), 0.0005, 0.03)


def sticker_wobble(rng, take):
    """A wrong set: the sticker gives a friendly rubbery wobble ('boing-oing') and hops home with a soft 'tok'."""
    n = dsp.samples(0.75)
    t = dsp.timeline(n)
    out = np.zeros(n)
    f0 = 500 * (1 + rng.uniform(-0.04, 0.04))
    depth = 0.05 * np.exp(-t / 0.16)
    rate = 10.5 + rng.uniform(-1, 1)
    freq = f0 * (1 + depth * np.sin(2 * np.pi * rate * t)) * (1 - 0.03 * np.clip(t / 0.4, 0, 1))
    phase = dsp.phase_from_freq(freq)
    tone = np.sin(phase) + 0.25 * np.sin(3.93 * phase) * np.exp(-t / 0.04) + 0.15 * np.sin(2 * phase)
    tone *= dsp.attack_decay(n, 0.003, 0.16)
    out += 0.8 * tone
    home = rng.uniform(0.4, 0.44)
    tok = models.wood_strike(rng, 690, length=0.15, hardness=0.0009, tau_low=0.025, tau_high=0.004,
                             contact_noise=0.08, emphasis=(600, 2000))
    dsp.place(out, 0.55 * tok, home)
    p_len = dsp.samples(0.05)
    pat = dsp.lowpass(dsp.white(rng, p_len), 1400, 2) * dsp.attack_decay(p_len, 0.001, 0.008)
    dsp.place(out, 0.3 * pat / (np.max(np.abs(pat)) + 1e-12), home)
    return dsp.fade(dsp.trim_tail(out, -60), 0.001, 0.05)


def correct_glow(rng, take):
    """A right placement: a warm rising celesta arpeggio (D major) that blooms into a soft glow with sparkles."""
    length = 1.9
    n = dsp.samples(length)
    t = dsp.timeline(n)
    out = np.zeros(n)
    notes = [models.note_hz(x) for x in ("D5", "F#5", "A5", "D6")]
    for i, f in enumerate(notes):
        at = i * 0.075 + rng.uniform(-0.004, 0.004)
        dsp.place(out, models.mallet_bar(rng, f, length=1.5, softness=0.0016, kind="celesta") * (0.8 + 0.1 * i), at)
    glow = np.zeros(n)
    for f in notes[:3]:
        for detune in (-0.0025, 0.0025):
            glow += np.sin(2 * np.pi * f * (1 + detune) * t + rng.uniform(0, 6))
    glow *= dsp.attack_decay(n, 0.25, 0.6) * 0.12
    out += glow
    for _ in range(10):
        f = models.note_hz(rng.choice(["D7", "E7", "F#7", "A7", "B6"]))
        at = rng.uniform(0.15, 1.0)
        s_len = dsp.samples(0.25)
        st = dsp.timeline(s_len)
        spark = np.sin(2 * np.pi * f * st) * np.exp(-st / 0.05) * np.clip(st / 0.002, 0, 1)
        dsp.place(out, spark * rng.uniform(0.04, 0.1) * (1.2 - at), at)
    ir = dsp.reverb_ir(rng, rt60=1.0, length=1.0, early=((0.011, 0.3), (0.023, 0.2)))
    out = dsp.convolve(out, ir, wet=0.22)
    return dsp.fade(dsp.trim_tail(out, -60), 0.001, 0.2)


def ui_tap(rng, take):
    """A felt-covered button pressed: a short, soft wooden 'tup' with a breath of paper."""
    base = 880 * (1 + rng.uniform(-0.035, 0.035))
    tap = models.wood_strike(rng, base, length=0.12, hardness=0.0013, tau_low=0.022, tau_high=0.004,
                             brightness=0.5, contact_noise=0.05, emphasis=(800, 2000))
    paper = models.crackle(rng, dsp.samples(0.02), 1500, freq_range=(3000, 7000))
    tap[:len(paper)] += 0.15 * paper / (np.max(np.abs(paper)) + 1e-12)
    return dsp.fade(dsp.trim_tail(tap, -55), 0.0003, 0.02)


# ---------------------------------------------------------------------------------------------------------------
# Voices

BABBLE = {
    "pip": ("hm? BA-di, wi-YA-ko?", "oh! mi-PO, ta-li-BOO?", "hm-m~ NOO-la, be-di-wo."),
    "bea": ("ya-YA! bi-di-BA-di, ti-ki-TA!", "o-ho, PIP! na-ni-bo-DI-ba-la!", "bi-DI ba-di, wa-ta-KI ya?"),
    "vale": ("hm. MOO-ro~ wa-lo, BO-dum.", "ah. HO-mo, no-wa-DOO-ma.", "mm-hm~ BO-ro-wa, lo-DOOM."),
    "june": ("pss! shi-mi, NU-wa-ki~ hi-hi.", "pss~ shoo-NI-ma, wi-ki-SHA?", "hi-hi! mi-NU wa-shi, POO-ka."),
    "kit": ("oh-HO! la-la-mo-RE, ba-ra-NO-wa!", "ah~ MA-li-ma-LOO-na, de-RA-mo!", "o-ho-HO, ta-RA-la, bo-NEE-ta!"),
}


def babble(character):
    def render(rng, take):
        script = BABBLE[character][(take - 1) % len(BABBLE[character])]
        line = voice.speak(rng, voice.CAST[character], script)
        ir = dsp.reverb_ir(rng, rt60=0.35, length=0.35, early=((0.007, 0.2), (0.015, 0.12)))
        return dsp.fade(dsp.trim_tail(dsp.convolve(line, ir, wet=0.07), -60), 0.002, 0.05)
    return render


# ---------------------------------------------------------------------------------------------------------------
# Night ambience: a seamless stereo loop (everything is generated circularly, so the end flows into the start)

AMBIENCE_LENGTH = 24.0


def night_ambience(rng, take):
    n = dsp.samples(AMBIENCE_LENGTH)
    t = dsp.timeline(n)
    L = AMBIENCE_LENGTH
    # gusts: sums of sines with whole cycles per loop, so the gust pattern itself loops
    def gusts(offset):
        g = 0.5 + 0.24 * np.sin(2 * np.pi * 2 * t / L + rng.uniform(0, 6) + offset) \
            + 0.14 * np.sin(2 * np.pi * 3 * t / L + rng.uniform(0, 6)) \
            + 0.1 * np.sin(2 * np.pi * 5 * t / L + rng.uniform(0, 6)) \
            + 0.06 * np.sin(2 * np.pi * 11 * t / L + rng.uniform(0, 6))
        return np.clip(g, 0.12, 1.0)

    channels = []
    for ch in range(2):
        g = gusts(ch * 0.4)
        # wind soughing through trees: a soft band whose centre rises with each gust ("whoooh"), plus leafy air
        noise = dsp.coloured(rng, n, -3.0)
        centre = 260 + 650 * g ** 1.3

        def sough(x, c=centre):
            c3 = np.concatenate([c, c, c])
            return dsp.time_varying_resonators(x, [(c3, c3 * 1.1), (c3 * 1.6, c3 * 1.4)], block=96)
        body = np.concatenate([noise, noise, noise])
        body = sough(body)[n:2 * n]
        body /= np.std(body) + 1e-12
        air = dsp.spectral_shape(dsp.coloured(rng, n, -1), [(500, -40), (2000, -12), (4000, -9), (9000, -24)])
        air /= np.std(air) + 1e-12
        channels.append(body * g ** 1.6 + 0.3 * air * g ** 2.5)
    out = np.stack(channels, axis=1) * 0.25

    # leaves skittering across the street, timed on the gusts, panned around
    for i in range(int(rng.integers(7, 10))):
        at = rng.uniform(0, L)
        dur = rng.uniform(0.4, 1.3)
        m = dsp.samples(dur)
        tt = dsp.timeline(m)
        density = 1800 * np.sin(np.pi * tt / dur) ** 2 * (0.5 + 0.5 * np.abs(np.sin(2 * np.pi * rng.uniform(3, 7) * tt)))
        leaf = models.crackle(rng, m, density, freq_range=(1500, 7000), tau_range=(0.0004, 0.0018))
        leaf = leaf / (np.max(np.abs(leaf)) + 1e-12) * rng.uniform(0.1, 0.2)
        pan = rng.uniform(0.15, 0.85)
        dsp.place_wrapped(out[:, 0], leaf * np.sqrt(1 - pan), at)
        dsp.place_wrapped(out[:, 1], leaf * np.sqrt(pan), at)

    # two crickets, far apart, chirping in threes with rests (a quiet autumn-night texture, never insistent)
    for c, (carrier, pan) in enumerate(((4650.0, 0.25), (4980.0, 0.8))):
        at = rng.uniform(0, 1.0)
        while at < L:
            if rng.random() < 0.22:
                at += rng.uniform(2.0, 4.0)
                continue
            m = dsp.samples(0.12)
            tt = dsp.timeline(m)
            chirp = np.zeros(m)
            for k in range(3):
                p0 = k * 0.032
                win = np.clip(1 - np.abs((tt - p0 - 0.009) / 0.009), 0, 1) ** 1.5
                chirp += win * np.sin(2 * np.pi * carrier * (1 + 0.01 * k) * tt)
            gain = 0.045 * rng.uniform(0.7, 1.0)
            dsp.place_wrapped(out[:, 0], chirp * gain * np.sqrt(1 - pan), at)
            dsp.place_wrapped(out[:, 1], chirp * gain * np.sqrt(pan), at)
            at += rng.uniform(0.75, 1.1)

    # an outdoor space, applied circularly so the tail wraps into the loop start
    ir = dsp.reverb_ir(rng, rt60=1.4, length=1.6, stereo=True, early=((0.05, 0.3), (0.09, 0.2)))
    for ch in range(2):
        out[:, ch] = out[:, ch] + 0.25 * dsp.circular_convolve(out[:, ch], ir[:, ch])
    return out


# ---------------------------------------------------------------------------------------------------------------
# Music motif: three notes in the knock rhythm (short, short, long)

MOTIF = ("D4", "F4", "A4")
MOTIF_TIMES = (0.05, 0.36, 1.0)


def motif_pluck(rng, take):
    """Opening: the three-note motif on a soft plucked string, with a low D under it."""
    length = 3.6
    out = np.zeros(dsp.samples(length))
    dsp.place(out, models.pluck(rng, models.note_hz("D3"), length=3.0, brightness=0.45, decay=1.6), 0.05, 0.55)
    for note, at in zip(MOTIF, MOTIF_TIMES):
        dur = 2.4 if note == MOTIF[-1] else 1.6
        dsp.place(out, models.pluck(rng, models.note_hz(note), length=dur, brightness=0.6, decay=1.2),
                  at + rng.uniform(-0.006, 0.006), 0.9 if note == MOTIF[-1] else 0.75)
    ir = dsp.reverb_ir(rng, rt60=1.5, length=1.6, early=((0.013, 0.3), (0.029, 0.2)))
    out = dsp.convolve(out, ir, wet=0.28)
    return dsp.fade(dsp.trim_tail(out, -60), 0.001, 0.4)


def motif_wood(rng, take):
    """Closure: the same motif on a wooden marimba, then a warmer rolled D major chord (the minor third turns
    major, a 'Picardy' lift)."""
    length = 4.8
    out = np.zeros(dsp.samples(length))
    for note, at in zip(MOTIF, MOTIF_TIMES):
        dsp.place(out, models.mallet_bar(rng, models.note_hz(note), length=1.6, kind="marimba"),
                  at + rng.uniform(-0.005, 0.005), 0.8)
    chord = ("D3", "A3", "F#4", "A4", "D5", "E5")
    for i, note in enumerate(chord):
        at = 1.75 + i * 0.045 + rng.uniform(-0.004, 0.004)
        dsp.place(out, models.mallet_bar(rng, models.note_hz(note), length=2.8, softness=0.0035, kind="marimba"),
                  at, 0.5 if note != "E5" else 0.3)
        dsp.place(out, models.pluck(rng, models.note_hz(note), length=2.6, brightness=0.4, decay=1.8), at + 0.02,
                  0.22)
    ir = dsp.reverb_ir(rng, rt60=1.8, length=2.0, early=((0.013, 0.3), (0.029, 0.2)))
    out = dsp.convolve(out, ir, wet=0.3)
    return dsp.fade(dsp.trim_tail(out, -60), 0.001, 0.6)


# ---------------------------------------------------------------------------------------------------------------
# The registry

SOUNDS = [
    Sound("clue_knocks_distant", "clue", "The three knocks (from the street)",
          "Knock, knock… knock: hollow wooden knocks inside the blue house, muffled by the wall, with a little "
          "echo off the neighbouring houses.",
          "Arrival clue: raises 'Where are the knocks coming from?'. Mono, so the game places it at the house "
          "(direction and distance come from the 3D position).",
          knocks_distant, takes=4, budget=(1.6, 3.2), markers=KNOCK_ONSETS, group="Clues", lufs_offset=-2.0,
          listen_for="Are all three knocks distinct on the phone speaker, with the pause before the last? Does it "
                     "sound like it is inside a house, not in your ear, and still friendly rather than scary?"),
    Sound("clue_floor_hum", "clue", "Floor hum and knocks (inside the hall)",
          "The floor starts to hum and tremble, three knocks thump up through the boards with a rattle of dust, "
          "then the hum fades.",
          "Hall clue: becomes the floor-hum sticker; the knocks are under the floor.",
          floor_hum, takes=3, budget=(3.0, 4.8), markers=HUM_KNOCK_ONSETS, group="Clues", lufs_offset=-1.0,
          listen_for="Can you hear the hum on the phone (it is built from overtones, the speaker has no deep "
                     "bass)? Do the knocks feel under your feet rather than on a door?"),
    Sound("clue_cam_tap", "clue", "Cam tap (workshop, close up)",
          "A hard little wooden 'tok': the cam lobe tapping its follower, with a tiny settle.",
          "Workshop: the machine's knock source seen and heard close up; plays on each cam contact.",
          cam_tap, takes=5, budget=(0.15, 0.7), group="Clues", lufs_offset=-3.0,
          listen_for="Does it read as the same family as the knocks, only closer and harder?"),
    Sound("clue_wind_wheel", "clue", "Wind wheel whirr (rooftop)",
          "The little rooftop wheel spins up, whirrs and slows, with a soft axle creak.",
          "Bonus clue seen from the treehouse deck: it whirs before each knock cycle.",
          wind_wheel, takes=3, budget=(2.5, 3.6), group="Clues",
          listen_for="Whirr, not buzz? It should sound wooden and breezy."),
    Sound("step_wood", "footstep", "Footsteps on wood",
          "Pip's light heel-and-toe on porch planks; one take has a tiny creak.",
          "Porch, treehouse and the house interior floors.",
          step_wood, takes=5, budget=(0.15, 0.45), group="Pip moving", demo=walk_demo,
          listen_for="In the walk preview: do steps vary enough not to sound like a machine gun? Light, a child?"),
    Sound("step_grass", "footstep", "Footsteps on grass",
          "Soft brushy lawn steps with a gentle earth thud.",
          "Yards and verges.",
          step_grass, takes=5, budget=(0.15, 0.45), group="Pip moving", demo=walk_demo,
          listen_for="Grass, or does it sound like paper? Too hissy on the phone?"),
    Sound("step_stone", "footstep", "Footsteps on stone",
          "Crisp, small soft-sole ticks on paving with a little grit.",
          "Sidewalk, plaza and the porch steps.",
          step_stone, takes=5, budget=(0.1, 0.45), group="Pip moving", demo=walk_demo,
          listen_for="Soft costume shoes, not high heels or tap shoes?"),
    Sound("move_jump", "movement", "Jump",
          "A soft push-off and the cape whooshing up with a couple of flaps.",
          "Every jump; layered with the high-jump boost later.",
          jump, takes=4, budget=(0.2, 0.6), group="Pip moving",
          listen_for="Light and springy, not a sword swish?"),
    Sound("move_land", "movement", "Land",
          "A light thud, two quick soles, one cape flutter and a puff of dust.",
          "Every landing (spec §5: soft footfall, one flutter of the cape).",
          land, takes=4, budget=(0.25, 0.7), group="Pip moving",
          listen_for="Soft and friendly, no 'ouch' feeling?"),
    Sound("move_glide_open", "movement", "Glide: cape opens",
          "'Fwup': the bat cape spreads, then flutters in the air and folds.",
          "Start of the bat-cape glide, Pip's signature move.",
          glide_open, takes=3, budget=(1.0, 1.8), group="Pip moving",
          listen_for="Does it sound like fabric wings? Is the flutter pleasant rather than a motor?"),
    Sound("move_glide_loop", "movement", "Glide: flutter loop",
          "A seamless two-second flutter that repeats while the glide lasts.",
          "Held glide (loops under the opening sound).",
          glide_loop, takes=3, budget=(1.9, 2.1), loop=True, group="Pip moving",
          listen_for="With the player looping: can you hear the seam or a repeating pattern?"),
    Sound("ui_page_turn", "notebook", "Page turn (threshold)",
          "A big paper page lifting, sweeping over and landing with a soft flap.",
          "The 0.5 s paper page-turn transition at the house thresholds (D24.1); also the notebook page flip.",
          page_turn, takes=4, budget=(0.5, 1.0), group="Notebook and UI",
          listen_for="Paper, not wind or a shaker? Does it fit a half-second transition?"),
    Sound("note_sticker_pick", "notebook", "Sticker: pick up",
          "A sticker peeling off its backing: rising crackle and a small 'tk'.",
          "Lifting a clue sticker from the tray (mystery-first §5).",
          sticker_pick, takes=4, budget=(0.15, 0.45), group="Notebook and UI",
          listen_for="Satisfying peel? Not scratchy or harsh on the phone?"),
    Sound("note_sticker_drop", "notebook", "Sticker: press in",
          "A soft felt pat with a small wooden 'tuk' and the glue gripping.",
          "Placing a sticker on a question slot.",
          sticker_drop, takes=4, budget=(0.1, 0.4), group="Notebook and UI",
          listen_for="Does it feel like pressing something down, tactile and cosy?"),
    Sound("note_sticker_wobble", "notebook", "Sticker: wobble back",
          "A friendly rubbery 'boing-oing' and a soft 'tok' as the sticker hops home.",
          "A wrong set: gentle, never a failure buzzer (D26).",
          sticker_wobble, takes=3, budget=(0.45, 0.9), group="Notebook and UI",
          listen_for="Playful 'try again', not 'wrong!'? Would a 6-year-old smile?"),
    Sound("note_correct_glow", "notebook", "Correct: glow",
          "A warm rising celesta arpeggio that blooms into a soft glow with sparkles.",
          "A right placement: the answer appears (no fanfare, a warm reward).",
          correct_glow, takes=3, budget=(1.2, 2.6), group="Notebook and UI",
          listen_for="Warm and magical, not a slot-machine win? Not too long?"),
    Sound("ui_tap", "ui", "UI tap",
          "A short, soft wooden 'tup' of a felt button.",
          "Every button and control (spec §6: sound on every control).",
          ui_tap, takes=5, budget=(0.04, 0.2), group="Notebook and UI",
          listen_for="Pleasant after 100 taps? Too clicky or too dull?"),
]

_VOICE_INFO = {
    "pip": ("Pip", "The curious new kid: a bright child voice, medium pace, questions that rise."),
    "bea": ("Bea", "Fast and sure: higher, quicker, bouncier."),
    "vale": ("Mr. Vale", "Slow and dry: a kind old man, narrow melody, a gentle creak at the end of a thought."),
    "june": ("June", "Conspiratorial: half-whispered, close, quick little bursts and a giggle."),
    "kit": ("Aunt Kit", "Theatrical: wide swoops, warm vibrato, generous vowels."),
}
for _char, (_name, _desc) in _VOICE_INFO.items():
    SOUNDS.append(Sound(
        f"voice_{_char}", "voice", f"Babble: {_name}", _desc,
        "Non-verbal babble under the caption when the character speaks (D20, D23.1). Each take is a different "
        "line.", babble(_char), takes=3, budget=(1.0, 4.8), group="Voices",
        listen_for="Does it sound like this character? Cute and kind, never creepy or robotic? Too buzzy?"))

SOUNDS += [
    Sound("amb_night", "ambience", "Night ambience loop",
          "A gentle autumn night: soft gusts of wind, leaves skittering, two far-off crickets. Stereo, 24 s, loops "
          "seamlessly.",
          "Bed under the street (spec §6: ambient life that never competes with evidence).",
          night_ambience, takes=3, budget=(20.0, 30.0), loop=True, group="Ambience and music",
          listen_for="Calm, not spooky-scary? Is the loop point audible? Are the crickets too sharp?"),
    Sound("music_motif_pluck", "music", "Motif: plucked (opening)",
          "The three-note motif in the knock rhythm (short, short, long) on a soft plucked string.",
          "Opening music seed (spec §6: sparse plucked strings).",
          motif_pluck, takes=3, budget=(2.5, 5.0), group="Ambience and music",
          listen_for="Gentle and curious? Does the string sound like a real instrument?"),
    Sound("music_motif_wood", "music", "Motif: wood with a warm chord (closure)",
          "The same motif on a wooden marimba, then a warmer rolled D major chord.",
          "Case closure (spec §6: the motif on wood with a warmer final chord).",
          motif_wood, takes=3, budget=(3.0, 6.5), group="Ambience and music",
          listen_for="Does the final chord feel warm and 'case solved'?"),
]


def by_id():
    return {s.id: s for s in SOUNDS}
