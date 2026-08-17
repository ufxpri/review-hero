// 프롤로그 슬라이드쇼 — 「이세계 리뷰어는 어떻게 시작했는가」 (design/prologue-v1.md).
//
// 이관 원본: ui/prologue.html (+ tools/ui/build_prologue.py). 본문은 여기 없다 —
// <see cref="PrologueDoc"/> 가 정본 문서를 읽어 온다. 배치만 코드로 조립하고 .tscn 은 루트뿐이다.
//
// ── 이 화면이 무엇을 하는가 ────────────────────────────
// 게임은 첫 30초 안에 세 가지를 납득시켜야 한다(문서 §1) — 왜 리뷰가 무기인가 · 왜 하필 이
// 사람인가 · 왜 그를 향해서는 아무도 못 쓰는가. 규칙 설명으로는 안 들어가므로 이야기로 먼저
// 심는다. 그래서 이 화면에는 **읽기와 넘기기 말고 아무 상호작용도 없다.**
//
// ── 지켜야 하는 규칙 ───────────────────────────────────
// ① **슬라이드 중간에는 어떤 입력도 끼우지 않는다** (ADR-022). 이름도 서명도 슬라이드가 다
//    끝난 뒤 게이트를 지나 서명 화면(Signature.tscn) 한 장에서 받는다.
// ② 자동 진행 없음. 클릭/스페이스로 넘기고 읽는 속도는 플레이어가 정한다.
// ③ **건너뛰기 상시 노출.** 2회차 플레이어를 붙잡지 않는다.
// ④ 비트마다 텍스트가 넘어가고 **이미지는 슬라이드 단위로 유지된다** — 비주얼 노벨의 기본 문법.
//
// ── 디버그 (스크린샷 검증용) ────────────────────────────
//   --rh-pro=8      8번째 슬라이드의 마지막 비트로 열고
//   --rh-pro=8:2    8번째 슬라이드의 2번째 비트로 연다 (둘 다 1부터 센다)
//   --rh-pro=gate   슬라이드를 건너뛰고 마지막 게이트를 띄운다

using Godot;
using ReviewHero.Game.Audio;
using ReviewHero.Game.Combat;   // CombatArt — 색·조각 사전 (읽기만 한다)
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Prologue;

public partial class PrologueScene : Control
{
    private const int W = CombatArt.ScreenW;   // 1344
    private const int H = CombatArt.ScreenH;   // 768

    /// <summary>이미지 여유분 — 충격 슬라이드에서 화면을 흔들 때 가장자리가 비지 않게 넉넉히 깐다</summary>
    private const int Bleed = 28;

    private const int PadX = 96;               // 웹판 .copy 의 padding:0 96px
    private const int CopyW = 940;             // 본문 최대 폭 — 가로 1344 를 다 채우면 눈이 줄을 놓친다
    private const int CopyBottom = H - 96;     // 웹판 .copy 의 bottom:96px

    private static readonly Color Shade = new(6f / 255f, 5f / 255f, 4f / 255f);
    private static readonly Color Say = new("e0a94b");
    private static readonly Color Hint = new("9a8f7d");

    private IReadOnlyList<PrologueSlide> _slides = System.Array.Empty<PrologueSlide>();
    private int _slide = -1;
    private int _beat;
    private bool _gate;

    private Control _shake = null!;      // 충격 연출이 흔드는 판 — 화면 전체가 여기 들어간다
    private TextureRect _imgPrev = null!;
    private TextureRect _imgCur = null!;
    private ColorRect _missBg = null!;   // 그림이 없는 슬라이드(P15)의 바탕
    private Label _missing = null!;      // 그 슬라이드가 무엇인지 알려 주는 자리표시
    private VBoxContainer _copy = null!;
    private Control _dots = null!;
    private Control _ticks = null!;
    private ColorRect _flash = null!;
    private Control _gateBox = null!;

    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();
        UiTheme.Apply(this);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _slides = PrologueDoc.Slides;
        Build();

        if (_slides.Count == 0)
        {
            // 본문을 못 읽었다면 붙잡아 둘 이유가 없다 — 등록으로 가는 길은 열어 둔다
            GD.PushError("[Prologue] 슬라이드가 0장이다 — 게이트만 띄운다");
            ShowGate();
            return;
        }

        var (slide, beat, gate) = Debug();
        if (gate) ShowGate();
        else Show(slide, beat, animate: false);
    }

    // ── 조립 ─────────────────────────────────────────

    private void Build()
    {
        foreach (var c in GetChildren()) c.QueueFree();

        var bg = new ColorRect { Color = new Color("070605"), MouseFilter = MouseFilterEnum.Ignore };
        bg.At(0, 0, W, H);
        AddChild(bg);

        _shake = new Control { MouseFilter = MouseFilterEnum.Ignore, ClipContents = false };
        _shake.At(0, 0, W, H);
        AddChild(_shake);

        // ── 그림 두 장을 겹쳐 둔다. 아래가 지난 슬라이드, 위가 지금 것 — 위를 페이드인해 넘긴다
        _imgPrev = Plate();
        _shake.AddChild(_imgPrev);
        _imgCur = Plate();
        _shake.AddChild(_imgCur);

        // 그림이 아직 없는 슬라이드(P15)의 자리표시. 검은 화면이 아니라 「비어 있다」로 보여야
        // 링크가 끊긴 사고와 구분된다 — 웹판 .miss 와 같은 자리(위쪽)에 둔다
        _missBg = new ColorRect { Color = new Color("15120e"), MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _missBg.At(0, 0, W, H);
        _shake.AddChild(_missBg);

        _missing = CombatArt.Text("", 12, new Color("4a4438"), HorizontalAlignment.Center);
        _missing.At(0, 70, W, 20);
        _shake.AddChild(_missing);

        _shake.AddChild(BottomShade());

        // ── 본문. 아래에서 위로 자라야 해서 세로 정렬을 End 로 둔다 (비트마다 줄 수가 다르다)
        _copy = UiTheme.VBox(6);
        _copy.Alignment = BoxContainer.AlignmentMode.End;
        _copy.MouseFilter = MouseFilterEnum.Ignore;
        _copy.At(PadX, CopyBottom - 420, CopyW, 420);
        _shake.AddChild(_copy);

        // ── 진행 표시 — 슬라이드 점(위)과 비트 눈금(아래)
        _dots = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _dots.At(PadX, H - 59, W - PadX * 2, 3);
        // 그리는 자리는 여기 한 번만 잇는다 — 비트가 넘어갈 때는 QueueRedraw 만 부른다
        _dots.Draw += () =>
        {
            for (int i = 0; i < _slides.Count; i++)
                _dots.DrawRect(new Rect2(i * 33, 0, 26, 3),
                    i <= _slide ? CombatArt.Gold : CombatArt.Ink with { A = 0.22f });
        };
        _shake.AddChild(_dots);

        _ticks = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _ticks.At(PadX, H - 46, W - PadX * 2, 2);
        _ticks.Draw += () =>
        {
            if (_slide < 0 || _slide >= _slides.Count) return;
            int beats = _slides[_slide].Beats.Count;
            for (int i = 0; i < beats; i++)
                _ticks.DrawRect(new Rect2(i * 13, 0, 9, 2),
                    i <= _beat ? Say with { A = 0.85f } : CombatArt.Ink with { A = 0.2f });
        };
        _shake.AddChild(_ticks);

        _shake.AddChild(CombatArt.Text("클릭 또는 Space 로 계속   ·   ← 되돌리기", 13, Hint,
            HorizontalAlignment.Right).At(W - PadX - 400, H - 68, 400, 20));

        // ── 건너뛰기. 2회차를 붙잡지 않는다 (문서 §2)
        var skip = UiTheme.Btn("건너뛰기", () => ShowGate(), size: 13);
        skip.FocusMode = FocusModeEnum.None;   // 포커스 테두리가 normal 위에 겹쳐 그려진다
        skip.AddThemeStyleboxOverride("focus", CombatArt.Box(new Color(0, 0, 0, 0), null, 5, 0));
        skip.AddThemeStyleboxOverride("normal", CombatArt.Box(new Color(0, 0, 0, 0.45f), new Color("4a4238"), 5));
        skip.AddThemeStyleboxOverride("hover", CombatArt.Box(new Color(0, 0, 0, 0.6f), CombatArt.Gold, 5));
        skip.AddThemeStyleboxOverride("pressed", CombatArt.Box(new Color(0, 0, 0, 0.7f), CombatArt.Gold, 5));
        skip.AddThemeColorOverride("font_color", Hint);
        skip.AddThemeColorOverride("font_hover_color", CombatArt.Ink);
        _shake.AddChild(skip.At(W - 24 - 96, 20, 96, 30));

        // ── 섬광. 흔들리는 판 밖에 둬야 화면 구석까지 덮는다
        _flash = new ColorRect { Color = new Color(1, 1, 1, 0), MouseFilter = MouseFilterEnum.Ignore };
        _flash.At(0, 0, W, H);
        AddChild(_flash);

        _gateBox = Gate();
        _gateBox.Visible = false;
        AddChild(_gateBox);
    }

    private static TextureRect Plate()
    {
        // ClipContents 가 없으면 KeepAspectCovered 가 제 칸 밖으로 흘러 화면을 덮는다 (Title.Hero 와 같은 함정)
        var t = new TextureRect
        {
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            ClipContents = true,
            Modulate = new Color(1, 1, 1, 0),
        };
        t.At(-Bleed, -Bleed, W + Bleed * 2, H + Bleed * 2);
        t.PivotOffset = new Vector2((W + Bleed * 2) / 2f, (H + Bleed * 2) / 2f);
        return t;
    }

    /// <summary>
    /// 아래로 갈수록 어두워지는 띠 — 글이 그림 위에서 읽히게 하는 유일한 장치다(웹판 .slide::after).
    /// DrawRect 를 여러 층 쌓아 만들면 층 경계가 가로줄로 보인다(실측). 그라디언트 텍스처 한 장으로 깐다.
    /// </summary>
    private static Control BottomShade()
    {
        var g = new Gradient();
        g.SetOffset(0, 0f);
        g.SetColor(0, Shade with { A = 0f });
        g.SetOffset(1, 1f);
        g.SetColor(1, Shade with { A = 0.94f });
        g.AddPoint(0.55f, Shade with { A = 0.52f });

        var tex = new GradientTexture2D
        {
            Gradient = g,
            Width = 4,
            Height = 512,
            Fill = GradientTexture2D.FillEnum.Linear,
            FillFrom = new Vector2(0, 0.40f),
            FillTo = new Vector2(0, 0.80f),      // 그 아래는 끝 색으로 유지된다 (Repeat=None)
        };
        var tr = new TextureRect
        {
            Texture = tex,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        tr.At(0, 0, W, H);
        return tr;
    }

    // ── 한 비트를 그린다 ─────────────────────────────

    private void Show(int slide, int beat, bool animate = true)
    {
        if (slide >= _slides.Count) { ShowGate(); return; }
        slide = Mathf.Clamp(slide, 0, _slides.Count - 1);
        var s = _slides[slide];
        beat = Mathf.Clamp(beat, 0, s.Beats.Count - 1);

        bool changed = slide != _slide;
        _slide = slide;
        _beat = beat;

        if (changed) SwapImage(s, animate);
        DrawCopy(s, beat, animate);
        _dots.QueueRedraw();
        _ticks.QueueRedraw();

        if (animate)
        {
            if (changed && s.Impact) Boom();
            else if (changed) Sfx.Play(SfxId.CardDrop, -14f);
            else Sfx.Play(SfxId.CardPick, -16f, 1f + beat * 0.01f);
        }
    }

    private void SwapImage(PrologueSlide s, bool animate)
    {
        _imgPrev.Texture = _imgCur.Texture;
        _imgPrev.Modulate = new Color(1, 1, 1, _imgCur.Texture is null ? 0 : 1);
        _imgPrev.Scale = Vector2.One;

        var tex = CombatArt.Load(s.ImagePath);
        _imgCur.Texture = tex;
        _missing.Text = tex is null ? $"{s.Key} · {s.Title} — 이미지 준비 중" : "";
        _missBg.Visible = tex is null;

        if (tex is null) { _imgCur.Modulate = new Color(1, 1, 1, 0); return; }

        // 충격 슬라이드는 페이드 없이 즉시 바꾼다 — 서서히 밝아지면 터지는 느낌이 죽는다
        if (!animate || s.Impact)
        {
            _imgCur.Modulate = Colors.White;
            _imgCur.Scale = Vector2.One;
            return;
        }
        _imgCur.Modulate = new Color(1, 1, 1, 0);
        _imgCur.Scale = new Vector2(1.06f, 1.06f);
        var t = CreateTween().SetParallel();
        t.TweenProperty(_imgCur, "modulate:a", 1f, 0.7f);
        t.TweenProperty(_imgCur, "scale", Vector2.One, 16f).SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private void DrawCopy(PrologueSlide s, int beat, bool animate)
    {
        foreach (var c in _copy.GetChildren()) { _copy.RemoveChild(c); c.QueueFree(); }

        var no = CombatArt.Text($"{_slide + 1:00} · {s.Title}", 13, CombatArt.Gold);
        _copy.AddChild(no);
        _copy.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4), MouseFilter = MouseFilterEnum.Ignore });

        foreach (var line in s.Beats[beat].Lines) _copy.AddChild(Row(line));

        if (!animate) return;
        _copy.Modulate = new Color(1, 1, 1, 0);
        _copy.Position = new Vector2(PadX, CopyBottom - 420 + 14);
        var t = CreateTween().SetParallel();
        t.TweenProperty(_copy, "modulate:a", 1f, 0.34f);
        t.TweenProperty(_copy, "position:y", (float)(CopyBottom - 420), 0.34f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    /// <summary>본문 한 줄 — 지문은 그냥, 대사는 금색 세로줄을 세우고 화자 폭만큼 들여쓴다</summary>
    private static Control Row(PrologueLine line)
    {
        var body = Body(line.Say
            ? (line.Speaker is { } sp ? $"[color=#e0a94b][b]{sp}[/b][/color]  {line.Text}" : line.Text)
            : line.Text);
        if (!line.Say) return body;

        var h = UiTheme.HBox(0);
        h.MouseFilter = MouseFilterEnum.Ignore;

        var bar = new ColorRect { Color = Say with { A = 0.45f }, MouseFilter = MouseFilterEnum.Ignore };
        bar.CustomMinimumSize = new Vector2(2, 0);
        h.AddChild(bar);

        // 이어지는 줄은 화자 이름 폭만큼 더 들여써 라인을 맞춘다 (웹판 .say.cont)
        float indent = 20;
        if (line.Continues && line.Owner is { } owner)
            indent += CombatArt.Font().GetStringSize(owner, HorizontalAlignment.Left, -1, 17).X + 12;
        h.AddChild(new Control { CustomMinimumSize = new Vector2(indent, 0), MouseFilter = MouseFilterEnum.Ignore });

        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        h.AddChild(body);
        return h;
    }

    /// <summary>본문 글상자. `[` 는 PrologueDoc 이 이미 막아 뒀다 — 여기서는 태그를 믿고 쓴다</summary>
    private static RichTextLabel Body(string bbcode)
    {
        var r = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        r.AddThemeFontSizeOverride("normal_font_size", 20);
        r.AddThemeFontSizeOverride("bold_font_size", 20);
        r.AddThemeColorOverride("default_color", CombatArt.Ink);
        r.AddThemeConstantOverride("line_separation", 10);
        // 그림 위에 얹히는 글이라 그림자가 없으면 밝은 컷에서 통째로 씻긴다
        r.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        r.AddThemeConstantOverride("shadow_offset_x", 0);
        r.AddThemeConstantOverride("shadow_offset_y", 2);
        r.AddThemeConstantOverride("shadow_outline_size", 4);
        r.Text = bbcode;
        return r;
    }

    /// <summary>충격 슬라이드(P07b 폭발) — 섬광 한 번과 짧은 흔들림</summary>
    private void Boom()
    {
        Sfx.Play(SfxId.Hurt, -1f, 0.82f);

        _flash.Color = new Color(1, 1, 1, 0.95f);
        var f = CreateTween();
        f.TweenProperty(_flash, "color:a", 0.18f, 0.22f);
        f.TweenProperty(_flash, "color:a", 0f, 0.30f);

        (float X, float Y, float T)[] path =
        {
            (-10, 6, 0.05f), (9, -7, 0.07f), (-6, -3, 0.09f), (5, 4, 0.08f), (-3, 2, 0.07f), (0, 0, 0.06f),
        };
        var t = CreateTween();
        foreach (var (x, y, sec) in path)
            t.TweenProperty(_shake, "position", new Vector2(x, y), sec);
    }

    // ── 게이트 — 여기서 서명 등록으로 넘긴다 (ADR-022) ─

    private Control Gate()
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Stop };
        c.At(0, 0, W, H);

        var dim = new ColorRect { Color = new Color(6f / 255f, 5f / 255f, 4f / 255f, 0.94f) };
        dim.At(0, 0, W, H);
        c.AddChild(dim);

        var center = new CenterContainer();
        center.At(0, 0, W, H);
        c.AddChild(center);

        var box = UiTheme.VBox(16);
        center.AddChild(box);

        var h2 = UiTheme.Text("마지막 한 가지가 남았습니다", 25, CombatArt.Ink, wrap: false);
        h2.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(h2);

        var p1 = UiTheme.Text("만물대장에 리뷰를 올리려면 이름과 서명을 등록해야 합니다.", 15, Hint, wrap: false);
        p1.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(p1);
        var p2 = UiTheme.Text("등록하고 나면 바로 쓰실 수 있어요.", 15, Hint, wrap: false);
        p2.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(p2);

        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

        var go = UiTheme.Btn("서명 남기러 가기 →", GoSignature, size: 22);
        go.AddThemeStyleboxOverride("normal", CombatArt.Box(new Color("4d1a11"), new Color("a4553c"), 9, 1));
        go.AddThemeStyleboxOverride("hover", CombatArt.Box(new Color("7a2a1c"), CombatArt.Gold, 9, 1));
        go.AddThemeStyleboxOverride("pressed", CombatArt.Box(new Color("3a140c"), CombatArt.Gold, 9, 1));
        go.AddThemeColorOverride("font_color", CombatArt.Ink);
        go.CustomMinimumSize = new Vector2(0, 54);
        box.AddChild(go);

        var back = UiTheme.Btn("타이틀로", SceneRouter.GoTitle, size: 13);
        back.Flat = true;
        back.AddThemeColorOverride("font_color", new Color("6f675a"));
        box.AddChild(back);
        return c;
    }

    private void ShowGate()
    {
        _gate = true;
        _gateBox.Visible = true;
        Sfx.Play(SfxId.Toast, -10f);
    }

    private static void GoSignature()
    {
        if (SceneRouter.Exists(SceneRouter.Signature)) { SceneRouter.Go(SceneRouter.Signature); return; }
        GD.PushWarning("[Prologue] 서명 씬이 없다 — 타이틀로 되돌린다");
        SceneRouter.GoTitle();
    }

    // ── 입력 — 넘기고, 되돌리고, 건너뛴다. 그게 전부다 ─

    public override void _UnhandledInput(InputEvent e)
    {
        if (_gate || _slides.Count == 0) return;

        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            Advance();
            AcceptEvent();
            return;
        }
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        switch (k.Keycode)
        {
            case Key.Space or Key.Enter or Key.KpEnter or Key.Right:
                Advance();
                AcceptEvent();
                break;
            case Key.Left:
                Back();
                AcceptEvent();
                break;
            case Key.Escape:
                ShowGate();
                AcceptEvent();
                break;
        }
    }

    private void Advance()
    {
        var s = _slides[_slide];
        if (_beat + 1 < s.Beats.Count) Show(_slide, _beat + 1);
        else if (_slide + 1 < _slides.Count) Show(_slide + 1, 0);
        else ShowGate();
    }

    private void Back()
    {
        if (_beat > 0) Show(_slide, _beat - 1);
        else if (_slide > 0) Show(_slide - 1, _slides[_slide - 1].Beats.Count - 1);
    }

    // ── 디버그 스위치 ────────────────────────────────

    private (int Slide, int Beat, bool Gate) Debug()
    {
        string? v = CombatEntry.ArgValue(OS.GetCmdlineUserArgs(), "pro");
        if (v is null) return (0, 0, false);
        if (v == "gate") return (0, 0, true);

        var parts = v.Split(':');
        int slide = int.TryParse(parts[0], out int a) ? Mathf.Clamp(a - 1, 0, _slides.Count - 1) : 0;
        int beat = parts.Length > 1 && int.TryParse(parts[1], out int b)
            ? Mathf.Clamp(b - 1, 0, _slides[slide].Beats.Count - 1)
            : _slides[slide].Beats.Count - 1;      // 비트를 안 주면 그 슬라이드의 마지막 비트
        return (slide, beat, false);
    }
}
