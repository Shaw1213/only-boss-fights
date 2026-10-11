"""
Synthesizes the sound effects the free packs don't cover (0 credits, no downloads): sword whooshes, the parry
ring, the telegraph cues, fire, low booms, the charge and relic swells, and the win / lose stings.

Everything is built from noise, filtered noise and inharmonic partials (metal rings like a struck bar, not like a
sine), with a small Schroeder reverb so the hits sit in the stone hall. Plain numpy, no scipy. Deterministic (fixed
seeds), so re-running reproduces the files.

Usage:
  python -I Tools/synth_sfx.py [--out Assets/Audio/SFX/Synth]
"""
import argparse
import os
import wave

import numpy as np

SR = 44100


# ------------------------------------------------------------------ building blocks

def t_axis(seconds):
    return np.arange(int(seconds * SR)) / SR


def env(seconds, attack, decay, hold=0.0):
    """Linear attack, optional hold, exponential decay (decay = time constant in seconds)."""
    t = t_axis(seconds)
    e = np.where(t < attack, t / max(attack, 1e-6), np.exp(-(t - attack - hold).clip(0) / decay))
    return e


def svf(x, cutoff, q=0.7, mode="bp"):
    """State-variable filter with a per-sample cutoff (Hz); mode lp / bp / hp."""
    cutoff = np.broadcast_to(np.asarray(cutoff, dtype=np.float64), x.shape)
    f = 2.0 * np.sin(np.pi * np.clip(cutoff, 20, SR * 0.24) / SR)
    damp = 1.0 / q
    low = band = 0.0
    out = np.empty_like(x)
    for i in range(len(x)):
        high = x[i] - low - damp * band
        band += f[i] * high
        low += f[i] * band
        out[i] = low if mode == "lp" else band if mode == "bp" else high
    return out


def fade_out(y, frac=0.15):
    """Brings the end to zero: a sound cut off mid-ring clicks (a broadband line in the spectrogram)."""
    n = max(1, int(len(y) * frac))
    y = y.copy()
    y[-n:] *= np.cos(np.linspace(0, np.pi / 2, n)) ** 2
    return y


def bell(f0, seconds, ratios=(1.0, 2.76, 5.40, 8.93), amps=(1.0, 0.6, 0.35, 0.2), decays=(0.9, 0.5, 0.3, 0.18)):
    """A struck metal bar: inharmonic partials, the high ones dying first."""
    t = t_axis(seconds)
    y = np.zeros_like(t)
    for r, a, d in zip(ratios, amps, decays):
        y += a * np.sin(2 * np.pi * f0 * r * t) * np.exp(-t / (d * seconds))
    return fade_out(y)


def whoosh(seconds, f_start, f_end, q, rng, attack_frac=0.35, bright=0.0):
    """Air moved by a blade: noise through two band-pass stages (a 2-pole band alone leaves a broad hiss) sweeping
    up then back down, swelling then fading, plus a faint resonant whistle that follows the sweep."""
    n = int(seconds * SR)
    t = np.linspace(0, 1, n)
    sweep = np.sin(np.pi * np.clip(t / (attack_frac * 1.6), 0, 1))  # up to the peak at the swing's fastest point, then back
    cutoff = f_start * (f_end / f_start) ** sweep
    shape = np.where(t < attack_frac, (t / attack_frac) ** 2, np.exp(-(t - attack_frac) / (0.28 * (1 - attack_frac))))
    noise = rng.standard_normal(n)
    y = svf(svf(noise, cutoff, q, "bp"), cutoff, q, "bp")
    y = y / max(np.max(np.abs(y)), 1e-9) * shape
    y += 0.25 * (lambda w: w / max(np.max(np.abs(w)), 1e-9))(svf(noise, cutoff * 0.8, 6.0, "bp")) * shape
    if bright:
        z = svf(svf(rng.standard_normal(n), cutoff * 2.0, 2.0, "bp"), cutoff * 2.0, 2.0, "bp")
        y += bright * z / max(np.max(np.abs(z)), 1e-9) * shape ** 2
    return fade_out(svf(y, 6000.0, 0.7, "lp"), 0.1)


def boom(seconds, f_hi, f_lo, rng, noise=0.4):
    """A low impact: a falling sine thump plus low-passed noise rumble."""
    t = t_axis(seconds)
    f = f_lo + (f_hi - f_lo) * np.exp(-t / 0.08)
    phase = 2 * np.pi * np.cumsum(f) / SR
    y = np.sin(phase) * np.exp(-t / (0.35 * seconds))
    y += noise * svf(rng.standard_normal(len(t)), 180.0, 0.7, "lp") * np.exp(-t / (0.25 * seconds))
    return fade_out(y)


def reverb(x, mix=0.25, room=0.82, tail=0.9):
    """A small Schroeder reverb: 4 parallel combs into 2 allpasses; adds `tail` seconds."""
    x = np.concatenate([fade_out(x, 0.05), np.zeros(int(tail * SR))])
    combs = [1557, 1617, 1491, 1422]
    wet = np.zeros_like(x)
    for d in combs:
        buf = np.zeros_like(x)
        for i in range(len(x)):
            buf[i] = x[i] + (room * buf[i - d] if i >= d else 0.0)
        wet += buf
    wet /= len(combs)
    for d, g in [(225, 0.5), (556, 0.5)]:
        out = np.zeros_like(wet)
        for i in range(len(wet)):
            delayed = out[i - d] if i >= d else 0.0
            src = wet[i - d] if i >= d else 0.0
            out[i] = -g * wet[i] + src + g * delayed
        wet = out
    return (1 - mix) * x + mix * wet


def finish(y, peak=0.85, fade=0.02):
    y = y - np.mean(y)
    # Trim trailing near-silence (reverb tails) to keep files small, then fade so the cut can't click.
    loud = np.where(np.abs(y) > 1e-3 * np.max(np.abs(y)))[0]
    if len(loud):
        y = y[: loud[-1] + 1].copy()
    n = min(int(fade * SR), len(y) // 2)
    if n:
        y[-n:] *= np.cos(np.linspace(0, np.pi / 2, n)) ** 2
    return y / max(np.max(np.abs(y)), 1e-9) * peak


def write(path, y):
    data = (np.clip(y, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


# ------------------------------------------------------------------ the sounds

def sounds():
    out = {}
    for i in range(3):  # the hero's light slashes: quick and bright
        rng = np.random.default_rng(10 + i)
        out[f"swing_light_{i}"] = reverb(whoosh(0.2 + 0.02 * i, 900 + 150 * i, 3800, 1.6, rng, 0.4, 0.35), 0.12)
    for i in range(2):  # the charged heavy / riposte: longer, lower
        rng = np.random.default_rng(20 + i)
        out[f"swing_heavy_{i}"] = reverb(whoosh(0.36, 380 + 60 * i, 2400, 1.3, rng, 0.45, 0.25), 0.15)
    for i in range(2):  # the knight's greatsword: slow and heavy
        rng = np.random.default_rng(30 + i)
        out[f"boss_swing_{i}"] = reverb(whoosh(0.5, 160 + 30 * i, 1300, 1.1, rng, 0.55, 0.1), 0.18)

    # Parry: must sound unlike anything else. A bright struck-steel ring over a sharp click, with a long shimmer.
    rng = np.random.default_rng(40)
    t = t_axis(1.2)
    ring = bell(1180, 1.2) + 0.5 * bell(1180 * 1.5, 1.2, amps=(0.6, 0.3, 0.2, 0.1))
    click = svf(rng.standard_normal(len(t)), 6000, 0.8, "hp") * np.exp(-t / 0.006)
    # The ring carries the level (a dominant click would set the peak and leave the ring quiet); soft saturation thickens it.
    out["parry_ring"] = reverb(np.tanh(2.2 * (ring / np.max(np.abs(ring)) + 0.3 * click)) / np.tanh(2.2), 0.3, 0.85, 1.0)
    # Clash: two blades meeting, lower and grittier than the parry.
    rng = np.random.default_rng(41)
    t = t_axis(0.9)
    ring = bell(640, 0.9, decays=(0.6, 0.35, 0.2, 0.12))
    grit = svf(rng.standard_normal(len(t)), 2500, 1.2, "bp") * np.exp(-t / 0.05)
    out["clash_ring"] = reverb(np.tanh(2.0 * (ring / np.max(np.abs(ring)) + 0.5 * grit)) / np.tanh(2.0), 0.25)
    # Flash counter: a rising shimmer through the air, then the cut.
    rng = np.random.default_rng(42)
    t = t_axis(0.45)
    glide = np.sin(2 * np.pi * np.cumsum(500 + 2600 * (t / t[-1]) ** 2) / SR) * np.sin(np.pi * t / t[-1]) ** 2
    out["flash_counter"] = reverb(0.35 * glide + whoosh(0.45, 600, 5000, 1.5, rng, 0.7, 0.4), 0.3, 0.85, 0.8)

    # Telegraph cues: a clean high glint for "parry this", a low snarl for "dash, don't parry".
    t = t_axis(0.5)
    out["cue_glint"] = reverb(bell(2350, 0.5, decays=(0.5, 0.3, 0.2, 0.1)) * 0.8, 0.3, 0.85, 0.6)
    rng = np.random.default_rng(50)
    t = t_axis(0.45)
    saw = 2 * ((110 * t) % 1) - 1 + 2 * ((116.5 * t) % 1) - 1
    snarl = svf(saw, 900, 1.5, "lp") * (0.6 + 0.4 * np.sin(2 * np.pi * 18 * t)) * env(0.45, 0.03, 0.15, 0.1)
    out["cue_danger"] = reverb(snarl + 0.3 * svf(rng.standard_normal(len(t)), 300, 0.7, "lp") * env(0.45, 0.02, 0.12), 0.2)

    # Low impacts: the slam, the phase change, the kill, the name card.
    out["boom_slam"] = reverb(boom(0.9, 140, 38, np.random.default_rng(60), 0.6), 0.25, 0.86, 1.0)
    out["boom_deep"] = reverb(boom(1.6, 90, 28, np.random.default_rng(61), 0.5), 0.35, 0.9, 1.4)

    # Fire: rushing low noise with crackles (the Flame Wave), and a burn tick.
    rng = np.random.default_rng(70)
    n = int(0.9 * SR)
    t = np.linspace(0, 1, n)
    rush = svf(rng.standard_normal(n), 700 + 900 * t, 0.9, "lp") * np.sin(np.pi * np.clip(t * 1.4, 0, 1)) ** 0.5 * np.exp(-t * 1.6)
    crackle = np.zeros(n)
    for p in rng.integers(0, n - 400, 40):
        crackle[p:p + 300] += rng.uniform(0.3, 1.0) * svf(rng.standard_normal(300), 3000, 1.0, "bp") * np.exp(-np.arange(300) / 40)
    out["fire_wave"] = reverb(rush + 0.5 * crackle * np.exp(-t * 1.2), 0.15)
    rng = np.random.default_rng(71)
    t = t_axis(0.35)
    out["fire_tick"] = svf(rng.standard_normal(len(t)), 1800, 0.9, "lp") * env(0.35, 0.005, 0.08)

    # Harvest: three quick bell notes going up (the blade drinks the spell); release: a whoosh with a tone in it.
    notes = [(784, 0.0), (1047, 0.07), (1568, 0.14)]
    y = np.zeros(int(0.9 * SR))
    for f, start in notes:
        b = bell(f, 0.7, decays=(0.7, 0.4, 0.25, 0.15)) * 0.5
        s = int(start * SR)
        y[s:s + len(b)] += b[: len(y) - s]
    out["harvest"] = reverb(y, 0.3, 0.85, 0.8)
    rng = np.random.default_rng(80)
    t = t_axis(0.5)
    tone = np.sin(2 * np.pi * np.cumsum(300 + 500 * t / t[-1]) / SR) * env(0.5, 0.05, 0.15, 0.1)
    out["wave_release"] = reverb(whoosh(0.5, 300, 3000, 1.2, rng, 0.3, 0.3) + 0.4 * tone, 0.2)

    # Charge: a rising hum while the heavy charges; dash: a short air push.
    t = t_axis(0.6)
    f = 180 + 260 * (t / t[-1])
    hum = (np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.4 * np.sin(4 * np.pi * np.cumsum(f) / SR)) * (t / t[-1]) ** 0.7
    out["charge"] = hum * env(0.6, 0.0, 0.5, 0.45)
    out["dash"] = reverb(whoosh(0.22, 500, 1800, 0.9, np.random.default_rng(90), 0.25), 0.1)

    # Lobby: the relic charging (a rising shimmer, ~0.6 s like its charge) and the step into the fight.
    rng = np.random.default_rng(100)
    t = t_axis(0.7)
    shimmer = sum(np.sin(2 * np.pi * np.cumsum(f0 * (1 + 0.5 * t / t[-1])) / SR) for f0 in (523, 659, 784))
    out["relic_charge"] = reverb(shimmer * (t / t[-1]) ** 1.5 * 0.4 + 0.2 * whoosh(0.7, 400, 4000, 1.0, rng, 0.9), 0.3, 0.86, 0.6)
    y = boom(1.2, 120, 40, np.random.default_rng(101), 0.3) * 0.7
    b = bell(392, 1.6, decays=(0.9, 0.6, 0.35, 0.2)) * 0.5
    y = np.concatenate([y, np.zeros(len(b) - len(y))]) + b
    out["relic_enter"] = reverb(y, 0.35, 0.9, 1.4)

    # Stings: victory rises in major, defeat sinks in minor.
    def chord(freqs, gap, seconds):
        y = np.zeros(int((seconds + gap * len(freqs)) * SR))
        for k, f in enumerate(freqs):
            b = bell(f, seconds, ratios=(1.0, 2.0, 3.01, 4.2), amps=(1.0, 0.35, 0.2, 0.08), decays=(0.9, 0.6, 0.4, 0.25)) * 0.35
            s = int(k * gap * SR)
            y[s:s + len(b)] += b
        return y
    out["sting_victory"] = reverb(chord([392, 494, 587, 784, 988], 0.11, 2.2), 0.35, 0.9, 1.5)
    out["sting_defeat"] = reverb(chord([294, 233, 175, 147], 0.18, 2.4), 0.35, 0.9, 1.5)
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", default="Assets/Audio/SFX/Synth")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    for name, y in sounds().items():
        y = finish(y)
        write(os.path.join(args.out, name + ".wav"), y)
        print(f"{name}.wav  {len(y) / SR:.2f} s")


if __name__ == "__main__":
    main()
