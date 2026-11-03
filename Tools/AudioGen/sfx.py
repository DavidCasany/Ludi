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


def write_mono(path, x):
    import wave
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def main():
    p = argparse.ArgumentParser(description="Genera los efectos de interfaz en .wav")
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
