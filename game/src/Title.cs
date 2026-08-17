// 메인 허브 — 런의 입구이자 계정 홈. (ADR-029 4차)
//
// 이관 원본: ui/game/index.html. 이 화면은 게임에서 유일하게 상단 내비(Hub.TopBar)가 없다 —
// 대신 하단 내비로 이동 경로를 확보한다(웹판 CONTRACT 「공통 뼈대」의 예외를 그대로 승계).
//
// ── 웹판과 달라진 것 ────────────────────────────────────
// ① 웹판은 모바일 세로(760px 한 컬럼)라 히어로 아래로 계속 스크롤했다. 여기 화면은 1344×768
//    가로라 같은 순서를 세로로 쌓으면 절반이 화면 밖으로 나간다. **읽는 순서(정체성 → 지금 할 일)
//    는 왼쪽 열이, 쌓인 것(수집·명단)은 오른쪽 열이** 맡도록 두 열로 접었다. 스크롤은 없앴다.
// ② 명단 프리뷰는 <see cref="Roster.Top"/> 를 부른다 — 웹판이 index/board 에 같은 배열을 두 벌
//    두었다가 어긋난 자리다(Board.cs 머리말 참조).
// ③ 필명이 없으면 「새 원정」이 **프롤로그(scenes/Prologue.tscn) → 서명 등록** 순으로 간다.
//    이름은 사람들이 보고 서명은 대장이 대조한다 — 대장에 오르지 않은 채로는 원정이 시작되지
//    않는다 (ADR-020). 등록을 프롤로그 뒤에 두는 이유는 ADR-022.
//    이미 등록한 사람은 프롤로그를 건너뛰고 곧장 지도로 간다.
//
// ── 디버그 (스크린샷 검증용) ────────────────────────────
//   --rh-hub=meta    쌓인 계정(명성·수집·지난 원정·진행 중인 런)으로 그린다
//   --rh-hub=empty   아무것도 없는 첫 실행 상태로 그린다
// 둘 다 **메모리 위에서만** 값을 갈아 끼우고 Save() 를 부르지 않는다 — 캡처가 세이브를 더럽히지 않는다.

using Godot;
using ReviewHero.Game.Combat;   // CombatArt — 색·조각 사전 / AutoPlay — 헤드리스 완주 검증
using ReviewHero.Game.Fx;       // SignatureInk — 리뷰어 카드의 서명 썸네일
using ReviewHero.Game.Run;

namespace ReviewHero.Game;

public partial class Title : Control
{
    private const int W = CombatArt.ScreenW;   // 1344
    private const int H = CombatArt.ScreenH;   // 768

    private const int StripH = 32;
    private const int HeroY = StripH, HeroH = 216;
    private const int BodyY = HeroY + HeroH + 12;      // 260
    private const int NavY = 704, NavH = H - NavY;     // 64

    private const int LX = 92, LW = 660;
    private const int RX = LX + LW + 20, RW = 480;     // 772 / 480

    /// <summary>업적 총량 — 판정 로직은 다음 단계라 아직 데이터가 없다 (index.html BADGE_TOTAL)</summary>
    private const int BadgeTotal = 14;

    private static readonly Color Ledger = new("cdbfa4");
    private static readonly Color CtaSub = new("cdb98b");
    private static readonly Color CtaNote = new("a2947a");
    private static readonly Color LockDim = new("6f675a");
    private static readonly Color Stamp = new("e0876c");
    private static readonly Color StampEdge = new("c2452e");

    /// <summary>화면이 그리는 값 한 벌. 디버그 스위치가 여기만 갈아 끼우면 나머지는 그대로 돈다</summary>
    private sealed record HubView(string? Pen, ResumeInfo? Resume, RunState? Run, bool Signed);

    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();

        // 헤드리스 완주 검증: `Godot --headless --path game -- --autoplay`
        if (AutoPlay.Requested())
        {
            AutoPlay.RunAndQuit(GetTree());
            return;
        }

        // 씬 하나를 곧장 열어 본다 — 내보낸 실행 파일은 --path·-s 를 못 받아서, .pck 안의 화면을
        // 확인할 길이 타이틀을 거치는 것뿐이다. `-- --rh-go=prologue` 로 프롤로그 본문 로드를 본다.
        if (CombatEntry.ArgValue(Godot.OS.GetCmdlineUserArgs(), "go") == "prologue")
        {
            // 첫 씬의 _Ready 안에서는 트리가 아직 자식을 붙이는 중이라 그 자리에서 갈아탈 수 없다
            Callable.From(() => SceneRouter.Go(SceneRouter.Prologue)).CallDeferred();
            return;
        }

        UiTheme.Apply(this);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Build(Resolve(Godot.OS.GetCmdlineUserArgs()));
    }

    // ── 무엇을 그릴지 정한다 ─────────────────────────

    private static HubView Resolve(IReadOnlyList<string> args)
    {
        string? mode = CombatEntry.ArgValue(args, "hub");
        if (mode is null)
        {
            var resume = RunStore.Resume();
            return new HubView(RunStore.Registered ? RunStore.Penname : null, resume, RunStore.Current,
                RunStore.Signature is { HasStrokes: true });
        }

        // 아래는 캡처용이다. RunStore.Meta 를 제자리에서 고치되 Save() 는 부르지 않는다
        var meta = RunStore.Meta;
        meta.Expedition.Clear();
        meta.Seen.Clear();
        meta.Badges.Clear();

        if (mode == "empty")
        {
            meta.Runs = meta.Wins = meta.BestFloor = meta.Rp = meta.P = 0;
            return new HubView(null, null, null, false);
        }

        meta.Runs = 7; meta.Wins = 2; meta.BestFloor = 6; meta.Rp = 186; meta.P = 1240;
        for (int i = 0; i < 31; i++) meta.Seen.Add($"demo{i}");
        for (int i = 0; i < 5; i++) meta.Badges.Add($"badge{i}");
        meta.Expedition.Add(new ExpeditionEntry
        {
            Name = "별점깎는노인", Result = "clear", Floor = 6, Stars = 5,
            Review = "사장님, 3년 치 답글 잘 받았습니다.", Status = "게시", Date = "2026-08-07",
        });
        meta.Expedition.Add(new ExpeditionEntry
        {
            Name = "별점깎는노인", Result = "death", Floor = 2, Stars = 1,
            Review = "환불은 저승에서 받겠습니다.", Status = "계류", Date = "2026-08-05",
        });

        var demo = new RunState
        {
            Seed = 42, Act = 1, Floor = 3, Gold = 64, Will = 21, MaxWill = 30,
            Deck = new List<string>(GameData.StartingDeck),
        };
        return new HubView("별점깎는노인",
            new ResumeInfo(ResumeKind.Map, null, null, 3, SceneRouter.Map, "중단 지점: 지도 (1막 3층)"), demo,
            RunStore.Signature is { HasStrokes: true });
    }

    // ── 조립 ─────────────────────────────────────────

    private void Build(HubView v)
    {
        foreach (var c in GetChildren()) c.QueueFree();

        var bg = new ColorRect { Color = new Color("0a0806"), MouseFilter = MouseFilterEnum.Ignore };
        bg.At(0, 0, W, H);
        AddChild(bg);

        AddChild(Strip());
        AddChild(Hero());

        // ── 왼쪽 열: 내가 누구이고 지금 무엇을 하는가 ──
        float y = BodyY;
        AddChild(ReviewerCard(v).At(LX, y, LW, 120));
        y += 132;

        if (v.Resume is { } resume && v.Run is { } run)
        {
            AddChild(ResumeCta(resume, run).At(LX, y, LW, 94));
            y += 106;
        }

        AddChild(NewRunCta(v).At(LX, y, LW, v.Run is null ? 72 : 88));
        y += (v.Run is null ? 72 : 88) + 12;

        AddChild(SummonLock().At(LX, y, LW, 76));

        // ── 오른쪽 열: 쌓인 것 ──
        AddChild(Tiles().At(RX, BodyY, RW, 88));
        AddChild(RosterPreview().At(RX, BodyY + 100, RW, 170));
        AddChild(RegisterBox(v).At(RX, BodyY + 282, RW, 96));

        AddChild(Nav());
    }

    // ── A 세계관 띠 ──────────────────────────────────

    private static Control Strip()
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore };
        c.At(0, 0, W, StripH);

        var bg = new ColorRect { Color = CombatArt.Slab, MouseFilter = MouseFilterEnum.Ignore };
        bg.At(0, 0, W, StripH);
        c.AddChild(bg);
        var line = new ColorRect { Color = CombatArt.Edge, MouseFilter = MouseFilterEnum.Ignore };
        line.At(0, StripH - 1, W, 1);
        c.AddChild(line);

        c.AddChild(CombatArt.Text("★★★★★", 12, CombatArt.Gold).At(20, 8, 90, 18));
        c.AddChild(CombatArt.Text("4.9 · 누적 리뷰 1,048,576개 · 만물마켓 공식", 12, CombatArt.Dim)
            .At(118, 8, 500, 18));
        c.AddChild(CombatArt.Text("dev 빌드", 11, new Color("5e564a"), HorizontalAlignment.Right)
            .At(W - 140, 9, 120, 16));
        return c;
    }

    // ── B 히어로 ─────────────────────────────────────

    private static Control Hero()
    {
        // ClipContents 는 **부모가** 켜야 자식(배경 텍스처)이 잘린다. KeepAspectCovered 는 띠보다
        // 큰 그림을 그대로 그리므로, 이걸 빼면 시장 사진이 화면 전체로 흘러 본문이 씻긴다(실제로 겪었다).
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        c.At(0, HeroY, W, HeroH);

        var bg = new ColorRect { Color = new Color("0a0806"), MouseFilter = MouseFilterEnum.Ignore };
        bg.At(0, 0, W, HeroH);
        c.AddChild(bg);

        if (CombatArt.Load("res://assets/scene-market.png") is { } tex)
        {
            // ClipContents 가 없으면 KeepAspectCovered 가 띠 밖으로 흘러 화면 전체를 덮는다
            var plate = new TextureRect
            {
                Texture = tex,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
                ClipContents = true,
            };
            plate.At(0, 0, W, HeroH);
            c.AddChild(plate);
        }
        var vig = new ColorRect { Color = new Color(0.039f, 0.031f, 0.024f, 0.76f), MouseFilter = MouseFilterEnum.Ignore };
        vig.At(0, 0, W, HeroH);
        c.AddChild(vig);

        var edge = new ColorRect { Color = CombatArt.Edge, MouseFilter = MouseFilterEnum.Ignore };
        edge.At(0, HeroH - 1, W, 1);
        c.AddChild(edge);

        // 로고 — 웹판의 그라디언트 글자는 못 옮기므로 그림자를 깔아 두께를 만든다
        c.AddChild(CombatArt.Text("이세계 리뷰용사", 54, new Color(0, 0, 0, 0.6f), HorizontalAlignment.Center)
            .At(0, 48, W, 68));
        c.AddChild(CombatArt.Text("이세계 리뷰용사", 54, new Color("f6e3b4"), HorizontalAlignment.Center)
            .At(0, 45, W, 68));

        // 부제 = 도장. 커머스 세계관답게 뾰족한 판타지 대신 인주 냄새로
        var stamp = new Control { MouseFilter = MouseFilterEnum.Ignore };
        stamp.At((W - 200) / 2f, 142, 200, 38);
        stamp.PivotOffset = new Vector2(100, 19);
        stamp.Rotation = Mathf.DegToRad(-2.4f);
        stamp.Draw += () =>
        {
            var r = new Rect2(0, 0, 200, 38);
            stamp.DrawRect(r, new Color(0.478f, 0.227f, 0.173f, 0.16f));
            stamp.DrawRect(r, StampEdge, filled: false, width: 2f);
            stamp.DrawRect(new Rect2(4, 4, 192, 30), StampEdge with { A = 0.75f }, filled: false, width: 1f);
        };
        var sl = CombatArt.Text("리 뷰 가   무 기 다", 15, Stamp, HorizontalAlignment.Center);
        sl.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        sl.VerticalAlignment = VerticalAlignment.Center;
        stamp.AddChild(sl);
        c.AddChild(stamp);
        return c;
    }

    // ── C 리뷰어 카드 ────────────────────────────────

    private static Control ReviewerCard(HubView v)
    {
        var c = Card(LW, 120);
        var meta = RunStore.Meta;

        // 서명 썸네일 — 등록한 것이 없으면 「서명 없음」. 여기가 서명을 확인할 수 있는 유일한 자리다
        var sig = new Control { MouseFilter = MouseFilterEnum.Ignore };
        sig.At(14, 16, 132, 52);
        sig.Draw += () =>
        {
            sig.DrawStyleBox(CombatArt.Box(CombatArt.Parch, new Color("8a744d"), 4), new Rect2(0, 0, 132, 52));
        };
        if (v.Signed)
        {
            var ink = new SignatureInk();
            ink.At(6, 5, 120, 42);
            sig.AddChild(ink);
            ink.Ready += () => ink.Progress = 1f;
        }
        else
        {
            var none = CombatArt.Text("서명 없음", 11, new Color("a08f68"), HorizontalAlignment.Center);
            none.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            none.VerticalAlignment = VerticalAlignment.Center;
            sig.AddChild(none);
        }
        c.AddChild(sig);

        // 필명 — 이 세계에서 조회되지 않는 이름이라 금색으로 따로 세운다
        var font = CombatArt.Font();
        if (v.Pen is { } pen)
        {
            c.AddChild(CombatArt.Text("✍", 16, CombatArt.Dim).At(162, 18, 24, 22));
            float nw = font.GetStringSize(pen, HorizontalAlignment.Left, -1, 17).X;
            c.AddChild(CombatArt.Text(pen, 17, CombatArt.Gold).At(186, 17, nw + 8, 24));
            c.AddChild(CombatArt.Text("님의 만물대장", 16, CombatArt.Ink).At(186 + nw + 14, 18, 220, 22));
        }
        else
        {
            c.AddChild(CombatArt.Text("✍", 16, CombatArt.Dim).At(162, 18, 24, 22));
            c.AddChild(CombatArt.Text("미등록 리뷰어", 17, CombatArt.Gold).At(186, 17, 130, 24));
            c.AddChild(CombatArt.Text("— 첫 원정에서 등록합니다", 14, CombatArt.Dim).At(320, 19, 260, 22));
        }

        c.AddChild(CombatArt.Text($"명성 {N(meta.Rp)} RP   ·   협찬 {N(meta.P)} P", 14, Ledger).At(162, 46, 400, 20));

        c.Draw += () =>
        {
            c.DrawRect(new Rect2(14, 78, LW - 28, 1), CombatArt.Edge with { A = 0.7f });
        };

        int cards = GameData.Cards.ById.Count;
        c.AddChild(CombatArt.Text(
            $"원정 {N(meta.Runs)}   ·   생환 {N(meta.Wins)}   ·   최고 {(meta.BestFloor > 0 ? $"1막 {meta.BestFloor}층" : "—")}"
            + $"   ·   등재 {meta.Seen.Count}/{cards}   ·   기록 {meta.Badges.Count}/{BadgeTotal}",
            13, CombatArt.Dim).At(16, 90, LW - 32, 20));
        return c;
    }

    // ── D 이어하기 ───────────────────────────────────

    private Control ResumeCta(ResumeInfo resume, RunState run)
    {
        var c = Cta(LW, 94, primary: true);
        c.AddChild(CombatArt.Text("이어하기", 17, new Color("ffe9bd")).At(18, 12, 300, 24));
        c.AddChild(CombatArt.Text($"1막 {run.Floor}층   ·   {NextLine(run)}", 13, CtaSub).At(18, 38, LW - 60, 20));
        c.AddChild(CombatArt.Text($"🧠 {run.Will}/{run.MaxWill}   ·   🪙 {run.Gold}   ·   덱 {run.Deck.Count}장",
            13, CtaSub).At(18, 56, LW - 60, 20));
        c.AddChild(CombatArt.Text(resume.Label, 12, CtaNote).At(300, 12, LW - 340, 20));
        c.AddChild(CombatArt.Text("›", 22, CombatArt.Gold, HorizontalAlignment.Right).At(LW - 44, 34, 28, 28));
        Hit(c, () => SceneRouter.Go(resume.ScenePath));
        return c;
    }

    /// <summary>다음에 무엇을 하러 가는가 — 노드 진행 중이면 그 노드, 아니면 지금 고를 수 있는 종류들</summary>
    private static string NextLine(RunState run)
    {
        if (run.CurrentNode is { } node) return $"진행 중 {node.Type.Icon()} {node.Type.Label()}";
        var row = run.Map.Row(run.Floor);
        var open = RunStore.Current == run ? RunStore.Reachable() : Array.Empty<string>();
        var types = open.Select(id => row.FirstOrDefault(n => n.Id == id)?.Type)
            .Where(t => t is not null).Select(t => t!.Value).Distinct().ToList();
        if (types.Count == 0) return "다음 배송지 선택";
        return "다음 " + string.Join(" · ", types.Select(t => $"{t.Icon()} {t.Label()}"));
    }

    // ── E 새 원정 ────────────────────────────────────

    private Control NewRunCta(HubView v)
    {
        bool run = v.Run is not null;
        var c = Cta(LW, run ? 88 : 72, primary: !run);
        c.AddChild(CombatArt.Text("새 원정", 17, run ? CombatArt.Ink : new Color("ffe9bd")).At(18, 12, 300, 24));
        c.AddChild(CombatArt.Text(v.Pen is null ? "당신이 어떻게 여기 왔는지부터 시작합니다" : "만물대장을 들고 던전으로",
            13, run ? CombatArt.Dim : CtaSub).At(18, 38, LW - 60, 20));
        if (run)
            c.AddChild(CombatArt.Text("⚠ 진행 중인 원정이 사라집니다.", 12, new Color("e08a72")).At(18, 60, 400, 20));
        c.AddChild(CombatArt.Text("›", 22, CombatArt.Gold, HorizontalAlignment.Right)
            .At(LW - 44, (run ? 88 : 72) / 2f - 14, 28, 28));
        Hit(c, OnNewRun);
        return c;
    }

    private void OnNewRun()
    {
        // 필명이 없으면 **프롤로그 → 서명 등록** 순이다 (ADR-022). 이름을 먼저 받으면 「왜 내가
        // 여기 있는가」를 모른 채 필명부터 짓게 된다 — 프롤로그 P14 의 결론이 곧 등록의 이유다.
        // 등록 화면이 끝나면 스스로 새 런을 깔고 지도로 보낸다.
        if (!RunStore.Registered)
        {
            if (SceneRouter.Exists(SceneRouter.Prologue)) { SceneRouter.Go(SceneRouter.Prologue); return; }
            if (SceneRouter.Exists(SceneRouter.Signature)) { SceneRouter.Go(SceneRouter.Signature); return; }
        }
        // 이미 등록한 사람에게는 프롤로그를 다시 보이지 않는다 — 2회차를 붙잡지 않는다
        RunStore.NewRun();
        SceneRouter.GoMap();
    }

    // ── F 대원 소환 잠금 스트립 (ADR-028 · Layer 1.5) ─

    private static Control SummonLock()
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore };
        c.CustomMinimumSize = new Vector2(LW, 76);
        c.Draw += () =>
        {
            c.DrawRect(new Rect2(0, 0, LW, 76), new Color(0, 0, 0, 0.28f));
            DashRect(c, new Rect2(0, 0, LW, 76), CombatArt.Edge with { A = 0.85f }, 5, 4);
        };
        c.AddChild(CombatArt.Text("🕯", 20, CombatArt.Dim).At(18, 26, 30, 26));
        c.AddChild(CombatArt.Text("대원 소환 — 다른 세계의 리뷰어", 14, new Color("9a8f7d")).At(56, 12, 400, 20));
        c.AddChild(CombatArt.Text("소환사는 아직 다음 사람을 부르지 못했다.", 12, LockDim).At(56, 34, 460, 18));
        c.AddChild(CombatArt.Text("대원마다 전생의 리뷰 12장과 습관 하나를 들고 온다.", 12, LockDim).At(56, 52, 460, 18));
        c.AddChild(Hub.Chip("Layer 1.5 예정").At(LW - 136, 26, 120, 24));
        return c;
    }

    // ── G 수집·업적 타일 ─────────────────────────────

    private static Control Tiles()
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore };
        c.CustomMinimumSize = new Vector2(RW, 88);
        var meta = RunStore.Meta;
        float tw = (RW - 12) / 2f;

        var items = new (string Icon, string Key, int V, int Total, string Unit)[]
        {
            ("📕", "등재된 리뷰", meta.Seen.Count, GameData.Cards.ById.Count, "종"),
            ("🏅", "등재 기록", meta.Badges.Count, BadgeTotal, "건"),
        };
        for (int i = 0; i < items.Length; i++)
        {
            var (icon, key, val, total, unit) = items[i];
            var t = new Control { MouseFilter = MouseFilterEnum.Ignore };
            t.At(i * (tw + 12), 0, tw, 88);
            float ratio = total > 0 ? Mathf.Clamp(val / (float)total, 0, 1) : 0;
            t.Draw += () =>
            {
                t.DrawStyleBox(CombatArt.Box(new Color(0, 0, 0, 0.30f), CombatArt.Edge, 6), new Rect2(0, 0, tw, 88));
                t.DrawStyleBox(CombatArt.Box(new Color(0.11f, 0.10f, 0.08f), null, 2), new Rect2(13, 68, tw - 26, 6));
                t.DrawStyleBox(CombatArt.Box(new Color("c39a52"), null, 2), new Rect2(13, 68, (tw - 26) * ratio, 6));
            };
            t.AddChild(CombatArt.Text($"{icon} {key}", 12, CombatArt.Dim).At(13, 11, tw - 90, 18));
            t.AddChild(CombatArt.Text("🔒 준비 중", 10, LockDim, HorizontalAlignment.Right).At(tw - 90, 12, 77, 16));
            t.AddChild(CombatArt.Text($"{val}", 21, CombatArt.Ink).At(13, 32, 70, 28));
            t.AddChild(CombatArt.Text($"/{total}{unit}", 11, CombatArt.Dim)
                .At(15 + CombatArt.Font().GetStringSize($"{val}", HorizontalAlignment.Left, -1, 21).X, 43, 90, 18));
            t.Modulate = new Color(1, 1, 1, 0.78f);
            c.AddChild(t);
        }
        return c;
    }

    // ── H 명단 프리뷰 ────────────────────────────────

    private static Control RosterPreview()
    {
        var c = new Control();
        c.CustomMinimumSize = new Vector2(RW, 170);
        c.Draw += () =>
        {
            c.DrawStyleBox(CombatArt.Box(new Color(0, 0, 0, 0.30f), CombatArt.Edge, 6), new Rect2(0, 0, RW, 170));
            c.DrawRect(new Rect2(0, 34, RW, 1), CombatArt.Edge);
        };
        c.AddChild(CombatArt.Text("환불원정대 명단", 13, CombatArt.Gold).At(14, 9, 220, 20));

        var all = Hub.NavBtn("전체 보기 ›", false, () => SceneRouter.Go(SceneRouter.Board));
        all.At(RW - 108, 5, 94, 24);
        c.AddChild(all);

        var rows = Roster.Top(3);
        for (int i = 0; i < rows.Count; i++)
        {
            var e = rows[i];
            var r = new Control { MouseFilter = MouseFilterEnum.Ignore };
            r.At(0, 35 + i * 45, RW, 45);
            if (e.Me)
            {
                var mark = new ColorRect { Color = new Color(0.88f, 0.66f, 0.29f, 0.07f), MouseFilter = MouseFilterEnum.Ignore };
                mark.At(1, 0, RW - 2, 44);
                r.AddChild(mark);
            }
            if (i < rows.Count - 1)
            {
                var line = new ColorRect { Color = CombatArt.Edge with { A = 0.24f }, MouseFilter = MouseFilterEnum.Ignore };
                line.At(14, 44, RW - 28, 1);
                r.AddChild(line);
            }
            r.AddChild(CombatArt.Text($"{i + 1}", 13, i == 0 ? CombatArt.Gold : LockDim, HorizontalAlignment.Center)
                .At(8, 13, 26, 18));
            r.AddChild(CombatArt.Text($"✍ {e.Name}", 13, CombatArt.Ink).At(40, 13, 210, 18));
            if (e.Me)
            {
                float nw = CombatArt.Font().GetStringSize($"✍ {e.Name}", HorizontalAlignment.Left, -1, 13).X;
                var badge = CombatArt.Slabbed(CombatArt.Gold, null, 3);
                badge.At(48 + nw, 14, 24, 17);
                r.AddChild(badge);
                r.AddChild(CombatArt.Text("나", 10, new Color("1c1409"), HorizontalAlignment.Center)
                    .At(48 + nw, 15, 24, 16));
            }
            r.AddChild(CombatArt.Text($"1막 {e.Floor}층", 12, CombatArt.Dim, HorizontalAlignment.Right)
                .At(RW - 170, 14, 90, 18));
            r.AddChild(CombatArt.Text(Roster.FateLabel(e.Fate), 12, Roster.FateColor(e.Fate), HorizontalAlignment.Right)
                .At(RW - 78, 14, 64, 18));
            c.AddChild(r);
        }
        return c;
    }

    // ── 서명 등록 진입점 ─────────────────────────────

    private static Control RegisterBox(HubView v)
    {
        var c = Card(RW, 96);
        bool has = v.Pen is not null;
        c.AddChild(CombatArt.Text("만물대장 · 리뷰어 등록", 13, CombatArt.Gold).At(16, 12, 300, 20));
        c.AddChild(CombatArt.Text(
            has ? "필명은 저쪽 세계에서 쓰던 이름이다. 이 세계에서는 조회되지 않는다."
                : "이름은 사람들이 보고, 서명은 대장이 대조한다. 둘을 한 장에서 받는다.",
            11, CombatArt.Dim, wrap: true).At(16, 34, RW - 32, 32));

        var b = Hub.NavBtn(has ? "필명·서명 다시 만들기" : "리뷰어 등록하기", true,
            SceneRouter.Exists(SceneRouter.Signature) ? () => SceneRouter.Go(SceneRouter.Signature) : null);
        b.At(16, 62, 190, 26);
        c.AddChild(b);
        return c;
    }

    // ── I 하단 내비 ──────────────────────────────────

    private Control Nav()
    {
        var c = new Control();
        c.At(0, NavY, W, NavH);

        var bg = new ColorRect { Color = CombatArt.Slab, MouseFilter = MouseFilterEnum.Ignore };
        bg.At(0, 0, W, NavH);
        c.AddChild(bg);
        var line = new ColorRect { Color = CombatArt.Edge, MouseFilter = MouseFilterEnum.Ignore };
        line.At(0, 0, W, 1);
        c.AddChild(line);

        var items = new (string Icon, string Text, Action? Go)[]
        {
            ("📕", "만물대장", () => SceneRouter.Go(SceneRouter.Codex)),
            ("🏅", "등재 기록", () => SceneRouter.Go(SceneRouter.Badges)),
            ("👥", "원정대 명단", () => SceneRouter.Go(SceneRouter.Board)),
            ("🧾", "계정", () => SceneRouter.Go(SceneRouter.Account)),
            ("⚙", "설정", () => SceneRouter.Go(SceneRouter.Settings)),
            ("⏻", "종료", () => GetTree().Quit()),
        };
        float cw = W / (float)items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            var (icon, text, go) = items[i];
            var cell = new Control { MouseFilter = MouseFilterEnum.Ignore };
            cell.At(i * cw, 0, cw, NavH);
            cell.AddChild(CombatArt.Text(icon, 16, go is null ? LockDim : CombatArt.Dim, HorizontalAlignment.Center)
                .At(0, 12, cw, 20));
            cell.AddChild(CombatArt.Text(text, 11, go is null ? LockDim : CombatArt.Dim, HorizontalAlignment.Center)
                .At(0, 36, cw, 18));
            if (i < items.Length - 1)
            {
                var sep = new ColorRect { Color = CombatArt.Edge with { A = 0.28f }, MouseFilter = MouseFilterEnum.Ignore };
                sep.At(cw - 1, 10, 1, NavH - 20);
                cell.AddChild(sep);
            }
            if (go is not null)
            {
                var hit = new Button { FocusMode = FocusModeEnum.None, Flat = true };
                hit.AddThemeStyleboxOverride("normal", CombatArt.Box(new Color(0, 0, 0, 0), null, 0));
                hit.AddThemeStyleboxOverride("hover", CombatArt.Box(new Color(1, 1, 1, 0.04f), null, 0));
                hit.AddThemeStyleboxOverride("pressed", CombatArt.Box(new Color(0, 0, 0, 0.3f), null, 0));
                hit.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                hit.Pressed += go;
                cell.MouseFilter = MouseFilterEnum.Pass;
                cell.AddChild(hit);
            }
            c.AddChild(cell);
        }
        return c;
    }

    // ── 공통 조각 ────────────────────────────────────

    /// <summary>만물대장 레이어의 판 하나 (index.html .card)</summary>
    private static Control Card(float w, float h)
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore };
        c.CustomMinimumSize = new Vector2(w, h);
        c.Draw += () => c.DrawStyleBox(CombatArt.Box(new Color(0.10f, 0.09f, 0.07f, 0.92f), CombatArt.Edge, 6),
            new Rect2(0, 0, w, h));
        return c;
    }

    /// <summary>큰 행동 버튼 한 장 (index.html .cta)</summary>
    private static Control Cta(float w, float h, bool primary)
    {
        var c = new Control();
        c.CustomMinimumSize = new Vector2(w, h);
        var bg = primary ? new Color(0.33f, 0.24f, 0.09f) : new Color(0.16f, 0.13f, 0.09f);
        var border = primary ? CombatArt.Gold : CombatArt.EdgeHi;
        c.Draw += () => c.DrawStyleBox(CombatArt.Box(bg, border, 6), new Rect2(0, 0, w, h));
        return c;
    }

    /// <summary>판 전체를 덮는 투명 버튼 — 판을 Button 으로 만들면 자식 배치가 스타일박스에 끌려간다</summary>
    private static void Hit(Control host, Action onPressed)
    {
        var b = new Button { FocusMode = FocusModeEnum.None, Flat = true };
        b.AddThemeStyleboxOverride("normal", CombatArt.Box(new Color(0, 0, 0, 0), null, 6));
        b.AddThemeStyleboxOverride("hover", CombatArt.Box(new Color(1, 1, 1, 0.07f), null, 6));
        b.AddThemeStyleboxOverride("pressed", CombatArt.Box(new Color(0, 0, 0, 0.25f), null, 6));
        b.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        b.Pressed += onPressed;
        host.AddChild(b);
    }

    private static void DashRect(Control c, Rect2 r, Color col, float on, float off)
    {
        Vector2 a = r.Position, b = r.Position + new Vector2(r.Size.X, 0);
        Vector2 d = r.Position + r.Size, e = r.Position + new Vector2(0, r.Size.Y);
        foreach (var (p, q) in new[] { (a, b), (b, d), (d, e), (e, a) })
        {
            float len = p.DistanceTo(q);
            var dir = (q - p) / len;
            for (float t = 0; t < len; t += on + off)
                c.DrawLine(p + dir * t, p + dir * Mathf.Min(len, t + on), col, 1f);
        }
    }

    private static string N(int v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
}
