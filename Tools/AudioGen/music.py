"""
music.py - Genera la musica ROCK del juego de hockey patines en .wav

Estilo: rock arcade competitivo (guitarras distorsionadas a dos pistas,
bajo de puya y bateria de rock). Todas las pistas comparten el mismo
leitmotiv (La-Do-Re-Mi-Re) y la misma paleta, para que suenen al mismo juego.

USO
    py music.py                                  -> genera todas las pistas en output/music
    py music.py --out ..\\..\\Assets\\Audio\\Music   -> directamente en el proyecto de Unity
    py music.py match goldengoal                 -> solo las pistas indicadas

Pistas: credits, menu, match, hub, goldengoal, pause, victory, defeat, champion, eliminated
(La version funk anterior sigue en music_funk.py, por si la quereis para creditos.)
"""
import argparse
import time
from pathlib import Path

import numpy as np

import synth as S
import rock as R
from synth import SR, midi_hz

# Raiz (nota MIDI) del acorde de quinta en la guitarra; el bajo va una octava por debajo
ROOT = {"E5": 40, "F5": 41, "G5": 43, "A5": 45, "Bb5": 46, "B5": 47, "C5": 48, "D5": 50}

BUS_NAMES = ["kick", "drums", "bass", "gtr", "lead", "pad"]
GAINS = {"kick": 0.8, "drums": 0.85, "bass": 0.75, "gtr": 0.8, "lead": 0.65, "pad": 0.25}
SENDS = {"kick": 0.0, "drums": 0.16, "bass": 0.0, "gtr": 0.05, "lead": 0.22, "pad": 0.4}
DUCKED = ("bass", "gtr", "pad")
A_MINOR = [9, 11, 0, 2, 4, 5, 7]      # clases de nota de La menor natural


class Arr:
    """Una pieza: convierte pulsos en segundos y va sumando instrumentos."""

    def __init__(self, bpm, bars, loop=True, tail=4.0, seed=1):
        self.spb = 60.0 / bpm
        self.loop = loop
        self.length_sec = bars * 4 * self.spb
        self.buses = {n: S.Bus(self.length_sec, tail) for n in BUS_NAMES}
        self.kicks = []
        self.rng = np.random.default_rng(seed)
        self.kit = R.RockKit()
        self._cache = {}

    def sec(self, beat, human_ms=0.0):
        s = beat * self.spb + (self.rng.uniform(-human_ms, human_ms) / 1000 if human_ms else 0)
        return s + self.length_sec if (self.loop and s < 0) else max(0.0, s)

    # ---- instrumentos ----
    def gtr(self, chord, beat, dur, mute=False, vel=1.0, dive=None, drive=22.0, double=True):
        """Acorde de quinta grabado a dos guitarras (izquierda y derecha)."""
        root = ROOT[chord]
        key = (root, round(dur, 3), mute, dive, drive)
        variants = self._cache.setdefault(key, [])
        for pan in ((-0.75, 0.75) if double else (0.0,)):
            if len(variants) < 4 or dive:
                variants.append(R.power_chord(root, dur * self.spb, self.rng, mute, drive, dive=dive))
            sig = variants[self.rng.integers(len(variants))]
            self.buses["gtr"].add(sig, self.sec(beat, 6), vel, pan)

    def gtr_note(self, midi, beat, dur, mute=False, vel=1.0, drive=22.0):
        """Nota suelta de riff (nota + su octava), a dos guitarras."""
        key = ("note", midi, round(dur, 3), mute, drive)
        variants = self._cache.setdefault(key, [])
        for pan in (-0.75, 0.75):
            if len(variants) < 4:
                variants.append(R.power_chord(midi, dur * self.spb, self.rng, mute, drive, fifth=False))
            sig = variants[self.rng.integers(len(variants))]
            self.buses["gtr"].add(sig, self.sec(beat, 6), vel, pan)

    def bass(self, chord_or_midi, beat, dur, vel=1.0):
        m = ROOT[chord_or_midi] - 12 if isinstance(chord_or_midi, str) else chord_or_midi
        self.buses["bass"].add(R.bass_guitar(midi_hz(m), dur * self.spb, vel, self.rng), self.sec(beat, 3))

    def lead(self, notes, offset, vel=1.0, transpose=0, pan=0.0, drive=7.0):
        for n in notes:
            b, d, m = n[:3]
            bend = n[3] if len(n) > 3 else 0.0
            sig = R.lead_guitar(midi_hz(m + transpose), d * self.spb, vel, self.rng, bend=bend, drive=drive)
            self.buses["lead"].add(sig, self.sec(offset + b, 4), 1.0, pan)

    def pad(self, midis, beat, dur, vel=0.7):
        for m in midis:
            self.buses["pad"].add(S.pad(midi_hz(m), dur * self.spb, vel, self.rng), self.sec(beat), 1 / len(midis))

    def hit(self, name, beat, vel=1.0, pan=0.0):
        start = self.sec(beat, 0 if name == "kick" else 3)
        if name == "kick":
            self.kicks.append(start)
        self.buses["kick" if name == "kick" else "drums"].add(self.kit[name], start, vel, pan)

    # ---- mezcla ----
    def mixdown(self, mute=(), post_lowpass=None, rms_db=-14.5, duck=0.14):
        n = self.buses["kick"].buf.shape[1]
        tt = np.arange(n) / SR
        duck_env = np.ones(n)
        for k in self.kicks:
            for kk in ((k, k + self.length_sec) if self.loop else (k,)):
                i0 = int(kk * SR)
                if i0 < n:
                    i1 = min(n, i0 + int(0.3 * SR))
                    duck_env[i0:i1] = np.minimum(duck_env[i0:i1], 1 - duck * np.exp(-(tt[i0:i1] - kk) / 0.08))

        total, sends = np.zeros((2, n)), np.zeros((2, n))
        for name, bus in self.buses.items():
            if name in mute:
                continue
            b = bus.buf * GAINS[name]
            if name in DUCKED:
                b = b * duck_env
            if name == "lead":                       # eco de 3/16 (corchea con puntillo)
                d = int(0.75 * self.spb * SR)
                echo = np.zeros_like(b)
                echo[0, d:] += b[1, :-d] * 0.28
                echo[1, 2 * d:] += b[0, :-2 * d] * 0.16
                b = b + echo
            total += b
            sends += b * SENDS[name]
        total += S.apply_reverb(sends, S.reverb_ir(1.5, damp=7000), 1.0)

        L = int(self.length_sec * SR)
        if self.loop:
            out = S.fold_loop(total, L)
            if post_lowpass:
                out = S.lowpass(np.concatenate([out, out], axis=1), post_lowpass, 2)[:, L:]
        else:
            out = S.lowpass(total, post_lowpass, 2) if post_lowpass else total
            env = np.abs(out).max(axis=0)
            last = np.nonzero(env > 2e-4)[0]
            out = out[:, :min(out.shape[1], (last[-1] if len(last) else L) + int(0.05 * SR))]
            f = min(int(0.4 * SR), out.shape[1])
            out[:, -f:] *= np.linspace(1, 0, f)
        return S.master(out, rms_db, ceiling=0.93)


# ---------------------------------------------------------------------------
# Bateria: estilos de compas (en semicorcheas, 16 por compas)
# ---------------------------------------------------------------------------
BEATS = {
    "verse":  dict(kick=[0, 8, 10], snare=[4, 12], hat=list(range(0, 16, 2))),
    "drive":  dict(kick=[0, 6, 8, 14], snare=[4, 12], hat=list(range(0, 16, 2)), open=[14]),
    "chorus": dict(kick=[0, 6, 8, 10], snare=[4, 12], ride=list(range(0, 16, 2))),
    "half":   dict(kick=[0, 10], snare=[8], ride=[0, 4, 8, 12]),
    "double": dict(kick=[0, 4, 6, 8, 12, 14], snare=[4, 12], hat=list(range(16))),
    "fourfloor": dict(kick=[0, 4, 8, 12], snare=[], hat=[]),
}


def beat_bar(a, bar, style, vel=1.0):
    p = BEATS[style]
    b = bar * 4
    for s in p.get("kick", []):
        a.hit("kick", b + s / 4, vel)
    for s in p.get("snare", []):
        a.hit("snare", b + s / 4, vel)
    for s in p.get("hat", []):
        if s in p.get("open", []):
            a.hit("openhat", b + s / 4, 0.8 * vel, 0.3)
        else:
            a.hit("hat", b + s / 4, (0.8 if s % 4 == 0 else 0.55) * vel, 0.3)
    for s in p.get("ride", []):
        a.hit("ride", b + s / 4, (0.75 if s % 4 == 0 else 0.5) * vel, -0.3)


def fill(a, bar, start_step=8, kind="toms"):
    names = {"toms": ["snare", "snare", "tom_hi", "tom_hi", "tom_mid", "tom_mid", "tom_lo", "tom_lo"],
             "snare": ["snare"] * 8}[kind]
    for i, s in enumerate(range(start_step, 16)):
        nm = names[min(len(names) - 1, i * len(names) // max(1, 16 - start_step))]
        a.hit(nm, bar * 4 + s / 4, 0.7 + 0.3 * i / max(1, 15 - start_step), 0.4 - 0.1 * i)


def crash(a, beat, vel=0.9):
    a.hit("crash", beat, vel, -0.2)
    a.hit("kick", beat, 1.0)


def harmonize(notes, degrees=2):
    """Segunda guitarra solista: la misma melodia una tercera diatonica por debajo (La menor)."""
    out = []
    for n in notes:
        m = n[2]
        while m % 12 not in A_MINOR:          # notas fuera de escala (p. ej. Sol#) -> la de abajo
            m -= 1
        count = 0
        while count < degrees:
            m -= 1
            if m % 12 in A_MINOR:
                count += 1
        out.append((n[0], n[1], m) + tuple(n[3:]))
    return out


# ---------------------------------------------------------------------------
# M2 - PARTIDO: 150 BPM, 32 compases (~51 s). Riff - estribillo x2 - breakdown/subida
# ---------------------------------------------------------------------------
MATCH_LEAD = [
    (0, .5, 69), (.5, .5, 72), (1, 1, 74, -2), (2, .5, 76), (2.5, 1.5, 74),
    (4, .5, 72), (4.5, .5, 74), (5, .5, 72), (5.5, .5, 69), (6, 2, 65, -1),
    (8, .5, 67), (8.5, .5, 72), (9, 1, 74), (10, .5, 76), (10.5, .5, 79), (11, 1, 76),
    (12, .75, 74), (12.75, .25, 76), (13, 1, 74), (14, 2, 74, -2),
    (16, .5, 69), (16.5, .5, 72), (17, 1, 74), (18, .5, 76), (18.5, .5, 79), (19, 1, 81, -2),
    (20, .75, 79), (20.75, .25, 77), (21, 1, 76), (22, 2, 72),
    (24, .5, 72), (24.5, .5, 74), (25, 1, 76), (26, .5, 79), (26.5, 1.5, 76),
    (28, .5, 76), (28.5, .5, 74), (29, 1, 71), (30, 2, 68, -1),
]

VERSE_RIFF = [  # (paso de 16a dentro de 2 compases, acorde, duracion en pasos, palm mute)
    (0, "A5", 2, False), (2, "A5", 2, True), (4, "A5", 2, True), (6, "A5", 2, True), (8, "A5", 2, True),
    (10, "C5", 2, False), (12, "D5", 4, False),
    (16, "A5", 2, False), (18, "A5", 2, True), (20, "A5", 2, True), (22, "A5", 2, True), (24, "A5", 2, True),
    (26, "G5", 2, False), (28, "F5", 2, False), (30, "E5", 2, False),
]


def play_riff(a, bar, riff, vel=1.0):
    for step, ch, ln, mute in riff:
        beat = bar * 4 + step / 4
        a.gtr(ch, beat, ln / 4, mute, vel * (0.85 if mute else 1.0))
        a.bass(ch, beat, ln / 4, 0.9 if mute else 1.0)


def driving_8ths(a, bar, chord, vel=1.0, accent_mute=False):
    for s in range(0, 16, 2):
        a.gtr(chord, bar * 4 + s / 4, 0.5, mute=accent_mute and s % 4 == 2, vel=vel * (1.0 if s == 0 else 0.85))
        a.bass(chord, bar * 4 + s / 4, 0.5, 1.0 if s % 4 == 0 else 0.85)


def build_match():
    a = Arr(150, 32, seed=11)
    chorus = ["A5", "F5", "C5", "G5", "A5", "F5", "C5", "E5"]

    # Compases 0-7: riff de estrofa
    for bar in range(0, 8, 2):
        play_riff(a, bar, VERSE_RIFF)
    for bar in range(8):
        beat_bar(a, bar, "verse" if bar < 4 else "drive")
    crash(a, 0, 0.8)
    fill(a, 7, 12)

    # Compases 8-23: estribillo x2 con la melodia (el 2o, con armonia a dos guitarras)
    for bar in range(8, 24):
        driving_8ths(a, bar, chorus[bar % 8])
        beat_bar(a, bar, "chorus")
        if bar % 4 == 0:
            crash(a, bar * 4, 0.85)
    fill(a, 15, 12)
    fill(a, 23, 8)
    a.lead(MATCH_LEAD, 8 * 4, pan=-0.1)
    a.lead(MATCH_LEAD, 16 * 4, pan=-0.35)
    a.lead(harmonize(MATCH_LEAD), 16 * 4, vel=0.8, pan=0.35)

    # Compases 24-27: breakdown a medio tiempo (acordes que suenan enteros)
    for i, ch in enumerate(["A5", "F5", "G5", "E5"]):
        bar = 24 + i
        a.gtr(ch, bar * 4, 3.5)
        a.gtr(ch, bar * 4 + 3.5, 0.5, mute=True)
        a.bass(ch, bar * 4, 4)
        beat_bar(a, bar, "half")
        crash(a, bar * 4, 0.9)

    # Compases 28-31: subida (chugs cada vez mas rapidos + redoble) que vuelve al riff
    for bar in range(28, 32):
        step = 2 if bar < 30 else 1
        for s in range(0, 16 if bar < 31 else 8, step):
            a.gtr("A5", bar * 4 + s / 4, step / 4, mute=True, vel=0.8 + 0.05 * (bar - 28))
            a.bass("A5", bar * 4 + s / 4, step / 4, 0.9)
        for s in range(0, 16, 4 if bar < 30 else 2):
            a.hit("kick", bar * 4 + s / 4)
        for s in range(0, 16, 4 if bar < 29 else (2 if bar < 31 else 1)):
            a.hit("snare", bar * 4 + s / 4, 0.45 + 0.15 * (bar - 28) + 0.02 * s)
    a.gtr("E5", 31 * 4 + 2, 2, vel=1.0)
    a.bass("E5", 31 * 4 + 2, 2)
    fill(a, 31, 10)
    return a


# ---------------------------------------------------------------------------
# M4 - GOL DE ORO: 170 BPM, 16 compases (~23 s). Tension: tremolo, semitonos y "reloj"
# ---------------------------------------------------------------------------
GG_LEAD = [
    (0, .5, 69), (.5, .5, 70), (1, 1, 72), (2, .5, 74), (2.5, 1.5, 72, -1),
    (4, .5, 70), (4.5, .5, 72), (5, .5, 70), (5.5, .5, 69), (6, 2, 64, -2),
    (8, .5, 70), (8.5, .5, 74), (9, 1, 77), (10, .5, 74), (10.5, 1.5, 77, -2),
    (12, 2, 76), (14, 2, 72),
    (16, .5, 69), (16.5, .5, 70), (17, 1, 72), (18, .5, 74), (18.5, .5, 76), (19, 1, 77, -1),
    (20, 1, 76), (21, 1, 74), (22, 2, 72),
    (24, 1, 79, -2), (25, 1, 76), (26, 2, 72),
    (28, 1, 78, -2), (29, 1, 75), (30, 2, 71, -1),
]


def build_goldengoal():
    a = Arr(170, 16, seed=41)
    prog = ["A5", "A5", "Bb5", "A5", "A5", "A5", "C5", "B5"]
    for bar in range(16):
        ch = prog[bar % 8]
        b = bar * 4
        if bar < 8:   # tremolo en semicorcheas con palm mute (tension)
            a.gtr(ch, b, 0.25, vel=1.0)
            for s in range(1, 16):
                a.gtr(ch, b + s / 4, 0.25, mute=True, vel=0.75 if s % 4 else 0.9)
        else:
            driving_8ths(a, bar, ch)
        for s in range(0, 16, 2):
            a.bass(ch, b + s / 4, 0.5, 1.0 if s % 4 == 0 else 0.85)
        # "Reloj": golpe seco en cada pulso, que no deja de sonar
        for q in range(4):
            a.hit("rim", b + q, 0.9 if q == 0 else 0.7, 0.5)
        beat_bar(a, bar, "double", 0.9 if bar < 8 else 1.0)
        if bar % 4 == 0:
            crash(a, b, 0.8 if bar else 0.6)
    a.pad([57, 64, 69], 0, 32, 0.6)
    a.pad([57, 64, 69], 32, 32, 0.6)
    fill(a, 7, 8)
    fill(a, 15, 8)
    a.lead(GG_LEAD, 8 * 4, drive=9.0)
    a.lead(GG_LEAD, 8 * 4, vel=0.6, transpose=-12, pan=0.4, drive=9.0)
    return a


# ---------------------------------------------------------------------------
# M3 - HUB DEL TORNEO: 120 BPM, 16 compases (~32 s). Rock a medio tiempo, menos denso
# ---------------------------------------------------------------------------
HUB_LEAD = [
    (0, 1, 69), (1, 1, 72), (2, 2, 74, -2), (4, 1, 76), (5, 3, 74),
    (8, 1, 72), (9, 1, 76), (10, 2, 74), (12, 4, 71, -1),
    (16, 1, 69), (17, 1, 72), (18, 2, 74), (20, 1, 76), (21, 1, 79), (22, 2, 81, -2),
    (24, 2, 79), (26, 2, 77), (28, 4, 76, -1),
]
HUB_RIFF = [(0, False, 3), (3, False, 3), (6, True, 2), (8, True, 2), (10, False, 2), (12, False, 4)]


def build_hub():
    a = Arr(120, 16, seed=31)
    prog = ["A5", "F5", "C5", "G5"]
    for bar in range(16):
        ch = prog[bar % 4]
        for step, mute, ln in HUB_RIFF:
            a.gtr(ch, bar * 4 + step / 4, ln / 4, mute, 0.8 if mute else 0.9, drive=12.0)
        a.bass(ch, bar * 4, 1.5)
        a.bass(ch, bar * 4 + 1.5, 0.5, 0.8)
        a.bass(ch, bar * 4 + 2.5, 1.5, 0.9)
        beat_bar(a, bar, "verse", 0.85)
        if bar % 8 == 0:
            crash(a, bar * 4, 0.6)
        if bar % 8 == 7:
            fill(a, bar, 12)
    a.lead(HUB_LEAD, 8 * 4, vel=0.85, drive=4.0)
    return a


# ---------------------------------------------------------------------------
# M1 - MENU: 112 BPM, 16 compases (~34 s). Riff con chulería + "stomp-stomp-palmada" de estadio
# ---------------------------------------------------------------------------
MENU_RIFF = [  # (paso de 16a en 2 compases, nota MIDI, duracion en pasos, palm mute)
    (0, 45, 2, True), (2, 45, 1, True), (3, 48, 2, False), (6, 50, 2, False),
    (8, 45, 1, True), (10, 45, 1, True), (11, 52, 3, False), (14, 50, 2, False),
    (16, 45, 2, True), (18, 45, 1, True), (19, 48, 2, False), (22, 50, 2, False),
    (24, 53, 2, False), (26, 52, 2, False), (28, 50, 2, False), (30, 48, 2, False),
]
MENU_LEAD = [
    (0, .5, 69), (.5, .5, 72), (1, 1, 74, -2), (2, .5, 76), (2.5, 1.5, 74),
    (4, .5, 72), (4.5, .5, 74), (5, .5, 72), (5.5, .5, 69), (6, 2, 67, -2),
    (8, .5, 69), (8.5, .5, 72), (9, 1, 74), (10, .5, 76), (10.5, .5, 77), (11, 1, 74),
    (12, 1, 72), (13, 1, 69), (14, 2, 68, -1),
    (16, .5, 76), (16.5, .5, 79), (17, 1, 81, -2), (18, .5, 79), (18.5, 1.5, 76),
    (20, .5, 72), (20.5, .5, 76), (21, 1, 79), (22, 2, 76, -2),
    (24, .5, 74), (24.5, .5, 77), (25, 1, 81), (26, 2, 77),
    (28, 1, 77), (29, 1, 76), (30, 2, 71, -2),
]


def stomp_clap(a, bar, vel=1.0):
    b = bar * 4
    for s in (0, 2, 8, 10):                    # stomp
        a.hit("kick", b + s / 4, vel)
        a.hit("tom_lo", b + s / 4, 0.55 * vel, 0.1)
    for s in (4, 12):                          # palmada
        a.hit("clap", b + s / 4, 1.0 * vel, -0.1)
        a.hit("snare", b + s / 4, 0.7 * vel)
    for s in range(0, 16, 2):
        a.hit("hat", b + s / 4, 0.7 if s % 4 == 0 else 0.45, 0.3)


def build_menu():
    a = Arr(112, 16, seed=21)
    # A (0-7): riff de una sola cuerda doblado por el bajo
    for bar in range(0, 8, 2):
        for step, m, ln, mute in MENU_RIFF:
            beat = bar * 4 + step / 4
            a.gtr_note(m, beat, ln / 4, mute, 0.9 if mute else 1.0)
            a.bass(m - 12, beat, ln / 4, 0.9 if mute else 1.0)
    for bar in range(8):
        stomp_clap(a, bar)
    fill(a, 7, 12, "snare")

    # B (8-15): acordes abiertos + melodia (tema principal)
    prog = [["A5"], ["C5"], ["D5"], ["F5", "E5"]]
    for bar in range(8, 16):
        chords = prog[bar % 4]
        for s in range(0, 16, 2):
            ch = chords[0] if (len(chords) == 1 or s < 8) else chords[1]
            a.gtr(ch, bar * 4 + s / 4, 0.5, mute=(s % 8 == 6), vel=1.0 if s % 8 == 0 else 0.85)
            a.bass(ch, bar * 4 + s / 4, 0.5, 1.0 if s % 4 == 0 else 0.85)
        beat_bar(a, bar, "chorus")
        a.hit("clap", bar * 4 + 1, 0.8, -0.1)
        a.hit("clap", bar * 4 + 3, 0.8, -0.1)
        if bar % 4 == 0:
            crash(a, bar * 4, 0.85)
    fill(a, 15, 8)
    crash(a, 0, 0.7)
    a.lead(MENU_LEAD, 8 * 4)
    return a


# ---------------------------------------------------------------------------
# Stingers (no son loops)
# ---------------------------------------------------------------------------

def build_victory():
    a = Arr(150, 2, loop=False, tail=3.0, seed=51)
    for i, nm in enumerate(["snare", "snare", "tom_hi", "tom_mid"]):
        a.hit(nm, i / 4, 0.75 + 0.08 * i)
    for beat, dur, ch in ((1, 0.5, "F5"), (1.5, 0.5, "G5"), (2, 3, "C5")):
        a.gtr(ch, beat, dur)
        a.bass(ch, beat, dur)
        crash(a, beat, 0.9 if beat == 2 else 0.6)
    a.hit("snare", 2, 1.0)
    a.lead([(1, .25, 72), (1.25, .25, 76), (1.5, .25, 79), (1.75, .25, 81), (2, 3, 84, -2)], 0)
    return a


def build_defeat():
    a = Arr(110, 2, loop=False, tail=3.0, seed=61)
    for beat, ch in ((0, "A5"), (1, "G5"), (2, "F5")):
        a.gtr(ch, beat, 0.85)
        a.bass(ch, beat, 0.85)
        crash(a, beat, 0.55)
        a.hit("snare", beat, 0.8)
    a.gtr("E5", 3, 3.5, dive=(0.45, 9))          # el ultimo acorde se "desinfla"
    a.bass("E5", 3, 1.5, 0.9)
    crash(a, 3, 0.9)
    a.hit("tom_lo", 3.5, 0.9)
    a.lead([(3, 2.5, 76, -1)], 0, vel=0.6)
    return a


def build_champion():
    a = Arr(150, 6, loop=False, tail=4.0, seed=71)
    # Compas 0: redoble creciente
    for s in range(16):
        a.hit("snare" if s < 12 else ["tom_hi", "tom_mid", "tom_lo", "tom_lo"][s - 12], s / 4, 0.35 + 0.045 * s)
    a.gtr("G5", 0, 3.5, vel=0.7)
    a.bass("G5", 2, 2, 0.8)
    # Compases 1-2: estribillo triunfal
    for bar, (c1, c2) in ((1, ("C5", "G5")), (2, ("A5", "F5"))):
        for s in range(0, 16, 2):
            ch = c1 if s < 8 else c2
            a.gtr(ch, bar * 4 + s / 4, 0.5, vel=1.0 if s % 8 == 0 else 0.85)
            a.bass(ch, bar * 4 + s / 4, 0.5)
        beat_bar(a, bar, "chorus")
    crash(a, 4, 1.0)
    crash(a, 8, 0.85)
    # Compas 3: golpes de parada (todos a la vez)
    for beat in (12, 12.75, 13.5):
        a.gtr("G5", beat, 0.4)
        a.bass("G5", beat, 0.4)
        crash(a, beat, 0.75)
        a.hit("snare", beat, 1.0)
    for s in range(8, 16):
        a.hit(["tom_hi", "tom_mid", "tom_lo"][min(2, (s - 8) // 3)], 12 + s / 4, 0.75 + 0.03 * s)
    # Compas 4-5: acorde final con redoble de toms encima y golpe final
    a.gtr("C5", 16, 3.4)
    a.bass("C5", 16, 3.4)
    crash(a, 16, 1.0)
    for i in range(10):
        a.hit(["tom_hi", "tom_mid", "tom_lo"][i % 3], 17 + i * 0.2, 0.6 + 0.04 * i)
    a.gtr("C5", 19.5, 1.5)
    a.bass("C5", 19.5, 1.5)
    crash(a, 19.5, 1.0)
    a.hit("snare", 19.5, 1.0)
    a.lead([(4, .5, 72), (4.5, .5, 76), (5, 1, 79, -2), (6, .5, 81), (6.5, 1.5, 79),
            (8, .5, 77), (8.5, .5, 79), (9, 1, 81), (10, .5, 84), (10.5, 1.5, 81, -2),
            (12, .4, 79), (12.75, .4, 81), (13.5, .4, 83), (16, 3.4, 84, -2), (19.5, 1.5, 84)], 0)
    return a


def build_eliminated():
    a = Arr(90, 3, loop=False, tail=3.5, seed=81)
    for beat, ch in ((0, "A5"), (2, "F5")):
        a.gtr(ch, beat, 1.9)
        a.bass(ch, beat, 1.9)
        crash(a, beat, 0.7)
        a.hit("tom_lo", beat + 1, 0.7)
    a.gtr("E5", 4, 4.5, dive=(0.9, 12))
    a.bass("E5", 4, 2.5)
    crash(a, 4, 0.9)
    a.hit("tom_lo", 4, 1.0)
    a.lead([(0, 1, 76), (1, 1, 74), (2, 2, 72), (4, 3, 69, -1)], 0, vel=0.7)
    return a


# ---------------------------------------------------------------------------
# Lista de pistas
# ---------------------------------------------------------------------------

def render_credits():
    import music_funk
    return music_funk.build_menu().mixdown()


TRACKS = {
    "credits":    ("music_credits.wav",    render_credits),
    "menu":       ("music_menu.wav",       lambda: build_menu().mixdown()),
    "match":      ("music_match.wav",      lambda: build_match().mixdown()),
    "hub":        ("music_hub.wav",        lambda: build_hub().mixdown(rms_db=-16.0)),
    "goldengoal": ("music_goldengoal.wav", lambda: build_goldengoal().mixdown(rms_db=-14.0)),
    # Pausa: la del partido, sin bateria y "tras una pared" (mismo tempo y duracion)
    "pause":      ("music_pause.wav",      lambda: build_match().mixdown(mute=("kick", "drums"), post_lowpass=600, rms_db=-21, duck=0)),
    "victory":    ("stinger_victory.wav",  lambda: build_victory().mixdown(rms_db=-14, duck=0)),
    "defeat":     ("stinger_defeat.wav",   lambda: build_defeat().mixdown(rms_db=-15, duck=0)),
    "champion":   ("stinger_champion.wav", lambda: build_champion().mixdown(rms_db=-14, duck=0.1)),
    "eliminated": ("stinger_eliminated.wav", lambda: build_eliminated().mixdown(rms_db=-15.5, duck=0)),
}


def main():
    p = argparse.ArgumentParser(description="Genera la musica rock del juego en .wav")
    p.add_argument("tracks", nargs="*", help="Pistas (por defecto todas): " + ", ".join(TRACKS))
    p.add_argument("--out", default=str(Path(__file__).parent / "output" / "music"), help="Carpeta de salida")
    args = p.parse_args()

    names = args.tracks or list(TRACKS)
    bad = [n for n in names if n not in TRACKS]
    if bad:
        p.error("Pista desconocida: " + ", ".join(bad))

    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    for n in names:
        fname, fn = TRACKS[n]
        t0 = time.time()
        audio = fn()
        S.write_wav(out / fname, audio)
        print(f"  {fname:28s} {audio.shape[1] / SR:6.1f} s   ({time.time() - t0:.1f} s)")
    print(f"Listo. Archivos en: {out.resolve()}")


if __name__ == "__main__":
    main()
