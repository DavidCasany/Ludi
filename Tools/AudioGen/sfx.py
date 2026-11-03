"""
sfx.py - Efectos de sonido de la INTERFAZ, con la misma paleta que la musica rock
(cuerdas de guitarra, distorsion y bateria), para que todo suene al mismo juego.

USO
    py sfx.py                                      -> genera todos en output/sfx
    py sfx.py --out ..\\..\\Assets\\Resources\\Audio\\SFX  -> directamente en Unity
"""
import argparse
from pathlib import Path

import numpy as np

import synth as S
import rock as R
from synth import SR, midi_hz, lowpass, highpass, bandpass, time_axis

RNG = np.random.default_rng(2026)
KIT = R.RockKit()


def mix(length, *parts):
    """Suma (señal, inicio_en_segundos, ganancia) en un buffer mono."""
    out = np.zeros(int(length * SR))
    for sig, start, gain in parts:
        i = int(start * SR)
        n = min(len(sig), len(out) - i)
        if n > 0:
            out[i:i + n] += sig[:n] * gain
    return out


def finish(x, peak, fade=0.02):
    """Recorta el silencio final, normaliza el pico y suaviza el final."""
    env = np.abs(x)
    last = np.nonzero(env > env.max() * 0.002)[0]
    x = x[: (last[-1] + 1) if len(last) else len(x)]
    x = x / (np.abs(x).max() + 1e-9) * peak
    f = min(int(fade * SR), len(x) // 2)
    x[-f:] *= np.linspace(1, 0, f)
    return x


def pluck(m, dur, damping=0.95, bright=0.85):
    s = R.karplus(midi_hz(m), dur, RNG, damping=damping, bright=bright)
    return s * np.linspace(1, 0, len(s)) ** 0.5


def swish(dur=0.08, lo=1500, hi=7000):
    t = time_axis(dur)
    n = RNG.uniform(-1, 1, len(t))
    env = np.sin(np.pi * t / dur) ** 2
    return bandpass(n, lo, hi) * env


def lead(m, dur, bend=0.0):
    return R.lead_guitar(midi_hz(m), dur, 1.0, RNG, bend=bend)


# ---------------------------------------------------------------------------

def ui_hover():
    s = pluck(76, 0.09, 0.9, 0.8)
    tick = KIT["hat"][: int(0.03 * SR)] * 0.3
    x = mix(0.1, (s, 0, 1.0), (tick, 0, 1.0))
    return finish(highpass(lowpass(x, 6500), 300), 0.32)


def ui_click():
    chug = R.power_chord(45, 0.1, RNG, mute=True)
    x = mix(0.18, (chug, 0, 1.0), (KIT["hat"], 0, 0.25), (KIT["kick"][: int(0.08 * SR)], 0, 0.25))
    return finish(x, 0.8)


def ui_back():
    a = R.power_chord(45, 0.07, RNG, mute=True)
    e = R.power_chord(40, 0.1, RNG, mute=True)
    x = mix(0.22, (a, 0, 0.8), (e, 0.065, 1.0))
    return finish(lowpass(x, 3000), 0.7)


def ui_pause_open():
    """Acorde que se "frena" como una cinta al parar (cae de tono y se apaga)."""
    ch = R.power_chord(45, 0.55, RNG, dive=(0.03, 14))
    t = np.arange(len(ch)) / SR
    ch *= np.clip(1 - t / 0.6, 0, 1) ** 1.5
    x = mix(0.65, (ch, 0, 1.0), (KIT["kick"], 0, 0.35))
    return finish(lowpass(x, 4000), 0.75, 0.05)


def ui_pause_close():
    """Al reves: el acorde "arranca" (sube desde la nada) y remata con un golpe."""
    ch = R.power_chord(45, 0.35, RNG)
    rev = ch[::-1] * np.linspace(0, 1, len(ch)) ** 2
    hit = R.power_chord(45, 0.12, RNG, mute=True)
    x = mix(0.55, (rev, 0, 0.8), (hit, len(rev) / SR, 1.0), (KIT["kick"], len(rev) / SR, 0.4))
    return finish(x, 0.75)


def ui_slider():
    s = pluck(88, 0.035, 0.85, 0.9)
    return finish(highpass(s, 800), 0.25, 0.008)


def ui_cards_appear():
    parts = []
    for i, m in enumerate((57, 60, 64)):
        t0 = i * 0.085
        parts.append((swish(0.09), t0, 0.35))
        parts.append((pluck(m, 0.25, 0.97, 0.75), t0 + 0.02, 0.9))
    x = mix(0.5, *parts)
    return finish(highpass(x, 120), 0.6, 0.06)


def ui_card_pick_common():
    ch = R.power_chord(48, 0.32, RNG)
    x = mix(0.45, (ch, 0, 1.0), (KIT["kick"], 0, 0.6))
    return finish(x, 0.8, 0.06)


def ui_card_pick_rare():
    ch = R.power_chord(48, 0.45, RNG)
    x = mix(0.75, (ch, 0, 0.9), (KIT["kick"], 0, 0.6), (KIT["snare"], 0, 0.5),
            (lead(76, 0.5, bend=-2), 0.06, 0.8))
    return finish(x, 0.85, 0.08)


def ui_card_pick_epic():
    ch = R.power_chord(48, 0.9, RNG)
    arp = [(lead(m, 0.11), 0.05 + i * 0.09, 0.75) for i, m in enumerate((72, 76, 79))]
    x = mix(1.6, (ch, 0, 0.9), (KIT["kick"], 0, 0.7), (KIT["snare"], 0, 0.5),
            (KIT["crash"], 0.32, 0.55), (KIT["kick"], 0.32, 0.6),
            (lead(84, 0.8, bend=-2), 0.32, 0.85), *arp)
    return finish(x, 0.9, 0.15)


def ui_play_match():
    ch = R.power_chord(45, 1.1, RNG)
    x = mix(1.8, (ch, 0, 1.0), (KIT["kick"], 0, 0.9), (KIT["snare"], 0, 0.8),
            (KIT["crash"], 0, 0.7), (lead(81, 0.9, bend=-2), 0.02, 0.6))
    return finish(x, 0.92, 0.2)


def ui_deny():
    a = R.power_chord(40, 0.07, RNG, mute=True)
    x = mix(0.2, (a, 0, 1.0), (a, 0.085, 0.9))
    return finish(lowpass(x, 900), 0.6)



# ===========================================================================
# EFECTOS DEL PARTIDO
# ===========================================================================

def noise(dur):
    return RNG.uniform(-1, 1, int(dur * SR))


def P(*arrs):
    """Suma señales de distinta longitud (rellena con ceros)."""
    n = max(len(a) for a in arrs)
    out = np.zeros(n)
    for a in arrs:
        out[:len(a)] += a
    return out


def modes(spec, dur, jitter=0.03):
    """Sintesis modal: suma de resonancias que se apagan (madera, plastico, metal)."""
    t = time_axis(dur)
    out = np.zeros_like(t)
    for f, decay, amp in spec:
        f *= 1 + RNG.uniform(-jitter, jitter)
        out += amp * np.sin(2 * np.pi * f * t + RNG.uniform(0, 6)) * np.exp(-t * decay)
    return out


def click(dur=0.01, lo=2500, decay=900):
    t = time_axis(dur)
    return highpass(noise(dur), lo) * np.exp(-t * decay)


def thump(f0=90, f1=45, dur=0.25, decay=18):
    t = time_axis(dur)
    f = f1 + (f0 - f1) * np.exp(-t * 25)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * decay)


def sweep(dur, f_start, f_end, width=0.6, shape=None):
    """Ruido filtrado cuyo centro se mueve de f_start a f_end (whoosh)."""
    n = int(dur * SR)
    x = noise(dur)
    out = np.zeros(n)
    block = 256
    from scipy.signal import butter, sosfilt, sosfilt_zi
    zi = None
    for b0 in range(0, n, block):
        k = b0 / max(1, n - 1)
        fc = f_start * (f_end / f_start) ** k
        lo, hi = max(40, fc * (1 - width / 2)), min(SR * 0.45, fc * (1 + width / 2))
        sos = butter(2, [lo, hi], btype="band", fs=SR, output="sos")
        if zi is None:
            zi = np.zeros((sos.shape[0], 2))
        seg, zi = sosfilt(sos, x[b0:b0 + block], zi=zi)
        out[b0:b0 + block] = seg
    t = np.arange(n) / n
    env = shape(t) if shape else np.sin(np.pi * t) ** 1.5
    return out * env


WOOD_A = [(1150, 70, 1.0), (2280, 95, 0.6), (3420, 130, 0.35), (540, 45, 0.5)]
WOOD_B = [(980, 65, 1.0), (2050, 90, 0.55), (3150, 120, 0.3), (470, 40, 0.5)]
STICK = [(690, 55, 1.0), (1580, 80, 0.6), (2700, 110, 0.35)]


def crack(spec, dur=0.15, gain=1.0):
    return P(modes(spec, dur), click(0.012) * 1.2) * gain


# ---- Bola y stick ----

def impact(dur, bright, body, seed_shift=0.0, decay=90.0):
    """Golpe realista: un impulso de ruido muy corto que excita resonancias amortiguadas
    (palo + bola de plastico duro). Nada de tonos puros: por eso suena a objeto real."""
    from scipy.signal import iirpeak, lfilter
    n = int(dur * SR)
    t = np.arange(n) / SR
    exc = RNG.uniform(-1, 1, n) * np.exp(-t * 2500)            # contacto de ~1 ms
    out = lowpass(exc, 8000) * 0.3 * bright                     # parte "crack" de banda ancha
    # Resonancias inarmonicas: bola (agudas, cortas) y palo (medias), algo aleatorias
    res = [(2900, 9, 1.0 * bright), (4300, 10, 0.7 * bright), (6100, 12, 0.4 * bright),
           (1350, 7, 0.8), (820, 6, 0.7 * body), (430, 5, 0.6 * body)]
    for f, q, g in res:
        f *= 1 + RNG.uniform(-0.06, 0.06) + seed_shift
        b, a = iirpeak(f, q, fs=SR)
        out += lfilter(b, a, exc) * g * 3.0
    out *= np.exp(-t * decay)                                   # todo muere muy rapido
    # flexion del palo: golpe grave muy breve
    out += np.sin(2 * np.pi * (210 + RNG.uniform(-20, 20)) * t) * np.exp(-t * 70) * 0.35 * body
    return out


def scrape(dur, lo=900, hi=6000, level=1.0):
    """El stick barriendo el suelo justo antes de golpear la bola."""
    t = time_axis(dur)
    env = (t / dur) ** 1.5                                      # crece hasta el golpe
    return bandpass(noise(dur), lo, hi) * env * level


def arena(x, size=0.5, wet=0.18):
    """Pabellon: un rebote temprano en las vallas + una cola corta."""
    early = np.zeros(len(x) + int(0.03 * SR))
    d = int(0.013 * SR)
    early[d:d + len(x)] += lowpass(x, 3500) * 0.25
    early[:len(x)] += x
    return room(early, size, wet)


def follow(dur, lo, hi, level):
    """Barrido del stick por el suelo DESPUES del golpe (el gesto sigue)."""
    t = time_axis(dur)
    return bandpass(noise(dur), lo, hi) * np.exp(-t * 35) * level


def shot(seed_shift):
    # El juego lanza el sonido en el instante del golpe: el "crack" tiene que estar al principio
    sc = scrape(0.012, 1000, 6000, 0.2)
    hit = impact(0.25, 0.9, 1.0, seed_shift, decay=80)
    fol = follow(0.12, 800, 4000, 0.12)
    air = lowpass(noise(0.25), 1200) * np.exp(-time_axis(0.25) * 14) * 0.05   # aire de la bola, muy sutil
    x = mix(0.5, (sc, 0, 1.0), (hit, 0.012, 1.0), (fol, 0.015, 1.0), (air, 0.02, 1.0))
    return finish(lowpass(arena(x), 9000), 0.95, 0.06)


def sfx_shot_1():
    return shot(0.0)


def sfx_shot_2():
    return shot(-0.07)


def sfx_pass():
    sc = scrape(0.012, 600, 3000, 0.1)
    hit = impact(0.18, 0.45, 0.8, 0.03, decay=110)
    fol = follow(0.1, 500, 2500, 0.08)
    x = mix(0.35, (sc, 0, 1.0), (lowpass(hit, 4500), 0.012, 1.0), (fol, 0.015, 1.0))
    return finish(arena(x, 0.4, 0.14), 0.6, 0.04)


def sfx_stick_swing():
    return finish(sweep(0.2, 500, 2200, 0.9), 0.45, 0.02)


def sfx_steal():
    blip = np.sin(2 * np.pi * np.cumsum(np.linspace(700, 1500, int(0.08 * SR))) / SR) * np.linspace(1, 0, int(0.08 * SR))
    x = mix(0.35, (crack(STICK, 0.12), 0, 1.0), (crack(WOOD_A, 0.1, 0.7), 0.03, 1.0),
            (sweep(0.18, 900, 2600, 0.8), 0, 0.35), (blip, 0.05, 0.12))
    return finish(x, 0.85, 0.04)


def sfx_intercept():
    x = mix(0.3, (crack(WOOD_B, 0.12), 0, 1.0), (thump(150, 80, 0.15, 30), 0.005, 0.5))
    return finish(x, 0.8, 0.03)


def sfx_stick_block():
    x = mix(0.25, (crack(STICK, 0.1), 0, 1.0), (crack(STICK, 0.1, 0.7), 0.028, 1.0))
    return finish(x, 0.75, 0.03)


def board(intensity):
    spec = [(105, 16, 1.0), (175, 22, 0.6), (260, 28, 0.4), (720, 60, 0.35 * intensity), (1250, 90, 0.2 * intensity)]
    t = time_axis(0.35)
    rattle = lowpass(noise(0.35), 1500 + 2500 * intensity) * np.exp(-t * 22) * 0.35
    return P(modes(spec, 0.35), rattle, click(0.008, 1500, 700) * 0.6 * intensity)


def sfx_ball_wall_soft():
    return finish(lowpass(board(0.3), 2500), 0.35, 0.05)


def sfx_ball_wall_mid():
    return finish(board(0.65), 0.6, 0.05)


def sfx_ball_wall_hard():
    x = mix(0.45, (board(1.0), 0, 1.0), (thump(90, 45, 0.3, 12), 0, 0.5))
    return finish(x, 0.9, 0.06)


def sfx_juke():
    t = time_axis(0.25)
    scrape = bandpass(noise(0.25), 1500, 5000) * np.minimum(1, t / 0.03) * np.exp(-t * 14)
    x = mix(0.3, (sweep(0.22, 2800, 700, 0.9), 0, 0.7), (scrape, 0.02, 0.4))
    return finish(x, 0.6, 0.04)


def sfx_keeper_save():
    t = time_axis(0.3)
    pad = lowpass(noise(0.3), 700) * np.exp(-t * 25)
    slap = bandpass(noise(0.3), 1000, 3500) * np.exp(-t * 60)
    x = mix(0.35, (pad, 0, 1.0), (slap, 0, 0.5), (thump(95, 50, 0.25, 16), 0, 0.9))
    return finish(x, 0.9, 0.05)


def sfx_ball_pickup():
    x = mix(0.12, (modes([(1500, 90, 1.0), (2900, 140, 0.4)], 0.08), 0, 0.6), (thump(160, 100, 0.06, 50), 0, 0.4))
    return finish(x, 0.35, 0.02)


def sfx_skate_loop():
    """Ruedas rodando (loop perfecto de 2 s): ruido grave con 'golpecitos' de juntas del suelo."""
    L = int(2.0 * SR)
    def circ(x, f):                                    # filtrado circular: sin corte en el loop
        y = f(np.concatenate([x, x]))
        return y[L:]
    rumble = circ(RNG.uniform(-1, 1, L), lambda v: bandpass(v, 140, 900))
    hiss = circ(RNG.uniform(-1, 1, L), lambda v: bandpass(v, 3000, 7000)) * 0.12
    t = np.arange(L) / SR
    grit = np.ones(L)
    for _ in range(26):                                # irregularidades del suelo
        c = RNG.uniform(0, 2.0)
        d = np.minimum(np.abs(t - c), 2.0 - np.abs(t - c))
        grit += 0.5 * np.exp(-(d / 0.012) ** 2)
    wobble = 1 + 0.25 * np.sin(2 * np.pi * 3 * t)      # 3 Hz: entero en 2 s
    x = (rumble * grit * wobble + hiss)
    return x / np.abs(x).max() * 0.5


def sfx_sprint():
    t = time_axis(0.35)
    push = bandpass(noise(0.35), 900, 4500) * np.minimum(1, t / 0.04) * np.exp(-t * 9)
    x = mix(0.4, (push, 0, 1.0), (thump(80, 50, 0.2, 20), 0, 0.4), (sweep(0.3, 700, 1800, 0.8), 0.03, 0.3))
    return finish(x, 0.55, 0.05)


def breath(dur, f1=750, f2=1250):
    t = time_axis(dur)
    n = noise(dur)
    v = bandpass(n, f1 * 0.8, f1 * 1.2) + 0.6 * bandpass(n, f2 * 0.85, f2 * 1.15) + 0.2 * highpass(n, 3000)
    return v * np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 0.7


def sfx_exhausted():
    x = mix(0.9, (breath(0.32), 0, 1.0), (breath(0.42, 650, 1100), 0.38, 0.9))
    return finish(x, 0.45, 0.06)


def sfx_player_bump():
    t = time_axis(0.25)
    body = lowpass(noise(0.25), 500) * np.exp(-t * 30)
    x = mix(0.3, (body, 0, 1.0), (thump(85, 45, 0.22, 20), 0, 0.9),
            (modes([(900, 70, 1.0), (2100, 110, 0.5)], 0.1), 0.01, 0.3))
    return finish(x, 0.7, 0.04)


# ---- QTE y camara lenta ----

def sfx_slowmo_in():
    t = time_axis(0.8)
    f = 85 * np.exp(-t * 1.5) + 35
    boom = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 3.5)
    x = mix(0.85, (sweep(0.6, 3500, 250, 1.0, lambda k: (1 - k) ** 0.6 * np.minimum(1, k * 12)), 0, 0.8), (boom, 0, 0.7))
    return finish(x, 0.75, 0.08)


def sfx_slowmo_out():
    x = sweep(0.35, 250, 3500, 1.0, lambda k: k ** 1.2 * (1 - k) ** 0.25)
    return finish(x, 0.6, 0.03)


def sfx_qte_success():
    ch = R.power_chord(48, 0.6, RNG)
    x = mix(0.9, (ch, 0, 1.0), (KIT["kick"], 0, 0.7), (KIT["snare"], 0, 0.6),
            (KIT["crash"], 0, 0.45), (lead(84, 0.5, bend=-2), 0.03, 0.6))
    return finish(x, 0.9, 0.12)


def sfx_qte_fail():
    a = R.power_chord(40, 0.12, RNG, mute=True)
    l = R.pitch_dive(lead(76, 0.6), 0.08, 5)
    x = mix(0.8, (a, 0, 1.0), (l, 0.04, 0.6))
    return finish(lowpass(x, 3500), 0.75, 0.1)


def sfx_qte_tick():
    return finish(P(KIT["rim"][: int(0.05 * SR)], modes([(2400, 120, 0.4)], 0.05)), 0.3, 0.01)


# ---- Arbitro, pabellon y sorteo ----

def whistle(dur):
    """Silbato de bola (pea whistle): tono agudo con el 'trino' de la bolita."""
    t = time_axis(dur)
    trill = np.sin(2 * np.pi * (26 + 4 * np.sin(2 * np.pi * 0.7 * t)) * t)
    f = 3050 * (1 + 0.022 * trill)
    ph = 2 * np.pi * np.cumsum(f) / SR
    tone = np.sin(ph) + 0.18 * np.sin(2 * ph) + 0.05 * np.sin(3 * ph)
    am = 0.78 + 0.22 * trill
    air = bandpass(noise(dur), 2200, 7000) * 0.12
    env = np.minimum(1, t / 0.02) * np.minimum(1, (dur - t) / 0.05)
    return (tone * am + air) * np.clip(env, 0, 1)


def room(x, seconds=0.9, mix_=0.25):
    ir = S.reverb_ir(seconds)[0]
    from scipy.signal import fftconvolve
    wet = fftconvolve(x, ir)[: len(x) + int(seconds * SR)]
    out = np.zeros(len(wet))
    out[: len(x)] += x
    return out + wet * mix_


def sfx_whistle_short():
    return finish(room(whistle(0.35)), 0.75, 0.08)


def sfx_whistle_long():
    return finish(room(whistle(1.1)), 0.75, 0.1)


def sfx_whistle_end():
    x = mix(1.8, (whistle(0.28), 0, 1.0), (whistle(0.28), 0.42, 1.0), (whistle(0.95), 0.84, 1.0))
    return finish(room(x), 0.75, 0.1)


def sfx_goal_horn():
    dur = 2.4
    t = time_axis(dur)
    out = np.zeros_like(t)
    for f0 in (146.8, 185.0, 220.0):
        f = f0 * (1 + 0.004 * np.sin(2 * np.pi * 5 * t))
        ph = 2 * np.pi * np.cumsum(f) / SR
        for k in range(1, 22):
            out += np.sin(k * ph) / k ** 1.1
    out = np.tanh(out * 0.6)
    out = lowpass(out, 2600, 2)
    env = np.minimum(1, t / 0.05) * np.clip((dur - t) / 0.35, 0, 1)
    return finish(room(out * env, 1.6, 0.35), 0.85, 0.2)


def voice(dur, f0, f1, f2, glide=0.15, onset=0.0, decay=1.2, breathy=0.3):
    """Una voz de publico: pulso glotal + formantes de vocal + aire."""
    t = time_axis(dur)
    k = np.clip((t - onset) / 0.35, 0, 1)
    f = f0 * (1 + glide * np.sin(np.pi * k) )                # sube y baja ("¡ehhh!")
    f *= 1 + 0.01 * RNG.standard_normal(len(t)).cumsum() / np.sqrt(len(t))
    ph = 2 * np.pi * np.cumsum(f) / SR
    src = sum(np.sin(h * ph) / h for h in range(1, 18))
    src = src + breathy * RNG.uniform(-1, 1, len(t))
    v = bandpass(src, f1 * 0.85, f1 * 1.15) + 0.7 * bandpass(src, f2 * 0.88, f2 * 1.12)
    env = np.where(t < onset, 0, np.minimum(1, (t - onset) / 0.08) * np.exp(-np.maximum(0, t - onset - 0.25) * decay))
    return v * env


def crowd(dur, n, vowels, glide, decay, onset_spread, roar_level=0.6):
    out = np.zeros(int(dur * SR))
    for _ in range(n):
        f1, f2 = vowels[RNG.integers(len(vowels))]
        male = RNG.random() < 0.6
        f0 = RNG.uniform(105, 160) if male else RNG.uniform(190, 290)
        v = voice(dur, f0, f1 * RNG.uniform(0.92, 1.08), f2 * RNG.uniform(0.92, 1.08),
                  glide * RNG.uniform(0.5, 1.4), RNG.uniform(0, onset_spread), decay * RNG.uniform(0.7, 1.3))
        out += v * RNG.uniform(0.4, 1.0)
    t = time_axis(dur)
    roar = bandpass(noise(dur), 250, 3000) * roar_level * np.minimum(1, t / 0.15) * np.exp(-t * decay * 0.6)
    out = out / (np.abs(out).max() + 1e-9) + roar
    k = np.clip((t - dur * 0.55) / (dur * 0.45), 0, 1)
    return out * (1 - k) ** 2          # se apaga de forma natural (sin corte)


def sfx_crowd_cheer():
    x = crowd(3.6, 70, [(750, 1250), (650, 1100), (550, 1800), (450, 2000)], 0.25, 0.75, 0.35)
    return finish(room(x, 1.8, 0.4), 0.8, 0.5)


def sfx_crowd_ooh():
    x = crowd(1.8, 55, [(330, 750), (380, 850), (430, 900)], -0.08, 1.6, 0.15, 0.25)
    t = time_axis(len(x) / SR)
    x *= np.minimum(1, t / 0.25)
    return finish(room(x, 1.6, 0.4), 0.65, 0.3)


def sfx_crowd_loop():
    """Murmullo del pabellon (loop perfecto de 8 s)."""
    L = int(8.0 * SR)
    out = np.zeros(L)
    vowels = [(750, 1250), (500, 1500), (400, 2000), (600, 1000), (350, 800)]
    for _ in range(140):
        dur = RNG.uniform(0.15, 0.6)
        f1, f2 = vowels[RNG.integers(len(vowels))]
        f0 = RNG.uniform(100, 280)
        v = voice(dur + 0.4, f0, f1, f2, RNG.uniform(-0.1, 0.1), 0, 4.0, 0.5)
        start = RNG.integers(0, L)
        idx = (start + np.arange(len(v))) % L               # se envuelve: sin corte en el loop
        out[idx] += v * RNG.uniform(0.2, 1.0)
    base = RNG.uniform(-1, 1, L)
    roar = bandpass(np.concatenate([base, base]), 200, 2200)[L:]
    t = np.arange(L) / SR
    roar *= 0.8 + 0.2 * np.sin(2 * np.pi * t / 8.0) + 0.1 * np.sin(2 * np.pi * 3 * t / 8.0)
    x = out / np.abs(out).max() + roar * 0.9
    x = lowpass(np.concatenate([x, x]), 4500)[L:]
    return x / np.abs(x).max() * 0.5


def sfx_coin_flip():
    t = time_axis(0.9)
    ring = modes([(4250, 5, 1.0), (6130, 6, 0.6), (8310, 8, 0.4), (9480, 9, 0.3)], 0.9, 0.0)
    spin = 0.55 + 0.45 * np.sin(2 * np.pi * (22 - 8 * t) * t)
    flick = P(modes([(2600, 200, 1.0)], 0.03), click(0.01))
    x = mix(0.9, (flick, 0, 0.8), (ring * spin * np.exp(-t * 2.5), 0.005, 0.6))
    return finish(x, 0.55, 0.15)


def sfx_coin_land():
    parts = []
    t0, amp, gap = 0.0, 1.0, 0.11
    for i in range(6):
        hit = P(modes([(3900, 35, 1.0), (5800, 45, 0.6), (8100, 60, 0.4)], 0.25, 0.02), click(0.006, 3000))
        parts.append((hit, t0, amp))
        t0 += gap
        amp *= 0.6
        gap *= 0.6
    wobble_t = time_axis(0.35)
    wob = modes([(4100, 10, 1.0)], 0.35, 0.0) * (0.5 + 0.5 * np.sin(2 * np.pi * (30 + 60 * wobble_t) * wobble_t)) * np.exp(-wobble_t * 9)
    parts.append((wob, t0, 0.25))
    x = mix(1.0, *parts)
    return finish(x, 0.6, 0.08)


MATCH_SFX = {
    "sfx_shot_1": sfx_shot_1, "sfx_shot_2": sfx_shot_2, "sfx_pass": sfx_pass,
    "sfx_stick_swing": sfx_stick_swing, "sfx_steal": sfx_steal, "sfx_intercept": sfx_intercept,
    "sfx_stick_block": sfx_stick_block, "sfx_ball_wall_soft": sfx_ball_wall_soft,
    "sfx_ball_wall_mid": sfx_ball_wall_mid, "sfx_ball_wall_hard": sfx_ball_wall_hard,
    "sfx_juke": sfx_juke, "sfx_keeper_save": sfx_keeper_save, "sfx_ball_pickup": sfx_ball_pickup,
    "sfx_skate_loop": sfx_skate_loop, "sfx_sprint": sfx_sprint, "sfx_exhausted": sfx_exhausted,
    "sfx_player_bump": sfx_player_bump,
    "sfx_slowmo_in": sfx_slowmo_in, "sfx_slowmo_out": sfx_slowmo_out, "sfx_qte_success": sfx_qte_success,
    "sfx_qte_fail": sfx_qte_fail, "sfx_qte_tick": sfx_qte_tick,
    "sfx_whistle_short": sfx_whistle_short, "sfx_whistle_long": sfx_whistle_long,
    "sfx_whistle_end": sfx_whistle_end, "sfx_goal_horn": sfx_goal_horn, "sfx_crowd_cheer": sfx_crowd_cheer,
    "sfx_crowd_ooh": sfx_crowd_ooh, "sfx_crowd_loop": sfx_crowd_loop,
    "sfx_coin_flip": sfx_coin_flip, "sfx_coin_land": sfx_coin_land,
}


SFX = {
    "ui_hover": ui_hover,
    "ui_click": ui_click,
    "ui_back": ui_back,
    "ui_deny": ui_deny,
    "ui_pause_open": ui_pause_open,
    "ui_pause_close": ui_pause_close,
    "ui_slider": ui_slider,
    "ui_cards_appear": ui_cards_appear,
    "ui_card_pick_common": ui_card_pick_common,
    "ui_card_pick_rare": ui_card_pick_rare,
    "ui_card_pick_epic": ui_card_pick_epic,
    "ui_play_match": ui_play_match,
}
SFX.update(MATCH_SFX)


def write_mono(path, x):
    import wave
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def main():
    p = argparse.ArgumentParser(description="Genera los efectos (interfaz y partido) en .wav")
    p.add_argument("names", nargs="*", help="Efectos (por defecto todos): " + ", ".join(SFX))
    p.add_argument("--out", default=str(Path(__file__).parent / "output" / "sfx"))
    args = p.parse_args()
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    for n in args.names or list(SFX):
        x = SFX[n]()
        write_mono(out / f"{n}.wav", x)
        print(f"  {n + '.wav':26s} {len(x) / SR:5.2f} s")
    print(f"Listo. Archivos en: {out.resolve()}")


if __name__ == "__main__":
    main()
