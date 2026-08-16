// 손패 카드 — 큰 양피지 한 장. 카드 1장 = 완성 리뷰다 (ADR-011).
//
// **본문(text)이 잘리지 않고 전부 읽히는 것**이 이 화면의 1순위다. 웹판에서 사용자가 가장
// 강하게 요구한 지점이고, 그래서 combat.html 은 fitTexts() 로 넘치면 글자를 줄였다.
// 여기서도 같다 — 본문은 스크롤도 말줄임도 없이, 들어갈 때까지 글자 크기를 낮춰 전부 보여준다.
//
// 카드가 들고 있는 것: ★별점 · 판정 뱃지 · 제목 · 본문 · 📍원산지 표기 · 👍예상 좋아요 ·
// #태그 · ✍비용 · 그리고 낼 때 그어지는 **서명**(SignatureInk).
// 판정·예상 좋아요는 전부 엔진이 준 SubmitPreview 를 옮겨 적을 뿐이다 (ADR-025).

using Godot;
using ReviewHero.Engine;
using ReviewHero.Game.Fx;

namespace ReviewHero.Game.Combat;

public partial class CardView : Control
{
    public const int W = 175;
    public const int H = 240;

    public int Uid { get; }
    public CardDef Def { get; }
    public SubmitPreview Preview { get; }

    public SignatureInk Sig { get; private set; } = null!;

    private readonly string _originLine;
    private readonly bool _originHere;
    private Panel _bg = null!;
    private bool _selected;

    private static readonly Color TextCol = new("3a3229");
    private static readonly Color NameCol = new("1c1812");
    private static readonly Color OriginCol = new("8a6f3f");
    private static readonly Color OriginHit = new("7d5a08");
    private static readonly Color OriginNone = new("a2947a");
    private static readonly Color UiCol = new("5a4c34");
    private static readonly Color ExpCol = new("8a2f1c");

    public CardView(int uid, CardDef def, SubmitPreview pv, string originLine, bool originHere)
    {
        Uid = uid;
        Def = def;
        Preview = pv;
        _originLine = originLine;
        _originHere = originHere;
        Size = new Vector2(W, H);
        PivotOffset = new Vector2(W / 2f, H);
        MouseFilter = MouseFilterEnum.Ignore;   // 히트 판정은 CombatScene 이 직접 한다
    }

    public override void _Ready() => Build();

    // ── 상태 표시 ─────────────────────────────────────

    public void SetSelected(bool on)
    {
        _selected = on;
        _bg.AddThemeStyleboxOverride("panel", BgStyle());
    }

    /// <summary>끌려 나간 원본 — 흐릿하게 남는다</summary>
    public void SetGhost(bool on) => Modulate = new Color(1, 1, 1, on ? 0.34f : 1f);

    /// <summary>마우스가 얹혔거나 골라진 카드는 들어 올린다 (겹치지 않으므로 본문은 계속 읽힌다)</summary>
    public void SetLift(float dy, float scale)
    {
        Position = new Vector2(Position.X, _homeY + dy);
        Scale = new Vector2(scale, scale);
    }

    private float _homeY;

    public void RememberHome() => _homeY = Position.Y;

    // ── 조립 ──────────────────────────────────────────

    private StyleBoxFlat BgStyle()
    {
        var s = CombatArt.Box(CombatArt.Parch, _selected ? CombatArt.Gold : CombatArt.ParchD, 9,
            _selected ? 3 : 1);
        s.ShadowColor = new Color(0, 0, 0, 0.6f);
        s.ShadowSize = 8;
        s.ShadowOffset = new Vector2(0, 5);
        if (Def is SpecialDef)
        {
            s.BorderWidthTop = 4;
            if (!_selected) s.BorderColor = new Color("a3301c");
        }
        return s;
    }

    private void Build()
    {
        _bg = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        _bg.AddThemeStyleboxOverride("panel", BgStyle());
        _bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_bg);

        var font = CombatArt.Font();
        const float padX = 9f, padY = 8f;
        float innerW = W - padX * 2f;

        bool special = Def is SpecialDef;
        string name = Def.Name;
        string body = (Def.Text ?? string.Empty).Trim();
        if (body.Length == 0) body = CombatSession.Likeify(Def.Ui);
        string uiLine = BuildUiLine();
        string foot = special
            ? "진상 화법" + (Def is SpecialDef { OncePerCombat: true } ? " · 1회" : string.Empty)
            : "#" + (Def as ReviewCardDef)?.Tag;

        // ── 아래에서부터 자리를 잡는다: 꼬리(태그·비용) → 효과줄 → 원산지 → 남는 곳이 본문 ──
        const float footH = 17f;
        float footTop = H - padY - footH;

        float uiH = Mathf.Min(LineH(font, 10) * 3f, Measure(font, uiLine, innerW, 10).Y + 1f);
        float uiTop = footTop - 5f - uiH;

        const float originH = 13f;
        float originTop = uiTop - 4f - originH;

        // ── 위에서부터: 별점·뱃지 → 제목 ──
        float y = padY;
        AddChild(Stars(font, padX, y, innerW));
        y += 16f;

        float nameH = Mathf.Min(LineH(font, 13) * 2f, Measure(font, name, innerW, 13).Y + 1f);
        var nameLabel = Wrapped(name, 13, NameCol);
        nameLabel.At(padX, y, innerW, nameH);
        AddChild(nameLabel);
        y += nameH + 3f;

        // ── 본문 — 남은 자리에 전부 들어갈 때까지 글자를 줄인다 ──
        float bodyH = Mathf.Max(24f, originTop - 3f - y);
        int size = FitSize(font, body, innerW, bodyH, 11, 8);
        var bodyLabel = Wrapped(body, size, TextCol);
        bodyLabel.At(padX, y, innerW, bodyH);
        AddChild(bodyLabel);

        // ── 원산지 — 이 리뷰가 어느 상품에서 태어났는가. 상성의 근거라 항상 보인다 ──
        var origin = CombatArt.Text(_originLine, 10,
            _originHere ? OriginHit : (special || !_originHere ? OriginNone : OriginCol));
        origin.At(padX, originTop, innerW, originH);
        AddChild(origin);

        // ── 서명란 — 낼 때 여기에 획이 그어진다 (= 게시) ──
        Sig = new SignatureInk();
        Sig.At(10, H - 62 - 36, W - 20, 36);
        AddChild(Sig);

        // ── 효과 한 줄 (👍 예상 좋아요 + 카드 ui) ──
        var ui = Wrapped(uiLine, 10, uiLine.StartsWith("👍", System.StringComparison.Ordinal) ? ExpCol : UiCol);
        ui.At(padX, uiTop, innerW, uiH);
        AddChild(ui);

        AddChild(Divider(padX, uiTop - 3f, innerW));
        AddChild(Divider(padX, footTop - 2f, innerW));

        // ── 꼬리: #태그 / ✍비용 ──
        var tag = Chip(foot, new Color("e8d9ae"), new Color("2f2a20"), 10);
        tag.Position = new Vector2(padX, footTop + 1f);
        AddChild(tag);

        var cost = CombatArt.Text($"✍{Def.Cost}", 11, NameCol, HorizontalAlignment.Right);
        cost.At(padX, footTop + 1f, innerW, 14);
        AddChild(cost);
    }

    private Control Stars(Font font, float x, float y, float w)
    {
        var row = new Control { MouseFilter = MouseFilterEnum.Ignore };
        row.At(x, y, w, 15);

        int stars = Def switch
        {
            ReviewCardDef r => r.Stars,
            SpecialDef s => s.Stars ?? 1,
            _ => 1,
        };
        var st = CombatArt.Text(new string('★', stars) + new string('☆', 5 - stars), 12, new Color("9a6b12"));
        st.At(0, 0, w * 0.62f, 15);
        row.AddChild(st);

        var (label, fg, border) = BadgeOf(Preview);
        if (label is not null)
        {
            var chip = Chip(label, fg, border with { A = 0.22f }, 10, border);
            chip.Position = new Vector2(w - chip.Size.X, 0);
            row.AddChild(chip);
        }
        return row;
    }

    /// <summary>판정 뱃지 (card-system-v2 §8) — 판정은 엔진이 준 것을 옮길 뿐이다</summary>
    public static (string?, Color, Color) BadgeOf(SubmitPreview pv)
    {
        if (pv.Blocked is BlockedReason b)
        {
            return b switch
            {
                BlockedReason.Miss => ("빗나감", CombatArt.JNone, new Color("a08f68")),
                BlockedReason.Void => ("대상 없음", CombatArt.JNone, new Color("a08f68")),
                _ => ("무판정", CombatArt.JNone, new Color("b4a37c")),
            };
        }
        return pv.Judgement switch
        {
            Judgement.Origin => ("★ 원산지", CombatArt.JOrigin, new Color("c9a227")),
            Judgement.Fact => ("● 팩트", CombatArt.JFact, new Color("3c6b49")),
            Judgement.Fumble => ("⚠ 헛소리", CombatArt.JFumble, new Color("a3553c")),
            _ => (null, CombatArt.JNone, CombatArt.ParchD),
        };
    }

    private string BuildUiLine()
    {
        string ui = CombatSession.Likeify(Def.Ui);
        if (Def is SpecialDef) return ui;
        if (Preview.Likes is int likes)
        {
            string head = Preview.LikesKind switch
            {
                LikesKind.Defense => $"👍 예상 방어 {likes}",
                LikesKind.Equipment => $"👍 예상 내구도 {likes}",
                _ => $"👍 예상 {likes}",
            };
            return string.IsNullOrEmpty(ui) ? head : $"{head} · {ui}";
        }
        return ui;
    }

    // ── 잡일 ──────────────────────────────────────────

    private static Label Wrapped(string s, int size, Color c)
    {
        var l = CombatArt.Text(s, size, c, wrap: true);
        l.AddThemeConstantOverride("line_spacing", 2);
        l.ClipText = false;
        return l;
    }

    private static Control Divider(float x, float y, float w)
    {
        var r = new ColorRect { Color = CombatArt.ParchD with { A = 0.8f }, MouseFilter = MouseFilterEnum.Ignore };
        r.At(x, y, w, 1);
        return r;
    }

    private static Control Chip(string text, Color fg, Color bg, int size, Color? border = null)
    {
        var font = CombatArt.Font();
        var ts = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        var p = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", CombatArt.Box(bg, border, 3, 1));
        p.Size = new Vector2(ts.X + 11, ts.Y + 3);
        var l = CombatArt.Text(text, size, fg, HorizontalAlignment.Center);
        l.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        l.VerticalAlignment = VerticalAlignment.Center;
        p.AddChild(l);
        return p;
    }

    private static float LineH(Font f, int size) => f.GetHeight(size) + 2f;

    private static Vector2 Measure(Font f, string s, float w, int size) =>
        f.GetMultilineStringSize(s, HorizontalAlignment.Left, w, size, -1,
            TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound
            | TextServer.LineBreakFlag.GraphemeBound);

    /// <summary>들어갈 때까지 글자를 줄인다 — 본문을 자르지 않기 위한 유일한 수단 (웹판 fitTexts 이식)</summary>
    private static int FitSize(Font f, string s, float w, float h, int start, int min)
    {
        for (int size = start; size > min; size--)
        {
            var m = Measure(f, s, w, size);
            int lines = Mathf.Max(1, Mathf.RoundToInt(m.Y / Mathf.Max(1f, f.GetHeight(size))));
            if (lines * (f.GetHeight(size) + 2f) - 2f <= h) return size;
        }
        return min;
    }
}
