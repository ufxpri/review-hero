// 합성 파형 검증 — 헤드리스에는 오디오 장치가 없다. 그래서 「소리가 났다」를 귀로 못 본다.
//
//   Godot --headless --path game -- --sfxdump[=경로]
//
// ① 모든 <see cref="SfxId"/> 의 **AudioStreamWav 가 실제로 들고 있는 바이트**를 WAV 파일로 떨군다
//    (합성 결과를 따로 계산해 쓰는 게 아니라 스트림의 Data 를 그대로 쓴다 — 굳힌 것을 검증한다)
// ② 길이·피크·RMS·클리핑·무음·선두 무음을 수치로 찍는다
// ③ 그다음 **한 프레임에 하나씩 실제로 Play 를 태워** 장치 없는 환경에서 예외가 없는지 본다
//
// 기본 출력 경로는 user://sfx (macOS: ~/Library/Application Support/Godot/app_userdata/…/sfx).

using Godot;
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Audio;

public static class SfxDump
{
    /// <summary>덤프 + 재생 점검을 시작한다. 끝나면 스스로 종료한다(종료 코드는 검사 결과)</summary>
    public static void Start(SceneTree tree, string outDir)
    {
        var probe = new SfxProbe { OutDir = outDir };
        tree.Root.CallDeferred(Node.MethodName.AddChild, probe);
    }

    /// <summary>파형 한 개의 계측치</summary>
    public readonly record struct Stat(
        SfxId Id, int Samples, double Ms, double Peak, double Rms, int Clipped, double LeadMs, bool Silent);

    public static Stat Measure(SfxId id, byte[] pcm)
    {
        int n = pcm.Length / 2;
        double peak = 0, sumSq = 0;
        int clipped = 0, lead = -1;
        for (int i = 0; i < n; i++)
        {
            short s = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
            double v = s / 32768.0;
            double a = Math.Abs(v);
            if (a > peak) peak = a;
            sumSq += v * v;
            if (s >= 32767 || s <= -32768) clipped++;
            if (lead < 0 && a > 0.002) lead = i;
        }
        double rms = n > 0 ? Math.Sqrt(sumSq / n) : 0;
        return new Stat(id, n, n * 1000.0 / Synth.Rate, peak, rms, clipped,
            (lead < 0 ? n : lead) * 1000.0 / Synth.Rate, peak < 1e-4);
    }

    /// <summary>16bit 모노 PCM 을 RIFF WAV 로</summary>
    public static byte[] Wav(byte[] pcm, int rate)
    {
        var w = new System.IO.MemoryStream();
        var b = new System.IO.BinaryWriter(w);
        b.Write("RIFF".ToCharArray());
        b.Write(36 + pcm.Length);
        b.Write("WAVE".ToCharArray());
        b.Write("fmt ".ToCharArray());
        b.Write(16);
        b.Write((short)1);          // PCM
        b.Write((short)1);          // mono
        b.Write(rate);
        b.Write(rate * 2);          // byte rate
        b.Write((short)2);          // block align
        b.Write((short)16);         // bits
        b.Write("data".ToCharArray());
        b.Write(pcm.Length);
        b.Write(pcm);
        b.Flush();
        return w.ToArray();
    }
}

/// <summary>덤프하고, 프레임마다 한 소리씩 실제로 재생해 보고, 끝나면 종료하는 일회용 노드</summary>
public partial class SfxProbe : Node
{
    public string OutDir = "user://sfx";

    private readonly List<SfxId> _queue = new();
    private int _i;
    private int _tail = 12;
    private bool _ok = true;

    public override void _Ready()
    {
        string dir = OutDir.StartsWith("user://", StringComparison.Ordinal)
            || OutDir.StartsWith("res://", StringComparison.Ordinal)
            ? Platform.GlobalizePath(OutDir)
            : OutDir;

        try { Directory.CreateDirectory(dir); }
        catch (Exception e) { GD.PrintErr($"[sfxdump] 출력 폴더 실패: {e.Message}"); _ok = false; }

        GD.Print($"[sfxdump] 출력 {dir}");
        GD.Print("[sfxdump] id | samples | ms | peak | rms | clip | lead(ms)");

        foreach (SfxId id in Enum.GetValues<SfxId>())
        {
            _queue.Add(id);
            try
            {
                var stream = SfxBank.Stream(id);
                var pcm = stream.Data;
                var st = SfxDump.Measure(id, pcm);
                File.WriteAllBytes(Path.Combine(dir, $"{id}.wav"), SfxDump.Wav(pcm, (int)stream.MixRate));
                GD.Print($"[sfxdump] {id} | {st.Samples} | {st.Ms:F1} | {st.Peak:F3} | {st.Rms:F4} | "
                       + $"{st.Clipped} | {st.LeadMs:F1}");

                if (st.Silent) { GD.PrintErr($"[sfxdump] ✗ {id} 가 무음이다"); _ok = false; }
                if (st.Clipped > 0) { GD.PrintErr($"[sfxdump] ✗ {id} 클리핑 {st.Clipped}표본"); _ok = false; }
                if (st.Peak > 0.95) { GD.PrintErr($"[sfxdump] ✗ {id} 피크가 너무 높다 {st.Peak:F3}"); _ok = false; }
                if (st.LeadMs > 30) { GD.PrintErr($"[sfxdump] ✗ {id} 선두 무음 {st.LeadMs:F1}ms"); _ok = false; }
                if (stream.MixRate != Synth.Rate || stream.Stereo)
                {
                    GD.PrintErr($"[sfxdump] ✗ {id} 포맷이 다르다 ({stream.MixRate}Hz stereo={stream.Stereo})");
                    _ok = false;
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[sfxdump] ✗ {id} 합성/기록 실패: {e}");
                _ok = false;
            }
        }

        GD.Print("[sfxdump] — 재생 점검 (오디오 장치 없이 Sfx.Play 가 예외 없이 지나가는지) —");
    }

    public override void _Process(double delta)
    {
        // 프레임마다 하나씩 — 실제 재생 경로(풀 배정 → AudioStreamPlayer.Play)를 그대로 태운다
        if (_i < _queue.Count)
        {
            var id = _queue[_i++];
            try { Sfx.Play(id); }
            catch (Exception e) { GD.PrintErr($"[sfxdump] ✗ Play({id}) 예외: {e}"); _ok = false; }
            return;
        }

        if (_i == _queue.Count)
        {
            _i++;
            try
            {
                Sfx.Stroke(0.18); Sfx.Stroke(0.40); Sfx.Stroke(0.9);   // 획 길이 3종
                for (int likes = 0; likes <= 14; likes += 7) Sfx.Like(likes);
                Sfx.Stamp(ReviewHero.Engine.Judgement.Origin, false);
                Sfx.Stamp(null, true);
            }
            catch (Exception e) { GD.PrintErr($"[sfxdump] ✗ 헬퍼 예외: {e}"); _ok = false; }
            return;
        }

        if (--_tail > 0) return;
        GD.Print(_ok ? "[sfxdump] ✅ 전부 통과" : "[sfxdump] ✗ 실패한 항목이 있다");
        GetTree().Quit(_ok ? 0 : 1);
    }
}
