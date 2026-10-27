"""タビスズメを、日本のスズメの冬羽 (ふくら雀) の色と模様に塗り替えた Penumbra mod を作る。

先に tools/ExtractSparrowMaps で、元のテクスチャと画素ごとの部位・位置・法線を書き出しておく。
numpy だけを使う。使い方 (羽を細かく描けるよう、1024x1024 で塗る):
    dotnet run --project tools/ExtractSparrowMaps -- "<sqpack フォルダ>" out/sparrow_maps_1024 1024
    py tools/winter_texture.py out/sparrow_maps_1024 out/winter
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

# 元のテクスチャは 256x256。maps を大きく書き出すと (ExtractSparrowMaps の 3 つ目の引数)、その大きさで細かく塗る
BASE_SIZE = 256
SIZE = BASE_SIZE
SCALE = 1
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
# 翼は写真の赤茶色が残るよう、灰色は控えめに混ぜる
WING_GREY_MIX = 0.3
WING_RUFOUS = greyish((156, 98, 56), WING_GREY_MIX)   # 羽の赤茶の縁
WING_DARK = greyish((70, 54, 42), WING_GREY_MIX)      # 風切羽の黒褐色
WING_BLACK = (34, 28, 24)                             # 雨覆・三列風切の黒い芯
WING_PALE = (214, 200, 172)                           # 羽の淡い縁取り・大雨覆の先


def load(folder):
    """maps と元のテクスチャを読む。テクスチャは maps の大きさに引き伸ばす。"""
    global SIZE, SCALE
    folder = Path(folder)
    raw = np.fromfile(folder / "maps.f32", dtype=np.float32)
    SIZE = int(round((raw.size / 7) ** 0.5))
    SCALE = SIZE // BASE_SIZE
    maps = raw.reshape(SIZE, SIZE, 7)
    textures = {k: resize(np.fromfile(folder / f"{k}.bgra", dtype=np.uint8).reshape(BASE_SIZE, BASE_SIZE, 4), SIZE)
                for k in "dns"}
    return maps, textures


def resize(image, size):
    """双線形で引き伸ばす (同じ大きさならそのまま)"""
    if image.shape[0] == size:
        return image
    src = image.astype(np.float32)
    n = image.shape[0]
    coords = (np.arange(size) + 0.5) * n / size - 0.5
    lo = np.clip(np.floor(coords).astype(int), 0, n - 1)
    hi = np.clip(lo + 1, 0, n - 1)
    f = np.clip(coords - lo, 0.0, 1.0)
    rows = src[lo] * (1 - f)[:, None, None] + src[hi] * f[:, None, None]
    out = rows[:, lo] * (1 - f)[None, :, None] + rows[:, hi] * f[None, :, None]
    return np.clip(out + 0.5, 0, 255).astype(image.dtype) if image.dtype == np.uint8 else out


def smooth_noise(seed, radius, channels=None):
    """なめらかなむら。元の 256 画素の大きさで作って引き伸ばすので、テクスチャの大きさによらず同じ見た目になる"""
    shape = (BASE_SIZE, BASE_SIZE) if channels is None else (BASE_SIZE, BASE_SIZE, channels)
    noise = box_blur(np.random.default_rng(seed).standard_normal(shape).astype(np.float32), radius)
    noise = noise if noise.ndim == 3 else noise[..., None]
    out = resize(noise, SIZE)
    return out if channels is not None else out[..., 0]


def blur(image, radius):
    """ぼかし。radius は元の 256 画素での幅 (大きいテクスチャでは同じ見た目になるよう広げる)"""
    return box_blur(image, radius * SCALE)


def box_blur(image, radius):
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


# 翼の表の座標: 付け根から先へ (たたむと体の前から後ろへ) と、前縁から後縁へ (たたむと上から下へ)
WING_ROOT, WING_TIP = 0.031, 0.134
WING_TOP, WING_BOTTOM = 0.041, -0.023


def scale_row(s, t, t0, t1, count, s_max, shift):
    """うろこ状に並ぶ雨覆の 1 段。羽は前後に並び、丸い先端が下を向く。
    戻り値: (この段の羽が見える所, 羽の真ん中からの横のずれ -0.5〜0.5, 根元から先端への 0〜1)"""
    u = np.mod(s / s_max * count + shift, 1.0) - 0.5
    v = (t - t0) / (t1 - t0)
    tip = 1.0 - 0.45 * (2.0 * u) ** 2
    return (s < s_max) & (v >= 0.0) & (v <= tip), u, v / np.maximum(tip, 1e-3)


def paint_wing_top(color, top, x, z):
    """翼の表を、実物のスズメ (写真) のたたんだ翼のように塗る。下 (奥) の羽から順に重ねる:
    風切羽 → 三列風切 → 大雨覆 → 中雨覆 → 小雨覆。"""
    s = (x - WING_ROOT) / (WING_TIP - WING_ROOT)
    t = (WING_TOP - z) / (WING_TOP - WING_BOTTOM)
    sway = smooth_noise(11, 3) * 0.04

    def put(mask, rgb):
        color[top & mask] = rgb

    # 風切羽: 後ろへ伸びる細長い羽が上下に重なる。黒褐色の芯に、赤茶の外縁と細い淡い縁取り
    across = np.mod(t * 6.0 + 0.15 * s + sway, 1.0)
    edge = np.abs(across - 0.5) * 2.0
    put(np.ones_like(top), WING_DARK)
    put(edge > 0.6, WING_RUFOUS)
    put(edge > 0.9, WING_PALE)

    # 三列風切: 背中寄りの後ろに 3 枚の大きな羽。黒い芯を赤茶の太い縁が囲み、外側に淡い縁取り。丸い先端は後ろを向く
    u = np.mod((t + sway) / 0.66 * 3.0, 1.0) - 0.5
    v = (s - 0.34) / 0.6
    tip = 1.0 - 0.4 * (2.0 * u) ** 2
    tertial = (t < 0.66) & (v >= 0.0) & (v <= tip)
    put(tertial, WING_RUFOUS)
    put(tertial & (np.abs(u) < 0.3 * (1.0 - 0.5 * v)) & (v < tip - 0.15), WING_BLACK)
    put(tertial & ((np.abs(u) > 0.44) | (v > tip - 0.05)), WING_PALE)

    # 大雨覆: 黒い芯に赤茶の縁、淡い先端 (2 本目の帯)
    row, u, v = scale_row(s + sway, t, 0.34, 0.58, 5, 0.5, 0.5)
    put(row, WING_RUFOUS)
    put(row & (np.abs(u) < 0.22) & (v < 0.86), WING_BLACK)
    put(row & (v > 0.86), WING_PALE)

    # 中雨覆: 黒い芯に白い先端。先端が並んで白い帯になる
    row, u, v = scale_row(s + sway, t, 0.17, 0.37, 6, 0.5, 0.0)
    put(row, WING_BLACK)
    put(row & (v > 0.6), WHITE)

    # 小雨覆: 肩の小さな赤茶の羽。芯だけ少し濃い
    row, u, v = scale_row(s + sway, t, -0.02, 0.2, 6, 0.5, 0.5)
    put(row, WING_RUFOUS)
    put(row & (np.abs(u) < 0.14) & (v < 0.7), WING_DARK)


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
    wobble = smooth_noise(7, 2) * 6.0
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

    # 翼: 裏は淡い色。表は実物の写真に合わせて、羽の種類ごとに描き分ける (paint_wing_top)。
    # UV では上の島が裏 (淡い色)、下の島が表
    row = np.arange(SIZE)[:, None]
    wing = np.isin(bone, WING_INNER + WING_OUTER)
    under = wing & (row < 135 * SCALE)
    top = wing & ~under
    paint(under, WING_UNDER)
    paint_wing_top(color, top, x, z)

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
    down = smooth_noise(1225, 1)  # ふわふわした綿毛のむら
    # 翼は羽を描き込んだので、元の絵の濃淡は弱く重ねる (強いと描いた羽の上に元の斑点が透ける)
    detail_weight = np.where(np.isin(bone, WING_INNER + WING_OUTER), 0.4, 1.4)[..., None]
    shaded = soft * (1.0 + detail_weight * detail[..., None] + 0.05 * down[..., None])

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
    bump = smooth_noise(2024, 1, channels=2) * 14.0
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
    preview = diffuse[..., [2, 1, 0]]
    if SIZE < 512:
        preview = np.repeat(np.repeat(preview, 2, 0), 2, 1)
    write_png(out / "preview_d.png", preview)
    pmp = write_mod(out, {"d": diffuse, "n": normal})
    print(pmp)


if __name__ == "__main__":
    main(*sys.argv[1:3])
