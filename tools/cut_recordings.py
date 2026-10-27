"""イエスズメの録音から鳴き声 4 種 × 3 バリエーションを切り出して WAV に書き出す。

元の録音は assets/recordings/ にあり、出典とライセンスは assets/sounds/CREDITS.md に書いてある。
先に tools/DecodeRecordings で 44.1kHz・モノラルの WAV に変換しておく。

使い方:
    dotnet run --project tools/DecodeRecordings -- assets/recordings out/recordings
    dotnet run --project tools/DecodeRecordings -- assets/recordings/montanus out/recordings_montanus
    py tools/cut_recordings.py out/recordings out/recordings_montanus assets/sounds
"""
import sys
import wave
from pathlib import Path

import numpy as np

RATE = 44100
PEAK = 0.8
# 鳴き声より下の帯域(風・車・ハンドリングの雑音)を落とす
HIGHPASS_HZ = 1000

# 出力名: (元の録音, 開始秒, 終了秒, フェード秒[, 加工])
# 鳴き声を澄ませる加工 (チュン・さえずり・飛び立ちに使う)
CLEAR = {"pitch": 1.12, "denoise": True, "tonal": True}

# 加工: pitch = 再生速度の倍率 (高くなり短くなる)、denoise = 背景雑音を差し引く、
#       tonal = 主な音の筋 (基本音と 2 倍音) だけを残して濁りを弱める
SEGMENTS = {
    # 「チュン」地鳴き: Benoît Van Hecke のさえずりから、高く澄んだ単発の声を選び、少し高くして雑音を除く
    "contact_1": ("vanhecke_XC543237", 4.81, 5.04, 0.01, CLEAR),
    "contact_2": ("vanhecke_XC543237", 11.98, 12.22, 0.01, CLEAR),
    "contact_3": ("vanhecke_XC543237", 23.93, 24.13, 0.01, CLEAR),
    # 「チュン」地鳴き (日本のスズメ): iNaturalist の山梨の録音 (CC0)。本物らしさを残すため雑音の除去だけかける
    "contact_4": ("inat_1913285", 2.52, 2.76, 0.01, {"denoise": True}),
    "contact_5": ("inat_1913285", 4.17, 4.41, 0.01, {"denoise": True}),
    "contact_6": ("inat_1913285", 5.78, 6.02, 0.01, {"denoise": True}),
    # 「チュンチュン」囀り: Benoît Van Hecke のオスのさえずりから 3 音ずつ、澄ませる
    "song_1": ("vanhecke_XC543237", 4.80, 6.20, 0.02, CLEAR),
    "song_2": ("vanhecke_XC543237", 15.53, 16.57, 0.02, CLEAR),
    "song_3": ("vanhecke_XC543237", 25.49, 26.76, 0.02, CLEAR),
    # 「チュンチュン」囀り (日本のスズメ): Derek Paulo の録音から。雑音の除去だけかける
    "song_4": ("commons_XC347807", 3.89, 5.31, 0.02, {"denoise": True}),
    "song_5": ("commons_XC347807", 7.09, 8.57, 0.02, {"denoise": True}),
    "song_6": ("commons_XC347807", 16.30, 17.67, 0.02, {"denoise": True}),
    "song_7": ("commons_XC347806", 1.88, 2.91, 0.02, {"denoise": True}),
    "song_8": ("commons_XC347806", 8.08, 8.81, 0.02, {"denoise": True}),
    "song_9": ("commons_XC347806", 13.19, 14.12, 0.02, {"denoise": True}),
    # 「ジュクジュク」警戒・騒ぎ声: Wildlife sound の群れの chattering
    "alarm_1": ("wildlifesound_chattering", 41.55, 42.15, 0.04),
    "alarm_2": ("wildlifesound_chattering", 44.20, 44.80, 0.04),
    "alarm_3": ("wildlifesound_chattering", 15.15, 15.75, 0.04),
    # 「チュリッ」飛び立ち: ngoomie の掛け合いから 2 山の声、澄ませる
    "flight_1": ("ngoomie_202279702", 2.14, 2.58, 0.01, CLEAR),
    "flight_2": ("ngoomie_202279702", 4.75, 5.24, 0.01, CLEAR),
    "flight_3": ("ngoomie_202279702", 26.74, 27.24, 0.01, CLEAR),
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


def denoise(signal, context, frame=512, strength=1.5):
    """スペクトル減算: context (前後を含む長めの区間) から周波数ごとの背景雑音を推定し、signal から差し引く"""
    window = np.hanning(frame)
    hop = frame // 4

    def stft(x):
        return np.array([np.fft.rfft(x[i:i + frame] * window) for i in range(0, len(x) - frame, hop)])

    # 鳴いていない時間が多いので、各周波数の小さい方 20% を雑音とみなす
    noise = np.percentile(np.abs(stft(context)), 20, axis=0)
    spec = stft(np.concatenate([signal, np.zeros(frame)]))
    magnitude = np.maximum(np.abs(spec) - strength * noise, 0.0)
    cleaned = magnitude * np.exp(1j * np.angle(spec))
    out = np.zeros(len(signal) + frame)
    norm = np.zeros(len(signal) + frame)
    for k, i in enumerate(range(0, len(out) - frame, hop)):
        out[i:i + frame] += np.fft.irfft(cleaned[k], frame) * window
        norm[i:i + frame] += window ** 2
    return (out / np.maximum(norm, 1e-6))[:len(signal)]


def tonal(signal, frame=512, keep_hz=250, residual=0.15):
    """各瞬間の一番強い音 (2〜8kHz) とその 2 倍音の近くだけを残し、それ以外を residual 倍に弱める"""
    window = np.hanning(frame)
    hop = frame // 4
    freqs = np.fft.rfftfreq(frame, 1.0 / RATE)
    band = (freqs >= 2000) & (freqs <= 8000)
    padded = np.concatenate([signal, np.zeros(frame)])
    out = np.zeros(len(padded))
    norm = np.zeros(len(padded))
    for i in range(0, len(padded) - frame, hop):
        spec = np.fft.rfft(padded[i:i + frame] * window)
        f0 = freqs[band][np.argmax(np.abs(spec[band]))]
        near = (np.abs(freqs - f0) <= keep_hz) | (np.abs(freqs - 2 * f0) <= keep_hz)
        spec *= np.where(near, 1.0, residual)
        out[i:i + frame] += np.fft.irfft(spec, frame) * window
        norm[i:i + frame] += window ** 2
    return (out / np.maximum(norm, 1e-6))[:len(signal)]


def pitch_up(signal, factor):
    """再生速度を factor 倍にする (周波数が factor 倍、長さが 1/factor)"""
    positions = np.arange(0, len(signal) - 1, factor)
    return np.interp(positions, np.arange(len(signal)), signal)


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


def main(*args):
    # 録音のフォルダは複数渡せる (イエスズメ、日本のスズメ)。最後の引数が出力先
    *recording_dirs, out_dir = [Path(a) for a in args]
    out = out_dir
    out.mkdir(parents=True, exist_ok=True)
    cache = {}
    for name, (source, start, end, fade_seconds, *options) in SEGMENTS.items():
        options = options[0] if options else {}
        if source not in cache:
            path = next((d / f"{source}.wav" for d in recording_dirs if (d / f"{source}.wav").exists()), None)
            if path is None:
                raise FileNotFoundError(f"{source}.wav が見つかりません: {', '.join(map(str, recording_dirs))}")
            cache[source] = load(path)
        recording = cache[source]
        # フィルタの端の乱れを避けるため、前後に余裕を持たせて処理してから切る
        margin = int(RATE * 0.05)
        a, b = int(RATE * start), int(RATE * end)
        piece = highpass(recording[max(0, a - margin):b + margin], HIGHPASS_HZ)
        piece = piece[a - max(0, a - margin):][:b - a]
        if options.get("denoise"):
            context = highpass(recording[max(0, a - RATE * 2):b + RATE * 2], HIGHPASS_HZ)
            piece = denoise(piece, context)
        if options.get("tonal"):
            piece = tonal(piece)
        if "pitch" in options:
            piece = pitch_up(piece, options["pitch"])
        piece = fade(piece - np.mean(piece), fade_seconds)
        piece *= PEAK / (np.max(np.abs(piece)) + 1e-9)
        path = out / f"{name}.wav"
        write_wav(path, piece)
        print(path)


if __name__ == "__main__":
    main(*sys.argv[1:])
