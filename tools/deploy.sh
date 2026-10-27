#!/bin/sh
# プラグインをビルドし、Dalamud の開発プラグイン用フォルダへ配置する。
# ソリューション (Suzume.slnx) でビルドすると出力先が bin/x64/Debug になり、
# bin/Debug に古い DLL が残るため、配置は必ずこのスクリプトでプロジェクト単体をビルドして行う。
set -e
cd "$(dirname "$0")/.."
dotnet build src/Suzume/Suzume.csproj -c Debug
mkdir -p /c/ff14_suzume
cp -r src/Suzume/bin/Debug/. /c/ff14_suzume/
PYTHONIOENCODING=utf-8 py -c "print('埋め込み音声', open('C:/ff14_suzume/Suzume.dll', 'rb').read().count(b'Suzume.Sounds.'), '個')"
