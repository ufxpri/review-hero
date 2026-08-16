// 결과 — 마지막 리뷰(유언 / 정복 후기)를 올리고 명단에 오른다. (ADR-029 2차)
//
// 웹판은 result.html?outcome=death 로 결과를 받았다. Godot 에는 쿼리 문자열이 없으므로
// **RunState.Ended 를 읽는다** — 어차피 그것이 정본이고, URL 로 결과를 바꿔 쓰던 여지도 없어진다.
// 자유 입력은 2차 범위 밖이라 별점 1~5 + 템플릿 택1 로 받는다 (템플릿 문구는 result.html 그대로).

using Godot;
using ReviewHero.Game.Run;

namespace ReviewHero.Game;

public partial class Result : Control
{
    private static readonly string[] DeathTemplates =
    {
        "별점 테러당했다. 내가.",
        "환불은 저승에서 받겠습니다.",
        "재구매 의사: 다음 생에.",
        "단점: 죽음. 장점: 아직 못 찾음.",
        "사장님, 답글 좀 다세요. 이번엔 사람이 죽었어요.",
        "택배는 문 앞에 두고 가세요. 받으러 못 나갑니다.",
    };

    private static readonly string[] ClearTemplates =
    {
        "사장님, 3년 치 답글 잘 받았습니다. 이제 제 답글 받으세요.",
        "환불 완료. 별점은 돌려받았는데 마음고생은 환불이 안 된답니다.",
        "무응답 고객센터, 오늘부로 폐업시켰습니다. 재오픈 시 재방문 의사 있음.",
    };

    private string _outcome = "death";
    private int _stars = 3;
    private int _tplIndex;
    private readonly List<Button> _starBtns = new();
    private readonly List<Button> _tplBtns = new();
    private Label? _preview;

    public override void _Ready()
    {
        SceneRouter.Tree = GetTree();
        UiTheme.Apply(this);

        var run = RunStore.Current;
        if (run is null) { SceneRouter.GoTitle(); return; }   // 이미 정산된 런 — 올릴 리뷰가 없다
        _outcome = run.Ended == "clear" ? "clear" : "death";
        Build(run);
        Refresh();
    }

    private string[] Templates => _outcome == "clear" ? ClearTemplates : DeathTemplates;

    private void Build(RunState run)
    {
        foreach (var c in GetChildren()) c.QueueFree();

        var pad = new MarginContainer();
        pad.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            pad.AddThemeConstantOverride(side, 24);
        AddChild(pad);

        var scroll = new ScrollContainer();
        pad.AddChild(scroll);

        var box = UiTheme.VBox(12);
        box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(box);

        bool clear = _outcome == "clear";
        box.AddChild(UiTheme.Text(clear ? "🏁 정복" : "💀 원정 종료", 42,
            clear ? new Color(1f, 0.85f, 0.4f) : new Color(0.85f, 0.4f, 0.4f)));
        box.AddChild(UiTheme.Text(
            clear
                ? "무응답 고객센터를 폐업시켰다. 정복 후기를 올릴 차례다."
                : "여기까지다. 마지막 리뷰를 올리면 명단에 오른다.",
            18, new Color(0.75f, 0.75f, 0.8f)));

        box.AddChild(new HSeparator());
        box.AddChild(UiTheme.Text(
            $"도달 1막 {run.Floor}층 · 🪙 {run.Gold} · ⚔ {run.BattlesWon}승 · 🃏 {run.Deck.Count}장 · 시드 {run.Seed}", 20));

        if (_outcome == "death" && run.BattlesWon == 0)
        {
            box.AddChild(UiTheme.Text(
                "전투 승리 기록이 없는 원정의 유언은 게시되지 않고 계류됩니다.",
                15, new Color(0.8f, 0.65f, 0.4f)));
        }

        box.AddChild(new HSeparator());
        box.AddChild(UiTheme.Text("별점", 22));
        var starRow = UiTheme.HBox(6);
        for (int i = 1; i <= 5; i++)
        {
            int n = i;
            var b = UiTheme.Btn(new string('★', n), () => { _stars = n; Refresh(); }, size: 20);
            _starBtns.Add(b);
            starRow.AddChild(b);
        }
        box.AddChild(starRow);

        box.AddChild(UiTheme.Text(clear ? "정복 후기 (택1)" : "마지막 리뷰 — 유언 (택1)", 22));
        for (int i = 0; i < Templates.Length; i++)
        {
            int n = i;
            var b = UiTheme.Btn(Templates[i], () => { _tplIndex = n; Refresh(); }, size: 16);
            b.Alignment = HorizontalAlignment.Left;
            _tplBtns.Add(b);
            box.AddChild(b);
        }

        box.AddChild(new HSeparator());
        _preview = UiTheme.Text("", 18, new Color(0.7f, 0.85f, 0.7f));
        box.AddChild(_preview);
        box.AddChild(UiTheme.Btn("리뷰 제출 — 명단에 오른다", Submit, size: 24));
    }

    private void Refresh()
    {
        for (int i = 0; i < _starBtns.Count; i++)
            _starBtns[i].Modulate = i + 1 == _stars ? Colors.White : new Color(0.5f, 0.5f, 0.55f);
        for (int i = 0; i < _tplBtns.Count; i++)
            _tplBtns[i].Modulate = i == _tplIndex ? Colors.White : new Color(0.55f, 0.55f, 0.6f);
        if (_preview is not null)
            _preview.Text = $"{new string('★', _stars)}{new string('☆', 5 - _stars)}  「{Templates[_tplIndex]}」";
    }

    private void Submit()
    {
        var meta = RunStore.FinalizeRun(_outcome, _stars, Templates[_tplIndex]);
        GD.Print($"[Result] 정산 — 원정 {meta.Runs}회 · 정복 {meta.Wins}회 · 최고 {meta.BestFloor}층 " +
                 $"· 명단 {meta.Expedition.Count}줄 (최신: {meta.Expedition[0].Status})");
        SceneRouter.GoTitle();
    }
}
