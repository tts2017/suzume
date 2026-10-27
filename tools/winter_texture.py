"""タビスズメを、日本のスズメの冬羽 (ふくら雀) の色と模様に塗り替えた Penumbra mod を作る。

先に tools/ExtractSparrowMaps で、元のテクスチャと画素ごとの部位・位置・法線を書き出しておく。
numpy だけを使う。使い方:
    dotnet run --project tools/ExtractSparrowMaps -- "<sqpack フォルダ>" out/sparrow_maps
    py tools/winter_texture.py out/sparrow_maps out/winter
出力:
    out/winter/preview_d.png                            塗った色 (確認用)
    out/winter/mod/...                                  Penumbra の mod フォルダ
    out/winter/タビスズメ冬羽.pmp                         Penumbra に読み込む mod ファイル
"""
import json
import struct
import sys
import zipfile
import zlib
from pathlib import Path

import numpy as np

SIZE = 256
GAME_PATH = "chara/monster/m8217/obj/body/b0001/texture/v01_m8217b0001_{}.tex"
MOD_NAME = "タビスズメ 冬羽 (ふくら雀)"

# ボーン番号 (tools/ExtractSparrowMaps の bones.txt と同じ並び)
SEBO, KUBI, MOUTH_UP, MOUTH, KOSI, TAIL = 0, 1, 2, 3, 4, 5
LEGS = (6, 7, 8, 9)
WING_INNER, WING_OUTER = (10, 13), (11, 12)

# 日本のスズメの冬羽の色 (sRGB)
# 茶色の部分は灰色に寄せる (GREY_MIX の割合だけ、同じ明るさの灰色を混ぜる)
GREY_MIX = 0.7


def greyish(rgb, mix=GREY_MIX):
    grey = sum(rgb) / 3
    return tuple(round(c * (1 - mix) + grey * mix) for c in rgb)


CHESTNUT = greyish((120, 86, 66))      # 頭のてっぺん・後頭部
BROWN = greyish((136, 92, 54))         # 背中・翼
DARK = greyish((44, 32, 24))           # 背中の縦じま・羽の芯
BLACK = (24, 21, 20)                   # のど・ほっぺの斑点・くちばしの付け根
WHITE = (236, 233, 224)                # ほっぺ・襟・翼の帯
BELLY = (208, 202, 190)                # 胸・腹 (灰色がかった白)
BUFF = greyish((186, 156, 116))        # 脇腹・羽の縁
TAIL_COLOR = greyish((92, 68, 48))     # 尾
VENT = greyish((180, 164, 142))        # 尾の下 (淡い灰茶色)
WING_UNDER = greyish((176, 162, 146))  # 翼の裏


def load(folder):
    folder = Path(folder)
    maps = np.fromfile(folder / "maps.f32", dtype=np.float32).reshape(SIZE, SIZE, 7)
    textures = {k: np.fromfile(folder / f"{k}.bgra", dtype=np.uint8).reshape(SIZE, SIZE, 4) for k in "dns"}
    return maps, textures


def blur(image, radius):
    """簡単な箱ぼかし (縦横に 2 回)"""
    if radius <= 0:
        return image
    out = image.astype(np.float32)
    k = 2 * radius + 1
    for axis in (0, 1):
        for _ in range(2):
            padded = np.pad(out, [(radius, radius) if a == axis else (0, 0) for a in range(out.ndim)], mode="edge")
            cumsum = np.cumsum(padded, axis=axis)
            zero = np.zeros_like(np.take(cumsum, [0], axis=axis))
            cumsum = np.concatenate([zero, cumsum], axis=axis)
            out = (np.take(cumsum, range(k, cumsum.shape[axis]), axis=axis) - np.take(cumsum, range(0, cumsum.shape[axis] - k), axis=axis)) / k
    return out


def regions(maps, original):
    """画素ごとの部位を、色 (float RGB 0〜255) と「元のまま残す」マスクにする。original は元の絵 (float RGB)"""
    original_luma = original.mean(axis=2)
    bone = maps[..., 0].astype(int)
    x, y, z = np.abs(maps[..., 1]), maps[..., 2], maps[..., 3]
    ny = maps[..., 5]
    color = np.zeros((SIZE, SIZE, 3), np.float32)

    def paint(mask, rgb):
        color[mask] = rgb

    body = (bone == SEBO) | (bone == KOSI)
    head = bone == KUBI

    # 体: 腹 → 尾の下 → 背中 (縦じまの羽模様、肩から尾の付け根まで) → 首の白い襟 → のどの黒
    # 茶色い背中の範囲は、元の絵が茶色に塗った所に合わせる (上を向いた面だけにすると、後ろの下半分が白くなる)
    paint(body, BELLY)
    paint(body & (np.abs(maps[..., 4]) > 0.8), np.add(BELLY, BUFF) / 2)  # 脇腹はうっすら黄土色
    paint(body & (z < -0.035) & (ny < 0.1), VENT)
    # 茶色は赤みが強い (R - B が大きい)。クリーム色の腹は影でも赤みが弱い
    # 羽毛の細かい濃淡でまだらにならないよう、赤みを大きくぼかしてから分ける (体の外は混ぜない)
    redness = (original[..., 0] - original[..., 2]) * body
    redness = blur(redness, 6) / np.maximum(blur(body.astype(np.float32), 6), 1e-3)
    # 元の絵の首の後ろのクリーム色の帯も背中にする (残すと うなじまで白い襟が一周する)
    back = body & ((redness > 50) | (bone == SEBO) & (y > 0.09) & (z < 0.01))
    paint(back, BROWN)
    # 背中の黒い縦じま: 体の前後に流れる筋を、むらで途切れさせる
    wobble = blur(np.random.default_rng(7).standard_normal((SIZE, SIZE)).astype(np.float32), 2) * 6.0
    streak = back & (np.sin(x * 420.0 + z * 40.0 + wobble) > 0.65)
    paint(streak, DARK)
    # 白い襟は首の横だけ。首の後ろ (うなじ) では途切れて茶色になる
    collar = (bone == SEBO) & (y > 0.1) & (x > 0.012) & (z > -0.01) & ((x > 0.022) | (z > 0.004))
    paint(collar, WHITE)
    # のどの黒は胸の上まで縦に細く伸びる
    bib_body = (bone == SEBO) & (z > 0.035) & (y > 0.075) & (x < 0.010 - (0.101 - y) * 0.18)
    paint(bib_body, BLACK)

    # 頭 (実物の写真に合わせる): 帽子は目の高さまで下り、額は くちばしの付け根まで茶色。
    # 目は くちばしから続く黒い筋の中にある。のどの黒は くちばしの下から縦に細く。
    # ほっぺの黒い斑点は、正面から見て目の外側・下 (目の高さに置くと、もう 1 つの目に見える)
    paint(head, WHITE)
    paint(head & ((y > 0.115) | (z < 0.0) & (y > 0.105)), CHESTNUT)
    paint(head & (z < 0.004) & (x < 0.022) & (y <= 0.105), BROWN)                 # うなじ
    paint(head & (x < 0.014) & (y > 0.112) & (z > 0.035), CHESTNUT)                # 額
    paint(head & (y < 0.104) & (z > 0.030) & (x < 0.010), BLACK)                  # のど
    paint(head & (z > 0.041) & (x < 0.022) & (y > 0.103) & (y < 0.114), BLACK)    # 目先 (目とくちばしの間)
    cheek_spot = head & ((x - 0.028) ** 2 + (y - 0.098) ** 2 + (z - 0.034) ** 2 < 0.0065 ** 2)
    paint(cheek_spot, BLACK)

    # 尾
    paint(bone == TAIL, TAIL_COLOR)

    # 翼: 表は茶色で、元の絵の濃い羽 (風切羽の先など) を黒褐色に。前縁に沿って白い帯 2 本。
    # UV では上の島が裏 (淡い色)、下の島が表
    row = np.arange(SIZE)[:, None]
    wing = np.isin(bone, WING_INNER + WING_OUTER)
    under = wing & (row < 135)
    top = wing & ~under
    paint(under, WING_UNDER)
    darkness = np.clip((105.0 - original_luma) / 45.0, 0.0, 1.0)[..., None]
    color[top] = (np.array(BROWN) * (1 - darkness) + np.array(DARK) * darkness)[top]
    # 前縁から後ろの羽 (風切羽・三列風切): 付け根から先へ並ぶ細長い羽。1 枚ごとに黒い芯・茶色・淡い縁が、
    # 羽の向き (後ろへ行くほど少し外へ流れる斜め) に筋になる。付け根側の三列風切は幅が広い
    width = np.where(x < 0.06, 0.02, 0.015)
    sway = blur(np.random.default_rng(11).standard_normal((SIZE, SIZE)).astype(np.float32), 3) * 0.08
    across = np.mod((x + 0.15 * (0.04 - z)) / width + sway, 1.0)
    edge = np.abs(across - 0.5) * 2.0          # 0 が羽の芯、1 が縁
    flight = top & (z < 0.024)
    paint(flight & (edge < 0.5), DARK)
    paint(flight & (edge >= 0.5) & (edge < 0.8), BROWN)
    paint(flight & (edge >= 0.8), BUFF)
    # 前縁 (z が大きい側) から少し後ろ、付け根側だけに白い帯 (中雨覆の先) と淡い帯 (大雨覆の先)
    paint(top & (z > 0.025) & (z < 0.030) & (x < 0.075), WHITE)
    paint(top & (z > 0.012) & (z < 0.0155) & (x < 0.07), BUFF)

    keep = (bone < 0) | np.isin(bone, (MOUTH_UP, MOUTH) + LEGS)
    return color, keep, bone


def winter_diffuse(maps, original_bgra):
    original = original_bgra[..., [2, 1, 0]].astype(np.float32)
    color, keep, bone = regions(maps, original)
    luma = original.mean(axis=2) / 255.0

    # 部位の境目を少しぼかしてから、元の羽毛の細かい濃淡を重ねる
    # UV の島の外 (塗っていない黒) を混ぜないよう、島の中だけでぼかす (混ぜると継ぎ目が黒い線になる)
    inside = (bone >= 0).astype(np.float32)[..., None]
    soft = blur(color * inside, 1) / np.maximum(blur(inside, 1), 1e-3)
    detail = luma - blur(luma, 3)
    rng = np.random.default_rng(1225)
    down = blur(rng.standard_normal((SIZE, SIZE)).astype(np.float32), 1)  # ふわふわした綿毛のむら
    shaded = soft * (1.0 + 1.4 * detail[..., None] + 0.05 * down[..., None])

    # 元の目 (黒目と光) はまわりごとそのまま残す
    p = maps[..., 1:4]
    eye = (bone == KUBI) & ((np.abs(p[..., 0]) - 0.019) ** 2 + (p[..., 1] - 0.1105) ** 2 + (p[..., 2] - 0.0425) ** 2 < 0.0062 ** 2)
    result = np.where((keep | eye)[..., None], original, shaded)
    rgb = np.clip(result, 0, 255).astype(np.uint8)
    bgra = np.concatenate([rgb[..., [2, 1, 0]], original_bgra[..., 3:4]], axis=2)
    return bgra


def fluffy_normal(maps, original_bgra):
    """法線マップに、羽毛の細かい凹凸を足す (体・頭・翼・尾だけ)"""
    bone = maps[..., 0].astype(int)
    feathered = np.isin(bone, (SEBO, KUBI, KOSI, TAIL) + WING_INNER + WING_OUTER)
    rng = np.random.default_rng(2024)
    bump = blur(rng.standard_normal((SIZE, SIZE, 2)).astype(np.float32), 1) * 14.0
    out = original_bgra.astype(np.float32)
    # BGRA の R (2) と G (1) が法線の向き
    out[..., 2] += np.where(feathered, bump[..., 0], 0)
    out[..., 1] += np.where(feathered, bump[..., 1], 0)
    return np.clip(out, 0, 255).astype(np.uint8)


def mipmaps(bgra):
    levels = [bgra]
    while levels[-1].shape[0] > 1:
        prev = levels[-1].astype(np.float32)
        levels.append(((prev[0::2, 0::2] + prev[1::2, 0::2] + prev[0::2, 1::2] + prev[1::2, 1::2]) / 4).astype(np.uint8))
    return levels


def write_tex(path, bgra):
    """無圧縮 B8G8R8A8 の .tex を書く (ミップマップ付き)"""
    levels = mipmaps(bgra)
    offsets = []
    offset = 80
    for level in levels:
        offsets.append(offset)
        offset += level.nbytes
    offsets += [0] * (13 - len(offsets))
    header = struct.pack(
        "<IIHHHBB3I13I",
        0x00800000,          # 2D テクスチャ
        0x1450,              # B8G8R8A8
        SIZE, SIZE, 1,
        len(levels), 0,      # ミップ数、配列数
        0, 1, 2,             # LOD ごとの先頭ミップ
        *offsets[:13])
    Path(path).write_bytes(header + b"".join(level.tobytes() for level in levels))


def write_png(path, rgb):
    h, w = rgb.shape[:2]
    raw = b"".join(b"\x00" + rgb[y].tobytes() for y in range(h))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    Path(path).write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                           + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


def write_mod(folder, textures):
    """Penumbra の mod フォルダと、それを zip にまとめた .pmp を作る"""
    mod = Path(folder) / "mod"
    (mod / "textures").mkdir(parents=True, exist_ok=True)
    files = {}
    for kind, bgra in textures.items():
        name = f"textures/v01_m8217b0001_{kind}.tex"
        write_tex(mod / name, bgra)
        files[GAME_PATH.format(kind)] = name.replace("/", "\\")
    meta = {
        "FileVersion": 3,
        "Name": MOD_NAME,
        "Author": "Suzume",
        "Description": "ミニオン「タビスズメ」を、日本のスズメの冬羽の色と模様にし、羽毛の凹凸を足します。",
        "Version": "1.0",
        "Website": "",
        "ModTags": ["minion"],
    }
    default = {"Name": "", "Description": "", "Files": files, "FileSwaps": {}, "Manipulations": []}
    (mod / "meta.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
    (mod / "default_mod.json").write_text(json.dumps(default, ensure_ascii=False, indent=2), encoding="utf-8")

    pmp = Path(folder) / "タビスズメ冬羽.pmp"
    with zipfile.ZipFile(pmp, "w", zipfile.ZIP_DEFLATED) as z:
        for path in sorted(mod.rglob("*")):
            if path.is_file():
                z.write(path, path.relative_to(mod).as_posix())
    return pmp


def main(maps_folder, out_folder):
    maps, textures = load(maps_folder)
    out = Path(out_folder)
    out.mkdir(parents=True, exist_ok=True)
    diffuse = winter_diffuse(maps, textures["d"])
    normal = fluffy_normal(maps, textures["n"])
    write_png(out / "preview_d.png", np.repeat(np.repeat(diffuse[..., [2, 1, 0]], 2, 0), 2, 1))
    pmp = write_mod(out, {"d": diffuse, "n": normal})
    print(pmp)


if __name__ == "__main__":
    main(*sys.argv[1:3])
