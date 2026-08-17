// 등재 기록(업적)의 정의 — 이 파일이 목록의 정본이다.
//
// ── 왜 GDD 가 아니라 코드에 두는가 ──────────────────────
// design/GDD-v1.0.md 를 뒤져도 업적/등재 기록을 다루는 절이 없다(§4 런 구조·메타는 재화·유언만
// 다룬다). 없는 절을 새로 여는 것은 이 작업의 범위를 넘고, 정본 문서는 "현재 확정 상태만
// 담백하게"(CLAUDE.md) 담아야 하는데 여기 조건 수치는 밸런스 라운드에서 통째로 흔들릴 잠정값이다.
// 그래서 지금은 코드가 정본이고, 조건이 굳으면 GDD §4 에 절을 열어 옮긴다.
//
// ── 무엇을 업적으로 삼을 수 있는가 ──────────────────────
// **이미 쌓이고 있는 계측값만 쓴다.** 원천은 <see cref="MetaState"/> 의 Stats(=StatsState)·
// Runs·Wins·BestFloor·Seen 뿐이며, 여기 없는 지표를 요구하는 조건은 영원히 달성되지 않으므로
// 만들지 않는다. 「한 턴에 ~」·「연속 ~」류가 전부 빠진 이유가 이것이다 — 그런 계측이 없다.
//
// ── 언제 판정하는가 ─────────────────────────────────────
// <see cref="Evaluate"/> 는 MetaState 만 읽는 순수 함수다. RunState.cs 의 정산(FinalizeRun)에
// 판정을 심으면 그 파일을 고쳐야 하고, 소급 판정도 안 된다(그 전에 쌓인 기록이 등재되지 않는다).
// 그래서 **화면에 들어올 때 전량 재평가**한다 — 조건을 나중에 추가해도 과거 기록이 그대로 등재된다.
//
// ── 어휘 ────────────────────────────────────────────────
// 이 세계에서 「업적」이라는 말은 쓰지 않는다. 대장에 오른 것은 **등재**다(worldview §6).
// 문구는 풍자 8 : 판타지 2 (§5.1) — 커머스·물류·자영업의 말투로 쓰고, 실존 기업은 언급하지 않는다.

using ReviewHero.Game.Run;

namespace ReviewHero.Game.Meta;

/// <summary>등재 항목 하나. 조건은 「Value(meta) ≥ Goal」 단일 형태로 통일했다 — 진행도를 항상 그릴 수 있다</summary>
public sealed class BadgeDef
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>미등재일 때 보여주는 조건 한 줄</summary>
    public required string Cond { get; init; }

    /// <summary>등재된 뒤 보여주는 기록 문구</summary>
    public required string Flavor { get; init; }

    /// <summary>목록에서 이 항목을 대표하는 글자 (도장 자리)</summary>
    public string Seal { get; init; } = "✓";

    /// <summary>등재되기 전에는 이름도 조건도 가린다</summary>
    public bool Hidden { get; init; }

    /// <summary>지금까지 쌓인 값</summary>
    public required Func<MetaState, int> Value { get; init; }

    /// <summary>등재에 필요한 값. 1이면 단발 항목이라 진행도 막대를 그리지 않는다</summary>
    public required Func<MetaState, int> Goal { get; init; }

    /// <summary>진행도 대신 보여줄 문구 (단발 항목의 「아직 없음」 등)</summary>
    public Func<MetaState, string>? Detail { get; init; }

    public int Have(MetaState m) => Math.Max(0, Value(m));

    public int Need(MetaState m) => Math.Max(1, Goal(m));

    public bool Earned(MetaState m) => Have(m) >= Need(m);

    /// <summary>0~1</summary>
    public float Ratio(MetaState m) => Math.Clamp(Have(m) / (float)Need(m), 0f, 1f);
}

public static class BadgeDefs
{
    /// <summary>
    /// 등재 항목 20건. 순서가 곧 화면 순서다 — 게시 → 판정 → 베스트 리뷰 → 전투 → 살림 → 수집.
    /// </summary>
    public static readonly IReadOnlyList<BadgeDef> All = new BadgeDef[]
    {
        // ── 게시 (Submissions) ────────────────────────
        new()
        {
            Id = "post-first", Name = "첫 게시", Seal = "✍",
            Cond = "리뷰를 1건 올린다",
            Flavor = "올렸다. 이제 지울 수 있는 자는 없다.",
            Value = m => m.Stats.Submissions, Goal = _ => 1,
        },
        new()
        {
            Id = "post-100", Name = "백 편", Seal = "📄",
            Cond = "리뷰를 100건 올린다",
            Flavor = "백 번 썼고 백 번 다 남아 있다.",
            Value = m => m.Stats.Submissions, Goal = _ => 100,
        },
        new()
        {
            Id = "post-500", Name = "상습 게시자", Seal = "🗂",
            Cond = "리뷰를 500건 올린다",
            Flavor = "대장 어딘가에 같은 필명이 오백 번 적혀 있다.",
            Value = m => m.Stats.Submissions, Goal = _ => 500,
        },

        // ── 판정 (Judgements) ─────────────────────────
        new()
        {
            Id = "origin-30", Name = "직접 써 봤습니다", Seal = "📍",
            Cond = "원산지 판정을 30회 받는다",
            Flavor = "겪은 사람의 말에는 「평가 불가 항목」이 통하지 않는다.",
            Value = m => m.Stats.Judgements.Origin, Goal = _ => 30,
        },
        new()
        {
            Id = "fact-150", Name = "사실 관계 확인", Seal = "●",
            Cond = "팩트 판정을 150회 받는다",
            Flavor = "들어맞는 말만 골라 썼다. 독자는 그런 걸 안다.",
            Value = m => m.Stats.Judgements.Fact, Goal = _ => 150,
        },
        new()
        {
            Id = "fumble-100", Name = "그래도 올렸습니다", Seal = "⚠", Hidden = true,
            Cond = "헛소리 판정을 100회 받는다",
            Flavor = "헛소리도 지워지지 않는다. 그것이 이 세계의 공평함이다.",
            Value = m => m.Stats.Judgements.Fumble, Goal = _ => 100,
        },

        // ── 베스트 리뷰 (Crits) ───────────────────────
        new()
        {
            Id = "crit-first", Name = "이 라운드 베스트 리뷰", Seal = "★",
            Cond = "크리티컬 리뷰를 1회 쓴다",
            Flavor = "신뢰도 10을 전부 태워 한 편을 냈다.",
            Value = m => m.Stats.Crits, Goal = _ => 1,
        },
        new()
        {
            Id = "crit-30", Name = "상습 베스트", Seal = "🏆",
            Cond = "크리티컬 리뷰를 30회 쓴다",
            Flavor = "베스트 리뷰는 리뷰 한 건에 붙는 표식이다. 그걸 서른 번 받았다.",
            Value = m => m.Stats.Crits, Goal = _ => 30,
        },
        new()
        {
            Id = "critmiss-20", Name = "부재중 방문", Seal = "🚪", Hidden = true,
            Cond = "크리티컬 리뷰가 20회 빗나간다",
            Flavor = "마감까지 썼는데 받는 쪽이 자리에 없었다. 스무 번이나.",
            Value = m => m.Stats.CritMisses, Goal = _ => 20,
        },

        // ── 전투 (BattlesWon·Surrender·Retreat) ───────
        new()
        {
            // 이 게임의 소리도 그림도 검이 아니라 사무·물류다(SfxId 머리말) — 도장 글자도 그 규칙을 따른다
            Id = "battle-first", Name = "첫 접수", Seal = "🧾",
            Cond = "전투에서 1회 이긴다",
            Flavor = "한 건 처리했다. 처리할 것이 아직 많다.",
            Value = m => m.Stats.BattlesWon, Goal = _ => 1,
        },
        new()
        {
            Id = "battle-50", Name = "오십 건 처리", Seal = "📋",
            Cond = "전투에서 50회 이긴다",
            Flavor = "상점가에 소문이 돈다. 저 사람은 답글을 받아 낸다고.",
            Value = m => m.Stats.BattlesWon, Goal = _ => 50,
        },
        new()
        {
            Id = "surrender-10", Name = "자진 폐업 열 곳", Seal = "🏚",
            Cond = "장비를 전부 부숴 항복을 10회 받는다",
            Flavor = "때린 적도 없는데 문을 닫았다. 진열대만 비웠을 뿐이다.",
            Value = m => m.Stats.SurrenderWins, Goal = _ => 10,
        },
        new()
        {
            Id = "retreat-first", Name = "수취 거부", Seal = "↩", Hidden = true,
            Cond = "전투에서 1회 물러난다",
            Flavor = "물러난 것도 기록이다. 대장은 사유를 묻지 않는다.",
            Value = m => m.Stats.Retreats, Goal = _ => 1,
        },
        new()
        {
            Id = "minwill-1", Name = "의지 하나", Seal = "🧠", Hidden = true,
            Cond = "의지 1만 남기고 전투에서 이긴다",
            Flavor = "한 대만 더 맞았으면 명단에 이름이 올라갔다.",
            Value = m => m.Stats.MinWillWin is { } w && w <= 1 ? 1 : 0, Goal = _ => 1,
            Detail = m => m.Stats.MinWillWin is { } w ? $"최소 잔여 의지 {w}" : "승리 기록 없음",
        },

        // ── 살림 (방어·회복·덱·보급품) ────────────────
        new()
        {
            Id = "defense-500", Name = "완충 포장", Seal = "🛡",
            Cond = "방어로 좋아요 500을 흡수한다",
            Flavor = "맞을 것을 미리 싸 두는 것도 필력이다.",
            Value = m => m.Stats.DefenseAbsorbed, Goal = _ => 500,
        },
        new()
        {
            Id = "heal-300", Name = "멘탈 관리", Seal = "☕",
            Cond = "의지를 누적 300 회복한다",
            Flavor = "깎이는 것보다 빨리 채우면 존재는 계속된다.",
            Value = m => m.Stats.WillHealed, Goal = _ => 300,
        },
        new()
        {
            Id = "shred-30", Name = "초고 정리", Seal = "🗑",
            Cond = "덱에서 리뷰 30장을 덜어낸다",
            Flavor = "안 쓸 초고를 들고 다니는 것도 무게다.",
            Value = m => m.Stats.CardsRemoved, Goal = _ => 30,
        },
        new()
        {
            Id = "parcel-first", Name = "개봉기", Seal = "📦",
            Cond = "보스에게 가는 보급품을 1회 개봉한다",
            Flavor = "받는 사람이 열지 않은 택배를 대신 열었다.",
            Value = m => m.Stats.ParcelsOpened, Goal = _ => 1,
        },

        // ── 수집·원정 (Seen·Wins) ─────────────────────
        new()
        {
            Id = "codex-all", Name = "만물대장 완독", Seal = "📕",
            Cond = "리뷰 전 종을 손에 넣어 도감에 등재한다",
            Flavor = "장부에 적힌 것을 전부 자기 눈으로 확인했다.",
            Value = m => m.Seen.Count, Goal = _ => Math.Max(1, GameData.Cards.ById.Count),
        },
        new()
        {
            Id = "clear-first", Name = "생환", Seal = "🏁",
            Cond = "1막을 돌파하고 정복 후기를 올린다",
            Flavor = "명단에서 유일하게 살아 돌아온 줄이 되었다.",
            Value = m => m.Wins, Goal = _ => 1,
        },
    };

    /// <summary>등재 항목 총수 — 화면의 「N/총수」 분모</summary>
    public static int Total => All.Count;

    public static BadgeDef? Find(string id) => All.FirstOrDefault(b => b.Id == id);

    /// <summary>
    /// 조건을 만족한 항목 전부. **MetaState 만 읽는 순수 함수다** — 세이브를 건드리지 않으므로
    /// 테스트에서도 화면에서도 같은 답을 낸다.
    /// </summary>
    public static IReadOnlyList<string> Evaluate(MetaState meta) =>
        All.Where(b => b.Earned(meta)).Select(b => b.Id).ToList();

    /// <summary>
    /// 평가해서 아직 대장에 안 올라간 것을 올린다. 화면 진입 시 부르면 과거 기록도 소급 등재된다.
    /// 반환값 = 이번에 새로 오른 건수 (0이면 세이브를 쓰지 않는다 — RecordBadges 가 그렇게 동작한다).
    /// </summary>
    public static int Sync(MetaState meta) => RunStore.RecordBadges(Evaluate(meta));
}
