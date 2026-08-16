// 전투 상태머신 규칙 검증 v2 (GDD §2·§3 + card-system-v2 판정 4단계 + 악용 검증 엣지)
// packages/sim/test/battle.test.ts 이관 (ADR-029). 실데이터(cards-v2.0.yaml / enemies-v1.0.yaml)를
// 로드해 engine 을 구동한다. 기대 수치는 TS 판과 동일하다 — 이관이지 재밸런싱이 아니다.

using System.Text.Json;
using ReviewHero.Data;
using ReviewHero.Engine;
using static ReviewHero.Engine.Tests.TestData;

namespace ReviewHero.Engine.Tests;

public class BattleTests
{
    // ── §2 공통 계산 + v2 판정 4단계 ──────────────────────

    [Fact(DisplayName = "v2 판정: 팩트 ×1.5 내림, 게이지 +3")]
    public void FactJudgement()
    {
        var b = MakeBattle("E01", new[] { "Z06" }); // Z06 #마감 👍4 — E01 약점 [마감]
        var r = b.SubmitReview(Uid(b, "Z06"));
        Assert.Equal(Judgement.Fact, r.Judgement);
        Assert.Equal(EWill("E01") - 6, b.State.Enemy.Will); // floor(4×1.5)=6
        Assert.Equal(3, b.State.Player.Gauge);
        Assert.Equal(1, b.State.Stats.Judgements[Judgement.Fact]);
    }

    [Fact(DisplayName = "v2 판정: 원산지 ×1.5 + 고정 +1(내림 후·배율 비대상), 게이지 +4")]
    public void OriginJudgement()
    {
        var b = MakeBattle("E01", new[] { "Q01" }); // Q01 origin {E01, 짝퉁 단검} 👍7 — 적 본체 대상
        var r = b.SubmitReview(Uid(b, "Q01"));
        Assert.Equal(Judgement.Origin, r.Judgement);
        // floor(7×1.5)+1 = 11. 고정 가산이 배율을 받으면 floor((7+1)×1.5)=12가 되므로 구분됨 (GDD §2)
        Assert.Equal(EWill("E01") - 11, b.State.Enemy.Will);
        Assert.Equal(4, b.State.Player.Gauge);
        Assert.Equal(1, b.State.Stats.Judgements[Judgement.Origin]);
    }

    [Fact(DisplayName = "v2 판정: 헛소리 ×0.5(최소 1), 게이지 −2")]
    public void FumbleJudgement()
    {
        var b = MakeBattle("E01", new[] { "Z01" }, startGauge: 3); // Z01 #연비 👍5 — E01 무효 [연비,이펙트]
        var r = b.SubmitReview(Uid(b, "Z01"));
        Assert.Equal(Judgement.Fumble, r.Judgement);
        Assert.Equal(EWill("E01") - 2, b.State.Enemy.Will); // floor(5×0.5)=2
        Assert.Equal(1, b.State.Player.Gauge);
    }

    [Fact(DisplayName = "v2 원산지: 무효 태그 무시 (같은 태그 비원산지 카드는 헛소리)")]
    public void OriginIgnoresNullTags()
    {
        // 실데이터엔 "origin 태그 = 그 적 무효 태그" 조합이 없어 E02 무효 태그에 무게를 얹어 검증
        var enemyMod = Enemies["E02"] with { NullTags = new[] { "무게", "이펙트", "개연성" } };
        var b = MakeBattle("E02", new[] { "W01", "A03" }, enemy: enemyMod);
        var r1 = b.SubmitReview(Uid(b, "W01")); // W01 origin E02 #무게 👍7 — 무효 태그여도 원산지
        Assert.Equal(Judgement.Origin, r1.Judgement);
        Assert.Equal(EWill("E02") - 11, b.State.Enemy.Will); // floor(7×1.5)+1
        var r2 = b.SubmitReview(Uid(b, "A03")); // A03 origin E05 #무게 👍6 — 원산지 아님 → 헛소리
        Assert.Equal(Judgement.Fumble, r2.Judgement);
        Assert.Equal(EWill("E02") - 11 - 3, b.State.Enemy.Will); // floor(6×0.5)=3
    }

    [Fact(DisplayName = "§2-2: 게이지 상한 10 초과 소실 / 하한 0")]
    public void GaugeClamp()
    {
        var hi = MakeBattle("E01", new[] { "Z06" }, startGauge: 9);
        hi.SubmitReview(Uid(hi, "Z06")); // 팩트 +3
        Assert.Equal(10, hi.State.Player.Gauge); // 9+3=12 → 10 (초과 소실)

        var lo = MakeBattle("E01", new[] { "Z01" }, startGauge: 1);
        lo.SubmitReview(Uid(lo, "Z01")); // 헛소리 −2
        Assert.Equal(0, lo.State.Player.Gauge); // 1−2 → 0 (하한)
    }

    // ── v2 원산지 판정 범위 ───────────────────────────────

    [Fact(DisplayName = "v2 원산지 범위: 적 본체 대상 → origin.enemy 일치 (다른 적에겐 일반)")]
    public void OriginScopeEnemy()
    {
        var vs02 = MakeBattle("E02", new[] { "W01" });
        Assert.Equal(Judgement.Origin, vs02.SubmitReview(Uid(vs02, "W01")).Judgement);
        var vs01 = MakeBattle("E01", new[] { "W01" }); // #무게 — E01 약점/무효 아님
        Assert.Equal(Judgement.Normal, vs01.SubmitReview(Uid(vs01, "W01")).Judgement);
        Assert.Equal(EWill("E01") - 6, vs01.State.Enemy.Will); // 일반 ×0.9 내림 → floor(7×0.9)=6
    }

    [Fact(DisplayName = "v2 원산지 범위: 구성품 대상 → origin.equipment 이름 완전 일치")]
    public void OriginScopeEquipment()
    {
        // E04 구성품: [삐걱거리는 쌍단검, 삐걱거리는 쌍단검 (한 짝)] — C03c origin은 전자만
        var b = MakeBattle("E04", new[] { "C03c" });
        var r = b.SubmitReview(Uid(b, "C03c"), enemyEquipmentIndex: 0);
        Assert.Equal(Judgement.Origin, r.Judgement);
        Assert.True(b.State.Enemy.Equipment[0].Destroyed); // floor(7×1.5)+1=11 ≥ 내구도 5
        Assert.Equal(4, b.State.Player.Gauge);

        var b2 = MakeBattle("E04", new[] { "C03c" });
        var r2 = b2.SubmitReview(Uid(b2, "C03c"), enemyEquipmentIndex: 1); // "(한 짝)" — 이름 불일치
        Assert.Equal(Judgement.Normal, r2.Judgement); // #내구도 ∉ [속도,마감], 무효 [감성,디자인] 아님
        Assert.True(b2.State.Enemy.Equipment[1].Destroyed); // 7 ≥ 5
        Assert.Equal(0, b2.State.Player.Gauge);
    }

    [Fact(DisplayName = "v2: 전생 카드(Z01~Z12)는 origin 없음 — 원산지 영구 미발동")]
    public void PastLifeCardsHaveNoOrigin()
    {
        for (int i = 1; i <= 12; i++)
        {
            string id = $"Z{i:D2}";
            var d = Cards.ById[id];
            var review = Assert.IsType<ReviewCardDef>(d);
            Assert.True(review.Origin is null, $"{id}에 origin이 있으면 안 됨");
        }
        var b = MakeBattle("E04", new[] { "Z07" }); // Z07 #속도 = E04 약점 — 팩트까지만 (원산지 불가)
        Assert.Equal(Judgement.Fact, b.SubmitReview(Uid(b, "Z07")).Judgement);
        Assert.Equal(EWill("E04") - 7, b.State.Enemy.Will); // floor(5×1.5)
    }

    [Fact(DisplayName = "v2: 단일 카드 제출 — 특수 카드는 submitReview 불가, 리뷰 카드는 playSpecial 불가")]
    public void CardKindGuards()
    {
        var b = MakeBattle("E01", new[] { "X01", "Z01" });
        ThrowsWith<InvalidOperationException>("리뷰 카드가 아님", () => b.SubmitReview(Uid(b, "X01")));
        ThrowsWith<InvalidOperationException>("특수 카드가 아님", () => b.PlaySpecial(Uid(b, "Z01")));
    }

    [Fact(DisplayName = "v2 로드 검증: tag 배열이면 로드 에러 (단일 초점 원칙)")]
    public void LoadRejectsTagArray()
    {
        // TS 는 `assert.throws(fn, /배열 금지/)` 로 메시지만 못박았다 — 여기서도 같다.
        // (현재 로더는 InvalidDataException 을 던진다. 엔진의 InvalidOperationException 규약과
        //  다른 계층이라 종류를 고정하지 않고 원본과 동일하게 메시지를 잠근다.)
        string path = Path.Combine(TestPaths.FixturesDir, "bad-tag.yaml");
        var ex = Record.Exception(() => Loader.LoadCards(path));
        Assert.NotNull(ex);
        Assert.Contains("배열 금지", ex!.Message, StringComparison.Ordinal);
    }

    // ── §3.2 턴 시퀀스·기절·리액션·지연 ──────────────────

    [Fact(DisplayName = "§3.2: 기절 시 attack 불발, gimmick은 기절 무시 발동")]
    public void StunBlocksAttackNotGimmick()
    {
        var b = MakeBattle("B01", new[] { "B01c", "W03" });
        b.SubmitReview(Uid(b, "B01c")); // 원산지: floor(9×1.5)+1=14
        Assert.Equal(EWill("B01") - 14, b.State.Enemy.Will);
        b.EndTurn(); // contract_slash
        Assert.Equal(30 - SlashDamage, b.State.Player.Will);
        b.SubmitReview(Uid(b, "W03")); // 기절 1턴 (판정 일반 — #무게)
        Assert.Equal(1, b.State.Enemy.StunTurns);
        b.State.Enemy.PatternIndex = 2;
        b.State.Enemy.IntentId = "owner_reply"; // 기믹 강제
        b.EndTurn();
        Assert.Equal(EWill("B01") - 14 + 5, b.State.Enemy.Will); // 기절 무시 발동: 반박 대상 없음 → 의지 +5
        Assert.Equal(0, b.State.Enemy.StunTurns);
        Assert.Equal(1, b.State.Enemy.StaggerImmunityTurns); // 기상 → 경직 내성 1턴
    }

    [Fact(DisplayName = "§3.2: 경직 내성 중 지연(X01) 면역 — 연쇄 락 불가")]
    public void StaggerImmunityBlocksDelay()
    {
        var b = MakeBattle("E01", new[] { "W03", "X01" });
        b.SubmitReview(Uid(b, "W03")); // 기절
        b.EndTurn(); // stab 불발, 기상 + 경직 내성
        Assert.Equal(30, b.State.Player.Will);
        Assert.Equal(1, b.State.Enemy.StaggerImmunityTurns);
        b.PlaySpecial(Uid(b, "X01")); // 지연 시도 → 면역
        Assert.False(b.State.Enemy.PendingDelay);
        b.EndTurn(); // 행동 정상 실행 (stab 5)
        Assert.Equal(25, b.State.Player.Will);
    }

    [Fact(DisplayName = "§3.2: X06 리액션 — attack에 자동 발동, −50% 후 50% 반사, 1회 소진")]
    public void ReactionCounter()
    {
        var b = MakeBattle("E01", new[] { "X06" });
        b.PlaySpecial(Uid(b, "X06"));
        Assert.NotNull(b.State.Player.Reaction);
        b.EndTurn(); // stab 5 → floor(5×0.5)=2 피해, floor(2×0.5)=1 반사
        Assert.Equal(28, b.State.Player.Will);
        Assert.Equal(EWill("E01") - 1, b.State.Enemy.Will);
        Assert.Null(b.State.Player.Reaction); // 소진
    }

    [Fact(DisplayName = "§3.2: X01 지연 — 적 행동 스킵(인텐트 유지), v2 X01은 게이지 비용 없음")]
    public void DelayKeepsIntent()
    {
        var b = MakeBattle("E01", new[] { "X01" }, startGauge: 5);
        b.PlaySpecial(Uid(b, "X01"));
        Assert.Equal(5, b.State.Player.Gauge); // v1 X01의 인라인 게이지 −1은 v2 YAML에서 사라짐
        b.EndTurn();
        Assert.Equal(30, b.State.Player.Will); // stab 스킵
        Assert.Equal("stab", b.State.Enemy.IntentId); // 인텐트 유지
        b.EndTurn();
        Assert.Equal(25, b.State.Player.Will); // 다음 턴 정상 실행
    }

    [Fact(DisplayName = "E02: 준비(charge) 중 지연 적중 시 내려찍기 캔슬 (cancel_on: delay_enemy_action)")]
    public void ChargeCancelBySpecialDelay()
    {
        var b = MakeBattle("E02", new[] { "X01", "Z01", "Z02", "Z03", "Z04" }, deck: new[] { "Z05", "Z06" });
        b.EndTurn(); // roar (공격력 +2)
        Assert.Equal("smash", b.State.Enemy.IntentId);
        b.EndTurn(); // smash 준비 시작
        Assert.NotNull(b.State.Enemy.Charging);
        b.PlaySpecial(Uid(b, "X01")); // 준비 중 지연 → 캔슬
        Assert.Null(b.State.Enemy.Charging);
        Assert.Equal("roar", b.State.Enemy.IntentId); // 행동 소멸, 패턴 진행
        Assert.Equal(30, b.State.Player.Will); // 9딜 무효
    }

    [Fact(DisplayName = "E02: 리뷰 카드의 delay_enemy_action(W02)도 준비 캔슬 + 원산지 피해 동반")]
    public void ChargeCancelByReviewDelay()
    {
        var b = MakeBattle("E02", new[] { "W02", "Z01", "Z02", "Z03", "Z04" }, deck: new[] { "Z05", "Z06" });
        b.EndTurn(); // roar
        b.EndTurn(); // smash 준비
        Assert.NotNull(b.State.Enemy.Charging);
        var r = b.SubmitReview(Uid(b, "W02")); // origin E02: 동반 피해 floor(5×1.5)+1=8
        Assert.Equal(Judgement.Origin, r.Judgement);
        Assert.Equal(EWill("E02") - 8, b.State.Enemy.Will);
        Assert.Null(b.State.Enemy.Charging); // 캔슬
        Assert.Equal("roar", b.State.Enemy.IntentId);
    }

    [Fact(DisplayName = "E03: casting_weakness — 영창 중 #이펙트 리뷰 효과 ×2 (적 특성으로 이관)")]
    public void CastingWeakness()
    {
        var b = MakeBattle("E03", new[] { "L03" });
        b.EndTurn(); // fireball 영창 시작
        Assert.NotNull(b.State.Enemy.Charging);
        var r = b.SubmitReview(Uid(b, "L03")); // origin E03 · #이펙트 · 👍6 + 기절 1
        Assert.Equal(Judgement.Origin, r.Judgement);
        Assert.Equal(EWill("E03") - 19, b.State.Enemy.Will); // floor(6×1.5×2)=18, +1 = 19
        Assert.Equal(1, b.State.Enemy.StunTurns);
    }

    // ── §3.3 버프 부착 ────────────────────────────────────

    [Fact(DisplayName = "§3.3: damage_buff 부착(슬롯 점유) — 가산은 기본 좋아요에 합산 후 판정 배율")]
    public void DamageBuffAttachment()
    {
        var b = MakeBattle("E01", new[] { "D03", "Z06" });
        b.SubmitReview(Uid(b, "D03"), myEquipmentIndex: 0); // 롱소드[마감] vs #속도 → 일반 → floor(3×0.9)=2
        var att = b.State.Player.Equipment[0].Attachments[0];
        Assert.Equal(2, att.Value);
        Assert.True(att.UsesSlot); // v2: 리뷰 유래 부착은 슬롯 사용 (GDD §3.9)
        b.SubmitReview(Uid(b, "Z06")); // 팩트: (4+2)×1.5 = 9 — 가산이 배율 안쪽에 들어간다
        Assert.Equal(EWill("E01") - 9, b.State.Enemy.Will);
    }

    [Fact(DisplayName = "§2(GDD): X05 예약 가산은 고정 가산 — 내림 후 더한다")]
    public void StoredDamageBonusIsFixedAdd()
    {
        var b = MakeBattle("E01", new[] { "X05", "Z06" });
        b.PlaySpecial(Uid(b, "X05"));
        b.EndTurn(); // stab 5 피격 → 예약 확정 5
        Assert.Equal(25, b.State.Player.Will);
        b.SubmitReview(Uid(b, "Z06")); // 팩트 floor(4×1.5)=6, +5(고정) = 11
        Assert.Equal(EWill("E01") - 11, b.State.Enemy.Will);
        Assert.Equal(0, b.State.Player.StoredDamageBonus); // 소진
    }

    // ── §3.5 크리티컬 ─────────────────────────────────────

    [Fact(DisplayName = "§3.5: 크리티컬은 턴당 1회, 게이지 전량 소모")]
    public void CriticalOncePerTurn()
    {
        var b = MakeBattle("B01", Array.Empty<string>());
        b.State.Player.Gauge = 10;
        b.UseCritical(); // 품질 논점 (초기값) — 고정 20
        Assert.Equal(EWill("B01") - 20, b.State.Enemy.Will);
        Assert.Equal(0, b.State.Player.Gauge);
        b.State.Player.Gauge = 10;
        ThrowsWith<InvalidOperationException>("턴당 1회", () => b.UseCritical());
    }

    [Fact(DisplayName = "§3.5: 감성 논점 — 버프 2배 가산, 크리 간 공유 상한 +12")]
    public void ViralSharedCap()
    {
        var b = MakeBattle("B01", new[] { "D03" }, initialSuitCounters: Counters(Suit.감성, 2));
        Assert.Equal("감성 논점", DispositionLabel(b)); // 스냅샷 (argmax)
        b.SubmitReview(Uid(b, "D03"), myEquipmentIndex: 0); // 부착 floor(3×0.9)=2 (일반)
        int Att() => b.State.Player.Equipment[0].Attachments[0].Value;
        b.State.Player.Gauge = 10;
        b.UseCritical(); // 2→4 (+2, 누적 2)
        Assert.Equal(4, Att());
        b.EndTurn();
        b.State.Player.Gauge = 10;
        b.UseCritical(); // 4→8 (+4, 누적 6)
        Assert.Equal(8, Att());
        b.EndTurn();
        b.State.Player.Gauge = 10;
        b.UseCritical(); // 잔여 예산 6 → 8→14 (누적 12)
        Assert.Equal(14, Att());
        b.EndTurn();
        b.State.Player.Gauge = 10;
        b.UseCritical(); // 상한 소진 → 변화 없음
        Assert.Equal(14, Att());
        Assert.Equal(12, b.State.Player.ViralBonusGranted);
    }

    // ── §3.6 덱 규칙 ──────────────────────────────────────

    [Fact(DisplayName = "§3.6: X04 증정 카드는 런 제외, 비용 ×4 데미지(0코는 최소 1)")]
    public void GiftCardRemovedFromRun()
    {
        var b = MakeBattle("E01", new[] { "X04", "G01" });
        b.PlaySpecial(Uid(b, "X04"), giftUid: Uid(b, "G01"));
        Assert.Equal(EWill("E01") - 4, b.State.Enemy.Will); // 1코 ×4 = 4
        Assert.Single(b.State.Player.RemovedFromRun);
        Assert.Equal("G01", b.State.Player.RemovedFromRun[0].CardId);
        Assert.DoesNotContain(b.State.Player.Discard, c => c.CardId == "G01"); // 묘지 순환에서 제외

        var b2 = MakeBattle("E01", new[] { "X04", "X03" });
        b2.PlaySpecial(Uid(b2, "X04"), giftUid: Uid(b2, "X03"));
        Assert.Equal(EWill("E01") - 1, b2.State.Enemy.Will); // 0코 → max(1, 0×4) = 1 (가정: §2-1 최소 1)
    }

    // ── §3.8 적 규칙 ──────────────────────────────────────

    [Fact(DisplayName = "E01: 골드 갈취 하한 0 (음수 골드 없음)")]
    public void GoldStealFloor()
    {
        var b = MakeBattle("E01", Array.Empty<string>(), gold: 2);
        b.State.Enemy.PatternIndex = 2;
        b.State.Enemy.IntentId = "ripoff";
        b.EndTurn();
        Assert.Equal(0, b.State.Player.Gold);
    }

    [Fact(DisplayName = "E04: 은신 중 배송 계열만 명중(카드 suit 기준), 명중 시 은신 해제 → 기습 12→6")]
    public void StealthGate()
    {
        var b = MakeBattle("E04", new[] { "Z06", "D01" });
        b.EndTurn(); // hide → 은신
        Assert.True(b.State.Enemy.Stealth);
        var miss = b.SubmitReview(Uid(b, "Z06")); // 품질 계열 → 빗나감
        Assert.True(miss.Missed);
        Assert.Equal(EWill("E04"), b.State.Enemy.Will);
        Assert.Equal(0, JudgementTotal(b)); // 빗나간 리뷰는 무판정
        var hit = b.SubmitReview(Uid(b, "D01")); // 배송(D01, origin E04) → 명중 + 은신 해제
        Assert.Equal(Judgement.Origin, hit.Judgement);
        Assert.False(b.State.Enemy.Stealth);
        Assert.Equal(EWill("E04") - 10, b.State.Enemy.Will); // floor(6×1.5)+1 = 10
        b.EndTurn(); // ambush: 은신 해제 상태 → 6
        Assert.Equal(24, b.State.Player.Will);
    }

    [Fact(DisplayName = "E05: 감성 계열 의지 데미지 ×2(vanity) + 찬양 강요 게이지 −1(하한 0)")]
    public void VanityAndDemandPraise()
    {
        var b = MakeBattle("E05", new[] { "Z05" }, startGauge: 5);
        b.SubmitReview(Uid(b, "Z05")); // #감성 팩트 → floor(4×1.5×2)=12
        Assert.Equal(EWill("E05") - 12, b.State.Enemy.Will);
        Assert.Equal(8, b.State.Player.Gauge); // 5 + 팩트 +3
        b.State.Enemy.PatternIndex = 2;
        b.State.Enemy.IntentId = "demand_praise";
        b.EndTurn();
        Assert.Equal(7, b.State.Player.Gauge); // −1 (§3.4)

        var lo = MakeBattle("E05", Array.Empty<string>());
        lo.State.Enemy.PatternIndex = 2;
        lo.State.Enemy.IntentId = "demand_praise";
        lo.EndTurn();
        Assert.Equal(0, lo.State.Player.Gauge); // 하한 0
    }

    [Fact(DisplayName = "B01: 사장님 답글 — 정지 → 같은 계열 원산지/팩트로 재반박(부활+게이지+1) → 재정지 불가")]
    public void OwnerReplyRebut()
    {
        var b = MakeBattle("B01", new[] { "K01", "B01c" });
        b.SubmitReview(Uid(b, "K01")); // 원산지(B01) → 공격력 −4 디버프(배송 계열): floor(3×1.5)=4
        var debuff = b.State.Enemy.Debuffs[0];
        Assert.Equal(4, debuff.Value);
        b.State.Enemy.PatternIndex = 2;
        b.State.Enemy.IntentId = "owner_reply";
        b.EndTurn();
        Assert.True(debuff.Suspended); // 이번 전투 한정 정지
        int g = b.State.Player.Gauge;
        b.SubmitReview(Uid(b, "B01c")); // 같은 계열(배송) 원산지 → 재반박 (원산지는 팩트의 상위 판정 — 해석)
        Assert.False(debuff.Suspended); // 부활
        Assert.Equal(g + 4 + 1, b.State.Player.Gauge); // 원산지 +4, 재반박 성공 +1 (§3.4)
        int will = b.State.Enemy.Will;
        b.State.Enemy.PatternIndex = 2;
        b.State.Enemy.IntentId = "owner_reply";
        b.State.Enemy.CooldownLastFired["owner_reply"] = -10; // cooldown 3 경과 가정
        b.EndTurn();
        Assert.False(debuff.Suspended); // 디버프당 반박 1회 소진 → 재정지 불가
        Assert.Equal(Math.Min(EWill("B01"), will + 5), b.State.Enemy.Will); // 대상 없음 → 의지 +5만
    }

    [Fact(DisplayName = "B01: 반박 우선순위 — 「힙스터 인증」 크리(Tier 3)를 일반 디버프(Tier 1)보다 먼저 정지")]
    public void RebutPriority()
    {
        var b = MakeBattle("B01", new[] { "K01" }, initialSuitCounters: Counters(Suit.성능, 1));
        Assert.Equal("성능 논점", DispositionLabel(b));
        b.SubmitReview(Uid(b, "K01")); // Tier 1 attack_down 4 (원산지 강화)
        b.State.Player.Gauge = 10;
        b.UseCritical(); // Tier 3 attack_halve
        b.State.Enemy.PatternIndex = 2;
        b.State.Enemy.IntentId = "owner_reply";
        b.EndTurn();
        var halve = b.State.Enemy.Debuffs.First(d => d.Kind == EnemyDebuffKind.AttackHalve);
        var down = b.State.Enemy.Debuffs.First(d => d.Kind == EnemyDebuffKind.AttackDown);
        Assert.True(halve.Suspended); // 최우선 반박 (R22)
        Assert.False(down.Suspended);
        b.State.Enemy.PatternIndex = 0;
        b.State.Enemy.IntentId = "contract_slash";
        b.EndTurn();
        // 「힙스터 인증」 정지 → −50% 미적용, attack_down 4만
        Assert.Equal(30 - (SlashDamage - 4), b.State.Player.Will);
    }

    [Fact(DisplayName = "전 장비 파괴 → 항복 승리 + 6G (원산지 구성품 피해 +1 포함)")]
    public void SurrenderOnAllEquipmentDestroyed()
    {
        var b = MakeBattle("E01", new[] { "Q03" }, gold: 0);
        b.SubmitReview(Uid(b, "Q03"), enemyEquipmentIndex: 0);
        // 짝퉁 단검 원산지: floor(8×1.5)+1=13 ≥ 내구도 6 → 파괴 (내구도도 좋아요 단위 — ADR-015)
        Assert.Equal(BattleResult.Win, b.State.Result);
        Assert.True(b.State.Stats.Surrender);
        Assert.Equal(6, b.State.Player.Gold);
    }

    [Fact(DisplayName = "X02: 별점 테러 — 무판정 의지 12 (배율·게이지 비대상)")]
    public void X02NoJudgement()
    {
        var b = MakeBattle("E01", new[] { "X02" });
        b.PlaySpecial(Uid(b, "X02"));
        Assert.Equal(EWill("E01") - 12, b.State.Enemy.Will);
        Assert.Equal(0, JudgementTotal(b));
        Assert.Equal(0, b.State.Player.Gauge);
    }

    [Fact(DisplayName = "X08: 별점 구걸 — 신뢰도 +3 (v2)")]
    public void X08Gauge()
    {
        var b = MakeBattle("E01", new[] { "X08" });
        b.PlaySpecial(Uid(b, "X08"));
        Assert.Equal(3, b.State.Player.Gauge);
    }

    [Fact(DisplayName = "X09(Layer 2): Σp(상한 5)×3 데미지, Layer 1에선 사용 불가")]
    public void X09LayerGate()
    {
        var b = MakeBattle("B01", new[] { "X09" }, layer: 2, sigmaP: 5);
        b.PlaySpecial(Uid(b, "X09"));
        Assert.Equal(EWill("B01") - 15, b.State.Enemy.Will); // 5×3 = 15

        var b2 = MakeBattle("B01", new[] { "X09" }, layer: 2, sigmaP: 9);
        b2.PlaySpecial(Uid(b2, "X09"));
        Assert.Equal(EWill("B01") - 15, b2.State.Enemy.Will); // cap_points 5 → min(9,5)×3

        var b3 = MakeBattle("B01", new[] { "X09" }, layer: 1);
        // MVP(Layer 1)에서는 사용 불가
        ThrowsWith<InvalidOperationException>("Layer", () => b3.PlaySpecial(Uid(b3, "X09")));
    }

    [Fact(DisplayName = "결정성: 같은 시드·같은 정책 입력 → 같은 결과")]
    public void Deterministic()
    {
        string Run()
        {
            var b = new Battle(new BattleConfig
            {
                Cards = Cards,
                Enemy = Enemies["E03"],
                Deck = StartingDeck,
                Rng = RngFactory.Mulberry32(123),
            });
            // 고정 스크립트: 매턴 낼 수 있는 첫 리뷰 카드 제출
            int guard = 200;
            while (b.State.Result is null && guard-- > 0)
            {
                var c = b.State.Player.Hand.FirstOrDefault(h =>
                    Cards.ById[h.CardId] is ReviewCardDef d && d.Cost <= b.State.Player.Energy);
                if (c is not null) b.SubmitReview(c.Uid);
                else b.EndTurn();
            }
            var s = b.State.Stats;
            return string.Join('|',
                b.State.Result, b.State.Turn, b.State.Player.Will, b.State.Enemy.Will,
                s.Submissions, s.Judgements[Judgement.Origin], s.Judgements[Judgement.Fact],
                s.Judgements[Judgement.Normal], s.Judgements[Judgement.Fumble],
                s.GaugeGained, s.GaugeLost, s.GaugeOverflowLost, s.GaugeReached10,
                string.Join(',', s.Crits), s.CritMisses, s.Surrender,
                s.WillHealed, s.DefenseGained, s.DefenseAbsorbed);
        }

        Assert.Equal(Run(), Run());
    }

    // ── previewSubmit: 미리보기 = 실제 (화면이 규칙을 재구현하지 않는다) ──

    [Fact(DisplayName = "previewSubmit: 판정 4단계 전부 실제 제출과 일치")]
    public void PreviewMatchesAllJudgements()
    {
        AssertPreviewMatches(MakeBattle("E01", new[] { "Q01" }), 1); // 원산지
        AssertPreviewMatches(MakeBattle("E01", new[] { "Z06" }), 1); // 팩트
        AssertPreviewMatches(MakeBattle("E01", new[] { "Z01" }), 1); // 일반
        AssertPreviewMatches(MakeBattle("E05", new[] { "Z01" }), 1); // 헛소리 (E05 무효 [출력]… 계열 확인용)
    }

    [Fact(DisplayName = "previewSubmit: 구성품 대상·내 장비 대상도 일치")]
    public void PreviewMatchesEquipmentTarget()
    {
        var b = MakeBattle("E02", new[] { "W01" }); // 초대형 둔기 원산지
        AssertPreviewMatches(b, Uid(b, "W01"), enemyEquipmentIndex: 0);
    }

    [Fact(DisplayName = "previewSubmit: 상태를 바꾸지 않는다")]
    public void PreviewIsPure()
    {
        var b = MakeBattle("E01", new[] { "Q01" });
        string snap = Snapshot(b);
        b.PreviewSubmit(Uid(b, "Q01"));
        b.PreviewSubmit(Uid(b, "Q01"), enemyEquipmentIndex: 0);
        Assert.Equal(snap, Snapshot(b)); // previewSubmit 이 상태를 오염시킴
    }

    private static readonly JsonSerializerOptions SnapshotOptions = new() { WriteIndented = false };

    private static string Snapshot(Battle b) => JsonSerializer.Serialize(b.State, SnapshotOptions);

    [Fact(DisplayName = "previewSubmit: 은신 빗나감·무판정 카드를 blocked 로 알린다")]
    public void PreviewReportsBlocked()
    {
        var b = MakeBattle("E04", new[] { "Z01", "X01" }); // E04 은신 게이트
        b.State.Enemy.Stealth = true;
        var pv = b.PreviewSubmit(Uid(b, "Z01"));
        Assert.Equal(BlockedReason.Miss, pv.Blocked);
        Assert.Null(pv.Likes);
        Assert.Equal(0, pv.Heal); // 빗나감 = 판정 없음 = 회복 없음 (ADR-023 ②)
        Assert.Equal(BlockedReason.NotReview, b.PreviewSubmit(Uid(b, "X01")).Blocked); // 진상 화법 = 무판정
    }

    // ── ADR-023 ①: 방어 축 (찬양 리뷰 → 내 장비 방어) ─────

    [Fact(DisplayName = "ADR-023 ①: defense_buff는 판정 배율을 받는다 (팩트 ×1.5 / 일반 ×0.9 / 헛소리 ×0.5)")]
    public void DefenseBuffScalesWithJudgement()
    {
        var fact = MakeBattle("E01", new[] { "T_DEF6" }, cards: TestIndex);
        fact.SubmitReview(Uid(fact, "T_DEF6"), myEquipmentIndex: 0); // 롱소드[마감] vs #마감 → 팩트
        Assert.Equal(9, fact.State.Player.Equipment[0].Defense); // floor(6×1.5)
        Assert.Equal(9, fact.State.Stats.DefenseGained);

        var normal = MakeBattle("E01", new[] { "T_DEF6" }, cards: TestIndex);
        normal.SubmitReview(Uid(normal, "T_DEF6"), myEquipmentIndex: 1); // 갑옷[내구도,무게] → 일반
        Assert.Equal(5, normal.State.Player.Equipment[1].Defense); // floor(6×0.9)

        var fumble = MakeBattle("E01", new[] { "T_DEFN" }, cards: TestIndex);
        fumble.SubmitReview(Uid(fumble, "T_DEFN"), myEquipmentIndex: 0); // 롱소드 무효 태그[연비] → 헛소리
        Assert.Equal(3, fumble.State.Player.Equipment[0].Defense); // floor(6×0.5)
    }

    [Fact(DisplayName = "ADR-023 ①: 방어는 부착 슬롯을 쓰지 않는다 (damage_buff 2칸 만석이어도 부여 가능)")]
    public void DefenseIgnoresAttachSlots()
    {
        var b = MakeBattle("E01", new[] { "D03", "A01", "T_DEF6" }, cards: TestIndex);
        b.SubmitReview(Uid(b, "D03"), myEquipmentIndex: 0);
        b.SubmitReview(Uid(b, "A01"), myEquipmentIndex: 0);
        var eq = b.State.Player.Equipment[0];
        Assert.Equal(2, eq.Attachments.Count(a => a.UsesSlot)); // 슬롯 만석 (GDD §3.9)
        b.EndTurn(); // 필력 회복 (stab 5)
        b.SubmitReview(Uid(b, "T_DEF6"), myEquipmentIndex: 0);
        Assert.Equal(9, eq.Defense); // 슬롯과 무관하게 누적
        Assert.Equal(2, eq.Attachments.Count(a => a.UsesSlot));
    }

    [Fact(DisplayName = "ADR-023 ①: 부분 흡수 — 방어로 막고 남은 만큼만 의지가 깎인다")]
    public void PartialAbsorb()
    {
        var b = MakeBattle("E01", new[] { "T_DEF2" }, cards: TestIndex);
        b.SubmitReview(Uid(b, "T_DEF2"), myEquipmentIndex: 0); // 팩트 → 방어 3
        Assert.Equal(3, DefenseTotal(b));
        b.EndTurn(); // stab 5 → 3 흡수, 2만 의지로
        Assert.Equal(28, b.State.Player.Will);
        Assert.Equal(0, DefenseTotal(b));
        Assert.Equal(3, b.State.Stats.DefenseAbsorbed);
    }

    [Fact(DisplayName = "ADR-023 ①: 흡수분은 \"이번 턴 받은 피해\"에 포함되지 않는다 (X05 예약 산정)")]
    public void AbsorbedIsNotDamageTaken()
    {
        var b = MakeBattle("E01", new[] { "T_DEF2", "X05", "Z06" }, cards: TestIndex);
        b.SubmitReview(Uid(b, "T_DEF2"), myEquipmentIndex: 0); // 방어 3
        b.PlaySpecial(Uid(b, "X05")); // 이번 턴 받은 피해량 예약
        b.EndTurn(); // stab 5 → 방어 3 흡수, 의지 2만 감소 → 예약 확정은 2
        Assert.Equal(28, b.State.Player.Will);
        Assert.Equal(2, b.State.Player.StoredDamageBonus); // 5가 아니다
    }

    [Fact(DisplayName = "ADR-023 ①: 전량 흡수 — 의지 무손실, 잔량은 다음 턴에도 유지된다(턴 리셋 없음)")]
    public void FullAbsorbPersists()
    {
        var b = MakeBattle("E01", new[] { "T_DEF6" }, cards: TestIndex);
        b.SubmitReview(Uid(b, "T_DEF6"), myEquipmentIndex: 0); // 팩트 → 방어 9
        b.EndTurn(); // stab 5 → 전량 흡수
        Assert.Equal(30, b.State.Player.Will);
        Assert.Equal(4, DefenseTotal(b)); // 잔량 유지 (다음 턴에 리셋되지 않는다)
        b.EndTurn(); // stab 5 → 4 흡수 + 1 의지
        Assert.Equal(29, b.State.Player.Will);
        Assert.Equal(0, DefenseTotal(b));
        Assert.Equal(9, b.State.Stats.DefenseAbsorbed);
    }

    [Fact(DisplayName = "ADR-023 ①: 여러 장비의 방어는 슬롯 선언 순으로 소모된다")]
    public void AbsorbInSlotOrder()
    {
        var b = MakeBattle("E01", Array.Empty<string>(), cards: TestIndex);
        b.State.Player.Equipment[0].Defense = 2;
        b.State.Player.Equipment[1].Defense = 4;
        b.EndTurn(); // stab 5 → 슬롯0에서 2, 슬롯1에서 3
        Assert.Equal(0, b.State.Player.Equipment[0].Defense);
        Assert.Equal(1, b.State.Player.Equipment[1].Defense);
        Assert.Equal(30, b.State.Player.Will);
    }

    // ── ADR-023 ②: 호응 회복 ──────────────────────────────

    [Fact(DisplayName = "ADR-023 ②: 판정 회복 4단계 — 원산지 +2 / 팩트 +1 / 일반 0 / 헛소리 0")]
    public void HealByJudgement()
    {
        (string CardId, Judgement Judgement, int Heal)[] cases =
        {
            ("Q01", Judgement.Origin, 2), // origin E01
            ("Z06", Judgement.Fact, 1),   // #마감 ∈ E01 약점
            ("W01", Judgement.Normal, 0), // #무게 — E01과 무관
            ("Z01", Judgement.Fumble, 0), // #연비 ∈ E01 무효
        };
        foreach (var (cardId, judgement, heal) in cases)
        {
            var b = MakeBattle("E01", new[] { cardId });
            b.State.Player.Will = 20;
            var r = b.SubmitReview(Uid(b, cardId));
            Assert.Equal(judgement, r.Judgement);
            Assert.Equal(20 + heal, b.State.Player.Will);
            Assert.Equal(heal, b.State.Stats.WillHealed);
        }
    }

    [Fact(DisplayName = "ADR-023 ②: 회복은 maxWill 상한 — 초과분은 버려지고 계측도 실제 증가분만")]
    public void HealClampedToMaxWill()
    {
        var b = MakeBattle("E01", new[] { "Q01" });
        b.State.Player.Will = 29; // 원산지 +2 → 30에서 멈춘다
        var pv = b.PreviewSubmit(Uid(b, "Q01"));
        Assert.Equal(1, pv.Heal); // 클램프 반영한 실제 증가분 (2가 아니다)
        b.SubmitReview(Uid(b, "Q01"));
        Assert.Equal(30, b.State.Player.Will);
        Assert.Equal(1, b.State.Stats.WillHealed);

        var full = MakeBattle("E01", new[] { "Z06" }); // 만피 팩트 → 회복 0
        Assert.Equal(0, full.PreviewSubmit(Uid(full, "Z06")).Heal);
        full.SubmitReview(Uid(full, "Z06"));
        Assert.Equal(0, full.State.Stats.WillHealed);
    }

    [Fact(DisplayName = "ADR-023 ②: 빗나감(은신 게이트)은 회복 0")]
    public void MissHealsNothing()
    {
        var b = MakeBattle("E04", new[] { "D01" }); // D01 = 배송(origin E04) — 명중 계열
        b.State.Player.Will = 20;
        b.State.Enemy.Stealth = true;
        b.State.Player.Hand.Add(new CardInstance { Uid = 999, CardId = "Z06" }); // 품질 계열 → 은신 중 빗나감
        var miss = b.SubmitReview(999);
        Assert.True(miss.Missed);
        Assert.Equal(20, b.State.Player.Will); // 판정 없음 → 회복 없음
        Assert.Equal(0, b.State.Stats.WillHealed);
        b.SubmitReview(Uid(b, "D01")); // 원산지 명중 → +2
        Assert.Equal(22, b.State.Player.Will);
    }

    [Fact(DisplayName = "previewSubmit: 방어·회복 예상치도 실제 결과와 일치")]
    public void PreviewMatchesDefenseAndHeal()
    {
        var def = MakeBattle("E01", new[] { "T_DEF6" }, cards: TestIndex);
        def.State.Player.Will = 25; // 팩트 판정 → 방어 9 + 회복 1
        var pv = def.PreviewSubmit(Uid(def, "T_DEF6"), myEquipmentIndex: 0);
        Assert.Equal(LikesKind.Defense, pv.LikesKind);
        Assert.Equal(9, pv.Likes);
        Assert.Equal(1, pv.Heal);
        AssertPreviewMatches(def, Uid(def, "T_DEF6"), myEquipmentIndex: 0);

        // 판정별 회복 — 한 전투에서 연속 제출하면 적이 먼저 죽으므로 전투를 분리한다
        Battle Wounded(string cardId)
        {
            var b = MakeBattle("E01", new[] { cardId });
            b.State.Player.Will = 20; // 회복 여지를 만든다 (만피면 클램프로 전부 0)
            return b;
        }

        AssertPreviewMatches(Wounded("Q01"), 1); // 원산지 +2
        AssertPreviewMatches(Wounded("Z06"), 1); // 팩트 +1
        AssertPreviewMatches(Wounded("W01"), 1); // 일반 0
        AssertPreviewMatches(Wounded("Z01"), 1); // 헛소리 0
    }

    // ── 택배 개봉 (ADR-024 ③) ──

    [Fact(DisplayName = "택배: 보스전에만 따라오고 개봉하면 내 장비가 된다")]
    public void ParcelOnBossOnly()
    {
        var b = MakeBattle("B01", new[] { "Z01" });
        Assert.True(b.ParcelAvailable, "보스전엔 택배가 있어야 한다");
        int before = b.State.Player.Equipment.Count;
        int e0 = b.State.Player.Energy;
        var got = b.OpenParcel();
        Assert.Equal(before + 1, b.State.Player.Equipment.Count);
        Assert.Equal(e0 - 1, b.State.Player.Energy); // 필력 1 소모
        Assert.Contains("디자인", got.Tags); // 디자인 태그 — 디자인 찬양 카드의 해금 고리
        Assert.False(b.ParcelAvailable); // 전투당 1회
        ThrowsWith<InvalidOperationException>("이미 개봉", () => b.OpenParcel());
    }

    [Fact(DisplayName = "택배: 일반 전투엔 없다")]
    public void NoParcelInNormalBattle()
    {
        var b = MakeBattle("E01", new[] { "Z01" });
        Assert.False(b.ParcelAvailable);
        ThrowsWith<InvalidOperationException>("개봉할 택배가 없다", () => b.OpenParcel());
    }

    [Fact(DisplayName = "택배: 개봉한 장비가 찬양 리뷰의 대상이 된다 (디자인 → 팩트)")]
    public void ParcelUnlocksDesignPraise()
    {
        var b = MakeBattle("B01", new[] { "N03" }); // N03 #디자인 my_equipment 찬양
        int idx = b.State.Player.Equipment.Count; // 개봉 후 추가될 슬롯
        b.OpenParcel();
        var pv = b.PreviewSubmit(Uid(b, "N03"), myEquipmentIndex: idx);
        Assert.Equal(Judgement.Fact, pv.Judgement); // 명패의 #디자인에 맞아 팩트여야 한다
    }
}
