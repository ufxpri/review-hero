// 소리의 정본 — 각 <see cref="SfxId"/> 가 어떤 파형인지 여기 한 곳에 적혀 있다.
//
// 읽는 법: 소리 하나는 「재료(노이즈/사인) → 필터 → 포락선」의 조합이고, 여러 재료를 겹칠 때는
// 각각 따로 만들어 <see cref="Synth.MixIn"/> 으로 얹는다(필터가 옆 재료까지 먹으면 안 되므로).
// 마지막 <see cref="Synth.Normalize"/> 의 목표 피크가 이 소리의 최종 크기다 = 믹스 밸런스.
//
// 시드는 소리마다 **손으로 박은 상수**다. 같은 빌드는 언제나 같은 바이트를 낸다.

using Godot;

namespace ReviewHero.Game.Audio;

internal static class SfxBank
{
    private static readonly Dictionary<SfxId, AudioStreamWav> Cache = new();

    /// <summary>스트림 하나 (처음 요청할 때 합성하고 그 뒤로는 캐시)</summary>
    public static AudioStreamWav Stream(SfxId id)
    {
        if (Cache.TryGetValue(id, out var s)) return s;
        var made = Synth.ToStream(Render(id));
        Cache[id] = made;
        return made;
    }

    /// <summary>합성한 원본 파형 (검증 덤프가 이걸 그대로 WAV 로 쓴다)</summary>
    public static float[] Render(SfxId id) => id switch
    {
        SfxId.CardPick => Paper(0.075, 2900, 1.1, 900, 0.70, 0x51A1, 0.22, atk: 0.002, dec: 0.066),
        SfxId.CardDrop => Paper(0.095, 1700, 1.0, 700, 0.60, 0x51A2, 0.20, atk: 0.004, dec: 0.082),

        SfxId.SignatureShort => Signature(0.25, 0x9E11),
        SfxId.Signature => Signature(0.40, 0x9E12),
        SfxId.SignatureLong => Signature(0.62, 0x9E13),

        SfxId.CardThrow => Throw(),

        //                      길이   딸깍  타격f0→f1   감쇠   몸통Hz  울림Hz 울림감쇠 피크
        SfxId.StampOrigin => Stamp(0.42, 0.50, 110, 70, 0.100, 420, 72, 0.30, 0.62, 0x5701),
        SfxId.StampFact => Stamp(0.20, 0.95, 230, 170, 0.045, 1100, 0, 0.00, 0.52, 0x5702),
        SfxId.StampNormal => Stamp(0.13, 0.30, 165, 130, 0.035, 600, 0, 0.00, 0.30, 0x5703),
        SfxId.StampFumble => Fumble(),

        SfxId.Like => Like(),
        SfxId.Crit => Crit(),

        SfxId.Hurt => Hurt(),
        SfxId.Block => Block(),

        SfxId.Win => Win(),
        SfxId.Lose => Lose(),

        SfxId.Click => Click(),
        SfxId.Toast => Toast(),

        SfxId.Parcel => Parcel(),
        SfxId.Crumple => Crumple(),
        SfxId.Coin => Coin(),

        _ => Synth.Buf(0.01),
    };

    // ══ 종이 ═════════════════════════════════════════════

    /// <summary>종이 스침 — 좁은 대역의 노이즈에 알갱이를 먹인다. 섬유가 스치는 것이 이 알갱이다</summary>
    private static float[] Paper(double sec, double fc, double q, double grainHz, double grainDepth,
                                 uint seed, double peak, double atk, double dec)
    {
        var b = Synth.Buf(sec);
        var r = new SfxRng(seed);
        Synth.Noise(b, r);
        Synth.Band(b, fc, q);
        Synth.Grain(b, r, grainHz, grainDepth);
        Synth.Shape(b, atk, Math.Max(0, sec - atk - dec), dec, curve: 2.0);
        Synth.FadeOut(b);
        Synth.Normalize(b, peak);
        return b;
    }

    /// <summary>
    /// 펜촉이 종이를 긁는다. 획이 길수록 길게 이어지므로 길이를 인자로 받는다
    /// (<see cref="Sfx.Stroke"/> 가 획 시간에 가장 가까운 길이를 고른다).
    /// 재료: 긁힘(2kHz대 좁은 노이즈 + 낮은 알갱이) + 종이를 누르는 저역 마찰.
    /// </summary>
    private static float[] Signature(double sec, uint seed)
    {
        var b = Synth.Buf(sec);
        var r = new SfxRng(seed);

        var scratch = Synth.Buf(sec);
        Synth.Noise(scratch, r);
        Synth.BandSweep(scratch, 1500, 2500, 2.2);   // 획이 진행되며 조금 밝아진다
        Synth.Grain(scratch, r, 68, 0.60);           // 펜촉이 종이 결에 걸리는 사각거림
        Synth.Grain(scratch, r, 240, 0.30);
        Synth.MixIn(b, scratch, 1.0f);

        var drag = Synth.Buf(sec);
        Synth.Noise(drag, r);
        Synth.Low(drag, 300);                        // 종이를 누르며 끄는 저역
        Synth.MixIn(b, drag, 0.55f);

        double atk = 0.018, dec = 0.075;
        Synth.Shape(b, atk, Math.Max(0, sec - atk - dec), dec, curve: 1.4);
        Synth.FadeOut(b, 0.01);
        Synth.Normalize(b, 0.26f);
        return b;
    }

    /// <summary>종이가 날아간다 — 대역을 쓸어 올리는 노이즈 하나면 「휙」이 된다</summary>
    private static float[] Throw()
    {
        var b = Synth.Buf(0.22);
        var r = new SfxRng(0x7A05);
        Synth.Noise(b, r);
        Synth.BandSweep(b, 600, 2800, 1.2);
        Synth.Shape(b, 0.060, 0.020, 0.140, curve: 1.6);
        Synth.FadeOut(b);
        Synth.Normalize(b, 0.16f);   // 아주 작게 — 제출마다 나는 소리다
        return b;
    }

    /// <summary>종이를 구긴다 — 성긴 알갱이(찌그러지는 결)가 이 소리의 전부다</summary>
    private static float[] Crumple()
    {
        var b = Synth.Buf(0.34);
        var r = new SfxRng(0xC201);
        Synth.Noise(b, r);
        Synth.Band(b, 2600, 1.4);
        Synth.Grain(b, r, 58, 0.95);
        Synth.Grain(b, r, 380, 0.55);
        Synth.Shape(b, 0.010, 0.100, 0.230, curve: 1.5);
        Synth.FadeOut(b);
        Synth.Normalize(b, 0.26f);
        return b;
    }

    // ══ 도장 ═════════════════════════════════════════════

    /// <summary>
    /// 판정 도장. 세 겹이다 — ① 고무가 종이에 닿는 딸깍(고역 순간음)
    /// ② 책상을 때리는 몸통(중역 노이즈) ③ 눌러 찍히는 타격(저역 사인).
    /// 원산지만 ④ 낮은 울림(ringHz)을 더 얹어 「묵직하게 남는」 소리가 된다.
    /// </summary>
    private static float[] Stamp(double sec, double clickAmp, double f0, double f1, double tau,
                                 double bodyHz, double ringHz, double ringTau, double peak, uint seed)
    {
        var b = Synth.Buf(sec);
        var r = new SfxRng(seed);

        var click = Synth.Buf(0.006);
        Synth.Noise(click, r);
        Synth.High(click, 3000);
        Synth.Shape(click, 0.0004, 0.0006, 0.005, curve: 3.0);
        Synth.MixIn(b, click, clickAmp);

        var body = Synth.Buf(0.055);
        Synth.Noise(body, r);
        Synth.Band(body, bodyHz, 1.3);
        Synth.Shape(body, 0.001, 0.004, 0.050, curve: 2.4);
        Synth.MixIn(b, body, 0.75f);

        Synth.Tone(b, f0, f1, 1.0f, tau, harm2: 0.22f);
        if (ringHz > 0) Synth.Tone(b, ringHz, ringHz * 0.92, 0.55f, ringTau);

        Synth.FadeOut(b, 0.010);
        Synth.Normalize(b, peak);
        return b;
    }

    /// <summary>헛소리·빗나감 — 도장이 안 찍히고 바람이 샌다. 값싸게 하강하는 톤 + 새는 공기</summary>
    private static float[] Fumble()
    {
        var b = Synth.Buf(0.36);
        var r = new SfxRng(0x5704);

        var tap = Synth.Buf(0.02);
        Synth.Noise(tap, r);
        Synth.Band(tap, 900, 1.0);
        Synth.Shape(tap, 0.001, 0.002, 0.017, curve: 2.5);
        Synth.MixIn(b, tap, 0.35f);

        Synth.Tri(b, 300, 95, 0.5f, 0.16, start: Synth.Samples(0.015));

        var hiss = Synth.Buf(0.30);
        Synth.Noise(hiss, r);
        Synth.BandSweep(hiss, 1500, 700, 0.9);
        Synth.Shape(hiss, 0.012, 0.030, 0.258, curve: 1.2);
        Synth.MixIn(b, hiss, 0.32f, Synth.Samples(0.02));

        Synth.FadeOut(b);
        Synth.Normalize(b, 0.34f);
        return b;
    }

    // ══ 좋아요·베스트 리뷰 ═══════════════════════════════

    /// <summary>좋아요 적중 — 아주 짧은 클릭. 피치는 재생 시점에 좋아요 수로 올린다</summary>
    private static float[] Like()
    {
        var b = Synth.Buf(0.045);
        var r = new SfxRng(0x11CE);

        var tick = Synth.Buf(0.0015);
        Synth.Noise(tick, r);
        Synth.High(tick, 4000);
        Synth.MixIn(b, tick, 0.8f);

        Synth.Tone(b, 950, 860, 0.8f, 0.012);
        Synth.Shape(b, 0.0005, 0.002, 0.042, curve: 3.0);
        Synth.FadeOut(b, 0.004);
        Synth.Normalize(b, 0.30f);
        return b;
    }

    /// <summary>베스트 리뷰 — 도장 한 방 뒤에 종소리가 세 계단 올라간다 (도장 + 상승음)</summary>
    private static float[] Crit()
    {
        var b = Synth.Buf(0.85);
        Synth.MixIn(b, Render(SfxId.StampOrigin), 0.85f);

        double[] notes = { 523.25, 659.25, 880.0 };
        double[] at = { 0.10, 0.20, 0.31 };
        double[] amp = { 0.60, 0.55, 0.50 };
        for (int i = 0; i < notes.Length; i++)
        {
            Synth.Tone(b, notes[i], notes[i] * 1.004, amp[i], 0.34,
                start: Synth.Samples(at[i]), harm2: 0.18f);
        }

        var shimmer = Synth.Buf(0.30);
        var r = new SfxRng(0xC217);
        Synth.Noise(shimmer, r);
        Synth.Band(shimmer, 5200, 1.6);
        Synth.Shape(shimmer, 0.004, 0.02, 0.276, curve: 2.0);
        Synth.MixIn(b, shimmer, 0.12f, Synth.Samples(0.09));

        Synth.FadeOut(b, 0.02);
        Synth.Normalize(b, 0.60f);
        return b;
    }

    // ══ 피격·방어 ════════════════════════════════════════

    /// <summary>둔탁한 저역 타격 — 좋아요가 내 의지를 때린다</summary>
    private static float[] Hurt()
    {
        var b = Synth.Buf(0.30);
        var r = new SfxRng(0x4017);

        var body = Synth.Buf(0.075);
        Synth.Noise(body, r);
        Synth.Low(body, 320);
        Synth.Shape(body, 0.001, 0.006, 0.068, curve: 2.2);
        Synth.MixIn(b, body, 0.60f);

        // 130→68Hz. 더 낮추면 「둔탁」이 아니라 노트북 스피커에서 안 들리는 소리가 된다
        Synth.Tone(b, 130, 68, 1.0f, 0.110, harm2: 0.20f);
        Synth.FadeOut(b, 0.012);
        Synth.Normalize(b, 0.55f);
        return b;
    }

    /// <summary>막았다 — 피격과 같은 계열이되 짧고 높다(그래서 「안 아프다」가 즉시 읽힌다)</summary>
    private static float[] Block()
    {
        var b = Synth.Buf(0.13);
        var r = new SfxRng(0x4018);

        var tick = Synth.Buf(0.004);
        Synth.Noise(tick, r);
        Synth.High(tick, 3000);
        Synth.MixIn(b, tick, 0.45f);

        var body = Synth.Buf(0.05);
        Synth.Noise(body, r);
        Synth.Band(body, 950, 1.8);
        Synth.Shape(body, 0.001, 0.004, 0.045, curve: 2.6);
        Synth.MixIn(b, body, 0.7f);

        Synth.Tone(b, 330, 280, 0.7f, 0.030);
        Synth.FadeOut(b, 0.008);
        Synth.Normalize(b, 0.40f);
        return b;
    }

    // ══ 전투 종료 ════════════════════════════════════════

    /// <summary>승리 — 도장 세 방이 계단으로 올라가고 마지막에 종이 울린다 (승인 도장의 정서)</summary>
    private static float[] Win()
    {
        var b = Synth.Buf(0.80);
        var r = new SfxRng(0x9001);
        double[] f = { 180, 230, 300 };
        for (int i = 0; i < 3; i++)
        {
            int at = Synth.Samples(0.11 * i);
            var click = Synth.Buf(0.005);
            Synth.Noise(click, r);
            Synth.High(click, 3000);
            Synth.MixIn(b, click, 0.5f, at);
            Synth.Tone(b, f[i], f[i] * 0.75, 0.7f, 0.055, start: at, harm2: 0.2f);
        }
        Synth.Tone(b, 523.25, 523.25, 0.55f, 0.30, start: Synth.Samples(0.26), harm2: 0.2f);
        Synth.Tone(b, 783.99, 783.99, 0.40f, 0.28, start: Synth.Samples(0.26), harm2: 0.15f);
        Synth.FadeOut(b, 0.02);
        Synth.Normalize(b, 0.50f);
        return b;
    }

    /// <summary>패배 — 두 계단 내려가고 종이 한 장이 구겨진다</summary>
    private static float[] Lose()
    {
        var b = Synth.Buf(0.85);
        Synth.Tone(b, 262, 196, 0.60f, 0.35, harm2: 0.15f);
        Synth.Tone(b, 196, 130, 0.55f, 0.40, start: Synth.Samples(0.22), harm2: 0.15f);
        Synth.Tone(b, 70, 58, 0.45f, 0.22, start: Synth.Samples(0.45));
        Synth.MixIn(b, Render(SfxId.Crumple), 0.45f, Synth.Samples(0.02));
        Synth.FadeOut(b, 0.03);
        Synth.Normalize(b, 0.45f);
        return b;
    }

    // ══ 잡음 ═════════════════════════════════════════════

    /// <summary>버튼 — 「아주 작은 클릭」. 여기가 커지면 읽는 게임이 시끄러워진다</summary>
    private static float[] Click()
    {
        var b = Synth.Buf(0.022);
        var r = new SfxRng(0x0C11);
        var tick = Synth.Buf(0.0012);
        Synth.Noise(tick, r);
        Synth.High(tick, 3000);
        Synth.MixIn(b, tick, 1.0f);
        Synth.Tone(b, 720, 620, 0.5f, 0.008);
        Synth.Shape(b, 0.0004, 0.001, 0.020, curve: 3.0);
        Synth.FadeOut(b, 0.003);
        Synth.Normalize(b, 0.14f);
        return b;
    }

    /// <summary>토스트 — 안내가 떴다는 신호. 버튼보다도 작다</summary>
    private static float[] Toast()
    {
        var b = Synth.Buf(0.05);
        var r = new SfxRng(0x0C12);
        var tick = Synth.Buf(0.001);
        Synth.Noise(tick, r);
        Synth.High(tick, 5000);
        Synth.MixIn(b, tick, 0.5f);
        Synth.Tone(b, 1250, 1180, 0.6f, 0.018);
        Synth.Shape(b, 0.001, 0.004, 0.045, curve: 2.6);
        Synth.FadeOut(b, 0.005);
        Synth.Normalize(b, 0.11f);
        return b;
    }

    /// <summary>택배 개봉 — 테이프가 뜯긴다. 촘촘한 알갱이가 밀도를 올리다가 끝에 툭 끊긴다</summary>
    private static float[] Parcel()
    {
        var b = Synth.Buf(0.46);
        var r = new SfxRng(0x7A1E);

        var rip = Synth.Buf(0.44);
        Synth.Noise(rip, r);
        Synth.BandSweep(rip, 1800, 3200, 1.0);
        Synth.Grain(rip, r, 150, 0.85);
        Synth.Grain(rip, r, 40, 0.40);
        Synth.Shape(rip, 0.050, 0.200, 0.190, curve: 1.3);
        Synth.MixIn(b, rip, 1.0f);

        var box = Synth.Buf(0.40);
        Synth.Noise(box, r);
        Synth.Low(box, 250);
        Synth.Shape(box, 0.040, 0.180, 0.180, curve: 1.4);
        Synth.MixIn(b, box, 0.30f);

        var snap = Synth.Buf(0.02);
        Synth.Noise(snap, r);
        Synth.High(snap, 2500);
        Synth.Shape(snap, 0.0006, 0.001, 0.018, curve: 3.0);
        Synth.MixIn(b, snap, 0.5f, Synth.Samples(0.42));

        Synth.FadeOut(b);
        Synth.Normalize(b, 0.34f);
        return b;
    }

    /// <summary>골드 — 동전 두 닢. 금속은 배음이 안 맞는 사인 몇 개면 된다</summary>
    private static float[] Coin()
    {
        var b = Synth.Buf(0.24);
        Synth.Tone(b, 2100, 2050, 0.60f, 0.050);
        Synth.Tone(b, 3500, 3420, 0.25f, 0.030);
        Synth.Tone(b, 2790, 2700, 0.45f, 0.070, start: Synth.Samples(0.03));
        Synth.Tone(b, 4180, 4050, 0.18f, 0.040, start: Synth.Samples(0.03));
        Synth.FadeOut(b, 0.015);
        Synth.Normalize(b, 0.28f);
        return b;
    }
}
