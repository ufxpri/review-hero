// 밸런스 라운드 1 (GDD v1.1) 신규 규칙 검증 — 제안 1·3·5·6 + 퇴고
// packages/sim/test/balance-v1.1.test.ts 이관 (ADR-029).
//
// v2 전환 노트: 카드 참조만 v2(단일 리뷰 카드)로 교체. 규칙 자체는 판정 하류라 무변경.
// 퇴고는 v1의 "교착 안전장치"에서 v2의 "태그 사냥 도구"로 역할이 승격됐다 (card-system-v2 §7).

using ReviewHero.Engine;
using static ReviewHero.Engine.Tests.TestData;

namespace ReviewHero.Engine.Tests;

public class BalanceV11Tests
{
    [Fact(DisplayName = "v1.1 제안 1: 팩트 판정 게이지 +3")]
    public void FactGaugeIsThree()
    {
        var b = MakeBattle("E01", new[] { "Z06" });
        b.SubmitReview(Uid(b, "Z06")); // #마감 ∈ E01 약점 → 팩트
        Assert.Equal(3, b.State.Player.Gauge);
    }

    // 밸런스 라운드 1(v2) 확정 수치 잠금 — 여기가 "의지 수치 자체"를 검증하는 유일한 자리다.
    // 규칙 테스트(BattleTests)는 이 값을 YAML 에서 읽으므로, 수치가 바뀌면 이 테스트만 깨진다.
    // 근거: design/balance-report-v2-round1.md (일반 2~4턴 / 정예 5~6턴 / 보스 6~7턴, GDD §3.1)
    [Fact(DisplayName = "밸런스 v2-r1: 적 의지 확정치 (E01 30 / E02 55 / E03 65 / E04 55 / E05 70 / B01 78)")]
    public void EnemyWillLock()
    {
        Assert.Equal(30, Enemies["E01"].Will);
        Assert.Equal(55, Enemies["E02"].Will);
        Assert.Equal(65, Enemies["E03"].Will);
        Assert.Equal(55, Enemies["E04"].Will);
        Assert.Equal(70, Enemies["E05"].Will);
        Assert.Equal(78, Enemies["B01"].Will);
    }

    // 판정 배율 — 우위 판정(원산지·팩트)과 일반 판정의 간격이 밸런스의 중심축이다 (card-system-v2 §2)
    [Fact(DisplayName = "밸런스 v2-r1: 판정 배율 (원산지·팩트 ×1.5 / 일반 ×0.9 / 헛소리 ×0.5)")]
    public void JudgementMultLock()
    {
        var mult = RulesConfig.Default.Judge.Mult;
        Assert.Equal(1.5, mult[Judgement.Origin]);
        Assert.Equal(1.5, mult[Judgement.Fact]);
        Assert.Equal(0.9, mult[Judgement.Normal]);
        Assert.Equal(0.5, mult[Judgement.Fumble]);
    }

    [Fact(DisplayName = "v1.1 제안 3: 페이즈2 비례 트리거 — 의지 48이면 50% = 24에서 발동")]
    public void Phase2ProportionalTrigger()
    {
        var baseDef = Enemies["B01"];
        Assert.Equal(50, baseDef.Phase2!.TriggerPct); // YAML "의지 50% 이하" 파싱
        var enemy = baseDef with { Will = 48 };
        var b = MakeBattle("B01", Array.Empty<string>(), enemy: enemy);
        // 의지를 25까지 깎아도 미발동 (문턱 = floor(48×50%) = 24)
        b.State.Enemy.Will = 25;
        b.EndTurn();
        Assert.False(b.State.Enemy.Phase2Done);
        // 24 이하로 깎이면 발동 (직전 턴 답글 회복 +5 가능성 배제 위해 직접 설정)
        b.State.Enemy.Will = 24;
        b.EndTurn();
        Assert.True(b.State.Enemy.Phase2Done);
    }

    [Fact(DisplayName = "v1.1 제안 5: 「바이럴 확산」 바닥 보장 — 버프 0개면 +3 가산 버프 부착 (+12 상한 공유)")]
    public void ViralFloorBonus()
    {
        var b = MakeBattle("E01", Array.Empty<string>(), initialSuitCounters: Counters(Suit.감성, 5));
        Assert.Equal("감성 논점", DispositionLabel(b));
        b.State.Player.Gauge = 10;
        b.UseCritical();
        var buffs = b.State.Player.Equipment
            .SelectMany(eq => eq.Attachments.Where(a => a.Kind == AttachmentKind.DamageBuff))
            .ToList();
        Assert.Single(buffs);
        Assert.Equal(3, buffs[0].Value); // 기본 버프 상당
        Assert.False(buffs[0].UsesSlot); // 크리 산출물 — 부착 슬롯 미점유
        Assert.Equal(3, b.State.Player.ViralBonusGranted); // +12 상한 공유
    }

    [Fact(DisplayName = "v1.1 제안 6: 「진상 접수」 — 기절 + 다음 행동 위력 −50% (기절 면역 시에도 −50%는 적용)")]
    public void InconvenienceWeakenSurvivesStunImmunity()
    {
        var b = MakeBattle("E01", Array.Empty<string>(), initialSuitCounters: Counters(Suit.배송, 5));
        Assert.Equal("배송 논점", DispositionLabel(b));
        // 경직 내성 상태를 만들어 기절이 무효인 상황에서 −50%가 남는지 확인
        b.State.Enemy.StaggerImmunityTurns = 1;
        b.State.Player.Gauge = 10;
        b.UseCritical();
        Assert.Equal(0, b.State.Enemy.StunTurns); // 기절 무효 (경직 내성)
        Assert.Equal(-50, b.State.Enemy.WeakenNextActionPct); // 위력 감소는 적용
        b.EndTurn(); // E01 stab 5 → floor(5×0.5)=2
        Assert.Equal(28, b.State.Player.Will);
    }

    [Fact(DisplayName = "v2 퇴고: 필력 1로 손패 1장 교체 — 태그 사냥 (card-system-v2 §7)")]
    public void Revise()
    {
        var b = MakeBattle("E01", new[] { "Z01", "Z02", "Z04", "Z05", "Z06" }, deck: new[] { "G01" });
        // E01전에서 헛소리(Z01 #연비)를 버리고 원하는 태그를 찾는 용도
        int before = b.State.Player.Energy;
        b.Revise(Uid(b, "Z01"));
        Assert.Equal(before - 1, b.State.Player.Energy);
        Assert.Contains(b.State.Player.Hand, c => c.CardId == "G01");
        Assert.Equal(5, b.State.Player.Hand.Count);

        // 뽑을 카드가 없으면 사용 불가
        var empty = MakeBattle("E01", new[] { "Z01", "Z02", "Z04", "Z05", "Z06" });
        ThrowsWith<InvalidOperationException>("뽑을 카드 없음", () => empty.Revise(Uid(empty, "Z01")));
    }
}
