<p align="center"><img src="assets/icon.png" width="128" alt="Suzume icon"></p>

# Suzume

[日本語](#日本語) | [English](#english)

FINAL FANTASY XIV のミニオン「タビスズメ」(英語版: Road Sparrow) を、本物のスズメのように動かし、鳴かせる [Dalamud](https://github.com/goatcorp/Dalamud) プラグインです。

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin that makes the FINAL FANTASY XIV minion Road Sparrow (タビスズメ) move and call like a real sparrow.

---

## 日本語

### できること

- **スズメらしい動き**: 両足をそろえたホップで歩き、キョロキョロ見回し、地面をついばみ、毛づくろいをします。離れすぎると波を描くように飛んで追いかけ、着地します。地面の高さに合わせて歩くので、段差や坂でも浮いたり埋まったりしません。
- **しぐさ**: 首をクイッとかしげる、尾を振る、翼をちょっと動かす、といった細かいしぐさが入ります。
- **頭に乗る**: `/beckon` (手まねき) でプレイヤーの頭に飛び乗り、もう一度 `/beckon` で下ります。
- **驚いて逃げる**: スズメに向けて `/angry` `/furious` `/slap` `/battlestance` `/box` `/stomp` `/throw` をすると、警戒の声を上げて飛び去ります。
- **砂浴び**: ときどき地面で羽をばたつかせて砂浴びをします。`/suzume dust` で次に落ち着いたときにさせることもできます。
- **鳴き声**: 行動に合わせて、地鳴き・さえずり・警戒の声・飛び立ちの声を鳴きます。声は実際のスズメの録音です。距離で音量が下がり、スズメのいる向き (左右・前後) から聞こえます。
- **ふくら雀**: 冬のスズメのように、羽毛をふくらませた丸い体にできます。ふくらみ具合は設定で変えられます。
- **他の人のスズメも**: 周りの人が出しているタビスズメも、あなたの画面の中で同じように動きます (設定で切り替え)。
- **多言語**: 設定画面とメッセージは Dalamud の言語設定に合わせて、日本語・英語・ドイツ語・フランス語で表示されます。

### 使い方

1. タビスズメを呼び出します。
2. `/suzume` で設定画面を開きます。

| コマンド | 内容 |
|---|---|
| `/suzume` | 設定画面を開く |
| `/suzume on` / `/suzume off` | 有効・無効を切り替える |
| `/suzume reset` | スズメをそばの地面に置き直す |
| `/suzume dust` | 次に落ち着いたら砂浴びをする |
| `/suzume preen` | 次に落ち着いたら毛づくろいをする |

設定画面では、鳴き声のオンオフ・音量・鳴く頻度・向きによる聞こえ方、飛び始める距離、あなたから離れて動き回る距離、頭に乗るときの高さの補正、ふくら雀とふくらみ具合を変えられます。鳴き声は設定画面の「試聴」ボタンで確かめられます。

### インストール

カスタムプラグインリポジトリから入れます。

1. Dalamud の設定 (`/xlsettings`) → 実験的機能 → 「カスタムプラグインリポジトリ」に、次の URL を追加して保存します。

   ```
   https://raw.githubusercontent.com/tts2017/suzume/main/repo.json
   ```

2. プラグインインストーラー (`/xlplugins`) で Suzume を探してインストールします。

#### 自分でビルドする場合

1. [.NET SDK 10](https://dotnet.microsoft.com/) を入れます。
2. `dotnet build src/Suzume/Suzume.csproj -c Debug` でビルドします。
3. Dalamud の設定 → 実験的機能 → 「開発用プラグインの場所」に、`src/Suzume/bin/Debug` の中の `Suzume.dll` を追加します。
4. プラグインインストーラーの「開発ツール」から Suzume を有効にします。

#### リリースの出し方

`v1.2.3` の形のタグを push すると、GitHub Actions がテスト・ビルドしてリリースに `latest.zip` を付け、`repo.json` を更新します。

### 冬羽テクスチャ (おまけ)

`tools/` に、タビスズメの見た目を日本のスズメの冬羽 (ふくら雀) の色と模様に塗り替える [Penumbra](https://github.com/xivdev/Penumbra) mod を作るスクリプトがあります。ゲームのデータから作るため、mod そのものは同梱していません。作り方は [tools/winter_texture.py](tools/winter_texture.py) の先頭を見てください。

### 開発

- 動きの判断 (`src/Suzume.Core`) はゲームに依存しない部分として分けてあり、`dotnet test` でテストできます。
- ゲームへの書き込み (`src/Suzume`) は、ミニオンの位置・向き・アニメーション・ボーンを毎フレーム上書きします。

### ライセンス

- プログラム: [MIT](LICENSE)
- 鳴き声: 元の録音それぞれのライセンスに従います (CC0・CC BY-SA)。作者とライセンスは [assets/sounds/CREDITS.md](assets/sounds/CREDITS.md) にあります。CC BY-SA の録音を加工したものは、同じ CC BY-SA で配布されます。

FINAL FANTASY XIV は株式会社スクウェア・エニックスの登録商標です。このプラグインはスクウェア・エニックスとは関係ありません。

---

## English

### Features

- **Sparrow-like movement**: It hops with both feet together, looks around, pecks at the ground and preens. When you get too far away, it follows in a bounding flight and lands. It walks on the actual ground height, so it neither floats nor sinks on steps and slopes.
- **Gestures**: Small touches such as a quick head tilt, tail flicks and wing twitches.
- **Perching**: `/beckon` makes it fly up onto your head; `/beckon` again brings it down.
- **Scaring**: It gives an alarm call and flies off if you `/angry` `/furious` `/slap` `/battlestance` `/box` `/stomp` `/throw` at it.
- **Dust bathing**: Now and then it flutters on the ground in a dust bath. `/suzume dust` asks for one the next time it settles down.
- **Calls**: Contact calls, song, alarm calls and flight calls that match what it does, made from real sparrow recordings. They get quieter with distance and come from where the sparrow is (left/right, front/back).
- **Puffed up**: Like a sparrow in winter, it can fluff up into a round ball. How much is adjustable.
- **Other players' sparrows**: Road Sparrows summoned by people around you behave the same on your screen (can be turned off).
- **Languages**: The settings and messages follow Dalamud's language setting: Japanese, English, German or French.

### Usage

1. Summon your Road Sparrow.
2. Open the settings with `/suzume`.

| Command | What it does |
|---|---|
| `/suzume` | Open the settings |
| `/suzume on` / `/suzume off` | Turn it on or off |
| `/suzume reset` | Put the sparrow back on the ground beside you |
| `/suzume dust` | Take a dust bath once it settles down |
| `/suzume preen` | Preen once it settles down |

The settings cover calls on/off, volume, call frequency, directional calls, the distance at which it flies, how far it roams from you, a head height adjustment for perching, and puffing up and how much. The Preview buttons play each call.

### Installation

Install it from a custom plugin repository.

1. In Dalamud Settings (`/xlsettings`) → Experimental → Custom Plugin Repositories, add this URL and save:

   ```
   https://raw.githubusercontent.com/tts2017/suzume/main/repo.json
   ```

2. Find Suzume in the plugin installer (`/xlplugins`) and install it.

#### Building it yourself

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/).
2. Build with `dotnet build src/Suzume/Suzume.csproj -c Debug`.
3. In Dalamud Settings → Experimental → Dev Plugin Locations, add `Suzume.dll` from `src/Suzume/bin/Debug`.
4. Enable Suzume under Dev Tools in the plugin installer.

#### Making a release

Pushing a tag like `v1.2.3` makes GitHub Actions test and build the plugin, attach `latest.zip` to a release and update `repo.json`.

### Winter plumage texture (extra)

`tools/` contains a script that builds a [Penumbra](https://github.com/xivdev/Penumbra) mod repainting the Road Sparrow in the colours and pattern of a Japanese tree sparrow's puffed-up winter plumage. It is built from the game's own data, so the mod itself is not included. See the top of [tools/winter_texture.py](tools/winter_texture.py) for how to build it.

### Development

- The behaviour logic (`src/Suzume.Core`) has no game dependency and is tested with `dotnet test`.
- The game side (`src/Suzume`) overrides the minion's position, facing, animation and bones every frame.

### License

- Code: [MIT](LICENSE)
- Calls: each follows the license of its source recording (CC0 or CC BY-SA). Authors and licenses are listed in [assets/sounds/CREDITS.md](assets/sounds/CREDITS.md). Calls made from CC BY-SA recordings are distributed under the same CC BY-SA license.

FINAL FANTASY XIV is a registered trademark of Square Enix Holdings Co., Ltd. This plugin is not affiliated with Square Enix.
