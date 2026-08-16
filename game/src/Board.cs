// 원정대 명단 — 죽은 대원들의 마지막 리뷰 (ADR-029 4차 · worldview §1.7).
//
// 이관 원본: ui/game/board.html.
//
// ── 웹판에서 고친 것 ────────────────────────────────────
// 웹판은 NPC 8명을 board.html 스크립트 안에 박아 두었고, 홈(index.html)이 「상위 3인」을
// 보여주려고 그중 3명을 **손으로 복제**해 두 파일이 따로 늙었다(주석에도 "board.html 명단
// 상위 3인의 사본"이라 적혀 있다). 여기서는 명단 데이터와 병합·정렬 규칙을
// <see cref="Roster"/> 하나가 소유하고, 홈 프리뷰는 <see cref="Roster.Top"/> 를 부른다.
//
// 상단 내비(<see cref="Hub"/>)도 지도·명단이 같이 쓰는 조각이라 여기 함께 둔다 —
// 웹판 shared.css 의 topbar 에 해당한다. (메인 허브 index.html 은 topbar 가 없는 유일한 화면이라
// 여기 조각을 쓰지 않는다 — 대신 하단 내비를 가진다.)

using Godot;
using ReviewHero.Game.Combat;   // CombatArt — 색·조립 조각의 정본
using ReviewHero.Game.Run;

namespace ReviewHero.Game;

/// <summary>
/// 명단 한 줄 — NPC 선배 대원과 내 지난 런을 같은 모양으로 다룬다.
/// Status 는 "게시" | "계류" | "유실", Fate 는 "전사" | "생환" | "두절".
/// </summary>
public sealed record RosterRow(
    string Name, int Floor, int Stars, string Status, string Fate, string Date, string Review, bool Me = false);

/// <summary>
/// 환불원정대 명단의 정본. **다른 화면(메인 허브 프리뷰)도 여기서만 읽는다** —
/// 웹판이 홈과 명단에 같은 배열을 두 벌 두었다가 어긋난 자리다.
/// </summary>
public static class Roster
{
    /// <summary>
    /// NPC 선배 대원 — 전원 다른 세계에서 불려 온 익명 리뷰어. 전원 사망 또는 소식 두절.
    /// (worldview §1.7 — "살아 있는 사람은 주인공 하나다")
    /// </summary>
    public static readonly IReadOnlyList<RosterRow> Npcs = new RosterRow[]
    {
        new("배송조회중독자", 6, 1, "게시", "전사", "2026-05-02",
            "주문하신 복수는 배송 중 분실되었습니다. 수령인도 함께요."),
        new("미개봉철수", 5, 2, "게시", "전사", "2026-03-18",
            "미개봉 새 상품인데 중고로 반품됩니다. 저 말입니다."),
        new("반품테러리스트", 5, 1, "유실", "두절", "2025-08-14", ""),
        new("최저가비교왕", 4, 2, "게시", "전사", "2026-01-27",
            "목숨 최저가 비교하다 품절됐습니다. 재입고 문의는 저승으로."),
        new("리뷰쓰면오백원", 3, 1, "계류", "전사", "2025-11-09",
            "오백 원 받고 솔직하게 씁니다. 여긴 오지 마세요. 진심입니다."),
        new("교환말고환불", 3, 3, "유실", "두절", "2025-12-30", ""),
        new("구매확정안누름", 2, 1, "계류", "전사", "2026-06-21",
            "구매확정은 끝까지 안 눌렀는데 인생이 확정됐네요."),
        new("새벽배송희생자", 1, 1, "게시", "전사", "2026-02-05",
            "문 앞에 두고 가라니까 저를 문 앞에 두고 갔습니다."),
    };

    /// <summary>내 지난 런(meta.Expedition) + NPC → 도달 층 내림차순, 동률이면 게시일 최신</summary>
    public static List<RosterRow> All()
    {
        var mine = RunStore.Meta.Expedition.Select(e => new RosterRow(
            string.IsNullOrWhiteSpace(e.Name) ? "무명" : e.Name,
            e.Floor,
            e.Stars,
            string.IsNullOrEmpty(e.Status) ? "게시" : e.Status,
            e.Result == "clear" ? "생환" : "전사",
            e.Date ?? "",
            (e.Review ?? "").Split('\n')[0],
            Me: true));

        return Npcs.Concat(mine)
            .OrderByDescending(r => r.Floor)
            .ThenByDescending(r => r.Date, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>상위 n인 — 메인 허브의 명단 프리뷰가 쓴다(같은 순위가 나오는 유일한 이유)</summary>
    public static List<RosterRow> Top(int n) => All().Take(n).ToList();

    public static string FateLabel(string fate) => fate == "두절" ? "소식 두절" : fate;

    public static Color FateColor(string fate) => fate switch
    {
        "생환" => CombatArt.Gold,
        "두절" => new Color(0.44f, 0.40f, 0.35f),
        _ => new Color(0.69f, 0.42f, 0.33f),
    };
}

public partial class Board : Control
{
    private const int W = 1344, H = 768;
    private const int ColW = 1160;
    private const int ColX = (W - ColW) / 2;

    // 표 열 폭 (board.html grid-template-columns)
    private const int CRank = 44, CPen = 168, CReach = 96, CStars = 104, CStatus = 62, CDate = 88, Gap = 10;
    private const int Pad = 16;

    /// <summary>표 한 줄의 폭 — 세로 스크롤바(14px)가 게시일을 갉지 않게 미리 뺀다</summary>
    private const int RowW = ColW - Pad * 2 - 14;
    private const int CReview = RowW - (CRank + CPen + CReach + CStars + CStatus + CDate) - Gap * 6;

    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();
        UiTheme.Apply(this);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // 캡처용 — 저장된 명단(자동 완주 검증이 남긴 「무명」 더미가 쌓인다) 대신 시연 기록으로 그린다.
        // 저장은 일절 하지 않으며, 화면 확인이 세이브 상태에 좌우되지 않게 하는 것이 목적이다.
        var args = Godot.OS.GetCmdlineUserArgs();
        List<RosterRow> rows;
        if (CombatEntry.HasFlag(args, "demo"))
        {
            rows = Roster.Npcs.Concat(new[]
            {
                new RosterRow("테스트용사", 6, 5, "게시", "생환", "2026-08-07",
                    "사장님, 3년 치 답글 잘 받았습니다.", Me: true),
                new RosterRow("테스트용사", 2, 1, "계류", "전사", "2026-08-05",
                    "환불은 저승에서 받겠습니다.", Me: true),
            }).OrderByDescending(r => r.Floor)
              .ThenByDescending(r => r.Date, StringComparer.Ordinal).ToList();
        }
        else rows = Roster.All();

        Build(rows);
    }

    private void Build(List<RosterRow> rows)
    {
        foreach (var c in GetChildren()) c.QueueFree();

        var bg = new ColorRect { Color = CombatArt.Bg, MouseFilter = MouseFilterEnum.Ignore };
        bg.At(0, 0, W, H);
        AddChild(bg);

        AddChild(Hub.TopBar("board", RunStore.Current));

        AddChild(Intro(rows));

        // ── 표 ──
        var panel = Hub.Panel(ColX, 172, ColW, 500);
        AddChild(panel);

        panel.AddChild(Head());

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.At(Pad, 42, ColW - Pad * 2, 444);
        panel.AddChild(scroll);

        var list = UiTheme.VBox(0);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(list);

        for (int i = 0; i < rows.Count; i++) list.AddChild(Row(i + 1, rows[i]));

        var foot = CombatArt.Text(
            "유언의 좋아요 보상은 그 런에서 전투 승리 1회 이상인 경우에만 집계된다 (만물대장 운영 원칙 — GDD §4.3).\n"
            + "「유실」은 대장 이관 중 원문이 사라진 기록이다. 복구 요청은 접수되지 않는다.",
            12, new Color(0.44f, 0.40f, 0.35f), HorizontalAlignment.Center, wrap: true);
        foot.At(ColX, 688, ColW, 40);
        AddChild(foot);
    }

    private static Control Intro(List<RosterRow> rows)
    {
        var panel = Hub.Panel(ColX, 52, ColW, 108);

        panel.AddChild(CombatArt.Text("환불원정대 명단", 21, CombatArt.Gold).At(Pad + 4, 14, 400, 26));
        panel.AddChild(CombatArt.Text(
            "소환된 자들의 기록이다. 전원 필명, 전원 익명 — 소환이 곧 익명의 조건이다.",
            13, CombatArt.Dim).At(Pad + 4, 46, 780, 20));
        panel.AddChild(CombatArt.Text(
            "죽은 대원은 지워지지 않고 마지막 리뷰와 함께 여기 남는다. 아직, 오래 살아남은 대원은 없다.",
            13, CombatArt.Dim).At(Pad + 4, 70, 780, 20));

        int lost = rows.Count(r => r.Status == "유실");
        string[] chips = { $"등재 대원 {rows.Count}명", "생존 1명 — 당신", $"유언 유실 {lost}건" };
        for (int i = 0; i < chips.Length; i++)
            panel.AddChild(Hub.Chip(chips[i]).At(ColW - Pad - 170, 14 + i * 28, 170, 24));

        return panel;
    }

    private static Control Head()
    {
        var head = new Control { MouseFilter = MouseFilterEnum.Ignore };
        head.At(Pad, 10, RowW, 32);

        var line = new ColorRect { Color = CombatArt.Edge, MouseFilter = MouseFilterEnum.Ignore };
        line.At(0, 30, RowW, 1);
        head.AddChild(line);

        var cols = new (string Text, int Width, HorizontalAlignment Align)[]
        {
            ("순위", CRank, HorizontalAlignment.Center),
            ("필명", CPen, HorizontalAlignment.Left),
            ("도달", CReach, HorizontalAlignment.Left),
            ("별점", CStars, HorizontalAlignment.Left),
            ("마지막 리뷰 (유언)", CReview, HorizontalAlignment.Left),
            ("상태", CStatus, HorizontalAlignment.Center),
            ("게시일", CDate, HorizontalAlignment.Right),
        };
        float x = 0;
        foreach (var (text, w, align) in cols)
        {
            head.AddChild(CombatArt.Text(text, 11, CombatArt.Dim, align).At(x, 6, w, 18));
            x += w + Gap;
        }
        return head;
    }

    private static Control Row(int rank, RosterRow e)
    {
        bool lost = e.Status == "유실";
        var row = new Control { CustomMinimumSize = new Vector2(RowW, 44) };
        row.MouseFilter = MouseFilterEnum.Ignore;

        if (e.Me)
        {
            var mark = CombatArt.Slabbed(new Color(0.88f, 0.66f, 0.29f, 0.07f), CombatArt.Gold, 5);
            mark.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row.AddChild(mark);
        }
        else
        {
            var under = new ColorRect { Color = new Color(0.42f, 0.34f, 0.21f, 0.28f), MouseFilter = MouseFilterEnum.Ignore };
            under.At(0, 43, RowW, 1);
            row.AddChild(under);
        }

        var ink = lost ? new Color(0.55f, 0.52f, 0.47f) : CombatArt.Ink;
        float x = 0;

        row.AddChild(CombatArt.Text($"{rank}", 15,
            rank == 1 ? CombatArt.Gold : CombatArt.Dim, HorizontalAlignment.Center).At(x, 12, CRank, 20));
        x += CRank + Gap;

        row.AddChild(CombatArt.Text($"✍ {e.Name}", 14, ink).At(x, 13, CPen - 34, 20));
        if (e.Me)
        {
            var badge = CombatArt.Slabbed(CombatArt.Gold, null, 3);
            badge.At(x + Mathf.Min(CPen - 40, 22 + e.Name.Length * 15), 14, 26, 18);
            row.AddChild(badge);
            row.AddChild(CombatArt.Text("나", 11, new Color(0.11f, 0.08f, 0.04f), HorizontalAlignment.Center)
                .At(badge.Position.X, 15, 26, 16));
        }
        x += CPen + Gap;

        row.AddChild(CombatArt.Text($"1막 {e.Floor}층", 13, ink).At(x, 7, CReach, 18));
        row.AddChild(CombatArt.Text(Roster.FateLabel(e.Fate), 10, Roster.FateColor(e.Fate)).At(x, 25, CReach, 16));
        x += CReach + Gap;

        row.AddChild(Hub.Stars(e.Stars, 13, lost).At(x, 13, CStars, 18));
        x += CStars + Gap;

        row.AddChild(Ribbon(e, lost).At(x, 9, CReview, 26));
        x += CReview + Gap;

        var (sc, sb) = e.Status switch
        {
            "게시" => (new Color(0.49f, 0.77f, 0.55f), new Color(0.24f, 0.35f, 0.27f)),
            "계류" => (new Color(0.82f, 0.69f, 0.44f), new Color(0.35f, 0.29f, 0.17f)),
            _ => (new Color(0.44f, 0.40f, 0.35f), CombatArt.Edge),
        };
        var chip = CombatArt.Slabbed(new Color(0, 0, 0, 0.30f), sb, 4);
        chip.At(x, 12, CStatus, 22);
        row.AddChild(chip);
        row.AddChild(CombatArt.Text(e.Status, 11, sc, HorizontalAlignment.Center).At(x, 15, CStatus, 18));
        x += CStatus + Gap;

        row.AddChild(CombatArt.Text(e.Date, 11, CombatArt.Dim, HorizontalAlignment.Right).At(x, 14, CDate, 18));

        if (lost) row.Modulate = new Color(1, 1, 1, 0.55f);
        return row;
    }

    /// <summary>유언 한 줄 — 양피지 칩. 유실이면 종이가 아니라 지워진 자국이다</summary>
    private static Control Ribbon(RosterRow e, bool lost)
    {
        var box = new Control { MouseFilter = MouseFilterEnum.Ignore };
        var bg = lost
            ? CombatArt.Slabbed(new Color(0.23f, 0.20f, 0.16f), new Color(0.29f, 0.25f, 0.21f), 4)
            : CombatArt.Slabbed(CombatArt.Parch, new Color(0.54f, 0.45f, 0.30f), 4);
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        box.AddChild(bg);

        string text = lost ? "유언 원문 유실 — 복구 불가"
                           : $"“{(string.IsNullOrWhiteSpace(e.Review) ? "…" : e.Review)}”";
        var l = CombatArt.Text(text, 12, lost ? new Color(0.42f, 0.36f, 0.29f) : CombatArt.Inkc);
        l.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        l.OffsetLeft = 10;
        l.OffsetRight = -10;
        l.VerticalAlignment = VerticalAlignment.Center;
        l.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        box.AddChild(l);
        return box;
    }
}

/// <summary>
/// 화면 공통 조각 — 상단 내비·패널·칩·별점. 지도와 명단이 같이 쓴다.
/// (웹판 shared.css + RH.ui.topbar 에 대응. 색의 정본은 <see cref="CombatArt"/> 다)
/// </summary>
internal static class Hub
{
    public const int TopH = 40;

    public static Control TopBar(string current, RunState? run)
    {
        var bar = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.At(0, 0, 1344, TopH);

        var bg = new ColorRect { Color = CombatArt.Slab, MouseFilter = Control.MouseFilterEnum.Ignore };
        bg.At(0, 0, 1344, TopH);
        bar.AddChild(bg);
        var line = new ColorRect { Color = CombatArt.Edge, MouseFilter = Control.MouseFilterEnum.Ignore };
        line.At(0, TopH - 1, 1344, 1);
        bar.AddChild(line);

        bar.AddChild(CombatArt.Text("이세계 리뷰용사", 15, CombatArt.Gold).At(20, 11, 160, 20));

        string pen = RunStore.Registered ? RunStore.Penname : "미등록";
        var chip = Chip($"✍ {pen}");
        chip.At(190, 8, 20 + pen.Length * 15, 24);
        bar.AddChild(chip);

        float x = 190 + chip.Size.X + 18;
        if (run is not null)
        {
            foreach (var s in new[]
                     {
                         $"🧠 {run.Will}/{run.MaxWill}", $"🪙 {run.Gold}",
                         $"🃏 {run.Deck.Count}장", $"1막 {run.Floor}층",
                     })
            {
                bar.AddChild(CombatArt.Text(s, 14, CombatArt.Ink).At(x, 12, 120, 18));
                x += 108;
            }
        }

        // 우측 내비 — 웹판 topbar 와 같은 순서
        var items = new (string Key, string Text, Action? Go)[]
        {
            ("map", "지도", run is null ? null : SceneRouter.GoMap),
            ("board", "원정대 명단", () => SceneRouter.Go(SceneRouter.Board)),
            ("home", "메인", SceneRouter.GoTitle),
        };
        float bx = 1344 - 20;
        for (int i = items.Length - 1; i >= 0; i--)
        {
            var (key, text, go) = items[i];
            float w = 30 + text.Length * 15;
            bx -= w;
            var b = NavBtn(text, key == current, go);
            b.At(bx, 6, w, 28);
            bar.AddChild(b);
            bx -= 6;
        }
        return bar;
    }

    public static Button NavBtn(string text, bool active, Action? onPressed)
    {
        var b = new Button { Text = text, Disabled = onPressed is null, FocusMode = Control.FocusModeEnum.None };
        b.AddThemeFontSizeOverride("font_size", 13);
        b.AddThemeColorOverride("font_color", active ? CombatArt.Gold : CombatArt.Dim);
        b.AddThemeColorOverride("font_hover_color", CombatArt.Ink);
        b.AddThemeColorOverride("font_disabled_color", new Color(0.35f, 0.33f, 0.29f));
        var normal = CombatArt.Box(new Color(0, 0, 0, active ? 0.35f : 0f), active ? CombatArt.EdgeHi : null, 4);
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("disabled", CombatArt.Box(new Color(0, 0, 0, 0f), null, 4));
        b.AddThemeStyleboxOverride("hover", CombatArt.Box(new Color(1, 1, 1, 0.05f), CombatArt.Edge, 4));
        b.AddThemeStyleboxOverride("pressed", CombatArt.Box(new Color(0, 0, 0, 0.5f), CombatArt.EdgeHi, 4));
        if (onPressed is not null) b.Pressed += onPressed;
        return b;
    }

    /// <summary>웹판 .panel — 어두운 판 하나</summary>
    public static Control Panel(float x, float y, float w, float h)
    {
        var c = new Control();
        c.At(x, y, w, h);
        var p = CombatArt.Slabbed(CombatArt.Panel, CombatArt.Edge, 6);
        p.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        c.AddChild(p);
        return c;
    }

    public static Control Chip(string text, Color? color = null)
    {
        var c = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        var p = CombatArt.Slabbed(new Color(0, 0, 0, 0.35f), CombatArt.Edge, 4);
        p.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        c.AddChild(p);
        var l = CombatArt.Text(text, 12, color ?? CombatArt.Dim, HorizontalAlignment.Center);
        l.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        l.VerticalAlignment = VerticalAlignment.Center;
        c.AddChild(l);
        return c;
    }

    /// <summary>★ 채운 만큼 금색, 나머지는 흐리게. 한 라벨에 색을 두 개 못 쓰므로 두 겹으로 얹는다</summary>
    public static Control Stars(int n, int size, bool grey = false)
    {
        n = Math.Clamp(n, 0, 5);
        var c = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        var dim = CombatArt.Text(new string('☆', 5), size, new Color(0.42f, 0.39f, 0.34f));
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        c.AddChild(dim);
        if (n > 0)
        {
            var lit = CombatArt.Text(new string('★', n), size,
                grey ? new Color(0.55f, 0.53f, 0.49f) : CombatArt.Gold);
            lit.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            c.AddChild(lit);
        }
        return c;
    }
}
