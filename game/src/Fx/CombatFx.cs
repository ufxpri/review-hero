// 판정 연출 — 잉크 튀김·판정 도장·좋아요 숫자·별점 추락·화면 흔들림·헛소리 흑백 플래시.
//
// 웹판 combat.html 의 CSS 키프레임(.splat / .stampfx / .num / .starfall / .shake / .dud)을
// Godot Tween 으로 옮겼다. **수치는 전부 호출자가 준다** — 여기서 판정을 다시 계산하지 않는다.
// 이 레이어는 마우스를 먹지 않는다(MouseFilterEnum.Ignore) — 연출이 조작을 막으면 안 된다.

using Godot;
using ReviewHero.Game.Combat;

namespace ReviewHero.Game.Fx;

public partial class CombatFx : Control
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    // ── 잉크 튀김 ─────────────────────────────────────

    public void Splat(Vector2 at)
    {
        var s = new SplatFx();
        s.Position = at - new Vector2(60, 60);
        s.Size = new Vector2(120, 120);
        s.PivotOffset = new Vector2(60, 60);
        s.Scale = new Vector2(0.2f, 0.2f);
        s.Modulate = new Color(1, 1, 1, 0);
        AddChild(s);

        var t = CreateTween().SetParallel(true);
        t.TweenProperty(s, "scale", new Vector2(1.05f, 1.05f), 0.18f).SetTrans(Tween.TransitionType.Cubic);
        t.TweenProperty(s, "modulate:a", 0.9f, 0.14f);
        t.Chain().TweenProperty(s, "scale", new Vector2(1.35f, 1.35f), 0.32f);
        t.TweenProperty(s, "modulate:a", 0f, 0.32f);
        t.Chain().TweenCallback(Callable.From(s.QueueFree));
    }

    // ── 판정 도장 ─────────────────────────────────────

    /// <summary>도장 4종 + 베스트 리뷰 — 라벨과 색은 호출자(=엔진 결과를 읽은 쪽)가 정한다</summary>
    public StampFx Stamp(string label, Color color, Vector2 at, int fontSize = 32)
    {
        var s = new StampFx(label, color, fontSize);
        AddChild(s);
        s.Position = at - s.Size / 2f;
        s.PivotOffset = s.Size / 2f;
        s.Rotation = Mathf.DegToRad(-24);
        s.Scale = new Vector2(2.6f, 2.6f);
        s.Modulate = new Color(1, 1, 1, 0);

        var t = CreateTween().SetParallel(true);
        t.TweenProperty(s, "scale", new Vector2(0.94f, 0.94f), 0.19f).SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        t.TweenProperty(s, "rotation", Mathf.DegToRad(-11), 0.19f);
        t.TweenProperty(s, "modulate:a", 1f, 0.14f);
        t.Chain().TweenProperty(s, "scale", new Vector2(1.02f, 1.02f), 0.09f);
        t.Chain().TweenInterval(0.42f);
        t.Chain().TweenProperty(s, "modulate:a", 0f, 0.2f);
        t.TweenProperty(s, "scale", new Vector2(1.08f, 1.08f), 0.2f);
        t.Chain().TweenCallback(Callable.From(s.QueueFree));
        return s;
    }

    // ── 좋아요 / 회복 / 골드 숫자 (빨간펜) ────────────

    public void Num(string text, Vector2 at, bool heal = false, bool small = false)
    {
        var col = heal ? new Color("5fbf72") : new Color("e8532f");
        var l = CombatArt.Text(text, small ? 15 : 26, col, HorizontalAlignment.Center);
        l.AddThemeColorOverride("font_outline_color", CombatArt.Inkc);
        l.AddThemeConstantOverride("outline_size", 5);
        AddChild(l);
        var sz = l.GetMinimumSize();
        l.Size = sz;
        l.Position = at - sz / 2f;
        l.PivotOffset = sz / 2f;
        l.Scale = new Vector2(0.5f, 0.5f);
        l.Modulate = new Color(1, 1, 1, 0);

        var t = CreateTween().SetParallel(true);
        t.TweenProperty(l, "scale", new Vector2(1.18f, 1.18f), 0.17f);
        t.TweenProperty(l, "position:y", l.Position.Y - 26, 0.17f);
        t.TweenProperty(l, "modulate:a", 1f, 0.12f);
        t.Chain().TweenProperty(l, "scale", Vector2.One, 0.13f);
        t.Chain().TweenProperty(l, "position:y", l.Position.Y - 78, 0.62f);
        t.TweenProperty(l, "modulate:a", 0f, 0.62f);
        t.Chain().TweenCallback(Callable.From(l.QueueFree));
    }

    // ── 별점 추락 ─────────────────────────────────────

    public void StarFall(Vector2 at)
    {
        var l = CombatArt.Text("★", 22, CombatArt.Gold, HorizontalAlignment.Center);
        AddChild(l);
        var sz = l.GetMinimumSize();
        l.Size = sz;
        l.Position = at - sz / 2f;
        l.PivotOffset = sz / 2f;

        var t = CreateTween().SetParallel(true);
        t.TweenProperty(l, "position:y", l.Position.Y + 120, 1.0f).SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        t.TweenProperty(l, "rotation", Mathf.Pi, 1.0f);
        t.TweenProperty(l, "scale", new Vector2(0.6f, 0.6f), 1.0f);
        t.TweenProperty(l, "modulate:a", 0f, 1.0f);
        t.Chain().TweenCallback(Callable.From(l.QueueFree));
    }

    // ── 화면 흔들림 / 헛소리 흑백 플래시 ──────────────

    /// <summary>적중 시 무대를 흔든다. 설정에서 끄면(Shake=false) 아무 일도 하지 않는다</summary>
    public void Shake(Control target, float power = 6f)
    {
        if (!Embers.MotionAllowed()) return;
        var home = target.Position;
        var t = CreateTween();
        t.TweenProperty(target, "position", home + new Vector2(-power, power * 0.5f), 0.06f);
        t.TweenProperty(target, "position", home + new Vector2(power, -power * 0.5f), 0.06f);
        t.TweenProperty(target, "position", home + new Vector2(-power * 0.55f, -power * 0.35f), 0.06f);
        t.TweenProperty(target, "position", home + new Vector2(power * 0.55f, power * 0.35f), 0.06f);
        t.TweenProperty(target, "position", home, 0.06f);
    }

    /// <summary>
    /// 헛소리·빗나감 — 화면이 잠깐 흑백으로 식는다 (웹판 .dud).
    /// 평소에는 이 판을 숨겨 둔다 — 화면을 통째로 다시 그리는 레이어라 필요할 때만 켠다.
    /// </summary>
    public void Dud(ColorRect? layer)
    {
        if (layer?.Material is not ShaderMaterial mat) return;
        layer.Visible = true;
        var t = CreateTween();
        t.TweenMethod(Callable.From<float>(v => mat.SetShaderParameter("amount", v)), 0f, 0.62f, 0.2f);
        t.TweenMethod(Callable.From<float>(v => mat.SetShaderParameter("amount", v)), 0.62f, 0f, 0.3f);
        t.TweenCallback(Callable.From(() => layer.Visible = false));
    }

    /// <summary>흑백 플래시용 전면 레이어 — 화면을 그대로 읽어 채도만 뺀다</summary>
    public static ColorRect MakeDesaturateLayer()
    {
        var shader = new Shader
        {
            Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float amount = 0.0;
void fragment() {
    vec3 c = texture(screen_tex, SCREEN_UV).rgb;
    float g = dot(c, vec3(0.299, 0.587, 0.114));
    COLOR = vec4(mix(c, vec3(g), amount) * mix(1.0, 0.82, amount), 1.0);
}",
        };
        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("amount", 0f);
        var rect = new ColorRect { Material = mat, MouseFilter = MouseFilterEnum.Ignore };
        rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return rect;
    }
}

/// <summary>잉크 방울 11개 (combat.html splat() 의 결정적 배치 그대로)</summary>
public partial class SplatFx : Control
{
    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        for (int i = 0; i < 11; i++)
        {
            float r = 4f + Mathf.Abs(Mathf.Sin(i * 12.9898f) * 13f);
            float ang = i * 2.399f;
            float rad = 12f + Mathf.Abs(Mathf.Cos(i * 4.1f)) * 42f;
            var c = new Vector2(60 + Mathf.Cos(ang) * rad, 60 + Mathf.Sin(ang) * rad);
            DrawCircle(c, r / 2f, CombatArt.Inkc);
        }
    }
}

/// <summary>판정 도장 — 겹줄 테두리 + 글자</summary>
public partial class StampFx : Control
{
    private readonly string _label;
    private readonly Color _color;
    private readonly int _fontSize;

    public StampFx(string label, Color color, int fontSize)
    {
        _label = label;
        _color = color;
        _fontSize = fontSize;
        MouseFilter = MouseFilterEnum.Ignore;
        var sz = CombatArt.Font().GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
        Size = new Vector2(sz.X + 36, sz.Y + 14);
    }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        // 어두운 무대 위에서도 읽히도록 도장 바닥을 깔고 겹줄 테두리를 두른다
        DrawRect(r, new Color(0.04f, 0.03f, 0.02f, 0.72f));
        DrawRect(r, _color, filled: false, width: 3f);
        DrawRect(r.Grow(-6f), _color, filled: false, width: 2f);

        var font = CombatArt.Font();
        float baseline = Size.Y / 2f + _fontSize * 0.36f;
        var shadow = new Color(0, 0, 0, 0.85f);
        foreach (var d in new[] { new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, -2), new Vector2(0, 2) })
            DrawString(font, new Vector2(0, baseline) + d, _label, HorizontalAlignment.Center, Size.X, _fontSize, shadow);
        DrawString(font, new Vector2(0, baseline), _label, HorizontalAlignment.Center, Size.X, _fontSize, _color);
    }
}
