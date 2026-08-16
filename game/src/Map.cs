// 지도 — 1막 6층을 세로로 늘어놓고, 갈 수 있는 노드만 연다. (ADR-029 2차)
//
// 두 가지 상태를 한 씬이 맡는다:
//   ① 선택 화면 — Pos 가 비어 있을 때. 현재 층의 Reachable() 만 활성.
//   ② 노드 화면 — Pos 가 찍혀 있는데 전용 씬이 없을 때(이벤트·상점·휴식은 2차 범위 밖,
//      전투 씬은 다른 작업자 소관이라 아직 없을 수 있다). 「통과」 버튼 하나로 CompleteNode 를
//      부르고 지도로 돌아온다 — 흐름이 끊기지 않는 것이 우선이다.
// ②를 별도 씬으로 빼지 않은 이유: 중단 후 이어하기가 곧바로 여기로 복원되므로
// (Resume 이 Pos 가 남은 런을 map 으로 보낸다) 한 씬이 두 상태를 다 그리는 편이 경로가 짧다.

using Godot;
using ReviewHero.Engine;
using ReviewHero.Game.Run;

namespace ReviewHero.Game;

public partial class Map : Control
{
    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();
        UiTheme.Apply(this);

        var run = RunStore.Current;
        if (run is null) { SceneRouter.GoTitle(); return; }
        if (run.Ended is not null) { SceneRouter.GoResult(); return; }   // 정산 전 — 지도는 잠긴다

        Build(run);
    }

    private void Build(RunState run)
    {
        foreach (var c in GetChildren()) c.QueueFree();

        var pad = new MarginContainer();
        pad.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            pad.AddThemeConstantOverride(side, 16);
        AddChild(pad);

        var root = UiTheme.VBox(10);
        pad.AddChild(root);

        root.AddChild(Hud(run));
        root.AddChild(new HSeparator());

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(scroll);

        var body = UiTheme.VBox(10);
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(body);

        var pending = run.CurrentNode;
        if (pending is not null && !SceneRouter.HasSceneFor(pending)) BuildNodePanel(body, run, pending);
        else BuildFloors(body, run);
    }

    // ── 상단 상태 ────────────────────────────────────

    private Control Hud(RunState run)
    {
        var h = UiTheme.HBox(18);
        h.AddChild(UiTheme.Text($"1막 {run.Floor}층", 24));
        h.AddChild(UiTheme.Text($"🧠 의지 {run.Will}/{run.MaxWill}", 20));
        h.AddChild(UiTheme.Text($"🪙 골드 {run.Gold}", 20));
        h.AddChild(UiTheme.Text($"🃏 덱 {run.Deck.Count}장", 20));
        h.AddChild(UiTheme.Text($"⚔ {run.BattlesWon}승", 20));
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        h.AddChild(spacer);
        h.AddChild(UiTheme.Text($"시드 {run.Seed}", 14, new Color(0.55f, 0.55f, 0.6f)));
        h.AddChild(UiTheme.Btn("타이틀", SceneRouter.GoTitle, size: 14));
        return h;
    }

    // ── ① 선택 화면 ─────────────────────────────────

    private void BuildFloors(VBoxContainer body, RunState run)
    {
        var open = RunStore.Reachable().ToHashSet();
        var walked = run.Path.ToHashSet();

        for (int f = 1; f <= run.Map.Floors.Count; f++)
        {
            var row = UiTheme.HBox(10);
            bool isNow = f == run.Floor;
            var head = UiTheme.Text($"{f}층{(isNow ? " ▶" : "  ")}", 18,
                isNow ? new Color(1f, 0.85f, 0.4f) : new Color(0.5f, 0.5f, 0.55f));
            head.CustomMinimumSize = new Vector2(72, 0);
            row.AddChild(head);

            foreach (var node in run.Map.Row(f))
            {
                bool enabled = isNow && open.Contains(node.Id);
                string mark = walked.Contains(node.Id) ? "✔ " : "";
                var b = UiTheme.Btn(mark + NodeLabel(node), null, enabled, 16);
                b.CustomMinimumSize = new Vector2(300, 44);
                if (enabled)
                {
                    string id = node.Id;
                    b.Pressed += () => RunStore.EnterNode(id);
                }
                row.AddChild(b);
            }
            body.AddChild(row);
        }

        body.AddChild(new HSeparator());
        body.AddChild(UiTheme.Text(
            "갈 수 있는 곳은 직전에 지난 노드에서 이어진 길뿐이다. 1층은 전부 열려 있다.",
            14, new Color(0.55f, 0.55f, 0.6f)));
    }

    private static string NodeLabel(MapNode node)
    {
        string s = $"{node.Type.Icon()} {node.Type.Label()}";
        if (node.Enemy is { } e)
        {
            int will = GameData.EnemyWill(e);
            s += $" — {GameData.EnemyName(e)} (의지 {will})";
        }
        return s;
    }

    // ── ② 노드 화면 (전용 씬이 없는 노드) ───────────

    private void BuildNodePanel(VBoxContainer body, RunState run, MapNode node)
    {
        bool combat = node.Type.IsCombat();
        body.AddChild(UiTheme.Text($"{node.Type.Icon()} {node.Type.Label()}", 34));
        if (node.Enemy is { } e)
        {
            body.AddChild(UiTheme.Text($"{GameData.EnemyName(e)} · 의지 {GameData.EnemyWill(e)}", 20));
        }

        string why = combat
            ? "전투 씬이 아직 붙지 않았다(다른 작업자 소관). 흐름을 끊지 않으려고 자동 승리로 통과한다."
            : "이 노드의 화면은 2차 범위 밖이다. 지금은 통과만 한다.";
        body.AddChild(UiTheme.Text(why, 16, new Color(0.8f, 0.65f, 0.4f)));
        body.AddChild(new HSeparator());

        if (combat)
        {
            int reward = CombatReward(node);
            body.AddChild(UiTheme.Btn($"자동 승리로 통과 (🪙 +{reward})", () => PassCombat(node), size: 22));
        }
        else
        {
            body.AddChild(UiTheme.Btn("(미구현) 통과", () => Pass(0), size: 22));
        }
        body.AddChild(UiTheme.Btn("타이틀로 (여기서 중단해도 이 노드로 복원된다)", SceneRouter.GoTitle, size: 14));
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
        RunStore.EndCombat();                // Save 포함
        Pass(CombatReward(node));
    }

    private void Pass(int gold)
    {
        string next = RunStore.CompleteNode(gold: gold);
        SceneRouter.Go(next);
    }
}
