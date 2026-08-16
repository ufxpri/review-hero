// 획순 재생 카드 미리보기 — 「카드에 얹힌 모습」 (이관 원본: ui/signature.html .preview).
//
// 등록 화면의 값은 여기 있다. 서명은 **올리는 행위 그 자체**라 독자가 보는 물건이 아니고,
// 플레이어가 그것을 확인할 수 있는 유일한 자리가 카드 하단이다(GDD §4.4 선행 단계).
// 그래서 그은 획이 **순서대로** 다시 그어지는 것을 그 자리에서 보여 준다.
//
// 실제 전투 카드와 같은 SignatureInk 를 쓴다 — 서명란 규격(140×38)과 정규화 계산이 하나뿐이라
// 여기서 본 모양이 전투에서 그대로 나온다.

using Godot;
using ReviewHero.Game.Combat;   // CombatArt — 색·조각 사전 (읽기만 한다)
using ReviewHero.Game.Fx;

namespace ReviewHero.Game.Signature;

public partial class SignaturePreviewCard : Control
{
    public const float W = 168f;
    public const float H = 214f;

    private static readonly Color TextCol = new("3a3229");

    private SignatureInk _ink = null!;
    private Tween? _play;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(W, H);
        Build();
    }

    private void Build()
    {
        AddChild(CombatArt.Text("★☆☆☆☆", 11, new Color("c9a24a")).At(10, 8, W - 20, 14));
        AddChild(CombatArt.Text("손목 나감", 13, CombatArt.Inkc).At(10, 24, W - 20, 17));

        var body = CombatArt.Text("위력은 확실한데 손목이 나갑니다.\n데드리프트 삼 년 하고 오세요.", 11, TextCol, wrap: true);
        AddChild(body.At(10, 46, W - 20, 62));

        // 서명란 — 웹판 .psig(left/right 11, bottom 40, height 38) 그대로
        _ink = new SignatureInk();
        AddChild(_ink.At(11, H - 40 - 38, W - 22, 38));

        AddChild(CombatArt.Text("#무게", 10, new Color("e8d9ae"), HorizontalAlignment.Center).At(10, H - 26, 38, 14));
        AddChild(CombatArt.Text("👍 7", 10, TextCol).At(54, H - 26, 46, 14));
        AddChild(CombatArt.Text("✍1", 11, CombatArt.Inkc, HorizontalAlignment.Right).At(10, H - 27, W - 20, 15));
    }

    public override void _Draw()
    {
        DrawStyleBox(CombatArt.Box(new Color(0f, 0f, 0f, 0.45f), null, 9), new Rect2(new Vector2(0, 8), Size));
        DrawStyleBox(CombatArt.Box(CombatArt.Parch, CombatArt.ParchD, 9), new Rect2(Vector2.Zero, Size));
        // 꼬리 구분선 — 태그·비용 줄 위
        DrawLine(new Vector2(10, H - 31), new Vector2(W - 10, H - 31), CombatArt.ParchD, 1f);
        // #태그 칩 — 양피지 위의 밝은 글자는 묻힌다. 전투 카드와 같이 어두운 바탕을 깔아 준다
        DrawStyleBox(CombatArt.Box(new Color("2f2a20"), null, 3), new Rect2(10, H - 27, 38, 16));
    }

    /// <summary>
    /// 이 획들로 다시 그어 보인다. 전역 저장본(<see cref="SignatureStore"/>)은 건드리지 않는다 —
    /// 등록을 누르기 전의 획이 전투에 새어 나가면 안 된다.
    /// </summary>
    public void Play(IReadOnlyList<Vector2[]> strokes)
    {
        _play?.Kill();
        _ink.Preview = strokes;
        _ink.Rebuild();

        // 획이 많을수록 조금 더 오래 — 획순이 보이는 것이 이 미리보기의 전부다
        float dur = Mathf.Clamp(0.45f + 0.22f * Mathf.Max(0, strokes.Count - 1), 0.45f, 1.6f);
        _play = CreateTween().SetLoops();
        _play.TweenProperty(_ink, "Progress", 1f, dur).From(0f);
        _play.TweenInterval(1.1f);
    }
}
