// 설정 (S51 · ADR-029 4차).
//
// 이관 원본: ui/game/settings.html.
//
// ── 웹판에서 뺀 것과 그 이유 ────────────────────────────
// ① **텍스트 속도** — 웹판은 3단(차분히/빠르게/바로바로)을 두었지만, Godot 판에는 속도를
//    읽는 코드가 없다. 프롤로그는 비트마다 **입력으로** 넘기고 자동 타자기 연출이 없으며
//    (Prologue/PrologueScene.cs 머리말 ②), 전투 대사도 즉시 출력이다. 아무 데도 안 걸리는
//    조작을 화면에 두면 「눌러도 아무 일이 없다」가 되므로 뺐다. 연출에 시간축이 생기면
//    그때 세이브의 SettingsState.TextSpeed 와 함께 되살린다.
// ② **디버그 패널** — 웹판 debug.js 의 🐞 오버레이는 브라우저 전용이다. Godot 판의 디버그는
//    명령줄 스위치(`--rh-hub=meta`, `--rh-pro=gate`, `--rh-set=…`)로 들어가므로 화면에서
//    켜고 끌 대상이 아니다.
//
// ── 웹판에 없던 것 ──────────────────────────────────────
// ③ **음량** — 웹판에는 소리 자체가 없었다. Master 버스 볼륨으로 건다(Meta/AppSettings.cs).
// ④ **프롤로그 다시 보기** — 프롤로그는 「첫 원정」 경로에만 있어서(Title.OnNewRun) 한 번
//    등록하면 두 번 다시 못 본다. 여기가 유일한 재생 경로다.
//
// ── 위험한 동작은 두 번 묻는다 ─────────────────────────
// 웹판은 `confirm()` 한 방이었다. 여기서는 그 줄이 **직접 확인 줄로 바뀌고**(무엇이 지워지는지
// 다시 적는다) 두 번째 누름에서만 지운다. 취소가 기본값이다.
//
// ── 어디에 저장되는가 ───────────────────────────────────
//   user://settings.json  음량·음소거 (이 화면 소관 — Meta/AppSettings.cs)
//   user://save.json      화면 흔들림 (Fx/Embers·Fx/CombatFx 가 읽는 값이라 정본을 옮기지 않았다)
//
// ── 디버그 (검증용) ─────────────────────────────────────
//   --rh-set=vol:35      음량을 35%로 저장하고(실제 저장 경로를 그대로 탄다) 결과를 찍는다
//   --rh-set=mute        음소거 켬 (unmute 로 끔). 쉼표로 이어 붙일 수 있다

using Godot;
using ReviewHero.Game.Audio;
using ReviewHero.Game.Combat;   // CombatArt — 색·조각 사전 / CombatEntry.ArgValue — 명령줄
using ReviewHero.Game.Fx;       // SignatureStore — 전체 초기화 시 캐시를 버린다
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Meta;

public partial class Settings : Control
{
    private const int W = CombatArt.ScreenW;   // 1344
    private const int H = CombatArt.ScreenH;   // 768

    private const int ColX = 92, ColW = 1160;
    private const int Pad = 16;

    /// <summary>조작(오른쪽) 영역의 시작 — 설명이 아무리 길어도 여기를 넘지 않는다</summary>
    private const int CtlX = ColW - Pad - 380;

    private static readonly Color Faint = new("6f675a");
    private static readonly Color Warn = new("e08a72");

    /// <summary>확인 대기 중인 위험 동작 (없으면 null) — 한 번에 하나만 열린다</summary>
    private string? _pending;

    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();
        UiTheme.Apply(this);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        ApplyDebugSwitch();
        // 이 화면에 들어온 김에 저장값을 다시 건다 — 다른 경로로 버스가 흔들렸어도 여기서 맞춰진다
        AppSettings.ApplyAudio();
        Build();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!e.IsActionPressed("ui_cancel")) return;
        // 확인 줄이 열려 있으면 Esc 는 먼저 그것을 닫는다 — 실수로 화면을 나가는 것보다 낫다
        if (_pending is not null) { _pending = null; Build(); return; }
        SceneRouter.Go(SceneRouter.Title);
    }

    // ── 조립 ─────────────────────────────────────────

    private void Build()
    {
        foreach (var c in GetChildren()) c.QueueFree();

        var bg = new ColorRect { Color = CombatArt.Bg, MouseFilter = MouseFilterEnum.Ignore };
        bg.At(0, 0, W, H);
        AddChild(bg);

        AddChild(Hub.TopBar("settings", RunStore.Current));

        AddChild(CombatArt.Text("설정", 21, CombatArt.Gold).At(ColX, 52, 200, 26));
        AddChild(CombatArt.Text("바꾸면 즉시 저장된다.", 12, CombatArt.Dim).At(ColX + 52, 60, 400, 18));

        AddChild(Sound());
        AddChild(Screen());
        AddChild(Reviewer());
        AddChild(Data());

        AddChild(CombatArt.Text("Esc — 타이틀로", 12, Faint).At(ColX, 690, 300, 18));
        AddChild(CombatArt.Text($"설정 {AppSettings.Path}   ·   세이브 {RunStore.SavePath}", 12, Faint,
            HorizontalAlignment.Right).At(ColX + ColW - 600, 690, 600, 18));
        AddChild(CombatArt.Text(AppSettings.RealPath, 11, new Color("4a443a"), HorizontalAlignment.Right)
            .At(ColX + ColW - 900, 710, 900, 16));
    }

    // ── 소리 ─────────────────────────────────────────

    private Control Sound()
    {
        var p = Hub.Panel(ColX, 100, ColW, 140);
        p.AddChild(Head("소리"));

        // ① 음량 — 슬라이더를 움직이면 그 자리에서 들린다
        var row = Row(p, 44, "음량", "효과음 크기다. 이 게임의 소리는 전부 종이·도장·동전이라 기본값이 낮게 깔려 있다.");

        var val = CombatArt.Text($"{AppSettings.Percent}%", 14, CombatArt.Ink, HorizontalAlignment.Right);
        val.At(CtlX, row + 4, 52, 20);
        p.AddChild(val);

        // 지금 버스에 걸려 있는 값 — 슬라이더를 움직이면 이 줄도 같이 움직인다(귀와 눈이 어긋나지 않게)
        var busline = CombatArt.Text("", 11, Faint, HorizontalAlignment.Right);

        var slider = new HSlider
        {
            MinValue = 0, MaxValue = 100, Step = 5, Value = AppSettings.Percent,
            CustomMinimumSize = new Vector2(210, 24),
        };
        slider.At(CtlX + 62, row + 4, 210, 24);
        slider.ValueChanged += v =>
        {
            AppSettings.Volume = v / 100.0;   // setter 가 저장 + 버스 반영까지 한다
            val.Text = $"{AppSettings.Percent}%";
            busline.Text = BusLine();
            Sfx.Play(SfxId.Click);            // 지금 크기가 귀로 확인된다 (25ms 중복 차단이 걸려 있다)
        };
        p.AddChild(slider);

        var test = Hub.NavBtn("들어보기", false, () => Sfx.Play(SfxId.StampFact));
        test.At(CtlX + 288, row + 2, 88, 28);
        p.AddChild(test);

        // ② 음소거
        int row2 = Row(p, 92, "음소거", "켜면 음량과 상관없이 아무 소리도 나지 않는다.");
        var mute = Toggle(!AppSettings.Muted ? "소리 켬" : "음소거", !AppSettings.Muted, () =>
        {
            AppSettings.Muted = !AppSettings.Muted;
            if (!AppSettings.Muted) Sfx.Play(SfxId.Click);
            Build();
        });
        mute.At(CtlX + 288, row2 + 2, 88, 28);
        p.AddChild(mute);

        busline.Text = BusLine();
        busline.At(CtlX, row2 + 8, 270, 18);
        p.AddChild(busline);
        return p;
    }

    private static string BusLine() =>
        AppSettings.Audible ? $"지금 Master 버스 {AppSettings.Db(AppSettings.Volume):0.0}dB" : "지금 소리 없음";

    // ── 화면·연출 ────────────────────────────────────

    private Control Screen()
    {
        var p = Hub.Panel(ColX, 256, ColW, 96);
        p.AddChild(Head("화면·연출"));

        int row = Row(p, 44, "화면 흔들림",
            "팩트·원산지 판정이 꽂힐 때 화면이 흔들리고 불티가 튄다. 어지러우면 끈다.");
        bool on = RunStore.Settings.Shake;
        var t = Toggle(on ? "켬" : "끔", on, () =>
        {
            // 이 값의 정본은 세이브다 — Fx/Embers·Fx/CombatFx 가 RunStore.Settings.Shake 를 읽는다
            RunStore.Settings.Shake = !RunStore.Settings.Shake;
            RunStore.Save();
            Build();
        });
        t.At(CtlX + 288, row + 2, 88, 28);
        p.AddChild(t);
        return p;
    }

    // ── 리뷰어·이야기 ────────────────────────────────

    private Control Reviewer()
    {
        var p = Hub.Panel(ColX, 368, ColW, 140);
        p.AddChild(Head("리뷰어·이야기"));

        int r1 = Row(p, 44, "필명·서명",
            RunStore.Registered
                ? $"지금 계정: {RunStore.Penname} — 다시 만들면 이전 서명은 덮어쓴다. 쌓인 기록은 그대로 남는다."
                : "아직 등록된 필명이 없다. 새 원정을 시작하면 등록부터 진행한다.");
        var sig = Hub.NavBtn(RunStore.Registered ? "다시 만들기" : "등록하기", false,
            SceneRouter.Exists(SceneRouter.Signature) ? () => SceneRouter.Go(SceneRouter.Signature) : null);
        sig.At(CtlX + 246, r1 + 2, 130, 28);
        p.AddChild(sig);

        int r2 = Row(p, 92, "프롤로그 다시 보기",
            "「어쩌다 여기까지 왔는가」를 처음부터 다시 본다. 진행 중인 원정은 건드리지 않는다.");
        var pro = Hub.NavBtn("다시 보기", false,
            SceneRouter.Exists(SceneRouter.Prologue) ? () => SceneRouter.Go(SceneRouter.Prologue) : null);
        pro.At(CtlX + 246, r2 + 2, 130, 28);
        p.AddChild(pro);
        return p;
    }

    // ── 데이터 관리 (전부 두 번 묻는다) ──────────────

    private Control Data()
    {
        var p = Hub.Panel(ColX, 524, ColW, 152);
        p.AddChild(Head("데이터 관리"));

        var run = RunStore.Current;
        Danger(p, 36, "run", "진행 중인 원정 삭제",
            run is not null
                ? $"1막 {run.Floor}층까지 온 원정을 버린다. 계정에 쌓인 기록은 남는다."
                : "진행 중인 원정이 없다.",
            "원정을 버린다", run is not null, () =>
            {
                RunStore.ClearRun();
                GD.Print("[Settings] 진행 중인 원정을 지웠다");
            });

        Danger(p, 74, "meta", "계정 기록 초기화",
            "누적 전적·통계·명성 RP·적립금 P·명단에 남긴 글·도감을 지운다. 필명과 서명은 남는다.",
            "기록을 지운다", true, () =>
            {
                var m = RunStore.Meta;
                m.Runs = m.Wins = m.BestFloor = m.Rp = m.P = 0;
                m.Expedition.Clear();
                m.Seen.Clear();
                m.Badges.Clear();
                m.Stats = new StatsState();
                RunStore.Save();
                GD.Print("[Settings] 계정 기록을 초기화했다 (필명·서명 유지)");
            });

        Danger(p, 112, "all", "전체 초기화",
            "필명·서명·원정·계정 기록·설정까지 이 기기의 저장 데이터를 전부 지운다. 되돌릴 수 없다.",
            "전부 지운다", true, () =>
            {
                RunStore.WipeAll();
                SignatureStore.Invalidate();   // 이 프로세스에 남은 서명 캐시도 함께 버린다
                AppSettings.Reset();
                GD.Print("[Settings] 전체 초기화 — 타이틀로 돌아간다");
                SceneRouter.Go(SceneRouter.Title);
            });
        return p;
    }

    /// <summary>
    /// 위험한 줄 하나. 첫 누름은 **확인 줄을 여는 것뿐**이고 두 번째 누름에서만 실행된다.
    /// 다른 줄의 확인이 열려 있으면 그것은 닫힌다 — 열린 확인은 언제나 하나다.
    /// </summary>
    private void Danger(Control p, int y, string key, string title, string desc, string confirm, bool enabled,
        Action act)
    {
        bool armed = _pending == key;
        p.AddChild(CombatArt.Text(title, 14, enabled ? CombatArt.Ink : Faint).At(Pad + 2, y, 260, 20));
        p.AddChild(CombatArt.Text(armed ? $"정말 지울까? — {desc}" : desc, 11, armed ? Warn : CombatArt.Dim)
            .At(Pad + 2, y + 20, CtlX - Pad - 20, 16));

        if (!armed)
        {
            var b = Hub.NavBtn("지우기", false, enabled ? () => { _pending = key; Build(); } : null);
            b.At(CtlX + 246, y + 2, 130, 28);
            p.AddChild(b);
            return;
        }

        var no = Hub.NavBtn("취소", false, () => { _pending = null; Build(); });
        no.At(CtlX + 120, y + 2, 76, 28);
        p.AddChild(no);

        var yes = Hub.NavBtn(confirm, true, () =>
        {
            _pending = null;
            act();
            // 지운 뒤의 화면을 다시 그린다 (전체 초기화는 이미 타이틀로 떠났다 — Build 는 무해하다)
            Build();
        });
        yes.At(CtlX + 202, y + 2, 174, 28);
        p.AddChild(yes);
    }

    // ── 공통 조각 ────────────────────────────────────

    private static Control Head(string title)
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore };
        c.At(Pad + 2, 10, 400, 22);
        c.AddChild(CombatArt.Text(title, 13, CombatArt.Gold).At(0, 0, 400, 20));
        return c;
    }

    /// <summary>설명 줄 하나를 깔고 그 줄의 y 를 돌려준다 (조작은 부르는 쪽이 오른쪽에 붙인다)</summary>
    private static int Row(Control p, int y, string title, string desc)
    {
        p.AddChild(CombatArt.Text(title, 14, CombatArt.Ink).At(Pad + 2, y, 300, 20));
        p.AddChild(CombatArt.Text(desc, 11, CombatArt.Dim).At(Pad + 2, y + 21, CtlX - Pad - 20, 16));
        return y;
    }

    /// <summary>켬/끔 버튼 — 켜져 있으면 금색으로 선다 (웹판 .tog)</summary>
    private static Button Toggle(string text, bool on, Action onPressed) => Hub.NavBtn(text, on, onPressed);

    // ── 디버그 스위치 ────────────────────────────────

    private static void ApplyDebugSwitch()
    {
        string? spec = CombatEntry.ArgValue(Godot.OS.GetCmdlineUserArgs(), "set");
        if (spec is null) return;
        foreach (string part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("vol:", StringComparison.Ordinal)
                && int.TryParse(part[4..], out int pct)) AppSettings.Volume = pct / 100.0;
            else if (part == "mute") AppSettings.Muted = true;
            else if (part == "unmute") AppSettings.Muted = false;
        }
        GD.Print($"[Settings] --rh-set 적용 — 음량 {AppSettings.Percent}% · 음소거 {AppSettings.Muted}"
            + $" · Master {AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master")):0.0}dB");
    }
}
