// 등재 기록 — 대장에 올라간 내 기록들 (ADR-029 4차 · worldview §6 「등재」).
//
// ── 어휘 ────────────────────────────────────────────────
// 「업적」이라는 말은 이 세계에 없다. 대장에 올랐다는 뜻의 **등재**가 정본이다(worldview §6).
// 화면의 모든 문구가 그 어휘를 따른다 — 달성/획득이 아니라 **오르다·등재되다**다.
//
// ── 판정을 여기서 부르는 이유 ───────────────────────────
// 조건 판정은 <see cref="BadgeDefs.Evaluate"/> 가 MetaState 만 읽는 순수 함수로 소유한다.
// 그것을 **화면에 들어올 때** 부른다. 정산(RunState.cs 의 FinalizeRun)에 심으면
//   ① 담당 밖 파일을 고쳐야 하고
//   ② 조건을 나중에 추가했을 때 이미 쌓인 기록이 영원히 등재되지 않는다.
// 화면 진입 시 전량 재평가하면 둘 다 없다. 대장은 소급해서 읽어도 같은 답을 낸다.
//
// ── 디버그 (스크린샷 검증용) ────────────────────────────
//   --rh-badges=full   전 항목이 오른 상태
//   --rh-badges=some   절반쯤 오른 상태(진행도 막대가 보이게)
//   --rh-badges=empty  첫 실행(0건) 상태
// 전부 **메모리 위에서만** Stats 를 갈아 끼우고 Save() 를 부르지 않는다.

using Godot;
using ReviewHero.Game.Combat;   // CombatArt — 색·조각 사전 / CombatEntry — 인자 파서
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Meta;

public partial class Badges : Control
{
    private const int W = CombatArt.ScreenW;   // 1344
    private const int H = CombatArt.ScreenH;   // 768

    private const int ColX = 32, ColW = W - ColX * 2;
    private const int HeadY = 52, HeadH = 92;
    private const int ListY = 160, ListH = 560;
    private const int Pad = 14;

    private const int RowW = 610, RowH = 92, RowGapX = 16, RowGapY = 10;

    private static readonly Color Mask = new("6f675a");
    private static readonly Color Sealed = new("7db98a");
    private static readonly Color Locked = new(0.42f, 0.39f, 0.34f);

    private HashSet<string> _earned = new(StringComparer.Ordinal);
    private int _fresh;

    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();
        UiTheme.Apply(this);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        bool demo = ApplyDemo(Godot.OS.GetCmdlineUserArgs());
        var meta = RunStore.Meta;

        // 실제 플레이에서는 여기서 대장에 올린다. 캡처 모드는 세이브를 더럽히지 않으려고 평가만 한다.
        if (!demo) _fresh = BadgeDefs.Sync(meta);

        // 표시는 「지금 조건을 만족하는 것」 ∪ 「이미 올라가 있는 것」이다.
        // 뒤쪽을 합치는 이유: 조건이 나중에 바뀌어도 한 번 오른 기록은 대장에서 내려가지 않는다.
        _earned = BadgeDefs.Evaluate(meta).ToHashSet(StringComparer.Ordinal);
        foreach (var id in meta.Badges) if (BadgeDefs.Find(id) is not null) _earned.Add(id);

        Build();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) SceneRouter.Go(SceneRouter.Title);
    }

    /// <summary>캡처용 — Stats 를 제자리에서 갈아 끼운다. Save() 는 절대 부르지 않는다</summary>
    private static bool ApplyDemo(IReadOnlyList<string> args)
    {
        string? mode = CombatEntry.ArgValue(args, "badges");
        if (mode is null) return false;

        var meta = RunStore.Meta;
        meta.Badges.Clear();
        meta.Stats = new StatsState();
        meta.Runs = meta.Wins = meta.BestFloor = 0;
        meta.Seen.Clear();

        if (mode == "full")
        {
            var s = meta.Stats;
            s.Submissions = 1204; s.Judgements.Origin = 88; s.Judgements.Fact = 402;
            s.Judgements.Normal = 610; s.Judgements.Fumble = 104;
            s.Crits = 41; s.CritMisses = 23; s.BattlesWon = 63; s.SurrenderWins = 12;
            s.Retreats = 4; s.CardsRemoved = 37; s.MinWillWin = 1;
            s.DefenseAbsorbed = 742; s.WillHealed = 388; s.ParcelsOpened = 6;
            meta.Runs = 21; meta.Wins = 5; meta.BestFloor = 6;
            meta.Seen.AddRange(GameData.Cards.ById.Keys);
        }
        else if (mode == "some")
        {
            var s = meta.Stats;
            s.Submissions = 147; s.Judgements.Origin = 12; s.Judgements.Fact = 61;
            s.Judgements.Normal = 63; s.Judgements.Fumble = 11;
            s.Crits = 6; s.CritMisses = 3; s.BattlesWon = 9; s.SurrenderWins = 2;
            s.Retreats = 1; s.CardsRemoved = 8; s.MinWillWin = 4;
            s.DefenseAbsorbed = 186; s.WillHealed = 97; s.ParcelsOpened = 1;
            meta.Runs = 6; meta.Wins = 0; meta.BestFloor = 5;
            meta.Seen.AddRange(GameData.Cards.ById.Keys.Take(24));
        }
        return true;
    }

    // ── 조립 ─────────────────────────────────────────

    private void Build()
    {
        foreach (var c in GetChildren()) c.QueueFree();

        var bg = new ColorRect { Color = CombatArt.Bg, MouseFilter = MouseFilterEnum.Ignore };
        bg.At(0, 0, W, H);
        AddChild(bg);

        AddChild(Hub.TopBar("badges", RunStore.Current));
        AddChild(Header());
        AddChild(List());

        var foot = CombatArt.Text(
            "한 번 오른 기록은 내려가지 않는다. 지울 수 있었던 것은 신뿐이고, 신은 사라졌다.",
            12, new Color(0.44f, 0.40f, 0.35f), HorizontalAlignment.Center);
        foot.At(ColX, 732, ColW, 20);
        AddChild(foot);
    }

    private Control Header()
    {
        var p = Hub.Panel(ColX, HeadY, ColW, HeadH);
        var meta = RunStore.Meta;

        p.AddChild(CombatArt.Text("등재 기록", 21, CombatArt.Gold).At(Pad + 4, 12, 300, 26));
        p.AddChild(CombatArt.Text("당신이 쓴 것들 중 대장이 따로 한 줄을 내어 준 기록이다.",
            13, CombatArt.Dim).At(Pad + 4, 44, 700, 20));
        p.AddChild(CombatArt.Text("조건을 채우면 그 자리에서 올라간다. 심사는 없다 — 대조만 있다.",
            13, CombatArt.Dim).At(Pad + 4, 64, 700, 20));

        if (_fresh > 0)
            p.AddChild(Hub.Chip($"이번에 새로 오름 {_fresh}건", Sealed).At(Pad + 520, 12, 190, 24));

        int total = BadgeDefs.Total;
        int have = _earned.Count;
        int hidden = BadgeDefs.All.Count(b => b.Hidden && !_earned.Contains(b.Id));
        float ratio = total > 0 ? have / (float)total : 0f;
        const float bx = ColW - Pad - 400, bw = 400;

        p.AddChild(CombatArt.Text("대장에 오른 기록", 12, CombatArt.Dim).At(bx, 12, 220, 18));
        p.AddChild(CombatArt.Text($"{have}", 26, CombatArt.Ink, HorizontalAlignment.Right)
            .At(bx + bw - 210, 8, 120, 32));
        p.AddChild(CombatArt.Text($"/ {total}건", 13, CombatArt.Dim, HorizontalAlignment.Right)
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

        string tail = hidden > 0 ? $"   ·   아직 드러나지 않은 항목 {hidden}건" : string.Empty;
        p.AddChild(CombatArt.Text($"원정 {meta.Runs}회   ·   생환 {meta.Wins}회{tail}",
            12, CombatArt.Dim, HorizontalAlignment.Right).At(bx - 60, 64, bw + 60, 18));
        return p;
    }

    private Control List()
    {
        var p = Hub.Panel(ColX, ListY, ColW, ListH);

        float viewW = ColW - Pad * 2;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.At(Pad, Pad, viewW, ListH - Pad * 2);
        p.AddChild(scroll);

        var defs = BadgeDefs.All;
        int rows = (defs.Count + 1) / 2;
        var canvas = new Control
        {
            CustomMinimumSize = new Vector2(viewW - 16, rows * (RowH + RowGapY)),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        scroll.AddChild(canvas);

        var meta = RunStore.Meta;
        for (int i = 0; i < defs.Count; i++)
        {
            var r = Row(defs[i], meta);
            r.At((i % 2) * (RowW + RowGapX), (i / 2) * (RowH + RowGapY), RowW, RowH);
            canvas.AddChild(r);
        }
        return p;
    }

    private Control Row(BadgeDef b, MetaState meta)
    {
        bool got = _earned.Contains(b.Id);
        bool veiled = b.Hidden && !got;   // 숨은 항목은 오르기 전까지 이름도 조건도 가린다

        var c = new Control { MouseFilter = MouseFilterEnum.Ignore };
        c.CustomMinimumSize = new Vector2(RowW, RowH);
        c.Draw += () =>
        {
            var bg = got ? new Color(0.13f, 0.12f, 0.09f, 0.95f) : new Color(0.06f, 0.06f, 0.05f, 0.9f);
            c.DrawStyleBox(CombatArt.Box(bg, got ? new Color("6b7f5c") : CombatArt.Edge, 6),
                new Rect2(0, 0, RowW, RowH));
            // 도장 자리 — 오른 것은 인주가 눌렸고, 아직인 것은 빈 칸이다
            var seal = new Rect2(14, 20, 52, 52);
            if (got)
            {
                c.DrawStyleBox(CombatArt.Box(new Color(0.30f, 0.42f, 0.31f, 0.35f), Sealed, 5), seal);
            }
            else
            {
                c.DrawStyleBox(CombatArt.Box(new Color(0, 0, 0, 0.35f), null, 5), seal);
                Dash(c, seal, CombatArt.Edge with { A = 0.8f });
            }
        };

        c.AddChild(CombatArt.Text(veiled ? "?" : b.Seal, 20, got ? Sealed : Locked, HorizontalAlignment.Center)
            .At(14, 36, 52, 26));

        string name = veiled ? "▨▨▨▨ 숨은 항목" : b.Name;
        c.AddChild(CombatArt.Text(name, 15, got ? CombatArt.Gold : (veiled ? Mask : CombatArt.Ink))
            .At(80, 14, RowW - 190, 22));

        if (got)
        {
            var chip = Hub.Chip("등재", Sealed);
            chip.At(RowW - 86, 15, 68, 22);
            c.AddChild(chip);
        }

        string line = veiled
            ? "조건은 이 기록이 대장에 오른 뒤에 드러난다."
            : (got ? b.Flavor : b.Cond);
        var body = CombatArt.Text(line, 12, got ? new Color("cdbfa4") : Locked);
        body.ClipText = true;
        body.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        body.At(80, 40, RowW - 96, 20);
        c.AddChild(body);

        // ── 진행도 — 누적형만 막대를 그린다. 단발 항목은 문구로 족하다 ──
        int have = b.Have(meta), need = b.Need(meta);
        if (veiled) return c;

        if (need > 1)
        {
            const float bw = 380f;
            float ratio = b.Ratio(meta);
            var bar = new Control { MouseFilter = MouseFilterEnum.Ignore };
            bar.At(80, 68, bw, 8);
            bar.Draw += () =>
            {
                bar.DrawStyleBox(CombatArt.Box(new Color(0.11f, 0.10f, 0.08f), CombatArt.Edge, 3),
                    new Rect2(0, 0, bw, 8));
                if (ratio > 0)
                    bar.DrawStyleBox(CombatArt.Box(got ? Sealed : new Color("c39a52"), null, 3),
                        new Rect2(0, 0, bw * ratio, 8));
            };
            c.AddChild(bar);
            c.AddChild(CombatArt.Text($"{Math.Min(have, need)} / {need}", 11,
                got ? Sealed : CombatArt.Dim, HorizontalAlignment.Right).At(RowW - 130, 65, 112, 16));
        }
        else
        {
            string detail = b.Detail?.Invoke(meta) ?? (got ? "기록됨" : "아직 없음");
            c.AddChild(CombatArt.Text(detail, 11, got ? Sealed : Locked).At(80, 66, RowW - 200, 16));
        }
        return c;
    }

    private static void Dash(Control c, Rect2 r, Color col)
    {
        Vector2 a = r.Position, b = r.Position + new Vector2(r.Size.X, 0);
        Vector2 d = r.Position + r.Size, e = r.Position + new Vector2(0, r.Size.Y);
        foreach (var (p, q) in new[] { (a, b), (b, d), (d, e), (e, a) })
        {
            float len = p.DistanceTo(q);
            var dir = (q - p) / len;
            for (float t = 0; t < len; t += 8)
                c.DrawLine(p + dir * t, p + dir * Mathf.Min(len, t + 4), col, 1f);
        }
    }
}
