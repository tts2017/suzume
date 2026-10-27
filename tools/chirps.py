"""砂浴びの羽音 (dust) 3 バリエーションを合成して WAV に書き出す。

鳴き声 4 種は録音から切り出す (tools/cut_recordings.py)。ここでは羽音だけを作る。
numpy だけを使う。使い方:
    py tools/chirps.py assets/sounds
"""
import sys
import wave
from pathlib import Path

import numpy as np

RATE = 44100
VARIANTS = 3
PEAK = 0.8
# 種類ごとの振幅のピーク。羽音は鳴き声より控えめにする
PEAK_BY_KIND = {"dust": 0.45}


def envelope(n, attack, release):
    """立ち上がり attack、減衰 release (どちらも全体に対する割合) の台形エンベロープ"""
    t = np.linspace(0.0, 1.0, n, endpoint=False)
    return np.clip(t / attack, 0.0, 1.0) * np.clip((1.0 - t) / release, 0.0, 1.0)


def bandpass(signal, low, high):
    """FFT で low〜high Hz 以外を落とす簡易バンドパス"""
    spectrum = np.fft.rfft(signal)
    freqs = np.fft.rfftfreq(len(signal), 1.0 / RATE)
    spectrum[(freqs < low) | (freqs > high)] = 0.0
    return np.fft.irfft(spectrum, len(signal))


def dust(rng):
    """「バサバサ」砂浴びの羽音: 約 20Hz の羽ばたきで低めの風切り音を断続させ、砂のこすれを混ぜる"""
    n = int(RATE * rng.uniform(0.5, 0.9))
    t = np.arange(n) / RATE
    wing = bandpass(rng.standard_normal(n), 250, 2500)
    wing /= np.max(np.abs(wing)) + 1e-9
    beats = (0.5 + 0.5 * np.sin(2 * np.pi * rng.uniform(16, 22) * t)) ** 2
    sand = bandpass(rng.standard_normal(n), 3000, 8000)
    sand /= np.max(np.abs(sand)) + 1e-9
    # 砂粒: まばらな粒に短い減衰を付ける
    grains = np.convolve((rng.random(n) < 0.01).astype(float), np.exp(-np.arange(200) / 40.0), mode="same")
    return (0.8 * wing * beats + 0.35 * sand * np.clip(grains, 0.0, 1.0)) * envelope(n, 0.08, 0.3)


def normalize(signal, peak=PEAK):
    signal = signal - np.mean(signal)
    return signal * (peak / (np.max(np.abs(signal)) + 1e-9))


def write_wav(path, signal):
    data = (np.clip(signal, -1.0, 1.0) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())


KINDS = {"dust": dust}


def main(out_dir):
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    for name, make in KINDS.items():
        for variant in range(1, VARIANTS + 1):
            # 番号から決まるシードで、何度実行しても同じ音になる(以前の 5 種合成時と同じシード)
            rng = np.random.default_rng(4000 + variant)
            path = out / f"{name}_{variant}.wav"
            write_wav(path, normalize(make(rng), PEAK_BY_KIND.get(name, PEAK)))
            print(path)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "assets/sounds")
