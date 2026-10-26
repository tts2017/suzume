# タビスズメ リアル化プラグイン 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 自分のミニオン「タビスズメ」を、両足ホップ・キョロキョロ・ついばみ・波状飛行で動かし、行動に合わせて 4 種類の鳴き声を鳴らす Dalamud プラグインを作る。

**Architecture:** ゲームの追従 AI が決めるミニオンの論理位置には手を加えず、ゲーム非依存の `SparrowBrain`(Suzume.Core)が「見た目の位置」を計算する。プラグインはその差分を `GameObject.DrawOffset` に、向きを `SetRotation` に、アニメーションを `Timeline.BaseOverride` に毎フレーム書き込む。鳴き声は numpy で合成した WAV を埋め込み、NAudio で再生する。

**Tech Stack:** C# / .NET 10、Dalamud.NET.Sdk 15.0.0、FFXIVClientStructs(Dalamud 同梱)、NAudio.WinMM 2.2.1、xUnit 2.9.3、Python 3.12 + numpy 2.2

**Spec:** `docs/superpowers/specs/2026-10-27-suzume-minion-design.md`

## Global Constraints

- 対象は自分のミニオンのみ。Companion ID は `247`(タビスズメ)、モデルは `chara/monster/m8217`
- v1 ではボーン操作をしない(首・尾・翼は v2)
- 距離の単位は yalm、時間の単位は秒、パラメータの角度は度
- プラグイン SDK: `Dalamud.NET.Sdk/15.0.0`。Core とテストは `net10.0`
- 音声: 44.1kHz / 16bit / モノラル WAV、4 種類 × 3 バリエーション、合成には numpy だけを使う
- WAV のファイル名は `<種類>_<番号>.wav`(種類は `contact` `song` `alarm` `flight`、番号は 1〜3)。埋め込みリソース名は `Suzume.Sounds.<ファイル名>`
- コードのコメントは日本語(既存プラグイン `G:\ff14\emote` と同じスタイル)
- 実機用の配置先は `C:\ff14_suzume\`(Dalamud の開発プラグインとして `C:\ff14_suzume\Suzume.dll` を登録する)
- プラグインを無効化・アンロードしたら、DrawOffset と BaseOverride を 0 に戻す

## Review Focus

- ロード明けなどでフレーム時間が極端に長いとき → 瞬間移動せず、1 ホップ分以上は動かない(Task 3 `HugeFrameDeltaDoesNotTeleport`)
- テレポート・エリア移動で論理位置が遠くへ飛んだとき → 追いかけず、その場に置き直す(Task 3 `SnapsToTargetAfterTeleport`)
- 長時間の放置 → 向きの値が発散せず、位置もずれない(Task 3 `StaysPutAndKeepsYawWrappedOverLongIdle`)
- 騎乗中の高速移動 → 置いていかれてリセットを繰り返さない(Task 3 `KeepsUpWithFastMountWithoutResetting`)
- 段差・階段で論理位置の高さだけ変わる → 宙に浮いたり埋まったりせず、ホップで登る(Task 3 `HopsUpOntoSmallStep`)

---

## File Structure

```
G:\ff14\suzume
├─ .gitignore
├─ Suzume.slnx
├─ src/
│  ├─ Suzume.Core/
│  │  ├─ Suzume.Core.csproj
│  │  ├─ Angles.cs            角度・距離の小さな数学関数
│  │  ├─ Random.cs            IRandom、SeededRandom、範囲指定の乱数
│  │  ├─ Hop.cs               1 回のホップの放物線
│  │  ├─ SparrowTypes.cs      列挙型と入力・出力の record
│  │  ├─ SparrowParams.cs     行動パラメータ
│  │  ├─ SparrowBrain.cs      行動の状態機械
│  │  ├─ ChirpScheduler.cs    いつ・どの鳴き声を鳴らすか
│  │  └─ ChirpVolume.cs       距離による音量減衰
│  └─ Suzume/
│     ├─ Suzume.csproj
│     ├─ Suzume.json          プラグインのマニフェスト
│     ├─ Plugin.cs            エントリポイント・コマンド
│     ├─ MinionTracker.cs     自分のタビスズメを探す
│     ├─ ProbeRunner.cs       スパイク用の確認コマンド(デバッグ用に残す)
│     ├─ Configuration.cs
│     ├─ ChirpPlayer.cs       NAudio 再生
│     ├─ PoseApplier.cs       ポーズをゲームに書き込む
│     ├─ SparrowController.cs 毎フレームの統括
│     └─ Windows/ConfigWindow.cs
├─ tests/Suzume.Core.Tests/
│  ├─ Suzume.Core.Tests.csproj
│  ├─ SequenceRandom.cs
│  ├─ AnglesTests.cs  RandomTests.cs  HopTests.cs
│  ├─ SparrowBrainTests.cs
│  └─ ChirpTests.cs
├─ tools/
│  ├─ chirps.py           鳴き声の合成
│  └─ check_chirps.py     鳴き声の検証とスペクトログラム出力
└─ assets/sounds/         合成済み WAV 12 個
```

---

### Task 1: リポジトリ初期化とスパイク(実機で書き込み方法を確認)

設計上の最大リスク(描画位置・向き・前傾・アニメの上書きがゲームに打ち消されないか)を、最小のプラグインで実機確認する。**このタスクの結果が出るまで Task 7 には進まない。** Task 2〜6 はゲームに依存しないので、結果待ちの間に進めてよい。

**Files:**
- Create: `.gitignore`, `Suzume.slnx`
- Create: `src/Suzume/Suzume.csproj`, `src/Suzume/Suzume.json`, `src/Suzume/Plugin.cs`, `src/Suzume/MinionTracker.cs`, `src/Suzume/ProbeRunner.cs`
- Create: `docs/superpowers/plans/2026-10-27-spike-results.md`

**Interfaces:**
- Produces: `MinionTracker.FindSparrow(out Character* owner) → Companion*`(Task 7 で使用)、`MinionTracker.SparrowCompanionId = 247`
- Produces: `ProbeRunner`(`Off` 定数、`Mode`、`Set(string, ushort)`、`OnUpdate(IFramework)`、`OnDraw()`、`Release()`、`Describe() → string`)
- Produces: スパイク結果(Task 7 の `PoseApplier` の定数を決める)

- [ ] **Step 1: git を初期化し、.gitignore を作る**

```bash
cd /g/ff14/suzume && git init
```

`.gitignore`:

```gitignore
bin/
obj/
.vs/
*.user
__pycache__/
out/
```

- [ ] **Step 2: ソリューションとプラグインのプロジェクトを作る**

`Suzume.slnx`:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/Suzume/Suzume.csproj">
      <Platform Project="x64" />
    </Project>
  </Folder>
</Solution>
```

`src/Suzume/Suzume.csproj`:

```xml
<Project Sdk="Dalamud.NET.Sdk/15.0.0">
  <PropertyGroup>
    <Version>0.1.0.0</Version>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
</Project>
```

`src/Suzume/Suzume.json`:

```json
{
  "Author": "Suzume",
  "Name": "Suzume",
  "Punchline": "ミニオン「タビスズメ」を本物のスズメらしく動かし、鳴かせます。",
  "Description": "自分のタビスズメが、両足ホップ・キョロキョロ・ついばみ・波状飛行で動き、行動に合わせて鳴きます。\n/suzume で設定画面を開きます。",
  "Tags": [ "minion", "companion" ]
}
```

- [ ] **Step 3: MinionTracker を書く**

`src/Suzume/MinionTracker.cs`:

```csharp
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Suzume;

/// <summary>ローカルプレイヤーが呼び出しているタビスズメを探す。</summary>
public static unsafe class MinionTracker
{
    public const ushort SparrowCompanionId = 247;

    /// <summary>タビスズメが出ていなければ null。owner はプレイヤーがいれば常に設定する。</summary>
    public static Companion* FindSparrow(out Character* owner)
    {
        owner = null;
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null)
            return null;

        owner = (Character*)player.Address;
        if (owner->CompanionData.CompanionId != SparrowCompanionId)
            return null;

        return owner->CompanionData.CompanionObject;
    }
}
```

- [ ] **Step 4: ProbeRunner を書く**

`src/Suzume/ProbeRunner.cs`:

```csharp
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Suzume;

/// <summary>
/// ゲームへの書き込み方法を実機で確かめるための確認コマンド。
/// /suzume probe &lt;モード&gt; [timelineId] で切り替える。
/// </summary>
public sealed unsafe class ProbeRunner
{
    public const string Off = "off";

    private float time;

    public string Mode { get; private set; } = Off;
    public ushort TimelineId { get; private set; }

    public void Set(string mode, ushort timelineId)
    {
        Release();
        Mode = mode;
        TimelineId = timelineId;
        time = 0f;
    }

    public void OnUpdate(IFramework framework)
    {
        if (Mode == Off)
            return;

        var minion = MinionTracker.FindSparrow(out _);
        if (minion == null)
            return;

        time += (float)framework.UpdateDelta.TotalSeconds;
        var go = (GameObject*)minion;
        switch (Mode)
        {
            case "bounce":
                // 0.3y の高さで 1 秒に 2 回跳ねる
                go->SetDrawOffset(0f, 0.3f * MathF.Abs(MathF.Sin(time * MathF.PI * 2f)), 0f);
                break;
            case "offsetx":
                // ワールド X 方向に 1y ずらしたまま、ゆっくり回す
                go->SetDrawOffset(1f, 0f, 0f);
                go->SetRotation(WrapAngle(time));
                break;
            case "spin":
                go->SetRotation(WrapAngle(time * 2f));
                break;
            case "pitch":
                WritePitch(go, 0.5f);
                break;
            case "anim":
                ((Character*)minion)->Timeline.BaseOverride = TimelineId;
                break;
            case "freeze":
                minion->MoveDestination = go->Position;
                break;
        }
    }

    /// <summary>描画直前に書き込む版の確認(pitchdraw)。</summary>
    public void OnDraw()
    {
        if (Mode != "pitchdraw")
            return;

        var minion = MinionTracker.FindSparrow(out _);
        if (minion != null)
            WritePitch((GameObject*)minion, 0.5f);
    }

    public void Release()
    {
        var minion = MinionTracker.FindSparrow(out _);
        if (minion == null)
            return;

        ((GameObject*)minion)->SetDrawOffset(0f, 0f, 0f);
        ((Character*)minion)->Timeline.BaseOverride = 0;
    }

    public string Describe()
    {
        var minion = MinionTracker.FindSparrow(out var owner);
        if (owner == null)
            return "プレイヤーがいません";
        if (minion == null)
            return $"タビスズメが出ていません (CompanionId={owner->CompanionData.CompanionId})";

        var go = (GameObject*)minion;
        return $"CompanionId={owner->CompanionData.CompanionId} BaseId={go->BaseId} "
            + $"Pos={go->Position:F2} Rot={go->Rotation:F2} DrawOffset={go->DrawOffset:F2} "
            + $"BaseOverride={((Character*)minion)->Timeline.BaseOverride} Behavior={minion->Behavior} "
            + $"MoveDest={minion->MoveDestination:F2} DrawObject={(nint)go->DrawObject:X}";
    }

    private static void WritePitch(GameObject* go, float pitch)
    {
        var draw = go->DrawObject;
        if (draw != null)
            draw->Object.Rotation = Quaternion.CreateFromYawPitchRoll(go->Rotation, pitch, 0f);
    }

    private static float WrapAngle(float angle) =>
        MathF.IEEERemainder(angle, MathF.Tau);
}
```

- [ ] **Step 5: Plugin.cs(スパイク版)を書く**

`src/Suzume/Plugin.cs`:

```csharp
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Suzume;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/suzume";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly ProbeRunner probe = new();

    public Plugin()
    {
        Framework.Update += probe.OnUpdate;
        PluginInterface.UiBuilder.Draw += probe.OnDraw;
        CommandManager.AddHandler(CommandName, new CommandInfo((_, args) => HandleCommand(args))
        {
            HelpMessage = "/suzume probe <off|bounce|offsetx|spin|pitch|pitchdraw|anim <id>|freeze> / /suzume info",
        });
    }

    private void HandleCommand(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[0] == "probe")
        {
            var id = parts.Length >= 3 && ushort.TryParse(parts[2], out var value) ? value : (ushort)0;
            probe.Set(parts[1], id);
            ChatGui.Print($"[Suzume] probe = {parts[1]} {id}");
            return;
        }

        ChatGui.Print($"[Suzume] {probe.Describe()}");
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
        Framework.Update -= probe.OnUpdate;
        PluginInterface.UiBuilder.Draw -= probe.OnDraw;
        probe.Release();
    }
}
```

- [ ] **Step 6: ビルドして配置する**

```bash
cd /g/ff14/suzume && dotnet build src/Suzume/Suzume.csproj -c Debug
```

Expected: `0 エラー`。`src/Suzume/bin/Debug/Suzume.dll` ができる。

```bash
mkdir -p /c/ff14_suzume && cp -r /g/ff14/suzume/src/Suzume/bin/Debug/. /c/ff14_suzume/
```

- [ ] **Step 7: ユーザーに実機確認を依頼する**

ユーザーへの依頼内容(Dalamud の設定 → 実験的機能 → 開発プラグインの場所に `C:\ff14_suzume\Suzume.dll` を追加し、`/xlplugins` の開発ツールから Suzume を読み込む。タビスズメを呼び出す):

1. `/suzume info` → `CompanionId=247` と出るか。止まっているときの `DrawOffset` が `<0.00, 0.00, 0.00>` か
2. `/suzume probe bounce` → タビスズメが上下に跳ねるか。**止まっているときと、プレイヤーが走って追従させているときの両方**で確認
3. `/suzume probe offsetx` → ミニオンが 1y ずれた場所にいるか。そのずれの方向が、ミニオンの回転に合わせて回るか(回る = ローカル座標)、回らないか(回らない = ワールド座標)
4. `/suzume probe spin` → 止まっているときに回るか。移動中はどうなるか
5. `/suzume probe pitch` → 頭が下がって前のめりになるか、頭が上がるか、変化なしか
6. `pitch` で変化がなければ `/suzume probe pitchdraw` → 前のめりになるか
7. `/suzume probe anim 3`、`anim 13`、`anim 22`、`anim 30` をそれぞれ試し、**走って追従させながら**、どの ID で「待機の姿勢」になり、どの ID で「羽ばたき(飛行ループ)」になるか。`anim 3` で待機にならなければ `anim 4`、`anim 5`、`anim 6` も試す
8. `/suzume probe freeze` → ミニオンが追従しなくなるか(記録のみ)
9. `/suzume probe off` → 元に戻るか

- [ ] **Step 8: 結果を記録し、続行可否を判断する**

`docs/superpowers/plans/2026-10-27-spike-results.md` に、上の 9 項目の結果を書く。判断基準:

| 結果 | 対応 |
|---|---|
| bounce が止まっているときも移動中も効く | 計画どおり DrawOffset 方式で進める |
| bounce が効かない、または移動中にだけ打ち消される | **Task 7 に進まない。** 計画を見直す(freeze の結果をもとに、論理位置ごと自前で制御する方式を検討) |
| offsetx のずれが回る | Task 7 の `PoseApplier.DrawOffsetIsLocal = true` |
| offsetx のずれが回らない | `DrawOffsetIsLocal = false` |
| pitch で前のめり | `PitchWrite = PitchMode.Update`、`PitchSign = 1f` |
| pitch で頭が上がる | `PitchWrite = PitchMode.Update`、`PitchSign = -1f` |
| pitch は変化なし、pitchdraw で前のめり | `PitchWrite = PitchMode.Draw`(頭が上がるなら `PitchSign = -1f`) |
| どちらも変化なし | `PitchWrite = PitchMode.Off`(ついばみ・羽繕いの前傾は出ない。v2 のボーン操作で対応) |
| anim で待機になった ID | `IdleTimelineId` に設定 |
| anim で羽ばたきになった ID | `FlyTimelineId` に設定 |
| 平常時の DrawOffset が 0 でない | その値を `PoseApplier.BaseDrawOffset` に設定 |

- [ ] **Step 9: コミット**

```bash
cd /g/ff14/suzume && git add .gitignore Suzume.slnx src/Suzume docs && git commit -m "feat: スパイク用の確認コマンドを持つプラグインの骨組み

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Core の基盤(角度・乱数・ホップ・型・パラメータ)

**Files:**
- Create: `src/Suzume.Core/Suzume.Core.csproj`, `src/Suzume.Core/Angles.cs`, `src/Suzume.Core/Random.cs`, `src/Suzume.Core/Hop.cs`, `src/Suzume.Core/SparrowTypes.cs`, `src/Suzume.Core/SparrowParams.cs`
- Create: `tests/Suzume.Core.Tests/Suzume.Core.Tests.csproj`, `tests/Suzume.Core.Tests/SequenceRandom.cs`, `tests/Suzume.Core.Tests/AnglesTests.cs`, `tests/Suzume.Core.Tests/RandomTests.cs`, `tests/Suzume.Core.Tests/HopTests.cs`
- Modify: `Suzume.slnx`

**Interfaces:**
- Produces:
  - `static class Angles { float YawTowards(Vector3 from, Vector3 to); float Wrap(float angle); float DegToRad(float degrees); float HorizontalDistance(Vector3 a, Vector3 b); Vector3 Horizontal(Vector3 v); }`
  - `interface IRandom { float NextFloat(); }`(0 以上 1 未満)、`sealed class SeededRandom(int seed) : IRandom`
  - `static class RandomExtensions { float Range(this IRandom, float min, float max); int RangeInt(this IRandom, int min, int maxInclusive); bool Chance(this IRandom, float probability); }`
  - `readonly record struct Hop(Vector3 From, Vector3 To, float Height, float Duration) { Vector3 Evaluate(float time); }`
  - `enum SparrowState { Idle, Hop, Forage, Preen, TakeOff, Fly, Land, Alarm }`
  - `enum SparrowAnim { Idle, Fly }`
  - `enum ChirpKind { Contact, Song, Alarm, Flight }`
  - `[Flags] enum SparrowTrigger { None = 0, TakeOff = 1, Alarm = 2, LongIdleStart = 4 }`
  - `readonly record struct SparrowInput(float Dt, Vector3 Target, float TargetYaw, Vector3 PlayerPosition, Vector3 PlayerVelocity, bool PlayerMounted)`
  - `readonly record struct SparrowPose(Vector3 Position, float Yaw, float Pitch, SparrowAnim Anim, SparrowState State, SparrowTrigger Triggers)`
  - `sealed class SparrowParams`(全プロパティは下のコード)
  - テスト用 `SequenceRandom(params float[] values) : IRandom`(値を順に繰り返し返す)

座標の約束: Y が上。yaw は FFXIV と同じで、0 のとき +Z を向き、前方ベクトルは `(sin yaw, 0, cos yaw)`。pitch は正で頭が下がる。

- [ ] **Step 1: プロジェクトを作る**

`src/Suzume.Core/Suzume.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

`tests/Suzume.Core.Tests/Suzume.Core.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Suzume.Core\Suzume.Core.csproj" />
  </ItemGroup>

</Project>
```

`Suzume.slnx`(全体を置き換え):

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/Suzume.Core/Suzume.Core.csproj" />
    <Project Path="src/Suzume/Suzume.csproj">
      <Platform Project="x64" />
    </Project>
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/Suzume.Core.Tests/Suzume.Core.Tests.csproj" />
  </Folder>
</Solution>
```

`tests/Suzume.Core.Tests/SequenceRandom.cs`:

```csharp
using Suzume.Core;

namespace Suzume.Core.Tests;

/// <summary>決めた値を順に繰り返し返す乱数(テスト用)。</summary>
internal sealed class SequenceRandom(params float[] values) : IRandom
{
    private int index;

    public float NextFloat() => values[index++ % values.Length];
}
```

- [ ] **Step 2: 失敗するテストを書く**

`tests/Suzume.Core.Tests/AnglesTests.cs`:

```csharp
using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class AnglesTests
{
    [Fact]
    public void YawTowardsPlusZIsZero() =>
        Assert.Equal(0f, Angles.YawTowards(Vector3.Zero, Vector3.UnitZ), 5);

    [Fact]
    public void YawTowardsPlusXIsHalfPi() =>
        Assert.Equal(MathF.PI / 2f, Angles.YawTowards(Vector3.Zero, Vector3.UnitX), 5);

    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(4f, 4f - MathF.Tau)]
    [InlineData(-4f, -4f + MathF.Tau)]
    [InlineData(20f, 20f - 3f * MathF.Tau)]
    public void WrapKeepsAngleWithinPi(float angle, float expected) =>
        Assert.Equal(expected, Angles.Wrap(angle), 4);

    [Fact]
    public void HorizontalDistanceIgnoresHeight() =>
        Assert.Equal(5f, Angles.HorizontalDistance(new Vector3(0f, 5f, 0f), new Vector3(3f, 0f, 4f)), 5);

    [Fact]
    public void DegToRadConverts() =>
        Assert.Equal(MathF.PI, Angles.DegToRad(180f), 5);
}
```

`tests/Suzume.Core.Tests/RandomTests.cs`:

```csharp
using Suzume.Core;

namespace Suzume.Core.Tests;

public class RandomTests
{
    [Theory]
    [InlineData(0f, 2)]
    [InlineData(0.5f, 4)]
    [InlineData(0.9999f, 6)]
    public void RangeIntCoversInclusiveBounds(float value, int expected) =>
        Assert.Equal(expected, new SequenceRandom(value).RangeInt(2, 6));

    [Fact]
    public void RangeScalesLinearly() =>
        Assert.Equal(1.5f, new SequenceRandom(0.25f).Range(1f, 3f), 5);

    [Fact]
    public void ChanceComparesAgainstProbability()
    {
        Assert.True(new SequenceRandom(0.1f).Chance(0.2f));
        Assert.False(new SequenceRandom(0.3f).Chance(0.2f));
    }

    [Fact]
    public void SeededRandomIsDeterministic()
    {
        var a = new SeededRandom(42);
        var b = new SeededRandom(42);
        for (var i = 0; i < 10; i++)
            Assert.Equal(a.NextFloat(), b.NextFloat());
    }
}
```

`tests/Suzume.Core.Tests/HopTests.cs`:

```csharp
using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class HopTests
{
    [Fact]
    public void StartsAndEndsAtEndpoints()
    {
        var hop = new Hop(Vector3.Zero, new Vector3(0.2f, 0f, 0f), 0.08f, 0.12f);
        Assert.Equal(Vector3.Zero, hop.Evaluate(0f));
        Assert.Equal(hop.To, hop.Evaluate(0.12f));
        Assert.Equal(hop.To, hop.Evaluate(1f));
    }

    [Fact]
    public void PeaksAtHeightHalfway()
    {
        var hop = new Hop(Vector3.Zero, new Vector3(0.2f, 0.1f, 0f), 0.08f, 0.12f);
        var mid = hop.Evaluate(0.06f);
        Assert.Equal(0.1f, mid.X, 4);
        Assert.Equal(0.05f + 0.08f, mid.Y, 4);
    }
}
```

- [ ] **Step 3: テストが失敗することを確認する**

Run: `cd /g/ff14/suzume && dotnet test tests/Suzume.Core.Tests`
Expected: ビルドエラー(`Angles`、`Hop`、`IRandom` などが見つからない)

- [ ] **Step 4: 実装を書く**

`src/Suzume.Core/Angles.cs`:

```csharp
using System.Numerics;

namespace Suzume.Core;

/// <summary>
/// 角度と水平距離の小さな関数。yaw は FFXIV と同じく 0 で +Z を向き、前方は (sin, 0, cos)。
/// </summary>
public static class Angles
{
    public static float YawTowards(Vector3 from, Vector3 to) =>
        MathF.Atan2(to.X - from.X, to.Z - from.Z);

    /// <summary>角度を -π〜π に収める。</summary>
    public static float Wrap(float angle) =>
        MathF.IEEERemainder(angle, MathF.Tau);

    public static float DegToRad(float degrees) => degrees * MathF.PI / 180f;

    public static Vector3 Horizontal(Vector3 v) => new(v.X, 0f, v.Z);

    public static float HorizontalDistance(Vector3 a, Vector3 b) => Horizontal(a - b).Length();
}
```

`src/Suzume.Core/Random.cs`:

```csharp
namespace Suzume.Core;

/// <summary>0 以上 1 未満の乱数。テストで差し替えられるようにインターフェースにしている。</summary>
public interface IRandom
{
    float NextFloat();
}

public sealed class SeededRandom(int seed) : IRandom
{
    private readonly Random random = new(seed);

    public float NextFloat() => random.NextSingle();
}

public static class RandomExtensions
{
    public static float Range(this IRandom random, float min, float max) =>
        min + (max - min) * random.NextFloat();

    /// <summary>min 以上 maxInclusive 以下の整数。</summary>
    public static int RangeInt(this IRandom random, int min, int maxInclusive)
    {
        var offset = (int)MathF.Floor(random.NextFloat() * (maxInclusive - min + 1));
        return Math.Min(maxInclusive, min + offset);
    }

    public static bool Chance(this IRandom random, float probability) =>
        random.NextFloat() < probability;
}
```

`src/Suzume.Core/Hop.cs`:

```csharp
using System.Numerics;

namespace Suzume.Core;

/// <summary>両足ホップ 1 回分の放物線。time は開始からの秒数。</summary>
public readonly record struct Hop(Vector3 From, Vector3 To, float Height, float Duration)
{
    public Vector3 Evaluate(float time)
    {
        var u = Math.Clamp(time / Duration, 0f, 1f);
        var point = Vector3.Lerp(From, To, u);
        point.Y += 4f * Height * u * (1f - u);
        return point;
    }
}
```

`src/Suzume.Core/SparrowTypes.cs`:

```csharp
using System.Numerics;

namespace Suzume.Core;

public enum SparrowState
{
    Idle,
    Hop,
    Forage,
    Preen,
    TakeOff,
    Fly,
    Land,
    Alarm,
}

/// <summary>ゲーム側で再生させるベースアニメーション。</summary>
public enum SparrowAnim
{
    Idle,
    Fly,
}

public enum ChirpKind
{
    /// <summary>「チュン」地鳴き</summary>
    Contact,
    /// <summary>「チュンチュン」囀り</summary>
    Song,
    /// <summary>「ジュクジュク」警戒</summary>
    Alarm,
    /// <summary>「チュリリ」飛び立ち</summary>
    Flight,
}

/// <summary>その Tick で起きた出来事。鳴き声のきっかけに使う。</summary>
[Flags]
public enum SparrowTrigger
{
    None = 0,
    TakeOff = 1,
    Alarm = 2,
    LongIdleStart = 4,
}

/// <param name="Target">ゲームの AI が決めたミニオンの論理位置(地面の高さ込み)</param>
/// <param name="TargetYaw">論理位置での向き。置き直すときに使う</param>
public readonly record struct SparrowInput(
    float Dt,
    Vector3 Target,
    float TargetYaw,
    Vector3 PlayerPosition,
    Vector3 PlayerVelocity,
    bool PlayerMounted);

/// <param name="Position">見た目の位置</param>
/// <param name="Pitch">前傾角(ラジアン、正で頭が下がる)</param>
public readonly record struct SparrowPose(
    Vector3 Position,
    float Yaw,
    float Pitch,
    SparrowAnim Anim,
    SparrowState State,
    SparrowTrigger Triggers);
```

`src/Suzume.Core/SparrowParams.cs`:

```csharp
namespace Suzume.Core;

/// <summary>スズメの行動パラメータ。距離は yalm、時間は秒、角度は度。</summary>
public sealed class SparrowParams
{
    // ホップ
    public float HopDuration { get; set; } = 0.12f;
    public float HopDistanceMin { get; set; } = 0.15f;
    public float HopDistanceMax { get; set; } = 0.30f;
    public float HopHeightMin { get; set; } = 0.06f;
    public float HopHeightMax { get; set; } = 0.10f;
    public float HopGapMin { get; set; } = 0.05f;
    public float HopGapMax { get; set; } = 0.15f;
    public int HopsPerBoutMin { get; set; } = 2;
    public int HopsPerBoutMax { get; set; } = 6;

    // 待機・キョロキョロ
    public float IdleMin { get; set; } = 0.3f;
    public float IdleMax { get; set; } = 1.5f;
    public float LongIdleThreshold { get; set; } = 1.0f;
    public float ScanIntervalMin { get; set; } = 0.5f;
    public float ScanIntervalMax { get; set; } = 1.0f;
    public float ScanAngleMinDeg { get; set; } = 20f;
    public float ScanAngleMaxDeg { get; set; } = 70f;
    public float ArriveDistance { get; set; } = 0.4f;
    public float StepHeight { get; set; } = 0.3f;

    // 採餌・羽繕い
    public float ForageChance { get; set; } = 0.15f;
    public float PreenChance { get; set; } = 0.03f;
    public int PecksMin { get; set; } = 2;
    public int PecksMax { get; set; } = 5;
    public float PeckDuration { get; set; } = 0.15f;
    public float PeckPitchDeg { get; set; } = 35f;
    public float PreenDurationMin { get; set; } = 1.0f;
    public float PreenDurationMax { get; set; } = 2.0f;

    // 飛行
    public float FlyDistance { get; set; } = 6f;
    public float FastFlyDistance { get; set; } = 1.5f;
    public float SprintSpeed { get; set; } = 7.5f;
    public float FlyHeightTrigger { get; set; } = 2f;
    public float TakeOffDuration { get; set; } = 0.25f;
    public float TakeOffClimbSpeed { get; set; } = 2f;
    public float FlySpeed { get; set; } = 8f;
    public float FlapDuration { get; set; } = 0.3f;
    public float GlideDuration { get; set; } = 0.2f;
    public float FlapClimbSpeed { get; set; } = 1.2f;
    public float GlideSinkSpeed { get; set; } = 1.0f;
    public float FlyAltitudeMin { get; set; } = 0.5f;
    public float FlyAltitudeMax { get; set; } = 2.0f;
    public float LandDistance { get; set; } = 1.0f;
    public float LandDuration { get; set; } = 0.25f;
    public float LandSettleDuration { get; set; } = 0.15f;
    public float LandPitchDeg { get; set; } = 15f;
    public float ResetDistance { get; set; } = 30f;

    // 警戒
    public float AlarmDistance { get; set; } = 2f;
    public float AlarmPlayerSpeed { get; set; } = 5f;
    public float AlarmCooldown { get; set; } = 10f;
    public int AlarmHopsMin { get; set; } = 1;
    public int AlarmHopsMax { get; set; } = 3;

    // 鳴き声
    public float ChirpMinInterval { get; set; } = 1.5f;
    public float ChirpFrequency { get; set; } = 1.0f;
    public float ContactRatePerSecond { get; set; } = 0.15f;
    public float SongChance { get; set; } = 0.3f;
}
```

- [ ] **Step 5: テストが通ることを確認する**

Run: `cd /g/ff14/suzume && dotnet test tests/Suzume.Core.Tests`
Expected: 全テスト PASS

- [ ] **Step 6: コミット**

```bash
cd /g/ff14/suzume && git add Suzume.slnx src/Suzume.Core tests && git commit -m "feat: Core の基盤 (角度・乱数・ホップ・型・パラメータ)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: SparrowBrain(行動の状態機械)

**Files:**
- Create: `src/Suzume.Core/SparrowBrain.cs`
- Test: `tests/Suzume.Core.Tests/SparrowBrainTests.cs`

**Interfaces:**
- Consumes: Task 2 のすべての型
- Produces: `sealed class SparrowBrain(SparrowParams parameters, IRandom random) { SparrowPose Tick(in SparrowInput input); void Invalidate(); }`

振る舞いの要点(spec 4.2〜4.4):
- 初回・`Invalidate()` 後・論理位置との距離が `ResetDistance` 超 → 論理位置に置き直して Idle(その Tick は動かない)
- フレーム時間は 0.1 秒で頭打ち
- Idle 中は 0.5〜1.0 秒ごとに yaw を 20〜70° 一瞬で変える(キョロキョロ)。Idle が終わったら、離れていれば Hop、そうでなければ確率で Forage / Preen / 次の Idle
- Hop 中、yaw はホップとホップの間にだけ変わる
- 地上にいる間は毎 Tick、警戒 → 飛び立ちの順に割り込みを確認する

- [ ] **Step 1: 失敗するテストを書く**

`tests/Suzume.Core.Tests/SparrowBrainTests.cs`:

```csharp
using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class SparrowBrainTests
{
    private const float Dt = 1f / 60f;

    private static SparrowBrain NewBrain(SparrowParams? parameters = null, int seed = 1) =>
        new(parameters ?? new SparrowParams(), new SeededRandom(seed));

    // プレイヤーは既定で論理位置の 3y 後ろに静止している(警戒の範囲外)
    private static SparrowInput Input(
        Vector3 target,
        Vector3? playerPosition = null,
        Vector3? playerVelocity = null,
        bool mounted = false) =>
        new(Dt, target, 0f, playerPosition ?? target + new Vector3(0f, 0f, -3f), playerVelocity ?? Vector3.Zero, mounted);

    private static List<SparrowPose> Run(SparrowBrain brain, SparrowInput input, float seconds)
    {
        var poses = new List<SparrowPose>();
        var frames = (int)MathF.Round(seconds / input.Dt);
        for (var i = 0; i < frames; i++)
            poses.Add(brain.Tick(input));
        return poses;
    }

    [Fact]
    public void FirstTickStartsAtTargetInIdle()
    {
        var target = new Vector3(10f, 2f, -5f);
        var pose = NewBrain().Tick(Input(target));
        Assert.Equal(target, pose.Position);
        Assert.Equal(SparrowState.Idle, pose.State);
        Assert.Equal(SparrowAnim.Idle, pose.Anim);
    }

    [Fact]
    public void MovesToNearbyTargetByHoppingWithPauses()
    {
        var p = new SparrowParams();
        var brain = NewBrain(p);
        brain.Tick(Input(Vector3.Zero));
        var target = new Vector3(2f, 0f, 0f);

        var poses = Run(brain, Input(target), 15f);

        Assert.Contains(poses, x => x.State == SparrowState.Hop);
        Assert.DoesNotContain(poses, x => x.Anim == SparrowAnim.Fly);
        Assert.True(poses.Max(x => x.Position.Y) > 0.05f, "ホップで体が浮くこと");
        Assert.True(Vector3.Distance(poses[^1].Position with { Y = 0f }, target) <= p.ArriveDistance + p.HopDistanceMax);
        // 滑るように動かず、移動の途中で止まっているフレームがある
        var stillFramesWhileFar = poses.Zip(poses.Skip(1)).Count(pair =>
            pair.First.Position == pair.Second.Position
            && Vector3.Distance(pair.First.Position, target) > p.ArriveDistance + 0.5f);
        Assert.True(stillFramesWhileFar > 10);
    }

    [Fact]
    public void YawNeverChangesMidHop()
    {
        var brain = NewBrain();
        brain.Tick(Input(Vector3.Zero));

        var poses = Run(brain, Input(new Vector3(0f, 0f, 3f)), 10f);

        foreach (var (prev, next) in poses.Zip(poses.Skip(1)))
        {
            var midAir = prev.Position.Y > 1e-4f && next.Position.Y > 1e-4f;
            if (midAir)
                Assert.Equal(prev.Yaw, next.Yaw);
        }
    }

    [Fact]
    public void LooksAroundWithInstantTurnsWhileIdle()
    {
        var p = new SparrowParams { ForageChance = 0f, PreenChance = 0f };
        var brain = NewBrain(p);

        var poses = Run(brain, Input(Vector3.Zero), 5f);

        var minSnap = Angles.DegToRad(p.ScanAngleMinDeg) - 1e-3f;
        var snaps = poses.Zip(poses.Skip(1)).Count(pair =>
            MathF.Abs(Angles.Wrap(pair.Second.Yaw - pair.First.Yaw)) >= minSnap);
        Assert.True(snaps >= 3, $"snaps={snaps}");
        Assert.All(poses, x => Assert.Equal(Vector3.Zero, x.Position));
    }

    [Fact]
    public void ForagingPecksDownAndRecovers()
    {
        var p = new SparrowParams { ForageChance = 1f };
        var brain = NewBrain(p);

        var poses = Run(brain, Input(Vector3.Zero), 4f);

        Assert.Contains(poses, x => x.State == SparrowState.Forage && x.Pitch > Angles.DegToRad(20f));
        Assert.All(poses.Where(x => x.State == SparrowState.Idle), x =>
            Assert.True(x.Pitch <= Angles.DegToRad(p.LandPitchDeg) + 1e-4f));
    }

    [Fact]
    public void StaysPutAndKeepsYawWrappedOverLongIdle()
    {
        var brain = NewBrain();

        var poses = Run(brain, Input(Vector3.Zero), 120f);

        Assert.All(poses, x =>
        {
            Assert.InRange(x.Yaw, -MathF.PI, MathF.PI);
            Assert.True(Vector3.Distance(x.Position, Vector3.Zero) < 0.2f);
        });
    }

    [Fact]
    public void HugeFrameDeltaDoesNotTeleport()
    {
        var p = new SparrowParams();
        var brain = NewBrain(p);
        brain.Tick(Input(Vector3.Zero));
        var target = new Vector3(3f, 0f, 0f);

        var before = brain.Tick(Input(target)).Position;
        var after = brain.Tick(Input(target) with { Dt = 5f }).Position;

        Assert.True(Vector3.Distance(before, after) <= p.HopDistanceMax + 1e-3f);
    }

    [Fact]
    public void HopsUpOntoSmallStep()
    {
        var brain = NewBrain();
        brain.Tick(Input(Vector3.Zero));
        var target = new Vector3(0.2f, 0.5f, 0f);

        var poses = Run(brain, Input(target), 10f);

        Assert.Equal(0.5f, poses[^1].Position.Y, 3);
        Assert.DoesNotContain(poses, x => x.Anim == SparrowAnim.Fly);
    }

    [Fact]
    public void FliesToDistantTargetThenLands()
    {
        var p = new SparrowParams();
        var brain = NewBrain(p);
        brain.Tick(Input(Vector3.Zero));
        var target = new Vector3(10f, 0f, 0f);

        var poses = Run(brain, Input(target), 8f);

        Assert.True(poses[0].Triggers.HasFlag(SparrowTrigger.TakeOff));
        Assert.Equal(SparrowAnim.Fly, poses[0].Anim);
        Assert.True(poses.Max(x => x.Position.Y) >= p.FlyAltitudeMin);
        Assert.Contains(poses, x => x.State == SparrowState.Land);

        var flightYs = poses.Where(x => x.State == SparrowState.Fly).Select(x => x.Position.Y).ToList();
        var pairs = flightYs.Zip(flightYs.Skip(1)).ToList();
        Assert.True(pairs.Any(pair => pair.Second > pair.First) && pairs.Any(pair => pair.Second < pair.First),
            "波状飛行で上下すること");

        var final = poses[^1];
        Assert.Equal(SparrowAnim.Idle, final.Anim);
        Assert.Equal(0f, final.Position.Y, 3);
        Assert.True(Vector3.Distance(final.Position, target) <= p.ArriveDistance + p.HopDistanceMax);
    }

    [Fact]
    public void SnapsToTargetAfterTeleport()
    {
        var brain = NewBrain();
        brain.Tick(Input(Vector3.Zero));
        var far = new Vector3(100f, 5f, 0f);

        var pose = brain.Tick(Input(far));

        Assert.Equal(far, pose.Position);
        Assert.Equal(SparrowState.Idle, pose.State);
    }

    [Fact]
    public void InvalidateSnapsToTargetOnNextTick()
    {
        var brain = NewBrain();
        brain.Tick(Input(Vector3.Zero));
        brain.Invalidate();
        var target = new Vector3(3f, 0f, 0f);

        Assert.Equal(target, brain.Tick(Input(target)).Position);
    }

    [Fact]
    public void KeepsUpWithFastMountWithoutResetting()
    {
        var p = new SparrowParams();
        var brain = NewBrain(p);
        brain.Tick(Input(Vector3.Zero));
        var velocity = new Vector3(20f, 0f, 0f);
        var maxGap = 0f;

        for (var i = 1; i <= 600; i++)
        {
            var target = velocity * (i * Dt);
            var pose = brain.Tick(Input(target, target + new Vector3(1.5f, 0f, 0f), velocity, mounted: true));
            maxGap = MathF.Max(maxGap, Angles.HorizontalDistance(pose.Position, target));
        }

        Assert.True(maxGap < p.ResetDistance * 0.5f, $"maxGap={maxGap}");
    }

    [Fact]
    public void FleesWithAlarmWhenPlayerRushesIn()
    {
        var brain = NewBrain();
        brain.Tick(Input(Vector3.Zero));
        var rush = Input(Vector3.Zero, new Vector3(-1.5f, 0f, 0f), new Vector3(6f, 0f, 0f));

        var first = brain.Tick(rush);
        Assert.Equal(SparrowState.Alarm, first.State);
        Assert.True(first.Triggers.HasFlag(SparrowTrigger.Alarm));

        var poses = Run(brain, rush, 1f);
        Assert.DoesNotContain(poses, x => x.Triggers.HasFlag(SparrowTrigger.Alarm)); // クールダウン中
        Assert.True(poses.Max(x => x.Position.X) > 0.1f, "プレイヤーと反対 (+X) へ逃げること");
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `cd /g/ff14/suzume && dotnet test tests/Suzume.Core.Tests --filter SparrowBrainTests`
Expected: ビルドエラー(`SparrowBrain` が見つからない)

- [ ] **Step 3: 実装を書く**

`src/Suzume.Core/SparrowBrain.cs`:

```csharp
using System.Numerics;

namespace Suzume.Core;

/// <summary>
/// 実際のスズメ(地上は両足ホップ、遠くへは波状飛行)を真似る状態機械。
/// ゲームの論理位置 (Target) を、ホップや飛行で追いかける「見た目の位置」を計算する。
/// </summary>
public sealed class SparrowBrain
{
    // ロード明けなどの巨大なフレーム時間で瞬間移動しないように抑える
    private const float MaxDt = 0.1f;
    private const float Epsilon = 1e-3f;

    private readonly SparrowParams p;
    private readonly IRandom rng;

    private bool initialized;
    private Vector3 position;
    private float yaw;
    private float pitch;
    private SparrowState state;
    private SparrowAnim anim;
    private SparrowTrigger triggers;
    private float stateTime;
    private float stateDuration;

    // Idle
    private float scanCountdown;
    private bool settling;

    // Hop / Alarm
    private int hopsLeft;
    private Hop? hop;
    private float hopTime;
    private float hopGapLeft;
    private Vector3 fleeDirection;

    // Forage / Preen
    private int pecks;
    private float preenBaseYaw;

    // TakeOff / Fly / Land
    private bool flapping;
    private float flightPhaseLeft;
    private float groundY;
    private float altitude;
    private float landStartAltitude;

    private float alarmCooldown;

    public SparrowBrain(SparrowParams parameters, IRandom random)
    {
        p = parameters;
        rng = random;
    }

    /// <summary>次の Tick で論理位置へ置き直す(ミニオンの再召喚・一時停止明け)。</summary>
    public void Invalidate() => initialized = false;

    public SparrowPose Tick(in SparrowInput input)
    {
        triggers = SparrowTrigger.None;
        var dt = Math.Clamp(input.Dt, 0f, MaxDt);

        if (!initialized || Vector3.Distance(position, input.Target) > p.ResetDistance)
        {
            Reset(input);
            return CurrentPose();
        }

        stateTime += dt;
        alarmCooldown = MathF.Max(0f, alarmCooldown - dt);

        switch (state)
        {
            case SparrowState.Idle:
                TickIdle(input, dt);
                break;
            case SparrowState.Hop:
            case SparrowState.Alarm:
                TickHop(input, dt);
                break;
            case SparrowState.Forage:
                TickForage(input);
                break;
            case SparrowState.Preen:
                TickPreen(input);
                break;
            case SparrowState.TakeOff:
                TickTakeOff(dt);
                break;
            case SparrowState.Fly:
                TickFly(input, dt);
                break;
            case SparrowState.Land:
                TickLand(input, dt);
                break;
        }

        yaw = Angles.Wrap(yaw);
        return CurrentPose();
    }

    private SparrowPose CurrentPose() => new(position, yaw, pitch, anim, state, triggers);

    private void Reset(in SparrowInput input)
    {
        initialized = true;
        position = input.Target;
        yaw = Angles.Wrap(input.TargetYaw);
        hop = null;
        alarmCooldown = 0f;
        scanCountdown = rng.Range(p.ScanIntervalMin, p.ScanIntervalMax);
        EnterIdle();
    }

    private void SetState(SparrowState next)
    {
        state = next;
        stateTime = 0f;
    }

    // ---- 割り込み ----

    /// <summary>地上にいる間の割り込み(警戒・飛び立ち)。遷移したら true。</summary>
    private bool TryInterrupt(in SparrowInput input)
    {
        if (ShouldAlarm(input))
        {
            EnterAlarm(input);
            return true;
        }

        if (ShouldFly(input))
        {
            EnterTakeOff(input);
            return true;
        }

        return false;
    }

    private bool ShouldFly(in SparrowInput input)
    {
        var gap = Angles.HorizontalDistance(position, input.Target);
        var fast = input.PlayerMounted || Angles.Horizontal(input.PlayerVelocity).Length() > p.SprintSpeed;
        return gap > p.FlyDistance
            || (fast && gap > p.FastFlyDistance)
            || MathF.Abs(input.Target.Y - position.Y) > p.FlyHeightTrigger;
    }

    /// <summary>プレイヤーが速く、こちらへ向かって近づいてきたら警戒する。</summary>
    private bool ShouldAlarm(in SparrowInput input)
    {
        if (alarmCooldown > 0f)
            return false;

        var toBird = Angles.Horizontal(position - input.PlayerPosition);
        var velocity = Angles.Horizontal(input.PlayerVelocity);
        var distance = toBird.Length();
        var speed = velocity.Length();
        if (distance > p.AlarmDistance || distance < Epsilon || speed < p.AlarmPlayerSpeed)
            return false;

        return Vector3.Dot(velocity / speed, toBird / distance) > 0.7f;
    }

    private bool NeedsToMove(in SparrowInput input) =>
        Angles.HorizontalDistance(position, input.Target) > p.ArriveDistance
        || MathF.Abs(input.Target.Y - position.Y) > p.StepHeight;

    // ---- Idle ----

    private void EnterIdle(bool afterLanding = false)
    {
        SetState(SparrowState.Idle);
        anim = SparrowAnim.Idle;
        pitch = 0f;
        settling = afterLanding;
        stateDuration = rng.Range(p.IdleMin, p.IdleMax);
        if (stateDuration >= p.LongIdleThreshold)
            triggers |= SparrowTrigger.LongIdleStart;
    }

    private void TickIdle(in SparrowInput input, float dt)
    {
        if (TryInterrupt(input))
            return;

        // 着地直後は尾を上げるように少し前傾する(v2 で尾のボーン操作に置き換える)
        pitch = settling && stateTime < p.LandSettleDuration
            ? Angles.DegToRad(p.LandPitchDeg) * MathF.Sin(MathF.PI * stateTime / p.LandSettleDuration)
            : 0f;

        scanCountdown -= dt;
        if (scanCountdown <= 0f)
        {
            // キョロキョロ: 補間せず一瞬で向きを変える
            var sign = rng.Chance(0.5f) ? 1f : -1f;
            yaw += sign * Angles.DegToRad(rng.Range(p.ScanAngleMinDeg, p.ScanAngleMaxDeg));
            scanCountdown = rng.Range(p.ScanIntervalMin, p.ScanIntervalMax);
        }

        if (stateTime < stateDuration)
            return;

        if (NeedsToMove(input))
            EnterHop(input);
        else if (rng.Chance(p.ForageChance))
            EnterForage();
        else if (rng.Chance(p.PreenChance))
            EnterPreen();
        else
            EnterIdle();
    }

    // ---- Hop / Alarm ----

    private void EnterHop(in SparrowInput input)
    {
        SetState(SparrowState.Hop);
        anim = SparrowAnim.Idle;
        pitch = 0f;
        hopsLeft = rng.RangeInt(p.HopsPerBoutMin, p.HopsPerBoutMax);
        StartHop(input);
    }

    private void EnterAlarm(in SparrowInput input)
    {
        SetState(SparrowState.Alarm);
        anim = SparrowAnim.Idle;
        pitch = 0f;
        triggers |= SparrowTrigger.Alarm;
        alarmCooldown = p.AlarmCooldown;
        fleeDirection = Vector3.Normalize(Angles.Horizontal(position - input.PlayerPosition));
        hopsLeft = rng.RangeInt(p.AlarmHopsMin, p.AlarmHopsMax);
        StartHop(input);
    }

    private void StartHop(in SparrowInput input)
    {
        var distance = rng.Range(p.HopDistanceMin, p.HopDistanceMax);
        Vector3 to;
        if (state == SparrowState.Alarm)
        {
            to = position + fleeDirection * distance;
        }
        else
        {
            var delta = Angles.Horizontal(input.Target - position);
            var gap = delta.Length();
            if (gap <= distance)
            {
                to = input.Target;
            }
            else
            {
                to = position + delta / gap * distance;
                to.Y = position.Y + (input.Target.Y - position.Y) * (distance / gap);
            }
        }

        // 向きはホップの前に一瞬で変える。跳んでいる間は変えない
        if (Angles.HorizontalDistance(position, to) > Epsilon)
            yaw = Angles.YawTowards(position, to);

        hop = new Hop(position, to, rng.Range(p.HopHeightMin, p.HopHeightMax), p.HopDuration);
        hopTime = 0f;
    }

    private void TickHop(in SparrowInput input, float dt)
    {
        if (hop is { } current)
        {
            hopTime += dt;
            position = current.Evaluate(hopTime);
            if (hopTime < current.Duration)
                return;

            position = current.To;
            hop = null;
            hopsLeft--;
            hopGapLeft = rng.Range(p.HopGapMin, p.HopGapMax);
            return;
        }

        hopGapLeft -= dt;
        if (hopGapLeft > 0f)
            return;

        var fleeing = state == SparrowState.Alarm;
        if (fleeing)
        {
            if (ShouldFly(input))
            {
                EnterTakeOff(input);
                return;
            }
        }
        else if (TryInterrupt(input))
        {
            return;
        }

        if (hopsLeft > 0 && (fleeing || NeedsToMove(input)))
            StartHop(input);
        else
            EnterIdle();
    }

    // ---- Forage / Preen ----

    private void EnterForage()
    {
        SetState(SparrowState.Forage);
        pecks = rng.RangeInt(p.PecksMin, p.PecksMax);
    }

    private void TickForage(in SparrowInput input)
    {
        if (TryInterrupt(input))
            return;

        if (stateTime >= pecks * p.PeckDuration)
        {
            EnterIdle();
            return;
        }

        // ついばみ: 1 回ごとに頭を下げて戻す
        var u = stateTime % p.PeckDuration / p.PeckDuration;
        pitch = Angles.DegToRad(p.PeckPitchDeg) * MathF.Sin(MathF.PI * u);
    }

    private void EnterPreen()
    {
        SetState(SparrowState.Preen);
        stateDuration = rng.Range(p.PreenDurationMin, p.PreenDurationMax);
        preenBaseYaw = yaw;
    }

    private void TickPreen(in SparrowInput input)
    {
        if (TryInterrupt(input))
            return;

        if (stateTime >= stateDuration)
        {
            yaw = preenBaseYaw;
            EnterIdle();
            return;
        }

        // 羽繕いの代用: 小刻みに体を揺らす(v2 でボーン操作に置き換える)
        pitch = Angles.DegToRad(10f) * MathF.Sin(stateTime * MathF.Tau * 3f);
        yaw = preenBaseYaw + 0.2f * MathF.Sin(stateTime * MathF.Tau * 1.5f);
    }

    // ---- TakeOff / Fly / Land ----

    private void EnterTakeOff(in SparrowInput input)
    {
        SetState(SparrowState.TakeOff);
        anim = SparrowAnim.Fly;
        pitch = 0f;
        hop = null;
        triggers |= SparrowTrigger.TakeOff;
        groundY = position.Y;
        altitude = 0f;
        if (Angles.HorizontalDistance(position, input.Target) > Epsilon)
            yaw = Angles.YawTowards(position, input.Target);
    }

    private void TickTakeOff(float dt)
    {
        altitude += p.TakeOffClimbSpeed * dt;
        position.Y = groundY + altitude;
        if (stateTime < p.TakeOffDuration)
            return;

        SetState(SparrowState.Fly);
        flapping = true;
        flightPhaseLeft = p.FlapDuration;
    }

    private void TickFly(in SparrowInput input, float dt)
    {
        // 波状飛行: 羽ばたいて上昇 → 翼を畳んで滑空・下降 を繰り返す
        flightPhaseLeft -= dt;
        if (flightPhaseLeft <= 0f)
        {
            flapping = !flapping;
            flightPhaseLeft = flapping ? p.FlapDuration : p.GlideDuration;
        }

        altitude += (flapping ? p.FlapClimbSpeed : -p.GlideSinkSpeed) * dt;
        altitude = Math.Clamp(altitude, p.FlyAltitudeMin, p.FlyAltitudeMax);

        // 騎乗中などで論理位置が速く動くときは、それより速く飛ぶ
        var speed = MathF.Max(p.FlySpeed, Angles.Horizontal(input.PlayerVelocity).Length() * 1.3f);
        MoveHorizontallyTowards(input.Target, speed * dt);
        position.Y = groundY + altitude;

        if (Angles.HorizontalDistance(position, input.Target) < p.LandDistance)
        {
            SetState(SparrowState.Land);
            landStartAltitude = altitude;
        }
    }

    private void TickLand(in SparrowInput input, float dt)
    {
        var u = MathF.Min(1f, stateTime / p.LandDuration);
        altitude = landStartAltitude * (1f - u);
        MoveHorizontallyTowards(input.Target, p.FlySpeed * 0.5f * dt);
        position.Y = groundY + altitude;

        if (u >= 1f)
            EnterIdle(afterLanding: true);
    }

    /// <summary>水平に step だけ進み、地面の高さも進んだ割合だけ目標に近づける。</summary>
    private void MoveHorizontallyTowards(Vector3 target, float step)
    {
        var delta = Angles.Horizontal(target - position);
        var gap = delta.Length();
        if (gap <= Epsilon)
            return;

        yaw = Angles.YawTowards(position, target);
        step = MathF.Min(step, gap);
        groundY += (target.Y - groundY) * (step / gap);
        position += delta / gap * step;
    }
}
```

- [ ] **Step 4: テストが通ることを確認する**

Run: `cd /g/ff14/suzume && dotnet test tests/Suzume.Core.Tests`
Expected: 全テスト PASS。失敗したテストがあれば、テストの期待値ではなく実装側を spec 4.2〜4.4 に合わせて直す

- [ ] **Step 5: コミット**

```bash
cd /g/ff14/suzume && git add src/Suzume.Core/SparrowBrain.cs tests/Suzume.Core.Tests/SparrowBrainTests.cs && git commit -m "feat: スズメの行動状態機械 (ホップ・キョロキョロ・ついばみ・波状飛行・警戒)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: 鳴き声のスケジューラと距離減衰

**Files:**
- Create: `src/Suzume.Core/ChirpScheduler.cs`, `src/Suzume.Core/ChirpVolume.cs`
- Test: `tests/Suzume.Core.Tests/ChirpTests.cs`

**Interfaces:**
- Consumes: `SparrowParams`、`IRandom`、`SparrowState`、`SparrowTrigger`、`ChirpKind`
- Produces:
  - `sealed class ChirpScheduler(SparrowParams parameters, IRandom random) { ChirpKind? Update(float dt, SparrowState state, SparrowTrigger triggers); }`
  - `static class ChirpVolume { float Compute(float baseVolume, float distance); }`(5y 以内は等倍、30y で 0、その間は直線)

ルール:
- 警戒 (`Alarm` トリガー) と飛び立ち (`TakeOff` トリガー) は、最小間隔に関係なく必ず鳴らし、間隔をリセットする
- それ以外は最小間隔 `ChirpMinInterval` を守る。`ChirpFrequency` が 0 なら鳴らさない
- 長めの停止の開始 (`LongIdleStart`) で、確率 `SongChance × ChirpFrequency` で囀り
- Idle / Hop / Forage 中は、毎秒 `ContactRatePerSecond × ChirpFrequency` の割合で地鳴き。飛行中は鳴かない

- [ ] **Step 1: 失敗するテストを書く**

`tests/Suzume.Core.Tests/ChirpTests.cs`:

```csharp
using Suzume.Core;

namespace Suzume.Core.Tests;

public class ChirpSchedulerTests
{
    private const float Dt = 1f / 60f;

    [Fact]
    public void AlarmAndFlightIgnoreCooldown()
    {
        var scheduler = new ChirpScheduler(new SparrowParams(), new SequenceRandom(0f));
        Assert.Equal(ChirpKind.Contact, scheduler.Update(Dt, SparrowState.Idle, SparrowTrigger.None));
        Assert.Equal(ChirpKind.Alarm, scheduler.Update(Dt, SparrowState.Alarm, SparrowTrigger.Alarm));
        Assert.Equal(ChirpKind.Flight, scheduler.Update(Dt, SparrowState.TakeOff, SparrowTrigger.TakeOff));
    }

    [Fact]
    public void RandomChirpsRespectMinimumInterval()
    {
        var p = new SparrowParams { ContactRatePerSecond = 50f };
        var scheduler = new ChirpScheduler(p, new SeededRandom(3));
        var times = new List<float>();

        for (var i = 0; i < 60 * 60; i++)
        {
            if (scheduler.Update(Dt, SparrowState.Idle, SparrowTrigger.None) is not null)
                times.Add(i * Dt);
        }

        Assert.True(times.Count > 10);
        Assert.All(times.Zip(times.Skip(1)), pair =>
            Assert.True(pair.Second - pair.First >= p.ChirpMinInterval - Dt));
    }

    [Fact]
    public void ZeroFrequencySilencesRandomChirps()
    {
        var scheduler = new ChirpScheduler(new SparrowParams { ChirpFrequency = 0f }, new SequenceRandom(0f));
        for (var i = 0; i < 600; i++)
            Assert.Null(scheduler.Update(Dt, SparrowState.Idle, SparrowTrigger.LongIdleStart));
    }

    [Fact]
    public void LongIdleMaySing()
    {
        var scheduler = new ChirpScheduler(new SparrowParams { SongChance = 1f }, new SequenceRandom(0f));
        Assert.Equal(ChirpKind.Song, scheduler.Update(Dt, SparrowState.Idle, SparrowTrigger.LongIdleStart));
    }

    [Fact]
    public void SilentWhileFlying()
    {
        var scheduler = new ChirpScheduler(new SparrowParams { ContactRatePerSecond = 1000f }, new SequenceRandom(0f));
        Assert.Null(scheduler.Update(Dt, SparrowState.Fly, SparrowTrigger.None));
    }
}

public class ChirpVolumeTests
{
    [Theory]
    [InlineData(0f, 0.6f)]
    [InlineData(5f, 0.6f)]
    [InlineData(17.5f, 0.3f)]
    [InlineData(30f, 0f)]
    [InlineData(100f, 0f)]
    public void AttenuatesLinearlyBetween5And30(float distance, float expected) =>
        Assert.Equal(expected, ChirpVolume.Compute(0.6f, distance), 4);

    [Fact]
    public void ClampsBaseVolume() =>
        Assert.Equal(1f, ChirpVolume.Compute(3f, 0f), 4);
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `cd /g/ff14/suzume && dotnet test tests/Suzume.Core.Tests --filter "ChirpSchedulerTests|ChirpVolumeTests"`
Expected: ビルドエラー(`ChirpScheduler`、`ChirpVolume` が見つからない)

- [ ] **Step 3: 実装を書く**

`src/Suzume.Core/ChirpScheduler.cs`:

```csharp
namespace Suzume.Core;

/// <summary>行動に合わせて、いつ・どの鳴き声を鳴らすかを決める。鳴きすぎないよう最小間隔で間引く。</summary>
public sealed class ChirpScheduler
{
    private readonly SparrowParams p;
    private readonly IRandom rng;
    private float cooldown;

    public ChirpScheduler(SparrowParams parameters, IRandom random)
    {
        p = parameters;
        rng = random;
    }

    public ChirpKind? Update(float dt, SparrowState state, SparrowTrigger triggers)
    {
        cooldown = MathF.Max(0f, cooldown - dt);

        // 行動に結びついた鳴き声は間隔に関係なく鳴らす
        if (triggers.HasFlag(SparrowTrigger.Alarm))
            return Emit(ChirpKind.Alarm);
        if (triggers.HasFlag(SparrowTrigger.TakeOff))
            return Emit(ChirpKind.Flight);

        if (cooldown > 0f || p.ChirpFrequency <= 0f)
            return null;

        if (triggers.HasFlag(SparrowTrigger.LongIdleStart)
            && rng.Chance(MathF.Min(1f, p.SongChance * p.ChirpFrequency)))
            return Emit(ChirpKind.Song);

        if (state is SparrowState.Idle or SparrowState.Hop or SparrowState.Forage)
        {
            var chance = 1f - MathF.Exp(-p.ContactRatePerSecond * p.ChirpFrequency * dt);
            if (rng.Chance(chance))
                return Emit(ChirpKind.Contact);
        }

        return null;
    }

    private ChirpKind Emit(ChirpKind kind)
    {
        cooldown = p.ChirpMinInterval;
        return kind;
    }
}
```

`src/Suzume.Core/ChirpVolume.cs`:

```csharp
namespace Suzume.Core;

/// <summary>プレイヤーとの距離による音量の減衰。</summary>
public static class ChirpVolume
{
    public const float FullVolumeDistance = 5f;
    public const float SilentDistance = 30f;

    public static float Compute(float baseVolume, float distance)
    {
        var attenuation = Math.Clamp(
            (SilentDistance - distance) / (SilentDistance - FullVolumeDistance), 0f, 1f);
        return Math.Clamp(baseVolume, 0f, 1f) * attenuation;
    }
}
```

- [ ] **Step 4: テストが通ることを確認する**

Run: `cd /g/ff14/suzume && dotnet test tests/Suzume.Core.Tests`
Expected: 全テスト PASS

- [ ] **Step 5: コミット**

```bash
cd /g/ff14/suzume && git add src/Suzume.Core/ChirpScheduler.cs src/Suzume.Core/ChirpVolume.cs tests/Suzume.Core.Tests/ChirpTests.cs && git commit -m "feat: 鳴き声のスケジューラと距離減衰

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: 鳴き声の合成(Python)

**Files:**
- Create: `tools/check_chirps.py`, `tools/chirps.py`
- Create(生成物): `assets/sounds/{contact,song,alarm,flight}_{1,2,3}.wav`

**Interfaces:**
- Produces: WAV 12 個(Global Constraints の命名・形式)。Task 6 が埋め込みリソースとして読む

検証の基準(spec 4.5):

| 種類 | 長さ | ピーク周波数 |
|---|---|---|
| contact | 0.07〜0.13 秒 | 2.5〜5.5kHz |
| song | 0.15〜0.9 秒 | 2.5〜5.5kHz |
| alarm | 0.35〜0.65 秒 | 2.5〜7.5kHz |
| flight | 0.2〜0.4 秒 | 2.5〜6kHz |

すべて振幅のピークが 0.5〜0.9。

- [ ] **Step 1: 検証スクリプトを書く(失敗するテストに相当)**

`tools/check_chirps.py`:

```python
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
LIMITS = {
    "contact": (0.07, 0.13, 2500, 5500),
    "song": (0.15, 0.9, 2500, 5500),
    "alarm": (0.35, 0.65, 2500, 7500),
    "flight": (0.2, 0.4, 2500, 6000),
}


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
    if not 0.5 <= peak_amp <= 0.9:
        problems.append(f"{path.name}: 振幅 {peak_amp:.2f} が 0.5〜0.9 の範囲外")
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
        if len(files) != EXPECTED_VARIANTS:
            problems.append(f"{kind}: {len(files)} 個 (期待値 {EXPECTED_VARIANTS} 個)")
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
```

- [ ] **Step 2: 検証が失敗することを確認する**

Run: `cd /g/ff14/suzume && py tools/check_chirps.py assets/sounds`
Expected: `NG: contact: 0 個 (期待値 3 個)` など 4 件、終了コード 1

- [ ] **Step 3: 合成スクリプトを書く**

`tools/chirps.py`:

```python
"""タビスズメの鳴き声 4 種 × 3 バリエーションを合成して WAV に書き出す。

numpy だけを使う。使い方:
    py tools/chirps.py assets/sounds
"""
import sys
import wave
from pathlib import Path

import numpy as np

RATE = 44100
VARIANTS = 3
PEAK = 0.8


def envelope(n, attack, release):
    """立ち上がり attack、減衰 release (どちらも全体に対する割合) の台形エンベロープ"""
    t = np.linspace(0.0, 1.0, n, endpoint=False)
    return np.clip(t / attack, 0.0, 1.0) * np.clip((1.0 - t) / release, 0.0, 1.0)


def bandpass(signal, low, high):
    """FFT で low〜high Hz 以外を落とす簡易バンドパス"""
    spectrum = np.fft.rfft(signal)
    freqs = np.fft.rfftfreq(len(signal), 1.0 / RATE)
    spectrum[(freqs < low) | (freqs > high)] = 0.0
    return np.fft.irfft(spectrum, len(signal))


def tone(duration, freq_points, harmonics=(1.0, 0.35, 0.12), attack=0.1, release=0.5):
    """周波数を freq_points に沿って動かす、倍音付きの音"""
    n = int(RATE * duration)
    t = np.linspace(0.0, 1.0, n, endpoint=False)
    freq = np.interp(t, np.linspace(0.0, 1.0, len(freq_points)), freq_points)
    phase = 2.0 * np.pi * np.cumsum(freq) / RATE
    signal = sum(amp * np.sin(k * phase) for k, amp in enumerate(harmonics, start=1))
    return signal * envelope(n, attack, release)


def chun(rng, duration, base):
    """「チュン」1 音: 少し上がってから大きく下がる。息っぽいノイズを少し混ぜる"""
    body = tone(duration, [base * 0.85, base * 1.08, base * 0.72])
    breath = bandpass(rng.standard_normal(len(body)), 2500, 7000) * envelope(len(body), 0.05, 0.6)
    return body + 0.15 * breath / (np.max(np.abs(breath)) + 1e-9)


def contact(rng):
    """「チュン」地鳴き"""
    return chun(rng, rng.uniform(0.08, 0.12), rng.uniform(3500, 4500))


def song(rng):
    """「チュンチュン」囀り: 2〜4 音を、音の頭から 0.12〜0.2 秒間隔で"""
    base = rng.uniform(3500, 4500)
    parts = []
    for _ in range(int(rng.integers(2, 5))):
        note = chun(rng, rng.uniform(0.08, 0.11), base * rng.uniform(0.92, 1.08))
        parts.append(note)
        parts.append(np.zeros(max(0, int(RATE * rng.uniform(0.12, 0.2)) - len(note))))
    return np.concatenate(parts[:-1])


def alarm(rng):
    """「ジュクジュク」警戒: 3〜7kHz の雑音とざらついた音を、約 12Hz で断続させる"""
    n = int(RATE * rng.uniform(0.4, 0.6))
    t = np.arange(n) / RATE
    noise = bandpass(rng.standard_normal(n), 3000, 7000)
    noise /= np.max(np.abs(noise)) + 1e-9
    buzz = np.sin(2 * np.pi * rng.uniform(3300, 3800) * t) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 120 * t)))
    pulses = (0.5 + 0.5 * np.sin(2 * np.pi * rng.uniform(11, 14) * t)) ** 3
    return (0.7 * noise + 0.4 * buzz) * pulses * envelope(n, 0.03, 0.15)


def flight(rng):
    """「チュリリ」飛び立ち: 上がる短い音を 15〜20Hz で転がす"""
    out = np.zeros(int(RATE * rng.uniform(0.25, 0.35)))
    rate = rng.uniform(15, 20)
    period = int(RATE / rate)
    element = tone(0.7 / rate, [3000, 5000], harmonics=(1.0, 0.25), attack=0.15, release=0.4)
    for start in range(0, len(out) - len(element), period):
        out[start:start + len(element)] += element * rng.uniform(0.8, 1.0)
    return out * envelope(len(out), 0.05, 0.3)


def normalize(signal):
    signal = signal - np.mean(signal)
    return signal * (PEAK / (np.max(np.abs(signal)) + 1e-9))


def write_wav(path, signal):
    data = (np.clip(signal, -1.0, 1.0) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())


KINDS = {"contact": contact, "song": song, "alarm": alarm, "flight": flight}


def main(out_dir):
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    for kind_index, (name, make) in enumerate(KINDS.items()):
        for variant in range(1, VARIANTS + 1):
            # 種類と番号から決まるシードで、何度実行しても同じ音になる
            rng = np.random.default_rng(1000 * kind_index + variant)
            path = out / f"{name}_{variant}.wav"
            write_wav(path, normalize(make(rng)))
            print(path)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "assets/sounds")
```

- [ ] **Step 4: 生成して検証が通ることを確認する**

```bash
cd /g/ff14/suzume && py tools/chirps.py assets/sounds && py tools/check_chirps.py assets/sounds out/spectrograms
```

Expected: 12 ファイルの情報と `OK`、終了コード 0。NG が出たら `LIMITS` ではなく `chirps.py` の合成パラメータを直す。

- [ ] **Step 5: スペクトログラムを目で確認する**

`out/spectrograms/*.png` を Read ツールで開き、確認する:
- contact: 山なりの 1 本の線(倍音が上に薄く 2 本)
- song: 同じ形が 2〜4 個並ぶ
- alarm: 3〜7kHz に縦縞(約 12Hz の断続)
- flight: 右上がりの短い線が規則的に並ぶ

- [ ] **Step 6: コミット**

```bash
cd /g/ff14/suzume && git add tools assets/sounds && git commit -m "feat: 鳴き声 4 種の合成スクリプトと検証スクリプト

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: プラグイン — 設定・鳴き声の再生・設定画面

**Files:**
- Modify: `src/Suzume/Suzume.csproj`(全体を置き換え)
- Create: `src/Suzume/Configuration.cs`, `src/Suzume/ChirpPlayer.cs`, `src/Suzume/Windows/ConfigWindow.cs`
- Modify: `src/Suzume/Plugin.cs`(全体を置き換え)

**Interfaces:**
- Consumes: `ChirpKind`(Task 2)、WAV 12 個(Task 5)、`ProbeRunner`(Task 1)
- Produces:
  - `Configuration { bool Enabled; bool SoundEnabled; float Volume; float ChirpFrequency; float FlyDistance; bool ShowDebug; void Normalize(); void Save(); }`
  - `ChirpPlayer : IDisposable { void Play(ChirpKind kind, float volume); }`(同時再生は最大 2)
  - `ConfigWindow(Configuration config, Func<string> debugText, Action<ChirpKind> preview)`

ゲーム API に依存する部分なので単体テストは書かず、ビルドと実機の試聴で確認する。

- [ ] **Step 1: csproj を書き換える**

`src/Suzume/Suzume.csproj`:

```xml
<Project Sdk="Dalamud.NET.Sdk/15.0.0">
  <PropertyGroup>
    <Version>0.1.0.0</Version>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Suzume.Core\Suzume.Core.csproj" />
    <PackageReference Include="NAudio.WinMM" Version="2.2.1" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="..\..\assets\sounds\*.wav" LogicalName="Suzume.Sounds.%(Filename)%(Extension)" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Configuration を書く**

`src/Suzume/Configuration.cs`:

```csharp
using Dalamud.Configuration;

namespace Suzume;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public float Volume { get; set; } = 0.6f;
    public float ChirpFrequency { get; set; } = 1.0f;
    public float FlyDistance { get; set; } = 6f;
    public bool ShowDebug { get; set; }

    public void Normalize()
    {
        Volume = Math.Clamp(Volume, 0f, 1f);
        ChirpFrequency = Math.Clamp(ChirpFrequency, 0f, 3f);
        FlyDistance = Math.Clamp(FlyDistance, 2f, 20f);
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
```

- [ ] **Step 3: ChirpPlayer を書く**

`src/Suzume/ChirpPlayer.cs`:

```csharp
using System.Reflection;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Suzume.Core;

namespace Suzume;

/// <summary>埋め込みの WAV を NAudio で鳴らす。同時再生は最大 2 つ。</summary>
public sealed class ChirpPlayer : IDisposable
{
    private const int MaxConcurrent = 2;
    private static readonly WaveFormat Format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

    private readonly Dictionary<ChirpKind, float[][]> clips = new();
    private readonly MixingSampleProvider mixer = new(Format) { ReadFully = true };
    private readonly WaveOutEvent output = new() { DesiredLatency = 100 };
    private readonly Random random = new();

    public ChirpPlayer()
    {
        foreach (var kind in Enum.GetValues<ChirpKind>())
            clips[kind] = LoadVariants(kind);

        output.Init(mixer);
        output.Play();
    }

    public void Play(ChirpKind kind, float volume)
    {
        if (volume <= 0f || mixer.MixerInputs.Count() >= MaxConcurrent)
            return;

        var variants = clips[kind];
        var clip = variants[random.Next(variants.Length)];
        mixer.AddMixerInput(new VolumeSampleProvider(new ClipSampleProvider(clip)) { Volume = volume });
    }

    public void Dispose()
    {
        output.Stop();
        output.Dispose();
    }

    private static float[][] LoadVariants(ChirpKind kind)
    {
        var prefix = $"Suzume.Sounds.{kind.ToString().ToLowerInvariant()}_";
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Order()
            .ToArray();
        if (names.Length == 0)
            throw new InvalidOperationException($"鳴き声のリソースがありません: {prefix}*");

        return names.Select(name => ReadSamples(assembly, name)).ToArray();
    }

    private static float[] ReadSamples(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"リソースを開けません: {name}");
        using var reader = new WaveFileReader(stream);
        var provider = reader.ToSampleProvider();
        if (provider.WaveFormat.SampleRate != Format.SampleRate || provider.WaveFormat.Channels != Format.Channels)
            throw new InvalidOperationException($"{name} は 44.1kHz モノラルではありません");

        var samples = new List<float>();
        var buffer = new float[4096];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            samples.AddRange(buffer.Take(read));
        return samples.ToArray();
    }

    /// <summary>メモリ上の 1 クリップを最後まで流す。</summary>
    private sealed class ClipSampleProvider(float[] clip) : ISampleProvider
    {
        private int position;

        public WaveFormat WaveFormat => Format;

        public int Read(float[] buffer, int offset, int count)
        {
            var n = Math.Min(count, clip.Length - position);
            Array.Copy(clip, position, buffer, offset, n);
            position += n;
            return n;
        }
    }
}
```

- [ ] **Step 4: ConfigWindow を書く**

`src/Suzume/Windows/ConfigWindow.cs`:

```csharp
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Suzume.Core;

namespace Suzume.Windows;

public sealed class ConfigWindow : Window
{
    private readonly Configuration config;
    private readonly Func<string> debugText;
    private readonly Action<ChirpKind> preview;

    public ConfigWindow(Configuration config, Func<string> debugText, Action<ChirpKind> preview)
        : base("タビスズメ###SuzumeConfig")
    {
        this.config = config;
        this.debugText = debugText;
        this.preview = preview;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 260),
            MaximumSize = new Vector2(800, 900),
        };
    }

    public override void Draw()
    {
        var changed = false;

        var enabled = config.Enabled;
        if (ImGui.Checkbox("有効", ref enabled))
        {
            config.Enabled = enabled;
            changed = true;
        }

        var sound = config.SoundEnabled;
        if (ImGui.Checkbox("鳴き声", ref sound))
        {
            config.SoundEnabled = sound;
            changed = true;
        }

        var volume = config.Volume;
        if (ImGui.SliderFloat("音量", ref volume, 0f, 1f))
        {
            config.Volume = volume;
            changed = true;
        }

        var frequency = config.ChirpFrequency;
        if (ImGui.SliderFloat("鳴く頻度", ref frequency, 0f, 3f))
        {
            config.ChirpFrequency = frequency;
            changed = true;
        }

        var flyDistance = config.FlyDistance;
        if (ImGui.SliderFloat("飛ぶ距離 (yalm)", ref flyDistance, 2f, 20f))
        {
            config.FlyDistance = flyDistance;
            changed = true;
        }

        ImGui.Separator();
        ImGui.TextUnformatted("試聴");
        foreach (var kind in Enum.GetValues<ChirpKind>())
        {
            ImGui.SameLine();
            if (ImGui.Button(Label(kind)))
                preview(kind);
        }

        var showDebug = config.ShowDebug;
        if (ImGui.Checkbox("デバッグ表示", ref showDebug))
        {
            config.ShowDebug = showDebug;
            changed = true;
        }

        if (config.ShowDebug)
            ImGui.TextUnformatted(debugText());

        if (changed)
        {
            config.Normalize();
            config.Save();
        }
    }

    private static string Label(ChirpKind kind) => kind switch
    {
        ChirpKind.Contact => "チュン",
        ChirpKind.Song => "チュンチュン",
        ChirpKind.Alarm => "ジュクジュク",
        ChirpKind.Flight => "チュリリ",
        _ => kind.ToString(),
    };
}
```

- [ ] **Step 5: Plugin.cs を書き換える**

`src/Suzume/Plugin.cs`:

```csharp
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Suzume.Windows;

namespace Suzume;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/suzume";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly Configuration config;
    private readonly WindowSystem windowSystem = new("Suzume");
    private readonly ConfigWindow configWindow;
    private readonly ChirpPlayer? chirpPlayer;
    private readonly ProbeRunner probe = new();

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.Normalize();

        try
        {
            chirpPlayer = new ChirpPlayer();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "鳴き声の初期化に失敗しました。音なしで動作します");
        }

        configWindow = new ConfigWindow(config, probe.Describe, kind => chirpPlayer?.Play(kind, config.Volume));
        windowSystem.AddWindow(configWindow);
        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi += configWindow.Toggle;
        Framework.Update += probe.OnUpdate;
        CommandManager.AddHandler(CommandName, new CommandInfo((_, args) => HandleCommand(args))
        {
            HelpMessage = "設定画面を開きます。/suzume probe <モード> [id] / /suzume info はデバッグ用です。",
        });
    }

    private void OnDraw()
    {
        probe.OnDraw();
        windowSystem.Draw();
    }

    private void HandleCommand(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            configWindow.Toggle();
            return;
        }

        if (parts.Length >= 2 && parts[0] == "probe")
        {
            var id = parts.Length >= 3 && ushort.TryParse(parts[2], out var value) ? value : (ushort)0;
            probe.Set(parts[1], id);
            ChatGui.Print($"[Suzume] probe = {parts[1]} {id}");
            return;
        }

        ChatGui.Print($"[Suzume] {probe.Describe()}");
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
        Framework.Update -= probe.OnUpdate;
        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= configWindow.Toggle;
        windowSystem.RemoveAllWindows();
        probe.Release();
        chirpPlayer?.Dispose();
    }
}
```

- [ ] **Step 6: ビルドして、リソースと NAudio が入っていることを確認する**

```bash
cd /g/ff14/suzume && dotnet build src/Suzume/Suzume.csproj -c Debug && ls src/Suzume/bin/Debug | grep -i naudio
```

Expected: `0 エラー`。`NAudio.Core.dll` と `NAudio.WinMM.dll` が出力にある。

埋め込みリソースの確認:

```bash
cd /g/ff14/suzume && py -c "print(open('src/Suzume/bin/Debug/Suzume.dll','rb').read().count(b'Suzume.Sounds.'))"
```

Expected: `12`(リソース名はメタデータに UTF-8 で 1 回ずつ入る)

- [ ] **Step 7: 配置して、ユーザーに試聴を依頼する**

```bash
mkdir -p /c/ff14_suzume && cp -r /g/ff14/suzume/src/Suzume/bin/Debug/. /c/ff14_suzume/
```

ユーザーへの依頼: プラグインを再読み込み → `/suzume` で設定画面 → 試聴ボタン 4 つがそれぞれ鳴るか、音量スライダーが効くか。

- [ ] **Step 8: コミット**

```bash
cd /g/ff14/suzume && git add src/Suzume && git commit -m "feat: 設定画面と鳴き声の再生

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: プラグイン — 行動の反映(PoseApplier と SparrowController)

**前提:** Task 1 のスパイク結果で「bounce が止まっているときも移動中も効く」こと。そうでなければこのタスクを始めず、計画を見直す。

**Files:**
- Create: `src/Suzume/PoseApplier.cs`, `src/Suzume/SparrowController.cs`
- Modify: `src/Suzume/Plugin.cs`(全体を置き換え)

**Interfaces:**
- Consumes: `SparrowBrain`、`ChirpScheduler`、`ChirpVolume`、`SparrowParams`、`SparrowInput`、`SparrowPose`(Task 2〜4)、`ChirpPlayer`、`Configuration`、`ConfigWindow`(Task 6)、`MinionTracker`、`ProbeRunner`(Task 1)、スパイク結果
- Produces: 完成したプラグイン

- [ ] **Step 1: PoseApplier を書く(定数はスパイク結果で決める)**

`src/Suzume/PoseApplier.cs`。`IdleTimelineId`、`FlyTimelineId`、`DrawOffsetIsLocal`、`PitchWrite`、`PitchSign`、`BaseDrawOffset` は、Task 1 Step 8 の表に従って `docs/superpowers/plans/2026-10-27-spike-results.md` の値に置き換える。下の値は既定値。

```csharp
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Suzume.Core;

namespace Suzume;

public enum PitchMode
{
    /// <summary>Framework.Update で書き込む</summary>
    Update,
    /// <summary>描画直前 (UiBuilder.Draw) で書き込む</summary>
    Draw,
    /// <summary>前傾を使わない</summary>
    Off,
}

/// <summary>
/// SparrowPose をミニオンの見た目に書き込む。
/// 各定数の値は実機スパイクの結果 (docs/superpowers/plans/2026-10-27-spike-results.md) で決めた。
/// </summary>
public sealed unsafe class PoseApplier
{
    // normal/idle。モンスターでは待機アニメ cbnm_id0 になる
    private static readonly ushort IdleTimelineId = 3;
    // 飛行ループ cbnm_02f_lp0 になる ID
    private static readonly ushort FlyTimelineId = 22;
    // DrawOffset がモデルの向きに合わせて回るなら true
    private static readonly bool DrawOffsetIsLocal = false;
    private static readonly PitchMode PitchWrite = PitchMode.Update;
    // 正の pitch で頭が上がってしまう場合は -1
    private static readonly float PitchSign = 1f;
    // ゲームが平常時に入れている DrawOffset
    private static readonly Vector3 BaseDrawOffset = Vector3.Zero;

    public void Apply(Companion* minion, in SparrowPose pose)
    {
        var go = (GameObject*)minion;
        go->SetRotation(pose.Yaw);

        var offset = pose.Position - go->Position;
        if (DrawOffsetIsLocal)
            offset = Vector3.Transform(offset, Quaternion.CreateFromAxisAngle(Vector3.UnitY, -pose.Yaw));
        offset += BaseDrawOffset;
        go->SetDrawOffset(offset.X, offset.Y, offset.Z);

        ((Character*)minion)->Timeline.BaseOverride =
            pose.Anim == SparrowAnim.Fly ? FlyTimelineId : IdleTimelineId;

        if (PitchWrite == PitchMode.Update)
            WritePitch(go, pose);
    }

    /// <summary>描画直前に呼ぶ。PitchWrite が Draw のときだけ前傾を書き込む。</summary>
    public void ApplyLate(Companion* minion, in SparrowPose pose)
    {
        if (PitchWrite == PitchMode.Draw)
            WritePitch((GameObject*)minion, pose);
    }

    /// <summary>ゲーム本来の見た目に戻す。</summary>
    public void Release(Companion* minion)
    {
        ((GameObject*)minion)->SetDrawOffset(BaseDrawOffset.X, BaseDrawOffset.Y, BaseDrawOffset.Z);
        ((Character*)minion)->Timeline.BaseOverride = 0;
    }

    private static void WritePitch(GameObject* go, in SparrowPose pose)
    {
        var draw = go->DrawObject;
        if (draw != null)
            draw->Object.Rotation = Quaternion.CreateFromYawPitchRoll(pose.Yaw, PitchSign * pose.Pitch, 0f);
    }
}
```

- [ ] **Step 2: SparrowController を書く**

`src/Suzume/SparrowController.cs`:

```csharp
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Suzume.Core;

namespace Suzume;

/// <summary>毎フレーム SparrowBrain を進め、結果をミニオンの見た目と音に反映する。</summary>
public sealed unsafe class SparrowController : IDisposable
{
    // これだけ続けて例外が出たら自動で無効化する
    private const int MaxConsecutiveErrors = 10;

    private readonly Configuration config;
    private readonly ChirpPlayer? chirpPlayer;
    private readonly SparrowParams parameters = new();
    private readonly SparrowBrain brain;
    private readonly ChirpScheduler scheduler;
    private readonly PoseApplier applier = new();
    private Vector3? lastPlayerPosition;
    private int consecutiveErrors;

    public SparrowController(Configuration config, ChirpPlayer? chirpPlayer)
    {
        this.config = config;
        this.chirpPlayer = chirpPlayer;
        var random = new SeededRandom(Environment.TickCount);
        brain = new SparrowBrain(parameters, random);
        scheduler = new ChirpScheduler(parameters, random);
    }

    public bool Active { get; private set; }
    public SparrowPose LastPose { get; private set; }
    public Vector3 LastTarget { get; private set; }

    public void Update(IFramework framework)
    {
        try
        {
            Step((float)framework.UpdateDelta.TotalSeconds);
            consecutiveErrors = 0;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "タビスズメの更新に失敗しました");
            ReleaseIfActive();
            if (++consecutiveErrors >= MaxConsecutiveErrors)
            {
                config.Enabled = false;
                config.Save();
                consecutiveErrors = 0;
                Plugin.ChatGui.PrintError("[Suzume] エラーが続いたため無効にしました。/xllog を確認してください。");
            }
        }
    }

    /// <summary>描画直前の書き込み(PoseApplier が Draw モードのときの前傾)。</summary>
    public void OnDraw()
    {
        if (!Active)
            return;

        var minion = MinionTracker.FindSparrow(out _);
        if (minion != null)
            applier.ApplyLate(minion, LastPose);
    }

    /// <summary>上書きをやめ、次に有効になったとき論理位置から始め直す。</summary>
    public void Suspend()
    {
        ReleaseIfActive();
        brain.Invalidate();
        lastPlayerPosition = null;
    }

    private void Step(float dt)
    {
        Character* owner = null;
        var minion = config.Enabled && !IsSuspendedByGame() ? MinionTracker.FindSparrow(out owner) : null;
        if (minion == null || ((GameObject*)minion)->DrawObject == null)
        {
            Suspend();
            return;
        }

        var go = (GameObject*)minion;
        var playerPosition = ((GameObject*)owner)->Position;
        var playerVelocity = lastPlayerPosition is { } last && dt > 0f ? (playerPosition - last) / dt : Vector3.Zero;
        lastPlayerPosition = playerPosition;

        parameters.FlyDistance = config.FlyDistance;
        parameters.ChirpFrequency = config.ChirpFrequency;

        var pose = brain.Tick(new SparrowInput(
            dt, go->Position, go->Rotation, playerPosition, playerVelocity, Plugin.Condition[ConditionFlag.Mounted]));
        applier.Apply(minion, pose);
        Active = true;
        LastPose = pose;
        LastTarget = go->Position;

        var chirp = scheduler.Update(dt, pose.State, pose.Triggers);
        if (chirp is { } kind && config.SoundEnabled && chirpPlayer != null)
            chirpPlayer.Play(kind, ChirpVolume.Compute(config.Volume, Vector3.Distance(playerPosition, pose.Position)));
    }

    private void ReleaseIfActive()
    {
        if (!Active)
            return;

        Active = false;
        var minion = MinionTracker.FindSparrow(out _);
        if (minion != null)
            applier.Release(minion);
    }

    private static bool IsSuspendedByGame() =>
        Plugin.ClientState.IsGPosing
        || Plugin.Condition[ConditionFlag.BetweenAreas]
        || Plugin.Condition[ConditionFlag.BetweenAreas51]
        || Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent]
        || Plugin.Condition[ConditionFlag.WatchingCutscene]
        || Plugin.Condition[ConditionFlag.WatchingCutscene78];

    public void Dispose() => ReleaseIfActive();
}
```

- [ ] **Step 3: Plugin.cs を書き換える**

`src/Suzume/Plugin.cs`:

```csharp
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Suzume.Core;
using Suzume.Windows;

namespace Suzume;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/suzume";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly Configuration config;
    private readonly WindowSystem windowSystem = new("Suzume");
    private readonly ConfigWindow configWindow;
    private readonly ChirpPlayer? chirpPlayer;
    private readonly SparrowController controller;
    private readonly ProbeRunner probe = new();

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.Normalize();

        try
        {
            chirpPlayer = new ChirpPlayer();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "鳴き声の初期化に失敗しました。音なしで動作します");
        }

        controller = new SparrowController(config, chirpPlayer);
        configWindow = new ConfigWindow(config, DescribeState, kind => chirpPlayer?.Play(kind, config.Volume));
        windowSystem.AddWindow(configWindow);
        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi += configWindow.Toggle;
        Framework.Update += OnUpdate;
        CommandManager.AddHandler(CommandName, new CommandInfo((_, args) => HandleCommand(args))
        {
            HelpMessage = "設定画面を開きます。/suzume on|off で切り替え。/suzume probe <モード> [id] / /suzume info はデバッグ用です。",
        });
    }

    private bool Probing => probe.Mode != ProbeRunner.Off;

    private void OnUpdate(IFramework framework)
    {
        // 確認コマンドの実行中は、通常の制御を止める
        if (Probing)
        {
            controller.Suspend();
            probe.OnUpdate(framework);
            return;
        }

        controller.Update(framework);
    }

    private void OnDraw()
    {
        if (Probing)
            probe.OnDraw();
        else
            controller.OnDraw();

        windowSystem.Draw();
    }

    private string DescribeState()
    {
        if (!controller.Active)
            return $"停止中\n{probe.Describe()}";

        var pose = controller.LastPose;
        var gap = Angles.HorizontalDistance(pose.Position, controller.LastTarget);
        return $"状態: {pose.State} / アニメ: {pose.Anim}\n"
            + $"見た目の位置: {pose.Position:F2}\n"
            + $"論理位置: {controller.LastTarget:F2}\n"
            + $"差: {gap:F2} y";
    }

    private void HandleCommand(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            configWindow.Toggle();
            return;
        }

        switch (parts[0])
        {
            case "on":
            case "off":
                config.Enabled = parts[0] == "on";
                config.Save();
                ChatGui.Print($"[Suzume] {(config.Enabled ? "有効" : "無効")}にしました");
                return;
            case "probe" when parts.Length >= 2:
                var id = parts.Length >= 3 && ushort.TryParse(parts[2], out var value) ? value : (ushort)0;
                probe.Set(parts[1], id);
                ChatGui.Print($"[Suzume] probe = {parts[1]} {id}");
                return;
            default:
                ChatGui.Print($"[Suzume] {probe.Describe()}");
                return;
        }
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
        Framework.Update -= OnUpdate;
        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= configWindow.Toggle;
        windowSystem.RemoveAllWindows();
        probe.Release();
        controller.Dispose();
        chirpPlayer?.Dispose();
    }
}
```

- [ ] **Step 4: 全体をビルドし、テストを実行する**

```bash
cd /g/ff14/suzume && dotnet build Suzume.slnx -c Debug && dotnet test tests/Suzume.Core.Tests
```

Expected: `0 エラー`、全テスト PASS

- [ ] **Step 5: 配置して、ユーザーに実機確認を依頼する**

```bash
mkdir -p /c/ff14_suzume && cp -r /g/ff14/suzume/src/Suzume/bin/Debug/. /c/ff14_suzume/
```

ユーザーへの確認依頼(`/suzume` → デバッグ表示を ON にすると状態名が見える):

1. 止まっていると、その場でキョロキョロし、ときどき前のめりでついばむ。ときどき「チュン」「チュンチュン」と鳴く
2. ゆっくり歩くと、ピョンピョンと跳ねて止まるを繰り返して付いてくる(滑らない)
3. 走って距離が開くと「チュリリ」と鳴いて飛び立ち、上下に波打ちながら飛んで追いつき、着地してまた跳ねる
4. 鳥のいる方へ走り込むと「ジュクジュク」と鳴いて反対側へ跳ねて逃げる
5. 騎乗して走っても、置いていかれずに飛んで付いてくる
6. テレポート後やエリア移動後に、変な場所から飛んでこない
7. `/suzume off`、プラグインの無効化、GPose 中、カットシーン中は、ゲーム本来の動きに戻る

不自然な点があれば、`SparrowParams` の既定値(Task 2)を調整して再配置する。

- [ ] **Step 6: コミット**

```bash
cd /g/ff14/suzume && git add src/Suzume && git commit -m "feat: スズメの行動をミニオンの見た目と鳴き声に反映

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
