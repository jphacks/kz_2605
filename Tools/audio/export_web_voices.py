"""VR のナレーション音声（WAV）を、視聴ページ用の MP3（モノラル 64kbps）に変換する。

    python Tools/audio/export_web_voices.py          # 新しい・更新された音声だけ変換
    python Tools/audio/export_web_voices.py --force  # すべて変換し直す

入力：Assets/_Project/Audio/Voice/Resources/Narration/{id}.wav
出力：Web/public/voice/{id}.mp3（視聴ページが行の id で再生する）。元の WAV が無くなった MP3 は消す。
ffmpeg が必要（PATH に通しておく）。
"""
import argparse
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "Assets/_Project/Audio/Voice/Resources/Narration"
OUT = ROOT / "Web/public/voice"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--force", action="store_true")
    args = ap.parse_args()
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg is None:
        sys.exit("ffmpeg が見つかりません")
    OUT.mkdir(parents=True, exist_ok=True)
    made = 0
    ids = set()
    for wav in sorted(SRC.glob("*.wav")):
        ids.add(wav.stem)
        mp3 = OUT / (wav.stem + ".mp3")
        if mp3.exists() and not args.force and mp3.stat().st_mtime >= wav.stat().st_mtime:
            continue
        subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-i", str(wav), "-ac", "1", "-ar", "24000",
                        "-codec:a", "libmp3lame", "-b:a", "64k", str(mp3)], check=True)
        made += 1
    removed = 0
    for mp3 in OUT.glob("*.mp3"):
        if mp3.stem not in ids:
            mp3.unlink(); removed += 1
    total = sum(p.stat().st_size for p in OUT.glob("*.mp3"))
    print(f"{made} 件を変換、{removed} 件を削除 → {OUT.relative_to(ROOT)}（{len(ids)} 件、合計 {total / 1024:.0f} KB）")


if __name__ == "__main__":
    main()
