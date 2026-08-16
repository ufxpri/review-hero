// TS 테스트 헬퍼 이관 (packages/sim/test/*.test.ts 의 makeBattle·uid·eWill·slashDamage·
// defenseTotal·assertPreviewMatches + packages/core/test 공용부).
//
// **이관 원칙 (ADR-029)**: 검증 의도를 옮기되 밸런스 리터럴은 옮기지 않는다.
// 적 의지·행동 피해 같은 수치는 여기서 YAML(design/)을 읽어 상수화한다 — 규칙 테스트가
// 밸런스 조정으로 깨지지 않게 하는 장치다. 수치 자체의 잠금은 BalanceV1_1Tests 가 담당한다.

using ReviewHero.Data;
using ReviewHero.Engine;

namespace ReviewHero.Engine.Tests;

/// <summary>저장소 경로 — 테스트 어셈블리 위치에서 ReviewHero.sln 을 찾아 올라간다</summary>
public static class TestPaths
{
    public static readonly string RepoRoot = FindRepoRoot();

    public static string DesignDir => Path.Combine(RepoRoot, "design");

    /// <summary>bad-tag.yaml 등 — csproj 가 출력 디렉터리로 복사한다</summary>
    public static string FixturesDir => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ReviewHero.sln"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("ReviewHero.sln 을 찾지 못했다 — 저장소 루트 확인 필요");
    }
}

/// <summary>실데이터(cards-v2.0.yaml / enemies-v1.0.yaml) 1회 로드 + 테스트 공용 헬퍼</summary>
public static class TestData
{
    private static readonly LoadedData Loaded = Loader.LoadAll(TestPaths.DesignDir);

    public static CardIndex Cards => Loaded.Cards;
    public static IReadOnlyList<string> StartingDeck => Loaded.StartingDeck;
    public static IReadOnlyDictionary<string, EnemyDef> Enemies => Loaded.Enemies;

    /// <summary>
    /// 적 최대 의지를 YAML 에서 읽는다 — <b>규칙 검증이 밸런스 수치에 묶이지 않게 하는 장치.</b>
    /// 규칙 테스트가 검증하는 것은 "판정 배율·가산 순서·효과 처리"이지 "고블린의 의지가 14 인가"가 아니다.
    /// </summary>
    public static int EWill(string id) => Enemies[id].Will;

    /// <summary>B01 「계약 갱신 베기」 피해도 같은 이유로 YAML 에서 읽는다</summary>
    public static readonly int SlashDamage =
        Enemies["B01"].Actions.First(a => a.Id == "contract_slash").Effects.First(e => e.Op == "damage").Value!.Value;

    // ── ADR-023 ① 검증용 합성 카드 ────────────────────────
    // cards-v2.0.yaml 재구성 전이라 실데이터에 defense_buff 가 없다. 카드 데이터가 들어오면 실카드로 교체할 것.
    // 시작 장비: [0] 롱소드 #마감 (무효 #연비) / [1] 가죽 갑옷 #내구도 #무게 / [2] 목걸이 #감성

    private static readonly ReviewCardDef[] TestCards =
    {
        new()
        {
            Id = "T_DEF6", Name = "테스트 찬양(방어 6)", Suit = Suit.품질, Tag = "마감", Cost = 1, Stars = 5,
            Rarity = Rarity.Rare, Target = TargetKind.MyEquipment,
            Effect = new EffectDef { Type = "defense_buff", Value = 6 }, Layer = 1,
        },
        new()
        {
            Id = "T_DEF2", Name = "테스트 찬양(방어 2)", Suit = Suit.품질, Tag = "마감", Cost = 1, Stars = 4,
            Rarity = Rarity.Common, Target = TargetKind.MyEquipment,
            Effect = new EffectDef { Type = "defense_buff", Value = 2 }, Layer = 1,
        },
        new()
        {
            Id = "T_DEFN", Name = "테스트 찬양(무효 태그)", Suit = Suit.성능, Tag = "연비", Cost = 1, Stars = 4,
            Rarity = Rarity.Common, Target = TargetKind.MyEquipment,
            Effect = new EffectDef { Type = "defense_buff", Value = 6 }, Layer = 1,
        },
    };

    /// <summary>실데이터 + 합성 카드 (TS 의 TEST_INDEX)</summary>
    public static readonly CardIndex TestIndex = BuildTestIndex();

    private static CardIndex BuildTestIndex()
    {
        var byId = new Dictionary<string, CardDef>(Cards.ById, StringComparer.Ordinal);
        var allIds = new List<string>(Cards.AllIds);
        foreach (var c in TestCards)
        {
            byId[c.Id] = c;
            allIds.Add(c.Id);
        }
        return new CardIndex { ById = byId, AllIds = allIds };
    }

    // ── 헬퍼 ──────────────────────────────────────────────

    /// <summary>NoShuffle 덱으로 시작 손패를 고정한다 (handIds 순서대로 드로우)</summary>
    public static Battle MakeBattle(
        string enemyId,
        IReadOnlyList<string> handIds,
        IReadOnlyList<string>? deck = null,
        EnemyDef? enemy = null,
        CardIndex? cards = null,
        int? startGauge = null,
        int? gold = null,
        int? layer = null,
        int? sigmaP = null,
        int? maxTurns = null,
        IReadOnlyDictionary<Suit, int>? initialSuitCounters = null,
        IReadOnlyList<PlayerEquipmentDef>? playerEquipment = null,
        OnboardingMods? onboarding = null)
    {
        var full = new List<string>(deck ?? Array.Empty<string>());
        full.AddRange(handIds.Reverse());
        return new Battle(new BattleConfig
        {
            Cards = cards ?? Cards,
            Enemy = enemy ?? Enemies[enemyId],
            Rng = RngFactory.Mulberry32(1),
            NoShuffle = true,
            Deck = full,
            StartGauge = startGauge,
            Gold = gold,
            Layer = layer,
            SigmaP = sigmaP,
            MaxTurns = maxTurns,
            InitialSuitCounters = initialSuitCounters,
            PlayerEquipment = playerEquipment,
            Onboarding = onboarding,
        });
    }

    public static int Uid(Battle b, string cardId, int nth = 0)
    {
        var found = b.State.Player.Hand.Where(c => c.CardId == cardId).ToList();
        Assert.True(found.Count > nth, $"손패에 {cardId} 없음");
        return found[nth].Uid;
    }

    public static int DefenseTotal(Battle b) => b.State.Player.Equipment.Sum(q => q.Defense);

    /// <summary>판정 4종 합계 — "무판정"(빗나감·특수 카드) 검증용</summary>
    public static int JudgementTotal(Battle b) => b.State.Stats.Judgements.Values.Sum();

    public static Dictionary<Suit, int> Counters(Suit suit, int n) => new() { [suit] = n };

    public static string DispositionLabel(Battle b) => Types.DispositionLabel[b.State.Player.Disposition];

    /// <summary>
    /// 미리보기 값과 실제 제출 결과가 어긋나면 실패한다 — 밸런스 변경 시 UI 드리프트 탐지용 (ADR-025).
    /// </summary>
    public static void AssertPreviewMatches(
        Battle b,
        int cardUid,
        int? enemyEquipmentIndex = null,
        int? myEquipmentIndex = null)
    {
        var pv = b.PreviewSubmit(cardUid, enemyEquipmentIndex, myEquipmentIndex);
        int willBefore = b.State.Enemy.Will;
        int gaugeBefore = b.State.Player.Gauge;
        var eqBefore = b.State.Enemy.Equipment.Select(q => q.Durability).ToArray();
        int defBefore = DefenseTotal(b);
        // 클램프 후 실제 회복량 (의지 증감으로 재면 반사 피격에 오염됨)
        int healedBefore = b.State.Stats.WillHealed;
        var r = b.SubmitReview(cardUid, enemyEquipmentIndex, myEquipmentIndex);

        Assert.Equal(pv.Judgement, r.Judgement);
        Assert.Equal(pv.Blocked is BlockedReason.Miss or BlockedReason.Void, r.Missed);
        if (pv.LikesKind == LikesKind.Will)
        {
            Assert.Equal(pv.Likes, willBefore - b.State.Enemy.Will);
        }
        else if (pv.LikesKind == LikesKind.Equipment)
        {
            int dealt = eqBefore.Select((d, i) => d - b.State.Enemy.Equipment[i].Durability).Sum();
            Assert.Equal(pv.Likes, dealt);
        }
        else if (pv.LikesKind == LikesKind.Defense)
        {
            Assert.Equal(pv.Likes, DefenseTotal(b) - defBefore); // ADR-023 ①
        }

        if (!r.Missed)
        {
            Assert.Equal(pv.Gauge, b.State.Player.Gauge - gaugeBefore);
            Assert.Equal(pv.Heal, b.State.Stats.WillHealed - healedBefore); // ADR-023 ②
        }
    }

    /// <summary>TS `assert.throws(fn, /메시지/)` — 예외 종류 + 한국어 메시지를 함께 못박는다</summary>
    public static void ThrowsWith<TException>(string expected, Action act) where TException : Exception
    {
        var ex = Assert.Throws<TException>(act);
        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
    }
}
