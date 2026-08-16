// 감사 지적 반영분 검증 (2026-07-31 감사 — 은신 크리 게이트, 적 피해 배율 §2-1,
// 온보딩 보정, cooldown 하한, timeout 클램프, 게이지 도달/초과 소실 계측)
// packages/sim/test/audit-fixes.test.ts 이관 (ADR-029).
//
// v2 전환 노트: v1의 S07(장비 비활성화) 무한 락 테스트는 v2 카드 60장에 disable_equipment가
// 없어 제거 — 엔진의 disable_equipment 케이스·경직 내성 규칙은 예비로 유지된다 (Battle.cs).

using ReviewHero.Engine;
using static ReviewHero.Engine.Tests.TestData;

namespace ReviewHero.Engine.Tests;

public class AuditFixesTests
{
    // ── E04 은신 게이트 — 크리티컬 리뷰 (§3.8 특성 문언 "리뷰만 명중") ────

    [Fact(DisplayName = "E04: 은신 중 비배송 크리(품질 논점)는 빗나감 — 게이지만 소모")]
    public void StealthGateBlocksNonDeliveryCrit()
    {
        var b = MakeBattle("E04", Array.Empty<string>()); // 초기 논점 = 품질 논점
        b.EndTurn(); // hide → 은신
        Assert.True(b.State.Enemy.Stealth);
        b.State.Player.Gauge = 10;
        b.UseCritical();
        Assert.Equal(EWill("E04"), b.State.Enemy.Will); // 고정 20 미적용 (즉사 우회 봉쇄)
        Assert.Equal(0, b.State.Player.Gauge); // 자원 소모는 유지 (빗나간 리뷰와 일관)
        Assert.Equal(1, b.State.Stats.CritMisses);
    }

    [Fact(DisplayName = "E04: 은신 중 배송 크리(배송 논점)는 명중 + 은신 해제")]
    public void StealthGateAllowsDeliveryCrit()
    {
        var b = MakeBattle("E04", Array.Empty<string>(), initialSuitCounters: Counters(Suit.배송, 1));
        Assert.Equal("배송 논점", DispositionLabel(b));
        b.EndTurn(); // hide → 은신
        b.State.Player.Gauge = 10;
        b.UseCritical();
        Assert.False(b.State.Enemy.Stealth); // breakOnHit
        Assert.Equal(1, b.State.Enemy.StunTurns);
        Assert.Equal(15, b.State.Player.Gold); // 정예 15G (§3.5)
        Assert.Equal(0, b.State.Stats.CritMisses);
    }

    // ── §2-1 "모든 배율은 내림, 최소 1" — 적 피해 배율 경로 ───────────────

    [Fact(DisplayName = "§2-1: 적 피해도 배율 결과는 최소 1 (감산 하한 0은 유지)")]
    public void EnemyDamageMultFloorIsOne()
    {
        // E01 stab 5, attack_down 4 → 감산 후 1, 「힙스터 인증」 ×0.5 → floor(0.5)=0이 아니라 최소 1
        var b = MakeBattle("E01", Array.Empty<string>());
        b.State.Enemy.Debuffs.Add(new EnemyDebuff
        {
            Uid = 900, Kind = EnemyDebuffKind.AttackDown, Value = 4, Suit = Suit.배송,
            Tier = 1, Suspended = false, BeenRebutted = false, CreatedAt = 900,
        });
        b.State.Enemy.Debuffs.Add(new EnemyDebuff
        {
            Uid = 901, Kind = EnemyDebuffKind.AttackHalve, Value = 50, Suit = Suit.성능,
            Tier = 3, Suspended = false, BeenRebutted = false, CreatedAt = 901,
        });
        b.EndTurn(); // stab: max(0, 5−4)=1 → applyMult(1, 0.5)=1
        Assert.Equal(29, b.State.Player.Will);

        // 감산만으로 0이면 배율 없이 0 유지 (감산은 배율이 아님 — 가정)
        var b2 = MakeBattle("E01", Array.Empty<string>());
        b2.State.Enemy.Debuffs.Add(new EnemyDebuff
        {
            Uid = 902, Kind = EnemyDebuffKind.AttackDown, Value = 9, Suit = Suit.배송,
            Tier = 1, Suspended = false, BeenRebutted = false, CreatedAt = 902,
        });
        b2.EndTurn();
        Assert.Equal(30, b2.State.Player.Will);
    }

    // ── 온보딩 보정 (§3.3 버프 무판정, §4.4 적 공격 ×0.75·헛소리 −1) ─────

    [Fact(DisplayName = "§4.4: 온보딩 1판 보정 — 헛소리 −1, 버프 카드 무판정, 적 공격 ×0.75")]
    public void OnboardingMods()
    {
        // 시작 장비 태그로는 v2 버프 카드(#속도·#디자인)가 팩트일 수 없어 검증용 장비를 주입
        var b = MakeBattle(
            "E01",
            new[] { "Z01", "D03" },
            startGauge: 3,
            playerEquipment: new[]
            {
                new PlayerEquipmentDef { Name = "중고 러닝화", Tags = new[] { "속도" }, NullTags = Array.Empty<string>() },
            },
            onboarding: new OnboardingMods { EnemyDamageMult = 0.75, FumbleGaugeDelta = -1, BuffNoJudgement = true });

        b.SubmitReview(Uid(b, "Z01")); // #연비 → 헛소리
        Assert.Equal(2, b.State.Player.Gauge); // −2 대신 −1
        Assert.Equal(EWill("E01") - 2, b.State.Enemy.Will); // 피해 규칙은 정상: max(1, floor(5×0.5)) = 2
        b.SubmitReview(Uid(b, "D03"), myEquipmentIndex: 0); // 러닝화[속도] — 팩트감이지만 무판정
        // ×1.5 미적용 (항상 일반 — floor(3×0.9))
        Assert.Equal(2, b.State.Player.Equipment[0].Attachments[0].Value);
        Assert.Equal(2, b.State.Player.Gauge); // 게이지 변화 없음
        b.EndTurn(); // stab 5 → applyMult(5, 0.75) = 3
        Assert.Equal(27, b.State.Player.Will);
    }

    // ── B01 사장님 답글 cooldown 하한 (가정: "3턴마다 발동" 강제) ─────────

    [Fact(DisplayName = "B01: owner_reply는 마지막 발동 후 3턴 전 재도래 시 불발 (패턴 앞당김 방어)")]
    public void OwnerReplyCooldownFloor()
    {
        var b = MakeBattle("B01", new[] { "B01c" });
        b.SubmitReview(Uid(b, "B01c")); // 원산지 floor(9×1.5)+1=14
        int afterReply = EWill("B01") - 14 + 5;

        void ForceOwnerReply()
        {
            b.State.Enemy.PatternIndex = 2;
            b.State.Enemy.IntentId = "owner_reply";
            b.EndTurn();
        }

        ForceOwnerReply(); // 턴1 발동: 반박 대상 없음 → 의지 +5
        Assert.Equal(afterReply, b.State.Enemy.Will);
        ForceOwnerReply(); // 턴2: 2−1=1 < 3 → 재사용 대기 (불발)
        Assert.Equal(afterReply, b.State.Enemy.Will);
        ForceOwnerReply(); // 턴3: 3−1=2 < 3 → 불발
        Assert.Equal(afterReply, b.State.Enemy.Will);
        ForceOwnerReply(); // 턴4: 4−1=3 ≥ 3 → 발동 (+5)
        Assert.Equal(afterReply + 5, b.State.Enemy.Will);
    }

    // ── 시뮬 통계 정확성 ─────────────────────────────────────────────────

    [Fact(DisplayName = "timeout 시 기록 턴은 maxTurns로 클램프 (off-by-one 방지)")]
    public void TimeoutTurnClamp()
    {
        var b = MakeBattle("E01", Array.Empty<string>(), maxTurns: 3);
        b.EndTurn();
        b.EndTurn();
        b.EndTurn();
        Assert.Equal(BattleResult.Timeout, b.State.Result);
        Assert.Equal(3, b.State.Turn);
    }

    [Fact(DisplayName = "§2-2 계측: 게이지 10 도달(크리 가능)·초과 소실이 stats에 기록된다")]
    public void GaugeStats()
    {
        var b = MakeBattle("E01", new[] { "Z06" }, startGauge: 9);
        b.SubmitReview(Uid(b, "Z06")); // 팩트 +3 → 9+3=12 → 10 (2 소실)
        Assert.Equal(10, b.State.Player.Gauge);
        Assert.Equal(1, b.State.Stats.GaugeReached10);
        Assert.Equal(2, b.State.Stats.GaugeOverflowLost);
    }
}
