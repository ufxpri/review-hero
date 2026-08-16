// 판정 말풍선 — 카드를 어떤 상품 위에 올렸을 때, **그 대상 기준**의 판정을 미리 보여준다.
//
// 여기 뜨는 숫자는 전부 Battle.PreviewSubmit 이 준 것이다 (ADR-025). 웹판에서 화면이 판정을
// 다시 계산하다가 밸런스를 고칠 때마다 표시값만 조용히 틀려지는 버그를 겪었고, previewSubmit 은
// 그 버그를 없애려고 만든 것이다. **이 파일에 규칙을 넣지 말 것.**

using Godot;

namespace ReviewHero.Game.Combat;

public partial class VerdictBubble : Control
{
    public const int W = 376;

    private Panel _bg = null!;
    private Label _badge = null!;
    private Label _note = null!;
    private Label _num = null!;
    private BubbleTail _tail = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(W, 80);
        Visible = false;

        _bg = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        var s = CombatArt.Box(CombatArt.Parch, CombatArt.Inkc, 12, 2);
        s.ShadowColor = new Color(0, 0, 0, 0.55f);
        s.ShadowSize = 6;
        s.ShadowOffset = new Vector2(5, 5);
        _bg.AddThemeStyleboxOverride("panel", s);
        _bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_bg);

        _tail = new BubbleTail();
        AddChild(_tail);

        _badge = CombatArt.Text(string.Empty, 16, CombatArt.Inkc, wrap: false);
        AddChild(_badge);

        _note = CombatArt.Text(string.Empty, 12, new Color("4a4136"), wrap: true);
        _note.AddThemeConstantOverride("line_spacing", 3);
        AddChild(_note);

        _num = CombatArt.Text(string.Empty, 13, CombatArt.Inkc, wrap: false);
        AddChild(_num);
    }

    public void HideBubble() => Visible = false;

    /// <summary>존 위에 띄운다. <paramref name="anchor"/> 는 대상 존의 화면 사각형</summary>
    public void ShowFor(Color kind, string badge, string note, string nums, Rect2 anchor)
    {
        const float padX = 13f, padY = 9f;
        float innerW = W - padX * 2f;
        var font = CombatArt.Font();

        _badge.Text = badge;
        _badge.AddThemeColorOverride("font_color", kind);
        _note.Text = note;
        _num.Text = nums;

        float y = padY;
        _badge.At(padX, y, innerW, 20);
        y += 22f;

        var nm = font.GetMultilineStringSize(note, HorizontalAlignment.Left, innerW, 12, -1,
            TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound
            | TextServer.LineBreakFlag.GraphemeBound);
        int lines = Mathf.Max(1, Mathf.RoundToInt(nm.Y / Mathf.Max(1f, font.GetHeight(12))));
        float noteH = lines * (font.GetHeight(12) + 3f);
        _note.At(padX, y, innerW, noteH);
        y += noteH + 4f;

        _num.At(padX, y, innerW, 18);
        y += 18f + padY;

        Size = new Vector2(W, y);
        _tail.Position = new Vector2(30, y - 1);

        // 화면 밖으로 나가지 않게 물린다
        float x = Mathf.Clamp(anchor.Position.X - 24f, 12f, CombatArt.ScreenW - W - 12f);
        float top = Mathf.Max(8f, anchor.Position.Y - y - 18f);
        Position = new Vector2(x, top);
        Visible = true;
    }
}

/// <summary>말풍선 꼬리 — 아래를 가리키는 삼각형</summary>
internal partial class BubbleTail : Control
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(28, 16);
    }

    public override void _Draw()
    {
        var outer = new[] { new Vector2(0, 0), new Vector2(24, 0), new Vector2(6, 16) };
        DrawColoredPolygon(outer, CombatArt.Inkc);
        var inner = new[] { new Vector2(3, -2), new Vector2(19, -2), new Vector2(6.5f, 11) };
        DrawColoredPolygon(inner, CombatArt.Parch);
    }
}
