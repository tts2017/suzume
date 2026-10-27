namespace Suzume.Core;

/// <summary>設定画面とチャットに出す文言。Dalamud の言語設定が日本語・ドイツ語・フランス語ならその言語、それ以外は英語。</summary>
public sealed class UiText
{
    public required string WindowTitle { get; init; }
    public required string Enabled { get; init; }
    public required string IncludeOthers { get; init; }
    public required string Sound { get; init; }
    public required string StereoSound { get; init; }
    public required string Volume { get; init; }
    public required string ChirpFrequency { get; init; }
    public required string FlyDistance { get; init; }
    public required string WanderDistance { get; init; }
    public required string HeadOffset { get; init; }
    public required string Puffed { get; init; }
    public required string PuffAmount { get; init; }
    public required string BeckonHint { get; init; }
    public required string ScareHint { get; init; }
    public required string ResetPosition { get; init; }
    public required string Preview { get; init; }
    public required string ShowDebug { get; init; }
    public required string CommandHelp { get; init; }
    public required string TurnedOn { get; init; }
    public required string TurnedOff { get; init; }
    public required string ResetDone { get; init; }
    public required string PreenQueued { get; init; }
    public required string DustQueued { get; init; }
    public required string DisabledAfterErrors { get; init; }
    public required string DebugStopped { get; init; }
    public required string DebugActiveCount { get; init; }
    public required string DebugState { get; init; }
    public required string DebugAnim { get; init; }
    public required string DebugPosition { get; init; }
    public required string DebugTarget { get; init; }
    public required string DebugGap { get; init; }
    public required IReadOnlyDictionary<ChirpKind, string> ChirpLabels { get; init; }

    public string ChirpLabel(ChirpKind kind) =>
        ChirpLabels.TryGetValue(kind, out var label) ? label : kind.ToString();

    /// <summary>Dalamud の言語コード ("ja", "en" など) に合う文言。</summary>
    public static UiText For(string? language) => language switch
    {
        "ja" => Japanese,
        "de" => German,
        "fr" => French,
        _ => English,
    };

    public static readonly UiText Japanese = new()
    {
        WindowTitle = "タビスズメ",
        Enabled = "有効",
        IncludeOthers = "他の人のタビスズメにも適用",
        Sound = "鳴き声",
        StereoSound = "鳴き声を定位 (スズメのいる向きから聞こえる。後ろはこもる)",
        Volume = "音量",
        ChirpFrequency = "鳴く頻度",
        FlyDistance = "飛ぶ距離 (yalm)",
        WanderDistance = "あなたから離れて動き回る距離 (yalm)",
        HeadOffset = "頭の高さの補正 (yalm)",
        Puffed = "ふくら雀 (羽毛をふくらませた丸い形)",
        PuffAmount = "ふくらみ (%)",
        BeckonHint = "/beckon (手まねき) で頭に乗り、もう一度で下ります",
        ScareHint = "スズメに向けて /angry /furious /slap /battlestance /box /stomp /throw をすると逃げます",
        ResetPosition = "スズメをそばに置き直す",
        Preview = "試聴",
        ShowDebug = "デバッグ表示",
        CommandHelp = "設定画面を開きます。/suzume on|off で切り替え。/suzume reset で置き直し。/suzume dust で砂浴び、/suzume preen で毛づくろい。/suzume probe <モード> [id] / /suzume info はデバッグ用です。",
        TurnedOn = "有効にしました",
        TurnedOff = "無効にしました",
        ResetDone = "スズメをそばの地面に置き直しました",
        PreenQueued = "次に落ち着いたら毛づくろいします",
        DustQueued = "次に落ち着いたら砂浴びします",
        DisabledAfterErrors = "エラーが続いたため無効にしました。/xllog を確認してください。",
        DebugStopped = "停止中",
        DebugActiveCount = "動かしている数",
        DebugState = "状態",
        DebugAnim = "アニメ",
        DebugPosition = "見た目の位置",
        DebugTarget = "目標地点",
        DebugGap = "差",
        ChirpLabels = new Dictionary<ChirpKind, string>
        {
            [ChirpKind.Contact] = "チュン",
            [ChirpKind.Song] = "チュンチュン",
            [ChirpKind.Alarm] = "ジュクジュク",
            [ChirpKind.Flight] = "チュリリ",
            [ChirpKind.Dust] = "バサバサ",
        },
    };

    public static readonly UiText English = new()
    {
        WindowTitle = "Suzume",
        Enabled = "Enabled",
        IncludeOthers = "Also apply to other players' sparrows",
        Sound = "Calls",
        StereoSound = "Directional calls (heard from where the sparrow is; muffled from behind)",
        Volume = "Volume",
        ChirpFrequency = "Call frequency",
        FlyDistance = "Fly distance (yalms)",
        WanderDistance = "How far it roams from you (yalms)",
        HeadOffset = "Head height adjustment (yalms)",
        Puffed = "Puffed up (round, fluffed winter feathers)",
        PuffAmount = "Puffiness (%)",
        BeckonHint = "/beckon to have it hop on your head; /beckon again to get it down",
        ScareHint = "It flies off if you /angry /furious /slap /battlestance /box /stomp /throw at it",
        ResetPosition = "Put the sparrow back beside you",
        Preview = "Preview",
        ShowDebug = "Show debug info",
        CommandHelp = "Opens the settings. /suzume on|off to toggle. /suzume reset to put it back. /suzume dust for a dust bath, /suzume preen to preen. /suzume probe <mode> [id] and /suzume info are for debugging.",
        TurnedOn = "Enabled",
        TurnedOff = "Disabled",
        ResetDone = "Put the sparrow back on the ground beside you",
        PreenQueued = "It will preen once it settles down",
        DustQueued = "It will take a dust bath once it settles down",
        DisabledAfterErrors = "Disabled after repeated errors. Check /xllog.",
        DebugStopped = "Stopped",
        DebugActiveCount = "Sparrows controlled",
        DebugState = "State",
        DebugAnim = "Anim",
        DebugPosition = "Shown at",
        DebugTarget = "Target",
        DebugGap = "Gap",
        ChirpLabels = new Dictionary<ChirpKind, string>
        {
            [ChirpKind.Contact] = "Cheep",
            [ChirpKind.Song] = "Cheep-cheep",
            [ChirpKind.Alarm] = "Churr",
            [ChirpKind.Flight] = "Chirrup",
            [ChirpKind.Dust] = "Flutter",
        },
    };

    public static readonly UiText German = new()
    {
        WindowTitle = "Suzume",
        Enabled = "Aktiviert",
        IncludeOthers = "Auch auf Spatzen anderer Spieler anwenden",
        Sound = "Rufe",
        StereoSound = "Räumliche Rufe (aus der Richtung des Spatzen; von hinten gedämpft)",
        Volume = "Lautstärke",
        ChirpFrequency = "Ruf-Häufigkeit",
        FlyDistance = "Flugdistanz (Yalm)",
        WanderDistance = "Wie weit er sich von dir entfernt (Yalm)",
        HeadOffset = "Kopfhöhe anpassen (Yalm)",
        Puffed = "Aufgeplustert (rund, mit aufgestelltem Winterfederkleid)",
        PuffAmount = "Aufplusterung (%)",
        BeckonHint = "Mit /beckon hüpft er auf deinen Kopf, mit erneutem /beckon wieder herunter",
        ScareHint = "Er fliegt davon, wenn du ihm gegenüber /angry /furious /slap /battlestance /box /stomp /throw verwendest",
        ResetPosition = "Spatz wieder neben dich setzen",
        Preview = "Probehören",
        ShowDebug = "Debug-Infos anzeigen",
        CommandHelp = "Öffnet die Einstellungen. /suzume on|off zum Umschalten. /suzume reset setzt ihn neben dich. /suzume dust für ein Sandbad, /suzume preen zum Gefiederputzen. /suzume probe <Modus> [id] und /suzume info dienen der Fehlersuche.",
        TurnedOn = "Aktiviert",
        TurnedOff = "Deaktiviert",
        ResetDone = "Der Spatz wurde neben dir auf den Boden gesetzt",
        PreenQueued = "Er putzt sein Gefieder, sobald er zur Ruhe kommt",
        DustQueued = "Er nimmt ein Sandbad, sobald er zur Ruhe kommt",
        DisabledAfterErrors = "Nach wiederholten Fehlern deaktiviert. Siehe /xllog.",
        DebugStopped = "Angehalten",
        DebugActiveCount = "Gesteuerte Spatzen",
        DebugState = "Zustand",
        DebugAnim = "Anim",
        DebugPosition = "Angezeigt bei",
        DebugTarget = "Ziel",
        DebugGap = "Abstand",
        ChirpLabels = new Dictionary<ChirpKind, string>
        {
            [ChirpKind.Contact] = "Tschilp",
            [ChirpKind.Song] = "Tschilp-tschilp",
            [ChirpKind.Alarm] = "Tscherr",
            [ChirpKind.Flight] = "Tschirrip",
            [ChirpKind.Dust] = "Flatter",
        },
    };

    public static readonly UiText French = new()
    {
        WindowTitle = "Suzume",
        Enabled = "Activé",
        IncludeOthers = "Appliquer aussi aux moineaux des autres joueurs",
        Sound = "Cris",
        StereoSound = "Cris spatialisés (entendus depuis la position du moineau ; étouffés par-derrière)",
        Volume = "Volume",
        ChirpFrequency = "Fréquence des cris",
        FlyDistance = "Distance de vol (yalms)",
        WanderDistance = "Distance à laquelle il s'éloigne de vous (yalms)",
        HeadOffset = "Ajustement de la hauteur de la tête (yalms)",
        Puffed = "Ébouriffé (rond, plumage d'hiver gonflé)",
        PuffAmount = "Gonflement (%)",
        BeckonHint = "Avec /beckon, il saute sur votre tête ; refaites /beckon pour qu'il descende",
        ScareHint = "Il s'envole si vous faites /angry /furious /slap /battlestance /box /stomp /throw dans sa direction",
        ResetPosition = "Replacer le moineau à côté de vous",
        Preview = "Écouter",
        ShowDebug = "Afficher le débogage",
        CommandHelp = "Ouvre les paramètres. /suzume on|off pour activer ou désactiver. /suzume reset pour le replacer. /suzume dust pour un bain de poussière, /suzume preen pour qu'il se lisse les plumes. /suzume probe <mode> [id] et /suzume info servent au débogage.",
        TurnedOn = "Activé",
        TurnedOff = "Désactivé",
        ResetDone = "Le moineau a été replacé au sol à côté de vous",
        PreenQueued = "Il se lissera les plumes dès qu'il sera calme",
        DustQueued = "Il prendra un bain de poussière dès qu'il sera calme",
        DisabledAfterErrors = "Désactivé après des erreurs répétées. Consultez /xllog.",
        DebugStopped = "Arrêté",
        DebugActiveCount = "Moineaux contrôlés",
        DebugState = "État",
        DebugAnim = "Anim",
        DebugPosition = "Affiché à",
        DebugTarget = "Cible",
        DebugGap = "Écart",
        ChirpLabels = new Dictionary<ChirpKind, string>
        {
            [ChirpKind.Contact] = "Piou",
            [ChirpKind.Song] = "Piou-piou",
            [ChirpKind.Alarm] = "Tchrr",
            [ChirpKind.Flight] = "Tchirrip",
            [ChirpKind.Dust] = "Frou-frou",
        },
    };
}
