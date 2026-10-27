"""イエスズメの録音から鳴き声 4 種 × 3 バリエーションを切り出して WAV に書き出す。

元の録音は assets/recordings/ にあり、出典とライセンスは assets/sounds/CREDITS.md に書いてある。
先に tools/DecodeRecordings で 44.1kHz・モノラルの WAV に変換しておく。

使い方:
    dotnet run --project tools/DecodeRecordings -- assets/recordings out/recordings
    py tools/cut_recordings.py out/recordings assets/sounds
"""
import sys
import wave
from pathlib import Path

import numpy as np

RATE = 44100
PEAK = 0.8
# 鳴き声より下の帯域(風・車・ハンドリングの雑音)を落とす
HIGHPASS_HZ = 1000

# 出力名: (元の録音, 開始秒, 終了秒, フェード秒)
SEGMENTS = {
    # 「チュン」地鳴き: Joseph Sardin の tschilp call
    "contact_1": ("sardin_tschilp", 0.10, 0.36, 0.01),
    "contact_2": ("sardin_tschilp", 1.33, 1.59, 0.01),
    "contact_3": ("sardin_tschilp", 3.96, 4.22, 0.01),
    # 「チュンチュン」囀り: Benoît Van Hecke のオスのさえずりから 3 音ずつ
    "song_1": ("vanhecke_XC543237", 4.80, 6.20, 0.02),
    "song_2": ("vanhecke_XC543237", 15.53, 16.57, 0.02),
    "song_3": ("vanhecke_XC543237", 25.49, 26.76, 0.02),
    # 「ジュクジュク」警戒・騒ぎ声: Wildlife sound の群れの chattering
    "alarm_1": ("wildlifesound_chattering", 41.55, 42.15, 0.04),
    "alarm_2": ("wildlifesound_chattering", 44.20, 44.80, 0.04),
    "alarm_3": ("wildlifesound_chattering", 15.15, 15.75, 0.04),
    # 「チュリッ」飛び立ち: ngoomie の掛け合いから 2 山の声
    "flight_1": ("ngoomie_202279702", 2.14, 2.58, 0.01),
    "flight_2": ("ngoomie_202279702", 4.75, 5.24, 0.01),
    "flight_3": ("ngoomie_202279702", 26.74, 27.24, 0.01),
}


def load(path):
    with wave.open(str(path), "rb") as w:
        if (w.getnchannels(), w.getsampwidth(), w.getframerate()) != (1, 2, RATE):
            raise ValueError(f"{path.name} は 44.1kHz/16bit/モノラルではありません。先に DecodeRecordings で変換してください")
        return np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32768.0


def highpass(signal, cutoff):
    """FFT で cutoff Hz 未満を落とす。境目は 200Hz かけてなだらかにする"""
    spectrum = np.fft.rfft(signal)
    freqs = np.fft.rfftfreq(len(signal), 1.0 / RATE)
    spectrum *= np.clip((freqs - (cutoff - 200)) / 200, 0.0, 1.0)
    return np.fft.irfft(spectrum, len(signal))


def fade(signal, seconds):
    n = max(1, int(RATE * seconds))
    ramp = np.linspace(0.0, 1.0, n)
    signal = signal.copy()
    signal[:n] *= ramp
    signal[-n:] *= ramp[::-1]
    return signal


def write_wav(path, signal):
    data = (np.clip(signal, -1.0, 1.0) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())


def main(recording_dir, out_dir):
    recording_dir, out = Path(recording_dir), Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    cache = {}
    for name, (source, start, end, fade_seconds) in SEGMENTS.items():
        if source not in cache:
            cache[source] = load(recording_dir / f"{source}.wav")
        # フィルタの端の乱れを避けるため、前後に余裕を持たせて処理してから切る
        margin = int(RATE * 0.05)
        a, b = int(RATE * start), int(RATE * end)
        piece = highpass(cache[source][max(0, a - margin):b + margin], HIGHPASS_HZ)
        piece = piece[a - max(0, a - margin):][:b - a]
        piece = fade(piece - np.mean(piece), fade_seconds)
        piece *= PEAK / (np.max(np.abs(piece)) + 1e-9)
        path = out / f"{name}.wav"
        write_wav(path, piece)
        print(path)


if __name__ == "__main__":
    main(*sys.argv[1:3])
