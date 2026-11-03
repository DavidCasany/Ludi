"""
music.py - Genera toda la musica del juego de hockey patines en .wav

Estilo: funk arcade deportivo de los 80-90 (piano electrico FM, bajo funk,
metales sinteticos y caja de ritmos). Todas las pistas comparten el mismo
"leitmotiv" (La-Do-Re-Mi-Re) y la misma paleta de instrumentos, para que
suenen al mismo juego.

USO
    python music_funk.py                 -> genera todas las pistas
    python music_funk.py match hub       -> genera solo las que indiques
    python music_funk.py --out C:/ruta   -> carpeta de salida (por defecto ./output/music)

Pistas: menu, match, hub, goldengoal, pause, victory, defeat, champion, eliminated
"""
import argparse
import time
from pathlib import Path

import numpy as np

import synth as S
from synth import SR, midi_hz

# ---------------------------------------------------------------------------
# Acordes: (nota del bajo, voicing del piano, intervalo de "septima" para el bajo)
# ---------------------------------------------------------------------------
CHORDS = {
    "Am7":   (33, [57, 60, 64, 67], 10),
    "Am":    (33, [57, 60, 64, 69], 7),
    "Aadd9": (33, [57, 59, 60, 64], 7),
    "D9":    (38, [54, 57, 60, 64], 10),
    "Dm7":   (38, [53, 57, 60, 64], 10),
    "Dm":    (38, [53, 57, 62, 65], 7),
    "Fmaj7": (41, [53, 57, 60, 64], 9),
    "F":     (41, [53, 57, 60, 65], 7),
    "G6":    (43, [55, 59, 62, 64], 9),
    "G7sus": (43, [53, 57, 60, 62], 10),
    "G7":    (43, [53, 59, 62, 64], 10),
    "Em7":   (40, [52, 55, 59, 62], 10),
    "E7":    (40, [56, 59, 62, 64], 10),
    "E7#9":  (40, [56, 62, 67, 68], 10),
    "Cmaj7": (36, [55, 59, 60, 64], 9),
    "Cmaj9": (36, [55, 59, 62, 64], 9),
}
INTERVAL = {"R": 0, "O": 12, "5": 7}

BUS_NAMES = ["kick", "drums", "bass", "keys", "pad", "brass", "lead"]
GAINS = {"kick": 0.7, "drums": 0.75, "bass": 0.55, "keys": 0.6, "pad": 0.3, "brass": 0.45, "lead": 0.5}
SENDS = {"kick": 0.0, "drums": 0.10, "bass": 0.0, "keys": 0.28, "pad": 0.45, "brass": 0.22, "lead": 0.32}
DUCKED = ("bass", "keys", "pad", "brass")


class Arr:
    """Una pieza musical: convierte compases/pasos en segundos y suma notas."""

    def __init__(self, bpm, bars, swing=0.0, swing_unit=16, loop=True, tail=4.0, seed=1):
        self.spb = 60.0 / bpm
        self.bars = bars
        self.swing, self.swing_unit = swing, swing_unit
        self.loop = loop
        self.length_sec = bars * 4 * self.spb
        self.buses = {n: S.Bus(self.length_sec, tail) for n in BUS_NAMES}
        self.kicks = []
        self.rng = np.random.default_rng(seed)
        self.kit = S.DrumKit()

    # ---- tiempo ----
    def t(self, beat):
        step = beat * 4.0
        if abs(step - round(step)) < 1e-6 and self.swing:
            s = int(round(step))
            if self.swing_unit == 16 and s % 2 == 1:
                beat += self.swing * 0.25
            elif self.swing_unit == 8 and s % 4 == 2:
                beat += self.swing * 0.5
        return beat * self.spb

    def wrap(self, sec):
        """En un loop, una nota que cae un pelin antes del 0 se coloca al final."""
        return sec + self.length_sec if (self.loop and sec < 0) else max(0.0, sec)

    def human(self, ms=4):
        return self.rng.uniform(-ms, ms) / 1000.0

    # ---- notas ----
    def note(self, bus, inst, midi, beat, dur, vel=1.0, pan=0.0, gain=1.0):
        sig = inst(midi_hz(midi), dur * self.spb, vel, self.rng)
        self.buses[bus].add(sig, self.wrap(self.t(beat) + self.human()), gain, pan)

    def chord(self, bus, inst, notes, beat, dur, vel=1.0, gain=1.0, spread=0.5):
        n = len(notes)
        for i, m in enumerate(notes):
            pan = (i / max(1, n - 1) - 0.5) * spread
            self.note(bus, inst, m, beat, dur, vel * self.rng.uniform(0.92, 1.0), pan, gain / np.sqrt(n))

    def hit(self, name, beat, vel=1.0, pan=0.0):
        bus = "kick" if name == "kick" else "drums"
        start = self.wrap(self.t(beat) + (0 if name == "kick" else self.human(3)))
        if name == "kick":
            self.kicks.append(start)
        self.buses[bus].add(self.kit[name], start, vel, pan)

    def melody(self, bus, inst, notes, offset_beats, vel=0.9, transpose=0, pan=0.0, gain=1.0):
        for (b, d, m) in notes:
            self.note(bus, inst, m + transpose, offset_beats + b, d, vel, pan, gain)

    # ---- mezcla ----
    def mixdown(self, mute=(), only=None, post_lowpass=None, rms_db=-17.0, duck=0.32):
        total = None
        ir = S.reverb_ir(1.7)
        send_sum = None
        n = self.buses["kick"].buf.shape[1]
        tt = np.arange(n) / SR

        duck_env = np.ones(n)
        if duck:
            for k in self.kicks:
                for kk in (k, k + self.length_sec) if self.loop else (k,):
                    i0 = int(kk * SR)
                    if i0 >= n:
                        continue
                    i1 = min(n, i0 + int(0.45 * SR))
                    duck_env[i0:i1] = np.minimum(duck_env[i0:i1], 1 - duck * np.exp(-(tt[i0:i1] - kk) / 0.11))

        for name, bus in self.buses.items():
            if name in mute or (only is not None and name not in only):
                continue
            b = bus.buf * GAINS[name]
            if name in DUCKED:
                b = b * duck_env
            total = b if total is None else total + b
            if SENDS[name]:
                send_sum = b * SENDS[name] if send_sum is None else send_sum + b * SENDS[name]

        if send_sum is not None:
            total = total + S.apply_reverb(send_sum, ir, 1.0) - send_sum

        L = int(self.length_sec * SR)
        if self.loop:
            out = S.fold_loop(total, L)
            if post_lowpass:
                twice = np.concatenate([out, out], axis=1)       # filtrado sin clic en el loop
                out = S.lowpass(twice, post_lowpass, 2)[:, L:]
        else:
            out = total
            if post_lowpass:
                out = S.lowpass(out, post_lowpass, 2)
            # recorta el silencio del final y hace un fade corto
            env = np.abs(out).max(axis=0)
            last = np.nonzero(env > 1e-4)[0]
            end = min(out.shape[1], (last[-1] if len(last) else L) + int(0.05 * SR))
            out = out[:, :end]
            f = min(int(0.3 * SR), out.shape[1])
            out[:, -f:] *= np.linspace(1, 0, f)
        return S.master(out, rms_db)


# ---------------------------------------------------------------------------
# Patrones comunes
# ---------------------------------------------------------------------------

def bass_bar(a, chord, bar, pattern, vel=1.0):
    root, _, sev = CHORDS[chord]
    for step, sym, length, v in pattern:
        iv = sev if sym == "7" else INTERVAL[sym]
        a.note("bass", S.bass, root + iv, bar * 4 + step / 4, length / 4, v * vel)


def keys_bar(a, chord, bar, hits, vel=0.7, inst=S.epiano, bus="keys", transpose=0, gain=1.0):
    voicing = [m + transpose for m in CHORDS[chord][1]]
    for step, length in hits:
        a.chord(bus, inst, voicing, bar * 4 + step / 4, length / 4, vel, gain)


def drum_bar(a, bar, kicks, snares, hats=None, hat_vel=(0.55, 0.3), openhat=(), ghosts=(), clap=True,
             shaker=None, snare_name="snare", snare_vel=1.0):
    b = bar * 4
    for s in kicks:
        a.hit("kick", b + s / 4)
    for s in snares:
        a.hit(snare_name, b + s / 4, snare_vel)
        if clap:
            a.hit("clap", b + s / 4, 0.45, 0.15)
    for s in ghosts:
        a.hit("ghost", b + s / 4, 1.0, -0.1)
    if hats:
        for s in hats:
            if s in openhat:
                a.hit("openhat", b + s / 4, 0.7, 0.25)
            else:
                a.hit("hat", b + s / 4, hat_vel[0] if s % 2 == 0 else hat_vel[1], 0.25)
    if shaker:
        for s in shaker:
            a.hit("shaker", b + s / 4, 0.6 if s % 2 == 0 else 0.35, -0.3)


def fill(a, bar, kind="toms"):
    b = bar * 4 + 3
    if kind == "toms":
        for i, name in enumerate(["tom_hi", "tom_hi", "tom_mid", "tom_lo"]):
            a.hit(name, b + i / 4, 0.8 + 0.05 * i, 0.3 - 0.2 * i)
    else:
        for i in range(4):
            a.hit("snare", b + i / 4, 0.45 + 0.15 * i)


# Leitmotiv principal (La menor). Se reutiliza en todas las pistas.
MOTIF_A = [
    (0, .5, 69), (.5, .5, 72), (1, 1, 74), (2, .5, 76), (2.5, 1.5, 74),
    (4, .5, 72), (4.5, .5, 74), (5, .5, 72), (5.5, .5, 69), (6, 2, 66),
    (8, .5, 69), (8.5, .5, 72), (9, 1, 74), (10, .5, 76), (10.5, .5, 79), (11, 1, 81),
    (12, .75, 79), (12.75, .25, 76), (13, 1, 74), (14, 2, 76),
    (16, 1, 72), (17, .5, 76), (17.5, .5, 77), (18, 2, 79),
    (20, .5, 79), (20.5, .5, 76), (21, 1, 74), (22, 1, 71), (23, 1, 74),
    (24, 1.5, 71), (25.5, .5, 74), (26, 2, 76),
    (28, .5, 80), (28.5, .5, 79), (29, 1, 76), (30, 2, 74),
]


# ---------------------------------------------------------------------------
# M2 - Partido (loop, 118 BPM, 32 compases)
# ---------------------------------------------------------------------------

def build_match(bpm=118):
    a = Arr(bpm, 32, swing=0.12, seed=11)
    prog = ["Am7", "D9", "Am7", "D9", "Fmaj7", "G6", "Em7", "E7#9"]
    bass_pat = [(0, "R", 3, 1.0), (3, "O", 1, 0.6), (6, "R", 2, 0.85), (8, "R", 1, 0.7),
                (10, "5", 2, 0.8), (12, "7", 1, 0.7), (13, "O", 1, 0.75), (14, "R", 2, 0.85)]
    comp = [(2, 1), (6, 2), (10, 1), (13, 2)]
    hats16 = list(range(16))

    for bar in range(32):
        ch = prog[bar % 8]
        sec = bar // 8                      # 0 intro, 1 melodia, 2 metales+melodia, 3 llamada-respuesta
        bass_bar(a, ch, bar, bass_pat)
        keys_bar(a, ch, bar, comp, vel=0.75 if sec in (0, 3) else 0.6)
        if sec in (1, 2):
            keys_bar(a, ch, bar, [(0, 16)], vel=0.6, inst=S.pad, bus="pad")

        kicks = [0, 7, 10] if bar % 2 == 0 else [0, 6, 10, 14]
        drum_bar(a, bar, kicks, [4, 12], hats16 if sec != 0 or bar >= 4 else list(range(0, 16, 2)),
                 openhat=(14,) if sec >= 2 else (), ghosts=(9, 15))
        if bar % 8 == 0:
            a.hit("crash", bar * 4, 0.8 if bar else 0.55)
        if bar % 8 == 7:
            fill(a, bar, "toms" if sec % 2 else "snare")

        if sec == 2:
            keys_bar(a, ch, bar, [(0, 2), (6, 1), (10, 3)], vel=0.9, inst=S.brass, bus="brass", transpose=12)
        if sec == 3 and bar % 2 == 0:
            a.melody("brass", S.brass, MOTIF_A[:5], bar * 4, vel=0.9, transpose=-12)
            a.melody("brass", S.brass, MOTIF_A[:5], bar * 4, vel=0.8, transpose=-17)

    a.melody("lead", S.lead, MOTIF_A, 8 * 4, vel=0.9)
    a.melody("lead", S.lead, MOTIF_A, 16 * 4, vel=0.95)
    return a


# ---------------------------------------------------------------------------
# M1 - Menu principal (loop, 100 BPM, 16 compases)
# ---------------------------------------------------------------------------

MENU_LEAD = [
    (0, .5, 69), (.5, .5, 72), (1, 1, 74), (2, .5, 76), (2.5, 1.5, 74),
    (4, .5, 72), (4.5, .5, 74), (5, .5, 72), (5.5, .5, 69), (6, 2, 67),
    (8, .5, 69), (8.5, .5, 72), (9, 1, 74), (10, .5, 76), (10.5, .5, 79), (11, 1, 81),
    (12, .75, 79), (12.75, .25, 77), (13, 1, 74), (14, 2, 72),
    (16, 1, 76), (17, .5, 79), (17.5, .5, 83), (18, 2, 79),
    (20, .5, 76), (20.5, .5, 74), (21, 1, 72), (22, 2, 69),
    (24, .5, 69), (24.5, .5, 72), (25, 1, 74), (26, .5, 77), (26.5, 1.5, 76),
    (28, .5, 74), (28.5, .5, 71), (29, 1, 74), (30, 2, 76),
]


def build_menu():
    a = Arr(100, 16, swing=0.18, seed=21)
    prog = ["Cmaj7", "Am7", "Fmaj7", "G7sus", "Cmaj7", "Am7", "Dm7", "G7"]
    bass_pat = [(0, "R", 6, 1.0), (6, "5", 2, 0.75), (8, "R", 4, 0.85), (14, "O", 2, 0.7)]
    for bar in range(16):
        ch = prog[bar % 8]
        bass_bar(a, ch, bar, bass_pat, 0.9)
        keys_bar(a, ch, bar, [(0, 6), (6, 2), (10, 6)], vel=0.65)
        keys_bar(a, ch, bar, [(0, 16)], vel=0.55, inst=S.pad, bus="pad")
        drum_bar(a, bar, [0, 10], [4, 12], list(range(0, 16, 2)), hat_vel=(0.45, 0.3), clap=False,
                 shaker=list(range(16)) if bar >= 8 else None, snare_vel=0.75, ghosts=(15,))
        if bar % 8 == 0:
            a.hit("crash", bar * 4, 0.45)
        if bar % 8 == 7:
            fill(a, bar, "snare")
    a.melody("lead", S.lead, MENU_LEAD, 8 * 4, vel=0.8)
    a.melody("keys", S.bell, MENU_LEAD, 8 * 4, vel=0.35, transpose=12, gain=0.6)
    return a


# ---------------------------------------------------------------------------
# M3 - Hub del torneo / eleccion de mejoras (loop, 88 BPM, 16 compases)
# ---------------------------------------------------------------------------

HUB_BELL = [
    (0, 1, 69), (1, 1, 72), (2, 2, 74), (4, 1, 76), (5, 3, 74),
    (8, 1, 72), (9, 1, 74), (10, 2, 72), (12, 4, 67),
    (16, 1, 69), (17, 1, 72), (18, 2, 74), (20, 1, 76), (21, 1, 79), (22, 2, 81),
    (24, 2, 79), (26, 2, 77), (28, 4, 74),
]


def build_hub():
    a = Arr(88, 16, swing=0.22, swing_unit=8, seed=31)
    prog = ["Fmaj7", "Em7", "Dm7", "Cmaj7", "Fmaj7", "Em7", "Dm7", "G7sus"]
    arp = [0, 1, 2, 3, 2, 1, 0, 1]
    for bar in range(16):
        ch = prog[bar % 8]
        root, voicing, _ = CHORDS[ch]
        a.note("bass", S.bass, root, bar * 4, 2.5, 0.8)
        a.note("bass", S.bass, root + 7, bar * 4 + 2.5, 1.5, 0.6)
        for i, idx in enumerate(arp):
            a.note("keys", S.epiano, voicing[idx] + 12, bar * 4 + i * 0.5, 0.75,
                   0.45 + (0.1 if i % 2 == 0 else 0), pan=(idx - 1.5) * 0.25)
        keys_bar(a, ch, bar, [(0, 16)], vel=0.7, inst=S.pad, bus="pad")
        drum_bar(a, bar, [0, 10], [12], list(range(0, 16, 2)) if bar >= 8 else None,
                 hat_vel=(0.35, 0.25), clap=False, shaker=list(range(0, 16, 2)),
                 snare_name="rim", snare_vel=0.8)
    a.melody("lead", S.bell, HUB_BELL, 8 * 4, vel=0.75)
    return a


# ---------------------------------------------------------------------------
# M4 - Prorroga / gol de oro (loop, 132 BPM, 16 compases, mas tension)
# ---------------------------------------------------------------------------

GG_LEAD = [
    (0, .5, 69), (.5, .5, 72), (1, 1, 74), (2, .5, 76), (2.5, 1.5, 74),
    (4, .5, 72), (4.5, .5, 74), (5, .5, 76), (5.5, .5, 77), (6, 2, 76),
    (8, .5, 72), (8.5, .5, 74), (9, 1, 77), (10, .5, 81), (10.5, 1.5, 79),
    (12, .5, 77), (12.5, .5, 76), (13, 1, 74), (14, 2, 72),
    (16, .5, 69), (16.5, .5, 72), (17, 1, 74), (18, .5, 77), (18.5, 1.5, 74),
    (20, .5, 72), (20.5, .5, 74), (21, .5, 77), (21.5, .5, 81), (22, 2, 77),
    (24, 1, 80), (25, 1, 76), (26, 1, 71), (27, 1, 74),
    (28, 1, 76), (29, 1, 80), (30, 2, 83),
]


def build_goldengoal():
    a = Arr(132, 16, seed=41)
    prog = ["Am", "Am", "F", "F", "Dm", "Dm", "E7", "E7"]
    bass_pat = [(s, "O" if s in (4, 10, 14) else "R", 2, 1.0 if s % 4 == 0 else 0.75) for s in range(0, 16, 2)]
    for bar in range(16):
        ch = prog[bar % 8]
        bass_bar(a, ch, bar, bass_pat)
        keys_bar(a, ch, bar, [(0, 1), (6, 1), (12, 2)], vel=0.9, inst=S.brass, bus="brass", transpose=12)
        keys_bar(a, ch, bar, [(0, 16)], vel=0.6, inst=S.pad, bus="pad")
        hats = list(range(16)) if bar >= 8 else [2, 6, 10, 14]
        drum_bar(a, bar, [0, 4, 8, 12], [4, 12], hats, openhat=(2, 6, 10, 14) if bar < 8 else (14,))
        if bar % 8 == 0:
            a.hit("crash", bar * 4, 0.8)
        if bar % 8 == 7:
            for i in range(8):
                a.hit(["tom_hi", "tom_mid", "tom_lo"][min(2, i // 3)], bar * 4 + 2 + i / 4, 0.7 + 0.04 * i)
    a.melody("lead", S.lead, GG_LEAD, 8 * 4, vel=1.0)
    a.melody("brass", S.brass, GG_LEAD, 8 * 4, vel=0.7, transpose=-12)
    return a


# ---------------------------------------------------------------------------
# Stingers (no son loops)
# ---------------------------------------------------------------------------

def build_victory():
    a = Arr(118, 2, loop=False, tail=3.0, seed=51)
    for i in range(3):
        a.hit("snare", i / 4, 0.5 + 0.2 * i)
    a.melody("lead", S.lead, [(0, .25, 72), (.25, .25, 76), (.5, .25, 79), (.75, .75, 81),
                              (1.5, .5, 79), (2, 3, 84)], 0, vel=1.0)
    for beat, dur, ch, root in ((0.75, 0.75, [65, 69, 72, 76], 41), (1.5, 0.5, [67, 71, 74, 76], 43),
                                (2, 3, [67, 71, 74, 76, 79], 36)):
        a.chord("brass", S.brass, ch, beat, dur, 0.95)
        a.chord("keys", S.epiano, [m - 12 for m in ch], beat, dur, 0.8)
        a.note("bass", S.bass, root, beat, dur, 1.0)
    a.hit("kick", 2)
    a.hit("crash", 2, 0.9)
    a.hit("clap", 2, 0.6)
    return a


def build_defeat():
    a = Arr(90, 3, loop=False, tail=3.0, seed=61)
    for beat, dur, ch in ((0, 2, "Am7"), (2, 2, "Fmaj7"), (4, 2, "Dm7"), (6, 4, "E7")):
        root, voicing, _ = CHORDS[ch]
        a.chord("keys", S.epiano, voicing, beat, dur, 0.6)
        a.chord("pad", S.pad, voicing, beat, dur, 0.6)
        a.note("bass", S.bass, root, beat, dur, 0.8)
    mel = [(0, .5, 76), (.5, .5, 74), (1, 1, 72), (2, 2, 69), (4, .5, 69), (4.5, .5, 65), (5, 1, 62), (6, 4, 64)]
    a.melody("lead", S.lead, mel, 0, vel=0.65)
    a.melody("keys", S.bell, mel, 0, vel=0.3, transpose=12)
    a.hit("kick", 0, 0.7)
    a.hit("rim", 2, 0.5)
    return a


def build_champion():
    a = Arr(118, 5, loop=False, tail=4.0, seed=71)
    # Compas 0: redoble creciente + colchon
    for i in range(16):
        a.hit("snare", i / 4, 0.25 + 0.045 * i)
    a.chord("pad", S.pad, CHORDS["G7sus"][1], 0, 4, 0.8)
    a.note("bass", S.bass, 43, 3.5, 0.5, 0.9)
    prog = [("Cmaj7", 1), ("Fmaj7", 2), ("G6", 3)]
    for ch, bar in prog:
        bass_bar(a, ch, bar, [(0, "R", 3, 1.0), (3, "O", 1, 0.6), (6, "R", 2, 0.85), (10, "5", 2, 0.8),
                              (14, "O", 2, 0.8)])
        keys_bar(a, ch, bar, [(0, 4), (6, 2), (10, 4)], vel=0.75)
        keys_bar(a, ch, bar, [(0, 2), (6, 1), (10, 3)], vel=0.95, inst=S.brass, bus="brass", transpose=12)
        drum_bar(a, bar, [0, 7, 10], [4, 12], list(range(16)), openhat=(14,), ghosts=(9, 15))
    a.hit("crash", 4, 0.9)
    lead_notes = [(0, .5, 72), (.5, .5, 76), (1, 1, 79), (2, .5, 81), (2.5, 1.5, 79),
                  (4, .5, 77), (4.5, .5, 79), (5, 1, 81), (6, .5, 84), (6.5, 1.5, 81),
                  (8, .5, 79), (8.5, .5, 81), (9, .5, 83), (9.5, .5, 86), (10, 2, 83),
                  (12, 4, 84)]
    a.melody("lead", S.lead, lead_notes, 4, vel=1.0)
    # Final: Do mayor con platillo
    fin = [55, 59, 62, 64, 67]
    a.chord("brass", S.brass, [m + 12 for m in fin], 16, 3.5, 1.0)
    a.chord("keys", S.epiano, fin, 16, 3.5, 0.8)
    a.chord("pad", S.pad, fin, 16, 3.5, 0.8)
    a.note("bass", S.bass, 36, 16, 3.5, 1.0)
    a.hit("kick", 16)
    a.hit("crash", 16, 1.0)
    a.hit("clap", 16, 0.6)
    return a


def build_eliminated():
    a = Arr(80, 3, loop=False, tail=3.5, seed=81)
    for beat, dur, ch in ((0, 4, "Dm7"), (4, 5, "Aadd9")):
        root, voicing, _ = CHORDS[ch]
        a.chord("pad", S.pad, voicing, beat, dur, 0.9)
        a.chord("keys", S.epiano, voicing, beat, dur, 0.45)
        a.note("bass", S.bass, root, beat, dur, 0.7)
    a.melody("lead", S.bell, [(0, 1, 69), (1, 1, 72), (2, 2, 74), (4, 1, 72), (5, 1, 71), (6, 3, 69)], 0, vel=0.8)
    return a


# ---------------------------------------------------------------------------
# Lista de pistas
# ---------------------------------------------------------------------------

def render_pause():
    """M5: la musica del partido, filtrada y sin bateria (mismo tempo y duracion,
    se puede cambiar de una a otra en el mismo punto de reproduccion)."""
    return build_match().mixdown(mute=("drums",), post_lowpass=650, rms_db=-21, duck=0.15)


TRACKS = {
    "menu":       ("music_menu.wav",       lambda: build_menu().mixdown()),
    "match":      ("music_match.wav",      lambda: build_match().mixdown()),
    "hub":        ("music_hub.wav",        lambda: build_hub().mixdown(rms_db=-18.5, duck=0.15)),
    "goldengoal": ("music_goldengoal.wav", lambda: build_goldengoal().mixdown(rms_db=-16.5)),
    "pause":      ("music_pause.wav",      render_pause),
    "victory":    ("stinger_victory.wav",  lambda: build_victory().mixdown(rms_db=-16, duck=0)),
    "defeat":     ("stinger_defeat.wav",   lambda: build_defeat().mixdown(rms_db=-18, duck=0)),
    "champion":   ("stinger_champion.wav", lambda: build_champion().mixdown(rms_db=-16, duck=0.2)),
    "eliminated": ("stinger_eliminated.wav", lambda: build_eliminated().mixdown(rms_db=-19, duck=0)),
}


def main():
    p = argparse.ArgumentParser(description="Genera la musica del juego en .wav")
    p.add_argument("tracks", nargs="*", help="Pistas a generar (por defecto, todas): " + ", ".join(TRACKS))
    p.add_argument("--out", default=str(Path(__file__).parent / "output" / "music_funk"), help="Carpeta de salida")
    args = p.parse_args()

    names = args.tracks or list(TRACKS)
    unknown = [n for n in names if n not in TRACKS]
    if unknown:
        p.error("Pista desconocida: " + ", ".join(unknown))

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
