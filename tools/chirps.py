"""タビスズメの鳴き声 4 種 × 3 バリエーションを合成して WAV に書き出す。

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


def tone(duration, freq_points, harmonics=(1.0, 0.35, 0.12), attack=0.1, release=0.5):
    """周波数を freq_points に沿って動かす、倍音付きの音"""
    n = int(RATE * duration)
    t = np.linspace(0.0, 1.0, n, endpoint=False)
    freq = np.interp(t, np.linspace(0.0, 1.0, len(freq_points)), freq_points)
    phase = 2.0 * np.pi * np.cumsum(freq) / RATE
    signal = sum(amp * np.sin(k * phase) for k, amp in enumerate(harmonics, start=1))
    return signal * envelope(n, attack, release)


def chun(rng, duration, base):
    """「チュン」1 音: 少し上がってから大きく下がる。息っぽいノイズを少し混ぜる"""
    body = tone(duration, [base * 0.85, base * 1.08, base * 0.72])
    breath = bandpass(rng.standard_normal(len(body)), 2500, 7000) * envelope(len(body), 0.05, 0.6)
    return body + 0.15 * breath / (np.max(np.abs(breath)) + 1e-9)


def contact(rng):
    """「チュン」地鳴き"""
    return chun(rng, rng.uniform(0.08, 0.12), rng.uniform(3500, 4500))


def song(rng):
    """「チュンチュン」囀り: 2〜4 音を、音の頭から 0.12〜0.2 秒間隔で"""
    base = rng.uniform(3500, 4500)
    parts = []
    for _ in range(int(rng.integers(2, 5))):
        note = chun(rng, rng.uniform(0.08, 0.11), base * rng.uniform(0.92, 1.08))
        parts.append(note)
        parts.append(np.zeros(max(0, int(RATE * rng.uniform(0.12, 0.2)) - len(note))))
    return np.concatenate(parts[:-1])


def alarm(rng):
    """「ジュクジュク」警戒: 3〜7kHz の雑音とざらついた音を、約 12Hz で断続させる"""
    n = int(RATE * rng.uniform(0.4, 0.6))
    t = np.arange(n) / RATE
    noise = bandpass(rng.standard_normal(n), 3000, 7000)
    noise /= np.max(np.abs(noise)) + 1e-9
    buzz = np.sin(2 * np.pi * rng.uniform(3300, 3800) * t) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 120 * t)))
    pulses = (0.5 + 0.5 * np.sin(2 * np.pi * rng.uniform(11, 14) * t)) ** 3
    return (0.7 * noise + 0.4 * buzz) * pulses * envelope(n, 0.03, 0.15)


def flight(rng):
    """「チュリリ」飛び立ち: 上がる短い音を 15〜20Hz で転がす"""
    out = np.zeros(int(RATE * rng.uniform(0.25, 0.35)))
    rate = rng.uniform(15, 20)
    period = int(RATE / rate)
    element = tone(0.7 / rate, [3000, 5000], harmonics=(1.0, 0.25), attack=0.15, release=0.4)
    for start in range(0, len(out) - len(element), period):
        out[start:start + len(element)] += element * rng.uniform(0.8, 1.0)
    return out * envelope(len(out), 0.05, 0.3)


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


KINDS = {"contact": contact, "song": song, "alarm": alarm, "flight": flight, "dust": dust}


def main(out_dir):
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    for kind_index, (name, make) in enumerate(KINDS.items()):
        for variant in range(1, VARIANTS + 1):
            # 種類と番号から決まるシードで、何度実行しても同じ音になる
            rng = np.random.default_rng(1000 * kind_index + variant)
            path = out / f"{name}_{variant}.wav"
            write_wav(path, normalize(make(rng), PEAK_BY_KIND.get(name, PEAK)))
            print(path)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "assets/sounds")
