// 이벤트 노드 — 폐허에서 마주친 한 장면. (ADR-029 3차, 이관 원본 ui/game/event.html)
//
// 3종 중 하나를 노드 문맥 난수로 고른다: 협찬 상자(골드) / 리뷰 초안(카드) / 노숙 대원(골드→의지).
// 선택은 각 2지선다이며 「거절」쪽은 아무 변화가 없다 — 원정대 수칙과 금화 사이에서 고르는 것이
// 이 노드의 내용이라, 이득이 없는 선택지가 있어야 고르는 일이 성립한다.
//
// 결과 반영은 <see cref="NodeScene.Finish"/> 하나만 지난다 = RunStore.CompleteNode.
// 카드 획득분의 도감 등재(RecordSeen)는 CompleteNode 의 deckAdd 경로가 이미 수행한다.

using Godot;
using ReviewHero.Game.Audio;
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Nodes;

public partial class Event : NodeScene
{
    protected override NodeType Kind => NodeType.Event;
    protected override string PageKey => "event";

    private sealed record Choice(
        string Label, string Fx, string ResultText, string ResultFx,
        int Gold = 0, int Will = 0, string? DeckAdd = null,
        bool Enabled = true, string BlockedMsg = "");

    private sealed record Scene(string Ico, string Title, string Story, Choice[] Choices);

    private VBoxContainer _choices = null!;
    private VBoxContainer _result = null!;

    protected override void Build()
    {
        var r = NodeRng();
        int pick = (int)Math.Floor(r() * 3);
        string cardId = PickFrom(ReviewPool, r);   // 3번 이벤트가 안 쓰더라도 항상 굴린다(선택과 무관하게 고정)
        var ev = Scenes(cardId)[pick];

        var col = Column();
        Body.AddChild(col);

        var ico = UiTheme.Text(ev.Ico, 46);
        ico.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(ico);

        var title = UiTheme.Text(ev.Title, 28, Gold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(title);

        col.AddChild(Parch(ev.Story));

        _choices = UiTheme.VBox(10);
        col.AddChild(_choices);
        foreach (var c in ev.Choices)
        {
            bool ok = c.Enabled;
            var b = UiTheme.Btn($"{c.Label}      ▸ {(ok ? c.Fx : c.BlockedMsg + " — " + c.Fx)}", null, ok, 18);
            b.Alignment = HorizontalAlignment.Left;
            b.CustomMinimumSize = new Vector2(0, 46);
            if (ok) b.Pressed += () => Choose(c);
            _choices.AddChild(b);
        }

        _result = UiTheme.VBox(10);
        _result.Visible = false;
        col.AddChild(_result);
    }

    private void Choose(Choice c)
    {
        Sfx.Play(SfxId.Click);
        if (c.Gold != 0) Sfx.Play(SfxId.Coin);
        else if (c.DeckAdd is not null) Sfx.Play(SfxId.CardPick);   // 양피지가 손에 들어온다
        _choices.Visible = false;
        Clear(_result);

        _result.AddChild(Parch(c.ResultText));
        var fx = UiTheme.Text(c.ResultFx, 18, Gold);
        fx.HorizontalAlignment = HorizontalAlignment.Center;
        _result.AddChild(fx);

        var go = UiTheme.Btn("지도로", () => Finish(gold: c.Gold, will: c.Will, deckAdd: c.DeckAdd), size: 22);
        go.CustomMinimumSize = new Vector2(0, 48);
        _result.AddChild(go);
        _result.Visible = true;
    }

    // ── 3종 ─────────────────────────────────────────

    private Scene[] Scenes(string cardId)
    {
        string cardName = GameData.CardName(cardId);
        bool canGive = Run.Gold >= 10;

        return new[]
        {
            new Scene("🎁", "수상한 협찬 상자",
                "무너진 상가 골목, 발끝에 리본 묶인 상자 하나가 놓여 있다. 이 폐허에서 유일하게 포장이 말끔한 물건이다.\n\n" +
                "뚜껑에는 또박또박한 손글씨가 적혀 있다.\n\n" +
                "  「먼저 써 보시고 솔직한 후기 부탁드립니다 ^^ (되도록 긍정적으로)」\n\n" +
                "원정대 수칙 제1조가 머릿속을 스친다. 「대가를 받고 쓴 리뷰는 리뷰가 아니다. 그것은 광고이고, " +
                "광고가 이 세계를 이렇게 만들었다.」\n\n" +
                "…그래도 상자 틈으로 비치는 금화는 진짜다.",
                new[]
                {
                    new Choice("상자를 받는다", "🪙 골드 +25",
                        "주위를 두 번 살피고 상자를 챙겼다. 안에는 금화 25닢, 그리고 「추후 협업 제안」이라 적힌 명함 한 장.\n\n" +
                        "명함은 읽지 않고 삼켰다. 증거는 없다. 양심에만 남았다.",
                        "🪙 골드 +25", Gold: 25),
                    new Choice("못 본 척 지나간다", "아무 일 없음",
                        "상자를 지나쳐 걸었다. 등 뒤에서 뚜껑이 스르르 닫히는 소리가 들렸다.\n\n" +
                        "오늘 일지에 쓸 한 줄이 생겼다. 「나는 아직 리뷰어다.」",
                        "변화 없음"),
                }),

            new Scene("📜", "길에서 주운 리뷰 초안",
                "부서진 진열대 밑에 구겨진 양피지 한 장이 끼여 있다. 펼쳐 보니 누군가 쓰다 만 리뷰 초안이다.\n\n" +
                "별점 칸은 비어 있고, 문장은 중간에서 끊겨 있다. 마지막 획에 힘이 없다 — 쓰다가 무슨 일이 있었던 모양이다.\n\n" +
                "만물대장에 오르지 못한 미완성 리뷰는, 마침표를 찍어 줄 사람을 만날 때까지 힘을 잃지 않는다고 한다.",
                new[]
                {
                    new Choice("이어서 완성한다", "📋 무작위 카드 1장",
                        "끊긴 문장을 이어 붙이고 마침표를 찍었다. 양피지가 가볍게 떨리더니 손바닥에 스며들었다.\n\n" +
                        $"어딘가의 누군가가 남긴 문장이 내 것이 되었다. — 「{cardName}」",
                        $"📋 「{cardName}」 덱에 추가", DeckAdd: cardId),
                    new Choice("도로 밀어 넣는다", "아무 일 없음",
                        "남의 초고에는 손을 대지 않는 법이다. 양피지를 진열대 밑으로 도로 밀어 넣었다.\n\n" +
                        "주인이 돌아온다면, 마침표는 그가 찍을 것이다.",
                        "변화 없음"),
                }),

            new Scene("🥫", "노숙 원정대원의 부탁",
                "상가 처마 밑에 원정대 망토를 뒤집어쓴 사람이 웅크리고 있다. 망토의 원정대 문장은 반쯤 뜯겨 나갔다.\n\n" +
                "  「환불받으러 들어왔다가… 여비까지 환불당했습니다.」\n\n" +
                "그가 내민 깡통 옆면에는 서툰 글씨가 적혀 있다. 「후원 1건당 진심 어린 감사 후기를 작성해 드립니다」",
                new[]
                {
                    new Choice("10골드를 깡통에 넣는다", "🪙 -10 · 🧠 의지 +8",
                        "금화 열 닢을 깡통에 넣었다. 그는 약속대로 그 자리에서 감사 후기를 읊기 시작했다 — " +
                        "내 걸음걸이, 망토 매무새, 눈빛의 결의까지 전부 별 다섯 개짜리 문장으로.\n\n" +
                        "낯간지럽지만, 이상하게 힘이 난다.",
                        "🪙 골드 -10 · 🧠 의지 +8", Gold: -10, Will: 8,
                        Enabled: canGive, BlockedMsg: "골드 부족"),
                    new Choice("무시한다", "아무 일 없음",
                        "못 본 척 지나쳤다. 등 뒤에서 「별점… 1점…」 하는 중얼거림이 들렸다.\n\n" +
                        "괜찮다. 원정대원끼리는 서로를 평가할 수 없다는 규정이 있다. 아마, 있을 것이다.",
                        "변화 없음"),
                }),
        };
    }
}
