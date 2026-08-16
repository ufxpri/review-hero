// 비전투 노드 3종(이벤트·상점·휴식)의 공통 뼈대 — ADR-029 3차.
//
// 웹판은 event/shop/rest.html 세 페이지가 상단 칩·양피지 패널·덱 목록 모달을 각자 복사해
// 들고 있었다(shared.css 로도 못 묶인 부분이 그대로 세 번 반복됐다). 여기서는 그 셋을
// 한 자리에 모은다 — 화면 틀(칩 줄 + 본문 스크롤), 양피지/패널 스타일, 덱 목록 모달,
// 카드 미니 뷰, 그리고 **노드 문맥 난수**다.
//
// ── 미리보기 폴백 (웹판 CONTRACT §확인 절차) ────────────────
// 런이 없거나 노드에 발을 들이지 않은 채로 열리면 화면이 죽는 대신 시연용 런으로 그린다.
// 저장은 일절 하지 않으며 「런 없음 — 미리보기」 칩이 붙는다. 화면 확인(스크린샷)이
// 세이브 상태에 의존하지 않게 하는 것이 목적이다.

using Godot;
using ReviewHero.Engine;
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Nodes;

public abstract partial class NodeScene : Control
{
    // ── 색 (웹판 shared.css 의 --gold/--ink/--dim/--parch 대응) ──
    protected static readonly Color Gold = new(0.88f, 0.66f, 0.29f);
    protected static readonly Color Dim = new(0.62f, 0.60f, 0.56f);
    protected static readonly Color Warn = new(0.85f, 0.42f, 0.38f);
    protected static readonly Color Good = new(0.55f, 0.80f, 0.55f);
    protected static readonly Color Ink = new(0.11f, 0.09f, 0.07f);
    protected static readonly Color InkDim = new(0.30f, 0.26f, 0.20f);
    protected static readonly Color Brown = new(0.54f, 0.44f, 0.25f);

    protected RunState Run = null!;
    protected MapNode Node = null!;

    /// <summary>런/노드가 없어 시연용 런으로 그리는 중 — 어떤 저장도 하지 않는다</summary>
    protected bool Preview;

    /// <summary>본문이 쌓이는 자리 (스크롤 안)</summary>
    protected VBoxContainer Body = null!;

    private Label? _willLbl;
    private Label? _goldLbl;
    private Label? _deckLbl;
    private Control? _overlay;

    /// <summary>이 씬이 맡는 노드 종류 — 칩 머리글과 미리보기용 가짜 노드에 쓴다</summary>
    protected abstract NodeType Kind { get; }

    /// <summary>노드 문맥 난수의 소금 ("event" | "shop" | "rest")</summary>
    protected abstract string PageKey { get; }

    /// <summary>본문을 그린다 (<see cref="Body"/> 에 붙인다)</summary>
    protected abstract void Build();

    // 상점은 장바구니를 들고 있어 표시값이 런과 다르다 — 칩은 이 셋만 본다
    protected virtual int ShownWill => Run.Will;
    protected virtual int ShownGold => Run.Gold;
    protected virtual int ShownDeck => Run.Deck.Count;

    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();
        UiTheme.Apply(this);

        var run = RunStore.Current;
        var node = run?.CurrentNode;
        Preview = run is null || node is null;
        Run = run ?? DemoRun();
        Node = node ?? new MapNode { Id = "preview", Type = Kind };

        BuildFrame();
        Build();
        PaintHud();
    }

    /// <summary>런 없이 열렸을 때 화면을 채우는 시연용 런 (저장되지 않는다)</summary>
    private static RunState DemoRun()
    {
        // 시작 덱 12장은 전부 생계형 리뷰(제거 불가)다 — 그것만 넣으면 파쇄·소각 화면이
        // 「고를 게 없다」로만 보인다. 확인용으로 제거 가능한 카드 두 장을 얹는다.
        var deck = GameData.StartingDeck.ToList();
        deck.AddRange(ReviewPool.Take(2));
        return new RunState { Seed = 42, Floor = 3, Gold = 60, Will = 21, MaxWill = 30, Deck = deck };
    }

    /// <summary>
    /// 한 줄짜리 표기(칩·머리글). <see cref="UiTheme.Text"/> 는 자동 줄바꿈이 켜져 있어
    /// HBox 안에서는 최소 폭이 한 글자가 된다 — 가로줄에 놓을 글은 줄바꿈을 꺼야 눌리지 않는다.
    /// </summary>
    protected static Label Line(string s, int size = 18, Color? color = null)
    {
        var l = UiTheme.Text(s, size, color);
        l.AutowrapMode = TextServer.AutowrapMode.Off;
        return l;
    }

    // ── 화면 틀 ──────────────────────────────────────

    private void BuildFrame()
    {
        foreach (var c in GetChildren()) c.QueueFree();

        var pad = new MarginContainer();
        pad.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            pad.AddThemeConstantOverride(side, 16);
        AddChild(pad);

        var root = UiTheme.VBox(10);
        pad.AddChild(root);

        var chips = UiTheme.HBox(18);
        chips.AddChild(Line($"{Kind.Icon()} {Kind.Label()}", 24, Gold));
        chips.AddChild(Line($"1막 {Run.Floor}층", 20));
        _willLbl = Line("", 20);
        _goldLbl = Line("", 20);
        _deckLbl = Line("", 20);
        chips.AddChild(_willLbl);
        chips.AddChild(_goldLbl);
        chips.AddChild(_deckLbl);
        chips.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        if (Preview) chips.AddChild(Line("런 없음 — 미리보기", 14, Warn));
        chips.AddChild(Line($"시드 {Run.Seed}", 14, Dim));
        chips.AddChild(UiTheme.Btn("타이틀", SceneRouter.GoTitle, size: 14));
        root.AddChild(chips);
        root.AddChild(new HSeparator());

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        root.AddChild(scroll);

        Body = UiTheme.VBox(12);
        Body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(Body);
    }

    /// <summary>골드·의지·덱 칩 갱신 (상점의 구매 직후에 부른다)</summary>
    protected void PaintHud()
    {
        if (_willLbl is not null) _willLbl.Text = $"🧠 의지 {ShownWill}/{Run.MaxWill}";
        if (_goldLbl is not null) _goldLbl.Text = $"🪙 골드 {ShownGold}";
        if (_deckLbl is not null) _deckLbl.Text = $"🃏 덱 {ShownDeck}장";
    }

    // ── 조각 ────────────────────────────────────────

    /// <summary>자식을 즉시 떼어 낸다. QueueFree 만 하면 이번 프레임에 옛 자식이 같이 그려진다</summary>
    protected static void Clear(Godot.Node n)
    {
        foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); }
    }

    protected static StyleBoxFlat ParchStyle()
    {
        var sb = new StyleBoxFlat { BgColor = Color.FromHtml("e7dbb4"), BorderColor = Color.FromHtml("a78f5f") };
        sb.SetBorderWidthAll(1);
        sb.SetCornerRadiusAll(4);
        sb.ContentMarginLeft = sb.ContentMarginRight = 18;
        sb.ContentMarginTop = sb.ContentMarginBottom = 16;
        return sb;
    }

    protected static StyleBoxFlat PanelStyle()
    {
        var sb = new StyleBoxFlat { BgColor = Color.FromHtml("1a1713f2"), BorderColor = Color.FromHtml("3d352a") };
        sb.SetBorderWidthAll(1);
        sb.SetCornerRadiusAll(4);
        sb.ContentMarginLeft = sb.ContentMarginRight = 16;
        sb.ContentMarginTop = sb.ContentMarginBottom = 14;
        return sb;
    }

    /// <summary>양피지 위의 이야기 한 덩이 — 본문 폭을 책 한 단으로 묶는다</summary>
    protected static PanelContainer Parch(string body, int width = 900, int size = 16)
    {
        var p = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        p.AddThemeStyleboxOverride("panel", ParchStyle());
        p.CustomMinimumSize = new Vector2(width, 0);
        var l = UiTheme.Text(body, size, Ink);
        l.CustomMinimumSize = new Vector2(width - 40, 0);
        p.AddChild(l);
        return p;
    }

    protected static PanelContainer Panel(Control inner, int width = 0)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", PanelStyle());
        if (width > 0) p.CustomMinimumSize = new Vector2(width, 0);
        p.AddChild(inner);
        return p;
    }

    /// <summary>가운데 한 단으로 모으는 자리 (본문 폭 고정)</summary>
    protected static VBoxContainer Column(int width = 900, int sep = 12)
    {
        var v = UiTheme.VBox(sep);
        v.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        v.CustomMinimumSize = new Vector2(width, 0);
        return v;
    }

    // ── 카드 표기 ────────────────────────────────────

    /// <summary>GDD §2 규칙 7 — UI 에서 피해는 예외 없이 「좋아요」 (data 표기를 표시 시점에 바꾼다)</summary>
    protected static string Likeify(string? s) => (s ?? "")
        .Replace("의지 데미지", "좋아요")
        .Replace("의지 피해", "좋아요")
        .Replace("데미지", "좋아요")
        .Replace("피해량", "좋아요 수")
        .Replace("피해", "좋아요");

    protected static string KindLabel(CardDef c) => c.Kind == CardKind.Review ? "리뷰" : "진상 화법";

    /// <summary>덱 목록 한 줄 표기 — 「이름」 리뷰 · 품질 · ✍2</summary>
    protected static string CardLine(string id)
    {
        if (!GameData.Cards.ById.TryGetValue(id, out var c)) return id;
        string extra = c is ReviewCardDef r ? $" · {r.Suit}" : "";
        return $"「{c.Name}」  {KindLabel(c)}{extra} · ✍{c.Cost}";
    }

    /// <summary>상점 진열용 카드 미니 뷰 (양피지)</summary>
    protected static Control CardMini(string id, int width = 300)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", ParchStyle());
        p.CustomMinimumSize = new Vector2(width, 168);

        var v = UiTheme.VBox(6);
        p.AddChild(v);

        if (!GameData.Cards.ById.TryGetValue(id, out var c))
        {
            v.AddChild(UiTheme.Text(id, 15, Ink));
            return p;
        }

        var meta = new List<string> { KindLabel(c) };
        int stars = c is ReviewCardDef rc ? rc.Stars : (c as SpecialDef)?.Stars ?? 0;
        if (stars > 0) meta.Add(new string('★', stars));
        if (c is ReviewCardDef r2) meta.Add("#" + r2.Tag);

        var head = UiTheme.HBox(6);
        head.AddChild(Line(string.Join(" · ", meta), 11, InkDim));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        head.AddChild(Line($"✍{c.Cost}", 13, Ink));
        v.AddChild(head);

        var name = UiTheme.Text(c.Name, 17, Ink);
        v.AddChild(name);

        var text = UiTheme.Text(c.Text?.TrimEnd() ?? "…내용은 써 봐야 안다.", 12, InkDim);
        text.SizeFlagsVertical = SizeFlags.ExpandFill;
        v.AddChild(text);

        if (!string.IsNullOrEmpty(c.Ui)) v.AddChild(UiTheme.Text(Likeify(c.Ui), 11, Brown));
        return p;
    }

    // ── 덱 목록 모달 (파쇄 / 소각 공용) ────────────────

    /// <summary>
    /// 덱에서 카드 1장을 고른다. <b>생계형 리뷰(Irremovable)는 비활성</b> — 전생의 기록은 갈리지도
    /// 타지도 않는다 (GDD §3.6). 고르면 <paramref name="onPick"/> 에 덱 인덱스를 넘긴다.
    /// </summary>
    protected void OpenDeckPicker(string title, string hint, string okText,
                                  IReadOnlyList<string> deck, Action<int> onPick)
    {
        CloseOverlay();
        var irre = GameData.All.Irremovable;
        int picked = -1;
        var items = new List<Button>();

        var layer = new Control { MouseFilter = MouseFilterEnum.Stop };
        layer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(layer);
        _overlay = layer;

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.72f) };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        layer.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        layer.AddChild(center);

        var box = UiTheme.VBox(10);
        var panel = Panel(box, 980);
        center.AddChild(panel);

        box.AddChild(UiTheme.Text(title, 24, Gold));
        box.AddChild(UiTheme.Text(hint, 14, Dim));

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(940, 360) };
        box.AddChild(scroll);
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 6);
        scroll.AddChild(grid);

        var ok = UiTheme.Btn(okText, null, enabled: false, size: 18);

        for (int i = 0; i < deck.Count; i++)
        {
            int idx = i;
            bool locked = irre.Contains(deck[i]);
            string label = CardLine(deck[i]) + (locked ? "  — 생계형 리뷰 · 제거 불가" : "");
            var b = UiTheme.Btn(label, null, enabled: !locked, size: 14);
            b.Alignment = HorizontalAlignment.Left;
            b.CustomMinimumSize = new Vector2(455, 34);
            if (locked) b.Modulate = new Color(1f, 1f, 1f, 0.45f);
            else
                b.Pressed += () =>
                {
                    picked = idx;
                    foreach (var x in items) if (!x.Disabled) x.Modulate = new Color(0.72f, 0.72f, 0.75f);
                    b.Modulate = Colors.White;
                    ok.Disabled = false;
                };
            items.Add(b);
            grid.AddChild(b);
        }

        var row = UiTheme.HBox(10);
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        row.AddChild(UiTheme.Btn("그만둔다", CloseOverlay, size: 18));
        ok.Pressed += () =>
        {
            if (picked < 0) return;
            if (irre.Contains(deck[picked])) return;   // 비활성의 이중 안전장치
            CloseOverlay();
            onPick(picked);
        };
        row.AddChild(ok);
        box.AddChild(row);
    }

    protected void CloseOverlay()
    {
        if (_overlay is null) return;
        RemoveChild(_overlay);
        _overlay.QueueFree();
        _overlay = null;
    }

    // ── 노드 문맥 난수 ───────────────────────────────

    /// <summary>
    /// 이 노드 전용 난수. **런 시드와 노드 id 에서만 파생한다** — 같은 런의 같은 노드를 다시 열면
    /// 같은 이벤트·같은 진열이 나온다. 웹판이 run.nodeCtx 에 첫 결과를 적어 두어 리롤을 막던 것을,
    /// 여기서는 애초에 굴릴 때마다 같은 수가 나오게 해서 해결한다(저장할 것이 없다).
    /// Godot.GD.Randi 는 게임 규칙에 쓰지 않는다 (CLAUDE.md · GDD §8-3).
    /// </summary>
    protected Rng NodeRng() => RngFactory.Mulberry32(unchecked(Run.Seed + Fnv1a($"{PageKey}:{Node.Id}")));

    private static uint Fnv1a(string s)
    {
        unchecked
        {
            uint h = 2166136261u;
            foreach (char ch in s) { h ^= ch; h *= 16777619u; }
            return h;
        }
    }

    /// <summary>
    /// 획득 가능한 리뷰 카드 목록. **생계형 리뷰(Z01~Z12)는 뺀다** — 전생에 내가 쓴 리뷰라
    /// 「주인 잃은 문장」으로 팔릴 물건이 아니고, 사 봐야 제거도 안 되는 카드가 덱에 쌓인다.
    /// (웹판 event/shop.html 은 kind 로만 걸러 Z## 이 섞여 들어갔다 — 이관하며 바로잡았다.)
    /// </summary>
    protected static IReadOnlyList<string> ReviewPool => Pool(CardKind.Review);

    protected static IReadOnlyList<string> SpecialPool => Pool(CardKind.Special);

    private static List<string> Pool(CardKind kind)
    {
        var irre = GameData.All.Irremovable;
        // AllIds 순서는 로더가 고정한다 — 같은 시드에서 같은 카드가 나오려면 이 순서를 써야 한다
        return GameData.Cards.AllIds
            .Where(id => !irre.Contains(id) && GameData.Cards.ById[id].Kind == kind)
            .ToList();
    }

    protected static string PickFrom(IReadOnlyList<string> pool, Rng r, params string[] exclude)
    {
        for (int guard = 0; guard < 64; guard++)
        {
            string id = pool[(int)Math.Floor(r() * pool.Count)];
            if (Array.IndexOf(exclude, id) < 0) return id;
        }
        return pool[0];
    }

    // ── 노드 종료 ────────────────────────────────────

    /// <summary>
    /// 노드를 끝내고 다음 씬으로. 런 갱신은 전부 <see cref="RunStore.CompleteNode"/> 한 곳을 지난다.
    /// 미리보기에서는 아무것도 저장하지 않고 지도(또는 타이틀)로 돌아간다.
    /// </summary>
    protected void Finish(int gold = 0, int will = 0, string? deckAdd = null, int? deckRemoveIdx = null)
    {
        if (Preview) { SceneRouter.Go(RunStore.Current is null ? SceneRouter.Title : SceneRouter.Map); return; }
        string next = RunStore.CompleteNode(gold: gold, will: will, deckAdd: deckAdd, deckRemoveIdx: deckRemoveIdx);
        SceneRouter.Go(next);
    }
}
