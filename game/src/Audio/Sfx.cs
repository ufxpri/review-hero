// 효과음 창구 — 화면 코드는 `Sfx.Play(SfxId.X)` 한 줄만 쓴다.
//
// ── 왜 오토로드가 아니라 정적 클래스인가 ────────────────────
// 오토로드로 만들면 project.godot 에 자리를 잡아야 하고, 그 파일은 지금 여러 화면 작업자가
// 동시에 건드리는 파일이다(씬 추가). 여기서는 **처음 소리가 필요해진 순간** 재생기 묶음을
// 씬 트리 루트에 스스로 붙인다 — 설정 파일을 안 건드리고, 씬을 갈아타도 루트에 남아 있으며,
// 소리를 한 번도 안 내는 실행(헤드리스 완주)에서는 노드가 아예 생기지 않는다.
//
// ── 헤드리스에서 죽지 않는다 ───────────────────────────
// 자동 완주는 씬 트리가 없거나(순수 하네스) 오디오 장치가 없는 채로 돈다. 그래서 이 파일의
// 공개 메서드는 **어떤 경우에도 예외를 밖으로 내보내지 않는다** — 소리는 흐름을 막지 않는다.
//
// ── 음소거·볼륨 ────────────────────────────────────────
// RunStore.Settings 에 **오디오 토글이 아직 없다**(TextSpeed/Shake/Debug 뿐). 서명 담당이
// RunState.cs 를 잡고 있어 필드를 새로 넣지 않았다 — 기본 on 으로 두되, 나중에 SettingsState 에
// bool Sound(또는 Audio/Sfx/Mute) 가 생기면 <see cref="SyncSettings"/> 가 자동으로 집어 간다.

using Godot;
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Audio;

public static class Sfx
{
    /// <summary>동시 재생 수 — 카드가 연달아 나가도 앞 소리가 잘리지 않을 만큼</summary>
    public const int Voices = 8;

    /// <summary>전체 음소거 스위치 (설정에 오디오 토글이 생기면 <see cref="SyncSettings"/> 가 여기에 꽂는다)</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>마스터 볼륨(dB). 텍스트를 읽는 게임이라 기본을 낮게 깐다</summary>
    public static float MasterDb { get; set; } = -7f;

    private static SfxBus? _bus;
    private static bool _settingsRead;

    // 같은 소리가 같은 프레임에 여러 번 겹치면 위상이 쌓여 갑자기 커진다 — 아주 짧게 막는다
    private static readonly Dictionary<SfxId, ulong> LastAt = new();
    private const ulong DedupeMs = 25;

    /// <summary>
    /// 소리 추적 — `RH_SFXTRACE=1` 이면 재생되는 소리를 로그로 찍는다.
    /// 헤드리스에는 오디오 장치가 없어 「그 자리에서 그 소리가 났는가」를 귀로 못 본다.
    /// </summary>
    private static readonly bool Trace =
        !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("RH_SFXTRACE"));

    /// <summary>소리 하나. 실패해도 조용히 지나간다 (오디오 장치·씬 트리 없음 포함)</summary>
    public static void Play(SfxId id, float volumeDb = 0f, float pitch = 1f)
    {
        try
        {
            if (Trace) GD.Print($"[sfx] {id} db={MasterDb + volumeDb:F1} pitch={pitch:F2}");
            if (!Ready()) return;
            ulong now = Time.GetTicksMsec();
            if (LastAt.TryGetValue(id, out var t) && now - t < DedupeMs) return;
            LastAt[id] = now;
            _bus!.Fire(SfxBank.Stream(id), MasterDb + volumeDb, Mathf.Clamp(pitch, 0.25f, 4f));
        }
        catch (Exception e)
        {
            GD.PushWarning($"[Sfx] 재생 실패({id}) — 무시하고 진행한다: {e.Message}");
        }
    }

    /// <summary>
    /// 서명 획 — 획이 길수록 긁는 소리도 길게 이어진다.
    /// 미리 구운 세 길이(0.25 · 0.40 · 0.62초) 중 가장 가까운 것을 고르고, 남은 차이는 피치로 맞춘다.
    /// </summary>
    public static void Stroke(double seconds)
    {
        (SfxId Id, double Sec)[] bank =
        {
            (SfxId.SignatureShort, 0.25),
            (SfxId.Signature, 0.40),
            (SfxId.SignatureLong, 0.62),
        };
        double want = Math.Clamp(seconds, 0.12, 1.2);
        var best = bank[0];
        foreach (var b in bank)
        {
            if (Math.Abs(Math.Log(b.Sec / want)) < Math.Abs(Math.Log(best.Sec / want))) best = b;
        }
        // 길이를 맞추려면 피치는 반대로 간다 (원본이 짧으면 느리게 재생 = 낮은 피치)
        Play(best.Id, pitch: (float)Math.Clamp(best.Sec / want, 0.72, 1.4));
    }

    /// <summary>좋아요 적중 — 좋아요 수가 클수록 피치가 살짝 오른다 (12를 넘으면 더 오르지 않는다)</summary>
    public static void Like(int likes)
    {
        if (likes <= 0) return;
        float k = Math.Min(likes, 12) / 12f;
        Play(SfxId.Like, pitch: 1f + 0.28f * k);
    }

    /// <summary>판정 도장 — 판정별로 다른 소리. 판정은 엔진이 준 것을 옮길 뿐이다</summary>
    public static void Stamp(ReviewHero.Engine.Judgement? judgement, bool missed)
    {
        if (missed) { Play(SfxId.StampFumble); return; }
        Play(judgement switch
        {
            ReviewHero.Engine.Judgement.Origin => SfxId.StampOrigin,
            ReviewHero.Engine.Judgement.Fact => SfxId.StampFact,
            ReviewHero.Engine.Judgement.Fumble => SfxId.StampFumble,
            _ => SfxId.StampNormal,
        });
    }

    /// <summary>미리 합성해 둔다 — 전투 첫 제출에서 합성 때문에 한 프레임 튀는 것을 막는다</summary>
    public static void Warm(params SfxId[] ids)
    {
        try
        {
            foreach (var id in ids) SfxBank.Stream(id);
        }
        catch (Exception e)
        {
            GD.PushWarning($"[Sfx] 예열 실패 — 무시하고 진행한다: {e.Message}");
        }
    }

    /// <summary>전투 진입처럼 「곧 여러 소리가 난다」는 자리에서 한 번 부른다</summary>
    public static void WarmCombat() => Warm(
        SfxId.CardPick, SfxId.CardDrop, SfxId.Signature, SfxId.CardThrow,
        SfxId.StampOrigin, SfxId.StampFact, SfxId.StampNormal, SfxId.StampFumble,
        SfxId.Like, SfxId.Hurt, SfxId.Block, SfxId.Click, SfxId.Toast);

    // ── 내부 ─────────────────────────────────────────

    private static bool Ready()
    {
        if (!_settingsRead) SyncSettings();
        if (!Enabled) return false;
        if (_bus is not null && GodotObject.IsInstanceValid(_bus)) return true;

        // 씬 트리가 없으면(순수 하네스) 소리는 없다 — 그것이 정상 동작이다
        if (Godot.Engine.GetMainLoop() is not SceneTree { Root: { } root }) return false;

        var bus = new SfxBus { Name = "SfxBus", ProcessMode = Node.ProcessModeEnum.Always };
        _bus = bus;
        // 씬의 _Ready 안에서 불릴 수 있다 — 그때 루트는 자식을 정리하는 중이라 즉시 붙이면 경고가 난다
        root.CallDeferred(Node.MethodName.AddChild, bus);
        return true;
    }

    /// <summary>
    /// 설정 반영. 지금 <see cref="SettingsState"/> 에는 오디오 항목이 없어 기본 on 이다.
    /// 필드가 생기면(Sound/Audio/Sfx/Mute) 이 함수가 알아서 집어 간다 — 이름만 맞추면 된다.
    /// </summary>
    public static void SyncSettings()
    {
        _settingsRead = true;
        try
        {
            var s = RunStore.Settings;
            var t = s.GetType();
            foreach (var name in new[] { "Sound", "Audio", "Sfx" })
            {
                if (t.GetProperty(name)?.GetValue(s) is bool on) { Enabled = on; return; }
            }
            if (t.GetProperty("Mute")?.GetValue(s) is bool mute) { Enabled = !mute; return; }
            if (t.GetProperty("Volume")?.GetValue(s) is double vol)
            {
                Enabled = vol > 0.001;
                MasterDb = Enabled ? (float)(20.0 * Math.Log10(Math.Clamp(vol, 0.001, 1.0))) - 7f : -80f;
            }
        }
        catch (Exception e)
        {
            GD.PushWarning($"[Sfx] 설정을 못 읽었다 — 기본값으로 간다: {e.Message}");
        }
    }
}

/// <summary>재생기 묶음 — 트리 루트에 하나만 산다. 씬을 갈아타도 살아남는다</summary>
public partial class SfxBus : Node
{
    private readonly List<AudioStreamPlayer> _voices = new();
    private readonly List<(AudioStream Stream, float Db, float Pitch)> _pending = new();
    private int _next;
    private bool _live;

    public override void _Ready()
    {
        for (int i = 0; i < Sfx.Voices; i++)
        {
            var p = new AudioStreamPlayer { Name = $"v{i}", ProcessMode = ProcessModeEnum.Always };
            AddChild(p);
            _voices.Add(p);
        }
        _live = true;
        foreach (var q in _pending) Fire(q.Stream, q.Db, q.Pitch);
        _pending.Clear();
    }

    /// <summary>비어 있는 재생기를 쓰고, 전부 물려 있으면 가장 오래된 것을 뺏는다(round-robin)</summary>
    public void Fire(AudioStream stream, float db, float pitch)
    {
        if (!_live)
        {
            // 아직 트리에 붙기 전(같은 프레임의 첫 소리) — 붙자마자 낸다
            if (_pending.Count < 16) _pending.Add((stream, db, pitch));
            return;
        }

        AudioStreamPlayer? v = null;
        foreach (var p in _voices)
        {
            if (p.Playing) continue;
            v = p;
            break;
        }
        if (v is null)
        {
            v = _voices[_next];
            _next = (_next + 1) % _voices.Count;
        }

        v.Stream = stream;
        v.VolumeDb = db;
        v.PitchScale = pitch;
        v.Play();
    }
}
