// 시드 주입 PRNG — Date.now()/Math.random() 금지 (CLAUDE.md, GDD §8-3 리플레이 검증 재사용 전제)
//
// TS(packages/core/src/rng.ts) 이관. **TS 와 비트 단위로 같은 수열을 내야 한다** —
// 시뮬 대조가 여기에 걸려 있다. TS 의 `>>> 0` · `Math.imul` · `| 0` 은 전부 32비트 연산이므로
// C# 에서는 상태를 `uint` 로 들고 `unchecked` 로 감싸 재현한다:
//   `a | 0` → uint 그대로(값 변화 없음) / `a >>> n` → uint 의 `>>` /
//   `Math.imul(x, y)` → uint 곱셈(하위 32비트만 남으므로 int32 곱셈과 같은 비트열) /
//   `x >>> 0` → uint 그대로.

namespace ReviewHero.Engine;

/// <summary>0 이상 1 미만의 난수를 하나 돌려준다 (TS `type Rng = () =&gt; number`).</summary>
public delegate double Rng();

/// <summary>32비트 결정적 난수 — 인스턴스가 아니라 클로저로 상태를 들고 있다(TS 와 동형).</summary>
public static class RngFactory
{
    /// <summary>mulberry32 — 32비트 시드 결정적 PRNG</summary>
    public static Rng Mulberry32(uint seed)
    {
        uint a = seed;
        return () =>
        {
            unchecked
            {
                a += 0x6d2b79f5u;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        };
    }

    /// <summary>Fisher–Yates 셔플 (제자리, rng 주입)</summary>
    public static IList<T> Shuffle<T>(IList<T> arr, Rng rng)
    {
        for (int i = arr.Count - 1; i > 0; i--)
        {
            int j = (int)Math.Floor(rng() * (i + 1));
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return arr;
    }
}
