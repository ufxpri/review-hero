// 만물대장 도감 — 내가 이 장부에서 **직접 확인한 부분** (ADR-029 4차 · worldview §1.1).
//
// ── 이 화면의 정체 ──────────────────────────────────────
// 만물대장은 「존재하는 모든 것은 리뷰 카드를 가진다」는 세계의 장부다(§1.1 기록 원칙).
// 그러니 이 화면은 **수집품 목록이 아니다.** 장부는 처음부터 전부 적혀 있고, 비어 있는 것은
// 대장이 아니라 플레이어 쪽이다 — 아직 그 항목을 손에 들어 본 적이 없을 뿐이다.
// 그래서 못 본 칸도 **자리는 있다.** 다만 내용이 안 읽힌다.
//
// 못 본 항목이 노출하는 것은 **대상과 계열까지**다. 그 둘은 대장의 색인(어느 상품군의
// 어느 논점인가)이라 목록에 서 있는 것만으로 드러나지만, 이름·본문·비용·태그·원산지는
// 그 항목을 직접 손에 넣어야 읽힌다.
//
// ── 원천 ────────────────────────────────────────────────
// 등재 여부: RunStore.Meta.Seen (카드를 손에 넣는 모든 경로가 RunStore.RecordSeen 을 지난다)
// 카드 정본: GameData.All (Loader 직접 호출 금지 — 내보낸 빌드에서 죽는다)
//
// ── 분류 ────────────────────────────────────────────────
// 66장을 한 화면에 늘어놓으면 못 쓴다. 축을 셋 둔다 —
//   대상별  전체 / 적 본체 / 적 구성품 / 내 장비 / 진상 화법   (무엇을 겨누는 리뷰인가)
//   계열별  전체 / 품질 / 성능 / 배송 / 감성                   (무슨 논점인가 · GDD §3.5)
//   등재별  전체 / 등재됨 / 미등재                             (내가 봤는가)
// 대상이 첫 축인 이유는 카드 데이터의 1차 분포가 대상이기 때문이다(cards-v2.0.yaml 머리말).
//
// ── 디버그 (스크린샷 검증용) ────────────────────────────
//   --rh-codex=full   전 항목 등재 상태로 그린다
//   --rh-codex=some   절반쯤 등재된 상태로 그린다
//   --rh-codex=empty  첫 실행(0장) 상태로 그린다
// 전부 **메모리 위에서만** Seen 을 갈아 끼우고 Save() 를 부르지 않는다.

using Godot;
using ReviewHero.Engine;
using ReviewHero.Game.Combat;   // CombatArt — 색·조각 사전 / CombatEntry — 인자 파서
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Meta;

public partial class Codex : Control
{
    private const int W = CombatArt.ScreenW;   // 1344
    private const int H = CombatArt.ScreenH;   // 768

    private const int ColX = 32, ColW = W - ColX * 2;   // 32 / 1280
    private const int HeadY = 52, HeadH = 92;
    private const int FiltY = 152, FiltH = 38;
    private const int BodyY = 200, BodyH = 520;

    private const int GridW = 956;
    private const int DetX = ColX + GridW + 12, DetW = ColW - GridW - 12;   // 1000 / 312

    private const int Pad = 14;
    private const int TileW = 142, TileH = 84, TileGap = 12, Cols = 6;

    private static readonly Color Mask = new("6f675a");
    private static readonly Color Hatch = new(0.42f, 0.38f, 0.31f, 0.35f);
    private static readonly Color CardInk = new("2b2119");
    private static readonly Color CardDim = new("6d5f45");

    /// <summary>계열 색 — 목록에서 논점을 눈으로 가르는 유일한 단서다</summary>
    private static Color SuitColor(Suit s) => s switch
    {
        Suit.품질 => new Color("c9a227"),
        Suit.성능 => new Color("6f9bc9"),
        Suit.배송 => new Color("5f9e6d"),
        _ => new Color("b083c2"),
    };

    // ── 필터 상태 ────────────────────────────────────

    private int _cat;      // 0 전체 / 1 적 본체 / 2 적 구성품 / 3 내 장비 / 4 진상 화법
    private int _suit;     // 0 전체 / 1 품질 / 2 성능 / 3 배송 / 4 감성
    private int _own;      // 0 전체 / 1 등재됨 / 2 미등재
    private string? _sel;

    private HashSet<string> _seen = new(StringComparer.Ordinal);
    private List<CardDef> _cards = new();

    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();
        UiTheme.Apply(this);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _cards = GameData.Cards.ById.Values.OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        ApplyDemo(Godot.OS.GetCmdlineUserArgs());
        _seen = RunStore.Meta.Seen.ToHashSet(StringComparer.Ordinal);

        Build();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) SceneRouter.Go(SceneRouter.Title);
    }

    /// <summary>캡처용 — Seen 을 제자리에서 갈아 끼운다. Save() 는 절대 부르지 않는다</summary>
    private void ApplyDemo(IReadOnlyList<string> args)
    {
        string? mode = CombatEntry.ArgValue(args, "codex");
        if (mode is null) return;
        var seen = RunStore.Meta.Seen;
        seen.Clear();
        if (mode == "full") seen.AddRange(_cards.Select(c => c.Id));
        else if (mode == "some")
        {
            // 실제 플레이와 닮게 — 시작 덱은 전부, 나머지는 띄엄띄엄
            seen.AddRange(GameData.StartingDeck.Distinct());
            for (int i = 0; i < _cards.Count; i += 3)
                if (!seen.Contains(_cards[i].Id)) seen.Add(_cards[i].Id);
        }
    }

    // ── 조립 ─────────────────────────────────────────

    private void Build()
    {
        foreach (var c in GetChildren()) c.QueueFree();

        var bg = new ColorRect { Color = CombatArt.Bg, MouseFilter = MouseFilterEnum.Ignore };
        bg.At(0, 0, W, H);
        AddChild(bg);

        AddChild(Hub.TopBar("codex", RunStore.Current));
        AddChild(Header());
        AddChild(Filters());

        var shown = Filtered();
        if (_sel is null || shown.All(c => c.Id != _sel)) _sel = shown.FirstOrDefault()?.Id;

        AddChild(Grid(shown));
        AddChild(Detail(_sel));

        var foot = CombatArt.Text(
            "대장은 처음부터 전부 적혀 있다. 비어 있는 것은 당신이 아직 손에 들어 보지 못한 칸이다 — 그 칸도 자리는 있다.",
            12, new Color(0.44f, 0.40f, 0.35f), HorizontalAlignment.Center);
        foot.At(ColX, 732, ColW, 20);
        AddChild(foot);
    }

    // ── 머리 — 이 장부가 무엇이고 내가 어디까지 봤는가 ─

    private Control Header()
    {
        var p = Hub.Panel(ColX, HeadY, ColW, HeadH);

        p.AddChild(CombatArt.Text("만물대장", 21, CombatArt.Gold).At(Pad + 4, 12, 300, 26));
        p.AddChild(CombatArt.Text("존재하는 모든 것은 리뷰 카드를 가진다. 예외는 없다.",
            13, CombatArt.Dim).At(Pad + 4, 44, 700, 20));
        p.AddChild(CombatArt.Text("여기 비어 있는 칸은 대장의 결락이 아니라 당신의 미확인이다.",
            13, CombatArt.Dim).At(Pad + 4, 64, 700, 20));

        // ── 수집률 ──
        int total = _cards.Count;
        int have = _cards.Count(c => _seen.Contains(c.Id));
        float ratio = total > 0 ? have / (float)total : 0f;
        const float bx = ColW - Pad - 400, bw = 400;

        p.AddChild(CombatArt.Text("확인한 항목", 12, CombatArt.Dim).At(bx, 12, 200, 18));
        p.AddChild(CombatArt.Text($"{have}", 26, CombatArt.Ink, HorizontalAlignment.Right)
            .At(bx + bw - 210, 8, 120, 32));
        p.AddChild(CombatArt.Text($"/ {total}종", 13, CombatArt.Dim, HorizontalAlignment.Right)
            .At(bx + bw - 90, 20, 84, 20));

        var bar = new Control { MouseFilter = MouseFilterEnum.Ignore };
        bar.At(bx, 48, bw, 10);
        bar.Draw += () =>
        {
            bar.DrawStyleBox(CombatArt.Box(new Color(0.11f, 0.10f, 0.08f), CombatArt.Edge, 3), new Rect2(0, 0, bw, 10));
            if (ratio > 0)
                bar.DrawStyleBox(CombatArt.Box(new Color("c39a52"), null, 3), new Rect2(0, 0, bw * ratio, 10));
        };
        p.AddChild(bar);

        p.AddChild(CombatArt.Text($"{ratio * 100:0.#}% 확인", 12, CombatArt.Dim, HorizontalAlignment.Right)
            .At(bx, 64, bw, 18));
        return p;
    }

    // ── 분류 막대 ────────────────────────────────────

    private Control Filters()
    {
        var c = Hub.Panel(ColX, FiltY, ColW, FiltH);
        float x = Pad;

        x = Group(c, x, "대상", new[] { "전체", "적 본체", "적 구성품", "내 장비", "진상 화법" }, _cat,
            i => { _cat = i; Rebuild(); });
        x = Sep(c, x);
        x = Group(c, x, "계열", new[] { "전체", "품질", "성능", "배송", "감성" }, _suit,
            i => { _suit = i; Rebuild(); });
        x = Sep(c, x);
        Group(c, x, "등재", new[] { "전체", "등재됨", "미등재" }, _own,
            i => { _own = i; Rebuild(); });
        return c;
    }

    private static float Group(Control host, float x, string label, string[] items, int active, Action<int> pick)
    {
        host.AddChild(CombatArt.Text(label, 12, new Color("8a7c62")).At(x, 11, 40, 18));
        x += 38;
        for (int i = 0; i < items.Length; i++)
        {
            int idx = i;
            float w = 22 + CombatArt.Font().GetStringSize(items[i], HorizontalAlignment.Left, -1, 13).X;
            var b = Hub.NavBtn(items[i], i == active, () => { Audio.Sfx.Play(Audio.SfxId.Click); pick(idx); });
            b.At(x, 6, w, 26);
            host.AddChild(b);
            x += w + 5;
        }
        return x;
    }

    private static float Sep(Control host, float x)
    {
        var line = new ColorRect { Color = CombatArt.Edge with { A = 0.5f }, MouseFilter = MouseFilterEnum.Ignore };
        line.At(x + 6, 9, 1, FiltH - 18);
        host.AddChild(line);
        return x + 18;
    }

    private void Rebuild() => Callable.From(Build).CallDeferred();

    private List<CardDef> Filtered() => _cards.Where(c =>
        (_cat == 0 || CatOf(c) == _cat)
        && (_suit == 0 || (c is ReviewCardDef r && (int)r.Suit + 1 == _suit))
        && (_own == 0 || (_own == 1) == _seen.Contains(c.Id))).ToList();

    // ── 격자 ─────────────────────────────────────────

    private Control Grid(List<CardDef> shown)
    {
        var p = Hub.Panel(ColX, BodyY, GridW, BodyH);

        p.AddChild(CombatArt.Text($"{Title(_cat)} · {shown.Count}종", 13, CombatArt.Gold).At(Pad + 2, 10, 400, 20));
        int have = shown.Count(c => _seen.Contains(c.Id));
        p.AddChild(CombatArt.Text($"이 분류에서 확인 {have} / {shown.Count}", 12, CombatArt.Dim,
            HorizontalAlignment.Right).At(GridW - Pad - 300, 11, 300, 18));

        var line = new ColorRect { Color = CombatArt.Edge, MouseFilter = MouseFilterEnum.Ignore };
        line.At(Pad, 36, GridW - Pad * 2, 1);
        p.AddChild(line);

        if (shown.Count == 0)
        {
            p.AddChild(CombatArt.Text("이 조건에 해당하는 항목이 대장에 없다.", 14, Mask,
                HorizontalAlignment.Center).At(Pad, BodyH / 2f - 10, GridW - Pad * 2, 22));
            return p;
        }

        float viewW = GridW - Pad * 2;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.At(Pad, 46, viewW, BodyH - 46 - Pad);
        p.AddChild(scroll);

        int rows = (shown.Count + Cols - 1) / Cols;
        var canvas = new Control
        {
            CustomMinimumSize = new Vector2(viewW - 16, rows * (TileH + TileGap)),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        scroll.AddChild(canvas);

        for (int i = 0; i < shown.Count; i++)
        {
            var t = Tile(shown[i]);
            t.At((i % Cols) * (TileW + TileGap), (i / Cols) * (TileH + TileGap), TileW, TileH);
            canvas.AddChild(t);
        }
        return p;
    }

    private Control Tile(CardDef d)
    {
        bool seen = _seen.Contains(d.Id);
        bool sel = d.Id == _sel;
        var accent = d is ReviewCardDef r ? SuitColor(r.Suit) : new Color("a3553c");

        // ClipContents 가 없으면 미확인 칸의 빗금이 타일 밖으로 삐져나가 옆 칸을 넘본다
        var c = new Control { ClipContents = true };
        c.CustomMinimumSize = new Vector2(TileW, TileH);
        c.Draw += () =>
        {
            var bg = seen ? new Color(0.13f, 0.12f, 0.09f, 0.95f) : new Color(0.06f, 0.06f, 0.05f, 0.9f);
            c.DrawStyleBox(CombatArt.Box(bg, sel ? CombatArt.Gold : CombatArt.Edge, 5, sel ? 2 : 1),
                new Rect2(0, 0, TileW, TileH));
            // 계열 띠 — 못 본 칸에도 남는다. 대장의 색인이라 자리만으로 드러난다
            c.DrawStyleBox(CombatArt.Box(accent with { A = seen ? 1f : 0.45f }, null, 2), new Rect2(0, 0, 4, TileH));
            if (!seen)
            {
                // 빗금 — 읽히지 않는 칸
                for (float o = -TileH; o < TileW; o += 9)
                    c.DrawLine(new Vector2(o, TileH), new Vector2(o + TileH, 0), Hatch, 1f);
            }
        };

        // 계열·대상 — 확인 여부와 무관하게 보인다
        c.AddChild(CombatArt.Text(SuitLabel(d), 10, accent with { A = seen ? 1f : 0.75f }).At(10, 7, 60, 15));
        c.AddChild(CombatArt.Text(CatLabel(CatOf(d)), 10, Mask, HorizontalAlignment.Right).At(TileW - 82, 7, 74, 15));

        if (seen)
        {
            var name = CombatArt.Text(d.Name, 13, CombatArt.Ink);
            name.ClipText = true;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            name.At(10, 26, TileW - 20, 20);
            c.AddChild(name);
            c.AddChild(Hub.Stars(StarsOf(d), 11).At(10, 50, 70, 16));
            c.AddChild(CombatArt.Text($"✍{d.Cost}", 11, new Color("cdbfa4"), HorizontalAlignment.Right)
                .At(TileW - 46, 50, 36, 16));
        }
        else
        {
            c.AddChild(CombatArt.Text("▨▨▨▨▨", 13, Mask).At(10, 26, TileW - 20, 20));
            c.AddChild(CombatArt.Text("미확인", 11, new Color(0.35f, 0.33f, 0.29f)).At(10, 50, 80, 16));
        }

        var hit = new Button { FocusMode = FocusModeEnum.None, Flat = true };
        hit.AddThemeStyleboxOverride("normal", CombatArt.Box(new Color(0, 0, 0, 0), null, 5));
        hit.AddThemeStyleboxOverride("hover", CombatArt.Box(new Color(1, 1, 1, 0.07f), null, 5));
        hit.AddThemeStyleboxOverride("pressed", CombatArt.Box(new Color(0, 0, 0, 0.25f), null, 5));
        hit.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        hit.Pressed += () => { Audio.Sfx.Play(Audio.SfxId.CardPick); _sel = d.Id; Rebuild(); };
        c.AddChild(hit);
        return c;
    }

    // ── 상세 — 카드 실물 ─────────────────────────────

    private Control Detail(string? id)
    {
        var p = Hub.Panel(DetX, BodyY, DetW, BodyH);
        var def = id is not null && GameData.Cards.ById.TryGetValue(id, out var d) ? d : null;

        if (def is null)
        {
            p.AddChild(CombatArt.Text("선택된 항목이 없다.", 14, Mask, HorizontalAlignment.Center)
                .At(Pad, BodyH / 2f - 10, DetW - Pad * 2, 22));
            return p;
        }

        // 카드 아래로 색인 5줄(110)과 확인 표시(18)가 차례로 선다 — 겹치지 않게 높이를 역산한다
        bool seen = _seen.Contains(def.Id);
        const float cw = DetW - Pad * 2, ch = BodyH - Pad * 2 - 10 - 110 - 8 - 18;
        p.AddChild((seen ? CardFace(def, cw, ch) : SealedFace(def, cw, ch)).At(Pad, Pad, cw, ch));

        // ── 대장 색인 ──
        float y = Pad + ch + 10;
        var rows = new (string K, string V, bool Reveal)[]
        {
            ("대상", CatLabel(CatOf(def)), true),
            ("계열", SuitLabel(def), true),
            ("판정 태그", def is ReviewCardDef r ? $"#{r.Tag}" : "무판정", seen),
            ("원산지", OriginShort(def), seen),
            ("등급", RarityLabel(def), seen),
        };
        foreach (var (k, v, reveal) in rows)
        {
            p.AddChild(CombatArt.Text(k, 11, new Color("8a7c62")).At(Pad, y + 3, 78, 18));
            // 색인 값은 판을 넘지 않는다 — 긴 원산지 이름이 패널 밖으로 흘러 나가던 자리다
            var val = CombatArt.Text(reveal ? v : "▨▨▨▨", 12, reveal ? CombatArt.Ink : Mask);
            val.ClipText = true;
            val.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            val.At(Pad + 82, y + 2, cw - 82, 20);
            p.AddChild(val);
            y += 22;
        }

        p.AddChild(CombatArt.Text(seen ? "✔ 대장에서 직접 확인함" : "미확인 — 손에 넣으면 읽힌다",
            11, seen ? new Color("7db98a") : Mask, HorizontalAlignment.Center)
            .At(Pad, BodyH - Pad - 18, cw, 18));
        return p;
    }

    /// <summary>손에 넣은 항목 — 카드 실물 한 장 (전투 CardView 와 같은 양피지 서식)</summary>
    private static Control CardFace(CardDef def, float w, float h)
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore };
        c.CustomMinimumSize = new Vector2(w, h);

        bool special = def is SpecialDef;
        c.Draw += () =>
        {
            var s = CombatArt.Box(CombatArt.Parch, special ? new Color("a3301c") : CombatArt.ParchD, 9, 1);
            if (special) s.BorderWidthTop = 4;
            c.DrawStyleBox(s, new Rect2(0, 0, w, h));
        };

        const float padX = 14f;
        float innerW = w - padX * 2f;
        var font = CombatArt.Font();

        // 아래에서부터 자리를 잡는다 — 본문이 남는 자리를 전부 먹는다 (CardView 와 같은 순서)
        const float footH = 22f;
        float footTop = h - 12f - footH;

        string uiLine = CombatSession.Likeify(def.Ui);
        float uiH = Mathf.Min(52f, Measure(font, uiLine, innerW, 12).Y + 2f);
        float uiTop = footTop - 8f - uiH;

        const float originH = 16f;
        float originTop = uiTop - 6f - originH;

        float y = 12f;
        c.AddChild(Hub.Stars(StarsOf(def), 14).At(padX, y, 92, 18));
        c.AddChild(CombatArt.Text(special ? "무판정" : RarityLabel(def), 11,
            special ? new Color("a3301c") : new Color("8a6f3f"), HorizontalAlignment.Right)
            .At(w - padX - 120, y + 2, 120, 16));
        y += 24f;

        float nameH = Mathf.Min(56f, Measure(font, def.Name, innerW, 17).Y + 2f);
        var name = Wrapped(def.Name, 17, new Color("1c1812"));
        name.At(padX, y, innerW, nameH);
        c.AddChild(name);
        y += nameH + 6f;

        string body = (def.Text ?? string.Empty).Trim();
        if (body.Length == 0) body = uiLine;
        float bodyH = Mathf.Max(30f, originTop - 6f - y);
        var text = Wrapped(body, FitSize(font, body, innerW, bodyH, 13, 9), new Color("3a3229"));
        text.At(padX, y, innerW, bodyH);
        c.AddChild(text);

        c.AddChild(CombatArt.Text(OriginLine(def), 11,
            def is ReviewCardDef { Origin: not null } ? new Color("8a6f3f") : new Color("a2947a"))
            .At(padX, originTop, innerW, originH));

        var ui = Wrapped(uiLine, 12, new Color("5a4c34"));
        ui.At(padX, uiTop, innerW, uiH);
        c.AddChild(ui);

        var div = new ColorRect { Color = CombatArt.ParchD with { A = 0.8f }, MouseFilter = MouseFilterEnum.Ignore };
        div.At(padX, footTop - 6f, innerW, 1);
        c.AddChild(div);

        string foot = def is ReviewCardDef rr ? $"#{rr.Tag}" : "진상 화법";
        c.AddChild(CombatArt.Text(foot, 12, CardInk).At(padX, footTop + 2f, innerW - 60, 18));
        c.AddChild(CombatArt.Text($"✍{def.Cost}", 13, CardInk, HorizontalAlignment.Right)
            .At(padX, footTop + 1f, innerW, 20));
        return c;
    }

    /// <summary>못 본 항목 — 자리는 있으나 읽히지 않는다. 대상·계열까지만 남는다</summary>
    private static Control SealedFace(CardDef def, float w, float h)
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        c.CustomMinimumSize = new Vector2(w, h);
        var accent = def is ReviewCardDef r ? SuitColor(r.Suit) : new Color("a3553c");

        c.Draw += () =>
        {
            c.DrawStyleBox(CombatArt.Box(new Color(0.09f, 0.08f, 0.07f, 0.95f), CombatArt.Edge, 9),
                new Rect2(0, 0, w, h));
            for (float o = -h; o < w; o += 12)
                c.DrawLine(new Vector2(o, h), new Vector2(o + h, 0), Hatch with { A = 0.22f }, 1f);
            c.DrawStyleBox(CombatArt.Box(accent with { A = 0.5f }, null, 3), new Rect2(0, 0, w, 5));
        };

        c.AddChild(CombatArt.Text("▨▨▨▨▨▨", 22, Mask, HorizontalAlignment.Center).At(0, 96, w, 30));
        c.AddChild(CombatArt.Text("미확인 항목", 14, new Color("8a7c62"), HorizontalAlignment.Center)
            .At(0, 140, w, 22));

        var note = CombatArt.Text(
            "대장에는 이미 적혀 있다. 다만 당신이 이 항목을 손에 들어 본 적이 없어 읽히지 않는다.",
            12, new Color(0.42f, 0.39f, 0.34f), HorizontalAlignment.Center, wrap: true);
        note.At(20, 176, w - 40, 60);
        c.AddChild(note);

        c.AddChild(CombatArt.Text($"색인 · {CatLabel(CatOf(def))} · {SuitLabel(def)}", 12, accent,
            HorizontalAlignment.Center).At(0, 248, w, 20));
        c.AddChild(CombatArt.Text("이름 · 본문 · 비용 · 태그 · 원산지는 가려져 있다", 11, Mask,
            HorizontalAlignment.Center).At(0, 274, w, 18));
        return c;
    }

    // ── 이름표 ───────────────────────────────────────

    private static int CatOf(CardDef d) => d switch
    {
        SpecialDef => 4,
        ReviewCardDef { Target: TargetKind.EnemyEquipment } => 2,
        ReviewCardDef { Target: TargetKind.MyEquipment } => 3,
        _ => 1,
    };

    private static string CatLabel(int cat) => cat switch
    {
        1 => "적 본체",
        2 => "적 구성품",
        3 => "내 장비",
        _ => "진상 화법",
    };

    private static string Title(int cat) => cat == 0 ? "전체 항목" : CatLabel(cat);

    private static string SuitLabel(CardDef d) => d is ReviewCardDef r ? r.Suit.ToString() : "무계열";

    private static int StarsOf(CardDef d) => d switch
    {
        ReviewCardDef r => r.Stars,
        SpecialDef s => s.Stars ?? 1,
        _ => 1,
    };

    private static string RarityLabel(CardDef d)
    {
        var rar = d switch { ReviewCardDef r => (Rarity?)r.Rarity, SpecialDef s => s.Rarity, _ => null };
        return rar switch
        {
            Rarity.Basic => "기본",
            Rarity.Common => "일반",
            Rarity.Rare => "희귀",
            Rarity.Legendary => "전설",
            _ => "—",
        };
    }

    /// <summary>이 리뷰가 태어난 상품 (원산지 판정의 근거 — card-system-v2 §2)</summary>
    private static string OriginLine(CardDef d)
    {
        if (d is not ReviewCardDef r) return "📍 원산지 없음 · 진상 화법은 판정을 받지 않는다";
        if (r.Origin?.Equipment is { } q) return $"📍 원산지 · {q}";
        if (r.Origin?.Enemy is { } e) return $"📍 원산지 · {GameData.EnemyName(e)}";
        return "📍 원산지 없음 · 전생에 쓴 리뷰";
    }

    /// <summary>색인 줄에 들어가는 짧은 형태 (카드 본문의 긴 문장은 판을 넘는다)</summary>
    private static string OriginShort(CardDef d)
    {
        if (d is not ReviewCardDef r) return "없음 — 진상 화법";
        if (r.Origin?.Equipment is { } q) return q;
        if (r.Origin?.Enemy is { } e) return GameData.EnemyName(e);
        return "없음 — 전생에 쓴 리뷰";
    }

    // ── 잡일 ─────────────────────────────────────────

    private static Label Wrapped(string s, int size, Color c)
    {
        var l = CombatArt.Text(s, size, c, wrap: true);
        l.AddThemeConstantOverride("line_spacing", 2);
        l.ClipText = false;
        return l;
    }

    private static Vector2 Measure(Font f, string s, float w, int size) =>
        f.GetMultilineStringSize(s, HorizontalAlignment.Left, w, size, -1,
            TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound
            | TextServer.LineBreakFlag.GraphemeBound);

    /// <summary>본문을 자르지 않기 위해 들어갈 때까지 글자를 줄인다 (CardView 와 같은 규칙)</summary>
    private static int FitSize(Font f, string s, float w, float h, int start, int min)
    {
        for (int size = start; size > min; size--)
            if (Measure(f, s, w, size).Y <= h) return size;
        return min;
    }
}
