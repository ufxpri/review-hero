// 계정 — 리뷰어 수첩 (S52 · ADR-029 4차).
//
// 이관 원본: ui/game/account.html.
//
// ── 웹판에서 고친 것 ────────────────────────────────────
// ① 웹판은 「시작 덱 12장」 표가 화면의 절반이었다. 그 표는 **카드 데이터**이지 계정 기록이
//    아니고, 같은 목록을 만물대장(Codex)이 더 잘 보여 준다. 여기서는 뺐다 — 계정 화면은
//    「이 계정에 무엇이 쌓였는가」만 답한다.
// ② 웹판 통계는 타일 6개(meta)뿐이고 <see cref="StatsState"/> 13개 필드는 아무 데서도 안
//    보였다. 그냥 나열하면 읽히지 않으므로 **묻는 질문 세 개**로 묶었다 —
//    무엇을 올렸는가(리뷰) · 어떻게 싸웠는가(전투) · 무엇을 치웠는가(살림).
// ③ 서명은 등록부의 양피지 서명란을 그대로 축소한다. 획은 <see cref="SignatureInk"/> 가
//    긋는다 — 전투 카드의 서명란과 **같은 코드**라 두 화면이 어긋날 수 없다.
//
// ── 용어 (ADR-031 ④) ────────────────────────────────────
// **필명은 사람이 아니라 계정이다.** 대장에 오른 이름 하나에 대원이 갈아 끼워지며, 대장은
// 이름과 서명을 대조만 할 뿐 그 주인이 누구인지 묻지 않는다(worldview §6 「계정」).
// 그래서 이 화면의 문구는 「당신의 전적」이 아니라 **「이 계정에 쌓인 기록」**이다.

using Godot;
using ReviewHero.Game.Combat;   // CombatArt — 색·조각 사전 (읽기만 한다)
using ReviewHero.Game.Fx;       // SignatureInk — 등록 서명을 긋는다
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Meta;

public partial class Account : Control
{
    private const int W = CombatArt.ScreenW;   // 1344
    private const int H = CombatArt.ScreenH;   // 768

    private const int ColX = 92, ColW = 1160;
    private const int Pad = 16;

    private const int TopY = 100, TopH = 300;
    private const int LeftW = 470;
    private const int RightX = ColX + LeftW + 16, RightW = ColW - LeftW - 16;
    private const int BotY = TopY + TopH + 16, BotH = 260;

    /// <summary>업적 총량 — 판정 로직은 다음 단계다 (Title.cs 와 같은 상수)</summary>

    private static readonly Color Ledger = new("cdbfa4");
    private static readonly Color Faint = new("6f675a");

    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();
        UiTheme.Apply(this);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Build();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) SceneRouter.Go(SceneRouter.Title);
    }

    // ── 조립 ─────────────────────────────────────────

    private void Build()
    {
        foreach (var c in GetChildren()) c.QueueFree();

        var bg = new ColorRect { Color = CombatArt.Bg, MouseFilter = MouseFilterEnum.Ignore };
        bg.At(0, 0, W, H);
        AddChild(bg);

        AddChild(Hub.TopBar("account", RunStore.Current));

        AddChild(CombatArt.Text("계정 — 리뷰어 수첩", 21, CombatArt.Gold).At(ColX, 52, 400, 26));
        AddChild(CombatArt.Text(
            "만물대장에 오른 계정 하나다. 이름과 서명이 한 쌍으로 등록되고, 대장은 대조만 할 뿐 주인이 누구인지 묻지 않는다.",
            12, CombatArt.Dim).At(ColX, 80, ColW, 18));

        AddChild(Identity());
        AddChild(Totals());
        AddChild(Measures());

        AddChild(CombatArt.Text("Esc — 타이틀로", 12, Faint).At(ColX, 690, 300, 18));
        AddChild(CombatArt.Text("이 기록은 이 기기의 세이브에만 남는다 (user://save.json)", 12, Faint,
            HorizontalAlignment.Right).At(ColX + ColW - 520, 690, 520, 18));
    }

    // ── 필명과 서명 ──────────────────────────────────

    private static Control Identity()
    {
        var p = Hub.Panel(ColX, TopY, LeftW, TopH);
        var sig = RunStore.Signature;
        bool signed = sig is { HasStrokes: true };
        bool registered = RunStore.Registered;

        p.AddChild(CombatArt.Text("필명과 서명", 13, CombatArt.Gold).At(Pad + 2, 12, 300, 20));

        // 서명란 — 등록부(660×236)와 같은 비율의 양피지
        const int PadW = LeftW - Pad * 2 - 4, PadH = 156;
        var pad = new Control { MouseFilter = MouseFilterEnum.Ignore };
        pad.At(Pad + 2, 40, PadW, PadH);
        pad.Draw += () =>
        {
            pad.DrawStyleBox(CombatArt.Box(CombatArt.Parch, CombatArt.Inkc, 8, 2), new Rect2(0, 0, PadW, PadH));
            // 서명선 — 「여기에 서명하세요」의 그 줄
            float ry = PadH * 0.77f;
            pad.DrawRect(new Rect2(PadW * 0.08f, ry, PadW * 0.84f, 1), CombatArt.ParchD);
            pad.DrawString(CombatArt.Font(), new Vector2(PadW * 0.08f - 16, ry + 5), "×",
                HorizontalAlignment.Left, -1, 15, CombatArt.ParchD);
        };
        p.AddChild(pad);

        if (signed)
        {
            // 획을 그대로 쓴다 — 카드 서명란과 같은 코드라 두 화면이 어긋나지 않는다.
            // 전역 저장본(SignatureStore)이 아니라 Preview 로 넣는다: 이 화면이 전투의 캐시를 건드리지 않는다.
            var ink = new SignatureInk { Preview = sig!.ToVectors() };
            // 서명란(140×38) 비율을 지켜 잡는다 — 폭·높이를 따로 늘리면 글씨가 눌린다
            const int InkW = PadW - 56;
            const int InkH = (int)(InkW * 38f / 140f);
            ink.At((PadW - InkW) / 2f, PadH * 0.72f - InkH, InkW, InkH);
            pad.AddChild(ink);
            ink.Ready += () => ink.Progress = 1f;
        }
        else
        {
            var none = CombatArt.Text("서명 없음\n리뷰어 등록에서 이름과 함께 받는다.", 12, new Color("a08f68"),
                HorizontalAlignment.Center, wrap: true);
            none.At(0, PadH / 2f - 22, PadW, 44);
            pad.AddChild(none);
        }

        // 필명 = 계정 이름
        float y = 40 + PadH + 12;
        if (registered)
        {
            string pen = RunStore.Penname;
            var font = CombatArt.Font();
            p.AddChild(CombatArt.Text($"✍ {pen}", 19, CombatArt.Gold).At(Pad + 2, y, LeftW - Pad * 2, 26));
            float nw = font.GetStringSize($"✍ {pen}", HorizontalAlignment.Left, -1, 19).X;
            p.AddChild(CombatArt.Text("님의 계정", 13, Ledger).At(Pad + 10 + nw, y + 8, 200, 20));
        }
        else
        {
            p.AddChild(CombatArt.Text("✍ 미등록 계정", 19, CombatArt.Gold).At(Pad + 2, y, LeftW - Pad * 2, 26));
        }

        p.AddChild(CombatArt.Text(
            registered
                ? (signed ? $"등록 서명 {sig!.Strokes.Count}획 — 대장이 이 획으로 대조한다."
                          : "서명이 없다 — 카드에는 기본 필체가 그어진다.")
                : "새 원정을 시작하면 리뷰어 등록부터 진행한다.",
            12, CombatArt.Dim).At(Pad + 2, y + 28, LeftW - Pad * 2 - 4, 18));

        var b = Hub.NavBtn(registered ? "필명·서명 다시 만들기" : "리뷰어 등록하기", true,
            SceneRouter.Exists(SceneRouter.Signature) ? () => SceneRouter.Go(SceneRouter.Signature) : null);
        b.At(Pad + 2, TopH - 40, 200, 28);
        p.AddChild(b);

        p.AddChild(CombatArt.Text("다시 만들어도 쌓인 기록은 그대로다.", 11, Faint)
            .At(Pad + 212, TopH - 34, LeftW - Pad - 216, 18));
        return p;
    }

    // ── 누적 전적 ────────────────────────────────────

    private static Control Totals()
    {
        var p = Hub.Panel(RightX, TopY, RightW, TopH);
        var m = RunStore.Meta;

        p.AddChild(CombatArt.Text("이 계정에 쌓인 것", 13, CombatArt.Gold).At(Pad + 2, 12, 300, 20));

        var tiles = new (string Key, string Val, string Unit)[]
        {
            ("원정", N(m.Runs), "회"),
            ("생환", N(m.Wins), "회"),
            ("최고 도달", m.BestFloor > 0 ? $"1막 {m.BestFloor}" : "—", m.BestFloor > 0 ? "층" : ""),
            ("명성", N(m.Rp), "RP"),
            ("적립금", N(m.P), "P"),
            ("명단에 남긴 글", N(m.Expedition.Count), "건"),
        };
        const int Cols = 3, Gap = 10;
        float tw = (RightW - Pad * 2 - Gap * (Cols - 1)) / Cols;
        const int th = 84;
        for (int i = 0; i < tiles.Length; i++)
        {
            var (key, val, unit) = tiles[i];
            float x = Pad + (i % Cols) * (tw + Gap);
            float y = 42 + (i / Cols) * (th + Gap);
            var t = new Control { MouseFilter = MouseFilterEnum.Ignore };
            t.At(x, y, tw, th);
            t.Draw += () => t.DrawStyleBox(CombatArt.Box(new Color(0, 0, 0, 0.30f), CombatArt.Edge, 5),
                new Rect2(0, 0, tw, th));
            t.AddChild(CombatArt.Text(key, 11, CombatArt.Dim).At(12, 12, tw - 24, 18));
            t.AddChild(CombatArt.Text(val, 24, CombatArt.Ink).At(12, 38, tw - 24, 32));
            float vw = CombatArt.Font().GetStringSize(val, HorizontalAlignment.Left, -1, 24).X;
            t.AddChild(CombatArt.Text(unit, 11, CombatArt.Dim).At(14 + vw, 52, 60, 18));
            p.AddChild(t);
        }

        int cards = 0;
        try { cards = GameData.Cards.ById.Count; }
        catch (Exception e) { GD.PushWarning($"[Account] 카드 데이터를 못 읽었다: {e.Message}"); }

        p.AddChild(CombatArt.Text(
            $"등재된 리뷰 {m.Seen.Count}/{(cards > 0 ? cards.ToString() : "?")}종   ·   등재 기록 {m.Badges.Count}/{BadgeDefs.Total}건"
            + $"   ·   집계 제외(계류) {m.Expedition.Count(e => e.Status == "계류")}건",
            12, Ledger).At(Pad + 2, TopH - 40, RightW - Pad * 2, 20));
        return p;
    }

    // ── 계측 (StatsState 13개를 질문 셋으로 묶는다) ──

    private static Control Measures()
    {
        var p = Hub.Panel(ColX, BotY, ColW, BotH);
        var s = RunStore.Meta.Stats;
        var j = s.Judgements;

        const int Gap = 14;
        float cw = (ColW - Pad * 2 - Gap * 2) / 3f;

        // ① 무엇을 올렸는가 — 제출과 판정 분포
        var c1 = Column(cw, "리뷰", "무엇을 올렸는가");
        c1.At(Pad, 12, cw, BotH - 24);
        c1.AddChild(CombatArt.Text(N(s.Submissions), 30, CombatArt.Ink).At(0, 46, 160, 36));
        float sw = CombatArt.Font().GetStringSize(N(s.Submissions), HorizontalAlignment.Left, -1, 30).X;
        c1.AddChild(CombatArt.Text("건 제출", 12, CombatArt.Dim).At(sw + 8, 64, 120, 18));

        var rows = new (string Name, int V, Color C)[]
        {
            ("원산지", j.Origin, CombatArt.StampOrigin),
            ("팩트", j.Fact, CombatArt.StampFact),
            ("일반", j.Normal, CombatArt.StampNormal),
            ("헛소리", j.Fumble, CombatArt.StampFumble),
        };
        int total = Math.Max(1, rows.Sum(r => r.V));
        for (int i = 0; i < rows.Length; i++)
        {
            var (name, v, col) = rows[i];
            float y = 96 + i * 26;
            c1.AddChild(CombatArt.Text(name, 12, CombatArt.Dim).At(0, y, 60, 18));
            float ratio = v / (float)total;
            float bx = 62, bw = cw - 62 - 74;
            var bar = new Control { MouseFilter = MouseFilterEnum.Ignore };
            bar.At(bx, y + 6, bw, 8);
            bar.Draw += () =>
            {
                bar.DrawStyleBox(CombatArt.Box(new Color(0.11f, 0.10f, 0.08f), null, 2), new Rect2(0, 0, bw, 8));
                if (ratio > 0)
                    bar.DrawStyleBox(CombatArt.Box(col, null, 2), new Rect2(0, 0, Math.Max(2f, bw * ratio), 8));
            };
            c1.AddChild(bar);
            c1.AddChild(CombatArt.Text($"{N(v)}", 12, CombatArt.Ink, HorizontalAlignment.Right)
                .At(cw - 70, y, 44, 18));
            c1.AddChild(CombatArt.Text(s.Submissions > 0 ? $"{ratio * 100:0}%" : "—", 11, Faint,
                HorizontalAlignment.Right).At(cw - 24, y + 1, 24, 18));
        }
        p.AddChild(c1);

        // ② 어떻게 싸웠는가
        var c2 = Column(cw, "전투", "어떻게 싸웠는가");
        c2.At(Pad + cw + Gap, 12, cw, BotH - 24);
        Rows(c2, cw, new (string, string)[]
        {
            ("이긴 판", $"{N(s.BattlesWon)}판"),
            ("항복시킨 판", $"{N(s.SurrenderWins)}판"),
            ("발 뺀 판", $"{N(s.Retreats)}판"),
            ("크리티컬 리뷰", $"{N(s.Crits)}회"),
            ("빗나간 크리티컬", $"{N(s.CritMisses)}회"),
            ("가장 아슬아슬했던 승리", s.MinWillWin is { } w ? $"의지 {N(w)} 남기고" : "—"),
        });
        p.AddChild(c2);

        // ③ 무엇을 치웠는가
        var c3 = Column(cw, "살림", "무엇을 치웠는가");
        c3.At(Pad + (cw + Gap) * 2, 12, cw, BotH - 24);
        Rows(c3, cw, new (string, string)[]
        {
            ("파쇄한 리뷰", $"{N(s.CardsRemoved)}장"),
            ("막아 낸 좋아요", $"{N(s.DefenseAbsorbed)}"),
            ("되찾은 의지", $"{N(s.WillHealed)}"),
            ("개봉한 보급품", $"{N(s.ParcelsOpened)}개"),
        });
        if (s.Submissions == 0 && RunStore.Meta.Runs == 0)
        {
            c3.AddChild(CombatArt.Text("아직 아무것도 올리지 않았다.\n첫 원정을 마치면 여기부터 채워진다.",
                12, Faint, wrap: true).At(0, 150, cw, 44));
        }
        p.AddChild(c3);

        // 열 사이 경계선
        p.Draw += () =>
        {
            for (int i = 1; i < 3; i++)
            {
                float x = Pad + (cw + Gap) * i - Gap / 2f;
                p.DrawRect(new Rect2(x, 20, 1, BotH - 40), CombatArt.Edge with { A = 0.45f });
            }
        };
        return p;
    }

    private static Control Column(float w, string title, string sub)
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore };
        c.AddChild(CombatArt.Text(title, 13, CombatArt.Gold).At(0, 0, w, 20));
        float tw = CombatArt.Font().GetStringSize(title, HorizontalAlignment.Left, -1, 13).X;
        c.AddChild(CombatArt.Text($"— {sub}", 11, Faint).At(tw + 8, 2, w - tw - 8, 18));
        return c;
    }

    /// <summary>키-값 줄 묶음. 가로줄이라 라벨은 wrap:false 그대로 둔다</summary>
    private static void Rows(Control host, float w, IReadOnlyList<(string Key, string Val)> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var (k, v) = rows[i];
            float y = 40 + i * 30;
            host.AddChild(CombatArt.Text(k, 12, CombatArt.Dim).At(0, y, w - 130, 18));
            host.AddChild(CombatArt.Text(v, 14, CombatArt.Ink, HorizontalAlignment.Right).At(w - 150, y - 2, 150, 20));
            if (i < rows.Count - 1)
            {
                var line = new ColorRect
                {
                    Color = CombatArt.Edge with { A = 0.22f },
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                line.At(0, y + 22, w, 1);
                host.AddChild(line);
            }
        }
    }

    private static string N(int v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
}
