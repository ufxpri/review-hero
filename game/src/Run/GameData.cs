// 게임 데이터(카드·적)를 한 번만 읽어 들고 있는 자리.
//
// 파싱·검증은 data/Loader 가 소유한다(엔진은 fs 무의존 — GDD §1.1). 씬마다 로드를 부르면
// 같은 YAML 을 수십 번 파싱하게 되므로 여기서 캐시한다. 전투 담당도 GameData.All 을 쓰면 된다.
//
// ── 읽는 자리가 왜 여기냐 ──────────────────────────────
// 내보낸 실행 파일에서는 저장소의 design/*.yaml 이 옆에 없다. Godot 은 리소스를 .pck 로 묶고
// res:// 가상 경로로만 열어 주므로 System.IO 로는 못 읽는다. 그렇다고 data/ 가 res:// 를 알면
// Godot 에 묶여 ADR-029(engine/data 는 Godot 무의존)가 깨진다. 그래서 **읽기만 여기서** 하고
// 로더에는 본문 문자열을 넘긴다(Loader.LoadAllFromText).
//
//   1순위 res://data/*.yaml  — export preset 의 include_filter 로 .pck 에 들어간다.
//                              내보낸 빌드에서 동작하는 유일한 경로다.
//   2순위 Loader.LoadAll()   — 저장소 design/ 탐색. 링크가 빠졌거나 .pck 에 데이터가 안 들어간
//                              개발 환경을 위한 폴백.
// 어느 쪽을 탔는지 한 줄 찍는다 — 내보낸 빌드가 조용히 폴백으로 굴러가는 사고를 막는다.

using Godot;
using ReviewHero.Data;
using ReviewHero.Engine;

namespace ReviewHero.Game.Run;

public static class GameData
{
    private const string CardsResPath = "res://data/" + Loader.CardsFileName;
    private const string EnemiesResPath = "res://data/" + Loader.EnemiesFileName;

    private static LoadedData? _all;

    public static LoadedData All => _all ??= Load();

    /// <summary>진단용 — 마지막으로 탄 경로("res://" 또는 "design/")</summary>
    public static string Source { get; private set; } = "(미로드)";

    private static LoadedData Load()
    {
        if (LoadFromRes() is { } fromRes) return fromRes;

        var data = Loader.LoadAll();
        Source = $"design/ ({Loader.DesignDir})";
        GD.Print($"[GameData] {Source} 에서 로드 — 카드 {data.Cards.AllIds.Count}장 · 적 {data.Enemies.Count}종");
        return data;
    }

    private static LoadedData? LoadFromRes()
    {
        try
        {
            string cards = Godot.FileAccess.GetFileAsString(CardsResPath);
            string enemies = Godot.FileAccess.GetFileAsString(EnemiesResPath);
            // GetFileAsString 은 파일이 없으면 예외가 아니라 빈 문자열을 준다 — 직접 걸러낸다.
            if (cards.Length == 0 || enemies.Length == 0)
            {
                GD.Print($"[GameData] {CardsResPath} 없음(err={Godot.FileAccess.GetOpenError()}) — design/ 폴백");
                return null;
            }

            var data = Loader.LoadAllFromText(cards, enemies);
            Source = "res://data/";
            GD.Print($"[GameData] {Source} 에서 로드 — 카드 {data.Cards.AllIds.Count}장 · 적 {data.Enemies.Count}종");
            return data;
        }
        catch (System.Exception e)
        {
            // YAML 이 깨졌으면 폴백도 같은 내용을 읽어 같은 자리에서 실패한다 — 그래도 조용히 죽지 않게 남긴다.
            GD.PushWarning($"[GameData] res:// 로드 실패: {e.Message} — design/ 폴백 시도");
            return null;
        }
    }

    public static CardIndex Cards => All.Cards;

    public static IReadOnlyList<string> StartingDeck => All.StartingDeck;

    public static IReadOnlyDictionary<string, EnemyDef> Enemies => All.Enemies;

    /// <summary>적 이름 (없으면 id 를 그대로 — 지도가 빈칸을 그리지 않게)</summary>
    public static string EnemyName(string? id) =>
        id is not null && All.Enemies.TryGetValue(id, out var e) ? e.Name : id ?? "";

    /// <summary>적 의지 (지도 표시용, 없으면 0)</summary>
    public static int EnemyWill(string? id) =>
        id is not null && All.Enemies.TryGetValue(id, out var e) ? e.Will : 0;

    public static string CardName(string id) =>
        All.Cards.ById.TryGetValue(id, out var c) ? c.Name : id;
}
