// 상점 노드 — 던전 상가 뒷골목의 잡화점. (ADR-029 3차, 이관 원본 ui/game/shop.html)
//
// 진열: 무작위 카드 3장(일반 25G 둘 · 특수 45G 하나) + 의지 회복제 12G(+10) + 리뷰 파쇄 20G(1장 제거).
// 서비스는 방문당 1회, 카드는 장당 1개.
//
// ── 왜 「장바구니」인가 ──────────────────────────────
// 웹판은 구입할 때마다 run.gold/run.deck 을 곧바로 고쳐 저장하고, 재입장 시 재고가 부활하지
// 않도록 run.nodeCtx 에 sold 표시를 남겼다. Godot 판은 RunState 에 그런 자유 칸이 없다.
// 그래서 **구입은 가게 안에서만 쌓아 두고 나설 때 한 번에 반영한다** — 중간에 타이틀로 나가면
// 산 것도 쓴 돈도 없던 일이 되므로 재고 부활을 이용한 되사기가 성립하지 않는다.
// 반영은 RunStore.CompleteNode 한 번(골드·의지·파쇄) + 산 카드 덱 추가로 끝난다.

using Godot;
using ReviewHero.Game.Run;

namespace ReviewHero.Game.Nodes;

public partial class Shop : NodeScene
{
    private const int PriceNormal = 25;
    private const int PriceSpecial = 45;
    private const int PricePotion = 12;
    private const int PriceShred = 20;
    private const int PotionHeal = 10;

    protected override NodeType Kind => NodeType.Shop;
    protected override string PageKey => "shop";

    private sealed class Item
    {
        public required string Id;
        public required int Price;
        public bool Sold;
    }

    private readonly List<Item> _stock = new();
    private readonly List<string> _bought = new();
    private int _spent;
    private int _healed;
    private bool _potionSold;
    private bool _shredUsed;
    private int? _shredIdx;

    private HBoxContainer _goods = null!;
    private HBoxContainer _svc = null!;
    private Label _msg = null!;

    // 칩은 장바구니를 반영한 값을 보여 준다 (웹판이 구매 즉시 topbar 를 다시 그리던 자리)
    protected override int ShownGold => Run.Gold - _spent;
    protected override int ShownWill => Math.Min(Run.MaxWill, Run.Will + _healed);
    protected override int ShownDeck => Deck.Count - (_shredIdx is null ? 0 : 1);

    /// <summary>가게 안에서 보이는 덱 = 원래 덱 + 장바구니 (파쇄 인덱스가 가리키는 목록)</summary>
    private List<string> Deck => Run.Deck.Concat(_bought).ToList();

    protected override void Build()
    {
        var r = NodeRng();
        var normals = ReviewPool;
        var specials = SpecialPool;
        string a = PickFrom(normals, r);
        string b = PickFrom(normals, r, a);
        string c = PickFrom(specials, r);
        _stock.Add(new Item { Id = a, Price = PriceNormal });
        _stock.Add(new Item { Id = b, Price = PriceNormal });
        _stock.Add(new Item { Id = c, Price = PriceSpecial });

        // 상점은 세로로 긴 화면이라(진열 3장 + 서비스 2칸) 머리글을 한 줄로 눌러 담는다
        var col = Column(1000, 10);
        Body.AddChild(col);

        var title = Line("🏚 만물잡화 「재고처리반」", 26, Gold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(title);

        var keeper = UiTheme.HBox(14);
        keeper.AddChild(Line("🧌", 34));
        var say = UiTheme.Text(
            "「어서 오십쇼. 보다시피 정식 협찬 매장은 아니고… 주인 잃은 물건들 새 주인 찾아주는 일을 합니다.\n" +
            " 정찰제고, 흥정 안 받고, 환불은 제일 안 받습니다.」", 15, Dim);
        say.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        keeper.AddChild(say);
        col.AddChild(Panel(keeper));

        col.AddChild(SectionHead("📜 떠돌이 리뷰 초안", "주인 잃은 문장들 — 일반 25G · 특수 45G · 반품 불가"));
        _goods = UiTheme.HBox(12);
        _goods.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        col.AddChild(_goods);

        col.AddChild(SectionHead("🛎 서비스", "방문당 1회"));
        _svc = UiTheme.HBox(12);
        col.AddChild(_svc);

        _msg = UiTheme.Text(" ", 15, Gold);
        _msg.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(_msg);

        var exit = UiTheme.Btn("가게를 나선다", Leave, size: 22);
        exit.CustomMinimumSize = new Vector2(0, 48);
        col.AddChild(exit);

        Paint();
    }

    private Control SectionHead(string head, string note)
    {
        var h = UiTheme.HBox(10);
        h.AddChild(Line(head, 19, Gold));
        h.AddChild(Line(note, 13, Dim));
        return h;
    }

    // ── 진열 ────────────────────────────────────────

    private void Paint()
    {
        Clear(_goods);
        for (int i = 0; i < _stock.Count; i++)
        {
            var it = _stock[i];
            bool afford = ShownGold >= it.Price;
            var v = UiTheme.VBox(8);
            v.CustomMinimumSize = new Vector2(300, 0);
            var mini = CardMini(it.Id);
            if (it.Sold) mini.Modulate = new Color(1f, 1f, 1f, 0.45f);
            v.AddChild(mini);
            var b = UiTheme.Btn(it.Sold ? "판매 완료" : $"구입 — 🪙 {it.Price}", null, !it.Sold && afford, 17);
            b.CustomMinimumSize = new Vector2(0, 40);
            if (!it.Sold && afford) b.Pressed += () => Buy(it);
            v.AddChild(b);
            _goods.AddChild(v);
        }

        Clear(_svc);
        _svc.AddChild(PotionBox());
        _svc.AddChild(ShredBox());
        PaintHud();
    }

    private Control PotionBox()
    {
        var v = UiTheme.VBox(8);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        v.AddChild(UiTheme.Text($"🧪 의지 회복제 — 🪙 {PricePotion}", 19, Gold));
        v.AddChild(UiTheme.Text(
            $"유통기한은 지났지만 효능은 지나지 않았습니다. 들이켜면 의지 +{PotionHeal}. 맛은 별 한 개.", 14, Dim));

        bool full = ShownWill >= Run.MaxWill;
        bool can = !_potionSold && ShownGold >= PricePotion && !full;
        var row = UiTheme.HBox(10);
        var b = UiTheme.Btn(_potionSold ? "판매 완료" : $"마신다 — 🪙 {PricePotion}", null, can, 16);
        b.CustomMinimumSize = new Vector2(150, 38);
        if (can) b.Pressed += BuyPotion;
        row.AddChild(b);
        row.AddChild(Line(
            _potionSold ? "" : full ? "의지가 이미 가득하다" : $"현재 의지 {ShownWill}/{Run.MaxWill}", 13, Dim));
        v.AddChild(row);
        return Panel(v, 480);
    }

    private Control ShredBox()
    {
        var v = UiTheme.VBox(8);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        v.AddChild(UiTheme.Text($"🗜 리뷰 파쇄 서비스 — 🪙 {PriceShred}", 19, Gold));
        v.AddChild(UiTheme.Text(
            "덱에서 카드 1장을 곱게 갈아 드립니다. 흑역사도 초심도, 갈리면 똑같은 가루입니다. 복구 불가.", 14, Dim));

        bool can = !_shredUsed && ShownGold >= PriceShred && ShownDeck > 0;
        var row = UiTheme.HBox(10);
        var b = UiTheme.Btn(_shredUsed ? "이용 완료" : $"맡긴다 — 🪙 {PriceShred}", null, can, 16);
        b.CustomMinimumSize = new Vector2(150, 38);
        if (can) b.Pressed += OpenShred;
        row.AddChild(b);
        row.AddChild(Line(_shredUsed ? "" : $"덱 {ShownDeck}장", 13, Dim));
        v.AddChild(row);
        return Panel(v, 480);
    }

    // ── 거래 ────────────────────────────────────────

    private void Buy(Item it)
    {
        if (it.Sold || ShownGold < it.Price) return;
        it.Sold = true;
        _spent += it.Price;
        _bought.Add(it.Id);
        Paint();
        Say($"「{GameData.CardName(it.Id)}」 구입 — 덱에 추가되었다. 주인 잃은 문장에 새 주인이 생겼다.");
    }

    private void BuyPotion()
    {
        if (_potionSold || ShownGold < PricePotion) return;
        _potionSold = true;
        _spent += PricePotion;
        _healed += PotionHeal;
        Paint();
        Say("회복제를 들이켰다. 지독하게 쓰다. 의지 +10 — 역시 회복은 쓴맛이다.");
    }

    private void OpenShred()
    {
        OpenDeckPicker(
            "🗜 리뷰 파쇄 서비스 — 갈아 버릴 카드를 고르십쇼",
            "파쇄된 카드는 덱에서 사라지며 가루는 반환되지 않습니다. (수수료 20G)",
            $"파쇄한다 (🪙 {PriceShred})",
            Deck,
            idx =>
            {
                if (_shredUsed || ShownGold < PriceShred) return;
                string id = Deck[idx];
                _shredUsed = true;
                _shredIdx = idx;
                _spent += PriceShred;
                Paint();
                Say($"「{GameData.CardName(id)}」 파쇄 완료. 위이잉— 소리와 함께 문장이 가루가 되었다.");
            });
    }

    private void Say(string s) => _msg.Text = s;

    // ── 나가기 ──────────────────────────────────────

    private void Leave()
    {
        if (Preview) { Finish(); return; }

        // 산 카드는 덱 뒤에 붙는다 — 파쇄 인덱스가 가리키던 목록(원래 덱 + 장바구니)과 같은 순서다.
        // 여기만 덱을 직접 만지는데, CompleteNode 가 카드 여러 장 추가를 받지 않기 때문이다(보고서 참조).
        if (_bought.Count > 0 && RunStore.Current is { } run)
        {
            run.Deck.AddRange(_bought);
            RunStore.RecordSeen(_bought);   // 구입도 카드를 손에 넣는 경로 — 도감에 등재한다
        }
        // 파쇄 통계(cardsRemoved)는 CompleteNode 의 deckRemoveIdx 경로가 센다 — 따로 세면 이중 계상이다
        Finish(gold: -_spent, will: _healed, deckRemoveIdx: _shredIdx);
    }
}
