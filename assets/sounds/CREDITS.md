# 音声のクレジット

鳴き声 4 種 (`contact` `song` `alarm` `flight`) は、Wikimedia Commons で公開されているイエスズメ (*Passer domesticus*) の録音から切り出し、低音の雑音を落として音量をそろえたものです。`contact`・`song`・`flight` はさらに背景雑音を差し引き、主な音の筋以外の濁りを弱め、再生速度を 1.12 倍にして少し高くしています。元の録音は `assets/recordings/` にあります。切り出し方は `tools/cut_recordings.py` を参照してください。

砂浴びの羽音 (`dust`) は `tools/chirps.py` で合成したものです。

| 使っているファイル | 元の録音 | 作者 | ライセンス |
|---|---|---|---|
| `flight_1〜3.wav` | [House Sparrow 202279702.wav](https://commons.wikimedia.org/wiki/File:House_Sparrow_202279702.wav) | ngoomie | CC0 1.0 |
| `song_1〜3.wav`、`contact_1〜3.wav` | [Passer domesticus - House Sparrow XC543237.mp3](https://commons.wikimedia.org/wiki/File:Passer_domesticus_-_House_Sparrow_XC543237.mp3) (xeno-canto XC543237) | Benoît Van Hecke | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/) |
| `alarm_1〜3.wav` | [House sparrows 070407 chattering.ogg](https://commons.wikimedia.org/wiki/File:House_sparrows_070407_chattering.ogg) | Wildlife sound (Wikimedia Commons) | [CC BY-SA 2.0 UK](https://creativecommons.org/licenses/by-sa/2.0/uk/) |
| (未使用・予備) | [Joseph Sardin - Passer domesticus tschilp call.oga](https://commons.wikimedia.org/wiki/File:Joseph_Sardin_-_Passer_domesticus_tschilp_call.oga) | Joseph Sardin | CC0 1.0 |
| (未使用・予備) | [Passer domesticus.ogg](https://commons.wikimedia.org/wiki/File:Passer_domesticus.ogg) | Oona Räisänen | パブリックドメイン |

`song_*.wav`・`contact_*.wav`・`alarm_*.wav` は CC BY-SA の録音を加工したものなので、同じ CC BY-SA ライセンスに従います (作者の表記と、同じライセンスでの共有が必要です)。
