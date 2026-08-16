// 런/메타 상태 — 모든 씬이 공유하는 단일 정본 (ADR-029 2차).
//
// 이관 원본: ui/game/state.js (window.RH). **규칙은 그대로 옮겼다** —
// 지도 생성(MapGen.cs), Reachable 의 세이브 스커밍 차단, CompleteNode 의 층 진행,
// FinalizeRun 의 명단 정산, 계측(seen/stats/badges)까지 같은 판정을 낸다.
//
// 저장 위치는 localStorage 대신 Godot 의 `user://save.json` 이다. 웹판이 키 5개로
// 나눠 두던 것(penname/meta/run/settings)을 한 파일에 담는다 — 파일 하나면 원자적으로
// 쓰고 통째로 백업·삭제할 수 있고, 키가 서로 어긋난 상태(런은 있는데 메타가 없다)가 안 생긴다.
//
// ── 전투 담당(Combat.cs)이 쓰는 계약 ──────────────────────
//   RunStore.Current            진행 중인 런 (없으면 null). 필드를 직접 고치고 RunStore.Save()
//   RunStore.BeginCombat(id)    전투가 실제로 시작됐다는 표시 → Reachable() 이 그 노드로 잠긴다
//   RunStore.EndCombat()        승/패/이탈로 판이 끝났다
//   RunStore.MergeBattleStats() 엔진 BattleStats 1판치를 계정 누적으로 옮긴다
//   RunStore.MarkEnded("death") 사망 — 유언을 올릴 때까지 지도가 잠긴다
//   RunStore.CompleteNode(...)  노드 완료 → 다음 씬 경로를 돌려준다 (SceneRouter.Go 로 이동)
//   RunStore.RecordSeen(ids)    카드를 손에 넣는 모든 경로가 지나는 도감 등재
// 전투 결과 되써넣기는 웹판 writeBackRun() 과 같다 — SuitCounters/LastSuit/Will/BattlesWon 을
// 직접 대입하고 Save() 를 부른다.

using System.Text.Json;
using System.Text.Json.Serialization;
using ReviewHero.Engine;

namespace ReviewHero.Game.Run;

// ── 지도 ────────────────────────────────────────────────

/// <summary>노드 종류 (state.js 의 문자열 type 과 1:1 — JSON 은 소문자로 쓴다)</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NodeType>))]
public enum NodeType
{
    Battle,
    Elite,
    Event,
    Shop,
    Rest,
    Boss,
}

public static class NodeTypeExt
{
    /// <summary>state.js NODE_LABEL</summary>
    public static string Label(this NodeType t) => t switch
    {
        NodeType.Battle => "전투",
        NodeType.Elite => "정예",
        NodeType.Event => "이벤트",
        NodeType.Shop => "상점",
        NodeType.Rest => "휴식",
        NodeType.Boss => "보스",
        _ => "?",
    };

    /// <summary>state.js NODE_ICON</summary>
    public static string Icon(this NodeType t) => t switch
    {
        NodeType.Battle => "⚔",
        NodeType.Elite => "🛡",
        NodeType.Event => "❓",
        NodeType.Shop => "🪙",
        NodeType.Rest => "🏕",
        NodeType.Boss => "👑",
        _ => "?",
    };

    /// <summary>전투 씬으로 들어가는 노드인가 (state.js NODE_PAGE 가 combat.html 로 보내던 셋)</summary>
    public static bool IsCombat(this NodeType t) =>
        t is NodeType.Battle or NodeType.Elite or NodeType.Boss;

    /// <summary>웹판 문자열 표기 (지도 대조·로그용)</summary>
    public static string Key(this NodeType t) => t.ToString().ToLowerInvariant();
}

public sealed class MapNode
{
    public string Id { get; set; } = "";
    public NodeType Type { get; set; }

    /// <summary>전투·정예·보스만 채운다 (적 id)</summary>
    public string? Enemy { get; set; }

    /// <summary>다음 층에서 이어지는 노드 id — 없으면 지도가 전결합으로 보인다</summary>
    public List<string> Next { get; set; } = new();

    public bool Visited { get; set; }
}

public sealed class MapData
{
    /// <summary>1막 6층 — Floors[0] 이 1층</summary>
    public List<List<MapNode>> Floors { get; set; } = new();

    public IReadOnlyList<MapNode> Row(int floor) =>
        floor >= 1 && floor <= Floors.Count ? Floors[floor - 1] : Array.Empty<MapNode>();

    public MapNode? Find(int floor, string? id) =>
        id is null ? null : Row(floor).FirstOrDefault(n => n.Id == id);
}

// ── 런 ──────────────────────────────────────────────────

/// <summary>「전투 중」 표시 — 노드에 발만 들인 상태와 실제 교전을 가르는 유일한 근거</summary>
public sealed class CombatMarker
{
    public string NodeId { get; set; } = "";

    /// <summary>표시용 타임스탬프. 게임 규칙 판정에는 절대 쓰지 않는다</summary>
    public long StartedAt { get; set; }
}

/// <summary>보스에게 가는 보급품 — 보스전에서 개봉한다 (ADR-024 ③)</summary>
public sealed class ParcelState
{
    public bool Opened { get; set; }
}

/// <summary>진행 중인 런 하나 (state.js 의 reviewhero.run)</summary>
public sealed class RunState
{
    public uint Seed { get; set; }
    public int Act { get; set; } = 1;
    public int Floor { get; set; } = 1;

    /// <summary>지금 발을 들인 노드 id. 완료 전에는 남아 있고 CompleteNode 가 비운다</summary>
    public string? Pos { get; set; }

    public int Gold { get; set; }
    public int Will { get; set; }
    public int MaxWill { get; set; }
    public List<string> Deck { get; set; } = new();
    public MapData Map { get; set; } = new();

    /// <summary>지나온 노드 id — 지도가 경로를 그리고 Reachable 이 분기를 판정한다</summary>
    public List<string> Path { get; set; } = new();

    public Dictionary<Suit, int> SuitCounters { get; set; } = new();
    public Suit? LastSuit { get; set; }
    public int BattlesWon { get; set; }

    /// <summary>"death" | "clear" | null — 정산(FinalizeRun) 전까지 지도가 잠긴다</summary>
    public string? Ended { get; set; }

    public CombatMarker? Combat { get; set; }
    public ParcelState Parcel { get; set; } = new();

    /// <summary>ADR-028 대비. 지금은 "default"</summary>
    public string CharacterId { get; set; } = "default";

    /// <summary>표시용. 게임 규칙 판정에는 쓰지 않는다</summary>
    public string StartedAt { get; set; } = "";

    [JsonIgnore]
    public MapNode? CurrentNode => Map.Find(Floor, Pos);
}

// ── 메타(계정 누적) ─────────────────────────────────────

/// <summary>판정별 카운트 (card-system-v2 §2)</summary>
public sealed class JudgementCounts
{
    public int Origin { get; set; }
    public int Fact { get; set; }
    public int Normal { get; set; }
    public int Fumble { get; set; }

    public void Add(Judgement j, int n)
    {
        switch (j)
        {
            case Judgement.Origin: Origin += n; break;
            case Judgement.Fact: Fact += n; break;
            case Judgement.Normal: Normal += n; break;
            case Judgement.Fumble: Fumble += n; break;
        }
    }
}

/// <summary>업적·도감 계측 누적 (state.js STATS0). 소급 계산이 불가능한 값들이라 판정 로직보다 먼저 심는다</summary>
public sealed class StatsState
{
    public int Submissions { get; set; }
    public JudgementCounts Judgements { get; set; } = new();
    public int Crits { get; set; }
    public int CritMisses { get; set; }
    public int BattlesWon { get; set; }
    public int SurrenderWins { get; set; }
    public int Retreats { get; set; }
    public int CardsRemoved { get; set; }

    /// <summary>최소 의지 승리 기록 (미달성 null)</summary>
    public int? MinWillWin { get; set; }

    public int DefenseAbsorbed { get; set; }
    public int WillHealed { get; set; }
    public int ParcelsOpened { get; set; }
}

/// <summary>원정대 명단 한 줄</summary>
public sealed class ExpeditionEntry
{
    public string Name { get; set; } = "무명";
    public bool Me { get; set; } = true;

    /// <summary>"clear" | "death"</summary>
    public string Result { get; set; } = "death";

    public int Floor { get; set; }
    public int Stars { get; set; }
    public string Review { get; set; } = "";

    /// <summary>"게시" | "계류" — 전투 승리 0회 사망의 유언은 집계 제외 (GDD §4.3)</summary>
    public string Status { get; set; } = "게시";

    public string Date { get; set; } = "";
}

/// <summary>계정 누적 (state.js 의 reviewhero.meta)</summary>
public sealed class MetaState
{
    public int Runs { get; set; }
    public int Wins { get; set; }
    public int BestFloor { get; set; }
    public int Rp { get; set; }
    public int P { get; set; }
    public List<ExpeditionEntry> Expedition { get; set; } = new();

    /// <summary>한 번이라도 손에 넣은 카드 id (도감 원천)</summary>
    public List<string> Seen { get; set; } = new();

    /// <summary>등재된 업적 id</summary>
    public List<string> Badges { get; set; } = new();

    public StatsState Stats { get; set; } = new();

    /// <summary>ADR-028 대비</summary>
    public string CharacterId { get; set; } = "default";
}

public sealed class SettingsState
{
    public double TextSpeed { get; set; } = 1;
    public bool Shake { get; set; } = true;
    public bool Debug { get; set; } = true;
}

// ── 이어하기 안내 ───────────────────────────────────────

public enum ResumeKind
{
    Map,
    Combat,
    Result,
}

/// <summary>중단 지점 안내 — 타이틀이 읽어 「이어하기」 문구와 목적지를 만든다</summary>
public sealed record ResumeInfo(
    ResumeKind Kind,
    string? NodeId,
    NodeType? NodeType,
    int Floor,
    string ScenePath,
    string Label);

// ── 저장 파일 ───────────────────────────────────────────

/// <summary>user://save.json 의 최상위 구조</summary>
public sealed class SaveFile
{
    public int V { get; set; } = 1;
    public string? Penname { get; set; }
    public MetaState Meta { get; set; } = new();
    public RunState? Run { get; set; }
    public SettingsState Settings { get; set; } = new();
}

// ── 저장소 ──────────────────────────────────────────────

/// <summary>
/// 런/메타의 단일 창구. 웹판 window.RH 에 대응한다.
/// 정적 상태라 씬을 갈아타도 살아 있고, 변경은 반드시 <see cref="Save"/> 로 디스크에 내린다.
/// </summary>
public static class RunStore
{
    /// <summary>저장 경로. Godot 의 앱 데이터 경로(macOS 는 ~/Library/Application Support/Godot/app_userdata/…)</summary>
    public const string SavePath = "user://save.json";

    private static SaveFile? _save;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static SaveFile Data => _save ??= LoadFromDisk();

    /// <summary>진행 중인 런 (없으면 null)</summary>
    public static RunState? Current => Data.Run;

    public static MetaState Meta => Data.Meta;

    public static SettingsState Settings => Data.Settings;

    public static string Penname
    {
        get => string.IsNullOrWhiteSpace(Data.Penname) ? "무명" : Data.Penname!;
        set { Data.Penname = value; Save(); }
    }

    // ── 저장/로드 ────────────────────────────────────

    /// <summary>디스크를 다시 읽는다 (테스트의 왕복 검증용 — 평소에는 부를 일이 없다)</summary>
    public static void Reload() => _save = LoadFromDisk();

    private static SaveFile LoadFromDisk()
    {
        try
        {
            string real = Platform.GlobalizePath(SavePath);
            if (!File.Exists(real)) return new SaveFile();
            var loaded = JsonSerializer.Deserialize<SaveFile>(File.ReadAllText(real), JsonOpts);
            return Normalize(loaded ?? new SaveFile());
        }
        catch (Exception e)
        {
            // 세이브가 깨졌다고 게임이 안 켜지면 안 된다 — 새 파일로 출발하고 사실만 남긴다
            Platform.Print($"[RunStore] 세이브 로드 실패 — 새로 시작한다: {e.Message}");
            return new SaveFile();
        }
    }

    /// <summary>저장본에 없는 필드를 기본값으로 채운다 (state.js getMeta 의 층별 병합 대응)</summary>
    private static SaveFile Normalize(SaveFile s)
    {
        s.Meta ??= new MetaState();
        s.Meta.Expedition ??= new List<ExpeditionEntry>();
        s.Meta.Seen ??= new List<string>();
        s.Meta.Badges ??= new List<string>();
        s.Meta.Stats ??= new StatsState();
        s.Meta.Stats.Judgements ??= new JudgementCounts();
        if (string.IsNullOrEmpty(s.Meta.CharacterId)) s.Meta.CharacterId = "default";
        s.Settings ??= new SettingsState();
        if (s.Run is { } r)
        {
            r.Map ??= new MapData();
            r.Map.Floors ??= new List<List<MapNode>>();
            r.Deck ??= new List<string>();
            r.Path ??= new List<string>();
            r.SuitCounters ??= new Dictionary<Suit, int>();
            r.Parcel ??= new ParcelState();
            if (string.IsNullOrEmpty(r.CharacterId)) r.CharacterId = "default";
        }
        return s;
    }

    public static void Save()
    {
        try
        {
            string real = Platform.GlobalizePath(SavePath);
            string? dir = Path.GetDirectoryName(real);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(real, JsonSerializer.Serialize(Data, JsonOpts));
        }
        catch (Exception e)
        {
            Platform.Print($"[RunStore] 세이브 실패: {e.Message}");
        }
    }

    public static void ClearRun()
    {
        Data.Run = null;
        Save();
    }

    /// <summary>세이브 전체 삭제 (자동 플레이 검증이 깨끗한 상태에서 출발하려고 쓴다)</summary>
    public static void WipeAll()
    {
        _save = new SaveFile();
        Save();
    }

    // ── 런 생성 ──────────────────────────────────────

    /// <summary>
    /// 새 런. 시드를 안 주면 여기서 한 번만 뽑는다 — 게임 규칙은 전부 이 시드에서 파생되며
    /// 그 뒤로는 어떤 코드도 비결정 난수를 쓰지 않는다.
    /// </summary>
    public static RunState NewRun(uint? seed = null)
    {
        uint s = seed ?? (uint)Random.Shared.NextInt64(0, 0xffffffffL);
        int will = RulesConfig.Default.Player.Will;   // 시작 의지의 정본은 엔진 rules (ADR-025)
        var run = new RunState
        {
            Seed = s,
            Act = 1,
            Floor = 1,
            Pos = null,
            CharacterId = Data.Meta.CharacterId,
            Gold = 0,
            Will = will,
            MaxWill = will,
            Deck = GameData.StartingDeck.ToList(),
            Map = MapGen.Generate(s),
            SuitCounters = new Dictionary<Suit, int>(),
            LastSuit = null,
            Path = new List<string>(),
            Parcel = new ParcelState(),
            BattlesWon = 0,
            StartedAt = DateTime.UtcNow.ToString("O"),
        };
        Data.Run = run;
        Save();
        RecordSeen(run.Deck);   // 시작 덱도 도감에 오른다 — 카드를 손에 넣는 첫 경로다
        return run;
    }

    // ── 전투 표시 ────────────────────────────────────

    /// <summary>전투가 실제로 시작됐다. Combat 이 Battle 을 만든 직후 부른다</summary>
    public static void BeginCombat(string? nodeId = null)
    {
        var run = Current;
        if (run is null) return;
        string? id = nodeId ?? run.Pos;
        if (id is null) return;
        run.Combat = new CombatMarker { NodeId = id, StartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        Save();
    }

    /// <summary>전투가 끝났다(승/패/항복/이탈). 이후의 중단은 「지도」로 복원된다</summary>
    public static void EndCombat()
    {
        var run = Current;
        if (run?.Combat is null) return;
        run.Combat = null;
        Save();
    }

    // ── 이동 ─────────────────────────────────────────

    /// <summary>
    /// 지금 선택할 수 있는 노드 id — 직전에 지나온 노드의 Next 만 갈 수 있다.
    /// 1층(경로 없음)은 전부 열린다.
    ///
    /// **세이브 스커밍 차단**: 노드에 들어갔는데 아직 끝내지 않았다면(Pos 가 남아 있다) 그 노드만
    /// 돌려준다. 완료는 CompleteNode 가 Path 에 적고 Pos 를 비우는 것이 유일한 경로라
    /// 「Pos 는 있는데 완료는 안 됨」 = 중도 이탈이다. 이걸 열어 두면 지고 있는 전투를 닫고
    /// 같은 층의 더 쉬운 노드로 갈아탈 수 있다 — 재전은 허용하되 갈아타기는 막는다.
    /// </summary>
    public static IReadOnlyList<string> Reachable()
    {
        var run = Current;
        if (run is null) return Array.Empty<string>();
        var row = run.Map.Row(run.Floor);
        var all = row.Select(n => n.Id).ToList();
        if (run.Ended is not null) return Array.Empty<string>();     // 정산 전이다 — 지도는 잠긴다
        if (run.Pos is { } pos && all.Contains(pos)) return new[] { pos };
        string? prevId = run.Path.Count > 0 ? run.Path[^1] : null;
        if (prevId is null) return all;
        var prev = run.Map.Find(run.Floor - 1, prevId);
        if (prev is null || prev.Next.Count == 0) return all;
        var open = prev.Next.Where(all.Contains).ToList();
        return open.Count > 0 ? open : all;
    }

    /// <summary>
    /// 지도에서 노드 선택 → Pos 를 새기고 해당 씬으로 이동한다.
    /// navigate:false 는 씬 전환 없이 상태만 옮긴다 (헤드리스 자동 플레이 검증용).
    /// </summary>
    public static bool EnterNode(string nodeId, bool navigate = true)
    {
        var run = Current;
        if (run is null) return false;
        var node = run.Map.Find(run.Floor, nodeId);
        if (node is null) return false;
        if (!Reachable().Contains(nodeId)) return false;   // 경로 밖 — 배송 경로를 벗어날 수 없다
        run.Pos = nodeId;
        Save();
        if (navigate) SceneRouter.GoToNode(node);
        return true;
    }

    /// <summary>
    /// 노드가 끝났을 때 호출. 반환값 = 다음에 이동할 씬 경로 (SceneRouter.Go 로 옮긴다).
    /// 보스를 잡으면 런이 끝난다 — 정복 후기를 올릴 때까지 지도를 잠근다 (사망과 대칭).
    /// </summary>
    public static string CompleteNode(int gold = 0, int will = 0, string? deckAdd = null, int? deckRemoveIdx = null)
    {
        var run = Current;
        if (run is null) return SceneRouter.Title;
        var node = run.CurrentNode;
        // 노드 진입 없이 호출되면 층을 공짜로 넘기지 않는다
        if (node is null) return SceneRouter.Map;

        node.Visited = true;
        run.Path.Add(node.Id);
        if (gold != 0) run.Gold = Math.Max(0, run.Gold + gold);
        if (will != 0) run.Will = Math.Max(1, Math.Min(run.MaxWill, run.Will + will));
        if (deckAdd is not null)
        {
            run.Deck.Add(deckAdd);
            RecordSeen(new[] { deckAdd });   // 카드를 손에 넣는 모든 경로가 여기를 지난다
        }
        if (deckRemoveIdx is { } idx && idx >= 0 && idx < run.Deck.Count)
        {
            run.Deck.RemoveAt(idx);
            BumpCardsRemoved();
        }

        bool isBoss = node.Type == NodeType.Boss;
        run.Pos = null;
        run.Combat = null;               // 노드를 끝냈으니 「전투 중」 표시도 함께 걷는다
        if (!isBoss) run.Floor += 1;
        // 이미 결말이 정해진 런(보스전 패배 등)을 덮지 않는다.
        // 덮으면 진 판이 클리어로 집계된다 — 헤드리스 완주 검증에서 실제로 잡힌 버그다.
        if (isBoss && run.Ended is null) run.Ended = "clear";
        Save();
        return isBoss ? SceneRouter.Result : SceneRouter.Map;
    }

    /// <summary>런 종료 표시 — Result 의 FinalizeRun 이 정산할 때까지 지도가 잠긴다</summary>
    public static void MarkEnded(string outcome)
    {
        var run = Current;
        if (run is null) return;
        run.Ended = outcome;             // "death" | "clear"
        run.Combat = null;
        Save();
    }

    /// <summary>
    /// 이어하기 안내. 전투 중 이탈은 그 노드로 강제 복귀하며 전투는 처음부터 다시 시작한다
    /// (전투 상태는 저장하지 않는다 — 재전은 허용, 노드 갈아타기만 막는다).
    /// </summary>
    public static ResumeInfo? Resume()
    {
        var run = Current;
        if (run is null) return null;
        var node = run.CurrentNode;

        // 끝났는데 유언을 아직 안 올렸다면 결과 화면으로 돌려보낸다 —
        // 그냥 두면 의지 0으로 그 노드에 갇힌다(진입해도 즉시 다시 죽는다)
        if (run.Ended is { } ended)
        {
            string label = ended == "death"
                ? "중단 지점: 💀 마지막 리뷰를 아직 올리지 않았다"
                : "중단 지점: 🏁 정복 후기를 아직 올리지 않았다";
            return new ResumeInfo(ResumeKind.Result, run.Pos, null, run.Floor, SceneRouter.Result, label);
        }

        bool inCombat = node is not null && run.Combat is not null && run.Combat.NodeId == node.Id;
        if (inCombat)
        {
            return new ResumeInfo(ResumeKind.Combat, node!.Id, node.Type, run.Floor,
                SceneRouter.Combat, "중단 지점: ⚔ 전투 — 처음부터 다시 붙습니다");
        }
        return new ResumeInfo(ResumeKind.Map, node?.Id, node?.Type, run.Floor,
            SceneRouter.Map, $"중단 지점: 지도 (1막 {run.Floor}층)");
    }

    // ── 정산 ─────────────────────────────────────────

    /// <summary>
    /// 런 종료 정산. Result 가 리뷰(유언) 제출 시 호출한다.
    /// 보상 수치는 GDD §4.2 를 개발용으로 단순화한 값 — 밸런스 라운드에서 재조정.
    /// </summary>
    public static MetaState FinalizeRun(string outcome, int stars, string text)
    {
        var run = Current;
        var meta = Data.Meta;
        if (run is null) return meta;

        meta.Runs += 1;
        int reachedFloor = run.Floor;
        bool newBest = reachedFloor > meta.BestFloor;
        if (newBest) meta.BestFloor = reachedFloor;
        if (outcome == "clear") { meta.Wins += 1; meta.Rp += 40; meta.P += 23; }
        else { if (newBest) meta.Rp += 5; meta.P += Math.Min(8, reachedFloor); }

        meta.Expedition.Insert(0, new ExpeditionEntry
        {
            Name = Penname,
            Me = true,
            Result = outcome,
            Floor = reachedFloor,
            Stars = stars,
            Review = text,
            // 전투 승리 0회인 런의 유언은 집계 제외(자살 파밍 차단, GDD §4.3) → 계류
            Status = outcome == "death" && run.BattlesWon == 0 ? "계류" : "게시",
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
        });

        Data.Run = null;
        Save();
        return meta;
    }

    // ── 계측 ─────────────────────────────────────────

    /// <summary>도감 등재. 이미 본 카드는 무시하며, 새로 등재한 장수를 돌려준다</summary>
    public static int RecordSeen(IEnumerable<string>? ids)
    {
        if (ids is null) return 0;
        var seen = Data.Meta.Seen;
        var has = seen.ToHashSet(StringComparer.Ordinal);
        int added = 0;
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id) || !has.Add(id)) continue;
            seen.Add(id);
            added++;
        }
        if (added > 0) Save();
        return added;
    }

    /// <summary>업적 등재. 판정 로직은 다음 단계이며 여기서는 등재 경로만 연다</summary>
    public static int RecordBadges(IEnumerable<string>? ids)
    {
        if (ids is null) return 0;
        var badges = Data.Meta.Badges;
        var has = badges.ToHashSet(StringComparer.Ordinal);
        int added = 0;
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id) || !has.Add(id)) continue;
            badges.Add(id);
            added++;
        }
        if (added > 0) Save();
        return added;
    }

    /// <summary>덱에서 지운 카드 카운터 — 상점 파쇄는 덱을 직접 만지므로 상점이 따로 부른다</summary>
    public static void BumpCardsRemoved(int n = 1)
    {
        Data.Meta.Stats.CardsRemoved += n;
        Save();
    }

    public static void BumpParcelsOpened(int n = 1)
    {
        Data.Meta.Stats.ParcelsOpened += n;
        Save();
    }

    /// <summary>
    /// 전투 1판의 엔진 계측(<see cref="BattleStats"/>)을 계정 누적으로 옮긴다.
    /// result: "win" | "lose" | "timeout" | "retreat"
    /// </summary>
    public static StatsState MergeBattleStats(BattleStats? bs, string result, int willLeft)
    {
        var s = Data.Meta.Stats;
        if (bs is null) return s;
        s.Submissions += bs.Submissions;
        foreach (var (j, n) in bs.Judgements) s.Judgements.Add(j, n);
        s.Crits += bs.Crits.Count;
        s.CritMisses += bs.CritMisses;
        s.DefenseAbsorbed += bs.DefenseAbsorbed;
        s.WillHealed += bs.WillHealed;
        if (bs.Surrender) s.SurrenderWins += 1;
        if (result == "retreat") s.Retreats += 1;
        if (result == "win")
        {
            s.BattlesWon += 1;
            if (s.MinWillWin is null || willLeft < s.MinWillWin) s.MinWillWin = willLeft;
        }
        Save();
        return s;
    }
}
