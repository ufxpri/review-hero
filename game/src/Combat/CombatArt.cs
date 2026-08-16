// 전투 화면의 색·이미지 사전 (ADR-029 3차).
//
// 웹판 ui/game/shared.css 의 CSS 변수와 combat.html 의 ART/SCENE 표를 그대로 옮긴 것이다.
// 색을 코드 곳곳에 흩뿌리지 않기 위해 여기 한 곳에만 둔다 — 미술 방향이 바뀌면 이 파일만 고친다.
//
// 이미지는 `res://assets/` 에 있다. 저장소의 원본은 `ui/assets/`(ComfyUI 산출물이라 .gitignore
// 대상)이고, `game/assets/` 는 그 중 전투가 쓰는 14장만 파일 단위 심볼릭 링크로 걸어 둔 것이다.
// 사본을 만들지 않은 이유: 원본이 커밋되지 않는 생성물이라 게임 쪽에 복사하면 같은 15MB 바이너리가
// 저장소 두 곳에 생긴다. 링크가 끊긴 환경에서도 화면은 살아야 하므로 로드는 전부 폴백을 갖는다.

using Godot;
using ReviewHero.Engine;

namespace ReviewHero.Game.Combat;

public static class CombatArt
{
    // ── 색 (shared.css :root 변수) ─────────────────────
    public static readonly Color Bg = new("12100e");
    public static readonly Color Panel = new(20f / 255f, 18f / 255f, 15f / 255f, 0.88f);
    public static readonly Color Slab = new(9f / 255f, 8f / 255f, 7f / 255f, 0.80f);
    public static readonly Color Edge = new("6b5636");
    public static readonly Color EdgeHi = new("c39a52");
    public static readonly Color Ink = new("efe7d8");
    public static readonly Color Dim = new("9a8f7d");
    public static readonly Color Gold = new("e0a94b");
    public static readonly Color Red = new("c2452e");
    public static readonly Color Green = new("4f9a5e");
    public static readonly Color Parch = new("e8dcc0");
    public static readonly Color ParchD = new("cbb98f");
    public static readonly Color Inkc = new("2b2119");

    // 판정색 (combat.html .st-* / .v-* / .jb-*)
    public static readonly Color JOrigin = new("9a6b12");
    public static readonly Color JFact = new("2f6b3c");
    public static readonly Color JFumble = new("a3301c");
    public static readonly Color JNone = new("7b6a4d");
    public static readonly Color StampOrigin = new("e0a94b");
    public static readonly Color StampFact = new("5fbf72");
    public static readonly Color StampNormal = new("b4a894");
    public static readonly Color StampFumble = new("d4553c");
    public static readonly Color StampMiss = new("8a8272");

    public const int ScreenW = 1344;
    public const int ScreenH = 768;

    /// <summary>손패가 차지하는 아래쪽 높이 (combat.html .hand)</summary>
    public const int HandH = 272;

    // ── 이미지 ────────────────────────────────────────

    private static readonly Dictionary<string, string> EnemyArtFile = new(StringComparer.Ordinal)
    {
        ["E01"] = "enemy-goblin", ["E01T"] = "enemy-goblin",
        ["E02"] = "enemy-orc",
        ["E03"] = "enemy-elf",
        ["E04"] = "enemy-thief",
        ["E05"] = "enemy-knight",
        ["B01"] = "enemy-boss", ["B01T"] = "enemy-boss",
    };

    /// <summary>아트가 없는 적의 자리표시 (combat.html THUMB)</summary>
    private static readonly Dictionary<string, string> EnemyThumb = new(StringComparer.Ordinal)
    {
        ["E01"] = "👺", ["E01T"] = "👺", ["E02"] = "🪓", ["E03"] = "🧝",
        ["E04"] = "🥷", ["E05"] = "💂", ["B01"] = "🕴", ["B01T"] = "🕴",
    };

    /// <summary>무대 배경 — 상대별로 갈아 끼운다 (같은 배경 재탕 방지). 없으면 scene.png</summary>
    private static readonly Dictionary<string, string> SceneByEnemy = new(StringComparer.Ordinal)
    {
        ["E01"] = "market", ["E01T"] = "market",
        ["E02"] = "arcade",
        ["E03"] = "basement",
        ["E04"] = "warehouse",
        ["E05"] = "rest",
        ["B01"] = "boss", ["B01T"] = "boss",
    };

    public static string ThumbOf(string enemyId) =>
        EnemyThumb.TryGetValue(enemyId, out var t) ? t : "📦";

    public static Texture2D? EnemyTexture(string enemyId) =>
        EnemyArtFile.TryGetValue(enemyId, out var f) ? Load($"res://assets/{f}.png") : null;

    public static Texture2D? HeroTexture() => Load("res://assets/hero-back.png");

    public static Texture2D? SceneTexture(EnemyDef enemy)
    {
        string key = SceneByEnemy.TryGetValue(enemy.Id, out var k)
            ? k
            : enemy.Tier switch { EnemyTier.Boss => "boss", EnemyTier.Elite => "basement", _ => "arcade" };
        return Load($"res://assets/scene-{key}.png") ?? Load("res://assets/scene.png");
    }

    /// <summary>없는 파일에 걸려 화면이 통째로 죽지 않게 — 링크가 끊겨도 색 배경으로 굴러간다</summary>
    public static Texture2D? Load(string path)
    {
        if (!ResourceLoader.Exists(path)) return null;
        try { return GD.Load<Texture2D>(path); }
        catch (System.Exception e)
        {
            GD.PushWarning($"[CombatArt] 이미지 로드 실패 {path}: {e.Message}");
            return null;
        }
    }

    // ── 조립 조각 ─────────────────────────────────────

    public static StyleBoxFlat Box(Color bg, Color? border = null, int radius = 6, int width = 1)
    {
        var s = new StyleBoxFlat { BgColor = bg, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };
        if (border is Color b)
        {
            s.BorderColor = b;
            s.BorderWidthLeft = s.BorderWidthTop = s.BorderWidthRight = s.BorderWidthBottom = width;
        }
        return s;
    }

    public static Panel Slabbed(Color bg, Color? border, int radius = 6, int width = 1)
    {
        var p = new Panel();
        p.AddThemeStyleboxOverride("panel", Box(bg, border, radius, width));
        p.MouseFilter = Control.MouseFilterEnum.Ignore;
        return p;
    }

    public static Label Text(string s, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left,
        bool wrap = false)
    {
        var l = new Label { Text = s, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    /// <summary>화면 조립용 — 좌표를 웹 CSS 그대로 쓰기 위해 앵커 대신 절대 배치</summary>
    public static T At<T>(this T c, float x, float y, float w, float h) where T : Control
    {
        c.Position = new Vector2(x, y);
        c.Size = new Vector2(w, h);
        return c;
    }

    public static Font Font()
    {
        var f = ReviewHero.Game.Run.UiTheme.Get().DefaultFont;
        return f ?? ThemeDB.FallbackFont;
    }

    public static string Stars5(double ratio)
    {
        int n = System.Math.Clamp((int)System.Math.Ceiling(ratio * 5), 0, 5);
        return new string('★', n) + new string('☆', 5 - n);
    }
}
