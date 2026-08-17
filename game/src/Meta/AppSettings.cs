// 화면 설정 — `user://settings.json` 한 장 (ADR-029 4차).
//
// ── 왜 세이브(user://save.json)와 나눠 두는가 ──────────────
// 세이브에는 이미 <see cref="SettingsState"/>(TextSpeed·Shake·Debug)가 있다. 거기에 오디오
// 항목을 더하려면 `Run/RunState.cs` 를 고쳐야 하는데 그 파일은 런·메타의 정본이라 화면 하나가
// 필드를 붙이러 들어갈 자리가 아니다. **화면 설정은 세이브가 아니다** — 세이브를 지워도
// 볼륨은 남는 편이 맞고(기기 설정에 가깝다), 세이브가 깨져도 소리는 나야 한다.
// 그래서 파일을 따로 둔다. 「전체 초기화」만 이 파일까지 함께 지운다.
//
//   user://settings.json = { "v": 1, "volume": 0.8, "muted": false }
//
// 화면 흔들림(Shake)은 여기 오지 않는다 — 이미 <see cref="RunStore.Settings"/>.Shake 를
// Fx/Embers·Fx/CombatFx 가 읽고 있어서, 정본을 옮기면 그 두 곳이 같이 바뀌어야 한다.
// **읽는 쪽이 이미 있는 값은 그 자리에 둔다.**
//
// ── 음량은 AudioServer 버스로 건다 ─────────────────────
// Audio/Sfx.cs 의 재생기는 전부 기본 버스(Master)에 붙는다. 버스 볼륨은 씬을 갈아타도 남고
// 이미 울리고 있는 소리에도 즉시 걸리므로, 재생 시점마다 dB 를 더하는 방식(Sfx.MasterDb)보다
// 이쪽이 정석이다. Sfx.MasterDb(-7dB)는 그대로 두고 그 위에 버스가 곱해진다.
//
// ── 언제 걸리는가 ──────────────────────────────────────
// 설정 화면을 한 번도 안 열어도 게임을 켠 순간부터 걸려야 한다. 그래서 어셈블리가 로드될 때
// (<see cref="Boot"/> — [ModuleInitializer]) 한 번 적용한다. 그 시점에 엔진이 아직 준비되지
// 않았다면 조용히 지나가고 값도 캐시하지 않는다 — 다음에 읽을 때 다시 파일을 본다.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Meta;

/// <summary>settings.json 의 최상위 구조</summary>
public sealed class AppSettingsData
{
    public int V { get; set; } = 1;

    /// <summary>0~1 선형 음량 (1 = 버스 0dB)</summary>
    public double Volume { get; set; } = AppSettings.DefaultVolume;

    public bool Muted { get; set; }
}

public static class AppSettings
{
    public const string Path = "user://settings.json";

    /// <summary>기본 음량 — 소리가 있다는 것은 알되 놀라지 않을 만큼</summary>
    public const double DefaultVolume = 0.8;

    /// <summary>음량 0 취급 경계 (이 아래면 버스를 음소거한다)</summary>
    private const double Silent = 0.0005;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static AppSettingsData? _cache;

    private static AppSettingsData Data
    {
        get
        {
            if (_cache is not null) return _cache;
            var (loaded, ok) = LoadFromDisk();
            // 엔진이 아직 없어 경로를 못 구한 경우(ok=false)는 캐시하지 않는다 —
            // 여기서 굳혀 버리면 저장돼 있던 값이 영영 안 걸린다.
            if (ok) _cache = loaded;
            return loaded;
        }
    }

    // ── 값 ───────────────────────────────────────────

    /// <summary>0~1. 대입하면 즉시 저장되고 버스에 걸린다</summary>
    public static double Volume
    {
        get => Math.Clamp(Data.Volume, 0, 1);
        set
        {
            var d = Data;
            d.Volume = Math.Clamp(value, 0, 1);
            Commit(d);
        }
    }

    public static bool Muted
    {
        get => Data.Muted;
        set
        {
            var d = Data;
            d.Muted = value;
            Commit(d);
        }
    }

    /// <summary>지금 소리가 나는가 (음소거이거나 음량 0이면 안 난다)</summary>
    public static bool Audible => !Muted && Volume > Silent;

    /// <summary>표시용 백분율</summary>
    public static int Percent => (int)Math.Round(Volume * 100);

    /// <summary>실제 파일 경로 (설정 화면이 「어디에 저장되는가」를 보여 준다)</summary>
    public static string RealPath => Platform.GlobalizePath(Path);

    // ── 적용 ─────────────────────────────────────────

    /// <summary>선형 음량 → 버스 dB. 0 은 -80dB(무음)로 떨어뜨린다</summary>
    public static float Db(double volume) =>
        volume <= Silent ? -80f : Mathf.LinearToDb((float)Math.Clamp(volume, 0, 1));

    /// <summary>지금 값을 Master 버스에 건다. 엔진이 없으면 조용히 지나간다</summary>
    public static void ApplyAudio()
    {
        try
        {
            int bus = AudioServer.GetBusIndex("Master");
            if (bus < 0) bus = 0;
            var d = Data;
            AudioServer.SetBusVolumeDb(bus, Db(d.Volume));
            AudioServer.SetBusMute(bus, d.Muted || d.Volume <= Silent);
        }
        catch (Exception e)
        {
            // 소리가 흐름을 막지 않는다 — Audio/Sfx.cs 와 같은 원칙
            try { GD.PushWarning($"[AppSettings] 음량 적용 실패 — 무시하고 진행한다: {e.Message}"); }
            catch { /* 엔진 밖 */ }
        }
    }

    /// <summary>어셈블리 로드 시점 1회 — 설정 화면을 안 열어도 저장된 음량이 걸린다</summary>
    // CA2255 는 「라이브러리에 모듈 초기화자를 두지 말라」는 권고다. 이 어셈블리는 라이브러리가
    // 아니라 게임 실행 그 자체이고, 첫 씬보다 먼저 도는 자리가 여기뿐이라(오토로드는
    // project.godot 을 건드려야 한다 — Audio/Sfx.cs 머리말) 의도적으로 쓴다.
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Boot()
    {
        try { ApplyAudio(); }
        catch { /* 엔진 준비 전이면 다음 접근에서 다시 건다 */ }
    }
#pragma warning restore CA2255

    // ── 저장/로드 ────────────────────────────────────

    /// <summary>기본값으로 되돌리고 파일을 지운다 (전체 초기화가 부른다)</summary>
    public static void Reset()
    {
        _cache = new AppSettingsData();
        try
        {
            string real = RealPath;
            if (File.Exists(real)) File.Delete(real);
        }
        catch (Exception e)
        {
            Platform.Print($"[AppSettings] 설정 파일 삭제 실패: {e.Message}");
        }
        ApplyAudio();
    }

    /// <summary>디스크를 다시 읽는다 (재시작 없이 왕복을 확인할 때)</summary>
    public static void Reload()
    {
        _cache = null;
        ApplyAudio();
    }

    private static void Commit(AppSettingsData d)
    {
        _cache = d;
        Save(d);
        ApplyAudio();
    }

    private static void Save(AppSettingsData d)
    {
        try
        {
            string real = RealPath;
            string? dir = System.IO.Path.GetDirectoryName(real);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(real, JsonSerializer.Serialize(d, JsonOpts));
        }
        catch (Exception e)
        {
            Platform.Print($"[AppSettings] 설정 저장 실패: {e.Message}");
        }
    }

    /// <summary>(값, 읽기를 신뢰할 수 있는가). 엔진이 없어 경로를 못 구하면 신뢰 불가다</summary>
    private static (AppSettingsData Data, bool Ok) LoadFromDisk()
    {
        string? real = null;
        try
        {
            string p = Godot.ProjectSettings.GlobalizePath(Path);
            if (!string.IsNullOrEmpty(p)) real = p;
        }
        catch
        {
            // Godot 런타임 밖 — 아래에서 신뢰 불가로 돌려준다
        }
        if (real is null) return (new AppSettingsData(), false);

        try
        {
            if (!File.Exists(real)) return (new AppSettingsData(), true);
            var loaded = JsonSerializer.Deserialize<AppSettingsData>(File.ReadAllText(real), JsonOpts);
            return (loaded ?? new AppSettingsData(), true);
        }
        catch (Exception e)
        {
            // 설정이 깨졌다고 게임이 안 켜지면 안 된다 — 기본값으로 출발한다
            Platform.Print($"[AppSettings] 설정 로드 실패 — 기본값으로 간다: {e.Message}");
            return (new AppSettingsData(), true);
        }
    }
}
