// 이세계 리뷰용사 — 전투 상태 타입 (packages/core/src/battle.ts 이관)
//
// battle.ts 는 상태 타입과 상태머신을 한 파일에 담았으나, C# 에서는 타입 선언이 길어져
// 상태 타입만 이 파일로 분리한다. **정의·주석은 원본 그대로다** — 분할은 편집상의 편의이지
// 규칙 변경이 아니다. 상태머신 본체는 Battle.cs.
//
// UI 무의존 순수 상태머신. fs/network 접근 금지, DateTime.Now/시드 없는 Random 금지 — rng 주입.

namespace ReviewHero.Engine;

// ── 상태 타입 ─────────────────────────────────────────

public sealed class CardInstance
{
    public int Uid { get; init; }
    public string CardId { get; init; } = string.Empty;
}

/// <summary>부착물 종류 — v2 데이터에는 damage_buff 하나뿐 (TS 리터럴 유니온 'damage_buff' 대응)</summary>
public enum AttachmentKind
{
    DamageBuff,
}

public sealed class Attachment
{
    public AttachmentKind Kind { get; init; } = AttachmentKind.DamageBuff;
    public int Value { get; set; }
    public bool UsesSlot { get; init; }
}

public sealed class PlayerEquipmentState
{
    public PlayerEquipmentDef Def { get; init; } = null!;

    /// <summary>부착 슬롯 2칸 (GDD §3.9)</summary>
    public List<Attachment> Attachments { get; } = new();

    /// <summary>
    /// 방어 (ADR-023 ①) — 찬양 리뷰(defense_buff)가 이 장비에 쌓는 흡수량.
    /// 피해를 흡수하며 소모되고, <b>남은 방어는 전투 내내 유지</b>된다(턴 리셋 없음).
    /// 결정(ADR-023 근거): 방어는 부착 슬롯(GDD §3.9, 2칸)을 <b>쓰지 않는다</b> — 슬롯은
    /// "부착물 개수"를 제한하는 자리인데 방어는 개별 부착물이 아니라 장비의 수치 누적이고,
    /// 소모되면 사라져 슬롯을 점유·해제하는 수명 개념이 성립하지 않는다.
    /// (Attachment 로 만들면 흡수로 0이 된 부착물의 슬롯 회수 시점을 따로 정의해야 한다.)
    /// </summary>
    public int Defense { get; set; }
}

/// <summary>S06 장비 도트 (틱 값 + 남은 턴)</summary>
public sealed class EquipmentDot
{
    public int Value { get; set; }
    public int Remaining { get; set; }
}

public sealed class EnemyEquipmentState
{
    public string Name { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();
    public int Durability { get; set; }

    /// <summary>S07</summary>
    public int DisabledTurns { get; set; }

    /// <summary>S06</summary>
    public EquipmentDot? Dot { get; set; }

    public bool Destroyed { get; set; }
}

/// <summary>attack_halve = 「힙스터 인증」 크리 (공격력 −50%)</summary>
public enum EnemyDebuffKind
{
    AttackDown,
    AttackHalve,
}

/// <summary>플레이어가 적에게 부착한 디버프 (B01 사장님 답글의 반박 풀)</summary>
public sealed class EnemyDebuff
{
    public int Uid { get; init; }
    public EnemyDebuffKind Kind { get; init; }

    /// <summary>attack_down의 감소량 / attack_halve는 50(위력 표기)</summary>
    public int Value { get; init; }

    /// <summary>재반박 계열 매칭용</summary>
    public Suit Suit { get; init; }

    /// <summary>「힙스터 인증」 크리 = 3 (R22). 전투 부착 일반 디버프 = 1 (가정)</summary>
    public int Tier { get; init; }

    /// <summary>사장님 답글로 "이번 전투 한정 정지"</summary>
    public bool Suspended { get; set; }

    /// <summary>디버프당 반박 1회 (once_per_debuff)</summary>
    public bool BeenRebutted { get; set; }

    /// <summary>최근성 tiebreak</summary>
    public int CreatedAt { get; init; }
}

public enum EnemyBuffKind
{
    AttackUp,
}

public sealed class EnemyBuff
{
    public int Uid { get; init; }
    public EnemyBuffKind Kind { get; init; } = EnemyBuffKind.AttackUp;
    public int Value { get; init; }

    /// <summary>phase2 알바_리뷰: counter_card로만 제거 가능</summary>
    public string? ProtectedBy { get; init; }

    /// <summary>저격 카드 id (B02c) — remove_enemy_buff가 대조</summary>
    public string? CounterCard { get; init; }
}

public sealed class BattleStats
{
    public int Submissions { get; set; }

    public Dictionary<Judgement, int> Judgements { get; } = new()
    {
        [Judgement.Origin] = 0,
        [Judgement.Fact] = 0,
        [Judgement.Normal] = 0,
        [Judgement.Fumble] = 0,
    };

    /// <summary>0~10 클램프 후 실제 반영된 획득분 (§3.4 판정 순증과는 정의가 다름 — 시뮬 해석 주의)</summary>
    public int GaugeGained { get; set; }

    public int GaugeLost { get; set; }

    /// <summary>상한 10 초과로 소실된 획득분 (GDD §2-2 "초과 소실" 계측)</summary>
    public int GaugeOverflowLost { get; set; }

    /// <summary>게이지 10 도달 이벤트 수 = 크리 "발동 가능" 횟수 (발동과 구분 — §3.4 검증용)</summary>
    public int GaugeReached10 { get; set; }

    public List<Disposition> Crits { get; } = new();

    /// <summary>은신 게이트에 빗나간 크리 (E04)</summary>
    public int CritMisses { get; set; }

    /// <summary>전 장비 파괴 항복 승리</summary>
    public bool Surrender { get; set; }

    /// <summary>실제 반영된 의지 회복 총량 (maxWill 클램프 후 — 판정 회복 + 카드 heal 동반)</summary>
    public int WillHealed { get; set; }

    /// <summary>defense_buff 로 부여한 방어 총량 (판정 배율 적용 후)</summary>
    public int DefenseGained { get; set; }

    /// <summary>방어가 흡수해 의지에 닿지 않은 피해 총량 (ADR-023 밸런스 라운드 1 계측)</summary>
    public int DefenseAbsorbed { get; set; }
}

/// <summary>온보딩 전투 보정 (GDD §3.3 버프 무판정, §4.4 1~2판 보정) — 런 레벨이 주입</summary>
public sealed class OnboardingMods
{
    /// <summary>적 공격 배율 (1판 0.75 / 2판 0.9 — §4.4). 배율이므로 §2-1 내림·최소 1 적용</summary>
    public double? EnemyDamageMult { get; init; }

    /// <summary>헛소리 판정 게이지 증감 (1판 −1, 기본 −2 — §4.4)</summary>
    public int? FumbleGaugeDelta { get; init; }

    /// <summary>true면 버프 카드(내 장비 대상)는 무판정 = 항상 일반 (1판 한정 — §3.3)</summary>
    public bool? BuffNoJudgement { get; init; }
}

public sealed class BattleConfig
{
    public CardIndex Cards { get; init; } = null!;
    public EnemyDef Enemy { get; init; } = null!;

    /// <summary>카드 id 목록 (시작 덱 12장 등)</summary>
    public IReadOnlyList<string> Deck { get; init; } = Array.Empty<string>();

    public Rng Rng { get; init; } = null!;
    public IReadOnlyList<PlayerEquipmentDef>? PlayerEquipment { get; init; }
    public int? Gold { get; init; }

    /// <summary>
    /// 밸런스 수치 부분 오버라이드 (ADR-025). 미지정 필드는 DEFAULT_RULES 를 따른다.
    /// </summary>
    public RulesOverride? Rules { get; init; }

    /// <summary>
    /// 보스에게 가던 보급품 (ADR-024 ③). 주면 전투 중 OpenParcel() 로 개봉할 수 있다.
    /// TS 의 `parcel?: X | null` 3상태(미지정 / null / 값)를 C# 에서 살리기 위해
    /// <see cref="ParcelSpecified"/> 로 "미지정"과 "명시적 null"을 구분한다 —
    /// 미지정이면 보스일 때 기본 보급품, 명시적 null 이면 택배 없음.
    /// </summary>
    public PlayerEquipmentDef? Parcel { get; init; }

    /// <summary>true 면 <see cref="Parcel"/> 값을(null 이라도) 그대로 쓴다. false = TS 의 `undefined`.</summary>
    public bool ParcelSpecified { get; init; }

    /// <summary>미지정 시 rules.battle.maxTurns — 초과 시 패배(timeout) 처리</summary>
    public int? MaxTurns { get; init; }

    /// <summary>기본 1 (MVP). X09는 layer 2</summary>
    public int? Layer { get; init; }

    /// <summary>논점 스냅샷용 런 누적 제출 카드 계열 카운터 (GDD §3.5 — 전투 시작 시 스냅샷 고정)</summary>
    public IReadOnlyDictionary<Suit, int>? InitialSuitCounters { get; init; }

    public Suit? InitialLastSuit { get; init; }

    /// <summary>외부 보정 (캡 ±는 런 레벨 규칙 — 시뮬은 값 그대로 클램프만)</summary>
    public int? StartGauge { get; init; }

    /// <summary>X09용 악평 페널티 합 (Layer 2)</summary>
    public int? SigmaP { get; init; }

    /// <summary>온보딩 1~2판 보정 (§3.3/§4.4) — 미지정 시 정상 난이도</summary>
    public OnboardingMods? Onboarding { get; init; }

    /// <summary>테스트 전용: 덱 순서 고정</summary>
    public bool? NoShuffle { get; init; }

    public bool? CollectLog { get; init; }
}

/// <summary>TS 의 `'win' | 'lose' | 'retreat' | 'timeout' | null` — C# 에서는 <c>BattleResult?</c></summary>
public enum BattleResult
{
    Win,
    Lose,
    Retreat,
    Timeout,
}

/// <summary>제출이 막힌 사유 — 은신 빗나감 / 남은 구성품 없음 / 무판정 카드</summary>
public enum BlockedReason
{
    Miss,
    Void,
    NotReview,
}

/// <summary>미리보기 수치가 무엇에 쓰이는가</summary>
public enum LikesKind
{
    Will,
    Equipment,
    Defense,
}

/// <summary>PreviewSubmit 결과 — 화면이 제출 전에 보여줄 값. 규칙 계산은 전부 엔진이 소유한다.</summary>
public sealed class SubmitPreview
{
    /// <summary>Blocked 가 있으면 null</summary>
    public Judgement? Judgement { get; init; }

    /// <summary>은신 빗나감 / 남은 구성품 없음 / 무판정 카드</summary>
    public BlockedReason? Blocked { get; init; }

    /// <summary>최종 좋아요 (피해가 없는 카드는 null)</summary>
    public int? Likes { get; init; }

    /// <summary>
    /// 그 수치가 무엇에 쓰이는가. Defense 는 피해가 아니라 내 장비에 붙는 방어량이다
    /// (UI 표기 "방어 +6" — 좋아요 아이콘을 붙이지 말 것). ADR-023 ①
    /// </summary>
    public LikesKind? LikesKind { get; init; }

    /// <summary>신뢰도 게이지 증감 (카드 인라인 gauge 포함)</summary>
    public int Gauge { get; init; }

    /// <summary>의지 회복 예상치 — maxWill 클램프를 <b>반영한 실제 증가분</b> (판정 회복 + 카드 heal 동반). ADR-023 ②</summary>
    public int Heal { get; init; }

    /// <summary>지금 필력으로 낼 수 있는가</summary>
    public bool Affordable { get; init; }

    /// <summary>절대 수치에 걸리는 배율 = 판정 × 조건부(E03 영창 약점). 지속 턴·%·개수는 비대상</summary>
    public double Mult { get; init; }

    /// <summary>의지 피해 전용 추가 배율 (E05 vanity)</summary>
    public double VanityMult { get; init; }

    /// <summary>내림 뒤 더해지는 고정 가산 (원산지 +1)</summary>
    public int FixedAdd { get; init; }
}

/// <summary>SubmitReview 반환 — TS `{ missed, judgement }`</summary>
public readonly record struct SubmitResult(bool Missed, Judgement? Judgement);

/// <summary>X06 대기 슬롯 (설치형 리액션)</summary>
public sealed class ReactionState
{
    public int WeakenPct { get; init; }
    public int ReflectPct { get; init; }
}

/// <summary>준비(charge) 진행 상태</summary>
public sealed class ChargingState
{
    public string ActionId { get; init; } = string.Empty;
    public int Remaining { get; set; }
}

public sealed class PlayerState
{
    public int Will { get; set; }
    public int MaxWill { get; set; }
    public int Energy { get; set; }

    /// <summary>S15</summary>
    public int EnergyNextTurnBonus { get; set; }

    /// <summary>B01 야근 강요</summary>
    public int EnergyNextTurnPenalty { get; set; }

    public int Gold { get; set; }

    /// <summary>0~10 (GDD §2-2)</summary>
    public int Gauge { get; set; }

    public List<PlayerEquipmentState> Equipment { get; } = new();
    public List<CardInstance> Hand { get; } = new();
    public List<CardInstance> Deck { get; set; } = new();
    public List<CardInstance> Discard { get; set; } = new();

    /// <summary>X04 증정 (GDD §3.6)</summary>
    public List<CardInstance> RemovedFromRun { get; } = new();

    /// <summary>택배 개봉 여부 — 전투당 1회 (ADR-024 ③)</summary>
    public bool ParcelOpened { get; set; }

    public bool CritUsedThisTurn { get; set; }

    /// <summary>「진상 접수」 골드 갈취 전투당 1회</summary>
    public bool InconvenienceGoldUsed { get; set; }

    /// <summary>「바이럴 확산」 가산 누적 (상한 12, 크리 간 공유 — GDD §3.5)</summary>
    public int ViralBonusGranted { get; set; }

    public bool X05Armed { get; set; }

    /// <summary>X05 예약 확정분 — 다음 리뷰 1회에 가산</summary>
    public int StoredDamageBonus { get; set; }

    public int DamageTakenThisTurn { get; set; }

    /// <summary>X06 대기 슬롯 1</summary>
    public ReactionState? Reaction { get; set; }

    public Dictionary<Suit, int> SuitCounters { get; init; } = new();
    public Suit? LastSuit { get; set; }

    /// <summary>전투 시작 스냅샷 (GDD §3.5)</summary>
    public Disposition Disposition { get; set; }

    /// <summary>X09 등</summary>
    public HashSet<string> OncePerCombatUsed { get; } = new();
}

public sealed class EnemyState
{
    public EnemyDef Def { get; init; } = null!;
    public int Will { get; set; }
    public int MaxWill { get; set; }
    public List<EnemyEquipmentState> Equipment { get; init; } = new();
    public List<EnemyBuff> Buffs { get; } = new();
    public List<EnemyDebuff> Debuffs { get; } = new();
    public int StunTurns { get; set; }

    /// <summary>기절 해제 후 1턴 경직 내성 (GDD §3.2)</summary>
    public int StaggerImmunityTurns { get; set; }

    /// <summary>X01</summary>
    public bool PendingDelay { get; set; }

    public bool Stealth { get; set; }

    /// <summary>E04 ambush if_stealth_broken 판정용 (이번 사이클)</summary>
    public bool StealthEverBroken { get; set; }

    public ChargingState? Charging { get; set; }

    /// <summary>S08 (음수 %)</summary>
    public int WeakenNextActionPct { get; set; }

    public int DamageReductionNextHit { get; set; }
    public int ReflectNextHit { get; set; }
    public int PatternIndex { get; set; }
    public string IntentId { get; set; } = string.Empty;
    public bool Phase2Done { get; set; }

    /// <summary>cooldown 있는 행동의 마지막 발동 턴 (B01 사장님 답글 "3턴마다" 하한 강제용)</summary>
    public Dictionary<string, int> CooldownLastFired { get; } = new();
}

public sealed class BattleState
{
    /// <summary>플레이어 턴 번호 (1부터)</summary>
    public int Turn { get; set; }

    public BattleResult? Result { get; set; }
    public PlayerState Player { get; init; } = null!;
    public EnemyState Enemy { get; init; } = null!;
    public BattleStats Stats { get; init; } = null!;
    public List<string> Log { get; } = new();
}
