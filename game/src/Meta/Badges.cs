// 등재기록 화면 — 골격만. 내용은 후속 작업이 채운다.
using Godot;
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Meta;

public partial class Badges : Control
{
    public override void _Ready()
    {
        var lab = UiTheme.Text("등재기록 — 준비 중", 24);
        lab.SetAnchorsPreset(LayoutPreset.Center);
        AddChild(lab);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) SceneRouter.Go(SceneRouter.Title);
    }
}
