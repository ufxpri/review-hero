// 최소 공통 테마 — 2차의 목표는 「미술 없이 1막이 굴러가는 것」이라 꾸미지 않는다.
// 다만 **한글이 보여야 화면을 읽을 수 있다.** Godot 기본 폰트(Open Sans)에는 한글 글리프가
// 없어 그대로 두면 전부 네모로 나온다. 그래서 시스템 폰트(SystemFont)로 한글 글꼴을 잡아
// 테마 기본 폰트로 끼운다 — 저장소에 폰트 바이너리를 넣지 않고 해결하는 유일한 길이다.
// 미술 단계에서 전용 폰트가 정해지면 이 파일만 바꾸면 된다.

using Godot;

namespace ReviewHero.Game.Run;

public static class UiTheme
{
    private static Theme? _theme;

    /// <summary>플랫폼별 한글 글꼴 후보 — 앞에서부터 있는 것을 쓴다</summary>
    private static readonly string[] KoreanFonts =
    {
        "Apple SD Gothic Neo",   // macOS
        "AppleGothic",
        "Noto Sans CJK KR",      // Linux
        "NanumGothic",
        "Malgun Gothic",         // Windows
        "sans-serif",
    };

    public static Theme Get()
    {
        if (_theme is not null) return _theme;
        var theme = new Theme();
        try
        {
            var font = new SystemFont { FontNames = KoreanFonts };
            theme.DefaultFont = font;
        }
        catch (System.Exception e)
        {
            GD.PushWarning($"[UiTheme] 시스템 폰트를 못 잡았다(한글이 네모로 보일 수 있다): {e.Message}");
        }
        theme.DefaultFontSize = 18;
        _theme = theme;
        return theme;
    }

    /// <summary>씬 루트에 붙이면 자식이 전부 상속받는다</summary>
    public static void Apply(Control root) => root.Theme = Get();

    // ── 화면 조립을 짧게 쓰는 조각들 ────────────────────

    /// <param name="wrap">
    /// 자동 줄바꿈. **HBox 안의 가로줄에는 false 를 줘라** — 켜져 있으면 최소 폭이 한 글자가 되어
    /// HUD 칩이 세로로 눌린다(노드 화면에서 실제로 겪은 증상).
    /// </param>
    public static Label Text(string s, int size = 18, Color? color = null, bool wrap = true)
    {
        var l = new Label
        {
            Text = s,
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
        };
        l.AddThemeFontSizeOverride("font_size", size);
        if (color is { } c) l.AddThemeColorOverride("font_color", c);
        return l;
    }

    public static Button Btn(string s, System.Action? onPressed = null, bool enabled = true, int size = 18)
    {
        var b = new Button { Text = s, Disabled = !enabled };
        b.AddThemeFontSizeOverride("font_size", size);
        if (onPressed is not null) b.Pressed += onPressed;
        return b;
    }

    public static VBoxContainer VBox(int separation = 8)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", separation);
        return v;
    }

    public static HBoxContainer HBox(int separation = 8)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", separation);
        return h;
    }
}
