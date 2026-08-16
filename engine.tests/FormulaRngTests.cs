// packages/core/test/rng.test.ts 이관 (ADR-029) — 순수 함수 2종.
// TS 의 applyMult 는 C# 에서 Formula.ApplyMult, mulberry32 는 RngFactory.Mulberry32 다.

using ReviewHero.Engine;

namespace ReviewHero.Engine.Tests;

public class FormulaRngTests
{
    [Fact(DisplayName = "공통 계산 §2-1: 배율 내림·최소 1")]
    public void ApplyMultFloorsAndClampsToOne()
    {
        Assert.Equal(4, Formula.ApplyMult(3, 1.5)); // 3×1.5=4.5 → 4 (GDD 예시)
        Assert.Equal(2, Formula.ApplyMult(5, 0.5)); // 5×0.5=2.5 → 2 (GDD 예시)
        Assert.Equal(1, Formula.ApplyMult(1, 0.5)); // 최소 1
        Assert.Equal(1, Formula.ApplyMult(0, 4));   // 최소 1 (X04 0코 증정)
    }

    [Fact(DisplayName = "mulberry32: 같은 시드 = 같은 수열 (결정적 리플레이 전제)")]
    public void Mulberry32IsDeterministic()
    {
        var a = RngFactory.Mulberry32(42);
        var b = RngFactory.Mulberry32(42);
        for (int i = 0; i < 100; i++) Assert.Equal(a(), b());
        var c = RngFactory.Mulberry32(43);
        Assert.NotEqual(RngFactory.Mulberry32(42)(), c());
    }
}
