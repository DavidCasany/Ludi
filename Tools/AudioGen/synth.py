"""
synth.py - Motor de sintesis para la musica del juego de hockey patines.

Todo el sonido se genera desde cero con matematicas (osciladores, ruido,
envolventes y filtros). No se usa ningun sample externo, asi que el
resultado es 100 % original y sin licencias de terceros.

Lo usa music.py. No hace falta ejecutarlo directamente.
"""
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve

SR = 44100  # frecuencia de muestreo (Hz)


# ---------------------------------------------------------------------------
# Utilidades basicas
# ---------------------------------------------------------------------------

def midi_hz(m):
    """Nota MIDI -> frecuencia (69 = La 440 Hz)."""
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


def time_axis(seconds):
    return np.arange(int(seconds * SR)) / SR


def fade_edges(sig, attack=0.003, release=0.03):
    """Evita clics al principio y al final de cada nota."""
    n = len(sig)
    a = min(int(attack * SR), n // 2)
    r = min(int(release * SR), n // 2)
    env = np.ones(n)
    if a > 0:
        env[:a] = np.linspace(0, 1, a)
    if r > 0:
        env[-r:] *= np.linspace(1, 0, r)
    return sig * env


def lowpass(x, cutoff, order=2):
    sos = butter(order, cutoff, btype="low", fs=SR, output="sos")
    return sosfilt(sos, x, axis=-1)


def highpass(x, cutoff, order=2):
    sos = butter(order, cutoff, btype="high", fs=SR, output="sos")
    return sosfilt(sos, x, axis=-1)


def bandpass(x, lo, hi, order=2):
    sos = butter(order, [lo, hi], btype="band", fs=SR, output="sos")
    return sosfilt(sos, x, axis=-1)


# ---------------------------------------------------------------------------
# Instrumentos melodicos (sintesis aditiva y FM)
# ---------------------------------------------------------------------------

def _harmonic_limit(freq, maximum):
    """Numero de armonicos que caben por debajo de Nyquist (evita aliasing)."""
    return max(1, min(maximum, int((SR * 0.45) / freq)))


def bass(freq, dur, vel=1.0, rng=None):
    """Bajo funk 'pluck': brillante al pulsar y se oscurece rapido."""
    t = time_axis(dur + 0.04)
    out = np.zeros_like(t)
    for k in range(1, _harmonic_limit(freq, 30) + 1):
        decay = 2.0 + 7.0 * (k ** 0.75) * (0.6 + 0.4 * vel)
        out += (1.0 / k) * np.sin(2 * np.pi * k * freq * t) * np.exp(-t * decay)
    out += 0.6 * np.sin(2 * np.pi * freq * t)          # fundamental solida
    out *= np.exp(-t * 1.2)
    out = np.tanh(out * 1.6) * vel                       # un poco de saturacion
    return fade_edges(out, 0.002, 0.03)


def epiano(freq, dur, vel=1.0, rng=None):
    """Piano electrico FM (estilo años 80): campana suave y calida."""
    t = time_axis(dur + 0.35)
    index = (1.4 * vel + 0.3) * np.exp(-t * 3.5) + 0.25
    mod = np.sin(2 * np.pi * freq * t) * index
    body = np.sin(2 * np.pi * freq * t + mod)
    tine = np.sin(2 * np.pi * freq * 14.0 * t) * 0.06 * vel * np.exp(-t * 40)
    env = np.exp(-t * 1.1)
    rel = np.ones_like(t)
    off = int(dur * SR)
    rel[off:] = np.exp(-(t[off:] - dur) * 14)
    out = (body + tine) * env * rel * (0.55 + 0.45 * vel)
    return fade_edges(out, 0.002, 0.02)


def bell(freq, dur, vel=1.0, rng=None):
    """Campanita FM para melodias suaves (hub / eliminado)."""
    t = time_axis(dur + 0.8)
    index = 2.2 * np.exp(-t * 4) + 0.15
    mod = np.sin(2 * np.pi * freq * 3.5 * t) * index
    out = np.sin(2 * np.pi * freq * t + mod) * np.exp(-t * 2.2) * vel
    return fade_edges(out, 0.002, 0.05)


def pad(freq, dur, vel=1.0, rng=None):
    """Colchon suave: tres sierras desafinadas y muy filtradas."""
    t = time_axis(dur + 0.6)
    out = np.zeros_like(t)
    for det in (-0.12, 0.0, 0.11):
        f = freq * 2 ** (det / 12)
        ph = 0.0 if rng is None else rng.uniform(0, 2 * np.pi)
        for k in range(1, _harmonic_limit(f, 12) + 1):
            out += (1.0 / k ** 1.6) * np.sin(2 * np.pi * k * f * t + ph * k)
    attack = np.clip(t / 0.35, 0, 1)
    rel = np.ones_like(t)
    off = int(dur * SR)
    rel[off:] = np.exp(-(t[off:] - dur) * 6)
    return out * attack * rel * vel * 0.33


def brass(freq, dur, vel=1.0, rng=None):
    """Metales sinteticos (stabs): el 'filtro' se abre al atacar y se cierra."""
    t = time_axis(dur + 0.12)
    open_ = 1.0 - np.exp(-t * 60)                      # se abre en ~20 ms
    close = 0.45 + 0.55 * np.exp(-t * 6)               # y se va cerrando
    bright = open_ * close
    out = np.zeros_like(t)
    for det in (-0.08, 0.08):
        f = freq * 2 ** (det / 12)
        for k in range(1, _harmonic_limit(f, 24) + 1):
            amp = (1.0 / k) * np.exp(-(k - 1) * (0.55 - 0.42 * bright))
            out += amp * np.sin(2 * np.pi * k * f * t)
    env = np.minimum(1.0, t / 0.012)
    rel = np.ones_like(t)
    off = int(dur * SR)
    rel[off:] = np.exp(-(t[off:] - dur) * 30)
    out = np.tanh(out * 0.9) * env * rel * vel
    return fade_edges(out, 0.002, 0.02)


def lead(freq, dur, vel=1.0, rng=None):
    """Synth lead calido (onda cuadrada suavizada) con vibrato retardado."""
    t = time_axis(dur + 0.15)
    vib_depth = 0.004 * np.clip((t - 0.18) / 0.25, 0, 1)
    inst_f = freq * (1 + vib_depth * np.sin(2 * np.pi * 5.5 * t))
    phase = 2 * np.pi * np.cumsum(inst_f) / SR
    out = np.zeros_like(t)
    for k in range(1, _harmonic_limit(freq, 19) + 1, 2):   # armonicos impares
        out += (1.0 / k ** 1.25) * np.sin(k * phase)
    env = np.minimum(1.0, t / 0.015) * (0.8 + 0.2 * np.exp(-t * 5))
    rel = np.ones_like(t)
    off = int(dur * SR)
    rel[off:] = np.exp(-(t[off:] - dur) * 25)
    out = lowpass(out * env * rel * vel, 4200)
    return fade_edges(out, 0.003, 0.03)


# ---------------------------------------------------------------------------
# Bateria sintetizada
# ---------------------------------------------------------------------------

class DrumKit:
    """Genera una vez todos los golpes de bateria (con semilla fija)."""

    def __init__(self, seed=7):
        rng = np.random.default_rng(seed)
        noise = lambda s: rng.uniform(-1, 1, int(s * SR))
        self.s = {}

        # Bombo: seno con caida de tono + click
        t = time_axis(0.45)
        f = 48 + 120 * np.exp(-t * 32)
        kick = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 7)
        click = highpass(noise(0.45), 2000) * np.exp(-t * 350) * 0.35
        self.s["kick"] = fade_edges(np.tanh((kick + click) * 1.8), 0.0005, 0.02)

        # Caja: tono + ruido
        t = time_axis(0.3)
        tone = np.sin(2 * np.pi * 185 * t) * np.exp(-t * 22) * 0.55
        sn = bandpass(noise(0.3), 1200, 9000) * np.exp(-t * 15)
        self.s["snare"] = fade_edges(np.tanh((tone + sn) * 1.4), 0.0005, 0.02)

        # Caja fantasma (ghost) mas corta y suave
        self.s["ghost"] = self.s["snare"][: int(0.08 * SR)] * np.linspace(1, 0, int(0.08 * SR)) * 0.35

        # Rimshot / clave para partes tranquilas
        t = time_axis(0.08)
        rim = (np.sin(2 * np.pi * 1700 * t) + 0.5 * np.sin(2 * np.pi * 820 * t)) * np.exp(-t * 70)
        self.s["rim"] = fade_edges(rim * 0.6, 0.0005, 0.01)

        # Palmada: tres rafagas de ruido
        t = time_axis(0.35)
        env = np.zeros_like(t)
        for d in (0.0, 0.011, 0.022):
            m = t >= d
            env[m] = np.maximum(env[m], np.exp(-(t[m] - d) * (180 if d < 0.02 else 18)))
        self.s["clap"] = fade_edges(bandpass(noise(0.35), 900, 4000) * env * 1.2, 0.0005, 0.02)

        # Charles cerrado / abierto / shaker
        t = time_axis(0.08)
        self.s["hat"] = fade_edges(highpass(noise(0.08), 7000, 4) * np.exp(-t * 70) * 0.8, 0.0005, 0.01)
        t = time_axis(0.4)
        self.s["openhat"] = fade_edges(highpass(noise(0.4), 6500, 4) * np.exp(-t * 9) * 0.6, 0.0005, 0.05)
        t = time_axis(0.12)
        sh_env = np.minimum(1, t / 0.012) * np.exp(-t * 35)
        self.s["shaker"] = fade_edges(highpass(noise(0.12), 5000, 2) * sh_env * 0.5, 0.0005, 0.01)

        # Platillo (crash): ruido largo + parciales metalicos
        t = time_axis(2.6)
        metal = sum(np.sin(2 * np.pi * f * t + rng.uniform(0, 6)) for f in (3120, 4370, 5210, 6830, 7710))
        crash = highpass(noise(2.6), 3500, 2) + 0.15 * metal
        self.s["crash"] = fade_edges(crash * np.exp(-t * 1.6) * 0.45, 0.0005, 0.1)

        # Toms
        for name, base in (("tom_hi", 210), ("tom_mid", 150), ("tom_lo", 105)):
            t = time_axis(0.4)
            f = base * (1 + 0.6 * np.exp(-t * 25))
            tom = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9)
            self.s[name] = fade_edges(np.tanh(tom * 1.5) * 0.8, 0.0005, 0.02)

    def __getitem__(self, name):
        return self.s[name]


# ---------------------------------------------------------------------------
# Mezcla
# ---------------------------------------------------------------------------

class Bus:
    """Pista estereo donde se van sumando notas. 'extra' deja sitio para colas."""

    def __init__(self, seconds, extra=4.0):
        self.length = int(seconds * SR)
        self.buf = np.zeros((2, self.length + int(extra * SR)))

    def add(self, sig, start_sec, gain=1.0, pan=0.0):
        i = int(round(start_sec * SR))
        if i < 0:
            sig, i = sig[-i:], 0
        n = min(len(sig), self.buf.shape[1] - i)
        if n <= 0:
            return
        ang = (pan + 1) * np.pi / 4                         # panorama de potencia constante
        self.buf[0, i:i + n] += sig[:n] * gain * np.cos(ang)
        self.buf[1, i:i + n] += sig[:n] * gain * np.sin(ang)


def reverb_ir(seconds=1.8, seed=3, damp=6000):
    """Respuesta de impulso sintetica (sala) en estereo."""
    rng = np.random.default_rng(seed)
    t = time_axis(seconds)
    env = np.exp(-t * 6.9 / seconds)
    ir = rng.standard_normal((2, len(t))) * env
    ir = lowpass(ir, damp)
    ir[:, : int(0.012 * SR)] *= np.linspace(0, 1, int(0.012 * SR))   # pre-delay suave
    return ir / np.sqrt(np.sum(ir ** 2, axis=1, keepdims=True))


def apply_reverb(buf, ir, mix):
    wet = np.stack([fftconvolve(buf[c], ir[c])[: buf.shape[1]] for c in range(2)])
    return buf + wet * mix


def fold_loop(buf, length):
    """Convierte la mezcla en un loop perfecto: la cola del final se suma al
    principio, asi el salto final -> inicio no se nota."""
    out = buf[:, :length].copy()
    tail = buf[:, length:]
    pos = 0
    while pos < tail.shape[1]:
        n = min(length, tail.shape[1] - pos)
        out[:, :n] += tail[:, pos:pos + n]
        pos += n
    return out


def master(x, target_rms_db=-17.0, ceiling=0.89):
    """Iguala el volumen percibido entre pistas y limita picos sin clips."""
    rms = np.sqrt(np.mean(x ** 2)) + 1e-9
    x = x * (10 ** (target_rms_db / 20) / rms)
    return ceiling * np.tanh(x / ceiling)


def write_wav(path, stereo):
    """Escribe WAV estereo de 16 bits (sin dependencias extra)."""
    import wave
    data = np.clip(stereo.T, -1, 1)
    pcm = (data * 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
