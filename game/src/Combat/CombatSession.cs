// 전투 세션 — 화면과 엔진 사이의 얇은 껍데기 (ADR-029 2차).
//
// **규칙을 여기서 재구현하지 않는다** (ADR-025). 판정·좋아요·게이지·회복은 전부
// Battle.PreviewSubmit / Battle.SubmitReview 가 준다. 이 파일이 하는 일은 셋뿐이다:
//   ① 대상 선택 상태(적 본체 / 적 구성품 i / 내 장비 i)를 들고 있다가 엔진 호출 인자로 넘긴다
//   ② 엔진이 던지는 예외를 화면이 보여줄 문자열로 바꾼다
//   ③ 제출 전후 상태를 스냅샷해 "무엇이 얼마나 변했는가"를 전투 로그에 적는다 (엔진 로그 보강)
//
// Godot 타입을 참조하지 않는다 — 자동 진행(AutoPlay)이 같은 경로로 전투를 완주시키기 위함이다.

using ReviewHero.Data;
using ReviewHero.Engine;

namespace ReviewHero.Game.Combat;

/// <summary>대상 선택 — 카드의 <see cref="CardDef.Target"/> 과 같은 3종</summary>
public enum TargetSlot
{
    Enemy,
    EnemyEquipment,
    MyEquipment,
}

/// <summary>전투 한 판을 열기 위해 런(또는 디버그 경로)이 넘겨주는 것 전부</summary>
public sealed record CombatContext
{
    public required EnemyDef Enemy { get; init; }
    public required IReadOnlyList<string> Deck { get; init; }
    public required uint Seed { get; init; }
    public int Gold { get; init; }

    /// <summary>런에서 이어받는 의지 (없으면 엔진 기본치)</summary>
    public int? Will { get; init; }

    public int? MaxWill { get; init; }

    /// <summary>논점 연속성 (GDD §3.5) — 런 누적 계열 카운터</summary>
    public IReadOnlyDictionary<Suit, int>? SuitCounters { get; init; }

    public Suit? LastSuit { get; init; }

    /// <summary>런에 붙어 있는가 (false = 디버그 단독 전투)</summary>
    public bool RunMode { get; init; }
}

/// <summary>손패 한 줄 — 카드 정의 + 지금 대상 기준 미리보기 (미리보기는 엔진이 소유한다)</summary>
public sealed record HandRow(int Uid, CardDef Def, SubmitPreview Preview);

/// <summary>대상 목록 한 줄</summary>
public sealed record TargetRow(TargetSlot Slot, int Index, string Label, bool Selected, bool Dead);

public sealed class CombatSession
{
    public LoadedData Data { get; }
    public Battle Battle { get; }
    public EnemyDef Enemy { get; }
    public CombatContext Context { get; }

    /// <summary>마지막으로 지목한 적 구성품 / 내 장비 인덱스 — 카드의 target 이 어느 쪽을 쓸지 정한다</summary>
    public int SelectedEnemyEq { get; private set; }

    public int SelectedMyEq { get; private set; }

    /// <summary>손패에서 고른 카드 (없으면 null)</summary>
    public int? SelectedCardUid { get; private set; }

    /// <summary>X04 증정 — 「증정할 카드를 고르는 중」 상태의 X04 uid</summary>
    public int? PendingGiftFor { get; private set; }

    /// <summary>마지막 안내 문구 (오류·성공)</summary>
    public string Status { get; private set; } = string.Empty;

    /// <summary>직전 제출의 엔진 결과 — 화면이 판정 도장을 찍는 근거다 (여기서 판정을 만들지 않는다)</summary>
    public SubmitResult? LastSubmit { get; private set; }

    /// <summary>직전 크리티컬 리뷰의 논점 (화면 연출용)</summary>
    public Disposition? LastCritical { get; private set; }

    public BattleState St => Battle.State;

    public CombatSession(LoadedData data, CombatContext ctx)
    {
        Data = data;
        Context = ctx;
        Enemy = ctx.Enemy;

        Battle = new Battle(new BattleConfig
        {
            Cards = data.Cards,
            Enemy = ctx.Enemy,
            Deck = ctx.Deck,
            Rng = RngFactory.Mulberry32(ctx.Seed),
            Gold = ctx.Gold,
            InitialSuitCounters = ctx.SuitCounters,
            InitialLastSuit = ctx.LastSuit,
            CollectLog = true,
        });

        // 의지는 런에서 잇는다 (ui/game/CONTRACT.md 전투 페이지 계약) — 전투 엔진은 매 판 30으로 시작한다
        if (ctx.Will is int w) St.Player.Will = w;
        if (ctx.MaxWill is int mw) St.Player.MaxWill = mw;

        Log($"── {Enemy.Name} ({TierLabel(Enemy.Tier)}) 전투 시작 · 의지 {St.Player.Will}/{St.Player.MaxWill} vs {St.Enemy.Will}");
    }

    // ── 표시 문자열 ────────────────────────────────────

    public static string TierLabel(EnemyTier t) => t switch
    {
        EnemyTier.Normal => "일반 셀러",
        EnemyTier.Elite => "파워 셀러",
        _ => "본사 직영",
    };

    /// <summary>데이터 문구의 「피해/데미지」를 게임 언어 「좋아요」로 (GDD §2 규칙 7)</summary>
    public static string Likeify(string? s) => (s ?? string.Empty)
        .Replace("의지 데미지", "좋아요")
        .Replace("의지 피해", "좋아요")
        .Replace("데미지", "좋아요")
        .Replace("피해", "좋아요");

    public static string JudgementBadge(SubmitPreview pv)
    {
        if (pv.Blocked is BlockedReason b)
        {
            return b switch
            {
                BlockedReason.Miss => "빗나감",
                BlockedReason.Void => "대상 없음",
                _ => "무판정",
            };
        }
        return pv.Judgement switch
        {
            Judgement.Origin => "★ 원산지",
            Judgement.Fact => "● 팩트",
            Judgement.Fumble => "⚠ 헛소리",
            _ => "일반",
        };
    }

    /// <summary>미리보기 수치 한 줄 — 좋아요 / 내구도 / 방어를 구분한다 (ADR-023 ①)</summary>
    public static string PreviewLine(SubmitPreview pv)
    {
        var parts = new List<string> { JudgementBadge(pv) };
        if (pv.Likes is int likes)
        {
            parts.Add(pv.LikesKind switch
            {
                LikesKind.Defense => $"방어 +{likes}",
                LikesKind.Equipment => $"내구도 −{likes} (좋아요)",
                _ => $"좋아요 {likes}",
            });
        }
        if (pv.Gauge != 0) parts.Add($"신뢰도 {(pv.Gauge > 0 ? "+" : string.Empty)}{pv.Gauge}");
        if (pv.Heal != 0) parts.Add($"의지 +{pv.Heal}");
        if (!pv.Affordable) parts.Add("필력 부족");
        return string.Join(" · ", parts);
    }

    // ── 손패·대상 목록 ─────────────────────────────────

    public CardDef DefOf(CardInstance c) => Data.Cards.ById[c.CardId];

    /// <summary>손패 — 카드마다 지금 지목한 대상 기준의 미리보기가 붙는다</summary>
    public List<HandRow> Hand()
    {
        var rows = new List<HandRow>();
        foreach (var c in St.Player.Hand)
        {
            rows.Add(new HandRow(c.Uid, DefOf(c), Battle.PreviewSubmit(c.Uid, SelectedEnemyEq, SelectedMyEq)));
        }
        return rows;
    }

    /// <summary>대상 목록 — 적 본체 / 적 구성품 N / 내 장비 N</summary>
    public List<TargetRow> Targets()
    {
        var rows = new List<TargetRow>
        {
            new(TargetSlot.Enemy, 0, $"「{Enemy.Name}」 의지 {Math.Max(0, St.Enemy.Will)}/{St.Enemy.MaxWill}", true, false),
        };
        for (int i = 0; i < St.Enemy.Equipment.Count; i++)
        {
            var q = St.Enemy.Equipment[i];
            string tags = q.Tags.Count > 0 ? " #" + string.Join(" #", q.Tags) : string.Empty;
            string dot = q.Dot is not null ? $" · 도트 {q.Dot.Value}×{q.Dot.Remaining}" : string.Empty;
            string dis = q.DisabledTurns > 0 ? $" · 비활성 {q.DisabledTurns}턴" : string.Empty;
            rows.Add(new TargetRow(
                TargetSlot.EnemyEquipment, i,
                q.Destroyed ? $"{q.Name} — 품절" : $"{q.Name} 내구도 {q.Durability}{tags}{dot}{dis}",
                i == SelectedEnemyEq, q.Destroyed));
        }
        for (int i = 0; i < St.Player.Equipment.Count; i++)
        {
            var eq = St.Player.Equipment[i];
            string tags = eq.Def.Tags.Count > 0 ? " #" + string.Join(" #", eq.Def.Tags) : string.Empty;
            string nulls = eq.Def.NullTags.Count > 0 ? " · 평가불가 #" + string.Join(" #", eq.Def.NullTags) : string.Empty;
            int buff = eq.Attachments.Where(a => a.Kind == AttachmentKind.DamageBuff).Sum(a => a.Value);
            string atk = buff > 0 ? $" · 가산 +{buff}" : string.Empty;
            rows.Add(new TargetRow(
                TargetSlot.MyEquipment, i,
                $"{eq.Def.Name} 방어 {eq.Defense}{tags}{nulls}{atk}",
                i == SelectedMyEq, false));
        }
        return rows;
    }

    /// <summary>적 인텐트 한 줄 (다음 행동)</summary>
    public string IntentLine()
    {
        var e = St.Enemy;
        var a = e.Def.Actions.FirstOrDefault(x => x.Id == e.IntentId);
        if (a is null) return "다음 행동: ?";
        var dmg = a.Effects.FirstOrDefault(f => f.Op == "damage")?.Value;
        var bits = new List<string> { a.Name };
        if (dmg is int d) bits.Add($"좋아요 {d}");
        if (a.ChargeTurns > 0) bits.Add($"준비 {a.ChargeTurns}턴");
        if (e.Charging is not null) bits.Add($"준비 중({e.Charging.Remaining})");
        if (e.StunTurns > 0) bits.Add($"기절 {e.StunTurns}턴");
        if (e.PendingDelay) bits.Add("지연됨");
        if (e.Stealth) bits.Add("은신");
        if (e.StaggerImmunityTurns > 0) bits.Add("경직 내성");
        return "다음 행동: " + string.Join(" · ", bits);
    }

    // ── 선택 ───────────────────────────────────────────

    public void SelectTarget(TargetSlot slot, int index)
    {
        if (slot == TargetSlot.EnemyEquipment) SelectedEnemyEq = index;
        else if (slot == TargetSlot.MyEquipment) SelectedMyEq = index;
        Status = string.Empty;
    }

    /// <summary>
    /// 「이 카드를 저 상품 위에 놓으면?」 — 지목 상태를 바꾸지 않고 그 대상 기준 미리보기만 얻는다.
    /// 드래그 중 말풍선이 쓴다. 계산은 전부 엔진이 한다 (ADR-025).
    /// </summary>
    public SubmitPreview PreviewOn(int uid, TargetSlot slot, int index) => slot switch
    {
        TargetSlot.EnemyEquipment => Battle.PreviewSubmit(uid, index, SelectedMyEq),
        TargetSlot.MyEquipment => Battle.PreviewSubmit(uid, SelectedEnemyEq, index),
        _ => Battle.PreviewSubmit(uid, SelectedEnemyEq, SelectedMyEq),
    };

    /// <summary>손패에서 카드를 내려놓는다 (선택 해제)</summary>
    public void Unselect()
    {
        SelectedCardUid = null;
        Status = string.Empty;
    }

    public void CancelGift()
    {
        PendingGiftFor = null;
        Status = string.Empty;
    }

    /// <summary>카드 선택. X04 증정 대기 중이면 이 카드가 증정 대상이 되고 즉시 사용한다</summary>
    public void SelectCard(int uid)
    {
        if (PendingGiftFor is int giftSrc && uid != giftSrc)
        {
            int src = giftSrc;
            PendingGiftFor = null;
            Special(src, uid);
            return;
        }
        SelectedCardUid = uid;
        Status = string.Empty;
    }

    public CardDef? SelectedDef()
    {
        if (SelectedCardUid is not int uid) return null;
        var c = St.Player.Hand.FirstOrDefault(x => x.Uid == uid);
        return c is null ? null : DefOf(c);
    }

    private void ClearSelectionIfGone()
    {
        if (SelectedCardUid is int uid && St.Player.Hand.All(c => c.Uid != uid)) SelectedCardUid = null;
    }

    // ── 액션 (엔진 호출 — 규칙은 전부 저쪽) ───────────────

    /// <summary>고른 카드를 제출한다. 리뷰면 SubmitReview, 진상 화법이면 PlaySpecial</summary>
    public bool Play()
    {
        if (SelectedCardUid is not int uid) { Status = "낼 카드를 고르시오"; return false; }
        var def = SelectedDef();
        if (def is null) { Status = "손패에 없는 카드"; return false; }
        if (def is SpecialDef spec)
        {
            if (spec.Effect.Type == "gift_card")
            {
                PendingGiftFor = uid;
                Status = "증정할 카드를 고르시오 (손패에서 한 장)";
                return false;
            }
            return Special(uid, null);
        }
        return Submit(uid);
    }

    private bool Submit(int uid)
    {
        var def = Data.Cards.ById[St.Player.Hand.First(c => c.Uid == uid).CardId];
        var pre = Snapshot();
        SubmitResult res;
        try
        {
            res = Battle.SubmitReview(uid, SelectedEnemyEq, SelectedMyEq);
        }
        catch (InvalidOperationException ex)
        {
            Status = ex.Message;
            return false;
        }
        LastSubmit = res;
        string verdict = res.Missed ? "빗나감" : JudgementLabel(res.Judgement);
        Log($"T{St.Turn} 제출 「{def.Name}」→ {TargetLabelFor(def)} · {verdict}{Diff(pre)}");
        Status = $"「{def.Name}」 {verdict}";
        ClearSelectionIfGone();
        return true;
    }

    private bool Special(int uid, int? giftUid)
    {
        var def = Data.Cards.ById[St.Player.Hand.First(c => c.Uid == uid).CardId];
        var pre = Snapshot();
        try
        {
            Battle.PlaySpecial(uid, giftUid);
        }
        catch (InvalidOperationException ex)
        {
            Status = ex.Message;
            return false;
        }
        Log($"T{St.Turn} 진상 화법 「{def.Name}」{Diff(pre)}");
        Status = $"「{def.Name}」 사용";
        ClearSelectionIfGone();
        return true;
    }

    /// <summary>퇴고 — 고른 카드를 버리고 1장 드로우 (card-system-v2 §7 태그 사냥)</summary>
    public bool Revise()
    {
        if (SelectedCardUid is not int uid) { Status = "퇴고할 카드를 고르시오"; return false; }
        return Revise(uid);
    }

    /// <summary>초고 폐기함에 끌어다 놓은 카드를 퇴고한다 (지목 상태와 무관하게 그 장을 버린다)</summary>
    public bool Revise(int uid)
    {
        var c = St.Player.Hand.FirstOrDefault(x => x.Uid == uid);
        var def = c is null ? null : DefOf(c);
        if (def is null) { Status = "손패에 없는 카드"; return false; }
        try
        {
            Battle.Revise(uid);
        }
        catch (InvalidOperationException ex)
        {
            Status = ex.Message;
            return false;
        }
        Log($"T{St.Turn} 퇴고 — 「{def?.Name}」 버리고 1장");
        Status = "퇴고했다";
        ClearSelectionIfGone();
        return true;
    }

    public bool Critical()
    {
        var pre = Snapshot();
        Disposition d;
        try
        {
            d = Battle.UseCritical();
        }
        catch (InvalidOperationException ex)
        {
            Status = ex.Message;
            return false;
        }
        LastCritical = d;
        Log($"T{St.Turn} 크리티컬 리뷰 「{Types.CriticalName[d]}」({Types.DispositionLabel[d]}){Diff(pre)}");
        Status = $"크리티컬 — {Types.CriticalName[d]}";
        return true;
    }

    /// <summary>
    /// 항복 — 위로금 6골드를 뜯어내고 물러난다 (ui/game/CONTRACT.md 전투 페이지 계약 승계).
    /// <b>엔진에 항복 API 는 없다.</b> X07 「전투 이탈」과 달리 이건 카드가 아니라 화면 층의 약속이고,
    /// 웹판(combat.html doRetreat)도 화면이 처리했다. 여기서 만드는 유일한 수치가 위로금 6이며
    /// 승리 골드(<see cref="CombatEnd.WinGold"/>)와 같은 층에 산다 — 규칙 엔진을 침범하지 않는다.
    /// </summary>
    public const int SurrenderConsolation = 6;

    public bool Surrender()
    {
        if (St.Result is not null) return false;
        if (Enemy.Tier == EnemyTier.Boss) { Status = "보스전은 도망칠 수 없다"; return false; }
        St.Player.Gold += SurrenderConsolation;
        St.Result = BattleResult.Retreat;
        Log($"T{St.Turn} 항복 — 위로금 {SurrenderConsolation}골드를 뜯어내고 물러났다");
        Status = "항복 — 주문 취소";
        return true;
    }

    /// <summary>택배 개봉 (ADR-024 ③) — 보스전에서만 열린다</summary>
    public bool Parcel()
    {
        PlayerEquipmentDef got;
        try
        {
            got = Battle.OpenParcel();
        }
        catch (InvalidOperationException ex)
        {
            Status = ex.Message;
            return false;
        }
        SelectedMyEq = St.Player.Equipment.Count - 1; // 방금 얻은 장비를 바로 지목
        Log($"T{St.Turn} 택배 개봉 — {got.Name} (#{string.Join(" #", got.Tags)})");
        Status = $"📦 {got.Name} — 이제 내 장비다";
        return true;
    }

    public bool EndTurn()
    {
        if (St.Result is not null) return false;
        var pre = Snapshot();
        int turn = St.Turn;
        Battle.EndTurn();
        Log($"T{turn} 턴 종료 → 적 턴{Diff(pre)}");
        SelectedCardUid = null;
        PendingGiftFor = null;
        return true;
    }

    // ── 로그 보강 ──────────────────────────────────────

    private sealed record Snap(int EnemyWill, int PlayerWill, int Gold, int Gauge, int[] Durability, int Defense);

    private Snap Snapshot() => new(
        Math.Max(0, St.Enemy.Will),
        Math.Max(0, St.Player.Will),
        St.Player.Gold,
        St.Player.Gauge,
        St.Enemy.Equipment.Select(q => q.Durability).ToArray(),
        St.Player.Equipment.Sum(q => q.Defense));

    /// <summary>제출 전후 차이 — 엔진이 낸 결과를 읽어 적을 뿐, 여기서 수치를 만들지 않는다</summary>
    private string Diff(Snap pre)
    {
        var now = Snapshot();
        var bits = new List<string>();
        if (now.EnemyWill != pre.EnemyWill) bits.Add($"적 의지 {pre.EnemyWill}→{now.EnemyWill}");
        if (now.PlayerWill != pre.PlayerWill) bits.Add($"내 의지 {pre.PlayerWill}→{now.PlayerWill}");
        if (now.Defense != pre.Defense) bits.Add($"방어 {pre.Defense}→{now.Defense}");
        if (now.Gauge != pre.Gauge) bits.Add($"신뢰도 {pre.Gauge}→{now.Gauge}");
        if (now.Gold != pre.Gold) bits.Add($"골드 {pre.Gold}→{now.Gold}");
        for (int i = 0; i < now.Durability.Length && i < pre.Durability.Length; i++)
        {
            if (now.Durability[i] != pre.Durability[i])
            {
                bits.Add($"{St.Enemy.Equipment[i].Name} 내구도 {pre.Durability[i]}→{now.Durability[i]}");
            }
        }
        return bits.Count == 0 ? string.Empty : " · " + string.Join(", ", bits);
    }

    private string TargetLabelFor(CardDef def) => def.Target switch
    {
        TargetKind.Enemy => $"「{Enemy.Name}」",
        TargetKind.EnemyEquipment => St.Enemy.Equipment.Count > SelectedEnemyEq
            ? St.Enemy.Equipment[SelectedEnemyEq].Name
            : "구성품",
        _ => St.Player.Equipment.Count > SelectedMyEq ? St.Player.Equipment[SelectedMyEq].Def.Name : "내 장비",
    };

    private static string JudgementLabel(Judgement? j) => j switch
    {
        Judgement.Origin => "원산지!",
        Judgement.Fact => "팩트!",
        Judgement.Fumble => "헛소리…",
        Judgement.Normal => "일반",
        _ => "무판정",
    };

    public void Log(string line) => St.Log.Add(line);
}
