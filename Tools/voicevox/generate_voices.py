"""
StoryScript.cs の台詞のうち、音声ファイル（Assets/_Project/Audio/Voice/Resources/Narration/{id}.wav）が
まだ無いものを VOICEVOX で生成する。

使い方（リポジトリのルートで。VOICEVOX を起動しておく）:
    python Tools/voicevox/generate_voices.py            # 無いものだけ作る
    python Tools/voicevox/generate_voices.py --force    # すべて作り直す
    python Tools/voicevox/generate_voices.py --only beach_03 ed1_01

話者（StoryScript.cs の冒頭のコメントと合わせる）:
    ナレーション：栗田まろん／浦島太郎：ずんだもん／亀：春日部つむぎ／乙姫：四国めたん
    子ども：小夜/SAYO／村人：ちび式じい
"""
import argparse
import json
import re
import sys
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "Assets/_Project/Scripts/Story/StoryScript.cs"
OUT_DIR = ROOT / "Assets/_Project/Audio/Voice/Resources/Narration"
ENGINE = "http://127.0.0.1:50021"

# StoryScript.cs の話者の定数名 → (VOICEVOX のキャラクター名, スタイル名)
SPEAKERS = {
    "N": ("栗田まろん", "ノーマル"),
    "Taro": ("ずんだもん", "ノーマル"),
    "Kame": ("春日部つむぎ", "ノーマル"),
    "Oto": ("四国めたん", "ノーマル"),
    "Kodomo": ("小夜/SAYO", "ノーマル"),
    "Murabito": ("ちび式じい", "ノーマル"),
}
# 読み上げの調子（ナレーションは少しゆっくり、絵本の語り口に）
SPEED = {"N": 0.95}

LINE = re.compile(r'L\("(?P<id>[A-Za-z0-9_]+)",\s*(?P<speaker>\w+),\s*"(?P<text>(?:[^"\\]|\\.)*)"\)')


def request(path, params=None, body=None):
    url = ENGINE + path + ("?" + urllib.parse.urlencode(params) if params else "")
    data = None if body is None else json.dumps(body).encode("utf-8")
    req = urllib.request.Request(url, data=data, method="POST" if params is not None or body is not None else "GET",
                                 headers={"Content-Type": "application/json"})
    # ローカルのエンジンなので、Windows のプロキシ設定を通さない（通すと 503 になることがある）
    with urllib.request.build_opener(urllib.request.ProxyHandler({})).open(req, timeout=120) as res:
        return res.read()


def style_ids():
    speakers = json.loads(request("/speakers"))
    ids = {}
    for key, (name, style) in SPEAKERS.items():
        sp = next((s for s in speakers if s["name"] == name), None)
        if sp is None:
            sys.exit(f"VOICEVOX に「{name}」がいません（エンジンのバージョンを確認してください）")
        st = next((s for s in sp["styles"] if s["name"] == style), sp["styles"][0])
        ids[key] = st["id"]
    return ids


def synthesize(text, style_id, speed):
    query = json.loads(request("/audio_query", {"text": text, "speaker": style_id}))
    query["speedScale"] = speed
    query["outputSamplingRate"] = 24000
    query["outputStereo"] = False
    query["prePhonemeLength"] = 0.1
    query["postPhonemeLength"] = 0.2
    return request("/synthesis", {"speaker": style_id}, query)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--force", action="store_true", help="既にある音声も作り直す")
    ap.add_argument("--only", nargs="*", help="この id だけ作る")
    args = ap.parse_args()

    lines = [m.groupdict() for m in LINE.finditer(SCRIPT.read_text(encoding="utf-8"))]
    ids = style_ids()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    made = 0
    for line in lines:
        out = OUT_DIR / f"{line['id']}.wav"
        if args.only and line["id"] not in args.only:
            continue
        if out.exists() and not args.force and not args.only:
            continue
        key = line["speaker"]
        if key not in ids:
            print(f"  skip {line['id']}: 話者 {key} の声が決まっていません")
            continue
        text = line["text"].replace("\\n", "").replace("……", "…")
        out.write_bytes(synthesize(text, ids[key], SPEED.get(key, 1.0)))
        made += 1
        print(f"  {line['id']} [{SPEAKERS[key][0]}] {line['text']}")
    print(f"{made} 件の音声を作りました → {OUT_DIR.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
