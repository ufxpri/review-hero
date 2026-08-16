// 만물대장 · 리뷰어 등록 — 이름과 서명을 한 화면에서 받는다 (ADR-020·022, GDD §4.4 선행 단계).
//
// 이관 원본: ui/signature.html. 배치는 코드로 조립하고 .tscn 은 루트와 스크립트만 든다.
//
// ── 이 화면이 무엇을 하는가 ────────────────────────────
// **이름은 사람들이 보고, 서명은 대장이 대조한다.** 둘의 역할이 다르므로 같은 장에 나란히 받는다.
// 서명은 독자에게 보이는 물건이 아니라 **올리는 행위 그 자체**여서, 플레이어가 그것을 확인할 수
// 있는 자리는 카드 하단뿐이다 — 그래서 우하단에 획순 재생 미리보기를 붙였다.
// 그리고 필명은 저쪽 세계에서 쓰던 이름이라 이 세계에서 조회되지 않는다. 그것이 주인공이
// 살아남는 유일한 근거다(worldview §2.2) — 입력란 안내문이 그 사실을 그대로 말한다.
//
// ── 디버그 ─────────────────────────────────────────────
//   --rh-sig=demo   샘플 획을 주입한 상태로 연다 (마우스 없이 캡처할 때)
//   --rh-sig=done   샘플 획 + 이름으로 등록 완료 오버레이까지 띄운다

using Godot;
using ReviewHero.Game.Combat;   // CombatArt — 색·조각 사전 (읽기만 한다)
using ReviewHero.Game.Fx;
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Signature;

public partial class SignatureScene : Control
{
    private const int ScreenW = CombatArt.ScreenW;
    private const int ScreenH = CombatArt.ScreenH;

    /// <summary>필명 길이 상한 (ADR-016 「남은 것」 — 서버 검증은 Layer 3)</summary>
    private const int NameMax = 12;

    private const float PadX = (ScreenW - SignaturePad.BoxW) / 2f;

    private LineEdit _name = null!;
    private SignaturePad _pad = null!;
    private SignaturePreviewCard _card = null!;
    private Button _undo = null!;
    private Button _clear = null!;
    private Button _save = null!;

    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();
        UiTheme.Apply(this);
        Build();

        // 이미 등록한 것이 있으면 그대로 얹는다 — 재등록은 「지우고 다시」다
        _name.Text = RunStore.Registered ? RunStore.Penname : "";
        if (RunStore.Signature is { HasStrokes: true } saved) _pad.Load(saved.ToVectors());

        ApplyDebugSwitch();
        Refresh();
    }

    // ── 조립 ─────────────────────────────────────────

    private void Build()
    {
        foreach (var c in GetChildren()) c.QueueFree();

        var bg = new ColorRect { Color = CombatArt.Bg, MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        AddChild(CombatArt.Text("만물마켓", 18, CombatArt.Gold).At(20, 16, 200, 24));

        AddChild(CombatArt.Text("만물대장 · 리뷰어 등록", 26, CombatArt.Ink, HorizontalAlignment.Center)
            .At(0, 48, ScreenW, 34));
        AddChild(CombatArt.Text("사람들은 이름으로 당신을 구분하고, 대장은 서명을 대조한다.",
            15, CombatArt.Dim, HorizontalAlignment.Center).At(0, 96, ScreenW, 22));
        AddChild(CombatArt.Text("서명하지 않은 글은 올라가지 않는다 — 올리는 것이 곧 서명하는 것이다.",
            15, CombatArt.Dim, HorizontalAlignment.Center).At(0, 120, ScreenW, 22));

        // ── 작성자명 ──
        AddChild(CombatArt.Text("작 성 자 명", 12, new Color("a08f68")).At(PadX, 164, 400, 16));
        _name = NameField();
        AddChild(_name.At(PadX, 184, SignaturePad.BoxW, 52));

        // ── 서명 패드 ──
        _pad = new SignaturePad();
        _pad.Changed += OnPadChanged;
        AddChild(_pad.At(PadX, 262, SignaturePad.BoxW, SignaturePad.BoxH));

        // ── 조작 ──
        var row = UiTheme.HBox(10);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(row.At(PadX, 524, SignaturePad.BoxW, 46));
        _undo = UiTheme.Btn("한 획 지우기", () => _pad.Undo(), size: 17);
        _clear = UiTheme.Btn("지우고 다시", () => _pad.Clear(), size: 17);
        _save = UiTheme.Btn("이 이름과 서명으로 등록", OnSave, size: 17);
        row.AddChild(_undo);
        row.AddChild(_clear);
        row.AddChild(_save);

        AddChild(CombatArt.Text(
            "그은 획은 그대로 남는다. 전투에서 카드를 낼 때마다 같은 순서로 다시 그어진다 — 그것이 게시다.",
            13, new Color("6f675a"), HorizontalAlignment.Center).At(0, 592, ScreenW, 20));

        // ── 카드 미리보기 ──
        AddChild(CombatArt.Text("카드에 얹힌 모습", 13, CombatArt.Dim, HorizontalAlignment.Center)
            .At(1130, 482, SignaturePreviewCard.W, 20));
        _card = new SignaturePreviewCard();
        AddChild(_card.At(1130, 508, SignaturePreviewCard.W, SignaturePreviewCard.H));
    }

    private LineEdit NameField()
    {
        var le = new LineEdit
        {
            MaxLength = NameMax,
            PlaceholderText = "저쪽 세계에서 쓰던 이름",
            CaretBlink = true,
        };
        le.AddThemeStyleboxOverride("normal", FieldBox(CombatArt.Parch, CombatArt.Inkc));
        le.AddThemeStyleboxOverride("focus", FieldBox(new Color("f2e8d2"), CombatArt.EdgeHi));
        le.AddThemeColorOverride("font_color", CombatArt.Inkc);
        le.AddThemeColorOverride("font_placeholder_color", new Color("a89572"));
        le.AddThemeColorOverride("caret_color", CombatArt.Inkc);
        le.AddThemeColorOverride("font_selected_color", CombatArt.Parch);
        le.AddThemeColorOverride("selection_color", new Color(0.17f, 0.13f, 0.1f, 0.55f));
        le.AddThemeFontSizeOverride("font_size", 24);
        le.TextSubmitted += _ => OnSave();
        return le;
    }

    private static StyleBoxFlat FieldBox(Color bg, Color border)
    {
        var s = CombatArt.Box(bg, border, 8, 2);
        s.ContentMarginLeft = s.ContentMarginRight = 14;
        s.ContentMarginTop = s.ContentMarginBottom = 8;
        return s;
    }

    // ── 상태 ─────────────────────────────────────────

    private void OnPadChanged()
    {
        Refresh();
        _card.Play(Current());
    }

    /// <summary>패드가 모은 획 — 미리보기와 저장이 같은 것을 본다</summary>
    private List<Vector2[]> Current()
    {
        var outp = new List<Vector2[]>();
        foreach (var s in _pad.Strokes)
        {
            if (s.Count >= 2) outp.Add(s.ToArray());
        }
        return outp;
    }

    private void Refresh()
    {
        bool has = _pad.HasInk;
        _undo.Disabled = _clear.Disabled = _save.Disabled = !has;
    }

    // ── 등록 ─────────────────────────────────────────

    private void OnSave()
    {
        string name = _name.Text.Trim();
        if (name.Length == 0)
        {
            // 이름 없이는 대장에 오르지 않는다. 서명만으로는 누가 썼는지가 비어 버린다
            Shake(_name);
            _name.GrabFocus();
            return;
        }
        var strokes = Current();
        if (strokes.Count == 0)
        {
            Shake(_pad);
            return;
        }
        // 등록되는 순간 = 대장에 도장이 찍히는 순간
        Audio.Sfx.Play(Audio.SfxId.StampOrigin);

        var data = SignatureData.FromVectors(strokes);
        RunStore.Penname = name;      // 두 setter 가 각각 세이브를 내린다 (user://save.json)
        RunStore.Signature = data;

        // 이 프로세스에서도 즉시 반영 — 다음 전투의 카드가 곧바로 이 획으로 그어진다
        SignatureStore.Strokes = data.ToVectors();

        GD.Print($"[Signature] 등록 — 「{name}」 · 획 {data.Strokes.Count}개 · {RunStore.SavePath}");
        ShowDone(name);
    }

    private static void Shake(Control c)
    {
        float x = c.Position.X;
        var t = c.CreateTween();
        t.TweenProperty(c, "position:x", x - 8f, 0.06f);
        t.TweenProperty(c, "position:x", x + 7f, 0.07f);
        t.TweenProperty(c, "position:x", x - 4f, 0.06f);
        t.TweenProperty(c, "position:x", x, 0.07f);
    }

    // ── 등록 완료 ────────────────────────────────────

    private void ShowDone(string name)
    {
        var overlay = new Control { MouseFilter = MouseFilterEnum.Stop };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        // 웹판은 backdrop-filter 로 뒤를 흐렸다. 여기서는 더 짙게 덮는다 —
        // 등록이 끝난 뒤에도 서명 패드가 비쳐 보이면 아직 고칠 수 있는 화면처럼 읽힌다
        var dim = new ColorRect { Color = new Color(8f / 255f, 6f / 255f, 4f / 255f, 0.965f) };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.AddChild(center);

        var box = UiTheme.VBox(14);
        center.AddChild(box);

        var call = UiTheme.Text($"─ {name} 님.", 32, CombatArt.Gold, wrap: false);
        call.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(call);

        var dn = UiTheme.Text("그 이름을 소리 내어 부른 사람은 처음이었다.", 16, new Color("9b8c74"), wrap: false);
        dn.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(dn);

        var dq = UiTheme.Text("뭐라고 쓰실 겁니까? — 일단 좀 보고요.", 19, new Color("f2e6cf"), wrap: false);
        dq.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(dq);

        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
        box.AddChild(UiTheme.Btn("첫 리뷰 쓰러 가기 →", GoFirstReview, size: 22));
        box.AddChild(UiTheme.Btn("타이틀로", SceneRouter.GoTitle, size: 17));
    }

    private static void GoFirstReview()
    {
        if (RunStore.Current is null) RunStore.NewRun();
        SceneRouter.GoMap();
    }

    // ── 디버그 스위치 ────────────────────────────────

    private void ApplyDebugSwitch()
    {
        string mode = ArgValue("sig") ?? "";
        if (mode is not ("demo" or "done")) return;

        _pad.Load(DemoStrokes());
        if (mode == "done")
        {
            if (_name.Text.Trim().Length == 0) _name.Text = "손목나감";
            OnSave();
        }
    }

    private static string? ArgValue(string name)
    {
        string prefix = $"--rh-{name}=";
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith(prefix, System.StringComparison.Ordinal)) return a[prefix.Length..];
        }
        return null;
    }

    /// <summary>
    /// 마우스 없이 화면을 확인하기 위한 표본 획. 손글씨처럼 보이기만 하면 되므로
    /// 사인파를 세 획으로 끊어 그린다 — 획이 여러 개여야 획순 재생이 보인다.
    /// </summary>
    private static List<Vector2[]> DemoStrokes()
    {
        const float w = SignaturePad.BoxW, h = SignaturePad.BoxH;
        var strokes = new List<Vector2[]>();

        // 본체 — 왼쪽에서 오른쪽으로 흘려 쓴 획
        var main = new List<Vector2>();
        for (int i = 0; i <= 120; i++)
        {
            float t = i / 120f;
            float x = w * 0.13f + t * w * 0.62f;
            float y = h * 0.52f
                      - Mathf.Sin(t * Mathf.Pi * 4.5f) * h * 0.20f
                      - Mathf.Sin(t * Mathf.Pi * 1.1f) * h * 0.07f;
            main.Add(new Vector2(x, y));
        }
        strokes.Add(main.ToArray());

        // 꼬리 — 마지막에 위로 튀어 올리는 획
        var tail = new List<Vector2>();
        for (int i = 0; i <= 40; i++)
        {
            float t = i / 40f;
            float x = w * 0.72f + t * w * 0.16f;
            float y = h * 0.60f - t * t * h * 0.34f;
            tail.Add(new Vector2(x, y));
        }
        strokes.Add(tail.ToArray());

        // 밑줄 — 대장에 긋는 마무리
        var rule = new List<Vector2>();
        for (int i = 0; i <= 60; i++)
        {
            float t = i / 60f;
            float x = w * 0.11f + t * w * 0.78f;
            float y = h * 0.74f + Mathf.Sin(t * Mathf.Pi) * h * 0.05f;
            rule.Add(new Vector2(x, y));
        }
        strokes.Add(rule.ToArray());

        return strokes;
    }
}
