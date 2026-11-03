"""
rock.py - Instrumentos de rock sintetizados desde cero.

  - Guitarra ritmica: modelo fisico de cuerda pulsada (Karplus-Strong)
    + distorsion de amplificador + simulacion de pantalla (cabinet).
  - Bajo electrico tocado con puya (mismo modelo, mas oscuro).
  - Guitarra solista: onda de sierra con bends y vibrato, muy saturada.
  - Bateria de rock (bombo con pegada, caja grande, toms, ride, crash).

No usa samples: todo sale de formulas, sin licencias de terceros.
"""
import numpy as np
from scipy.signal import lfilter

import synth as S
from synth import SR, midi_hz, lowpass, highpass, bandpass, time_axis


# ---------------------------------------------------------------------------
# Cuerda pulsada (Karplus-Strong vectorizado por periodos)
# ---------------------------------------------------------------------------

def karplus(freq, seconds, rng, damping=0.996, bright=0.6, pick_pos=0.13):
    """Simula una cuerda: un golpe de ruido que recorre un bucle con perdidas."""
    period = SR / freq - 0.5                      # el filtro promedio añade medio sample
    N = max(2, int(round(period)))
    n = int(seconds * SR)
    exc = rng.uniform(-1, 1, N)
    # Brillo de la puya: filtro paso-bajo de un polo sobre la excitacion
    a = 1.0 - bright
    exc = lfilter([1 - a], [1, -a], exc)
    # Posicion de la puya (filtro peine)
    d = max(1, int(N * pick_pos))
    exc = exc - np.roll(exc, d)
    exc /= (np.abs(exc).max() + 1e-9)

    y = np.zeros(n + N + 1)
    off = N + 1                                    # zona "negativa" de ceros
    y[off:off + min(N, n)] = exc[:min(N, n)]
    g = damping * 0.5
    pos = N
    while pos < n:
        end = min(pos + N, n)
        i = np.arange(pos, end) + off
        y[i] += g * (y[i - N] + y[i - N - 1])
        pos = end
    return y[off:off + n]


def _release_env(n_total, dur_sec, rel=0.03):
    env = np.ones(n_total)
    a = int(dur_sec * SR)
    r = int(rel * SR)
    if a < n_total:
        env[a:a + r] = np.linspace(1, 0, len(env[a:a + r]))
        env[a + r:] = 0
    return env


def cabinet(x):
    """Pantalla de guitarra 4x12 aproximada: corta graves sueltos y agudos asperos."""
    x = highpass(x, 85, 2)
    x = lowpass(x, 5200, 4)
    # pequeño realce de medios (presencia)
    return x + 0.35 * bandpass(x, 1400, 3200, 2)


def power_chord(root, dur, rng, mute=False, drive=22.0, fifth=True, octave=True, dive=None):
    """Acorde de quinta distorsionado (raiz + 5a + 8a), rasgueado hacia abajo.
    mute=True -> 'palm mute' (chug corto y seco).
    dive=(segundos, semitonos) -> efecto de palanca: el acorde cae de tono."""
    notes = [root] + ([root + 7] if fifth else []) + ([root + 12] if octave else [])
    total = dur + 0.06
    n = int(total * SR)
    sig = np.zeros(n)
    for i, m in enumerate(notes):
        delay = int((0.004 + rng.uniform(0, 0.003)) * i * SR)        # rasgueo
        s = karplus(midi_hz(m), total, rng,
                    damping=0.93 if mute else 0.9985,
                    bright=0.45 if mute else 0.75)
        sig[delay:] += s[:n - delay]
    sig = highpass(sig, 120, 2)                                      # graves tensos antes de saturar
    sig = np.tanh(sig * drive) * 0.9
    sig = cabinet(sig)
    if mute:
        sig = lowpass(sig, 1700, 2) * 1.4
    sig *= _release_env(n, dur, 0.02 if mute else 0.04)
    if dive:
        sig = pitch_dive(sig, *dive)
    return sig * 0.55


def pitch_dive(sig, start_sec, semitones):
    """Baja el tono progresivamente desde start_sec (palanca de vibrato)."""
    n = len(sig)
    t = np.arange(n) / SR
    k = np.clip((t - start_sec) / max(0.05, (n / SR - start_sec)), 0, 1)
    rate = 2 ** (-semitones * k ** 1.5 / 12)
    pos = np.cumsum(rate)
    pos = pos[pos < n - 1]
    return np.interp(pos, np.arange(n), sig)


# ---------------------------------------------------------------------------
# Bajo electrico
# ---------------------------------------------------------------------------

def bass_guitar(freq, dur, vel=1.0, rng=None):
    rng = rng or np.random.default_rng()
    total = dur + 0.03
    s = karplus(freq, total, rng, damping=0.997, bright=0.55, pick_pos=0.2)
    t = time_axis(total)[:len(s)]
    s = s + 0.6 * np.sin(2 * np.pi * freq * t) * np.exp(-t * 2)
    s = np.tanh(s * 2.2)
    s = lowpass(s, 2200, 2) + 0.15 * bandpass(s, 700, 1600, 2)      # "clank" de la puya
    s *= _release_env(len(s), dur, 0.02)
    return s * vel * 0.8


# ---------------------------------------------------------------------------
# Guitarra solista
# ---------------------------------------------------------------------------

def lead_guitar(freq, dur, vel=1.0, rng=None, bend=0.0, vibrato=True, drive=7.0):
    """bend: semitonos desde los que sube a la nota al atacar (p. ej. -2 = bend de tono)."""
    total = dur + 0.12
    t = time_axis(total)
    f = np.full_like(t, freq)
    if bend:
        k = np.clip(t / 0.09, 0, 1)
        f = freq * 2 ** (bend * (1 - k) ** 2 / 12)
    if vibrato and dur > 0.3:
        depth = 0.016 * np.clip((t - 0.22) / 0.2, 0, 1)
        f = f * (1 + depth * np.sin(2 * np.pi * 6.0 * t))
    out = np.zeros_like(t)
    for det in (0.0, 0.07):
        ph = 2 * np.pi * np.cumsum(f * 2 ** (det / 12)) / SR
        K = max(1, int(7000 / freq))
        for k in range(1, K + 1):
            out += np.sin(k * ph) / k
    out = highpass(out, 350, 2)
    out = np.tanh(out * drive)
    out = cabinet(out)
    env = np.minimum(1, t / 0.006) * (0.85 + 0.15 * np.exp(-t * 6))
    out *= env * _release_env(len(t), dur, 0.05)
    return out * vel * 0.5


# ---------------------------------------------------------------------------
# Bateria de rock
# ---------------------------------------------------------------------------

class RockKit(S.DrumKit):
    def __init__(self, seed=17):
        super().__init__(seed)
        rng = np.random.default_rng(seed + 1)
        noise = lambda s: rng.uniform(-1, 1, int(s * SR))

        # Bombo: cuerpo grave + pegada del mazo
        t = time_axis(0.5)
        f = 52 + 140 * np.exp(-t * 28)
        body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 6.5)
        beater = bandpass(noise(0.5), 2500, 6000) * np.exp(-t * 220) * 0.7
        self.s["kick"] = S.fade_edges(np.tanh((body * 1.2 + beater) * 2.2) * 0.9, 0.0003, 0.02)

        # Caja: dos modos de parche + bordonera larga
        t = time_axis(0.45)
        tone = (np.sin(2 * np.pi * 195 * t) + 0.6 * np.sin(2 * np.pi * 330 * t)) * np.exp(-t * 18) * 0.7
        wires = bandpass(noise(0.45), 1500, 10000) * np.exp(-t * 9)
        crack = highpass(noise(0.45), 3000) * np.exp(-t * 90) * 0.6
        self.s["snare"] = S.fade_edges(np.tanh((tone + wires + crack) * 2.0) * 0.85, 0.0003, 0.03)
        self.s["ghost"] = self.s["snare"][: int(0.1 * SR)] * np.linspace(1, 0, int(0.1 * SR)) * 0.3

        # Toms grandes
        for name, base in (("tom_hi", 165), ("tom_mid", 120), ("tom_lo", 82)):
            t = time_axis(0.7)
            ff = base * (1 + 0.45 * np.exp(-t * 18))
            tom = np.sin(2 * np.pi * np.cumsum(ff) / SR) * np.exp(-t * 5.5)
            att = bandpass(noise(0.7), 1500, 5000) * np.exp(-t * 120) * 0.4
            self.s[name] = S.fade_edges(np.tanh((tom + att) * 2.0) * 0.85, 0.0003, 0.03)

        # Ride: parciales metalicos + "ping"
        t = time_axis(1.2)
        parts = (3410, 4876, 5933, 7123, 8211, 9502)
        metal = sum(np.sin(2 * np.pi * p * t + rng.uniform(0, 6)) for p in parts) / len(parts)
        ride = (metal * 0.6 + highpass(noise(1.2), 6000, 2) * 0.35) * np.exp(-t * 3.5)
        ride += np.sin(2 * np.pi * 2500 * t) * np.exp(-t * 25) * 0.2
        self.s["ride"] = S.fade_edges(ride * 0.5, 0.0003, 0.05)

        # Crash mas grande
        t = time_axis(3.0)
        metal = sum(np.sin(2 * np.pi * p * t + rng.uniform(0, 6)) for p in (2870, 3990, 5160, 6440, 7830, 9120))
        crash = highpass(noise(3.0), 3000, 2) + 0.12 * metal
        crash *= np.exp(-t * 1.3) * np.minimum(1, t / 0.004)
        self.s["crash"] = S.fade_edges(crash * 0.55, 0.0003, 0.2)

        # Charles un poco mas presente
        t = time_axis(0.07)
        self.s["hat"] = S.fade_edges(highpass(noise(0.07), 7500, 4) * np.exp(-t * 75) * 1.0, 0.0003, 0.01)
