"""ビルドしたプラグインのマニフェストから、Dalamud のカスタムプラグインリポジトリ用の repo.json を作る。

使い方 (Release ビルドのあと。タグは GitHub のリリースのタグ):
    py tools/make_repo_json.py src/Suzume/bin/Release/Suzume/Suzume.json v0.1.0 repo.json
"""
import json
import sys
import time
from pathlib import Path

REPO = "https://github.com/tts2017/suzume"


def main():
    manifest_path, tag, out_path = sys.argv[1:4]
    manifest = json.loads(Path(manifest_path).read_text(encoding="utf-8-sig"))
    # リリースに添付した latest.zip を、インストール・更新・テスト版のすべてに使う
    download = f"{REPO}/releases/download/{tag}/latest.zip"
    manifest.update({
        "DownloadLinkInstall": download,
        "DownloadLinkUpdate": download,
        "DownloadLinkTesting": download,
        "LastUpdate": int(time.time()),
    })
    text = json.dumps([manifest], ensure_ascii=False, indent=2) + "\n"
    Path(out_path).write_bytes(text.encode("utf-8"))


if __name__ == "__main__":
    main()
