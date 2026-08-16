// design/*.yaml 을 한 번만 읽어 들고 있는 자리.
//
// 로드는 data/Loader 가 소유한다(엔진은 fs 무의존 — GDD §1.1). 씬마다 LoadAll() 을 부르면
// 같은 YAML 을 수십 번 파싱하게 되므로 여기서 캐시한다. 전투 담당도 GameData.All 을 쓰면 된다.

using ReviewHero.Data;
using ReviewHero.Engine;

namespace ReviewHero.Game.Run;

public static class GameData
{
    private static LoadedData? _all;

    public static LoadedData All => _all ??= Loader.LoadAll();

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
