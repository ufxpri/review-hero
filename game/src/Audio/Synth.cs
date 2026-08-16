// 파형 합성 — 이 게임의 소리는 전부 여기서 계산해서 만든다. 음원 파일을 저장소에 두지 않는다.
//
// ── 왜 합성인가 ────────────────────────────────────────
// 이 게임의 소리는 도장·잉크·종이·펜·테이프다. 전부 「짧은 노이즈 + 필터 + 포락선」이거나
// 「감쇠하는 저역 사인」으로 만들어지는 것들이라, 음원을 사 오는 것보다 계산이 정확하고 짧다.
// 라이선스도 바이너리 자산 관리도 따라오지 않는다.
//
// ── 결정성 ────────────────────────────────────────────
// 노이즈는 **시드 고정 xorshift**로 만든다 (<see cref="SfxRng"/>). System.Random 도
// GD.Randi 도 쓰지 않는다 — 같은 빌드는 언제나 같은 바이트를 내야 캡처·검증이 재현된다.
// (게임 규칙 난수와는 무관한 표현 계층이지만, 검증 가능성 때문에 같은 원칙을 지킨다.)
//
// ── 진폭 규약 ──────────────────────────────────────────
// 각 소리는 <see cref="Normalize"/> 로 자기 목표 피크에 맞춰 끝낸다. 목표 피크는 1.0 이 아니라
// 소리마다 다르다(0.18~0.62) — 그것이 「도장은 크고 버튼은 아주 작다」는 믹스의 정본이다.
// 16비트로 굳히기 전에 ±1.0 을 넘는 표본이 없으므로 클리핑은 구조적으로 0이다.

using Godot;

namespace ReviewHero.Game.Audio;

/// <summary>시드 고정 xorshift32 — 같은 시드면 언제나 같은 노이즈</summary>
internal sealed class SfxRng
{
    private uint _s;

    public SfxRng(uint seed) => _s = seed == 0 ? 0x9E3779B9u : seed;

    public uint NextU()
    {
        unchecked
        {
            _s ^= _s << 13;
            _s ^= _s >> 17;
            _s ^= _s << 5;
            return _s;
        }
    }

    /// <summary>[0,1)</summary>
    public float Next01() => (NextU() >> 8) * (1f / 16777216f);

    /// <summary>[-1,1)</summary>
    public float Bi() => Next01() * 2f - 1f;
}

internal static class Synth
{
    /// <summary>표본률. 22050 이면 종이·도장의 고역(2~6kHz)이 아슬아슬해서 44100 을 쓴다</summary>
    public const int Rate = 44100;

    public static float[] Buf(double seconds) => new float[Math.Max(1, (int)(seconds * Rate))];

    public static int Samples(double seconds) => Math.Max(1, (int)(seconds * Rate));

    // ── 소스 ─────────────────────────────────────────

    /// <summary>백색 잡음을 얹는다 (종이·도장의 재료)</summary>
    public static void Noise(float[] b, SfxRng r, double amp = 1.0, int start = 0, int len = -1)
    {
        int end = len < 0 ? b.Length : Math.Min(b.Length, start + len);
        for (int i = Math.Max(0, start); i < end; i++) b[i] += (float)(r.Bi() * amp);
    }

    /// <summary>
    /// 감쇠하는 사인 — 도장의 「울림」, 피격의 「퉁」. f0→f1 로 지수 글라이드하며
    /// tau 초의 지수 감쇠를 탄다. 2ms 어택 램프를 넣어 시작 클릭을 없앤다.
    /// </summary>
    public static void Tone(float[] b, double f0, double f1, double amp, double tau,
                            int start = 0, int len = -1, double harm2 = 0, double phase0 = 0)
    {
        int end = len < 0 ? b.Length : Math.Min(b.Length, start + len);
        int n = Math.Max(1, end - start);
        double phase = phase0;
        int atk = Samples(0.002);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / n;
            double f = f0 * Math.Pow(f1 / f0, t);
            phase += 2 * Math.PI * f / Rate;
            double env = Math.Exp(-(i / (double)Rate) / tau);
            if (i < atk) env *= (double)i / atk;
            double v = Math.Sin(phase);
            if (harm2 != 0) v += harm2 * Math.Sin(2 * phase);
            b[start + i] += (float)(v * env * amp);
        }
    }

    /// <summary>삼각파 톤 — 사인보다 조금 더 「값싼 전자음」. 김빠지는 소리에 쓴다</summary>
    public static void Tri(float[] b, double f0, double f1, double amp, double tau, int start = 0, int len = -1)
    {
        int end = len < 0 ? b.Length : Math.Min(b.Length, start + len);
        int n = Math.Max(1, end - start);
        double phase = 0;
        int atk = Samples(0.003);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / n;
            double f = f0 * Math.Pow(f1 / f0, t);
            phase += f / Rate;
            phase -= Math.Floor(phase);
            double v = 4.0 * Math.Abs(phase - 0.5) - 1.0;
            double env = Math.Exp(-(i / (double)Rate) / tau);
            if (i < atk) env *= (double)i / atk;
            b[start + i] += (float)(v * env * amp);
        }
    }

    // ── 필터 (Chamberlin 상태변수 필터) ────────────────

    /// <summary>대역통과. fc 는 Hz, q 는 1 이상일수록 좁다. 종이·도장의 「재질」이 여기서 정해진다</summary>
    public static void Band(float[] b, double fc, double q) => Svf(b, fc, fc, q, Mode.Band);

    /// <summary>중심 주파수를 fc0→fc1 로 쓸어 내리는 대역통과 — 「휙」 소리의 정체</summary>
    public static void BandSweep(float[] b, double fc0, double fc1, double q) => Svf(b, fc0, fc1, q, Mode.Band);

    public static void Low(float[] b, double fc, double q = 0.7) => Svf(b, fc, fc, q, Mode.Low);

    public static void High(float[] b, double fc, double q = 0.7) => Svf(b, fc, fc, q, Mode.High);

    private enum Mode { Low, Band, High }

    private static void Svf(float[] b, double fc0, double fc1, double q, Mode mode)
    {
        double low = 0, band = 0;
        int n = b.Length;
        for (int i = 0; i < n; i++)
        {
            double t = n <= 1 ? 0 : (double)i / (n - 1);
            double fc = fc0 * Math.Pow(fc1 / fc0, t);
            // f 는 1.0 을 넘기면 발산한다 — Rate/6 근처에서 잘라 둔다
            double f = Math.Clamp(2.0 * Math.Sin(Math.PI * Math.Min(fc, Rate * 0.24) / Rate), 0.0, 1.0);
            double q1 = Math.Clamp(1.0 / Math.Max(0.35, q), 0.02, 2.0 - f);
            double x = b[i];
            double high = x - low - q1 * band;
            band += f * high;
            low += f * band;
            b[i] = (float)(mode switch { Mode.Low => low, Mode.High => high, _ => band });
        }
    }

    // ── 포락선 ───────────────────────────────────────

    /// <summary>어택-유지-감쇠. curve 가 클수록 뚝 떨어진다(도장), 작을수록 늘어진다(펜)</summary>
    public static void Shape(float[] b, double attack, double hold, double decay, double curve = 2.6,
                             int start = 0, int len = -1)
    {
        int end = len < 0 ? b.Length : Math.Min(b.Length, start + len);
        int a = Samples(attack), h = Samples(hold), d = Samples(decay);
        for (int i = start; i < end; i++)
        {
            int k = i - start;
            double e;
            if (k < a) e = a <= 1 ? 1.0 : (double)k / a;
            else if (k < a + h) e = 1.0;
            else
            {
                double t = (double)(k - a - h) / Math.Max(1, d);
                e = t >= 1.0 ? 0.0 : Math.Pow(1.0 - t, curve);
            }
            b[i] *= (float)e;
        }
    }

    /// <summary>거친 알갱이 — 종이 섬유·테이프 접착제가 뜯기는 「사각사각」의 정체</summary>
    public static void Grain(float[] b, SfxRng r, double rateHz, double depth)
    {
        int period = Math.Max(1, (int)(Rate / Math.Max(1.0, rateHz)));
        float g = 1f;
        for (int i = 0; i < b.Length; i++)
        {
            if (i % period == 0) g = (float)(1.0 - depth * r.Next01());
            b[i] *= g;
        }
    }

    /// <summary>끝을 부드럽게 눕힌다 — 잘린 파형의 「똑」 소리를 없앤다</summary>
    public static void FadeOut(float[] b, double seconds = 0.006)
    {
        int n = Math.Min(b.Length, Samples(seconds));
        for (int i = 0; i < n; i++) b[b.Length - 1 - i] *= (float)i / n;
    }

    public static void MixIn(float[] dst, float[] src, double gain = 1.0, int offset = 0)
    {
        for (int i = 0; i < src.Length && offset + i < dst.Length; i++) dst[offset + i] += (float)(src[i] * gain);
    }

    /// <summary>목표 피크로 맞춘다 = 이 소리의 최종 크기. 무음이면 손대지 않는다</summary>
    public static void Normalize(float[] b, double peak)
    {
        float max = 0f;
        foreach (var v in b) max = Math.Max(max, Math.Abs(v));
        if (max <= 1e-6f) return;
        float k = (float)(peak / max);
        for (int i = 0; i < b.Length; i++) b[i] *= k;
    }

    // ── 굳히기 ───────────────────────────────────────

    /// <summary>float[-1,1] → 16bit PCM 리틀엔디언 (WAV 덤프와 AudioStreamWav 가 같은 바이트를 본다)</summary>
    public static byte[] ToPcm16(float[] b)
    {
        var bytes = new byte[b.Length * 2];
        for (int i = 0; i < b.Length; i++)
        {
            int v = (int)Math.Round(Math.Clamp(b[i], -1f, 1f) * 32767f);
            bytes[i * 2] = (byte)(v & 0xFF);
            bytes[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return bytes;
    }

    public static AudioStreamWav ToStream(float[] b) => new()
    {
        Data = ToPcm16(b),
        Format = AudioStreamWav.FormatEnum.Format16Bits,
        MixRate = Rate,
        Stereo = false,
        LoopMode = AudioStreamWav.LoopModeEnum.Disabled,
    };
}
