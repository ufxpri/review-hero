// 배송 조회 — 지도는 「남의 운송장을 조회한 화면」이다. (ADR-024 ① · ADR-029 4차)
//
// 이관 원본: ui/game/map.html.
//
// ── 이 화면이 무엇인가 ──────────────────────────────────
// 노드는 경유지, 간선은 배송 경로, 현재 위치는 트럭, 최종 목적지는 6층 지배인실이다.
// 그 택배는 보스에게 가는 보급품이고, 경로를 흘린 것은 물류 담당(택배좌)이다 — 주인공은
// 남의 운송장을 조회하며 그 택배와 같은 길로 올라간다. 지도를 본다는 행위 자체가 서사다.
//
// ── 화면에서 유일하게 색이 뒤집히는 곳 ──────────────────
// 운송장은 실제로 손에 들린 물류 용지다. 그래서 이 블록만 배경이 종이(res://assets/paper-waybill.png)
// 이고 **글자가 잉크**(어두운 남색 #232a35)다. 금색을 그대로 두면 종이가 화면에 먹힌다.
// 종이 텍스처는 ui/assets 의 심볼릭 링크라 없을 수 있다 — CombatArt.Load 가 null 을 주면
// 단색(#e0d4b2)으로 굴러간다. 링크가 끊긴 환경에서도 화면은 살아야 한다.
//
// ── 간선은 MapNode.Next 로만 그린다 ─────────────────────
// 인접 층을 전결합으로 잇지 않는다. 실제 경로가 아닌 선을 그리면 「갈 수 있어 보이는데
// 못 가는 길」이 생기고, 그것이 곧 Reachable() 의 세이브 스커밍 차단과 어긋난다.
// Next 가 비어 있는 옛 세이브만 전결합으로 폴백한다(웹판과 같은 처리).
//
// ── 디버그 (스크린샷 검증용) ────────────────────────────
//   --rh-newrun            런이 없으면(또는 있어도) 새 런을 깐다
//   --rh-seed=42           위 런의 시드
//   --rh-floor=4           지나온 길이 보이게 4층까지 자동으로 걸어 둔다
//   --rh-issue[=done]      발급 연출을 강제로 띄운다(done 이면 애니메이션 없이 완성 상태)

using Godot;
using ReviewHero.Engine;
using ReviewHero.Game.Combat;   // CombatArt — 색·조각 사전 / CombatEntry — 인자 파싱
using ReviewHero.Game.Run;

namespace ReviewHero.Game;

public partial class Map : Control
{
    private const int W = CombatArt.ScreenW;    // 1344
    private const int H = CombatArt.ScreenH;    // 768
    private const int ColW = 1160;
    private const int ColX = (W - ColW) / 2;    // 92

    // 운송장 — 종이 한 장
    private const int WbY = 52, WbH = 200;
    private const int WbPad = 30;               // 천공(16px) 안쪽 여백

    // 서사 한 줄
    private const int LeakY = 258;

    // 경로 그래프 — 위(6F 목적지)에서 아래(1F 집화)로 쌓는다
    private const int RouteY = 286, RowH = 62, RowGap = 12;
    private const int GutW = 100, GutGap = 10;
    private const int NodesX = ColX + GutW + GutGap;
    private const int NodesW = ColW - GutW - GutGap;
    private const int NodeGap = 12;
    private const int OriginY = RouteY + 6 * RowH + 5 * RowGap + 6;

    // ── 종이 위의 색 (map.html .waybill 의 CSS 변수) ──
    private static readonly Color Paper = new("e0d4b2");
    private static readonly Color WbInk = new("232a35");
    private static readonly Color WbInkB = new("151b26");
    private static readonly Color WbDim = new("6d6353");
    private static readonly Color WbPast = new("4b4536");
    private static readonly Color WbStep = new("6f6552");
    private static readonly Color WbLine = new(72f / 255f, 56f / 255f, 34f / 255f, 0.38f);
    private static readonly Color WbRed = new("a8321f");

    // ── 간선 색 (map.html .route path) ──
    private static readonly Color EdgeDone = new("c39a52");
    private static readonly Color EdgeAhead = new("5b5041");
    private static readonly Color EdgeMiss = new("3d372d");

    // ── 6F 목적지 행 (map.html .row.dest) ──
    private static readonly Color DestEdge = new("7a3a2c");
    private static readonly Color DestType = new("e08b72");
    private static readonly Color DestName = new("f0c9a0");

    /// <summary>층 = 배송 단계. 물류 용어로 읽히게 두되 층 번호는 지우지 않는다</summary>
    private static readonly string[] Stage =
        { "집화 처리", "간선 상차", "중간 분류", "간선 하차", "배송 준비", "인수 예정" };

    private static readonly Dictionary<NodeType, string> NodeDesc = new()
    {
        [NodeType.Event] = "확인되지 않은 취급 구간",
        [NodeType.Shop] = "취급점 — 카드·소모품",
        [NodeType.Rest] = "휴게 시설 — 의지 회복",
    };

    /// <summary>상품 별점 — 노드 종류 기준(위험할수록 「프리미엄」)</summary>
    private static int TypeStars(NodeType t) => t switch
    {
        NodeType.Boss => 5,
        NodeType.Elite => 3,
        NodeType.Battle => 2,
        _ => 0,
    };

    /// <summary>
    /// 발급 연출을 이미 보여 준 런. RunState 에 필드를 새로 심을 수 없어(다른 담당 소관)
    /// 프로세스 수명 동안만 기억한다 — 같은 런이라도 게임을 껐다 켜면 1층 도중에 한 번 더 나온다.
    /// </summary>

    private Control? _issue;

    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();
        UiTheme.Apply(this);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var args = Godot.OS.GetCmdlineUserArgs();
        ApplyDebugSwitch(args);

        var run = RunStore.Current;
        if (run?.Ended is not null) { SceneRouter.GoResult(); return; }   // 정산 전 — 지도는 잠긴다

        Build(run);

        if (run is not null && WantIssue(run, args)) ShowIssue(run, args);
    }

    // ── 디버그 스위치 ────────────────────────────────

    private static void ApplyDebugSwitch(IReadOnlyList<string> args)
    {
        if (!CombatEntry.HasFlag(args, "newrun")) return;
        uint seed = uint.TryParse(CombatEntry.ArgValue(args, "seed"), out uint s) ? s : 42u;
        RunStore.NewRun(seed);

        // 지나온 길(금색 실선)과 앞길(점선)이 한 화면에 같이 보여야 경로 그래프를 눈으로 검증할 수 있다.
        // 규칙은 그대로 쓴다 — Reachable → EnterNode → CompleteNode 만 부른다.
        if (!int.TryParse(CombatEntry.ArgValue(args, "floor"), out int target)) return;
        for (int guard = 0; guard < 40; guard++)
        {
            var run = RunStore.Current;
            if (run is null || run.Ended is not null || run.Floor >= target) break;
            var open = RunStore.Reachable();
            if (open.Count == 0) break;
            if (!RunStore.EnterNode(open[open.Count / 2], navigate: false)) break;
            RunStore.CompleteNode();
        }
    }

    private static bool WantIssue(RunState run, IReadOnlyList<string> args)
    {
        if (CombatEntry.ArgValue(args, "issue") is not null || CombatEntry.HasFlag(args, "issue")) return true;
        // 런당 첫 지도 방문 = 아직 아무 경유지도 끝내지 않은 1층.
        // 플래그를 세이브에 남겨 게임을 껐다 켜도 다시 나오지 않게 한다 (웹판 issueWaybill 과 동일)
        if (run.WaybillIssued) return false;
        if (run.Floor != 1 || run.Path.Count > 0 || run.Pos is not null) return false;
        run.WaybillIssued = true;
        RunStore.Save();
        return true;
    }

    // ── 조립 ─────────────────────────────────────────

    private void Build(RunState? run)
    {
        foreach (var c in GetChildren()) c.QueueFree();

        AddChild(Backdrop());
        AddChild(Hub.TopBar("map", run));

        if (run is null) { AddChild(NoRun()); return; }

        string no = OrderNo(run);
        AddChild(Waybill(run, no));
        AddChild(Leak());

        // 전용 씬이 없는 노드에 발이 묶였을 때만 — 흐름이 끊기지 않는 것이 우선이다
        var pending = run.CurrentNode;
        if (pending is not null && !SceneRouter.HasSceneFor(pending)) { AddChild(NodePanel(run, pending)); return; }

        AddChild(Route(run));
    }

    private static string OrderNo(RunState run) => $"RH-{run.Act}A-{run.Seed % 10000:0000}";

    /// <summary>어두운 물류 창고 위에 조회 화면이 떠 있다 (map.html body 배경)</summary>
    private static Control Backdrop()
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore };
        c.At(0, 0, W, H);

        var bg = new ColorRect { Color = CombatArt.Bg, MouseFilter = MouseFilterEnum.Ignore };
        bg.At(0, 0, W, H);
        c.AddChild(bg);

        if (CombatArt.Load("res://assets/map-dispatch.png") is { } tex)
        {
            var plate = new TextureRect
            {
                Texture = tex,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            plate.At(0, 0, W, H);
            c.AddChild(plate);
        }

        var veil = new ColorRect { Color = new Color(0.055f, 0.048f, 0.04f, 0.91f), MouseFilter = MouseFilterEnum.Ignore };
        veil.At(0, 0, W, H);
        c.AddChild(veil);
        return c;
    }

    // ── ① 운송장 ─────────────────────────────────────

    private static Control Waybill(RunState run, string no)
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore };
        c.At(ColX, WbY, ColW, WbH);

        int floor = Mathf.Clamp(run.Floor, 1, 6);
        var tex = CombatArt.Load("res://assets/paper-waybill.png");

        const float GridTop = 112f, GridBot = 168f;
        const float StepY = 176f;
        float cellW = (ColW - WbPad * 2) / 4f;
        float stepW = (ColW - WbPad * 2) / 6f;

        // 종이 자체를 여기서 그린다 — 자식(글자)보다 먼저 깔려야 잉크가 종이 위에 얹힌다
        c.Draw += () =>
        {
            var full = new Rect2(0, 0, ColW, WbH);
            c.DrawRect(full, Paper);
            if (tex is not null) c.DrawTextureRect(tex, full, tile: false, new Color(1, 1, 1, 0.92f));

            // 천공 — 연속 용지의 스프로킷 구멍. 좌우 세로 띠
            for (float y = 9; y < WbH; y += 18)
            {
                c.DrawCircle(new Vector2(8, y), 2.3f, new Color(0.18f, 0.14f, 0.08f, 0.55f));
                c.DrawCircle(new Vector2(ColW - 8, y), 2.3f, new Color(0.18f, 0.14f, 0.08f, 0.55f));
            }
            Dash(c, new Vector2(16, 0), new Vector2(16, WbH), WbLine, 1, 3, 3);
            Dash(c, new Vector2(ColW - 16, 0), new Vector2(ColW - 16, WbH), WbLine, 1, 3, 3);

            // 바코드 — 도트프린터가 찍은 것이라 잉크 한 색이다
            for (float x = 0; x < 190; x += 14)
            {
                c.DrawRect(new Rect2(WbPad + x, 63, 2, 17), WbInk with { A = 0.66f });
                c.DrawRect(new Rect2(WbPad + x + 4, 63, 1, 17), WbInk with { A = 0.66f });
                c.DrawRect(new Rect2(WbPad + x + 9, 63, 3, 17), WbInk with { A = 0.66f });
            }

            // 서식 괘선
            Dash(c, new Vector2(24, 106), new Vector2(ColW - 24, 106), WbLine, 1, 5, 4);
            Dash(c, new Vector2(24, GridBot), new Vector2(ColW - 24, GridBot), WbLine, 1, 5, 4);
            for (int i = 1; i < 4; i++)
            {
                float x = WbPad + cellW * i - 8;
                Dash(c, new Vector2(x, GridTop), new Vector2(x, GridBot - 4), WbLine, 1, 4, 4);
            }

            // 진행 단계 띠 — 배송 조회의 그 막대. 종이 위이므로 잉크 두 색으로만 찍는다
            for (int i = 1; i < 6; i++)
            {
                float x0 = WbPad + stepW * (i - 1) + stepW / 2f;
                float x1 = WbPad + stepW * i + stepW / 2f;
                bool passed = i < floor;
                c.DrawLine(new Vector2(x0, StepY), new Vector2(x1, StepY),
                    passed ? new Color(0.24f, 0.19f, 0.12f, 0.60f) : new Color(0.28f, 0.22f, 0.13f, 0.30f), 1f);
            }
            for (int i = 0; i < 6; i++)
            {
                int f = i + 1;
                var p = new Vector2(WbPad + stepW * i + stepW / 2f, StepY);
                if (f < floor) c.DrawCircle(p, 5.5f, WbInk);
                else if (f == floor)
                {
                    c.DrawCircle(p, 9f, WbRed with { A = 0.18f });
                    c.DrawCircle(p, 6f, WbRed);
                }
                else
                {
                    c.DrawCircle(p, 5.5f, new Color(1f, 0.98f, 0.93f, 0.35f));
                    c.DrawArc(p, 5.5f, 0, Mathf.Tau, 20, new Color(0.24f, 0.19f, 0.12f, 0.5f), 1f, true);
                }
            }
        };

        // ── 운송장 번호 ──
        c.AddChild(CombatArt.Text("운송장 번호", 10, WbDim).At(WbPad, 12, 220, 14));
        c.AddChild(CombatArt.Text(no, 26, WbInk).At(WbPad, 26, 340, 34));
        c.AddChild(CombatArt.Text("만물마켓 물류 · 1막 구간 조회", 11, WbDim).At(WbPad, 88, 340, 16));

        // ── 지금 상태 + 무단 조회 도장 ──
        var seal = Seal();
        seal.At(ColW - WbPad - 66, 22, 66, 48);
        c.AddChild(seal);

        c.AddChild(CombatArt.Text(run.Floor >= 6 ? "배송지 도착" : "배송 중", 17, WbInk, HorizontalAlignment.Right)
            .At(ColW - WbPad - 480, 26, 390, 24));
        c.AddChild(CombatArt.Text($"1막 {floor}/6 구간 · {Stage[floor - 1]}", 11, WbDim, HorizontalAlignment.Right)
            .At(ColW - WbPad - 480, 54, 390, 16));

        // ── 4칸 서식 ──
        var cells = new (string K, string V, bool Strong, string V2)[]
        {
            ("보내는 사람", "발송인 미표기", false, "물류 담당 직인만 찍혀 있다"),
            ("받는 사람", "본사 직영 · 답글 없는 사장", true, "6F 지배인실 — 관계자 외 출입 금지"),
            ("품목", "보급품 · 내용 미상", false, "파손 주의 · 개봉 시 반품 불가"),
            ("조회자", RunStore.Registered ? RunStore.Penname : "미등록", false,
                $"의지 {run.Will}/{run.MaxWill} · 소지금 {run.Gold} · 카드 {run.Deck.Count}장"),
        };
        for (int i = 0; i < cells.Length; i++)
        {
            var (k, v, strong, v2) = cells[i];
            float x = WbPad + cellW * i;
            float w = cellW - 20;
            c.AddChild(CombatArt.Text(k, 10, WbDim).At(x, GridTop + 4, w, 14));
            var vl = CombatArt.Text(v, 13, strong ? WbInkB : WbInk).At(x, GridTop + 20, w, 18);
            vl.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            c.AddChild(vl);
            var v2l = CombatArt.Text(v2, 10, WbDim).At(x, GridTop + 40, w, 14);
            v2l.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            c.AddChild(v2l);
        }

        // ── 단계 라벨 ──
        for (int i = 0; i < 6; i++)
        {
            int f = i + 1;
            var col = f < floor ? WbPast : f == floor ? WbRed : WbStep;
            c.AddChild(CombatArt.Text($"{f}F {Stage[i]}", 10, col, HorizontalAlignment.Center)
                .At(WbPad + stepW * i, StepY + 8, stepW, 16));
        }

        return c;
    }

    /// <summary>붉은 잉크가 번진 고무 도장. 사람이 찍은 것이니 기울어 있다</summary>
    private static Control Seal(int size = 13)
    {
        var s = new Control { MouseFilter = MouseFilterEnum.Ignore };
        s.CustomMinimumSize = new Vector2(66, 48);
        s.PivotOffset = new Vector2(33, 24);
        s.Rotation = Mathf.DegToRad(-8.5f);
        s.Draw += () =>
        {
            var r = new Rect2(0, 0, s.Size.X, s.Size.Y);
            s.DrawRect(r, WbRed with { A = 0.10f });
            s.DrawRect(r, WbRed with { A = 0.72f }, filled: false, width: 2f);
        };
        var l = CombatArt.Text("무단\n조회", size, WbRed, HorizontalAlignment.Center);
        l.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        l.VerticalAlignment = VerticalAlignment.Center;
        s.AddChild(l);
        return s;
    }

    /// <summary>서사 한 줄 — 매번 읽히는 자리라 짧게 (worldview §5.3 간결한 평서체)</summary>
    private static Control Leak()
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore };
        c.At(ColX, LeakY, ColW, 22);
        c.AddChild(CombatArt.Text(
            "이 경로를 흘린 것은 물류 담당이다. 남의 운송장이고, 나는 이 택배와 같은 길로 6층까지 올라간다.",
            12, new Color("b5a68c")).At(2, 2, 900, 18));
        c.AddChild(Hub.Chip("열람 기록 남지 않음").At(ColW - 150, 0, 150, 22));
        return c;
    }

    // ── ② 경로 그래프 ────────────────────────────────

    private Control Route(RunState run)
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore };
        c.At(0, 0, W, H);

        var open = RunStore.Reachable().ToHashSet(StringComparer.Ordinal);
        var origin = new Vector2(NodesX + 5, OriginY + 4);

        // 간선이 먼저 — 경유지 카드 밑으로 깔린다
        c.AddChild(Edges(run, origin));

        for (int f = 1; f <= 6; f++)
        {
            var row = run.Map.Row(f);
            bool here = f == run.Floor;
            c.AddChild(Gutter(f, here));

            for (int i = 0; i < row.Count; i++)
            {
                var node = row[i];
                string state = f < run.Floor ? (node.Visited ? "done" : "miss")
                    : f == run.Floor ? (open.Contains(node.Id) ? "sel" : "miss")
                    : "ahead";
                c.AddChild(NodeCard(node, state, f == 6, NodeRect(f, i, row.Count)));
            }
        }

        // 출발지 표식
        var pin = new Control { MouseFilter = MouseFilterEnum.Ignore };
        pin.At(ColX, OriginY, ColW, 20);
        pin.Draw += () =>
        {
            pin.DrawCircle(new Vector2(GutW + GutGap + 5, 9), 6.5f, CombatArt.EdgeHi with { A = 0.18f });
            pin.DrawCircle(new Vector2(GutW + GutGap + 5, 9), 4.5f, CombatArt.EdgeHi);
        };
        pin.AddChild(CombatArt.Text("발송", 11, CombatArt.Dim, HorizontalAlignment.Right).At(0, 2, GutW - 10, 16));
        pin.AddChild(CombatArt.Text("집화 접수 — 이 지점부터 추적이 시작된다", 10, CombatArt.Dim)
            .At(GutW + GutGap + 18, 2, 520, 16));
        c.AddChild(pin);

        return c;
    }

    private static Rect2 NodeRect(int floor, int index, int count)
    {
        float w = (NodesW - (count - 1) * NodeGap) / (float)Math.Max(1, count);
        return new Rect2(NodesX + index * (w + NodeGap), RouteY + (6 - floor) * (RowH + RowGap), w, RowH);
    }

    /// <summary>층 안내 — 층 번호는 지우지 않되 물류 단계로 읽힌다</summary>
    private static Control Gutter(int floor, bool here)
    {
        var g = new Control { MouseFilter = MouseFilterEnum.Ignore };
        g.At(ColX, RouteY + (6 - floor) * (RowH + RowGap), GutW, RowH);

        var head = floor == 6 ? DestType : here ? CombatArt.Gold : CombatArt.Dim;
        g.AddChild(CombatArt.Text($"{floor}F", 15, head, HorizontalAlignment.Right).At(0, 2, GutW - 10, 20));
        g.AddChild(CombatArt.Text(Stage[floor - 1], 10, here ? new Color("b5a68c") : new Color("6f6656"),
            HorizontalAlignment.Right).At(0, 22, GutW - 10, 14));

        if (here)
        {
            var chip = new Control { MouseFilter = MouseFilterEnum.Ignore };
            chip.At(GutW - 84, 38, 74, 20);
            chip.Draw += () => chip.DrawStyleBox(CombatArt.Box(CombatArt.Gold, null, 4), new Rect2(0, 0, 74, 20));
            var l = CombatArt.Text("🚚 현재 위치", 10, new Color("1c1408"), HorizontalAlignment.Center);
            l.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            l.VerticalAlignment = VerticalAlignment.Center;
            chip.AddChild(l);
            g.AddChild(chip);
        }
        else if (floor == 6)
        {
            g.AddChild(CombatArt.Text("지배인실", 10, new Color("6f6656"), HorizontalAlignment.Right)
                .At(0, 36, GutW - 10, 14));
        }
        return g;
    }

    /// <summary>경유지 카드. 선택 가능한 것만 눌린다 — 경로 밖은 볼 수는 있어도 갈 수 없다</summary>
    private Control NodeCard(MapNode node, string state, bool dest, Rect2 rect)
    {
        bool sel = state == "sel";
        var card = new Control { MouseFilter = MouseFilterEnum.Ignore };
        card.At(rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y);

        var border = dest ? DestEdge : CombatArt.Edge;
        var bg = dest ? new Color(0.30f, 0.13f, 0.10f, 0.42f) : new Color(0.12f, 0.11f, 0.09f, 0.72f);
        if (state == "done") { border = new Color("4d6b50"); bg = new Color(0.31f, 0.60f, 0.37f, 0.10f); }
        if (sel)
        {
            border = dest ? new Color("c2452e") : CombatArt.Gold;
            bg = dest ? new Color(0.55f, 0.22f, 0.15f, 0.24f) : new Color(0.55f, 0.42f, 0.18f, 0.20f);
        }

        var panel = CombatArt.Slabbed(bg, border, 6);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        card.AddChild(panel);

        float w = rect.Size.X;
        var typeCol = dest ? DestType : CombatArt.Ink;
        card.AddChild(CombatArt.Text($"{node.Type.Icon()} {node.Type.Label()}", 13, typeCol).At(12, 7, w - 110, 18));

        string badge = state switch { "done" => "통과 ✓", "miss" => "미경유", "sel" => "선택 가능", _ => "예정" };
        var badgeCol = state == "done" ? CombatArt.Green : sel ? CombatArt.Gold : CombatArt.Dim;
        card.AddChild(Hub.Chip(badge, badgeCol).At(w - 84, 8, 72, 20));

        if (node.Enemy is { } e)
        {
            card.AddChild(CombatArt.Text(GameData.EnemyName(e), 13, dest ? DestName : CombatArt.Ink)
                .At(12, 26, w - 24, 18));
            card.AddChild(Hub.Stars(TypeStars(node.Type), 12).At(12, 43, 68, 16));
            card.AddChild(CombatArt.Text($"🧠 의지 {GameData.EnemyWill(e)}", 11, CombatArt.Dim).At(84, 44, 120, 16));
        }
        else
        {
            card.AddChild(CombatArt.Text(NodeDesc.GetValueOrDefault(node.Type, ""), 11, CombatArt.Dim)
                .At(12, 30, w - 24, 18));
        }

        if (sel)
        {
            card.AddChild(CombatArt.Text("경유 ▸", 10, CombatArt.Gold, HorizontalAlignment.Right)
                .At(w - 80, 42, 68, 16));

            // 카드 전체를 덮는 투명 버튼 — 카드를 버튼으로 만들면 자식 배치가 스타일박스에 끌려간다
            string id = node.Id;
            var hit = new Button { FocusMode = FocusModeEnum.None, Flat = true, MouseFilter = MouseFilterEnum.Stop };
            hit.AddThemeStyleboxOverride("normal", CombatArt.Box(new Color(0, 0, 0, 0), null, 6));
            hit.AddThemeStyleboxOverride("hover", CombatArt.Box(new Color(1, 0.85f, 0.5f, 0.10f), CombatArt.Gold, 6));
            hit.AddThemeStyleboxOverride("pressed", CombatArt.Box(new Color(0, 0, 0, 0.25f), CombatArt.Gold, 6));
            hit.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            hit.Pressed += () => RunStore.EnterNode(id);
            card.AddChild(hit);
        }
        // 지나온 길은 흐리지 않다 — 흐린 것은 「못 간 길」과 「아직 안 온 길」뿐이다
        else if (state == "miss") card.Modulate = new Color(1, 1, 1, 0.34f);
        else if (state == "ahead") card.Modulate = new Color(1, 1, 1, 0.62f);

        return card;
    }

    // ── 간선 — MapNode.Next 만 그린다 ────────────────

    private static Control Edges(RunState run, Vector2 origin)
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore };
        c.At(0, 0, W, H);

        // (from, to, class) 를 미리 뽑아 두고 「지나온 길」이 위에 오도록 순서를 잡는다
        var lines = new List<(Vector2 A, Vector2 B, int Order)>();

        for (int f = 0; f <= 5; f++)
        {
            var upper = run.Map.Row(f + 1);
            if (upper.Count == 0) continue;
            int toFloor = f + 1;

            var froms = new List<(Vector2 P, bool Visited, List<string>? Next)>();
            if (f == 0) froms.Add((origin, true, null));
            else
            {
                var lower = run.Map.Row(f);
                for (int i = 0; i < lower.Count; i++)
                {
                    var r = NodeRect(f, i, lower.Count);
                    froms.Add((new Vector2(r.Position.X + r.Size.X / 2f, r.Position.Y), lower[i].Visited,
                        lower[i].Next.Count > 0 ? lower[i].Next : null));
                }
            }

            for (int j = 0; j < upper.Count; j++)
            {
                var tr = NodeRect(toFloor, j, upper.Count);
                var to = new Vector2(tr.Position.X + tr.Size.X / 2f, tr.Position.Y + tr.Size.Y);
                foreach (var a in froms)
                {
                    // 실제 경로만 — Next 가 없는 옛 세이브만 전결합으로 폴백한다
                    if (a.Next is { } nx && !nx.Contains(upper[j].Id)) continue;
                    int order = a.Visited && upper[j].Visited ? 2
                        : a.Visited && toFloor == run.Floor ? 3
                        : toFloor > run.Floor ? 1
                        : 0;
                    lines.Add((a.P, to, order));
                }
            }
        }

        lines.Sort((x, y) => x.Order.CompareTo(y.Order));

        c.Draw += () =>
        {
            foreach (var (a, b, order) in lines)
            {
                float dy = Mathf.Abs(a.Y - b.Y) * 0.55f;
                var pts = Bezier(a, new Vector2(a.X, a.Y - dy), new Vector2(b.X, b.Y + dy), b);
                switch (order)
                {
                    case 3: c.DrawPolyline(pts, CombatArt.Gold, 2.4f, true); break;
                    case 2: c.DrawPolyline(pts, EdgeDone, 2f, true); break;
                    case 1: DashPoly(c, pts, EdgeAhead, 1.3f, 4, 5); break;
                    default: DashPoly(c, pts, EdgeMiss, 1f, 2, 6); break;
                }
            }
        };
        return c;
    }

    private static Vector2[] Bezier(Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p1, int steps = 28)
    {
        var pts = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps, u = 1 - t;
            pts[i] = u * u * u * p0 + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * p1;
        }
        return pts;
    }

    private static void Dash(Control c, Vector2 a, Vector2 b, Color col, float w, float on, float off)
    {
        float len = a.DistanceTo(b);
        if (len <= 0.01f) return;
        var dir = (b - a) / len;
        for (float t = 0; t < len; t += on + off)
            c.DrawLine(a + dir * t, a + dir * Mathf.Min(len, t + on), col, w);
    }

    private static void DashPoly(Control c, Vector2[] pts, Color col, float w, float on, float off)
    {
        bool ink = true;
        float rem = on;
        for (int i = 1; i < pts.Length; i++)
        {
            Vector2 a = pts[i - 1], b = pts[i];
            float seg = a.DistanceTo(b);
            if (seg <= 0.001f) continue;
            float t = 0;
            while (t < seg)
            {
                float take = Mathf.Min(rem, seg - t);
                if (ink) c.DrawLine(a.Lerp(b, t / seg), a.Lerp(b, (t + take) / seg), col, w);
                t += take;
                rem -= take;
                if (rem > 0.001f) continue;
                ink = !ink;
                rem = ink ? on : off;
            }
        }
    }

    // ── 런 없음 ──────────────────────────────────────

    private Control NoRun()
    {
        var c = Hub.Panel((W - 480) / 2f, 240, 480, 232);
        c.AddChild(CombatArt.Text("📦", 38, CombatArt.Dim, HorizontalAlignment.Center).At(0, 22, 480, 46));
        c.AddChild(CombatArt.Text("조회할 운송장이 없습니다", 18, CombatArt.Ink, HorizontalAlignment.Center)
            .At(0, 78, 480, 24));
        c.AddChild(CombatArt.Text(
            "배송 조회는 원정 중에만 열람할 수 있습니다.\n새 원정을 시작하면 1막 운송장이 발급됩니다.",
            12, CombatArt.Dim, HorizontalAlignment.Center, wrap: true).At(30, 112, 420, 46));

        var start = Hub.NavBtn("새 원정 시작", true, () =>
        {
            if (!RunStore.Registered && SceneRouter.Exists(SceneRouter.Signature))
            { SceneRouter.Go(SceneRouter.Signature); return; }
            RunStore.NewRun();
            SceneRouter.GoMap();
        });
        start.At(90, 176, 140, 32);
        c.AddChild(start);

        var home = Hub.NavBtn("메인으로", false, SceneRouter.GoTitle);
        home.At(250, 176, 140, 32);
        c.AddChild(home);
        return c;
    }

    // ── 전용 씬이 없는 노드 (안전판) ─────────────────

    private Control NodePanel(RunState run, MapNode node)
    {
        bool combat = node.Type.IsCombat();
        var c = Hub.Panel(ColX + 200, 300, ColW - 400, 240);
        float w = ColW - 400;

        c.AddChild(CombatArt.Text($"{node.Type.Icon()} {node.Type.Label()}", 26, CombatArt.Gold,
            HorizontalAlignment.Center).At(0, 24, w, 32));
        if (node.Enemy is { } e)
            c.AddChild(CombatArt.Text($"{GameData.EnemyName(e)} · 의지 {GameData.EnemyWill(e)}", 15, CombatArt.Ink,
                HorizontalAlignment.Center).At(0, 62, w, 20));

        c.AddChild(CombatArt.Text(
            combat ? "전투 씬이 아직 붙지 않았다. 흐름을 끊지 않으려고 자동 승리로 통과한다."
                   : "이 경유지의 화면이 아직 없다. 지금은 통과만 한다.",
            12, new Color(0.8f, 0.65f, 0.4f), HorizontalAlignment.Center, wrap: true).At(30, 96, w - 60, 36));

        int reward = combat ? CombatReward(node) : 0;
        var pass = Hub.NavBtn(combat ? $"자동 승리로 통과 (🪙 +{reward})" : "(미구현) 통과", true,
            combat ? () => PassCombat(node) : () => Pass(0));
        pass.At(w / 2f - 130, 150, 260, 36);
        c.AddChild(pass);

        var back = Hub.NavBtn("타이틀로 — 여기서 중단해도 이 경유지로 복원된다", false, SceneRouter.GoTitle);
        back.At(w / 2f - 180, 194, 360, 28);
        c.AddChild(back);
        return c;
    }

    /// <summary>전투 보상 — combat.html 과 같은 값 (보스 50 / 정예 24 / 일반 15)</summary>
    private static int CombatReward(MapNode node)
    {
        var tier = node.Enemy is { } id && GameData.Enemies.TryGetValue(id, out var def) ? def.Tier : EnemyTier.Normal;
        return tier switch { EnemyTier.Boss => 50, EnemyTier.Elite => 24, _ => 15 };
    }

    private void PassCombat(MapNode node)
    {
        var run = RunStore.Current;
        if (run is null) { SceneRouter.GoTitle(); return; }
        run.BattlesWon += 1;                 // 전투 승리 0회 사망의 「계류」 판정이 정상 동작하도록 센다
        RunStore.EndCombat();
        Pass(CombatReward(node));
    }

    private void Pass(int gold) => SceneRouter.Go(RunStore.CompleteNode(gold: gold));

    // ── ③ 발급 연출 (ADR-024 ②) ─────────────────────
    //
    // 택배좌는 얼굴을 보이지 않는다 — 카운터 너머 장갑 낀 손까지만이다(3막 반전 보호).

    private void ShowIssue(RunState run, IReadOnlyList<string> args)
    {
        bool instant = CombatEntry.ArgValue(args, "issue") == "done";
        string no = OrderNo(run);

        var ov = new Control { MouseFilter = MouseFilterEnum.Stop };
        ov.At(0, 0, W, H);
        _issue = ov;

        var bg = new ColorRect { Color = new Color("070605") };
        bg.At(0, 0, W, H);
        ov.AddChild(bg);

        if (CombatArt.Load("res://assets/issue-hand.png") is { } hand)
        {
            var plate = new TextureRect
            {
                Texture = hand,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                Modulate = new Color(1, 1, 1, 0.62f),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            plate.At(0, 0, W, H);
            ov.AddChild(plate);
        }
        var vig = new ColorRect { Color = new Color(0.027f, 0.024f, 0.02f, 0.34f), MouseFilter = MouseFilterEnum.Ignore };
        vig.At(0, 0, W, H);
        ov.AddChild(vig);

        // ── 건네받는 종이 ──
        const float SlipW = 400, SlipH = 148;
        var slipHome = new Vector2((W - SlipW) / 2f, 226);
        var slip = Slip(no, SlipW, SlipH, out var seal);
        slip.At(slipHome.X, slipHome.Y, SlipW, SlipH);
        slip.PivotOffset = new Vector2(SlipW / 2f, SlipH / 2f);
        ov.AddChild(slip);

        // ── 대사 ──
        var lines = new List<Control>();
        var narr = CombatArt.Text("창구 너머로 장갑 낀 손이 종이 한 장을 밀어 놓는다.", 14, new Color("c8bda6"),
            HorizontalAlignment.Center).At(0, 428, W, 22);
        ov.AddChild(narr);
        lines.Add(narr);

        // 화자와 대사를 한 덩어리로 가운데 맞춘다 — 글자 폭을 재지 않으면 매번 왼쪽으로 쏠린다
        var font = CombatArt.Font();
        float nameW = font.GetStringSize("택배좌", HorizontalAlignment.Left, -1, 14).X;
        string[] says = { "배송 오류입니다. 종종 있는 일이고요.", "조회만 하십시오. 서명은 하지 마시고." };
        for (int i = 0; i < says.Length; i++)
        {
            float sayW = font.GetStringSize(says[i], HorizontalAlignment.Left, -1, 14).X;
            float x0 = (W - (nameW + 16 + sayW)) / 2f;
            var holder = new Control { MouseFilter = MouseFilterEnum.Ignore };
            holder.At(0, 470f + i * 34f, W, 24);
            holder.AddChild(CombatArt.Text("택배좌", 14, new Color("9fb2c8")).At(x0, 0, nameW + 6, 22));
            holder.AddChild(CombatArt.Text(says[i], 14, CombatArt.Ink).At(x0 + nameW + 16, 0, sayW + 24, 22));
            ov.AddChild(holder);
            lines.Add(holder);
        }

        ov.AddChild(CombatArt.Text("클릭 · Esc — 건너뛰기", 11, new Color("6f6656"), HorizontalAlignment.Center)
            .At(0, 712, W, 18));

        ov.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) CloseIssue(); };
        AddChild(ov);

        if (instant) { slip.Rotation = Mathf.DegToRad(-1.6f); return; }

        // ── 연출 ──
        ov.Modulate = new Color(1, 1, 1, 0);
        slip.Position = slipHome + new Vector2(-W * 0.58f, 64);
        slip.Scale = new Vector2(0.62f, 0.62f);
        slip.Rotation = Mathf.DegToRad(-15f);
        seal.Scale = new Vector2(2.7f, 2.7f);
        seal.Modulate = new Color(1, 1, 1, 0);
        foreach (var l in lines) l.Modulate = new Color(1, 1, 1, 0);

        var t = CreateTween().SetParallel(true);
        t.TweenProperty(ov, "modulate:a", 1f, 0.3f);
        t.TweenProperty(slip, "position", slipHome, 0.85f).SetDelay(0.35)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.TweenProperty(slip, "scale", Vector2.One, 0.85f).SetDelay(0.35)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.TweenProperty(slip, "rotation", Mathf.DegToRad(-1.6f), 0.85f).SetDelay(0.35)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.TweenProperty(seal, "scale", Vector2.One, 0.26f).SetDelay(1.9)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenProperty(seal, "modulate:a", 0.9f, 0.16f).SetDelay(1.9);
        for (int i = 0; i < lines.Count; i++)
            t.TweenProperty(lines[i], "modulate:a", 1f, 0.45f).SetDelay(1.3 + i * 0.9);
        t.Chain().TweenInterval(1.0);
        t.TweenCallback(Callable.From(CloseIssue));
    }

    /// <summary>운송장 본문과 같은 종이를 쓴다 — 같은 물건이라는 것이 눈으로 보여야 한다</summary>
    private static Control Slip(string no, float w, float h, out Control seal)
    {
        var slip = new Control { MouseFilter = MouseFilterEnum.Ignore };
        var tex = CombatArt.Load("res://assets/paper-waybill.png");
        slip.Draw += () =>
        {
            var full = new Rect2(0, 0, w, h);
            slip.DrawRect(full, Paper);
            if (tex is not null) slip.DrawTextureRect(tex, full, tile: false, new Color(1, 1, 1, 0.92f));
            for (float x = 0; x < w - 130; x += 14)
            {
                slip.DrawRect(new Rect2(20 + x, 62, 2, 22), WbInk with { A = 0.66f });
                slip.DrawRect(new Rect2(20 + x + 4, 62, 1, 22), WbInk with { A = 0.66f });
                slip.DrawRect(new Rect2(20 + x + 9, 62, 3, 22), WbInk with { A = 0.66f });
            }
            Dash(slip, new Vector2(20, 96), new Vector2(w - 20, 96), WbLine, 1, 5, 4);
        };
        slip.AddChild(CombatArt.Text("운송장 번호", 10, WbDim).At(20, 14, 200, 14));
        slip.AddChild(CombatArt.Text(no, 22, WbInk).At(20, 28, 300, 30));
        slip.AddChild(CombatArt.Text("받는 사람 · 6F 지배인실 — 관계자 외 출입 금지", 11, WbPast).At(20, 106, w - 110, 18));

        seal = Seal(12);
        seal.At(w - 78, h - 62, 60, 44);
        seal.PivotOffset = new Vector2(30, 22);
        slip.AddChild(seal);
        return slip;
    }

    private void CloseIssue()
    {
        if (_issue is not { } ov) return;
        _issue = null;
        var t = CreateTween();
        t.TweenProperty(ov, "modulate:a", 0f, 0.4f);
        t.TweenCallback(Callable.From(ov.QueueFree));
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (_issue is null || @event is not InputEventKey { Pressed: true } k) return;
        if (k.Keycode is Key.Escape or Key.Enter or Key.Space or Key.KpEnter) CloseIssue();
    }
}
