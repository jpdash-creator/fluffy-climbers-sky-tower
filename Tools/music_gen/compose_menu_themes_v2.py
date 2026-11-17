#!/usr/bin/env python3
"""Five NEW original menu-music candidates for Fluffy Climbers (owner request 2026-11-17).

Pure numpy synthesis, no samples, no third-party material, no licence needed. Deterministic (fixed seeds, no clock).
All five are seamless 16-bar loops (notes, echoes, filters and ducking wrap around the loop point),
44.1 kHz stereo 16 bit, levelled to about RMS 6000 / peak under 26000 of 32767, warm mid register.

  python Tools/music_gen/compose_menu_themes_v2.py [outdir]

Default outdir: Tools/music_gen/candidates_2026-11-17/. Writes <n>_<name>.wav plus <n>_<name>_preview.wav
(22.05 kHz mono, the full loop) and prints a stats table (duration, RMS, peak, loop-join check, spectrum).

Melodies are written as 16-character bars (one character per sixteenth note):
  1..7 = scale degrees of the key from the key root, a..n = the same one octave up (and beyond), A..G = lower octave,
  '-' = hold the previous note, '.' = rest/gap. Chords per bar come from the progression (diatonic triads).
"""
import os
import sys
import wave
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import compose_menu_themes as old  # noqa: E402  (reuse Mix, kick, clap, bell, glock, pluck, ... ; the old script is not modified)

SR = old.SR
MAJ = [0, 2, 4, 5, 7, 9, 11]
MIXO = [0, 2, 4, 5, 7, 9, 10]
RNG = np.random.default_rng(20261117)


def mtof(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


def _t(n):
    return np.arange(n) / SR


def _env(n, atk, dur, rel):
    return old.envelope(n, atk, dur, rel)


# ---------------------------------------------------------------- instruments (mono float arrays)

def synth(f, dur, vel=1.0, cut=2500.0, kd=0.0, atk=0.004, rel=0.08, vib=0.0, maxf=3600.0,
          detune=0.0, voices=1, swell=0.0, duty=None, cutfn=None, t0=0.0):
    """Band-limited additive saw (or pulse when duty is given). cut = gentle spectral roll-off in Hz,
    kd = per-harmonic decay (a plucky filter envelope), swell = brass-like brightness rise, cutfn = moving cutoff."""
    n = int(SR * (dur + rel * 5))
    t = _t(n)
    dets = [0.0] if voices == 1 else [-detune, 0.0, detune]
    out = np.zeros(n)
    cuta = cutfn(t0 + t) if cutfn is not None else cut
    for vi, dc in enumerate(dets):
        ff = f * 2.0 ** (dc / 1200.0)
        if vib:
            ph = 2 * np.pi * np.cumsum(ff * (1 + vib * np.sin(2 * np.pi * 5.3 * t) * np.minimum(1.0, t / 0.25))) / SR
        else:
            ph = 2 * np.pi * ff * t
        ph = ph + vi * 1.7
        K = max(1, min(48, int(maxf / ff)))
        for k in range(1, K + 1):
            a = 1.0 / k / np.sqrt(1.0 + (k * ff / cuta) ** 2)
            if kd:
                a = a * np.exp(-t * (k - 1) * kd)
            if swell:
                a = a * np.minimum(1.0, t / (swell * (1 + 0.25 * k)))
            if duty is None:
                out += a * np.sin(k * ph)
            else:
                out += a * 2.0 * np.sin(np.pi * k * duty) * np.cos(k * ph - np.pi * k * duty)
    out *= _env(n, atk, dur, rel) * vel * 0.5 / np.sqrt(len(dets))
    return out


def chip_pulse(f, dur, vel=1.0, duty=0.25):
    return synth(f, dur, vel * 0.9, cut=3000, atk=0.002, rel=0.025, duty=duty, maxf=3400)


def chip_tri(f, dur, vel=1.0):
    n = int(SR * (dur + 0.05))
    t = _t(n)
    y = (2 / np.pi) * np.arcsin(np.sin(2 * np.pi * f * t))
    y = np.round(y * 7.5) / 7.5            # 4-bit staircase, the arcade flavour
    y *= _env(n, 0.002, dur * 0.95, 0.02)
    return y * vel * 0.9


def sub_sine(f, dur, vel=1.0, decay=0.35):
    n = int(SR * (dur + 0.08))
    t = _t(n)
    y = np.sin(2 * np.pi * f * t) * np.exp(-t / decay)
    y *= _env(n, 0.004, dur, 0.05)
    return y * vel


def boing_bass(f, dur, vel=1.0):
    n = int(SR * (dur + 0.1))
    t = _t(n)
    fr = f * (1 + 0.16 * np.exp(-t / 0.035))
    ph = 2 * np.pi * np.cumsum(fr) / SR
    y = np.sin(ph) + 0.3 * np.sin(2 * ph) * np.exp(-t / 0.12)
    y *= _env(n, 0.004, dur * 0.85, 0.05) * np.exp(-t / 0.4)
    return y * vel * 0.9


def tom(vel=1.0, f0=110.0):
    n = int(SR * 0.45)
    t = _t(n)
    fr = f0 * (1 + 0.9 * np.exp(-t / 0.035))
    ph = 2 * np.pi * np.cumsum(fr) / SR
    return np.sin(ph) * np.exp(-t / 0.16) * vel


def _soft_noise(n, lp=2):
    x = RNG.uniform(-1, 1, n + 8)
    if lp > 1:
        x = np.convolve(x, np.ones(lp) / lp, mode="same")
    return x[:n]


def snare(vel=1.0):
    n = int(SR * 0.22)
    t = _t(n)
    y = _soft_noise(n, 3) * np.exp(-t / 0.07) + 0.7 * np.sin(2 * np.pi * 185 * t) * np.exp(-t / 0.045)
    return y * vel * 0.8


def chip_snare(vel=1.0):
    n = int(SR * 0.13)
    t = _t(n)
    nz = np.repeat(RNG.uniform(-1, 1, n // 3 + 2), 3)[:n]
    return nz * np.exp(-t / 0.045) * vel * 0.7


def chip_hat(vel=1.0):
    n = int(SR * 0.04)
    t = _t(n)
    nz = np.repeat(RNG.uniform(-1, 1, n // 2 + 2), 2)[:n]
    return nz * np.exp(-t / 0.011) * vel * 0.4


def chip_kick(vel=1.0):
    n = int(SR * 0.16)
    t = _t(n)
    fr = 50 + 150 * np.exp(-t / 0.025)
    ph = 2 * np.pi * np.cumsum(fr) / SR
    y = np.sign(np.sin(ph)) * 0.5 + np.sin(ph) * 0.5
    return y * np.exp(-t / 0.06) * vel * 0.8


def hat(vel=1.0, open_=False):
    n = int(SR * (0.2 if open_ else 0.05))
    t = _t(n)
    y = np.diff(RNG.uniform(-1, 1, n + 1))
    y = np.convolve(y, np.ones(2) / 2, mode="same")
    return y * np.exp(-t / (0.07 if open_ else 0.014)) * vel * 0.5


def crash(vel=1.0):
    n = int(SR * 1.3)
    t = _t(n)
    y = np.diff(RNG.uniform(-1, 1, n + 1))
    y = np.convolve(y, np.ones(3) / 3, mode="same")
    return y * np.exp(-t / 0.4) * vel * 0.45


def riser(dur, vel=1.0):
    n = int(SR * dur)
    t = _t(n)
    x = t / dur
    nz = old.lowpass_circular(_soft_noise(n, 2), 3000)
    sw = np.sin(2 * np.pi * np.cumsum(180 * 2.0 ** (x * 3.0)) / SR)
    return (nz * 0.8 + sw * 0.5) * (x ** 2.2) * vel * 0.6


# ---------------------------------------------------------------- song helper

def _deg(c):
    if c.isdigit():
        return int(c) - 1
    if c.islower():
        return 7 + ord(c) - 97
    return ord(c) - 65 - 7


def parse_bar(txt):
    txt = txt.replace(" ", "").replace("|", "")
    assert len(txt) % 16 == 0, "bad bar length %d: %r" % (len(txt), txt)
    ev = []
    i = 0
    L = len(txt)
    while i < L:
        c = txt[i]
        if c in ".-":
            i += 1
            continue
        j = i + 1
        while j < L and txt[j] == "-":
            j += 1
        k = j
        while k < L and txt[k] == ".":
            k += 1
        ev.append((i, _deg(c), (j - i) + min(1, k - j)))
        i = j
    return ev


class Song:
    def __init__(self, bpm, root, scale, prog, swing=0.0):
        self.bpm, self.root, self.scale, self.prog, self.swing = bpm, root, scale, prog, swing
        self.step = 60.0 / bpm / 4.0
        self.bars = 16
        self.n = int(round(self.bars * 16 * self.step * SR))
        self.mix = old.Mix(self.n)
        self.warn = []

    def tm(self, bar, st):
        t = (bar * 16 + st) * self.step
        if st % 2 == 1:
            t += self.swing * self.step
        return t

    def note(self, d):
        o, i = divmod(d, 7)
        return self.root + self.scale[i] + 12 * o

    def tones(self, bar):
        d = self.prog[bar]
        return [self.note(d), self.note(d + 2), self.note(d + 4)]

    def ct(self, bar):
        tri = self.tones(bar)
        return tri + [x + 12 for x in tri]

    def add(self, bus, t, sig, pan=0.0, gain=1.0):
        self.mix.add(bus, t, sig, pan, gain)

    def mel(self, bus, fn, strings, bar0=0, shift=0, vel=0.9, pan=0.0, gate=0.92):
        for bi, txt in enumerate(strings):
            bar = bar0 + bi
            pcs = {x % 12 for x in self.tones(bar)}
            for (st, deg, ln) in parse_bar(txt):
                m = self.note(deg)
                if st in (0, 4, 8, 12) and (m % 12) not in pcs:
                    self.warn.append("%s bar %d step %d: pitch class %d not in chord" % (bus, bar, st, m % 12))
                d = ln * self.step * gate
                self.add(bus, self.tm(bar, st), fn(mtof(m + shift), d, vel * (1.0 if st % 4 == 0 else 0.82)), pan)

    def duck(self, buses, depth, rel, per_bar_beats=4):
        beats = self.bars * per_bar_beats
        ph = (np.arange(self.n) / self.n * beats) % 1.0
        e = 1.0 - depth * np.exp(-(ph * 60.0 / self.bpm) / rel)
        for b in buses:
            if b in self.mix.buses:
                self.mix.buses[b][0] *= e
                self.mix.buses[b][1] *= e


def finish(song, gains, lp=5200.0, rms_target=0.1925, ceil=0.79):
    L, R = song.mix.render(gains)
    for _ in range(2):
        L, R = old.lowpass_circular(L, lp), old.lowpass_circular(R, lp)
    L -= L.mean()
    R -= R.mean()
    for _ in range(6):                       # level to the RMS target, soft-limit the peaks, repeat
        rms = np.sqrt(np.mean((L ** 2 + R ** 2) / 2))
        g = rms_target / max(rms, 1e-9)
        L, R = L * g, R * g
        L, R = ceil * np.tanh(L / ceil), ceil * np.tanh(R / ceil)
    pk = max(np.abs(L).max(), np.abs(R).max())
    if pk > 0.79:
        L, R = L * 0.79 / pk, R * 0.79 / pk
    return L, R


# ---------------------------------------------------------------- theme 1: retro 8-bit arcade

def theme_chip():
    prog = [0, 4, 5, 3, 0, 4, 5, 4, 3, 0, 4, 5, 3, 4, 5, 4]
    s = Song(148, 67, MAJ, prog)
    lead = [
        "5.5.a---5.3.5.3.", "5.7.b---7.5.7.5.", "6.a.c---a.6.a.6.", "4.6.a---6.5.6.7.",
        "a.a.e---a.c.e.c.", "e.c.b---7.b.7.5.", "6.a.c.e.f---c.a.", "b.g.e.c.b.7.5.7.",
        "d.d.f.d.a.a.f.d.", "c.c.a.c.e.e.c.a.", "e.e.b.e.g.g.e.b.", "f.f.c.f.a-a-c-e-",
        "dfadfadfadfadfad", "egbegbegbegbegbe", "facfacfacfacfacf", "e-g-b-g-e-7-5-4-",
    ]
    s.mel("lead", lambda f, d, v: chip_pulse(f, d, v, 0.25), lead[:8], 0, 0, 0.85)
    s.mel("lead", lambda f, d, v: chip_pulse(f, d, v, 0.5), lead[8:], 8, 0, 0.8)
    # soft octave-up sparkle shadow of the hook (second voice, quieter, offset a sixteenth)
    s.mel("lead2", lambda f, d, v: chip_pulse(f, d, v, 0.125), lead[4:8], 4, 12, 0.35, 0.3)
    for bar in range(16):
        ct = s.ct(bar)
        # arpeggio voice (lower octave, under the lead)
        for st in range(16):
            idx = (0, 1, 2, 1)[st % 4]
            if bar < 4 and st % 2:
                continue
            g = 0.30 if bar < 8 else 0.40
            s.add("arp", s.tm(bar, st), chip_pulse(mtof(ct[idx] - 12), s.step * 0.8, g, 0.125),
                  -0.4 if st % 2 == 0 else 0.4)
        # triangle bass
        r = s.note(s.prog[bar]) - 24
        pat = [(0, 0), (2, 0), (4, 12), (6, 0), (8, 0), (10, 0), (12, 12), (14, 7)]
        for st, k in pat:
            s.add("bass", s.tm(bar, st), chip_tri(mtof(r + k), s.step * 1.7, 0.9))
        # drums
        for st in (0, 8) + ((6,) if bar >= 8 else ()):
            s.add("drums", s.tm(bar, st), chip_kick(1.0), 0.0)
        for st in (4, 12):
            s.add("drums", s.tm(bar, st), chip_snare(1.0), 0.05)
        step_h = 2 if bar < 8 else 1
        for st in range(0, 16, step_h):
            s.add("drums", s.tm(bar, st), chip_hat(1.0 if st % 4 == 0 else 0.6), 0.25)
        if bar in (3, 7, 11):
            for st in range(12, 16):
                s.add("drums", s.tm(bar, st), chip_snare(0.6 + 0.12 * (st - 12)), 0.0)
        if bar == 15:
            for st in range(8, 16):
                s.add("drums", s.tm(bar, st), chip_snare(0.4 + 0.08 * (st - 8)), 0.0)
    s.mix.echo("lead", s.step * 3, s.step * 6, 0.28, 0.14)
    s.mix.echo("arp", s.step * 2, s.step * 4, 0.12, 0.06)
    return s, {"lead": 0.95, "lead2": 0.6, "arp": 0.75, "bass": 0.95, "drums": 0.8}, 5600.0


# ---------------------------------------------------------------- theme 2: cheerful electro / techno

def theme_techno():
    prog = [0, 4, 5, 3, 0, 4, 3, 4, 5, 3, 0, 4, 5, 3, 4, 4]
    s = Song(128, 62, MAJ, prog)
    lead = [
        "1..3..5.a..5.3..", "5..7..b.e..b.7..", "6..a..c.a..c.a..", "4..6..a.d..a.6.7",
        "a..c..e.a..e.c..", "b..e..g.e..b.7..", "d..f..a.d..a.f..", "e.e.e.g.e.b.7.5.",
        "c--.c.e-c--.a.6.", "d--.d.f-d--.a.6.", "a--.a.c-e--.c.a.", "b--.b.e-g--.e.b.",
        "cacacacacacacaca", "dfdfdfdfdfdfdfdf", "egegegegegegegeg", "e.e.e.e.e.e.....",
    ]
    fn = lambda f, d, v: synth(f, d, v * 1.1, cut=1700, kd=1.6, atk=0.005, rel=0.09, vib=0.0, detune=9, voices=3)
    s.mel("lead", fn, lead, 0, 0, 0.8)
    sweep = lambda tt_: 260.0 * (2200.0 / 260.0) ** ((tt_ / (s.step * 16 * 8)) % 1.0)
    for bar in range(16):
        tri = s.tones(bar)
        t0 = s.tm(bar, 0)
        # sweeping pad (filter opens over each 8 bars), ducked by the kick
        for k, nt in enumerate(tri):
            s.add("pad", t0, synth(mtof(nt), s.step * 16, 0.55, atk=0.05, rel=0.2, detune=11, voices=3,
                                   cutfn=sweep, t0=t0), (-0.5, 0.0, 0.5)[k])
        # off-beat stabs (mid register)
        for st in (3, 6, 11):
            for k, nt in enumerate(tri):
                s.add("stab", s.tm(bar, st), synth(mtof(nt + 12), s.step * 1.3, 0.28, cut=1800, kd=6.0, atk=0.003, rel=0.05),
                      (-0.35, 0.0, 0.35)[k])
        # off-beat bass
        r = s.note(s.prog[bar]) - 24
        for st in (2, 6, 10, 14):
            s.add("bass", s.tm(bar, st), synth(mtof(r), s.step * 2.2, 1.0, cut=520, kd=1.2, atk=0.004, rel=0.05, maxf=900), 0.0)
            s.add("bass", s.tm(bar, st), sub_sine(mtof(r), s.step * 2.2, 0.55), 0.0)
        if bar % 4 == 3:
            s.add("bass", s.tm(bar, 15), synth(mtof(r + 12), s.step * 1.0, 0.8, cut=520, kd=1.2, maxf=900))
        # drums
        no_kick = bar in (8, 9)
        for st in (0, 4, 8, 12):
            if not no_kick:
                s.add("drums", s.tm(bar, st), old.kick(1.0), 0.0)
        for st in (4, 12):
            s.add("drums", s.tm(bar, st), old.clap(0.8), 0.05)
        for st in (2, 6, 10, 14):
            s.add("drums", s.tm(bar, st), hat(0.8, True), 0.3)
        if bar >= 4:
            for st in (1, 3, 5, 7, 9, 11, 13, 15):
                s.add("drums", s.tm(bar, st), hat(0.32), -0.3)
        if bar in (0, 8):
            s.add("fx", s.tm(bar, 0), crash(0.7), 0.0)
        if bar in (7, 15):
            for st in range(8, 16):
                s.add("drums", s.tm(bar, st), snare(0.35 + 0.08 * (st - 8)), 0.0)
            s.add("fx", s.tm(bar, 0), riser(s.step * 16, 0.8), 0.0)
        elif bar in (3, 11):
            s.add("drums", s.tm(bar, 14), snare(0.7), 0.0)
            s.add("drums", s.tm(bar, 15), snare(0.9), 0.0)
    s.duck(["pad", "stab", "bass"], 0.62, 0.17)
    s.mix.echo("lead", s.step * 3, s.step * 6, 0.24, 0.12)
    s.mix.echo("stab", s.step * 3, s.step * 6, 0.18, 0.08)
    return s, {"lead": 0.9, "pad": 0.5, "stab": 0.7, "bass": 0.9, "drums": 0.8, "fx": 0.55}, 5000.0


# ---------------------------------------------------------------- theme 3: kawaii pop

def theme_kawaii():
    prog = [0, 2, 3, 4, 0, 5, 3, 4, 3, 4, 2, 5, 1, 4, 3, 4]
    s = Song(126, 65, MAJ, prog)
    lead = [
        "5.3.1.3.5-..a-..", "7.5.3.5.7-..c-..", "6.4.1.4.6-..d-..", "7.5.2.5.7-..b-e-",
        "a.c.e.c.a-..5-..", "f.c.a.c.f-..a-..", "d.a.6.a.d-..a-..", "e.b.7.b.e-b-7-5-",
        "d.a.d.a.6.a.6.a.", "e.b.e.b.7.b.7.b.", "c.e.c.e.7.e.7.c.", "f.c.a.c.6.a.c.f.",
        "b-b-d-d-f-f-d-b-", "e-e-b-b-e-e-b-7-", "d-d-a-a-d-d-a-6-", "e.e.b.b.7.7.5...",
    ]
    bellv = lambda f, d, v: old.bell(f, d, v * 1.1, 0.7)
    s.mel("lead", bellv, lead, 0, 0, 0.9, 0.0)
    # music-box echo layer in the second half, an octave up, softer, to the right
    s.mel("lead2", lambda f, d, v: old.glock(f, d, v), lead[8:], 8, 12, 0.35, 0.45)
    for bar in range(16):
        tri = s.tones(bar)
        ct = s.ct(bar)
        # pizzicato chords, off-beats
        for st in (2, 6, 11, 14):
            for k, nt in enumerate(tri):
                s.add("pizz", s.tm(bar, st) + k * 0.007, old.pluck(mtof(nt), s.step * 1.2, 0.45, 0.992),
                      -0.3 + 0.3 * k)
        # bouncy bass
        r = s.note(s.prog[bar]) - 24
        for st, k in ((0, 0), (3, 12), (6, 0), (8, 0), (11, 12), (14, 7)):
            s.add("bass", s.tm(bar, st), boing_bass(mtof(r + k), s.step * 2.0, 0.9))
        # twinkles
        if bar % 2 == 1:
            for j, st in enumerate((12, 13, 14, 15)):
                s.add("spark", s.tm(bar, st), old.glock(mtof(ct[(j + 1) % 6] + 12), s.step * 1.5, 0.35), -0.5 + 0.35 * j)
        if bar in (7, 15):
            for j in range(8):
                s.add("spark", s.tm(bar, 8 + j), old.glock(mtof(ct[j % 6] + (12 if j < 6 else 24)), s.step * 1.5, 0.3 + 0.03 * j),
                      -0.6 + 0.17 * j)
        # drums
        for st in (0, 8) + ((6,) if bar >= 8 else ()):
            s.add("drums", s.tm(bar, st), old.kick(0.85), 0.0)
        for st in (4, 12):
            s.add("drums", s.tm(bar, st), old.clap(0.7), 0.1)
        for st in range(0, 16, 2):
            s.add("drums", s.tm(bar, st), old.shaker(0.8 if st % 4 == 0 else 0.5), 0.35)
        if bar >= 8:
            for st in (3, 7, 11, 15):
                s.add("drums", s.tm(bar, st), hat(0.4), -0.3)
        if bar == 15:
            for st in range(12, 16):
                s.add("drums", s.tm(bar, st), old.clap(0.4 + 0.12 * (st - 12)), 0.0)
        if bar in (0, 8):
            s.add("fx", s.tm(bar, 0), crash(0.4), 0.0)
    s.mix.echo("lead", s.step * 3, s.step * 6, 0.25, 0.14)
    s.mix.echo("spark", s.step * 3, s.step * 5, 0.28, 0.16)
    s.mix.echo("lead2", s.step * 4, s.step * 8, 0.3, 0.15)
    return s, {"lead": 0.95, "lead2": 0.6, "pizz": 0.9, "bass": 0.95, "spark": 0.55, "drums": 0.7, "fx": 0.6}, 5200.0


# ---------------------------------------------------------------- theme 4: casual-mobile groove

def theme_groove():
    prog = [0, 5, 3, 4, 0, 2, 3, 4, 3, 4, 5, 0, 3, 4, 5, 4]
    s = Song(112, 63, MAJ, prog, swing=0.14)
    lead = [
        "1..3.5.a..5..3..", "6..a.c.a..c..6..", "4..6.a.d..a..6..", "5..7.b.e..b..7..",
        "a..c.e.a..e..c..", "c..e.7.c..e..7..", "d..f.a.d..a..6..", "e..b.7.5.7.b.e..",
        "a.d.a.6.a.d.f.d.", "b.e.b.7.b.e.g.e.", "c.f.c.a.c.f.c.a.", "e.a.e.c.e.h.e.c.",
        "d-d-d-d-f-f-d-a-", "e-e-g-g-e-e-b-7-", "f-f-c-c-f-f-c-a-", "e.e.g.g.e.b.7.5.",
    ]
    fn = lambda f, d, v: synth(f, d, v * 1.15, cut=2300, kd=3.0, atk=0.003, rel=0.1, detune=7, voices=3)
    s.mel("lead", fn, lead[:8], 0, 0, 0.85)
    s.mel("lead", fn, lead[8:], 8, 0, 0.85)
    s.mel("lead2", lambda f, d, v: old.glock(f, d, v), lead[8:], 8, 12, 0.3, 0.4)
    for bar in range(16):
        tri = s.tones(bar)
        # bright chord plucks (syncopated)
        for st in (2, 7, 10):
            for k, nt in enumerate(tri):
                s.add("pluck", s.tm(bar, st), synth(mtof(nt + 12), s.step * 1.4, 0.3, cut=2200, kd=8.0, atk=0.002, rel=0.05),
                      (-0.4, 0.0, 0.4)[k])
        # funk bass (saw with a falling filter) plus sub
        r = s.note(s.prog[bar]) - 24
        pat = [(0, 0, 2), (3, 0, 1), (6, 12, 1), (8, 0, 2), (10, 7, 1), (11, 0, 1), (14, 12, 1)]
        if bar % 4 == 3:
            pat = [(0, 0, 2), (3, 0, 1), (6, 12, 1), (8, 0, 2), (11, 7, 1), (12, 0, 1), (14, 10, 1), (15, 12, 1)]
        for st, k, ln in pat:
            d = s.step * (ln * 0.9 + 0.5)
            s.add("bass", s.tm(bar, st), synth(mtof(r + k), d, 1.0, cut=900, kd=5.0, atk=0.003, rel=0.04, maxf=1500))
            s.add("bass", s.tm(bar, st), sub_sine(mtof(r + k), d, 0.5, 0.25))
        # drums
        for st in (0, 6, 8, 11):
            s.add("drums", s.tm(bar, st), old.kick(0.9), 0.0)
        for st in (4, 12):
            s.add("drums", s.tm(bar, st), old.clap(0.95), 0.05)
        for st in range(16):
            s.add("drums", s.tm(bar, st), hat(0.7 if st % 4 == 0 else (0.5 if st % 2 == 0 else 0.28)), -0.3)
        if bar % 2 == 1:
            s.add("drums", s.tm(bar, 14), hat(0.8, True), 0.3)
        if bar in (3, 7, 11):
            s.add("drums", s.tm(bar, 14), old.clap(0.6), 0.0)
            s.add("drums", s.tm(bar, 15), old.clap(0.8), 0.0)
        if bar == 15:
            for st in range(12, 16):
                s.add("drums", s.tm(bar, st), snare(0.4 + 0.15 * (st - 12)), 0.0)
            s.add("fx", s.tm(bar, 0), riser(s.step * 16, 0.6), 0.0)
        if bar in (0, 8):
            s.add("fx", s.tm(bar, 0), crash(0.5), 0.0)
    s.duck(["bass", "pluck"], 0.3, 0.12)
    s.mix.echo("lead", s.step * 3, s.step * 6, 0.22, 0.1)
    s.mix.echo("lead2", s.step * 3, s.step * 6, 0.3, 0.15)
    return s, {"lead": 0.95, "lead2": 0.6, "pluck": 0.7, "bass": 0.95, "drums": 0.8, "fx": 0.5}, 5200.0


# ---------------------------------------------------------------- theme 5: playful heroic adventure

def theme_epic():
    prog = [0, 6, 3, 0, 0, 6, 3, 6, 5, 3, 0, 6, 1, 3, 6, 6]
    s = Song(140, 60, MIXO, prog)
    lead = [
        "1.1.5---3.5.a---", "7.7.b---d.b.7---", "4.4.6---a.6.4---", "5.5.a---c.c.e---",
        "a-.a.c.e---c.a.5", "g.g.d---d.b.d---", "d.d.f---f.d.a---", "g.g.g.g.d.d.d.d.",
        "6.a.c---f---c.a.", "d.f.a---d.f.h---", "e.g.a---c.e.h---", "g.g.d.d.b.b.7---",
        "b.b.d.d.f.f.d.d.", "d.d.f.f.h.h.f.f.", "7.b.d.g.i.g.d.b.", "g.g.g.g.d-d-b---",
    ]
    brass = lambda f, d, v: synth(f, d, v * 1.25, cut=1500, atk=0.015, rel=0.12, vib=0.004, detune=7, voices=3, swell=0.045)
    s.mel("lead", brass, lead, 0, 0, 0.85)
    s.mel("horn", lambda f, d, v: synth(f, d, v, cut=900, atk=0.03, rel=0.15, swell=0.06, vib=0.003), lead[8:], 8, -12, 0.6)
    for bar in range(16):
        tri = s.tones(bar)
        ct = s.ct(bar)
        t0 = s.tm(bar, 0)
        # string-like 16th ostinato under the lead
        for st in range(16):
            idx = (0, 1, 2, 1)[st % 4]
            s.add("ost", s.tm(bar, st), synth(mtof(ct[idx] - 12), s.step * 1.4, 0.5, cut=1400, kd=7.0, atk=0.003, rel=0.04),
                  -0.45 if st % 2 == 0 else 0.45)
        # sustained brass-pad chord on the B part
        if bar >= 8:
            for k, nt in enumerate(tri):
                s.add("pad", t0, synth(mtof(nt - 12), s.step * 16, 0.35, cut=900, atk=0.08, rel=0.2, swell=0.1, voices=3, detune=8),
                      (-0.5, 0.0, 0.5)[k])
        # driving bass: eighths with the fifth on the last
        r = s.note(s.prog[bar]) - 24
        for st in range(0, 16, 2):
            kk = 7 if st == 14 else (12 if st in (6, 10) else 0)
            s.add("bass", s.tm(bar, st), synth(mtof(r + kk), s.step * 1.7, 0.95, cut=650, kd=1.5, atk=0.004, rel=0.04, maxf=1200))
        s.add("bass", t0, sub_sine(mtof(r), s.step * 3.0, 0.5, 0.5))
        # drums: kick on the quarters, snare backbeat, taiko toms, tambourine eighths
        for st in (0, 4, 8, 12):
            s.add("drums", s.tm(bar, st), old.kick(0.9), 0.0)
        for st in (4, 12):
            s.add("drums", s.tm(bar, st), snare(0.9), 0.0)
        for st in range(0, 16, 2):
            s.add("drums", s.tm(bar, st), old.shaker(0.8 if st % 4 == 0 else 0.55), 0.35)
        s.add("drums", s.tm(bar, 0), tom(0.9, 70), -0.2)
        if bar >= 8:
            for st in (6, 10, 14):
                s.add("drums", s.tm(bar, st), tom(0.55, 95), 0.2)
        if bar in (3, 7, 11, 15):
            for j, st in enumerate(range(12, 16)):
                s.add("drums", s.tm(bar, st), tom(0.75, 150 - 18 * j), (-0.5, -0.15, 0.2, 0.5)[j])
        if bar in (0, 8):
            s.add("fx", s.tm(bar, 0), crash(0.8), 0.0)
        if bar == 15:
            s.add("fx", s.tm(bar, 0), riser(s.step * 16, 0.5), 0.0)
    s.mix.echo("lead", s.step * 3, s.step * 6, 0.2, 0.1)
    s.mix.echo("horn", s.step * 4, s.step * 8, 0.2, 0.1)
    return s, {"lead": 0.95, "horn": 0.7, "ost": 0.55, "pad": 0.6, "bass": 0.95, "drums": 0.85, "fx": 0.6}, 5200.0


THEMES = [
    ("1_arcade_sprint", "retro 8-bit chiptune arcade", theme_chip, "G major", 148),
    ("2_sky_circuit", "cheerful driving electro", theme_techno, "D major", 128),
    ("3_kawaii_bounce", "kawaii pop game", theme_kawaii, "F major", 126),
    ("4_happy_groove", "casual-mobile funky groove", theme_groove, "E-flat major", 112),
    ("5_summit_quest", "playful heroic adventure", theme_epic, "C mixolydian (heroic major)", 140),
]


# ---------------------------------------------------------------- io and checks

def save_wav(path, L, R=None, rate=SR):
    old.save_wav(path, L, R, rate)


def read_wav(path):
    with wave.open(path, "rb") as w:
        raw = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64)
        ch = w.getnchannels()
        return raw.reshape(-1, ch) / 32768.0, w.getframerate()


def stats(path):
    x, sr = read_wav(path)
    n = len(x)
    rms = np.sqrt(np.mean(x ** 2)) * 32768
    pk = np.abs(x).max() * 32768
    # loop join: the step across the wrap vs the typical step inside the file
    mono = x.mean(axis=1)
    inner = np.abs(np.diff(mono))
    wrap = abs(mono[0] - mono[-1])
    n50 = int(0.05 * sr)
    a, b = mono[-n50:], mono[:n50]
    ra, rb = np.sqrt(np.mean(a ** 2)), np.sqrt(np.mean(b ** 2))
    spec = np.abs(np.fft.rfft(mono)) ** 2
    fr = np.fft.rfftfreq(n, 1.0 / sr)
    cent = (fr * spec).sum() / spec.sum()
    hi = spec[fr > 4000].sum() / spec.sum() * 100
    return dict(sec=n / sr, rms=rms, peak=pk, wrap=wrap, wrap_ratio=wrap / (np.percentile(inner, 99.9) + 1e-9),
                r50=(ra, rb), cent=cent, hi4=hi, clip=int((np.abs(x) >= 0.9999).sum()))


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "candidates_2026-11-17")
    os.makedirs(out, exist_ok=True)
    rows = []
    for name, desc, fn, key, bpm in THEMES:
        s, gains, lp = fn()
        for w in s.warn:
            print("  note check:", name, w)
        L, R = finish(s, gains, lp)
        p = os.path.join(out, name + ".wav")
        save_wav(p, L, R)
        save_wav(os.path.join(out, name + "_preview.wav"), old.preview(L, R), None, SR // 2)
        rows.append((name, key, bpm, stats(p)))
    print("%-18s %-12s %4s %6s %7s %7s %9s %6s %7s %5s" % ("file", "key", "bpm", "sec", "rms", "peak", "join(x99.9)", "cent", "hi>4k%", "clip"))
    for name, key, bpm, st in rows:
        print("%-18s %-12s %4d %6.2f %7.0f %7.0f %9.2f %8.0f %6.2f %5d  last/first 50ms rms %.3f/%.3f" % (
            name, key.split()[0] + " " + key.split()[1][:3], bpm, st["sec"], st["rms"], st["peak"], st["wrap_ratio"],
            st["cent"], st["hi4"], st["clip"], st["r50"][0], st["r50"][1]))


if __name__ == "__main__":
    main()
