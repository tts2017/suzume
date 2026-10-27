"""合成した鳴き声 WAV を検証し、スペクトログラム PNG を書き出す。

使い方:
    py tools/check_chirps.py assets/sounds [PNG の出力先]
問題があれば終了コード 1 を返す。
"""
import struct
import sys
import wave
import zlib
from pathlib import Path

import numpy as np

RATE = 44100
EXPECTED_VARIANTS = 3
# 種類ごとの (最短秒, 最長秒, ピーク周波数の下限 Hz, 上限 Hz)
# 鳴き声 4 種はイエスズメの録音から切り出したもの (tools/cut_recordings.py)
LIMITS = {
    "contact": (0.12, 0.3, 4000, 6500),
    "song": (0.7, 1.6, 2500, 6500),
    "alarm": (0.5, 0.75, 2000, 8000),
    "flight": (0.35, 0.65, 2500, 7000),
    # 砂浴びの羽音: 低めの「バサバサ」と砂のこすれ
    "dust": (0.45, 1.0, 200, 6000),
}

# 振幅のピークの範囲。羽音は鳴き声より控えめにする
AMPLITUDE = {"dust": (0.3, 0.5)}


def check(path, problems):
    kind = path.stem.rsplit("_", 1)[0]
    min_sec, max_sec, min_hz, max_hz = LIMITS[kind]
    with wave.open(str(path), "rb") as w:
        fmt = (w.getnchannels(), w.getsampwidth(), w.getframerate())
        samples = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32768.0

    duration = len(samples) / RATE
    spectrum = np.abs(np.fft.rfft(samples))
    peak_hz = np.fft.rfftfreq(len(samples), 1.0 / RATE)[np.argmax(spectrum)]
    peak_amp = np.max(np.abs(samples))
    print(f"{path.name}: {duration:.3f}s peak={peak_hz:.0f}Hz amp={peak_amp:.2f}")

    if fmt != (1, 2, RATE):
        problems.append(f"{path.name}: 形式が 44.1kHz/16bit/モノラルでない {fmt}")
    if not min_sec <= duration <= max_sec:
        problems.append(f"{path.name}: 長さ {duration:.3f}s が {min_sec}〜{max_sec}s の範囲外")
    if not min_hz <= peak_hz <= max_hz:
        problems.append(f"{path.name}: ピーク {peak_hz:.0f}Hz が {min_hz}〜{max_hz}Hz の範囲外")
    min_amp, max_amp = AMPLITUDE.get(kind, (0.5, 0.9))
    if not min_amp <= peak_amp <= max_amp:
        problems.append(f"{path.name}: 振幅 {peak_amp:.2f} が {min_amp}〜{max_amp} の範囲外")
    return samples


def spectrogram_png(samples, path, frame=512, hop=128):
    """上が高音のグレースケール スペクトログラムを PNG で書く(numpy と標準ライブラリだけ)"""
    window = np.hanning(frame)
    frames = [samples[i:i + frame] * window for i in range(0, max(1, len(samples) - frame), hop)]
    magnitude = np.abs(np.fft.rfft(np.array(frames), axis=1)).T
    db = 20.0 * np.log10(magnitude + 1e-6)
    image = np.clip((db - (db.max() - 60.0)) / 60.0, 0.0, 1.0)
    image = np.repeat((255 * image[::-1]).astype(np.uint8), 4, axis=1)
    height, width = image.shape
    raw = b"".join(b"\x00" + image[y].tobytes() for y in range(height))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    path.write_bytes(
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 0, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw))
        + chunk(b"IEND", b""))


def main(sound_dir, png_dir=None):
    sound_dir = Path(sound_dir)
    problems = []
    for kind in LIMITS:
        files = sorted(sound_dir.glob(f"{kind}_*.wav"))
        if len(files) < EXPECTED_VARIANTS:
            problems.append(f"{kind}: {len(files)} 個 (最低 {EXPECTED_VARIANTS} 個)")
        for path in files:
            samples = check(path, problems)
            if png_dir:
                Path(png_dir).mkdir(parents=True, exist_ok=True)
                spectrogram_png(samples, Path(png_dir) / f"{path.stem}.png")

    for problem in problems:
        print("NG:", problem)
    print("OK" if not problems else f"{len(problems)} 件の問題")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(*sys.argv[1:3]))
