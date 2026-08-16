// 타이틀 — 런의 입구. (ADR-029 2차)
//
// 화면은 코드로 조립한다. 2차의 목표가 「미술 없이 1막이 굴러가는 것」이라 .tscn 에는
// 루트 노드만 두고, 배치는 여기서 만든다 — 씬 파일과 코드가 어긋날 여지를 없앤다.

using Godot;
using ReviewHero.Game.Run;
using ReviewHero.Game.Combat;   // AutoPlay — 헤드리스 완주 검증

namespace ReviewHero.Game;

public partial class Title : Control
{
    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();

        // 헤드리스 완주 검증: `Godot --headless --path game -- --autoplay`
        if (AutoPlay.Requested())
        {
            AutoPlay.RunAndQuit(GetTree());
            return;
        }

        UiTheme.Apply(this);
        Build();
    }

    private void Build()
    {
        foreach (var c in GetChildren()) c.QueueFree();

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var box = UiTheme.VBox(14);
        box.CustomMinimumSize = new Vector2(560, 0);
        center.AddChild(box);

        var title = UiTheme.Text("이세계 리뷰용사", 52);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);

        var sub = UiTheme.Text("리뷰가 무기다", 22, new Color(0.75f, 0.75f, 0.8f));
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(sub);

        box.AddChild(new HSeparator());

        var resume = RunStore.Resume();
        if (resume is not null)
        {
            var run = RunStore.Current!;
            var line = UiTheme.Text(
                $"{resume.Label}\n1막 {run.Floor}층 · 🧠 {run.Will}/{run.MaxWill} · 🪙 {run.Gold} · 🃏 {run.Deck.Count}장 · 시드 {run.Seed}",
                16, new Color(0.7f, 0.8f, 0.7f));
            line.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(line);
            box.AddChild(UiTheme.Btn("이어하기", () => SceneRouter.Go(resume.ScenePath), size: 24));
        }

        box.AddChild(UiTheme.Btn(resume is null ? "새 원정" : "새 원정 (진행 중인 원정은 사라진다)", OnNewRun, size: 24));
        box.AddChild(UiTheme.Btn("종료", () => GetTree().Quit(), size: 20));

        box.AddChild(new HSeparator());

        var meta = RunStore.Meta;
        var stat = UiTheme.Text(
            $"원정 {meta.Runs}회 · 정복 {meta.Wins}회 · 최고 {meta.BestFloor}층 · 도감 {meta.Seen.Count}장 · RP {meta.Rp} · P {meta.P}",
            15, new Color(0.6f, 0.6f, 0.65f));
        stat.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(stat);

        if (!SceneRouter.Exists(SceneRouter.Combat))
        {
            var warn = UiTheme.Text("(전투 씬 미구현 — 전투 노드는 지도에서 자동 승리로 통과한다)", 14,
                new Color(0.8f, 0.65f, 0.4f));
            warn.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(warn);
        }
    }

    private void OnNewRun()
    {
        RunStore.NewRun();
        SceneRouter.GoMap();
    }
}
