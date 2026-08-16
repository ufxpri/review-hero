using Godot;

namespace ReviewHero.Game;

/// <summary>
/// 연결 확인용 최소 씬 — engine/ 과 data/ 가 Godot 런타임에서 실제로 살아나는지 본다.
/// 2차 작업에서 진짜 타이틀 화면으로 대체된다.
/// </summary>
public partial class Title : Control
{
    public override void _Ready()
    {
        var d = ReviewHero.Data.Loader.LoadAll();
        var enemy = d.Enemies["E01"];
        var battle = new ReviewHero.Engine.Battle(new ReviewHero.Engine.BattleConfig
        {
            Cards = d.Cards,
            Enemy = enemy,
            Deck = new System.Collections.Generic.List<string>(d.StartingDeck),
            Rng = ReviewHero.Engine.RngFactory.Mulberry32(42),
        });
        var msg = $"엔진 연결 확인\n카드 {d.Cards.AllIds.Count}장 · 적 {d.Enemies.Count}종\n"
                + $"{enemy.Name} 의지 {battle.State.Enemy.Will} · 손패 {battle.State.Player.Hand.Count}장";
        GetNode<Label>("Label").Text = msg;
        GD.Print(msg.Replace("\n", " | "));
    }
}
