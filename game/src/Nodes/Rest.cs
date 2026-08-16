// 휴식 노드 — 모닥불 앞의 택1. (GDD R09, ADR-029 3차, 이관 원본 ui/game/rest.html)
//
//   모닥불에서 쉰다  → 의지 30% 회복 (올림)
//   초고를 태운다    → 덱에서 카드 1장 제거 (무료)
//
// 둘 다는 안 된다. 고른 뒤 결과를 보고 「지도로」에서 CompleteNode 로 한 번에 반영한다 —
// 즉시 반영하지 않는 덕에 결과 화면에서 타이틀로 나가도 런이 어중간하게 변하지 않는다.

using Godot;
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Nodes;

public partial class Rest : NodeScene
{
    protected override NodeType Kind => NodeType.Rest;
    protected override string PageKey => "rest";

    private VBoxContainer _choices = null!;
    private VBoxContainer _result = null!;
    private int _heal;

    protected override void Build()
    {
        _heal = (int)Math.Ceiling(Run.MaxWill * 0.3);   // GDD R09 — 의지 30%

        var col = Column();
        Body.AddChild(col);

        var ico = UiTheme.Text("🔥", 46);
        ico.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(ico);
        var title = UiTheme.Text("먼저 간 원정대의 모닥불", 28, Gold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(title);

        col.AddChild(Parch(
            "무너진 회랑 한켠, 먼저 다녀간 원정대가 남긴 모닥불 자리가 있다. 재를 헤치자 불씨가 아직 살아 있다.\n\n" +
            "옆의 돌에는 누군가 새겨 둔 글귀.\n\n" +
            "  「쉬어라. 별점은 내일도 짤 수 있다.」\n\n" +
            "불을 쬐며 쉴 수도 있고, 그 불에 마음에 안 드는 초고를 태워 버릴 수도 있다. 둘 다는 안 된다 — 밤은 짧다."));

        _choices = UiTheme.VBox(10);
        col.AddChild(_choices);

        _choices.AddChild(ChoiceBtn(
            "🛌 모닥불에서 쉰다",
            "아무것도 평가하지 않는 시간. 불멍은 어느 세계에서나 통한다.",
            $"🧠 의지 +{_heal} (최대 {Run.MaxWill})",
            true, DoRest));

        bool canBurn = Run.Deck.Any(id => !GameData.All.Irremovable.Contains(id));
        _choices.AddChild(ChoiceBtn(
            "📜 초고를 태운다",
            "버린 문장 수만큼 필치는 가벼워진다 — 선배들의 유일한 공통 조언.",
            canBurn ? "📋 덱에서 카드 1장 제거" : "태울 수 있는 초고가 없다 (전부 생계형 리뷰)",
            canBurn, OpenBurn));

        _result = UiTheme.VBox(10);
        _result.Visible = false;
        col.AddChild(_result);
    }

    private Control ChoiceBtn(string title, string desc, string fx, bool enabled, Action onPressed)
    {
        var b = UiTheme.Btn($"{title}\n{desc}\n{fx}", null, enabled, 17);
        b.Alignment = HorizontalAlignment.Left;
        b.CustomMinimumSize = new Vector2(0, 92);
        if (enabled) b.Pressed += onPressed;
        return b;
    }

    // ── 택1 ─────────────────────────────────────────

    private void DoRest() => ShowResult(
        "망토를 말아 베고 불가에 누웠다. 타닥, 타닥. 아무것도 평가하지 않아도 되는 시간이 흘러갔다.\n\n" +
        "눈을 떴을 때, 재 속의 불씨가 한 번 크게 숨을 쉬었다.",
        $"🧠 의지 +{_heal}",
        () => Finish(will: _heal));

    private void OpenBurn() => OpenDeckPicker(
        "📜 어떤 초고를 태울까",
        "태운 카드는 이 런에서 사라진다. 연기는 사과를 받아 주지 않는다.",
        "불 속으로",
        Run.Deck,
        idx =>
        {
            string name = GameData.CardName(Run.Deck[idx]);
            ShowResult(
                $"「{name}」 초고를 불 속에 던졌다. 종이가 오그라들며 파란 불꽃이 잠깐 일었다.\n\n" +
                "그 문장을 쓰던 밤의 기억도 같이 타 올라갔다. 조금 가벼워졌다.",
                $"📋 「{name}」 제거 — 덱 {Run.Deck.Count - 1}장",
                () => Finish(deckRemoveIdx: idx));
        });

    private void ShowResult(string text, string fx, Action onGo)
    {
        _choices.Visible = false;
        Clear(_result);

        _result.AddChild(Parch(text));
        var l = UiTheme.Text(fx, 18, Gold);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        _result.AddChild(l);

        var go = UiTheme.Btn("지도로", onGo, size: 22);
        go.CustomMinimumSize = new Vector2(0, 48);
        _result.AddChild(go);
        _result.Visible = true;
    }
}
